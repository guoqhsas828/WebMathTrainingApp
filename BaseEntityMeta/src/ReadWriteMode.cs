// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Controls the behavior of the CommitTransaction method for <see cref="IEditableEntityContext"/> implementations
  /// </summary>
  public enum ReadWriteMode
  {
    /// <summary>
    /// The CommitTransaction method will cause changes to be committed to the database provided that the entity-level permission checks pass.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// The CommitTransaction method will throw an exception
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Similar to ReadWrite mode except that the entity-level permission checks are skipped (it is the responsibility 
    /// of the application to perform permission checks at a higher level).
    /// </summary>
    Workflow,
  }
}