// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Linq.Expressions;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="TValue"></typeparam>
  public static class ValueComparer<TValue>
  {
    private static Func<TValue, TValue, bool> _impl = Init();

    private static Func<TValue, TValue, bool> Init()
    {
      if (typeof(TValue) == typeof(bool) ||
          typeof(TValue) == typeof(int) ||
          typeof(TValue) == typeof(int?) ||
          typeof(TValue) == typeof(long) ||
          typeof(TValue) == typeof(long?) ||
          typeof(TValue) == typeof(double) ||
          typeof(TValue) == typeof(double?) ||
          typeof(TValue) == typeof(DateTime) ||
          typeof(TValue) == typeof(DateTime?) ||
          typeof(TValue) == typeof(string) ||
          typeof(TValue) == typeof(Guid))
      {
        var valueA = Expression.Parameter(typeof(TValue), "valueA");
        var valueB = Expression.Parameter(typeof(TValue), "valueB");
        return Expression.Lambda<Func<TValue, TValue, bool>>(
          Expression.Equal(valueA, valueB), valueA, valueB).Compile();
      }

      if (typeof(TValue) == typeof(ObjectRef))
      {
        var methodInfo = typeof(ObjectRef).GetMethod("IsSame");
        var objectRefA = Expression.Parameter(typeof(TValue), "objectRefA");
        var objectRefB = Expression.Parameter(typeof(TValue), "objectRefB");
        return Expression.Lambda<Func<TValue, TValue, bool>>(
          Expression.Call(null, methodInfo, objectRefA, objectRefB), objectRefA, objectRefB).Compile();
      }

      if (typeof(TValue).IsEnum)
      {
        var valueA = Expression.Parameter(typeof(TValue), "valueA");
        var valueB = Expression.Parameter(typeof(TValue), "valueB");
        return Expression.Lambda<Func<TValue, TValue, bool>>(
          Expression.Equal(valueA, valueB), valueA, valueB).Compile();
      }

      if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        var enumType = typeof(TValue).GetGenericArguments()[0];
        if (enumType.IsEnum)
        {
          var valueA = Expression.Parameter(typeof(TValue), "valueA");
          var valueB = Expression.Parameter(typeof(TValue), "valueB");
          return Expression.Lambda<Func<TValue, TValue, bool>>(
            Expression.Equal(valueA, valueB), valueA, valueB).Compile();
        }
      }

      if (typeof(BaseEntityObject).IsAssignableFrom(typeof(TValue)))
      {
        var methodInfo = typeof(ClassMeta).GetMethod("IsSame", new[] { typeof(object), typeof(object) });
        var objA = Expression.Parameter(typeof(TValue), "objA");
        var objB = Expression.Parameter(typeof(TValue), "objB");
        return Expression.Lambda<Func<TValue, TValue, bool>>(
          Expression.Call(null, methodInfo, objA, objB), objA, objB).Compile();
      }

      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueWriter"></param>
    public static void Register(Func<TValue, TValue, bool> valueWriter)
    {
      _impl = valueWriter;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueA"></param>
    /// <param name="valueB"></param>
    public static bool IsSame(TValue valueA, TValue valueB)
    {
      if (_impl == null)
      {
        throw new InvalidOperationException("No implementation registered for [" + typeof(TValue) + "]");
      }
      return _impl(valueA, valueB);
    }

    /// <summary>
    /// 
    /// </summary>
    public static Func<TValue, TValue, bool> Instance
    {
      get { return _impl; }
    }
  }
}