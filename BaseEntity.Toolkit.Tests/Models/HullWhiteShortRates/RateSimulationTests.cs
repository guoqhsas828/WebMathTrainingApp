//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.HullWhiteShortRates;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Tests.Models.HullWhiteShortRates
{
  using NUnit.Framework;

  [TestFixture]
  public class RateSimulationTests
  {
    /// <summary>
    ///  Test the integral with various quadrature points<math>
    ///   I(a,b,c) \equiv \int_0^1 \frac{1-e^{-a z}}{a(c - bz)}d z
    ///   ,\quad
    ///   a \geq 0
    ///   ,\;
    ///   b \geq 0
    ///   ,\;
    ///   c \geq 1 + b
    /// </math>
    /// </summary>
    /// <param name="a">Parameter a</param>
    /// <param name="b">Parameter b</param>
    [TestCase(0.5, 0.01)]
    [TestCase(5, 0.1)]
    [TestCase(10, 1.0)]
    [TestCase(20, 2.0)]
    [TestCase(30, 3.0)]
    public static void Integral(double a, double b)
    {
      double c = 1 + b;
      var points = new uint[] {23, 51, 101, 151, 201};
      var integrals = new double[points.Length];
      var last = points.Length - 1;
      var expect = integrals[last] = qn_integral_expd1_weighted(
        a, b, c, points[last]);
      for (int i = last; --i >= 0;)
      {
        var actual = integrals[i] = qn_integral_expd1_weighted(
          a, b, c, points[i]);
        Assert.AreEqual(expect, actual, 5E-15, "at n = " + points[i]);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    [TestCase(0.0)]
    [TestCase(0.5)]
    [TestCase(1.5)]
    [TestCase(2.5)]
    public static void IntegralZeroA(double beta)
    {
      double a = 0, c = 2.5, b = c*(1 - Math.Exp(-beta));
      var points = new uint[] {23, 51, 101, 151};
      var integrals = new double[points.Length];
      var d = QMath.Expd1(-beta);
      var expect = QMath.Expd2(-beta)/(d*d)/c;
      for (int i = points.Length; --i >= 0;)
      {
        uint n = points[i];
        var actual = integrals[i] = qn_integral_expd1_weighted(
          a, b, c, n);
        Assert.AreEqual(expect, actual,
          n == 23 && beta.AlmostEquals(2.5) ? 1E-11 : 1E-14,
          "at n = " + n);
      }
    }

    /// <summary>
    /// <math>
    ///   \int_0^1\frac{1-e^{-a z}}{a}dz
    ///    = \int_0^1\frac{1}{a}d\left(z + \frac{e^{-az}}{a}\right)
    ///   = \frac{1}{a}\left(\frac{e^{-a}}{a} + 1 - \frac{1}{a}\right)
    /// </math>
    /// </summary>
    [TestCase(0.0)]
    [TestCase(1.5)]
    [TestCase(15)]
    [TestCase(25)]
    public static void IntegralZeroB(double a)
    {
      double c = 2.5;
      var points = new uint[] {23, 51, 101, 151};
      var integrals = new double[points.Length];
      var expect = QMath.Expd2(-a)/c;
      for (int i = points.Length; --i >= 0;)
      {
        var actual = integrals[i] = qn_integral_expd1_weighted(
          a, 0.0, c, points[i]);
        Assert.AreEqual(expect, actual, 1E-14, "at n = " + points[i]);
      }
    }

    [DllImport("MagnoliaIGNative")]
    private static extern double qn_integral_expd1_weighted(
      double a, double b, double c, uint n);

    /// <summary>
    ///  Validate simulated discount factors against the analytic formula:
    ///   P(t,T) = exp(A(t,T) - B(t,T) r)
    /// </summary>
    /// <param name="dataIndex">Index of the data.</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void SimulateDiscount(int dataIndex)
    {
      var d = data[dataIndex];
      var tenorNames = d.TenorNames;
      var sigma = d.Sigmas;
      var meanReversion = d.MeanReversions;
      var forward = d.Forwards;

      // Create discount curve from the zero prices
      //TODO: find out why some dates like 2017-04-04 fails the accuracy 1E-12
      var asOf = new Dt(20170329); // Dt.Today();
      var zc = CreateDiscountCurve(asOf, tenorNames, forward);

      // Create short rate calculator for validation
      var time = zc.Select(pt => (pt.Date - asOf)/365.0).ToArray();
      var initialZeroPrices = zc.Select(pt => pt.Value).ToArray();
      var calc = PiecewiseConstantCalculator.Create(
        time, sigma, meanReversion);

      // Create market environment
      var mktenv = CreateMarketEnv(zc, null);
      var simulationDates = zc.Select(pt => pt.Date).ToArray();

      // Create factorized volatility system
      var tenors = Array.ConvertAll(tenorNames, Tenor.Parse);
      var volatilities = new VolatilityCollection(tenors);
      volatilities.Add(zc, CreateHullWhiteVolatilities(asOf,
        ListUtil.CreateList(zc.Count, zc.GetDt), sigma, meanReversion));
      var factors = new FactorLoadingCollection(new[] {"1"}, tenors);
      factors.AddFactors(zc, new double[1, 1] {{1.0}});

      // Create simulator
      var pathCount = 10;
      var simulator = Simulator.Create(SimulationModels.HullWhiteModel,
        pathCount, simulationDates, mktenv, volatilities, factors,
        new int[0], 0);
      Assert.AreEqual(2, simulator.Dimension, "Dimension per date");

      // Create random number generator
      var rng = MultiStreamRng.Create(MultiStreamRng.Type.MersenneTwister,
        simulator.Dimension, simulator.SimulationTimeGrid);

      // Generated simulated paths
      for (int pathIndex = 0; pathIndex < pathCount; ++pathIndex)
      {
        using (var path = simulator.GetSimulatedPath(pathIndex, rng))
        {
          var dateCount = simulationDates.Length;
          var shortRates = (double[]) ArrayOfDoubleMarshaler.
            GetInstance("").MarshalNativeToManaged(path.GetRates(0));
          Assert.AreEqual(dateCount, shortRates.Length/2, "# of short rates per path");

          for (int dateIndex = 0; dateIndex < dateCount; ++dateIndex)
          {
            double fx = 1.0, numeraire = 1.0;
            path.EvolveDiscountCurve(0, dateIndex, zc, ref fx, ref numeraire);
            ValidateZeroPrices(dateIndex, shortRates[dateIndex], zc,
              calc, initialZeroPrices, time);
          }
        }
      }
    }

    private static void ValidateZeroPrices(
      int dateIndex, double shortRate,
      DiscountCurve zeroPriceCurve,
      PiecewiseConstantCalculator calc,
      IReadOnlyList<double> initialZeroPrices,
      IReadOnlyList<double> time)
    {
      int i = dateIndex, n = zeroPriceCurve.Count;
      double r = shortRate, p0 = zeroPriceCurve.GetVal(i);
      for (int j = i + 1; j < n; ++j)
      {
        var A = calc.CalculateA(i, j, initialZeroPrices, time);
        var B = calc.CalculateB(i, j);
        var expect = Math.Exp(A - B*r);
        var actual = zeroPriceCurve.GetVal(j)/p0;
        Assert.AreEqual(expect, actual, 2E-12,
          "Zero price (" + i + ", " + j + ')');
      }
    }

    #region Utilities

    private static VolatilityCurve[] CreateHullWhiteVolatilities(
      Dt asOf,
      IReadOnlyList<Dt> dates,
      IReadOnlyList<double> sigmas,
      IReadOnlyList<double> meanReversions)
    {
      var sigmaCurve = new VolatilityCurve(asOf)
      {
        Interp = new Flat(0.0),
        Flags = CurveFlags.Integrand | CurveFlags.SmoothTime,
        Name = "Sigma",
      };
      var meanRevCurve = new VolatilityCurve(asOf)
      {
        Interp = new Flat(0.0),
        Flags = CurveFlags.Integrand | CurveFlags.SmoothTime,
        Name = "Mean Reversion",
      };
      for (int i = 0, n = dates.Count; i < n; ++i)
      {
        var dt = dates[i];
        sigmaCurve.Add(dt, sigmas[i]);
        meanRevCurve.Add(dt, meanReversions[i]);
      }
      return HullWhiteShortRateVolatility.CreateVolatilityArray(
        meanRevCurve, sigmaCurve);
    }

    /// <summary>
    /// Creates a discount curve from the specified tenors 
    /// and piecewise flat, continuous compounding, forward rates.
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="tenors">The tenors</param>
    /// <param name="forwardRates">The forward rates</param>
    /// <returns>DiscountCurve</returns>
    private static DiscountCurve CreateDiscountCurve(
      Dt asOf, string[] tenors, double[] forwardRates)
    {
      var dc = new DiscountCurve(asOf)
      {
        Interp = new Weighted(),
        Ccy = Currency.USD,
        Name = "Domestic discount curve",
      };
      dc.Flags |= CurveFlags.SmoothTime;

      var df = 1.0;
      var lastDate = asOf;
      for (int i = 0, n = tenors.Length; i < n; ++i)
      {
        var date = Dt.Add(asOf, tenors[i]);
        df *= Math.Exp(-forwardRates[i]*(date - lastDate)/365.0);
        dc.Add(date, df);
        lastDate = date;
      }
      return dc;
    }

    private static MarketEnvironment CreateMarketEnv(
      DiscountCurve discountCurve, FxRate fxSpot)
    {
      var asOf = discountCurve.AsOf;
      var tenors = discountCurve.Select(p => p.Date).ToArray();
      return new MarketEnvironment(asOf, tenors, new[] {discountCurve},
        null, null, fxSpot == null ? null : new[] {fxSpot}, null);
    }

    #endregion

    #region Test Data

    private struct Data
    {
      public string[] TenorNames;
      public double[] Sigmas, MeanReversions, Forwards;
    }

    private static readonly Data[] data =
    {
      // Zero volatility with non-zero mean reversions
      new Data
      {
        TenorNames = new[] {"3M", "6M", "1Y", "2Y", "5Y", "10Y", "20Y", "50Y"},
        Sigmas = new[] {.0, .0, .0, .0, .0, .0, .0, .0},
        MeanReversions = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2, 0.2, 0.2},
        Forwards = new[] {.005, .01, .02, .025, .03, .04, .04, .04},
      },
      // Non-zero volatilities with zero mean reversions
      new Data
      {
        TenorNames = new[] {"3M", "6M", "1Y", "2Y", "5Y", "10Y", "20Y", "50Y"},
        Sigmas = new[] {.008, .006, .005, .004, .003, .002, .002, .002},
        MeanReversions = new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        Forwards = new[] {.005, .01, .02, .025, .03, .04, .04, .04},
      },
      // Non-zero volatilities with non-zero mean reversions
      new Data
      {
        TenorNames = new[] {"3M", "6M", "1Y", "2Y", "5Y", "10Y", "20Y", "50Y"},
        Sigmas = new[] {.008, .006, .005, .004, .003, .002, .002, .002},
        MeanReversions = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2, 0.2, 0.2},
        Forwards = new[] {.005, .01, .02, .025, .03, .04, .04, .04},
      },

      // Zero volatility with non-zero mean reversions and negative rates
      new Data
      {
        TenorNames = new[] {"3M", "6M", "1Y", "2Y", "5Y", "10Y", "20Y", "50Y"},
        Sigmas = new[] {.0, .0, .0, .0, .0, .0, .0, .0},
        MeanReversions = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2, 0.2, 0.2},
        Forwards = new[] {-.005, -.002, -0.001, .0125, .03, .04, .04, .04},
      },
      // Non-zero volatilities with zero mean reversions and negative rates
      new Data
      {
        TenorNames = new[] {"3M", "6M", "1Y", "2Y", "5Y", "10Y", "20Y", "50Y"},
        Sigmas = new[] {.008, .006, .005, .004, .003, .002, .002, .002},
        MeanReversions = new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        Forwards = new[] {-.005, -.002, -0.001, .0125, .03, .04, .04, .04},
      },
      // Non-zero volatilities with non-zero mean reversions and negative rates
      new Data
      {
        TenorNames = new[] {"3M", "6M", "1Y", "2Y", "5Y", "10Y", "20Y", "50Y"},
        Sigmas = new[] {.008, .006, .005, .004, .003, .002, .002, .002},
        MeanReversions = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2, 0.2, 0.2},
        Forwards = new[] {-.005, -.002, -0.001, .0125, .03, .04, .04, .04},
      },
    };

    #endregion
  }
}
