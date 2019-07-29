// 
//  -2012. All rights reserved.
// 

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Option underlying price determination method.
  /// </summary>
  /// <remarks>
  /// How underlying price of option is calculated.
  /// </remarks>
  public enum OptionUnderlyingDeterminationMethod
  {
    /// <summary>Underlying Quote</summary>
    Regular,
    /// <summary>Average price (Asian)</summary>
    Average,
    /// <summary>Optimal (look-back)</summary>
    Lookback
  }
}
