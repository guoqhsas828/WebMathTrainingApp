/*
 * DelegateFactory.cs
 *
 * Copyright (c)    2004-2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   A class helps creating delegates to invoke various methods,
  ///   include internal, protected or private methods in a public
  ///   or non-public class.
  ///   It is useful for unit tests to invoke non-public methods
  ///   without exposing them publicly to external users.
  ///   It also has advantages over MethodInfo.Invoke() in both
  ///   efficiency and exception location (since no
  ///   TargetInvocationException involved).
  ///   <preliminary>  </preliminary>
  /// </summary>
  /// <exclude/>
  public static class DelegateFactory
  {
    #region Get/Set fields
    /// <summary>
    ///  Creates a getter for a field
    /// </summary>
    /// <param name="declaringType">The type declaring the field</param>
    /// <param name="fieldName">The name of the field</param>
    /// <returns>Delegate to get the field</returns>
    /// <remarks>
    ///   The return delegate is of the type <c>Func&lt;FieldType&gt;</c>
    ///   for static fields, or <c>Func&lt;DeclaringType,FieldType&gt;</c>
    ///   for instance field.
    /// </remarks>
    public static Delegate CreateGetter(Type declaringType, string fieldName)
    {
      return CreateGetter(GetFieldInfo(declaringType, fieldName));
    }

    internal static Delegate CreateGetter(FieldInfo fieldInfo)
    {
      if (fieldInfo == null)
        throw new ArgumentNullException("fieldInfo");
      LambdaExpression lambda;
      if (fieldInfo.IsStatic)
      {
        lambda = Expression.Lambda(
          typeof(Func<>).MakeGenericType(new[] { fieldInfo.FieldType }),
          Expression.Field(null, fieldInfo), null);
      }
      else
      {
        var par = Expression.Parameter(fieldInfo.DeclaringType);
        lambda = Expression.Lambda(
          typeof(Func<,>).MakeGenericType(new[] {
            fieldInfo.DeclaringType, fieldInfo.FieldType }),
          Expression.Field(par, fieldInfo), new[] { par });
      }
      return lambda.Compile();
    }

    internal static FieldInfo GetFieldInfo(Type declaringType, string fieldName)
    {
      if (declaringType == null)
        throw new ArgumentNullException("declaringType");
      if (String.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException("fieldName cannot be empty");
      const BindingFlags flags = BindingFlags.DeclaredOnly |
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic;
      var mi = declaringType.GetField(fieldName, flags);
      if (mi != null) return mi;
      return declaringType.GetField("<" + fieldName + ">k__BackingField", flags);
    }

    #endregion

    #region Actions
    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action GetAction(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[0]);
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action)
        dm.CreateDelegate(typeof(Action));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg">The argument type.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg> GetAction<TArg>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg>)
        dm.CreateDelegate(typeof(Action<TArg>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2>
      GetAction<TArg1, TArg2>(string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2>));
      // Return
      return fn;
    }


    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2, TArg3>
      GetAction<TArg1, TArg2, TArg3>(string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2, TArg3>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2, TArg3>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2, TArg3, TArg4>
      GetAction<TArg1, TArg2, TArg3, TArg4>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4)});
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2, TArg3, TArg4>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2, TArg3, TArg4>));

      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2, TArg3, TArg4, TArg5>
      GetAction<TArg1, TArg2, TArg3, TArg4, TArg5>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2, TArg3, TArg4, TArg5>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2, TArg3, TArg4, TArg5>));

      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>
      GetAction<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>));

      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <typeparam name="TArg7">The type of argument 7.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>
      GetAction<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific action.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <typeparam name="TArg7">The type of argument 7.</typeparam>
    /// <typeparam name="TArg8">The type of argument 8.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>
      GetAction<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        null, new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7), typeof(TArg8) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>)
        dm.CreateDelegate(typeof(Action<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>));
      // Return
      return fn;
    }

    #endregion Actions

    #region Functions
    /// <summary>
    ///   Gets a delegate to invoke a member function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TReturn">The return type.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    /// <exception cref="InvalidOperationException">This is thrown
    ///  if the matched method does not have the correct return type.</exception>
    public static Func<TReturn> GetFunc<TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[0]);
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TReturn>)
        dm.CreateDelegate(typeof(Func<TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a member function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg">The argument type.</typeparam>
    /// <typeparam name="TReturn">The return type.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    /// <exception cref="InvalidOperationException">This is thrown
    ///  if the matched method does not have the correct return type.</exception>
    public static Func<TArg, TReturn> GetFunc<TArg, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof (TReturn), new[] {typeof (TArg)});
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TReturn>
      GetFunc<TArg1, TArg2, TReturn>(string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TReturn>(string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TArg4, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TArg4, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TArg4, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TArg4, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <typeparam name="TArg7">The type of argument 7.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <typeparam name="TArg7">The type of argument 7.</typeparam>
    /// <typeparam name="TArg8">The type of argument 8.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7), typeof(TArg8) });
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn>));
      // Return
      return fn;
    }

    /// <summary>
    ///   Gets a delegate to invoke a specific function.
    ///   <preliminary>  </preliminary>
    /// </summary>
    /// <typeparam name="TArg1">The type of argument 1.</typeparam>
    /// <typeparam name="TArg2">The type of argument 2.</typeparam>
    /// <typeparam name="TArg3">The type of argument 3.</typeparam>
    /// <typeparam name="TArg4">The type of argument 4.</typeparam>
    /// <typeparam name="TArg5">The type of argument 5.</typeparam>
    /// <typeparam name="TArg6">The type of argument 6.</typeparam>
    /// <typeparam name="TArg7">The type of argument 7.</typeparam>
    /// <typeparam name="TArg8">The type of argument 8.</typeparam>
    /// <typeparam name="TArg9">The type of argument 9.</typeparam>
    /// <typeparam name="TArg10">The type of argument 10.</typeparam>
    /// <typeparam name="TArg11">The type of argument 11.</typeparam>
    /// <typeparam name="TReturn">The type of return value.</typeparam>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">The type declaring the method.
    /// If it is null, the declaring type is assumed to be <c>TObj</c>.</param>
    /// <returns>A delegate, or null if the method is not found.</returns>
    public static Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn>
      GetFunc<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn>(
      string methodName, Type declaringType)
    {
      DynamicMethod dm = CreateDynamicMethod(methodName, declaringType,
        typeof(TReturn), new Type[] { typeof(TArg1), typeof(TArg2), typeof(TArg3),
          typeof(TArg4), typeof(TArg5), typeof(TArg6), typeof(TArg7), typeof(TArg8),
          typeof(TArg9),typeof(TArg10),typeof(TArg11)});
      if (dm == null)
        return null;
      // Create the delegate
      var fn = (Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn>)
        dm.CreateDelegate(typeof(Func<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn>));
      // Return
      return fn;
    }

    #endregion Functions

    #region Lookup type
    /// <summary>
    /// Looks up type.
    /// </summary>
    /// <param name="typeName">Name of the type.</param>
    /// <param name="throwOnNotFound">if set to <c>true</c> [throw on not found].</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static Type LookUpType(string typeName, bool throwOnNotFound)
    {
      // First try Type.GetType(), which looks in the
      // current executing assembly and system assembly.
      Type type = Type.GetType(typeName);
      if (typeName != null) return type;
      // Then all other assemblies currently loaded.
      Assembly me = Assembly.GetExecutingAssembly();
      Assembly sys = typeof(Type).Assembly;
      foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
      {
        if (a == me || a == sys) continue;
        if (null != (type = a.GetType(typeName))) return type;
      }
      if (throwOnNotFound)
      {
        throw new Exception("Type not found: " + typeName);
      }
      return null;
    }

    /// <summary>
    /// Invokes the specified method name.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">Type of the declaring.</param>
    /// <param name="thisObj">The this obj.</param>
    /// <param name="args">The args.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static object Invoke(string methodName, Type declaringType, object thisObj, object[] args)
    {
      // First look for a static method.
      MethodInfo m = declaringType.GetMethod(methodName,
        BindingFlags.Static | BindingFlags.DeclaredOnly
        | BindingFlags.NonPublic | BindingFlags.Public);
      if (m == null)
      {
        m = declaringType.GetMethod(methodName,
          BindingFlags.Instance | BindingFlags.DeclaredOnly
          | BindingFlags.NonPublic | BindingFlags.Public);
      }

      // Have we found it?
      if (m == null)
      {
        throw new InvalidOperationException(String.Format(
          "Method {0}.{1} not found.",
          declaringType.FullName, methodName));
      }

      return m.Invoke(thisObj, args);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates the dynamic method.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="declaringType">Type of the declaring.</param>
    /// <param name="returnType">Type of the return.</param>
    /// <param name="paramTypes">The param types.</param>
    /// <returns>A dynamic method, or null if the method is not found.</returns>
    /// <exception cref="ArgumentException">Thrown if number of parameters
    ///  is larger than 255, or the declaring type is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the return type of
    ///  the method is not compatible with the specified return type.</exception>
    internal static DynamicMethod CreateDynamicMethod(
      string methodName, Type declaringType,
      Type returnType, Type[] paramTypes)
    {
      if (paramTypes == null) paramTypes = new Type[0];
      int npars = paramTypes.Length;
      if (npars > 255)
        throw new ArgumentException("Number of parameters cannot be more than 255.");
      if (declaringType == null)
        throw new ArgumentException("The declaring type cannot be null.");

      // First look for a static method.
      MethodInfo m = declaringType.GetMethod(methodName,
        BindingFlags.Static | BindingFlags.DeclaredOnly
        | BindingFlags.NonPublic | BindingFlags.Public,
        null, paramTypes, null);
      if (m == null && npars != 0 && (paramTypes[0] == declaringType
        || paramTypes[0].IsSubclassOf(declaringType)))
      {
        // If not found, try to look for an instance method.
        Type[] pars = new Type[paramTypes.Length - 1];
        for (int i = 1; i <= pars.Length; ++i)
          pars[i - 1] = paramTypes[i];
        m = declaringType.GetMethod(methodName,
          BindingFlags.Instance | BindingFlags.DeclaredOnly
          | BindingFlags.NonPublic | BindingFlags.Public,
          null, pars, null);
      }

      // Have we found it?
      if (m == null)
      {
        //throw new InvalidOperationException(String.Format(
        //  "Method {0}.{1} with the specified signature not found.",
        //  declaringType.FullName, methodName));
        return null;
      }

      // Check the return type of the method.
      bool needReturnType =
        returnType != null && returnType != typeof(void);
      bool hasReturnType =
        m.ReturnType != typeof(void) && m.ReturnType != null;
      if (needReturnType && !(hasReturnType
        && returnType.IsAssignableFrom(m.ReturnType)))
      {
        throw new InvalidOperationException(String.Format(
          "Method {0}.{1} does not return object of type {2}.",
          declaringType.FullName, methodName, returnType.FullName));
      }

      // Now we create delegate by dynamic method.
      var dm = new DynamicMethod(m.Name + "_dyn",
        returnType, paramTypes, declaringType);
      ILGenerator il = dm.GetILGenerator();
      if (npars > 0)
        il.Emit(OpCodes.Ldarg_0);
      if (npars > 1)
        il.Emit(OpCodes.Ldarg_1);
      if (npars > 2)
        il.Emit(OpCodes.Ldarg_2);
      if (npars > 3)
        il.Emit(OpCodes.Ldarg_3);
      for (int i = 4; i < npars; ++i)
        il.Emit(OpCodes.Ldarg_S, i);
      if (m.IsFinal || m.IsStatic)
        il.Emit(OpCodes.Call, m);
      else
        il.Emit(OpCodes.Callvirt, m);
      if (needReturnType)
      {
        // If the member method returns a value type but the
        // delegate expects an object reference as the return
        // type, then we need to box the value.
        if (m.ReturnType.IsValueType && !returnType.IsValueType)
          il.Emit(OpCodes.Box, m.ReturnType);
      }
      else if (hasReturnType)
      {
        // The delegate has no return type but the member
        // method returns something, we need to pop off
        // the returned value.
        il.Emit(OpCodes.Pop);
      }
      il.Emit(OpCodes.Ret);

      return dm;
    }
    #endregion Helpers

    /// <summary>
    /// Constants the function.
    /// </summary>
    /// <typeparam name="TOut">The type of the t out.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Func&lt;TOut&gt;.</returns>
    public static Func<TOut> ConstantFn<TOut>(TOut value)
    {
      return new ConstantFunction<TOut>(value).GetValue;
    }

    /// <summary>
    /// Constants the function.
    /// </summary>
    /// <typeparam name="TArg">The type of the t argument.</typeparam>
    /// <typeparam name="TOut">The type of the t out.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Func&lt;TArg, TOut&gt;.</returns>
    public static Func<TArg, TOut> ConstantFn<TArg, TOut>(TOut value)
    {
      return new ConstantFunction<TOut>(value).GetValue<TArg>;
    }


    [Serializable]
    private class ConstantFunction<TOut>
    {
      public ConstantFunction(TOut value)
      {
        Value = value;
      }

      internal TOut GetValue()
      {
        return Value;
      }

      internal TOut GetValue<TArg>(TArg arg)
      {
        return Value;
      }

      private TOut Value { get; }
    }
  } // class 

}
