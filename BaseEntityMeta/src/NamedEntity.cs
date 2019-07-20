// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Serves as the base class for extended entities that have a unique Name property
  /// </summary>
  [Entity(SubclassMapping = SubclassMappingStrategy.Hybrid, PropertyMapping = PropertyMappingStrategy.Hybrid,
    AuditPolicy = AuditPolicy.History)]
  public abstract class NamedEntity : AuditedObject
  {
    /// <summary>
    /// Business key for this entity
    /// </summary>
    [StringProperty(IsKey = true, MaxLength = 32)]
    public string Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [StringProperty(MaxLength = 512)]
    public string Description { get; set; }
  }
}