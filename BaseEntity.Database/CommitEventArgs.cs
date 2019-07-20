// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Database
{
  /// <summary>
  /// Represents a commit to the database
  /// </summary>
  public class CommitEventArgs : EventArgs, IEnumerable<AuditLog>
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="tid"></param>
    /// <param name="lastUpdated"></param>
    /// <param name="updatedBy"></param>
    public CommitEventArgs(int tid, DateTime lastUpdated, long updatedBy)
    {
      Tid = tid;
      LastUpdated = lastUpdated;
      UpdatedBy = updatedBy;

      AuditLogs = new List<AuditLog>();
    }

    #region Public Properties

    /// <summary>
    /// Gets or sets the tid.
    /// </summary>
    /// <value>The tid.</value>
    public long Tid { get; private set; }

    /// <summary>
    /// Gets or sets the last updated.
    /// </summary>
    /// <value>The last updated.</value>
    public DateTime LastUpdated { get; private set; }

    /// <summary>
    /// Gets or sets the updated by.
    /// </summary>
    /// <value>The updated by.</value>
    public long UpdatedBy { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    private IList<AuditLog> AuditLogs { get; set; }

    #endregion

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    public void Add(AuditLog auditLog)
    {
      AuditLogs.Add(auditLog);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<AuditLog> GetEnumerator()
    {
      return AuditLogs.GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
      return string.Format("[Tid:{0} LastUpdated:[{1}] UpdatedBy:{2}]", Tid, LastUpdated, UpdatedBy);
    }
  }
}