using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM.Native;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using Correlation = BaseEntity.Toolkit.Models.BGM.BgmCorrelation;

namespace BaseEntity.Toolkit.Models.BGM
{
  public abstract class BgmCalibrations: Native.BgmCalibrations
  {
    #region Nested type: Result

    private class Result : IForwardVolatilityInfo
    {
      #region IForwardVolatilityInfo Members

      public VolatilityCurve[] ForwardVolatilityCurves { get; set; }

      public DistributionType DistributionType { get; set; }

      public BgmCorrelation Correlation { get; set; }

      public Dt[] ResetDates { get; set; }

      #endregion
    }

    #endregion

    #region Nested type: Swap volatility data

    struct SwapVolatilityData
    {
      public readonly Dt[] TenorDates;
      public readonly double[] TenorTimes;
      public readonly double[] DiscountFactors;
      public readonly int[] ExpiryIndices;
      public readonly SwapVolatilityInfo[] Points;

      public SwapVolatilityData(
        DateCalculator date,
        DiscountCurve discountCurve,
        string[] expirySpecs,
        string[] tenorSpecs,
        double[,] volatilities)
      {
        var expiryMonths = expirySpecs.Select(DateCalculator.TenorToMonths).ToArray();
        var tenorMonths = tenorSpecs.Select(DateCalculator.TenorToMonths).ToArray();
        var fullTenors = expiryMonths
          .SelectMany(e => tenorMonths.Select(t => t + e))
          .Concat(expiryMonths).OrderBy(t => t).Distinct().ToArray();
        int rateCount = fullTenors.Length - 1;
        if (rateCount <= 0)
        {
          throw new ArgumentException("No volatility data");
        }

        var firstExpiry = date.AddMonths(fullTenors[0]);
        var times = new double[rateCount + 1];
        var dfs = new double[rateCount];

        var resetDates = new Dt[rateCount + 1];
        var dt = resetDates[0] = date.AddMonths(fullTenors[0]);
        times[0] = date.ToTime(dt);
        for (int i = 1; i <= rateCount; ++i)
        {
          dt = resetDates[i] = date.AddMonths(fullTenors[i]);
          times[i] = date.ToTime(dt);
          dfs[i - 1] = discountCurve.Interpolate(firstExpiry, dt);
        }

        int m = expiryMonths.Length, n = tenorMonths.Length, len = m * n;
        var expiries = new int[m];
        var maturityIndices = new int[len];
        for (int i = 0; i < m; ++i)
        {
          int expiry = expiryMonths[i];
          var idx0 = Array.BinarySearch(fullTenors, expiry);
          if (idx0 < 0)
            throw new InvalidOperationException("public error");
          expiries[i] = idx0;

          int baseIdx = i * n;
          for (int j = 0; j < n; ++j)
          {
            var maturity = tenorMonths[j] + expiry;
            var idx = Array.BinarySearch(fullTenors, maturity) - 1;
            if (idx < idx0)
              throw new InvalidOperationException("public error");
            maturityIndices[baseIdx + j] = idx;
          }
        }
        var points = Enumerable.Range(0, len)
          .OrderBy(i => maturityIndices[i])
          .ThenBy(i => i)
          .Select(i => new SwapVolatilityInfo
          {
            First = expiries[i / n],
            Last = maturityIndices[i],
            Volatility = volatilities[i / n, i % n]
          })
          .Where(s => s.Volatility > 0)
          .ToArray();

        TenorDates = resetDates;
        TenorTimes = times;
        DiscountFactors = dfs;
        ExpiryIndices = expiries;
        Points = points;
      }
    }
    #endregion

    #region Cascading
    public static IForwardVolatilityInfo CascadingCalibrate(
      Dt asOf,
      DiscountCurve discountCurve,
      string[] expirySpecs, string[] tenorSpecs,
      CycleRule rule, BDConvention bdc, Calendar cal,
      BgmCorrelation correlation,
      double[,] volatilities,
      DistributionType volatilityModel)
    {
      var result = new Result
      {
        DistributionType = volatilityModel,
        Correlation = correlation ?? Correlation.CreateBgmCorrelation(
          BgmCorrelationType.PerfectCorrelation, 1, new double[0, 0])
      };
      CascadingCalibrate(new DateCalculator(asOf, rule, bdc, cal),
        discountCurve, expirySpecs, tenorSpecs, volatilities,
        result);
      return result;
    }

