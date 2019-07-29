/*
 * MonteCarloCDO2BasketPricer.cs
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
	/// <summary>
	///   CDO squared basket pricer based on pure Monte Carlo default time simulations
	/// </summary>
  [Serializable]
 	public class MonteCarloCDO2BasketPricer : CDOSquaredBasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(MonteCarloCDO2BasketPricer));

		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">Pricing as-of date</param>
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
		/// <param name="seed">Random number seed</param>
		/// <param name="cdoMaturities">Same or different CDO maturities</param>
		/// 
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		MonteCarloCDO2BasketPricer( Dt asOf,
																Dt settle,
																Dt maturity,
																SurvivalCurve [] survivalCurves,
																RecoveryCurve[] recoveryCurves,
																double [] principals,
																double [] attachments,
																double [] detachments,
																bool crossSubordination,
																Copula copula,
																GeneralCorrelation correlation,
																int stepSize,
																TimeUnit stepUnit,
																Array lossLevels,
																int sampleSize,
                                int seed,
                                Dt[] cdoMaturities)
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves,
							principals, attachments, detachments, cdoMaturities, crossSubordination,
							copula, correlation, stepSize, stepUnit, lossLevels )
		{
			logger.Debug( String.Format("Creating Monte Carlo Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity) );

			if( sampleSize > 0 )
				this.SampleSize = sampleSize ;
      if(seed < 0)
        throw new ArgumentException("Seed should be positive integer");
      seed_ = seed;
			logger.Debug( "Basket created" );
		}
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="asOf">Pricing as-of date</param>
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
    public
    MonteCarloCDO2BasketPricer(Dt asOf,
                                Dt settle,
                                Dt maturity,
                                SurvivalCurve[] survivalCurves,
                                RecoveryCurve[] recoveryCurves,
                                double[] principals,
                                double[] attachments,
                                double[] detachments,
                                bool crossSubordination,
                                Copula copula,
                                GeneralCorrelation correlation,
                                int stepSize,
                                TimeUnit stepUnit,
                                Array lossLevels,
                                int sampleSize)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves,
              principals, attachments, detachments, null, crossSubordination,
              copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.Debug(String.Format("Creating Monte Carlo Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity));

      if (sampleSize > 0)
        this.SampleSize = sampleSize;
      logger.Debug("Basket created");
    }
		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			MonteCarloCDO2BasketPricer obj = (MonteCarloCDO2BasketPricer)base.Clone();
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
			logger.Debug( "Computing distribution for Monte Carlo CDO squared basket" );
			Timer timer = new Timer();
			timer.start();

			double[] recoveryRates = RecoveryRates;
			double[] recoveryDispersions = RecoveryDispersions;

			GeneralCorrelation corr = (GeneralCorrelation) Correlation;

      // Validate inputs
      if (SurvivalCurves.Length != corr.BasketSize)
      {
        throw new ArgumentException(String.Format(
          "Constituents of currelation ({0} names) and basket ({1} names) not match.",
          corr.BasketSize, SurvivalCurves.Length));
      }

			// Run simulation
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      MonteCarloCDOSquaredModel.ComputeDistributions(wantProbability,
																										 start,
																										 maturity,
																										 stepSize,
																										 stepUnit,
																										 this.CopulaType,
																										 this.DfCommon,
																										 this.DfIdiosyncratic,
																										 corr.Correlations,
																										 SurvivalCurves,
																										 recoveryRates,
																										 recoveryDispersions,
																										 Principals,
																										 attachs,
																										 Detachments,
																										 levels,
																										 this.SampleSize,
																										 this.UseQuasiRng,
                                                     lossDistribution, this.CdoMaturities, (uint)this.Seed);

      timer.stop();
			logger.Debug( String.Format("Completed CDO Squared basket distribution in {0} seconds", timer.getElapsed()) );
		}

		#endregion // Methods

		#region Properties
    /// <summary>
    ///  Get the seed of random number generator user input
    /// </summary>
	  public int Seed
	  {
	    get { return seed_;}
	  }

		#endregion // Properties

		#region Data
	  private int seed_;
		#endregion Data

	} // class MonteCarloCDO2BasketPricer

}
