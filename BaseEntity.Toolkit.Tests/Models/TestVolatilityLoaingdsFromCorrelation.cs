using System;
using System.Runtime.InteropServices;

namespace BaseEntity.Toolkit.Tests.Models
{
  using NUnit.Framework;

  [TestFixture]
  public class TestVolatilityLoaingdsFromCorrelation
  {
    [Test]
    public static void OneNonzeroFactor()
    {
      const double rho = 0.3777;
      double cRho = Math.Sqrt(1 - rho*rho);

      var results = ImplyFactors(new[] {1.0}, rho);
      Assert.AreEqual(1.0, results[0]);

      results = ImplyFactors(new[] { 0.0, 1.0, 0.0 }, rho);
      Assert.AreEqual(cRho, results[0]);
      Assert.AreEqual(rho, results[1]);
      Assert.AreEqual(0.0, results[2]);

      results = ImplyFactors(new[] { 1.0, 0.0, 0.0 }, rho);
      Assert.AreEqual(rho, results[0]);
      Assert.AreEqual(cRho, results[1]);
      Assert.AreEqual(0.0, results[2]);
    }

    [Test]
    public static void TwoFactors()
    {
      var baseFactors = new[] {0.6, -0.8};
      foreach (var rho in new[] { -1.0, -0.9, -0.5, -0.2, 0, 0.2, 0.5, 0.9, 1.0 })
      {
        RoundTripRho(rho, baseFactors, ImplyFactors(baseFactors, rho));
      }
    }


    [Test]
    public void ThreeFactors()
    {
      var baseFactors = NormalizeFactors(new[] { 0.3, -0.2, 0.1 });

      foreach (var rho in new[] { -1.0, -0.9, -0.5, -0.2, 0, 0.2, 0.5, 0.9, 1.0 })
      {
        RoundTripRho(rho, baseFactors, ImplyFactors(baseFactors, rho));
      }
    }

    [Test]
    public void FourFactors()
    {
      var baseFactors = NormalizeFactors(new[] { 0.3, 0.5, -0.2, 0.1 });

      foreach (var rho in new[] { -1.0, -0.9, -0.5, -0.2, 0, 0.2, 0.5, 0.9, 1.0 })
      {
        RoundTripRho(rho, baseFactors, ImplyFactors(baseFactors, rho));
      }
    }

    [Test]
    public void SomeNonzeroFactors()
    {
      var baseFactors = NormalizeFactors(new[] { 0.0, 0.3, 0, -0.2, 0, 0.1, 0 });
      var nonZeros = CountNonzeros(baseFactors);
      Assert.AreEqual(3, nonZeros);

      foreach (var rho in new[] { -1.0, -0.9, -0.5, -0.2, 0, 0.2, 0.5, 0.9, 1.0 })
      {
        var factors = ImplyFactors(baseFactors, rho);
        Assert.AreEqual(nonZeros, CountNonzeros(factors), "Count non-zeros");
        RoundTripRho(rho, baseFactors, factors);
      }
    }


    private static void RoundTripRho(double expect,
      double[] baseFactors, double[] factors)
    {
      // round trip correlation
      double corr = 0, sumsq = 0;
      for (int i = 0, n = baseFactors.Length; i < n; ++i)
      {
        var xi = factors[i];
        sumsq += xi*xi;
        corr += baseFactors[i]*xi;
      }
      Assert.AreEqual(expect, corr, 1E-15, "Round-trip rho");
      Assert.AreEqual(1.0, sumsq, 1E-15, "Sum factors squared");
    }

    private static int CountNonzeros(double[] a)
    {
      int count = 0;
      for (int i = 0; i < a.Length; ++i)
        if (Math.Abs(a[i]) > 1E-16) ++count;
      return count;
    }

    private static double[] NormalizeFactors(double[] f)
    {
      var sumsq = 0.0;
      for (int i = 0, n = f.Length; i < n; ++i)
      {
        sumsq += f[i] * f[i];
      }
      var scale = Math.Sqrt(sumsq);
      for (int i = 0, n = f.Length; i < n; ++i)
      {
        f[i] /= scale;
      }
      return f;
    }

    private static double[] ImplyFactors(
      double[] baseFactors, double rho)
    {
      var output = new double[baseFactors.Length];
      qn_FactorLoadingsFromCorrelation(baseFactors.Length,
        baseFactors, rho, output);
      return output;
    }

    [DllImport("MagnoliaIGNative")]
    private static extern void qn_FactorLoadingsFromCorrelation(
      int factorCount, [In] double[] baseFactors,
      double rho, [Out] double[] outputFactors);
  }
}
