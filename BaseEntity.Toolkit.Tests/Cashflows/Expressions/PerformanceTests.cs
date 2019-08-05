//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture, Explicit, Category("Performance"), Category("LongRunning")]
  public class PerformanceTests
  {
    private DiscountCurve _discountCurve, _projectCurve;
    private IList<string> _performanceRecords;
#if DEBUG
    private const bool WantTiming = false;
#else
    private const bool WantTiming = true;
#endif

    [OneTimeSetUp]
    public void Initialize()
    {
      var asOf = new Dt(20140626);
      const string fedfunds = @"toolkit\test\data\FEDFUNDS-20140826.xml";
      _discountCurve = CurveLoader.GetDiscountCurve(
        fedfunds, asOf);

      const string usdlibor3M = @"toolkit\test\data\USDLIBOR3M-20140826.xml";
      _projectCurve = CurveLoader.GetProjectionCurve(
        usdlibor3M, _discountCurve, asOf);

      _performanceRecords = new List<string>();
    }

    [OneTimeTearDown]
    public void PrintPerformance()
    {
      if (_performanceRecords == null) return;
      foreach (var record in _performanceRecords)
      {
        Console.WriteLine(record);
      }
      _performanceRecords = null;
    }


    [TestCase("5Y", 50)]
    [TestCase("5Y", 100)]
    [TestCase("5Y", 250)]
    [TestCase("5Y", 500)]
    [TestCase("5Y", 1000)]
    [TestCase("40Y", 50)]
    [TestCase("40Y", 100)]
    [TestCase("40Y", 250)]
    [TestCase("40Y", 500)]
    [TestCase("40Y", 1000)]
    public void SwapPerformance(string tenor, int pathCount)
    {
      var pricer = CreatePricer(tenor);
      TestPerformance(pricer, pricer.DiscountCurve, tenor, pathCount);
    }

    [TestCase("5Y", 50)]
    [TestCase("5Y", 100)]
    [TestCase("5Y", 250)]
    [TestCase("5Y", 500)]
    [TestCase("5Y", 1000)]
    [TestCase("40Y", 50)]
    [TestCase("40Y", 100)]
    [TestCase("40Y", 250)]
    [TestCase("40Y", 500)]
    [TestCase("40Y", 1000)]
    public void SwapFixedLegPerformance(string tenor, int pathCount)
    {
      var pricer = CreatePricer(tenor).ReceiverSwapPricer;
      TestPerformance(pricer, pricer.DiscountCurve, tenor, pathCount);
    }

    [TestCase("5Y", 50)]
    [TestCase("5Y", 100)]
    [TestCase("5Y", 250)]
    [TestCase("5Y", 500)]
    [TestCase("5Y", 1000)]
    [TestCase("40Y", 50)]
    [TestCase("40Y", 100)]
    [TestCase("40Y", 250)]
    [TestCase("40Y", 500)]
    [TestCase("40Y", 1000)]
    public void SwapFloatingLegPerformance(string tenor, int pathCount)
    {
      var pricer = CreatePricer(tenor).PayerSwapPricer;
      TestPerformance(pricer, pricer.DiscountCurve, tenor, pathCount);
    }

    public void TestPerformance(PricerBase pricer,
      Curve curve, string tenor, int pathCount,
      [System.Runtime.CompilerServices.CallerMemberName] string method = "")
    {
      var dates = new UniqueSequence<Dt>(pricer.Settle);
      dates.Add((pricer.GetPaymentSchedule(null, pricer.AsOf)
        as IEnumerable<Payment>).Select(p => p.PayDt).ToList());
      var dateCount = dates.Count;

      //const int pathCount = 1000;
      var expects = new double[pathCount, dateCount];
      var actual = new double[pathCount, dateCount];
      long time1, time2;

      var ccrPricer = CcrPricer.Get((IPricer)pricer);

      // Calculate the values by the old CCR methods
      {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        for (int i = 0; i < pathCount; ++i)
        {
          for (int d = 0; d < dateCount; ++d)
          {
            // Fast simulate curve evolve
            curve.Spread = 0.02 * (i * dateCount + d) / pathCount / dateCount;
            // Calculate the fast pv
            expects[i, d] = ccrPricer.FastPv(dates[d]);
          }
        }
        timer.Stop();
        time1 = timer.ElapsedMilliseconds;
      }

      // Calculate the values by the new exposure expressions
      {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        IPvEvaluator exPricer;
        IResettable[] nodes;
        InializeExposurePricer(ccrPricer, dates, out exPricer, out nodes);
        for (int i = 0; i < pathCount; ++i)
        {
          for (int d = 0; d < dateCount; ++d)
          {
            // Fast simulate curve evolve
            curve.Spread = 0.02 * (i * dateCount + d) / pathCount / dateCount;
            // Reset all the nodes
            for (int k = 0; k < nodes.Length; ++k) nodes[k].Reset();
            // Reevaluate the fast pv
            actual[i, d] = exPricer.FastPv(d, dates[d]);
          }
        }
        timer.Stop();
        time2 = timer.ElapsedMilliseconds;
      }

      {
        var record = method + '\t' + tenor + '\t'
          + pathCount + '\t' + time1 + '\t' + time2;
        _performanceRecords.Add(record);
        Console.WriteLine(record);
      }


      Assert.That(actual,Is.EqualTo(expects)
        .Within(1E-13 * pricer.Notional));

      if (WantTiming && time1 > 10 && !method.Contains("SwapFixedLeg"))
        Assert.That(time2,Is.LessThan(time1));
    }

    private static void InializeExposurePricer(
       CcrPricer pricer, IReadOnlyList<Dt> dates,
       out IPvEvaluator exPricer, out IResettable[] nodes)
    {
      using (Evaluable.PushVariants())
      {
        exPricer = PvEvaluator.Get(pricer, dates);
        nodes = Evaluable.GetCommonEvaluables().OfType<IResettable>().ToArray();
      }
    }

    private SwapPricer CreatePricer(string tenor)
    {
      var cmpd = CompoundingConvention.None;
      double spread = 0.0, fixedCouponRate = 0.0344;
      double? cap = null, floor = null;
      //string tenor = "40Y";

      var index = _projectCurve.ReferenceIndex;
      Dt effective = new Dt(20101015), maturity = Dt.Add(effective, tenor);
      var compounding = cmpd != CompoundingConvention.None;
      var floatLeg = new SwapLeg(effective, maturity,
        compounding ? Frequency.Annual : Frequency.Quarterly,
        spread, index)
      {
        Cap = cap,
        Floor = floor,
        CompoundingConvention = cmpd,
        CompoundingFrequency = compounding
          ? index.IndexTenor.ToFrequency() : Frequency.None,
        ProjectionType = ProjectionType.SimpleProjection
      };
      var fixedLeg = new SwapLeg(effective, maturity,
        Frequency.SemiAnnual, fixedCouponRate, null,
        index.Currency, index.DayCount, index.Roll, index.Calendar);
      var swap = new Swap(fixedLeg, floatLeg);

      double notional = 5000000;
      Dt asOf = _discountCurve.AsOf,
        settle = Dt.AddDays(asOf, 2, index.Calendar);
      double currentReset = Double.NaN, nextReset = Double.NaN;
      var modelParams = new RateModelParameters(
        RateModelParameters.Model.BGM,
        new[] { RateModelParameters.Param.Sigma },
        new[] { new VolatilityCurve(asOf, 0.0), },
        index.IndexTenor, index.Currency);
      return new SwapPricer(swap, asOf, settle,
        notional, _discountCurve, index, _projectCurve,
        new RateResets(currentReset, nextReset),
        cap.HasValue || floor.HasValue ? modelParams : null,
        null)
      {
        ApproximateForFastCalculation = true,
      };
    }
  }
}
