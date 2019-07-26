/*
 * SubclassMappingStragegy.cs -
 *
 */

namespace BaseEntity.Metadata
{

  /// <summary>
  ///  Used to define how class inheritance is modeled in the database
  /// </summary>
  public enum SubclassMappingStrategy
  {
    /// <summary>
    /// 
    /// </summary>
    None,

    /// <summary>
    /// All entities (base and derived) has its own table
    /// </summary>
    TablePerSubclass,

    /// <summary>
    /// All entities in a class hierarchy share one common table
    /// </summary>
    TablePerClassHierarchy,

    /// <summary>
    /// Each derived entity can use a separate join table or can share the table with its base entity
    /// </summary>
    Hybrid
  }

}

