// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  ///  Indicates the Import Mode.
  /// </summary>
  public enum ImportMode
  {
    /// <summary>
    ///   Insert new entities. Don't update existing ones.
    /// </summary>
    Insert,

    /// <summary>
    ///   Update existing entities. Don't insert new ones.
    /// </summary>
    Update,

    /// <summary>
    ///   Update existing and insert new.
    /// </summary>
    InsertOrUpdate
  }
}