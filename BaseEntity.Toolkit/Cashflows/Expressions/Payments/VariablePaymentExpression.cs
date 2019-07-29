/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Payments
{
  /// <summary>
  ///  Representing a payment with variable amount
  /// </summary>
  [DebuggerDisplay("{DebugDisplay}")]
  internal sealed class VariablePaymentExpression : PaymentExpression
    , IStructuralEquatable, IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    public readonly Evaluable DiscountedAmount;

    public static VariablePaymentExpression Create(
      Evaluable amount, Curve discountCurve, Dt payDt)
    {
      return Unique(new VariablePaymentExpression(
        amount, discountCurve, payDt));
    }

    private VariablePaymentExpression(
      Evaluable amount, Curve discountCurve, Dt payDt)
      : base(payDt, Interpolate(discountCurve, payDt))
    {
      DiscountedAmount = amount*Discount;
    }

    #region Overrides of Evaluable

    /// <summary>
    ///  Compute the value of this expression
    /// </summary>
    /// <returns>The value</returns>
    public override double Evaluate()
    {
      return DiscountedAmount.Evaluate();
    }

    /// <summary>
    /// Reduces this node to a simpler expression. If CanReduce returns true, this should return a valid expression. This method can return another node which itself must be reduced.
    /// </summary>
    /// <returns>
    /// The reduced expression.
    /// </returns>
    protected override Expression Reduce()
    {
      return DiscountedAmount.ToExpression();
    }

    #endregion

    #region IStructuralEquatable Members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var node = other as VariablePaymentExpression;
      return node != null
        && node.PayDt == PayDt
        && ReferenceEquals(node.DiscountedAmount, DiscountedAmount);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return DiscountedAmount.GetHashCode();
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 1; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return DiscountedAmount;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return DiscountedAmount;
    }

    #endregion

    #region IDebugDisplay members

    public string DebugDisplay
    {
      get
      {
        var d = DiscountedAmount as IDebugDisplay;
        return String.Format("PvAt({0}, {1})", PayDt,
          d == null ? DiscountedAmount.ToString() : d.DebugDisplay);
      }
    }

    #endregion
  }
}
