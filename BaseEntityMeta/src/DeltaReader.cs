// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
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
  public static class DeltaReader<TReader, TValue> where TReader : IEntityDeltaReader
  {
    private static Func<TReader, ISnapshotDelta> _impl = Init();

    private static Func<TReader, ISnapshotDelta> Init()
    {
      var readerType = typeof(TReader);

      var reader = Expression.Parameter(readerType, "reader");

      var methodInfo = typeof(BaseEntityObject).IsAssignableFrom(typeof(TValue)) ? readerType.GetMethod("ReadObjectDelta") : readerType.GetMethod("ReadScalarDelta").MakeGenericMethod(typeof(TValue));

      return Expression.Lambda<Func<TReader, ISnapshotDelta>>(Expression.Call(reader, methodInfo), reader).Compile();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="deltaReader"></param>
    public static void Register(Func<TReader, ISnapshotDelta> deltaReader)
    {
      _impl = deltaReader;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static ISnapshotDelta ReadDelta(TReader reader)
    {
      return _impl(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    public static Func<TReader, ISnapshotDelta> Instance
    {
      get { return _impl; }
    }
  }
}