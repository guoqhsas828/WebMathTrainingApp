// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
//

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BaseEntity.Shared.Dynamic
{
  /// <summary>
  ///  Dynamic invokers of the member getters and setters, with DLR caching layers.
  /// </summary>
  /// <remarks>
  ///   For the call site caching mechanism, please see 
  ///   <a href="http://msdn.microsoft.com/en-us/library/dd233052%28v=vs.100%29.aspx">MSDN documentation</a>.
  /// </remarks>
  /// <exclude>For WebMathTraining internal use only.</exclude>
  public static class DynamicInvokers
  {
    #region Public methods
    /// <summary>
    /// Determines whether the object has specified property or field.
    /// </summary>
    /// <param name="obj">The target object.</param>
    /// <param name="propertyOrFieldName">Name of the property or field.</param>
    /// <returns><c>true</c> if the object has specified property or field; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException">obj</exception>
    public static bool HasPropertyOrField(this object obj,
      string propertyOrFieldName)
    {
      if (obj == null) return false;
      return DynamicPropertyChecker.Invoke(
        obj.GetType(), propertyOrFieldName);
    }

    /// <summary>
    /// Gets the value of the specified property or field.
    /// </summary>
    /// <typeparam name="T">The type of the value to get.</typeparam>
    /// <param name="obj">The target object.</param>
    /// <param name="propertyOrFieldName">The name of the property or field.</param>
    /// <returns>The value of the specified property or field</returns>
    /// <exception cref="System.ArgumentNullException">obj</exception>
    public static T GetValue<T>(this object obj,
      string propertyOrFieldName)
    {
      if (obj == null)
        throw new ArgumentNullException("obj");
      return DynamicPropertyGetter<T>.Invoke(
        obj, obj.GetType(), propertyOrFieldName);
    }

    /// <summary>
    /// Gets the value of the the specified property or field, on the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the value to get.</typeparam>
    /// <param name="obj">The target object.</param>
    /// <param name="type">The target type.</param>
    /// <param name="propertyOrFieldName">Name of the property or field.</param>
    /// <returns>The value of the field or property</returns>
    /// <exception cref="System.ArgumentNullException">obj</exception>
    public static T GetValue<T>(this object obj,
      Type type, string propertyOrFieldName)
    {
      if (obj == null && type == null)
        throw new ArgumentNullException("obj");
      return DynamicPropertyGetter<T>.Invoke(
        obj, type, propertyOrFieldName);
    }

    /// <summary>
    /// Sets the value of the specified field or property.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="obj">The target object.</param>
    /// <param name="propertyOrFieldName">Name of the property or field.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="System.ArgumentNullException">obj</exception>
    public static void SetValue<T>(this object obj,
      string propertyOrFieldName, T value)
    {
      if (obj == null)
        throw new ArgumentNullException("obj");
      DynamicPropertySetter<T>.Invoke(obj, obj.GetType(),
        propertyOrFieldName, value);
    }

    /// <summary>
    /// Sets the value of the specified field or property.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="obj">The target object.</param>
    /// <param name="type">The target type.</param>
    /// <param name="propertyOrFieldName">Name of the property or field.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="System.ArgumentNullException">obj</exception>
    public static void SetValue<T>(this object obj,
      Type type, string propertyOrFieldName, T value)
    {
      if (obj == null && type == null)
        throw new ArgumentNullException("obj");
      DynamicPropertySetter<T>.Invoke(obj, type,
        propertyOrFieldName, value);
    }
    #endregion

    #region Helpers

    /// <summary>
    /// Gets or sets a value indicating whether [ignore case].
    /// </summary>
    /// <value><c>true</c> if [ignore case]; otherwise, <c>false</c>.</value>
    internal static bool IgnoreCase { get; set; }

    /// <summary>
    ///   Check if a member is static
    /// </summary>
    /// <param name="mi"></param>
    /// <returns></returns>
    internal static bool IsStatic(this MemberInfo mi)
    {
      var fi = mi as FieldInfo;
      if (fi != null)
        return fi.IsStatic;
      var m = mi as MethodInfo;
      if (m == null)
      {
        var pi = mi as PropertyInfo;
        if (pi == null) return false;
        m = pi.GetGetMethod() ?? pi.GetSetMethod();
      }
      return m != null && m.IsStatic;
    }

    /// <summary>
    ///  Get the restriction indicating when the rule applies.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The name.</param>
    /// <param name="typeParam">The type param.</param>
    /// <param name="nameParam">The name param.</param>
    /// <returns>Expression.</returns>
    internal static Expression Restriction(Type type, string name,
      Expression typeParam, Expression nameParam)
    {
      // Apply the existing rule if both the type and the member name match.
      // This is the expression:
      //   type == typeParam && 0 == String.Compare(name, nameParam, stringComarison)
      return Expression.AndAlso(
        Expression.Equal(typeParam, Expression.Constant(type)),
        Expression.Equal(Expression.Constant(0), Expression.Call(
          _stringCompareMethod, Expression.Constant(name), nameParam,
          Expression.Constant(IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal))));
    }

    /// <summary>
    /// The string compare method
    /// </summary>
    private static MethodInfo _stringCompareMethod = typeof(String).GetMethod(
      "Compare", BindingFlags.Public | BindingFlags.Static,
      null, new[] { typeof(String), typeof(String), typeof(StringComparison) },
      null);
    #endregion

    /// <summary>
    ///   Convert the specified value to the target type.
    /// </summary>
    /// <typeparam name="T">The target type</typeparam>
    /// <param name="value">The value to convert</param>
    /// <returns>The value in target type</returns>
    public static T Convert<T>(object value)
    {
      var targetType = typeof(T);

      if (targetType.IsInstanceOfType(value))
        return (T)value;

      var str = value as string;
      if (value == null || str == String.Empty)
      {
        // Null or empty input converts to the default value.
        return default(T);
      }

      // Now let's check if the value is assignable
      var type = value.GetType();
      if (targetType.IsAssignableFrom(type) || targetType.IsPrimitive)
        return (T)System.Convert.ChangeType(value, targetType);

      const BindingFlags bf = BindingFlags.Public | BindingFlags.Static;

      // Try any implicit or explicit conversion on the target type
      var method = targetType.GetMethods(bf).FirstOrDefault(
        m => m.IsSpecialName && (m.Name == "op_Implicit" || m.Name == "op_Explicit")
          && m.ReturnType == type && m.GetParameters()[0].ParameterType == type);
      if (method != null)
        return (T)method.Invoke(null, new[] {value});

      // Try Parse method on the target type
      if (str != null)
      {
        if (targetType.IsEnum)
          return (T)Enum.Parse(targetType, str);

        method = targetType.GetMethod("Parse", bf, null, _stringArgument, null);
        if (method != null)
          return (T)method.Invoke(null, new[] {value});
      }

      // The last resort
      return (T)System.Convert.ChangeType(value, targetType);
    }

    private static readonly Type[] _stringArgument = { typeof(string) };
  }

  /// <summary>
  ///   Check if a field or property exists
  /// </summary>
  internal static class DynamicPropertyChecker
  {
    public static bool Invoke(Type type, string propertyName)
    {
      return CallSite.Target(_site, type, propertyName);
    }

    class Binder : CallSiteBinder
    {
      public override Expression Bind(object[] args,
        ReadOnlyCollection<ParameterExpression> parameters,
        LabelTarget returnLabel)
      {
        var type = (Type)args[0];
        var name = (string)args[1];
        var bf = BindingFlags.Public | BindingFlags.NonPublic
          | BindingFlags.Static | BindingFlags.Instance;
        if (DynamicInvokers.IgnoreCase)
          bf |= BindingFlags.IgnoreCase;
        var result = type != null && (
          type.GetProperty(name, bf) != null
            || type.GetField(name, bf) != null);
        return Expression.IfThen(
          DynamicInvokers.Restriction(type, name,
            parameters[0], parameters[1]),
          Expression.Return(returnLabel, Expression.Constant(result)));
      }
    }

    private static CallSite<Func<CallSite, Type, string, bool>>
      CallSite
    {
      get
      {
        if (_site != null) return _site;
        _site = CallSite<Func<CallSite, Type, string, bool>>
          .Create(new Binder());
        return _site;
      }
    }

    static CallSite<Func<CallSite, Type, string, bool>> _site;
  }

  /// <summary>
  ///   Property or field getter
  /// </summary>
  /// <typeparam name="T"></typeparam>
  internal static class DynamicPropertyGetter<T>
  {
    public static T Invoke(object obj, Type type, string propertyName)
    {
      return CallSite.Target(_site, obj, type, propertyName);
    }

    class Binder : CallSiteBinder
    {
      public override Expression Bind(object[] args,
        ReadOnlyCollection<ParameterExpression> parameters,
        LabelTarget returnLabel)
      {
        var acc = new DynamicAccessor(args[0], (Type)args[1]);
        var name = (string)args[2];
        var ignoreCase = DynamicInvokers.IgnoreCase;
        var m = acc.GetPropertyOrFieldInfo(name, ignoreCase);
        Expression member = Expression.MakeMemberAccess(
          m.IsStatic() ? null : Expression.Convert(parameters[0], acc.TargetType),
          m);
        if (member.Type != typeof(T))
          member = Expression.Convert(member, typeof(T));
        return Expression.IfThen(
          DynamicInvokers.Restriction(acc.TargetType,
            name, parameters[1], parameters[2]),
          Expression.Return(returnLabel, member));
      }
    }

    private static CallSite<Func<CallSite, object, Type, string, T>>
      CallSite
    {
      get
      {
        if (_site != null) return _site;
        _site = CallSite<Func<CallSite, object, Type, string, T>>
          .Create(new Binder());
        return _site;
      }
    }

    static CallSite<Func<CallSite, object, Type, string, T>> _site;
  }

  /// <summary>
  ///   Property or field setter
  /// </summary>
  /// <typeparam name="T"></typeparam>
  internal static class DynamicPropertySetter<T>
  {
    public static void Invoke(object obj, Type type,
      string propertyName, T value)
    {
      CallSite.Target(_site, obj, type, propertyName, value);
    }

    class Binder : CallSiteBinder
    {
      public override Expression Bind(object[] args,
        ReadOnlyCollection<ParameterExpression> parameters,
        LabelTarget returnLabel)
      {
        var acc = new DynamicAccessor(args[0], (Type)args[1]);
        var name = (string)args[2];
        var ignoreCase = DynamicInvokers.IgnoreCase;
        var m = acc.GetPropertyOrFieldInfo(name, ignoreCase);
        Expression member = Expression.MakeMemberAccess(
          m.IsStatic() ? null : Expression.Convert(parameters[0], acc.TargetType),
          m);
        Expression value = parameters[3];
        if (value.Type != member.Type)
        {
          if (member.Type.IsAssignableFrom(value.Type) || 
            (member.Type.IsPrimitive && value.Type.IsPrimitive))
          {
            value = Expression.Convert(value, member.Type);
          }
          else
          {
            if (value.Type.IsValueType)
              value = Expression.Convert(value, typeof(object));
            value = Expression.Call(typeof(DynamicInvokers), "Convert",
              new[] {member.Type}, value);
          }
        }
        Expression result = Expression.Assign(member, value);
        return Expression.IfThen(
          DynamicInvokers.Restriction(acc.TargetType,
            name, parameters[1], parameters[2]),
          Expression.Return(returnLabel, result, typeof(void)));
      }
    }

    private static CallSite<Action<CallSite, object, Type, string, T>>
      CallSite
    {
      get
      {
        if (_site != null) return _site;
        _site = CallSite<Action<CallSite, object, Type, string, T>>
          .Create(new Binder());
        return _site;
      }
    }

    static CallSite<Action<CallSite, object, Type, string, T>> _site;
  }
}
