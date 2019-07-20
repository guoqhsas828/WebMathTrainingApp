// 
// Copyright (c) WebMathTraining Inc 2002-2016. All rights reserved.
// 

using System;
using System.Runtime.Serialization;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// Argument used when deleting an entity using EntityWorkflow
  /// </summary>
  [DataContract]
  public class EntityDeleteInput : IHasValidFrom
  {
    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public long ObjectId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public DateTime AsOf { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public bool SetValidFrom { get; set; }
  }
}