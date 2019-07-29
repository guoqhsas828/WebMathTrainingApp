/*
 * AnalyticCDO2BasketPricer.cs
 *
 *
 */

using System;

using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
	/// <summary>
	///   Pricing helper class for Heterogeneous basket pricer
	/// </summary>
	///
	/// <remarks>
	///   This helper class sets up a basket and pre-calculates anything specific to the basket but
	///   independent of the product.
	/// </remarks>
	///
  [Serializable]
 	public class AnalyticCDO2BasketPricer : CDOSquaredBasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(AnalyticCDO2BasketPricer));

		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="maturity">Maturity date</param>
		/// <param name="survivalCurves">Array of Survival Curves of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Principals of individual names in child CDOs</param>
		/// <param name="attachments">Attachment points of child CDOs</param>
		/// <param name="detachments">Detachment points of child CDOs</param>
		/// <param name="crossSubordination">If true, with cross subordination</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Correlation of the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		AnalyticCDO2BasketPricer( Dt asOf,
															Dt settle,
															Dt maturity,
															SurvivalCurve [] survivalCurves,
															RecoveryCurve[] recoveryCurves,
															double [] principals,
															double [] attachments,
															double [] detachments,
															bool crossSubordination,
															Copula copula,
															FactorCorrelation correlation,
															int stepSize,
															TimeUnit stepUnit,
															Array lossLevels )
			: base(asOf, settle, maturity, survivalCurves, recoveryCurves,
						 principals, attachments, detachments, null, crossSubordination,
						 copula, correlation, stepSize, stepUnit, lossLevels )
		{
			logger.Debug( String.Format("Creating Analytic Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity) );

			this.GridSize = 0.005;

			logger.Debug( "Basket created" );
		}
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Array of Survival Curves of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals of individual names in child CDOs</param>
    /// <param name="attachments">Attachment points of child CDOs</param>
    /// <param name="detachments">Detachment points of child CDOs</param>
    /// <param name="cdoMaturities">Same of different underlying CDO maturities</param>
    /// <param name="crossSubordination">If true, with cross subordination</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Correlation of the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    /// <remarks>
    ///   <para>This is obsolete.</para>
    /// </remarks>
    public
    AnalyticCDO2BasketPricer(Dt asOf,
                              Dt settle,
                              Dt maturity,
                              SurvivalCurve[] survivalCurves,
                              RecoveryCurve[] recoveryCurves,
                              double[] principals,
                              double[] attachments,
                              double[] detachments,
                              Dt[] cdoMaturities,
                              bool crossSubordination,
                              Copula copula,
                              FactorCorrelation correlation,
                              int stepSize,
                              TimeUnit stepUnit,
                              Array lossLevels)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves,
             principals, attachments, detachments, cdoMaturities, crossSubordination,
             copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.Debug(String.Format("Creating Analytic Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity));

      this.GridSize = 0.005;

      logger.Debug("Basket created");
    }
		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			AnalyticCDO2BasketPricer obj = (AnalyticCDO2BasketPricer)base.Clone();
			return obj;
		}

		#endregion // Constructors

		#region Methods

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		protected override void
		ComputeDistribution( double[] attachs, double[] levels,
												 Dt maturity, int stepSize, TimeUnit stepUnit,
												 bool wantProbability, Curve2D lossDistribution )
		{
			logger.Debug( "Computing distribution for analytic CDO squared basket" );
			Timer timer = new Timer();
			timer.start();

			double[] recoveryRates = RecoveryRates;

			FactorCorrelation corr = (FactorCorrelation) Correlation;

			// Run simulation
		  AnalyticCDOSquaredModel.ComputeDistributions( wantProbability,
																										Settle,
																										maturity,
																										stepSize,
																										stepUnit,
																										this.CopulaType,
																										this.DfCommon,
																										this.DfIdiosyncratic,
																										this.IntegrationPointsFirst,
																										this.IntegrationPointsSecond,
																										corr.Correlations,
																										SurvivalCurves,
																										recoveryRates,
																										Principals,
																										attachs,
																										Detachments,
																										levels,
																										GridSize,
																										lossDistribution);
      timer.stop();
			logger.Debug( String.Format("Completed CDO Squared basket distribution in {0} seconds", timer.getElapsed()) );
		}

		#endregion // Methods

		#region Properties
		#endregion // Properties

		#region Data
		#endregion Data

	} // class AnalyticCDO2BasketPricer

}
