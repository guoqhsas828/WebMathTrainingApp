//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics.Rng;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Models.Simulations.MarketEnvironment;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{

  [TestFixture("Plain")]
  [TestFixture("WithCorrectiveOverlay")]
  class PvConsistencyTests
  {
    public PvConsistencyTests(string flag)
    {
      _withCorrectiveOverlay = (flag == "WithCorrectiveOverlay");
    }

    private static readonly string PricerFolder = Path.Combine(
      BaseEntityContext.InstallDir, "toolkit", "test", "data", "Pricers");


    #region Configuration

    private readonly bool _withCorrectiveOverlay;

    private IDisposable _changedConfig;

    [OneTimeSetUp]
    public void SetUp()
    {
      if (_withCorrectiveOverlay)
      {
        _changedConfig = new ConfigItems
        {
          {"Simulations.EnableCorrectionForCurveTenorChange", true},
          {"CcrPricer.PaymentScheduleFromSettle", true}
        }.Update();
      }
      else
      {
        _changedConfig = new ConfigItems
        {
          {"CcrPricer.PaymentScheduleFromSettle", true}
        }.Update();
      }
    }

    [OneTimeTearDown]
    public void TearDown()
    {
      if (_changedConfig == null) return;
      _changedConfig.Dispose();
      _changedConfig = null;
    }

    #endregion

    [TestCaseSource(nameof(PricerFiles))]
    public void CheckPricer(string caseName)
    {
      var pricer = LoadPricer(caseName);
      if (pricer is CDSCashflowPricer || pricer is SwapBermudanBgmTreePricer)
      {
        return;
      }
      CheckPvConsistency(pricer);
    }

    #region Load pricer from XML files

    public static IEnumerable<string> PricerFiles
    {
      get
      {
        return new DirectoryInfo(PricerFolder)
          .GetFiles("*.xml", SearchOption.AllDirectories)
          .Select(f => f.Name.Replace(f.Extension,""))
          .Where(s => s != "mx_489609_5499971" && s != "mx_2392033_4236861");
      }
    }

    public PricerBase LoadPricer(string baseName)
    {
      var path = Path.Combine(PricerFolder, baseName + ".xml");
      return (PricerBase)XmlSerialization.ReadXmlFile<object>(path);
    }

    #endregion

    #region Check consistency

    public void CheckPvConsistency(PricerBase pricer,
      IReadOnlyList<Dt> exposureDates = null)
    {
      FxCurve fxCurve = null;
      if (pricer.HasPropertyOrField("FxCurve"))
      {
        fxCurve = pricer.GetValue<FxCurve>("FxCurve");
      }
      else if (pricer.HasPropertyOrField("FxCurves"))
      {
        fxCurve = pricer.GetValue<IEnumerable<FxCurve>>("FxCurves")
          .FirstOrDefault();
      }
      var env = CreateMarketEnv(
        pricer.GetDiscountCurve(),
        fxCurve?.SpotFxRate,
        null//pricer.GetReferenceCurve()
        );
      CheckConsistency(pricer, env, exposureDates);
    }

    public static void CheckConsistency(
      PricerBase pricer, MarketEnvironment mktEnv,
      IReadOnlyList<Dt> exposureDates = null)
    {
      var discountCurve = mktEnv.DiscountCurves[0];
      var fxSpotRate = mktEnv.FxRates.IsNullOrEmpty() ? null : mktEnv.FxRates[0];

      Dt asOf = pricer.AsOf, settle = pricer.Settle,
        maturity = pricer.Product.Maturity;

      // Regular PV
      var expect = pricer.Pv();
      var tol = 1E-13*GetAbsNotional(pricer)*(maturity - asOf)/365;

      // PV through payment schedule
      var ps = GetPaymentSchedule(pricer);
      if (ps != null)
      {
        var cfPv = ps.Pv(asOf, settle, discountCurve, null, false, true)
          *GetNotional(pricer);
        Assert.That(cfPv, Is.EqualTo(expect).Within(tol), "Payment Schedule PV");
      }

      // CCR pricer
      var ccrPricer = CcrPricer.Get((IPricer) pricer);
      var fastPv = ccrPricer.FastPv(asOf);
      Assert.That(fastPv, Is.EqualTo(expect).Within(tol), "CCR Fast PV");

      if (exposureDates == null)
      {
        exposureDates = GetExposureDates(asOf,
          pricer.Product.Maturity, Frequency.Monthly).ToArray();
      }
      var simulatedObjects = new List<object>();
      simulatedObjects.AddRange(mktEnv.DiscountCurves);
      if (fxSpotRate != null)
        simulatedObjects.Add(fxSpotRate);

      IPvEvaluator ep;
      IResettable[] states;
      using (Evaluable.PushVariants(simulatedObjects))
      {
        ep = PvEvaluator.Get(ccrPricer, exposureDates);
        states = Evaluable.GetCommonEvaluables()
          .OfType<IResettable>().ToArray();
      }
      var optPv = ep.FastPv(0, asOf);
      Assert.That(optPv, Is.EqualTo(expect).Within(tol), "Optimized PV");

      //
      // Consistency after curve reformat
      //
      mktEnv.Conform();
      fastPv = ccrPricer.FastPv(asOf);
      ResetAll(states);
      optPv = ep.FastPv(0, asOf);
      Assert.That(optPv, Is.EqualTo(fastPv).Within(tol), "Optimized PV after conform");

      //
      // Consistency along a path where curve evolves
      //
      var curve = (Curve) GetNative(discountCurve);
      var rnd = new RandomNumberGenerator();
      for (int i = 1, n = exposureDates.Count; i < n; ++i)
      {
        var date = exposureDates[i];

        // Fake some simulations
        curve.Spread += rnd.Uniform(-0.0035, 0.0035);
        if (fxSpotRate != null)
        {
          fxSpotRate.Update(date, fxSpotRate.FromCcy, fxSpotRate.ToCcy,
            fxSpotRate.Value*(1 + rnd.Uniform(-0.05, 0.05)));
        }

        // CCR pricer fast PV
        fastPv = ccrPricer.FastPv(date);

        // Optimized PV evaluator
        ResetAll(states);
        optPv = ep.FastPv(i, date);

        // Should match exactly
        Assert.That(optPv, Is.EqualTo(fastPv).Within(tol), "Date " + i);
      }
      curve.Spread = 0;
    }

    private MarketEnvironment CreateMarketEnv(
      DiscountCurve discountCurve, FxRate fxSpot,
      DiscountCurve referenceCurve = null)
    {
      var asOf = discountCurve.AsOf;
      var tenors = _withCorrectiveOverlay
        ? Array.ConvertAll(StdTenors, s => Dt.Add(asOf, s))
        : discountCurve.Select(p => p.Date).ToArray();
      var dicountCurves = referenceCurve != discountCurve
        && referenceCurve != null && referenceCurve.NativeCurve.Overlay == null
        ? new[] {discountCurve, referenceCurve} : new[] {discountCurve};
      return new MarketEnvironment(asOf, tenors, dicountCurves,
        null, null, fxSpot == null ? null : new[] {fxSpot}, null);
    }

    private static readonly string[] StdTenors =
    {
      "1D", "1W", "2W", "1M", "3M", "6M", "1Y", "5Y", "10Y", "20Y", "30Y", "60Y"
    };

    private static PaymentSchedule GetPaymentSchedule(PricerBase pricer)
    {
      try
      {
        var swaps = pricer as IEnumerable<SwapLegPricer>;
        return swaps != null
          ? PvEvaluator.GetSwapPayments(swaps)
          : GetAllPaymentSchedule(pricer);
      }
      catch (NotImplementedException)
      {
        return null;
      }
    }

    private static PaymentSchedule GetAllPaymentSchedule(PricerBase pricer)
    {
      var retVal = pricer.GetPaymentSchedule(null, pricer.Settle);
      var paymentPricer = PvEvaluator.TryGetPaymentPricer(pricer);
      if (paymentPricer != null)
      {
        var ps = paymentPricer.GetPaymentSchedule(null, pricer.Settle);
        if (ps != null)
          retVal.AddPayments(PvEvaluator.Scale(ps, 
            1.0/PvEvaluator.GetNotionalScale(pricer)));
      }
      return retVal;
    }

    private static double GetNotional(PricerBase pricer)
    {
      var notional = pricer.Notional;
      var swap = pricer as SwapLegPricer;
      if (swap == null) return notional;
      var unit = swap.SwapLeg.Notional;
      return unit > 0 ? (notional / unit) : notional;
    }

    private static double GetAbsNotional(PricerBase pricer)
    {
      var notional = Math.Abs(pricer.Notional);
      var swaps = pricer as IEnumerable<SwapLegPricer>;
      if (swaps == null) return notional;
      foreach (var swapLegPricer in swaps)
      {
        var n = Math.Abs(swapLegPricer.Notional);
        if (n > notional)
          notional = n;
      }
      return notional;
    }

    public static IEnumerable<Dt> GetExposureDates(
      Dt begin, Dt end, Frequency freq)
    {
      yield return begin;
      for (int i = 1; i <= 5000; ++i)
      {
        var date = Dt.Add(begin, freq, i, CycleRule.None);
        if (date >= end) yield break;
        yield return date;
      }
    }

    private static void ResetAll(IResettable[] nodes)
    {
      if (nodes == null) return;

      for (int i = 0; i < nodes.Length; ++i)
      {
        nodes[i].Reset();
      }
    }

    #endregion
  }
}
