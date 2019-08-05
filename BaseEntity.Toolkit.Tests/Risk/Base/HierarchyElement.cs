using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Hierarchy Element Level
  /// </summary>
  public enum HierarchyLevel
  {
    /// <summary>
    /// Level 1
    /// </summary>
    Level1 = 1,
    /// <summary>
    /// Level 2
    /// </summary>
    Level2 = 2,
    /// <summary>
    /// Level 3
    /// </summary>
    Level3 = 3,
    /// <summary>
    /// Level 4
    /// </summary>
    Level4 = 4,
    /// <summary>
    /// Level 5
    /// </summary>
    Level5 = 5,
    /// <summary>
    /// Level 6
    /// </summary>
    Level6 = 6
  }

  /// <summary>
  /// Hierarchy Element represents a node within the Hierarchy tree which is defined within the Hierarchy Schema
  /// </summary>
  [Serializable]
  [Entity(EntityId = 526,
    PropertyMapping = PropertyMappingStrategy.Hybrid,
    Key = new[] { "Name", "HierarchySchema", "Level", "Parent" },  
    AuditPolicy = AuditPolicy.History)]
  public class HierarchyElement : AuditedObject
  {
    #region Persistent Properties

    /// <summary>
    /// The name of the Hierarchy Element
    /// </summary>
    [StringProperty(MaxLength = 128)]
    public string Name { get; set; }

    ///// <summary>
    ///// Display Name 
    ///// </summary>
    //[StringProperty(MaxLength = 128)]
    //public string DisplayName { get; set; }

    /// <summary>
    /// The description of the Hierarchy Element
    /// </summary>
    [StringProperty(MaxLength = 128, AllowNullValue = true)]
    public string Description { get; set; }

    /// <summary>
    /// The Hierarchy Schema associated with the Hierarchy Element
    /// </summary>
    [ManyToOneProperty]
    public HierarchySchema HierarchySchema
    {
      get { return (HierarchySchema)ObjectRef.Resolve(_hierarchySchema); }
      set { _hierarchySchema = ObjectRef.Create(value); }
    }

    /// <summary>
    /// The Level of the Hierarchy Element within the Hierarchy tree defined within the associated HierarchySchema
    /// </summary>
    [EnumProperty]
    public HierarchyLevel Level { get; set; }

    /// <summary>
    /// The parent of the Hierarchy Element
    /// Will be null if the Level is 1 (root of the Hierarchy tree)
    /// </summary>
    [ManyToOneProperty(AllowNullableKey = true)]
    public HierarchyElement Parent
    {
      get { return (HierarchyElement)ObjectRef.Resolve(_parent); }
      set { _parent = ObjectRef.Create(value); }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Clone method for the Hierarchy Element
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var other = (HierarchyElement)base.Clone();

      return other;
    }

    /// <summary>
    /// Validation method for the Hierarchy Element
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (Level == HierarchyLevel.Level1 && HierarchySchema.Level1 == null)
      {
        InvalidValue.AddError(errors, HierarchySchema, "HierarchySchema.Level1", "HierarchySchema does not define a L1 name.");
      }

      if (Level == HierarchyLevel.Level2 && HierarchySchema.Level2 == null)
      {
        InvalidValue.AddError(errors, HierarchySchema, "HierarchySchema.Level2", "HierarchySchema does not define a L2 name.");
      }

      if (Level == HierarchyLevel.Level3 && HierarchySchema.Level3 == null)
      {
        InvalidValue.AddError(errors, HierarchySchema, "HierarchySchema.Level3", "HierarchySchema does not define a L3 name.");
      }

      if (Level == HierarchyLevel.Level4 && HierarchySchema.Level4 == null)
      {
        InvalidValue.AddError(errors, HierarchySchema, "HierarchySchema.Level4", "HierarchySchema does not define a L4 name.");
      }

      if (Level == HierarchyLevel.Level5 && HierarchySchema.Level5 == null)
      {
        InvalidValue.AddError(errors, HierarchySchema, "HierarchySchema.Level5", "HierarchySchema does not define a L5 name.");
      }

      if (Level == HierarchyLevel.Level6 && HierarchySchema.Level6 == null)
      {
        InvalidValue.AddError(errors, HierarchySchema, "HierarchySchema.Level6", "HierarchySchema does not define a L6 name.");
      }

      if (Level != HierarchyLevel.Level1 && Parent == null)
      {
        InvalidValue.AddError(errors, Parent, "Parent", "Unless Level is 1 Parent must be defined for the Hierarchy Element.");
      }

      if (Parent != null && ((int)Parent.Level + 1) != (int)Level)
      {
        InvalidValue.AddError(errors, Parent, "Parent", "The Parent Defined must exist at the prior Level.");
      }
    }

    /// <summary>
    /// Hash Code for the Hierarchy Element
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      var hashCode = Name.GetHashCode() ^ Level.GetHashCode() ^ HierarchySchema.Name.GetHashCode();
      if (Parent != null)
      {
        hashCode ^= Parent.GetHashCode();
      }
      return hashCode;
    }

    /// <summary>
    /// Equality Operator for Hierarchy Element
    /// </summary>
    /// <param name="other">Object which is compared to</param>
    /// <returns>True if the other class is equal to this class</returns>
    public override bool Equals(object other)
    {
      var otherHierarchyElement = other as HierarchyElement;
      if (otherHierarchyElement == null)
      {
        return false;
      }
      if (Parent == null && otherHierarchyElement.Parent == null)
      {
        return otherHierarchyElement.Name == Name && otherHierarchyElement.Level == Level &&
               otherHierarchyElement.HierarchySchema.Name == HierarchySchema.Name;
      }
      if (Parent == null || otherHierarchyElement.Parent == null)
      {
        return false;
      }
      return otherHierarchyElement.Name == Name && otherHierarchyElement.Level == Level &&
             otherHierarchyElement.HierarchySchema.Name == HierarchySchema.Name && otherHierarchyElement.Parent.Equals(Parent);
    }

    /// <summary>
    /// ToString method for the Hierarchy Element
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      var str = $"Name: {Name} | Level: {Level} | HierarchySchema: {HierarchySchema.Name}";
      if (Parent != null)
      {
        str += $" | Parent : {this.XPath(false)}";
      }
      return str;
    }

    #endregion

    #region Data

    private ObjectRef _hierarchySchema;
    private ObjectRef _parent;

    #endregion
  }
}
