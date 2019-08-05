//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using BaseEntity.Shared;
using BaseEntity.Shared.Dynamic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Cashflows.Expressions.Payments;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using CcrSimulations = BaseEntity.Toolkit.Ccr.Simulations;

using BaseEntity.Toolkit.Util;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture]
  public class CcrExposureTests
  {
    private readonly CcrTestData _data = new CcrTestData();

    #region Exposure tests

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void PathValues(int pricerIndex)
    {
      const int sample = 1, nFactors = 2;
      var gridSize = Tenor.Empty;
      var currencies = _data.Currencies;
      var input = _data.CreateInput(nFactors, sample, gridSize,
        currencies.Length, 2, false, false, false, false);
      input.Pricers = new[] {input.Pricers[pricerIndex]};
      input.Cpty = new[] {input.Cpty[0]};
      input.CptyRec = new[] {input.CptyRec[0]};
      input.Names = new[] {input.Names[0]};

      //GC.Collect();
      var expect = Simulate(input, gridSize, null);
      //GC.Collect();
      var actual = Simulate(input, gridSize, new ExposureCalculator());
      actual.ShouldMatchObjectGraph(expect, GetTolerance(input.Pricers[0]));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void TestExposurePricer(int pricerIndex)
    {
      const int sample = 10, nFactors = 2;
      var gridSize = Tenor.Empty;
      var currencies = _data.Currencies;
      var input = _data.CreateInput(nFactors, sample, gridSize,
        currencies.Length, 2, false, false, false, false);
      TestExposurePricer(input.Pricers[pricerIndex]);
    }

    public static void TestExposurePricer(IPricer pricer)
    {
      Dt asOf = pricer.AsOf, settle = pricer.Settle,
        maturity = pricer.Product.Maturity;
      var expect = pricer.Pv();
      var discountCurve = pricer.GetValue<DiscountCurve>("DiscountCurve");
      var settleDf = discountCurve.DiscountFactor(asOf, settle);
      var tol = 1E-15*pricer.GetValue<double>("Notional")*(maturity - asOf)/365;

      var ep = PvEvaluator.Get(pricer, new[] {settle}, null);
      var actual = ep.FastPv(0, settle)*settleDf;
      Assert.That(actual,Is.EqualTo(expect).Within(tol));

      var cp = CcrPricer.Get(pricer);
      var actual2 = cp.FastPv(settle)*settleDf;
      Assert.That(actual2,Is.EqualTo(expect).Within(tol));
    }

    private static ISimulatedValues Simulate(CcrTestData.Input input,
      Tenor gridSize, IExposureCalculator exposureCalculator)
    {
      var engine = CcrSimulations.SimulateCounterpartyCreditRisks(
        MultiStreamRng.Type.MersenneTwister, input.AsOf, input.Pricers,
        input.Names, input.Cpty, input.CptyRec, input.TenorDates,
        input.DiscountCurves, input.FxRates, input.FwdCurves,
        input.CreditCurves, input.Volatilities, input.FactorLoadings,
        -100.0, input.Sample, input.SimulDates, gridSize, false);
      using (CcrUtility.PushExposureCalculator(exposureCalculator))
      {
        engine.Execute();
      }

      return engine.SimulatedValues;
    }

    private static double GetTolerance(IPricer pricer)
    {
      Dt asOf = pricer.AsOf, maturity = pricer.Product.Maturity;
      return Math.Abs(1E-15*pricer.GetValue<double>("Notional")
        *(maturity - asOf)/365);
    }

    #endregion

    #region Sensitivities

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void XccySwapPaymentNodes(int index)
    {
      const int nFactors = 5, sample = 1;
      var input = CreateThisInput(nFactors, sample);
      var pricer = (SwapPricer)input.Pricers[index];
      Dt asOf = pricer.AsOf, settle = pricer.Settle;
      var discountCurve = pricer.DiscountCurve;
      var notional = pricer.Notional;

      var expect = pricer.Pv();
      var tol = 1E-15*pricer.Notional*(pricer.Product.Maturity - asOf)/365;

      PaymentExpression[] ep0, ep1;
      IResettable[] states0, states1;

      using (Evaluable.PushVariants())
      {
        ep0 = PaymentExpression.GetPayments(
          GetPaymentSchedule(pricer), discountCurve).ToArray();
        var actual0 = ep0.FullPv(0, settle,
          discountCurve.DiscountFactor(asOf, settle))
          *discountCurve.DiscountFactor(asOf, settle)
          *notional;
        Assert.That(actual0,Is.EqualTo(expect).Within(tol));
        Evaluable.RecordCommonExpressions(ep0);
        states0 = Evaluable.GetCommonEvaluables().OfType<IResettable>().ToArray();
      }

      using (Evaluable.PushVariants())
      {
        ep1 = PaymentExpression.GetPayments(
          GetSwapPayments(pricer), discountCurve).ToArray();
        var actual1 = ep1.FullPv(0, settle,
          discountCurve.DiscountFactor(asOf, settle))
          *discountCurve.DiscountFactor(asOf, settle)
          *notional;
        Assert.That(actual1,Is.EqualTo(expect).Within(tol));
        Evaluable.RecordCommonExpressions(ep1);
        states1 = Evaluable.GetCommonEvaluables().OfType<IResettable>().ToArray();
      }

      var ccr = CcrPricer.Get(pricer);
      foreach (var date in input.SimulDates)
      {
        var df = discountCurve.Interpolate(asOf, date);
        TestForwardFastPv(ccr, notional, ep0, states0,
          ep1, states1, input.FxRates, date, df, tol);
      }
    }

    private static void TestForwardFastPv(
      CcrPricer ccr, double notional,
      PaymentExpression[] ep0, IResettable[] states0,
      PaymentExpression[] ep1, IResettable[] states1,
      FxRate[] fxRates, Dt fwdDate, double df, double tol)
    {
      foreach (var fxRate in fxRates)
      {
        fxRate.Update(fwdDate, fxRate.FromCcy, fxRate.ToCcy, fxRate.Rate);
      }

      var expect = ccr.FastPv(fwdDate);

      foreach (var state in states0)
      {
        state.Reset();
      }
      var pv0 = ep0.FullPv(GetStartIndex(ep0, fwdDate), fwdDate, df)*notional;
      Assert.That(pv0,Is.EqualTo(expect).Within(tol));

      foreach (var state in states1)
      {
        state.Reset();
      }
      var pv1 = ep1.FullPv(GetStartIndex(ep0, fwdDate), fwdDate, df)*notional;
      Assert.That(pv1,Is.EqualTo(expect).Within(tol));
    }

    private static int GetStartIndex(PaymentExpression[] nodes,
      Dt settle, bool includeSettlePayments = false)
    {
      int idx = 0;
      for (int i = 0; i < nodes.Length; ++i, ++idx)
      {
        var cmp = Dt.Cmp(nodes[i].PayDt, settle);
        if (cmp < 0 || (cmp == 0 && !includeSettlePayments))
          continue;
        break;
      }
      if (idx >= nodes.Length)
        return -1;
      return idx;
    }

    private static PaymentSchedule GetPaymentSchedule(SwapPricer swap)
    {
      var from = swap.Settle;
      var swapScale = GetNotionalScale(swap);
      var recScale = GetNotionalScale(swap.ReceiverSwapPricer) / swapScale;
      var payScale = GetNotionalScale(swap.PayerSwapPricer) / swapScale;
      var ps = new PaymentSchedule();
      ps.AddPayments(swap.ReceiverSwapPricer
        .GetPaymentSchedule(null, from)
        .ConvertAll<Payment>(p => p.Scale(recScale)));
      ps.AddPayments(swap.PayerSwapPricer
        .GetPaymentSchedule(null, from)
        .ConvertAll<Payment>(p => p.Scale(payScale)));
      return ps;
    }

    private static IEnumerable<Payment> GetSwapPayments(SwapPricer swap)
    {
      return PvEvaluator.GetSwapPayments(swap);
    }


    private static double GetNotionalScale(PricerBase pricer)
    {
      var productNotional = pricer.Product.Notional;
      return productNotional.Equals(0.0)
        ? pricer.Notional : (pricer.Notional / productNotional);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestBumpingVsSensitivities(bool optimize)
    {
      const int nFactors = 5, sample = 1;
      using (CcrUtility.PushExposureCalculator(
        optimize? new ExposureCalculator() : null))
      {
        BaseTestSensitivityVsBumping(CreateThisInput(nFactors, sample));
      }
    }

    private CcrTestData.Input CreateThisInput(
      int nFactors, int sample)
    {
      var currencies = _data.Currencies;
      var gridSize = Tenor.Empty;
      var input = _data.CreateInput(nFactors, sample, gridSize,
        currencies.Length, 10, true, false, false, false);
      return input;
    }

    private static Netting getNetting(CcrTestData.Input input)
    {
      var groups = new UniqueSequence<string>(input.Names);
      string[] groupArray = groups.ToArray();
      string[] subGroupArray = Array.ConvertAll(groupArray, g => "1");
      return new Netting(groupArray, subGroupArray, null);
    }

    private static ICounterpartyCreditRiskCalculations CreateEngine(
      CcrTestData.Input input)
    {
      return CcrSimulations.SimulateCounterpartyCreditRisks(
        MultiStreamRng.Type.MersenneTwister, input.AsOf, input.Pricers,
        input.Names, input.Cpty,
        input.CptyRec, input.TenorDates, input.DiscountCurves,
        input.FxRates, input.FwdCurves, input.CreditCurves,
        input.Volatilities, input.FactorLoadings,
        -100.0, input.Sample, input.SimulDates, input.GridSize, false);
    }

    private void BaseTestSensitivityVsBumping(CcrTestData.Input input)
    {
      var netting = getNetting(input);
      var currencies = _data.Currencies;

      var timer = new Stopwatch();
      timer.Start();
      var engine = CreateEngine(input);
      engine.Execute();
      var rt = CcrSimulations.RateSensitivities(engine, netting, 0.01, 0.0,
        true, BumpType.Parallel, QuotingConvention.None, null, false, null);
      var ct = CcrSimulations.CreditSpreadSensitivities(engine, netting, 0.01,
        0.0, true, BumpType.Parallel, QuotingConvention.None, null, false, null);
      var fxt = CcrSimulations.FxSensitivities(engine, netting, 0.01, 0.0, true,
        BumpType.Parallel, false, null);
      var ft = CcrSimulations.ForwardPriceSensitivities(engine, netting, 0.01,
        0.0, true, BumpType.Parallel, QuotingConvention.None, null, false, null);
      var st = CcrSimulations.SpotSensitivities(engine, netting, 0.01, 0.0,
        true, BumpType.Parallel, false, null);
      var cva = engine.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
      if (rt.Rows.Count != 0)
      {
        var dc = input.DiscountCurves;
        var bumpedCva = dc.Select((d, i) =>
        {
          var original = CloneUtil.Clone(d);
          var flags = BumpFlags.BumpRelative;
          var dependentCurves = d.DependentCurves;
          d.DependentCurves = new Dictionary<long, CalibratedCurve>();
          //empty the dependent curve list
          new CalibratedCurve[] {d}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
            flags | BumpFlags.RefitCurve);
          d.DependentCurves = dependentCurves;
          var bumpedEng = CreateEngine(input);
          bumpedEng.Execute();
          var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
          new Curve[] {dc[i]}.CurveSet(new Curve[] {original});
          return retVal;
        }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(dc, d => d.Name);
        for (int i = 0; i < rt.Rows.Count; ++i)
        {
          var row = rt.Rows[i];
          if ((string) row["Measure"] == "CVA")
          {
            var name = (string) row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double) row["Delta"], 1e-6);
          }
        }
      }
      if (ct.Rows.Count != 0)
      {
        var cc = input.CreditCurves;
        var bumpedCva = cc.Select((c, i) =>
        {
          var original = CloneUtil.Clone(c);
          var flags = BumpFlags.BumpRelative;
          var dependentCurves = c.DependentCurves;
          c.DependentCurves = new Dictionary<long, CalibratedCurve>();
          //empty the dependent curve list
          new CalibratedCurve[] {cc[i]}.BumpQuotes(new string[0], QuotingConvention.None,
            0.01,
            flags | BumpFlags.RefitCurve);
          cc[i].DependentCurves = dependentCurves;
          var bumpedEng = CreateEngine(input);
          bumpedEng.Execute();
          var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
          new Curve[] {cc[i]}.CurveSet(new Curve[] {original});
          return retVal;
        }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(cc, c => c.Name);
        for (int i = 0; i < ct.Rows.Count; ++i)
        {
          var row = ct.Rows[i];
          if ((string) row["Measure"] == "CVA")
          {
            var name = (string) row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double) row["Delta"], 1e-6);
          }
        }
      }
      if (fxt.Rows.Count != 0)
      {
        var fx = input.FxRates;
        var bumpedCva = fx.Select((f, i) =>
        {
          var original = CloneUtil.Clone(f);
          var c = new VolatilityCurve(f.Spot, f.GetRate(currencies[i + 1], currencies[0]));
          var flags = BumpFlags.BumpRelative;
          //empty the dependent curve list
          new CalibratedCurve[] {c}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
            flags | BumpFlags.RefitCurve);
          fx[i].SetRate(currencies[i + 1], currencies[0], c.GetVal(0));
          var bumpedEng = CreateEngine(input);
          bumpedEng.Execute();
          var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
          fx[i].SetRate(currencies[i + 1], currencies[0],
            original.GetRate(currencies[i + 1], currencies[0]));
          return retVal;
        }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(fx,
          f =>
            string.Concat(Enum.GetName(typeof (Currency), f.FromCcy),
              Enum.GetName(typeof (Currency), f.ToCcy)));
        for (int i = 0; i < fxt.Rows.Count; ++i)
        {
          var row = fxt.Rows[i];
          if ((string) row["Measure"] == "CVA")
          {
            var name = (string) row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double) row["Delta"], 1e-6);
          }
        }
      }
      if (ft.Rows.Count != 0)
      {
        var fc = input.FwdCurves;
        var bumpedCva = fc.Select((f, i) =>
        {
          var original = CloneUtil.Clone(f);
          var flags = BumpFlags.BumpRelative;
          var dependentCurves = fc[i].DependentCurves;
          f.DependentCurves = new Dictionary<long, CalibratedCurve>();
          //empty the dependent curve list
          new[] {f}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
            flags | BumpFlags.RefitCurve);
          f.DependentCurves = dependentCurves;
          var bumpedEng = CreateEngine(input);
          bumpedEng.Execute();
          var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
          new Curve[] {f}.CurveSet(new Curve[] {original});
          return retVal;
        }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(fc, c => c.Name);
        for (int i = 0; i < ft.Rows.Count; ++i)
        {
          var row = ft.Rows[i];
          if ((string) row["Measure"] == "CVA")
          {
            var name = (string) row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double) row["Delta"], 1e-6);
          }
        }
      }
      if (st.Rows.Count != 0)
      {
        var spot = input.FwdCurves.OfType<IForwardPriceCurve>()
          .Select(c => c.Spot)
          .Where(sp => input.Volatilities.References.Contains(sp) &&
            input.FactorLoadings.References.Contains(sp))
          .ToArray();
        var bumpedCva = spot.Select((f, i) =>
        {
          var original = f.CloneObjectGraph();
          var c = new VolatilityCurve(f.Spot, f.Value);
          var flags = BumpFlags.BumpRelative;
          //empty the dependent curve list
          new CalibratedCurve[] {c}.BumpQuotes(new string[0],
            QuotingConvention.None, 0.01, flags | BumpFlags.RefitCurve);
          spot[i].Value = c.GetVal(0);
          var bumpedEng = CreateEngine(input);
          bumpedEng.Execute();
          var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
          spot[i].Value = original.Value;
          return retVal;
        }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(spot, s => s.Name);
        for (int i = 0; i < st.Rows.Count; ++i)
        {
          var row = st.Rows[i];
          if ((string) row["Measure"] == "CVA")
          {
            var name = (string) row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double) row["Delta"], 1e-6);
          }
        }
      }
    }

    #endregion
  }

  internal static class CcrUtility
  {
    #region Change exposure calculator on the fly

    internal static IDisposable PushExposureCalculator(
      IExposureCalculator calculator)
    {
      return new ExposureCalculatorSetter(calculator);
    }

    private class ExposureCalculatorSetter : IDisposable
    {
      private static FieldInfo _f1;
      private readonly object _prevCalculator;

      public ExposureCalculatorSetter(IExposureCalculator calculator)
      {
        const BindingFlags flags = BindingFlags.DeclaredOnly |
          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var type = typeof (CcrSimulations);

        if (_f1 == null)
        {
          _f1 = type.GetField("_exposureCalculator", flags);
          if (_f1 == null)
            throw new ApplicationException("Field _exposureCalculator not found");
        }
        _prevCalculator = _f1.GetValue(null);
        _f1.SetValue(null, calculator);
      }

      public void Dispose()
      {
        _f1.SetValue(null, _prevCalculator);
      }
    }

    #endregion
  }
}
