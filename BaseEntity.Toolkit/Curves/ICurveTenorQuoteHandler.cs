/*
 * ICurveTenorQuoteHandler.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Interface that every handlers of curve tenor quotes must implement.
  ///   <preliminary>For public use only.</preliminary>
  /// </summary>
  public interface ICurveTenorQuoteHandler : ICloneable
  {
    /// <summary>
    /// Gets the current quote of a tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns>The current quote.</returns>
    IMarketQuote GetCurrentQuote(CurveTenor tenor);

    /// <summary>
    /// Gets the current quote of a tenor in the specified type.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="targetQuoteType">Type of the target quote.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <param name="recalculate">if set to <c>true</c>, recalculate the quote value.</param>
    /// <returns>The quote value.</returns>
    double GetQuote(CurveTenor tenor, QuotingConvention targetQuoteType,
      Curve curve, Calibrator calibrator, bool recalculate);

    /// <summary>
    /// Sets the value and type of the current quote.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="quoteType">Type of the quote.</param>
    /// <param name="quoteValue">The quote value.</param>
    void SetQuote(CurveTenor tenor, QuotingConvention quoteType, double quoteValue);

    /// <summary>
    /// Bumps the current quote of a tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>Actual size bumped.</returns>
    double BumpQuote(CurveTenor tenor, double bumpSize, BumpFlags bumpFlags);

    /// <summary>
    /// Creates a pricer and set up target MTM for calibration.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="curve">The curve.</param>
    /// <param name="calibrator">The calibrator.</param>
    /// <returns>The pricer.</returns>
    IPricer CreatePricer(CurveTenor tenor, Curve curve, Calibrator calibrator);

  } // interface ICurveTenorQuoteHandler

}
