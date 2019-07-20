// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An <see cref="IEntityContext"/> that provide a means for tracking changes to <see cref="PersistentObject">entities</see>.
  /// </summary>
  public interface IEditableEntityContext : ITransientEntityContext
  {
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    long Save(PersistentObject po);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    void Delete(PersistentObject po);

    /// <summary>
    /// Registers intent to update the specified <see cref="PersistentObject"/>
    /// </summary>
    /// <param name="po">The entity to update</param>
    void RequestUpdate(PersistentObject po);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    bool TryRequestUpdate(PersistentObject po, out string errorMsg);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    EntityLock FindLock(PersistentObject po);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    bool IsLocked(PersistentObject po);

    /// <summary>
    /// Return true if any entities in the session are dirty
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    List<AuditLog> GetAuditLogs();
 
    /// <summary>
    /// 
    /// </summary>
    void CommitTransaction(string comment = null);

    /// <summary>
    /// 
    /// </summary>
    void RollbackTransaction();

    /// <summary>
    /// 
    /// </summary>
    void SaveTransients();
  }
}