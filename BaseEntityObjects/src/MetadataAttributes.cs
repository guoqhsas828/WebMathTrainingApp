// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Class Attribute
  /// </summary>
  public abstract class ClassAttribute : Attribute
  {
    #region Properties

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public Type Type { get; set; }

    /// <summary>
    /// Category
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// DisplayName
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; set; }

    #endregion Properties
  }

  /// <summary>
  /// 
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public class ComponentAttribute : ClassAttribute
  {
    /// <summary>
    /// 
    /// </summary>
    public string[] ChildKey
    {
      get { return childKey_; }
      set { childKey_ = value; }
    }

    private string[] childKey_;
  }

  /// <summary>
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public abstract class PropertyAttribute : Attribute
  {
    /// <summary>
    /// PropertyMeta name (defaults to property name)
    /// </summary>
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    /// PropertyMeta DisplayName (defaults to Name)
    /// </summary>
    public string DisplayName
    {
      get { return displayName_; }
      set { displayName_ = value; }
    }

    /// <summary>
    /// Indicates if property is user editable (default=false)
    /// </summary>
    public bool ReadOnly
    {
      get { return readOnly_; }
      set { readOnly_ = value; }
    }

    /// <summary>
    /// Indicates if property is mapped to database
    /// </summary>
    public bool Persistent
    {
      get { return persistent_; }
      set { persistent_ = value; }
    }

    /// <summary>
    /// Database column name
    /// </summary>
    /// <remarks>
    /// If the entity does not map to a table, then this must be null.
    /// Otherwise, it defaults to Name.
    /// </remarks>
    public string Column
    {
      get { return column_; }
      set { column_ = value; }
    }

    /// <summary>
    /// Indicates if database column allows null (default=true)
    /// </summary>
    public bool AllowNullValue
    {
      get { return allowNullValue_; }
      set { allowNullValue_ = value; }
    }

    /// <summary>
    /// Name of related property (if any)
    /// </summary>
    public string RelatedProperty
    {
      get { return relatedProperty_; }
      set { relatedProperty_ = value; }
    }

    /// <summary>
    /// Specifies that a unique constraint should be enforced
    /// </summary>
    public bool IsUnique
    {
      get { return isUnique_; }
      set { isUnique_ = value; }
    }

    /// <summary>
    /// Used to specify single field primary key
    /// </summary>
    /// <remarks>
    /// Implies IsUnique=true and IsNullable=false
    /// </remarks>
    public bool IsPrimaryKey
    {
      get { return isPrimaryKey_; }
      set { isPrimaryKey_ = value; }
    }

    /// <summary>
    /// Used to specify single field business key
    /// </summary>
    /// <remarks>
    /// Implies IsUnique=true and IsNullable=false
    /// </remarks>
    public bool IsKey
    {
      get { return isKey_; }
      set { isKey_ = value; }
    }

    /// <summary>
    /// Used to specify if property maps to column or blob in the case where the
    /// PropertyMappingStrategy for the entity is Hybrid (in the other two cases, 
    /// this setting is ignored as it is determined at the entity level).
    /// </summary>
    public bool ExtendedData
    {
      get { return extendedData_; }
      set { extendedData_ = value; }
    }

    /// <summary>
    /// Flag to indicate nullable key column is allowed 
    /// </summary>
    public bool AllowNullableKey
    {
      get { return allowNullableKey_; }
      set { allowNullableKey_ = value; }
    }

    private string name_;
    private string displayName_;
    private bool readOnly_;
    private string column_;
    private bool persistent_ = true;
    private bool allowNullValue_ = true;
    private string relatedProperty_;
    private bool isUnique_;
    private bool isPrimaryKey_;
    private bool isKey_;
    private bool extendedData_;
    private bool allowNullableKey_ = false;
  }

  /// <summary>
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class ObjectIdPropertyAttribute : PropertyAttribute
  {
  }

  /// <summary>
  ///
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class ComponentPropertyAttribute : PropertyAttribute
  {
    /// <summary>
    ///
    /// </summary>
    public string Prefix
    {
      get { return prefix_; }
      set { prefix_ = value; }
    }

    private string prefix_;
  }

  /// <summary>
  /// Abstract base class for all collection property attributes
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public abstract class CollectionPropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// Specifies the type of collection (valid values: "map", "list", "bag")
    /// </summary>
    public string CollectionType
    {
      get { return collectionType_; }
      set { collectionType_ = value; }
    }

    /// <summary>
    /// Specifies join column(s) between the collection and collection owner tables
    /// </summary>
    /// <remarks>
    /// If not specified, will default to a single join column named {entity}Id where
    /// {entity} is the name of the collection owner.  For example, the TradeTags table
    /// has join column TradeId.
    /// </remarks>
    public string[] KeyColumns
    {
      get { return keyColumns_; }
      set { keyColumns_ = value; }
    }

    /// <summary>
    /// Index column (only relevent for indexed collection types)
    /// </summary>
    public string IndexColumn
    {
      get { return indexColumn_; }
      set { indexColumn_ = value; }
    }

    /// <summary>
    /// Type of the index field for this collection (applies to "map" collections only)
    /// </summary>
    public Type IndexType
    {
      get { return indexType_; }
      set { indexType_ = value; }
    }

    /// <summary>
    /// Max length of the index value (applies only to variable length types such as string)
    /// </summary>
    public int IndexMaxLength
    {
      get { return indexMaxLength_; }
      set { indexMaxLength_ = value; }
    }

    private string collectionType_;
    private string[] keyColumns_;
    private string indexColumn_;
    private Type indexType_;
    private int indexMaxLength_;
  }

  /// <summary>
  /// 
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public sealed class ComponentCollectionPropertyAttribute : CollectionPropertyAttribute
  {
    /// <summary>
    ///  Type of collection item
    /// </summary>
    public Type Clazz
    {
      get { return clazz_; }
      set { clazz_ = value; }
    }

    /// <summary>
    ///
    /// </summary>
    public string TableName
    {
      get { return tableName_; }
      set { tableName_ = value; }
    }

    private Type clazz_;
    private string tableName_;
  }

  /// <summary>
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public sealed class ElementCollectionPropertyAttribute : CollectionPropertyAttribute
  {
    /// <summary>
    /// </summary>
    public string TableName
    {
      get { return tableName_; }
      set { tableName_ = value; }
    }

    /// <summary>
    /// </summary>
    public Type ElementType
    {
      get { return elementType_; }
      set { elementType_ = value; }
    }

    /// <summary>
    /// </summary>
    public int ElementMaxLength
    {
      get { return elementMaxLength_; }
      set { elementMaxLength_ = value; }
    }

    /// <summary>
    /// </summary>
    public string ElementColumn
    {
      get { return elementColumn_; }
      set { elementColumn_ = value; }
    }

    private string tableName_;
    private Type elementType_;
    private int elementMaxLength_;
    private string elementColumn_;
  }

  /// <summary>
  /// Used to register a <see cref="System.Boolean">Boolean</see> property with the WebMathTraining metadata runtime.
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class BooleanPropertyAttribute : PropertyAttribute
  {}

  /// <summary>
  /// Used to register a <see cref="System.DateTime">DateTime</see> property with the WebMathTraining metadata runtime.
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class DateTimePropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// Gets or sets a value indicating whether this instance is treated as date only.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is treated as date only; otherwise, <c>false</c>.
    /// </value>
    public bool IsTreatedAsDateOnly { get; set; }
  }

  /// <summary>
  /// Used to register a <see cref="System.Guid">Guid</see> property with the WebMathTraining metadata runtime.
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class GuidPropertyAttribute : PropertyAttribute
  { }

  /// <summary>
  /// Used to register a <see cref="System.Enum">enum</see> property with the WebMathTraining metadata runtime.
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class EnumPropertyAttribute : PropertyAttribute
  {}

  /// <summary>
  /// Used to register a numeric property with the WebMathTraining metadata runtime.
  /// </summary>
  /// <remarks>
  /// The following numeric property types are currently supported:
  /// <list type="bullet">
  /// <item><see cref="System.Int32" /></item>
  /// <item><see cref="System.Int64" /></item>
  /// <item><see cref="System.Double" /></item>
  /// <item><see cref="System.Nullable{Int32}" /></item>
  /// <item><see cref="System.Nullable{Int64}" /></item>
  /// <item><see cref="System.Nullable{Double}" /></item>
  /// </list>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Property)]
  public class NumericPropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// Used to specify the display format
    /// </summary>
    public NumberFormat Format
    {
      get { return format_; }
      set { format_ = value; }
    }

    ///<summary>
    ///
    ///</summary>
    public string FormatString
    {
      get { return formatString_; }
      set { formatString_ = value; }
    }

    #region Data

    private NumberFormat format_;
    private string formatString_;

    #endregion
  }

  /// <summary>
  ///  Used to indicate the display format for the given number
  /// </summary>
  public enum NumberFormat
  {
    /// <summary>Standard format</summary>
    Default,

    /// <summary>Display as currency</summary>
    Currency,

    /// <summary>Display as basis points (e.g. 0.03 => 300)</summary>
    BasisPoints,

    /// <summary>Display as percentage (e.g. 0.03 => 3%)</summary>
    Percentage
  }

  /// <summary>
  /// Specify String property metadata
  /// </summary>
  [AttributeUsage(AttributeTargets.Property)]
  public class StringPropertyAttribute : PropertyAttribute
  {
    /// <summary>
    /// Maximum length of string
    /// </summary>
    public int MaxLength
    {
      get { return maxLength_; }
      set { maxLength_ = value; }
    }

    private int maxLength_;
  }
}