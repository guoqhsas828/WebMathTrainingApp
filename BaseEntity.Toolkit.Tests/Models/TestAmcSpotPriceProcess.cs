//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  /// Simple tests of the simulation engine for the spot price processes.
  /// </summary>
  [TestFixture]
  public class TestAmcSpotPriceProcess
  {
    /// <summary>
    ///  Test that the simulated prices are unbiased.
    /// </summary>
    /// <param name="rate">The risk free interest rate.</param>
    /// <param name="yield">The continuously compounded yield.</param>
    [TestCase(0.0, 0.0)]
    [TestCase(0.10, 0.05)]
    [TestCase(0.05, 0.10)]
    public static void Expectations(double rate, double yield)
    {
      Dt begin = Dt.Today();
      double sigma = 1.0;
      var stockvols = new StaticVolatilityCurves(
        new VolatilityCurve(begin, sigma));
      CheckExpectations(rate, yield, begin, stockvols);
    }

    [TestCase(0.0, 0.0)]
    [TestCase(0.10, 0.05)]
    [TestCase(0.05, 0.10)]
    public static void ExpectationsHeston(double rate, double yield)
    {
      Dt begin = Dt.Today();
      var heston = new HestonProcessParameter
      {
        InitialSigma = 0.625,
        Theta = 0.75,
        Kappa = 0.5,
        Nu = 0.1,
        Rho = 0.25
      };
      CheckExpectations(rate, yield, begin, heston);
    }

    private static void CheckExpectations(double rate, double yield,
      Dt begin, IVolatilityProcessParameter volatility)
    {
      // Simulation inputs.
      var T = (RelativeTime)1.0;
      Dt end = begin + T,
        mid = begin + (RelativeTime)(T/2); // the middle date
      double spot = 100;
      var discountCurve = new DiscountCurve(begin) { Ccy = Currency.USD }
        .SetRelativeTimeRate(rate);
      var stockCurve = new StockCurve(begin, spot, discountCurve, yield, null);
      var tenors = new[] { end };
      var factorLoaings = new[,] { { 1.0 } };
      int pathCount = 10000;

      // Create the simulator, with only one factor.
      var factorNames = new[] { "Primary" };
      var engine = SimulationModels.LiborMarketModel.CreateFactory(
        pathCount, factorNames.Length, begin, tenors, tenors);
      engine.AddSpot(stockCurve.Spot, discountCurve, volatility, factorLoaings,
        (b, e) => yield, null, true);
      var simulator = engine.Simulator;

      // Simulate and compute the expected prices at the middle and end dates.
      var rng = MultiStreamRng.Create(MultiStreamRng.Type.MersenneTwister,
        simulator.Dimension, simulator.SimulationTimeGrid);
      double endPrice = 0, midPrice = 0, t1 = T.Days/2,
        endDf = discountCurve.Interpolate(begin, end),
        midDf = discountCurve.Interpolate(begin, mid);
      for (int i = 0; i < pathCount; ++i)
      {
        var path = simulator.GetSimulatedPath(i, rng);
        // The price at the end date.
        var price = path.EvolveSpotPrice(0, 0) / endDf;
        endPrice += (price - endPrice) / (i + 1);
        // The price at middle date comes from Brownian bridge.
        price = path.EvolveSpotPrice(0, t1, 0) / midDf;
        midPrice += (price - midPrice) / (i + 1);
      }

      // Test equality
      var tolerance = spot/Math.Sqrt(pathCount);
      Assert.AreEqual(stockCurve.Interpolate(end), endPrice, tolerance);
      Assert.AreEqual(stockCurve.Interpolate(mid), midPrice, tolerance);
      return;
    }

    /// <summary>
    ///  Test the premium of stock options, Americans style.
    /// </summary>
    /// <param name="rate">The risk free interest rate.</param>
    /// <param name="yield">The continuously compounded yield.</param>
    /// <param name="optionType">The option type (call or put).</param>
    /// <param name="strike">The strike.</param>
    [TestCase(0.0, 0.0, OptionType.Call, 100.0)]
    [TestCase(0.0, 0.0, OptionType.Put, 100.0)]
    [TestCase(0.05, 0.0, OptionType.Put, 100.0)]
    [TestCase(0.05, 0.10, OptionType.Put, 100.0)]
    [TestCase(0.05, 0.05, OptionType.Put, 100.0)]
    public void AmericanOption(double rate, double yield,
      OptionType optionType, double strike)
    {
      double spot = 100, sigma = 0.4, T = 1.0;
      Dt asOf = Dt.Today(), expiry = asOf + (RelativeTime)1.0;
      var discountCurve = new DiscountCurve(asOf) { Ccy = Currency.USD }
        .SetRelativeTimeRate(rate);
      var stockCurve = new StockCurve(asOf, spot, discountCurve, yield, null);
      //var fwd = stockCurve.Interpolate(expiry);

      // Calculate the expected price
      var volSurface = new FlatVolatility{Volatility=sigma};
      var pricer = new StockOptionPricer(new StockOption(
        expiry, optionType, OptionStyle.American, strike),
        asOf, asOf, stockCurve, discountCurve, volSurface);
      var expect = pricer.ProductPv();

      //
      // Now construct American MC Engine
      //
      int pathCount = 10000, dateCount = 1;

      // Create the market environment 
      var tenors = Enumerable.Range(0, dateCount)
        .Select(i => asOf + (RelativeTime)((1 + i) * T / dateCount))
        .ToArray();
      var factorLoaings = Enumerable.Range(0, tenors.Length)
        .Aggregate(new double[tenors.Length, 1], (a, i) =>
        {
          a[i, 0] = 1;
          return a;
        });

      var mktenv = new MarketEnvironment(asOf, tenors,
        new[] { discountCurve }, null, null, null, new[] { stockCurve });

      // Create the simulator, with only one factor.
      var factorNames = new[] { "Primary" };
      var engine = SimulationModels.LiborMarketModel.CreateFactory(
        pathCount, factorNames.Length, asOf, tenors, tenors);
      var stockvols = new StaticVolatilityCurves(Enumerable
        .Repeat(new VolatilityCurve(asOf, sigma), tenors.Length).ToArray());
      engine.AddSpot(stockCurve.Spot, discountCurve, stockvols, factorLoaings,
        (b, e) => yield, null, true);
      var simulator = engine.Simulator;

      // Create the exercise evaluator
      var exerciseDateCount = 8;
      var exerciseDates = Enumerable.Range(1, exerciseDateCount)
        .Select(i => asOf + (RelativeTime)(i * T / exerciseDateCount))
        .ToArray();
      var exerciseEvaluator = new ExerciseValueEvaluator(
        optionType, stockCurve, strike, exerciseDates);

      // Create the basis function
      var basis = new ForwardPriceBasis(stockCurve, strike, optionType);

      // Run MC engine
      var actual = LeastSquaresMonteCarloPricingEngine.Calculate(
        1.0, mktenv, simulator, MultiStreamRng.Type.MersenneTwister,
        Currency.USD, null, basis, null, exerciseEvaluator,
        0.0, null)[0,0];
 
      // Check result, accuracy proportional to the inverse of square root of path count.
      Assert.AreEqual(expect, actual, 3*expect/Math.Sqrt(pathCount));
      return;
    }


    /// <summary>
    ///  Evaluate the value of the option at the exercise date.
    /// </summary>
    [Serializable]
    private class ExerciseValueEvaluator : ExerciseEvaluator
    {
      private readonly ForwardPriceCurve _fwdCurve;
      private readonly double _strike;
      private readonly int _sign; // call = 1, put = -1;

      public ExerciseValueEvaluator(
        OptionType otype,
        ForwardPriceCurve fwdPriceCurve, double strike,
        IEnumerable<Dt> exerciseDates)
        : base(exerciseDates, true, Dt.Empty)
      {
        _fwdCurve = fwdPriceCurve;
        _strike = strike;
        _sign = otype == OptionType.Put
          ? -1
          : (otype == OptionType.Call ? 1 : 0);
      }

      public override double Value(Dt date)
      {
        return _sign*(_fwdCurve.Interpolate(date) - _strike);
      }

      public override double Price(Dt date)
      {
        return 0;
      }
    }

    /// <summary>
    ///  The basis functions consist of 
    ///   (a) the 3rd polynomial of the current stock price (normalized by strike);
    ///   (b) the intrinsic value at the current date.
    /// </summary>
    [Serializable]
    private class ForwardPriceBasis : BasisFunctions
    {
      #region Data
      private readonly ForwardPriceCurve _fwdCurve;
      private readonly double _strike;
      private readonly int _sign;
      #endregion

      #region Constructors

      public ForwardPriceBasis(ForwardPriceCurve fwdPriceCurve,
        double strike, OptionType otype)
      {
        _fwdCurve = fwdPriceCurve;
        _strike = strike;
        _sign = otype == OptionType.Put
          ? -1
          : (otype == OptionType.Call ? 1 : 0);
        Dimension = 5;
        return;
      }

      #endregion

      #region IBasis Members

      public override void Generate(Dt date, double[] retVal)
      {
        // use the logarithm of the stock price as basis
        int last = retVal.Length - 1;
        double s =_fwdCurve.Interpolate(date)/_strike;
        double logs = Math.Log(s);
        retVal[0] = 1.0;
        for (int i = 1; i < last; ++i)
          retVal[i] = retVal[i - 1] * logs;
        // add the current instrinsic value to the basis.
        retVal[last] = Math.Max(0, _sign * (s - 1));
      }

      #endregion
    }
  }
}
