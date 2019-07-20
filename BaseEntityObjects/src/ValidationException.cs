// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Shared
{
  /// <summary>
  /// 
  /// </summary>
  public class ValidationException : Exception
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public ValidationException(string message)
      : base(message)
    {}
  }
}