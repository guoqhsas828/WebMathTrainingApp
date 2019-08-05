// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves.Volatilities;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  internal static class CommodityVolatilityUtils
  {
    /// <summary>
    /// Convert volatility data to volatility surface.
    /// </summary>
    /// <param name="data">The volatility data.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <returns></returns>
    /// <exception cref="System.Exception">Invalid volatility data.</exception>
    public static CalibratedVolatilitySurface ToVolatilitySurface(
      this string[,] data,
      Dt asOf, Dt settle, Dt maturity, double strike,
      IVolatilitySurfaceCalibrator calibrator,
      IVolatilitySurfaceInterpolator interpolator)
    {
      PlainVolatilityTenor[] tenors;

      if (data.Length > 1)
      {
        int rows = data.GetLength(0), cols = data.GetLength(1);
        if (cols == 3)
        {
          var tenorDict = new Dictionary<Dt, PlainVolatilityTenor>();
          foreach (var row in Enumerable.Range(0, rows))
          {
            var date = Dt.FromStr(data[row, 0]);
            if (!tenorDict.ContainsKey(date))
            {
              tenorDict.Add(date, new PlainVolatilityTenor(data[row, 0], date)
                               {
                                 Strikes = new List<double>(),
                                 Volatilities = new List<double>()
                               });
            }
            tenorDict[date].Strikes.Add(data[row, 1].ToDouble());
            tenorDict[date].QuoteValues.Add(data[row, 2].ToDouble());
          }
          tenors = tenorDict.Values.ToArray();
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
        tenors = new[]
                 {
                   new PlainVolatilityTenor(name, maturity)
                   {
                     Strikes = new List<double>() {strike},
                     Volatilities = new List<double>() {vol}
                   }
                 };
      }
      var volatilitySurface = new CalibratedVolatilitySurface(asOf,
                                                              tenors.Cast<IVolatilityTenor>().ToArray(),
                                                              calibrator,
                                                              interpolator);
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