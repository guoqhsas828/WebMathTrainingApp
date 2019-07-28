// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///  Free form name/value pair
  /// </summary>
  [Serializable]
  [Component(ChildKey = new[] {"Name"})]
  public class Tag : BaseEntityObject, IComparable<Tag>
  {
    #region Constructors

    /// <summary>
    ///  Construct default instance
    /// </summary>
    public Tag()
    {}

    /// <summary>
    ///  Construct instance with specified name and value
    /// </summary>
    public Tag(string name, string value)
    {
      _name = name;
      _value = value;
    }

    #endregion

    #region Properties

    /// <summary>
    /// </summary>
    [StringProperty(MaxLength = 64, AllowNullValue = false)]
    public string Name
    {
      get { return _name; }
      set { _name = value; }
    }

    /// <summary>
    /// </summary>
    [StringProperty(MaxLength = 256)]
    public string Value
    {
      get { return _value; }
      set { _value = value; }
    }

    #endregion

    #region Data

    private string _name;
    private string _value;

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      return new Tag {Name = Name, Value = Value};
    }

    /// <summary>
    ///   Validate
    /// </summary>
    /// 
    /// <param name="errors"></param>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (string.IsNullOrEmpty(Name))
        InvalidValue.AddError(errors, this, "Name cannot be blank");
    }

    #endregion

    #region IComparable<Tag> Members

    /// <summary>
    ///    CompareTo
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public int CompareTo(Tag other)
    {
      int result = String.Compare(Name, other.Name, StringComparison.Ordinal);
      if (result == 0)
        result = String.Compare(Value, other.Value, StringComparison.Ordinal);
      return result;
    }

    #endregion
  }
}