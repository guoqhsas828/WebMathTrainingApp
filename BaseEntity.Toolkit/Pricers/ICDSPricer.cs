/*
 * ICDSPricer.cs
 *
 *  -2008. All rights reserved.     
 *
 * TBD: To facilitate transition between cashflow implementations. To be revived, RTD Oct'07
 */

using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Pricer for a <see cref="BaseEntity.Toolkit.Products.CDS">CDS</see> based on the
  ///   <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDS" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDS">CDS Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow pricing model</seealso>
  public interface ICDSPricer : IPricer
  {
    #region Methods

    /// <summary>
    ///   Expected Loss at a date
    /// </summary>
    /// <param name="start">Protection start date</param>
    /// <param name="date">Date after settle</param>
    /// <returns>Expected loss</returns>
    /// <exclude />
    double ExpectedLossRate(Dt start, Dt date);

    /// <summary>
    ///   Calculate the present value of the fee part of the CDS
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Fee cashflows after the settlement date are present valued
    ///   back to the pricing as-of date.</para>
    /// </remarks>
    ///
    /// <returns>Present value of the fee part of the CDS</returns>
    ///
    double FeePv();

    /// <summary>
    ///   Calculate the present value of the protection part of a CDS
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Protection cashflows after the settlement date are present valued
    ///   back to the pricing as-of date.</para>
    /// </remarks>
    ///
    /// <returns>Present value of the protection part of the CDS</returns>
    ///
    double ProtectionPv();

    /// <summary>
    ///   Calculate the number of accrual days for a Credit Default Swap
    /// </summary>
    ///
    /// <returns>Number of days accrual for Credit Default Swap at the settlement date</returns>
    ///
    int AccrualDays();

    /// <summary>
    ///   Calculate break-even premium for a Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    ///
    /// <returns>Break-even premium for Credit Default Swap in percent</returns>
    ///
    double BreakEvenPremium();

    /// <summary>
    ///  Calculate breakeven fee for a CDS 
    /// </summary>
    /// <remarks>
    ///   <para>The breakeven fee satisfies: UpFront(BEF) + CleanFee(Premium) = ProtectionLeg</para>
    /// </remarks>
    /// <returns>Breakeven Fee</returns>
    double BreakEvenFee();

    /// <summary>
    ///  Calculate breakeven fee for a CDS 
    /// </summary>
    /// <remarks>
    ///   <para>The breakeven fee satisfies: UpFront(BEF) + CleanFee(Premium) = ProtectionLeg</para>
    /// </remarks>
    /// <param name="runningPremium">Optional running premium of CDS</param>
    /// <returns>Breakeven Fee</returns>
    double BreakEvenFee(double runningPremium);

    /// <summary>
    /// Replicating spread
    /// </summary>
    /// <returns></returns>
    double ReplicatingSpread();

    /// <summary>
    ///   Calculate the forward break-even premium for a Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The forward break-even premium is the premium which would imply a
    ///   zero MTM value on the specified forward settlement date.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    ///
    ///   <para>Note: Currently does not support CDS with up-front fees</para>
    /// </remarks>
    ///
    /// <param name="forwardSettle">Forward Settlement date</param>
    ///
    /// <returns>The forward break-even premium for the Credit Default Swap in percent</returns>
    ///
    double FwdPremium(Dt forwardSettle);

    /// <summary>
    ///   Calculate the forward break-even premium for a Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The forward break-even premium is the premium which would imply a
    ///   zero MTM value on the specified forward settlement date.</para>
    /// 
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">BreakEvenPremium = ProtectionLegPv / { Duration * Notional }</formula></para>
    ///
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    ///
    ///   <para>Note: Currently does not support CDS with up-front fees</para>
    /// </remarks>
    ///
    /// <param name="forwardSettle">Forward Settlement date</param>
    /// <param name="replicatingSpread">Optional bool argument. True means CDS assumes 0 upfront fee</param>
    /// <returns>The forward break-even premium for the Credit Default Swap in percent</returns>
    ///
    double FwdPremium(Dt forwardSettle, bool replicatingSpread);

    /// <summary>
    ///   Calculate the Premium 01 of Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Premium 01 is the change in PV (MTM) for a Credit
    ///   Default Swap if the premium is increased by one basis point.</para>
    ///
    ///   <para>The Premium 01 is calculated by calculating the PV (MTM) of the
    ///   Credit Default Swap then bumping up the premium by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    ///
    ///   <para>The Premium 01 includes accrued.</para>
    /// </remarks>
    ///
    /// <returns>Premium 01 of the Credit Default Swap</returns>
    ///
    double Premium01();
    
    /// <summary>
    ///   Calculate the Forward Premium 01 of Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Forward Premium 01 is the change in PV (MTM) at a
    ///   future settlement date for a Credit Default Swap if
    ///   the premium is increased by one basis point.</para>
    ///
    ///   <para>The Forward Premium 01 is calculated by calculating
    ///   the PV (MTM) of the Credit Default Swap at a specified
    ///   future settlement date and then bumping up the premium
    ///   by one basis point and re-calculating the MTM at the
    ///   same forward settlement date and returning the difference
    ///   in value.</para>
    /// 
    ///   <para>The Foward Premium 01 includes accrued.</para>
    /// </remarks>
    ///
    /// <param name="forwardSettle">Forward settlement date</param>
    ///
    /// <returns>Forward Premium 01 of the Credit Default Swap</returns>
    ///
    double FwdPremium01(Dt forwardSettle);

    /// <summary>
    ///   Calculate the risky duration of the Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is the fee pv of a CDS with a premium of 1(10000bps) and 
    ///    a notional of 1.0.</para>
    /// 
    ///   <para>The risky duration is based on the remaining premium that is uncertain and
    ///   as such does not include any accrued.</para>
    /// 
    ///   <para>The Duration is related to the Protection Leg PV, the Break Even Premium and the
    ///   Notional by <formula inline="true">Duration = ProtectionLegPv / { BreakEvenPremium * Notional }</formula></para>
    /// 
    ///   <para>The risky duration is discounted back to the settle date or
    ///   the pricing date (as-of date), depending on the runtime configuration in xml.
    ///   By default, it discounts back to as-of date.</para>
    /// </remarks>
    ///
    /// <returns>Risky duration of the Credit Default Swap</returns>
    ///
    double RiskyDuration();

    /// <summary>
    ///   Calculate the risky duration of the Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The risky duration is the pv of a premium of 1.</para>
    /// 
    ///   <para>The risky duration is based on the remaining premium that is uncertain and
    ///   as such does not include any accrued.</para>
    /// 
    ///   <para>The Duration is related to the Protection Leg PV, the Break Even Premium and the
    ///   Notional by <formula inline="true">Duration = ProtectionLegPv / { BreakEvenPremium * Notional }</formula></para>
    /// 
    ///   <para>The forward risky duration is discounted back to the forward settle date instead of
    ///   the pricing date (as-of date).</para>
    /// </remarks>
    ///
    /// <param name="forwardSettle">Forward settlement date</param>
    /// 
    /// <returns>Risky duration of the Credit Default Swap</returns>
    ///
    double RiskyDuration(Dt forwardSettle);

    /// <summary>
    ///   Calculate survival probability
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method handles the correlation between the credit and counterpaty defaults.</para>
    /// </remarks>
    ///
    /// <returns>Survival probability at the maturity date</returns>
    ///
    double SurvivalProbability();

    /// <summary>
    ///   Calculate survival probability
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method handles the correlation between the credit and counterpaty defaults.</para>
    /// </remarks>
    ///
    /// <returns>Survival probability at the maturity date</returns>
    ///
    double SurvivalProbability(Dt end);

    /// <summary>
    ///   Calculate the carry of the Credit Default Swap
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional.</para>
    /// </remarks>
    ///
    /// <returns>Carry of the Credit Default Swap</returns>
    ///
    double Carry();

    /// <summary>
    ///   Calculate the MTM Carry of the Credit Default Swap
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional.</para>
    /// </remarks>
    /// 
    /// <returns>MTM Carry of Credit Default Swap</returns>
    /// 
    double MTMCarry();

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Product to price
    /// </summary>
    CDS CDS { get;  }

    /// <summary>
    ///   Original notional amount for pricing
    /// </summary>
    double Notional { get; set; }

    /// <summary>
    ///   Outstanding notional on the settlement date
    /// </summary>
    double EffectiveNotional
    { get; }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    DiscountCurve DiscountCurve
    { get; set; }

    /// <summary>
    ///   Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
    DiscountCurve ReferenceCurve
    { get; set; }

    /// <summary>
    ///   Survival curve used for pricing
    /// </summary>
    SurvivalCurve SurvivalCurve
    { get; set; }

    /// <summary>
    ///   Counterparty curve used for pricing
    /// </summary>
    SurvivalCurve CounterpartyCurve
    { get; set; }

    /// <summary>
    ///   Recovery curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If a separate recovery curve has not been specified, the recovery from the survival
    ///   curve is used. In this case the survival curve must have a Calibrator which provides a
    ///   recovery curve otherwise an exception will be thrown.</para>
    /// </remarks>
    ///
    RecoveryCurve RecoveryCurve
    { get; set; }

    /// <summary>
    ///   Return the recovery rate matching the maturity of this product.
    /// </summary>
    double RecoveryRate
    { get; }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    int StepSize
    { get; set; }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    TimeUnit StepUnit
    { get; set; }

    /// <summary>
    ///   Default correlation between credit and counterparty
    /// </summary>
    double Correlation
    { get; set; }

    /// <summary>
    ///   Include maturity date in protection
    /// </summary>
    /// <exclude />
    bool IncludeMaturityProtection { get; }

    /// <summary>
    ///   Historical rate fixings
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Rate resets are stored as a SortedList.  The sort key is the date, and the
    ///   value is the rate, which means you can add the resets in any order but easily
    ///   retrieve them sorted by date.</para>
    /// </remarks>
    ///
    IList<RateReset> RateResets { get; } 

    #endregion Properties

  }
}
