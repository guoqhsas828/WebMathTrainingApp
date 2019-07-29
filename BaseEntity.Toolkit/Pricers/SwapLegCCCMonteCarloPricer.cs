/*
 * SwapLegCCCMonteCarloPricer.cs
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
	///   <para>Price a <see cref="BaseEntity.Toolkit.Products.SwapLeg">IR Swap Leg</see>
	///   using the <see cref="BaseEntity.Toolkit.Models.CCCMonteCarloModel">Cross Currency Contingent Credit Monte Carlo Model</see>.</para>
	/// </summary>
	///
	/// <seealso cref="BaseEntity.Toolkit.Products.SwapLeg">IR Swap Leg Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.CCCMonteCarloModel">Cross Currency Contingent Credit Monte Carlo Model</seealso>
	///
  [Serializable]
 	public class SwapLegCCCMonteCarloPricer : CCCPricer
  {
		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Swap Leg to price</param>
		///
    public
		SwapLegCCCMonteCarloPricer(SwapLeg product)
			: base(product)
		{}


    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Swap Leg to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="ccy">Currency</param>
		/// <param name="fxCcy">Foreign currency</param>
		/// <param name="coupon">Current coupon (if needed)</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		/// <param name="r0">Initial level for domestic short rate</param>
		/// <param name="rKappa">Mean reversion for domestic interest rate</param>
		/// <param name="rTheta">Long run mean for domestic interest rate</param>
		/// <param name="rSigma">Volatility curve of domestic interest rate</param>
		/// <param name="rf0">Initial level for foreign short rate</param>
		/// <param name="rfKappa">Mean reversion for foreign interest rate</param>
		/// <param name="rfTheta">Long run mean for foreign interest rate</param>
		/// <param name="rfSigma">Volatility curve of foreign interest rate</param>
		/// <param name="fx0">Initial level of fx rate</param>
		/// <param name="fxSigma">Volatility curve of fx</param>
		/// <param name="l0">Initial level for default rate intensity</param>
		/// <param name="lKappa">Mean reversion for default rate intensity</param>
		/// <param name="lTheta">Long run mean for default rate intensity</param>
		/// <param name="lSigma">Volatility of default rate intensity</param>
		/// <param name="correlation">Correlation coefficient for interest rates and default intensity</param>
		/// <param name="simulations">Number of simulations</param>
		///
		public
		SwapLegCCCMonteCarloPricer( SwapLeg product, Dt asOf, Dt settle,
																Currency ccy, Currency fxCcy, double coupon,
																int stepSize, TimeUnit stepUnit,
																double r0, double rKappa, Curve rTheta, Curve rSigma,
																double rf0, double rfKappa, Curve rfTheta, Curve rfSigma,
																double fx0, Curve fxSigma,
																double l0, double lKappa, double lTheta, double lSigma,
																double [,] correlation,
																int simulations
																)
			: base(product, asOf, settle, ccy, fxCcy, stepSize, stepUnit,
						 r0, rKappa, rTheta, rSigma,
						 rf0, rfKappa, rfTheta, rfSigma,
						 fx0, fxSigma,
						 l0, lKappa, lTheta, lSigma,
						 correlation)
		{
			Coupon = coupon;
			Simulations = simulations;
		}

    #endregion // Constructors

		#region Methods

    /// <summary>
		///   Price Cross-currency contingent swap leg
		/// </summary>
		///
		/// <returns>Pv of cross-currency contingent Swap Leg</returns>
		///
    public override double
		ProductPv()
		{
			// The correlations
			double rho12 = Correlation[0,1];
			double rho13 = Correlation[0,2];
			double rho14 = Correlation[0,3];
			double rho23 = Correlation[1,2];
			double rho24 = Correlation[1,3];
			double rho34 = Correlation[2,3];

			// Weighting for the stochastic vol piece
			double alpha = 0.5 * (1.0 / RTheta.Interpolate(AsOf));
			double beta = 0.5 * (1.0 / RfTheta.Interpolate(AsOf));

			// Return variable
			double [] result = new double[4];

			CCCMonteCarloModel.ComputePvs( AsOf, SwapLeg.Effective,
																		 SwapLeg.FirstCoupon, SwapLeg.Maturity,
																		 SwapLeg.Freq, SwapLeg.Calendar,
																		 SwapLeg.BDConvention, SwapLeg.DayCount,
																		 Coupon, SwapLeg.DayCount,
																		 SwapLeg.Coupon, (SwapLeg.Ccy == Ccy) ? 1 : 2,
																		 SwapLeg.Coupon, (SwapLeg.Ccy == Ccy) ? 1 : 2,
																		 StepSize, StepUnit,
																		 R0, Rf0, Fx0, L0,
																		 alpha, beta,
																		 rho12, rho13, rho14, rho23, rho24, rho34,
																		 RKappa, RTheta, RSigma,
																		 RfKappa, RfTheta, RfSigma,
																		 FxSigma, LKappa, LTheta, LSigma,
																		 Simulations, result );

			double pv;
			if( SwapLeg.Floating )
			  pv = result[0]; // floating pv
      else
        pv = result[1]; // fixed pv

			return pv * Notional;
		}

		#endregion // Methods

    #region Properties

		/// <summary>
		///   Product to price
		/// </summary>
		public SwapLeg SwapLeg
		{
			get { return (SwapLeg)Product; }
		}


		/// <summary>
		///   Currency coupon for floating rate swap legs
		/// </summary>
		public double Coupon
		{
			get { return coupon_; }
			set { coupon_ = value; }
		}

		/// <summary>
		///   Number of simulations
		/// </summary>
		public int Simulations
		{
			get { return simulations_; }
			set { simulations_ = (value > 0 ? value : 20000); }
		}



		#endregion // Properties

		#region Data

		private double coupon_;
		private int simulations_;

		#endregion // Data

  } // class SwapLegCCCMonteCarloPricer

}
