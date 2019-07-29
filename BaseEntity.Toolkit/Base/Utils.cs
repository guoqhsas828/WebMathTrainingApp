/*
 * Utils.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  enum TenorNameRound
  {
    None, Month, Year, Single
  }

	/// <summary>
	///   Commonly used small utility functions
	/// </summary>
    /// <exclude />
  public static class Utils
  {
    /// <summary>
    ///   Get an array of credit names from survival curves
    /// </summary>
    /// <param name="survivalCurves">Survival curves</param>
    /// <returns>Array of names</returns>
    /// <exclude />
    public static string[] GetCreditNames(SurvivalCurve[] survivalCurves)
    {
      string[] names = new string[survivalCurves.Length];
      for (int i = 0; i < survivalCurves.Length; i++)
        names[i] = survivalCurves[i].Name;
      return names;
    }

    /// <summary>
    ///   Scale an array proportionally such that the abosulute values
    ///   of all the non-zero elements are bigger than a minimum level.
    /// </summary>
    /// <param name="a">The array</param>
    /// <param name="minimum">The minimum value</param>
    /// <exclude />
    public static void ScaleUp(double[] a, double minimum)
    {
      // for safety
      if (a == null || a.Length == 0) return;

      // make sure the minimum level is positive
      if (minimum < 0) minimum = -minimum;

      // find the scaling factor
      double s = 1.0;
      for (int i = 0; i < a.Length; ++i)
      {
        double aa = Math.Abs(a[i]);
        if (aa < minimum && aa > 1E-16)
          s = Math.Max(s, minimum / aa);
      }
      if (s <= 1.0) return; // no need to scale up

      // scale the array by s
      for (int i = 0; i < a.Length; ++i)
        a[i] *= s;
      return;
    }

    /// <summary>
    ///   Scale an array proportionally such that the elements
    ///   sum up to a given value
    /// </summary>
    /// <param name="a">The array to scale</param>
    /// <param name="sum">Target sum to match</param>
    /// <exclude />
    public static void Normalize(double[] a, double sum)
    {
      double asum = 0;
      for (int i = 0; i < a.Length; ++i)
        asum += a[i];
      if (Math.Abs(asum) < 1E-16)
        throw new ArgumentException("Weights sum is near zero.");
      double scale = sum / asum;
      for (int i = 0; i < a.Length; ++i)
        a[i] *= scale;
      return;
    }

    /// <summary>
    ///   Create a tenor name for the period [effective, maturity]
    /// </summary>
    /// <param name="effective">Start date</param>
    /// <param name="maturity">End date</param>
    /// <param name="round">Round the tenor name to years or months</param>
    /// <returns>Tenor name</returns>
    /// <exclude />
    public static string ToTenorName(Dt effective, Dt maturity, bool round)
    {
      return ToTenorName(effective, maturity, TenorNameRound.Single);
    }

    internal static string ToTenorName(Dt effective, Dt maturity, TenorNameRound round)
    {
      if (effective >= maturity)
        throw new ArgumentException("Maturity must be after the effective date");

      string tenorName = null;
      int y = maturity.Year - effective.Year;
      int m = y * 12 + maturity.Month - effective.Month;
      if (m >= 12)
      {
        tenorName += String.Format("{0}Y", m / 12);
        if (round == TenorNameRound.Year || round == TenorNameRound.Single)
          return tenorName;
        m %= 12;
      }
      if (m > 0)
      {
        tenorName += String.Format("{0}M", m);
        if (round == TenorNameRound.Month || round == TenorNameRound.Single)
          return tenorName;
      }
      if (tenorName == null)
      {
        int d = Dt.FractionDays(effective, maturity, DayCount.Actual365Fixed);
        tenorName = String.Format("{0}D", d);
      }
      return tenorName;
    }

    /// <summary>
    ///   Round a double array
    /// </summary>
    static public double[] Round(double[] a, int digits)
    {
      for (int i = 0; i < a.Length; ++i)
        a[i] = (double)Math.Round((decimal)a[i], digits);

      return a;
    }

    /// <summary>
    ///   Parse a number with optional unit "%" or "bp"
    /// </summary>
    /// <param name="input">Input</param>
    /// <returns>BumpSize object</returns>
    public static BumpSize GetBumpSize(object input)
    {
      BumpSize b = new BumpSize();
      if (input is double)
      {
        b.Size = (double)input;
      }
      else
      {
        string s = input.ToString().Trim();
        int pos;
        if ((pos = s.IndexOf('u')) >= 0)
        {
          s = s.Remove(pos).Trim();
          b.Unit = BumpUnit.Natural;
        }
        else if ((pos = s.IndexOf("bp")) >= 0)
        {
          s = s.Remove(pos).Trim();
          b.Unit = BumpUnit.Percentage;
        }
        else if ((pos = s.IndexOf('%')) >= 0 || (pos = s.IndexOf("pc")) >= 0)
        {
          s = s.Remove(pos).Trim();
          b.Unit = BumpUnit.Percentage;
        }
        b.Size = Double.Parse(s);
      }
      return b;
    }

    /// <summary>
    ///   Round a double matrix
    /// </summary>
    static public double[,] Round(double[,] a, int digits)
    {
      for (int i = 0; i < a.GetLength(0); ++i)
        for (int j = 0; j < a.GetLength(1); ++j)
          a[i, j] = (double)Math.Round((decimal)a[i, j], digits);

      return a;
    }

    /// <summary>
    ///   Scale an array of doubles by a factor
    /// </summary>
    static public double[] Scale(double[] a, double factor)
    {
      if (a != null)
        for (int i = 0; i < a.Length; ++i)
          a[i] = a[i] * factor;
      return a;
    }

    /// <summary>
    ///   Return false if the number is NaN, Positive or Negative infinity, or true otherwise.
    /// </summary>
    public static bool IsFinite(double x)
    {
      if (Double.IsNaN(x) || Double.IsInfinity(x))
        return false;
      return true;
    }
  }
}
