// 
// Copyright (c) WebMathTraining Inc 2002-2014. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Database.Workflow
{
  /// <summary>
  /// An entity for the workflow instances table
  /// </summary>
  [Serializable]
  [DataContract]
  public class Instance
  {

    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    /// <value>
    /// The instance identifier.
    /// </value>
    [DataMember]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    [DataMember]
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the last updated time.
    /// </summary>
    /// <value>
    /// The last updated time.
    /// </value>
    [DataMember]
    public DateTime LastUpdatedTime { get; set; }

    /// <summary>
    /// Gets or sets the active bookmarks.
    /// </summary>
    /// <value>
    /// The active bookmarks.
    /// </value>
    [DataMember]
    public string ActiveBookmarks { get; set; }

    /// <summary>
    /// Gets or sets the name of the suspension exception.
    /// </summary>
    /// <value>
    /// The name of the suspension exception.
    /// </value>
    [DataMember]
    public string SuspensionExceptionName { get; set; }

    /// <summary>
    /// Gets or sets the suspension reason.
    /// </summary>
    /// <value>
    /// The suspension reason.
    /// </value>
    [DataMember]
    public string SuspensionReason { get; set; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    /// <value>
    /// The execution status.
    /// </value>
    [DataMember]
    public string ExecutionStatus { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    public bool IsSuspended { get; set; }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
      return string.Format("InstanceId: {0}, CreationTime: {1}, LastUpdatedTime: {2}, SuspensionExceptionName: {3}, SuspensionReason: {4}, ExecutionStatus: {5}", InstanceId, CreationTime, LastUpdatedTime, SuspensionExceptionName, SuspensionReason, ExecutionStatus);
    }
  }
}