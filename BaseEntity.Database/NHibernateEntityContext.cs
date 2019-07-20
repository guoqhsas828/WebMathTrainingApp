// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Engine;
using NHibernate.Exceptions;
using NHibernate.Impl;
using NHibernate.Linq;
using NHibernate.Stat;
using NHibernate.Type;
using BaseEntity.Database.Engine;
using BaseEntity.Metadata;
using System.Threading.Tasks;
#if NETSTANDARD2_0
using IDataReader = System.Data.Common.DbDataReader;
using IDbCommand = System.Data.Common.DbCommand;
using IDbConnection = System.Data.Common.DbConnection;
#endif

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class NHibernateEntityContext : EditableEntityContextBase, IQueryableAndEditableEntityContext, ILoadableEntityContext, ISession
  {
    #region Data

    private static readonly ILog Logger = LogManager.GetLogger(typeof(NHibernateEntityContext));

    // Use same logger for BulkUpdate as NHibernate uses for regular SQL
    private static readonly ILog SqlLogger = LogManager.GetLogger(typeof(NHibernate.SessionException)); // "NHibernate.SQL"

    private string _comment;
    private Guid _transactionId;

    private SessionImpl _session;
    private readonly AuditInterceptor _interceptor;

    private readonly IDictionary<long, PersistentObject> _revived = new Dictionary<long, PersistentObject>();

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public NHibernateEntityContext()
      : this(DateTime.Today)
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="NHibernateEntityContext"/> class.
    /// </summary>
    /// <param name="readWriteMode">The read write mode.</param>
    public NHibernateEntityContext(ReadWriteMode readWriteMode)
      : this(DateTime.Today, readWriteMode)
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    public NHibernateEntityContext(DateTime asOf)
      : this(asOf, asOf == DateTime.Today ? ReadWriteMode.ReadWrite : ReadWriteMode.ReadOnly)
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="readWriteMode"></param>
    /// <param name="setValidFrom"></param>
    public NHibernateEntityContext(DateTime asOf, ReadWriteMode readWriteMode, bool setValidFrom) : this(asOf, readWriteMode,
      setValidFrom ? HistorizationPolicy.All : HistorizationPolicy.None)
    { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="readWriteMode"></param>
    /// <param name="historizationPolicy"></param>
    public NHibernateEntityContext(DateTime asOf, ReadWriteMode readWriteMode, HistorizationPolicy historizationPolicy = HistorizationPolicy.None)
      : base(asOf, readWriteMode, historizationPolicy)
    {
      if (historizationPolicy != HistorizationPolicy.None)
      {
        if (asOf.Date > DateTime.Today)
        {
          throw new ArgumentException("Session AsOf cannot be in the future!");
        }
      }

      _session = (SessionImpl)SessionFactory.OpenSession(this);
      _interceptor = (AuditInterceptor)_session.Interceptor;
      _interceptor.HistorizationPolicy = historizationPolicy;
    }

    #endregion

    #region IEntityContext Members

    /// <summary>
    /// Is the <c>ISession</c> still open?
    /// </summary>
    public override bool IsOpen => _session != null && _session.IsOpen;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override bool IsDisposed()
    {
      return _isDisposed;
    }

    /// <summary>
    /// Get the <see cref="PersistentObject"/> with the specified id if in the context, else null
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public override PersistentObject Get(long id)
    {
      if (id == 0)
      {
        return null;
      }

      if (EntityHelper.IsTransient(id))
      {
        return TransientContext.Get(id);
      }

      var cm = ClassCache.Find(id);
      var po = (PersistentObject)_session.Get(cm.Type, id);
      if (po == null && AsOf < DateTime.Today)
      {
        if (_revived.TryGetValue(id, out po))
        {
        }
        else if (!cm.OldStyleValidFrom)
        {
          po = GetDeletedEntity(id);
          _interceptor.RolledBack.Add(id);
          _revived[id] = po;
        }
      }

      _interceptor.RollbackEvents();

      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override IEnumerator<PersistentObject> GetEnumerator()
    {
      var walker = new OwnedOrRelatedObjectWalker();

      foreach (var po in TransientContext)
      {
        walker.Walk(po);
      }

      foreach (var po in walker.OwnedObjects)
      {
        yield return po;
      }

      foreach (var po in _session.PersistenceContext.EntitiesByKey.Values.OfType<PersistentObject>())
      {
        yield return po;
      }
    }

    #endregion

    #region ILoadableEntityConext Members

    void ILoadableEntityContext.Load(PersistentObject po)
    {
      Merge(po);
    }

    #endregion

    #region IQueryableEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IQueryable<T> Query<T>()
    {
      return new QueryableProxy<T>(_session.Query<T>(), Interceptor);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IQueryable<T> Query<T>(string query)
    {
      return new QueryableProxy<T>(_session.Query<T>(query), Interceptor);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="keyList"></param>
    /// <returns></returns>
    public PersistentObject FindByKey(ClassMeta cm, IList<object> keyList)
    {
      // Lookup in database
      var keyPropList = cm.KeyPropertyList;
      ICriteria criteria = Session.CreateCriteria(cm.Type);
      for (int i = 0; i < keyPropList.Count; i++)
      {
        PropertyMeta keyProp = keyPropList[i];
        if (keyList[i] == null)
        {
          // Can happen when AllowNullableKey = true
          if (!keyProp.IsNullable)
            throw new DatabaseException($"{keyProp.Name} cannot be null");
          criteria.Add(Restrictions.IsNull(keyProp.Name));
        }
        else
        {
          var key = keyList[i] as PersistentObject;
          // If it is a persistent object compare on the ObjectId
          // otherwise compare on the object
          criteria.Add(key == null
            ? Restrictions.Eq(keyProp.Name, keyList[i])
            : Restrictions.Eq(keyProp.Name + ".id", key.ObjectId));
        }
      }

      try
      {
        IList list = criteria.List();
        if (list.Count == 0)
        {
          return null;
        }

        if (list.Count == 1)
        {
          var po = (PersistentObject)list[0];
          return po;
        }

        throw new DatabaseException("Unique key violation");
      }
      catch (Exception ex)
      {
        string hashKey = PersistentObject.FormKey(cm, keyList);
        throw new DatabaseException($"Error querying {cm.Name} with criteria [{hashKey}] : {ex}");
      }
    }

    #endregion

    #region IEditableEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public override long Save(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException(nameof(po));
      }

      var cm = ClassCache.Find(po);
      if (cm != null && cm.IsChildEntity)
      {
        throw new ArgumentException("Cannot call Save on ChildEntity [" + cm.Name + "]");
      }

      if (po.IsAnonymous)
      {
        _session.Save(po);
      }
      else
      {
        throw new InvalidOperationException("Attempt to save entity with ObjectId = [" + po.ObjectId + "]");
      }

      return po.ObjectId;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    protected override long SaveChild(PersistentObject po)
    {
      return (long)_session.Save(po);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public override void Delete(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException(nameof(po));
      }

      if (po.ObjectId != 0)
      {
        // Request Delete lock on this entity and its children.
        // The Delete itself will be cascaded by NHibernate, all we do in the
        // OnDelete handler is validate that the Delete lock exists.
        InternalRequestLock(po, LockType.Delete, null);
      }

      _session.Delete(po);
    }

    /// <summary>
    /// Commit the current transation
    /// </summary>
    /// <param name="comment">Optional comment to write to the CommitLog</param>
    public override void CommitTransaction(string comment = null)
    {
      if (EntityContextFactory.UserRole.ReadOnly)
      {
        throw new DatabaseException("ReadOnly user not allowed to call Commit!");
      }

      if (ReadWriteMode == ReadWriteMode.ReadOnly)
      {
        throw new InvalidOperationException("Cannot CommitTransaction using ReadOnly context");
      }

      if (_interceptor.RolledBack.Any() && !SecurityPolicy.CheckNamedPolicy("CanEditHistory"))
      {
        throw new DatabaseException("User is not allowed to commit changes to non-current versions!");
      }

      // Check if User has permission to create a new version
      if (HistorizationPolicy != HistorizationPolicy.None)
      {
        // At this point the new state's ValidFrom is not set to Asof
        if (RootLocks.Where(_ => _.LockType != LockType.Delete).Select(_ => _.NewState).OfType<AuditedObject>()
          .Any(_ => _.ValidFrom.ToDateTime() != AsOf.ToDateTime()))
        {
          if (!SecurityPolicy.CheckNamedPolicy("CanCreateHistoryVersion"))
          {
            throw new DatabaseException("User is not allowed to create new versions!");
          }
        }
      }

      var stopwatch = new Stopwatch();
      stopwatch.Start();

      try
      {
        Logger.DebugFormat("Commit transaction on thread [{0}]", Thread.CurrentThread.ManagedThreadId);

        SaveTransients();
      
        _comment = comment;
        _session.Transaction.Commit();

        // Start new transaction.  The reason we do this is that
        // NHibernate strongly prefers all SQL (including queries)
        // be in a transaction.
        _session.BeginTransaction();
      }
      catch (Exception ex)
      {
        var message = ex.Message;
        var genericAdoException = ex as GenericADOException;
        if (genericAdoException != null)
        {
          var sqlException = ex.InnerException as SqlException;
          if (sqlException != null)
          {
            message = sqlException.Message;
          }
        }

        throw new DatabaseException($"Commit failed: {message}", ex);
      }
      finally
      {
        stopwatch.Stop();
        Logger.DebugFormat("CommitTransaction: {0}", stopwatch.Elapsed);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override void RollbackTransaction()
    {
      Logger.DebugFormat("Rollback transaction on thread [{0}]", Thread.CurrentThread.ManagedThreadId);

      _session.Transaction.Rollback();

      FreeAllEntityLocks();

      _comment = null;

      _transactionId = Guid.Empty;

      TransientContext.Clear();

      // Start new transaction.  The reason we do this is that
      // NHibernate strongly prefers all SQL (including queries)
      // be in a transaction.
      _session.BeginTransaction();
    }

    #endregion

    #region ISession Members

    /// <summary> The entity mode in effect for this session.</summary>
    //EntityMode ISession.ActiveEntityMode => _session.ActiveEntityMode;

    /// <summary>
    /// Force the <c>ISession</c> to flush.
    /// </summary>
    /// <remarks>
    /// Flush may cause locks to be held in the underlying persistent store and should only 
    /// be called by application code in very rare cases. For most use cases, applications
    /// should rely on the default behavior, where Flush() is called internally when the
    /// transaction is committed.
    /// </remarks>
    public void Flush()
    {
      _session.Flush();
    }

    public async Task FlushAsync(CancellationToken token)
    {
      await _session.FlushAsync();
    }

    /// <summary>
    /// Determines at which points Hibernate automatically flushes the session.
    /// </summary>
    FlushMode ISession.FlushMode
    {
      get { return _session.FlushMode; }
      set { _session.FlushMode = value; }
    }

    /// <summary> The current cache mode. </summary>
    CacheMode ISession.CacheMode
    {
      get { return _session.CacheMode; }
      set { _session.CacheMode = value; }
    }

    /// <summary>
    /// Get the <see cref="ISessionFactory" /> that created this instance.
    /// </summary>
    ISessionFactory ISession.SessionFactory => _session.SessionFactory;

    /// <summary>
    /// Gets the ADO.NET connection.
    /// </summary>
    public DbConnection Connection => _session.Connection;

    /// <summary>
    /// Disconnect the <c>ISession</c> from the current ADO.NET connection.
    /// </summary>
    /// <remarks>
    /// If the connection was obtained by Hibernate, close it or return it to the connection
    /// pool. Otherwise return it to the application. This is used by applications which require
    /// long transactions.
    /// </remarks>
    /// <returns>The connection provided by the application or <see langword="null" /></returns>
    public DbConnection Disconnect()
    {
      return _session.Disconnect();
    }

    /// <summary>
    /// Obtain a new ADO.NET connection.
    /// </summary>
    /// <remarks>
    /// This is used by applications which require long transactions
    /// </remarks>
    void ISession.Reconnect()
    {
      _session.Reconnect();
    }

    /// <summary>
    /// Reconnect to the given ADO.NET connection.
    /// </summary>
    /// <remarks>This is used by applications which require long transactions</remarks>
    /// <param name="connection">An ADO.NET connection</param>
    void ISession.Reconnect(DbConnection connection)
    {
      _session.Reconnect(connection);
    }

    /// <summary>
    /// End the <c>ISession</c> by disconnecting from the ADO.NET connection and cleaning up.
    /// </summary>
    /// <remarks>
    /// It is not strictly necessary to <c>Close()</c> the <c>ISession</c> but you must
    /// at least <c>Disconnect()</c> it.
    /// </remarks>
    /// <returns>The connection provided by the application or <see langword="null" /></returns>
    DbConnection ISession.Close()
    {
      return _session.Close();
    }

    /// <summary>
    /// Cancel execution of the current query.
    /// </summary>
    /// <remarks>
    /// May be called from one thread to stop execution of a query in another thread.
    /// Use with care!
    /// </remarks>
    void ISession.CancelQuery()
    {
      _session.CancelQuery();
    }

    /// <summary>
    /// Is the <c>ISession</c> currently connected?
    /// </summary>
    bool ISession.IsConnected => _session.IsConnected;

    /// <summary>
    /// Does this <c>ISession</c> contain any changes which must be
    /// synchronized with the database? Would any SQL be executed if
    /// we flushed this session?
    /// </summary>
    bool ISession.IsDirty()
    {
      return _session.IsDirty();
    }

    async Task<bool> ISession.IsDirtyAsync(CancellationToken cancellationToken)
    {
      return await _session.IsDirtyAsync(cancellationToken);
    }

    /// <summary>
    /// Is the specified entity (or proxy) read-only?
    /// </summary>
    /// <remarks>
    /// Facade for <see cref="IPersistenceContext.IsReadOnly(object)" />.
    /// </remarks>
    /// <param name="entityOrProxy">An entity (or <see cref="NHibernate.Proxy.INHibernateProxy" />)</param>
    /// <returns>
    /// <c>true</c> if the entity (or proxy) is read-only, otherwise <c>false</c>.
    /// </returns>
    /// <seealso cref="ISession.DefaultReadOnly" />
    /// <seealso cref="ISession.SetReadOnly(object, bool)" />
    bool ISession.IsReadOnly(object entityOrProxy)
    {
      return _session.IsReadOnly(entityOrProxy);
    }

    /// <summary>
    /// Change the read-only status of an entity (or proxy).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read-only entities can be modified, but changes are not persisted. They are not dirty-checked 
    /// and snapshots of persistent state are not maintained. 
    /// </para>
    /// <para>
    /// Immutable entities cannot be made read-only.
    /// </para>
    /// <para>
    /// To set the <em>default</em> read-only setting for entities and proxies that are loaded 
    /// into the session, see <see cref="ISession.DefaultReadOnly" />.
    /// </para>
    /// <para>
    /// This method a facade for <see cref="IPersistenceContext.SetReadOnly(object, bool)" />.
    /// </para>
    /// </remarks>
    /// <param name="entityOrProxy">An entity (or <see cref="NHibernate.Proxy.INHibernateProxy" />).</param>
    /// <param name="readOnly">If <c>true</c>, the entity or proxy is made read-only; if <c>false</c>, it is made modifiable.</param>
    /// <seealso cref="ISession.DefaultReadOnly" />
    /// <seealso cref="ISession.IsReadOnly(object)" />
    void ISession.SetReadOnly(object entityOrProxy, bool readOnly)
    {
      _session.SetReadOnly(entityOrProxy, readOnly);
    }

    /// <summary>
    /// The read-only status for entities (and proxies) loaded into this Session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a proxy is initialized, the loaded entity will have the same read-only setting
    /// as the uninitialized proxy, regardless of the session's current setting.
    /// </para>
    /// <para>
    /// To change the read-only setting for a particular entity or proxy that is already in 
    /// this session, see <see cref="ISession.SetReadOnly(object, bool)" />.
    /// </para>
    /// <para>
    /// To override this session's read-only setting for entities and proxies loaded by a query,
    /// see <see cref="IQuery.SetReadOnly(bool)" />.
    /// </para>
    /// <para>
    /// This method is a facade for <see cref="IPersistenceContext.DefaultReadOnly" />.
    /// </para>
    /// </remarks>
    /// <seealso cref="ISession.IsReadOnly(object)" />
    /// <seealso cref="ISession.SetReadOnly(object, bool)" />
    bool ISession.DefaultReadOnly
    {
      get { return _session.DefaultReadOnly; }
      set { _session.DefaultReadOnly = value; }
    }

    /// <summary>
    /// Return the identifier of an entity instance cached by the <c>ISession</c>
    /// </summary>
    /// <remarks>
    /// Throws an exception if the instance is transient or associated with a different
    /// <c>ISession</c>
    /// </remarks>
    /// <param name="obj">a persistent instance</param>
    /// <returns>the identifier</returns>
    object ISession.GetIdentifier(object obj)
    {
      return _session.GetIdentifier(obj);
    }

    /// <summary>
    /// Is this instance associated with this Session?
    /// </summary>
    /// <param name="obj">an instance of a persistent class</param>
    /// <returns>true if the given instance is associated with this Session</returns>
    public bool Contains(object obj)
    {
      return _session.Contains(obj);
    }

    /// <summary>
    /// Remove this instance from the session cache.
    /// </summary>
    /// <remarks>
    /// Changes to the instance will not be synchronized with the database.
    /// This operation cascades to associated instances if the association is mapped
    /// with <c>cascade="all"</c> or <c>cascade="all-delete-orphan"</c>.
    /// </remarks>
    /// <param name="obj">a persistent instance</param>
    public void Evict(object obj)
    {
      _session.Evict(obj);

      var po = obj as PersistentObject;
      if (po == null)
        return;

      base.Evict(po);

      var walker = new OwnedObjectWalker(true);
        
      walker.Walk(po);

      foreach (var oo in walker.OwnedObjects)
      {
        _revived.Remove(oo.ObjectId);
        _interceptor.RolledBack.Remove(oo.ObjectId);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public override void Evict(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException(nameof(po));
      }

      _session.Evict(po);

      _interceptor.Evict(po.ObjectId);

      _revived.Remove(po.ObjectId);

      base.Evict(po);
    }

    public async Task EvictAsync(object obj, CancellationToken token)
    {
      await _session.EvictAsync(obj);

      var po = obj as PersistentObject;
      if (po == null)
        return;

      base.Evict(po);

      var walker = new OwnedObjectWalker(true);

      walker.Walk(po);

      foreach (var oo in walker.OwnedObjects)
      {
        _revived.Remove(oo.ObjectId);
        _interceptor.RolledBack.Remove(oo.ObjectId);
      }
    }

    /// <summary>
    /// Return the persistent instance of the given entity class with the given identifier,
    /// obtaining the specified lock mode.
    /// </summary>
    /// <param name="theType">A persistent class</param>
    /// <param name="id">A valid identifier of an existing persistent instance of the class</param>
    /// <param name="lockMode">The lock level</param>
    /// <returns>the persistent instance</returns>
    object ISession.Load(Type theType, object id, LockMode lockMode)
    {
      return _session.Load(theType, id, lockMode);
    }

    async Task<object> ISession.LoadAsync(Type theType, object id, LockMode lockMode, CancellationToken token)
    {
      return await _session.LoadAsync(theType, id, lockMode, token);
    }

    /// <summary>
    /// Return the persistent instance of the given entity class with the given identifier,
    /// obtaining the specified lock mode, assuming the instance exists.
    /// </summary>
    /// <param name="entityName">The entity-name of a persistent class</param>
    /// <param name="id">a valid identifier of an existing persistent instance of the class </param>
    /// <param name="lockMode">the lock level </param>
    /// <returns> the persistent instance or proxy </returns>
    object ISession.Load(string entityName, object id, LockMode lockMode)
    {
      return _session.Load(entityName, id, lockMode);
    }

    async Task<object> ISession.LoadAsync(string entityName, object id, LockMode lockMode, CancellationToken token)
    {
      return await _session.LoadAsync(entityName, id, lockMode);
    }

    /// <summary>
    /// Return the persistent instance of the given entity class with the given identifier,
    /// obtaining the specified lock mode.
    /// </summary>
    /// <typeparam name="T">A persistent class</typeparam>
    /// <param name="id">A valid identifier of an existing persistent instance of the class</param>
    /// <param name="lockMode">The lock level</param>
    /// <returns>the persistent instance</returns>
    T ISession.Load<T>(object id, LockMode lockMode)
    {
      return _session.Load<T>(id, lockMode);
    }

    async Task<T> ISession.LoadAsync<T>(object id, LockMode lockMode, CancellationToken token)
    {
      return await _session.LoadAsync<T>(id, lockMode, token);
    }

    /// <summary>
    /// Return the persistent instance of the given entity class with the given identifier,
    /// assuming that the instance exists.
    /// </summary>
    /// <remarks>
    /// You should not use this method to determine if an instance exists (use a query or
    /// <see cref="Get{T}(object)" /> instead). Use this only to retrieve an instance that you
    /// assume exists, where non-existence would be an actual error.
    /// </remarks>
    /// <typeparam name="T">A persistent class</typeparam>
    /// <param name="id">A valid identifier of an existing persistent instance of the class</param>
    /// <returns>The persistent instance or proxy</returns>
    T ISession.Load<T>(object id)
    {
      return _session.Load<T>(id);
    }

    async Task<T> ISession.LoadAsync<T>(object id, CancellationToken token)
    {
      return await _session.LoadAsync<T>(id);
    }

    /// <summary>
    /// Return the persistent instance of the given <paramref name="entityName"/> with the given identifier,
    /// assuming that the instance exists.
    /// </summary>
    /// <param name="entityName">The entity-name of a persistent class</param>
    /// <param name="id">a valid identifier of an existing persistent instance of the class </param>
    /// <returns> The persistent instance or proxy </returns>
    /// <remarks>
    /// You should not use this method to determine if an instance exists (use <see cref="Get(string,object)"/>
    /// instead). Use this only to retrieve an instance that you assume exists, where non-existence
    /// would be an actual error.
    /// </remarks>
    object ISession.Load(string entityName, object id)
    {
      return _session.Load(entityName, id);
    }

    async Task<object> ISession.LoadAsync(string entityName, object id, CancellationToken token)
    {
      return await _session.LoadAsync(entityName, id, token);
    }

    /// <summary>
    /// Read the persistent state associated with the given identifier into the given transient
    /// instance.
    /// </summary>
    /// <param name="obj">An "empty" instance of the persistent class</param>
    /// <param name="id">A valid identifier of an existing persistent instance of the class</param>
    void ISession.Load(object obj, object id)
    {
      _session.Load(obj, id);
    }

    /// <summary>
    /// Persist all reachable transient objects, reusing the current identifier
    /// values. Note that this will not trigger the Interceptor of the Session.
    /// </summary>
    /// <param name="obj">a detached instance of a persistent class</param>
    /// <param name="replicationMode"></param>
    void ISession.Replicate(object obj, ReplicationMode replicationMode)
    {
      _session.Replicate(obj, replicationMode);
    }

    async Task ISession.ReplicateAsync(object obj, ReplicationMode replicationMode, CancellationToken token)
    {
      await _session.ReplicateAsync(obj, replicationMode, token);
    }
    /// <summary>
    /// Persist the state of the given detached instance, reusing the current
    /// identifier value.  This operation cascades to associated instances if
    /// the association is mapped with <tt>cascade="replicate"</tt>.
    /// </summary>
    /// <param name="entityName"></param>
    /// <param name="obj">a detached instance of a persistent class </param>
    /// <param name="replicationMode"></param>
    void ISession.Replicate(string entityName, object obj, ReplicationMode replicationMode)
    {
      _session.Replicate(entityName, obj, replicationMode);
    }

    async Task ISession.ReplicateAsync(string entityName, object obj, ReplicationMode replicationMode, CancellationToken token)
    {
      await _session.ReplicateAsync(entityName, obj, replicationMode, token);
    }

    async Task<object> ISession.SaveAsync(object obj, CancellationToken token)
    {
      return await _session.SaveAsync(obj, token);
    }

    /// <summary>
    /// Persist the given transient instance, using the given identifier.
    /// </summary>
    /// <param name="obj">A transient instance of a persistent class</param>
    /// <param name="id">An unused valid identifier</param>
    void ISession.Save(object obj, object id)
    {
      _session.Save(obj, id);
    }

    async Task ISession.SaveAsync(object obj, object id, CancellationToken token)
    {
      await _session.SaveAsync(obj, id, token);
    }

    /// <summary>
    /// Persist the given transient instance, first assigning a generated identifier. (Or
    /// using the current value of the identifier property if the <tt>assigned</tt>
    /// generator is used.)
    /// </summary>
    /// <param name="entityName">The Entity name.</param>
    /// <param name="obj">a transient instance of a persistent class </param>
    /// <returns> the generated identifier </returns>
    /// <remarks>
    /// This operation cascades to associated instances if the
    /// association is mapped with <tt>cascade="save-update"</tt>.
    /// </remarks>
    object ISession.Save(string entityName, object obj)
    {
      return _session.Save(entityName, obj);
    }

    async Task<object> ISession.SaveAsync(string entityName, object obj, CancellationToken token)
    {
      return await _session.SaveAsync(entityName, obj, token);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityName"></param>
    /// <param name="obj"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    void ISession.Save(string entityName, object obj, object id)
    {
      _session.Save(entityName, obj, id);
    }

    async Task ISession.SaveAsync(string entityName, object obj, object id, CancellationToken token)
    {
      await _session.SaveAsync(entityName, obj, id, token);
    }

    /// <summary>
    /// Either <see cref="ISession.Save(String,Object)"/> or <see cref="ISession.Update(String,Object)"/>
    /// the given instance, depending upon resolution of the unsaved-value checks
    /// (see the manual for discussion of unsaved-value checking).
    /// </summary>
    /// <param name="entityName">The name of the entity </param>
    /// <param name="obj">a transient or detached instance containing new or updated state </param>
    /// <seealso cref="ISession.Save(String,Object)"/>
    /// <seealso cref="ISession.Update(String,Object)"/>
    /// <remarks>
    /// This operation cascades to associated instances if the association is mapped
    /// with <tt>cascade="save-update"</tt>.
    /// </remarks>
    void ISession.SaveOrUpdate(string entityName, object obj)
    {
      _session.SaveOrUpdate(entityName, obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityName"></param>
    /// <param name="obj"></param>
    /// <param name="id"></param>
    void ISession.SaveOrUpdate(string entityName, object obj, object id)
    {
      _session.SaveOrUpdate(entityName, obj, id);
    }

    async Task ISession.SaveOrUpdateAsync(string entityName, object obj, object id, CancellationToken token)
    {
      await _session.SaveOrUpdateAsync(entityName, obj, id, token);
    }

    /// <summary>
    /// Update the persistent instance with the identifier of the given transient instance.
    /// </summary>
    /// <remarks>
    /// If there is a persistent instance with the same identifier, an exception is thrown. If
    /// the given transient instance has a <see langword="null" /> identifier, an exception will be thrown.
    /// </remarks>
    /// <param name="obj">A transient instance containing updated state</param>
    void ISession.Update(object obj)
    {
      _session.Update(obj);
    }

    async Task ISession.UpdateAsync(object obj, CancellationToken token)
    {
      await _session.UpdateAsync(obj);
    }

    /// <summary>
    /// Update the persistent state associated with the given identifier.
    /// </summary>
    /// <remarks>
    /// An exception is thrown if there is a persistent instance with the same identifier
    /// in the current session.
    /// </remarks>
    /// <param name="obj">A transient instance containing updated state</param>
    /// <param name="id">Identifier of persistent instance</param>
    void ISession.Update(object obj, object id)
    {
      _session.Update(obj, id);
    }

    async Task ISession.UpdateAsync(object obj, object id, CancellationToken token)
    {
      await _session.UpdateAsync(obj, id);
    }

    /// <summary>
    /// Update the persistent instance with the identifier of the given detached
    /// instance.
    /// </summary>
    /// <param name="entityName">The Entity name.</param>
    /// <param name="obj">a detached instance containing updated state </param>
    /// <remarks>
    /// If there is a persistent instance with the same identifier,
    /// an exception is thrown. This operation cascades to associated instances
    /// if the association is mapped with <tt>cascade="save-update"</tt>.
    /// </remarks>
    void ISession.Update(string entityName, object obj)
    {
      _session.Update(entityName, obj);
    }

    async Task ISession.UpdateAsync(string entityName, object obj, CancellationToken token)
    {
      await _session.UpdateAsync(entityName, obj);
    }

    /// <summary>
    /// Update the persistent instance with the identifier of the given detached
    /// instance.
    /// </summary>
    /// <param name="entityName">The Entity name.</param>
    /// <param name="obj">a detached instance containing updated state </param>
    /// <remarks>
    /// If there is a persistent instance with the same identifier,
    /// an exception is thrown. This operation cascades to associated instances
    /// if the association is mapped with <tt>cascade="save-update"</tt>.
    /// </remarks>
    void ISession.Update(string entityName, object obj, object id)
    {
      _session.Update(entityName, obj, id);
    }

    async Task ISession.UpdateAsync(string entityName, object obj, object id, CancellationToken token)
    {
      await _session.UpdateAsync(entityName, obj, id);
    }

    /// <summary>
    /// Copy the state of the given object onto the persistent object with the same
    /// identifier. If there is no persistent instance currently associated with
    /// the session, it will be loaded. Return the persistent instance. If the
    /// given instance is unsaved, save a copy of and return it as a newly persistent
    /// instance. The given instance does not become associated with the session.
    /// This operation cascades to associated instances if the association is mapped
    /// with <tt>cascade="merge"</tt>.<br/>
    /// The semantics of this method are defined by JSR-220.
    /// <param name="entityName">Name of the entity.</param>
    /// <param name="obj">a detached instance with state to be copied </param>
    /// <returns> an updated persistent instance </returns>
    /// </summary>
    /// <returns></returns>
    object ISession.Merge(string entityName, object obj)
    {
      return _session.Merge(entityName, obj);
    }

    async Task<object> ISession.MergeAsync(string entityName, object obj, CancellationToken token)
    {
      return await _session.MergeAsync(entityName, obj, token);
    }

    async Task<object> ISession.MergeAsync(object obj, CancellationToken token)
    {
      return await _session.MergeAsync(obj, token);
    }

    /// <summary>
    /// Copy the state of the given object onto the persistent object with the same
    /// identifier. If there is no persistent instance currently associated with
    /// the session, it will be loaded. Return the persistent instance. If the
    /// given instance is unsaved, save a copy of and return it as a newly persistent
    /// instance. The given instance does not become associated with the session.
    /// This operation cascades to associated instances if the association is mapped
    /// with <tt>cascade="merge"</tt>.<br/>
    /// The semantics of this method are defined by JSR-220.
    /// </summary>
    /// <param name="entity">a detached instance with state to be copied </param>
    /// <returns> an updated persistent instance </returns>
    T ISession.Merge<T>(T entity)
    {
      return _session.Merge(entity);
    }

    async Task<T> ISession.MergeAsync<T>(T entity, CancellationToken token)
    {
      return await _session.MergeAsync(entity, token);
    }

    /// <summary>
    /// Copy the state of the given object onto the persistent object with the same
    /// identifier. If there is no persistent instance currently associated with
    /// the session, it will be loaded. Return the persistent instance. If the
    /// given instance is unsaved, save a copy of and return it as a newly persistent
    /// instance. The given instance does not become associated with the session.
    /// This operation cascades to associated instances if the association is mapped
    /// with <tt>cascade="merge"</tt>.<br/>
    /// The semantics of this method are defined by JSR-220.
    /// <param name="entityName">Name of the entity.</param>
    /// <param name="entity">a detached instance with state to be copied </param>
    /// <returns> an updated persistent instance </returns>
    /// </summary>
    /// <returns></returns>
    T ISession.Merge<T>(string entityName, T entity)
    {
      return _session.Merge(entityName, entity);
    }

    async Task<T> ISession.MergeAsync<T>(string entityName, T entity, CancellationToken token)
    {
      return await _session.MergeAsync(entityName, entity, token);
    }

    /// <summary>
    /// Make a transient instance persistent. This operation cascades to associated
    /// instances if the association is mapped with <tt>cascade="persist"</tt>.<br/>
    /// The semantics of this method are defined by JSR-220.
    /// </summary>
    /// <param name="obj">a transient instance to be made persistent </param>
    void ISession.Persist(object obj)
    {
      _session.Persist(obj);
    }

    async Task ISession.PersistAsync(object obj, CancellationToken token)
    {
      await _session.PersistAsync(obj);
    }

    /// <summary>
    /// Make a transient instance persistent. This operation cascades to associated
    /// instances if the association is mapped with <tt>cascade="persist"</tt>.<br/>
    /// The semantics of this method are defined by JSR-220.
    /// </summary>
    /// <param name="entityName">Name of the entity.</param>
    /// <param name="obj">a transient instance to be made persistent</param>
    void ISession.Persist(string entityName, object obj)
    {
      _session.Persist(entityName, obj);
    }

    async Task ISession.PersistAsync(string entityName, object obj, CancellationToken token)
    {
      await _session.PersistAsync(entityName, obj);
    }

    /// <summary>
    /// Copy the state of the given object onto the persistent object with the same
    /// identifier. If there is no persistent instance currently associated with
    /// the session, it will be loaded. Return the persistent instance. If the
    /// given instance is unsaved or does not exist in the database, save it and
    /// return it as a newly persistent instance. Otherwise, the given instance
    /// does not become associated with the session.
    /// </summary>
    /// <param name="obj">a transient instance with state to be copied</param>
    /// <returns>an updated persistent instance</returns>
    public async Task SaveOrUpdateAsync(object obj, CancellationToken token)
    {
      await _session.SaveOrUpdateAsync(obj, token);
    }

    /// <summary>
    /// Copy the state of the given object onto the persistent object with the
    /// given identifier. If there is no persistent instance currently associated
    /// with the session, it will be loaded. Return the persistent instance. If
    /// there is no database row with the given identifier, save the given instance
    /// and return it as a newly persistent instance. Otherwise, the given instance
    /// does not become associated with the session.
    /// </summary>
    /// <param name="obj">a persistent or transient instance with state to be copied</param>
    /// <param name="id">the identifier of the instance to copy to</param>
    /// <returns>an updated persistent instance</returns>
    public async Task SaveOrUpdateAsync(string entityName, object obj,  CancellationToken token)
    {
      await _session.SaveOrUpdateAsync(entityName, obj, token);
    }

    /// <summary>
    /// Remove a persistent instance from the datastore. The <b>object</b> argument may be
    /// an instance associated with the receiving <see cref="ISession"/> or a transient
    /// instance with an identifier associated with existing persistent state.
    /// This operation cascades to associated instances if the association is mapped
    /// with <tt>cascade="delete"</tt>.
    /// </summary>
    /// <param name="entityName">The entity name for the instance to be removed. </param>
    /// <param name="obj">the instance to be removed </param>
    void ISession.Delete(string entityName, object obj)
    {
      _session.Delete(entityName, obj);
    }

    async Task ISession.DeleteAsync(string entityName, object obj, CancellationToken token)
    {
      await _session.DeleteAsync(entityName, obj, token);
    }

    async Task ISession.DeleteAsync(object obj, CancellationToken token)
    {
      await _session.DeleteAsync(obj, token);
    }

    /// <summary>
    /// Delete all objects returned by the query.
    /// </summary>
    /// <param name="query">The query string</param>
    /// <returns>Returns the number of objects deleted.</returns>
    int ISession.Delete(string query)
    {
      return _session.Delete(query);
    }

    async Task<int> ISession.DeleteAsync(string query, CancellationToken token)
    {
      return await _session.DeleteAsync(query, token);
    }

    /// <summary>
    /// Delete all objects returned by the query.
    /// </summary>
    /// <param name="query">The query string</param>
    /// <param name="value">A value to be written to a "?" placeholer in the query</param>
    /// <param name="type">The hibernate type of value.</param>
    /// <returns>The number of instances deleted</returns>
    int ISession.Delete(string query, object value, IType type)
    {
      return _session.Delete(query, value, type);
    }

    async Task<int> ISession.DeleteAsync(string query, object value, IType type, CancellationToken token)
    {
      return await _session.DeleteAsync(query, value, type, token);
    }

    /// <summary>
    /// Delete all objects returned by the query.
    /// </summary>
    /// <param name="query">The query string</param>
    /// <param name="values">A list of values to be written to "?" placeholders in the query</param>
    /// <param name="types">A list of Hibernate types of the values</param>
    /// <returns>The number of instances deleted</returns>
    int ISession.Delete(string query, object[] values, IType[] types)
    {
      return _session.Delete(query, values, types);
    }

    async Task<int> ISession.DeleteAsync(string query, object[] values, IType[] types, CancellationToken token)
    {
      return await _session.DeleteAsync(query, values, types, token);
    }

    /// <summary>
    /// Obtain the specified lock level upon the given object.
    /// </summary>
    /// <param name="obj">A persistent instance</param>
    /// <param name="lockMode">The lock level</param>
    void ISession.Lock(object obj, LockMode lockMode)
    {
      _session.Lock(obj, lockMode);
    }

    async Task ISession.LockAsync(object obj, LockMode lockMode, CancellationToken token)
    {
      await _session.LockAsync(obj, lockMode, token);
    }

    /// <summary>
    /// Obtain the specified lock level upon the given object.
    /// </summary>
    /// <param name="entityName">The Entity name.</param>
    /// <param name="obj">a persistent or transient instance </param>
    /// <param name="lockMode">the lock level </param>
    /// <remarks>
    /// This may be used to perform a version check (<see cref="LockMode.Read"/>), to upgrade to a pessimistic
    /// lock (<see cref="LockMode.Upgrade"/>), or to simply reassociate a transient instance
    /// with a session (<see cref="LockMode.None"/>). This operation cascades to associated
    /// instances if the association is mapped with <tt>cascade="lock"</tt>.
    /// </remarks>
    void ISession.Lock(string entityName, object obj, LockMode lockMode)
    {
      _session.Lock(entityName, obj, lockMode);
    }

    async Task ISession.LockAsync(string entityName, object obj, LockMode lockMode, CancellationToken token)
    {
      await _session.LockAsync(entityName, obj, lockMode, token);
    }

    /// <summary>
    /// Re-read the state of the given instance from the underlying database, with
    /// the given <c>LockMode</c>.
    /// </summary>
    /// <remarks>
    /// It is inadvisable to use this to implement long-running sessions that span many
    /// business tasks. This method is, however, useful in certain special circumstances.
    /// </remarks>
    /// <param name="obj">a persistent or transient instance</param>
    /// <param name="lockMode">the lock mode to use</param>
    void ISession.Refresh(object obj, LockMode lockMode)
    {
      _session.Refresh(obj, lockMode);
    }

    async Task ISession.RefreshAsync(object obj, LockMode lockMode, CancellationToken token)
    {
      await _session.RefreshAsync(obj, lockMode, token);
    }

    async Task ISession.RefreshAsync(object obj, CancellationToken token)
    {
      await _session.RefreshAsync(obj, token);
    }

    /// <summary>
    /// Determine the current lock mode of the given object
    /// </summary>
    /// <param name="obj">A persistent instance</param>
    /// <returns>The current lock mode</returns>
    LockMode ISession.GetCurrentLockMode(object obj)
    {
      return _session.GetCurrentLockMode(obj);
    }

    /// <summary>
    /// Begin a unit of work and return the associated <c>ITransaction</c> object.
    /// </summary>
    /// <remarks>
    /// If a new underlying transaction is required, begin the transaction. Otherwise
    /// continue the new work in the context of the existing underlying transaction.
    /// The class of the returned <see cref="ITransaction" /> object is determined by
    /// the property <c>transaction_factory</c>
    /// </remarks>
    /// <returns>A transaction instance</returns>
    ITransaction ISession.BeginTransaction()
    {
      return _session.BeginTransaction();
    }

    /// <summary>
    /// Begin a transaction with the specified <c>isolationLevel</c>
    /// </summary>
    /// <param name="isolationLevel">Isolation level for the new transaction</param>
    /// <returns>A transaction instance having the specified isolation level</returns>
    ITransaction ISession.BeginTransaction(IsolationLevel isolationLevel)
    {
      return _session.BeginTransaction(isolationLevel);
    }

    /// <summary>
    /// Get the current Unit of Work and return the associated <c>ITransaction</c> object.
    /// </summary>
    ITransaction ISession.Transaction => _session.Transaction;

    /// <summary>
    /// Creates a new <c>Criteria</c> for the entity class.
    /// </summary>
    /// <typeparam name="T">The entity class</typeparam>
    /// <returns>An ICriteria object</returns>
    public ICriteria CreateCriteria<T>() where T : class
    {
      return new CriteriaProxy(_session.CreateCriteria<T>(), Interceptor);
    }

    /// <summary>
    /// Creates a new <c>Criteria</c> for the entity class with a specific alias
    /// </summary>
    /// <typeparam name="T">The entity class</typeparam>
    /// <param name="alias">The alias of the entity</param>
    /// <returns>An ICriteria object</returns>
    public ICriteria CreateCriteria<T>(string alias) where T : class
    {
      return new CriteriaProxy(_session.CreateCriteria<T>(alias), Interceptor);
    }

    /// <summary>
    /// Create a criteria query for the specified class
    /// </summary>
    public ICriteria CreateCriteria(Type persistentClass)
    {
      var impl = _session.CreateCriteria(persistentClass.FullName);
      return typeof(AuditedObject).IsAssignableFrom(persistentClass) ? new CriteriaProxy(impl, Interceptor) : impl;
    }

    /// <summary>
    /// Creates a new <c>Criteria</c> for the entity class with a specific alias
    /// </summary>
    /// <param name="persistentClass">The class to Query</param>
    /// <param name="alias">The alias of the entity</param>
    /// <returns>An ICriteria object</returns>
    public ICriteria CreateCriteria(Type persistentClass, string alias)
    {
      var impl = _session.CreateCriteria(persistentClass.FullName, alias);
      return typeof(AuditedObject).IsAssignableFrom(persistentClass) ? new CriteriaProxy(impl, Interceptor) : impl;
    }

    /// <summary>
    /// Create a new <c>Criteria</c> instance, for the given entity name.
    /// </summary>
    /// <param name="entityName">The name of the entity to Query</param>
    /// <returns>An ICriteria object</returns>
    public ICriteria CreateCriteria(string entityName)
    {
      return new CriteriaProxy(_session.CreateCriteria(entityName), Interceptor);
    }

    /// <summary>
    /// Create a new <c>Criteria</c> instance, for the given entity name,
    /// with the given alias.
    /// </summary>
    /// <param name="entityName">The name of the entity to Query</param>
    /// <param name="alias">The alias of the entity</param>
    /// <returns>An ICriteria object</returns>
    public ICriteria CreateCriteria(string entityName, string alias)
    {
      return new CriteriaProxy(_session.CreateCriteria(entityName, alias), Interceptor);
    }

    /// <exclude/>
    IQueryOver<T, T> ISession.QueryOver<T>()
    {
      throw new NotSupportedException();
    }

    /// <exclude/>
    IQueryOver<T, T> ISession.QueryOver<T>(Expression<Func<T>> alias)
    {
      throw new NotSupportedException();
    }

    /// <exclude/>
    IQueryOver<T, T> ISession.QueryOver<T>(string entityName)
    {
      throw new NotSupportedException();
    }

    /// <exclude/>
    IQueryOver<T, T> ISession.QueryOver<T>(string entityName, Expression<Func<T>> alias)
    {
      throw new NotSupportedException();
    }

    /// <summary>
    /// Create a new instance of <c>Query</c> for the given query string
    /// </summary>
    /// <param name="queryString">A hibernate query string</param>
    /// <returns>The query</returns>
    public IQuery CreateQuery(string queryString)
    {
      return new QueryProxy(_session.CreateQuery(queryString), Interceptor);
    }

    /// <summary>
    /// Create a new instance of <c>Query</c> for the given collection and filter string
    /// </summary>
    /// <param name="collection">A persistent collection</param>
    /// <param name="queryString">A hibernate query</param>
    /// <returns>A query</returns>
    IQuery ISession.CreateFilter(object collection, string queryString)
    {
      return _session.CreateFilter(collection, queryString);
    }

    async Task<IQuery> ISession.CreateFilterAsync(object collection, string queryString, CancellationToken token)
    {
      return await _session.CreateFilterAsync(collection, queryString, token);
    }

    /// <summary>
    /// Obtain an instance of <see cref="IQuery" /> for a named query string defined in the
    /// mapping file.
    /// </summary>
    /// <param name="queryName">The name of a query defined externally.</param>
    /// <returns>An <see cref="IQuery"/> from a named query string.</returns>
    /// <remarks>
    /// The query can be either in <c>HQL</c> or <c>SQL</c> format.
    /// </remarks>
    IQuery ISession.GetNamedQuery(string queryName)
    {
      return ((ISession)_session).GetNamedQuery(queryName);
    }

    /// <summary>
    /// Create a new instance of <see cref="ISQLQuery" /> for the given SQL query string.
    /// </summary>
    /// <param name="queryString">a query expressed in SQL</param>
    /// <returns>An <see cref="ISQLQuery"/> from the SQL string</returns>
    public ISQLQuery CreateSQLQuery(string queryString)
    {
      return _session.CreateSQLQuery(queryString);
    }

    /// <summary>
    /// Completely clear the session. Evict all loaded instances and cancel all pending
    /// saves, updates and deletions. Do not close open enumerables or instances of
    /// <c>ScrollableResults</c>.
    /// </summary>
    void ISession.Clear()
    {
      _session.Clear();
    }

    /// <summary>
    /// Return the persistent instance of the given entity class with the given identifier, or null
    /// if there is no such persistent instance. Obtain the specified lock mode if the instance
    /// exists.
    /// </summary>
    /// <param name="clazz">a persistent class</param>
    /// <param name="id">an identifier</param>
    /// <param name="lockMode">the lock mode</param>
    /// <returns>a persistent instance or null</returns>
    public object Get(Type clazz, object id, LockMode lockMode)
    {
      var result = _session.Get(clazz, id, lockMode);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Return the persistent instance of the given named entity with the given identifier,
    /// or null if there is no such persistent instance. (If the instance, or a proxy for the
    /// instance, is already associated with the session, return that instance or proxy.)
    /// </summary>
    /// <param name="entityName">the entity name </param>
    /// <param name="id">an identifier </param>
    /// <returns> a persistent instance or null </returns>
    public object Get(string entityName, object id)
    {
      var result = _session.Get(entityName, id);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Strongly-typed version of <see cref="Get(System.Type, object, LockMode)" />
    /// </summary>
    T ISession.Get<T>(object id, LockMode lockMode)
    {
      var result = _session.Get<T>(id, lockMode);
      _interceptor.RollbackEvents();
      return result;
    }

    async Task<T> ISession.GetAsync<T>(object id, LockMode lockModel, CancellationToken token)
    {
      var result = await _session.GetAsync<T>(id, lockModel);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Gets the instance of the object with the specified object id and type.
    /// The object will only be loaded from the database if it is not already in this session.
    /// </summary>
    /// <param name="theType">Type of object to find</param>
    /// <param name="id">Object id of the object to find</param>
    /// <returns>null if object is not found</returns>
    public object Get(Type theType, object id)
    {
      var result = _session.Get(theType, id);
      _interceptor.RollbackEvents();
      return result;
    }

    async Task<object> ISession.GetAsync(Type type, object id,  LockMode lockModel, CancellationToken token)
    {
      var result = await _session.GetAsync(type, id, lockModel, token);
      _interceptor.RollbackEvents();
      return result;
    }

    async Task<object> ISession.GetAsync(Type type, object id, CancellationToken token)
    {
      var result = await _session.GetAsync(type, id, token);
      _interceptor.RollbackEvents();
      return result;
    }

    async Task<object> ISession.GetAsync(string entityName, object id, CancellationToken token)
    {
      var result = await _session.GetAsync(entityName, id, token);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Return the entity name for a persistent entity
    /// </summary>
    /// <param name="obj">a persistent entity</param>
    /// <returns> the entity name </returns>
    string ISession.GetEntityName(object obj)
    {
      return _session.GetEntityName(obj);
    }

    async Task<string> ISession.GetEntityNameAsync(object obj, CancellationToken token)
    {
      return await _session.GetEntityNameAsync(obj, token);
    }

    /// <summary>
    /// Enable the named filter for this current session.
    /// </summary>
    /// <param name="filterName">The name of the filter to be enabled.</param>
    /// <returns>The Filter instance representing the enabled filter.</returns>
    IFilter ISession.EnableFilter(string filterName)
    {
      return _session.EnableFilter(filterName);
    }

    /// <summary>
    /// Retrieve a currently enabled filter by name.
    /// </summary>
    /// <param name="filterName">The name of the filter to be retrieved.</param>
    /// <returns>The Filter instance representing the enabled filter.</returns>
    IFilter ISession.GetEnabledFilter(string filterName)
    {
      return _session.GetEnabledFilter(filterName);
    }

    /// <summary>
    /// Disable the named filter for the current session.
    /// </summary>
    /// <param name="filterName">The name of the filter to be disabled.</param>
    void ISession.DisableFilter(string filterName)
    {
      _session.DisableFilter(filterName);
    }

    /// <summary>
    /// Create a multi query, a query that can send several
    /// queries to the server, and return all their results in a single
    /// call.
    /// </summary>
    /// <returns>
    /// An <see cref="IMultiQuery"/> that can return
    /// a list of all the results of all the queries.
    /// Note that each query result is itself usually a list.
    /// </returns>
    public IMultiQuery CreateMultiQuery()
    {
      return _session.CreateMultiQuery();
    }

    /// <summary>
    /// Sets the batch size of the session
    /// </summary>
    /// <param name="batchSize"></param>
    /// <returns></returns>
    public ISession SetBatchSize(int batchSize)
    {
      return _session.SetBatchSize(batchSize);
    }

    /// <summary>
    /// Gets the session implementation.
    /// </summary>
    /// <remarks>
    /// This method is provided in order to get the <b>NHibernate</b> implementation of the session from wrapper implementions.
    /// Implementors of the <seealso cref="ISession"/> interface should return the NHibernate implementation of this method.
    /// </remarks>
    /// <returns>
    /// An NHibernate implementation of the <seealso cref="ISessionImplementor"/> interface
    /// </returns>
    public ISessionImplementor GetSessionImplementation()
    {
      return _session.GetSessionImplementation();
    }

    /// <summary> Get the statistics for this session.</summary>
    public ISessionStatistics Statistics => _session.Statistics;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public object Merge(object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException(nameof(obj));
      }

      var po = (PersistentObject)obj;

      var cm = ClassCache.Find(obj);
      if (cm == null || cm.IsComponent)
      {
        throw new ArgumentException($"No Entity metadata for [{obj.GetType()}]");
      }

      if (cm.CascadeList.Any(c => c.Cascade != "none"))
      {
        throw new ArgumentException($"Entity [{cm.Name}] does not currently support Merge operations");
      }

      if (po.ObjectId == 0)
      {
        throw new ArgumentException($"Attempt to Merge with unsaved instance of type [{cm.Name}]");
      }

      var existingPo = (PersistentObject)Get(cm.Type, po.ObjectId);
      if (existingPo == null)
      {
        throw new ArgumentException($"PersistentObject [{po.ObjectId}] was not found");
      }

      var sessionLock = FindLock(existingPo);
      if (sessionLock == null)
      {
        existingPo.RequestUpdate();
      }
      else
      {
        throw new InvalidOperationException($"Attempt to lock entity [{existingPo.ObjectId}] that is already locked for [{sessionLock.LockType}]");
      }

      return _session.Merge(obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IMultiCriteria CreateMultiCriteria()
    {
      return _session.CreateMultiCriteria();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="theType"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public object Load(Type theType, object id)
    {
      var result = _session.Load(theType, id);
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task<object> LoadAsync(Type theType, object id, CancellationToken token)
    {
      var result = await _session.LoadAsync(theType, id);
      _interceptor.RollbackEvents();
      return result;
    }

    public async Task LoadAsync(object obj, object id, CancellationToken token)
    {
      await _session.LoadAsync(obj, id);
      _interceptor.RollbackEvents();
    }

    /// <summary>
    /// 
    /// </summary>
    public void Refresh(object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException(nameof(obj));
      }

      var po = obj as PersistentObject;
      if (po != null)
      {}

#if DEBUG
      var cm = ClassCache.Find(obj);
      if (cm.IsChildEntity)
      {
        throw new ArgumentException("Cannot call Save on ChildEntity [" + cm.Name + "]");
      }
#endif

      if (Locks.Any())
      {
        // In order to relax this restriction we need to make sure that existing entity locks
        // are updated or reacquired as a result of the refresh.  This likely requires hooking
        // into an NHibernate event.
        throw new DatabaseException("Cannot refresh entity if session has active locks!");
      }

      _session.Refresh(obj);
    }

    /// <summary>
    /// Adds the object to this session so that it will be saved to the database when CommitTransaction is called
    /// </summary>
    /// <returns></returns>
    public object Save(object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException(nameof(obj));
      }

      var po = obj as PersistentObject;

      if (po != null)
      {
        return ((IEditableEntityContext)this).Save(po);
      }

      return _session.Save(obj);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public void SaveOrUpdate(object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException(nameof(obj));
      }

#if DEBUG
      var cm = ClassCache.Find(obj);
      if (cm.IsChildEntity)
      {
        throw new ArgumentException("Cannot call SaveOrUpdate on ChildEntity [" + cm.Name + "]");
      }
#endif

      var po = (PersistentObject)obj;

      var existingLock = FindLock(po);
      if (existingLock == null)
      {}
      else if (existingLock.LockType == LockType.Delete)
      {
        // Raise error if object is locked for delete, in all other cases, looks ok.
        throw new InvalidOperationException(
          $"PersistentObject [{po.ObjectId}] is already locked for {existingLock.LockType}. SaveUpdate lock should not be requested.");
      }

      _session.SaveOrUpdate(obj);
    }

    /// <summary>
    /// Marks the object in this session to be deleted from the database when CommitTransaction is called
    /// </summary>
    public void Delete(object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException(nameof(obj));
      }

      var po = obj as PersistentObject;
      if (po != null && po.ObjectId != 0)
      {
        // Request Delete lock on this entity and its children.
        // The Delete itself will be cascaded by NHibernate, all we do in the
        // OnDelete handler is validate that the Delete lock exists.
        InternalRequestLock(po, LockType.Delete, null);
      }

      _session.Delete(obj);
    }

    T ISession.Get<T>(object id)
    {
      var result = _session.Get<T>(id);
      _interceptor.RollbackEvents();
      return result;
    }

    async Task<T> ISession.GetAsync<T>(object id, CancellationToken token)
    {
      var result = await _session.GetAsync<T>(id);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Starts a new Session with the given entity mode in effect. This secondary
    /// Session inherits the connection, transaction, and other context
    ///	information from the primary Session. It doesn't need to be flushed
    /// or closed by the developer.
    /// </summary>
    /// <param name="entityMode">The entity mode to use for the new session.</param>
    /// <returns>The new session</returns>
    ISession ISession.GetSession(EntityMode entityMode)
    {
      return _session.GetSession(entityMode);
    }

    void ISession.JoinTransaction()
    {
      _session.JoinTransaction();
    }

    ISharedSessionBuilder ISession.SessionWithOptions()
    {
      return _session.SessionWithOptions();
    }

    IOrderedQueryable<T> IQueryableEntityContext.Query<T>()
    {
      return _session.Query<T>() as IOrderedQueryable<T>;
    }

    #endregion

    #region Other Members

    /// <summary>
    /// 
    /// </summary>
    public AuditInterceptor Interceptor => _interceptor;

    /// <summary>
    ///   Gets the unique <see cref="Guid"/> associated with the active transaction
    /// </summary>
    public Guid TransactionId => _transactionId;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public T Get<T>(Type type, long id) where T : PersistentObject
    {
      var result = (T)_session.Get(type, id);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Close()
    {
      Dispose();
    }

    /// <summary>
    /// Perform an HQL query and return a list of objects
    /// </summary>
    /// <param name="query">HQL query to perform</param>
    /// <returns>List of objects. If not objects found an empty list is returned</returns>
    public IList Find(string query)
    {
      var result = _session.CreateQuery(query).List();
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Perform an HQL query and return a list of objects. Allows one parameter for the HQL string.
    /// </summary>
    /// <param name="query">HQL query to perform</param>
    /// <param name="value">object to format within the HQL string</param>
    /// <param name="type">type of the value parameter</param>
    /// <returns>List of objects. If not objects found an empty list is returned</returns>
    public IList Find(string query, object value, IType type)
    {
      var result = _session.CreateQuery(query).SetParameter(0, value, type).List();
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Perform an HQL query and return a list of objects. Allows multiple parameters for the HQL string.
    /// </summary>
    /// <param name="query">HQL query to perform</param>
    /// <param name="values">objects to format within the HQL string</param>
    /// <param name="types">types of the value parameters</param>
    /// <returns>List of objects. If not objects found an empty list is returned</returns>
    public IList Find(string query, object[] values, IType[] types)
    {
      if (values.Length != types.Length)
      {
        throw new ArgumentException("Array length mismatch");
      }
      var q = _session.CreateQuery(query);
      for (int i = 0; i < values.Length; i++)
      {
        q.SetParameter(i, values[i], types[i]);
      }
      var result = q.List();
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Create a new instance of <c>Query</c> for the given query string
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="returnAlias"></param>
    /// <param name="returnClass"></param>
    public IQuery CreateSqlQuery(string sql, string returnAlias, Type returnClass)
    {
      return _session.CreateSQLQuery(sql).AddEntity(returnAlias, returnClass);
    }

    /// <summary>
    /// Create a new instance of <see cref="ISQLQuery" /> for the given SQL query string.
    /// </summary>
    /// <param name="queryString"></param>
    /// <returns></returns>
    /// <exclude />
    public ISQLQuery CreateSqlQuery(string queryString)
    {
      return _session.CreateSQLQuery(queryString);
    }

    /// <summary>
    /// Gets the instance of the object with the specified object id and type.
    /// The object will only be loaded from the database if it is not already in this session.
    /// </summary>
    /// <typeparam name="T">Type of object to find</typeparam>
    /// <param name="id">Object id of the object to find</param>
    /// <returns>null if object is not found</returns>
    /// <exclude />
    public T Get<T>(object id) where T : PersistentObject
    {
      var result = (T)_session.Get(typeof(T), id);
      _interceptor.RollbackEvents();
      return result;
    }

    /// <summary>
    /// Bulk insert records into specified DataTable
    /// </summary>
    /// <param name="dataTable"></param>
    /// <remarks>
    ///  This function bypasses the ORM layer.
    /// </remarks>
    public void BulkInsert(DataTable dataTable)
    {
      if (dataTable == null)
      {
        throw new ArgumentNullException(nameof(dataTable));
      }

      if (dataTable.Rows.Count == 0)
      {
        return;
      }

      var conn = (SqlConnection)_session.Connection;

      if (_session == null)
      {
        throw new DatabaseException("BulkInsert requires ISession");
      }

      var tran = _session.Transaction;
      //using (var tran = _session.BeginTransaction())    // (SqlTransaction)_session.Transaction.GetNativeTransaction();
      {
        if (tran == null)
        {
          throw new DatabaseException("BulkInsert requires ITransaction");
        }

        using (var cmd = conn.CreateCommand())
        {
          tran.Enlist(cmd);

          using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, (SqlTransaction)cmd.Transaction) { DestinationTableName = dataTable.TableName, BulkCopyTimeout = SessionFactory.CommandTimeout })
          {
            bulkCopy.WriteToServer(dataTable);
          }

        }

      }
    }

    /// <summary>
    ///  Execute bulk update or delete against the database
    /// </summary>
    /// <remarks>
    ///  This function operates directly at the SQL level.
    /// </remarks>
    public int BulkUpdate(string sql)
    {
      return BulkUpdate(sql, null);
    }

    /// <summary>
    ///  Execute bulk update or delete against the database
    /// </summary>
    /// <remarks>
    ///  This function operates directly at the SQL level.
    /// </remarks>
    public int BulkUpdate(string sql, SqlParameter[] parameters)
    {
      ISession session = _session;
      using (var cmd = session.Connection.CreateCommand())
      {
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;
        if (parameters != null)
        {
          foreach (var param in parameters)
            cmd.Parameters.Add(param);
        }

        cmd.CommandTimeout = SessionFactory.CommandTimeout;

        // Required by ADO.NET
        session.Transaction?.Enlist(cmd);

        SqlLogger.Debug(sql);

        return cmd.ExecuteNonQuery();
      }
    }

    /// <summary>
    ///  Execute direct SQL
    /// </summary>
    /// <remarks>
    ///  This function operates directly at the SQL level.
    /// </remarks>
    /// <returns></returns>
    public IDataReader ExecuteReader(string sql)
    {
      return ExecuteSqlCommand(CreateDbCommand(sql));
    }

    /// <summary>
    /// Execute direct SQL
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameterName"></param>
    /// <param name="parameterValue"></param>
    /// <returns></returns>
    public IDataReader ExecuteReader(string sql, string parameterName, object parameterValue)
    {
      return ExecuteReader(sql, new[] {new DbDataParameter(parameterName, parameterValue)});
    }

    /// <summary>
    /// Execute direct SQL
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public IDataReader ExecuteReader(string sql, IList<DbDataParameter> parameters)
    {
      return ExecuteSqlCommand(CreateDbCommand(sql, parameters));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cmd"></param>
    /// <returns></returns>
    public IDataReader ExecuteSqlCommand(IDbCommand cmd)
    {
      ISession session = _session;
      IDbConnection conn = session.Connection;
      ITransaction tran = session.Transaction;
      if (tran == null)
      {
        throw new DatabaseException("No active Transaction for Session");
      }

      cmd.Connection = conn;
      cmd.CommandTimeout = SessionFactory.CommandTimeout;
      tran.Enlist(cmd as DbCommand);

      return cmd.ExecuteReader();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    /// <exclude />
    public IDbCommand CreateDbCommand(string sql)
    {
      ISession session = _session;
      IDbCommand cmd = session.Connection.CreateCommand();
      cmd.CommandType = CommandType.Text;
      cmd.CommandText = sql;
      return cmd;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public IDbCommand CreateDbCommand(string sql, IList<DbDataParameter> parameters)
    {
      ISession session = _session;
      IDbCommand cmd = session.Connection.CreateCommand();
      cmd.CommandText = sql;
      cmd.CommandType = CommandType.Text;
      for (int i = 0; i < parameters.Count; i++)
        parameters[i].AddToCommand(cmd, i, session);
      return cmd;
    }

    /// <summary>
    /// This is a direct ADO.NET query and is used in GUI applications where we need the latest
    /// data but cannot call Session.Clear. For example a dropdown list of available strategies.
    /// </summary>
    /// <param name="sql"></param>
    /// <returns></returns>
    /// <exclude />
    public DataTable DBQuery(string sql)
    {
      throw new NotImplementedException("NHibernateEntityContext.DBQuery");
      //IDbConnection conn = _session.Connection;
      //IDbCommand cmd = conn.CreateCommand();
      //cmd.CommandTimeout = SessionFactory.CommandTimeout;
      //cmd.CommandText = sql;

      //ITransaction tran = _session.Transaction;
      //if (tran.IsActive)
      //{
      //  // Required by ADO.NET
      //  tran.Enlist(cmd);
      //}

      //SqlLogger.Debug(sql);

      //var table = new DataTable();
      //DbProviderFactory factory = DbProviderFactories.GetFactory(SessionFactory.GetProvider());
      //DbDataAdapter da = factory.CreateDataAdapter();
      //Debug.Assert(da != null);
      //da.SelectCommand = (DbCommand)cmd;
      //da.Fill(table);

      //return table;
    }

    /// <summary>
    /// Obtains a LINQ query provider for NHibernate
    /// </summary>
    /// <typeparam name="T">Entity type to query on</typeparam>
    /// <returns>An NHibernate-queryable provider</returns>
    /// <exclude />
    public IQueryable<T> Linq<T>()
    {
      return Query<T>();
    }

    /// <summary>
    /// Upserts an item matching the specified criteria.
    /// </summary>
    /// <typeparam name="T">The type of the item</typeparam>
    /// <param name="criteria">The criteria.</param>
    /// <returns>An <see cref = "Upserter&lt;T&gt;" /> for handling the upsert.</returns>
    public Upserter<T> Upsert<T>(Expression<Func<T, bool>> criteria) where T : PersistentObject
    {
      return new Upserter<T>(criteria, new DirectRepository<T>((NHibernateEntityContext)EntityContext.Current));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public bool IsSaved(PersistentObject po)
    {
      return po.ObjectId != 0;
    }

    /// <summary>
    /// Returns true if the specified entity has been rolledback in this Session
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    public bool IsRolledBack(long objectId)
    {
      return Interceptor.RolledBack.Contains(objectId);
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool CheckPermission(EntityLock @lock, out string errorMsg)
    {
      if (@lock == null)
      {
        throw new ArgumentNullException(nameof(@lock));
      }

      var cm = @lock.Entity;

      if (cm.IsChildEntity)
      {
        throw new InvalidOperationException($"Cannot check permission for ChildEntity [{cm.Name}]");
      }

      var userRole = EntityContextFactory.UserRole;

      if (userRole.ReadOnly)
      {
        errorMsg = "UserRole [" + userRole.Name + "] is ReadOnly";
        return false;
      }

      if (userRole.Administrator)
      {
        errorMsg = null;
        return true;
      }

      if (ReadWriteMode == ReadWriteMode.Workflow)
      {
        errorMsg = null;
        return true;
      }

      if (@lock.OldState != null)
      {
        if (!SecurityPolicy.CheckEntityPolicy(@lock.OldState, @lock.LockType))
        {
          errorMsg = "Permission denied";
          return false;
        }
      }

      if (@lock.NewState != null)
      {
        if (!SecurityPolicy.CheckEntityPolicy(@lock.NewState, @lock.LockType))
        {
          errorMsg = "Permission denied";
          return false;
        }
      }

      errorMsg = null;
      return true;
    }

    #endregion

    #region Helper Methods

    internal void AfterTransactionBegin()
    {
      _transactionId = Guid.NewGuid();
    }

    /// <summary>
    ///
    /// </summary>
    internal void AfterTransactionCompletion()
    {
      FreeAllEntityLocks();

      _comment = null;

      _transactionId = Guid.Empty;

      TransientContext.Clear();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="table"></param>
    /// <param name="deltaNode"></param>
    public void CreateAuditLogRow(DataTable table, DeltaNode deltaNode)
    {
      if (deltaNode.Obj == null)
      {
        // Will get here if Save() and Delete() called on the entity in the same Session.
        return;
      }

      var classMeta = deltaNode.ClassMeta;

      if (classMeta.AuditPolicy == AuditPolicy.None)
      {
        return;
      }

      DateTime validFrom;
      byte[] bytes = null;
      if (classMeta.AuditPolicy == AuditPolicy.History)
      {
        using (var stream = new MemoryStream())
        {
          using (var writer = new BinaryEntityWriter(stream))
          {
            writer.WriteEntity(deltaNode.NewState ?? deltaNode.OldState);
          }
          bytes = stream.ToArray();
        }

        // In case of delete
        if (deltaNode.NewState == null)
        {
          if (HistorizationPolicy != HistorizationPolicy.None)
          {
            validFrom = AsOf.Date;
          }
          else
          {
            var ao = (AuditedObject)deltaNode.OldState;
            validFrom = ao.ValidFrom;
          }
        }
        else
        {
          var ao = (AuditedObject)deltaNode.NewState;
          validFrom = ao.ValidFrom;
        }
      }
      else
      {
        validFrom = DateTime.MinValue;
      }

      int itemAction;
      switch (deltaNode.LockType)
      {
        case LockType.Insert:
          itemAction = Convert.ToInt32(ItemAction.Added);
          break;
        case LockType.Delete:
          itemAction = Convert.ToInt32(ItemAction.Removed);
          break;
        default:
          itemAction = Convert.ToInt32(ItemAction.Changed);
          break;
      }

      var row = table.NewRow();
      row["Tid"] = 0;
      row["ObjectId"] = deltaNode.ObjectId;
      row["RootObjectId"] = deltaNode.RootObjectId;
      row["ParentObjectId"] = deltaNode.ParentObjectId;
      row["EntityId"] = classMeta.EntityId;
      row["ValidFrom"] = validFrom == DateTime.MinValue ? new DateTime(1753, 1, 1) : validFrom;
      row["Action"] = itemAction;
      row["ObjectDelta"] = bytes;
      row["IsArchived"] = 0;
      table.Rows.Add(row);
    }

    internal int PersistCommitLog(ITransaction tran)
    {
      var cmd = _session.Connection.CreateCommand();
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.CommandText = "InsertCommitLog";
      var userParam = new SqlParameter("@userId", SqlDbType.BigInt);
      var commentParam = new SqlParameter("@comment", SqlDbType.NVarChar, 140);
      var transactionIdParam = new SqlParameter("@transactionId", SqlDbType.UniqueIdentifier);
      var resultParam = new SqlParameter("@result", SqlDbType.Int) {Direction = ParameterDirection.ReturnValue};

      cmd.Parameters.Add(userParam);
      cmd.Parameters.Add(commentParam);
      cmd.Parameters.Add(transactionIdParam);
      cmd.Parameters.Add(resultParam);

      userParam.Value = EntityContextFactory.UserId;
      commentParam.Value = _comment ?? (object)DBNull.Value;
      transactionIdParam.Value = _transactionId;

      tran.Enlist(cmd);

      cmd.ExecuteNonQuery();
      return Convert.ToInt32(resultParam.Value);
    }

    #endregion

    #region IDisposable Implementation

    private bool _isDisposed;

    public override void Dispose()
    {
      if (!_isDisposed)
      {
        _isDisposed = true;
        if (_session == null) return;
        _session.Close();
        _session = null;
      }
    }

    #endregion

    #region History

    private long ReadMode { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public PersistentObject GetDeletedEntity(long id)
    {
      PersistentObject po;

      if (ReadMode == id)
      {
        // Return an empty entity
        var cm = ClassCache.Find(id);
        po = (PersistentObject)cm.CreateInstance();
      }
      else
      {
        // Get the last AuditLog for this date
        var auditLog = GetAuditLog(id);
        if (auditLog == null)
        {
          po = null;
        }
        else
        {
          using (var stream = new MemoryStream(auditLog.ObjectDelta))
          using (var reader = new BinaryEntityReader(stream, new EntityContextLoaderAdaptor(this)))
          {
            try
            {
              ReadMode = auditLog.ObjectId;
              po = reader.ReadEntity();
            }
            finally
            {
              ReadMode = 0;
            }
          }
        }
      }

      return po;
    }

    private AuditLog GetAuditLog(long id)
    {
      var asOf = AsOf.Date;

      const string sql = @"SELECT TOP 1 Tid,ObjectId,RootObjectId,ParentObjectId,EntityId,ValidFrom,Action,ObjectDelta,IsArchived FROM AuditLog WHERE ObjectId = @p0 AND (ValidFrom <= @p1 OR Action IN (0,1)) ORDER BY ValidFROM,Tid DESC";

      var parameters = new List<DbDataParameter>
      {
        new DbDataParameter("p0", id),
        new DbDataParameter("p1", asOf)
      };

      using (IDataReader reader = ExecuteReader(sql, parameters))
      {
        return reader.Read() ? EntityHistoryCache.ReadAuditLog(reader) : null;
      }
    }

    #endregion
  }
}