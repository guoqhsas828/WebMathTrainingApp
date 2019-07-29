/*
 * DistributionType.cs
 *
 *  -2011. All Rights Reserved.
 *
 */

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///  The underlying distribution of volatility model.
  /// </summary>
  public enum DistributionType
  {
    /// <summary>
    ///  Log-normal volatility.
    /// </summary>
    LogNormal,
    /// <summary>
    ///  Normal valitility
    /// </summary>
    Normal,
    /// <summary>
    ///  Shifted log-normal volatility.
    /// </summary>
    ShiftedLogNormal
  }
}
