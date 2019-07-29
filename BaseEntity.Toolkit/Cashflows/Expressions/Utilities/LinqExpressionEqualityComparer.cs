using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Utilities
{
  internal class LinqExpressionEqualityComparer : ExpressionVisitor
  {
    #region Stack manager

    private bool _diffFound;
    private readonly Stack<object> _stack = new Stack<object>();
    private void Push(object node) => _stack.Push(node);

    private void Pop() => _stack.Pop();

    private object Current => _stack.Peek();

    internal bool NotEqual<T>(T left, T right) where T : Expression
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      Push(right);
      Visit(left);
      Pop();
      return _diffFound;
    }

    private bool NotEqual<T>(ReadOnlyCollection<T> left,
      ReadOnlyCollection<T> right) where T : Expression
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      if (left == null || right == null || left.Count != right.Count)
        return true;
      for (int i = 0, n = left.Count; i < n; ++i)
      {
        if (NotEqual(left[i], right[i])) return true;
      }
      return false;
    }

    private bool NotEqual(LabelTarget left, LabelTarget right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      Push(right);
      VisitLabelTarget(left);
      Pop();
      return _diffFound;
    }

    private bool NotEqual(ElementInit left, ElementInit right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      Push(right);
      VisitElementInit(left);
      Pop();
      return _diffFound;
    }

    private bool NotEqual(ReadOnlyCollection<ElementInit> left,
      ReadOnlyCollection<ElementInit> right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      if (left == null || right == null || left.Count != right.Count)
        return true;
      for (int i = 0, n = left.Count; i < n; ++i)
      {
        if (NotEqual(left[i], right[i])) return true;
      }
      return false;
    }

    private bool NotEqual(MemberBinding left, MemberBinding right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      Push(right);
      VisitMemberBinding(left);
      Pop();
      return _diffFound;
    }

    private bool NotEqual(ReadOnlyCollection<MemberBinding> left,
      ReadOnlyCollection<MemberBinding> right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      if (left == null || right == null || left.Count != right.Count)
        return true;
      for (int i = 0, n = left.Count; i < n; ++i)
      {
        if (NotEqual(left[i], right[i])) return true;
      }
      return false;
    }

    private bool NotEqual(SwitchCase left, SwitchCase right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      Push(right);
      VisitSwitchCase(left);
      Pop();
      return _diffFound;
    }

    private bool NotEqual(ReadOnlyCollection<SwitchCase> left,
      ReadOnlyCollection<SwitchCase> right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      if (left == null || right == null || left.Count != right.Count)
        return true;
      for (int i = 0, n = left.Count; i < n; ++i)
      {
        if (NotEqual(left[i], right[i])) return true;
      }
      return false;
    }

    private bool NotEqual(CatchBlock left, CatchBlock right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      Push(right);
      VisitCatchBlock(left);
      Pop();
      return _diffFound;
    }

    private bool NotEqual(ReadOnlyCollection<CatchBlock> left,
      ReadOnlyCollection<CatchBlock> right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      if (left == null || right == null || left.Count != right.Count)
        return true;
      for (int i = 0, n = left.Count; i < n; ++i)
      {
        if (NotEqual(left[i], right[i])) return true;
      }
      return false;
    }

    private bool NotEqual(ReadOnlyCollection<MemberInfo> left,
      ReadOnlyCollection<MemberInfo> right)
    {
      if (_diffFound) return true;
      if (ReferenceEquals(left, right)) return false;
      if (left == null || right == null || left.Count != right.Count)
        return true;
      for (int i = 0, n = left.Count; i < n; ++i)
      {
        if (left[i] != right[i]) return true;
      }
      return false;
    }

    #endregion

    #region Visitor implementation

    public override Expression Visit(Expression node)
    {
      var other = Current as Expression;
      if (node == null || other == null || node.GetType() != other.GetType()
        || node.Type != other.Type || node.NodeType != other.NodeType)
      {
        _diffFound = true;
        return node;
      }
      return base.Visit(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
      var other = Current as BinaryExpression;
      if (other == null || node.IsLifted != other.IsLifted
        || node.IsLiftedToNull != other.IsLiftedToNull
        || node.Method != other.Method
        || NotEqual(node.Left, other.Left)
        || NotEqual(node.Right, other.Right)
        || NotEqual(node.Conversion, other.Conversion))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitBlock(BlockExpression node)
    {
      var other = Current as BlockExpression;
      if (other == null || NotEqual(node.Expressions, other.Expressions))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override CatchBlock VisitCatchBlock(CatchBlock node)
    {
      var other = Current as CatchBlock;
      if (other == null || node.Test != other.Test
        || NotEqual(node.Variable, other.Variable)
        || NotEqual(node.Filter, other.Filter)
        || NotEqual(node.Body, other.Body))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
      var other = Current as ConditionalExpression;
      if (other == null || NotEqual(node.Test, other.Test)
        || NotEqual(node.IfTrue, other.IfTrue)
        || NotEqual(node.IfFalse, other.IfFalse))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
      var other = Current as ConstantExpression;
      if (other == null || _diffFound) return node;
      if (node.Value is double)
        _diffFound = !((double) node.Value).IsAlmostSameAs((double) other.Value);
      else
        _diffFound = !node.Value.Equals(other.Value);
      return node;
    }

    // override Expression VisitDebugInfo(DebugInfoExpression node)
    // override Expression VisitDefault(DefaultExpression node)

    protected override Expression VisitDynamic(DynamicExpression node)
    {
      throw new ToolkitException("Dynamic expression not supported yet");
    }

    protected override ElementInit VisitElementInit(ElementInit node)
    {
      var other = Current as ElementInit;
      if (other == null || node.AddMethod != other.AddMethod
        || NotEqual(node.Arguments, other.Arguments))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitExtension(Expression node)
    {
      var ex = node as IEquatable<Expression>;
      if (ex != null)
      {
        if (!ex.Equals((Expression) Current)) _diffFound = true;
        return node;
      }
      return base.VisitExtension(node);
    }

    protected override Expression VisitGoto(GotoExpression node)
    {
      var other = Current as GotoExpression;
      if (other == null || node.Kind == other.Kind
        || NotEqual(node.Target, other.Target)
        || NotEqual(node.Value, other.Value))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
      var other = Current as IndexExpression;
      if (other == null || node.Indexer != other.Indexer
        || NotEqual(node.Arguments, other.Arguments)
        || NotEqual(node.Object, other.Object))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
      var other = Current as InvocationExpression;
      if (other == null || NotEqual(node.Arguments, other.Arguments)
        || NotEqual(node.Expression, other.Expression))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitLabel(LabelExpression node)
    {
      var other = Current as LabelExpression;
      if (other == null || NotEqual(node.Target, other.Target)
        || NotEqual(node.DefaultValue, other.DefaultValue))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override LabelTarget VisitLabelTarget(LabelTarget node)
    {
      var other = Current as LabelTarget;
      if (other == null || node.Name != other.Name)
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
      var other = Current as Expression<T>;
      if (other == null || node.Name != other.Name
        || node.TailCall != other.TailCall
        || NotEqual(node.Parameters, other.Parameters)
        || NotEqual(node.Body, other.Body))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
      var other = Current as ListInitExpression;
      if (other == null || NotEqual(node.Initializers, other.Initializers)
        || NotEqual(node.NewExpression, other.NewExpression))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitLoop(LoopExpression node)
    {
      var other = Current as LoopExpression;
      if (other == null || NotEqual(node.BreakLabel, other.BreakLabel)
        || NotEqual(node.ContinueLabel, other.ContinueLabel)
        || NotEqual(node.Body, other.Body))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
      var other = Current as MemberExpression;
      if (other == null || node.Member != other.Member
        || NotEqual(node.Expression, other.Expression))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override MemberBinding VisitMemberBinding(MemberBinding node)
    {
      var other = Current as MemberBinding;
      if (other == null || node.BindingType != other.BindingType
        || node.Member != other.Member)
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
      var other = Current as MemberInitExpression;
      if (other == null || NotEqual(node.Bindings, other.Bindings)
        || NotEqual(node.NewExpression, other.NewExpression))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
      var other = Current as MethodCallExpression;
      if (other == null || node.Method != other.Method
        || NotEqual(node.Arguments, other.Arguments)
        || NotEqual(node.Object, other.Object))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitNew(NewExpression node)
    {
      var other = Current as NewExpression;
      if (other == null || node.Constructor != other.Constructor
        || NotEqual(node.Arguments, other.Arguments)
        || NotEqual(node.Members, other.Members))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
      var other = Current as NewArrayExpression;
      if (other == null || NotEqual(node.Expressions, other.Expressions))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
      var other = Current as ParameterExpression;
      /*
      if (other == null || node.IsByRef != other.IsByRef
        || node.Name != other.Name)
      {
        _diffFound = true;
      }
       */
      // For parameters, we should always use reference equality.
      if (!ReferenceEquals(other, node)) _diffFound = true;
      return node;
    }

    protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
    {
      var other = Current as RuntimeVariablesExpression;
      if (other == null || NotEqual(node.Variables, other.Variables))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitSwitch(SwitchExpression node)
    {
      var other = Current as SwitchExpression;
      if (other == null || node.Comparison != other.Comparison
        || NotEqual(node.Cases, other.Cases)
        || NotEqual(node.DefaultBody, other.DefaultBody)
        || NotEqual(node.SwitchValue, other.SwitchValue))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override SwitchCase VisitSwitchCase(SwitchCase node)
    {
      var other = Current as SwitchCase;
      if (other == null || NotEqual(node.TestValues, other.TestValues)
        || NotEqual(node.Body, other.Body))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitTry(TryExpression node)
    {
      var other = Current as TryExpression;
      if (other == null || NotEqual(node.Body, other.Body)
        || NotEqual(node.Handlers, other.Handlers)
        || NotEqual(node.Fault, other.Fault)
        || NotEqual(node.Finally, other.Finally))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
      var other = Current as TypeBinaryExpression;
      if (other == null || node.TypeOperand != other.TypeOperand
        || NotEqual(node.Expression, other.Expression))
      {
        _diffFound = true;
      }
      return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
      var other = Current as UnaryExpression;
      if (other == null || node.IsLifted != other.IsLifted
        || node.IsLiftedToNull != other.IsLiftedToNull
        || node.Method != other.Method
        || NotEqual(node.Operand, other.Operand))
      {
        _diffFound = true;
      }
      return node;
    }

    #endregion
  }
}
