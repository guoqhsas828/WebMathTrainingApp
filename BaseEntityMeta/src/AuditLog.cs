// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///
  /// </summary>
  [Component]
  [Serializable]
  [DataContract]
  public sealed class AuditLog : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNull = false)]
    public int Tid { get; set; }

    /// <summary>
    /// ObjectId
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNull = false)]
    public long ObjectId { get; set; }

    /// <summary>
    /// ObjectId
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNull = false)]
    public long RootObjectId { get; set; }

    /// <summary>
    /// ObjectId
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNull = false)]
    public long ParentObjectId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public int EntityId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [EnumProperty]
    public ItemAction Action { get; set; }

    /// <summary>
    /// Contains the ObjectDifferences serialized as XML
    /// </summary>
    [DataMember]
    [BinaryBlobProperty]
    public byte[] ObjectDelta { get; set; }

    /// <summary>
    /// Gets or sets the ValidFrom time.
    /// </summary>
    [DataMember]
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// Gets or sets the time when the change represented by this <see cref="AuditLog"/> was committed.
    /// </summary>
    /// <remarks>
    /// NOTE: This field comes from the CommitLog and so processes that are reading fields just from the AuditLog table will not set this property.
    /// </remarks>
    [DataMember]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Gets or sets the id of the <see cref="User"/> who made the change represented by this <see cref="AuditLog"/>.
    /// </summary>
    /// <remarks>
    /// NOTE: This field comes from the CommitLog and so processes that are reading fields just from the AuditLog table will not set this property.
    /// </remarks>
    [DataMember]
    public long UpdatedBy { get; set; }

    /// <summary>
    /// Indicates if a AuditHistory record has been created for this <see cref="AuditLog"/>.
    /// </summary>
    [DataMember]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Returns string representation of AuditLog instance
    /// </summary>
    public override string ToString()
    {
      return string.Format("{0},{1},{2},{3},{4}", Tid, ObjectId, EntityId, Action, ObjectDelta);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      var other = obj as AuditLog;
      if (other == null) return false;
      return Equals(other);
    }

    private bool Equals(AuditLog other)
    {
      return Tid == other.Tid && ObjectId == other.ObjectId;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      unchecked
      {
        return (Tid * 397) ^ ObjectId.GetHashCode();
      }
    }
  }
}