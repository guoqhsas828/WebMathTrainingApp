//
// RecoverySwapPricer.cs
//  -2008. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// <para>Price a <see cref="BaseEntity.Toolkit.Products.RecoverySwap">RecoverySwap</see> using the
  /// <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.</para>
  /// </summary>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.RecoverySwap" />
  /// <remarks>
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.RecoverySwap">Recovery Swap Product</seealso>
  /// <seealso cref="CashflowPricer">Cashflow pricing</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow model</seealso>
  [Serializable]
  public partial class RecoverySwapPricer : CashflowPricer, IRecoverySensitivityCurvesGetter
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    ///
    public
    RecoverySwapPricer(RecoverySwap product)
      : base(product)
    { }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    /// <param name="recoveryRate">Recovery rate</param>
    ///
    public
    RecoverySwapPricer(RecoverySwap product,
                        Dt asOf,
                        Dt settle,
                        DiscountCurve discountCurve,
                        SurvivalCurve survivalCurve,
                        int stepSize,
                        TimeUnit stepUnit,
                        double recoveryRate)
      : base(product, asOf, settle,
              discountCurve, discountCurve, survivalCurve,
              stepSize, stepUnit, GetRecoveryCurve(asOf, recoveryRate))
    { }

    /// <summary>
    ///   Constructor with counterparty risks
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve of underlying credit</param>
    /// <param name="counterpartySurvivalCurve">Survival Curve of counterparty</param>
    /// <param name="correlation">Correlation between credit and conterparty defaults</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    /// <param name="recoveryRate">Recovery rate</param>
    ///
    public
    RecoverySwapPricer(RecoverySwap product,
                        Dt asOf,
                        Dt settle,
                        DiscountCurve discountCurve,
                        SurvivalCurve survivalCurve,
                        SurvivalCurve counterpartySurvivalCurve,
                        double correlation,
                        int stepSize,
                        TimeUnit stepUnit,
                        double recoveryRate)
      : base(product, asOf, settle,
              discountCurve, discountCurve,
              survivalCurve, counterpartySurvivalCurve, correlation,
              stepSize, stepUnit, GetRecoveryCurve(asOf, recoveryRate))
    { }

    #endregion // Constructors

    #region Methods

    private static RecoveryCurve GetRecoveryCurve(Dt asOf, double recoveryRate)
    {
      return Double.IsNaN(recoveryRate) ? null : new RecoveryCurve(asOf, recoveryRate);
    }

    /// <summary>
    ///   Calculate break-even premium for a Credit Default Swap
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a
    ///   zero MTM value.</para>
    /// </remarks>
    ///
    /// <returns>Break-even premium for Credit Default Swap in percent</returns>
    ///
    public double
    BreakEvenRecovery()
    {
      // Price stream of 1bp premiums matching CDS
      RecoverySwap swap = RecoverySwap;
      PaymentSchedule protectPayments;
      var feePayments = GetSeparatePayments(null, swap, AsOf, 1.0 + swap.RecoveryRate, 
        out protectPayments);

      bool discountingAccrued = DiscountingAccrued;
      double fee = feePayments.CalculatePv(Settle, Settle, DiscountCurve,
        SurvivalCurve, CounterpartyCurve, Correlation, StepSize, StepUnit,
        AdapterUtil.CreateFlags(discountingAccrued, false, false));

      double swap01 = protectPayments.CalculatePv(Settle, Settle, DiscountCurve,
        SurvivalCurve, CounterpartyCurve, Correlation, StepSize, StepUnit,
        AdapterUtil.CreateFlags(discountingAccrued, false, false));

      double recoveryRate = swap.RecoveryRate + fee / swap01;
      return recoveryRate;
    }

 
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ps"></param>
    /// <param name="from"></param>
    /// <returns></returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if(ps == null)
        ps = new PaymentSchedule();
      if (SurvivalCurve.DefaultDate.IsValid() 
        && (from >= SurvivalCurve.DefaultDate
        || SurvivalCurve.Defaulted == Defaulted.WillDefault))
      {
        Dt defaultDate, dfltSettle;
        double recoverRate;

        SurvivalCurve.GetDefaultDates(null, out defaultDate, 
          out dfltSettle, out recoverRate);
        if (!dfltSettle.IsValid())
        {
          // If we do not have the default settlement date specified yet, 
          // estimate it as the next business day ...
          dfltSettle = Dt.AddDays(Settle, 1, RecoverySwap.Calendar);
        }
        var notional = RecoverySwap.Notional > 0.0 ? RecoverySwap.Notional : 1.0;
        double defaultAmount = RecoveryRate - RecoverySwap.RecoveryRate;
        ps.AddPayment(new DefaultSettlement(defaultDate, dfltSettle,
          RecoverySwap.Ccy, notional, 1 + defaultAmount / notional));
      }
      else
      {
        PaymentSchedule recoveryPayments;
        ps.AddPaymentSchedule(GetSeparatePayments(null, RecoverySwap, from,
          RecoveryRate, out recoveryPayments));
        ps.AddPaymentSchedule(recoveryPayments);
      }

      return ps;
    }

    private static PaymentSchedule GetSeparatePayments(
      PaymentSchedule feePayments, RecoverySwap swap, Dt asOf, 
      double recoveryRate, out PaymentSchedule protectP)
    {
      if (feePayments == null)
        feePayments = new PaymentSchedule();
      var rps = new PaymentSchedule();
      Dt feeSettle = swap.FeeSettle;
      if (feeSettle.Month <= 0)
        feeSettle = asOf;

      double defaultAmount = recoveryRate - swap.RecoveryRate;
      var swapFee = swap.Fee;
      if (!swapFee.AlmostEquals(0.0))
      {
        feePayments.AddPayment(new PrincipalExchange(feeSettle, swapFee, swap.Ccy));
        rps.AddPayment(new RecoveryPayment(feeSettle, feeSettle,
            defaultAmount, swap.Ccy)
          { IsFunded = true, Notional = 1.0 });
      }
      rps.AddPayment(new RecoveryPayment(asOf,
          swap.Maturity, defaultAmount, swap.Ccy)
        { IsFunded = true, Notional = 1.0 });

      protectP = rps;
      return feePayments;
    }


    ///<summary>
    /// Pricer that will be used to price any additional (e.g. upfront) payment
    /// associated with the pricer.
    ///</summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    #endregion // Methods

    #region Explicit Implementation of Recovery Sensitivity

    /// <summary>
    /// Gets the Recovery Curve.
    /// </summary>
    /// 
    /// <returns>List of Curves</returns>
    /// 
    IList<Curve> IRecoverySensitivityCurvesGetter.GetCurves()
    {
      var curves = new List<Curve>();
      curves.Add(this.RecoveryCurve);
      return curves;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Product to price
    /// </summary>
    public RecoverySwap RecoverySwap
    {
      get { return (RecoverySwap)Product; }
    }

    #endregion // Properties

  } // class RecoverySwapPricer

}
