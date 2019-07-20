// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Metadata;
using BaseEntity.Core.Services.EventService.ServiceModel;

namespace BaseEntity.Database
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [DataContract]
  public class CommitLogEvent : BaseEvent, IEnumerable<AuditLog>
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="tid"></param>
    /// <param name="auditLogs"></param>
    public CommitLogEvent(int tid, IEnumerable<AuditLog> auditLogs)
    {
      Tid = tid;
      AuditLogs = auditLogs.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public int Tid { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public IList<AuditLog> AuditLogs { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<AuditLog> GetEnumerator()
    {
      return AuditLogs.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return $"Tid: {Tid}, AuditLogs: {AuditLogs.Count}";
    }
  }
}