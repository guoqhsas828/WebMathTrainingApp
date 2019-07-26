
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Native;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves.Native;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Models
{
  [ReadOnly(true)]
  public partial class CashflowStreamModel
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CashflowStreamModel));

    #region Config

    /// <summary>static constructor</summary>
    /// <exclude />
    static CashflowStreamModel()
    {
      setDiscountingAccrued(false);//ToolkitConfigurator.Settings.CashflowPricer.DiscountingAccrued);
    }

    /// <summary>DiscountingAccrued</summary>
    /// <exclude />
    [Category("Base")]
    public static bool DiscountingAccrued
    {
      get { return getDiscountingAccrued(); }
      set { setDiscountingAccrued(value); }
    }

    #endregion // Config

    #region Wrappers
    /// <exclude/>
    public static double ProtectionPv(CashflowStream cf, Dt asOf, Dt settle, Curve discountCurve, Curve lossCurve, Curve amorCurve, double defaultTimeFraction, double accruedFractionOnDefault, bool includeSettlePayments, int step, TimeUnit stepUnit)
    {
      double ret = BaseEntityPINVOKE.CashflowStreamModel_ProtectionPv(Cashflow.getCPtr(cf.WrappedCashflow), cf.GetUpfrontFee(), cf.GetFeeSettle(), asOf, settle, Curve.getCPtr(discountCurve), Curve.getCPtr(lossCurve), Curve.getCPtr(amorCurve), defaultTimeFraction, accruedFractionOnDefault, includeSettlePayments, step, (int)stepUnit);
      if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    }

    /// <exclude />
    public static double FeePv(CashflowStream cf, Dt asOf, Dt settle, Curve discountCurve, Curve lossCurve, Curve amorCurve, double defaultTimeFraction, double accruedFractionOnDefault, bool includeSettlePayments, int step, TimeUnit stepUnit)
    {
      double ret = BaseEntityPINVOKE.CashflowStreamModel_FeePv(Cashflow.getCPtr(cf.WrappedCashflow), cf.GetUpfrontFee(), cf.GetFeeSettle(), asOf, settle, Curve.getCPtr(discountCurve), Curve.getCPtr(lossCurve), Curve.getCPtr(amorCurve), defaultTimeFraction, accruedFractionOnDefault, includeSettlePayments, step, (int)stepUnit);
      if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    }

    /// <summary>
    ///   <para>Calculates forward value of set of cash flows to a forward settlement date
    ///   given a market full price.</para>
    ///
    ///   <para>To simply calculate the forward price of the CashflowStream stream, use the pv()
    ///   method.</para>
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If includeSettlePayments is true, CashflowStreams on or after settlement date are
    ///   included, otherwise CashflowStreams after settlement date are included</para>
    ///
    ///   <para>Includes counterparty default risk.</para>
    /// </remarks>
    ///
    /// <param name="cf">CashflowStream to fv</param>
    /// <param name="settle">Natural settlement date for product</param>
    /// <param name="price">Full market price for CashflowStream stream</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve for probability weighting cash flows.</param>
    /// <param name="counterpartyCurve">Survival curve of counterparty</param>
    /// <param name="correlation">Default correlation between the credit and the counterparty</param>
    /// <param name="recoveryType">Type of recovery rate</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="recoveryDispersion">Recovery rate dispersion</param>
    /// <param name="includeSettlePayments">Include payments on the settlement date</param>
    /// <param name="defaultTiming">Fraction of period default is assumed to occur (typically 0.5)</param>
    /// <param name="step">Size of pricing grid</param>
    /// <param name="stepUnit">Units of step size</param>
    /// <param name="forwardDate">Forward date</param>
    ///
    /// <returns>forward value of CashflowStreams</returns>
    public static double FwdValue(
      CashflowStream cf, Dt settle, double price,
      Curve discountCurve, Curve survivalCurve, Curve counterpartyCurve, double correlation,
      RecoveryType recoveryType, double recoveryRate, double recoveryDispersion,
      bool includeSettlePayments, double defaultTiming, int step, TimeUnit stepUnit, Dt forwardDate)
    {
      return FwdValue(cf.WrappedCashflow, cf.GetUpfrontFee(), cf.GetFeeSettle(),
        settle, price, discountCurve, survivalCurve, counterpartyCurve,
        correlation, recoveryType, recoveryRate, recoveryDispersion,
        includeSettlePayments, false, defaultTiming, step, stepUnit, forwardDate);
    }

    /// <summary>
    ///   Calculates IRR (yield) of cash flows under no default in specified rate
    ///   basis given full price.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If includeSettlePayments is true, CashflowStreams on or after settlement date are
    ///   included, otherwise CashflowStreams after settlement date are included</para>
    ///
    ///   <para>Includes counterparty default risk.</para>
    /// </remarks>
    ///
    /// <param name="cf">CashflowStream to fv</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="survivalCurve">Survival curve for probability weighting cash flows.</param>
    /// <param name="counterpartyCurve">Survival curve of counterparty</param>
    /// <param name="correlation">Default correlation between the credit and the counterparty</param>
    /// <param name="recoveryType">Type of recovery rate</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="recoveryDispersion">Recovery rate dispersion</param>
    /// <param name="includeSettlePayments">Include payments on the settlement date</param>
    /// <param name="defaultTiming">Fraction of period default is assumed to occur (typically 0.5)</param>
    /// <param name="step">Size of pricing grid</param>
    /// <param name="stepUnit">Units of step size</param>
    /// <param name="price">Full price of CashflowStreams</param>
    /// <param name="daycount">Daycount for rate</param>
    /// <param name="freq">Compounding frequency for rate</param>
    ///
    /// <returns>Irr (yield) implied by price</returns>
    public static double Irr(
      CashflowStream cf, Dt asOf, Dt settle,
      Curve survivalCurve, Curve counterpartyCurve, double correlation,
      RecoveryType recoveryType, double recoveryRate, double recoveryDispersion,
      bool includeSettlePayments, double defaultTiming, int step, TimeUnit stepUnit,
      double price, DayCount daycount, Frequency freq)
    {
      double ret = BaseEntityPINVOKE.CashflowStreamModel_Irr(
        Cashflow.getCPtr(cf.WrappedCashflow),
        cf.GetUpfrontFee(), cf.GetFeeSettle(), asOf, settle,
        Curve.getCPtr(survivalCurve), Curve.getCPtr(counterpartyCurve), correlation,
        (int)recoveryType, recoveryRate, recoveryDispersion, includeSettlePayments, false,
        defaultTiming, step, (int)stepUnit, price, (int)daycount, (int)freq);
      if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    }

    /// <summary>
    ///   Calculates constant (continuously compounded) spread over the discount
    ///   curve for the pv of the CashflowStreams to match the specified full price.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If includeSettlePayments is true, CashflowStreams on or after settlement date are
    ///   included, otherwise CashflowStreams after settlement date are included</para>
    ///
    ///   <para>Includes counterparty default risk.</para>
    ///
    ///   <para>This is also commonly called the Z-Spread.</para>
    /// </remarks>
    ///
    /// <param name="cf">CashflowStream to fv.</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve for probability weighting cash flows.</param>
    /// <param name="counterpartyCurve">Survival curve of counterparty</param>
    /// <param name="correlation">Default correlation between the credit and the counterparty</param>
    /// <param name="recoveryType">Type of recovery rate</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="recoveryDispersion">Recovery rate dispersion</param>
    /// <param name="includeSettlePayments">Include payments on the settlement date</param>
    /// <param name="defaultTiming">Fraction of period default is assumed to occur (typically 0.5)</param>
    /// <param name="step">Size of pricing grid</param>
    /// <param name="stepUnit">Units of step size</param>
    /// <param name="price">Target full price</param>
    ///
    /// <returns>spread over discount curve implied by price</returns>
    public static double ImpDiscountSpread(
      CashflowStream cf, Dt asOf, Dt settle,
      Curve discountCurve, Curve survivalCurve, Curve counterpartyCurve, double correlation,
      RecoveryType recoveryType, double recoveryRate, double recoveryDispersion,
      bool includeSettlePayments, double defaultTiming, int step, TimeUnit stepUnit, double price)
    {
      return ImpDiscountSpread(cf.WrappedCashflow, cf.GetUpfrontFee(),
        cf.GetFeeSettle(), asOf, settle, discountCurve, survivalCurve,
        counterpartyCurve, correlation, recoveryType, recoveryRate,
        recoveryDispersion, includeSettlePayments, false, defaultTiming, step,
        stepUnit, price);
    }
    #endregion Wrappers
  }
}