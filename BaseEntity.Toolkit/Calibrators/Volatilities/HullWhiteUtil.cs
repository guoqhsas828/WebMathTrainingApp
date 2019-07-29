using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Utility class for hull-white calibration use
  /// </summary>
  public static class HullWhiteUtil
  {
    internal static VolatilityCurve UpdateCurve(
     VolatilityCurve curve, Dt asOf,
     Dt[] dates, double[] values)
    {
      if (curve == null)
      {
        curve = CreateVolatilityCurve(asOf);
      }
      curve.Set(dates, values);
      return curve;
    }

    internal static double[] CalcSpreads(double[] dfs, double[] pfs)
    {
      Debug.Assert(dfs != null && pfs != null);
      int n = dfs.Length, m = pfs.Length;
      Debug.Assert(n == m);

      var retVal = new double[n];
      for (int i = 0; i < n; i++)
      {
        var pci1 = i == 0 ? 1 : pfs[i - 1];
        var dci1 = i == 0 ? 1 : dfs[i - 1];
        retVal[i] = pci1 * dfs[i] / (pfs[i] * dci1);
      }
      return retVal;
    }

    internal static double[] CalcSpreadFactors(double[] dfs, double[] pfs)
    {
      Debug.Assert(dfs != null && pfs != null);
      int n = dfs.Length, m = pfs.Length;
      Debug.Assert(n == m);

      var retVal = new double[n];
      for (int i = 0; i < n; i++)
      {
        retVal[i] = pfs[i]/dfs[i];
      }
      return retVal;
    }

    internal static DiscountCurve GetDiscountCurve(DiscountCurve rateCurve)
    {
      var projCal = rateCurve.Calibrator as ProjectionCurveFitCalibrator;
      if (projCal != null)
        return projCal.DiscountCurve ?? rateCurve;
      var mCal = rateCurve.Calibrator as MultiRateCurveFitCalibrator;
      if (mCal != null)
        return mCal.DiscountCurve ?? rateCurve;
      return rateCurve;
    }

    internal static VolatilityCurve CreateVolatilityCurve(Dt asOf)
    {
      return new VolatilityCurve(asOf)
      {
        Interp = new Flat(1E-15),
        IsInstantaneousVolatility = true,
      };
    }

    internal static double CalcSwapRate(int bI, int eI,
        double[] projFactors, double[] dicountFactors,
        double[] t, out double annuity)
    {
      double level = 0.0, floatValue = 0.0;
      for (int i = bI + 1; i <= eI; ++i)
      {
        var fraction = (t[i] - (i == 0 ? 0.0 : t[i - 1]));
        level += dicountFactors[i] * fraction;
        floatValue += (projFactors[i - 1] / projFactors[i] - 1) * dicountFactors[i];
      }
      annuity = level;
      return floatValue / level;
    }


    internal static int GetIndex(Dt[] dates, Dt date)
    {
      int i = 0;
      if (date == dates[dates.Length - 1])
        return (dates.Length - 1);
      for (int j = 0; j < dates.Length; ++j)
      {
        if (date < dates[j])
        {
          i = j - 1;
          break;
        }
      }
      return i;
    }

    internal static double Expd1(double x)
    {
      if (Math.Abs(x) < 1E-3)
        return 1 + x / 2 * (1 + x / 3 * (1 + x / 4 * (1 + x / 5)));
      return (Math.Exp(x) - 1) / x;
    }
  }
}
