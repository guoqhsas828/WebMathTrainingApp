// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// A barrier on an option
  /// </summary>
  [Serializable]
  public class Barrier : BaseEntityObject
  {
    #region Properties

    /// <summary>
    /// Type of barrier
    /// </summary>
    public OptionBarrierType BarrierType { get; set; }

    /// <summary>
    /// Barrier level
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Barrier monitoring frequency.
    /// </summary>
    public Frequency MonitoringFrequency { get; set; }

    /// <summary>
    /// Is up barrier
    /// </summary>
    public bool IsUp
    {
      get { return (BarrierType == OptionBarrierType.UpIn || BarrierType == OptionBarrierType.UpOut); }
    }

    /// <summary>
    /// Is down barrier
    /// </summary>
    public bool IsDown
    {
      get { return (BarrierType == OptionBarrierType.DownIn || BarrierType == OptionBarrierType.DownOut); }
    }

    /// <summary>
    /// Is touch barrier
    /// </summary>
    internal bool IsTouch
    {
      get { return (BarrierType == OptionBarrierType.OneTouch || BarrierType == OptionBarrierType.NoTouch); }
    }

    /// <summary>
    /// No barrier
    /// </summary>
    public static readonly Barrier None = new Barrier {BarrierType = OptionBarrierType.None};

    /// <summary>
    /// Is out barrier
    /// </summary>
    public bool IsOut
    {
      get { return (BarrierType == OptionBarrierType.UpOut || BarrierType == OptionBarrierType.DownOut); }
    }
    /// <summary>
    /// Is in barrier
    /// </summary>
    public bool IsIn
    {
      get { return (BarrierType == OptionBarrierType.UpIn || BarrierType == OptionBarrierType.DownIn); }
    }
    #endregion Properties

    #region Methods

    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the lifecycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      var obj = (Barrier)base.Clone();
      obj.BarrierType = BarrierType;
      obj.Value = Value;
      obj.MonitoringFrequency = MonitoringFrequency;
      return obj;
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      // Barrier cannot be None
      if (BarrierType == OptionBarrierType.None)
        InvalidValue.AddError(errors, this, "BarrierType", "BarrierType cannot be None");

      // Option Barrier has to be non-negative
      if (Value < 0)
        InvalidValue.AddError(errors, this, String.Format("Invalid barrier. Must be +ve, not ({0})", Value));

      // Monitoring Frequency cannot be None
      if (MonitoringFrequency == Frequency.None)
        InvalidValue.AddError(errors, this, "MonitoringFrequency", "MonitoringFrequency cannot be None");

      // Base
      base.Validate(errors);
    }

    #endregion
  }

  /// <summary>
  ///   Specification of the payoff time.
  /// </summary>
  /// <remarks></remarks>
  public enum BarrierOptionPayoffTime
  {
    /// <summary>Not explicitly specified.  The actual time is implied from the context.</summary>
    Default,
    /// <summary>Pay at barrier hit.</summary>
    AtBarrierHit,
    /// <summary>Pay at option expiry.</summary>
    AtExpiry,
  }

  /// <summary>
  ///   Utility methods related to barrier manipulation
  /// </summary>
  public static class BarrierUtility
  {
    /// <summary>
    ///   Add a barrier to a list of the barriers
    /// </summary>
    /// <param name="barriers">The list of barriers, or null if a new list is requested</param>
    /// <param name="type">The type of the barrier to add</param>
    /// <param name="level">The level of the barrier to add</param>
    /// <returns>The barrier list</returns>
    /// <remarks>The original list is not modified if the barrier type is None or the barrier level is NaN.</remarks>
    public static IList<Barrier> Add(this IList<Barrier> barriers,
      OptionBarrierType type, double level)
    {
      if (type == OptionBarrierType.None || Double.IsNaN(level))
        return barriers;
      var barrier = new Barrier {BarrierType = type, Value = level};
      if (barriers == null) return new List<Barrier> {barrier};
      barriers.Add(barrier);
      return barriers;
    }

    internal static Barrier ToOutType(this Barrier barrier)
    {
      barrier = (Barrier) barrier.Clone();
      switch (barrier.BarrierType)
      {
      case OptionBarrierType.UpIn:
        barrier.BarrierType = OptionBarrierType.UpOut;
        return barrier;
      case OptionBarrierType.DownIn:
        barrier.BarrierType = OptionBarrierType.DownOut;
        return barrier;
      }
      throw new ArgumentException($"Barrier type must be UpIn or DownIn");
    }
  }
}