/*
 *   2005-2016. All rights reserved.
 */

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  /// The global pricing date.
  /// </summary>
  /// <remarks>
  ///  The property PricingDate.Value is thread safe.
  ///  In simulation, the calculation engine can set this value
  ///  to the current exposure date along the path.
  ///  In sensitivities, setting this property in one thread
  ///  essentially changes all the cash flow pricing dates
  ///  controlled by the expression evaluators in the same thread.
  /// </remarks>
  public static class PricingDate
  {
    /// <summary>
    /// The current value of pricing date
    /// </summary>
    [ThreadStatic] private static Dt _value;

    /// <summary>
    /// The pricing date as a date variable
    /// </summary>
    private static readonly Variable VariableDate = new Variable();

    /// <summary>
    /// The expression to get the current pricing date value
    /// </summary>
    private static readonly Expression DateProperty
      = Expression.Property(null, typeof(PricingDate), "Value");

    public const string Name = "PricingDate";

    /// <summary>
    /// Gets or sets the current value of the pricing date.
    /// </summary>
    /// <value>The value.</value>
    public static Dt Value
    {
      get { return _value; }
      set { _value = value; }
    }

    /// <summary>
    /// Gets the pricing date as a variable.
    /// </summary>
    /// <value>The date variable represented as the days from 1900-01-01 0:0:0</value>
    public static DateEvaluable AsVariable
    {
      get { return DateEvaluable.Create(VariableDate); }
    }

    #region Type PricingDate.Variable

    /// <summary>
    /// Class Variable.
    /// </summary>
    [DebuggerDisplay(Name)]
    private class Variable : IVariableDate
    {
      /// <summary>
      /// Gets the current value of the date
      /// </summary>
      /// <value>The date.</value>
      Dt IVariableDate.Date
      {
        get { return Value; }
      }

      /// <summary>
      /// Gets the display name of this variable
      /// </summary>
      /// <value>The name.</value>
      string IVariableDate.Name
      {
        get { return Name; }
      }

      /// <summary>
      /// Gets the expression representing this variable
      /// </summary>
      /// <returns>Expression.</returns>
      Expression IVariableDate.GetExpression()
      {
        return DateProperty;
      }
    }

    #endregion
  }

}
