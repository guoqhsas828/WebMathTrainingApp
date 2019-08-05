/*
 * QuotationRateType.cs
 *
 *
 */

namespace BaseEntity.Risk
{
	/// <summary>
	///   The specification of the type of quotation rate to be
	///   obtained from each cash settlement reference bank.
	/// </summary>
	///
	/// <remarks>
	///   <para>ISDA 2003 Term: Quotation Method</para>
	/// </remarks>
	///
	public enum QuotationRateType
	{
		/// <summary>A bid rate.</summary>
		Bid,
		/// <summary>An ask rate.</summary>
		Ask,
		/// <summary>A mid-market rate.</summary>
		Mid,
		/// <summary>
    /// If optional early termination is applicable
		/// to a swap transaction, the rate, which may be a bid
		/// or ask rate, which would result, if seller is
		/// in-the-money, in the higher absolute value of the
		/// cash settlement amount, or, is seller is out-of-the-money,
		/// in the lower absolute value of the cash settlement amount.
    /// </summary>
		ExercisingPartyPays,

  } // enum QuotationRateType
}  
