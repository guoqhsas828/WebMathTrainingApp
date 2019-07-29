using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using BaseEntity.Toolkit.Cashflows.Expressions.Utilities;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  [DebuggerDisplay("{GetDebugDisplay()}")]
  public class FunctionCallEvaluable : ResettableValue
    , IStructuralEquatable, IDebugDisplay
  {
    public readonly Func<double> Function;

    public FunctionCallEvaluable(Func<double> func)
    {
      Debug.Assert(func != null);
      Function = func;
    }

    protected override double Compute()
    {
      return Function();
    }

    protected override Expression Reduce()
    {
      return Reduce(Function);
    }

    public static Expression Reduce(Delegate func, params Evaluable[] args)
    {
      var target = func.Target;
      var instance = target == null ? null : Expression.Constant(target);
      if (args == null || args.Length == 0)
      {
        return Expression.Call(instance, func.Method);
      }
      return Expression.Call(instance, func.Method,
        args.Select(e => e.ToExpression()));
    }


    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as FunctionCallEvaluable;
      return o != null && o.Function == Function;
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return Function.GetHashCode();
    }

    #endregion

    #region IDebugDisplay members

    internal static string Display(Delegate func, params Evaluable[] args)
    {
      var sb = new StringBuilder();
      var o = func.Target;
      if (o == null)
      {
        var t = func.Method.DeclaringType;
        if (t != null) sb.Append(t.Name).Append('.');
      }
      else
      {
        var d = o as IDebugDisplay;
        if (d != null) sb.Append(d.DebugDisplay).Append('.');
        else sb.Append('(').Append(o).Append(").");
      }
      sb.Append(func.Method.Name).Append('(');
      if (args != null && args.Length > 0)
      {
        Append(sb, args[0]);
        for (int i = 1; i < args.Length; ++i)
        {
          sb.Append(", ");
          Append(sb, args[i]);
        }
      }
      sb.Append(')');
      return sb.ToString();
    }

    private static void Append(StringBuilder sb, Evaluable a)
    {
      var d = a as IDebugDisplay;
      if (d != null) sb.Append(d.DebugDisplay);
      else sb.Append(a);
    }

    private string GetDebugDisplay()
    {
      return Display(Function);
    }

    string IDebugDisplay.DebugDisplay
    {
      get { return GetDebugDisplay(); }
    }

    #endregion
  }

  [DebuggerDisplay("{GetDebugDisplay()}")]
  internal class UnaryFunctionCallEvaluable : ResettableValue
    , IStructuralEquatable, IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    internal readonly Func<double, double> Func;
    internal readonly Evaluable Arg1;

    internal UnaryFunctionCallEvaluable(
      Func<double, double> func, Evaluable arg1)
    {
      Debug.Assert(func != null);
      Debug.Assert(arg1 != null);
      Func = func;
      Arg1 = arg1;
    }

    protected override Expression Reduce()
    {
      return FunctionCallEvaluable.Reduce(Func, Arg1);
    }

    protected override double Compute()
    {
      return Func(Arg1.Evaluate());
    }

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as UnaryFunctionCallEvaluable;
      return o != null && o.Func == Func &&
        ReferenceEquals(o.Arg1, Arg1);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(
        Func.GetHashCode(), Arg1.GetHashCode());
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 1; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Arg1;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return Arg1;
    }

    #endregion

    #region IDebugDisplay members

    private string GetDebugDisplay()
    {
      return FunctionCallEvaluable.Display(Func, Arg1);
    }

    string IDebugDisplay.DebugDisplay
    {
      get { return GetDebugDisplay(); }
    }

    #endregion
  }

  [DebuggerDisplay("{GetDebugDisplay()}")]
  internal class BinaryFunctionCallEvaluable : ResettableValue
    , IStructuralEquatable, IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    internal readonly Func<double, double, double> Func;
    internal readonly Evaluable Arg1, Arg2;

    internal BinaryFunctionCallEvaluable(
      Func<double, double, double> func,
      Evaluable arg1, Evaluable arg2)
    {
      Debug.Assert(func != null);
      Debug.Assert(arg1 != null);
      Debug.Assert(arg2 != null);
      Func = func;
      Arg1 = arg1;
      Arg2 = arg2;
    }

    protected override Expression Reduce()
    {
      return FunctionCallEvaluable.Reduce(Func, Arg1, Arg2);
    }

    protected override double Compute()
    {
      return Func(Arg1.Evaluate(), Arg2.Evaluate());
    }

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as BinaryFunctionCallEvaluable;
      return o != null && o.Func == Func &&
        ReferenceEquals(o.Arg1, Arg1) &&
        ReferenceEquals(o.Arg2, Arg2);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Func.GetHashCode(),
        Arg1.GetHashCode(), Arg2.GetHashCode());
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 2; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Arg1;
      yield return Arg2;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return Arg1;
      yield return Arg2;
    }

    #endregion

    #region IDebugDisplay members

    private string GetDebugDisplay()
    {
      return FunctionCallEvaluable.Display(Func, Arg1, Arg2);
    }

    string IDebugDisplay.DebugDisplay
    {
      get { return GetDebugDisplay(); }
    }

    #endregion
  }

  [DebuggerDisplay("{GetDebugDisplay()}")]
  internal class TernaryFunctionCallEvaluable : ResettableValue
    , IStructuralEquatable, IReadOnlyCollection<Evaluable>, IDebugDisplay
  {
    internal readonly Func<double, double, double, double> Func;
    internal readonly Evaluable Arg1, Arg2, Arg3;

    internal TernaryFunctionCallEvaluable(
      Func<double, double, double, double> func,
      Evaluable arg1, Evaluable arg2, Evaluable arg3)
    {
      Debug.Assert(func != null);
      Debug.Assert(arg1 != null);
      Debug.Assert(arg2 != null);
      Func = func;
      Arg1 = arg1;
      Arg2 = arg2;
      Arg3 = arg3;
    }

    protected override Expression Reduce()
    {
      return FunctionCallEvaluable.Reduce(Func, Arg1, Arg2, Arg3);
    }

    protected override double Compute()
    {
      return Func(Arg1.Evaluate(), Arg2.Evaluate(), Arg3.Evaluate());
    }

    #region IStructuralEquatable members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var o = other as TernaryFunctionCallEvaluable;
      return o != null && o.Func == Func &&
        ReferenceEquals(o.Arg1, Arg1) &&
        ReferenceEquals(o.Arg2, Arg2) &&
        ReferenceEquals(o.Arg3, Arg3);
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Func.GetHashCode(),
        Arg1.GetHashCode(), Arg2.GetHashCode(), Arg3.GetHashCode());
    }

    #endregion

    #region IReadOnlyCollection<Evaluable> members

    int IReadOnlyCollection<Evaluable>.Count
    {
      get { return 3; }
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Arg1;
      yield return Arg2;
      yield return Arg3;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      yield return Arg1;
      yield return Arg2;
      yield return Arg3;
    }

    #endregion

    #region IDebugDisplay members

    private string GetDebugDisplay()
    {
      return FunctionCallEvaluable.Display(Func, Arg1, Arg2, Arg3);
    }

    string IDebugDisplay.DebugDisplay
    {
      get { return GetDebugDisplay(); }
    }

    #endregion
  }
}
