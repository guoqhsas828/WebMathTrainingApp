//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Linq;
using static System.Linq.Enumerable;
using static BaseEntity.Toolkit.Base.RateCalc;

using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class AmcCapletTests
  {
    /// <summary>
    /// Simples the specified index tenor.
    /// </summary>
    /// <param name="indexTenor">The index tenor.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="sample">The sample.</param>
    [TestCase("1Y", 0, 1000)]
    [TestCase("1Y", -0.02, 1000)]
    [TestCase("1Y", 0.02, 1000)]
    [TestCase("6M", 0, 1000)]
    [TestCase("6M", -0.02, 1000)]
    [TestCase("6M", 0.02, 1000)]
    [TestCase("3M", 0, 5000)]
    [TestCase("3M", -0.02, 5000)]
    [TestCase("3M", 0.02, 5000)]
    [TestCase("1M", 0, 20000)]
    [TestCase("1M", -0.02, 20000)]
    [TestCase("1M", 0.02, 20000)]
    public void Simple(string indexTenor, double spread, int sample)
    {
      var index = new InterestRateIndex("Dummy", Tenor.Parse(indexTenor),
        Currency.USD, DayCount.Actual365Fixed, Calendar.None,
        BDConvention.None, 0);


      Dt asOf = new Dt(20160323), 
        expiry = Dt.Roll(Dt.Add(asOf, "1Y"), index.Roll, index.Calendar),
        maturity = Dt.Roll(Dt.Add(expiry, "1Y"), index.Roll, index.Calendar);

      double rate = 0.03;
      foreach (var sigma in new[] {0.1, 0.2, 0.4, 0.8, 1.0, 1.5, 2.0, 4.0})
      {
        var d = sigma;
        foreach (var strike in new[] {rate/(1 + d), rate, rate*(1 + d)})
        {
          var swapPricer = CreateSwapPricer(asOf, 
            expiry, maturity, index, rate, strike);
          var swapPv = swapPricer.ProductPv();

          var bermudanPricer = CreateBermudanPricer(
            swapPricer, sigma, expiry);
          var optPv = bermudanPricer.ProductPv();

          //var sample = baseSample*Math.Max(1, (int) (sigma*sigma));
          var engine = CreateMcEngine(new[] {swapPricer},
            swapPricer.DiscountCurve, sigma, sample);
          engine.Execute();
          var epv1 = CalculateExpectedPv(engine, expiry);
          Assert.AreEqual(swapPv, epv1, 5E-3*sigma, "EPV expiry w sigma=" + sigma);

          var epv0 = CalculateExpectedPv(engine, asOf);
          Assert.AreEqual(epv1, epv0, 1E-14, "EPV as-of vs expiry");

          var ee1 = CalculatePositiveExposure(engine, expiry);
          Assert.AreEqual(optPv, ee1, 5E-2*optPv, "Plain EE expiry");

          engine = CreateMcEngine(new[] { bermudanPricer },
            swapPricer.DiscountCurve, sigma, sample);
          engine.Execute();
          var ee0 = CalculatePositiveExposure(engine, asOf);
          Assert.AreEqual(ee1, ee0, 1E-14, "AMC EE as-of");
        }

      }

      return;
    }

    private static double CalculateFixedLegPv(SwapPricer swapPricer)
    {
      var pricer = swapPricer.PayerSwapPricer.SwapLeg.Floating
        ? swapPricer.ReceiverSwapPricer : swapPricer.PayerSwapPricer;
      return Math.Abs(pricer.ProductPv());
    }

    private static double CalculatePositiveExposure(
      ICounterpartyCreditRiskCalculations engine, Dt date)
    {
      return engine.GetMeasure(CCRMeasure.DiscountedEE,
        new Netting(new[] { "All" }, null, null), date, 1);
    }

    private static double CalculateExpectedPv(
      ICounterpartyCreditRiskCalculations engine, Dt date)
    {
      return engine.GetMeasure(CCRMeasure.DiscountedEPV,
        new Netting(new[] { "All" }, null, null), date, 1);
    }

    private static ICounterpartyCreditRiskCalculations CreateMcEngine(
      IPricer[] pricers, DiscountCurve dc, double volatility, int sample)
    {
      var asOf = dc.AsOf;
      var tenorDates = dc.Select(p => p.Date).ToArray();
      var simulDates = tenorDates;
      var tenors = Array.ConvertAll(tenorDates, d => Tenor.FromDateInterval(asOf, d));

      var factors = new FactorLoadingCollection(new[] {"Factor 1"}, tenors);
      factors.AddFactors(dc, Repeat(new[] {1.0}, tenors.Length)
        .ToArray2D(tenors.Length, 1));

      var volatilities = new VolatilityCollection(tenors);
      volatilities.Add(dc, Repeat(new VolatilityCurve(
        asOf, volatility), tenors.Length).ToArray());

      return BaseEntity.Toolkit.Ccr.Simulations.SimulateCounterpartyCreditRisks(
        MultiStreamRng.Type.MersenneTwister,
        asOf, pricers, Repeat("All", pricers.Length).ToArray(),
        new[] {new SurvivalCurve(asOf, 0)}, new[] {1.0},
        tenorDates, new[] {dc},
        null, null, null, volatilities, factors, -100.0, sample,
        simulDates, Tenor.Empty, false);
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
      swap.NotificationDays = 0;
      return new SwapBermudanBgmTreePricer(swap,
        swapPricer.AsOf, swapPricer.Settle, discountCurve, referenceCurve,
        null, new FlatVolatility { Volatility = volatility })
      {
        AmcNoForwardValueProcess = true
      };
    }


    private static SwapPricer CreateSwapPricer(
      Dt asOf, Dt effective, Dt maturity,
      ReferenceIndex index, double rate, double strike,
      double spread = 0)
    {
      Dt settle = asOf;

      var freq = index.IndexTenor.ToFrequency();
      var discountCurve = new DiscountCurve(asOf)
      {
        {
          effective, PriceFromRate(rate - spread,
            asOf, effective, index.DayCount, freq)
        },
        {
          maturity, PriceFromRate(rate - spread,
            asOf, maturity, index.DayCount, freq)
        },
      };
      discountCurve.ReferenceIndex = index;

      var referenceCurve = discountCurve;
      if (spread > 0 || spread < 0)
      {
        referenceCurve = new DiscountCurve(new ProjectionCurveFitCalibrator(
          asOf, discountCurve, index, null, new CalibratorSettings()),
          discountCurve)
        {
          {
            effective, PriceFromRate(rate - spread,
              asOf, effective, index.DayCount, freq)
          },
          {
            maturity, PriceFromRate(rate - spread,
              asOf, maturity, index.DayCount, freq)
          },
        };
      }

      var fixedLeg = new SwapLeg(effective, maturity, index.Currency,
        strike, index.DayCount, freq, index.Roll, index.Calendar, true)
      {
        Description = "Fixed Leg"
      };
      var floatLeg = new SwapLeg(effective, maturity, freq, 0.0, index)
      {
        AccrueOnCycle = true,
        Description = "Floating leg"
      };

      var fixedLegPricer = new SwapLegPricer(fixedLeg, asOf, settle,
        1, discountCurve, null, null, null, null, null);
      var floatLegPricer = new SwapLegPricer(floatLeg, asOf, settle,
        -1, discountCurve, index, referenceCurve, null, null, null);
      return new SwapPricer(fixedLegPricer, floatLegPricer);
    }
  }
}
