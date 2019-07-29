/*
 *  -2015. All rights reserved.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows.Expressions.Utilities;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  #region Unary operations

  [DebuggerDisplay("{DebugDisplay}")]
  internal abstract class UnaryEvaluable : ResettableValue,
    IStructuralEquatable, IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    internal readonly Operator Op;
    internal readonly double Const;
    internal readonly Evaluable Node;

    protected UnaryEvaluable(Operator op, double c, Evaluable node)
    {
      Op = op;
      Const = c;
      Node = node;
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
      switch (Op)
      {
        case Operator.Drift:
          return Expression.Add(Expression.Constant(Const), Node.ToExpression());
        case Operator.Scale:
          return Expression.Multiply(Expression.Constant(Const), Node.ToExpression());
        case Operator.Inverse:
          return Expression.Divide(Expression.Constant(Const), Node.ToExpression());
        case Operator.Cap:
          {
            Expression<Func<double, double, double>> f = (x, y) => Math.Min(x, y);
            return Expression.Call(GetMethod(f),
              Expression.Constant(Const), Node.ToExpression());
          }
        case Operator.Floor:
          {
            Expression<Func<double, double, double>> f = (x, y) => Math.Max(x, y);
            return Expression.Call(GetMethod(f),
              Expression.Constant(Const), Node.ToExpression());
          }
        default:
          throw new NotSupportedException(string.Format(
            "Unary operator {0} not supported", Op));
      }
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as UnaryEvaluable;
      return o != null && o.Op == Op &&
        ReferenceEquals(o.Node, Node) && o.Const.Equals(Const);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine((int)Op,
        Node.GetHashCode(), Const.GetHashCode());
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 1; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Node;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return Node;
    }

    #endregion

    #region IDebugDisplay members

    public string DebugDisplay
    {
      get
      {
        switch (Op)
        {
          case Operator.Drift:
            return Formula(" + ", Const, Node);
          case Operator.Scale:
            return Formula("*", Const, Node);
          case Operator.Inverse:
            return Formula("/", Const, Node);
          default:
            return Formula(Op.ToString(), Const, Node, true);
        }
      }
    }

    private static string Formula(string op,
      object left, object right, bool prefix = false)
    {
      var l = left as IDebugDisplay;
      var r = right as IDebugDisplay;
      return string.Format(
        prefix ? "{0}({1}, {2})" : "({1}{0}{2})", op,
        l != null ? l.DebugDisplay : left.ToString(),
        r != null ? r.DebugDisplay : right.ToString());
    }

    #endregion
  }

  internal sealed class Drift : UnaryEvaluable, IAffine
  {
    public Drift(double drift, Evaluable node)
      : base(Operator.Drift, drift, node)
    {
    }

    protected override double Compute()
    {
      return Const + Node.Evaluate();
    }

    #region IAffineExpression members

    double IAffine.A
    {
      get { return 1; }
    }

    double IAffine.B
    {
      get { return Const; }
    }

    Evaluable IAffine.X
    {
      get { return Node; }
    }

    #endregion
  }

  internal sealed class Scaled : UnaryEvaluable, IAffine
  {
    public Scaled(double scale, Evaluable node)
      : base(Operator.Scale, scale, node)
    {
    }

    protected override double Compute()
    {
      return Const * Node.Evaluate();
    }

    #region IAffineExpression members

    double IAffine.A
    {
      get { return Const; }
    }

    double IAffine.B
    {
      get { return 0; }
    }

    Evaluable IAffine.X
    {
      get { return Node; }
    }

    #endregion
  }

  internal sealed class Inverse : UnaryEvaluable, IAffine
  {
    public Inverse(double c, Evaluable node)
      : base(Operator.Inverse, c, node)
    {
    }

    protected override double Compute()
    {
      return Const / Node.Evaluate();
    }

    #region IAffineExpression members

    double IAffine.A
    {
      get { return Const; }
    }

    double IAffine.B
    {
      get { return 0; }
    }

    Evaluable IAffine.X
    {
      get { return 1.0 / Node; }
    }

    #endregion
  }

  internal sealed class Cap : UnaryEvaluable
  {
    public Cap(double c, Evaluable node)
      : base(Operator.Cap, c, node)
    {
    }

    protected override double Compute()
    {
      return Math.Min(Const, Node.Evaluate());
    }
  }

  internal sealed class Floor : UnaryEvaluable
  {
    public Floor(double c, Evaluable node)
      : base(Operator.Floor, c, node)
    {
    }

    protected override double Compute()
    {
      return Math.Max(Const, Node.Evaluate());
    }
  }

  #endregion

  #region Binary operations

  [DebuggerDisplay("{DebugDisplay}")]
  public abstract class BinaryEvaluable : ResettableValue,
    IStructuralEquatable, IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    public readonly Operator Op;
    public readonly Evaluable Left;
    public readonly Evaluable Right;

    protected BinaryEvaluable(Operator op,
      Evaluable left, Evaluable right)
    {
      Op = op;
      Left = left;
      Right = right;
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
      switch (Op)
      {
        case Operator.Add:
          return Expression.Add(Left.ToExpression(), Right.ToExpression());
        case Operator.Subtract:
          return Expression.Subtract(Left.ToExpression(), Right.ToExpression());
        case Operator.Multiply:
          return Expression.Multiply(Left.ToExpression(), Right.ToExpression());
        case Operator.Divide:
          return Expression.Divide(Left.ToExpression(), Right.ToExpression());
        default:
          throw new NotSupportedException(String.Format(
            "Binary operator {0} not supported", Op));
      }
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as BinaryEvaluable;
      return o != null && o.Op == Op &&
        ReferenceEquals(o.Left, Left) && ReferenceEquals(o.Right, Right);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine((int)Op,
        Left.GetHashCode(), Right.GetHashCode());
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 2; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Left;
      yield return Right;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return Left;
      yield return Right;
    }

    #endregion

    #region IDebugDisplay members

    public string DebugDisplay
    {
      get
      {
        string op;
        switch (Op)
        {
          case Operator.Add:
            op = " + ";
            break;
          case Operator.Subtract:
            op = " - ";
            break;
          case Operator.Multiply:
            op = "*";
            break;
          case Operator.Divide:
            op = "/";
            break;
          default:
            op = "<?>";
            break;
        }
        var l = Left as IDebugDisplay;
        var r = Right as IDebugDisplay;
        return string.Format("({0}{2}{1})",
          l != null ? l.DebugDisplay : Left.ToString(),
          r != null ? r.DebugDisplay : Right.ToString(),
          op);
      }
    }

    #endregion
  }

  internal sealed class Add : BinaryEvaluable
  {
    public Add(Evaluable left, Evaluable right)
      : base(Operator.Add, left, right)
    {
    }

    protected override double Compute()
    {
      return Left.Evaluate() + Right.Evaluate();
    }
  }

  internal sealed class Subtract : BinaryEvaluable
  {
    public Subtract(Evaluable left, Evaluable right)
      : base(Operator.Subtract, left, right)
    {
    }

    protected override double Compute()
    {
      return Left.Evaluate() - Right.Evaluate();
    }
  }

  internal sealed class Multiply : BinaryEvaluable
  {
    public Multiply(Evaluable left, Evaluable right)
      : base(Operator.Multiply, left, right)
    {
    }

    protected override double Compute()
    {
      return Left.Evaluate() * Right.Evaluate();
    }
  }

  internal sealed class Divide : BinaryEvaluable
  {
    public Divide(Evaluable left, Evaluable right)
      : base(Operator.Divide, left, right)
    {
    }

    protected override double Compute()
    {
      return Left.Evaluate() / Right.Evaluate();
    }
  }

  #endregion

  #region Affine

  [DebuggerDisplay("{DebugDisplay}")]
  internal sealed class Affine : ResettableValue,
    IStructuralEquatable, IAffine,
    IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    internal readonly double _a, _b;
    internal readonly Evaluable Node;

    internal Operator Op { get { return Operator.Affine; } }

    internal Affine(double a, Evaluable x, double b)
    {
      _a = a;
      _b = b;
      Node = x;
    }

    #region ResettableValue overrides

    protected override double Compute()
    {
      return _a * Node.Evaluate() + _b;
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
      return Expression.Add(Expression.Multiply(
        Expression.Constant(_a), Node.ToExpression()),
        Expression.Constant(_b));
    }

    #endregion

    #region IAffineExpression members

    double IAffine.A
    {
      get { return _a; }
    }

    double IAffine.B
    {
      get { return _b; }
    }

    Evaluable IAffine.X
    {
      get { return Node; }
    }

    #endregion

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as Affine;
      return o != null &&
        ReferenceEquals(o.Node, Node)
        && o._a.Equals(_a)
        && o._b.Equals(_b);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine((int)Operator.Affine,
        Node.GetHashCode(), _a.GetHashCode(), _b.GetHashCode());
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 1; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Node;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return Node;
    }

    #endregion

    #region IDebugDisplay members

    public string DebugDisplay
    {
      get
      {
        var d = Node as IDebugDisplay;
        return string.Format("({0}*{2} + {1})", _a, _b,
          d != null ? d.DebugDisplay : Node.ToString());
      }
    }

    #endregion
  }

  #endregion
}
