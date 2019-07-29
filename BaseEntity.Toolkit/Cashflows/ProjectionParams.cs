using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Coupon type
  /// </summary>
  public enum SpreadType
  {
    /// <summary>
    /// None specified
    /// </summary>
    None,
    /// <summary>
    /// Spread is multiplicative
    /// </summary>
    Multiplicative,
    /// <summary>
    /// Spread is additive
    /// </summary>
    Additive
  }


  /// <summary>
  /// Projection information
  /// </summary>
  [Serializable]
  public struct ProjectionParams
  {
    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType ProjectionType { get; set; }

    /// <summary>
    /// Projection flags
    /// </summary>
    public ProjectionFlag ProjectionFlags { get; set; }

    /// <summary>
    /// Compounding convention
    /// </summary>
    public CompoundingConvention CompoundingConvention { get; set; }

    /// <summary>
    /// Determines whether floating coupon is of the form coupon * Fixing or Fixing + coupon.
    /// </summary>
    public SpreadType SpreadType { get; set; }

    /// <summary>
    /// Contractually set base value
    /// </summary>
    public double BaseValue { get; set; }

    /// <summary>
    /// Lag between fixing date and period start
    /// </summary>
    public Tenor ResetLag { get; set; }

    /// <summary>
    /// Compounding frequency
    /// </summary>
    public Frequency CompoundingFrequency { get; set; }

    /// <summary>
    /// Year on year rate tenor
    /// </summary>
    public Tenor YoYRateTenor { get; set; }

    /// <summary>
    /// Indexation method for forward inflation fixing calculation
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }
  }
}