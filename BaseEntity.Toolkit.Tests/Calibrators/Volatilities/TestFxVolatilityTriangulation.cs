// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  [TestFixture]
  public class TestFxVolatilityTriangulation
  {
    [Test]
    public static void EurJpy()
    {
      Dt asOf = Dt.Today();
      var eurDiscount = new DiscountCurve(asOf, 0.01);
      var usdDiscount = new DiscountCurve(asOf, 0.02);
      var jpyDiscount = new DiscountCurve(asOf, 0.005);

      //double eurUsd = 1.087, usdJpy 109.715;
      var spot1 = new FxRate(asOf, asOf,
        Currency.EUR, Currency.USD, 1.087);
      var spot2 = new FxRate(asOf, asOf,
        Currency.USD, Currency.JPY, 109.715);
      var fxCurv1 = new FxCurve(spot1,
        null, usdDiscount, eurDiscount, "EURUSD");
      var fxCurv2 = new FxCurve(spot2,
        null, jpyDiscount, usdDiscount, "USDJPY");

      const double sigma1 = 0.8, sigma2 = 0.6, correlation = -0.9;
      var expect = Math.Sqrt(sigma1*sigma1
        + sigma2*sigma2 + 2*correlation*sigma1*sigma2);

      var fxSurface1 = CalibratedVolatilitySurface.FromCurve(
        new VolatilityCurve(asOf, sigma1));
      var fxSurface2 = CalibratedVolatilitySurface.FromCurve(
        new VolatilityCurve(asOf, sigma2));

      var triagSurface = FxVolatilityTriangulation.Create(
        fxCurv1, fxSurface1, fxCurv2, fxSurface2, correlation);

      var vol = triagSurface.Interpolate(asOf + 90, 100);

      Assert.AreEqual(expect, vol, 1E-16);
    }

    [Test]
    public static void EurGbp()
    {
      Dt asOf = Dt.Today();
      var eurDiscount = new DiscountCurve(asOf, 0.01);
      var usdDiscount = new DiscountCurve(asOf, 0.02);
      var gbpDiscount = new DiscountCurve(asOf, 0.005);

      //double eurUsd = 1.087, usdJpy 109.715;
      var spot1 = new FxRate(asOf, asOf,
        Currency.EUR, Currency.USD, 1.087);
      var spot2 = new FxRate(asOf, asOf,
        Currency.GBP, Currency.USD, 1.2794);
      var fxCurv1 = new FxCurve(spot1,
        null, usdDiscount, eurDiscount, "EURUSD");
      var fxCurv2 = new FxCurve(spot2,
        null, usdDiscount, gbpDiscount, "GBPUSD");

      const double sigma1 = 0.8, sigma2 = 0.6, correlation = 0.9;
      var expect = Math.Sqrt(sigma1*sigma1
        + sigma2*sigma2 - 2*correlation*sigma1*sigma2);

      var fxSurface1 = CalibratedVolatilitySurface.FromCurve(
        new VolatilityCurve(asOf, sigma1));
      var fxSurface2 = CalibratedVolatilitySurface.FromCurve(
        new VolatilityCurve(asOf, sigma2));

      var triagSurface = FxVolatilityTriangulation.Create(
        fxCurv1, fxSurface1, fxCurv2, fxSurface2, correlation);

      var vol = triagSurface.Interpolate(asOf + 90, 100);

      Assert.AreEqual(expect, vol, 1E-16);
    }
  }
}
