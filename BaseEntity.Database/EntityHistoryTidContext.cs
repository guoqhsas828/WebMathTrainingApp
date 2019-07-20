// 
// Copyright (c) WebMathTraining Inc 2002-2013. All rights reserved.
// 

using System.Collections.Generic;
using System.Data;
using log4net;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public sealed class EntityHistoryTidContext : EntityHistoryContext
  {
    #region Data

    private static readonly ILog Logger = LogManager.GetLogger(typeof(EntityHistoryTidContext));

    private const string ObjectIdSql =
      "WITH CTE (Tid) AS (SELECT MAX(al.Tid) AS Tid FROM AuditLog al WHERE al.ObjectId=@p1 AND (al.Tid<=@p0 OR al.Action IN (0,1))) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE ON CTE.Tid = al.Tid " +
      "WHERE al.ObjectId = @p1";

    private const string EntityIdSql =
      "WITH CTE (ObjectId, Tid) AS (SELECT al.ObjectId, MAX(al.Tid) AS Tid FROM AuditLog al WHERE al.EntityId=@p1 AND (al.Tid<=@p0 OR al.Action IN (0,1)) GROUP BY al.ObjectId) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE ON CTE.ObjectId = al.ObjectId AND CTE.Tid = al.Tid";

    private const string RootObjectIdSql =
      "WITH CTE (ObjectId, Tid) AS (SELECT al.ObjectId, MAX(al.Tid) AS Tid FROM AuditLog al JOIN #ids ON #ids.ObjectId = al.RootObjectId WHERE (al.Tid<=@p0 OR al.Action IN (0,1)) GROUP BY al.ObjectId) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE ON CTE.ObjectId = al.ObjectId AND CTE.Tid = al.Tid";

    #endregion

    #region Constructors

    /// <summary>
    /// Construct an instance of <see cref="EntityHistoryTidContext" /> that will resolve to the state of the entity immediately after the specified tid.
    /// </summary>
    /// <param name="tid"></param>
    /// <param name="auditLogs"></param>
    public EntityHistoryTidContext(int tid, ICollection<AuditLog> auditLogs)
      : base(auditLogs)
    {
      Tid = tid;
    }

    /// <summary>
    /// Construct an instance of <see cref="EntityHistoryTidContext" /> that will resolve to the state of the entity immediately after the specified tid.
    /// </summary>
    /// <param name="tid"></param>
    public EntityHistoryTidContext(int tid)
      : this(tid, LoadAuditLogsForTid(tid))
    {
    }
    
    /// <summary>
    ///
    /// </summary>
    /// <param name="tid"></param>
    /// <param name="rootObjectIds"></param>
    public EntityHistoryTidContext(int tid, IList<long> rootObjectIds)
    {
      Tid = tid;

      foreach (var auditLog in LoadAuditLogsForRootObjectIds(rootObjectIds, RootObjectIdSql, tid))
      {
        Add(auditLog);
      }
    }

    #endregion

    #region Properties

    private int Tid { get; set; }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    protected override AuditLog LoadAuditLog(long objectId)
    {
      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.CommandText = ObjectIdSql;
        AddParameter(cmd, "p0", Tid);
        AddParameter(cmd, "p1", objectId);
        using (var reader = cmd.ExecuteReader())
        {
          if (reader.Read())
            return ReadAuditLog(reader);
        }
      }

      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    protected override IEnumerable<AuditLog> LoadAuditLogsForEntityId(IDbConnection conn, int entityId)
    {
      Logger.DebugFormat("Loading AuditLogs for EntityId [{0}] and Tid [{1}]", entityId, Tid);

      var auditLogs = new List<AuditLog>();

      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.CommandText = EntityIdSql;
        AddParameter(cmd, "p0", Tid);
        AddParameter(cmd, "p1", entityId);
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
            auditLogs.Add(EntityHistoryCache.ReadAuditLog(reader));
        }
      }

      return auditLogs;
    }

    private static IList<AuditLog> LoadAuditLogsForTid(int tid)
    {
      var auditLogs = new List<AuditLog>();

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Tid,ObjectId,RootObjectId,ParentObjectId,EntityId,ValidFrom,Action,ObjectDelta,IsArchived FROM AuditLog WHERE Tid=@p0";
        AddParameter(cmd, "p0", tid);
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
            auditLogs.Add(EntityHistoryCache.ReadAuditLog(reader));
        }
      }

      return auditLogs;
    }

    #endregion
  }
}