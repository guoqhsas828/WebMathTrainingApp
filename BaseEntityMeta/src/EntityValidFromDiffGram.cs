// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Wrapper representing the diff between two versions of an entity
  /// </summary>
  public class EntityValidFromDiffGram : EntityDiffGram
  {
    /// <summary>
    /// Gets or sets the Tid
    /// </summary>
    /// <value>The id.</value>
    public DateTime NextValidFrom { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public DateTime PrevValidFrom { get; set; }
  }
}