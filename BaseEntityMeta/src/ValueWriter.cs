// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="TValue"></typeparam>
  /// <typeparam name="TWriter"></typeparam>
  public static class ValueWriter<TWriter, TValue> where TWriter : IEntityWriter
  {
    private static Action<TWriter, TValue> _impl = Init();

    private static Action<TWriter, TValue> Init()
    {
      var writerType = typeof(TWriter);

      MethodInfo methodInfo = GetWriteMethod(typeof(TValue));
      if (methodInfo != null)
      {
        return CreateAction(methodInfo);
      }

      if (typeof(TValue).IsEnum)
      {
        var enumType = typeof(TValue);
        return CreateAction(writerType.GetMethod("WriteEnum").MakeGenericMethod(enumType));
      }

      if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        var enumType = typeof(TValue).GetGenericArguments()[0];
        if (enumType.IsEnum)
          return CreateAction(writerType.GetMethod("WriteNullableEnum").MakeGenericMethod(enumType));
      }

      if (typeof(PersistentObject).IsAssignableFrom(typeof(TValue)))
      {
        methodInfo = writerType.GetMethods()
          .First(mi => mi.Name == "WriteEntity" && mi.GetParameters().Length == 1);

        return CreateAction(methodInfo);
      }

      if (typeof(BaseEntityObject).IsAssignableFrom(typeof(TValue)))
      {
        methodInfo = writerType.GetMethods()
          .First(mi => mi.Name == "WriteComponent" && mi.GetParameters().Length == 1);

        return CreateAction(methodInfo);
      }

      return null;
    }

    private static MethodInfo GetWriteMethod(Type valueType)
    {
      var writerType = typeof(IEntityWriter);

      foreach (var mi in writerType.GetMethods().Where(mi => mi.Name == "Write"))
      {
        var parameters = mi.GetParameters();
        if (parameters.Length == 1 && parameters[0].ParameterType == valueType)
          return mi;
      }

      return null;
    }

    private static Action<TWriter, TValue> CreateAction(MethodInfo methodInfo)
    {
      var value = Expression.Parameter(typeof(TValue), "value");
      var writer = Expression.Parameter(typeof(TWriter), "writer");
      return Expression.Lambda<Action<TWriter, TValue>>(
        Expression.Call(writer, methodInfo, value), writer, value).Compile();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueWriter"></param>
    public static void Register(Action<TWriter, TValue> valueWriter)
    {
      _impl = valueWriter;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public static void WriteValue(TWriter writer, TValue value)
    {
      if (_impl == null)
      {
        throw new InvalidOperationException(string.Format(
          "No implementation registered for [{0}] [{1}]",
          typeof(TWriter), typeof(TValue)));
      }
      _impl(writer, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static Action<TWriter, TValue> Instance
    {
      get { return _impl; }
    }
  }
}