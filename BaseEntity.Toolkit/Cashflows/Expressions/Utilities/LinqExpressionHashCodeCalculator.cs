using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Utilities
{
  internal class LinqExpressionHashCodeCalculator : ExpressionVisitor
  {
    public static int GetHashCode(Expression expression)
    {
      var calculator = new LinqExpressionHashCodeCalculator(null);
      calculator.Visit(expression);
      return calculator._hashCode;
    }

    internal static int GetHashCode(Expression expression,
      Dictionary<Expression, int> hashCodeTable)
    {
      var calculator = new LinqExpressionHashCodeCalculator(hashCodeTable);
      calculator.Visit(expression);
      return calculator._hashCode;
    }

    #region Hash code updating

    private int _hashCode;

    private void Add(int code)
    {
      _hashCode *= 37;
      _hashCode ^= code;
    }

    #endregion

    #region Hash code cache
    private readonly Dictionary<Expression, int> _map;

    private LinqExpressionHashCodeCalculator(Dictionary<Expression, int> hashCodeTable)
    {
      _map = hashCodeTable ?? new Dictionary<Expression, int>();
    }

    private int FindHashCode(Expression expr)
    {
      Debug.Assert(expr != null);
      int code;
      if (_map.TryGetValue(expr, out code))
      {
        return code;
      }
      var savedCode = _hashCode;
      _hashCode = 0;
      Visit(expr);
      code = _hashCode;
      _map.Add(expr, code);
      _hashCode = savedCode;
      return code;
    }

    private void Add(Expression expr)
    {
      if (expr == null) return;
      Add(FindHashCode(expr));
    }

    private void Add<T>(ReadOnlyCollection<T> list) where T : Expression
    {
      if (list == null) return;
      Add(list.Count);
      foreach (var e in list) Add(e);
    }

    private void Add(LabelTarget target)
    {
      if (target == null) return;
      Add(target.GetHashCode());
      /*
      if (target != null && target.Name != null)
        Add(target.Name.GetHashCode());
      return base.VisitLabelTarget(target);
      */
    }

    private void Add(CatchBlock node)
    {
      Add(node.Test.GetHashCode());
      Add(node.Variable);
      Add(node.Filter);
      Add(node.Body);
    }

    private void Add(ElementInit node)
    {
      if (node == null) return;
      Add(node.AddMethod.GetHashCode());
      Add(node.Arguments);
    }

    private void Add(MemberBinding node)
    {
      if (node == null) return;

      var ma = node as MemberAssignment;
      if (ma != null)
      {
        Add(ma);
        return;
      }
      var ml = node as MemberListBinding;
      if (ml != null)
      {
        Add(ml);
        return;
      }
      var mm = node as MemberMemberBinding;
      if (mm != null)
      {
        Add(mm);
        return;
      }

      Add((int)node.BindingType);
      if (node.Member != null) Add(node.Member.GetHashCode());
    }

    private void Add(MemberAssignment node)
    {
      if (node == null) return;
      Add((int)node.BindingType);
      if (node.Member != null) Add(node.Member.GetHashCode());
      Add(node.Expression);
    }

    private void Add(MemberListBinding node)
    {
      Add((int)node.BindingType);
      if (node.Member != null) Add(node.Member.GetHashCode());
      Add(node.Initializers, e => Add(e));
    }

    private void Add(MemberMemberBinding node)
    {
      if (node == null) return;
      Add((int)node.BindingType);
      if (node.Member != null) Add(node.Member.GetHashCode());
      Add(node.Bindings, e => Add(e));
    }

    private void Add(SwitchCase node)
    {
      Add(node.TestValues);
      Add(node.Body);
    }

    private void Add<T>(ReadOnlyCollection<T> list, Action<T> add)
    {
      if (list == null) return;
      Add(list.Count);
      foreach (var e in list) add(e);
    }

    #endregion

    #region Visitor implementations

    public override Expression Visit(Expression node)
    {
      if (node == null) return null;
      Add((int)node.NodeType);
      Add(node.Type.GetHashCode());
      return base.Visit(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
      if (node.Method != null) Add(node.Method.GetHashCode());
      if (node.Conversion != null) Add(node.Conversion);
      if (node.IsLifted) Add(1);
      if (node.IsLiftedToNull) Add(1);
      Add(node.Left);
      Add(node.Right);
      return node;
    }

    protected override Expression VisitBlock(BlockExpression node)
    {
      Add(node.Expressions.Count);
      Add(node.Variables);
      Add(node.Expressions);
      return node;
    }

    protected override CatchBlock VisitCatchBlock(CatchBlock node)
    {
      throw new NotImplementedException();
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
      Add(node.Test);
      Add(node.IfTrue);
      Add(node.IfFalse);
      return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
      if (node.Value != null)
        Add(node.Value.GetHashCode());
      return node;
    }

    protected override Expression VisitDebugInfo(
      DebugInfoExpression node)
    {
      //Ignore
      return node;
    }

    protected override Expression VisitDefault(DefaultExpression node)
    {
      // No data of it's own.
      return node;
    }

    protected override Expression VisitDynamic(DynamicExpression node)
    {
      throw new NotImplementedException();
    }

    protected override ElementInit VisitElementInit(ElementInit node)
    {
      //Should never be called.
      throw new NotImplementedException();
    }

    protected override Expression VisitExtension(Expression node)
    {
      //TODO: check known types.
      Add(node.GetType().GetHashCode());
      return base.VisitExtension(node);
    }

    protected override Expression VisitGoto(GotoExpression node)
    {
      Add((int)node.Kind);
      Add(node.Target);
      Add(node.Value);
      return node;
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
      if (node.Indexer != null) Add(node.Indexer.GetHashCode());
      Add(node.Object);
      Add(node.Arguments);
      return node;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
      Add(node.Expression);
      Add(node.Arguments);
      return node;
    }

    protected override Expression VisitLabel(LabelExpression node)
    {
      Add(node.Target);
      Add(node.DefaultValue);
      return node;
    }

    protected override LabelTarget VisitLabelTarget(LabelTarget node)
    {
      //Should never be called.
      throw new NotImplementedException();
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
      Add(typeof(T).GetHashCode());
      if (node.Name != null) Add(node.Name.GetHashCode());
      if (node.TailCall) Add(1);
      Add(node.Parameters);
      Add(node.Body);
      return node;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
      Add(node.Initializers, e => Add(e));
      return node;
    }

    protected override Expression VisitLoop(LoopExpression node)
    {
      Add(node.Body);
      Add(node.BreakLabel);
      Add(node.ContinueLabel);
      return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
      Add(node.Member.GetHashCode());
      Add(node.Expression);
      return node;
    }

    protected override MemberAssignment VisitMemberAssignment(
      MemberAssignment node)
    {
      throw new NotImplementedException();
    }

    protected override MemberBinding VisitMemberBinding(MemberBinding node)
    {
      throw new NotImplementedException();
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
      Add(node.Bindings, Add);
      Add(node.NewExpression);
      return node;
    }

    protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
    {
      throw new NotImplementedException();
    }

    protected override MemberMemberBinding VisitMemberMemberBinding(
      MemberMemberBinding node)
    {
      throw new NotImplementedException();
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
      Add(node.Method.GetHashCode());
      Add(node.Object);
      Add(node.Arguments);
      return node;
    }

    protected override Expression VisitNew(NewExpression node)
    {
      Add(node.Constructor.GetHashCode());
      Add(node.Arguments);
      Add(node.Members, m => Add(m.GetHashCode()));
      return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
      Add(node.Expressions);
      return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
      // For parameters, we should always use reference equality.
      Add(node.GetHashCode());
      return node;
    }

    protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
    {
      throw new NotImplementedException();
    }

    protected override Expression VisitSwitch(SwitchExpression node)
    {
      Add(node.SwitchValue);
      if (node.Comparison != null) Add(node.Comparison.GetHashCode());
      Add(node.Cases, c => Add(c));
      Add(node.DefaultBody);
      return node;
    }

    protected override SwitchCase VisitSwitchCase(SwitchCase node)
    {
      throw new NotImplementedException();
    }

    protected override Expression VisitTry(TryExpression node)
    {
      Add(node.Body);
      Add(node.Fault);
      Add(node.Finally);
      Add(node.Handlers, h => Add(h));
      return node;
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
      Add(node.TypeOperand.GetHashCode());
      Add(node.Expression);
      return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
      if (node.Method != null) Add(node.Method.GetHashCode());
      if (node.IsLifted) Add(1);
      if (node.IsLiftedToNull) Add(1);
      Add(node.Operand);
      return node;
    }

    #endregion
  }
}
