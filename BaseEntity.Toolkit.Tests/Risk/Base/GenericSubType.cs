using System;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Classify products into multiple sub-types. Used to create one-to-many relationship between product and add-on factors for Mtm AddOn Calculation.
  /// </summary>
  [Serializable]
  [Entity(EntityId = 1007, AuditPolicy = AuditPolicy.History, Key = new[] { "Name" })]
  public class GenericSubType : AuditedObject
  {
    #region Persistent Properties

    /// <summary>
    /// Name of the Product Sub-type.
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 128)]
    public string Name { get; set; }

    /// <summary>
    /// Description of the Product sub-type.
    /// </summary>
    [StringProperty(MaxLength = 512)]
    public string Description { get; set; }

    #endregion
  }
}