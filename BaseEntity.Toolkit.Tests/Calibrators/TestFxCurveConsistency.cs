// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Curves;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture("Forward Quotes only")]
  [TestFixture("FX Forward Curve")]
  [TestFixture("Mixed Forward and Basis Quotes")]
  public class TestFxCurveConsistency : ToolkitTestBase
  {
    public TestFxCurveConsistency(string name): base(name)
    {}

    #region Data
    private string basisCalibrationTenor_ = "3M";
    private InterpScheme basisInterp_ = new InterpScheme();
    private CurveFitMethod basisFitMethod_ = CurveFitMethod.Bootstrap;

    private string rateTenor_ = "3M";
    private string rateDataFile_ = "data/comac1_ir_data.csv";
    private InterpScheme rateCurveInterp_ = new InterpScheme();
    private CurveFitMethod rateCurveFitMethod_ = CurveFitMethod.Bootstrap;

    //
    // Computed itermediate results
    private Dt asOf_, settle_;
    private DiscountCurve domesticDiscount_, foreignDiscount_;
    private ReferenceIndex domesticIndex_, foreignIndex_;
    private SwapPricer[] pricers_;
    #endregion Data

    #region Inputs through Properties
    [Flags] public enum TestSpec
    {
      None,
      ForwardCurveOnly = 1,
      ExpectException = 2,
    }

    public TestSpec TestWhat { get; set; }
    private bool ForwardCurveOnly => (TestWhat & TestSpec.ForwardCurveOnly) != 0;
    private bool ExpectException => (TestWhat & TestSpec.ExpectException) != 0;

    public double FxSpotRate { get; set; }
    public int FxSpotDays { get; set; }
    public Currency FromCcy { get; set; }
    public Currency ToCcy { get; set; }

    public string[] FxFwdTenors { get; set; }
    public double[] FxFwdQuotes { get; set; }
    public Calendar FxCalendar { get; set; }

    public string[] FxBasisTenors { get; set; }
    public double[] FxBasisQuotes { get; set; }
    public Calendar FxBasisCalendar { get; set; }

    #endregion

    #region Helpers
    private DiscountCurve GetDiscountCurve(RateCurveData rateData,
      Currency ccy, Dt asOf)
    {
      var fitMethod = rateCurveFitMethod_;
      var interpScheme = rateCurveInterp_;
      var curveName = String.Format("{0}_Disc_Curve", ccy);
      var index = ccy + "LIBOR_" + rateTenor_;
      return rateData.CalibrateDiscountCurve(
        curveName, asOf, index, index, fitMethod, interpScheme, InstrumentType.MM, InstrumentType.Swap,
        InstrumentType.FUT);
    }

    private ReferenceIndex GetProjectIndex(Currency ccy)
    {
      var index = ccy + "LIBOR_" + basisCalibrationTenor_;
      var term = RateCurveTermsUtil.CreateDefaultCurveTerms(index);
      return term.ReferenceIndex;
    }

    private static SwapPricer CreateSwapPricer(Dt asOf,
      SwapLeg payerLeg, SwapLeg receiverLeg,
      DiscountCurve domesticDiscount,
      DiscountCurve foreignDiscount,
      ReferenceIndex domesticIndex,
      ReferenceIndex foreignIndex,
      FxCurve fxCurve,
      double fxSpotRate)
    {
      var domesticProjection = domesticDiscount;
      var foreignProjection = foreignDiscount;
      var payerPricer = new SwapLegPricer(payerLeg, asOf, asOf, -fxSpotRate,
        domesticDiscount, domesticIndex, domesticProjection, null, null, null);
      var receiverPricer = new SwapLegPricer(receiverLeg, asOf, asOf, 1.0,
        domesticDiscount, foreignIndex, foreignProjection, null, null, fxCurve);
      var pricer = new SwapPricer(receiverPricer, payerPricer);
      pricer.Validate();
      return pricer;
    }

    private void CalibrateIrCurves()
    {
      PricingDate = 20110916;
      SettleDate = 20110920;

      var rateData = RateCurveData.LoadFromCsvFile(rateDataFile_);
      asOf_ = PricingDate != 0 ? new Dt(PricingDate) : Dt.Today();
      settle_ = SettleDate != 0 ? new Dt(SettleDate) : Dt.Add(asOf_, 1);
      domesticDiscount_ = GetDiscountCurve(rateData, ToCcy, asOf_);
      domesticIndex_ = GetProjectIndex(ToCcy);
      foreignDiscount_ = GetDiscountCurve(rateData, FromCcy, asOf_);
      foreignIndex_ = GetProjectIndex(FromCcy);
    }

    private FxCurve CalibrateFxCurve()
    {
      if (domesticDiscount_ == null) CalibrateIrCurves();

      var name = String.Format("{0}/{1}_Curve", FromCcy, ToCcy);
      var settings = new CurveFitSettings(domesticDiscount_.AsOf)
      {
        InterpScheme = basisInterp_,
        Method = basisFitMethod_
      };
      var fxRate = new FxRate(asOf_, FxSpotDays, FromCcy, ToCcy,
        FxSpotRate, foreignIndex_.Calendar, domesticIndex_.Calendar);
      var basisTenors = FxBasisTenors;
      var basisDates = basisTenors == null
        ? null
        : basisTenors.Select(t => Dt.Roll(Dt.Add(fxRate.Spot,Tenor.Parse(t)),
          BDConvention.Following, FxBasisCalendar)).ToArray();
      var basisQuotes = FxBasisQuotes;
      var ccy1dc = foreignDiscount_;
      var ccy2dc = domesticDiscount_;
      if (ForwardCurveOnly)
      {
        if (!ExpectException)
        {
          basisDates = null;
          basisTenors = null;
          basisQuotes = null;
        }
        ccy1dc = ccy2dc = null;
      }
      var fxCurve = FxCurve.Create(asOf_, fxRate.Spot,
        fxRate.FromCcy, fxRate.ToCcy, fxRate.Rate, FxFwdTenors,
        FxFwdTenors.Select(t => Dt.Roll(Dt.Add(fxRate.Spot, Tenor.Parse(t)),
          BDConvention.Following, FxCalendar)).ToArray(), FxFwdQuotes,
        basisTenors, basisDates, basisQuotes, FxBasisCalendar,
        BasisSwapSide.Ccy1, ccy1dc, ccy2dc, settings, name);
      return fxCurve;
    }

    private SwapPricer[] CalibrateFxBasisSwapPricers()
    {
      var fxCurve = CalibrateFxCurve();
      if(fxCurve.BasisCurve==null) return EmptyArray<SwapPricer>.Instance;
      var list = new List<SwapPricer>();
      foreach (CurveTenor tenor in fxCurve.BasisCurve.Tenors)
      {
        var swap = tenor.Product as Swap;
        if (swap == null) continue;
        swap = (Swap)swap.Clone();
        var pricer = CreateSwapPricer(asOf_, swap.PayerLeg, swap.ReceiverLeg,
          domesticDiscount_, foreignDiscount_, domesticIndex_, foreignIndex_,
          fxCurve, fxCurve.SpotRate);
        pricer.Product.Description = swap.Description;
        list.Add(pricer);
        swap = (Swap)swap.Clone();
        pricer = CreateSwapPricer(asOf_, swap.ReceiverLeg, swap.PayerLeg,
          foreignDiscount_, domesticDiscount_, foreignIndex_, domesticIndex_,
          fxCurve, 1 / fxCurve.SpotRate);
        pricer.Product.Description = "Inverse." + swap.Description;
        list.Add(pricer);
      }
      return list.ToArray();
    }

    #endregion Helpers

    #region Pv and Rate01
    /// <summary>
    /// Sets up the basis swap pricers.
    /// </summary>
    [OneTimeSetUp]
    public void SetUp()
    {
      pricers_ = CalibrateFxBasisSwapPricers();
      return;
    }

    /// <summary>
    /// The pv of basis swap should be zero.
    /// </summary>
    [Test]
    public void Pv()
    {
      for (int i = 0; i < pricers_.Length; ++i)
      {
        var pricer = pricers_[i];
        var pv = pricer.Pv();
        Assert.AreEqual(0.0, pv, 5E-11, pricer.Product.Description + ".Pv");
      }
      return;
    }

    /// <summary>
    ///  The Rate01 of basis swap should be zero, since the basis are recalibrated.
    /// </summary>
    [Test]
    public void Rate01()
    {
      if (pricers_.Length == 0) return;
      var s = Sensitivities.Rate01(pricers_, null, 1, 1,
        SensitivityFlag.ForcePrincipalExcahnge, null, null, null);
      if (s.Length != pricers_.Length)
      {
        Assert.AreEqual(pricers_.Length, s.Length, "Rate01.Length");
      }
      for (int i = 0; i < pricers_.Length; ++i)
      {
        var pricer = pricers_[i];
        Assert.AreEqual(0.0, s[i], 1E-9, pricer.Product.Description + ".Rate01");
      }
      return;
    }
    #endregion

    [Test]
    public void TestSpotDaysConsistency()
    {
      var fxCurve = CalibrateFxCurve();
      Assert.AreEqual(fxCurve.SpotDays, fxCurve.SpotFxRate.SettleDays);

      var fxRate = new FxRate(asOf_, 2, FromCcy, ToCcy,
        FxSpotRate, foreignIndex_.Calendar, domesticIndex_.Calendar);

      var fxCurve1 = FxCurve.Create(fxRate, null, null,null, 
         null, null, null, Calendar.None, BasisSwapSide.Ccy2, 
         null, null, null, null);

     Assert.AreEqual(fxCurve1.SpotDays, fxRate.SettleDays);

    }

    #region Fx Sensitivity Consistency

    [Test]
    public void FxSensitivity()
    {
      // Create product
      var nearValueDate = new Dt(20110613);
      var nearReceiveCcy = ToCcy;
      var nearPayCcy = FromCcy;
      var nearFxRate = 0.01268;
      var farValueDate = new Dt(20120104);
      var farFxRate = 0.0129;
      var fxSwap = new FxSwap(nearValueDate, nearReceiveCcy, nearPayCcy,
                        nearFxRate, farValueDate, farFxRate)
      {
        NotionalType = FxSwapNotionalType.NearReceiveEqualsFarPay,
        Description = "JPYUSD Jun-11 0.01268 - Jan-12 0.0129 (3)"
      };

      // The base value
      var pricer = CreateFxSwapPricer(fxSwap);
      pricer.PricerFlags |= PricerFlags.SensitivityToAllRateTenors;
      var mtm = pricer.Pv();

      // Build the manually calculated sensitivity table
      var expects = new Dictionary<string, double>
      {
        {"SpotFx", CalculateSpotDelta(fxSwap, mtm)}
      };
      for (int i = 0; i < FxFwdTenors.Length; ++i)
      {
        var name = "FxForward." + FxFwdTenors[i];
        var val = CalculateBumpedPv(fxSwap, ref FxFwdQuotes[i]);
        expects.Add(name, val - mtm);
      }

      // Call Fx Sensitivity Function
      var table = Sensitivities.FxCurve(new IPricer[] { pricer }, null,
        0, 0.01, 0, true, false, BumpType.ByTenor, null, false,
        false, null, false, SensitivityMethod.FiniteDifference, null);

      // Compare the results
      int rows = table.Rows.Count;
      Assert.AreEqual(expects.Count, rows, "Count");
      for (int i = 0; i < rows; ++i)
      {
        var row = table.Rows[i];
        var curve = (string)row["Element"];
        var tenor = (string)row["Curve Tenor"];
        if (!curve.EndsWith("_BasisCurve") && tenor != "SpotFx")
          continue;
        var delta = (double)row["Delta"];
        Assert.AreEqual(expects[tenor], delta, 1E-2, tenor);
      }

      var curveShift = new ScenarioShiftCurves(new[] {domesticDiscount_}, 
        new []{0.2},ScenarioShiftType.Relative, true );
      var fxCurveShift = new ScenarioShiftFxCurves( pricer.FxCurves,
        new []{0.2}, ScenarioShiftType.Relative, new []{0.1}, ScenarioShiftType.Relative);
      var dt = Scenarios.CalcScenario(new[] {pricer}, new[] {"Pv"},
        new IScenarioShift[] {curveShift, fxCurveShift}, false, true, null);
      var baseValue = (double)(dt.Rows[0])["Base"];
      var pv = pricer.Pv();
      Assert.AreEqual(pv, baseValue, 10000.0*1E-14);
      return;
    }

    private double CalculateSpotDelta(FxSwap fxSwap, double mtm)
    {
      var savedSpot = FxSpotRate;
      var savedFwQuotes = FxFwdQuotes;
      try
      {
        // For SpotFx delta, all the forward quotes bumped proportinally.
        FxSpotRate *= 1.01;
        FxFwdQuotes = FxFwdQuotes.Select(v => v * 1.01).ToArray();
        var pricer = CreateFxSwapPricer(fxSwap);
        return pricer.Pv() - mtm;
      }
      finally
      {
        FxSpotRate = savedSpot;
        FxFwdQuotes = savedFwQuotes;
      }
    }
    private double CalculateBumpedPv(FxSwap fxSwap, ref double input)
    {
      double saved = input;
      try
      {
        input *= 1.01;
        var pricer = CreateFxSwapPricer(fxSwap);
        return pricer.Pv();
      }
      finally
      {
        input = saved;
      }
    }

    private FxSwapPricer CreateFxSwapPricer(FxSwap fxSwap)
    {
      var fxCurve = CalibrateFxCurve();
      var notional = 800 * 1000000;
      var valuationCcy = Currency.USD;
      var discountCurve = foreignDiscount_; //fxCurve.Ccy1DiscountCurve;
      var receiveCcyFxCurve = fxCurve;
      var pricer = new FxSwapPricer(fxSwap, asOf_, settle_, notional,
        valuationCcy, discountCurve, receiveCcyFxCurve, null);
      pricer.Validate();
      return pricer;
    }
    #endregion
  }
}
