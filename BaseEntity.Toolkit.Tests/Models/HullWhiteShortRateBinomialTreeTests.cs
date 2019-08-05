using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;
using static BaseEntity.Toolkit.Models.HullWhiteBinomialTreeModel;

namespace BaseEntity.Toolkit.Tests.Models
{
  using NUnit.Framework;

  [TestFixture]
  public class HullWhiteShortRateBinomialTreeTests
  {

    [TestCase(0.0, 0.8, 20, 1E-15)]
    [TestCase(0.4, 0.8, 20, 1E-15)]
    [TestCase(0.9, 1.5, 20, 1E-15)]
    [TestCase(0.9, 1.5, 2000, 1E-10)]
    public void StepVolatilities(double kapa, double sigma, int n,
      double tolerance)
    {
      Dt asOf = Dt.Today(), maturity = Dt.Add(asOf, 10, TimeUnit.Years);

      var dt = (maturity - asOf) / 365 / n;
      var tree = BuildRateStarTree(kapa, sigma, dt, n);
      for (uint k = 1; k <= n; ++k)
      {
        var x = tree[k];
        double mean = 0, variance = 0;
        for (uint j = 0; j <= k; ++j)
        {
          var xj = x[j];
          var p = QMath.BinomialPdf(j, k, 0.5);
          mean += p * xj;
          variance += p * xj * xj;
        }
        variance -= mean * mean;
        var std = Math.Sqrt(variance);
        Assert.AreEqual(0.0, mean, tolerance, "Step mean of x");

        var volatility = Math.Sqrt(QMath.Expd1(-2 * kapa * k * dt) * k * dt) * sigma;
        Assert.AreEqual(volatility, std, tolerance, "Step volatility of x");
      }
    }

    [TestCase(0.0, 0.8, 20, 1E-15)]
    [TestCase(0.4, 0.8, 20, 1E-15)]
    [TestCase(0.9, 1.5, 20, 1E-15)]
    [TestCase(0.9, 1.5, 2000, 1E-10)]
    public void RateAndZeroPrices(double kapa, double sigma, int n,
      double tolerance)
    {
      Dt asOf = Dt.Today(), maturity = Dt.Add(asOf, 10, TimeUnit.Years);
      var discountCurve = new DiscountCurve(asOf, -0.01); // negative rate

      var dt = (maturity - asOf) / 365 / n;
      var rateTree = BuildRateStarTree(kapa, sigma, dt, n);
      var dfTree = FitDiscountCurve(rateTree, dt, asOf, discountCurve);
      for (uint k = 1; k <= n; ++k)
      {
        var r = rateTree[k];
        var z = dfTree[k];
        double rMean = 0, rVariance = 0, dfMean = 0;
        for (uint j = 0; j <= k; ++j)
        {
          var rj = r[j];
          var p = QMath.BinomialPdf(j, k, 0.5);
          rMean += p * rj;
          rVariance += p * rj * rj;

          var zj = z[j];
          Assert.AreEqual(Math.Exp(-rj * dt), zj, tolerance,
            "State zero price");
          dfMean += p * zj;
        }
        rVariance -= rMean * rMean;
        var std = Math.Sqrt(rVariance);
        var volatility = Math.Sqrt(QMath.Expd1(-2 * kapa * k * dt) * k * dt) * sigma;
        Assert.AreEqual(volatility, std, 5 * tolerance,
          "Step volatility of short rate");

        var dfExpect = discountCurve.NativeCurve.Evaluate(k * dt * 365)
          / discountCurve.NativeCurve.Evaluate((k - 1) * dt * 365);
        Assert.AreEqual(dfExpect, dfMean, tolerance,
          "Mean zero price");
      }
    }

  }
}
