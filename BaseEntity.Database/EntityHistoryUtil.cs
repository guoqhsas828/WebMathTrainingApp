using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using log4net;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityHistoryUtil
  {
    #region Data

    private readonly ILog _logger = LogManager.GetLogger(typeof(EntityHistoryUtil));
    private readonly Lazy<EntityHistoryTidCache> _tidCache = new Lazy<EntityHistoryTidCache>(InitializeTidCache);
    private readonly Lazy<EntityHistoryValidFromCache> _validFromCache = new Lazy<EntityHistoryValidFromCache>(InitializeValidFromCache);

    private static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1);

    #endregion

    /// <summary>
    /// 
    /// </summary>
    public Action<string> PostStatus { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Action<int, int> PostProgress { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Action<string> PostVerboseStatus { get; set; }

    #region Private Helper Methods

    private static EntityHistoryTidCache InitializeTidCache()
    {
      return new EntityHistoryTidCache(DateTime.MinValue, DateTime.MaxValue);
    }

    private static IList<long> GetRootObjectidsFromAuditLogs()
    {
      // Return only root objects with AuditPolicy = AuditPolicy.History
      IList<int> entityIdsWithHistory =
        ClassCache.FindAll().Where(cm => cm.IsRootEntity && cm.AuditPolicy == AuditPolicy.History).Select(cm => cm.EntityId).ToList();
      IList<long> rootObjectIds = new List<long>();
      using (var conn = new RawConnection())
      {
        using (var cmd = conn.CreateCommand())
        {
          cmd.CommandText = "SELECT Distinct ObjectId, EntityId FROM AuditLog WHERE RootObjectId = ObjectId";
          using (var reader = cmd.ExecuteReader())
          {
            while (reader.Read())
            {
              long rootObjectId = reader.GetInt64(0);
              int entityId = reader.GetInt32(1);
              if (entityIdsWithHistory.Contains(entityId))
                rootObjectIds.Add(rootObjectId);
            }
          }
        }
      }
      return rootObjectIds;
    }

    private static EntityHistoryValidFromCache InitializeValidFromCache()
    {
      IList<long> rootObjectIds = GetRootObjectidsFromAuditLogs();
      return new EntityHistoryValidFromCache(rootObjectIds);
    }

    private void CommitOrRollback(bool commit, IDbTransaction tran)
    {
      if (commit)
        tran.Commit();
      else
        tran.Rollback();
    }

    private void CreateObjectIdJoinTable(SqlConnection conn, SqlTransaction tran, IEnumerable<long> objectIds)
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

    private void DropObjectIdJoinTable(IDbConnection conn, IDbTransaction tran)
    {
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandTimeout = 0;
        cmd.Transaction = tran;
        cmd.CommandText = "DROP TABLE #ids";
        cmd.ExecuteNonQuery();
      }
    }

    private void AddParameter(IDbCommand cmd, string paramName, object paramValue)
    {
      var p = cmd.CreateParameter();
      p.ParameterName = paramName;
      p.Value = paramValue;
      cmd.Parameters.Add(p);
    }

    private int InsertCommitLog(SqlConnection conn, SqlTransaction tran)
    {
      var cmd = conn.CreateCommand();
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.CommandText = "InsertCommitLog";
      cmd.Transaction = tran;
      var userParam = new SqlParameter("@userId", SqlDbType.BigInt);
      var commentParam = new SqlParameter("@comment", SqlDbType.NVarChar, 140);
      var transactionIdParam = new SqlParameter("@transactionId", SqlDbType.UniqueIdentifier);
      var resultParam = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };

      cmd.Parameters.Add(userParam);
      cmd.Parameters.Add(commentParam);
      cmd.Parameters.Add(transactionIdParam);
      cmd.Parameters.Add(resultParam);

      userParam.Value = EntityContextFactory.User.ObjectId;
      commentParam.Value = DBNull.Value;
      transactionIdParam.Value = DBNull.Value;

      cmd.ExecuteNonQuery();

      return (int)resultParam.Value;
    }

    private DataTable CreateAuditLogDataTable()
    {
      var table = new DataTable("AuditLog");

      table.Columns.AddRange(
        new[]
        {
          new DataColumn("Tid", typeof(int)),
          new DataColumn("ObjectId", typeof(long)),
          new DataColumn("RootObjectId", typeof(long)),
          new DataColumn("ParentObjectId", typeof(long)),
          new DataColumn("EntityId", typeof(int)),
          new DataColumn("ValidFrom", typeof(DateTime)),
          new DataColumn("Action", typeof(int)),
          new DataColumn("ObjectDelta", typeof(byte[])),
          new DataColumn("IsArchived", typeof(bool)),
        });

      return table;
    }

    private void CreateAuditLogRow(DataTable table, int tid, ClassMeta cm, AuditedObject ao, DateTime validFrom)
    {
      byte[] bytes;
      using (var stream = new MemoryStream())
      {
        using (var writer = new BinaryEntityWriter(stream))
        {
          writer.WriteEntity(ao);
        }
        bytes = stream.ToArray();
      }

      var row = table.NewRow();
      row["Tid"] = tid;
      row["ObjectId"] = ao.ObjectId;
      row["RootObjectId"] = cm.IsRootEntity ? ao.ObjectId : 0;
      row["ParentObjectId"] = 0;
      row["EntityId"] = cm.EntityId;
      row["ValidFrom"] = validFrom == DateTime.MinValue ? SqlMinDate : validFrom;
      row["Action"] = Convert.ToInt32(ItemAction.Added);
      row["ObjectDelta"] = bytes;
      row["IsArchived"] = 0;
      table.Rows.Add(row);
    }

    private void BulkInsert(SqlConnection conn, SqlTransaction tran, DataTable table)
    {
      if (table.Rows.Count > 0)
      {
        var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tran)
        {
          DestinationTableName = table.TableName,
          BulkCopyTimeout = SessionFactory.CommandTimeout
        };

        bulkCopy.WriteToServer(table);
      }
    }

    private void GetRootAndParent(PersistentObject po, long rootObjectId, IDictionary<long, long> rootObjMap, IDictionary<long, long> parentObjMap)
    {
      var cm = ClassCache.Find(po);
      foreach (var childObj in cm.CascadeList.Where(c => c.Cascade != "none" && c.ReferencedEntity.IsChildEntity).SelectMany(c => c.ReferencedObjects(po)))
      {
        rootObjMap[childObj.ObjectId] = rootObjectId;
        parentObjMap[childObj.ObjectId] = po.ObjectId;
        GetRootAndParent(childObj, rootObjectId, rootObjMap, parentObjMap);
      }
    }

    private void UpdateChildAuditLogs(IDictionary<long, long> rootObjMap, IDictionary<long, long> parentObjMap, bool commit)
    {
      var conn = (SqlConnection)Session.OpenConnection();
      try
      {
        foreach (var kvp in rootObjMap.Keys.GroupBy(EntityHelper.GetClassFromObjectId).ToDictionary(g => g.Key.Name, g => g.ToList()))
        {
          _logger.InfoFormat("Setting RootObjectId and ParentObjectId for {0} {1} audit logs", kvp.Value.Count, kvp.Key);
          
          using (var tran = conn.BeginTransaction())
          {
            foreach (var objectId in kvp.Value)
            {
              using (var cmd = conn.CreateCommand())
              {
                cmd.Transaction = tran;
                cmd.CommandTimeout = 0;
                cmd.CommandText = "UPDATE AuditLog SET RootObjectId=@p0, ParentObjectId=@p1 WHERE ObjectId=@p2 AND ParentObjectId=0";
                AddParameter(cmd, "p0", rootObjMap[objectId]);
                AddParameter(cmd, "p1", parentObjMap[objectId]);
                AddParameter(cmd, "p2", objectId);
                cmd.ExecuteNonQuery();
              }
            }

            CommitOrRollback(commit, tran);
          }
        }
      }
      finally
      {
        Session.CloseConnection(conn);
      }
    }

    private bool CanInitializeAuditLogs(ClassMeta cm)
    {
      if (cm.AuditPolicy != AuditPolicy.History)
      {
        _logger.ErrorFormat("Invalid entity [" + cm.Name + "] : AuditPolicy = " + cm.AuditPolicy);
        return false;
      }
      if (cm.IsChildEntity)
      {
        _logger.ErrorFormat("Invalid entity [" + cm.Name + "] : cannot be child entity");
        return false;
      }
      return true;
    }

    #endregion

    /// <summary>
    /// 
    /// </summary>
    /// <param name="typeName"></param>
    /// <param name="initializeRelatedObjects"></param>
    /// <param name="commit"></param>
    /// <param name="forceInception"></param>
    /// <returns></returns>
    public int InitializeOrReinitializeAuditLogs(string typeName, bool initializeRelatedObjects, bool commit, bool forceInception)
    {
      var cm = ClassCache.Find(typeName);
      if (cm == null)
      {
        _logger.ErrorFormat("Invalid entity [" + typeName + "] : not found");
        return -1;
      }

      if (!CanInitializeAuditLogs(cm))
        return -1;

      var objectIds = new List<long>();

      using (new SessionBinder())
      {
        objectIds.AddRange(Session.Find($"Select ObjectId from {cm.Name}").Cast<long>());
      }

      return InitializeOrReinitializeAuditLogs(cm, objectIds, initializeRelatedObjects, commit, forceInception);
    }

    /// <summary>
   /// 
   /// </summary>
   /// <param name="objectId"></param>
   /// <param name="initializeRelatedObjects"></param>
   /// <param name="commit"></param>
   /// <param name="forceInception"></param>
   /// <returns></returns>
    public int InitializeOrReinitializeAuditLogs(long objectId, bool initializeRelatedObjects, bool commit, bool forceInception)
    {
      var cm = ClassCache.Find(objectId);
      if (cm == null)
      {
        _logger.ErrorFormat("Invalid objectId [" + objectId + "]");
        return -1;
      }
      if (!CanInitializeAuditLogs(cm))
        return -1;
      return InitializeOrReinitializeAuditLogs(cm, new[] {objectId}, initializeRelatedObjects, commit, forceInception);
    }

    private int InitializeOrReinitializeAuditLogs(ClassMeta cm, IList<long> objectIds, bool initializeRelatedObjects, bool commit, bool forceInception)
    {
      int result = 0;
      if (objectIds.Count == 0)
        return result;

      PostStatus?.Invoke($"Processing {cm.Name}...");

      using (new SessionBinder())
      {
        var conn = (SqlConnection)Session.OpenConnection();

        try
        {
          var rootObjMap = new Dictionary<long, long>();
          var parentObjMap = new Dictionary<long, long>();
          var rootObjects = new List<AuditedObject>();

          using (var tran = conn.BeginTransaction())
          {
            var tid = InsertCommitLog(conn, tran);

            using (DataTable table = CreateAuditLogDataTable())
            {
              int cnt = 1;
              foreach (long objectId in objectIds)
              {
                PostProgress?.Invoke(cnt++, objectIds.Count);

                var po = Session.Get(objectId);
                if (po == null)
                {
                  _logger.ErrorFormat("No {0} found with ObjectId [{1}]", cm.Name, objectId);
                  result = -1;
                  continue;
                }

                PostVerboseStatus?.Invoke($"Create initial history record(s) for {po.FormKey()}");

                IEnumerable<AuditedObject> ownedObjects;
                if (initializeRelatedObjects)
                {
                  var walker = new OwnedOrRelatedObjectWalker(true);
                  walker.Walk(po);
                  ownedObjects = walker.OwnedObjects.OfType<AuditedObject>();
                }
                else
                {
                  var walker = new OwnedObjectWalker(true);
                  walker.Walk(po);
                  ownedObjects = walker.OwnedObjects.OfType<AuditedObject>();
                }

                foreach (AuditedObject ao in ownedObjects)
                {
                  ClassMeta aocm = ClassCache.Find(ao);

                  if (forceInception)
                  {
                    // This is not commited to the database
                    if (!aocm.OldStyleValidFrom)
                      ao.ValidFrom = DateTime.MinValue;
                    ao.ObjectVersion = 1;
                  }

                  // Delete Audit Logs for Owned Objects
                  using (var cmd = conn.CreateCommand())
                  {
                    cmd.Transaction = tran;
                    cmd.CommandTimeout = 0;
                    cmd.CommandText = "DELETE AuditLog where ObjectId = " + ao.ObjectId;
                    cmd.ExecuteNonQuery();
                  }

                  // Create Audit Logs for the Owned Object
                  CreateAuditLogRow(table, tid, aocm, ao, ao.ValidFrom);

                  if (aocm.IsRootEntity)
                  {
                    // Delete Audit History for Owned Objects
                    using (var cmd = conn.CreateCommand())
                    {
                      cmd.Transaction = tran;
                      cmd.CommandTimeout = 0;
                      cmd.CommandText = "DELETE AuditHistory where RootObjectId = " + ao.ObjectId;
                      cmd.ExecuteNonQuery();
                    }

                    rootObjects.Add(ao);
                  }
                }
              }

              BulkInsert(conn, tran, table);
            }

            CommitOrRollback(commit, tran);
          }

          foreach (var ro in rootObjects)
          {
            GetRootAndParent(ro, ro.ObjectId, rootObjMap, parentObjMap);
          }

          UpdateChildAuditLogs(rootObjMap, parentObjMap, commit);

          if (forceInception)
            ResetAuditProperties(objectIds, initializeRelatedObjects, commit);
        }
        finally
        {
          Session.CloseConnection(conn);
        }
      }

      return result;
    }

    private void ResetAuditProperties(IList<long> rootObjectIds, bool initializeRelatedObjects, bool commit)
    {
      if (rootObjectIds.Count == 0)
        return;

      PostStatus?.Invoke("Resetting Audit Properties...");

      using (var conn = Session.OpenConnection())
      {
        using (var tran = conn.BeginTransaction())
        {
          int i= 1;

          foreach (long objectId in rootObjectIds)
          {
            PostProgress?.Invoke(i++, rootObjectIds.Count);

            var po = Session.Get(objectId);
            if (po == null)
              continue;

            IEnumerable<AuditedObject> ownedObjects;
            if (initializeRelatedObjects)
            {
              var walker = new OwnedOrRelatedObjectWalker(true);
              walker.Walk(po);
              ownedObjects = walker.OwnedObjects.OfType<AuditedObject>();
            }
            else
            {
              var walker = new OwnedObjectWalker(true);
              walker.Walk(po);
              ownedObjects = walker.OwnedObjects.OfType<AuditedObject>();
            }


            foreach (AuditedObject ao in ownedObjects)
            {
              ClassMeta aocm = ClassCache.Find(ao);

              ClassMeta cm = ClassCache.Find(ao);
              string tableName = cm.TableName;
              while (cm.IsDerivedEntity)
              {
                tableName = cm.BaseEntity.TableName;
                cm = cm.BaseEntity;
              }

              if (string.IsNullOrEmpty(tableName))
              {
                _logger.ErrorFormat("Cannot Update ValidFrom for {0}", aocm.Name);
              }
              else
              {
                using (var cmd = conn.CreateCommand())
                {
                  cmd.Transaction = tran;
                  cmd.CommandTimeout = 0;
                  if (aocm.OldStyleValidFrom)
                  {
                    cmd.CommandText = $"UPDATE [{tableName.Trim('`')}] SET ObjectVersion = 1 WHERE ObjectId = " + ao.ObjectId;
                  }
                  else
                  {
                    cmd.CommandText = $"UPDATE [{tableName.Trim('`')}] SET ValidFrom = @p0, ObjectVersion = 1 WHERE ObjectId = " + ao.ObjectId;
                    AddParameter(cmd, "p0", SqlMinDate);
                  }
                  cmd.ExecuteNonQuery();
                }
              }
            }
          }

          CommitOrRollback(commit, tran);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="commit"></param>
    /// <param name="forceInception"></param>
    /// <returns></returns>
    public int InitializeOrReinitializeAuditLogs(bool commit, bool forceInception)
    {
      foreach (var cm in ClassCache.FindAll().Where(cm => cm.IsEntity && !cm.IsChildEntity && cm.EntityId != 0 && cm.AuditPolicy == AuditPolicy.History).OrderBy(cm => cm.Name))
      {
        InitializeOrReinitializeAuditLogs(cm.Name, false, commit, forceInception);
      }

      return 0;
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<long> GetObjectsWithNoInitialAuditLog(bool returnRootObjectIds = false)
    {
      var invalidIds = new HashSet<long>();

      using (new SessionBinder())
      using (var conn = new RawConnection())
      {
        using (var tran = conn.Connection.BeginTransaction())
        {
          var ids = new HashSet<long>();
          foreach (var cm in ClassCache.FindAll().Where(
            cm => (cm.IsBaseEntity || cm.IsStandaloneEntity) && cm.AuditPolicy == AuditPolicy.History && typeof(AuditedObject).IsAssignableFrom(cm.Type)))
          {
            using (var cmd = conn.CreateCommand())
            {
              cmd.Transaction = tran;
              cmd.CommandText = $"SELECT ObjectId FROM [{cm.TableName.Trim('`')}]";
              using (var reader = cmd.ExecuteReader())
              {
                while (reader.Read())
                  ids.Add((long)reader[0]);
              }
            }
          }

          CreateObjectIdJoinTable(conn.Connection, tran, ids);

          using (var cmd = conn.CreateCommand())
          {
            cmd.Transaction = tran;
            cmd.CommandText = "SELECT ObjectId FROM #ids where ObjectId NOT IN (SELECT ObjectId FROM AuditLog) ORDER BY ObjectId";
            using (var reader = cmd.ExecuteReader())
            {
              while (reader.Read())
              {
                var id = (long)reader[0];
                var po = Session.Get(id);
                if (po == null)
                  _logger.ErrorFormat(@"No audit history found for: {0}|{1}", EntityHelper.GetClassFromObjectId(id), id);
                else
                  _logger.ErrorFormat(@"No audit history found for: {0}", po.FormKey());
                invalidIds.Add(id);
              }
            }
          }

          DropObjectIdJoinTable(conn, tran);
        }
      }

      if (returnRootObjectIds)
      {
        var rootObjectIds = new HashSet<long>();

        foreach (long id in invalidIds)
        {
          ClassMeta cm = ClassCache.Find(id);
          if (cm.IsRootEntity)
          {
            rootObjectIds.Add(id);
          }
          else
          {
            foreach (ClassMeta classMeta in ClassCache.FindAll().Where(_ => _.IsRootEntity))
            {
              if (HasReference(classMeta, cm.Type))
              {
                foreach (PersistentObject rootObj in Session.Find($"from {classMeta.Type.Name}"))
                {
                  var walker = new OwnedObjectWalker();
                  walker.Walk(rootObj);

                  if (walker.OwnedObjects.Select(o => o.ObjectId).Contains(id))
                    rootObjectIds.Add(rootObj.ObjectId);
                }
              }
            }
          }
        }

        return rootObjectIds.ToList();
      }

      return invalidIds.ToList();
    }

    private static bool HasReference(ClassMeta cm, Type type)
    {
      foreach (PropertyMeta pm in cm.PropertyList)
      {
        var ccpm = pm as ComponentCollectionPropertyMeta;
        if (ccpm != null)
        {
          if (ccpm.Clazz.IsAssignableFrom(type) || HasReference(ClassCache.Find(ccpm.Clazz), type))
            return true;
          continue;
        }

        var mmpm = pm as ManyToManyPropertyMeta;
        if (mmpm != null)
        {
          if (mmpm.IsChild && (mmpm.Clazz.IsAssignableFrom(type) || HasReference(ClassCache.Find(mmpm.Clazz), type)))
            return true;
          continue;
        }

        var ompm = pm as OneToManyPropertyMeta;
        if (ompm != null)
        {
          if (ompm.IsChild && (ompm.Clazz.IsAssignableFrom(type) || HasReference(ClassCache.Find(ompm.Clazz), type)))
            return true;
          continue;
        }

        var oopm = pm as OneToOnePropertyMeta;
        if (oopm != null)
        {
          if (oopm.Clazz.IsAssignableFrom(type) || HasReference(ClassCache.Find(oopm.Clazz), type))
            return true;
        }
      }

      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<long> GetObjectsWithInconsistentAggregates()
    {
      var invalidObjectIds = new HashSet<long>();
      var byTidMap = new ConcurrentDictionary<int, ConcurrentDictionary<long, IList<AuditLog>>>();

      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Tid,ObjectId,RootObjectId,ParentObjectId,EntityId,ValidFrom,Action,ObjectDelta,IsArchived FROM AuditLog";
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            var objectId = (long)reader[1];
            var cm = ClassCache.Find(objectId);
            if (cm.IsRootEntity && cm.CascadeList.All(c => c.Cascade != "none")) continue;
            var auditLog = EntityHistoryCache.ReadAuditLog(reader);
            var byRootObjectIdMap = byTidMap.GetOrAdd(auditLog.Tid, tid => new ConcurrentDictionary<long, IList<AuditLog>>());
            var auditLogs = byRootObjectIdMap.GetOrAdd(auditLog.RootObjectId, id => new List<AuditLog>());
            auditLogs.Add(auditLog);
          }
        }
      }

      foreach (var tid in byTidMap.Keys)
      {
        var byRootObjectIdMap = byTidMap[tid];
        foreach (var rootObjectId in byRootObjectIdMap.Keys)
        {
          var auditLogs = byRootObjectIdMap[rootObjectId];
          
          var validFrom = auditLogs[0].ValidFrom;
          if (auditLogs.Any(al => al.ValidFrom != validFrom))
          {
            _logger.ErrorFormat("Not all entities with RootObjectId={0} and Tid={1} have same value for ValidFrom", rootObjectId, tid);
            invalidObjectIds.Add(rootObjectId);
          }

          var isArchived = auditLogs[0].IsArchived;
          if (auditLogs.Any(al => al.IsArchived != isArchived))
          {
            _logger.ErrorFormat("Not all entities with RootObjectId={0} and Tid={1} have same value for IsArchived", rootObjectId, tid);
            invalidObjectIds.Add(rootObjectId);
          }
        }
      }

      return invalidObjectIds.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<long> FixIncorrectInitialAuditLog(bool commit)
    {
      var ids = new HashSet<long>();

      var deleteLog = new Dictionary<long, int>();
      var markArchived = new Dictionary<long, int>();
      var resetAction = new Dictionary<long, int>();
      
      using (var conn = new RawConnection())
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = "SELECT Tid,EntityId,ObjectId,Action,IsArchived FROM AuditLog ORDER BY Tid,ObjectId";
        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            var tid = (int)reader[0];
            var entityId = (int)reader[1];
            var objectId = (long)reader[2];
            var action = (int)reader[3];
            var archived = (bool)reader[4];
            if (ids.Contains(objectId))
            {
              if (action == 0)
              {
                _logger.ErrorFormat("Invalid ItemAction [0] for non-initial entry for {0}", objectId);
              }
            }
            else
            {
              ids.Add(objectId);

              var cm = ClassCache.Find(entityId);

              if (action == 0)
              {
                if (!archived)
                  markArchived.Add(objectId, tid);
              }
              else if (action == 2)
              {
                _logger.ErrorFormat("Initial AuditLog for {0}|{1} has incorrect Action = Changed", cm.Name, objectId);
                resetAction.Add(objectId, tid);
                markArchived.Add(objectId, tid);
              }
              else if (action == 3)
              {
                _logger.ErrorFormat("Initial AuditLog for {0}|{1} has incorrect Action = Deleted", cm.Name, objectId);
                deleteLog.Add(objectId, tid);
              }
            }
          }
        }
      }

      if (resetAction.Any())
      {
        _logger.ErrorFormat("Found {0} initial state audit logs with Action=2 : settting Action=0", resetAction.Count);

        using (var conn = new RawConnection())
        using (var tran = conn.Connection.BeginTransaction())
        {
          foreach (var kvp in resetAction)
          {
            using (var cmd = conn.CreateCommand())
            {
              cmd.Transaction = tran;
              cmd.CommandText = "UPDATE AuditLog SET Action=0 WHERE ObjectId=@p0 AND Tid=@p1";
              AddParameter(cmd, "p0", kvp.Key);
              AddParameter(cmd, "p1", kvp.Value);
              cmd.ExecuteNonQuery();
            }
          }

          CommitOrRollback(commit, tran);
        }
      }

      if (markArchived.Any())
      {
        _logger.ErrorFormat("Setting IsArchived=1 for {0} audit logs where Action=0", markArchived.Count);

        using (var conn = new RawConnection())
        using (var tran = conn.Connection.BeginTransaction())
        using (var cmd = conn.CreateCommand())
        {
          cmd.Transaction = tran;
          cmd.CommandText = "UPDATE AuditLog SET IsArchived=1 WHERE Action=0 AND IsArchived=0";
          cmd.ExecuteNonQuery();
          CommitOrRollback(commit, tran);
        }
      }

      if (deleteLog.Any())
      {
        _logger.ErrorFormat("Deleting {0} initial state audit logs with Action=3", deleteLog.Count);

        using (var conn = new RawConnection())
        using (var tran = conn.Connection.BeginTransaction())
        {
          foreach (var kvp in deleteLog)
          {
            using (var cmd = conn.CreateCommand())
            {
              cmd.Transaction = tran;
              cmd.CommandText = "DELETE AuditLog WHERE ObjectId=@p0 AND Tid=@p1";
              AddParameter(cmd, "p0", kvp.Key);
              AddParameter(cmd, "p1", kvp.Value);
              cmd.ExecuteNonQuery();
            }
          }

          CommitOrRollback(commit, tran);
        }
      }

      var invalidObjectIds = new HashSet<long>();
      invalidObjectIds.UnionWith(resetAction.Keys);
      invalidObjectIds.UnionWith(markArchived.Keys);
      invalidObjectIds.UnionWith(deleteLog.Keys);
      return invalidObjectIds.ToList();
    }
    
    /// <summary>
    /// 
    /// </summary>
    public IList<long> GetInvalidObjectDeltasByTid()
    {
      var invalidObjects = new HashSet<long>();
      var entityCache = _tidCache.Value;
      foreach (int tid in entityCache.GetTids())
      {
        foreach (var auditLog in entityCache.GetAuditLogs(tid).Where(al => al.Action != ItemAction.Removed))
        {
          var cm = ClassCache.Find(auditLog.EntityId);
          if (cm.AuditPolicy == AuditPolicy.History)
          {
            try
            {
              var entity = entityCache.GetEntity(auditLog.ObjectId, tid);
              entity?.ResolveAll();
            }
            catch (Exception ex)
            {
              _logger.ErrorFormat("Unable to ResolveAll on [{0}:{1}] : {2}", EntityHelper.GetClassFromObjectId(auditLog.ObjectId), auditLog.ObjectId, ex.Message);
              invalidObjects.Add(auditLog.ObjectId);
            }
          }
        }
      }
      return invalidObjects.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<long> GetInvalidObjectDeltasByValidFrom()
    {
      // For each date, check if we can resolve the root objects
      var entityCache = _validFromCache.Value;
      var invalidObjects = new HashSet<long>();

      foreach (long rootObjectId in entityCache.GetRootObjectIds())
      {
        foreach (DateTime validFrom in entityCache.GetDates(rootObjectId))
        {
          foreach (var auditLog in entityCache.GetAuditLogs(rootObjectId, validFrom).Where(al => al.Action != ItemAction.Removed))
          {
            try
            {
              var entity = entityCache.GetEntity(auditLog.ObjectId, validFrom);
              entity?.ResolveAll();
            }
            catch (Exception ex)
            {
              _logger.ErrorFormat(@"Unable to ResolveAll on [{0}:{1}] as of {2}: {3}", EntityHelper.GetClassFromObjectId(rootObjectId).Name, rootObjectId,
                validFrom.ToShortDateString(), ex.Message);
              invalidObjects.Add(rootObjectId);
            }
          }
        }
      }

      return invalidObjects.ToList();
    }

    /// <summary>
    ///   Returns a list of Object Ids where the current state of the object in the database does not match the Audit History.
    /// </summary>
    public IList<long> GetInvalidCurrentStateObjects(string outputFile)
    {
      var invalidObjectIds = new HashSet<long>();
      var binder = new SessionBinder();

      var sb = new StringBuilder();
      XmlWriter xmlWriter = null;
      if (!string.IsNullOrWhiteSpace(outputFile))
      {
        xmlWriter = new XmlTextWriter(outputFile, Encoding.UTF8);
        xmlWriter.WriteStartElement("AuditHistory", "http://WebMathTrainingsolutions.com/Audit");
        xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
      }

      EntityHistoryValidFromCache entityCache = _validFromCache.Value;
      
      foreach (long rootObjectId in entityCache.GetRootObjectIds())
      {
        try
        {
          DateTime latestValidFrom = entityCache.GetDates(rootObjectId).Max();
          AuditLog log = entityCache.GetAuditLog(rootObjectId, latestValidFrom);

          // Do not continue to check because once the item is purged, 
          // the database will not have a record
          if (log.Action == ItemAction.Removed)
            continue;

          AuditHistoryWriter historyWriter = null;
          if (xmlWriter != null)
          {
            ILoadableEntityContext auditTableContext = latestValidFrom == DateTime.MinValue ? null : new EntityHistoryValidFromContext(latestValidFrom);
            historyWriter = new AuditHistoryWriter(xmlWriter, auditTableContext, Session.EntityContext);
          }


          PersistentObject entityFromDatabase = Session.Get(rootObjectId);
          if (entityFromDatabase == null)
          {
            _logger.ErrorFormat("Cannot find {0} with ObjetctId {1} in the database.", ClassCache.Find(rootObjectId).Type.Name, rootObjectId);
            invalidObjectIds.Add(rootObjectId);

            if (historyWriter != null)
            {
              PersistentObject objFromCache = entityCache.GetEntity(rootObjectId, latestValidFrom);
              var delta = (ObjectDelta)ClassMeta.CreateDelta(objFromCache, entityFromDatabase);
              historyWriter.WriteDelta(delta);
            }
          }
          else
          {
            var ownedObjWalker = new OwnedObjectWalker(true);
            ownedObjWalker.Walk(entityFromDatabase);
            foreach (PersistentObject ownedObjFromDb in ownedObjWalker.OwnedObjects)
            {
              PersistentObject ownedObjFromCache = entityCache.GetEntity(ownedObjFromDb.ObjectId, latestValidFrom);
              if (!ClassMeta.IsSame(ownedObjFromCache, ownedObjFromDb))
              {
                invalidObjectIds.Add(rootObjectId);

                if (historyWriter != null)
                {
                  var delta = (ObjectDelta)ClassMeta.CreateDelta(ownedObjFromCache, ownedObjFromDb);
                  historyWriter.WriteDelta(delta);
                }
              }
            }

            if (invalidObjectIds.Contains(rootObjectId))
            {
              _logger.ErrorFormat("Current state of {0} in database does not match latest audit history", Session.Get(rootObjectId).FormKey());
            }
          }

          historyWriter?.Dispose();
        }
        catch (Exception ex)
        {
          _logger.Error($@"Error checking {EntityHelper.GetClassFromObjectId(rootObjectId).Name} [{rootObjectId}], {ex.Message}");
        }
      }

      xmlWriter?.WriteEndElement();
      xmlWriter?.Flush();
      xmlWriter?.Close();
      xmlWriter?.Dispose();

      binder.Dispose();

      if (!string.IsNullOrWhiteSpace(outputFile))
      {
        if (!File.Exists(outputFile))
          File.Delete(outputFile);
        File.AppendAllText(outputFile, sb.ToString());
      }

      return invalidObjectIds.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<long> GetObjectsWithEmptyAuditLogs()
    {
      _logger.DebugFormat("Begin GetEmptyAuditLogs");

      var invalidObjectIds = new HashSet<long>();
      EntityHistoryTidCache entityCache = _tidCache.Value;

      foreach (long rootObjectId in entityCache.GetRootObjectIds())
      {
        ClassMeta cm = ClassCache.Find(rootObjectId);
        if (cm.AuditPolicy != AuditPolicy.History)
          continue;

        IList<int> tids = entityCache.GetTids(rootObjectId, false);
        int minTid = tids.Min();
        tids.Remove(minTid);

        foreach (int tid in tids)
        {
          int prevTid = entityCache.GetPriorVersionTid(rootObjectId, tid);
          if (prevTid == -1)
            continue;

          AuditLog log = entityCache.GetAuditLog(rootObjectId, tid);
          if (log.Action == ItemAction.Removed)
            continue;

          PersistentObject currentVersionObj = entityCache.GetEntity(rootObjectId, tid);

          var ownedObjectWalker = new OwnedObjectWalker(true);
          ownedObjectWalker.Walk(currentVersionObj);

          bool isSame = true;
          foreach (PersistentObject ownedObject in ownedObjectWalker.OwnedObjects)
          {
            PersistentObject curVer = entityCache.GetEntity(ownedObject.ObjectId, tid);
            PersistentObject priorVer = entityCache.GetEntity(ownedObject.ObjectId, prevTid);
            if (!ClassMeta.IsSame(priorVer, curVer, true))
            {
              isSame = false;
              break;
            }
          }

          if (isSame)
          {
            _logger.ErrorFormat($@"Empty AuditHistory for [{currentVersionObj.FormKey()}] with Tid [{tid}]");
            invalidObjectIds.Add(rootObjectId);
          }
        }
      }

      _logger.DebugFormat("End GetEmptyAuditLogs");
      return invalidObjectIds.ToList();
    }

    /// <summary>
    ///   Returns a list of Valid From Dates for the given rootObjectids which are after the asOf date.
    /// </summary>
    /// <param name="rootObjectIds"></param>
    /// <param name="asOf"></param>
    /// <returns></returns>
    public static IDictionary<long, IList<DateTime>> GetFutureValidFromDates(IEnumerable<long> rootObjectIds, DateTime asOf)
    {
      var datesPerObject = new Dictionary<long, IList<DateTime>>();

      using (var reader =
        Session.ExecuteReader(
          $"SELECT DISTINCT RootObjectId, ValidFrom FROM AuditLog WHERE RootObjectId IN ({string.Join(",", rootObjectIds)}) AND ValidFrom > @p0 ORDER BY RootObjectId, ValidFrom",
          "p0", asOf == DateTime.MinValue ? SessionFactory.SqlMinDateUtc : asOf))
      {
        while (reader.Read())
        {
          long rootObjectId = reader.GetInt64(0);
          DateTime validFrom = reader.GetDateTime(1);

          IList<DateTime> dates;
          if (!datesPerObject.TryGetValue(rootObjectId, out dates))
          {
            dates = new List<DateTime>();
            datesPerObject[rootObjectId] = dates;
          }
          dates.Add(validFrom);
        }
      }

      return datesPerObject;
    }

    /// <summary>
    ///   Returns a list of Valid From Dates for the given rootObjectid
    /// </summary>
    /// <param name="rootObjectIds"></param>
    /// <returns></returns>
    public static IList<DateTime> GetValidFromDates(IList<long> rootObjectIds)
    {
      var dates = new List<DateTime>();
      if (rootObjectIds.Any())
      {
        string sql = rootObjectIds.Count == 1
          ? $"SELECT DISTINCT ValidFrom FROM AuditLog WHERE RootObjectId = {rootObjectIds[0]} ORDER BY ValidFrom"
          : $"SELECT DISTINCT ValidFrom FROM AuditLog WHERE RootObjectId IN ({string.Join(",", rootObjectIds)}) ORDER BY ValidFrom";

        using (var reader = Session.ExecuteReader(sql))
        {
          while (reader.Read())
          {
            dates.Add(reader.GetDateTime(0));
          }
        }
      }
      return dates;
    }
  }
}
