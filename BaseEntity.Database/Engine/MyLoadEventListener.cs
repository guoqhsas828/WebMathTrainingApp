// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using log4net;
using NHibernate.Event;
using NHibernate.Event.Default;
using BaseEntity.Metadata;
using System.Threading.Tasks;
using System.Threading;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  internal class MyLoadEventListener : ILoadEventListener
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(MyLoadEventListener));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="event"></param>
    /// <param name="loadType"></param>
    public void OnLoad(LoadEvent @event, LoadType loadType)
    {
      Logger.DebugFormat("{0},{1},{2}", loadType, @event.EntityClassName, @event.EntityId);

      var classMeta = ClassCache.Find((long)@event.EntityId);
      if (classMeta != null)
      {
        ClassMeta @derivedClassMeta;

        if (classMeta.IsBaseEntity)
        {
          var objectId = (long)@event.EntityId;
          var @class = EntityHelper.GetClassFromObjectId(objectId);
          derivedClassMeta = ClassCache.Find(@class);
        }
        else
        {
          derivedClassMeta = classMeta;
        }
        if (loadType == LoadEventListener.InternalLoadLazy)
        {
          // Resolving lazy reference to PersistentObject
          var interceptor = (AuditInterceptor)@event.Session.Interceptor;
          @event.Result = new ObjectRef((long)@event.EntityId, interceptor.EntityContext);
        }
        else
        {
          if (classMeta != derivedClassMeta)
          {
            Logger.DebugFormat("Narrowing EntityClassName from [{0}] to [{1}]", classMeta.Name, derivedClassMeta.Name);
            @event.EntityClassName = derivedClassMeta.Name;
          }

          _defaultLoadEventListener.OnLoad(@event, loadType);
        }
      }
      else
      {
        // Entity not derived from PersistentObject
        _defaultLoadEventListener.OnLoad(@event, loadType);
      }
    }

    public async Task OnLoadAsync(LoadEvent @event, LoadType loadType, CancellationToken token)
    {
      Logger.DebugFormat("{0},{1},{2}", loadType, @event.EntityClassName, @event.EntityId);

      var classMeta = ClassCache.Find((long)@event.EntityId);
      if (classMeta != null)
      {
        ClassMeta @derivedClassMeta;

        if (classMeta.IsBaseEntity)
        {
          var objectId = (long)@event.EntityId;
          var @class = EntityHelper.GetClassFromObjectId(objectId);
          derivedClassMeta = ClassCache.Find(@class);
        }
        else
        {
          derivedClassMeta = classMeta;
        }
        if (loadType == LoadEventListener.InternalLoadLazy)
        {
          // Resolving lazy reference to PersistentObject
          var interceptor = (AuditInterceptor)@event.Session.Interceptor;
          @event.Result = new ObjectRef((long)@event.EntityId, interceptor.EntityContext);
        }
        else
        {
          if (classMeta != derivedClassMeta)
          {
            Logger.DebugFormat("Narrowing EntityClassName from [{0}] to [{1}]", classMeta.Name, derivedClassMeta.Name);
            @event.EntityClassName = derivedClassMeta.Name;
          }

          _defaultLoadEventListener.OnLoad(@event, loadType);
        }
      }
      else
      {
        // Entity not derived from PersistentObject
        _defaultLoadEventListener.OnLoad(@event, loadType);
      }
    }

    private readonly ILoadEventListener _defaultLoadEventListener = new DefaultLoadEventListener();
  }
}