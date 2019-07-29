namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Compounding convention
  /// </summary>
  public enum CompoundingConvention
  {
    /// <summary>
    /// No compounding
    /// </summary>
    None = 0,

    /// <summary>
    /// Compound ISDA convention
    /// </summary>
    ISDA = 1,

    /// <summary>
    /// Compound flat ISDA convention
    /// </summary>
    FlatISDA = 2,

    /// <summary>
    /// No Compound, just simple sum
    /// </summary>
    Simple=3
  }
}