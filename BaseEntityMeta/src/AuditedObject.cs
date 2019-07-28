// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   Base class for all audited objects
  /// </summary>
  /// <remarks>
  ///   For internal use only.
  /// </remarks>
  [DataContract]
  [Serializable]
  public abstract class AuditedObject : VersionedObject
  {
    #region Constructors

    /// <summary>
    ///
    /// </summary>
    protected AuditedObject()
    {
      _lastUpdated = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <param name="objectVersion"></param>
    /// <param name="validFrom"></param>
    /// <param name="lastUpdated"></param>
    /// <param name="updatedById"></param>
    protected AuditedObject(long objectId, int objectVersion, DateTime validFrom, DateTime lastUpdated, long updatedById)
      : base(objectId, objectVersion)
    {
      _validFrom = validFrom;
      _lastUpdated = lastUpdated;
      _updatedBy = updatedById == 0 ? null : new ObjectRef(updatedById, null);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="other"></param>
    protected AuditedObject(AuditedObject other) : base(other)
    {
      ValidFrom = other.ValidFrom;
      LastUpdated = other.LastUpdated;
      UpdatedBy = other.UpdatedBy;
    }

    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    [DateTimeProperty(Column = "ValidFrom", AllowNullValue = false, IsTreatedAsDateOnly = true)]
    public DateTime ValidFrom
    {
      get { return _validFrom; }
      set { _validFrom = value; }
    }

    /// <summary>
    ///   Last update time
    /// </summary>
    [DataMember]
    [DateTimeProperty(Column = "LastUpdated", AllowNullValue = false, ReadOnly = true)]
    public DateTime LastUpdated
    {
      get { return _lastUpdated; }
      set { _lastUpdated = value; }
    }

    /// <summary>
    ///   User who performed last updated
    /// </summary>
    [ManyToOneProperty(Column = "UpdatedById", ReadOnly = true)]
    public User UpdatedBy
    {
      get { return (User)ObjectRef.Resolve(_updatedBy); }
      set { _updatedBy = ObjectRef.Create(value); }
    }

    /// <summary>
    /// ObjectId of <see cref="User"/> who last updated this entity
    /// </summary>
    public long UpdatedById
    {
      get { return _updatedBy == null || _updatedBy.IsNull ? 0 : _updatedBy.Id; }
    }

    #endregion

    #region Data

    private DateTime _lastUpdated;
    [DataMember] private ObjectRef _updatedBy;
    [DataMember] private DateTime _validFrom;

    #endregion
  }
}
