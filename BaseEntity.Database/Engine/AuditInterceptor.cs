// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using log4net;
using NHibernate;
using NHibernate.Type;
using BaseEntity.Metadata;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// Used by persistence layer to set <see cref="AuditedObject.LastUpdated">LastUpdated</see>
  /// and <see cref="AuditedObject.UpdatedBy">UpdatedBy</see> when flushing auditable objects to
  /// the database.
  /// </summary>
  [Serializable]
  public class AuditInterceptor : EmptyInterceptor
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(AuditInterceptor));

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    public AuditInterceptor(NHibernateEntityContext context)
    {
      _context = context;
    }

    #endregion

    #region IInterceptor Members

    /// <summary>
    /// Called before a flush
    /// </summary>
    /// <param name="entities">The entities</param>
    public override void PreFlush(ICollection entities)
    {
      if (EntityContextFactory.UserRole.ReadOnly)
      {
        throw new DatabaseException("ReadOnly user not allowed to call Commit!");
      }

      if (ReadWriteMode == ReadWriteMode.ReadOnly)
      {
        throw new DatabaseException("Attempting to flush changes to ReadOnly session!");
      }

      // Save any transient root entities (all other saving will be done in the PreFlush interceptor)
      _context.SaveTransients();

      if (!_context.RootLocks.Any()) return;

      // Save transient children of non-transient root entities.
      // If any new instances are saved, Insert locks for those entities will be added.
      var rootLocks = _context.RootLocks.ToList();
      foreach (var rootLock in rootLocks)
      {
        _context.SaveUnsavedChildren(rootLock);
      }

      // Perform lock consistency check
      _context.ValidateRootLocks();

      if (ReadWriteMode != ReadWriteMode.Workflow)
      {
        // Check entity permissions
        foreach (var @lock in _context.RootLocks)
        {
          string errorMsg;
          if (!_context.CheckPermission(@lock, out errorMsg))
            throw new SecurityException(errorMsg);
        }
      }

      // Set ValidFrom on root entity if required.
      // Make sure all child entities have same ValidFrom as root entity.
      // Force parent entity to be dirty if any ChildEntity is dirty.
      var rootNodes = _context.GetDeltaNodes();
      foreach (var rootNode in rootNodes)
      {
        var ao = rootNode.Obj as AuditedObject;
        if (ao != null)
          ProcessAggregate(rootNode);
      }
    }

    private void ProcessAggregate(DeltaNode rootNode)
    {
      var ao = rootNode.Obj as AuditedObject;

      // Check if any member of the aggregate is dirty
      var isDirty = rootNode.IsDirty || rootNode.ChildIsDirty;

      // Determine the ValidFrom date for the aggregate from the root entity
      DateTime asOf;
      if (ao == null)
      {
        asOf = DateTime.MinValue;
      }
      else
      {
        var cm = ClassCache.Find(ao);
        if (cm.OldStyleValidFrom)
        {
          asOf = ao.ValidFrom;
        }
        else
        {
          switch (HistorizationPolicy)
          {
            case HistorizationPolicy.None:
              asOf = ao.ValidFrom;
              break;
            case HistorizationPolicy.All:
              asOf = AsOf.Date;
              break;
            default:
              asOf = ao.IsNewObject() ? ao.ValidFrom : AsOf.Date;
              break;
          }
        }
      }

      // Check if any member of the aggregate is rolled back
      bool isRolledBack = IsRolledBack(rootNode);

      InternalProcess(rootNode, isDirty, asOf, isRolledBack);
    }

    private bool IsRolledBack(DeltaNode deltaNode)
    {
      var po = deltaNode.Obj;
      return _rolledBack.Contains(po.ObjectId) || deltaNode.ChildNodes.Any(IsRolledBack);
    }

    private void InternalProcess(DeltaNode deltaNode, bool isDirty, DateTime asOf, bool isRolledBack)
    {
      if (isRolledBack)
      {
        var po = deltaNode.Obj;
        _rolledBack.Add(po.ObjectId);
      }

      var ao = deltaNode.NewState as AuditedObject;
      if (ao != null)
      {
        if (isDirty)
        {
          if (asOf < ao.ValidFrom)
          {
            throw new DatabaseException(
              $"AsOf [{asOf}] earlier than ValidFrom [{ao.ValidFrom}] for {deltaNode.ClassMeta.Name} [{deltaNode.ObjectId}]");
          }

          ao.ValidFrom = asOf;
        }
      }

      foreach (var childNode in deltaNode.ChildNodes)
      {
        InternalProcess(childNode, isDirty, asOf, isRolledBack);
      }
    }

    /// <summary>
    /// Override the default FindDirty logic so that any entity that is not locked will not be considered dirty.
    /// </summary>
    /// <remarks>
    /// Note that this only works for entities, not collections.
    /// </remarks>
    public override int[] FindDirty(object entity, object id, object[] currentState, object[] previousState, string[] propertyNames, IType[] types)
    {
      var po = entity as PersistentObject;
      if (po != null)
      {
        var @lock = _context.FindLock(po);
        if (@lock == null)
          return new int[0];
      }
    
      return base.FindDirty(entity, id, currentState, previousState, propertyNames, types);
    }


    /// <summary>
    /// 
    /// </summary>
    public override bool OnFlushDirty(
      object entity,
      object id,
      object[] currentState,
      object[] previousState,
      string[] propertyNames,
      IType[] types)
    {
      var po = entity as PersistentObject;
      if (po == null)
      {
        return false;
      }

      var cm = ClassCache.Find(entity);

      var @lock = _context.FindLock(po);
      if (@lock == null)
      {
        if (Logger.IsDebugEnabled)
        {
          for (int i = 0; i < currentState.Length; ++i)
          {
            if (!Equals(previousState[i], currentState[i]))
            {
              Logger.DebugFormat("Property [{0}] : [{1}] != [{2}]",
                propertyNames[i], previousState[i], currentState[i]);
            }
          }
        }
        throw new SecurityException($"Object {cm.Name} [{id}] is updated but an update lock was not requested.");
      }
      // OnFlushDirty may be called on deleted/inserted entities in addition to updated entities
      if (@lock.LockType == LockType.None)
      {
        Logger.DebugFormat(
          "Object {0} [{1}] is flushed as dirty but {2} lock is requested.",
          cm.Name, id, @lock.LockType);
      }

      RecordAction(po.ObjectId, EntityAction.OnFlushDirty);

      var ao = po as AuditedObject;
      if (ao != null)
      {
        var md = SessionFactory.GetClassMetadata(cm);
        var propNames = new List<string>(md.PropertyNames);

        ao.LastUpdated = DateTime.UtcNow;
        currentState[propNames.IndexOf("LastUpdated")] = DateTime.UtcNow;
        currentState[propNames.IndexOf("UpdatedBy")] = new ObjectRef(
          EntityContextFactory.UserId, EntityContext);

        return true;
      }

      return false;
    }

    /// <summary>
    /// Called before an object is saved
    /// </summary>
    /// <remarks>
    /// The interceptor may modify the <c>state</c>, which will be used for the SQL <c>INSERT</c>
    /// and propagated to the persistent object
    /// </remarks>
    /// <returns><c>true</c> if the user modified the <c>state</c> in any way</returns>
    public override bool OnSave(object entity,
      object id,
      object[] state,
      string[] propertyNames,
      IType[] types)
    {
      var po = entity as PersistentObject;
      if (po == null)
      {
        return false;
      }

      var @lock = _context.FindLock(po);
      if (@lock != null)
      {
        throw new InvalidOperationException($"OnSave triggered for entity [{po.ObjectId}] with existing [{@lock.LockType}] lock");
      }

      // Request lock and record action
      @lock = _context.InternalRequestLock(po, LockType.Insert, null);
      RecordAction(po.ObjectId, EntityAction.OnSave);

      var ao = po as AuditedObject;
      if (ao != null)
      {
        var cm = @lock.Entity;
        var md = SessionFactory.GetClassMetadata(cm);
        var propNames = new List<string>(md.PropertyNames);

        state[propNames.IndexOf("LastUpdated")] = DateTime.UtcNow;
        // There is a special case (AddMe) when DatabaseConfigurator.User is invalid
        var currentUser = TryGetCurrentUser();
        if (currentUser != null)
        {
          state[propNames.IndexOf("UpdatedBy")] = new ObjectRef(
            currentUser.ObjectId, EntityContext);
        }

        return true;
      }

      return false;
    }

    /// <summary>
    /// Called before an object is deleted
    /// </summary>
    /// <remarks>
    /// It is not recommended that the interceptor modify the <c>state</c>.
    /// </remarks>
    public override void OnDelete(object entity,
      object id,
      object[] state,
      string[] propertyNames,
      IType[] types)
    {
      var po = entity as PersistentObject;
      if (po == null)
      {
        return;
      }

      var sessionLock = _context.FindLock(po);
      if (sessionLock == null)
      {
        var cm = ClassCache.Find(po);

        if (_rolledBack.Contains(po.ObjectId))
        {
          Logger.DebugFormat("{0} [{1}] : OnDeleted called but not locked", cm.Name, po.ObjectId);
          return;
        }

        throw new InvalidOperationException($"OnDelete triggered for entity [{po.ObjectId}] but no Delete lock found");
      }
      
      if (sessionLock.LockType != LockType.Delete)
      {
        // True orphan (child not reachable but application did not explicitly Delete)
        _context.InternalRequestLock(po, LockType.Delete, null);
      }

      RecordAction(po.ObjectId, EntityAction.OnDelete);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="tran"></param>
    public override void BeforeTransactionCompletion(ITransaction tran)
    {
      if (!_context.RootLocks.Any()) return;

      using (var table = new DataTable("AuditLog"))
      {
        table.Columns.AddRange(new[]
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

        // Check that NHibernate actions are consistent with LockType
        foreach (var @lock in _context.Locks)
        {
          ValidateActions(@lock);
        }

        foreach (var rootNode in _context.RootLocks.Select(l => _context.CreateNode(l)))
        {
          CreateAuditLogRows(table, rootNode, false);
        }

        int tid = _context.PersistCommitLog(tran);

        if (table.Rows.Count > 0)
        {
          foreach (DataRow row in table.Rows)
          {
            row[0] = tid;
          }

          _context.BulkInsert(table);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="table"><see cref="DataTable"/> used to accumulate rows</param>
    /// <param name="node">Represents a possibly dirty node for a simple or complex entity</param>
    /// <param name="isArchived">True if AuditLog should be marked as archived (only set by ArchiveAuditLog process)</param>
    public void CreateAuditLogRows(DataTable table, DeltaNode node, bool isArchived)
    {
      if (node.IsDirty || node.ChildIsDirty)
      {
        _context.CreateAuditLogRow(table, node);
        foreach (var childNode in node.ChildNodes)
          CreateAuditLogRows(table, childNode, isArchived);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tx"></param>
    public override void AfterTransactionBegin(ITransaction tx)
    {
      base.AfterTransactionBegin(tx);

      _context.AfterTransactionBegin();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="tx"></param>
    public override void AfterTransactionCompletion(ITransaction tx)
    {
      base.AfterTransactionCompletion(tx);
      _context.AfterTransactionCompletion();
      _actions.Clear();
    }

    /// <summary>
    ///
    /// </summary>
    public override void SetSession(ISession session)
    {
      _session = session;
      _session.FlushMode = FlushMode.Commit;
      _session.BeginTransaction();
    }

    /// <summary>
    /// Called when a transient entity is passed to <c>SaveOrUpdate</c>.
    /// </summary>
    /// <remarks>
    ///	The return value determines if the object is saved
    ///	<list>
    ///		<item><see langword="true" /> - the entity is passed to <c>Save()</c>, resulting in an <c>INSERT</c></item>
    ///		<item><see langword="false" /> - the entity is passed to <c>Update()</c>, resulting in an <c>UPDATE</c></item>
    ///		<item><see langword="null" /> - Hibernate uses the <c>unsaved-value</c> mapping to determine if the object is unsaved</item>
    ///	</list>
    /// </remarks>
    /// <param name="entity">A transient entity</param>
    /// <returns>Boolean or <see langword="null" /> to choose default behaviour</returns>
    public override bool? IsTransient(object entity)
    {
      var objectRef = entity as ObjectRef;
      if (objectRef == null || objectRef.IsNull)
      {
        return null;
      }
      var po = (PersistentObject)objectRef.Obj;
      if (po != null)
      {
        return po.ObjectId == 0;
      }
      var id = objectRef.Id;
      return id == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="id"></param>
    /// <param name="state"></param>
    /// <param name="propertyNames"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    public override bool OnLoad(object entity, object id, object[] state, string[] propertyNames, IType[] types)
    {
      AuditedObject ao;

      if (AsOf == DateTime.MaxValue)
      {
        ao = null;
      }
      else
      {
        ao = entity as AuditedObject;
        if (ao != null)
        {
          var cm = ClassCache.Find((long)id);
          if (cm.AuditPolicy != AuditPolicy.History)
          {
            ao = null;
          }
          else
          {
            if (cm.OldStyleValidFrom)
            {
              ao = null;
            }
            else
            {
              var validFrom = (DateTime)state[1];
              if (validFrom.Date <= AsOf.Date)
                ao = null;
            }
          }
        }
      }

      if (ao != null)
      {
        _pendingRollback.Add(ao);
      }

      return base.OnLoad(entity, id, state, propertyNames, types);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns DateTime.MinValue if this is a current time session, else the effective date of the session
    /// </summary>
    public DateTime AsOf => _context.AsOf;

    /// <summary>
    /// Indicates if this Session is ReadOnly or ReadWrite
    /// </summary>
    /// <remarks>
    /// If a session is ReadOnly, then any attempt to do a Flush or Commit will result in an exception.
    /// </remarks>
    public ReadWriteMode ReadWriteMode => _context.ReadWriteMode;

    /// <summary>
    /// Provides access to underlying <see cref="NHibernateEntityContext"/> to NHibernate event listeners
    /// </summary>
    public NHibernateEntityContext EntityContext => _context;

    /// <summary>
    ///   
    /// </summary>
    public HistorizationPolicy HistorizationPolicy { get; set; }

    #endregion

    #region Methods

    internal void Evict(long objectId)
    {
      _actions.Remove(objectId);
      _rolledBack.Remove(objectId);
    }

    /// <summary>
    /// Returns the set of entities that have been rolled back in this session
    /// </summary>
    internal ISet<long> RolledBack => _rolledBack;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool RollbackEvents()
    {
      if (!_pendingRollback.Any())
      {
        return false;
      }

      var items = new Dictionary<long, AuditItem>();

      using (var conn = new RawConnection())
      using (var tran = (SqlTransaction)conn.BeginTransaction())
      {
        IDbCommand cmd;

        if (_pendingRollback.Count == 1)
        {
          cmd = conn.CreateCommand();
          cmd.CommandType = CommandType.Text;
          cmd.CommandText =
            "SELECT TOP(1) a.ObjectId, c.LastUpdated, a.ValidFrom, a.[Action], a.ObjectDelta FROM AuditLog a JOIN CommitLog c ON a.Tid = c.Tid WHERE a.ObjectId = @p1 AND (a.ValidFrom <= @p0 OR a.[Action] IN (0, 1)) ORDER BY ValidFrom DESC, a.Tid DESC";
          cmd.CommandTimeout = SessionFactory.CommandTimeout;
          cmd.Parameters.Add(new SqlParameter("p0", AsOf));
          cmd.Parameters.Add(new SqlParameter("p1", _pendingRollback[0].ObjectId));
          cmd.Transaction = tran;
        }
        else
        {
          using (var dt = ObjectIdJoinTable.CreateDataTable("#ObjectIds", "ObjectId", _pendingRollback.Select(ao => ao.ObjectId)))
          {
            using (var createTableCommand = conn.CreateCommand())
            {
              createTableCommand.CommandType = CommandType.Text;
              createTableCommand.CommandText = "CREATE TABLE #ObjectIds (ObjectId bigint NOT NULL PRIMARY KEY (ObjectId))";
              createTableCommand.CommandTimeout = SessionFactory.CommandTimeout;
              createTableCommand.Transaction = tran;
              createTableCommand.ExecuteNonQuery();
            }

            using (var bulkCopy = new SqlBulkCopy(conn.Impl, SqlBulkCopyOptions.Default, tran){DestinationTableName = dt.TableName,BulkCopyTimeout = SessionFactory.CommandTimeout})
            {
              bulkCopy.WriteToServer(dt);
            }
          }

          cmd = conn.CreateCommand();
          cmd.CommandType = CommandType.Text;
          cmd.CommandText = "WITH cte (Tid, ValidFrom, ObjectId) AS (SELECT MAX(Tid),ValidFrom,ObjectId FROM AuditLog WHERE ObjectId IN (SELECT ObjectId FROM #ObjectIds) AND (ValidFrom <= @p0 OR Action IN (0,1)) GROUP BY ObjectId, ValidFrom) SELECT al.ObjectId,cl.LastUpdated,al.ValidFrom,al.Action,al.ObjectDelta FROM AuditLog al JOIN cte ON al.Tid=cte.Tid AND al.ObjectId=cte.ObjectId JOIN CommitLog cl ON al.Tid=cl.Tid ORDER BY al.ValidFrom DESC";
          //cmd.CommandText = "SELECT a.ObjectId, c.LastUpdated, a.ValidFrom, a.[Action], a.ObjectDelta FROM AuditLog a JOIN #ObjectIds o ON a.ObjectId = o.ObjectId JOIN CommitLog c ON a.Tid = c.Tid WHERE a.ValidFrom <= @p0 OR a.[Action] IN (0, 1) ORDER BY a.ObjectId DESC, ValidFrom DESC, a.Tid DESC";
          cmd.CommandTimeout = SessionFactory.CommandTimeout;
          cmd.Parameters.Add(new SqlParameter("p0", AsOf));
          cmd.Transaction = tran;
        }

        Logger.DebugFormat("{0} [{1}]", cmd.CommandText, cmd.Parameters[0]);

        using (var reader = cmd.ExecuteReader())
        {
          while (reader.Read())
          {
            var objectId = reader.GetValue<long>(0);

            if (items.ContainsKey(objectId)) continue;

            var lastUpdated = reader.GetValue<DateTime>(1);
            var validFrom = reader.GetValue<DateTime>(2);

            items[objectId] = new AuditItem
            {
              ObjectId = objectId,
              ValidFrom = validFrom,
              LastUpdated = lastUpdated,
              Action = reader.GetValue<int>(3),
              ObjectDelta = reader.GetValue<byte[]>(4),
            };
          }
        }
      }

      var map = new Dictionary<long, AuditedObject>();
      foreach (var ao in _pendingRollback)
      {
        map[ao.ObjectId] = ao;
      }

      var itemsToRollback = new List<AuditItem>();

      foreach (var kvp in items)
      {
        var id = kvp.Key;
        var item = kvp.Value;

        var ao = map[id];
        if (ao.ValidFrom == item.ValidFrom)
        {
          // If the inception version has ValidFrom > SqlMinDate and Session.AsOf is 
          // less than ValidFrom, then the above query may return the current version. 
          // In this case, we do not want to rollback.
          continue;
        }

        itemsToRollback.Add(item);

        _rolledBack.Add(id);
      }

      _pendingRollback.Clear();

      foreach (var item in itemsToRollback)
      {
        Logger.DebugFormat("Rolling back [{0}]", item.ObjectId);
        using (var stream = new MemoryStream(item.ObjectDelta))
        using (var reader = new BinaryEntityReader(stream, new EntityContextLoaderAdaptor(EntityContext)))
        {
          reader.ReadEntity();
        }
      }

      return true;
    }

    /// <summary>
    /// Returns true if an <see cref="EntityLock"/> is found for the entity with the specified objectId.
    /// </summary>
    internal bool IsLocked(long objectId)
    {
      return _context.FindLock(objectId) != null;
    }

    /// <summary>
    /// This smaller function will handle a special case DatabaseConfigurator.User is invalid when addme.exe runs.
    /// </summary>
    /// <returns></returns>
    private static User TryGetCurrentUser()
    {
      try
      {
        return EntityContextFactory.User;
      }
      catch (Exception)
      {
        return null;
      }
    }

    #endregion

    #region EntityAction Management

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="record"></param>
    private void RecordAction(long objectId, EntityAction record)
    {
      List<EntityAction> actions;
      if (!_actions.TryGetValue(objectId, out actions))
      {
        actions = new List<EntityAction>();
        _actions[objectId] = actions;
      }
      actions.Add(record);
    }


    /// <summary>
    /// Check if lock is consistent with NHibernate interceptor actions
    /// </summary>
    /// <returns>True if </returns>
    public bool ValidateActions(EntityLock @lock)
    {
      List<EntityAction> actions;
      _actions.TryGetValue(@lock.ObjectId, out actions);

      bool isValid;
      bool isDirty;
      if (@lock.LockType == LockType.Insert)
      {
        isValid = ValidateInsertLockActions(actions);
        isDirty = isValid;
      }
      else if (@lock.LockType == LockType.Update)
      {
        isValid = ValidateUpdateLockActions(actions);
        isDirty = isValid && _actions.Count > 0;
      }
      else if (@lock.LockType == LockType.Delete)
      {
        isValid = ValidateDeleteLockActions(actions);
        isDirty = isValid;
      }
      else
      {
        isValid = ValidateNoneLockActions(actions);
        isDirty = !isValid;
      }
      if (!isValid)
      {
        string actionStr = string.Join(",", _actions.Select(
          a => a.ToString()).ToArray());

        throw new DatabaseException($"Invalid EntityAction sequence [{actionStr}] for SessionLock [{this}]");
      }
      return isDirty;
    }

    private static bool ValidateNoneLockActions(List<EntityAction> actions)
    {
      return actions.Count == 2 &&
             actions[0] == EntityAction.OnSave &&
             actions[1] == EntityAction.OnDelete;
    }

    private static bool ValidateInsertLockActions(List<EntityAction> actions)
    {
      if (actions == null || actions.Count == 0)
      {
        // There is no way to request an Insert lock without OnSave being called
        return false;
      }
      if (actions[0] != EntityAction.OnSave)
      {
        return false;
      }
      for (int i = 1; i < actions.Count; i++)
      {
        if (actions[i] != EntityAction.OnFlushDirty)
          return false;
      }
      return true;
    }

    private static bool ValidateUpdateLockActions(List<EntityAction> actions)
    {
      if (actions == null || actions.Count == 0)
      {
        // It is okay (and common) to request an update lock but not make any changes
        return true;
      }

      if (actions.Count != 1 || actions[0] != EntityAction.OnFlushDirty)
      {
        // If locked for update the only allowed action is OnFlushDirty
        return false;
      }

      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    private static bool ValidateDeleteLockActions(List<EntityAction> actions)
    {
      if (actions == null || actions.Count == 0)
      {
        return true;
      }
      switch (actions[0])
      {
        case EntityAction.OnSave:
          if (actions.Count != 2 || actions[1] != EntityAction.OnDelete) return false;
          break;
        case EntityAction.OnFlushDirty:
          if (actions.Count != 2 || actions[1] != EntityAction.OnDelete) return false;
          break;
        case EntityAction.OnDelete:
          if (actions.Count != 1) return false;
          break;
      }
      return true;
    }

    #endregion

    #region Data

    private ISession _session;
    private readonly NHibernateEntityContext _context;
    
    private readonly IDictionary<long, List<EntityAction>> _actions = new Dictionary<long, List<EntityAction>>();
    private readonly IList<AuditedObject> _pendingRollback = new List<AuditedObject>();
    private readonly ISet<long> _rolledBack = new HashSet<long>(); 

    #endregion

    #region Nested Types

    private class AuditItem
    {
      public long ObjectId { get; set; }
      public int Action { private get; set; }
      public DateTime ValidFrom { get; set; }
      public DateTime LastUpdated { get; set; }
      public byte[] ObjectDelta { get; set; }

      public override string ToString()
      {
        return $"{Action} {EntityHelper.GetClassFromObjectId(ObjectId).Name}:{ObjectId} ValidFrom [{ValidFrom:d}] LastUpdated [{LastUpdated}]";
      }
    }

    #endregion
  }
}