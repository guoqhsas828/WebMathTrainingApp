using System;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Calibrators;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;


namespace BaseEntity.Toolkit.Tests.Sensitivity
{
  [TestFixture]
  public class TestThetaScenario : ToolkitTestBase
  {
    #region Tests

    [TestCase(ThetaFlags.None, "Pv")]
    [TestCase(ThetaFlags.RefitRates, "Pv")]
    [TestCase(ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.None, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates, "CleanPv")]
    [TestCase(ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    public void TestNoteDifferentThetaFlags(ThetaFlags thetaFlag, string measure)
    {
      var targetAsOf = new Dt(20010920);
      var targetSettle = Dt.Add(targetAsOf, 1);
      TestNoteThetaScenario(targetAsOf, targetSettle, thetaFlag, measure);
    }

    private void TestNoteThetaScenario(Dt toAsOf, Dt toSettle, ThetaFlags thetaFlags, string measure)
    {
      var discountCurve = CreateRateCurve(_notePricingDay, "USDLIBOR_3M", _data);
      var notePricer = CreateNotePricer(_notePricingDay, _noteSettleDay, discountCurve);
      var pricers = new IPricer[] {notePricer};

      var scenarioShiftTime = new ScenarioShiftTime(toAsOf, toSettle, thetaFlags);

      DataTable dt;
      using (new CheckStates(true, pricers))
      {
        dt = Scenarios.CalcScenario(pricers, new[] { measure },
          new IScenarioShift[] {scenarioShiftTime}, false, true, null);
      }

      var thetaSen = (measure == "CleanPv")
        ? Sensitivities.Theta(notePricer, "Pv", toAsOf, toSettle,
          thetaFlags | ThetaFlags.Clean, SensitivityRescaleStrikes.No)
        : Sensitivities.Theta(notePricer, "Pv", toAsOf, toSettle,
          thetaFlags, SensitivityRescaleStrikes.No);

      for (int i = 0; i < dt.Rows.Count; i++)
      {
        var row = dt.Rows[i];
        var delta = (double) row["Delta"];
        Assert.AreEqual(thetaSen, delta, 1e-12);
      }
    }

    [TestCase(ThetaFlags.None, "Pv")]
    [TestCase(ThetaFlags.RefitRates, "Pv")]
    [TestCase(ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.None, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates, "CleanPv")]
    [TestCase(ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    public void TestSwapDifferentThetaFlags(ThetaFlags thetaFlag, string measure)
    {
      var targetAsOf = new Dt(20140410);
      var targetSettle = Dt.AddDays(targetAsOf, 2, Calendar.TGT);
      TestSwapThetaScenario(targetAsOf, targetSettle, thetaFlag, measure);
    }

    private void TestSwapThetaScenario(Dt toAsOf, Dt toSettle, ThetaFlags thetaFlags, string measure)
    {
      var payDistCurve = CreateRateCurve(_swapPricingDay, "EONIA", eonia);
      var recDistCurve = payDistCurve;
      var payProjCurve = CreateRateCurve(_swapPricingDay, "EURIBOR_6M", euribor6m);
      var recProjCurve = payDistCurve;

      var payLegPricer = SwapLegTestUtils.GetFloatingSwapPricer(_swapPricingDay, payDistCurve,
        payProjCurve, payProjCurve.ReferenceIndex, 0.0, 10 * 1e-4);

      var recLegPricer = SwapLegTestUtils.GetFloatingSwapPricer(_swapPricingDay, recDistCurve,
        recProjCurve, recProjCurve.ReferenceIndex, 0.002, 10 * 1e-4);

      var swapPricer = new SwapPricer(recLegPricer, payLegPricer);

      var pricers = new IPricer[] {swapPricer};
      
      var scenarioShiftTime = new ScenarioShiftTime(toAsOf, toSettle, thetaFlags);

      DataTable dt;
      using (new CheckStates(true, pricers))
      {
        dt = Scenarios.CalcScenario(pricers, new[] { measure },
          new IScenarioShift[] {scenarioShiftTime}, false, true, null);
      }

      var thetaSen = (measure == "CleanPv")
        ? BaseEntity.Toolkit.Sensitivity.Sensitivities.Theta(swapPricer, "Pv", toAsOf, toSettle,
          thetaFlags | ThetaFlags.Clean, SensitivityRescaleStrikes.No)
        : BaseEntity.Toolkit.Sensitivity.Sensitivities.Theta(swapPricer, "Pv", toAsOf, toSettle,
          thetaFlags, SensitivityRescaleStrikes.No);

      for (int i = 0; i < dt.Rows.Count; i++)
      {
        var row = dt.Rows[i];
        var delta = (double)row["Delta"];
        Assert.AreEqual(thetaSen, delta, 1e-12);
      }
    }

    [TestCase(ThetaFlags.None, "Pv")]
    [TestCase(ThetaFlags.RefitRates, "Pv")]
    [TestCase(ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.None, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates, "CleanPv")]
    [TestCase(ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    public void TestCDSDifferentThetaFlags(ThetaFlags thetaFlag, string measure)
    {
      var targetAsOf = new Dt(20140410);
      var targetSettle = Dt.AddDays(targetAsOf, 3, Calendar.TGT);
      TestCDSThetaScenario(targetAsOf, targetSettle, thetaFlag, measure);
    }


    private void TestCDSThetaScenario(Dt toAsOf, Dt toSettle, ThetaFlags thetaFlags, string measure)
    {

      var cdsPricer = CreateCDSPricer();
      var pricers = new IPricer[] {cdsPricer};

      var scenarioShiftTime = new ScenarioShiftTime(toAsOf, toSettle, thetaFlags);

      DataTable dt;
      using (new CheckStates(true, pricers))
      {
        dt = Scenarios.CalcScenario(pricers, new[]{measure},
          new IScenarioShift[] {scenarioShiftTime}, false, true, null);
      }

      var thetaSen = (measure == "CleanPv")
        ? BaseEntity.Toolkit.Sensitivity.Sensitivities.Theta(cdsPricer, "Pv", toAsOf, toSettle,
          thetaFlags | ThetaFlags.Clean, SensitivityRescaleStrikes.No)
        : BaseEntity.Toolkit.Sensitivity.Sensitivities.Theta(cdsPricer, "Pv", toAsOf, toSettle,
          thetaFlags, SensitivityRescaleStrikes.No);

      for (int i = 0; i < dt.Rows.Count; i++)
      {
        var row = dt.Rows[i];
        var delta = (double)row["Delta"];
        Assert.AreEqual(thetaSen, delta, 1e-12);
      }
    }

    [TestCase(ThetaFlags.Clean)]
    [TestCase(ThetaFlags.Clean | ThetaFlags.RefitRates)]
    public void TestCleanThetaFlag(ThetaFlags thetaFlags)
    {
      Assert.Throws<ArgumentException>(() =>
      {
        var targetAsOf = new Dt(20140410);
        var targetSettle = Dt.AddDays(targetAsOf, 3, Calendar.TGT);
        var scenarioShiftTime = new ScenarioShiftTime(targetAsOf, targetSettle, thetaFlags);
      });
    }



    [TestCase(ThetaFlags.RefitRates, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "Pv")]
    [TestCase(ThetaFlags.RefitRates, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    [TestCase(ThetaFlags.RefitRates | ThetaFlags.Recalibrate | ThetaFlags.IncludeDefaultPayment, "CleanPv")]
    public void TestDualCurveThetaScenario(ThetaFlags thetaFlag, string measure)
    {
      var targetAsOf = new Dt(20140410);
      var targetSettle = Dt.AddDays(targetAsOf, 2, Calendar.TGT);
      TestThetaScenarioDualCurve(targetAsOf, targetSettle, thetaFlag, measure);
    }

    private void TestThetaScenarioDualCurve(Dt toAsOf, Dt toSettle, ThetaFlags thetaFlags, string measure)
    {
      var payProjCurve = new RateCurveBuilder().CreateRateCurves(_swapPricingDay);
      var payDistCurve = ((ProjectionCurveFitCalibrator)payProjCurve.Calibrator).DiscountCurve;
      var recDistCurve = payDistCurve;
      var recProjCurve = payDistCurve;

      var shiftPayProjCurve = new RateCurveBuilder().CreateRateCurves(toAsOf);
      var shiftPayDistCurve = ((ProjectionCurveFitCalibrator)shiftPayProjCurve.Calibrator).DiscountCurve;
      var shiftRecDistCurve = shiftPayDistCurve;
      var shiftRecProjCurve = shiftPayDistCurve;

      var payLeg = new SwapLeg(_swapPricingDay, Dt.Add(_swapPricingDay, "5Y"), Frequency.Quarterly,
        10 * 1e-4, payProjCurve.ReferenceIndex, Currency.EUR, DayCount.Actual360, BDConvention.Modified, Calendar.TGT);
      var recLeg = new SwapLeg(_swapPricingDay, Dt.Add(_swapPricingDay, "5Y"), Frequency.Quarterly,
        10 * 1e-4, recProjCurve.ReferenceIndex, Currency.EUR, DayCount.Actual360, BDConvention.Modified, Calendar.TGT);

      var resets = new RateResets(0.003, 0.0);

      var payLegPricer = new SwapLegPricer(payLeg, _swapPricingDay, _swapPricingDay, 1.0, payDistCurve,
        payProjCurve.ReferenceIndex, payProjCurve, resets, null, null);
      var recLegPricer = new SwapLegPricer(recLeg, _swapPricingDay, _swapPricingDay, 1.0, recDistCurve,
        recProjCurve.ReferenceIndex, recProjCurve, resets, null, null);
      var swapPricer = new SwapPricer(recLegPricer, payLegPricer);

      var shiftPayLegPricer = new SwapLegPricer(payLeg, toAsOf, toSettle, 1.0, shiftPayDistCurve,
        shiftPayProjCurve.ReferenceIndex, shiftPayProjCurve, resets, null, null);
      var shiftRecLegPricer = new SwapLegPricer(recLeg, toAsOf, toSettle, 1.0, shiftRecDistCurve,
        shiftRecProjCurve.ReferenceIndex, shiftRecProjCurve, resets, null, null);
      var shiftSwapPricer = new SwapPricer(shiftRecLegPricer, shiftPayLegPricer);

      var diff = (measure == "CleanPv")
        ? shiftSwapPricer.Pv() - shiftSwapPricer.Accrued() - (swapPricer.Pv() - swapPricer.Accrued())
        : shiftSwapPricer.Pv() - swapPricer.Pv();

      var pricers = new IPricer[] { swapPricer };

      var thetaSen = (measure == "CleanPv")
        ? Sensitivities.Theta(swapPricer, "Pv", toAsOf, toSettle,
          thetaFlags | ThetaFlags.Clean, SensitivityRescaleStrikes.No)
        : Sensitivities.Theta(swapPricer, "Pv", toAsOf, toSettle,
          thetaFlags, SensitivityRescaleStrikes.No);

      Assert.AreEqual(diff, thetaSen, 1e-12);


      var scenarioShiftTime = new ScenarioShiftTime(toAsOf, toSettle, thetaFlags);

      DataTable dt;
      using (new CheckStates(true, pricers))
      {
        dt = Scenarios.CalcScenario(pricers, new []{measure},
          new IScenarioShift[] { scenarioShiftTime }, false, true, null);
      }

      for (int i = 0; i < dt.Rows.Count; i++)
      {
        var row = dt.Rows[i];
        var delta = (double)row["Delta"];
        Assert.AreEqual(diff, delta, 1e-12);
      }
    }

  #endregion Tests

    #region Util Methods
    private NotePricer CreateNotePricer(Dt asOf, Dt settleDay, 
      CalibratedCurve discountCurve)
    {
      var note = new Note(_noteEffectiveDay, _noteMaturityDay, Currency.USD, 0.01,
        DayCount.Actual360, Frequency.None, BDConvention.None, Calendar.None);

      return new NotePricer(note, asOf, settleDay, 1.0,
        (DiscountCurve) discountCurve);
    }


    private CDSCashflowPricer CreateCDSPricer()
    {
      var settle = Dt.AddDays(_cdsPricingDay, 3, Calendar.TGT);
      var discountCurve = CreateRateCurve(_cdsPricingDay, "EONIA", eonia);

      var tenors = new[] {"6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y"};
      var quotes = tenors.Select((t, i) => 200.0 + i*10).ToArray();
      var parameters = SurvivalCurveParameters.GetDefaultParameters();

      var survivalCurve = SurvivalCurve.FitCDSQuotes(
        String.Empty, _cdsPricingDay, settle, Currency.EUR, null, false,
        CDSQuoteType.ParSpread, Double.NaN, parameters, discountCurve,
        tenors, null, quotes, new[] {0.4},
        0, null, null, 0, Double.NaN, null, false);
      survivalCurve.Name = "Survival";

      var cds = new CDS(_cdsPricingDay, _cdsMaturityDay, Currency.EUR, 100/10000.0, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.TGT);

      return new CDSCashflowPricer(cds, _cdsPricingDay, settle, discountCurve,
        survivalCurve, null, 0, 0, TimeUnit.None);
    }


    private DiscountCurve CreateRateCurve(Dt asOf, string name, Data d)
    {
      var types = d.Types;
      var tenors = d.Tenors;
      var quotes = d.Quotes;

      var term = RateCurveTermsUtil.CreateDefaultCurveTerms(name);
      var fitSettings = new CurveFitSettings(asOf)
      {
        CurveSpotCalendar = term.ReferenceIndex.Calendar,
        Method = CashflowCalibrator.CurveFittingMethod.Bootstrap,
        InterpScheme = InterpScheme.FromString("Weighted", ExtrapMethod.Const, ExtrapMethod.Const)
      };

      var calibratorSettings = new CalibratorSettings(fitSettings)
      {
        Tolerance = 1e-14
      };

      return DiscountCurveFitCalibrator.DiscountCurveFit(asOf, term,
        name, quotes, types, tenors, calibratorSettings);
    }


    #endregion Util Methods

    #region Data

    private class Data
    {
      public string[] Tenors;
      public string[] Types;
      public double[] Quotes;
    }

    private static Data _data = new Data
    {
      Tenors = new[] {"1D", "1W", "2W", "1M", "2M", "3M", "1Y", "2Y", "3Y", "4Y"},
      Types = new[] {"MM", "MM", "MM", "MM", "MM", "MM", "Swap", "Swap", "Swap", "Swap"},
      Quotes = new[] {0.00127, 0.00163, 0.00171, 0.0019, 0.00221, 0.0025, 0.0037, 0.0058, 0.0093, 0.0135},
    };

    private static Data eonia = new Data
    {
      Tenors = new[]
      {
        "2W", "1M", "2M", "3M", "6M", "9M", 
        "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "20Y"
      },
      Types = new[]
      {
        "MM", "MM", "MM", "MM", "MM", "MM", 
        "Swap", "Swap", "Swap", "Swap","Swap", "Swap", "Swap", "Swap", "Swap", "Swap"
      },
      Quotes = new[]
      {
        0.00176, 0.00180, 0.00168, 0.00164, 0.00152, 0.00150, 
        0.00181, 0.00294, 0.00445, 0.00618, 0.00800, 0.00979, 0.01146, 0.01301, 0.01443, 0.02139
      },
    };

    private static Data euribor6m = new Data
    {
      Tenors = new[]
      {
        "2W", "1M", "2M", "3M", "6M", "9M",
        "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "20Y"
      },
      Types = new[]
      {
        "MM", "MM", "MM", "MM", "MM", "MM",
        "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap"
      },
      Quotes = new[]
      {
        0.00199, 0.00209, 0.00237, 0.00278, 0.00313, 0.00418,
        0.00477, 0.00610, 0.00787, 0.00972, 0.01158, 0.01335, 0.01498, 0.01647, 0.01781, 0.02228,
      },
    };


    private readonly Dt _noteEffectiveDay = new Dt(20010906);
    private readonly Dt _notePricingDay = new Dt(20010910);
    private readonly Dt _noteSettleDay = new Dt(20010911);
    private readonly Dt _noteMaturityDay = new Dt(20020307);
    private readonly Dt _swapPricingDay =new Dt(20140331);
    private readonly Dt _cdsPricingDay = new Dt(20140331);
    private readonly Dt _cdsMaturityDay = new Dt(20190620);

    #endregion Data
  }
}
