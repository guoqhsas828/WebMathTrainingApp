// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class DeltaNode
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObjectId"></param>
    /// <param name="parentObjectId"></param>
    /// <param name="oldState"></param>
    /// <param name="newState"></param>
    /// <param name="isDirty"></param>
    /// <param name="lockType"></param>
    /// <param name="childNodes"></param>
    internal DeltaNode(
      long rootObjectId,
      long parentObjectId,
      PersistentObject oldState,
      PersistentObject newState,
      bool isDirty,
      LockType lockType,
      IList<DeltaNode> childNodes)
    {
      RootObjectId = rootObjectId;
      ParentObjectId = parentObjectId;
      OldState = oldState;
      NewState = newState;
      IsDirty = isDirty;
      ChildIsDirty = childNodes.Any(cn => cn.IsDirty || cn.ChildIsDirty);
      LockType = lockType;
      ChildNodes = childNodes;
    }

    /// <summary>
    /// 
    /// </summary>
    public long RootObjectId { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public long ParentObjectId { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public PersistentObject OldState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public PersistentObject NewState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public bool ChildIsDirty { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public LockType LockType { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<DeltaNode> ChildNodes { get; private set; }

    /// <summary>
    /// Return NewState if it exists, otherwise OldState
    /// </summary>
    public PersistentObject Obj
    {
      get { return NewState ?? OldState; }
    }

    /// <summary>
    /// Returns the ObjectId for the underlying <see cref="PersistentObject" />
    /// </summary>
    public long ObjectId
    {
      get { return Obj == null ? 0 : Obj.ObjectId; }
    }

    /// <summary>
    /// Returns the ClassMeta for the underlying <see cref="PersistentObject" />
    /// </summary>
    public ClassMeta ClassMeta
    {
      get { return Obj == null ? null : ClassCache.Find(Obj); }
    }

    /// <summary>
    /// To string
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return String.Format("{0} : {1} [{2}]", LockType, Obj.GetType().Name, Obj.ObjectId);
    }
  }
}