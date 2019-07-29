/*
 * OptionStrikeType.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
	///   Option Strike Types
	/// </summary>
	public enum OptionStrikeType
	{
		/// <summary>None</summary>
		None,
		/// <summary>Struck on Price</summary>
		Price,
		/// <summary>Struck on Yield</summary>
		Yield,
    /// <summary>Struck on Asset Swap Spread</summary>
		AssetSpread
	}

}
