// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class CollectionDelta : ISnapshotDelta
  {
    /// <summary>
    /// 
    /// </summary>
    public bool IsScalar
    {
      get { return false; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public abstract void Serialize(IEntityDeltaWriter writer);
  }

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public abstract class CollectionDelta<T> : CollectionDelta
  {
  }
}