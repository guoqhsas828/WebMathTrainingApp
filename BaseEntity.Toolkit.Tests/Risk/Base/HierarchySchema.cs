using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Hierarchy Schema represents the blueprint of the Hierarchy tree which is composed of Hierarchy Elements
  /// </summary>
  [Serializable]
  [Entity(EntityId = 525, PropertyMapping = PropertyMappingStrategy.Hybrid, Key = new[] { "Name" }, AuditPolicy = AuditPolicy.History)]
  public class HierarchySchema : AuditedObject
  {
    #region Properties

    /// <summary>
    /// Name of the Hierarchy Schema
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Name { get; set; }

    /// <summary>
    /// Description of the Hierarchy Schema
    /// </summary>
    [StringProperty(MaxLength = 128, AllowNullValue = true)]
    public string Description { get; set; }

    /// <summary>
    /// The Data Type to which this schema applies
    /// </summary>
    [StringProperty(MaxLength = 256, AllowNullValue = false)]
    public string DataType { get; set; }

    /// <summary>
    /// The name of the 1st level of the Hierarchy
    /// </summary>
    [StringProperty(MaxLength = 128, DisplayName = "Level 1")]
    public string Level1 { get; set; }

    /// <summary>
    /// The name of the 2nd level of the Hierarchy
    /// </summary>
    [StringProperty(MaxLength = 128, DisplayName = "Level 2", AllowNullValue = true)]
    public string Level2 { get; set; }

    /// <summary>
    /// The name of the 3rd level of the Hierarchy
    /// </summary>
    [StringProperty(MaxLength = 128, DisplayName = "Level 3", AllowNullValue = true)]
    public string Level3 { get; set; }

    /// <summary>
    /// The name of the 4th level of the Hierarchy
    /// </summary>
    [StringProperty(MaxLength = 128, DisplayName = "Level 4", AllowNullValue = true)]
    public string Level4 { get; set; }

    /// <summary>
    /// The name of the 5th level of the Hierarchy
    /// </summary>
    [StringProperty(MaxLength = 128, DisplayName = "Level 5", AllowNullValue = true)]
    public string Level5 { get; set; }

    /// <summary>
    /// The name of the 6th level of the Hierarchy
    /// </summary>
    [StringProperty(MaxLength = 128, DisplayName = "Level 6", AllowNullValue = true)]
    public string Level6 { get; set; }


    #endregion

    /// <summary>
    /// Clone method for the Hierarchy Schema
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var other = (HierarchySchema)base.Clone();

      return other;
    }

    /// <summary>
    /// Equality operator based on value eqaulity of key.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public override bool Equals(object other)
    {
      if (!(other is HierarchySchema otherSchema))
      {
        return false;
      }
      return Name == otherSchema.Name;
    }

    /// <summary>
    /// Equality operator based on value eqaulity of key.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    protected bool Equals(HierarchySchema other)
    {
      return string.Equals(Name, other.Name);
    }

    /// <summary>
    /// Hash code function based on value eqaulity of key.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return (Name != null ? Name.GetHashCode() : 0);
    }

    /// <summary>
    /// Validation method for the Hierarchy Schema
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      var properties = new Dictionary<string, string>()
      {
        {"Level1", Level1}, {"Level2", Level2}, {"Level3", Level3}, {"Level4", Level4}, {"Level5", Level5}, {"Level6", Level6}
      };
      var duplicates = new Dictionary<string, string>();
      foreach (var key1 in properties.Keys)
      {
        foreach (var key2 in properties.Keys)
        {
          if (key1 != key2 && !string.IsNullOrEmpty(properties[key1]) && properties[key1] == properties[key2])
          {
            duplicates[key1] = key2;
          }
        }
      }
      foreach (var duplicate in duplicates.Keys)
      {
        InvalidValue.AddError(errors, this, duplicate, $"Duplicate Level Names for {duplicate} and {duplicates[duplicate]}");
      }
    }
  }
}
