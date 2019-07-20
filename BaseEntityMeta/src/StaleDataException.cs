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
  public class StaleDataException : Exception
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public StaleDataException(string message)
      : base(message)
    {}

    /// <summary>
    /// 
    /// </summary>
    public StaleDataException(string message, Exception inner)
      : base(message, inner)
    {}

    #endregion
  }
}
