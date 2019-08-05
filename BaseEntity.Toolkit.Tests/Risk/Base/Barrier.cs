using System;
using System.Runtime.Serialization;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A barrier on an option
  /// </summary>
  [Component(ChildKey = new[] {"Type"})]
  [Serializable]
  [DataContract]
  public class Barrier : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Barrier"/> class.
    /// </summary>
    public Barrier()
    {
      Type = OptionBarrierType.None;
      Value = 0;
      MonitoringFrequency = Frequency.Continuous;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Option Barrier type
    /// </summary>
    [EnumProperty]
    [DataMember]
    public OptionBarrierType Type { get; set; }

    /// <summary>
    /// Option barrier
    /// </summary>
    [NumericProperty]
    [DataMember]
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets the monitoring frequency.
    /// </summary>
    [EnumProperty]
    [DataMember]
    public Frequency MonitoringFrequency { get; set; }

    /// <summary>
    /// Is up barrier
    /// </summary>
    public bool IsUp
    {
      get { return (Type == OptionBarrierType.UpIn || Type == OptionBarrierType.UpOut); }
    }

    /// <summary>
    /// Is down barrier
    /// </summary>
    public bool IsDown
    {
      get { return (Type == OptionBarrierType.DownIn || Type == OptionBarrierType.DownOut); }
    }

    /// <summary>
    /// Is touch barrier
    /// </summary>
    public bool IsTouch
    {
      get { return (Type == OptionBarrierType.OneTouch || Type == OptionBarrierType.NoTouch); }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      // Barrier cannot be None
      if (Type == OptionBarrierType.None)
        InvalidValue.AddError(errors, this, "Type", "Type cannot be None");

      // Option Barrier has to be non-negative
      if (Value < 0)
        InvalidValue.AddError(errors, this, String.Format("Invalid barrier. Must be +ve, not ({0})", Value));

      // Monitoring Frequency cannot be None
      if (MonitoringFrequency == Frequency.None)
        InvalidValue.AddError(errors, this, "MonitoringFrequency", "MonitoringFrequency cannot be None");

      // Base
      base.Validate(errors);
    }

    #endregion Methods
  }
}
