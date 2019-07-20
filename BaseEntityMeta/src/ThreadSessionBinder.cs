// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Threading;
using BaseEntity.Core.Logging;
using log4net;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <returns></returns>
  public sealed class ThreadEntityContextBinder
  {
    // Lookup key used for thread-local storage
    private const string SessionKey = "SessionKey";

    private static readonly ILog Logger = QLogManager.GetLogger(typeof(ThreadEntityContextBinder));

    #region ISessionBinder Members

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEntityContext GetCurrentContext()
    {
      LocalDataStoreSlot sessionSlot = Thread.GetNamedDataSlot(SessionKey);
      var currentContext = (IEntityContext)Thread.GetData(sessionSlot);

      if (Logger.IsDebugEnabled)
      {
        Logger.DebugFormat("GetCurrentContext on Thread [{0}]", Thread.CurrentThread.ManagedThreadId);
      }

      return currentContext;
    }

    /// <summary>
    /// Bind the specified session and return any previously bound session
    /// </summary>
    /// <param name="context"></param>
    public IEntityContext Bind(IEntityContext context)
    {
      LocalDataStoreSlot dataSlot = Thread.GetNamedDataSlot(SessionKey);
      var prevContext = (IEntityContext)Thread.GetData(dataSlot);
      if (!ReferenceEquals(context, prevContext))
      {
        // TODO: Need to handle the case where session is null (since we unbind as well as bind with this method)
        // logger.InfoFormat("Binding session [{0}] on thread [{1}]", session.Timestamp, Thread.CurrentThread.ManagedThreadId);
        Thread.SetData(dataSlot, context);
      }

      return prevContext;
    }

    #endregion
  }
}