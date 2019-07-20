// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using NHibernate.Action;
using NHibernate.Event;
using NHibernate.Event.Default;
using System.Reflection;

namespace BaseEntity.Database.Engine
{
  [Serializable]
  internal class MyFlushEventListener : DefaultFlushEventListener
  {
    protected override void PerformExecutions(IEventSource session)
    {
      var interceptor = (AuditInterceptor)session.Interceptor;

      try
      {
        session.ConnectionManager.FlushBeginning();

        //TODO Josh this need to be fixed
        //if (session.ActionQueue.InsertionsCount > 0 || session.ActionQueue.DeletionsCount > 0 || session.ActionQueue.UpdatesCount > 0 || session.ActionQueue.CollectionCreationsCount > 0 ||
        //  session.ActionQueue.CollectionRemovalsCount > 0 || session.ActionQueue.CollectionUpdatesCount > 0)
        //  throw new NotImplementedException("MyFlushEventListener.PerformExectuion");
        //session.ActionQueue.Insertions.RemoveAll(action => RemoveAction(interceptor, action));
        ////session.ActionQueue.Deletions.RemoveAll(action => RemoveAction(interceptor, action));
        ////session.ActionQueue.Updates.RemoveAll(action => RemoveAction(interceptor, action));
        ////session.ActionQueue.CollectionCreations.RemoveAll(action => RemoveAction(interceptor, action));
        ////session.ActionQueue.CollectionRemovals.RemoveAll(action => RemoveAction(interceptor, action));
        ////session.ActionQueue.CollectionUpdates.RemoveAll(action => RemoveAction(interceptor, action));

        session.ActionQueue.PrepareActions();
        session.ActionQueue.ExecuteActions();
      }
      finally
      {
        session.ConnectionManager.FlushEnding();
      }
    }

    private static bool RemoveAction(AuditInterceptor interceptor, IExecutable executable)
    {
      var action = executable as EntityAction;
      return action != null && RemoveAction(interceptor, action);
    }

    private static bool RemoveAction(AuditInterceptor interceptor, EntityAction action)
    {
      if (action.Id is long)
      {
        var id = (long)action.Id;
        if (!interceptor.IsLocked(id) || interceptor.RolledBack.Contains(id))
          return true;
      }
      return false;
    }

    private static bool RemoveAction(AuditInterceptor interceptor, CollectionAction action)
    {
      var type = action.GetType();
      var actionKeyPi = type.GetProperty("Key", BindingFlags.NonPublic | BindingFlags.Instance );
      var actionKey = actionKeyPi.GetValue(action);
      if (actionKey is long)
      {
        var id = (long)actionKey;
        if (!interceptor.IsLocked(id) || interceptor.RolledBack.Contains(id))
          return true;
      }
      return false;
    }
  }
}