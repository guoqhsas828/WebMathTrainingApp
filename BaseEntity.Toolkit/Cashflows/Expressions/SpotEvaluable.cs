/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///  Representing a spot rate or price which evolves over time.
  /// </summary>
  [DebuggerDisplay("{DebugDisplay}")]
  public sealed class SpotEvaluable : Evaluable
    , IStructuralEquatable, IVariableDate, IDebugDisplay
  {
    #region Data and properties

    /// <summary>
    ///  Gets the spot rate/price object
    /// </summary>
    public readonly ISpot Spot;

    /// <summary>
    ///  Gets the value of the spot rate or price.
    /// </summary>
    public double Value
    {
      get { return Spot.Value; }
    }

    /// <summary>
    ///  Gets the spot date.
    /// </summary>
    public Dt Date
    {
      get { return Spot.Spot; }
    }

    #endregion

    #region Constructor

    internal SpotEvaluable(ISpot fxSpot)
    {
      Spot = fxSpot;
    }

    #endregion

    #region Overrides of Evaluable

    /// <summary>
    ///  Evaluate the expression
    /// </summary>
    /// <returns>The value of the expression</returns>
    public override double Evaluate()
    {
      return Spot.Value;
    }

    #endregion

    #region Overrides of Evaluable

    /// <summary>
    /// Reduces this node to a simpler expression. If CanReduce returns true, this should return a valid expression. This method can return another node which itself must be reduced.
    /// </summary>
    /// <returns>
    /// The reduced expression.
    /// </returns>
    protected override Expression Reduce()
    {
      return Expression.MakeMemberAccess(
        Expression.Constant(Spot), RateGetter);
    }

    private static readonly System.Reflection.MemberInfo
      RateGetter = GetMember<Func<ISpot, double>>(s => s.Value),
      DateGetter = GetMember<Func<ISpot, Dt>>(s => s.Spot);

    #endregion

    #region IVariableDate members

    string IVariableDate.Name
    {
      get
      {
        var name = Spot.Name;
        if (!string.IsNullOrEmpty(name))
        {
          name = Spot.GetType().Name;
        }
        return string.Format("SpotDate({0})", name);
      }
    }

    Expression IVariableDate.GetExpression()
    {
      return Expression.MakeMemberAccess(
        Expression.Constant(Spot, typeof(ISpot)), DateGetter);
    }

    #endregion

    #region IStructuralEquatable members

    /// <summary>
    /// Equality comparison against a target object with a given comparer.
    /// </summary>
    /// <param name="other">The target for comparison.</param>
    /// <param name="comparer">To compare the two objects.</param>
    /// <returns>True if equal, false otherwise</returns>
    bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
    {
      var fx = other as SpotEvaluable;
      return fx != null && ReferenceEquals(fx.Spot, Spot);
    }

    /// <summary>
    /// Returns a hash code for the current instance
    /// </summary>
    /// <param name="comparer">An object that computes the hash code of the current object</param>
    /// <returns>Hash code</returns>
    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
    {
      return Spot.GetHashCode();
    }

    #endregion

    #region IDebugDisplay members

    /// <summary>
    ///  Gets the display in debugger
    /// </summary>
    public string DebugDisplay
    {
      get
      {
        return string.Format("{0}({1})",
          Spot.GetType().Name, Spot);
      }
    }

    #endregion
  }
}
