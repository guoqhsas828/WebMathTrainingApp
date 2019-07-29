/*
 * CdxType.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	///   Specifies the CDX type.
	/// </summary>
	public enum CdxType
	{
    /// <summary>
    ///  Standard (unfunded) Index
    /// </summary>
    Unfunded,

    /// <summary>
    ///   Standard funded fixed Index. (protection seller pays an upfront 
    ///   and receives running premium and remaining principal at maturity)
    /// </summary>
    FundedFixed,

    /// <summary>
    ///   Standard funded floating Index. (protection seller pays an upfront 
    ///   and receives running premium + Libor + spread on current notional and 
    ///   remaining notional at maturity)
    /// </summary>
    FundedFloating,

	} // class CdxType
}
