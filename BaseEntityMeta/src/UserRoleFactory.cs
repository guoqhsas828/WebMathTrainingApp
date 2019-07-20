// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

using System;
using System.Collections;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  public class UserRoleFactory
  {
    /// <summary>
    /// Find all userRoles
    /// </summary>
    public static IList FindAll()
    {
      return EntityContext.Query<UserRole>().ToList();
    }

    /// <summary>
    /// Find userRole with specified userName
    /// </summary>
    public static UserRole FindByName(string userName)
    {
      IList list = EntityContext.Query<UserRole>().Where(_ => _.Name == userName).ToList();
      if (list.Count == 0)
        return null;
      if (list.Count > 1)
        throw new MetadataException("not unique");
      return (UserRole)list[0];
    }

    #region Factory

    /// <summary>
    /// </summary>
    public static UserRole CreateInstance()
    {
      UserRole role = (UserRole)Entity.CreateInstance();
      role.LastUpdated = DateTime.UtcNow;
      return role;
    }

    /// <summary>
    /// </summary>
    public static ClassMeta Entity => _entity ?? (_entity = ClassCache.Find("UserRole"));

    private static ClassMeta _entity;

    #endregion
  }
}