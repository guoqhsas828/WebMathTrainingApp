/*
 * BondFutureDeliveryDayRule.cs
 *
 *   2011. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{

  ///<summary>
  /// Specifies the first available futures delivery day within the contract month
  ///</summary>
  public enum BondFutureFirstDeliveryDayRule
  {
    /// <summary>
    /// First business day of within the contract month
    /// </summary>
    First,
    ///<summary>
    /// Same day as the last delivery day
    ///</summary>
    LastDelivery
  }

  /// <summary>
  ///   Specifies the last futures delivery/settlement day within the contract month
  /// </summary>
  public enum BondFutureDeliveryDayRule
  {
    /// <summary>
    /// Tenth day within contract month (or following business day if not a business day)
    /// </summary>
    Tenth,
    /// <summary>
    /// Twentieth day within contract month (or following business day if not a business day)
    /// </summary>
    Twentieth,
    /// <summary>
    /// Last business day within contract month
    /// </summary>
    Last,
    /// <summary>
    /// Third business day following last trading day within the contract month
    /// </summary>
    ThirdFollowingLastTradingDay,
    /// <summary>
    /// First business day following last trading day
    /// </summary>
    FollowingLastTradingDay
  } // class BondFutureDeliveryDayRule
}
