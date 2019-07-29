/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions.Utilities;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  [DebuggerDisplay("{" + nameof(DebugDisplay) + "}")]
  internal sealed class FxForwardEvaluable : ResettableValue
    , IStructuralEquatable, IDebugDisplay
  {
    public readonly Dt Date;
    public readonly FxCurve FxCurve;
    public readonly Currency FromCcy, ToCcy;

    public FxForwardEvaluable(FxCurve curve, Dt date, Currency from, Currency to)
    {
      Date = date;
      FxCurve = curve;
      FromCcy = from;
      ToCcy = to;
    }

    protected override double Compute()
    {
      return FxCurve.FxRate(Date, FromCcy, ToCcy);
    }

    #region Evaluable overrides

    protected override Expression Reduce()
    {
      return Expression.Call(Expression.Constant(FxCurve),
        FxRateMethod, Expression.Constant(Date),
        Expression.Constant(FromCcy), Expression.Constant(ToCcy));
    }

    private static MethodInfo FxRateMethod =
      GetMethod<Func<FxCurve, Dt, Currency, Currency, double>>(
      (fx, dt, ccy1, ccy2) => fx.FxRate(dt, ccy1, ccy2));

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as FxForwardEvaluable;
      return o != null && o.Date == Date &&
        o.FxCurve.Id == FxCurve.Id && o.FromCcy == FromCcy;
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Date.GetHashCode(),
        FxCurve.Id.GetHashCode(), FromCcy.GetHashCode());
    }

    #endregion

    public string DebugDisplay
    {
      get { return String.Format("FxForward({0}, {1}{2})", Date, FromCcy, ToCcy); }
    }
  }
}
