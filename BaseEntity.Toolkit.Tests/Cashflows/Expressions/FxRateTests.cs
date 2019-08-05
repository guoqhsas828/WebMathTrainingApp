//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Cashflows.Expressions;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture(0.0, InvariantSet.None)]
  [TestFixture(0.01, InvariantSet.None)]
  [TestFixture(0.01, InvariantSet.Basis)]
  [TestFixture(0.01, InvariantSet.Domestic)]
  [TestFixture(0.01, InvariantSet.Foreign)]
  [TestFixture(0.01, InvariantSet.Basis | InvariantSet.Foreign)]
  [TestFixture(0.01, InvariantSet.Basis | InvariantSet.Domestic)]
  [TestFixture(0.01, InvariantSet.Domestic | InvariantSet.Foreign)]
  [TestFixture(0.01, InvariantSet.All)]
  public class FxRateTests
  {
    private readonly double _basis;
    private readonly InvariantSet _invariant;
    private FxCurve _fxCurve;
    private IDisposable _managerStack;

    [Flags]
    public enum InvariantSet
    {
      None = 0,
      Domestic = 1,
      Foreign = 2,
      Basis = 4,
      All = Domestic | Foreign | Basis
    }

    public FxRateTests(double basis, InvariantSet invariant)
    {
      _basis = basis;
      _invariant = invariant;
    }

    [OneTimeSetUp]
    public void SetUpFixture()
    {
      var asOf = new Dt(20140626);
      var usd = new DiscountCurve(asOf, 0.05)
      {
        ReferenceIndex = TestUtility.GetIndex("USDFEDFUNDS_1D"),
        Ccy = Currency.USD,
        Name = "USD_DiscountCurve"
      };
      var eur = new DiscountCurve(asOf, 0.04)
      {
        ReferenceIndex = TestUtility.GetIndex("EONIA"),
        Ccy = Currency.EUR,
        Name = "EUR_DiscountCurve"
      };
      var basis = _basis.AlmostEquals(0.0) ? null :
        new DiscountCurve(asOf, _basis)
        {
          Name = string.Format("{0}{1}_BasisCurve", usd.Ccy, eur.Ccy)
        };
      _fxCurve = new FxCurve(new FxRate(asOf, asOf, eur.Ccy, usd.Ccy, 1.1),
        basis, usd, eur, string.Format("{0}{1}_Curve", usd.Ccy, eur.Ccy));

      var variables = new List<object> {_fxCurve.SpotFxRate};
      var exclude = _invariant;
      if ((exclude & InvariantSet.Domestic) == 0) variables.Add(usd);
      if ((exclude & InvariantSet.Foreign) == 0) variables.Add(eur);
      if ((exclude & InvariantSet.Basis) == 0) variables.Add(basis);
      _managerStack = E.PushVariants(variables);
    }

    [OneTimeTearDown]
    public void TearDownFixture()
    {
      if (_managerStack == null) return;
      _managerStack.Dispose();
      _managerStack = null;
    }


    [Test]
    public void EvolveFxRates()
    {
      var fx = _fxCurve.SpotFxRate.CloneObjectGraph();
      EvolveFxRates(fx.FromCcy, fx.ToCcy);
      _fxCurve.SpotFxRate.Update(fx.AsOf, fx.FromCcy, fx.ToCcy, fx.Rate);
    }

    [Test]
    public void EvolveInverseFxRates()
    {
      var fx = _fxCurve.SpotFxRate.CloneObjectGraph();
      EvolveFxRates(fx.ToCcy, fx.FromCcy);
      _fxCurve.SpotFxRate.Update(fx.AsOf, fx.FromCcy, fx.ToCcy, fx.Rate);
    }

    public void EvolveFxRates(Currency from, Currency to)
    {
      var curve = _fxCurve;
      var asOf = curve.AsOf;
      var dates = Enumerable.Range(0, 120).Select(i => asOf + i*60).ToArray();
      var nodes = dates
        .Select(d => E.FxRate(curve, d, from, to))
        .ToArray();

      // Test forward FX rates.
      ResetAll();
      for (int j = 0, n = nodes.Length; j < n; ++j)
      {
        var node = nodes[j];
        var expect = curve.FxRate(dates[j], from, to);
        var actual = node.Evaluate();
        Assert.That(actual,Is.EqualTo(expect).Within(1E-14));
      }

      var rng = new BaseEntity.Toolkit.Numerics.Rng.RandomNumberGenerator();
      for (int i = 0, n = nodes.Length; i < n; ++i)
      {
        // Get the spot rate
        var spot = dates[i];
        var rate = curve.FxRate(spot, from, to);

        // Evolve spot rate
        curve.SpotFxRate.Update(spot, from, to,
          rate + rng.Uniform(-0.5, 0.5));

        // Reset all calculated values
        ResetAll();

        // Test forward FX rates with evolved spot rate.
        for (int j = i; j < n; ++j)
        {
          var date = dates[j];
          var expect = curve.FxRate(date, from, to);
          var actual = nodes[j].Evaluate();
          Assert.That(actual,Is.EqualTo(expect)
            .Within(1E-14*(1 + Math.Abs(expect))));
        }
      }
      return;
    }

    private void ResetAll()
    {
      foreach (var node in E.GetAllEvaluables().OfType<IResettable>())
        node.Reset();
    }
  }
}
