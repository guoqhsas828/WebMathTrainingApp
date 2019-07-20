// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  [Component]
  [DataContract]
  public sealed class CommitLog : BaseEntityObject
  {
    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNull = false)]
    public int Tid { get; set; }

    /// <summary>
    /// LastUpdated
    /// </summary>
    [DataMember]
    [DateTimeProperty(AllowNull = false)]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// UpdatedBy
    /// </summary>
    [DataMember]
    [NumericProperty(AllowNull = false)]
    public long UpdatedBy { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [StringProperty(MaxLength = 140)]
    public string Comment { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [GuidProperty]
    public Guid TransactionId { get; set; }

    /// <summary>
    /// Returns string representation of AuditLog instance
    /// </summary>
    public override string ToString()
    {
      return string.Format("{0},{1},{2}", Tid, LastUpdated, UpdatedBy);
    }
  }
}