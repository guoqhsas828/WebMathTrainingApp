// 
// Copyright (c) WebMathTraining Inc 2002-2016. All rights reserved.
// 

using System;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  internal class DatabaseSecurityPolicyImplementor : ISecurityPolicyImplementor
  {
    private readonly Lazy<ISet<string>> _lazyNamedPermissions = new Lazy<ISet<string>>(InitNamedPermissions);

    private static ISet<string> InitNamedPermissions()
    {
      var results = new HashSet<string>();

      using (new SessionBinder())
      using (var reader = Session.ExecuteReader("SELECT * FROM NamedPermission"))
      {
        while (reader.Read())
          results.Add((string)reader[0]);
      }

      return results;
    }

    #region Implementation of ISecurityPolicyImplementor

    /// <summary>
    /// 
    /// </summary>
    public ISet<string> NamedPermissions => _lazyNamedPermissions.Value;

    /// <summary>
    /// 
    /// </summary>
    public string UserName => EntityContextFactory.UserName;

    /// <summary>
    /// 
    /// </summary>
    public UserRole UserRole => EntityContextFactory.UserRole;

    #endregion
  }
}