/*
 * MonteCarloBasketPricer.cs
 *
 *
 */


using System;
using BaseEntity.Toolkit.Numerics.Rng;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;
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
	public class MonteCarloBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(MonteCarloBasketPricer));

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
		/// <param name="principals">Principals of individual names</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Pairwise correlations between the underlying names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		MonteCarloBasketPricer(Dt asOf,
													 Dt settle,
													 Dt maturity,
													 SurvivalCurve [] survivalCurves,
													 RecoveryCurve[] recoveryCurves,
													 double [] principals,
													 Copula copula,
													 GeneralCorrelation correlation,
													 int stepSize,
													 TimeUnit stepUnit,
													 Array lossLevels,
													 int sampleSize)
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							copula, correlation, stepSize, stepUnit, lossLevels )
		{
			logger.Debug( String.Format("Creating Monte Carlo Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity) );

			if( sampleSize > 0 )
				this.SampleSize = sampleSize ;

			this.LossDistribution = new Curve2D();
			this.AmorDistribution = new Curve2D();
			this.distributionComputed_ = false;
			this.Seed = 0;
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			MonteCarloBasketPricer obj = (MonteCarloBasketPricer)base.Clone();

			obj.lossDistribution_ = lossDistribution_.clone();
			obj.amorDistribution_ = amorDistribution_.clone();

			return obj;
		}

		#endregion // Constructors

		#region Methods

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		private void	
		ComputeAndSaveDistribution()
		{
			logger.Debug( "Computing distribution for Monte Carlo basket" );
			Timer timer = new Timer();
			timer.start();

			double[] recoveryRates = RecoveryRates;
			double[] recoveryDispersions = RecoveryDispersions;

			GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation((Correlation) Correlation);

			// determine the seed
			int seed = this.Seed;
			if( seed < 0 )
			{
				seed = (int) RandomNumberGenerator.RandomSeed;
				if( seed < 0 )
					seed = - seed;
			}

			// Run simulation
			int [] dates = new int[1];
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      MonteCarloBasketModel.ComputeDistributions(false,
																								 start,
																								 Maturity,
																								 StepSize,
																								 StepUnit,
																								 this.CopulaType,
																								 this.DfCommon,
																								 this.DfIdiosyncratic,
																								 corr.Correlations,
																								 dates,
																								 SurvivalCurves,
																								 Principals,
																								 recoveryRates,
																								 recoveryDispersions,
																								 CookedLossLevels.ToArray(),
																								 this.SampleSize,
																								 this.UseQuasiRng,
																								 seed,
																								 LossDistribution,
																								 AmorDistribution);
			distributionComputed_ = true;

      timer.stop();
			logger.Debug( String.Format("Completed basket distribution in {0} seconds", timer.getElapsed()) );
		}


		/// <summary>
		///   Compute the accumlated loss on a tranche
		/// </summary>
		public override double
		AccumulatedLoss(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  if (!distributionComputed_)
				ComputeAndSaveDistribution();

			double loss = 0;
			AdjustTrancheLevels( false,
													 ref trancheBegin,
													 ref trancheEnd,
													 ref loss );
			loss += LossDistribution.Interpolate(date, trancheBegin, trancheEnd) / TotalPrincipal;

      //LogUtil.DebugFormat( logger, "Computed Loss for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, loss );

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
			logger.Debug( "Computing distribution for Monte Carlo basket" );
			Timer timer = new Timer();
			timer.start();

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      if (Dt.Cmp(start, date) > 0)
				throw new ArgumentException("date is before portfolio start");
			if( Dt.Cmp(Maturity, date) < 0 )
				throw new ArgumentException("date is after maturity");

			lossLevels = SetLossLevels(lossLevels, false).ToArray();
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

      GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation((Correlation)Correlation);

			Curve2D lossDistribution = new Curve2D();
			Curve2D amorDistribution = new Curve2D();

			int [] dates = new int[1];
      Dt portfolioStart = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      MonteCarloBasketModel.ComputeDistributions(wantProbability,
                                                  portfolioStart,
																									date,
																									50,
																									TimeUnit.Years,
																									// force to compute one period from portfolio start to date only

																									this.CopulaType,
																									this.DfCommon,
																									this.DfIdiosyncratic,
																									corr.Correlations,
																									dates,
																									SurvivalCurves,
																									Principals,
																									recoveryRates,
																									recoveryDispersions,

																									lossLevels,
																									this.SampleSize,
																									this.UseQuasiRng,
																									this.Seed,
																									lossDistribution,
																									amorDistribution );

      double totalPrincipal = TotalPrincipal;
      double initialBalance = this.InitialBalance;
      double prevLoss = this.PreviousLoss;
      int N = lossDistribution.NumLevels();
      double[,] results = new double[N, 2];
      for (int i = 0; i < N; ++i)
      {
        double level = lossDistribution.GetLevel(i);
        results[i, 0] = level * initialBalance + prevLoss;
        level = lossDistribution.Interpolate(date, level);
        results[i, 1] = wantProbability ? level : (level / totalPrincipal + prevLoss);
      }
      
      timer.stop();
			logger.Debug( String.Format("Completed loss distribution in {0} seconds", timer.getElapsed()) );

			return results;
		}


		/// <summary>
		///   Compute the amortized amount on a tranche
		/// </summary>
		public override double
		AmortizedAmount(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  if (!distributionComputed_)
				ComputeAndSaveDistribution();

			double amortized = 0;
			double tBegin = 1 - trancheEnd;
			double tEnd = 1 - trancheBegin;
			AdjustTrancheLevels( true,
													 ref tBegin,
													 ref tEnd,
													 ref amortized );
			amortized += AmorDistribution.Interpolate(date, tBegin, tEnd) / TotalPrincipal;

      //LogUtil.DebugFormat( logger, "Computed Amortization for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, amort );

			return amortized;
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
    ///   Experimental reset function
    ///   <preliminary/>
    /// </summary>
    /// <param name="what">Pricer attributes changed</param>
    /// <exclude/>
    public override void Reset(SyntheticCDOPricer.ResetFlag what)
    {
      base.Reset(what);
      if ((what & SyntheticCDOPricer.ResetFlag.Settle)
        == SyntheticCDOPricer.ResetFlag.Settle && lossDistribution_ != null)
      {
        Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
        if (start != lossDistribution_.GetAsOf())
        {
          lossDistribution_.SetAsOf(start);
          if (amorDistribution_ != null)
            amorDistribution_.SetAsOf(start);
        }
      }
      return;
    }
    #endregion // Methods

		#region Properties

		/// <summary>
		///   Computed distribution for basket
		/// </summary>
		public Curve2D LossDistribution
		{
			get { return lossDistribution_; }
			set { lossDistribution_ = value; }
		}


		/// <summary>
		///   Computed distribution for basket
		/// </summary>
		public Curve2D AmorDistribution
		{
			get { return amorDistribution_; }
			set { amorDistribution_ = value; }
		}


		/// <summary>
		///   Distribution computed
		/// </summary>
		public bool DistributionComputed
		{
			get { return distributionComputed_; }
			set { distributionComputed_ = value; }
		}

		/// <summary>
		///   Seed for random number generator
		/// </summary>
		public int Seed
		{
			get { return seed_; }
			set { seed_ = value; }
		}
		#endregion // Properties

		#region Data

		private Curve2D lossDistribution_;
		private Curve2D amorDistribution_;
		private bool distributionComputed_;
		private int seed_;

		#endregion Data

	} // class HeterogeneousBasketPricer

}
