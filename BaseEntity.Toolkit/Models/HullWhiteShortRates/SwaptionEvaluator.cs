/*
 * 
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  public class SwaptionsEvaluator
  {
    public SwaptionsEvaluator(
      SwaptionDataSet data,
      IReadOnlyList<int> kapaDates = null,
      IReadOnlyList<int> sigmaDates = null)
    {
      Debug.Assert(data != null && data.Swaptions.Count > 0);

      Data = data;
      _sigmaDates = (sigmaDates != null && sigmaDates.Count > 0)
        ? sigmaDates : data.Swaptions.Select(s => s.Expiry)
          .OrderBy(i => i).Distinct().ToArray();
      _kapaDates = kapaDates;

      var count = Data.Times.Count;
      _meanReversions = new double[count];
      var sigmas = _sigmas = new double[count];
      var sigma0 = 0.005;
      for (int i = 0; i < count; ++i)
        sigmas[i] = sigma0;
      _calc = PiecewiseConstantCalculator.Create(
        Data.Times, sigmas, _meanReversions);

      _arrayA = new double[count];
      _arrayB = new double[count];
      _arrayC = new double[count];
      var solver = _solver = new Brent2();
      solver.setToleranceF(1E-15);
      solver.setToleranceX(1E-15);
    }

    /// <summary>
    ///  Way to input the sigma and mean reversion levels directly.
    /// </summary>
    /// <param name="sigmaSigmaTimes"></param>
    /// <param name="sigmas"></param>
    /// <param name="meanRevTimes"></param>
    /// <param name="meanReversions"></param>
    internal SwaptionsEvaluator(double[] sigmaSigmaTimes, double[] sigmas, double[] meanRevTimes, double[] meanReversions)
    {
      _sigmaTimes = sigmaSigmaTimes;
      _meanRevTimes = meanRevTimes;
      _meanReversions = meanReversions;
      _sigmas = sigmas;
    }

    internal void Evaluate(IReadOnlyList<double> x,
      IList<double> f, IList<double> g)
    {
      Debug.Assert(g == null || g.Count == 0);

      // Set mean reversion values
      SetMeanReversions(x);
      var a = _meanReversions;

      // Set sigma values
      Debug.Assert(SigmaStart + _sigmaDates.Count == x.Count);

      var sigmas = _sigmas;
      InterpolateValues(_sigmaDates, x, SigmaStart, sigmas);

      // Calculate the swaption values
      var calc = _calc;
      var times = Data.Times;
      calc.Initialize(times, sigmas, a);

      var zeroPrices = Data.ZeroPrices;
      var fractioons = Data.Fractions;
      var swpns = Data.Swaptions;
      var swapCount = swpns.Count;
      Debug.Assert(swapCount == f.Count);
      for (int i = 0; i < swapCount; ++i)
      {
        var swpn = swpns[i];
        var modelPv = calc.EvaluateSwaptionPayer(
          swpn.Expiry, swpn.Maturity, swpn.Strike,
          zeroPrices, times, fractioons, null,
          _solver, _arrayA, _arrayB, _arrayC);
        f[i] = (modelPv - swpn.MarketPv)/(1E-6 + swpn.MarketPv);
        Debug.Assert(!double.IsNaN(f[i]));
      }
    }

    public double ImplyVoaltility(
      SwaptionDataItem swpn, VolatilitySpec volspec,
      out double modelPv)
    {
      var calc = _calc;
      var times = Data.Times;
      var zeroPrices = Data.ZeroPrices;
      var fractioons = Data.Fractions;
      double swapRate;
      var annuity = CalculateSwapAnnuity(swpn.Expiry,
        swpn.Maturity, times, zeroPrices, out swapRate);
      modelPv = calc.EvaluateSwaptionPayer(
        swpn.Expiry, swpn.Maturity, swpn.Strike,
        zeroPrices, times, fractioons);
      //TODO: avoid in-the-money options
      return volspec.ImplyVolatility(
        true, modelPv/annuity,
        times[swpn.Expiry], swapRate, swpn.Strike);
    }

    public IEnumerable<double> GetModelPvs()
    {
      var calc = _calc;
      var times = Data.Times;
      var zeroPrices = Data.ZeroPrices;
      var fractioons = Data.Fractions;
      var swpns = Data.Swaptions;
      var swapCount = swpns.Count;
      for (int i = 0; i < swapCount; ++i)
      {
        var swpn = swpns[i];
        yield return calc.EvaluateSwaptionPayer(
          swpn.Expiry, swpn.Maturity, swpn.Strike,
          zeroPrices, times, fractioons);
      }
    }

    public IEnumerable<double> GetImpliedVolatilities(
      VolatilitySpec volspec)
    {
      var calc = _calc;
      var times = Data.Times;
      var zeroPrices = Data.ZeroPrices;
      var fractioons = Data.Fractions;
      var swpns = Data.Swaptions;
      var swapCount = swpns.Count;
      for (int i = 0; i < swapCount; ++i)
      {
        var swpn = swpns[i];
        double swapRate;
        var annuity = CalculateSwapAnnuity(swpn.Expiry,
          swpn.Maturity, times, zeroPrices, out swapRate);
        var price = calc.EvaluateSwaptionPayer(
          swpn.Expiry, swpn.Maturity, swpn.Strike,
          zeroPrices, times, fractioons)/annuity;
        //TODO: avoid in-the-money options
        yield return volspec.ImplyVolatility(true,
          price, times[swpn.Expiry], swapRate, swpn.Strike);
      }
    }

    #region Utility methods

    internal static double CalculateSwapAnnuity(
      int expiry, int maturity,
      IReadOnlyList<double> times,
      IReadOnlyList<double> zeroPrices,
      out double swapRate)
    {
      var annuity = 0.0;
      for (int i = expiry + 1; i <= maturity; ++i)
        annuity += (times[i] - times[i - 1])*zeroPrices[i];
      swapRate = (zeroPrices[expiry] - zeroPrices[maturity])/annuity;
      return annuity;
    }

    internal static double[] GetFractions(IReadOnlyList<double> times)
    {
      var count = times.Count;
      var fractions = new double[count];
      double t0 = 0;
      for (int i = 0; i < count; ++i)
      {
        var t = times[i];
        fractions[i] = t - t0;
        t0 = t;
      }
      return fractions;
    }

    #endregion

    #region Set parameter values

    /// <summary>
    /// Sets the mean reversions.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <remarks>
    ///  If no kappa date provided, it is determined by logistic function
    ///  <math>
    ///    \kappa_t = a_0 + \frac{a_1 - a_0}{1 + e^{a_2*(a_3 - t)}}
    ///  </math>
    /// </remarks>
    private void SetMeanReversions(IReadOnlyList<double> x)
    {
      if (_kapaDates != null)
      {
        InterpolateValues(_kapaDates, x, 0, _meanReversions);
        return;
      }

      var a = _meanReversions;
      var count = a.Length;
      var times = Data.Times;
      double a0 = x[0], da = x[1] - x[0], a2 = x[2], a3 = x[3];
      if (Math.Abs(da) < 2E-16)
      {
        for (int i = 0; i < count; ++i)
          a[i] = a0;
      }
      else
      {
        for (int i = 0; i < count; ++i)
          a[i] = a0 + da/(1 + Math.Exp(a2*(a3 - times[i])));
      }
    }

    private static void InterpolateValues(
      IReadOnlyList<int> xDates,
      IReadOnlyList<double> x, int xStart,
      double[] values)
    {
      // For now, perform simple flat interpolation.
      int i = 0, valueCount = values.Length;
      double value = x[xStart]/100;
      for (int k = 0, xCount = xDates.Count; k < xCount; ++k)
      {
        value = x[k + xStart]/100;
        int end = xDates[k];
        if (end >= valueCount)
          break;
        for (; i <= end; ++i)
          values[i] = value;
      }
      for (; i < valueCount; ++i)
        values[i] = value;
    }

    private static VolatilityCurve CreateVolatilityCurve(
      Dt asOf,
      IReadOnlyList<double> values,
      IReadOnlyList<double> times)
    {
      var curve = new VolatilityCurve(asOf)
      {
        Interp = new Flat(1E-15),
        IsInstantaneousVolatility = true,
      };

      Debug.Assert(values.Count == times.Count);
      for (int i = 0, n = times.Count; i < n; ++i)
        curve.Add(new Dt(asOf, times[i]), values[i]);

      return curve;
    }

    internal static IReadOnlyList<int> GetKapaDates(
      IReadOnlyList<int> sigmaDates,
      IReadOnlyList<SwaptionDataItem> swpns)
    {
      var minMaturity = swpns.Min(s => s.Maturity);
      var maxExpiry = swpns.Max(s => s.Expiry);
      return sigmaDates.Where(d => d >= minMaturity).Concat(
        swpns.Where(s => s.Expiry == maxExpiry).Select(s => s.Maturity))
        .OrderBy(i => i).Distinct().ToArray();
    }

    internal VolatilityCurve GetMeanReversionCurve(Dt asOf)
      => CreateVolatilityCurve(asOf, _meanReversions, Data?.Times ?? _meanRevTimes);
    internal VolatilityCurve GetVolatilityCurve(Dt asOf)
      => CreateVolatilityCurve(asOf, _sigmas, Data?.Times ?? _sigmaTimes);

    #endregion

    #region Parameter bounds

    internal double[] GetLowerBounds(CalibrationSettings options = null)
    {
      var lowerBonds = new double[DimensionX];
      GetKappaBounds(options?.MeanReversionLowerBounds,
        double.MinValue, lowerBonds);
      GetSigmaBounds(options?.VolatilityLowerBounds, 0.0, lowerBonds);
      return lowerBonds;
    }

    internal double[] GetUpperBounds(CalibrationSettings options = null)
    {
      var upperBonds = new double[DimensionX];
      GetKappaBounds(options?.MeanReversionUpperBounds,
        double.MaxValue, upperBonds);
      GetSigmaBounds(options?.VolatilityUpperBounds,
        double.MaxValue, upperBonds);
      return upperBonds;
    }


    internal double[] GetIntialPoints(double[] lower, double[] upper)
    {
      int start = SigmaStart, dimX = DimensionX;
      var initialX = new double[dimX];
      for (int i = 0; i < start; ++i)
      {
        initialX[i] = BoundValue(0, lower[i], upper[i]);
      }
      for (int i = start; i < dimX; ++i)
        initialX[i] = BoundValue(0.5, lower[i], upper[i]);
      return initialX;
    }

    private static double BoundValue(double value, double lower, double upper)
    {
      if (!(upper >= lower))
        throw new SolverException($"Upper bound ({upper}) must be grater than or equal to lower bound ({lower})");
      if (value < lower) value = lower;
      if (value > upper) value = upper;
      return value;
    }

    private void GetKappaBounds(Curve curve,
      double defaultValue, double[] bounds)
    {
      if (_kapaDates == null)
      {
        bounds[0] = bounds[1] = curve?.GetVal(0) ?? defaultValue;
        bounds[2] = double.MinValue;
        bounds[3] = double.MinValue;
        return;
      }

      var time = Data.Times;
      int end = SigmaStart;
      for (int i = 0; i < end; ++i)
      {
        var date = time[_kapaDates[i]]*365;
        bounds[i] = curve?.Interpolate(date) ?? defaultValue;
      }
      return;
    }

    private void GetSigmaBounds(Curve curve,
      double defaultValue, double[] bounds)
    {
      var time = Data.Times;
      int begin = SigmaStart, end = bounds.Length;
      for (int i = begin; i < end; ++i)
      {
        var date = time[_sigmaDates[i - begin]]*365;
        bounds[i] = curve?.Interpolate(date) ?? defaultValue;
      }
      return;
    }
    
    #endregion

    #region Properties and Data

    public int SigmaStart => _kapaDates?.Count ?? 4;

    public int DimensionX => SigmaStart + (_sigmaDates?.Count ?? 0);
    public int DimensionF => Data.Swaptions.Count;

    internal readonly SwaptionDataSet Data;
    private readonly PiecewiseConstantCalculator _calc;
    private readonly IReadOnlyList<int> _sigmaDates, _kapaDates;
    private readonly double[] _meanReversions, _sigmas;
    private readonly Solver _solver;
    private readonly double[] _arrayA, _arrayB, _arrayC;
    private readonly double[] _sigmaTimes;
    private readonly double[] _meanRevTimes;

    #endregion
  }

}
