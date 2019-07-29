/*
 * ContingentSwapLegPricer.cs
 *
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{

	///
  /// <summary>
	///   Pricer for credit-contingent interest rate swap leg
	/// </summary>
	///
	/// <seealso cref="BaseEntity.Toolkit.Products.ContingentSwapLeg">Contingent Swap Leg Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.ContingentCredit">Contingent Credit Model</seealso>
	///
  [Serializable]
 	public class ContingentSwapLegPricer : PricerBase, IPricer
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="swapLeg">Contingent Swap Leg to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="coupon">Floating rate coupon for last reset</param>
		/// <param name="recovery">Recovery rate</param>
		/// <param name="r0">Initial level for short rate</param>
		/// <param name="rKappa">Mean reversion for interest rate</param>
		/// <param name="rTheta">Long run mean for interest rate</param>
		/// <param name="rSigma">Volatility of interest rate</param>
		/// <param name="l0">Initial level for default rate intensity</param>
		/// <param name="lKappa">Mean reversion for default rate intensity</param>
		/// <param name="lTheta">Long run mean for default rate intensity</param>
		/// <param name="lSigma">Volatility of default rate intensity</param>
		/// <param name="rho">Correlation coefficient for interest rates and default intensity</param>
		///
		public
		ContingentSwapLegPricer(ContingentSwapLeg swapLeg,
														Dt asOf, Dt settle, double coupon, double recovery,
														double r0, double rKappa, double rTheta, double rSigma,
														double l0, double lKappa, double lTheta, double lSigma,
														double rho
														)
			: base(swapLeg, asOf, settle)
		{
			Coupon = coupon;
			RecoveryRate = recovery;
			R0 = r0;
			RKappa = rKappa;
			RTheta = rTheta;
			RSigma = rSigma;
			L0 = l0;
			LKappa = lKappa;
			LTheta = lTheta;
			LSigma = lSigma;
			Rho = rho;
		}

		#endregion // Constructors

		#region Methods

    /// <summary>
    ///   Calculates present value of cash flows
    /// </summary>
		///
    /// <returns>Pv of contingent Swap Leg</returns>
		///
    public override double
		ProductPv()
		{
			int steps = 12;
			double pv;
			if( ContingentSwapLeg.Index == null )
			{
				pv = Toolkit.Models.ContingentCredit.FixedPv(AsOf,
																											ContingentSwapLeg.Effective,
																											ContingentSwapLeg.FirstCoupon,
																											ContingentSwapLeg.Maturity,
																											ContingentSwapLeg.Freq,
																											ContingentSwapLeg.Calendar,
																											ContingentSwapLeg.BDConvention,
																											ContingentSwapLeg.DayCount,
																											ContingentSwapLeg.Coupon,
																											steps, R0, L0, Rho,
																											RKappa, RTheta, RSigma,
																											LKappa, LTheta, LSigma);
			}
			else
			{
				pv = Toolkit.Models.ContingentCredit.FloatingPv(AsOf,
																												 ContingentSwapLeg.Effective,
																												 ContingentSwapLeg.FirstCoupon,
																												 ContingentSwapLeg.Maturity,
																												 ContingentSwapLeg.Freq,
																												 ContingentSwapLeg.Calendar,
																												 ContingentSwapLeg.BDConvention,
																												 ContingentSwapLeg.DayCount,
																												 ContingentSwapLeg.Coupon,
																												 ContingentSwapLeg.DayCount,
																												 Coupon,
																												 steps, R0, L0, Rho,
																												 RKappa, RTheta, RSigma,
																												 LKappa, LTheta, LSigma);
			}

			return pv * Notional;
		}

		#endregion // Methods

		#region Properties

		/// <summary>
		///   Product
		/// </summary>
		public ContingentSwapLeg ContingentSwapLeg
		{
			get { return (ContingentSwapLeg)Product; }
		}


		/// <summary>
		///   Current floating rate coupon
		/// </summary>
		public double Coupon
		{
			get { return coupon_; }
			set { coupon_ = value; }
		}


		/// <summary>
		///   Recovery rate
		/// </summary>
		public double RecoveryRate
		{
			get { return recovery_; }
			set { recovery_ = value; }
		}


		/// <summary>
		///   Initial level for short rate
		/// </summary>
		public double R0
		{
			get { return r0_; }
			set { r0_ = value; }
		}


		/// <summary>
		///   Mean reversion for interest rate
		/// </summary>
		public double RKappa
		{
			get { return rKappa_; }
			set { rKappa_ = value; }
		}


		/// <summary>
		///   Long run mean for interest rate
		/// </summary>
		public double RTheta
		{
			get { return rTheta_; }
			set { rTheta_ = value; }
		}


		/// <summary>
		///   Volatility of interest rate
		/// </summary>
		public double RSigma
		{
			get { return rSigma_; }
			set { rSigma_ = value; }
		}


		/// <summary>
		///   Initial level for default rate intensity
		/// </summary>
		public double L0
		{
			get { return l0_; }
			set { l0_ = value; }
		}


		/// <summary>
		///   Mean reversion for default rate intensity
		/// </summary>
		public double LKappa
		{
			get { return lKappa_; }
			set { lKappa_ = value; }
		}


		/// <summary>
		///   Long run mean for default rate intensity
		/// </summary>
		public double LTheta
		{
			get { return lTheta_; }
			set { lTheta_ = value; }
		}


		/// <summary>
		///   Volatility of default rate intensity
		/// </summary>
		public double LSigma
		{
			get { return lSigma_; }
			set { lSigma_ = value; }
		}


		/// <summary>
		///   Correlation coefficient for interest rates and default intensity
		/// </summary>
		public double Rho
		{
			get { return rho_; }
			set { rho_ = value; }
		}

		#endregion // Properties

		#region Data

		private double coupon_;
		private double recovery_;
		private double r0_;
		private double rKappa_;
		private double rTheta_;
		private double rSigma_;
		private double l0_;
		private double lKappa_;
		private double lTheta_;
		private double lSigma_;
		private double rho_;

		#endregion // Data

	} // class ContingentSwapLegPricer

}
