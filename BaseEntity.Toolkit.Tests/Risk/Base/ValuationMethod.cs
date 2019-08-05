/*
 * ValuationMethod.cs
 *
 */

namespace BaseEntity.Risk
{
	/// <summary>
	///   The ISDA defined methodology for determining the final
	///   price of the reference obligation for purposes of cash
	///   settlement.
	/// </summary>
	public enum ValuationMethod
	{
		/// <summary>Market</summary>
		Market,
		/// <summary>Highest</summary>
		Highest,
		/// <summary>Average market</summary>
		AverageMarket,
		/// <summary>Average highest</summary>
		AverageHighest,
		/// <summary>Blended market</summary>
		BlendedMarket,
		/// <summary>Blended highest</summary>
		BlendedHighest,
		/// <summary>Average blended market</summary>
		AverageBlendedMarket,
		/// <summary>Average blended highest</summary>
		AverageBlendedHighest,
  } // enum ValuationMethod
}

