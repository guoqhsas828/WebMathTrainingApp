/*
 * ICashflowPricer.cs
 *
 *  -2008. All rights reserved.
 * 
 */

using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using Cashflow = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Pricers
{
  ///
  /// <summary>
  ///   Common interface for cashflow-based pricers
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Pricers that leverage the <see cref="CashflowModel">Generalised Contingent Cashflow Model</see>
  ///   typically support this interface.</para>
  ///
  ///   <para>Allows use of the pricer in standard cashflow routines.</para>
  /// </remarks>
  ///
  internal interface ICashflowPricer : IPricer
  {
    #region Properties

    /// <summary>
    ///   Original notional amount for pricing
    /// </summary>
    double Notional { get; set; }

    /// <summary>
    ///   All future cashflows for this product.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    ///
    Cashflow Cashflow { get; }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    ///   Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
    DiscountCurve ReferenceCurve { get; set; }

    /// <summary>
    ///   Survival curve used for pricing
    /// </summary>
    SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    ///   Counterparty curve used for pricing
    /// </summary>
    SurvivalCurve CounterpartyCurve { get; set; }

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
    RecoveryCurve RecoveryCurve { get; set; }

    /// <summary>
    ///   Return the recovery rate matching the maturity of this product.
    /// </summary>
    double RecoveryRate { get; }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    int StepSize { get; set; }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    TimeUnit StepUnit { get; set; }

    /// <summary>
    ///   Default correlation between credit and counterparty
    /// </summary>
    double Correlation { get; set; }

    /// <summary>
    ///   Include maturity date in protection
    /// </summary>
    /// <exclude />
    bool IncludeMaturityProtection { get; }

    /// <summary>
    ///   Floating rate reset history
    /// </summary>
    IList<RateReset> RateResets { get; }

    /// <summary>
    /// Accrued Should be Discounted
    /// </summary>
    bool DiscountingAccrued { get; }

    /// <summary>
    /// Include Payments that fall on the settle date in the PV
    /// </summary>
    bool IncludeSettlePayments { get; }

    #endregion Properties

  } // interface ICashflowPricer
}
