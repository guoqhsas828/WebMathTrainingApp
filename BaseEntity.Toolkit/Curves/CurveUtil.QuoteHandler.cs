/*
 * CurveUtil.QuoteHandlers.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Curves
{
  public static partial class CurveUtil
  {
    #region Extension methods for CalibratedCurve
    /// <summary>
    /// Gets the current tenor quotes.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The current tenor quotes.</returns>
    public static IEnumerable<IMarketQuote> GetCurrentQuotes(
      this CalibratedCurve curve)
    {
      var tenors = curve.Tenors;
      int count = tenors.Count;
      for (int i = 0; i < count; ++i)
      {
        yield return tenors[i].CurrentQuote;
      }
    }

    /// <summary>
    /// Gets the current tenor quotes in the specified quote type.
    /// </summary>
    /// <remarks>
    /// <para>The target quote type need not be the same as the current quote type
    ///  specified in curve tenors.
    ///  When they are different, the quote values returned are
    ///  converted to the target quote type, providing they are convertible.
    ///  In order for the conversion to be consistent, the current quotes must
    ///  be already synchronized with the curve. 
    /// </para>
    /// <para>This method never modifies the curve.</para>
    /// </remarks>
    /// <param name="curve">The curve.</param>
    /// <param name="targetQuoteType">Type of the quote to get.</param>
    /// <returns>Quotes in the target types.</returns>
    /// <exception cref="QuoteTypeNotSupportedException">
    ///  Conversion to the target quote type is not supported by the quote handlers in the curve.
    /// </exception>
    public static IEnumerable<double> GetQuotes(this CalibratedCurve curve,
      QuotingConvention targetQuoteType)
    {
      var calibrator = curve.Calibrator;
      var tenors = curve.Tenors;
      int count = tenors.Count;
      for (int i = 0; i < count; ++i)
      {
        yield return tenors[i].GetQuote(
          targetQuoteType, curve, calibrator, false);
      }
    }

    /// <summary>
    /// Recalculate the curve tenor quotes in the specified type.
    /// </summary>
    /// <remarks>
    /// <para>This method always recalculates the implied quote values
    ///  and set the new values as the current quotes. </para>
    /// <para>The target quote type need not be the same as the current quote type
    ///  specified in curve tenors.  When they are different, the implied 
    ///  quote values are converted to the target quote type, providing they
    ///  are convertible.</para>
    /// </remarks>
    /// <param name="curve">The curve.</param>
    /// <param name="targetQuoteType">Type of the quote.</param>
    /// <exception cref="QuoteTypeNotSupportedException">
    ///  Conversion to the target quote type is not supported by the quote handlers in the curve.
    /// </exception>
    public static void UpdateQuotes(this CalibratedCurve curve,
      QuotingConvention targetQuoteType)
    {
      var calibrator = curve.Calibrator;
      var tenors = curve.Tenors;
      int count = tenors.Count;
      for (int i = 0; i < count; ++i)
        tenors[i].UpdateQuote(targetQuoteType, curve, calibrator);
      return;
    }

    /// <summary>
    /// Bump the current quote in the specified quote convention.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="tenorIndex">Index of the tenor.</param>
    /// <param name="targetQuoteType">Type of the target quote
    /// (None means to use the current quote type).</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>Actual bump size.</returns>
    public static double BumpQuote(this CalibratedCurve curve,
      int tenorIndex, QuotingConvention targetQuoteType,
      double bumpSize, BumpFlags bumpFlags)
    {
      return BumpQuote(curve, tenorIndex,
        targetQuoteType, bumpSize, bumpFlags, -1);
    }

    /// <summary>
    /// Bumps the current quotes in the specified quoting convention.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="targetQuoteType">Type of the target quote
    /// (None means to use the current quote type).</param>
    /// <param name="bumpSizes">The bump sizes.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>Average of the actual size bumped.</returns>
    public static double BumpQuotes(this CalibratedCurve curve,
      string[] tenors, QuotingConvention targetQuoteType, 
      double[] bumpSizes, BumpFlags bumpFlags)
    {
      return BumpQuotes(curve, tenors,
        targetQuoteType, bumpSizes, bumpFlags, -1);
    }

    /// <summary>
    /// Bumps the current quotes in the specified quoting convention.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="bumpSizes">The bump sizes.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <param name="includes">The includes.</param>
    /// <returns>An array of the average sizes actually bumped.</returns>
    public static double[] BumpQuotes(this CalibratedCurve[] curves,
      string[] tenors, QuotingConvention targetQuoteType, double[] bumpSizes,
      BumpFlags bumpFlags, bool[] includes)
    {
      // Validate
      if (curves == null || curves.Length == 0)
        throw new ArgumentException("No curves specified to bump");
      if (bumpSizes == null || bumpSizes.Length == 0)
        throw new ArgumentException("No bump units specified");
      if ((tenors == null || tenors.Length == 0) && bumpSizes.Length != 1)
        throw new ArgumentException("Multiple bumps have been specified for curve bumping when all tenors are to be bumped");
      if (tenors != null && bumpSizes.Length != 1 && tenors.Length != bumpSizes.Length)
        throw new ArgumentException("If specific tenors are to be bumped, one bump must be specified or the number of tenors must match the number of bump");
      if (includes != null && includes.Length > 0 && includes.Length != curves.Length)
        throw new ArgumentException("Number of includes must match number of curves");

      Timer timer = new Timer();
      timer.start();

      logger.DebugFormat("Bumping set of tenors ({0}...) for curves {1}... by {2} {3}",
        (tenors != null && tenors.Length > 0) ? tenors[0] : "all",
        curves[0].Name, (bumpFlags & BumpFlags.BumpRelative) != 0 ? "factor" : "spread",
        bumpSizes[0]);

      double[] avgBumps = new double[curves.Length];
      bool refit = (bumpFlags & BumpFlags.RefitCurve) != 0;
      if (refit && curves.Length > ParallelStart && Parallel.Enabled
        && !HasCurveDependency(curves))
      {
        
        Parallel.For(0, curves.Length, delegate(int j)
        {
          BumpFlags flags = bumpFlags;
          if (includes == null || includes.Length == 0 || includes[j])
          {
            if (AllowNegativeCDSSpreads(curves[j]))
              flags |= BumpFlags.AllowNegativeCDSSpreads;

            avgBumps[j] = BumpQuotes(curves[j], tenors, targetQuoteType,
              bumpSizes, flags, j);
          }
        });
      }
      else
      {
        for (int j = 0; j < curves.Length; j++)
          if (includes == null || includes.Length == 0 || includes[j])
          {
            BumpFlags flags = bumpFlags;
            if (AllowNegativeCDSSpreads(curves[j]))
              flags |= BumpFlags.AllowNegativeCDSSpreads;
            avgBumps[j] = BumpQuotes(curves[j], tenors, targetQuoteType,
              bumpSizes, flags, j);
          }
      }

      timer.stop();
      logger.InfoFormat("Completed bump in {0}s", timer.getElapsed());

      return avgBumps;
    }

    private static bool HasCurveDependency(CalibratedCurve[] curves)
    {
      return curves.SelectMany(curve => curve.EnumeratePrerequisiteCurves())
        .Any(curves.Contains);
    }

    /// <summary>
    /// Helper method that determines whether to allow negative CDS Spreads in the survival curves
    /// </summary>
    /// <param name="curve"></param>
    /// <returns></returns>
    public static bool AllowNegativeCDSSpreads(CalibratedCurve curve)
    {
      if(curve is SurvivalCurve)
      {
        if(curve.Calibrator is SurvivalFitCalibrator)
        {
          var calibrator = (SurvivalFitCalibrator) curve.Calibrator;
          return calibrator.AllowNegativeCDSSpreads;
        }
        return false;
      }
      return false;
    }

    /// <summary>
    /// Bumps uniformly the current quotes in the specified quoting convention.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>An array of the average sizes actually bumped.</returns>
    public static double[] BumpQuotes(this CalibratedCurve[] curves,
      QuotingConvention targetQuoteType, double bumpSize,
      BumpFlags bumpFlags)
    {
      return curves.BumpQuotes(null, targetQuoteType,
        new double[] { bumpSize }, bumpFlags, null);
    }

    /// <summary>
    /// Bumps uniformly the current quotes in the specified quoting convention.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="bumps">Size of the bumps.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>An array of the average sizes actually bumped.</returns>
    public static double[] BumpQuotes(this CalibratedCurve[] curves,
      QuotingConvention targetQuoteType, double[] bumps,
      BumpFlags bumpFlags)
    {
      return curves.BumpQuotes(null, targetQuoteType,
        bumps, bumpFlags, null);
    }

    /// <summary>
    /// Bumps the current quotes in the specified quoting convention.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>An array of the average sizes actually bumped.</returns>
    public static double[] BumpQuotes(this CalibratedCurve[] curves,
      string tenor, QuotingConvention targetQuoteType, double bumpSize,
      BumpFlags bumpFlags)
    {
      return curves.BumpQuotes((tenor != null) ? new string[] { tenor } : null,
        targetQuoteType, new double[] { bumpSize }, bumpFlags, null);
    }

    /// <summary>
    /// Bumps the current quotes in the specified quoting convention.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>An array of the average sizes actually bumped.</returns>
    public static double[] BumpQuotes(this CalibratedCurve[] curves,
      string[] tenors, QuotingConvention targetQuoteType,
      double bumpSize, BumpFlags bumpFlags)
    {
      return curves.BumpQuotes(tenors, targetQuoteType, new double[] { bumpSize }, bumpFlags, null);
    }

    private static double BumpQuote(CalibratedCurve curve,
      int tenorIndex, QuotingConvention targetQuoteType,
      double bumpSize, BumpFlags bumpFlags, int curveIndex)
    {
      double avgBump = 0;
      if (tenorIndex < curve.Tenors.Count)
      {
        avgBump = curve.Tenors[tenorIndex].BumpQuote(targetQuoteType,
          bumpSize, bumpFlags, curve, curve.Calibrator);
      }

      // Refit curve
      if ((bumpFlags & BumpFlags.RefitCurve) != 0)
      {
        try
        {
          curve.ReFit(tenorIndex);
        }
        catch (SurvivalFitException ex)
        {
          if (curveIndex >= 0)
            logger.DebugFormat(
              "Failed to fit tenor {0} bumping curve index {1}: {2}",
              ex.Tenor.Name, curveIndex, ex.Message);
          else
            logger.DebugFormat(
              "Failed to fit tenor {0} bumping curve {1}: {2}",
              ex.Tenor.Name, ex.CurveName, ex.Message);
          return Double.NaN;
        }
      }
      return avgBump;
    }

    private static double BumpQuotes(CalibratedCurve curve,
      string[] tenors, QuotingConvention targetQuoteType,
      double[] bumpSizes, BumpFlags bumpFlags, int curveIndex)
    {
      if (!curve.JumpDate.IsEmpty())
        return 0.0; // indicate that this curve is skipped.

      Calibrator calibrator = curve.Calibrator;

      double avgBump = 0;
      int minTenor = 100;   // First tenor bumped

      if (tenors == null || tenors.Length == 0)
      {
        // Bump all tenor point(s)
        minTenor = 0;
        int count = 0;
        foreach (CurveTenor t in curve.Tenors)
        {
          double a = t.BumpQuote(targetQuoteType,
            bumpSizes[0], bumpFlags, curve, calibrator);
          avgBump += (a - avgBump) / (++count);
        }
        if (count == 0)
          return 0.0; // indicate that this curve is skipped.
      }
      else
      {
        int count = 0;
        for (int i = 0; i < tenors.Length; i++)
        {
          int idx = curve.Tenors.Index(tenors[i]);
          if (idx >= 0)
          {
            if (idx < minTenor)
              minTenor = idx;
            double a = curve.Tenors[idx].BumpQuote(targetQuoteType,
              ((bumpSizes.Length > 1) ? bumpSizes[i] : bumpSizes[0]),
              bumpFlags, curve, calibrator);
            avgBump += (a - avgBump) / (++count);
          }
        }
        if (count == 0)
          return 0.0; // indicate that this curve is skipped.
      }

      // Refit curve
      if ((bumpFlags & BumpFlags.RefitCurve) != 0)
      {
        try
        {
          curve.ReFit(minTenor);
        }
        catch (SurvivalFitException ex)
        {
          if (curveIndex >= 0)
            logger.DebugFormat(
              "Failed to fit tenor {0} bumping curve index {1}: {2}",
              ex.Tenor.Name, curveIndex, ex.Message);
          else
            logger.DebugFormat(
              "Failed to fit tenor {0} bumping curve {1}: {2}",
              ex.Tenor.Name, ex.CurveName, ex.Message);
          return Double.NaN;
        }
      }
      return avgBump;
    }
    #endregion Extension methods for CalibratedCurve

    #region Extension methods for CurveTenor
    /// <summary>
    /// Retrieve ReferenceIndex objects from tenor products
    /// </summary>
    /// <param name="tenor">CurveTenor</param>
    /// <returns>All underlying reference indices</returns>
    private static IEnumerable<ReferenceIndex> GetReferenceIndices(this CurveTenor tenor)
    {
      if (tenor.Product != null)
      {
        var type = tenor.Product.GetType();
        var info = type.GetProperty("ReferenceIndex", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
        if (info != null)
        {
          var retVal = (ReferenceIndex)info.GetValue(tenor.Product, null);
          if (retVal != null)
            yield return retVal;
          yield break;
        }
        if ((info = type.GetProperty("ReferenceIndices", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)) != null)
        {
          var retVal = (IEnumerable<ReferenceIndex>)info.GetValue(tenor.Product, null);
          if (retVal != null)
            foreach (var referenceIndex in retVal)
              yield return referenceIndex;
        }
      }
    }

    
    /// <summary>
    /// Get all reference indices underlying the calibration tenors
    /// </summary>
    /// <param name="curveTenorCollection">Curve tenors</param>
    /// <returns>Underlying reference indices</returns>
    public static IEnumerable<ReferenceIndex> GetReferenceIndices(this CurveTenorCollection curveTenorCollection)
    {
      return curveTenorCollection.SelectMany(t => t.GetReferenceIndices()).Where(index => index != null).DistinctBy(index => index.IndexName);
    }


    /// <summary>
    /// Bumps the current tenor quote.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The flags specifying how to bump.</param>
    /// <returns>Actual bump size.</returns>
    public static double BumpQuote(this CurveTenor tenor,
      double bumpSize, BumpFlags bumpFlags)
    {
      return tenor.QuoteHandler.BumpQuote(tenor, bumpSize, bumpFlags);
    }

    /// <summary>
    /// Gets the current tenor quote in the specified quote type.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="recalculate">if set to <c>true</c>, recalculate the quote value.</param>
    /// <returns>Quote value.</returns>
    public static double GetQuote(this CurveTenor tenor,
      QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator,
      bool recalculate)
    {
      return tenor.QuoteHandler.GetQuote(tenor,
        targetQuoteType, curve, calibrator, recalculate);
    }

    /// <summary>
    /// Updates the current tenor quote in the specified quote type.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <returns>The updated quote value.</returns>
    public static double UpdateQuote(this CurveTenor tenor,
      QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator)
    {
      var quoteHandler = tenor.QuoteHandler;
      double quoteValue = quoteHandler.GetQuote(tenor,
        targetQuoteType, curve, calibrator, true);
      quoteHandler.SetQuote(tenor, targetQuoteType, quoteValue);
      return quoteValue;
    }

    /// <summary>
    /// Sets the current tenor quote.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="quoteType">Type of the quote.</param>
    /// <param name="quoteValue">The quote value.</param>
    public static void SetQuote(this CurveTenor tenor,
      QuotingConvention quoteType, double quoteValue)
    {
      tenor.QuoteHandler.SetQuote(tenor, quoteType, quoteValue);
    }

    public static void SetQuote(this CurveTenor tenor, IMarketQuote quote)
    {
      tenor.QuoteHandler.SetQuote(tenor, quote.Type, quote.Value);
    }

    /// <summary>
    /// Bumps the current quote in the specified quoting convention.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <returns>Actual size bumped.</returns>
    public static double BumpQuote(this CurveTenor tenor,
      QuotingConvention targetQuoteType, double bumpSize,
      BumpFlags bumpFlags, Curve curve, Calibrator calibrator)
    {
      if (targetQuoteType != QuotingConvention.None
        && targetQuoteType != tenor.CurrentQuote.Type)
      {
        tenor.UpdateQuote(targetQuoteType, curve, calibrator);
      }
      return tenor.BumpQuote(bumpSize, bumpFlags);
    }
    #endregion Extension methods for CurveTenor
  }
}
