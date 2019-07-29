/*
 *  -2015. All rights reserved.
 */

using System.Linq.Expressions;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  /// Represent a variable date with the value
  /// which may depend on the evaluation date or others.
  /// </summary>
  public interface IVariableDate
  {
    /// <summary>
    ///  Gets the current value of the date
    /// </summary>
    Dt Date { get; }

    /// <summary>
    ///  Gets the display name of this variable
    /// </summary>
    string Name { get; }

    /// <summary>
    ///  Gets the expression representing this variable
    /// </summary>
    Expression GetExpression();
  }

  interface IDatedValue
  {
    /// <summary>
    ///  Gets the date associated with this object
    /// </summary>
    Dt Date { get; }
  }
}
