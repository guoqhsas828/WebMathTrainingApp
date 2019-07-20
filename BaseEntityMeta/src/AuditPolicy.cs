// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <exclude />
  public enum AuditPolicy
  {
    /// <summary>
    /// No AuditLog
    /// </summary>
    None,

    /// <summary>
    /// Save AuditLog without ObjectDelta
    /// </summary>
    Log,

    /// <summary>
    /// Save AuditLog with ObjectDelta
    /// </summary>
    History
  }
}