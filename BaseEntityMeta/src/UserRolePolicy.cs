// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Base class for all policy classes that apply to UserRoles
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public abstract class UserRolePolicy : BaseEntityObject, IComparable<UserRolePolicy>
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public virtual int CompareTo(UserRolePolicy other)
    {
      return string.Compare(GetType().Name, other.GetType().Name, StringComparison.Ordinal);
    }
  }
}