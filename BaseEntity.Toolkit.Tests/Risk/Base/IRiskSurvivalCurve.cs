/*
 * IRiskSurvivalCurve.cs
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

/// <summary>
///   Type of survival curve
/// </summary>
public enum RiskSurvivalCurveType
{
  /// <summary>
  ///   Based on a term structure of quotes
  /// </summary>
  Regular,

  /// <summary>
  ///   Fixed spread over a term structure of quotes
  /// </summary>
  Spread,

  /// <summary>
  ///   Factor of a term structure of quotes
  /// </summary>
  Factor,

  /// <summary>
  ///  survival curve implied from Bond or Loan
  /// </summary>
  Implied,

  /// <summary>
  ///  survival curve defaulted on the product.
  /// </summary>
  Defaulted
}

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Interface for Risk survival curves.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This interface defines the public methods and properties of survival
  ///   curves used by Risk.</para>
  ///
  ///   <para>A factory class
  ///   is used for retrieving Risk survival curves.</para>
  ///
  /// </remarks>
  ///
  ///
  public interface IRiskSurvivalCurve : IMarketObject, ICloneable
  {
    #region Methods

    /// <summary>
    ///   Bumps specified tenor of a curve and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnit"/></description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="tenor">Tenor to bump or null for all tenors</param>
    /// <param name="bumpUnit">Bump unit for uniform shift (Eg. CDS = 1bp, Bonds = $1)</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="refit">True if refit of curve required</param>
    ///
    void Bump(string tenor, double bumpUnit, bool up, bool bumpRelative, bool refit);

    /// <summary>
    ///   Format the name of the curve.
    /// </summary>
    /// <returns>The curve name</returns>
    string GetCurveName();

    ///// <summary>
    ///// Return the valid Curve Tenors for this Calculation Environment.  
    ///// </summary>
    ///// <param name="calcEnv">the Calculation Environment to find valid Tenors</param>
    ///// <param name="asOf">protection start date</param>
    ///// <returns>A list of curve tenors</returns>
    //List<CurveTenor> ValidTenors(CalculationEnvironment calcEnv, Dt asOf);

    /// <summary>
    /// Has the credit been declared defaulted on or before the given date?
    /// </summary>
    /// <param name="onDate">The date.</param>
    /// <returns>
    /// 	<c>true</c> if the credit been declared defaulted on or before the given date; otherwise, <c>false</c>.
    /// </returns>
    bool IsDefaultedOn(Dt onDate);

    /// <summary>
    /// Have any recovery proceeds per ISDA auction process settled on or 
    /// before the given date?
    /// </summary>
    /// <param name="onDate">The on date.</param>
    /// <returns>
    /// 	<c>true</c> if any recovery proceeds per ISDA auction process settled on or 
    /// before the given date; otherwise, <c>false</c>.
    /// </returns>
    bool IsRecoverySettledOn(Dt onDate);

    /// <summary>
    /// Have any recovery proceeds per ISDA auction process announced on or 
    /// before the given date?
    /// </summary>
    /// <param name="onDate">The on date.</param>
    /// <returns>
    /// 	<c>true</c> if any recovery proceeds per ISDA auction process announced on or 
    /// before the given date; otherwise, <c>false</c>.
    /// </returns>
    bool IsRecoveryAnnouncedOn(Dt onDate);

    /// <summary>
    /// Sets the credit event info.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="eventDeterminationDate">The event determination date.</param>
    /// <param name="recoveryAnnounceDt">The recovery announce dt.</param>
    /// <param name="recoverySettlementDate">The recovery settlement date.</param>
    /// <param name="realisedRecoveryRate">The realised recovery rate.</param>
    void SetCreditEventInfo(Dt asOf, Dt eventDeterminationDate, Dt recoveryAnnounceDt, Dt recoverySettlementDate,
                            double realisedRecoveryRate);

    #endregion Methods

    #region Properties

    /// <summary>
    /// Gets or sets the unique name identifying this credit curve.
    /// </summary>
    /// <value>The name.</value>
    string Name { get; set; }

    /// <summary>
    /// Gets the Toolkit RiskSurvivalCurve.
    /// </summary>
    /// <value>The survival curve.</value>
    SurvivalCurve SurvivalCurve { get; }

    /// <summary>
    /// Gets or sets the Start date curve is effective (inclusive).
    /// </summary>
    /// <value>The valid from date.</value>
    DateTime ValidFrom { get; set; }

    /// <summary>
    /// Gets or sets the Tenor collection.
    /// </summary>
    /// <value>The tenors.</value>
    IList Tenors { get; set; }

    /// <summary>
    /// Gets the event determination date.
    /// Date defaulted or unset for active curve
    /// </summary>
    /// <value>The event determination date.</value>
    Dt EventDeterminationDate { get; }

    /// <summary>
    /// Gets the recovery announce date.
    /// Date defaulted or unset for active curve
    /// </summary>
    /// <value>The recovery announce date.</value>
    Dt RecoveryAnnounceDate { get; }

    /// <summary>
    /// Gets the recovery settlement date.
    /// Date defaulted or unset for active curve
    /// </summary>
    /// <value>The recovery settlement date.</value>
    Dt RecoverySettlementDate { get; }

    /// <summary>
    /// Gets the realised recovery rate.
    /// </summary>
    /// <value>The realised recovery rate.</value>
    double RealisedRecoveryRate { get; }

    /// <summary>
    /// Gets a value indicating whether this curve is defaulted.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this curve is defaulted; otherwise, <c>false</c>.
    /// </value>
    bool IsDefaulted { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is recovery settled.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is recovery settled; otherwise, <c>false</c>.
    /// </value>
    bool IsRecoverySettled { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is recovery announced.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is recovery announced; otherwise, <c>false</c>.
    /// </value>
    bool IsRecoveryAnnounced { get; }

    /// <summary>
    /// Gets or sets the reference entity.
    /// </summary>
    /// <value>The reference entity.</value>
    LegalEntity ReferenceEntity { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    /// <value>The currency.</value>
    Currency Currency { get; set; }

    /// <summary>
    /// Gets or sets the seniority.
    /// Capital tier for this credit curve
    /// </summary>
    /// <value>The seniority.</value>
    Seniority Seniority { get; set; }

    /// <summary>
    /// ISDA restructuring treatment
    /// </summary>
    /// <value>The type of the restructuring.</value>
    RestructuringType RestructuringType { get; set; }

    /// <summary>
    /// Gets the recovery rate reference.
    /// </summary>
    /// <value>The recovery rate reference.</value>
    string RecoveryRateReference { get; }

    /// <summary>
    /// Current recovery rate for this survival curve
    /// </summary>
    /// <value>The recovery rate.</value>
    double RecoveryRate { get; set; }

    /// <summary>
    /// Gets the underlying.
    /// The underlying survival curve for Factor curves.
    /// </summary>
    /// <value>The underlying.</value>
    IRiskSurvivalCurve Underlying { get; }

    /// <summary>
    /// Gets or sets the type of the curve.
    /// </summary>
    /// <value>The type of the curve.</value>
    RiskSurvivalCurveType CurveType { get;set; }

    /// <summary>
    /// Gets or sets the name of the index.
    /// </summary>
    /// <value>The name of the index.</value>
    string IndexName { get; set; }

    /// <summary>
    /// Gets or sets as of date.
    /// </summary>
    /// <value>As of date.</value>
    Dt AsOf { get; set; }

    /// <summary>
    /// Gets or sets the settle date.
    /// </summary>
    /// <value>The settle date.</value>
    Dt Settle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance can default on settle.
    /// This is used for Scenarios that generically calc JTD.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance can default on settle; otherwise, <c>false</c>.
    /// </value>
    bool CanDefaultOnSettle { get; set; }

    /// <summary>
    /// Gets a value indicating whether [quoted upfront].
    /// Is this curve's tenors quoted in Upfront Fees?
    /// </summary>
    /// <value><c>true</c> if [quoted upfront]; otherwise, <c>false</c>.</value>
    bool QuotedUpfront { get;}

    /// <summary>
    /// Gets a value indicating whether [quoted conv spread].
    /// Is this curve's tenors quoted as Convention Spreads?
    /// </summary>
    /// <value><c>true</c> if [quoted conv spread]; otherwise, <c>false</c>.</value>
    bool QuotedConvSpread { get; }

    /// <summary>
    /// Gets or sets the actual coupon.
    /// Running Premium to be used for upfront fees
    /// </summary>
    /// <value>The actual coupon.</value>
    double ActualCoupon { get; set; }

    /// <summary>
    /// Gets or sets the type of the quote.
    /// Enum indicating how the curve tenors are quoted.
    /// </summary>
    /// <value>The type of the quote.</value>
    CDSQuoteType QuoteType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is a standard contract.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is a standard contract; otherwise, <c>false</c>.
    /// </value>
    bool IsStandardContract { get; set; }

    /// <summary>
    /// Gets or sets the assumed recovery.
    /// Recovery Assumption for conveting between Conv Spread and Upfronts 
    /// </summary>
    /// <value>The assumed recovery.</value>
    double AssumedRecovery { get; set; }

    /// <summary>
    /// Gets or sets the neg SP treatment.
    /// </summary>
    /// <value>The neg SP treatment.</value>
    NegSPTreatment NegSPTreatment { get; set; }

    /// <summary>
    /// Gets or sets the Stressed calibration flag
    /// </summary>
    bool Stressed { get; set; }
    
    #endregion Properties

    /// <summary>
    /// Resets the toolkit survival curve.
    /// </summary>
    void ResetToolkitSurvivalCurve();

    /// <summary>
    /// Gets a credit curve key object representing this instance
    /// </summary>
    /// <returns></returns>
    Services.ServiceModel.CreditCurveKey GetCreditCurveKey();

  } // interface IRiskSurvivalCurve
}  
