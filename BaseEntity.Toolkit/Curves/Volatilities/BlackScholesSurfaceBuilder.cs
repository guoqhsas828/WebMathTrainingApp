/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   Utilities to build the volatility surface based on the Black-Scholes model.
  /// </summary>
  public static class BlackScholesSurfaceBuilder
  {
    #region Plain interpolation smile builder
    /// <summary>
    /// Builds the plain interpolation smile.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="bsModelParameters">The Black-Scholes model parameters.</param>
    /// <param name="interp">The interp.</param>
    /// <param name="interplationSpace">The interplation space.</param>
    /// <returns>Interpolator.</returns>
    public static SplineInterpolationSmile BuildPlainInterpolationSmile(
      this IEnumerable<StrikeVolatilityPair> data,
      IBlackScholesParameterData bsModelParameters,
      Interp interp, SmileInputKind interplationSpace)
    {
      double time = bsModelParameters.Time,
        spot = bsModelParameters.Spot,
        r1 = bsModelParameters.Rate1,
        r2 = bsModelParameters.Rate2;
      double fwdPrice = spot * Math.Exp((r2 - r1) * time);
      return SplineInterpolationSmile.Create(
        fwdPrice, interplationSpace, interp, data);
    }
    #endregion

    #region Quadratic regression smile builder

    private const double SigmaAtmFactor = 1.5;

    /// <summary>
    ///   Builds the Black-Scholes volatility smile based on quadratic regression plus error corrections.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="bsModelParameters">The parameter.</param>
    /// <param name="errorCorrectionInterp">The interpolation method for error corrections.</param>
    /// <returns>A function taking the strike as the argument and returning the volatility.</returns>
    public static QuadraticRegressionSmile BuildQuadraticRegressionSmile(
      this IEnumerable<StrikeVolatilityPair> data,
      IBlackScholesParameterData bsModelParameters,
      Interp errorCorrectionInterp)
    {
      double time = bsModelParameters.Time,
        spot = bsModelParameters.Spot,
        r1 = bsModelParameters.Rate1,
        r2 = bsModelParameters.Rate2;
      double fwdPrice = spot * Math.Exp((r2 - r1) * time);
      return QuadraticRegressionSmile(data, fwdPrice, time, errorCorrectionInterp);
    }

    /// <summary>
    /// Builds the Black-Scholes volatility smile based on quadratic regression plus error corrections.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="fwdPrice">The ATM forward price.</param>
    /// <param name="time">The time to expiry.</param>
    /// <param name="errorCorrectionInterp">The interpolation method for error corrections.</param>
    /// <returns>A function taking the strike as the argument and returning the volatility.</returns>
    /// <remarks>The smile interpolation has the following functional form:<math>
    /// \sigma(K) = a\,\Phi\left(d_a(K)\right)^2 + b\,\Phi\left(d_a(K)\right) + c
    /// + \epsilon\left(\log(K/F)\right)
    /// </math>where
    /// <list type="bullet">
    /// 		<item><description><m>K</m> is the strike;</description></item>
    /// 		<item><description><m>F = S \exp((r_2-r_1)T)</m> is the forward price;</description></item>
    /// 		<item><description><m>\Phi(\cdot)</m> is the standard normal distribution function;</description></item>
    /// 		<item><description><m>\displaystyle d_a(K) = \frac{\log(F/K)}{\sigma_{ref}\sqrt{T}}</m>;</description></item>
    /// 		<item><description><m>\sigma_{ref} = 1.5\sigma_{atm}</m>, where <m>\sigma_{atm}</m> is the input volatility with the strike closest to <m>F</m>;</description></item>
    /// 		<item><description><m>a</m>, <m>b</m> and <m>c</m> are the calibrated parameters obtained by linear least squares regression;</description></item>
    /// 		<item><description><m>\epsilon(\cdot)</m> is an interpolation functions for error corrections.</description></item>
    /// 	</list>
    /// This function first estimate the parameters <c>(a, b, c)</c> and then sets up the error corrections such that the volatilities
    /// match exactly at all the input data points.</remarks>
    internal static QuadraticRegressionSmile QuadraticRegressionSmile(
      this IEnumerable<StrikeVolatilityPair> data,
      double fwdPrice, double time,
      Interp errorCorrectionInterp)
    {
      var pairs = new List<StrikeVolatilityPair>();
      double refSigma = SigmaAtmFactor*FindAtmVolatility(data, fwdPrice, pairs);
      double sqrtTime = Math.Sqrt(time),
        refSigmaT = refSigma * sqrtTime,
        logFwd = Math.Log(fwdPrice);

      // Precalculates all the relevant variables.
      var dataPoints = pairs.Select(p =>
      {
        var logKoverF = Math.Log(p.Strike) - logFwd;
        return new
        {
          M = logKoverF,
          N = SpecialFunctions.NormalCdf(-logKoverF / refSigmaT),
          V = p.Volatility
        };
      }).ToList();

      // Run regression to get the coefficients.
      var regression = dataPoints.Aggregate(new TwoVariableLinearRegression(),
        (r, d) =>
        {
          double yi = d.V;
          double xi1 = d.N;
          double xi2 = xi1 * xi1;
          r.Add(yi, xi1, xi2);
          return r;
        }).GetResult();
      double a = regression.Beta2, b = regression.Beta1, c = regression.Beta0;

      // Create error correction interpolator.
      // We add two end points such that the error correction goes to zero
      //  at both far ends.
      var interpolator = dataPoints.Select(
        d => new KeyValuePair<double, double>(
          d.M, d.V - (a * d.N + b) * d.N - c)
        )
        .OrderBy(p => p.Key)
        .ToList()
        .SetUpErrorCorrector(-8 * refSigmaT, 0, 8 * refSigmaT, 0,
          errorCorrectionInterp);

      // Return the interpolator as a delegate.
      return new QuadraticRegressionSmile(a, b, c,
        logFwd, refSigmaT, sqrtTime,
        interpolator.evaluate);
    }

    /// <summary>
    /// Sets up error corrector, optionally add two end points.
    /// </summary>
    /// <param name="orderedPoints">The ordered points.</param>
    /// <param name="minX">The min X.</param>
    /// <param name="minY">The min Y.</param>
    /// <param name="maxX">The max X.</param>
    /// <param name="maxY">The max Y.</param>
    /// <param name="interp">The interp.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    private static Interpolator SetUpErrorCorrector(
      this IList<KeyValuePair<double, double>> orderedPoints,
      double minX, double minY, double maxX, double maxY,
      Interp interp)
    {
      var n = orderedPoints.Count - 1;
      double x0 = orderedPoints[0].Key, xn = orderedPoints[n].Key;
      bool pad0 = false, padn = false;
      if (minX < x0)
      {
        if (x0 < 0) minX = Math.Max(minX, 2 * x0);
        pad0 = true;
        ++n;
      }
      if (maxX > xn)
      {
        if (xn > 0) maxX = Math.Min(maxX, 2 * xn);
        padn = true;
        ++n;
      }
      double[] x = new double[n + 1], y = new double[n + 1];
      int first = 0, last = n;
      if (pad0)
      {
        x[0] = minX;
        y[0] = minY;
        ++first;
      }
      if (padn)
      {
        x[n] = maxX;
        y[n] = maxY;
        --last;
      }
      for (int i = first; i <= last; ++i)
      {
        x[i] = orderedPoints[i - first].Key;
        y[i] = orderedPoints[i - first].Value;
      }
      return new Interpolator(interp ?? new Linear(
        new Const(), new Const()), x, y);
    }

    /// <summary>
    ///   Finds the ATM volatility, i.e., the volatility associated with the ATM strike.
    ///   As a side effect, it also fills a list of <see cref="StrikeVolatilityPair" /> to avoid
    ///   iterating through the <see cref="ISmileDataProvider" /> again.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="forwardPrice">The forward price.</param>
    /// <param name="list">The list.</param>
    /// <returns>The reference volatility.</returns>
    /// <remarks>
    ///   Instead of finding the exact ATM strike, this function returns the volatility
    ///   of the strike which is closest to the forward price in the data points.
    /// </remarks>
    private static double FindAtmVolatility(
      IEnumerable<StrikeVolatilityPair> data,
      double forwardPrice,
      ICollection<StrikeVolatilityPair> list)
    {
      Debug.Assert(list != null);
      double distance = Double.MaxValue, atmVolatility = 0;
      foreach (StrikeVolatilityPair pair in data)
      {
        double d = Math.Abs(pair.Strike - forwardPrice);
        if (d < distance)
        {
          distance = d;
          atmVolatility = pair.Volatility;
        }
        list.Add(pair);
      }
      return atmVolatility;
    }

    #endregion

    #region Volatility surface builder

    /// <summary>
    ///   Builds the Black-Scholes volatility surface as a bi-variate function.
    /// </summary>
    /// <param name="smilesByDates">The smiles by dates.</param>
    /// <param name="timeInterp">The time interpolation method.</param>
    /// <returns>A function <c>f</c> such that <c>f(time,strike)</c>
    /// gives the volatility at the specified strike and time to expiry.</returns>
    /// <remarks>
    /// </remarks>
    public static Func<double, double, SmileInputKind, double> BuildBlackScholesSurface(
      this IEnumerable<KeyValuePair<double, Func<double, SmileInputKind, double>>> smilesByDates,
      Interp timeInterp)
    {
      var data = smilesByDates.Where(p => p.Value != null).ToArray();
      if (data.Length == 0) return null;
      return new SequentialInterpolator2D(
        data.Select(d => d.Value).ToArray(),
        data.Select(d => d.Key).ToArray(),
        timeInterp ?? new SquareLinearVolatilityInterp()).Evaluate;
    }

    /// <summary>
    ///   Convert an interpolation function to a volatility surface interpolator
    ///   by combining it with a year fraction calculator.
    /// </summary>
    /// <param name="interpolationFn">The interpolation function <c>f</c> such that <c>f(time,strike)</c>
    /// gives the volatility at the specified strike and time to expiry.</param>
    /// <param name="fractionFn">The function to calculate the time in years between two dates.</param>
    /// <returns>An new instance of <see cref="VolatilitySurfaceInterpolator" />.</returns>
    /// <remarks>
    /// </remarks>
    public static VolatilitySurfaceInterpolator ToSurfaceInterpolator(
      this Func<double, double, SmileInputKind, double> interpolationFn,
      Func<Dt, Dt, double> fractionFn)
    {
      return new VolatilitySurfaceInterpolator(fractionFn, interpolationFn);
    }

    #endregion

    #region ParameterData manipulation

    private static readonly Func<Dt, Dt, double> GetTimeFn
      = (b, e) => Dt.RelativeTime(b, e);

    public static IBlackScholesParameterData GetParameters(
      Dt asOf, Dt date, Func<Dt,Dt,double> getTime, 
      double spot, IFactorCurve curve1, IFactorCurve curve2,
      double shift = 0)
    {
      if (getTime == null) getTime = GetTimeFn;
      var time = getTime(asOf, date);
      var rate1 = CalculateRate(asOf, date, time, curve1);
      var rate2 = CalculateRate(asOf, date, time, curve2);
      return new BlackScholesParameterData(time, spot, rate1, rate2, shift);
    }

    private static double CalculateRate(Dt asOf, Dt date, double time, IFactorCurve curve)
    {
      if (curve == null || time < 1E-15) return 0.0;
      var priceChange = curve.Interpolate(asOf, date);
      return -Math.Log(priceChange) / time;
    }

    #endregion

    internal static double GetShiftValue(this IVolatilityObject v)
    {
      if (v?.DistributionType != DistributionType.ShiftedLogNormal)
        return 0.0;
      var svp = ((v as CalibratedVolatilitySurface)?.Calibrator
        as IShiftValueProvider) ?? (v as IShiftValueProvider);
      return svp?.ShiftValue ?? 0.0;
    }
  }
}

