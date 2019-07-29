// 
//  -2012. All rights reserved.
// 

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Option strike determination method
  /// </summary>
  /// <remarks>
  /// How option strike is calculated.
  /// </remarks>
  public enum OptionStrikeDeterminationMethod
  {
    /// <summary>Fixed strike (regular)</summary>
    Fixed,
    /// <summary>Average quote</summary>
    Average,
    /// <summary>Underlying quote at expiration (look-back floating)</summary>
    LookbackFloating,
    /// <summary>Optimal (look-back)</summary>
    Lookback
  }
}
