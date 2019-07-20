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
  public class SecurityException : Exception
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public SecurityException(string message)
      : base(message)
    {}

    /// <summary>
    /// 
    /// </summary>
    public SecurityException(string message, Exception inner)
      : base(message, inner)
    {}

    #endregion
  }
}
