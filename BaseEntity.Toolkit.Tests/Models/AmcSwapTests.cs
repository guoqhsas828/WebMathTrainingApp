//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using static System.Linq.Enumerable;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class AmcSwapTests
  {
    [Test]
    public void EuropeanConsistency()
    {
      CheckEuropeanConsistency(SwapPricer);
    }

    [Test]
    public void BermudanIncreasingProperty()
    {
      var values = Range(1, 10)
        .Select(n => CalculateBermudanValue(SwapPricer, n))
        .ToArray();
      for (int i = 1; i < values.Length; ++i)
        Assert.That(values[i], Is.GreaterThanOrEqualTo(values[i-1]));
    }

    public static double CalculateBermudanValue(SwapPricer pricer, int n)
    {
      double volatility = 0.2;
      Dt asOf = pricer.AsOf, effective = pricer.Swap.Effective;
      var exposureDates = GetExposureDates(pricer)
        .Where(d=>d == asOf || d >= effective).ToArray();
      var callDates = exposureDates.Where(d => d == effective ||
        (d.Year > effective.Year && d.Month == effective.Month &&
          d.Day >= effective.Day && d.Year - effective.Year < n))
        .DistinctBy(d => d.Year).Take(n).ToArray();
      var bermudanPricer = CreateBermudanPricer(
        pricer, volatility, callDates);
      var pv = bermudanPricer.ProductPv();
      Assert.That(pv, Is.GreaterThan(0.0));

      var engine = CreateAmcEngine(new IPricer[] { bermudanPricer },
        pricer.DiscountCurve, exposureDates, volatility);
      engine.Execute();
      var ee0 = CalculatePositiveExposure(engine, asOf);
      var ee1 = CalculatePositiveExposure(engine, effective);
      if (n == 1) Assert.AreEqual(ee0, ee1, 1E-14);
      return ee0;
    }

    public static void CheckEuropeanConsistency(SwapPricer pricer)
    {
      double volatility = 0.2;
      Dt asOf = pricer.AsOf, expiry = pricer.Swap.Effective;
      var bermudanPricer = CreateBermudanPricer(pricer, volatility, expiry);
      var pv = bermudanPricer.ProductPv();
      Assert.That(pv, Is.GreaterThan(0.0));

      var mcPv = CalculateSwapPositiveExposure(pricer, volatility);

      var engine = CreateAmcEngine(new IPricer[] { bermudanPricer },
        pricer.DiscountCurve, GetExposureDates(pricer), volatility);
      engine.Execute();
      var ee0 = CalculatePositiveExposure(engine, asOf);
      var ee1 = CalculatePositiveExposure(engine, expiry);
      Assert.AreEqual(mcPv, ee0, 1E-14);
      Assert.AreEqual(mcPv, ee1, 1E-14);
      return;
    }


    private static SwapBermudanBgmTreePricer CreateBermudanPricer(
      SwapPricer swapPricer, double volatility, params Dt[] exerciseDates)
    {
      var discountCurve = swapPricer.DiscountCurve;
      var referenceCurve = (DiscountCurve)swapPricer.ReferenceCurve;

      // Create the Bermudan pricer
      var swap = swapPricer.Swap.CloneObjectGraph();
      swap.ExerciseSchedule = exerciseDates
        .Select(d => new PutPeriod(d, d, 1, OptionStyle.Bermudan))
        .OfType<IOptionPeriod>().ToArray();
      swap.OptionRight = OptionRight.RightToEnter;
      swap.NotificationDays = 2;
      return new SwapBermudanBgmTreePricer(swap,
        swapPricer.AsOf, swapPricer.Settle, discountCurve, referenceCurve,
        null, new FlatVolatility {Volatility = volatility})
      {
        AmcNoForwardValueProcess = true
      };
    }

    private static double CalculateSwapPositiveExposure(
      SwapPricer pricer, double volatility)
    {
      var engine = CreateAmcEngine(
        new IPricer[] { pricer.PayerSwapPricer, pricer.ReceiverSwapPricer },
        pricer.DiscountCurve, GetExposureDates(pricer), volatility);
      engine.Execute();
      return CalculatePositiveExposure(engine, pricer.Swap.Effective);
    }

    private static double CalculatePositiveExposure(
      ICounterpartyCreditRiskCalculations engine, Dt date)
    {
      return engine.GetMeasure(CCRMeasure.DiscountedEE,
        new Netting(new[] { "All" }, null, null), date, 1);
    }

    private static Dt[] GetExposureDates(SwapPricer pricer)
    {
      var a = new UniqueSequence<Dt>();
      a.Add(CcrPricer.Get(pricer).ExposureDates);
      a.Add(pricer.Product.Effective);
      a.Add(pricer.DiscountCurve.Select(p => p.Date));
      return a.ToArray();
    }

    private static ICounterpartyCreditRiskCalculations CreateAmcEngine(
      IPricer[] pricers, DiscountCurve dc, Dt[] simulDates, double volatility)
    {
      var sample = 1000;
      var asOf = dc.AsOf;
      var idx = dc.ReferenceIndex;
      var curveSettle = dc.Calibrator.Settle;
      var tenorNames = new[]
      {
        "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y",
        "11Y", "12Y", "15Y", "20Y"
      };
      var tenors = Array.ConvertAll(tenorNames, Tenor.Parse);
      var tenorDates = Array.ConvertAll(tenorNames,
        s => Dt.Roll(Dt.Add(curveSettle, s), idx.Roll, idx.Calendar) + 1);

      var factors = new FactorLoadingCollection(new [] {"Factor 1"}, tenors);
      factors.AddFactors(dc, Repeat(new[] {1.0}, tenors.Length)
        .ToArray2D(tenors.Length, 1));

      var volatilities = new VolatilityCollection(tenors);
      volatilities.Add(dc, Enumerable.Repeat(
        new VolatilityCurve(asOf, volatility), tenors.Length)
        .ToArray());

      return BaseEntity.Toolkit.Ccr.Simulations.SimulateCounterpartyCreditRisks(
        MultiStreamRng.Type.MersenneTwister,
        asOf, pricers, Repeat("All", pricers.Length).ToArray(),
        new[] {new SurvivalCurve(asOf, 0)}, new[] {1.0},
        tenorDates, new[] {dc},
        null, null, null, volatilities, factors, -100.0, sample,
        simulDates, Tenor.Empty, false);
    }

    public static DiscountCurve GetDiscountCurve(
      string name, Dt asOf,
      ReferenceIndex index, double[,] data,
      DiscountCurve dc = null)
    {
      var calibrator = dc != null
        ? (DiscountCalibrator) new ProjectionCurveFitCalibrator(asOf,
          dc, index, null, new CalibratorSettings())
        : new DiscountCurveFitCalibrator(asOf,
          new CalibratorSettings(), new[] {index});
      var curve = dc != null
        ? new DiscountCurve(calibrator, dc)
        : new DiscountCurve(calibrator);
      curve.Interp = InterpScheme.FromString(dc == null
        ? "Linear; Const" : "Weighted; Const",
        ExtrapMethod.None, ExtrapMethod.None)
        .ToInterp();
      curve.Name = name;
      curve.Ccy = index.Currency;
      curve.ReferenceIndex = index;

      for (int i = 0, n = data.GetLength(0); i < n; ++i)
      {
        var date = new Dt((int) data[i, 0]);
        var val = data[i, 1];
        curve.Add(date, val);
      }
      return curve;
    }

    #region Constructor and Data

    public AmcSwapTests()
    {
      double fixedRate = 0.03, notional = 1000000;
      var calendar = Calendar.NYB;
      var index = new InterestRateIndex("USDLIBOR_3M", Frequency.Quarterly,
        Currency.USD, DayCount.Actual360, Calendar.NYB, 2);
      var fixedLeg = new SwapLeg(_effective, _maturity, index.Currency,
        fixedRate, DayCount.Thirty360, Frequency.SemiAnnual,
        BDConvention.Modified, calendar, true)
      {
        Description = "10Y USD 2%"
      };
      var floatLeg = new SwapLeg(_effective, _maturity,
        Frequency.SemiAnnual, 0.0, index)
      {
        AccrueOnCycle = true,
        Calendar = Calendar.NYB,
        Description = "10Y USDLIBOR_3M+0"
      };

      var discountCurve = GetDiscountCurve("FEDFUNDS_1D_Curve",
        _asOf, index, _discountCurveData);
      var referenceCurve = GetDiscountCurve("USDLIBOR_3M_Curve",
        _asOf, index, _projectCurveData, discountCurve);

      var fixedLegPricer = new SwapLegPricer(fixedLeg, _asOf, _settle,
        1, discountCurve, null, null, null, null, null);
      var floatLegPricer = new SwapLegPricer(floatLeg, _asOf, _settle,
        -1, discountCurve, index, referenceCurve, null, null, null);
      SwapPricer = new SwapPricer(fixedLegPricer, floatLegPricer);
    }


    private SwapPricer SwapPricer { get; }

    Dt _effective = new Dt(20221121),
      _maturity = new Dt(20321121),
      _asOf = new Dt(20121119),
      _settle = new Dt(20121121);

    private double[,] _discountCurveData =
    {
      {20131122, 0.998830606945946},
      {20141122, 0.995812581848916},
      {20151124, 0.989108823991398},
      {20161122, 0.975003502896219},
      {20171122, 0.951676740201091},
      {20181122, 0.92605609611825},
      {20191122, 0.89729343410241},
      {20201124, 0.867629071376187},
      {20211123, 0.838684316138307},
      {20221122, 0.81001024086358},
      {20241122, 0.753160068722225},
      {20271123, 0.676822919292769},
      {20321123, 0.567803309001357},
      {20371124, 0.470378043106209},
      {20421122, 0.398095721873296},
    };

    private double[,] _projectCurveData =
    {
      {20131121, 0.997151627483838},
      {20141121, 0.994326133864161},
      {20151123, 0.991505234344488},
      {20161122, 0.988722656727498},
      {20171121, 0.988560080308462},
      {20181121, 0.986372216685588},
      {20191121, 0.985324324986945},
      {20201123, 0.984617944352182},
      {20211123, 0.983092965331508},
      {20221122, 0.981307486685526},
      {20231121, 0.981037261289992},
      {20241121, 0.977702059375281},
      {20271123, 0.972382768487731},
      {20321123, 0.963541808962739},
      {20371123, 0.974566631662226},
      {20421121, 0.96837188849713},
      {20521121, 0.96309574302489},
      {20621121, 1.10766129401921},
    };

    #endregion
  }
}
