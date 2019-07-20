
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  ///   
  /// </summary>
  [Serializable]
  public class BusinessEventApplyException : Exception
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the BusinessEventApplyException 
    /// class with a specified error message.
    /// </summary>
    public BusinessEventApplyException(string message)
      : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BusinessEventApplyException 
    /// class with a specified error message and a reference to the inner 
    /// exception that is the cause of this exception.
    /// </summary>
    public BusinessEventApplyException(string message, Exception inner)
      : base(message, inner)
    {
    }

    #endregion
  }
}
