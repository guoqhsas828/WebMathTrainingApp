//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestSabrModel : ToolkitTestBase
  {
    [Test]
    public void CalibrationNoRestriction()
    {
      // We expect the mean of the square errors less than 1E-5,
      // with the following exceptions:
      Dictionary<string, double> errors = new Dictionary<string, double>
      {
        {"HSI[1]", 0.00025},
        {"HSI[2]", 7E-5},
        {"HSI[4]", 2E-5},
      };
      // Empty lower and upper bounds
      var nil = EmptyArray<double>.Instance;

      foreach (var data in TnPipVolatilityData.All)
      {
        var name = data.Name;
        var v = data.Volatilities;
        var strikes = data.Strikes;
        var times = data.Expiries;
        for (int i = 0, n = times.Length; i < n; ++i)
        {
          Calibrate(name + '[' + i + ']', times[i], strikes,
            Enumerable.Range(0, strikes.Length).Select(j => v[i, j]).ToArray(),
            nil, nil, errors);
        }
      }
    }

    [Test]
    public void CalibrationBetaIsOne()
    {
      // We expect the mean of the square errors less than 1E-5,
      // with the following exceptions:
      Dictionary<string, double> errors = new Dictionary<string, double>
      {
        {"N225[0]", 3E-5},
        {"HSI[1]", 0.002},
        {"HSI[1][0]", 0.10},
        {"HSI[1][1]", 0.10},
        {"HSI[2]", 0.002},
        {"HSI[2][0]", 0.10},
        {"HSI[2][1]", 0.10},
        {"HSI[3]", 0.001},
        {"HSI[3][0]", 0.10},
        {"HSI[4]", 0.0006},
        {"HSI[5]", 0.0003},
        {"HSI[6]", 0.0002},
        {"HSI[7]", 0.0002},
        {"HSI[8]", 0.0002},
      };
      // Empty lower and upper bounds
      double[] lowerBounds = {0.0,        1.0, 0.0,        Double.NaN};
      double[] upperBounds = {Double.NaN, 1.0, Double.NaN, Double.NaN};

      foreach (var data in TnPipVolatilityData.All)
      {
        var name = data.Name;
        var v = data.Volatilities;
        var strikes = data.Strikes;
        var times = data.Expiries;
        for (int i = 0, n = times.Length; i < n; ++i)
        {
          Calibrate(name + '[' + i + ']', times[i], strikes,
            Enumerable.Range(0, strikes.Length).Select(j => v[i, j]).ToArray(),
            lowerBounds, upperBounds, errors);
        }
      }
    }

    [Test]
    public void CalibrationBetaPosistive()
    {
      // We expect the mean of the square errors less than 1E-5,
      // with thefollowing exceptions:
      Dictionary<string, double> errors = new Dictionary<string, double>
      {
        {"HSI[2]", 8E-5},
        {"HSI[4]", 2E-5},
      };
      // Empty lower and upper bounds
      double[] lowerBounds = { 0.0, 0.0, 0.0, Double.NaN };
      double[] upperBounds = { Double.NaN, 1.0, Double.NaN, Double.NaN };

      foreach (var data in TnPipVolatilityData.All)
      {
        var name = data.Name;
        var v = data.Volatilities;
        var strikes = data.Strikes;
        var times = data.Expiries;
        for (int i = 0, n = times.Length; i < n; ++i)
        {
          Calibrate(name + '[' + i + ']', times[i], strikes,
            Enumerable.Range(0, strikes.Length).Select(j => v[i, j]).ToArray(),
            lowerBounds, upperBounds, errors);
        }
      }
    }

    private static void Calibrate(string name,
      double time, double[] moneyness, double[] volatilities,
      double[] lowerbounds,  double[] upperBounds,
      Dictionary<string, double> errors)
    {
      double zeta = 0, beta = 0, nu = 0, rho = 0;
      try
      {
        SabrModel.CalibrateParameters(time, moneyness, volatilities,
          lowerbounds, upperBounds, ref zeta, ref beta, ref nu, ref rho);
      }
      catch (Exception e)
      {
        Assert.IsFalse(true, name + ":Exception: " + e.Message);
        return;
      }
      double tol;
      double sumsq = 0;
      for (int i = 0, n = moneyness.Length; i < n; ++i)
      {
        var expect = volatilities[i];
        var sigma = SabrModel.BlackVolatility(zeta, beta, nu, rho, moneyness[i], time);
        var label = name + '[' + i + ']';
        if (!errors.TryGetValue(label, out tol)) tol = 0.05;
        Assert.AreEqual(expect, sigma, tol, label);
        sumsq += (sigma - expect) * (sigma - expect);
      }
      if (!errors.TryGetValue(name, out tol))
        tol = 1E-5;
      Assert.AreEqual(0, sumsq / moneyness.Length, tol, name + ".sumsq");
    }

  }
}
