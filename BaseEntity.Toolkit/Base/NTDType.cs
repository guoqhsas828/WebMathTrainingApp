/*
 * NTDType.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Specifies the NTD type.
  /// </summary>
  public enum NTDType
  {
    /// <summary>
    ///  Standard (unfunded)
    /// </summary>
    Unfunded,

    /// <summary>
    ///   Standard funded fixed NTD. (protection seller pays an upfront 
    ///   and receives running premium and remaining principal at maturity)
    /// </summary>
    FundedFixed,

    /// <summary>
    ///   Standard funded floating NTD. (protection seller pays an upfront 
    ///   and receives running premium + Libor + spread on current notional and 
    ///   remaining notional at maturity)
    /// </summary>
    FundedFloating,

    /// <summary>
    ///   Standard interest only (IO) funded NTD with fixed premium. 
    ///   Invester receives fixed premium interest only
    /// </summary>
    IOFundedFixed,

    /// <summary>
    ///   Standard interest only (IO) funded NTD with floating premium.
    ///   Investor receives floating premium interst only
    /// </summary>
    IOFundedFloating,

    /// <summary>
    ///   Standard principal only type. Investor receives principal at maturity.
    /// </summary>
    PO,
  }
}