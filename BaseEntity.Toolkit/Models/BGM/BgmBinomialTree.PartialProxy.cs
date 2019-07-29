/*
 * BgmBinomialTree.PartialProxy.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Models.BGM
{
  public static partial class BgmBinomialTree
  {
    public const int LogNormal = 0, Normal = 1, NormalToLogNormal = 2,
      AmericanOption = 16;
    public static readonly double[] EmptyArray = new double[0];

    /// <summary>
    /// Calculates the libor rate distributions.
    /// </summary>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="tolerance">Tolerance such that the nodes with tail probability
    ///   less than it are cut off.</param>
    /// <param name="today">The today.</param>
    /// <param name="rateCurve">The rate curve representing the initial forward rates.</param>
    /// <param name="maturityDates">The maturity dates.</param>
    /// <param name="volatilityCurves">The volatilities.</param>
    /// <param name="nodeDates">Node dates (null if the same as the maturity dates).</param>
    /// <returns>Rate distributions</returns>
    public static RateSystem CalculateRateSystem(
      double stepSize,
      double tolerance,
      Dt today,
      DiscountCurve rateCurve,
      IList<Dt> maturityDates,
      IList<VolatilityCurve> volatilityCurves,
      IList<Dt> nodeDates)
    {
      if (maturityDates == null || maturityDates.Count < 2)
        throw new ToolkitException("At least two maturity dates required.");
      if (maturityDates.Count == volatilityCurves.Count + 1)
      {
        // Since the first rate is reset on or before today, we can simply
        // set the first volatility curve to null.
        var curves = volatilityCurves;
        volatilityCurves= ListUtil.CreateList(maturityDates.Count,
           (i) => i == 0 ? null : curves[i - 1]);
      }
      else if (maturityDates.Count != volatilityCurves.Count)
        throw new ToolkitException("Maturity dates and volatility curves not match.");

      int count = maturityDates.Count;
      double[] fractions = new double[count],
        resetTimes = new double[count],
        initialRates = new double[count];
      {
        double lastDf = 1, lastReset = 0;
        for (int i = 0; i < count; ++i)
        {
          Dt maturity = maturityDates[i];
          Dt reset = i == 0 ? today : maturityDates[i - 1];
          double frac = (maturity - reset) / 365;
          if (frac <= 0)
          {
            throw new ToolkitException(String.Format(
              "Tenor date out of order at index {0} ", i));
          }
          fractions[i] = frac;
          resetTimes[i] = lastReset;
          lastReset += frac;
          double df = rateCurve.Interpolate(today, maturity);
          initialRates[i] = (lastDf/df - 1)/frac;
          lastDf = df;
        }
      }

      double[] dates;
      if (nodeDates == null || nodeDates.Count == 0)
      {
        nodeDates = ListUtil.CreateList(count,
          (i) => i == 0 ? today : maturityDates[i - 1]).ToArray();
        dates = EmptyArray;
      }
      else
      {
        Dt lastReset = maturityDates[count - 2];
        var list = new UniqueSequence<Dt>(today);
        for (int i = maturityDates.Count - 1; --i >= 0; )
        {
          if (maturityDates[i] > lastReset) continue;
          list.Add(maturityDates[i]);
        }
        for (int i = nodeDates.Count; --i >= 0; )
        {
          if (nodeDates[i] > lastReset) continue;
          list.Add(nodeDates[i]);
        }
        nodeDates = list;
        dates = list.ConvertAll((d) => (d-today)/365.0).ToArray();
      }
      var distributions = new RateSystem
      {
        AsOf = today,
        TenorDates = maturityDates.ToArray(),
        NodeDates = nodeDates.ToArray()
      };
      Native.BgmBinomialTree.calculateRateSystem(stepSize, tolerance, initialRates,
        fractions, resetTimes, volatilityCurves.ToArray(),
        dates, distributions);
      return distributions;
    }
  } // class BgmBinomialTree
}