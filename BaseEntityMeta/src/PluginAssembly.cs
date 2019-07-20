// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

using BaseEntity.Configuration;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Defines an assembly that is dynamically loaded by risk as an
  /// extension to the system
  /// </summary>
  [Entity(EntityId = 3, Key = new[] {"Name"}, AuditPolicy = AuditPolicy.History, OldStyleValidFrom = true,
    Description = "Defines an assembly that is dynamically loaded as an extension to the risk system")]
  public class PluginAssembly : AuditedObject
  {
    /// <summary>
    /// Uniquely identifies this plugin assembly
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Name { get; set; }

    /// <summary>
    /// Description of the contents of this plugin
    /// </summary>
    [StringProperty(MaxLength = 512)]
    public string Description { get; set; }

    /// <summary>
    /// Assembly name
    /// </summary>
    [StringProperty(MaxLength = 1024)]
    public string FileName { get; set; }

    /// <summary>
    /// If not enabled this plugin wont be loaded
    /// </summary>
    [BooleanProperty]
    public bool Enabled { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [EnumProperty(AllowNull = false)]
    public PluginType PluginType { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      if (PluginType.ToString() == "0")
        InvalidValue.AddError(errors, this, "PluginType", "Value cannot be empty!");
    }
  }
}