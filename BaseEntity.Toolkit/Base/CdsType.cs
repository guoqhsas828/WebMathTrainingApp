/*
 * CdsType.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	///   Specifies the CDS type.
	/// </summary>
	public enum CdsType
	{
    /// <summary>
    ///  Standard (unfunded) Index
    /// </summary>
    Unfunded = 0,

    /// <summary>
    ///   Standard funded fixed Index. (protection seller pays an upfront 
    ///   and receives running premium and remaining principal at maturity)
    /// </summary>
    FundedFixed = 1,

    /// <summary>
    ///   Standard funded floating Index. (protection seller pays an upfront 
    ///   and receives running premium + Libor + spread on current notional and 
    ///   remaining notional at maturity)
    /// </summary>
    FundedFloating = 2,

	} // class CdsType

  /// <summary>
  ///   CDS quote type
  /// </summary>
  public enum CDSQuoteType
  {
    /// <summary>
    ///   Par spread quote
    /// </summary>
    ParSpread = 0,

    /// <summary>
    ///  Upfront fee quote
    /// </summary>
    Upfront = 1,

    /// <summary>
    ///  Conventional spread quote
    /// </summary>
    ConvSpread = 2,
  }

}
