using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  ///<summary>
  /// Class for mapping of Seniority to recovery rate assumption in the Standard CDS Contract Type
  ///</summary>
  [Serializable]
  [Component(ChildKey = new[] {"Seniority"})]
  public class SeniorityToConvRecoveryMappingItem : BaseEntityObject
  {
    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (ConvRecovery <= 0.0)
      {
        InvalidValue.AddError(errors, this, "ConvRecovery", "Conv. Recovery cannot be zero or negative");
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Seniority as the key
    /// </summary>
    [EnumProperty(AllowNullValue = false)]
    public Seniority Seniority { get; set; }

    /// <summary>
    ///   Recovery Assumption for given seniority
    /// </summary>
    [NumericProperty(Format = NumberFormat.Percentage, AllowNullValue = false)]
    public double ConvRecovery { get; set; }

    #endregion
  }
}