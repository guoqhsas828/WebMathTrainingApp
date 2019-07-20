// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An <see cref="IEntityContext"/> that supports loading a <see cref="PersistentObject"/> from an underlying repository.
  /// </summary>
  public interface ILoadableEntityContext : IEntityContext
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    void Load(PersistentObject po);
  }
}