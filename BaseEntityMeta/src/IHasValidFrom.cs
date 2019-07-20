// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public interface IHasValidFrom
  {
    /// <summary>
    /// 
    /// </summary>
    DateTime AsOf { get; }

    /// <summary>
    /// 
    /// </summary>
    bool SetValidFrom { get; set; }
  }
}