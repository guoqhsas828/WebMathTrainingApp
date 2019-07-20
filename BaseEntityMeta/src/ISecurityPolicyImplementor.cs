// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface ISecurityPolicyImplementor
  {
    /// <summary>
    /// 
    /// </summary>
    ISet<string> NamedPermissions { get; } 

    /// <summary>
    /// 
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// 
    /// </summary>
    UserRole UserRole { get; }
  }
}
