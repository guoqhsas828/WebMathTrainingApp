using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Represent effects of season on Inflation/Commodities 
  /// </summary>
  public class SeasonalityEffect
  {
    private readonly List<Dt> dts_;
    private readonly List<double> pts_;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asof">As of date </param>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <param name="indexMultipliers">Index multipliers</param>
    public SeasonalityEffect(Dt asof, Dt start, Dt end, double[] indexMultipliers)
    {
      dts_ = new List<Dt>();
      pts_ = new List<double>();
      var normalizedMultipliers = Normalize(indexMultipliers);
      Dt s = asof;
      while (s <= end)
      {
        dts_.Add(s);
        pts_.Add((s < start) ? 1.0 : normalizedMultipliers[s.Month - 1]);
        s = (dts_.Count == 1) ? Dt.AddMonth(new Dt(1, s.Month, s.Year), 1, false) : Dt.AddMonth(s, 1, false);
      }
      s = Dt.AddMonth(dts_[dts_.Count - 1], 1, false);
      dts_.Add(s);
      pts_.Add(1.0);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asof">As of date</param>
    /// <param name="dts">Dates</param>
    /// <param name="indexMultipliers">Index multipliers</param>
    public SeasonalityEffect(Dt asof, Dt[] dts, double[] indexMultipliers)
    {
      if (dts.Length != indexMultipliers.Length)
        throw new ArgumentException("ExplicitSeasonalityIndex needs same number of dates as index factors");

      int remainder;
      Math.DivRem(indexMultipliers.Length, 12, out remainder);
      if (remainder != 0)
        throw new ArgumentException("ExplicitSeasonalityIndex must be built from multiples of 12 factors");
      dts_ = new List<Dt>();
      pts_ = new List<double>();
      var dt0 = new Dt(1, dts[0].Month, dts[0].Year);
      Dt s = asof;
      while (s < dt0)
      {
        dts_.Add(s);
        pts_.Add(1.0);
        s = (dts_.Count == 1) ? Dt.AddMonth(new Dt(1, s.Month, s.Year), 1, false) : Dt.AddMonth(s, 1, false);
      }
      for (int i = 0; i < indexMultipliers.Length; i++)
      {
        s = Dt.AddMonth(dt0, i, false);
        if (i > 0 && !FollowsOnFrom(dts_[dts_.Count - 1], s))
          throw new ArgumentException(
            String.Format("ExplicitSeasonalityIndex date {0} must be followed by a date in the next month", dts_[dts_.Count - 1]));
        if (s >= asof && s > dts_[dts_.Count - 1])
        {
          dts_.Add(s);
          pts_.Add(indexMultipliers[i]);
        }
      }
      s = Dt.AddMonth(dts_[dts_.Count - 1], 1, false);
      dts_.Add(s);
      pts_.Add(1.0);
    }


    /// <summary>
    /// Returns an overlay curve for seasonality adjustment
    /// </summary>
    /// <returns>Overlay curve to account for seasonality adjustment</returns>
    public Curve SeasonalityAdjustment()
    {
      var retVal = new Curve(dts_[0], DayCount.None, Frequency.Continuous) {Interp = InterpFactory.FromMethod(InterpMethod.Flat, ExtrapMethod.Const)};
      retVal.Add(dts_.ToArray(), pts_.ToArray());
      return retVal;
    }

    /// <summary>
    /// Perturb seasonality curve
    /// </summary>
    /// <param name="month">Month to perturb</param>
    /// <param name="adjustment">Original adjustment</param>
    /// <param name="bump">Bump size. The bump is multiplicative, i.e. adj[i] = adj[i](1 + bump)</param>
    /// <returns>A new seasonality adjustment curve perturbed by bump at month</returns>
    public static Curve PerturbSeasonalityAdjustment(Month month, Curve adjustment, double bump)
    {
      var retVal = (Curve)adjustment.Clone();
      for (int i = 0; i < retVal.Count; i++)
      {
        if (retVal.GetDt(i).Month == (int)month)
        {
          double v = retVal.GetVal(i);
          retVal.SetVal(i, v * (1 + bump) / (Math.Pow(1 + bump, 1 / 12)));
        }
      }
      return retVal;
    }

    /// <summary>
    /// Normalize the seasonality factors
    /// </summary>
    /// <param name="multipliers">Multipliers</param>
    /// <returns>An array of normalized inflation factors</returns>
    private static double[] Normalize(double[] multipliers)
    {
      int remainder;
      Math.DivRem(multipliers.Length, 12, out remainder);
      if (remainder != 0)
        throw new ArgumentException("Seasonality indices need to have element counts in multiples of 12 (months)");
      var newMultipliers = new double[multipliers.Length];
      for (int i = 0; i < multipliers.Length; i += 12)
      {
        double product = 1.0;
        for (int j = i; j < i + 12; j++)
          product *= multipliers[j];
        const double scalePower = 1.0 / 12.0;
        for (int j = i; j < i + 12; j++)
          newMultipliers[j] = multipliers[j] / Math.Pow(product, scalePower);
      }
      return newMultipliers;
    }

    private static bool FollowsOnFrom(Dt d1, Dt d2)
    {
      return ((d2.Month % 12) == ((d1.Month + 1) % 12) &&
              (d2.Year == ((d2.Month == 1)
                             ? d1.Year + 1
                             : d1.Year)));
    }
  }
}