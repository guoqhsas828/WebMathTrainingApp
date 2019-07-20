
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  ///   
  /// </summary>
  [Serializable]
  public class BusinessEventRollbackException : Exception
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the BusinessEventRollbackException 
    /// class with a specified error message.
    /// </summary>
    public BusinessEventRollbackException(string message)
      : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BusinessEventRollbackException 
    /// class with a specified error message and a reference to the inner 
    /// exception that is the cause of this exception.
    /// </summary>
    public BusinessEventRollbackException(string message, Exception inner)
      : base(message, inner)
    {
    }

    #endregion
  }
}