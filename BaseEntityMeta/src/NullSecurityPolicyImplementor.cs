// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class NullSecurityPolicyImplementor : ISecurityPolicyImplementor
  {
    /// <summary>
    /// 
    /// </summary>
    public NullSecurityPolicyImplementor()
    {
      UserName = "";

      NamedPermissions = new HashSet<string>();

      UserRole = new UserRole
      {
        Name = "",
        Administrator = true
      };
    }

    #region Implementation of ISecurityPolicyImplementor

    /// <summary>
    /// 
    /// </summary>
    public ISet<string> NamedPermissions { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public string UserName { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public UserRole UserRole { get; private set; }

    #endregion
  }
}
