/*
 * BondFutureLastTradingDayRule.cs
 *
 *   2011. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Specifies the last futures trading day within the contract month
  /// </summary>
  public enum BondFutureLastTradingDayRule
  {
    /// <summary>
    /// Two business days prior to contract date
    /// </summary>
    TwoBeforeDelivery,
    /// <summary>
    /// Seven business days prior to delivery day in contract month
    /// </summary>
    SevenBeforeDelivery,
    /// <summary>
    /// Eight business days prior to delivery day in contract month
    /// </summary>
    EighthBeforeDelivery,
    /// <summary>
    /// Fifteenth day of contract month or next business day
    /// </summary>
    Fifteenth,
    /// <summary>
    /// Last business day of contract month
    /// </summary>
    Last,
  } // class BondFutureLastTradingDayRule
}
