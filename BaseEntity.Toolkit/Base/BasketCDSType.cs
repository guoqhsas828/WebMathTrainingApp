/*
 * BasketCDSType.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Specifies the BasketCDS type.
  /// </summary>
  public enum BasketCDSType
  {
    /// <summary>
    ///  Standard (unfunded)
    /// </summary>
    Unfunded,

    /// <summary>
    ///   Standard funded fixed. (protection seller pays an upfront 
    ///   and receives running premium and remaining principal at maturity)
    /// </summary>
    FundedFixed,

    /// <summary>
    ///   Standard funded floating. (protection seller pays an upfront 
    ///   and receives running premium(spread) + Libor on current notional and 
    ///   remaining notional at maturity)
    /// </summary>
    FundedFloating,

  } // class BasketCDSType
}
