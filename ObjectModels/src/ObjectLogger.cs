// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Threading;
using log4net;
using log4net.Core;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Interface which all object loggers should coform to
  /// </summary>
  public interface IObjectLogger : ILog
  {
    /// <summary>
    /// Extension method to the standard ILog interface which verifies if object logging
    /// is enabled not just a specific level of logging
    /// </summary>
    /// <returns></returns>
    bool IsObjectLoggingEnabled { get; }
  }

  /// <summary>
  /// Object Logger implmentation. Maintains the functionality of the LogImpl class
  /// while adding the additional functionality of the IObjectLogger interface. Design was
  /// prefered to extension methods as the additional methods should be contrained to a subset
  /// of loggers
  /// </summary>
  public class ObjectLogger : LogImpl, IObjectLogger
  {
    private const string ThreadStorageKey = "ObjectLoggingEnabledKey";

    /// <summary>
    /// Object Logger Constructor
    /// </summary>
    /// <param name="log"></param>
    public ObjectLogger(ILoggerWrapper log) : base(log.Logger) { }

    /// <summary>
    /// Extension method to the standard ILog interface which verifies if object logging
    /// is enabled not just a specific level of logging. This method on return true if
    /// the object logger has at least debug level logging enabled and that object logging
    /// has been enabled within the specific threads context by calling ObjectLogger.EnableObjectLogging()
    /// </summary>
    /// <returns></returns>
    public bool IsObjectLoggingEnabled
    {
      get { return IsDebugEnabled && IsObjectLoggingEnabledWithinTheContext(); }
    }

    /// <summary>
    /// Method for enabling Object Logging within the current context. 
    /// </summary>
    public static void EnableObjectLogging()
    {
      Thread.SetData(Thread.GetNamedDataSlot(ThreadStorageKey), true);
    }

    /// <summary>
    /// Method for disabiling Object Logging within the current context. 
    /// </summary>
    public static void DisableObjectLogging()
    {
      Thread.SetData(Thread.GetNamedDataSlot(ThreadStorageKey), false);
    }

    /// <summary>
    /// Helper method, to verify if Object Logging is enabled within the current context
    /// </summary>
    /// <returns></returns>
    private static bool IsObjectLoggingEnabledWithinTheContext()
    {
      if (Thread.GetData(Thread.GetNamedDataSlot(ThreadStorageKey)) == null)
      {
        return false;
      }
      return (bool)Thread.GetData(Thread.GetNamedDataSlot(ThreadStorageKey));
    }
  }

}