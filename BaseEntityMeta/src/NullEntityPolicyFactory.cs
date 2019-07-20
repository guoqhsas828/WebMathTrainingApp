// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class NullEntityPolicyFactory : IEntityPolicyFactory
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public EntityPolicy GetPolicy(Type entityType)
    {
      return null;
    }
  }
}