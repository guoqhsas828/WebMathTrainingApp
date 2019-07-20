// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
#if NETSTANDARD2_0
using System.Collections.Generic;
#endif
using System.Text;
using Iesi.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Used to capture the original state of an entity and optionally an amended or deleted state
  /// </summary>
  public class EntityLock
  {
    #region Data

    private readonly ClassMeta _entity;
    private readonly PersistentObject _newState;
    private readonly PersistentObject _oldState;

    #endregion

    #region Constructor

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="newState"></param>
    /// <param name="lockType"></param>
    public EntityLock(SnapshotEntityContext context, PersistentObject newState, LockType lockType)
    {
      if (context == null)
      {
        throw new ArgumentNullException("context");
      }

      if (newState == null)
      {
        throw new ArgumentNullException("newState");
      }

      _newState = newState;
      _entity = ClassCache.Find(_newState);
      LockType = lockType;

      if (_entity.IsRootEntity)
      {
        RootLock = this;
      }

      if (LockType == LockType.Update || LockType == LockType.Delete)
      {
        _oldState = CaptureState(context, _newState);
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// The <see cref="PersistentObject" /> for which the lock was requested.
    /// </summary>
    /// <returns></returns>
    public PersistentObject NewState
    {
      get { return _newState; }
    }

    /// <summary>
    /// 
    /// </summary>
    public long ObjectId
    {
      get { return _newState == null ? 0 : NewState.ObjectId; }
    }

    /// <summary>
    /// The type of lock requested.
    /// </summary>
    public LockType LockType { get; internal set; }

    /// <summary>
    /// For child entities, this is the SessionLock of the immediate parent.  For non-child entities, it references itself.
    /// </summary>
    public EntityLock ParentLock { get; private set; }

    /// <summary>
    /// For child entities, this is the SessionLock of the Root.  For non-child entities, it references itself.
    /// </summary>
    public EntityLock RootLock { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public PersistentObject OldState
    {
      get { return _oldState; }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsRootLock
    {
      get { return Entity.IsRootEntity; }
    }

    /// <summary>
    /// The <see cref="ClassMeta" /> for the referenced <see cref="PersistentObject" />
    /// </summary>
    public ClassMeta Entity
    {
      get { return _entity; }
    }

    /// <summary>
    /// 
    /// </summary>
    public long ParentObjectId
    {
      get { return ParentLock == null ? 0 : ParentLock.ObjectId; }
    }

    /// <summary>
    /// 
    /// </summary>
    public long RootObjectId
    {
      get { return RootLock.ObjectId; }
    }

    /// <summary>
    /// Return true if there is a change that needs to be committed
    /// </summary>
    public bool IsDirty
    {
      get
      {
        switch (LockType)
        {
          case LockType.Insert:
          case LockType.Delete:
            return true;
          default:
            return !ClassMeta.IsSame(NewState, OldState);
        }
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Retrieves a string that indicates the current object
    /// </summary>
    public override string ToString()
    {
      return string.Format("{0} Lock [{1}][{2:X}]", LockType, _newState.GetType().Name, _newState.ObjectId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentLock"></param>
    public void SetParentLock(EntityLock parentLock)
    {
      if (Entity.IsRootEntity)
      {
        throw new InvalidOperationException(String.Format(
          "Cannot set ParentLock for RootEntity [{0}]", Entity.Name));
      }
      if (ParentLock == null)
      {
        ParentLock = parentLock;
      }
      else if (ParentLock != parentLock)
      {
        throw new ArgumentException(String.Format(
          "Attempt to change ParentLock for [{0}] from {1} to {2}",
          this, ParentLock, parentLock));
      }

      SetRootLock(parentLock.RootLock);
    }

    /// <summary>
    /// Change the lock from one type to another
    /// </summary>
    /// <param name="requestedLockType"></param>
    public bool UpgradeLock(LockType requestedLockType)
    {
      LockType newLockType = GetUpgradedLockType(requestedLockType);
      if (LockType == newLockType)
      {
        return false;
      }
      LockType = newLockType;
      return true;
    }

    /// <summary>
    /// Get the new LockType that would result if this lock were upgraded as specified,
    /// or throws an exception if the specified lock transition is not allowed.
    /// </summary>
    /// <param name="requestedLockType"></param>
    /// <returns></returns>
    public LockType GetUpgradedLockType(LockType requestedLockType)
    {
      if (requestedLockType == LockType)
      {
        return LockType;
      }
      if (LockType == LockType.Insert && requestedLockType == LockType.Update)
      {
        return LockType;
      }
      if (LockType == LockType.None)
      {
        return requestedLockType;
      }
      if (LockType == LockType.Update && requestedLockType == LockType.Delete)
      {
        return requestedLockType;
      }
      if (LockType == LockType.Insert && requestedLockType == LockType.Delete)
      {
        return LockType.None;
      }
      // In all other lock-mismatch cases, throw back exception        
      var cm = ClassCache.Find(_newState);
      throw new InvalidOperationException(String.Format(
        "PersistentObject object {0}[{1}] is already locked for {2}. {3} lock should not be requested.",
        cm.Name, _newState.ObjectId, LockType, requestedLockType));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootLock"></param>
    public void SetRootLock(EntityLock rootLock)
    {
      if (rootLock.Entity.IsChildEntity)
      {
        throw new ArgumentException(String.Format(
          "RootLock [{0}] references ChildEntity", rootLock));
      }
      if (RootLock == null)
      {
        RootLock = rootLock;
      }
      else if (RootLock != rootLock)
      {
        throw new ArgumentException(String.Format(
          "Attempt to change RootLock for [{0}] from {1} to {2}",
          this, RootLock, rootLock));
      }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Capture the current state of the specified entity and any owned (child) objects
    /// </summary>
    private static PersistentObject CaptureState(SnapshotEntityContext context, PersistentObject po)
    {
      var oldState = context.Get(po.ObjectId);
      if (oldState != null)
      {
        return oldState;
      }

      var sb = new StringBuilder();
      using (var writer = new XmlEntityWriter(sb))
      {
        new CaptureStateWalker(context, writer).Walk(po);
      }

      var xml = sb.ToString();
      var adaptor = new EntityContextLoaderAdaptor(context);
      using (var reader = new XmlEntityReader(xml, adaptor))
      {
        while (!reader.EOF)
          reader.ReadEntity();
      }

      return context.Get(po.ObjectId);
    }

    #endregion

    #region Nested Types

    private class CaptureStateWalker : CascadeWalker
    {
#if NETSTANDARD2_0
      private readonly ISet<long> _captured = new HashSet<long>();
#else
      private readonly ISet<long> _captured = new HashedSet<long>();
#endif
      private readonly SnapshotEntityContext _context;
      private readonly XmlEntityWriter _writer;

      public CaptureStateWalker(SnapshotEntityContext context, XmlEntityWriter writer) : base(true)
      {
        _context = context;
        _writer = writer;
      }

      public override bool Filter(ICascade cascade, PersistentObject parentObj)
      {
        var user = parentObj as User;
        return user == null || cascade.Name != "Role";
      }

      /// <summary>
      /// 
      /// </summary>
      public override bool Action(PersistentObject po)
      {
        if (_context.Contains(po.ObjectId) || _captured.Contains(po.ObjectId))
        {
          return false;
        }
        _writer.WriteEntity(po);
        _captured.Add(po.ObjectId);
        return true;
      }
    }

    #endregion
  }
}