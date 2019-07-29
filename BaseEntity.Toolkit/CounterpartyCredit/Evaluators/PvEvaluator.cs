/*
 * 
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Cashflows.Expressions.Payments;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///  Pricer to calculate the forward mark to market values
  ///  from a streams of payments.
  /// </summary>
  public class PvEvaluator : CcrPricer, IPvEvaluator
  {
    private readonly PvParameter[] _exposures;
    private readonly PaymentExpression[] _pvNodes;
    private readonly Evaluable[] _accrueds;
    private readonly double _notional;

    public PvEvaluator(IPricer pricer,
      PvParameter[] exposures,
      PaymentExpression[] nodes,
      Evaluable[] accrueds,
      double notional)
      : base(pricer)
    {
      _exposures = exposures;
      _pvNodes = nodes;
      _accrueds = accrueds;
      _notional = notional;
    }

    /// <summary>
    /// Calculate the PV at the specified settle date
    /// </summary>
    /// <param name="exposureIndex">The index of the settlement date in the exposure dates</param>
    /// <param name="settle">The settlement date</param>
    /// <returns></returns>
    public double FastPv(int exposureIndex, Dt settle)
    {
      if (exposureIndex >= _exposures.Length)
        return 0;
      var pv = _pvNodes.FullPv(
        _exposures[exposureIndex].StartIndex,
        _exposures[exposureIndex].Date,
        _exposures[exposureIndex].DiscountFactor,
        _accrueds?[exposureIndex])*_notional;
      return pv;
    }

    #region Static constructor and helpers

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pricer"></param>
    /// <param name="exposureDates"></param>
    /// <param name="simulatedObjs"></param>
    /// <returns></returns>
    public static IPvEvaluator Get(
      IPricer pricer, IReadOnlyList<Dt> exposureDates,
      IEnumerable<object> simulatedObjs)
    {
      using (Evaluable.PushVariants(simulatedObjs))
      {
        return Get(pricer as PricerBase, exposureDates);
      }
    }

    public static IPvEvaluator Get(CcrPricer ccr,
      IReadOnlyList<Dt> exposureDates)
    {
      // We don't support inflation bond yet.
      if (ccr is InflationBondCcrPricer) return null;

      // All pricers goes here
      return OptionPvEvaluator.Get(ccr as OptionCcrPricer, exposureDates)
        ?? Get(ccr.Pricer as PricerBase, exposureDates);
    }

    private static IPvEvaluator Get(PricerBase p,
      IReadOnlyList<Dt> exposureDates)
    {
      if (p == null) return null;

      // Retrieve the payment schedule
      DiscountCurve curve;
      PaymentSchedule ps;
      if (!TryGetPaymentSchedule(p, out curve, out ps))
      {
        return null;
      }
      if (ToolkitConfigurator.Settings.CcrPricer.FixVolatilityForConvexityAdjustment)
      {
        ConvexityAdjustmentUtility.FixVolatility(ps, exposureDates);
      }

      // Retrieve the discount curve, payments and exposures
      var pmts = PaymentExpression.GetPayments(ps, curve).ToArray();
      var exposures = GetExposureNodes(exposureDates,
        pmts, GetExposureEndDate(p, GetLastPaymentDate(pmts)), curve);
      var accruedAdjs = GetDiscountingAccrued(p) ? null
        : PaymentExpression.GetAccruedAdjustments(ps, exposureDates, curve);


      Evaluable.RecordCommonExpressions(pmts);
      Evaluable.RecordCommonExpressions(accruedAdjs);
      foreach (var exposureParameter in exposures)
      {
        Evaluable.RecordCommonExpressions(exposureParameter);
      }

      // Now construct a pricer for simulation
      return new PvEvaluator((IPricer) p, exposures, pmts,
        accruedAdjs, GetNotionalScale(p))
      {
        ExposureDates = exposureDates.ToArray()
      };
    }

    #region DiscountingAccrued adjustments

    public static bool GetDiscountingAccrued(PricerBase p)
    {
      const string discountingaccrued = "DiscountingAccrued";
      if (p.HasPropertyOrField(discountingaccrued))
        return p.GetValue<bool>(discountingaccrued);
      if (p.Product.HasPropertyOrField(discountingaccrued))
        return p.Product.GetValue<bool>(discountingaccrued);
      // discountingAccrued should default to true
      return true;
    }

    #endregion

    #region Payments schedule extraction

    public static bool TryGetPaymentSchedule(PricerBase pricer,
      out DiscountCurve discountCurve,
      out PaymentSchedule paymentSchedule)
    {
      discountCurve = null;
      paymentSchedule = null;

      bool approx = CcrPricerUtils.SetApproximateForFastCalculation(pricer);
      try
      {
        var swap = pricer as IEnumerable<SwapLegPricer>;
        if (swap != null)
        {
          paymentSchedule = GetSwapPayments(swap);
        }
        else
        {
          var inflBond = pricer as InflationBondPricer;
          if (inflBond != null && inflBond.SurvivalCurve != null)
          {
            return false;
          }
          var fromDate = GetPsFromDate(pricer);
          paymentSchedule = pricer.GetPaymentSchedule(null, fromDate);
          var paymentPricer = TryGetPaymentPricer(pricer);
          if (paymentPricer != null)
          {
            var ps = paymentPricer.GetPaymentSchedule(null, fromDate);
            if (ps != null)
              paymentSchedule.AddPayments(Scale(ps, 1/GetNotionalScale(pricer)));
          }
        }
        discountCurve = pricer.GetDiscountCurve();
        return true;
      }
      catch (NotImplementedException)
      {
        return false;
      }
      finally
      {
        pricer.ApproximateForFastCalculation = approx;
      }
    }

    public static PricerBase TryGetPaymentPricer(PricerBase pricer)
    {
      try
      {
        return (PricerBase)pricer.PaymentPricer;
      }
      catch (NotImplementedException)
      {
        return null;
      }
    }

    public static PaymentSchedule GetSwapPayments(
      IEnumerable<SwapLegPricer> swapPricer)
    {
      var swapScale = GetNotionalScale((PricerBase) swapPricer);
      var retVal = new PaymentSchedule();
      foreach (var pricer in swapPricer)
      {
        var scale = GetNotionalScale(pricer)/swapScale;
        bool approx = CcrPricerUtils.SetApproximateForFastCalculation(pricer);
        var ps = pricer.GetPaymentSchedule(null, pricer.Settle);
        pricer.ApproximateForFastCalculation = approx;
        if (ps != null) retVal.AddPayments(Scale(ps, scale));
        var paymentPricer = TryGetPaymentPricer(pricer);
        if (paymentPricer != null)
        {
          var ufps = paymentPricer.GetPaymentSchedule(null, pricer.Settle);
          if (ufps != null) retVal.AddPayments(Scale(ufps, 1 / swapScale));
        }
      }
      return retVal;
    }

    public static double GetNotionalScale(PricerBase pricer)
    {
      if (pricer is SimpleCashflowPricer)
        return pricer.Notional;
      var productNotional = pricer.Product.Notional;
      return productNotional.Equals(0.0)
        ? pricer.Notional : (pricer.Notional / productNotional);
    }

    public static IEnumerable<Payment> Scale(
      IEnumerable<Payment> payments, double scale)
    {
      return scale.AlmostEquals(1.0) ? payments
        : payments.Select(p => new ScaledPayment(p, scale));
    }

    #endregion

    #region Pv by exposure dates

    public static PvParameter[] GetExposureNodes(
      IReadOnlyList<Dt> exposureDates,
      PaymentExpression[] nodes, Dt maturity,
      DiscountCurve discountCurve)
    {
      var n = exposureDates.Count;
      var exposures = new List<PvParameter>();
      for (int i = 0; i < n; ++i)
      {
        var settle = exposureDates[i];
        if (settle >= maturity) break;
        var start = GetStartIndex(nodes, settle, false);
        if (start < 0) break;
        exposures.Add(new PvParameter(start,
          Evaluable.Interpolate(discountCurve, settle)));
      }
      return exposures.ToArray();
    }

    private static int GetStartIndex(PaymentExpression[] nodes,
      Dt settle, bool includeSettlePayments)
    {
      int idx = 0;
      for (int i = 0; i < nodes.Length; ++i, ++idx)
      {
        var cmp = Dt.Cmp(nodes[i].PayDt, settle);
        if (cmp < 0 || (cmp == 0 && !includeSettlePayments))
          continue;
        break;
      }
      if (idx >= nodes.Length)
        return -1;
      return idx;
    }

    public static Dt GetExposureEndDate(PricerBase pricer, Dt lastPyDt)
    {
      Dt maturity = pricer.Product.Maturity;
      if (maturity < lastPyDt) maturity = lastPyDt;
      Dt mutualPutDt = GetNextBreakDate(pricer);
      if (mutualPutDt.IsValid() && maturity > mutualPutDt)
      {
        return mutualPutDt;
        //for cpty risk purposes, we can safely assume that mutual put is exercised
      }
      return maturity;
    }

    public static Dt GetNextBreakDate(PricerBase pricer)
    {
      switch (pricer)
      {
        case SwapLegPricer swapleg:
          return swapleg.SwapLeg.NextBreakDate;
        case SwapPricer swap:
          var dt1 = GetNextBreakDate(swap.PayerSwapPricer);
          var dt2 = GetNextBreakDate(swap.ReceiverSwapPricer);
          if (dt1.IsEmpty()) return dt2;
          if (dt2.IsEmpty()) return dt1;
          return dt1 < dt2 ? dt1 : dt2;
        case MultiLeggedSwapPricer multiLeggedSwapPricer:
          var breakDates = multiLeggedSwapPricer.SwapLegPricers.Select(slp => slp.SwapLeg.NextBreakDate).ToList();
          if (breakDates.Any(d => !d.IsEmpty()))
            return breakDates.Where(d => !d.IsEmpty()).Min();
          break;
      }

      return Dt.Empty;
    }

    public static Dt GetLastPaymentDate(IReadOnlyList<PaymentExpression> ps)
    {
      return (ps == null || ps.Count <= 0) ? Dt.Empty : ps[ps.Count - 1].PayDt;
    }

    #endregion

    #endregion

    #region Overrides of CcrPricer

    /// <summary>
    /// Net present value of a stream of future cash flows 
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Pv discounted to settle</returns>
    /// <remarks>The public state of the pricers might be modified</remarks>
    public override double FastPv(Dt settle)
    {
      throw new NotImplementedException();
    }

    #endregion
  }


  public static class PricerUtility
  {
    public static DiscountCurve GetDiscountCurve(this IPricer p)
    {
      return FindDiscountCurve(p);
    }

    public static DiscountCurve GetDiscountCurve(this PricerBase p)
    {
      return FindDiscountCurve(p);
    }

    public static DiscountCurve GetReferenceCurve(this PricerBase p)
    {
      return FindReferenceCurve(p);
    }

    public static SurvivalCurve GetSurvivalCurve(this IPricer p)
    {
      return FindSurvivalCurve(p);
    }

    public static SurvivalCurve GetSurvivalCurve(this PricerBase p)
    {
      return FindSurvivalCurve(p);
    }

    private static DiscountCurve FindDiscountCurve(object p)
    {
      // Retrieve the discount curve
      const string discountcurve = "DiscountCurve";
      if (!p.HasPropertyOrField(discountcurve))
        return null;
      return p.GetValue<DiscountCurve>(discountcurve);
    }

    private static DiscountCurve FindReferenceCurve(object p)
    {
      // Retrieve the discount curve
      const string referenceCurve = "ReferenceCurve";
      if (!p.HasPropertyOrField(referenceCurve))
        return null;
      return p.GetValue<DiscountCurve>(referenceCurve);
    }

    private static SurvivalCurve FindSurvivalCurve(object p)
    {
      // Retrieve the discount curve
      const string discountcurve = "SurvivalCurve";
      if (!p.HasPropertyOrField(discountcurve))
        return null;
      return p.GetValue<SurvivalCurve>(discountcurve);
    }
  }
}
