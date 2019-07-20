// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Wrapper representing the diff between two versions of an entity
  /// </summary>
  public class EntityTidDiffGram : EntityDiffGram
  {
    /// <summary>
    /// Gets or sets the Tid
    /// </summary>
    /// <value>The id.</value>
    public int NextTid { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public int PrevTid { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <value>The name of the user.</value>
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the LastUpdated time
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime LastUpdated { get; set; }
  }
}