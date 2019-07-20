/*
 * PropertyMappingStrategy.cs -
 *
 * Copyright (c) WebMathTraining 2010. All rights reserved.
 *
 */

namespace BaseEntity.Metadata
{

  /// <summary>
  /// Specifies how persistent properties are stored in the database
  /// </summary>
  public enum PropertyMappingStrategy
  {
    /// <summary>
    /// Used for all properties of components (the mapping of components is
    /// defined by the containing entity)
    /// </summary>
    None,

    /// <summary>
    /// All properties are stored in normal relational form.  No
    /// ExtendedData column will be created, and therefore it will
    /// not be possible to define ExtendedData properties.
    /// </summary>
    RelationalOnly,

    /// <summary>
    /// Only valid for derived entities.  All properties of the subclass 
    /// are stored in an ExtendedData column.  No join table will be created,
    /// even if using TablePerSubclass.
    /// </summary>
    ExtendedOnly,

    /// <summary>
    /// At least one property is stored as a column, however ExtendedData
    /// properties are supported as well.  Supported for both base and
    /// derived entities.
    /// </summary>
    Hybrid
  }
}

