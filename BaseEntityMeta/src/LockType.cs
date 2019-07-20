// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public enum LockType
  {
    /// <summary>
    /// Not locked or lock is freed
    /// </summary>
    None,

    /// <summary>
    /// Locked for insert (create)
    /// </summary>
    Insert,

    /// <summary>
    /// Locked for update (edit)
    /// </summary>
    Update,

    /// <summary>
    /// Locked for delete (purge)
    /// </summary>
    Delete
  }
}