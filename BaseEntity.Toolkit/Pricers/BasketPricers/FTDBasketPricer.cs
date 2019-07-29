/*
 * FTDBasketPricer.cs
 *
 *
 * TODO: add cashflow/cashflow01 as monitored properties
 * TODO: Add watch on product to monitor if modified (affects nthSurvivalCurves_ and cashflows when added)
 *
 */

using System;

using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
	/// <summary>
	///   Pricing helper class for Homogeneous basket pricer
	/// </summary>
	///
	/// <remarks>
	///   This helper class sets up a basket and pre-calculates anything specific to the basket but
	///   independent of the product.
	/// </remarks>
	///
  [Serializable]
 	public class FTDBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(FTDBasketPricer));

		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="maturity">Maturity date</param>
		/// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="ftds">Array of FTD products</param>
		/// <param name="lastIndices">Last indices of FTD baskets</param>
		/// <param name="principals">Principals of FTDs</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Correlation of the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		FTDBasketPricer( Dt asOf,
										 Dt settle,
										 Dt maturity,
										 SurvivalCurve [] survivalCurves,
										 RecoveryCurve [] recoveryCurves,
										 FTD [] ftds,
										 int [] lastIndices,
										 double [] principals,
										 Copula copula,
										 FactorCorrelation correlation,
										 int stepSize,
										 TimeUnit stepUnit,
										 Array lossLevels )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							copula, correlation, stepSize, stepUnit, lossLevels)
		{
      logger.DebugFormat("Creating FTD Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);

			// Check consistency
			if( principals.Length != survivalCurves.Length )
				throw new ArgumentException( String.Format("Length of principals {0} and survival curves {1} not match",
					principals.Length, survivalCurves.Length) );
			if( recoveryCurves.Length != survivalCurves.Length )
				throw new ArgumentException( String.Format("Length of recovery curves {0} and survival curves {1} not match",
					recoveryCurves.Length, survivalCurves.Length) );
			if( ftds.Length != lastIndices.Length )
				throw new ArgumentException( String.Format("Length of ftds {0} and lastIndices {1} not match",
					ftds.Length, lastIndices.Length) );

			this.lastIndices_ = lastIndices;
			this.ftds_ = ftds;
			this.Distribution = new Curve2D();
			this.distributionComputed_ = false;

			this.GridSize = 0.0025;

			//this.wantSensitivity_ = false;
			//this.bumpedCurveIndex_ = 0;

			// important: forced to recalculate the total principal
			this.Principals = principals;

			logger.Debug( "FTD Basket created" );
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			FTDBasketPricer obj = (FTDBasketPricer)base.Clone();

			obj.distribution_ = distribution_.clone();

			int [] lastIndices = new int [lastIndices_.Length];
			FTD [] ftds = new FTD [ftds_.Length];
			for (int i = 0; i < ftds.Length; ++i) {
			  lastIndices[i] = lastIndices_[i];
			  ftds[i] = ftds_[i];
			}
			obj.lastIndices_ = lastIndices;
			obj.ftds_ = ftds;

			return obj;
		}

		#endregion // Constructors

		#region Methods

		/// <summary>
		///   Calculate common principals and recovery rates for each FTD
		/// </summary>
		private void
		GetFTDPrincipalsAndRecoveryRates( double[] principals,
																			out double[] prins,
																			out double[] means )
		{
		  // At this moment, we assume that each FTD refers to a homogeneous basket.
		  // If not, we compute the average principal/recovery rate and treat the basket
		  // as homogeneous.
		  double[] rc = this.RecoveryRates;
			if( principals == null || rc == null )
				throw new System.ArgumentNullException("principals and recoveries cannot be null");

		  int nFTD = lastIndices_.Length;
			prins = new double[ nFTD ];
			means = new double[ nFTD ];
			int start = 0;
			for( int j = 0; j < nFTD; ++j )
			{
				double notional = 0;
				double loss = 0;
				int stop = lastIndices_[j];
				for( int i = start; i < stop; ++i )
				{
					notional += principals[i];
					loss += principals[i] * (1 - rc[i]);
				}
				prins[j] = notional / (stop - start );
				means[j] = 1 - loss / notional;
				start = stop;
			}

			return ;
		}

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		private void
		ComputeAndSaveDistribution()
		{
			Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing distribution for FTD basket" );

			FactorCorrelation corr = (FactorCorrelation) Correlation;

			// create FTD info arrays
			int [] defaultStarts = new int[ftds_.Length];
			int [] numDefaults = new int[ftds_.Length];
			for (int i = 0; i < ftds_.Length; ++i) {
			  defaultStarts[i] = ftds_[i].First;
				numDefaults[i] = ftds_[i].NumberCovered;
			}
			double[] prins = null, means = null;
			GetFTDPrincipalsAndRecoveryRates( this.Principals, out prins, out means );

		  FTDBasketModel.ComputeDistributions( false,
																					 Settle,
																					 Maturity,
																					 StepSize,
																					 StepUnit,
																					 this.CopulaType,
																					 this.DfCommon,
																					 this.DfIdiosyncratic,
																					 corr.Correlations,
																					 this.IntegrationPointsFirst,
																					 this.IntegrationPointsSecond,
																					 SurvivalCurves,
																					 lastIndices_,
																					 defaultStarts,
																					 numDefaults,
																					 prins,
																					 means,
																					 CookedLossLevels.ToArray(),
																					 this.GridSize,
																					 distribution_);

			distributionComputed_ = true;
			//wantSensitivity_ = false;

      timer.stop();
      logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());
		}


		///
		/// <summary>
		///   Compute the accumlated loss on a tranche
		/// </summary>
		///
		/// <param name="date">The date at which to calculate the cumulative losses</param>
		/// <param name="trancheBegin">The attachment point of the tranche</param>
		/// <param name="trancheEnd">The detachment point of the tranche</param>
		///
		public override double
		AccumulatedLoss(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  int groupIdx = 0;
			if ( !distributionComputed_ )
				ComputeAndSaveDistribution();

			double loss = distribution_.Interpolate( groupIdx, date,
																							 trancheBegin,
																							 trancheEnd );
			loss /= TotalPrincipal;

			return loss;
		}


		///
		/// <summary>
		///   Compute the cumulative loss distribution
		/// </summary>
		///
		/// <remarks>
		///   The returned array has two columns, the first of which contains the
		///   loss levels and the second column contains the corresponding cumulative
		///   probabilities or expected base losses.
		/// </remarks>
		///
		/// <param name="wantProbability">If true, return probabilities; else, return expected base losses</param>
		/// <param name="date">The date at which to calculate the distribution</param>
		/// <param name="lossLevels">Array of lossLevels (should be between 0 and 1)</param>
		///
		public override double [,]
		CalcLossDistribution( bool wantProbability,
													Dt date, double [] lossLevels )
		{
		  Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing loss distribution for Heterogeneous basket" );

			if( Dt.Cmp(Settle, date) > 0 )
				throw new ArgumentOutOfRangeException("date", "date is before settlement");
			if( Dt.Cmp(Maturity, date) < 0 )
				throw new ArgumentOutOfRangeException("date", "date is after maturity");

			for (int i = 0; i < lossLevels.Length; ++i) {
			  // By its nature the distribution is disrete. To avoid unexpected
			  // results, we round numbers to nearest effective decimal points,
			  // to make sure, for example,  2.0 does not become somthing like
			  // 1.999999999999954
			  decimal x = (decimal) lossLevels[i] ;
				lossLevels[i] = (double) Math.Round(x, EffectiveDigits);
				if (lossLevels[i] > 1.0)
					lossLevels[i] = 1.0;
			}

			double[] recoveryRates = RecoveryRates;
			double[] recoveryDispersions = RecoveryDispersions;

			FactorCorrelation corr = (FactorCorrelation) Correlation;

			Curve2D lossDistribution = new Curve2D();

			int stepSize = (int)( 1 + Dt.TimeInYears(AsOf,Maturity) );
			TimeUnit stepUnit = TimeUnit.Years;

			// create FTD info arrays
			int [] defaultStarts = new int[ftds_.Length];
			int [] numDefaults = new int[ftds_.Length];
			for (int i = 0; i < ftds_.Length; ++i) {
			  defaultStarts[i] = ftds_[i].First ;
				numDefaults[i] = ftds_[i].NumberCovered;
			}

		  FTDBasketModel.ComputeDistributions( true,
																					 this.Settle,
																					 date,
																					 stepSize,
																					 stepUnit,
																					 this.CopulaType,
																					 this.DfCommon,
																					 this.DfIdiosyncratic,
																					 corr.Correlations,
																					 this.IntegrationPointsFirst,
																					 this.IntegrationPointsSecond,
																					 this.SurvivalCurves,
																					 this.lastIndices_,
																					 defaultStarts,
																					 numDefaults,
																					 this.Principals,
																					 this.RecoveryRates,
																					 lossLevels,
																					 this.GridSize,
																					 lossDistribution);
			double totalPrincipal = TotalPrincipal;
			int N = lossDistribution.NumLevels();
			double [,] results = new double[ N, 2 ];
			for( int i = 0; i < N; ++i ) {
			  double level = lossDistribution.GetLevel(i);
				results[i,0] = level;
				results[i,1] = lossDistribution.Interpolate( date, level );
				if( !wantProbability )
					results[i,1] /= totalPrincipal ;
			}

			timer.stop();
      logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());

			return results;
		}


		///
		/// <summary>
		///   Compute the amortized amount on a tranche
		/// </summary>
		///
		/// <param name="date">The date at which to calculate the amortized values</param>
		/// <param name="trancheBegin">The attachment point of the tranche</param>
		/// <param name="trancheEnd">The detachment point of the tranche</param>
		///
		public override double
		AmortizedAmount(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  // no amortize
		  return 0;
		}


		///
		/// <summary>
		///   Reset the pricer such that in the next request for AccumulatedLoss()
		///   or AmortizedAmount(), it recompute everything.
		/// </summary>
		///
		public override void Reset()
		{
		  distributionComputed_ = false;
		}

		/// <summary>
		///    The total principal in the basket
		/// </summary>
		/// <exclude />
		protected override double OnSetPrincipals( double [] principals )
		{
		  if( lastIndices_ == null )
				return base.OnSetPrincipals( principals );

			double [] prins = null, means = null;
			GetFTDPrincipalsAndRecoveryRates( principals, out prins, out means );
			double totalPrincipal = 0.0;
			for (int i = 0; i < ftds_.Length; i++)
			{
				double protection = ftds_[i].NumberCovered * prins[i];
				totalPrincipal += protection;
			}
			return totalPrincipal;
		}

		#endregion // Methods

		#region Properties

    /// <summary>
		///   Underlying FTDs
		/// </summary>
    public FTD [] FTDs
		{
			get {
			  return ftds_;
			}
		}


    /// <summary>
		///   Last indices of FTD baskets
		/// </summary>
    public int [] LastIndices
		{
			get {
				return lastIndices_;
			}
		}


		/// <summary>
		///   Computed distribution for basket
		/// </summary>
		public Curve2D Distribution
		{
			get { return distribution_; }
			set { distribution_ = value; }
		}

		#endregion // Properties

		#region Data

		private int [] lastIndices_ ;
		private FTD [] ftds_ ;
		private Curve2D distribution_;
		private bool distributionComputed_;

		//private bool wantSensitivity_;
		//private int bumpedCurveIndex_;

		#endregion // Data

	} // class FTDBasketPricer

}
