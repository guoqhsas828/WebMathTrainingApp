/*
 * BondType.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
	///   BondType represents the calculation conventions
	///   for various types of bonds.
	/// </summary>
	[BaseEntity.Shared.AlphabeticalOrderEnum]
	public enum BondType
	{
    /// <summary>None</summary>
		None,
    /// <summary>US Treasury Bond</summary>
		USGovt,
    /// <summary>US Corporate</summary>
		USCorp,
    /// <summary>Canadian Govt</summary>
		CADGovt,
    /// <summary>UK Gilt</summary>
		UKGilt,
    /// <summary>Japanese Govt Bond</summary>
		JGB,
    /// <summary>German Govt Bond</summary>
		DEMGovt,
    /// <summary>Italian Govt Bond</summary>
		ITLGovt,
    /// <summary>French Govt Bond</summary>
		FRFGovt,
    /// <summary>Spanish Govt Bond</summary>
		ESPGovt,
    /// <summary>EMU Member Country Govt Bond </summary>
		EURGovt,
		/// <summary>Australian Govt Bond</summary>
		AUSGovt,
		/// <summary>Euro Corporate Bond</summary>
		EURCorp,
    /// <summary> US Treasury Bill</summary>
    USTBill
	}

}
