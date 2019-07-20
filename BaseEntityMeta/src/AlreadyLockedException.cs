// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///
  /// </summary>
  [Serializable]
  public class AlreadyLockedException : Exception
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public AlreadyLockedException(string message)
      : base(message)
    {}

    /// <summary>
    /// 
    /// </summary>
    public AlreadyLockedException(string message, Exception inner)
      : base(message, inner)
    {}

    #endregion
  }
}
