using System;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Risk.Base
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 996, AuditPolicy = AuditPolicy.History, PropertyMapping = PropertyMappingStrategy.Hybrid)]
  public class StringLookup : AuditedObject
  {
    private IList<StringLookupItem> _items;

    /// <summary>
    ///   Gets or sets the name.
    /// </summary>
    /// <value>
    ///   The name.
    /// </value>
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name { get; set; }

    /// <summary>
    ///   Gets or sets the range lookup items.
    /// </summary>
    /// <value>
    ///   The range lookup items.
    /// </value>
    [ComponentCollectionProperty(CollectionType = "bag", ExtendedData = true)]
    public IList<StringLookupItem> Items
    {
      get { return _items ?? (_items = new List<StringLookupItem>()); }
      set { _items = value; }
    }
  }
}