    private static void CascadingCalibrate(
      DateCalculator date,
      DiscountCurve discountCurve,
      string[] expirySpecs, string[] tenorSpecs,
      double[,] volatilities,
      Result result)
    {
      // For the time being, only lognormal is supported
      if (result.DistributionType != DistributionType.LogNormal)
      {
        throw new NotSupportedException(String.Format(
          "{0} is not supported yet", result.DistributionType));
      }

      var d = new SwapVolatilityData(date, discountCurve,
        expirySpecs, tenorSpecs, volatilities);
      CascadeCalibrateLogNormal(d.TenorTimes, d.DiscountFactors,
        result.Correlation, d.Points);

      var resetDates = result.ResetDates = d.TenorDates;
      int rateCount = resetDates.Length - 1;
      var curves = new PiecewiseFlatVolatilityCurveBuilder[rateCount];
      for (int i = 0; i < rateCount; ++i)
        curves[i] = new PiecewiseFlatVolatilityCurveBuilder(date.AsOf);

      var points = d.Points;
      var expiryIndices = d.ExpiryIndices;
      int m0 = points[0].First, n0 = points[0].Last;
      double sigma = points[0].Volatility;
      for (int pos = 0, i = 0; i < rateCount; ++i)
      {
        for (int j = 0; j < expiryIndices.Length; ++j)
        {
          var e = expiryIndices[j];
          if (e > i) break;

          curves[i].AddVolatility(resetDates[e], sigma);
          if (e != m0 || i != n0) continue;

          if (++pos >= points.Length) goto out_of_loop;
          m0 = points[pos].First;
          n0 = points[pos].Last;
          sigma = points[pos].Volatility;
        }
      }
    out_of_loop:
      result.ForwardVolatilityCurves = curves.Select(b => b.GetCurve()).ToArray();
    }
    #endregion

    #region Piecewise constant fit
    public static IForwardVolatilityInfo PiecewiseConstantFit(
      bool asFunctionOfLength,
      bool calibrateCorrelation,
      double tolerance,
      double[] shapeControls,
      Dt asOf,
      DiscountCurve discountCurve,
      string[] expirySpecs, string[] tenorSpecs,
      CycleRule rule, BDConvention bdc, Calendar cal,
      BgmCorrelation correlation,
      double[,] volatilities,
      DistributionType volatilityModel)
    {
      var result = new Result
      {
        DistributionType = volatilityModel,
        Correlation = correlation ?? Correlation.CreateBgmCorrelation(
          BgmCorrelationType.PerfectCorrelation, 1, new double[0, 0])
      };

      int modelChoice = 0;
      var method = VolatilityBootstrapMethod.PiecewiseFitTime;
      if (asFunctionOfLength)
      {
        modelChoice |= 1;
        method = VolatilityBootstrapMethod.PiecewiseFitLength;
      }
      if (calibrateCorrelation)
        modelChoice |= 2;

      Calibrate(modelChoice, tolerance, shapeControls,
        new DateCalculator(asOf, rule, bdc, cal),
        discountCurve, expirySpecs, tenorSpecs, volatilities,
        result);
      return result;
    }

    private static void Calibrate(
      int modelChoice,
      double tolerance,
      double[] shapeControls,
      DateCalculator date,
      DiscountCurve discountCurve,
      string[] expirySpecs,
      string[] tenorSpecs,
      double[,] volatilities,
      Result result)
    {
      // For the time being, only lognormal is supported
      if (result.DistributionType != DistributionType.LogNormal)
      {
        throw new NotSupportedException(String.Format(
          "{0} is not supported yet", result.DistributionType));
      }

      var d = new SwapVolatilityData(date, discountCurve,
        expirySpecs, tenorSpecs, volatilities);
      var parameters = new double[d.DiscountFactors.Length,
        result.Correlation == null ? 3 : 2];
      PiecewiseConstantFitLogNormal(modelChoice, tolerance,
        shapeControls, d.TenorTimes, d.DiscountFactors,
        result.Correlation, d.Points, parameters);
      result.ResetDates = d.TenorDates;
      GetTimeFittedResults(date.AsOf, d.ExpiryIndices, parameters, result);
    }

