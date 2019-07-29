/*
 *   2005-2016. All rights reserved.
 * 
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  A date variable represented as an evaluable.
  ///  This class cannot be inherited.
  /// </summary>
  [DebuggerDisplay("{DebugDisplay}")]
  public sealed class DateEvaluable : Evaluable
    , IVariableDate, IStructuralEquatable, IDebugDisplay
  {
    private readonly IVariableDate _date;

    /// <summary>
    /// Initializes a new instance of the <see cref="DateEvaluable"/> class.
    /// </summary>
    /// <param name="date">The date.</param>
    private DateEvaluable(IVariableDate date)
    {
      Debug.Assert(date != null, "date is null");
      _date = date;
    }

    /// <summary>
    /// Create a <see cref="DateEvaluable"/> from <see cref="IVariableDate"/>.
    /// </summary>
    /// <param name="date">The variable date</param>
    /// <returns>The result of the conversion.</returns>
    public static DateEvaluable Create(IVariableDate date)
    {
      return Unique(new DateEvaluable(date));
    }

    /// <summary>
    /// Calculates the number of days from the base time (1900-01-01 0:0:0),
    ///  with the time within a day counted as the fraction of a day.
    /// </summary>
    /// <returns>The number of days</returns>
    public override double Evaluate()
    {
      return Dt.FractDiff(Dt.MinValue, _date.Date);
    }

    /// <summary>
    /// Create a LINQ expression representation
    /// </summary>
    /// <returns>The LINQ expression</returns>
    protected override Expression Reduce()
    {
      return Expression.Call(
        GetMethod<Func<Dt, Dt, double>>((b, e) => Dt.FractDiff(b, e)),
        Expression.MakeMemberAccess(null, GetMember<Func<Dt>>(() => Dt.MinValue)),
        _date.GetExpression());
    }

    #region operator overloads

    /// <summary>
    /// Implements date difference operator.
    /// </summary>
    /// <param name="left">The left hand operand</param>
    /// <param name="right">The right hand operand</param>
    /// <returns>The result of the operator.</returns>
    public static Evaluable operator -(Dt left, DateEvaluable right)
    {
      return CalculateDays(left) - right;
    }

    /// <summary>
    /// Implements date difference operator.
    /// </summary>
    /// <param name="left">The left hand operand</param>
    /// <param name="right">The right hand operand</param>
    /// <returns>The result of the operator.</returns>
    public static Evaluable operator -(DateEvaluable left, Dt right)
    {
      return left - CalculateDays(right);
    }

    private static double CalculateDays(Dt date)
    {
      return Dt.FractDiff(Dt.MinValue, date);
    }

    #endregion

    #region IVariableDate members

    Dt IVariableDate.Date
    {
      get { return _date.Date; }
    }

    string IVariableDate.Name
    {
      get { return _date.Name; }
    }

    Expression IVariableDate.GetExpression()
    {
      return _date.GetExpression();
    }

    #endregion

    #region IStructuralEquatable member

    bool IStructuralEquatable.Equals(
      object other, IEqualityComparer comparer)
    {
      var d = other as DateEvaluable;
      return d != null && ReferenceEquals(d._date, _date);
    }

    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
    {
      return _date.GetHashCode();
    }

    #endregion

    #region IDebugDisplay member

    /// <summary>
    /// Gets the debugger display string of this object
    /// </summary>
    /// <value>The debug display.</value>
    public string DebugDisplay
    {
      get { return _date.Name; }
    }

    #endregion
  }
}
