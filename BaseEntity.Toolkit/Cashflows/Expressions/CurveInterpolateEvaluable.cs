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
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  public abstract class CurveInterpolateEvaluable : ResettableValue
  {
    public readonly Curve Curve;

    protected CurveInterpolateEvaluable(Curve curve)
    {
      Debug.Assert(curve != null);
      Curve = curve;
    }

    /// <summary>
    ///  Gets debugger display string
    /// </summary>
    internal string GetDebugDisplay(string dateDisplay)
    {
      var name = Curve.Name;
      return string.Format("{0}{{{1}}}.Interpolate({2})",
        Curve.GetType().Name,
        string.IsNullOrEmpty(name) ? Curve.Id.ToString() : name,
        dateDisplay);
    }

    protected static readonly MethodInfo InterpolateDateMethod
      = GetMethod<Func<Curve, Dt, double>>((c, d) => c.Interpolate(d));

    protected static readonly MethodInfo InterpolateTimeMethod = GetMethod<
      Func<NativeCurve, double, double>>((c, d) => c.Evaluate(d));
  }

  [DebuggerDisplay("{DebugDisplay}")]
  public sealed class CurveInterpolateConstantTime
    : CurveInterpolateEvaluable, IDatedValue, IStructuralEquatable
      , IDebugDisplay
  {
    private readonly NativeCurve _nativeCurve;
    private readonly Dt _date;
    private readonly double _time;

    public Dt Date { get { return _date; } }

    public CurveInterpolateConstantTime(Curve curve, Dt date)
      : base(curve)
    {
      Debug.Assert(!date.IsEmpty());
      _nativeCurve = curve;
      _date = date;
      var asOf = curve.AsOf;
      _time = Dt.Cmp(asOf, date) > 0
        ? -Dt.FractDiff(date, asOf)
        : Dt.FractDiff(asOf, date);
    }

    #region Overrides of Evaluable

    /// <summary>
    /// Reduces this node to a simpler expression. If CanReduce returns true, this should return a valid expression. This method can return another node which itself must be reduced.
    /// </summary>
    /// <returns>
    /// The reduced expression.
    /// </returns>
    protected override Expression Reduce()
    {
      return Expression.Call(Expression.Constant(_nativeCurve),
        InterpolateTimeMethod, Expression.Constant(_time));
    }

    #endregion

    #region ResettableValue overrides

    protected override double Compute()
    {
      return _nativeCurve.Evaluate(_time);
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var ci = other as CurveInterpolateConstantTime;
      return ci != null && ci.Curve.Id == Curve.Id && ci._date == _date;
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Curve.Id.GetHashCode(), _date.GetHashCode());
    }

    #endregion

    #region IDebugDisplay member

    public string DebugDisplay
    {
      get { return GetDebugDisplay(_time.ToString()); }
    }

    #endregion
  }

  [DebuggerDisplay("{DebugDisplay}")]
  public sealed class CurveInterpolateConstantDate
    : CurveInterpolateEvaluable, IDatedValue, IStructuralEquatable
      , IDebugDisplay
  {
    private readonly Dt _date;

    public Dt Date { get { return _date; } }

    public CurveInterpolateConstantDate(Curve curve, Dt date)
      : base(curve)
    {
      Debug.Assert(!date.IsEmpty());
      _date = date;
    }

    #region Overrides of Evaluable

    /// <summary>
    /// Reduces this node to a simpler expression. If CanReduce returns true, this should return a valid expression. This method can return another node which itself must be reduced.
    /// </summary>
    /// <returns>
    /// The reduced expression.
    /// </returns>
    protected override Expression Reduce()
    {
      return Expression.Call(Expression.Constant(Curve),
        InterpolateDateMethod, Expression.Constant(_date));
    }

    #endregion

    #region ResettableValue overrides

    protected override double Compute()
    {
      return Curve.Interpolate(_date);
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var ci = other as CurveInterpolateConstantDate;
      return ci != null && ci.Curve.Id == Curve.Id && ci._date == _date;
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Curve.Id.GetHashCode(), _date.GetHashCode());
    }

    #endregion

    #region IDebugDisplay member

    public string DebugDisplay
    {
      get { return GetDebugDisplay(_date.ToString()); }
    }

    #endregion
  }

  [DebuggerDisplay("{DebugDisplay}")]
  public sealed class CurveInterpolateVariableDate
    : CurveInterpolateEvaluable, IStructuralEquatable, IDebugDisplay
  {
    /// <summary>
    ///  Gets the date variable
    /// </summary>
    public readonly IVariableDate DateVariable;

    public CurveInterpolateVariableDate(
      Curve curve, IVariableDate date) : base(curve)
    {
      Debug.Assert(date != null);
      DateVariable = date;
    }

    #region Overrides of Evaluable

    /// <summary>
    /// Reduces this node to a simpler expression. If CanReduce returns true, this should return a valid expression. This method can return another node which itself must be reduced.
    /// </summary>
    /// <returns>
    /// The reduced expression.
    /// </returns>
    protected override Expression Reduce()
    {
      return Expression.Call(Expression.Constant(Curve),
        InterpolateDateMethod, DateVariable.GetExpression());
    }

    #endregion

    #region ResettableValue overrides

    protected override double Compute()
    {
      return Curve.Interpolate(DateVariable.Date);
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var ci = other as CurveInterpolateVariableDate;
      return ci != null && ci.Curve.Id == Curve.Id
        && ReferenceEquals(ci.DateVariable, DateVariable);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Curve.Id.GetHashCode(), DateVariable.GetHashCode());
    }

    #endregion

    #region IDebugDisplay member

    public string DebugDisplay
    {
      get { return GetDebugDisplay(DateVariable.Name); }
    }

    #endregion
  }
}