    private static void GetTimeFittedResults(
      Dt asOf, int[] expiryIndices, double[,] parameters,
      Result result)
    {
      var resetDates = result.ResetDates;
      int expiryCount = expiryIndices.Length;
      int rateCount = resetDates.Length - 1;
      var curves = new VolatilityCurve[rateCount];
      for (int i = 0; i < rateCount; ++i)
      {
        var builder = new PiecewiseFlatVolatilityCurveBuilder(asOf);
        var phi = parameters[i, 1];
        for (int j = 0; j < expiryCount; ++j)
        {
          var e = expiryIndices[j];
          if (e > i) break;
          var sigma = phi * parameters[j, 0];
          builder.AddVolatility(resetDates[e], sigma);
        }
        curves[i] = builder.GetCurve();
      }
      result.ForwardVolatilityCurves = curves;
      if (parameters.GetLength(1) <= 2) return;

      result.Correlation = BgmCorrelation.CreateBgmCorrelation(
        BgmCorrelationType.SchoenmakersCoffey3Params, rateCount,
        new[,] { { parameters[0, 2], parameters[1, 2], parameters[2, 2] } });
    }

    #endregion
  }

  public static class BgmCalibrationExternsions
  {
    #region Forward Forward Volatility data
    /// <summary>
    /// Compute the forward instantaneous volatilities of the underlying libor rates 
    /// </summary>
    /// <returns>Instantaneous forward volatilities</returns>
    /// <remarks>retVal[i,j] = <m>\sigma_i(t)</m> for <m>t \in [T_{j-1},T_j]</m> with <m>T_{-1} =</m> asOf date, where i is the index of the libor rate</remarks>
    public static double[,] GetForwardVolatilities(
      this IForwardVolatilityInfo fwdVol)
    {
      var old = fwdVol as BgmCalibratedVolatilities;
      if (old != null) return old.GetForwardVolatilities();

      if (fwdVol == null) return null;
      var resets = fwdVol.ResetDates;
      if (resets == null || resets.Length < 2) return null;
      var count = resets.Length - 1;
      var curves = fwdVol.ForwardVolatilityCurves;
      if (curves == null || curves.Length != count)
        throw new ToolkitException("Inconsisten volatility object");
      var retVal = new double[count, count];
      for (int i = 0; i < count; ++i)
      {
        var curve = curves[i];
        if (curve == null)
          throw new ToolkitException("null curve");
        for (int j = 0; j <= i; ++j)
        {
          retVal[i, j] = curve.Interpolate(resets[j]);
        }
      }
      return retVal;
    }

    #endregion
  }

  #region Helper class: DateCalculator
  public class DateCalculator
  {
    public readonly Dt AsOf;
    private readonly CycleRule _rule;
    private readonly BDConvention _bdc;
    private readonly Calendar _cal;
    const double DaysPerYear = 365.0;

    public DateCalculator(
      Dt asOf, CycleRule rule,
      BDConvention bdc, Calendar cal)
    {
      AsOf = asOf;
      _rule = rule;
      _bdc = bdc;
      _cal = cal;
    }

    public Dt AddMonths(int months)
    {
      var dt = Dt.AddMonths(AsOf, months, _rule);
      return _bdc != BDConvention.None
        ? Dt.Roll(dt, _bdc, _cal)
        : dt;
    }

    public double ToTime(Dt date)
    {
      return (date - AsOf) / DaysPerYear;
    }

    public static int TenorToMonths(string tenor)
    {
      Tenor t = Tenor.Parse(tenor);
      int m = 0;
      switch (t.Units)
      {
      case TimeUnit.Years:
        m = 12;
        break;
      case TimeUnit.Months:
        m = 1;
        break;
      default:
        throw new NotSupportedException(
          "Tenors shorter than a month not supported");
      }
      return m * t.N;
    }
  }

  #endregion

  #region Helper class: PiecewiseFlatVolatilityCurveBuilder
  class PiecewiseFlatVolatilityCurveBuilder
  {
    private static readonly Interp _interp = new Flat(1.0);

    private readonly VolatilityCurve _curve;
    private double _lastV;
    private Dt _lastD;
    public PiecewiseFlatVolatilityCurveBuilder(Dt asOf)
    {
      _curve = new VolatilityCurve(asOf) { Interp = _interp };
      _lastV = -1;
    }

    public void AddVolatility(Dt dt, double vol)
    {
      if (_lastV > 0 && !_lastV.AlmostEquals(vol))
      {
        _curve.AddVolatility(_lastD, _lastV);
      }
      _lastV = vol;
      _lastD = dt;
    }

    public VolatilityCurve GetCurve()
    {
      if (_lastV > 0) _curve.AddVolatility(_lastD, _lastV);
      _curve.Fit();
      _curve.VerifyFlatCurveLeftContinous();
      _curve.Validate();
      return _curve;
    }
  }
  #endregion
}
