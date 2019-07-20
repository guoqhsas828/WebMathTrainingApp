// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityHistoryValidFromCache : EntityHistoryCache
  {
    #region Data

    private static readonly ILog Logger = LogManager.GetLogger(typeof(EntityHistoryValidFromCache));

    private readonly ISet<long> _rootObjectids = new HashSet<long>();
 
    private readonly IDictionary<DateTime, IDictionary<long, AuditLog>> _byValidFromMap =
      new Dictionary<DateTime, IDictionary<long, AuditLog>>();

    private readonly IDictionary<DateTime, EntityHistoryValidFromContext> _contextMaps =
      new Dictionary<DateTime, EntityHistoryValidFromContext>();

    private const string EntityIdSql =
      "WITH CTE (ObjectId, ValidFrom, Tid) AS (SELECT ObjectId, ValidFrom, MAX(Tid) FROM AuditLog GROUP BY ObjectId, ValidFrom) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE ON CTE.ObjectId = al.ObjectId AND CTE.Tid = al.Tid " +
      "WHERE al.EntityId=@p0";

    private const string RootObjectIdSql =
      "WITH CTE (ObjectId, ValidFrom, Tid) AS (SELECT al.ObjectId, al.ValidFrom, MAX(al.Tid) FROM AuditLog al JOIN #ids ON #ids.ObjectId = al.RootObjectId GROUP BY al.ObjectId, al.ValidFrom) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE ON CTE.ObjectId = al.ObjectId AND CTE.Tid = al.Tid ";

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public EntityHistoryValidFromCache()
    {
    }
    /// <summary>
    /// Initialize new instance with AuditLogs for all entities of the specified type
    /// </summary>
    /// <param name="entityId"></param>
    public EntityHistoryValidFromCache(int entityId)
    {
      foreach (var auditLog in LoadAuditLogs(entityId, EntityIdSql))
      {
        Add(auditLog);
      }
    }

    /// <summary>
    /// Initialize new instance with AuditLogs for all entities with one of the specified rootObjectIds
    /// </summary>
    /// <param name="rootObjectIds"></param>
    public EntityHistoryValidFromCache(IList<long> rootObjectIds)
    {
      foreach (var auditLog in LoadAuditLogs(rootObjectIds, RootObjectIdSql))
      {
        Add(auditLog);
      }
    }

    #endregion

    #region Implementation of IDisposable

    public override void Dispose()
    {
      foreach (var context in _contextMaps.Values)
      {
        context.Dispose();
      }

      _contextMaps.Clear();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override IList<long> GetRootObjectIds()
    {
      return _byValidFromMap.Values.SelectMany(_ => _).Select(kvp => kvp.Value.RootObjectId).Distinct().ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override IList<AuditLog> GetAuditLogs()
    {
      return _byValidFromMap.Values.SelectMany(_ => _).Select(kvp => kvp.Value).ToList();
    }

    /// <summary>
    /// Return all Valid From Dates for this instance
    /// </summary>
    /// <returns>List of Valid From Dates in ascending order.</returns>
    public IList<DateTime> GetDates()
    {
      return _byValidFromMap.Keys.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IList<EntityHistoryValidFromContext> GetContexts()
    {
      return _contextMaps.Values.ToList();
    }

    /// <summary>
    /// Load all CommitLogs that have an AuditLog for any of the specified rootObjectIds
    /// </summary>
    /// <param name="rootObjectIds"></param>
    /// <returns>List of tids in ascending order.</returns>
    public IList<DateTime> GetDates(IEnumerable<long> rootObjectIds)
    {
      return ByEntityMap.Values.SelectMany(dict => dict.Keys).OrderBy(_ => _).ToList();
    }

    /// <summary>
    /// Return the unique list of ValidFrom dates for the specified rootObjectId
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <returns></returns>
    public IList<DateTime> GetDates(long rootObjectId)
    {
      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> validFromMap;
      if (!ByEntityMap.TryGetValue(rootObjectId, out validFromMap))
      {
        return new DateTime[0];
      }
      return validFromMap.SelectMany(kvp => kvp.Value.Values).Select(al => al.ValidFrom).Distinct().OrderBy(_ => _).ToList();
    }

    /// <summary>
    /// Get the ValidFrom for the prior active version (i.e. version with older ValidFrom date) if any
    /// </summary>
    /// <param name="rootObjectId">The rootObjectId to use to lookup the prior active version</param>
    /// <param name="validFrom"></param>
    /// <returns>The ValidFrom for the prior active version if found, else validFrom.</returns>
    public DateTime GetPriorVersion(long rootObjectId, DateTime validFrom)
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

      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> byValidFromMap;
      if (!ByEntityMap.TryGetValue(rootObjectId, out byValidFromMap))
      {
        throw new ArgumentException("No audit logs found for rootObjectId [" + rootObjectId + "]");
      }

      SortedDictionary<int, AuditLog> list;
      if (!byValidFromMap.TryGetValue(validFrom, out list))
      {
        throw new ArgumentException("No audit logs found for rootObjectId [" + rootObjectId + "] and validFrom [" + validFrom + "]");
      }

      var auditLog = list.Values.Last(al => al.ObjectId == rootObjectId);
      if (auditLog.Action == ItemAction.Added)
      {
        return validFrom;
      }
     
      // Prior version for an edit must have ValidFrom <= new version
      var priorVersionAuditLogs = byValidFromMap.Where(_ => _.Key < auditLog.ValidFrom).OrderByDescending(_ => _.Key).Select(_ => _.Value).FirstOrDefault();
      if (priorVersionAuditLogs == null)
      {
        return validFrom;
      }

      var prevAuditLog = priorVersionAuditLogs.Select(_ => _.Value).LastOrDefault(_ => _.ObjectDelta != null);
      if (prevAuditLog != null)
        return prevAuditLog.ValidFrom;

      return validFrom;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="validFrom"></param>
    /// <returns></returns>
    public int GetEarliestTid(long rootObjectId, DateTime validFrom)
    {
      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> byValidFromMap;
      if (!ByEntityMap.TryGetValue(rootObjectId, out byValidFromMap))
      {
        throw new ArgumentException("AuditLog with ObjectId [" + rootObjectId + "] was not found in EntityHistoryMap");
      }

      // Prior version for an edit must have ValidFrom <= new version
      SortedDictionary<int, AuditLog> auditLogs;
      if (!byValidFromMap.TryGetValue(validFrom, out auditLogs))
      {
        throw new ArgumentException("AuditLog with ObjectId [" + rootObjectId + "] and ValidFrom [" + validFrom + "] was not found in EntityHistoryMap");
      }

      return auditLogs.Min(kvp => kvp.Key);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="validFrom"></param>
    /// <returns></returns>
    public int GetLatestTid(long rootObjectId, DateTime validFrom)
    {
      SortedDictionary<DateTime, SortedDictionary<int, AuditLog>> byValidFromMap;
      if (!ByEntityMap.TryGetValue(rootObjectId, out byValidFromMap))
      {
        throw new ArgumentException("AuditLog with ObjectId [" + rootObjectId + "] was not found in EntityHistoryMap");
      }

      // Prior version for an edit must have ValidFrom <= new version
      SortedDictionary<int, AuditLog> list;
      if (!byValidFromMap.TryGetValue(validFrom, out list))
      {
        throw new ArgumentException("AuditLog with ObjectId [" + rootObjectId + "] and ValidFrom [" + validFrom + "] was not found in EntityHistoryMap");
      }

      return list.Max(kvp => kvp.Key);
    }

    /// <summary>
    /// Get cached audit logs for the specified tid
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="validFrom"></param>
    /// <returns>List of cached audit logs (empty if none found for tid)</returns>
    public IList<AuditLog> GetAuditLogs(long rootObjectId, DateTime validFrom)
    {
      IDictionary<long, AuditLog> auditLogsForValidFrom;
      if (_byValidFromMap.TryGetValue(validFrom, out auditLogsForValidFrom))
      {
        // If this is a complex entity, then for each entity we want the one with the latest Tid
        return auditLogsForValidFrom.Values.Where(al => al.RootObjectId == rootObjectId).ToList();
      }
      return new AuditLog[0];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="validFrom"></param>
    /// <returns></returns>
    public AuditLog GetAuditLog(long objectId, DateTime validFrom)
    {
      IDictionary<long, AuditLog> auditLogsForValidFrom;
      if (!_byValidFromMap.TryGetValue(validFrom, out auditLogsForValidFrom))
      {
        throw new ArgumentException("No AuditLogs found for ValidFrom=" + validFrom);
      }
      AuditLog auditLog;
      auditLogsForValidFrom.TryGetValue(objectId, out auditLog);
      if (auditLog == null)
      {
        throw new ArgumentException("No AuditLog found for ValidFrom=" + validFrom + " and ObjectId=" + objectId);
      }
      return auditLog;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="prevValidFrom"></param>
    /// <param name="nextValidFrom"></param>
    /// <returns></returns>
    public EntityValidFromDiffGram GetDiffGram(long rootObjectId, DateTime prevValidFrom, DateTime nextValidFrom)
    {
      var rootObjectAuditLog = GetAuditLog(rootObjectId, nextValidFrom);
      if (rootObjectAuditLog == null)
      {
        throw new ArgumentException("No AuditLog for ValidFrom=" + nextValidFrom + " and rootObjectId=" + rootObjectId);
      }

      string xml = WriteAuditHistoryXml(rootObjectId, prevValidFrom, nextValidFrom);

      return new EntityValidFromDiffGram
      {
        NextValidFrom = nextValidFrom,
        PrevValidFrom = prevValidFrom,
        RootObjectId = rootObjectId,
        Xml = xml,
      };
    }

    /// <summary>
    /// Writes audit history XML.
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="prevValidFrom"></param>
    /// <param name="nextValidFrom"></param>
    /// <returns></returns>
    public string WriteAuditHistoryXml(long rootObjectId, DateTime prevValidFrom, DateTime nextValidFrom)
    {
      var auditLogs = GetAuditLogs(rootObjectId, nextValidFrom);
      if (!auditLogs.Any())
      {
        throw new DatabaseException("No AuditLogs found for tid [" + nextValidFrom + "] and rootObjectId [" + rootObjectId + "]");
      }

      var auditLogsForRootObject = auditLogs.Where(al => al.RootObjectId == rootObjectId).ToList();
      if (!auditLogsForRootObject.Any())
      {
        throw new DatabaseException("No AuditLogs found for Tid [" + nextValidFrom + "] and rootObjectId [" + rootObjectId + "]");
      }

      // Inception node needs to be handled as a special case
      var prevContext = prevValidFrom == nextValidFrom ? null : GetOrAddContext(prevValidFrom);
      var nextContext = GetOrAddContext(nextValidFrom);

      return WriteAuditHistoryXml(auditLogsForRootObject, prevContext, nextContext);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="validFrom"></param>
    /// <returns></returns>
    public PersistentObject GetEntity(long objectId, DateTime validFrom)
    {
      var context = GetOrAddContext(validFrom);
      return context.Get(objectId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="asOf"></param>
    /// <returns></returns>
    public DateTime GetNextValidFrom(long rootObjectId, DateTime asOf)
    {
      return GetDates(rootObjectId).OrderByDescending(_ => _).LastOrDefault(_ => _ > asOf);
    }

    /// <summary>
    /// Bulk load audit logs with specified rootObjectIds
    /// </summary>
    /// <param name="rootObjectIds"></param>
    /// <remarks>
    /// This can be used to incrementally bulk load entities based on references from other entities
    /// </remarks>
    public void LoadAuditLogs(ICollection<long> rootObjectIds)
    {
      var nonRootEntityId = rootObjectIds.Select(EntityHelper.GetEntityIdFromObjectId).FirstOrDefault(entityId => !RootEntityIds.Contains(entityId));
      if (nonRootEntityId != 0)
      {
        throw new DatabaseException("EntityId [" + nonRootEntityId + "] does not specify a RootEntity");
      }

      var loadedRootObjectIds = new HashSet<long>();

      var auditLogs = LoadAuditLogs(rootObjectIds.Where(id => !_rootObjectids.Contains(id)).ToList(), RootObjectIdSql);
      foreach (var auditLog in auditLogs)
      {
        Add(auditLog);
        loadedRootObjectIds.Add(auditLog.ObjectId);
      }

      // Add AuditLogs to any existing contexts
      foreach (var validFrom in _contextMaps.Keys)
      {
        var context = _contextMaps[validFrom];
        var map = new Dictionary<long, AuditLog>();
        foreach (var kvp in _byValidFromMap.OrderByDescending(_ => _.Key).Where(_ => _.Key <= validFrom))
        {
          foreach (var rootObjectId in loadedRootObjectIds.Where(id => !map.ContainsKey(id)))
          {
            AuditLog auditLog;
            if (kvp.Value.TryGetValue(rootObjectId, out auditLog))
              map[auditLog.ObjectId] = auditLog;
          }
        }
        foreach (var auditLog in map.Values)
        {
          context.Add(auditLog);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override void Clear()
    {
      base.Clear();

      foreach (var context in _contextMaps.Values)
      {
        context.Dispose();
      }
      
      _rootObjectids.Clear();
      _byValidFromMap.Clear();
      _contextMaps.Clear();
    }

    #endregion 
    
    #region Helper Methods

    private EntityHistoryValidFromContext GetOrAddContext(DateTime validFrom)
    {
      EntityHistoryValidFromContext context;
      if (!_contextMaps.TryGetValue(validFrom, out context))
      {
        var map = new Dictionary<long, AuditLog>();
        foreach (var k in _byValidFromMap.Keys.OrderByDescending(key => key).Where(key => key <= validFrom))
        {
          foreach (var auditLog in _byValidFromMap[k].Values.Where(al => !map.ContainsKey(al.ObjectId)))
            map[auditLog.ObjectId] = auditLog;
        }

        context = new EntityHistoryValidFromContext(validFrom, map.Values);
        _contextMaps[validFrom] = context;
      }
      return context;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    private void Add(AuditLog auditLog)
    {
      if (auditLog == null)
      {
        throw new ArgumentNullException("auditLog");
      }

      var cm = ClassCache.Find(auditLog.EntityId);
      if (cm.IsRootEntity)
      {
        _rootObjectids.Add(auditLog.ObjectId);
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
      // Insert in Tid order
      if (list.ContainsKey(auditLog.Tid))
      {
        Logger.WarnFormat("Attempt to add already cached AuditLog [{0}] [{1}]", auditLog.Tid, auditLog.ObjectId);
      }
      else
      {
        list[auditLog.Tid] = auditLog;
      }

      IDictionary<long, AuditLog> auditLogs;
      if (!_byValidFromMap.TryGetValue(auditLog.ValidFrom, out auditLogs))
      {
        auditLogs = new Dictionary<long, AuditLog>();
        _byValidFromMap[auditLog.ValidFrom] = auditLogs;
      }
      auditLogs.Add(auditLog.ObjectId, auditLog);
    }

    #endregion
  }
}