// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Shared
{
  /// <summary>
  /// 
  /// </summary>
  public class PolicyResult
  {
    /// <summary>
    /// 
    /// </summary>
    public PolicyResult()
    {
      Errors = new InvalidValue[0];
      Success = true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public PolicyResult(ArrayList errors)
    {
      Errors = errors == null ? new InvalidValue[0] : errors.Cast<InvalidValue>();
      Success = !Errors.Any();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public PolicyResult(IEnumerable<InvalidValue> errors)
    {
      Errors = errors ?? new InvalidValue[0];
      Success = !Errors.Any();
    }

    /// <summary>
    /// 
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<InvalidValue> Errors { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public string Message
    {
      get { return string.Join(Environment.NewLine, Errors.Select(iv => iv.Message)); }
    }
  }
}