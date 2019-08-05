//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using JumpKind = BaseEntity.Toolkit.Sensitivity.ScenarioShiftType;
using static BaseEntity.Toolkit.Models.Simulations.JumpSpecification;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture, Smoke]
  public class JumpSpecificationTests
  {
    [TestCase(JumpKind.Absolute, 0.1)]
    [TestCase(JumpKind.Absolute, -0.1)]
    [TestCase(JumpKind.Relative, 1.1)]
    [TestCase(JumpKind.Relative, 0.9)]
    [TestCase(JumpKind.Specified, 1.2)]
    [TestCase(JumpKind.Specified, 0.9)]
    public static void TestFxRateJump(JumpKind kind, double jump)
    {
      Dt asOf = Dt.Today(), spot = asOf + 2, jumpDate = asOf + 365;
      double rate = 1.1, expect = Bump(rate, kind, jump);
      var fxRate = new FxRate(asOf, spot, Currency.EUR, Currency.USD, rate);
      var spec = FxRateJump(fxRate, kind, dt => jump);
      Assert.That(spec.MarketObject, Is.SameAs(fxRate));
      spec.ApplyJump(jumpDate);
      Assert.AreEqual(expect, fxRate.Value, 1E-15, "FxRate");

      var fxRateWithBasis = new FxRateWithBasis(
        fxRate, new DiscountCurve(asOf, 0.001));
      var expect2 = Bump(fxRateWithBasis.Rate, kind, jump);
      var spec2 = FxRateJump(fxRateWithBasis, kind, dt => jump);
      Assert.That(spec2.MarketObject, Is.SameAs(fxRateWithBasis));
      spec.ApplyJump(jumpDate);
      Assert.AreEqual(expect2, fxRate.Value, 1E-15, "FxRateWithBasis");
    }

    [TestCase(JumpKind.Absolute, 0.1)]
    [TestCase(JumpKind.Absolute, -0.1)]
    [TestCase(JumpKind.Relative, 1.1)]
    [TestCase(JumpKind.Relative, 0.9)]
    [TestCase(JumpKind.Specified, 1.2)]
    [TestCase(JumpKind.Specified, 0.9)]
    public static void TestStockPriceJump(JumpKind kind, double jump)
    {
      Dt asOf = Dt.Today(), spot = asOf + 2, jumpDate = asOf + 365;
      double price = 1.1, expect = Bump(price, kind, jump);
      var stockCurve = new StockCurve(asOf, price,
        new DiscountCurve(asOf, 0.04), 0.0,
        new Stock {Description = "APL"});
      var spec = SpotJump(stockCurve, kind, dt => jump);
      Assert.That(spec.MarketObject, Is.SameAs(stockCurve));
      spec.ApplyJump(jumpDate);
      Assert.AreEqual(expect, stockCurve.Spot.Value, 1E-15);
    }

    [TestCase(JumpKind.Absolute, 0.01)]
    [TestCase(JumpKind.Absolute, -0.01)]
    [TestCase(JumpKind.Relative, 1.1)]
    [TestCase(JumpKind.Relative, 0.9)]
    [TestCase(JumpKind.Specified, 0.02)]
    [TestCase(JumpKind.Specified, -0.009)]
    public static void TestInterestRateJump(JumpKind kind, double jump)
    {
      Dt asOf = Dt.Today(), jumpDate = asOf + 400;
      string[] tenors = {"1Y", "2Y", "3Y", "4Y", "5Y", "10Y"};
      double rate = 0.005, expect = Bump(rate, kind, jump);
      var curve = new DiscountCurve(asOf)
      {
        Interp = new Weighted()
      };
      for (int i = 0; i < tenors.Length; ++i)
      {
        var dt = Dt.Add(asOf, tenors[i]);
        curve.Add(dt, 1.0);
        curve.SetRate(i, rate);
      }
      var spec =  CurveJump(curve, kind, dt => jump);
      Assert.That(spec.MarketObject, Is.SameAs(curve));

      spec.ApplyJump(jumpDate);
      for (int i = 0; i < tenors.Length; ++i)
      {
        if (curve.GetDt(i) <= jumpDate) continue;
        Assert.AreEqual(expect, curve.NativeCurve.GetY(i), 1E-15);
      }
    }

    private static double Bump(double baseValue, JumpKind kind, double size)
    {
      switch (kind)
      {
      case JumpKind.Absolute:
        return baseValue + size;
      case JumpKind.Relative:
        return baseValue * size;
      case JumpKind.Specified:
        return size;
      }
      throw new ArgumentException($"Invalid jump kind: {kind}");
    }
  }
}
