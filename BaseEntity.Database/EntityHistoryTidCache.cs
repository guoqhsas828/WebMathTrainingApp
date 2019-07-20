// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityHistoryTidCache : EntityHistoryCache
  {
    #region Data

    private readonly SortedDictionary<int, IDictionary<long, AuditLog>> _auditLogMap = new SortedDictionary<int, IDictionary<long, AuditLog>>();
    private readonly SortedDictionary<int, IDictionary<long, AuditLog>> _filteredAuditLogMap = new SortedDictionary<int, IDictionary<long, AuditLog>>();
    private readonly IDictionary<int, EntityHistoryTidContext> _contextMap = new Dictionary<int, EntityHistoryTidContext>();
    private Lazy<IDictionary<int, CommitLog>> _lazyCommitLogMap;

    private const string RootObjectIdSql =
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al JOIN #ids ON #ids.ObjectId=al.RootObjectId";

    #endregion

    #region Constructors

    /// <summary>
    /// Initialize new instance with AuditLogs for all entities with one of the specified rootObjectIds
    /// </summary>
    /// <param name="rootObjectIds"></param>
    public EntityHistoryTidCache(IList<long> rootObjectIds)
    {
      _lazyCommitLogMap = new Lazy<IDictionary<int, CommitLog>>(InitCommitLogMap);

      foreach (var auditLog in LoadAuditLogs(rootObjectIds, RootObjectIdSql))
      {
        Add(auditLog, true);
      }
    }

    /// <summary>
    /// Initialize new instance with AuditLogs for any entity that was inserted/updated/deleted in specified tid
    /// </summary>
    /// <param name="tid"></param>
    /// <returns></returns>
    public EntityHistoryTidCache(int tid)
    {
      _lazyCommitLogMap = new Lazy<IDictionary<int, CommitLog>>(InitCommitLogMap);

      LoadAuditLogs(tid);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="types"></param>
    public EntityHistoryTidCache(DateTime start, DateTime end, IList<Type> types = null)
    {
      _lazyCommitLogMap = new Lazy<IDictionary<int, CommitLog>>(InitCommitLogMap);

      LoadAuditLogs(start, end, types);
    }

    /// <summary>
    /// Initialize new instance with audit logs for 24-hour period starting from date and (optionally) filtering by type
    /// </summary>
    /// <param name="date">The starting date for the period. If not UTC, it will be converted to UTC.</param>
    /// <param name="types">If specified, only entities assignable to the specified types will be included.</param>
    public EntityHistoryTidCache(DateTime date, IList<Type> types = null)
      : this(date, date.AddDays(1), types)
    {
    }

    #endregion

    #region Implementation of IDisposable

    /// <summary>
    /// 
    /// </summary>
    public override void Dispose()
    {
      foreach (var context in _contextMap.Values)
      {
        context.Dispose();
      }

      _contextMap.Clear();
    }

    #endregion

    #region Properties

    private IDictionary<int, CommitLog> CommitLogMap
    {
      get { return _lazyCommitLogMap.Value; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Get distinct set of cached root object ids
    /// </summary>
    /// <returns></returns>
    public override IList<long> GetRootObjectIds()
    {
      return _filteredAuditLogMap.SelectMany(kvp => kvp.Value.Values).Select(al => al.RootObjectId).Distinct().ToList();
    }

    /// <summary>
    /// Get distinct set of cached root object ids
    /// </summary>
    /// <returns></returns>
    public IList<long> GetRootObjectIds(int tid, IEnumerable<Type> types = null)
    {
      IDictionary<long, AuditLog> byObjectIdMap;
      if (_auditLogMap.TryGetValue(tid, out byObjectIdMap))
      {
        return byObjectIdMap.Select(kvp => kvp.Value).Select(a => a.RootObjectId).Distinct().Where(_ => FilterType(types, _)).ToList();
      }
      return new long[0];
    }

    /// <summary>
    /// Get CommitLogs in Tid order
    /// </summary>
    /// <returns></returns>
    public IEnumerable<CommitLog> GetCommitLogs()
    {
      return GetTids().Select(GetCommitLog).ToList();
    }

    /// <summary>
    /// Get all cached audit logs
    /// </summary>
    /// <returns>List of audit logs</returns>
    public override IList<AuditLog> GetAuditLogs()
    {
      return _filteredAuditLogMap.SelectMany(kvp => kvp.Value.Values).ToList();
    }

    /// <summary>
    /// Get cached audit logs for the specified tid
    /// </summary>
    /// <param name="tid"></param>
    /// <returns>List of cached audit logs (empty if none found for tid)</returns>
    public IList<AuditLog> GetAuditLogs(int tid)
    {
      IDictionary<long, AuditLog> map;
      return _auditLogMap.TryGetValue(tid, out map) ? map.Values.ToArray() : new AuditLog[0];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="tid"></param>
    /// <returns></returns>
    public AuditLog GetAuditLog(long objectId, int tid)
    {
      IDictionary<long, AuditLog> map;
      if (!_auditLogMap.TryGetValue(tid, out map))
      {
        throw new ArgumentException("No AuditLogs found for Tid=" + tid);
      }
      AuditLog auditLog;
      if (!map.TryGetValue(objectId, out auditLog))
      {
        throw new ArgumentException("No AuditLog found for Tid=" + tid + " and ObjectId=" + objectId);
      }
      return auditLog;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tid"></param>
    /// <returns></returns>
    public CommitLog GetCommitLog(int tid)
    {
      CommitLog commitLog;
      if (!CommitLogMap.TryGetValue(tid, out commitLog))
      {
        throw new ArgumentException("No CommitLog found for Tid=" + tid);
      }
      return commitLog;
    }

    /// <summary>
    /// Return all tids for this instance
    /// </summary>
    /// <returns>List of tids in ascending order.</returns>
    public IList<int> GetTids()
    {
      return _filteredAuditLogMap.Keys.ToList();
    }

    /// <summary>
    /// Load all CommitLogs that have an AuditLog for the specified rootObjectId
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="ignoreArchived"></param>
    /// <returns></returns>
    public IList<int> GetTids(long rootObjectId, bool ignoreArchived = true)
    {
      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> validFromMap;
      if (!ByEntityMap.TryGetValue(rootObjectId, out validFromMap))
      {
        return new int[0];
      }
      var tids = ignoreArchived
        ? new HashSet<int>(validFromMap.SelectMany(kvp => kvp.Value.Values).Where(al => !al.IsArchived).Select(al => al.Tid))
        : new HashSet<int>(validFromMap.SelectMany(kvp => kvp.Value.Values).Select(al => al.Tid));
      return _filteredAuditLogMap.Keys.Where(tid => tids.Contains(tid)).ToList();
    }

    /// <summary>
    /// Get the Tid for the specified rootObjectId that represents the prior version 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="tid"></param>
    /// <returns></returns>
    public int GetPriorVersionTid(long rootObjectId, int tid)
    {
      var cm = ClassCache.Find(rootObjectId);
      if (cm == null)
      {
        throw new ArgumentException("No ClassMeta found for rootObjectId [" + rootObjectId + "]");
      }
      if (!cm.IsRootEntity)
      {
        throw new ArgumentException("Invalid rootObjectId [" + rootObjectId + "] : not a RootEntity");
      }
      IDictionary<long, AuditLog> map;
      if (!_auditLogMap.TryGetValue(tid, out map))
      {
        throw new ArgumentException("No audit logs found for tid [" + tid + "]");
      }
      AuditLog rootObjectAuditLog;
      if (!map.TryGetValue(rootObjectId, out rootObjectAuditLog))
      {
        throw new ArgumentException("No audit logs found for tid [" + tid + "] and rootObjectId [" + rootObjectId + "]");
      }

      if (rootObjectAuditLog.Action == ItemAction.Added)
      {
        return -1;
      }

      int priorVersionTid = -1;

      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> byValidFromMap;
      if (!ByEntityMap.TryGetValue(rootObjectId, out byValidFromMap))
      {
        throw new ArgumentException("AuditLog with ObjectId [" + rootObjectId + "] was not found in EntityHistoryMap");
      }

      // Prior version for an edit must have ValidFrom <= new version
      var kvp1 = byValidFromMap.Last(_ => _.Key <= rootObjectAuditLog.ValidFrom);
      var kvp2 = kvp1.Value.LastOrDefault(_ => _.Key < tid && _.Value != null);
      if (kvp2.Key > priorVersionTid)
      {
        priorVersionTid = kvp2.Key;
      }

      return priorVersionTid;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="tid"></param>
    /// <returns></returns>
    public PersistentObject GetEntity(long objectId, int tid)
    {
      var context = GetOrAddContext(tid);
      return context.Get(objectId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="prevTid"></param>
    /// <param name="nextTid"></param>
    /// <returns></returns>
    public EntityTidDiffGram GetDiffGram(long rootObjectId, int prevTid, int nextTid)
    {
      var commitLog = GetCommitLog(nextTid);
      if (commitLog == null)
      {
        throw new ArgumentException("No CommitLog for tid=" + nextTid);
      }

      var rootObjectAuditLog = GetAuditLog(rootObjectId, nextTid);
      if (rootObjectAuditLog == null)
      {
        throw new ArgumentException("No AuditLog for tid=" + nextTid + " and rootObjectId=" + rootObjectId);
      }

      string xml = WriteAuditHistoryXml(rootObjectId, prevTid, nextTid);

      var userName = EntityContextFactory.GetUserName(commitLog.UpdatedBy);

      return new EntityTidDiffGram
      {
        NextTid = nextTid,
        PrevTid = prevTid,
        Comment = commitLog.Comment,
        RootObjectId = rootObjectId,
        LastUpdated = commitLog.LastUpdated,
        UpdatedBy = userName,
        Xml = xml,
      };
    }

    /// <summary>
    /// Writes audit history XML.
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="prevTid"></param>
    /// <param name="nextTid"></param>
    /// <returns></returns>
    public string WriteAuditHistoryXml(long rootObjectId, int prevTid, int nextTid)
    {
      var auditLogs = GetAuditLogs(nextTid);
      if (!auditLogs.Any())
      {
        throw new DatabaseException("No AuditLogs found for tid [" + nextTid + "] and rootObjectId [" + rootObjectId + "]");
      }

      var auditLogsForRootObject = auditLogs.Where(al => al.RootObjectId == rootObjectId).ToList();
      if (!auditLogsForRootObject.Any())
      {
        throw new DatabaseException("No AuditLogs found for Tid [" + nextTid + "] and rootObjectId [" + rootObjectId + "]");
      }

      var prevTidContext = GetOrAddContext(prevTid);
      var nextTidContext = GetOrAddContext(nextTid);

      return WriteAuditHistoryXml(auditLogsForRootObject, prevTidContext, nextTidContext);
    }

    /// <summary>
    /// 
    /// </summary>
    public override void Clear()
    {
      base.Clear();

      foreach (var context in _contextMap.Values)
      {
        context.Dispose();
      }

      _auditLogMap.Clear();
      _filteredAuditLogMap.Clear();
      _contextMap.Clear();

      _lazyCommitLogMap = new Lazy<IDictionary<int, CommitLog>>(InitCommitLogMap);
    }

    #endregion 

    #region Helper Methods

    private EntityHistoryTidContext GetOrAddContext(int tid)
    {
      IDictionary<long, AuditLog> map;
      _auditLogMap.TryGetValue(tid, out map);

      EntityHistoryTidContext context;
      if (!_contextMap.TryGetValue(tid, out context))
      {
        context = map == null ? new EntityHistoryTidContext(tid) : new EntityHistoryTidContext(tid, map.Values);
        _contextMap[tid] = context;
      }
      return context;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    /// <param name="isMatch"></param>
    private void Add(AuditLog auditLog, bool isMatch)
    {
      if (auditLog == null)
      {
        throw new ArgumentNullException("auditLog");
      }

      var cm = ClassCache.Find(auditLog.RootObjectId);
      if (cm == null)
      {
        throw new ArgumentException("No ClassMeta found for rootObjectId [" + auditLog.RootObjectId + "]");
      }
      if (!cm.IsRootEntity)
      {
        throw new ArgumentException("Invalid rootObjectId [" + auditLog.RootObjectId + "] : not a RootEntity");
      }

      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> byValidFromMap;
      if (!ByEntityMap.TryGetValue(auditLog.ObjectId, out byValidFromMap))
      {
        byValidFromMap = new SortedDictionary<DateTime, SortedDictionary<int, AuditLog>>();
        ByEntityMap[auditLog.ObjectId] = byValidFromMap;
      }
      SortedDictionary<int, AuditLog> list;
      if (!byValidFromMap.TryGetValue(auditLog.ValidFrom, out list))
      {
        list = new SortedDictionary<int, AuditLog>();
        byValidFromMap[auditLog.ValidFrom] = list;
      }
      list[auditLog.Tid] = auditLog;

      IDictionary<long, AuditLog> byObjectIdMap;
      if (!_auditLogMap.TryGetValue(auditLog.Tid, out byObjectIdMap))
      {
        byObjectIdMap = new Dictionary<long, AuditLog>();
        _auditLogMap[auditLog.Tid] = byObjectIdMap;
      }
      byObjectIdMap.Add(auditLog.ObjectId, auditLog);

      if (isMatch)
      {
        IDictionary<long, AuditLog> filteredAuditLogMap;
        if (!_filteredAuditLogMap.TryGetValue(auditLog.Tid, out filteredAuditLogMap))
        {
          filteredAuditLogMap = new Dictionary<long, AuditLog>();
          _filteredAuditLogMap[auditLog.Tid] = filteredAuditLogMap;
        }
        filteredAuditLogMap.Add(auditLog.ObjectId, auditLog);
      }
    }
    private void LoadAuditLogs(int tid)
    {
      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Tid,ObjectId,RootObjectId,ParentObjectId,EntityId,ValidFrom,Action,ObjectDelta,IsArchived FROM AuditLog WHERE RootObjectId IN (SELECT DISTINCT RootObjectId FROM AuditLog WHERE Tid=@p0)";
        AddParameter(cmd, "p0", tid);
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            var auditLog = ReadAuditLog(reader);
            Add(auditLog, auditLog.Tid == tid);
          }
        }
      }
    }

    private void LoadAuditLogs(DateTime start, DateTime end, IList<Type> types)
    {
      using (var conn = new RawConnection())
      {
        using (var tran = (SqlTransaction)conn.BeginTransaction())
        {
          var entityIds = new List<int>();
          if (types != null)
          {
            foreach (var type in types)
            {
              var cm = ClassCache.Find(type);
              if (cm == null)
              {
                throw new ArgumentException("Invalid type [" + type.Name + "] : not an Entity!");
              }
              if (!cm.IsRootEntity)
              {
                throw new ArgumentException("Invalid type [" + type.Name + "] : not a RootEntity!");
              }
              if (cm.EntityId == 0)
              {
                entityIds.AddRange(ClassCache.FindAll().Where(_ => _.BaseEntity == cm).Select(_ => _.EntityId));
              }
              else
              {
                entityIds.Add(cm.EntityId);
              }
            }
          }

          start = start == DateTime.MinValue ? SessionFactory.SqlMinDate : start.ToUniversalTime();
          end = end == DateTime.MaxValue ? DateTime.MaxValue : end.ToUniversalTime();

          using (var cmd = conn.CreateCommand())
          {
            string joinClause;
            if (entityIds.Count == 0)
            {
              joinClause = "";
            }
            else
            {
              joinClause = " JOIN #ids ON #ids.EntityId = a1.EntityId";
              CreateEntityIdJoinTable(conn.Connection, tran, entityIds);
            }

            cmd.CommandText = "SELECT a1.Tid,a1.ObjectId,a1.RootObjectId,a1.ParentObjectId,a1.EntityId,a1.ValidFrom,a1.Action,a1.ObjectDelta,a1.IsArchived,c1.LastUpdated FROM AuditLog a1 JOIN CommitLog c1 ON c1.Tid = a1.Tid WHERE a1.RootObjectId IN (SELECT DISTINCT a2.RootObjectId FROM AuditLog a2 JOIN CommitLog c2 ON c2.Tid=a2.Tid" + joinClause + " WHERE c2.LastUpdated>=@p0 AND c2.LastUpdated<@p1)";
            AddParameter(cmd, "p0", start);
            AddParameter(cmd, "p1", end);

            cmd.Transaction = tran;

            try
            {
              using (var reader = cmd.ExecuteReader())
              {
                while (reader.Read())
                {
                  var auditLog = ReadAuditLog(reader);
                  var lastUpdated = (DateTime)reader[9];
                  var isMatch = lastUpdated >= start.ToUniversalTime() && lastUpdated < end.ToUniversalTime();
                  Add(auditLog, isMatch);
                }
              }
            }
            finally
            {
              if (entityIds.Count > 1)
                DropEntityIdJoinTable(conn, tran);
            }
          }
        }
      }
    }

    private static bool FilterType(IEnumerable<Type> types, long objectId)
    {
      if (types == null) return true;
      var type = EntityHelper.GetClassFromObjectId(objectId);
      return types.Any(t => t.IsAssignableFrom(type));
    }

    private IDictionary<int, CommitLog> InitCommitLogMap()
    {
      var map = new Dictionary<int, CommitLog>();

      using (var conn = new RawConnection())
      {
        if (_auditLogMap.Count == 0)
        {
          throw new InvalidOperationException("_byTidMap is empty");
        }

        if (_auditLogMap.Count <= 10)
        {
          var tids = _auditLogMap.Keys.OrderBy(_ => _).ToList();

          using (var cmd = conn.CreateCommand())
          {
            // To get the benefit of query caching we use the same query if searching for up to 10 CommitLogs
            cmd.CommandText = "SELECT DISTINCT cl.Tid, cl.LastUpdated, cl.UpdatedBy, cl.Comment FROM CommitLog cl JOIN AuditLog al ON cl.Tid=al.Tid WHERE cl.Tid IN (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)";
            AddParameter(cmd, "p0", tids.Count > 0 ? tids[0] : 0);
            AddParameter(cmd, "p1", tids.Count > 1 ? tids[1] : 0);
            AddParameter(cmd, "p2", tids.Count > 2 ? tids[2] : 0);
            AddParameter(cmd, "p3", tids.Count > 3 ? tids[3] : 0);
            AddParameter(cmd, "p4", tids.Count > 4 ? tids[4] : 0);
            AddParameter(cmd, "p5", tids.Count > 5 ? tids[5] : 0);
            AddParameter(cmd, "p6", tids.Count > 6 ? tids[6] : 0);
            AddParameter(cmd, "p7", tids.Count > 7 ? tids[7] : 0);
            AddParameter(cmd, "p8", tids.Count > 8 ? tids[8] : 0);
            AddParameter(cmd, "p9", tids.Count > 9 ? tids[9] : 0);
            using (var reader = cmd.ExecuteReader())
            {
              while (reader.Read())
              {
                CommitLog log = ReadCommitLog(reader);
                map[log.Tid] = log;
              }
            }
          }
        }
        else
        {
          using (var cmd = conn.CreateCommand())
          {
            cmd.CommandTimeout = 0;
            cmd.CommandText = "SELECT DISTINCT cl.Tid, cl.LastUpdated, cl.UpdatedBy, cl.Comment FROM CommitLog cl JOIN AuditLog al ON cl.Tid=al.Tid WHERE cl.Tid>=@p0 AND cl.Tid<=@p1";
            AddParameter(cmd, "p0", _auditLogMap.Min(kvp => kvp.Key));
            AddParameter(cmd, "p1", _auditLogMap.Max(kvp => kvp.Key));
            using (var reader = cmd.ExecuteReader())
            {
              while (reader.Read())
              {
                var commitLog = ReadCommitLog(reader);
                if (_auditLogMap.ContainsKey(commitLog.Tid))
                  map[commitLog.Tid] = commitLog;
              }
            }
          }
        }
      }

      return map;
    }

    private static void CreateEntityIdJoinTable(SqlConnection conn, SqlTransaction tran, IEnumerable<int> entityIds)
    {
      using (var dt = new DataTable("#ids"))
      {
        dt.Columns.Add("EntityId", typeof(int));
        foreach (var entityId in entityIds)
        {
          var row = dt.NewRow();
          row[0] = entityId;
          dt.Rows.Add(row);
        }

        using (var cmd = conn.CreateCommand())
        {
          cmd.CommandTimeout = 0;
          cmd.CommandText = "CREATE TABLE #ids (EntityId bigint NOT NULL PRIMARY KEY (EntityId))";
          cmd.Transaction = tran;
          cmd.ExecuteNonQuery();
        }

        using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tran) { DestinationTableName = dt.TableName, BulkCopyTimeout = 0 })
        {
          bulkCopy.WriteToServer(dt);
        }
      }
    }

    private static void DropEntityIdJoinTable(IDbConnection conn, IDbTransaction tran)
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
  }
}