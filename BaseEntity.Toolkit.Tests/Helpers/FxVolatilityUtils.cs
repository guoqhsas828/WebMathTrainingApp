//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  internal static class FxVolatilityUtils
  {
    /// <summary>
    ///   Convert volatility data to volatility surface.
    /// </summary>
    /// <param name="data">The volatility data.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="toCurve">To curve.</param>
    /// <param name="fromCurve">From curve.</param>
    /// <param name="fxCurve">The fx curve.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static CalibratedVolatilitySurface ToFxVolatilitySurface(
      this string[,] data,
      Dt asOf, Dt settle, Dt maturity,
      DiscountCurve toCurve,
      DiscountCurve fromCurve,
      FxCurve fxCurve)
    {
      var calibrator = new FxOptionVannaVolgaCalibrator(asOf, settle, toCurve, fromCurve, fxCurve, null);
      var interpolator = calibrator;
      IVolatilityTenor[] tenors;

      if (data.Length > 1)
      {
        int rows = data.GetLength(0), cols = data.GetLength(1);
        if (cols == 2)
        {
          tenors = Enumerable.Range(0, rows)
            .Select(i => new VolatilitySkewHolder(data[i, 0], Dt.FromStr(data[i, 0]),
              0.0, data[i, 1].ToDouble(), 0.0, 0.0))
            .ToArray();
        }
        else if (cols == 4)
        {
          tenors = Enumerable.Range(0, rows)
            .Select(i => new VolatilitySkewHolder(data[i, 0], Dt.FromStr(data[i, 0]),
              0.0, data[i, 1].ToDouble(), data[i, 2].ToDouble(),
              data[i, 3].ToDouble()))
            .ToArray();
        }
        else
        {
          throw new Exception("Invalid volatility data.");
        }
      }
      else
      {
        var name = Tenor.FromDateInterval(asOf, maturity).ToString();
        double vol = data.Length == 1 ? data[0, 0].ToDouble() : 0.1;
        tenors = new[]{ new VolatilitySkewHolder(name, maturity, 0.0, vol, 0.0, 0.0)};
      }
      var volatilitySurface = new CalibratedVolatilitySurface(asOf, tenors, calibrator, interpolator);
      volatilitySurface.Fit();
      return volatilitySurface;
    }

    private static double ToDouble(this string data)
    {
      double scale = 1.0;
      if (data.EndsWith("%"))
      {
        scale = 100;
        data = data.TrimEnd('%');
      }
      return Double.Parse(data) / scale;
    }
  }
}
