// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Used to enforce permissions on aggregate root entity instances within the database layer
  /// </summary>
  [Component]
  [DataContract]
  [Serializable]
  public abstract class EntityPolicy : UserRolePolicy, IEntityPolicy
  {
    /// <summary>
    /// Returns true if this policy applies to the specified entity, else false
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public abstract bool IsApplicable(Type type);

    /// <summary>
    /// Called when requesting a lock and immediately before committing changes
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public abstract bool CheckPolicy(PersistentObject entity, ItemAction action);

    /// <summary>
    /// For Update locks, called before committing changes
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    public abstract bool CheckPolicy(ISnapshotDelta delta);
  }
}