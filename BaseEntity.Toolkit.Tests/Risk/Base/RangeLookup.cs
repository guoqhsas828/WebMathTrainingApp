using System;
using System.Collections.Generic;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   RangeLookup
  /// </summary>
  [Serializable]
  [Entity(EntityId = 998,
    Name = "RangeLookup",
    PropertyMapping = PropertyMappingStrategy.Hybrid,
    AuditPolicy = AuditPolicy.History,
    TableName = "RangeLookups",
    Key = new[] {"Name"}
    )]
  public class RangeLookup : AuditedObject
  {
    /// <summary>
    ///   Initializes a new instance of the <see cref="RangeLookup" /> class.
    /// </summary>
    public RangeLookup()
    {
      Items = new List<RangeLookupItem>();
    }

    /// <summary>
    ///   Gets or sets the name.
    /// </summary>
    /// <value>
    ///   The name.
    /// </value>
    [StringProperty(MaxLength = 64, AllowNullValue = false)]
    public string Name { get; set; }


    /// <summary>
    ///   Gets or sets the range lookup items.
    /// </summary>
    /// <value>
    ///   The range lookup items.
    /// </value>
    [ComponentCollectionProperty(CollectionType = "bag",
      Clazz = typeof (RangeLookupItem),
      ExtendedData = true)]
    public IList<RangeLookupItem> Items { get; set; }
  }


  /// <summary>
  /// </summary>
  [Serializable]
  [Component]
  public class RangeLookupItem : BaseEntityObject
  {
    /// <summary>
    ///   Gets or sets the top end.
    /// </summary>
    /// <value>
    ///   The top end.
    /// </value>
    [NumericProperty(AllowNullValue = true)]
    public double? TopEnd { get; set; }

    /// <summary>
    ///   Gets or sets the bottom end.
    /// </summary>
    /// <value>
    ///   The bottom end.
    /// </value>
    [NumericProperty(AllowNullValue = true)]
    public double? BottomEnd { get; set; }

    /// <summary>
    ///   Gets or sets the numeric value.
    /// </summary>
    /// <value>
    ///   The numeric value.
    /// </value>
    [NumericProperty(AllowNullValue = true)]
    public double? NumericValue { get; set; }


    /// <summary>
    ///   Gets or sets the string value.
    /// </summary>
    /// <value>
    ///   The string value.
    /// </value>
    [StringProperty(MaxLength = 128, AllowNullValue = true)]
    public string StringValue { get; set; }
  }
}