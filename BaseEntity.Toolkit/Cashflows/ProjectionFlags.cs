using System;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Payment flags
  /// </summary>
  [Flags]
  public enum ProjectionFlag
  {
    /// <summary>
    /// None
    /// </summary>
    None = 0x0000,

    /// <summary>
    /// Reset in arrears
    /// </summary>
    ResetInArrears = 0x0001,

    /// <summary>
    /// Market to Market
    /// </summary>
    MarkedToMarket = 0x0002,

    /// <summary>
    /// Zero coupon
    /// </summary>
    ZeroCoupon = 0x0004,

    /// <summary>
    /// True if rate is reset with delay, i.e. 3 month Libor paid semiannualy. 
    /// If there is no compounding, the rate will have to be corrected with a convexity 
    /// adustment 
    /// </summary>
    ResetWithDelay = 0x0008,

    /// <summary>
    /// True to approximate projection: can be used for fast sensitivities for averaging swaps
    /// </summary>
    ApproximateProjection = 0x0010
  }
}