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
  /// <typeparam name="TReader"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public static class ValueReader<TReader, TValue> where TReader : IEntityReader
  {
    private static Func<TReader, TValue> _impl = Init();

    private static Func<TReader, TValue> Init()
    {
      var type = typeof(TReader);

      var reader = Expression.Parameter(type, "reader");

      MethodInfo methodInfo = null;
      if (typeof(TValue) == typeof(bool))
      {
        methodInfo = type.GetMethod("ReadBoolean");
      }
      else if (typeof(TValue) == typeof(double))
      {
        methodInfo = type.GetMethod("ReadDouble");
      }
      else if (typeof(TValue) == typeof(double?))
      {
        methodInfo = type.GetMethod("ReadNullableDouble");
      }
      else if (typeof(TValue) == typeof(int))
      {
        methodInfo = type.GetMethod("ReadInt32");
      }
      else if (typeof(TValue) == typeof(int?))
      {
        methodInfo = type.GetMethod("ReadNullableInt32");
      }
      else if (typeof(TValue) == typeof(long))
      {
        methodInfo = type.GetMethod("ReadInt64");
      }
      else if (typeof(TValue) == typeof(long?))
      {
        methodInfo = type.GetMethod("ReadNullableInt64");
      }
      else if (typeof(TValue) == typeof(DateTime))
      {
        methodInfo = type.GetMethod("ReadDateTime");
      }
      else if (typeof(TValue) == typeof(DateTime?))
      {
        methodInfo = type.GetMethod("ReadNullableDateTime");
      }
      else if (typeof(TValue) == typeof(String))
      {
        methodInfo = type.GetMethod("ReadString");
      }
      else if (typeof(TValue) == typeof(double[]))
      {
        methodInfo = type.GetMethod("ReadArrayOfDoubles");
      }
      else if (typeof(TValue) == typeof(byte[]))
      {
        methodInfo = type.GetMethod("ReadBinaryBlob");
      }
      else if (typeof(TValue) == typeof(ObjectRef))
      {
        methodInfo = type.GetMethod("ReadObjectRef");
      }
      else if (typeof(TValue) == typeof(Guid))
      {
        methodInfo = type.GetMethod("ReadGuid");
      }
      else if (typeof(TValue).IsEnum)
      {
        methodInfo = type.GetMethod("ReadEnum").MakeGenericMethod(typeof(TValue));
      }
      else if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        var enumType = typeof(TValue).GetGenericArguments()[0];
        if (enumType.IsEnum)
          methodInfo = type.GetMethod("ReadNullableEnum").MakeGenericMethod(enumType);
      }
      else if (typeof(PersistentObject).IsAssignableFrom(typeof(TValue)))
      {
        methodInfo = type.GetMethods().First(mi => mi.Name == "ReadEntity" && mi.GetParameters().Length == 0);
        return Expression.Lambda<Func<TReader, TValue>>(
          Expression.Convert(Expression.Call(reader, methodInfo), typeof(TValue)), reader).Compile();
      }
      else if (typeof(BaseEntityObject).IsAssignableFrom(typeof(TValue)))
      {
        methodInfo = type.GetMethods().First(mi => mi.Name == "ReadComponent");
        return Expression.Lambda<Func<TReader, TValue>>(
          Expression.Convert(Expression.Call(reader, methodInfo), typeof(TValue)), reader).Compile();
      }

      return methodInfo == null ? null : Expression.Lambda<Func<TReader, TValue>>(Expression.Call(reader, methodInfo), reader).Compile();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueReader"></param>
    public static void Register(Func<TReader, TValue> valueReader)
    {
      _impl = valueReader;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static TValue ReadValue(TReader reader)
    {
      return _impl(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    public static Func<TReader, TValue> Instance
    {
      get { return _impl; }
    }
  }
}