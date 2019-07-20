// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System.Linq;
using NHibernate.Criterion;
using BaseEntity.Database.Types;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  public static class PersistentObjectExt
  {
    /// <summary>
    /// Returns true if the user is permissioned to Insert this instance
    /// </summary>
    /// <remarks>
    /// The Insert request could still fail for several reasons, for example in
    /// the case of a duplicate key violation, or if this is not a new instance.
    /// </remarks>
    /// <param name="po"></param>
    /// <param name="errorMsg">If the method returns false, the errorMsg will contain a description of the reason.</param>
    /// <returns></returns>
    public static bool CanInsert(this PersistentObject po, out string errorMsg)
    {
      bool allowed = SecurityPolicy.CanCreate(po);
      errorMsg = (allowed) ? null : "Permission denied";
      return allowed;
    }

    /// <summary>
    /// Return true if the user is permissioned to Update this instance, else false
    /// </summary>
    /// <remarks>
    /// Depending on the SecurityPolicy, permissions can depend on the state of the instance and so even if
    /// this method returns true, if any property values are changed after the call but before the commit, then 
    /// the update may still be rejected at commit time.
    /// </remarks>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    public static bool CanUpdate(this PersistentObject po, out string errorMsg)
    {
      bool allowed = SecurityPolicy.CanUpdate(po);
      errorMsg = (allowed) ? null : "Permission denied";
      return allowed;
    }

    /// <summary>
    /// Return true if the user is permissioned to delete this instance, else false
    /// </summary>
    /// <param name="po"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    public static bool CanDelete(this PersistentObject po, out string errorMsg)
    {
      bool allowed = SecurityPolicy.CanDelete(po);
      errorMsg = (allowed) ? null : "Permission denied";
      return allowed;
    }

    /// <summary>
    /// Return if object has not yet been committed to the database
    /// </summary>
    public static bool IsNewObject(this PersistentObject po)
    {
      if (po.IsUnsaved)
      {
        return true;
      }

      var sessionLock = Session.FindLock(po);
      if (sessionLock != null && sessionLock.LockType == LockType.Insert)
      {
        return true;
      }

      return false;
    }

    /// <summary>
    /// Gets the object version.
    /// </summary>
    /// <returns></returns>
    public static int GetObjectVersion(this VersionedObject vo, long objectId)
    {
      var entityId = EntityHelper.GetEntityIdFromObjectId(objectId);
      var classMeta = ClassCache.Find(entityId);
      if (classMeta == null)
      {
        throw new DatabaseException(string.Format(
          "No class metadata found for object with ObjectId {0} (EntityId {1})", objectId, entityId));
      }

      var type = classMeta.Type;

      if (!typeof(VersionedObject).IsAssignableFrom(type))
      {
        throw new DatabaseException(
          string.Format("Type '{0}' of object with ObjectId {1} is not a VersionedObject", type, objectId));
      }

      using (new SessionBinder(ReadWriteMode.ReadOnly))
      {
        var criteria = Session.CreateCriteria(classMeta.Type).Add(Restrictions.Eq("ObjectId", objectId));
        var list = criteria.List<VersionedObject>();
        var @object = list.SingleOrDefault();
        if (@object != null)
        {
          return @object.ObjectVersion;
        }
        return 0;
      }
    }
  }
}