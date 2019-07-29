/*
 * SemiAnalyticCDO2BasketPricer.cs
 *
 *
 */
using System;
using System.Collections;

using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Concurrency;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
	/// <summary>
	///   CDO squared basket pricer based on semi-analytical approach, using
  ///   a small number of Monte Carlo to do numerical integration.
	/// </summary>
	[Serializable]
	public class SemiAnalyticCDO2BasketPricer : CDOSquaredBasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(SemiAnalyticCDO2BasketPricer));

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
		/// <param name="cdoMaturities">Same or different CDO maturities</param>
		/// <param name="crossSubordination">If true, with cross subordination</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Correlation of the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		SemiAnalyticCDO2BasketPricer( Dt asOf,
																	Dt settle,
																	Dt maturity,
																	SurvivalCurve [] survivalCurves,
																	RecoveryCurve[] recoveryCurves,
																	double [] principals,
																	double [] attachments,
																	double [] detachments,
                                  Dt[] cdoMaturities,
																	bool crossSubordination,
																	Copula copula,
																	FactorCorrelation correlation,
																	int stepSize,
																	TimeUnit stepUnit,
																	Array lossLevels,
																	int sampleSize )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, 
							principals, attachments, detachments, cdoMaturities, crossSubordination,
							copula, correlation, stepSize, stepUnit, lossLevels )
		{
      logger.DebugFormat("Creating SemiAnalytic CDO^2 Basket asof={0}, settle={1}, maturity={2}",
                        asOf, settle, maturity );
      
			if( sampleSize > 0 )
				this.SampleSize = sampleSize;

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
    /// <param name="crossSubordination">If true, with cross subordination</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Correlation of the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    /// <param name="sampleSize">Sample size of simulation</param>
    ///
    /// <remarks>
    ///   <para>This is obsolete.</para>
    /// </remarks>
    public
    SemiAnalyticCDO2BasketPricer(Dt asOf,
                                  Dt settle,
                                  Dt maturity,
                                  SurvivalCurve[] survivalCurves,
                                  RecoveryCurve[] recoveryCurves,
                                  double[] principals,
                                  double[] attachments,
                                  double[] detachments,
                                  bool crossSubordination,
                                  Copula copula,
                                  FactorCorrelation correlation,
                                  int stepSize,
                                  TimeUnit stepUnit,
                                  Array lossLevels,
                                  int sampleSize)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves,
              principals, attachments, detachments, null, crossSubordination,
              copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating SemiAnalytic CDO^2 Basket asof={0}, settle={1}, maturity={2}",
                        asOf, settle, maturity);

      if (sampleSize > 0)
        this.SampleSize = sampleSize;

      logger.Debug("Basket created");
    }
		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			SemiAnalyticCDO2BasketPricer obj = (SemiAnalyticCDO2BasketPricer)base.Clone();
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
			Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing distribution for SemiAnalytic CDO squared basket" );

			double[] recoveryRates = RecoveryRates;
			double[] recoveryDispersions = RecoveryDispersions;

			FactorCorrelation corr = (FactorCorrelation) Correlation;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      Dt[] dates = ConstructDateArray(start, maturity, stepSize, stepUnit);
      Curve2DFactory.Initialize(start, dates, 0, dates.Length, levels, 1, lossDistribution);

      int flags = (wantProbability ? SemiAnalyticCDOSquaredModel.WantProbability
        : 0) | (ParallelSupport.Enabled
        ? SemiAnalyticCDOSquaredModel.EnableParallel : 0);
      SemiAnalyticCDOSquaredModel.ComputeDistributions(flags,
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic, this.Copula.Data,
        this.IntegrationPointsFirst, this.IntegrationPointsSecond,
        corr.Correlations, SurvivalCurves, recoveryRates, recoveryDispersions,
        Principals, attachs, Detachments, CdoMaturities, this.SampleSize, lossDistribution);

			timer.stop();
      logger.DebugFormat("Completed CDO Squared basket distribution in {0} seconds", timer.getElapsed());
			
			return;
		}

		#endregion // Methods

		#region Properties
		#endregion // Properties

		#region Data
    #endregion Data

	} // class HeterogeneousBasketPricer

}
