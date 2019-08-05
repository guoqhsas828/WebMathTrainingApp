//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestRatchetOptionModel
  {
    private static double FirstOrder(Func<double, double> f, double f0)
    {
      const double dx = 1E-7;
      if (Double.IsNaN(f0)) f0 = f(0);
      return (f(dx) - f0) / dx;
    }
    private static double SecondOrder(Func<double, double> f, double f0)
    {
      const double dx = 1E-4;
      if (Double.IsNaN(f0)) f0 = f(0);
      return (0.5 * (f(dx) + f(-dx)) - f0) / dx / dx;
    }
    private static double P(OptionType type, double start, double expiry,
      double spot, double alpha, double rfr, double qrate, double sigma)
    {
      double delta = 0, gamma = 0, theta = 0, vega = 0, rho = 0;
      return BlackScholes.ForwardStartingOptionValue(type,
        start, expiry, spot, alpha, rfr, qrate, sigma, new DividendSchedule(Dt.Empty),
        ref delta, ref gamma, ref theta, ref vega, ref rho);
    }

    [Test]
    public void TestForwardStartingOptions()
    {
      // Test the correctness of the analytical derivatives.
      double start = 0.5, expiry = 1.0, spot = 2.5, alpha = 1.0, 
        rfr = 0.04, qrate = 0.1, sigma = 0.4;
      double delta = 0, gamma = 0, theta = 0, vega = 0, rho = 0;
      var type = OptionType.Call;
      var fv = BlackScholes.ForwardStartingOptionValue(type,
        start, expiry, spot, alpha, rfr, qrate, sigma, new DividendSchedule(Dt.Empty),
        ref delta, ref gamma, ref theta, ref vega, ref rho);
      var delta0 = FirstOrder(x => P(type, start, expiry, spot+x, alpha,
        rfr, qrate, sigma), fv);
      var gamma0 = SecondOrder(x => P(type, start, expiry, spot+x, alpha,
        rfr, qrate, sigma), fv);
      var theta0 = FirstOrder(x => P(type, start + x, expiry + x, spot, alpha,
        rfr, qrate, sigma), fv);
      var vega0 = FirstOrder(x => P(type, start, expiry, spot, alpha,
        rfr, qrate, sigma+x), fv);
      var rho0 = FirstOrder(x => P(type, start, expiry, spot, alpha,
        rfr + x, qrate, sigma), fv);
      Assert.AreEqual(delta0, delta, 1E-7, "delta");
      Assert.AreEqual(gamma0, gamma, 1E-7, "gamma");
      Assert.AreEqual(theta0, theta, 1E-7, "theta");
      Assert.AreEqual(vega0, vega, 1E-7, "vega");
      Assert.AreEqual(rho0, rho, 1E-7, "rho");
    }
  }
}
