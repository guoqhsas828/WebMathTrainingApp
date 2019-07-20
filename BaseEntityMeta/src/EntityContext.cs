// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Linq;
using log4net;
using BaseEntity.Core.Logging;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityContext
  {
    private static readonly ThreadEntityContextBinder Binder = new ThreadEntityContextBinder();

    private static readonly ILog Logger = QLogManager.GetLogger(typeof(EntityContext));

    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    public static IEntityContext Current
    {
      get
      {
        var context =  Binder.GetCurrentContext();

        if (context != null && context.IsDisposed())
        {
          Logger.WarnFormat("Trying to get current entity context on the thread, " +
                            "but the one thats bound was already been disposed. " +
                            "Returning null.");
          return null;
        }

        return context;
      }
    }

    /// <summary>
    /// Bind the specified session and return any previously bound session
    /// </summary>
    /// <param name="context"></param>
    /// <remarks>
    /// This is intended only to be used by the DatabaseConfigurator to handle
    /// legacy applications that open and bind a session during the Init.
    /// </remarks>
    public static IEntityContext Bind(IEntityContext context)
    {
      if (Binder == null)
      {
        throw new MetadataException(
          "No session binder was initialized. This usually means that Configurator.Init() was either never called for this application, or the given container does not provide a usable ISessionBinder registration.");
      }

      return Binder.Bind(context);
    }

    /// <summary>
    /// Create an <see cref="IQueryable{T}"/> using the current <see cref="IQueryableEntityContext"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IQueryable<T> Query<T>() where T: PersistentObject
    {
      var entityContext = Current;
      if (entityContext == null)
      {
        throw new MetadataException("No current entity context!");
      }

      var queryableEntityContext = entityContext as IQueryableEntityContext;
      if (queryableEntityContext == null)
      {
        throw new MetadataException("Current entity context [" + entityContext + "] is not an IQueryableEntityContext!");
      }

      return queryableEntityContext.Query<T>();
    }
  }
}