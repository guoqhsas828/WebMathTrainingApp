
/* Copyright (c) WebMathTraining 2011. All rights reserved. */

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Specified custom attributes of numeric (Int32, Int64, Double) properties
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class IdentityPropertyAttribute : PropertyAttribute
  {
  }

  /// <summary>
  /// Specified custom attributes of numeric (Int32, Int64, Double) properties
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class VersionPropertyAttribute : PropertyAttribute
  {
  }

  /// <summary>
  /// Specify String property metadata
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class XmlPropertyAttribute : PropertyAttribute
  {
  }

  /// <summary>
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class OneToOnePropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// 
    /// </summary>
    public string Fetch { get; set; }

    /// <summary>
    ///
    /// </summary>
    public string Cascade { get; set; }
  }

  /// <summary>
  /// Used to specify the metadata for a one-to-many property
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class OneToManyPropertyAttribute : CollectionPropertyAttribute
  {
    /// <summary>
    ///  Type of collection item
    /// </summary>
    public Type Clazz { get; set; }

    /// <summary>
    /// </summary>
    public string Fetch { get; set; }

    /// <summary>
    ///  Specifies which operations should be propagated from the parent (owner)
    ///  object to the associated object(s).
    /// </summary>
    public string Cascade { get; set; }

    /// <summary>
    ///
    /// </summary>
    public string Adder { get; set; }

    /// <summary>
    ///
    /// </summary>
    public string Remover { get; set; }

    /// <summary>
    ///
    /// </summary>
    public bool IsInverse { get; set; }

    /// <summary>
    ///   Name of the join table. Only applicable when UseJoinTable is set to true
    /// </summary>
    public string JoinTableName { get; set; }

    /// <summary>
    ///   Use join table to link owner and collection items instead 
    ///   of adding a reference to owner in the collection item table
    /// </summary>
    public bool UseJoinTable { get; set; }
  }

  /// <summary>
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class ManyToOnePropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// 
    /// </summary>
    public string Fetch { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string Cascade { get; set; }

    /// <summary>
    /// </summary>
    public Type OwnershipResolverType { get; set; }
  }

  /// <summary>
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class ManyToManyPropertyAttribute : CollectionPropertyAttribute
  {
    /// <summary>
    ///  Type of collection item
    /// </summary>
    public Type Clazz { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string Fetch { get; set; }

    /// <summary>
    /// </summary>
    public string Adder { get; set; }

    /// <summary>
    /// Strategy used for propagating database operations (insert/update/delete) from parent to child
    /// </summary>
    public string Cascade { get; set; }

    /// <summary>
    /// </summary>
    public bool IsInverse { get; set; }

    /// <summary>
    /// </summary>
    public string Remover { get; set; }

    /// <summary>
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public ManyToManyPropertyAttribute()
    {
      Cascade = "none";
    }
  }

  /// <summary>
  /// 
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class BinaryBlobPropertyAttribute : PropertyAttribute
  {
  }

  /// <summary>
  /// Indicates that a property represents an array of doubles.
  /// </summary>
  /// <remarks>
  /// <para>
  /// In general, it is recommended to use <see cref="IList{T}" /> and <see cref="ElementCollectionPropertyAttribute"/>
  /// for representing ordered list of doubles. The exception is where the overhead of the extra collection table is
  /// unacceptable, there is a known limit to the number of doubles stored, and no queries will filter based on the
  /// data in the column.
  /// </para>
  /// <para>
  /// The database layer will map this property to a varbinary column whose size is determined by MaxLength.
  /// </para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public class ArrayOfDoublesPropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// Specific Column Names for the Array of Doubles, this overrides the automatically generated names within the generic form
    /// </summary>
    public string[] ColumnNames { get; set; }

    /// <summary>
    /// Maximum number of doubles in the array
    /// </summary>
    public int MaxLength { get; set; }
  }

  /// <summary>
  /// Indicates that the class represents a database entity.
  /// </summary>
  /// <remarks>
  /// Entities must inherit from <see cref="PersistentObject" />.
  /// </remarks>
  [AttributeUsage(AttributeTargets.Class)]
  public class EntityAttribute : ClassAttribute
  {
    #region Properties

    /// <summary>
    /// Specifies a unique integer identifier for all persistent entities.
    /// </summary>
    /// <remarks>
    /// This identifier is embedded in the ObjectId.
    /// </remarks>
    public int EntityId { get; set; }

    /// <summary>
    /// For persistent entities, specifies the name of the table in the database that instances will be stored in.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// For entities that derive from <see cref="AuditedObject" />, indicates if the database layer should keep
    /// a full history of all inserts/updates/deletes in the AuditLog table.
    /// </summary>
    /// <remarks>
    /// It is recommended that entities that inherit from <see cref="AuditedObject" /> will have AuditPolicy = History.
    /// There may be very rare cases where the overhead of maintaing the audit trail outweighs the benefits.  In this
    /// case, the AuditPolicy should be set to None.
    /// </remarks>
    public AuditPolicy AuditPolicy { get; set; }

    /// <summary>
    /// Indicates the default <see cref="PropertyMappingStrategy" /> mapping strategy for properties of this Entity.
    /// </summary>
    public PropertyMappingStrategy PropertyMapping { get; set; }

    /// <summary>
    /// Indicates the <see cref="SubclassMappingStrategy" /> for this Entity.
    /// </summary>
    /// <remarks>
    /// The SubclassMapping for derived entities is inherited from the BaseEntity.
    /// </remarks>
    public SubclassMappingStrategy SubclassMapping { get; set; }

    /// <summary>
    /// Specifies the property names that form the natural key for this Entity.
    /// </summary>
    /// <remarks>
    /// It is recommended, but not required, that all root entities have a natural key defined.
    /// </remarks>
    public string[] Key { get; set; }

    /// <summary>
    /// Specifies the property names that uniquely identify a ChildEntity within a OneToMany or ManyToMany collection
    /// </summary>
    /// <remarks>
    /// Only child entities can specify a ChildKey.
    /// </remarks>
    public string[] ChildKey { get; set; }

    /// <summary>
    /// Indicates if this entity is a child entity of an aggregate
    /// </summary>
    public bool IsChildEntity { get; set; }

    /// <summary>
    /// Indicate that history is used only for auditing purposes and can be removed once archived.
    /// </summary>
    public bool OldStyleValidFrom { get; set; }

    #endregion
  }

  /// <summary>
  /// Indicates that this <see cref="Type"/> should substitute for the Type associated with the base entity
  /// </summary>
  /// <remarks>
  /// The ability to extend an entity is only supported for entities that satisfy the following rules:
  /// <list type="bullet">
  /// <item><description>Must be an Entity (not a Component)</description></item>
  /// <item><description>Must not have any derived entities (e.g. can extend Bond but not Product)</description></item>
  /// <item><description>Must have a single, protected constructor with no parameters</description></item>
  /// </list>
  /// <para>In order to create instances of the entity, our code uses the CreateInstance method on the
  /// corresponding <see cref="ClassMeta"/>. The <see cref="EntityExtensionAttribute"/> markup causes
  /// the type associated with the entity to be mapped to the derived (replacement) type, so that when
  /// CreateInstance is called an instance of the derived type is created.</para>
  /// <para>All properties associated with the derived type will appear as properties for the
  /// entity in the audit log and any generic UI's</para>
  /// <para>The entity name and table name is taken from the base entity. If you have added any 
  /// persistent properties to the derived type you will need to use genschema to extend your database 
  /// schema (unless the base entity supports extended data and the added properties are defined with 
  /// ExtendedData=true.</para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Class)]
  public class EntityExtensionAttribute : Attribute
  {
  }
}