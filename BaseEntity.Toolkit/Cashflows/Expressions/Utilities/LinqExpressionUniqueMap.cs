using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Utilities
{
  /// <summary>
  ///  A simple way to ensure that all the structurally equivalent
  ///  expression trees have the same instance.
  /// </summary>
  [DebuggerDisplay("Count = {" + nameof(Count) + "}")]
  internal class LinqExpressionUniqueMap : IReadOnlyCollection<Expression>
  {
    private class Visitor : ExpressionVisitor
    {
      internal readonly LinqExpressionMap<Expression> Cache
        = new LinqExpressionMap<Expression>();

      #region Overrides of ExpressionVisitor

      /// <summary>
      /// Dispatches the expression to one of the more specialized visit methods in this class.
      /// </summary>
      /// <returns>
      /// The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.
      /// </returns>
      /// <param name="node">The expression to visit.</param>
      public override Expression Visit(Expression node)
      {
        if (node == null) return null;
        Expression existed;
        if (Cache.TryGetValue(node, out existed))
          return existed;
        node = base.Visit(node);
        if (Cache.TryGetValue(node, out existed))
          return existed;
        Cache.Add(node, node);
        return node;
      }

      #endregion
    }
    private readonly Visitor _visitor = new Visitor();

    /// <summary>
    ///  Map the specified expression to a unique instance
    ///  of expression tree which is structurally equivalent
    ///  to the input.
    /// </summary>
    /// <param name="node">The input expression tree</param>
    /// <returns>A unique instance of structurally equivalent
    ///  expression</returns>
    public T Map<T>(T node) where T : Expression
    {
      return (T)_visitor.Visit(node);
    }

    #region IReadOnlyCollection<Expression> members

    public int Count
    {
      get { return _visitor.Cache.Count; }
    }

    public IEnumerator<Expression> GetEnumerator()
    {
      return _visitor.Cache.Keys.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return _visitor.Cache.Keys.GetEnumerator();
    }

    #endregion
  }
}
