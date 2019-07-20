
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

namespace BaseEntity.Database
{
  /// <summary>
  /// Can be used to represent a change to an object or collection
  /// </summary>
  public enum ObjectChangedType
  {
    /// <summary>Inserted</summary>
    Inserted,
    /// <summary>Updated</summary>
    Updated,
    /// <summary>Deleted</summary>
    Deleted,
  }
}