/*
 * CommoditySwap2.cs
 *
 */

using System;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  /// Swap contract composed of two swap legs. Could be both floating, fixed or any combination thereof. 
  /// </summary>
  /// <remarks>
  /// <para>A Commodity Swap is an agreement involving the exchange of a series of commodity price payments
  /// (fixed amount) against variable commodity price payments (market price) resulting exclusively in a
  /// cash settlement (settlement amount).</para>
  /// <para>The buyer of a Commodity Swap acquires the right to be paid a settlement amount (compensation) if
  /// the market price rises above the fixed amount. In contract, the buyer of a Commodity Swap is obliged
  /// to pay the settlement amount if the market price falls below the fixed amount.</para>
  /// <para>The buyer of a commodity Swap acquires the right to be paid a settlement amount, if the market price
  /// rises above the fixed amount. In contract, the seller of a commodity Swap is obligated to pay the
  /// settlement amount if the market price falls below the fixed amount.</para>
  /// <para>Both streams of payment (fixed/variable) are in the same currency and based on the same nominal
  /// amount. While the fixed side of the swap is of the benchmark nature ( it is constant), the variable side
  /// is related to the trading price of the relevant commodities quoted on a stock exchange or otherwise
  /// published on the commodities futures market on the relevant fixing date or to a commodity price
  /// index.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommoditySwapPricer"/>
  [Serializable]
  public class CommoditySwap2 : Product
  {
		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		protected
		CommoditySwap2()
		{}
    
		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="effective">Effective date</param>
		/// <param name="maturity">Maturity date</param>
		/// <param name="ccy">Currency</param>
		/// <param name="premiumPaymentDates">Premium payment dates</param>
		/// <param name="premiumPayments">Premium payments by dates</param>
		/// <param name="paymentOnDefault">Payment in the evnt of default</param>
		/// <param name="swapPeriodBegins">Begin dates of swap determination periods</param>
		/// <param name="swapPeriodEnds">End dates of swap determination periods</param>
		/// <param name="swapPaymentDates">Payment dates of swap determination periods</param>
		/// <param name="commodityQuantities">Commodity quantities by swap determination periods</param>
		/// <param name="commodityFixedPrices">Commodity fixed prices by swap determination periods</param>
		///
		public
		CommoditySwap2( Dt effective, Dt maturity, Currency ccy,
									 Dt [] premiumPaymentDates,
									 double [] premiumPayments,
									 double paymentOnDefault,
									 Dt [] swapPeriodBegins,
									 Dt [] swapPeriodEnds,
									 Dt [] swapPaymentDates,
									 double [] commodityQuantities,
									 double [] commodityFixedPrices )
			: base(effective, maturity, ccy)
		{
		  // TBD: errors check

		  premiumPaymentDates_ = premiumPaymentDates;
			premiumPayments_ = premiumPayments;
			paymentOnDefault_ = paymentOnDefault;
			swapPeriodBegins_ = swapPeriodBegins;
			swapPeriodEnds_ = swapPeriodEnds;
			swapPaymentDates_ = swapPaymentDates;
			commodityQuantities_ = commodityQuantities;
			commodityFixedPrices_ = commodityFixedPrices;
		}

		#endregion Constructors

		#region Properties

    /// <summary>
    ///   Premium payment schedule
    /// </summary>
		public Schedule PremiumPaymentSchedule
		{
		  get {
        Schedule sched = new Schedule(Effective, Effective, Effective, Maturity,
                                       premiumPaymentDates_,
                                       premiumPaymentDates_);
				return sched;
			}
		}

    /// <summary>
    ///   Premium amounts
    /// </summary>
		public double [] PremiumAmounts
		{
		  get {
			  return premiumPayments_;
			}
		}

    /// <summary>
    ///   Swap payment schedule
    /// </summary>
		public Schedule SwapPaymentSchedule
		{
		  get {
        Schedule sched = new Schedule(Effective, swapPeriodBegins_[0], Effective, Maturity,
                                      swapPeriodEnds_, swapPaymentDates_);
				return sched;
			}
		}

    /// <summary>
    ///   Commodity quantities
    /// </summary>
		public double[] CommodityQuantities
		{
		  get {
			  return commodityQuantities_;
			}
		}

    /// <summary>
    ///   Commodity fixed prices
    /// </summary>
		public double[] CommodityFixedPrices
		{
		  get {
			  return commodityFixedPrices_;;
			}
		}

    /// <summary>
    ///   Amount paid on default
    /// </summary>
		public double PaymentOnDefault
		{
		  get {
			  return paymentOnDefault_;
			}
		}

		#endregion Properties

		#region Data

		private Dt [] premiumPaymentDates_;
		private double [] premiumPayments_;
		private double paymentOnDefault_;
		private Dt [] swapPeriodBegins_;
		private Dt [] swapPeriodEnds_;
		private Dt [] swapPaymentDates_;
		private double [] commodityQuantities_;
		private double [] commodityFixedPrices_;

		#endregion Data

  } // class CommoditySwap

}
