// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// interface of class cache.
  /// </summary>
  public interface IClassCache
  {
    /// <summary>
    /// Find
    /// </summary>
    /// <param name="type">type</param>
    /// <returns></returns>
    ClassMeta Find(Type type);

    /// <summary>
    /// Find 
    /// </summary>
    /// <param name="name">name</param>
    /// <returns></returns>
    ClassMeta Find(string name);

    /// <summary>
    /// Find
    /// </summary>
    /// <param name="entityId">entity id</param>
    /// <returns></returns>
    ClassMeta Find(int entityId);
  }
}