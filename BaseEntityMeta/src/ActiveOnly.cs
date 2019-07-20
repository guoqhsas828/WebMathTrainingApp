/*
 * ActiveOnly.cs
 *
 * Copyright (c) WebMathTraining 2010. All rights reserved.
 *
 */

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class ActiveOnly
  {
    /// <summary>
    /// 
    /// </summary>
    public bool Checked;

    /// <summary>
    /// 
    /// </summary>
    public DateTime Date;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveOnly"/> class.
    /// </summary>
    public ActiveOnly()
    {
      Checked = true;
      Date = DateTime.Today;
    }
  }
}
