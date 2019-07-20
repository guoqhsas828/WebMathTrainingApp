// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
//

using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace BaseEntity.Shared.Dynamic
{
  /// <summary>
  ///  Dynamic accessor to all the public and non-public fields and properties of an object.
  /// </summary>
  /// <exclude>For WebMathTraining internal use only.</exclude>
  public class DynamicAccessor : IDynamicMetaObjectProvider
  {
    #region Fields and properties
    /// <summary>
    /// The target object to access
    /// </summary>
    private readonly object _obj;

    /// <summary>
    /// The target type to access.
    /// </summary>
    private readonly Type _type;

    /// <summary>
    /// Gets the target object.
    /// </summary>
    /// <value>The target object.</value>
    public object TargetObject
    {
      get { return _obj; }
    }

    /// <summary>
    /// Gets the target type.
    /// </summary>
    /// <value>The type of the target.</value>
    public Type TargetType
    {
      get { return _type; }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicAccessor"/> class.
    /// </summary>
    /// <param name="obj">The target object.</param>
    public DynamicAccessor(object obj)
      : this(obj, null)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicAccessor"/> class.
    /// </summary>
    /// <param name="obj">The target object.</param>
    /// <param name="type">The target type, which can be used to access a static
    ///  property or field when target object is null.</param>
    /// <exception cref="System.ArgumentNullException">obj</exception>
    /// <exception cref="System.InvalidCastException"></exception>
    public DynamicAccessor(object obj, Type type)
    {
      _obj = obj;
      if (type == null)
      {
        if (obj == null)
          throw new ArgumentNullException("obj");
        _type = obj.GetType();
        return;
      }
      _type = type;
      // For static fields and properties only.
      if (obj == null)
        return;
      // Check the compatibility of the type.
      if (!type.IsInstanceOfType(obj))
      {
        throw new InvalidCastException(String.Format(
          "{0} is not {1}", obj.GetType(), type));
      }
    }

    /// <summary>
    /// Returns the <see cref="T:System.Dynamic.DynamicMetaObject" /> responsible for binding operations performed on this object.
    /// </summary>
    /// <param name="parameter">The expression tree representation of the runtime value.</param>
    /// <returns>The <see cref="T:System.Dynamic.DynamicMetaObject" /> to bind this object.</returns>
    public DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new MetaObject(parameter, this);
    }

    /// <summary>
    /// Gets the property or field info.
    /// </summary>
    /// <param name="propertyOrFieldName">Name of the property or field.</param>
    /// <param name="ignoreCase">if set to <c>true</c> [ignore case].</param>
    /// <returns>MemberInfo.</returns>
    /// <exception cref="System.NullReferenceException"></exception>
    /// <exception cref="System.ApplicationException"></exception>
    public MemberInfo GetPropertyOrFieldInfo(
      string propertyOrFieldName, bool ignoreCase)
    {
      var type = _type;
      if (type == null)
      {
        throw new NullReferenceException(String.Format(
          "Cannot access {0} by null reference", propertyOrFieldName));
      }
      var bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
      if (_obj != null)
        bf |= BindingFlags.Instance;
      if (ignoreCase)
        bf |= BindingFlags.IgnoreCase;
      var pi = type.GetProperty(propertyOrFieldName, bf);
      if (pi != null) return pi;
      var fi = type.GetField(propertyOrFieldName, bf);
      if (fi == null)
      {
        throw new ApplicationException(String.Format(
          "{0}ember \'{1}\' not found in {2}",
          (_obj == null ? "Static m" : "M"), propertyOrFieldName, type));
      }
      return fi;
    }
    #endregion

    #region Nested Type: The Meta object

    /// <summary>
    /// Class MetaObject
    /// </summary>
    private class MetaObject : DynamicMetaObject
    {
      /// <summary>
      /// The default resitriction
      /// </summary>
      private static BindingRestrictions DefaultResitriction =
        BindingRestrictions.GetExpressionRestriction(Expression.Constant(true));

      /// <summary>
      /// Initializes a new instance of the <see cref="MetaObject"/> class.
      /// </summary>
      /// <param name="expr">The expr.</param>
      /// <param name="accessor">The accessor.</param>
      public MetaObject(Expression expr, DynamicAccessor accessor)
        : base(expr, BindingRestrictions.Empty, accessor)
      {}

      /// <summary>
      /// Performs the binding of the dynamic get member operation.
      /// </summary>
      /// <param name="binder">An instance of the <see cref="T:System.Dynamic.GetMemberBinder" /> that represents the details of the dynamic operation.</param>
      /// <returns>The new <see cref="T:System.Dynamic.DynamicMetaObject" /> representing the result of the binding.</returns>
      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        var acc = (DynamicAccessor)Value;
        Expression member = Expression.MakeMemberAccess(
          Expression.Constant(acc._obj), acc.GetPropertyOrFieldInfo(
            binder.Name, binder.IgnoreCase));
        if (member.Type != typeof(object))
          member = Expression.Convert(member, typeof(object));
        return new DynamicMetaObject(member, DefaultResitriction);
      }

      /// <summary>
      /// Performs the binding of the dynamic set member operation.
      /// </summary>
      /// <param name="binder">An instance of the <see cref="T:System.Dynamic.SetMemberBinder" /> that represents the details of the dynamic operation.</param>
      /// <param name="value">The <see cref="T:System.Dynamic.DynamicMetaObject" /> representing the value for the set member operation.</param>
      /// <returns>The new <see cref="T:System.Dynamic.DynamicMetaObject" /> representing the result of the binding.</returns>
      public override DynamicMetaObject BindSetMember(
        SetMemberBinder binder, DynamicMetaObject value)
      {
        var acc = (DynamicAccessor)Value;
        var member = Expression.MakeMemberAccess(Expression.Constant(acc._obj),
          acc.GetPropertyOrFieldInfo(binder.Name, binder.IgnoreCase));
        Expression rhs = Expression.Constant(value.Value, value.RuntimeType);
        if (value.RuntimeType != member.Type)
        {
          rhs = Expression.Convert(rhs, member.Type);
        }
        Expression result = Expression.Assign(member, rhs);
        if (result.Type != typeof(object))
        {
          result = Expression.Convert(result, typeof(object));
        }
        return new DynamicMetaObject(result, DefaultResitriction);
      }

    }

    #endregion
  }
}
