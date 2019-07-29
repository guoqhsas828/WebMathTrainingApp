// 
//  -2013. All rights reserved.
// 

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Type of quotes of CDX Options (typically used to build CDX option volatility surface)
  /// </summary>
  public enum CDXOptionQuoteType
  {
    /// <summary>
    /// None - not specified
    /// </summary>
    None,

    /// <summary>
    /// Option is quoted in price
    /// </summary>
    Price,

    /// <summary>
    /// Option is quoted in price volatility
    /// </summary>
    PriceVol,

    /// <summary>
    /// Option is quoted in spread volatility
    /// </summary>
    SpreadVol
  }
}