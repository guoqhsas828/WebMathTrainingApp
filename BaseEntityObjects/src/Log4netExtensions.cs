// 
// Copyright (c) WebMathTraining 2002-2012. All rights reserved.
// 

using System;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Helper class for logging errors
  /// </summary>
  public static class Log4NetExtensions
  {
    /// <summary>
    /// Log exception message and then throw <see cref="Exception"/>
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="ex"></param>
    public static Exception Exception(this log4net.ILog logger, Exception ex)
    {
      logger.Error(ex.Message);
      return ex;
    }
  }
}
