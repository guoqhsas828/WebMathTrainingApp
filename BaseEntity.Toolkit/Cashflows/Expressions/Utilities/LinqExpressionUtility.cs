using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Utilities
{
  /// <summary>
  ///  Utility methods manipulating Linq Expressions.
  /// </summary>
  /// <exclude>For internal use only.</exclude>
  internal static class LinqExpressionUtility
  {
    #region Methods

    /// <summary>
    /// Gets the default Linq expression comparer.
    /// </summary>
    /// <value>The comparer.</value>
    public static IEqualityComparer<Expression> Comparer
    {
      get { return new ExpressionComparer(); }
    }

    /// <summary>
    ///  Determines whether the left expression equals the right one.
    /// </summary>
    /// <param name="left">The left expression.</param>
    /// <param name="right">The right expression.</param>
    /// <returns><c>true</c> if the two expressions equal, <c>false</c> otherwise</returns>
    public static bool Equals(Expression left, Expression right)
    {
      return ReferenceEquals(left, right) ||
        !(new LinqExpressionEqualityComparer().NotEqual(left, right));
    }

    /// <summary>
    /// Determines whether the specified expression contains any of the specified parameters.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns><c>true</c> if the specified expression contains any of the specified parameters; otherwise, <c>false</c>.</returns>
    public static bool ContainsAny(this Expression expression,
      ICollection<ParameterExpression> parameters)
    {
      var checker = new ParameterChecker(parameters.Contains);
      checker.Visit(expression);
      return checker.Conained;
    }

    /// <summary>
    /// Determines whether the parameter list contains the specified expression.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="expression">The expression.</param>
    /// <returns><c>true</c> if the specified parameters contains the expression; otherwise, <c>false</c>.</returns>
    private static bool Contains(
      this ICollection<ParameterExpression> parameters,
      Expression expression)
    {
      var p = expression as ParameterExpression;
      return p != null && parameters.Contains(p);
    }

    /// <summary>
    /// Determines whether the specified expression contains any sub-expression
    ///  such that the specified predicate is true.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns><c>true</c> if the specified expression contains any; otherwise, <c>false</c>.</returns>
    public static bool ContainsAny(this Expression expression,
      Func<Expression, bool> predicate)
    {
      var checker = new ParameterChecker(predicate);
      checker.Visit(expression);
      return checker.Conained;
    }

    /// <summary>
    /// Walks through the specified expression, apply the map function to
    /// each node, and if the map function returns a different node,
    /// replace it with the returned node.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="map">The map function.</param>
    /// <returns>Expression.</returns>
    public static Expression Replace(this Expression expression,
      Func<Expression, Expression> map)
    {
      return new Replacer(map, null).Visit(expression);
    }

    /// <summary>
    /// Expands the specified lambda expression with the supplied arguments.
    /// </summary>
    /// <param name="lambda">The lambda expression.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>Expression.</returns>
    /// <exception cref="DealException">Null arguments</exception>
    public static Expression Expand(this LambdaExpression lambda,
      params Expression[] arguments)
    {
      if (lambda == null) return null;
      if (arguments == null)
      {
        if (lambda.Parameters.Count == 0)
          return lambda.Body;
        throw new ArgumentNullException(nameof(lambda));
      }
      var pars = lambda.Parameters.ToArray();
      if (arguments.Length != pars.Length)
      {
        throw new ArgumentException("Arguments not match");
      }
      return new LambdaExpander(pars, arguments).Visit(lambda.Body);
    }

    /// <summary>
    /// Expands the specified lambda expression with the supplied arguments.
    /// </summary>
    /// <param name="lambda">The lambda.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>Expression.</returns>
    public static Expression Expand(this LambdaExpression lambda,
      IEnumerable<Expression> arguments)
    {
      return Expand(lambda, arguments == null ? null : arguments.ToArray());
    }

    /// <summary>
    /// Expands the specified invocation expression.
    /// </summary>
    /// <param name="invoke">The invocation expression.</param>
    /// <returns>Expression.</returns>
    public static Expression Expand(this InvocationExpression invoke)
    {
      var lambda = invoke.Expression as LambdaExpression;
      return lambda == null ? invoke : Expand(lambda, invoke.Arguments);
    }

    #endregion

    #region Nested type: LambdaExpander

    internal sealed class LambdaExpander : ExpressionVisitor
    {
      private readonly ParameterExpression[] _parameters;
      private readonly Expression[] _arguments;

      public LambdaExpander(ParameterExpression[] pars, Expression[] args)
      {
        _parameters = pars;
        _arguments = args;
      }

      protected override Expression VisitParameter(ParameterExpression node)
      {
        var index = Array.IndexOf(_parameters, node);
        return index >= 0 ? _arguments[index] : node;
      }
    }

    #endregion

    #region Nested type: ParameterChecker

    class ParameterChecker : ExpressionVisitor
    {
      public bool Conained { get; set; }

      private readonly Func<Expression, bool> _predicate;

      public ParameterChecker(Func<Expression, bool> predicate)
      {
        _predicate = predicate;
      }

      public override Expression Visit(Expression node)
      {
        if (Conained) return node;
        if (_predicate(node))
        {
          Conained = true;
          return node;
        }
        return base.Visit(node);
      }
    }

    #endregion

    #region Nested type: ExpressionComparer

    class ExpressionComparer : IEqualityComparer<Expression>
    {
      private readonly Dictionary<Expression, int> _hashCodeTable
        = new Dictionary<Expression, int>();

      #region IEqualityComparer<Expression> members

      bool IEqualityComparer<Expression>.Equals(Expression x, Expression y)
      {
        return LinqExpressionUtility.Equals(x, y);
      }

      public int GetHashCode(Expression obj)
      {
        return LinqExpressionHashCodeCalculator.GetHashCode(obj, _hashCodeTable);
      }

      #endregion
    }

    #endregion

    #region Replace a set of sub expressions

    class Replacer : ExpressionVisitor
    {
      private readonly Func<Expression, Expression> _map;
      private readonly Func<Expression, bool> _select;

      public Replacer(Func<Expression, Expression> map,
        Func<Expression, bool> selector)
      {
        _map = map;
        _select = selector;
      }

      public override Expression Visit(Expression node)
      {
        if (_select == null || _select(node))
        {
          var ex = _map(node);
          if (!ReferenceEquals(ex, node)) return ex;
        }
        return base.Visit(node);
      }
    }

    #endregion

    #region Find sub-expressions with a predicate

    /// <summary>
    ///  Determine whether an expression tree has any sub-expression
    ///  (including itself) making the specified predicate to be true.
    /// </summary>
    /// <param name="expression">Expression tree</param>
    /// <param name="predicate">Predicate</param>
    /// <returns>True if there exists a sub-expression making the predicate to be true; otherwise, false</returns>
    public static bool HasAny(this Expression expression,
      Func<Expression, bool> predicate)
    {
      return new FindAnyVisitor(predicate).Apply(expression) != null;
    }

    /// <summary>
    ///  Find a sub-expression on which the specified predicate is true.
    /// </summary>
    /// <param name="expression">Expression tree</param>
    /// <param name="predicate">Predicate</param>
    /// <returns>A sub-expression on which the specified predicate becomes true, or null if not found</returns>
    public static Expression FindAny(this Expression expression,
      Func<Expression, bool> predicate)
    {
      return new FindAnyVisitor(predicate).Apply(expression);
    }

    /// <summary>
    ///  Find all the sub-expressions on which the specified predicate is true.
    /// </summary>
    /// <param name="expression">Expression tree</param>
    /// <param name="predicate">Predicate</param>
    /// <returns>A list of sub-expressions on which the specified predicate becomes true, or null if not found</returns>
    public static IList<Expression> FindAll(this Expression expression,
      Func<Expression, bool> predicate)
    {
      return new FindAllVisitor<Expression>(predicate).Apply(expression);
    }

    /// <summary>
    ///  Find all the sub-expressions which is derived from type T.
    /// </summary>
    /// <param name="expression">Expression tree</param>
    /// <returns>A list of sub-expressions on which the specified predicate becomes true, or null if not found</returns>
    public static IList<T> FindAllOfType<T>(this Expression expression)
      where T : Expression
    {
      return new FindAllVisitor<T>(e => e is T).Apply(expression);
    }

    private class FindAnyVisitor : ExpressionVisitor
    {
      private readonly Func<Expression, bool> _predicate;
      private Expression _result;

      internal FindAnyVisitor(Func<Expression, bool> predicate)
      {
        _predicate = predicate;
      }

      public Expression Apply(Expression expr)
      {
        _result = null;
        Visit(expr);
        return _result;
      }

      public override Expression Visit(Expression node)
      {
        if (_result != null) return node;
        if (_predicate(node)) return (_result = node);
        return base.Visit(node);
      }
    }

    private class FindAllVisitor<T> : ExpressionVisitor
      where T : Expression
    {
      private readonly Func<Expression, bool> _predicate;
      private IList<T> _result;

      internal FindAllVisitor(Func<Expression, bool> predicate)
      {
        _predicate = predicate;
      }

      public IList<T> Apply(Expression expr)
      {
        _result = null;
        Visit(expr);
        return _result;
      }

      public override Expression Visit(Expression node)
      {
        if (_predicate(node))
        {
          if (_result == null) _result = new List<T> {(T) node};
          else
          {
            var list = _result;
            for (int i = 0, n = list.Count; i < n; ++i)
              if (list[i] == node) goto call_base_vist;
            _result.Add((T) node);
          }
        }
        call_base_vist:
        return base.Visit(node);
      }
    }

    #endregion
  }
}
