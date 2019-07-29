/*
 *  -2015. All rights reserved.
 */

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Payments
{
  public static partial class CashflowEvaluator
  {
    #region Static methods to calculate full Pv

    public static double FullPv(this PaymentExpression[] payments,
      int startIndex, Dt settleDate, double settleDicountFactor,
      Evaluable accruedAdjustment = null)
    {
      int idx = startIndex;
      if (idx < 0) return 0.0;

      PricingDate.Value = settleDate;
      var pv = accruedAdjustment == null ? 0.0 : accruedAdjustment.Evaluate();
      for (int i = idx; i < payments.Length; ++i)
      {
        //p.VolatilityStartDt = settle;
        pv += payments[i].Evaluate();
      }
      return pv / settleDicountFactor;
    }

    #endregion
  }
}
