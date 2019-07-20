// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class EntityHistoryCache : IDisposable
  {
    #region Data

    /// <summary>
    /// 
    /// </summary>
    protected static readonly XmlWriterSettings XmlWriterSettings = new XmlWriterSettings
    {
      CloseOutput = true,
      ConformanceLevel = ConformanceLevel.Fragment,
      Indent = false,
      NewLineHandling = NewLineHandling.Entitize,
      OmitXmlDeclaration = true,
    };

    private readonly Dictionary<long, SortedDictionary<DateTime, SortedDictionary<int, AuditLog>>> _byEntityMap =
      new Dictionary<long, SortedDictionary<DateTime, SortedDictionary<int, AuditLog>>>();

    /// <summary>
    /// 
    /// </summary>
    protected const string AuditNamespace = "http://WebMathTrainingsolutions.com/Audit";

    internal static readonly ISet<int> RootEntityIds = new HashSet<int>(
      ClassCache.FindAll().Where(cm => cm.EntityId != 0 && cm.IsRootEntity).Select(cm => cm.EntityId));

    #endregion

    #region Implementation of IDisposable

    public abstract void Dispose();

    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    protected Dictionary<long, SortedDictionary<DateTime, SortedDictionary<int, AuditLog>>> ByEntityMap
    {
      get { return _byEntityMap; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Get distinct set of cached root object ids
    /// </summary>
    /// <returns></returns>
    public abstract IList<long> GetRootObjectIds();

    /// <summary>
    /// Get all cached audit logs
    /// </summary>
    /// <returns>List of audit logs</returns>
    public abstract IList<AuditLog> GetAuditLogs();

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
        Tid = (int)reader["TId"],
        ObjectId = (long)reader["ObjectId"],
        RootObjectId = (long)reader["RootObjectId"],
        ParentObjectId = (long)reader["ParentObjectId"],
        EntityId = (int)reader["EntityId"],
        ValidFrom = validFrom,
        Action = (ItemAction)reader["Action"],
        ObjectDelta = DBNull.Value.Equals(reader["ObjectDelta"]) ? null : (byte[])reader["ObjectDelta"],
        IsArchived = (bool)reader["IsArchived"]
      };
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual void Clear()
    {
      _byEntityMap.Clear();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected static IEnumerable<AuditLog> LoadAuditLogs()
    {
      var auditLogs = new List<AuditLog>();

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Tid,ObjectId,RootObjectId,ParentObjectId,EntityId,ValidFrom,Action,ObjectDelta,IsArchived FROM AuditLog";
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
            auditLogs.Add(ReadAuditLog(reader));
        }
      }

      return auditLogs;
    }

    /// <summary>
    /// Get all active versions of any entity with one of the specified rootObjectIds
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="sql"></param>
    /// <returns></returns>
    protected IEnumerable<AuditLog> LoadAuditLogs(int entityId, string sql)
    {
      var auditLogs = new List<AuditLog>();

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.CommandText = sql;
        AddParameter(cmd, "p0", entityId);
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
            auditLogs.Add(ReadAuditLog(reader));
        }
      }

      return auditLogs;
    }

    /// <summary>
    /// Get all active versions of any entity with one of the specified rootObjectIds
    /// </summary>
    /// <param name="rootObjectIds"></param>
    /// <param name="sql"></param>
    /// <returns></returns>
    protected IEnumerable<AuditLog> LoadAuditLogs(IList<long> rootObjectIds, string sql)
    {
      var auditLogs = new List<AuditLog>();

      if (rootObjectIds.Any())
      {
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
      }

      return auditLogs;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <returns></returns>
    protected static IEnumerable<AuditLog> LoadAuditLogs(long rootObjectId)
    {
      var auditLogs = new List<AuditLog>();

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Tid,ObjectId,RootObjectId,ParentObjectId,EntityId,ValidFrom,Action,ObjectDelta,IsArchived FROM AuditLog WHERE RootObjectId=@p0";
        AddParameter(cmd, "p0", rootObjectId);
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
            auditLogs.Add(ReadAuditLog(reader));
        }
      }

      return auditLogs;
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

    /// <summary>
    /// Return a new AuditLog instance with data from the specified <see cref="System.Data.IDataReader">reader</see>
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    protected static CommitLog ReadCommitLog(IDataReader reader)
    {
      return new CommitLog
      {
        Tid = reader.GetInt32(0),
        LastUpdated = reader.GetUtcDateTime(1),
        UpdatedBy = reader.GetInt64(2),
        Comment = reader[3] == DBNull.Value ? null : reader.GetString(3),
      };
    }

    /// <summary>
    /// Writes audit history XML.
    /// </summary>
    /// <param name="auditLogs"></param>
    /// <param name="prevContext"></param>
    /// <param name="nextContext"></param>
    /// <returns></returns>
    protected string WriteAuditHistoryXml(IList<AuditLog> auditLogs, ILoadableEntityContext prevContext, ILoadableEntityContext nextContext)
    {
      var sb = new StringBuilder();
      using (var xmlWriter = XmlWriter.Create(sb, XmlWriterSettings))
      {
        xmlWriter.WriteStartElement("AuditHistory", AuditNamespace);
        xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

        foreach (var auditLog in Sort(auditLogs))
        {
          using (var historyWriter = new AuditHistoryWriter(xmlWriter, prevContext, nextContext))
          {
            var oldState = prevContext == null || auditLog.Action == ItemAction.Added ? null : prevContext.Get(auditLog.ObjectId);
            var newState = nextContext == null || auditLog.Action == ItemAction.Removed ? null : nextContext.Get(auditLog.ObjectId);
            var objectDelta = (ObjectDelta)ClassMeta.CreateDelta(oldState, newState);
            if (objectDelta != null)
              historyWriter.WriteDelta(objectDelta);
          }
        }

        xmlWriter.WriteEndElement();
      }

      return sb.ToString();
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
    /// 
    /// </summary>
    /// <param name="list"></param>
    /// <returns></returns>
    private static List<AuditLog> Sort(IList<AuditLog> list)
    {
      var map = list.ToDictionary(item => item.ObjectId);
      var parentMap = list.ToDictionary(item => item.ObjectId, item => GetParent(map, item.ParentObjectId));
      var graph = new DependencyGraph<AuditLog>(list, at => parentMap[at.ObjectId]);
      return graph.ToList();
    }

    private static AuditLog[] GetParent(IDictionary<long, AuditLog> map, long parentObjectId)
    {
      AuditLog parent;
      return map.TryGetValue(parentObjectId, out parent) ? new[] { parent } : new AuditLog[0];
    }

    #endregion
  }
}