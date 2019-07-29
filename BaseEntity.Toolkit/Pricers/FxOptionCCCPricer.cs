/*
 * FxOptionCCCPricer.cs
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
	///   <para>Price a <see cref="BaseEntity.Toolkit.Products.FxOption">FX Option</see>
	///   using the <see cref="BaseEntity.Toolkit.Models.CCCModel">Cross Currency Contingent Model</see>.</para>
	/// </summary>
	///
	/// <seealso cref="BaseEntity.Toolkit.Products.FxOption">FX Option Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.CCCModel">Cross Currency Contingent Model</seealso>
	///
  [Serializable]
 	public class FxOptionCCCPricer : CCCPricer
  {
		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Fx Option to price</param>
		///
    public
		FxOptionCCCPricer(FxOption product)
			: base(product)
		{}


    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Fx Option to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="ccy">Currency</param>
		/// <param name="fxCcy">Foreign currency</param>
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
		///
		public
		FxOptionCCCPricer(FxOption product, Dt asOf, Dt settle,
											Currency ccy, Currency fxCcy, int stepSize, TimeUnit stepUnit,
											double r0, double rKappa, Curve rTheta, Curve rSigma,
											double rf0, double rfKappa, Curve rfTheta, Curve rfSigma,
											double fx0, Curve fxSigma,
											double l0, double lKappa, double lTheta, double lSigma,
											double [,] correlation
											)
			: base(product, asOf, settle, ccy, fxCcy, stepSize, stepUnit,
						 r0, rKappa, rTheta, rSigma,
						 rf0, rfKappa, rfTheta, rfSigma,
						 fx0, fxSigma,
						 l0, lKappa, lTheta, lSigma,
						 correlation)
		{}


    #endregion // Constructors

		#region Methods

		/// <summary>
		///   Price an FXOption in the quanto contingent model framework.
		/// </summary>
		///
		/// <returns>Fair value (PV) of FX option</returns>
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

			double optionValue = CCCModel.FxOption(AsOf,
																						 FxOption.Maturity,
																						 Calendar.None, BDConvention.None, DayCount.None, // Not used in model
																						 FxOption.Type, FxOption.Strike,
																						 StepSize, StepUnit,
																						 R0, Rf0, Fx0, L0,
																						 alpha, beta,
																						 rho12, rho13, rho14, rho23, rho24, rho34,
																						 RKappa, RTheta, RSigma,
																						 RfKappa, RfTheta, RfSigma,
																						 FxSigma,
																						 LKappa, LTheta, LSigma);
			return optionValue * Notional;
		}

		#endregion // Methods

    #region Properties

		/// <summary>
		///   Product to price
		/// </summary>
		public FxOption FxOption
		{
			get { return (FxOption)Product; }
		}

		#endregion // Properties

  } // class FxOptionCCCPricer

}
