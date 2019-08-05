//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Curves;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("RoundTrip_BasisSwaps_Basis_Vs_Libor")]
  public class TestBasisSwap : SensitivityTest
  {
    public TestBasisSwap(string name) : base(name)
    {}

    #region Helpers
    private static SwapLegPricer CreateSwapLegPricer(SwapLeg swapleg,
      Dt asOf, Dt settle, double notional,
      DiscountCurve discountCurve,
      ReferenceIndex referenceIndex, CalibratedCurve referenceCurve, double currentReset)
    {
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
      var referenceCurves = new Dictionary<string, DiscountCurve>();
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
          var tenor = f.GetString("PayLegTenor");
          var payRateIndex = f.GetString("PayRateIndex");
          if (String.IsNullOrEmpty(payRateIndex)) payRateIndex = "LIBOR";
          payRateIndex = ccy + payRateIndex + '_' + tenor;

          var rcvRateIndex = f.GetString("RcvRateIndex");
          var rcvTenor = f.GetString("RcvIndexTenor");
          if (String.IsNullOrEmpty(rcvRateIndex)) rcvRateIndex = "LIBOR";
          rcvRateIndex = ccy + rcvRateIndex + '_' + rcvTenor;
          var rcvRefIndex = StandardReferenceIndices.Create(rcvRateIndex);


          var discountCurveName = rcvRateIndex;

          // Get the discount curve
          DiscountCurve discountCurve;
          if (!rateCurves.TryGetValue(ccy, out discountCurve))
          {
            discountCurve = rateData.CalibrateDiscountCurve(
              discountCurveName, asOf, discountCurveName, discountCurveName, fitMethod, interpScheme);
            rateCurves.Add(ccy, discountCurve);
          }

          // Get the dates
          Dt effective = f.GetExcelDate("Effective");
          Dt maturity = f.GetExcelDate("Maturity");
          // ReferenceIndex 
          var payRefIndex = new InterestRateIndex(payRateIndex, Tenor.Parse(tenor), ccy,
                                                  f.GetEnum<DayCount>("PayLegDaycount"), f.GetCalendar("PayLegCal"),
                                                  f.GetEnum<BDConvention>("PayLegRoll"),
                                                  Convert.ToInt32(f.GetDouble("PayLegResetLag")));

          // Construc product
          var id = f.GetString("Id");
          var payLeg = new SwapLeg(effective, maturity,
            f.GetEnum<Frequency>("PayLegFreq"), f.GetDouble("PaySpread")/10000.0,
            payRefIndex, ccy, payRefIndex.DayCount, payRefIndex.Roll,
            payRefIndex.Calendar)
          {
            Description = id + "_BasisTgtLeg",
            ProjectionType = f.GetEnum<ProjectionType>("PayLegProjectionType"),
            CompoundingConvention =
              f.GetEnum<CompoundingConvention>("PayLegCompoundingConvention"),
            AccrueOnCycle = false,
            ResetLag = new Tenor(payRefIndex.SettlementDays, TimeUnit.Days),
            InArrears = Boolean.Parse(f.GetString("PayLegInArrears"))
          };


          DiscountCurve payReferenceCurve;

          var projectionTerms = new CurveTerms(payRateIndex, ccy, payRefIndex, new AssetCurveTerm[]
                                                                                   {
                                                                                     new BasisSwapAssetCurveTerm(
                                                                                       payRefIndex.SettlementDays, payLeg.Calendar, payLeg.ProjectionType,
                                                                                       payLeg.Freq, Frequency.None, CompoundingConvention.None, null,
                                                                                       ProjectionType.None, Frequency.None, Frequency.None,
                                                                                       CompoundingConvention.None, null, true)
                                                                                   });
          if (!referenceCurves.TryGetValue(payRateIndex, out payReferenceCurve))
          {
            payReferenceCurve = rateData.CalibrateBasisProjectionCurve(payRateIndex, asOf, rcvRateIndex, projectionTerms,
                                                                       fitMethod, interpScheme, discountCurve, discountCurve, payRefIndex, rcvRefIndex);
            referenceCurves.Add(payRateIndex, payReferenceCurve);
          }

          var payLegPricer = CreateSwapLegPricer(payLeg, asOf, settle,
            f.GetDouble("PayLegNotional"), discountCurve, payRefIndex, payReferenceCurve, Double.NaN);
          MatchProducts(payLegPricer, ((Swap)payReferenceCurve.Tenors[1].Product).ReceiverLeg);

          var rcvLeg = new SwapLeg(effective, maturity,
            f.GetEnum<Frequency>("RcvLegFreq"), f.GetDouble("RcvSpread") / 10000.0,
            StandardReferenceIndices.Create(rcvRateIndex), ccy,
            f.GetEnum<DayCount>("RcvLegDaycount"),
            f.GetEnum<BDConvention>("RcvLegRoll"),
            f.GetCalendar("RcvLegCal"))
          {
            Description = id + "_LiborLeg",
            ProjectionType = f.GetEnum<ProjectionType>("RcvLegProjectionType"),
            CompoundingConvention = f.GetEnum<CompoundingConvention>("RcvLegCompoundingConvention"),
            AccrueOnCycle = false,
            ResetLag = new Tenor(payRefIndex.SettlementDays, TimeUnit.Days),
          };
          var projectionCurves = ((ProjectionCurveFitCalibrator)payReferenceCurve.Calibrator).ProjectionCurves;
          var rcvReferenceCurve = (projectionCurves == null || projectionCurves.Count == 0) ? null : (DiscountCurve)projectionCurves.First();
          var rcvPricer = CreateSwapLegPricer(rcvLeg, asOf, settle,
            f.GetDouble("RcvLegNotional"), discountCurve, rcvRefIndex,
            rcvReferenceCurve, Double.NaN);
          MatchProducts(rcvPricer, ((Swap)payReferenceCurve.Tenors[1].Product).PayerLeg);

          var pricer = new SwapPricer(rcvPricer, payLegPricer);
          pricer.Product.Description = id;
          pricer.Validate();

          pricers.Add(pricer);
        }
      }
      return pricers.ToArray();
    }

    [Conditional("DEBUG")]
    static void MatchProducts(SwapLegPricer pricer, SwapLeg target)
    {
      pricer = (SwapLegPricer) pricer.ShallowCopy();
      pricer.Product = (SwapLeg)pricer.SwapLeg.ShallowCopy();
      var swapLeg = pricer.SwapLeg;
      var name = swapLeg.Description ?? target.Description;
      swapLeg.Description = target.Description;
      swapLeg.CycleRule = target.CycleRule;
      swapLeg.Maturity = Dt.Roll(swapLeg.Maturity,
        swapLeg.BDConvention, swapLeg.Calendar);
      pricer.Pv(); // fill all the intermediate fields.
      var mismatch = ObjectStatesChecker.Compare(swapLeg, target);
      if (mismatch != null)
      {
        var s = mismatch.ToString();
        Console.WriteLine("{0}: {1}", name, s);
      }
    }
    #endregion Helpers

    #region Data and set up
    private string rateDataFile_ = "data/comac1_ir_data.csv";
    private string swapDataFile_ = "data/basis_swap_blotter.csv";
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
    public void RateSensitivity()
    {
      Rate(pricers_);
    }

    [Test]
    public void DiscountSensitivity()
    {
      Discount(pricers_);
    }
    #endregion
  }
}
