/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  Represent a constant value, optionally dated
  /// </summary>
  public sealed class ConstantEvaluable
    : Evaluable, IDatedValue, IStructuralEquatable
  {
    private readonly double _value;
    private readonly Dt _date;

    /// <summary>Gets the underlying value</summary>
    public double Value { get { return _value; } }

    /// <summary>Gets the date associated with the value</summary>
    /// <value>The date associated with the value, or empty if no date associated</value>
    public Dt Date { get { return _date; } }

    internal ConstantEvaluable(double v)
    {
      _value = v;
    }

    internal ConstantEvaluable(double v, Dt date)
    {
      _value = v;
      _date = date;
    }

    public override string ToString()
    {
      return Value.ToString();
    }

    #region Evaluable overrides

    public override double Evaluate()
    {
      return _value;
    }

    protected override Expression Reduce()
    {
      return Expression.Constant(_value);
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var c = other as ConstantEvaluable;
      return c != null && c._value.Equals(_value)
        && c._date == _date;
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return _value.GetHashCode();
    }

    #endregion
  }
}
