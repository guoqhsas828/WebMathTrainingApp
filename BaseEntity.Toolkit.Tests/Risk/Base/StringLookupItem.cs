using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk.Base
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Component(ChildKey = new[] {"Name"})]
  public class StringLookupItem : BaseEntityObject
  {
    /// <summary>
    ///   Gets or sets the name.
    /// </summary>
    /// <value>
    ///   The name.
    /// </value>
    [StringProperty(MaxLength = 64, AllowNullValue = false)]
    public string Name { get; set; }

    /// <summary>
    ///   Gets or sets the name.
    /// </summary>
    /// <value>
    ///   The name.
    /// </value>
    [StringProperty(MaxLength = 128, AllowNullValue = true)]
    public string StringValue { get; set; }

    /// <summary>
    ///   Gets or sets the name.
    /// </summary>
    /// <value>
    ///   The name.
    /// </value>
    [NumericProperty(AllowNullValue = true)]
    public double? NumericValue { get; set; }
  }
}
