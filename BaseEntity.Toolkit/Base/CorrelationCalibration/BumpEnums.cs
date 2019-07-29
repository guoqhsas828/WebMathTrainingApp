/*
 * BumpEnums.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Bump targets
  /// </summary>
  [Flags]
  public enum BumpTarget
  {
    /// <summary>Bump the calibrated correlations directly</summary>
    Correlation = 0x0,
    /// <summary>Bump the standard tranche quotes used for calibration</summary>
    TrancheQuotes = 0x01,
    /// <summary>Bump the index quotes used for calibration (basis adjustment)</summary>
    IndexQuotes = 0x02,
    /// <summary>Bump both tranche and index quotes used for calibration</summary>
    TrancheAndIndexQuotes = 0x03,
    /// <summary>Bump interest rate quotes</summary>
    InterestRates = 0x04,
    /// <summary>Bump interest basis quotes</summary>
    InterestRateBasis = 0x08,
    /// <summary>Bump FX rate or FX basis quotes</summary>
    FxRates = 0x10,
    /// <summary>Bump volatility quotes</summary>
    Volatilities = 0x20,
    /// <summary>Credit spread or upfront quotes</summary>
    CreditQuotes = 0x40,
    /// <summary>Credit default recovery quotes</summary>
    RecoveryQuotes = 0x80,
    /// <summary>Inflation rates or real yields</summary>
    InflationRates = 0x100,
    //g : add two types of bump target
    /// <summary> Bump commodity price </summary>
    CommodityPrice = 0x200,
    /// <summary> Bump Stock price </summary>
    StockPrice = 0x400,
    /// <summary> Bump spot price </summary>
    IncludeSpot = 0x800,
  }

  /// <summary>
  ///   Bump method
  /// </summary>
  public enum BumpMethod
  {
    /// <summary>Bump absolutely</summary>
    Absolute,
    /// <summary>Bump relatively</summary>
    Relative,
  }

  /// <summary>
  ///   Bump Unit
  /// </summary>
  public enum BumpUnit
  {
    /// <summary>No conversion is required</summary>
    None,
    /// <summary>
    ///  Percentage point for price/upfront fee, basis point for spread
    /// </summary>
    Natural,
    /// <summary>The number is percentage (1 means 1% or 0.01)</summary>
    Percentage,
    /// <summary>The number is basis point (1 means 1bp or 0.0001)</summary>
    BasisPoint,
  }
  
  /// <summary>
  ///   Flags specifies how to bump quotes.
  /// </summary>
  [Flags]
  public enum BumpFlags
  {
    /// <summary>
    ///  No flag.
    /// </summary>
    None = 0,
    /// <summary>
    ///   Perform relative bump.
    /// </summary>
    BumpRelative = 0x0001,
    /// <summary>
    ///   Bump down instead of bump up.
    /// </summary>
    BumpDown = 0x0002,
    /// <summary>
    ///   Refit the curve after bumping.
    /// </summary>
    RefitCurve = 0x0004,
    /// <summary>
    /// Allow negative spreads for the survival curve
    /// </summary>
    AllowNegativeCDSSpreads = 0x0008,
    /// <summary>
    ///  Curve points are modified in place without using an overlay.
    /// </summary>
    BumpInPlace = 0x0010,
    /// <summary>
    ///  Recalibrate credit curves after the prerequisite curves are bumped.
    /// </summary>
    RecalibrateSurvival = 0x0020,
    /// <summary>
    ///  Recalculate the base correlations.
    /// </summary>
    RemapCorrelations = 0x0040,
    /// <summary>
    ///  In by-tenor sensitivities, do not calculate hedge on the tenor 
    ///  which does not match the specified hedge tenor (the old behavior).
    /// </summary>
    NoHedgeOnTenorMismatch = 0x0080,

    /// <summary>
    /// Forbid up crossing zero when bump a curve tenor
    /// </summary>
    ForbidUpCrossingZero = 0x0100,

    /// <summary>
    /// Allow down crossing zero when bump a curve tenor.
    /// </summary>
    AllowDownCrossingZero = 0x0200,

    /// <summary>
    /// Bump the results of interpolation instead of market quotes.
    /// Currently this applies only to volatility bump.
    /// </summary>
    BumpInterpolated = 0x0400,
  }
}
