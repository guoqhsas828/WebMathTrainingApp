using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  internal static class PricerUtils
  {
    internal static double? FloatingCouponAt(this PricerBase pricer, Dt asOf, Dt settle, RateResets rateResets, bool includeProjection)
    {
      var product = pricer.Product as Product;
      if (product == null)
        return null;

      if (rateResets != null && rateResets.HasCurrentReset)
        return rateResets.CurrentReset;

      FloatingInterestPayment ip;
      if (!TryGetCurrentPeriod(pricer, settle, product.CustomPaymentSchedule == null ? settle : Dt.Empty, out ip)) return null;

      var resetDt = ip.ResetDate;
      var state = ip.RateResetState;
      if (resetDt <= asOf)
      {
        if (state == RateResetState.Missing)
          throw new ToolkitException(String.Format("Missing Rate Reset for {0}", resetDt));
      }
      if (includeProjection || state != RateResetState.IsProjected)
        return ip.EffectiveRate;
      return null;
    }

    internal static double? FixedCouponAt(this PricerBase pricer, Dt settle, double coupon, IList<CouponPeriod> couponSchedule)
    {
      if (!(pricer.Product is Product))
        return null;

      // When there is no customized schedule, just use the coupon schedule, if any.
      if (((Product)pricer.Product).CustomPaymentSchedule == null || ((Product)pricer.Product).CustomPaymentSchedule.Count == 0)
      {
        return CouponPeriodUtil.CouponAt(settle, coupon, couponSchedule, -1.0);
      }

      FixedInterestPayment ip;
      if (!TryGetCurrentPeriod(pricer, settle, Dt.Empty, out ip)) return null;

      if (ip != null)
        return ip.EffectiveRate;
      return null;
    }

    private static bool TryGetCurrentPeriod<T>(PricerBase pricer, Dt settle, Dt from, out T ip) where T : InterestPayment
    {
      ip = null;
      var ps = pricer.GetPaymentSchedule(null, from);
      if (ps == null) return false;

      var ipList = ps.GetPaymentsByType<T>();
      if (ipList == null) return false;
      ip = ipList.TakeWhile(item => item.AccrualStart <= settle).LastOrDefault();

      return ip != null;
    }
  }
}
