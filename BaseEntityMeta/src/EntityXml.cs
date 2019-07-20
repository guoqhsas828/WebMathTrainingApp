// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class EntityXml : IHasValidFrom
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
    public string Xml { get; set; }

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