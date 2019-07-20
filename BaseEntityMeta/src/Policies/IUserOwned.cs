// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

namespace BaseEntity.Metadata.Policies
{
  /// <summary>
  /// Implemented by entities that have a single owner and can be written only by that <see cref="User"/>
  /// </summary>
  public interface IUserOwned
  {
    /// <summary>
    /// 
    /// </summary>
    User Owner { get; }
  }
}