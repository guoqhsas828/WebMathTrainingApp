// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using Iesi.Collections;
using BaseEntity.Shared;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IEntityReader : IDisposable
  {
    /// <summary>
    /// 
    /// </summary>
    IEntityContextAdaptor Adaptor { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    bool ReadBoolean();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    int ReadInt32();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    int? ReadNullableInt32();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    long ReadInt64();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    long? ReadNullableInt64();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    double ReadDouble();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    double? ReadNullableDouble();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    DateTime ReadDateTime();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    DateTime? ReadNullableDateTime();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    TValue ReadEnum<TValue>() where TValue : struct;

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    TValue? ReadNullableEnum<TValue>() where TValue : struct;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    string ReadString();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Guid ReadGuid();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    double[] ReadArrayOfDoubles();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    double[,] ReadArrayOfDoubles2D();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    byte[] ReadBinaryBlob();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    long ReadObjectId();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    ObjectRef ReadObjectRef();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T ReadValue<T>();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    DateTime ReadDate();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    DateTime? ReadNullableDate();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    PersistentObject ReadEntity();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    void ReadEntity(PersistentObject entity);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    BaseEntityObject ReadComponent();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    ISet ReadSetCollection<TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IDictionary<TKey, TValue> ReadMapCollection<TKey, TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    IList<TValue> ReadListCollection<TValue>();

    /// <summary>
    /// 
    /// </summary>
    bool EOF { get; }
  }
}