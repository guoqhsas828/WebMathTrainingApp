//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Curves;
using BaseEntity.Toolkit.Tests.Helpers;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  //[TestFixture]

  [TestFixture("Test_Comac1_Swaps_PCHIP_Smooth")]
  [TestFixture("Test_Comac1_Swaps_Weighted_Const")]
  [TestFixture("TestSwapsBug31758")]
  public class TestSwapPricer : SensitivityTest
  {
    public TestSwapPricer(string name) : base(name) {}

    #region Helpers
    private static SwapLegPricer CreateSwapLegPricer(SwapLeg swapleg,
      Dt asOf, Dt settle, double notional,
      DiscountCurve discountCurve,
      string index, CalibratedCurve referenceCurve, double currentReset)
    {
      ReferenceIndex referenceIndex = (index == null) ? null
        : StandardReferenceIndices.Create(index);
      RateResets resets = new RateResets((IList<RateReset>)null);
      if (!resets.HasAllResets && swapleg.Floating)
      {
        resets = new RateResets(currentReset, Double.NaN);
      }
      asOf = asOf.IsEmpty() ? settle : asOf;
      var p = new SwapLegPricer(swapleg, asOf, settle, notional,
          discountCurve, referenceIndex, referenceCurve,
          resets, null, null);
      p.Validate();
      return p;
    }

    private static SwapPricer[] CreatePricers(
      Dt asOf, Dt  settle,
      string swapDataFile,
      string rateDataFile,
      CurveFitMethod fitMethod,
      InterpScheme interpScheme)
    {
      var rateCurves = new Dictionary<Currency, DiscountCurve>();
      var rateData = RateCurveData.LoadFromCsvFile(rateDataFile);

      var pricers = new List<SwapPricer>();
      using (var reader = new CsvReader(GetTestFilePath(swapDataFile)))
      {
        string[] line;

        // Read the header
        var header = new Dictionary<string, int>();
        while ((line = reader.GetCsvLine()) != null)
        {
          if (line.Length == 0 || String.IsNullOrEmpty(line[0])) continue;
          for (int i = 0; i < line.Length; ++i)
          {
            var name = line[i];
            if (name == null) continue;
            name = name.Replace(" ", "");
            if (name.Length == 0) continue;
            header.Add(name, i);
          }
          break;
        }

        // Read the data and build pricers
        while ((line = reader.GetCsvLine()) != null)
        {
          if (line.Length == 0 || String.IsNullOrEmpty(line[0])) continue;
          var f = new CsvFields(header, line);
          var ccy = f.GetEnum<Currency>("Ccy");
          var tenor = f.GetString("IndexTenor");
          var index = f.GetString("RateIndex");
          if (String.IsNullOrEmpty(index)) index = "LIBOR";
          var curveName = ccy + index;
          index = curveName + '_' + tenor;

          // Get the discount curve
          DiscountCurve discountCurve;
          if(!rateCurves.TryGetValue(ccy, out discountCurve))
          {
            discountCurve = rateData.CalibrateDiscountCurve(
              curveName, asOf, index, index, fitMethod, interpScheme);
            rateCurves.Add(ccy, discountCurve);
          }

          // Get the dates
          Dt effective = f.GetExcelDate("Effective");
          Dt maturity = f.GetExcelDate("Maturity");

          // Construc product
          var id = f.GetString("Id");
          var fixedleg = new SwapLeg(effective, maturity,
            ccy, f.GetDouble("Coupon"),
            f.GetEnum<DayCount>("FixedLegDaycount"),
            f.GetEnum<Frequency>("FixedLegFreq"),
            f.GetEnum<BDConvention>("FixedLegRoll"),
            f.GetCalendar("FixedLegCal"), false)
          {
            Description = id + "_FixedLeg"
          };
          var fixedPricer = CreateSwapLegPricer(fixedleg, asOf, settle,
            f.GetDouble("FixedLegNotional"), discountCurve, null, null, Double.NaN);

          var floatleg = new SwapLeg(effective, maturity,
            f.GetEnum<Frequency>("FloatLegFreq"), 0.0,
            StandardReferenceIndices.Create(index), ccy,
            f.GetEnum<DayCount>("FloatLegDaycount"),
            f.GetEnum<BDConvention>("FloatLegRoll"),
            f.GetCalendar("FloatLegCal"))
          {
            Description = id + "_FloatLeg"
          };
          var floatPricer = CreateSwapLegPricer(floatleg, asOf, settle,
            f.GetDouble("FloatLegNotional"), discountCurve, index,
            discountCurve, f.GetDouble("LatestReset"));

          var pricer = new SwapPricer(floatPricer, fixedPricer);
          pricer.Product.Description = id;
          pricer.Validate();

          pricers.Add(pricer);
        }
      }
      return pricers.ToArray();
    }
    #endregion Helpers

    #region Data and set up
    private string rateDataFile_ = "data/comac1_ir_data.csv";
    private string swapDataFile_ = "data/comac1_swap_blotter.csv";
    private string rateCurveInterp_ = "Weighted";
    private CurveFitMethod rateCurveFitMethod_ = CurveFitMethod.Bootstrap;

    public string RateDataFile { set { rateDataFile_ = value; } }
    public string SwapDataFile { set { swapDataFile_ = value; } }
    public string RateCurveInterp { set { rateCurveInterp_ = value; } }
    public CurveFitMethod RateCurveFitMethod { set { rateCurveFitMethod_ = value; } }

    private SwapPricer[] pricers_;
    private string[] names_;

    [OneTimeSetUp]
    public void SetUp()
    {
      Dt asOf = new Dt(PricingDate != 0 ? PricingDate : 20110609);
      Dt settle = new Dt(SettleDate != 0 ? SettleDate : 20110613);
      var interpScheme = InterpScheme.FromString(
        rateCurveInterp_, ExtrapMethod.Const, ExtrapMethod.Const);
      pricers_ = CreatePricers(asOf, settle, swapDataFile_,
        rateDataFile_, rateCurveFitMethod_, interpScheme);
      names_ = pricers_
        .Select((p) => p.Product.Description)
        .ToArray();
      return;
    }
    #endregion

    #region Tests
    [Test]
    public void Pv()
    {
      TestNumeric(pricers_, names_,
        delegate(object p)
        {
          return ((SwapPricer)p).Pv();
        });
    }

    [Test]
    public void ParCoupon()
    {
      TestNumeric(pricers_, names_,
        delegate(object p)
        {
          return ((SwapPricer)p).ParCoupon();
        });
    }

    [Test]
    public void IR01()
    {
      IR01(pricers_, names_);
    }

    [Test]
    public void Theta()
    {
      Theta(pricers_, names_);
    }
    #endregion
  }
}
