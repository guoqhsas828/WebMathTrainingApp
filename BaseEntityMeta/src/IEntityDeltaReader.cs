// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IEntityDeltaReader : IEntityReader
  {
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    ScalarDelta<TValue> ReadScalarDelta<TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    ObjectDelta ReadObjectDelta();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    CollectionDelta ReadSetCollectionDelta<TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    CollectionDelta ReadMapCollectionDelta<TKey, TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    CollectionDelta ReadBagCollectionDelta<TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    CollectionDelta ReadOrderedCollectionDelta<TValue>();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    CollectionDelta ReadKeyedCollectionDelta<TValue>() where TValue : BaseEntityObject;
  }
}