// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  public class InvalidUserException : Exception
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the InvalidUserException class with a 
    /// specified error message.
    /// </summary>
    public InvalidUserException(string message)
      : base(message)
    {}

    #endregion
  }
}
