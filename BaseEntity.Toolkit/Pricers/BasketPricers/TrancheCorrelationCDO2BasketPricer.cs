/*
 * TrancheCorrelationCDO2BasketPricer.cs
 *
 *
 */
using System;

using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Concurrency;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
	/// <summary>
	///   CDO squared basket pricer based on semi-analytical approach.
	/// </summary>
  /// <remarks>
	///   <para>This pricer is based on semi-analytical approach.  It first computes
  ///   the joint loss distributions of sub-baskets based on Gaussian assumption
  ///   and then uses a small number of Monte Carlo to do numerical integration.</para>
  ///
  ///   <para>Each child CDO in the CDO squared has its own uniform correlation factor.
  ///   So does the intersection of any two sub-baskets. </para>
  ///
  ///   <para>The user should supply correlation factors as an array of size <formula inline="true">k^2</formula>,
  ///   where <formula inline="true">k</formula> is the number of sub-baskets.  The array
  ///   should be arranged as a symmetric <formula inline="true">k \times k</formula> matrix,
  ///   with the first <formula inline="true">k</formula> elements to be the first row,
  ///   the second <formula inline="true">k</formula> elements to be the second row, and so on.
  ///   The (i,i)-element of the matrix represents the implied correlation factor for the sub-basket i.
  ///   The (i,j)-element of the matrix is the correlation factor for the intersection
  ///   of sub-basket i and j, which is used to compute the conditional correlation of losses
  ///   in the two sub-baskets.
  ///   If the intersection is empty, then the correlation factor can be any number between 0 and 1.</para>
  /// </remarks>
	[Serializable]
	public class TrancheCorrelationCDO2BasketPricer : CDOSquaredBasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(TrancheCorrelationCDO2BasketPricer));

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
		/// <param name="cdoMaturities">Same or different underlying CDO maturities</param>
		/// <param name="crossSubordination">If true, with cross subordination</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="trancheFactors">Correlation factors for child CDOs</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		TrancheCorrelationCDO2BasketPricer( Dt asOf,
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
																				double[] trancheFactors,
																				int stepSize,
																				TimeUnit stepUnit,
																				Array lossLevels,
																				int sampleSize )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, 
							principals, attachments, detachments, cdoMaturities, crossSubordination,
							copula, new SingleFactorCorrelation(new string[]{"all"},0.0),
							stepSize, stepUnit, lossLevels )
		{
      logger.DebugFormat("Creating TrancheCorrelation CDO^2 Basket asof={0}, settle={1}, maturity={2}",
                          asOf, settle, maturity);
      
			if( sampleSize > 0 )
				this.SampleSize = sampleSize;

			TrancheFactors = trancheFactors;

			logger.Debug( "Basket created" );
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			TrancheCorrelationCDO2BasketPricer obj = (TrancheCorrelationCDO2BasketPricer)base.Clone();
			double[] factors = new double[trancheFactors_.Length];
			for( int i = 0; i < trancheFactors_.Length; ++i )
				factors[i] = trancheFactors_[i];
			obj.trancheFactors_ = factors;
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
			logger.Debug( "Computing distribution for TrancheCorrelation CDO squared basket" );

			double[] recoveryRates = RecoveryRates;
			double[] recoveryDispersions = RecoveryDispersions;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      Dt[] dates = ConstructDateArray(start, maturity, stepSize, stepUnit);
      Curve2DFactory.Initialize(start, dates, 0, dates.Length, levels, 1, lossDistribution);
     
      // Run simulation
      int flags = (wantProbability ? BaseCorrelationCDOSquaredModel.WantProbability
        : 0) | (ParallelSupport.Enabled
        ? BaseCorrelationCDOSquaredModel.EnableParallel : 0);
      BaseCorrelationCDOSquaredModel.ComputeDistributions(flags,
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic,
        this.IntegrationPointsFirst, this.IntegrationPointsSecond,
        trancheFactors_, this.SurvivalCurves, recoveryRates, recoveryDispersions,
        this.Principals, attachs, this.Detachments, CdoMaturities, this.SampleSize,
        lossDistribution);

			timer.stop();
      logger.DebugFormat("Completed CDO Squared basket distribution in {0} seconds", timer.getElapsed());
			
			return;
		}

		#endregion // Methods

		#region Properties
		/// <summary>
		///   Tranche factors for sub-baskets
		/// </summary>
		/// <remarks>
		///   <para>The factors are supplied as an array of size <formula inline="true">k^2</formula>,
		///   where <formula inline="true">k</formula> is the number of sub-baskets.  The array
		///   should be arranged as a symmetric <formula inline="true">k \times k</formula> matrix,
		///   with the first <formula inline="true">k</formula> elements to be the first row,
		///   the second <formula inline="true">k</formula> elements to be the second row, and so on.
		///   The (i,i)-element of the matrix represents the implied correlation factor for the sub-basket i.
		///   The (i,j)-element of the matrix is the correlation factor for the intersection
		///   of sub-basket i and j, which is used to compute the conditional correlation of losses
		///   in the two sub-baskets.
		///   If the intersection is empty, then the correlation factor can be any number between 0 and 1.</para>
		/// </remarks>
		public double[] TrancheFactors
		{
			get { return trancheFactors_; }
			set {
			  if( null == value )
					throw new ToolkitException( "null tranche factors" );
				int nChild = this.Attachments.Length;
				if( value.Length != nChild * nChild )
					throw new ToolkitException( String.Format("tranche factors (Length={0}) should be of length {1}",
																										value.Length, nChild * nChild) );

			  trancheFactors_ = value;
			}
		}
		#endregion // Properties

		#region Data
		private double[] trancheFactors_;
		#endregion Data

	} // class TrancheCorrelationCDO2BasketPricer

}
