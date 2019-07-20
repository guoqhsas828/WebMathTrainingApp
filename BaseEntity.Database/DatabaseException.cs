// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Database
{
  /// <summary>
  ///   Base class for all database related exceptions
  /// </summary>
  [Serializable]
  public class DatabaseException : Exception
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the DatabaseException class with a 
    /// specified error message.
    /// </summary>
    public DatabaseException(string message)
      : base(message)
    {}

    /// <summary>
    /// Initializes a new instance of the DatabaseException class with a 
    /// specified error message and a reference to the inner exception that 
    /// is the cause of this exception.
    /// </summary>
    public DatabaseException(string message, Exception inner)
      : base(message, inner)
    {}

    #endregion
  }
}