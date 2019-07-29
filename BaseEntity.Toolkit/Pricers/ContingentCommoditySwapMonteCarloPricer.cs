/*
 * ContingentCommoditySwapMonteCarloPricer.cs
 *
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
  ///
	/// <summary>
	///   <para>Price a <see cref="CommoditySwap2">Commodity Swap</see> using the
	///   <see cref="BaseEntity.Toolkit.Models.ContingentCommoditySwapMonteCarloModel">Cross Currency Hybrid Monte Carlo Model</see>.</para>
	/// </summary>
	///
	/// <seealso cref="CommoditySwap2">Commodity Swap Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.ContingentCommoditySwapMonteCarloModel">Cross Currency Hybrid Monte Carlo Model</seealso>
	///
  [Serializable]
  public class ContingentCommoditySwapMonteCarloPricer : PricerBase, IPricer
  {
		// Logger
		private static readonly log4net.ILog
		logger=log4net.LogManager.GetLogger(typeof(ContingentCommoditySwapMonteCarloPricer));

		#region Constructors
		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		///
		protected
		ContingentCommoditySwapMonteCarloPricer(IProduct product)
			: base(product)
		{}

		/// <summary>
		///   Constructor.
		/// </summary>
		///
		/// <param name="product">Commodity swap product to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount curve</param>
		/// <param name="price0">Initial level of commodity price</param>
		/// <param name="lambda0">Initial level of default rate intensity</param>
		/// <param name="correlation">Correlation coefficient for commodity prices and default intensity</param>
		/// <param name="kappaP">Mean reversion for commodity prices</param>
		/// <param name="thetaP">Long run mean for commodity prices</param>
		/// <param name="sigmaP">Volatility curve of commodity prices</param>
		/// <param name="kappaL">Mean reversion for default rate intensity</param>
		/// <param name="thetaL">Long run mean for default rate intensity</param>
		/// <param name="sigmaL">Volatility of default rate intensity</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		/// <param name="simulations">Number of simulated paths</param>
		///
		public
		ContingentCommoditySwapMonteCarloPricer( CommoditySwap2 product,
																						 Dt asOf, Dt settle,
																						 DiscountCurve discountCurve,
																						 double price0, double lambda0,
																						 double correlation,
																						 double kappaP, Curve thetaP, Curve sigmaP,
																						 double kappaL, double thetaL, double sigmaL,
																						 int stepSize, TimeUnit stepUnit,
																						 int simulations )
			: base(product, asOf, settle)
		{
		  // TBD: errors check

		  discountCurve_ = discountCurve;
			price0_ = price0;
			lambda0_ = lambda0;
			correlation_ = correlation;
			kappaP_ = kappaP;
			thetaP_ = thetaP;
			sigmaP_ = sigmaP;
			kappaL_ = kappaL;
			thetaL_ = thetaL;
			sigmaL_ = sigmaL;
			stepSize_ = stepSize;
			stepUnit_ = stepUnit;
			simulations_ = simulations;
		}

		#endregion // Constructors

		#region Methods

		private double[] CalculatePvs()
		{
		  double[] tmp = new double[4];
			ContingentCommoditySwapMonteCarloModel.ComputePvs( this.Settle,
										this.CommoditySwap.PremiumPaymentSchedule,
										this.CommoditySwap.PremiumAmounts,
										this.CommoditySwap.PaymentOnDefault,
										this.CommoditySwap.SwapPaymentSchedule,
										this.CommoditySwap.CommodityQuantities,
										this.CommoditySwap.CommodityFixedPrices,
										this.DiscountCurve,
										price0_, lambda0_, correlation_,
										kappaP_, ThetaP, SigmaP,
										kappaL_, thetaL_, sigmaL_,
										stepSize_, stepUnit_, simulations_,
										tmp );
			return tmp;
		}


		/// <summary>
		///  Pv of the deal
		/// </summary>
		public override double ProductPv()
		{
		  double[] results = CalculatePvs();
			return (results[0] + results[1]) / DiscountCurve.Interpolate( AsOf ) * Notional;
		}

    /// <summary>
		///  Pv of the deal
		/// </summary>
		public double[] Values()
		{
		  double[] results = CalculatePvs();
			double asOfDf = DiscountCurve.Interpolate( AsOf );
			results[0] /= asOfDf;
			results[1] /= asOfDf;
			results[2] /= asOfDf;
			results[3] /= asOfDf;
			return results;
		}

		#endregion // Methods

		#region Properties
		/// <summary>
		///  Commdity swap
		/// </summary>
		public CommoditySwap2 CommoditySwap
		{
		  get {
			  return (CommoditySwap2) Product;
			}
		}

		/// <summary>
		///  Commdity swap
		/// </summary>
		public Curve ThetaP
		{
		  get {
			  return thetaP_;
			}
		}

		/// <summary>
		///  Commdity swap
		/// </summary>
		public Curve SigmaP
		{
		  get {
			  return sigmaP_;
			}
		}

		/// <summary>
		///  Discount curve
		/// </summary>
		public DiscountCurve DiscountCurve
		{
		  get {
			  return discountCurve_;
			}
		}
		#endregion // Properties

		#region Data

		private DiscountCurve discountCurve_;
		private double price0_;
		private double lambda0_;
		private double correlation_;
		private double kappaP_;
		private Curve thetaP_;
		private Curve sigmaP_;
		private double kappaL_;
		private double thetaL_;
		private double sigmaL_;
		private int stepSize_;
		private TimeUnit stepUnit_;
		private int simulations_;

		#endregion Data

	} // class ContingentCommoditySwapMonteCarloPricer

}

