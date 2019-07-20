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
  public interface IEntityWriter : IDisposable
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(bool value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(int value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(int? value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(long value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(long? value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(double value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(double? value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(DateTime value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(DateTime? value);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    void WriteEnum<T>(T value) where T : struct;

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    void WriteNullableEnum<T>(T? value) where T : struct;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(string value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(Guid value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(double[] value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    void Write(double[,] value);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    void Write(byte[] value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    void Write(ObjectRef value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    void WriteObjectId(long value);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    void WriteValue<TValue>(TValue value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    void WriteDate(DateTime value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    void WriteDate(DateTime? value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    void WriteEntity(PersistentObject entity);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propertyMetas"></param>
    void WriteEntity(PersistentObject entity, IEnumerable<PropertyMeta> propertyMetas);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    void WriteComponent(BaseEntityObject obj);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    void WriteSet<TValue>(ISet value);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    void WriteMap<TKey, TValue>(IDictionary<TKey, TValue> value);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    void WriteList<TValue>(IList<TValue> value);
  }
}