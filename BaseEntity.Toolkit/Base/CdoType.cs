/*
 * CdoType.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
	/// <summary>
	///   Specifies the CDO type.
	/// </summary>
	public enum CdoType
	{
    /// <summary>
    ///  Standard (unfunded) tranche
    /// </summary>
    Unfunded,

    /// <summary>
    ///   Standard funded fixed tranche. (protection seller pays an upfront 
    ///   and receives running premium and remaining principal at maturity)
    /// </summary>
    FundedFixed,

    /// <summary>
    ///   Standard funded floating tranche. (protection seller pays an upfront 
    ///   and receives running premium + Libor + spread on current notional and 
    ///   remaining notional at maturity)
    /// </summary>
    FundedFloating,

    /// <summary>
    ///    Fixed Interest-only funded tranche. (protection seller  pays an upfront and
    ///    receives running on current notional) 
    /// </summary>
    IoFundedFixed,

    /// <summary>
    ///   Interest-only funded tranche. (protection seller pays an upfront and receives 
    ///   running + Libor (+ spread) on current notional) 
    /// </summary>
    IoFundedFloating,

    /// <summary>
    ///   Principal only tranche. (protection seller pays an upfront and receives  
    ///   remaining notional at maturity) 
    /// </summary>
    Po,
	} // class CdoType
}
