// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Keeps track of ObjectDeltas
  /// </summary>
  public abstract class EditableEntityContextBase : IEditableEntityContext
  {
    #region Data

    private readonly IDictionary<long, EntityLock> _locks = new Dictionary<long, EntityLock>();
    private readonly IDictionary<long, EntityLock> _rootLocks = new Dictionary<long, EntityLock>();  
    private readonly SnapshotEntityContext _transientContext = new SnapshotEntityContext();
    private readonly SnapshotEntityContext _snapshotContext = new SnapshotEntityContext();

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="readWriteMode"></param>
    /// <param name="historizationPolicy"></param>
    protected EditableEntityContextBase(DateTime asOf, ReadWriteMode readWriteMode, HistorizationPolicy historizationPolicy)
    {
      AsOf = asOf;
      ReadWriteMode = readWriteMode;
      HistorizationPolicy = historizationPolicy;
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// 
    /// </summary>
    public abstract void Dispose();

    #endregion

    #region IEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    public abstract bool IsOpen { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract bool IsDisposed();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public abstract PersistentObject Get(long id);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual ObjectRef GetObjectRef(long id)
    {
      if (id == 0) return null;

      return EntityHelper.IsTransient(id) ? _transientContext.GetObjectRef(id) : new ObjectRef(id, this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract IEnumerator<PersistentObject> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    #endregion

    #region ITransientEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public long GenerateTransientId(Type type)
    {
      return _transientContext.GenerateTransientId(type);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public long RegisterTransient(PersistentObject po)
    {
      return _transientContext.RegisterTransient(po);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <param name="id"></param>
    public void RegisterTransient(PersistentObject po, long id)
    {
      _transientContext.RegisterTransient(po, id);
    }

    /// <summary>
    /// 
    /// </summary>
    public void RegisterTransients()
    {
      // Register anonymous instances reachable from transient roots
      _transientContext.RegisterTransients();

      // Register anonymous instances reachable from non-transient roots
      foreach (var @lock in _rootLocks.Values)
      {
        var walker = new OwnedOrRelatedObjectWalker();
        walker.Walk(@lock.NewState);
        foreach (var oo in walker.OwnedObjects)
        {
          if (oo.IsAnonymous)
          {
            RegisterTransient(oo);
          }
          else if (Get(oo.ObjectId) == null)
          {
            throw new MetadataException(string.Format("Entity [{0}:{1}] does not exist in current context!", oo.GetType().Name, oo.ObjectId));
          }
        }
      }
    }

    #endregion

    #region IEditableEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public abstract long Save(PersistentObject po);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public abstract void Delete(PersistentObject po);

    /// <summary>
    /// 
    /// </summary>
    public bool IsDirty => _transientContext.Any() || Locks.Any(CheckIsDirty);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public virtual void Evict(PersistentObject po)
    {
      var walker = new OwnedObjectWalker(true);

      walker.Walk(po);

      foreach (var obj in walker.OwnedObjects)
      {
        _locks.Remove(obj.ObjectId);
        _rootLocks.Remove(obj.ObjectId);
        _snapshotContext.Evict(obj);
        _transientContext.Evict(obj);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public EntityLock FindLock(PersistentObject po)
    {
      EntityLock @lock;
      return _locks.TryGetValue(po.ObjectId, out @lock) ? @lock : null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    public EntityLock FindLock(long objectId)
    {
      EntityLock @lock;
      return _locks.TryGetValue(objectId, out @lock) ? @lock : null;
    }

    /// <summary>
    /// Bool type. Is locked.
    /// </summary>
    /// <param name="po">presisten object</param>
    /// <returns></returns>
    public bool IsLocked(PersistentObject po)
    {
      return _locks.ContainsKey(po.ObjectId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public void RequestUpdate(PersistentObject po)
    {
      string errorMsg;
      if (!TryRequestUpdate(po, out errorMsg))
        throw new SecurityException(errorMsg);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    public bool TryRequestUpdate(PersistentObject po, out string errorMsg)
    {
      return CheckPermission(RequestUpdateLock(po), out errorMsg);
    }

    /// <summary>
    /// Gets the root object deltas.
    /// </summary>
    /// <returns></returns>
    public List<DeltaNode> GetDeltaNodes()
    {
      var list = new List<DeltaNode>();

      // Add node for any new aggregates
      list.AddRange(_transientContext.Where(po => ClassCache.Find(po).IsRootEntity).Select(po => CreateNode(po, po.ObjectId, 0)));

      // Add node for any changed or removed aggregates
      list.AddRange(_rootLocks.Values.Select(CreateNode).Where(n => n.IsDirty || n.ChildIsDirty));

      return list;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lock"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    public virtual bool CheckPermission(EntityLock @lock, out string errorMsg)
    {
      if (@lock == null)
      {
        throw new ArgumentNullException("lock");
      }
      errorMsg = null;
      return true;
    }

    /// <summary>
    /// Gets the root object deltas.
    /// </summary>
    /// <returns></returns>
    public List<AuditLog> GetAuditLogs()
    {
      var list = new List<AuditLog>();

      foreach (var po in _transientContext.Where(po => ClassCache.Find(po).IsRootEntity))
      {
        GetAuditLogs(CreateNode(po, po.ObjectId, 0), list);
      }

      foreach (var node in _rootLocks.Values.Select(CreateNode).ToList())
      {
        GetAuditLogs(node, list);
      }

      return list;
    }

    /// <summary>
    /// 
    /// </summary>
    public abstract void CommitTransaction(string comment = null);

    /// <summary>
    /// 
    /// </summary>
    public abstract void RollbackTransaction();

    /// <summary>
    /// 
    /// </summary>
    public void SaveTransients()
    {
      foreach (var po in _transientContext)
      {
        po.ObjectId = 0;
      }

      foreach (var po in _transientContext.Where(po => po.IsAnonymous))
      {
        var cm = ClassCache.Find(po);
        if (cm.IsRootEntity)
          Save(po);
      }

      _transientContext.Clear();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Called by SaveUnsavedChildren
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    protected abstract long SaveChild(PersistentObject po);

    /// <summary>
    /// Return true if the object is dirty (i.e. has changes to be committed)
    /// </summary>
    private static bool CheckIsDirty(EntityLock entityLock)
    {
      switch (entityLock.LockType)
      {
        case LockType.Insert:
        case LockType.Delete:
          return true;
      }

      return !ClassMeta.IsSame(entityLock.OldState, entityLock.NewState);
    }

    /// <summary>
    /// Return ObjectDeltas for any Added/Changed/Removed entities in topological order
    /// </summary>
    internal static void GetObjectDeltas(DeltaNode parentNode, IList<ObjectDelta> list)
    {
      if (parentNode.IsDirty || parentNode.ChildIsDirty)
      {
        var delta = ClassMeta.CreateDelta(parentNode.OldState, parentNode.NewState);
        if (delta != null)
          list.Add((ObjectDelta)delta);
      }
      foreach (var childNode in parentNode.ChildNodes)
        GetObjectDeltas(childNode, list);
    }

    /// <summary>
    /// Return AuditLogs for any Added/Changed/Removed entities in topological order
    /// </summary>
    internal void GetAuditLogs(DeltaNode parentNode, IList<AuditLog> list)
    {
      if (parentNode.IsDirty || parentNode.ChildIsDirty)
      {
        list.Add(CreateAuditLog(parentNode));
      }
      foreach (var childNode in parentNode.ChildNodes)
        GetAuditLogs(childNode, list);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="deltaNode"></param>
    private static AuditLog CreateAuditLog(DeltaNode deltaNode)
    {
      var classMeta = deltaNode.ClassMeta;

      byte[] bytes = null;
      if (deltaNode.NewState != null)
      {
        using (var stream = new MemoryStream())
        {
          using (var writer = new BinaryEntityWriter(stream))
          {
            writer.WriteEntity(deltaNode.NewState);
          }
          bytes = stream.ToArray();
        }
      }

      ItemAction itemAction;
      switch (deltaNode.LockType)
      {
        case LockType.Insert:
          itemAction = ItemAction.Added;
          break;
        case LockType.Delete:
          itemAction = ItemAction.Removed;
          break;
        default:
          itemAction = ItemAction.Changed;
          break;
      }

      return new AuditLog
      {
        Action = itemAction,
        ObjectId = deltaNode.ObjectId,
        RootObjectId = deltaNode.RootObjectId,
        ParentObjectId = deltaNode.ParentObjectId,
        EntityId = classMeta.EntityId,
        ObjectDelta = bytes,
      };
    }

    #endregion

    #region Lock Management

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<EntityLock> Locks => _locks.Values;

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<EntityLock> RootLocks => _rootLocks.Values;

    private void AddLock(EntityLock @lock)
    {
      _locks.Add(@lock.ObjectId, @lock);

      if (@lock.IsRootLock)
      {
        _rootLocks.Add(@lock.ObjectId, @lock);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public void ClearLocks()
    {
      _locks.Clear();
      _rootLocks.Clear();
    }

    /// <summary>
    /// Set the ParentLock and RootLock for all child entities if not set and validate it for child entities where it is set.
    /// </summary>
    public void ValidateRootLocks()
    {
      foreach (var @lock in _rootLocks.Values)
      {
        ValidateChildLocks(@lock);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentLock"></param>
    private void ValidateChildLocks(EntityLock parentLock)
    {
      var parentEntity = parentLock.Entity;
      foreach (var cascade in parentEntity.CascadeList.Where(c => CascadeUtil.ShouldCascade(parentLock.LockType, c.Cascade)))
      {
        var parentObj = parentLock.NewState;
        foreach (PersistentObject childObj in cascade.ReferencedObjects(parentObj))
        {
          if (childObj.ObjectId == 0)
          {
            continue;
          }
          var childLock = FindLock(childObj);
          if (childLock == null)
          {
            var cm = ClassCache.Find(childObj);
            if (cm.IsChildEntity)
            {
              throw new MetadataException(string.Format("Unable to find ChildLock for [{0}]", childObj.ObjectId));
            }
          }
          else
          {
            if (!childLock.IsRootLock)
            {
              childLock.SetParentLock(parentLock);
            }

            ValidateChildLocks(childLock);
          }
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentLock"></param>
    public void SaveUnsavedChildren(EntityLock parentLock)
    {
      var parentEntity = parentLock.Entity;
      foreach (var cascade in parentEntity.CascadeList.Where(c => CascadeUtil.ShouldCascade(parentLock.LockType, c.Cascade)))
      {
        var parentObj = parentLock.NewState;
        foreach (var childObj in cascade.ReferencedObjects(parentObj))
        {
          if (childObj.IsAnonymous)
          {
            SaveChild(childObj);
          }
          else
          {
            var childLock = FindLock(childObj);
            if (childLock == null)
            {
              var cm = ClassCache.Find(childObj);
              if (cm.IsChildEntity)
                throw new MetadataException(string.Format("Unable to find ChildLock for [{0}]", childObj.ObjectId));
            }
            else
            {
              // It is possible this entity could have an update lock and have unsaved children
              SaveUnsavedChildren(childLock);
            }
          }
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public EntityLock RequestUpdateLock(PersistentObject po)
    {
      var entity = ClassCache.Find(po);

      if (entity.IsChildEntity)
      {
        throw new InvalidOperationException(String.Format(
          "Cannot request explicit lock on ChildEntity [{0}]", entity.Name));
      }

      return InternalRequestLock(po, LockType.Update, null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po">Entity to be locked</param>
    /// <param name="lockType">Requested <see cref="LockType" /></param>
    /// <param name="parentLock">Parent <see cref="EntityLock" /> (if cascading from parent)</param>
    /// <returns></returns>
    public EntityLock InternalRequestLock(PersistentObject po, LockType lockType, EntityLock parentLock)
    {
      var @lock = FindLock(po);
      if (@lock != null)
      {
        if (!@lock.UpgradeLock(lockType))
          return @lock;
      }
      else
      {
        if (po.ObjectId == 0)
        {
          throw new InvalidOperationException(String.Format(
            "Attempt to request [{0}] lock on unsaved object of type [{1}]",
            lockType, po.GetType().Name));
        }
        @lock = new EntityLock(_snapshotContext, po, lockType);
        AddLock(@lock);
      }

      var cm = @lock.Entity;

      // If cascading a lock from a parent entity, set the RootLock in the child
      if (cm.IsChildEntity)
      {
        if (parentLock != null)
          @lock.SetParentLock(parentLock);
      }

      // Do not cascade Insert locks because they are only requested via the OnSave handler
      if (lockType != LockType.Insert)
      {
        foreach (var cascade in cm.CascadeList.Where(c => CascadeUtil.ShouldCascade(lockType, c.Cascade)))
        {
          foreach (PersistentObject childObj in cascade.ReferencedObjects(po))
            InternalRequestLock(childObj, lockType, @lock);
        }
      }

      return @lock;
    }

    /// <summary>
    /// Release all locks 
    /// </summary>
    public void FreeAllEntityLocks()
    {
      ClearLocks();

      _snapshotContext.Clear();
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Gets the date associated with the Interceptor 
    /// </summary>
    public DateTime AsOf { get; }

    /// <summary>
    /// Indicates if this Session is ReadOnly or ReadWrite
    /// </summary>
    /// <remarks>
    /// If a session is ReadOnly, then any attempt to do a Flush or Commit will result in an exception.
    /// </remarks>
    public ReadWriteMode ReadWriteMode { get; }

    /// <summary>
    /// 
    /// </summary>
    public HistorizationPolicy HistorizationPolicy { get; }

    /// <summary>
    /// 
    /// </summary>
    protected SnapshotEntityContext TransientContext => _transientContext;

    #endregion

    #region Nested Types

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lock"></param>
    /// <returns></returns>
    public DeltaNode CreateNode(EntityLock @lock)
    {
      var childNodes = GetChildNodes(@lock.NewState, @lock.RootObjectId, @lock.ParentObjectId);

      bool isSame;
      switch (@lock.LockType)
      {
        case LockType.Insert:
        case LockType.Delete:
          isSame = false;
          break;
        default:
          isSame = ClassMeta.IsSame(@lock.OldState, @lock.NewState);
          break;
      }

      return new DeltaNode(
        @lock.RootObjectId, 
        @lock.ParentObjectId,
        @lock.LockType == LockType.Insert ? null : @lock.OldState,
        @lock.LockType == LockType.Delete ? null : @lock.NewState,
        !isSame, 
        @lock.LockType, 
        childNodes);
    }

    /// <summary>
    /// Create node for unsaved child entity
    /// </summary>
    /// <param name="po"></param>
    /// <param name="rootObjectId"></param>
    /// <param name="parentObjectId"></param>
    /// <returns></returns>
    private DeltaNode CreateNode(PersistentObject po, long rootObjectId, long parentObjectId)
    {
      if (po.ObjectId == 0)
      {
        RegisterTransient(po);
      }

      var childNodes = GetChildNodes(po, rootObjectId, po.ObjectId);
      return new DeltaNode(rootObjectId, parentObjectId, null, po, true, LockType.Insert, childNodes);
    }

    /// <summary>
    /// Get a tree representation of changes to children of this entity
    /// </summary>
    /// <param name="parentObj"></param>
    /// <param name="rootObjectId"></param>
    /// <param name="parentObjectId"></param>
    /// <returns></returns>
    private IList<DeltaNode> GetChildNodes(PersistentObject parentObj, long rootObjectId, long parentObjectId)
    {
      var list = new List<DeltaNode>();

      var reachable = new HashSet<long>();

      var cm = ClassCache.Find(parentObj);
      foreach (var cascade in cm.CascadeList.Where(c => c.Cascade != "none" && c.ReferencedEntity.IsChildEntity))
      {
        foreach (var childObj in cascade.ReferencedObjects(parentObj))
        {
          if (childObj.IsUnsaved)
          {
            // Convert anonymous entity to transient and create node hierarchy without relying on locks
            list.Add(CreateNode(childObj, rootObjectId, parentObjectId));
          }
          else
          {
            var childLock = FindLock(childObj);
            if (childLock == null)
            {
              throw new MetadataException("ChildEntity [" + childObj.ObjectId + "] not locked!");
            }
            var parentLock = childLock.ParentLock;
            if (!ReferenceEquals(parentLock.NewState, parentObj))
            {
              throw new MetadataException(string.Format(
                "ParentObj for ChildObj [{0}] has changed from [{1}] to [{2}]",
                childObj.ObjectId, parentLock.NewState.ObjectId, parentObj.ObjectId));
            }
            var node = CreateNode(childLock);
            reachable.Add(childObj.ObjectId);
            list.Add(node);
          }
        }
      }

      // Force delete of any orphans

      if (!EntityHelper.IsTransient(parentObj))
      {
        var parentLock = FindLock(parentObj);
        var locks = _locks.Values.Where(@lock => ReferenceEquals(parentLock, @lock.ParentLock) && !@reachable.Contains(@lock.ObjectId)).ToList();
        foreach (var @lock in locks)
        {
          InternalRequestLock(@lock.NewState, LockType.Delete, parentLock);
          var node = CreateNode(@lock);
          list.Add(node);
        }
      }

      return list;
    }

    #endregion
  }
}
