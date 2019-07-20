// 
// Copyright (c) WebMathTraining Inc 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using log4net;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// Abstract base class for <see cref="EntityHistoryTidContext"/> and <see cref="EntityHistoryValidFromContext"/>
  /// </summary>
  public abstract class EntityHistoryContext : ILoadableEntityContext, IQueryableEntityContext
  {
    #region Data

    private static readonly ILog Logger = LogManager.GetLogger(typeof(EntityHistoryContext));
    private readonly HashSet<long> _loadedEntityObjectIds;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eagerFetch"></param>
    protected EntityHistoryContext(bool eagerFetch)
    {
      EagerFetch = eagerFetch;
      EntityMap = new Dictionary<long, PersistentObject>();
      AuditLogMap = new Dictionary<int, IDictionary<long, AuditLog>>();
      EntityListMap = new Dictionary<int, IList<PersistentObject>>();
      Adaptor = new EntityContextLoaderAdaptor(this);
      IsOpen = true;
      _loadedEntityObjectIds = new HashSet<long>();
    }

    /// <summary>
    /// 
    /// </summary>
    protected EntityHistoryContext()
      : this(false)
    {
    }

    /// <summary>
    /// Initialize new instance with the specified <see cref="AuditLog">audit logs</see>
    /// </summary>
    /// <remarks>
    /// <para>When initialized this way, EagerFetch is disabled, therefore it will not be possible to use LINQ queries with this context.</para>
    /// </remarks>
    protected EntityHistoryContext(ICollection<AuditLog> auditLogs) : this(false)
    {
      if (auditLogs == null)
      {
        throw new ArgumentNullException("auditLogs");
      }

      foreach (var auditLog in auditLogs)
      {
        Add(auditLog);
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// If true, then when performaing LINQ queries pre-fetch any referenced entities to avoid having to open other connections when navigating references.
    /// </summary>
    /// <remarks>
    /// <para>Only applies to entities loaded via the Query{T} method.</para>
    /// </remarks>
    public bool ResolveAll { get; set; }

    private bool EagerFetch { get; set; }
    private IDictionary<long, PersistentObject> EntityMap { get; set; }
    private IDictionary<int, IDictionary<long, AuditLog>> AuditLogMap { get; set; }
    private IDictionary<int, IList<PersistentObject>> EntityListMap { get; set; }
    private IEntityContextAdaptor Adaptor { get; set; }

    #endregion

    #region IEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    public bool IsOpen { get; private set; }

    private long ReadMode { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public PersistentObject Get(long id)
    {
      PersistentObject po;
      if (EntityMap.TryGetValue(id, out po))
        return po;

      if (ReadMode == id)
      {
        // Return an empty entity
        var cm = ClassCache.Find(id);
        po = (PersistentObject)cm.CreateInstance();
        po.ObjectId = id;
        AddToCache(po);
      }
      else
      {
        // Get the version valid on ValidFrom
        var auditLog = GetAuditLog(id);
        po = auditLog == null ? AddToCache(id) : AddToCache(auditLog);
      }

      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public ObjectRef GetObjectRef(long id)
    {
      return new ObjectRef(id, this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<PersistentObject> GetEnumerator()
    {
      return EntityMap.Values.GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool Contains(long id)
    {
      // Does not return entities loaded using ILoadableEntityContext.Load()
      IDictionary<long, AuditLog> map;
      var entityId = EntityHelper.GetEntityIdFromObjectId(id);
      return AuditLogMap.TryGetValue(entityId, out map) && map.ContainsKey(id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public IEnumerable<long> GetObjectIds(int entityId)
    {
      // Does not return entities loaded using ILoadableEntityContext.Load()
      IDictionary<long, AuditLog> map;
      return AuditLogMap.TryGetValue(entityId, out map) ? map.Keys : new long[0];
    }

    #endregion

    #region ILoadableEntityContext Members

    void ILoadableEntityContext.Load(PersistentObject po)
    {
      EntityMap[po.ObjectId] = po;
      _loadedEntityObjectIds.Add(po.ObjectId);
    }

    #endregion

    #region IQueryableEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IOrderedQueryable<T> Query<T>()
    {
      if (!EagerFetch)
      {
        throw new InvalidOperationException("QueryMode not supported for this instance!");
      }
      var classMeta = ClassCache.Find(typeof(T));
      if (classMeta == null)
      {
        throw new DatabaseException("Type [" + typeof(T).Name + "] not found in ClassCache!");
      }
      if (!classMeta.IsEntity)
      {
        throw new DatabaseException("Type [" + typeof(T).Name + "] is not an Entity!");
      }
      if (!classMeta.IsRootEntity)
      {
        throw new DatabaseException("Type [" + typeof(T).Name + "] is not a RootEntity!");
      }
      var lazyConn = new Lazy<RawConnection>(() => new RawConnection());
      try
      {
        return (IOrderedQueryable<T>)InternalQuery(lazyConn, classMeta).Cast<T>().AsQueryable();
      }
      finally
      {
        if (lazyConn.IsValueCreated)
          lazyConn.Value.Dispose();
      }
    }

    /// <summary>
    /// Used by DataImporter
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="keyList"></param>
    /// <returns></returns>
    public PersistentObject FindByKey(ClassMeta cm, IList<object> keyList)
    {
      throw new NotImplementedException();
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
      IsOpen = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool IsDisposed()
    {
      return !IsOpen;
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    protected abstract AuditLog LoadAuditLog(long objectId);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    protected abstract IEnumerable<AuditLog> LoadAuditLogsForEntityId(IDbConnection conn, int entityId);
 
    /// <summary>
    /// Implemented in the base class because it is called by each derived class constructor
    /// </summary>
    protected IEnumerable<AuditLog> LoadAuditLogsForRootObjectIds(IList<long> rootObjectIds, string sql, object p0)
    {
      var auditLogs = new List<AuditLog>();

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        var tran = (SqlTransaction)conn.BeginTransaction();
        CreateObjectIdJoinTable(conn.Connection, tran, rootObjectIds);
        try
        {
          cmd.CommandTimeout = 0;
          cmd.Transaction = tran;
          cmd.CommandText = sql;
          AddParameter(cmd, "p0", p0);
          using (var reader = cmd.ExecuteReader())
          {
            while (reader.Read())
              auditLogs.Add(ReadAuditLog(reader));
          }
        }
        finally
        {
          DropObjectIdJoinTable(conn, tran);
          tran.Rollback();
        }
      }

      return auditLogs;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    public void Add(AuditLog auditLog)
    {
      int entityId = EntityHelper.GetEntityIdFromObjectId(auditLog.ObjectId);

      IDictionary<long, AuditLog> map;
      if (!AuditLogMap.TryGetValue(entityId, out map))
      {
        map = new Dictionary<long, AuditLog>();
        AuditLogMap[entityId] = map;
      }

      AuditLog existing;
      if (map.TryGetValue(auditLog.ObjectId, out existing))
      {
        if (auditLog.Tid == existing.Tid)
        {
          Logger.InfoFormat("Attempt to add existing AuditLog with Tid={0}, EntityId={1}, ObjectId={2}", auditLog.Tid, entityId, auditLog.ObjectId);
        }
        else
        {
          throw new DatabaseException(string.Format(
            "Attempt to add conflicting AuditLog with [{0}]: {1} != {2}", 
            auditLog.ObjectId, auditLog.Tid, existing.Tid));
        }
      }
      map[auditLog.ObjectId] = auditLog;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    /// <returns></returns>
    protected PersistentObject AddToCache(AuditLog auditLog)
    {
      if (_loadedEntityObjectIds.Contains(auditLog.ObjectId))
        return null;

      if (EntityMap.ContainsKey(auditLog.ObjectId))
      {
        throw new DatabaseException("Entity with ObjectId [" + auditLog.ObjectId + "] is already cached!");
      }

      if (auditLog.Action == ItemAction.Removed) return null;

      ClassMeta cm = ClassCache.Find(auditLog.RootObjectId);
      if (cm.AuditPolicy != AuditPolicy.History)
        return null;
      
      // Deserialize the after image for this audit log
      PersistentObject po;
      using (var stream = new MemoryStream(auditLog.ObjectDelta))
      using (var reader = new BinaryEntityReader(stream, Adaptor))
      {
        ReadMode = auditLog.ObjectId;
        po = reader.ReadEntity();
        ReadMode = 0;
      }

      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="tran"></param>
    /// <param name="objectIds"></param>
    protected static void CreateObjectIdJoinTable(SqlConnection conn, SqlTransaction tran, IEnumerable<long> objectIds)
    {
      using (var dt = new DataTable("#ids"))
      {
        dt.Columns.Add("ObjectId", typeof(long));
        foreach (var objectId in objectIds)
        {
          var row = dt.NewRow();
          row[0] = objectId;
          dt.Rows.Add(row);
        }

        using (var cmd = conn.CreateCommand())
        {
          cmd.CommandTimeout = 0;
          cmd.CommandText = "CREATE TABLE #ids (ObjectId bigint NOT NULL PRIMARY KEY (ObjectId))";
          cmd.Transaction = tran;
          cmd.ExecuteNonQuery();
        }

        using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tran) { DestinationTableName = dt.TableName, BulkCopyTimeout = 0 })
        {
          bulkCopy.WriteToServer(dt);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="tran"></param>
    protected void DropObjectIdJoinTable(IDbConnection conn, IDbTransaction tran)
    {
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.Transaction = tran;
        cmd.CommandText = "DROP TABLE #ids";
        cmd.ExecuteNonQuery();
      }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Return a new AuditLog instance with data from the specified <see cref="System.Data.IDataReader">reader</see>
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static AuditLog ReadAuditLog(IDataReader reader)
    {
      var validFrom = (DateTime)reader[5];
      if (validFrom == SessionFactory.SqlMinDate)
        validFrom = DateTime.MinValue;

      return new AuditLog
      {
        Tid = (int)reader[0],
        ObjectId = (long)reader[1],
        RootObjectId = (long)reader[2],
        ParentObjectId = (long)reader[3],
        EntityId = (int)reader[4],
        ValidFrom = validFrom,
        Action = (ItemAction)reader[6],
        ObjectDelta = DBNull.Value.Equals(reader[7]) ? null : (byte[])reader[7],
        IsArchived = (bool)reader[8]
      };
    }

    private PersistentObject AddToCache(long objectId)
    {
      if (EntityMap.ContainsKey(objectId))
      {
        throw new DatabaseException("Entity with ObjectId [" + objectId + "] is already cached!");
      }
      EntityMap.Add(objectId, null);
      return null;
    }

    private void AddToCache(PersistentObject po)
    {
      EntityMap.Add(po.ObjectId, po);
    }

    private AuditLog GetAuditLog(long objectId)
    {
      AuditLog auditLog;

      if (EagerFetch)
      {
        var entityId = EntityHelper.GetEntityIdFromObjectId(objectId);

        IDictionary<long, AuditLog> map;
        if (!AuditLogMap.TryGetValue(entityId, out map))
        {
          using (var conn = new RawConnection())
          {
            map = LoadAuditLogsForEntityId(conn, entityId).ToDictionary(al => al.ObjectId);
          }
          AuditLogMap[entityId] = map;
        }

        map.TryGetValue(objectId, out auditLog);
      }
      else
      {
        var entityId = EntityHelper.GetEntityIdFromObjectId(objectId);

        IDictionary<long, AuditLog> map;
        if (!AuditLogMap.TryGetValue(entityId, out map))
        {
          map = new Dictionary<long, AuditLog>();
          AuditLogMap[entityId] = map;
        }
        if (!map.TryGetValue(objectId, out auditLog))
        {
          auditLog = LoadAuditLog(objectId);
          map[objectId] = auditLog;
        }
      }

      return auditLog;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="paramName"></param>
    /// <param name="paramValue"></param>
    protected static void AddParameter(IDbCommand cmd, string paramName, object paramValue)
    {
      var p = cmd.CreateParameter();
      p.ParameterName = paramName;
      p.Value = paramValue;
      cmd.Parameters.Add(p);
    }

    private IEnumerable<PersistentObject> InternalQuery(Lazy<RawConnection> lazyConn, ClassMeta cm)
    {
      var list = new List<PersistentObject>();

      foreach (var entityId in GetEntityIds(cm))
      {
        var auditLogs = LoadAuditLogs(lazyConn, entityId);
        if (auditLogs.Any())
        {
          IList<PersistentObject> entityList;
          if (!EntityListMap.TryGetValue(entityId, out entityList))
          {
            if (Logger.IsDebugEnabled)
            {
              Logger.DebugFormat("Resolving {0} {1} entities...", auditLogs.Count, ClassCache.Find(entityId));
            }

            entityList = auditLogs.Select(AddToCache).Where(_ => _ != null).ToList();
            EntityListMap[entityId] = entityList;
          }
          list.AddRange(entityList);
        }
      }

      foreach (long id in _loadedEntityObjectIds)
      {
        if (EntityHelper.GetEntityIdFromObjectId(id) == cm.EntityId)
          list.Add(EntityMap[id]);
      }

      return list;
    }

    private ICollection<AuditLog> LoadAuditLogs(Lazy<RawConnection> lazyConn, int entityId)
    {
      IDictionary<long, AuditLog> auditLogMap;
      if (!AuditLogMap.TryGetValue(entityId, out auditLogMap))
      {
        var conn = lazyConn.Value;
        auditLogMap = LoadAuditLogsForEntityId(conn, entityId).ToDictionary(al => al.ObjectId);
        AuditLogMap[entityId] = auditLogMap;

        if (ResolveAll)
        {
          // Fetch logs for referenced entities
          var classMeta = ClassCache.Find(entityId);
          foreach (var cascade in classMeta.CascadeList)
          {
            var referencedEntityIds = new List<int>();

            var c = cascade;
            if (c.ReferencedEntity.IsBaseEntity)
            {
              var mopm = c as ManyToOnePropertyMeta;
              if (mopm != null && mopm.OwnershipResolver != null)
              {
                referencedEntityIds.Add(ClassCache.Find(mopm.OwnershipResolver.GetOwnedConcreteType(classMeta.Type)).EntityId);
              }
              else
              {
                referencedEntityIds.AddRange(GetEntityIds(c.ReferencedEntity));
              }
            }
            else
            {
              referencedEntityIds.AddRange(GetEntityIds(c.ReferencedEntity));
            }

            foreach (var referencedEntityId in referencedEntityIds)
            {
              LoadAuditLogs(lazyConn, referencedEntityId);
            }
          }
        }
      }

      return auditLogMap.Values;
    }

    private static IEnumerable<int> GetEntityIds(ClassMeta classMeta)
    {
      return classMeta.IsBaseEntity ? ClassCache.FindAll().Where(_ => _.BaseEntity == classMeta).Select(_ => _.EntityId) : new[] { classMeta.EntityId };
    }

    #endregion
  }
}