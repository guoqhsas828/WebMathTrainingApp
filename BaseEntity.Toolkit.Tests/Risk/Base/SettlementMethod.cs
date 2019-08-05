/*
 * SettlementMethod.cs
 *
 *
 */

namespace BaseEntity.Risk
{

  /// <summary>
	///   ISDA99 CDS SettlementMethods
	/// </summary>
	public enum SettlementMethod
	{
		/// <summary>
    ///  Cash settlement
    /// </summary>
		Cash,

		/// <summary>
    ///  Physical settlement
    /// </summary>
		Physical,

		/// <summary>
    ///  Seller has option
    /// </summary>
		Seller,

		/// <summary>
    ///  Buyer has option
    /// </summary>
    Buyer,

    /// <summary>
    ///  Conforms to ISDA Auction Settlement
    /// </summary>
    Auction
	}

}  
