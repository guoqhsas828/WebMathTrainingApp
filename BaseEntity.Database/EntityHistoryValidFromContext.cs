// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityHistoryValidFromContext : EntityHistoryContext
  {
    #region Data

    private static readonly ILog Logger = LogManager.GetLogger(typeof(EntityHistoryValidFromContext));

    private const string ObjectIdSql =
      "WITH CTE1 (ValidFrom) AS (SELECT MAX(al.ValidFrom) AS ValidFrom FROM AuditLog al WHERE (al.ValidFrom <= @p0 OR al.Action IN (0,1)) AND al.ObjectId = @p1), " +
      "     CTE2 (ValidFrom, Tid) AS (SELECT al.ValidFrom, MAX(al.Tid) FROM AuditLog al WHERE (al.ValidFrom <= @p0 OR al.Action IN (0,1)) AND al.ObjectId = @p1 GROUP BY al.ValidFrom) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE1 ON CTE1.ValidFrom = al.ValidFrom " +
      "INNER JOIN CTE2 ON CTE2.Tid = al.Tid " +
      "WHERE al.ObjectId = @p1";

    private const string EntityIdSql =
      "WITH CTE1 (ObjectId, ValidFrom) AS (SELECT al.ObjectId, MAX(al.ValidFrom) AS ValidFrom FROM AuditLog al WHERE (al.ValidFrom <= @p0 OR al.Action IN (0,1)) AND al.EntityId = @p1 GROUP BY al.ObjectId), " +
      "     CTE2 (ObjectId, ValidFrom, Tid) AS (SELECT al.ObjectId, al.ValidFrom, MAX(al.Tid) FROM AuditLog al WHERE (al.ValidFrom <= @p0 OR al.Action IN (0,1)) AND al.EntityId = @p1 GROUP BY al.ObjectId, al.ValidFrom) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE1 ON CTE1.ObjectId = al.ObjectId AND CTE1.ValidFrom = al.ValidFrom " +
      "INNER JOIN CTE2 ON CTE2.ObjectId = al.ObjectId AND CTE2.Tid = al.Tid ";

    private const string RootObjectIdSql =
      "WITH CTE1 (ObjectId, ValidFrom) AS (SELECT al.ObjectId, MAX(al.ValidFrom) AS ValidFrom FROM AuditLog al JOIN #ids ON #ids.ObjectId = al.RootObjectId WHERE (al.ValidFrom <= @p0 OR al.Action IN (0,1)) GROUP BY al.ObjectId), " +
      "     CTE2 (ObjectId, ValidFrom, Tid) AS (SELECT al.ObjectId, al.ValidFrom, MAX(al.Tid) FROM AuditLog al JOIN #ids ON #ids.ObjectId = al.RootObjectId WHERE (al.ValidFrom <= @p0 OR al.Action IN (0,1)) GROUP BY al.ObjectId, al.ValidFrom) " +
      "SELECT al.Tid,al.ObjectId,al.RootObjectId,al.ParentObjectId,al.EntityId,al.ValidFrom,al.Action,al.ObjectDelta,al.IsArchived FROM AuditLog al " +
      "INNER JOIN CTE1 ON CTE1.ObjectId = al.ObjectId AND CTE1.ValidFrom = al.ValidFrom " +
      "INNER JOIN CTE2 ON CTE2.ObjectId = al.ObjectId AND CTE2.Tid = al.Tid ";

    #endregion

    #region Constructors

    /// <summary>
    /// Initialize a new instance that can be used to query using LINQ
    /// </summary>
    /// <param name="validFrom"></param>
    /// <param name="eagerFetch"></param>
    public EntityHistoryValidFromContext(DateTime validFrom, bool eagerFetch = false)
      : base(eagerFetch)
    {
      //if (validFrom > DateTime.Today)
      //{
      //  throw new ArgumentException("Invalid validFrom : cannot be greater than today");
      //}

      ValidFrom = validFrom;
    }

    /// <summary>
    /// Initialize new instance using pre-loaded list of audit logs
    /// </summary>
    /// <param name="validFrom"></param>
    /// <param name="auditLogs"></param>
    /// <remarks>
    /// The <see cref="EntityHistoryValidFromCache"/> will bulk load all the driving audit logs required by all its contexts.
    /// When constructing a context it will provide the logs for the context to avoid having to reload them from the database.
    /// </remarks>
    public EntityHistoryValidFromContext(DateTime validFrom, ICollection<AuditLog> auditLogs)
      : base(auditLogs)
    {
      //if (validFrom > DateTime.Today)
      //{
      //  throw new ArgumentException("Invalid validFrom : cannot be greater than today");
      //}
  
      ValidFrom = validFrom;
    }

    /// <summary>
    /// Initialize a new instance and pre-load all active audit logs on validFrom with one of the specified rootObjectIds
    /// </summary>
    /// <param name="validFrom"></param>
    /// <param name="rootObjectIds"></param>
    public EntityHistoryValidFromContext(DateTime validFrom, IList<long> rootObjectIds)
    {
      //if (validFrom > DateTime.Today)
      //{
      //  throw new ArgumentException("Invalid validFrom : cannot be greater than today");
      //}

      ValidFrom = validFrom;

      foreach (var auditLog in LoadAuditLogsForRootObjectIds(rootObjectIds, RootObjectIdSql, validFrom))
      {
        Add(auditLog);
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the ValidFrom
    /// </summary>
    public DateTime ValidFrom { get; }

    private static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1);
 
    #endregion

    #region Overridden Methods

    /// <summary>
    /// Load the <see cref="AuditLog"/> active version for the specified objectId
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    protected override AuditLog LoadAuditLog(long objectId)
    {
      var validFrom = ValidFrom == DateTime.MinValue ? SqlMinDate : ValidFrom;

      if (Logger.IsInfoEnabled)
      {
        Logger.InfoFormat("Lazy loading AuditLog for {0} [{1}] with ValidFrom [{2:yyyy-MM-dd}]", EntityHelper.GetClassFromObjectId(objectId).Name, objectId, validFrom);
      }

      if (Logger.IsVerboseEnabled())
      {
        Logger.Verbose(new StackTrace(true).ToString());
      }

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.CommandText = ObjectIdSql;
        AddParameter(cmd, "p0", validFrom);
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
    /// Load <see cref="AuditLog">audit logs</see> for all active versions for specified entityId
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="conn"></param>
    /// <returns></returns>
    protected override IEnumerable<AuditLog> LoadAuditLogsForEntityId(IDbConnection conn, int entityId)
    {
      var validFrom = ValidFrom == DateTime.MinValue ? SqlMinDate : ValidFrom;

      var classMeta = ClassCache.Find(entityId);
      if (classMeta == null)
      {
        throw new ArgumentException(string.Format("No ClassMeta found for entityId={0}", entityId));
      }

      Logger.DebugFormat("ENTER LoadAuditLogsForEntityId|{0}|{1:yyyyMMdd}", classMeta, ValidFrom);

      var auditLogs = new List<AuditLog>();

      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.CommandText = EntityIdSql;
        AddParameter(cmd, "p0", validFrom);
        AddParameter(cmd, "p1", entityId);
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
            auditLogs.Add(EntityHistoryCache.ReadAuditLog(reader));
        }

      }

      Logger.DebugFormat("EXIT LoadAuditLogsForEntityId|{0}|{1:yyyyMMdd}|{2}", classMeta, ValidFrom, auditLogs.Count);

      return auditLogs;
    }

    #endregion
  }
}