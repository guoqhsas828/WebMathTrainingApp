// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Used to indicate if an EntityPolicy applies to an entity instance
  /// </summary>
  public enum FilterResult
  {
    /// <summary>
    /// The EntityPolicy does not apply to this instance
    /// </summary>
    None,

    /// <summary>
    /// The EntityPolicy does apply, permissions are determined by the settings on this policy
    /// </summary>
    Pass,

    /// <summary>
    /// The EntityPolicy does apply, all permissions are false
    /// </summary>
    Fail,
  }
}