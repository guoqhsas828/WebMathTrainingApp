using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Representation of a Country
  /// </summary>
  [Serializable]
  [DataContract]
  [Entity(EntityId = 198, AuditPolicy = AuditPolicy.History)]
  public class Country : AuditedObject, IHasTags
  {

    /// <summary>
    /// 
    /// </summary>
    protected Country()
    {}

    private IList<Tag> _tags;

    /// <summary>
    /// 
    /// </summary>
    [DataMember]
    [StringProperty(AllowNullValue = false, MaxLength = 64, IsUnique = true)]
    public string Name { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 2)]
    public string ISOAlpha2 { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 3, IsKey = true)]
    public string ISOAlpha3 { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [NumericProperty(FormatString = "{0:000}")]
    public int? ISONumeric { get; set; }

    /// <summary>
    /// </summary>
    [ComponentCollectionProperty(CollectionType = "bag")]
    public IList<Tag> Tags
    {
      get { return _tags ?? (_tags = new List<Tag>()); }
      set { _tags = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (!String.IsNullOrWhiteSpace(ISOAlpha2))
      {
        if(ISOAlpha2.Length != 2)
          InvalidValue.AddError(errors, this, "ISOAlpha2", "ISOAlpha2 must have 2 charactes");
        if (ISOAlpha2.Any(c => !Char.IsLetter(c)))
          InvalidValue.AddError(errors, this, "ISOAlpha2", "ISOAlpha2 can only have letters.");
      }

      if (!String.IsNullOrWhiteSpace(ISOAlpha3))
      {
        if (ISOAlpha3.Length != 3)
          InvalidValue.AddError(errors, this, "ISOAlpha3", "ISOAlpha3 must have 3 charactes");
        if (ISOAlpha3.Any(c => !Char.IsLetter(c)))
          InvalidValue.AddError(errors, this, "ISOAlpha3", "ISOAlpha3 can only have letters.");
      }

      if (ISONumeric.HasValue)
      {
        if (ISONumeric.Value > 999)
          InvalidValue.AddError(errors, this, "ISONumeric", "ISONumeric can only have 3 digits.");

        if (ISONumeric.Value < 0)
          InvalidValue.AddError(errors, this, "ISONumeric", "ISONumeric must be greater than zero.");
      }
    }
  }
}
