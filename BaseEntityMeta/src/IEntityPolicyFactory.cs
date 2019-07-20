// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IEntityPolicyFactory
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    EntityPolicy GetPolicy(Type entityType);
  }
}