// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using NHibernate.Event;
using NHibernate.Event.Default;

namespace BaseEntity.Database.Engine
{
  internal class MyInitializeCollectionEventListener : DefaultInitializeCollectionEventListener
  {
    public override void OnInitializeCollection(InitializeCollectionEvent @event)
    {
      base.OnInitializeCollection(@event);

      var session = @event.Session;
      var interceptor = (AuditInterceptor)session.Interceptor;
      interceptor.RollbackEvents();
    }
  }
}