/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Payments
{
  public abstract class PaymentExpression : Evaluable
  {
    #region Instance members

    public readonly Evaluable Discount;
    public readonly Dt PayDt;

    protected PaymentExpression(Dt payDt, Evaluable discount)
    {
      Discount = discount;
      PayDt = payDt;
    }

    public double DiscountFactor()
    {
      return Discount.Evaluate();
    }

    #endregion

    #region Convert payment schedule to evaluable

    public static IEnumerable<PaymentExpression> GetPayments(
      IEnumerable<Payment> payments, Curve discountCurve,
      Func<Payment, Evaluable> getter = null)
    {
      foreach (var grp in payments.GroupBy(p => p.PayDt).OrderBy(g => g.Key))
      {
        var payDt = grp.Key;
        var amount = grp.Aggregate(Zero, (a, p) => a + GetAmount(p, getter));
        var ce = amount as ConstantEvaluable;
        if (ce != null)
          yield return Unique(FixedPaymentExpression.Create(
            ce.Value, discountCurve, payDt));
        else
          yield return Unique(VariablePaymentExpression.Create(
            amount, discountCurve, payDt));
      }
    }

    private static Evaluable GetAmount(
      Payment payment, Func<Payment, Evaluable> getter)
    {
      var scale = GetScale(ref payment);
      var fxRate = GetFxRate(payment) ?? One;
      if (!payment.IsProjected)
      {
        return (payment.Amount*scale)*fxRate;
      }

      return (getter?.Invoke(payment) ??
        payment.GetEvaluableAmount())*scale*fxRate;
    }

    private static double GetScale(ref Payment payment)
    {
      var scaled = payment as ScaledPayment;
      if (scaled == null) return 1.0;
      payment = scaled.UnderlyingPayment;
      return scaled.Notional;
    }

    internal static Evaluable[] GetAccrueds(
      IEnumerable<Payment> payments,
      IReadOnlyList<Dt> pricingDates,
      Curve discountCurve,
      Func<Payment, Evaluable> getter = null)
    {
      return GetAccrueds(false, payments,
        pricingDates, discountCurve, getter);
    }

    internal static Evaluable[] GetAccruedAdjustments(
      IEnumerable<Payment> payments,
      IReadOnlyList<Dt> pricingDates,
      Curve discountCurve,
      Func<Payment, Evaluable> getter = null)
    {
      return GetAccrueds(true, payments,
        pricingDates, discountCurve, getter);
    }

    private static Evaluable[] GetAccrueds(
      bool adjustmentOnly,
      IEnumerable<Payment> payments,
      IReadOnlyList<Dt> pricingDates,
      Curve discountCurve,
      Func<Payment, Evaluable> getter)
    {
      int count = pricingDates.Count;
      Evaluable[] adjustments = null;
      foreach (var node in payments)
      {
        var payment = node;
        var scale = GetScale(ref payment);
        var ip = payment as InterestPayment;
        if (ip == null) continue;
        var period = GetPeriod(ip);

        for (int i = 0; i < count; ++i)
        {
          var settle = pricingDates[i];
          var cmp = Dt.Cmp(settle, ip.PayDt);
          if (cmp >= 0) break;
          cmp = Dt.Cmp(settle, period.Begin);
          if (cmp <= 0) continue;
          var adj = GetAccrued(adjustmentOnly, ip,
            scale, discountCurve, settle, getter);
          if (adj == Zero) continue;
          if (adjustments == null)
          {
            adjustments = new Evaluable[count];
            adjustments[i] = adj;
          }
          else if (adjustments[i] == null)
            adjustments[i] = adj;
          else
            adjustments[i] += adj;
        }
      }
      return adjustments;
    }

    private static Evaluable GetAccrued(
      bool adjustmentOnly,
      InterestPayment payment, double scale,
      Curve discountCurve, Dt settle,
      Func<Payment, Evaluable> getter)
    {
      if (settle == payment.PayDt)
        return Zero;
      var period = GetPeriod(payment);
      var ratio = Dt.Diff(period.Begin, settle, period.DayCount)/period.Diff();
      if (ratio.AlmostEquals(0.0))
        return Zero;
      var accrued = ratio*scale*GetAmount(payment, getter);
      return adjustmentOnly
        ? (accrued*Interpolate(discountCurve, settle))
        : (accrued*(Interpolate(discountCurve, settle)
          - Interpolate(discountCurve, payment.PayDt)));
    }


    private static Evaluable GetFxRate(Payment payment)
    {
      var fxCurve = payment.FXCurve;
      if (fxCurve == null)
        return null;
      var Ccy = payment.Ccy;
      var toCcy = (Ccy == fxCurve.SpotFxRate.FromCcy)
        ? fxCurve.SpotFxRate.ToCcy
        : fxCurve.SpotFxRate.FromCcy;
      return FxRate(fxCurve, payment.PayDt, Ccy, toCcy);
    }

    internal static AccrualPeriod GetPeriod(InterestPayment payment)
    {
      return payment == null ? null : AccrualPeriod.Create(
        payment.AccrualStart, payment.AccrualEnd, payment.DayCount);
    }

    #endregion
  }

}
