// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IEntityDeltaWriter : IEntityWriter
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    void WriteDelta<TValue>(ScalarDelta<TValue> delta);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    void WriteDelta(ObjectDelta delta);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="delta"></param>
    void WriteDelta<TValue>(SetCollectionItemDelta<TValue> delta);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="delta"></param>
    void WriteDelta<TKey, TValue>(MapCollectionItemDelta<TKey, TValue> delta);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="delta"></param>
    void WriteDelta<TValue>(ListCollectionItemDelta<TValue> delta);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="delta"></param>
    void WriteDelta<TValue>(BagCollectionItemDelta<TValue> delta);
  }
}