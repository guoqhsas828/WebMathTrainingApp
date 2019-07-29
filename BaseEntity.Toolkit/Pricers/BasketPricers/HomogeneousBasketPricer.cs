/*
 * HomogeneousBasketPricer.cs
 *
 *
 * TODO: add cashflow/cashflow01 as monitored properties
 * TODO: Add watch on product to monitor if modified (affects nthSurvivalCurves_ and cashflows when added)
 *
 * TBD: Does this need to handle defaulted names in a similar way to HeterogeneousBasketPricer? RTD Feb'07
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
	/// <summary>
	///   Pricing helper class for Homogeneous basket pricer
	/// </summary>
	///
	/// <remarks>
  ///   <para>This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.</para>
  ///
  ///   <para>BasketPricer classes are typically used internally by Pricer classes and are not used
  ///   directly by the user.</para>
	/// </remarks>
	///
	[Serializable]
	public class HomogeneousBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(HomogeneousBasketPricer));

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
		/// <param name="principals">Principals of individual names</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Factor correlations for the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		///
    public
		HomogeneousBasketPricer( Dt asOf,
														 Dt settle,
														 Dt maturity,
														 SurvivalCurve [] survivalCurves,
														 RecoveryCurve [] recoveryCurves,
														 double [] principals,
														 Copula copula,
														 FactorCorrelation correlation,
														 int stepSize,
														 TimeUnit stepUnit,
														 Array lossLevels )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							copula, correlation, stepSize, stepUnit, lossLevels)
		{
      logger.DebugFormat("Creating Homogeneous Basket asof={0}, settle={1}, maturity={2}, principal={3}", asOf, settle, maturity, principals[0]);

      // Validate
      //
      if( recoveryCurves != null && recoveryCurves.Length != survivalCurves.Length )
				throw new ArgumentException(String.Format("Number of recoveries {0} must equal number of names {1}",
					recoveryCurves.Length, survivalCurves.Length) );
			if( principals.Length != survivalCurves.Length )
				throw new ArgumentException(String.Format("Number of principals {0} must equal number of names {1}",
					principals.Length, survivalCurves.Length) );

      // Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			// Do not add complements to loss levels
			this.LossLevelAddComplement = false;

			// Set basket specific data memebers
			this.Distribution = new Curve2D();
			this.distributionComputed_ = false;

			this.wantSensitivity_ = false;
			this.bumpedCurveIndex_ = 0;

			logger.Debug( "Homogeneous Basket created" );

			return;
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			HomogeneousBasketPricer obj = (HomogeneousBasketPricer)base.Clone();

      obj.distribution_ = distribution_ == null ? null : distribution_.clone();

			return obj;
		}

    /// <summary>
    ///   Duplicate a basket pricer
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Duplicate() differs from Clone() in that it copies by references all the 
    ///   basic data and numerical options defined in the BasketPricer class.  But it is
    ///   not the same as the MemberwiseClone() function, since it does not copy by reference
    ///   the computational data such as LossDistributions in SemiAnalyticBasketPricer class.
    ///   </para>
    /// 
    ///   <para>This function provides an easy way to construct objects performing
    ///   independent calculations on the same set of input data.  We will get rid of it
    ///   once we have restructured the basket architecture by furthur separating the basket data,
    ///   the numerical options and the computational devices.</para>
    /// </remarks>
    /// 
    /// <returns>Duplicated basket pricer</returns>
    /// <exclude />
    public override BasketPricer Duplicate()
    {
      HomogeneousBasketPricer obj = (HomogeneousBasketPricer)base.Duplicate();

      // Make clone of computation devices
      obj.distribution_ = distribution_ == null ? null : distribution_.clone();

      return obj;
    }
    #endregion // Constructors

		#region Methods

		/// <summary>
		///   Calculate loss levels as the ratio of the number of defaults
    ///   to the number of total names.
		/// </summary>
		private double[] CalcLossLevels()
		{
      loadingFactor_ = (1 - DefaultedPrincipal / TotalPrincipal) / Count;

		  double recoveryRate = this.RecoveryRate;
		  double factorLoss = (recoveryRate > (1 - 1.0E-12) ? 0.0 : 1 / (1 - recoveryRate) );
		  double factorAmor = (recoveryRate < 1.0E-12 ? 0.0 : 1 / recoveryRate) ;
      double[] lossLevels = this.CookedLossLevels.ToArray();
      UniqueSequence<double> list = new UniqueSequence<double>();
      foreach(double loss in lossLevels)
      {
			  double level = loss * factorLoss;
				if (level > 1.0)
					level = 1.0;
        list.Add(level);
        level = (1 - loss) * factorAmor;
				if( level > 1.0 )
					level = 1.0;
        list.Add(level);
      }
      return list.ToArray();
		}

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		private void	
		ComputeAndSaveDistribution()
		{
			Timer timer = new Timer();
			timer.start();

			logger.Debug( "Computing distribution for Homogeneous basket" );

			double[] lossLevels = CalcLossLevels();
			CorrelationTermStruct corr = this.CorrelationTermStruct;
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

		  HomogeneousBasketModel.ComputeDistributions(
        false, start, Maturity,
        StepSize, StepUnit,
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic,
        corr.Correlations,
        corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
        this.IntegrationPointsFirst,
        this.IntegrationPointsSecond,
        SurvivalCurves,
        lossLevels,
        distribution_);
			distributionComputed_ = true;
			wantSensitivity_ = false;

			timer.stop();
      logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());
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
		/// <returns>Distribution as a two dimensional array</returns>
		public override double [,]
		CalcLossDistribution( bool wantProbability,
													Dt date, double [] lossLevels )
		{
		  Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing loss distribution for Homogeneous basket" );

		  double lossRate = (1 - RecoveryRate);
			double [] levels = new double [lossLevels.Length];
      for (int i = 0; i < lossLevels.Length; ++i)
      {
        // Adjust tranche levels by previous defaults (which had hapened
        // before settle)
        double level = AdjustTrancheLevel(false, lossLevels[i]);

        // By its nature the distribution is disrete. To avoid unexpected
        // results, we round numbers to nearest effective decimal points,
        // to make sure, for example,  2.0 does not become somthing like
        // 1.999999999999954
        decimal x = (decimal)(level / lossRate);
        level = (double)Math.Round(x, EffectiveDigits);
        if (level > 1.0)
          level = 1.0;
        levels[i] = level;
      }

      CorrelationTermStruct corr = this.CorrelationTermStruct;
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

			Curve2D distribution = new Curve2D();

		  HomogeneousBasketModel.ComputeDistributions(
        wantProbability, start, date,
        50, TimeUnit.Years, // force to compute one period from portfolio start to date only
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic,
        corr.Correlations,
        corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
        this.IntegrationPointsFirst,
        this.IntegrationPointsSecond,
        SurvivalCurves,
        levels,
        distribution );

			int N = distribution.NumLevels();
			double [,] results = new double[ N, 2 ];
			for( int i = 0; i < N; ++i ) {
			  double level = distribution.GetLevel(i);
        results[i, 0] = RestoreTrancheLevel(false, level * lossRate);
        results[i, 1] = distribution.Interpolate(date, level);
        if (!wantProbability)
          results[i, 1] = RestoreTrancheLevel(false,
            results[i, 1] * lossRate / Count);
			}

			timer.stop();
      logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());
			
			return results;
		}


		/// <summary>
		///   Compute the whole set of distributions for sensitivity analysis,
		///   save the result for later use
		/// </summary>
		///
		/// <param name="bumpedSurvivalCurves">A set of bumped survival curves</param>
		///
		private void	
		ComputeAndSaveSensitivities(SurvivalCurve [] bumpedSurvivalCurves)
		{
		  if (!wantSensitivity_)
				return;

		  Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing sensitivity distributions for Homogeneous basket" );

			double[] lossLevels = CalcLossLevels();

			Curve[] bcurves = new Curve[bumpedSurvivalCurves.Length];
			for( int i = 0; i < bcurves.Length; i++ )
			{
				bcurves[i] = bumpedSurvivalCurves[i];
			}

      CorrelationTermStruct corr = this.CorrelationTermStruct;
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

		  HomogeneousBasketModel.ComputeDistributions(
        false, start, Maturity,
        StepSize, StepUnit,
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic,
        corr.Correlations,
        corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
        this.IntegrationPointsFirst,
        this.IntegrationPointsSecond,
        SurvivalCurves,
        bcurves,
        lossLevels,
        distribution_);
			distributionComputed_ = true;

			timer.stop();
      logger.DebugFormat("Completed basket sensitivity distributions in {0} seconds", timer.getElapsed());
			
			return;
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
		/// <returns>The expected accumulative loss on a tranche</returns>
		public override double
		AccumulatedLoss(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  int groupIdx = bumpedCurveIndex_;
		  if (!wantSensitivity_) {
			  groupIdx = 0;
		    if (!distributionComputed_)
				  ComputeAndSaveDistribution();
			} else {
		    if (!distributionComputed_)
				  throw new ArgumentException("You must call ComputeAndSaveSensitivities() first.");
			}


			double loss = 0;
			AdjustTrancheLevels( false,
													 ref trancheBegin,
													 ref trancheEnd,
													 ref loss );

			double lossRate = 1 - RecoveryRate;
			if( lossRate < 1.0E-12 )
				return loss;
			trancheBegin /= lossRate;
			if (trancheBegin > 1.0) trancheBegin = 1.0;
			trancheEnd /= lossRate;
			if (trancheEnd > 1.0) trancheEnd = 1.0;
			double defaults = distribution_.Interpolate(groupIdx, date,
																									trancheBegin,
																									trancheEnd );
      loss += defaults * lossRate * loadingFactor_;
			return loss;
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
    /// <returns>The expected accumulative amortization on a tranche</returns>
    public override double
		AmortizedAmount(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  if (0 == RecoveryRate)
				return 0.0;

		  int groupIdx = bumpedCurveIndex_;
		  if (!wantSensitivity_) {
			  groupIdx = 0;
		    if (!distributionComputed_)
				  ComputeAndSaveDistribution();
			} else {
		    if (!distributionComputed_)
				  throw new ArgumentException("You must call ComputeAndSaveSensitivities() first.");
			}

			double amortized = 0;
			double tBegin = 1 - trancheEnd;
			double tEnd = 1 - trancheBegin;
			AdjustTrancheLevels( true,
													 ref tBegin,
													 ref tEnd,
													 ref amortized );

			if( RecoveryRate < 1.0E-12 )
				return 0.0;
			double multiplier = 1.0 / RecoveryRate;
			tBegin *= multiplier ;
			if (tBegin > 1.0) tBegin = 1.0;
			tEnd *= multiplier ;
			if (tEnd > 1.0) tEnd = 1.0;
			double defaults = distribution_.Interpolate(groupIdx, date,
																									tBegin,
																									tEnd);
      amortized += defaults * RecoveryRate * loadingFactor_;
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
      UpdateRecoveries();
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
        == SyntheticCDOPricer.ResetFlag.Settle && distribution_ != null)
      {
        Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
        if (start != distribution_.GetAsOf())
        {
          distribution_.SetAsOf(start);
        }
      }
      return;
    }

		/// <summary>
		///   Fast calculation of the MTM values for a series of Synthetic CDO tranches,
		///   with each of the survival curves replaced by its alternative.
		/// </summary>
 		///
		/// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
		/// <param name="altSurvivalCurves">Array alternative survival curves</param>
		///
		/// <remarks>
		///   <para>Recalculation is avoided if the basket and altSurvivalCurves are the same.</para>
		/// </remarks>
		///
		/// <returns>
		///    A table of MTM values represented by a two-dimensional array.
		///    Each column identifies a CDO tranche, while row 0 contains the base values
		///    and row i (i &gt; 0) contains the values when the curve i is replaced
		///    by its alternative
		/// </returns>
		///
		public override double [,]
		BumpedPvs(
							SyntheticCDOPricer[] pricers,
							SurvivalCurve [] altSurvivalCurves
							)
		{
		  // Sanity check
		  int basketSize = Count;
			if( altSurvivalCurves.Length != basketSize )
					throw new ArgumentException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length));
			for( int j = 0; j < pricers.Length; ++j )
				if( pricers[j].Basket != this )
					throw new ArgumentException(String.Format("Pricer #{0} is not using this basket pricer!", j));

			Timer timer = new Timer();
			timer.start();

			logger.Debug( "Computing spread sensitivity deltas for Homogeneous basket" );

			// compute the whole distributions
      logger.DebugFormat("Computing distributions for curves (time {0}s)", timer.getElapsed());
		  wantSensitivity_ = true;
			ComputeAndSaveSensitivities(altSurvivalCurves);

			// now create and fill the table of values
      logger.DebugFormat("Filling results table (time {0}s)", timer.getElapsed());
		  double [,] table = new double [basketSize + 1, pricers.Length];
			for( int i = 0; i <= basketSize; i++ )
			{
				if( i > 0 && SurvivalCurves[i-1] == altSurvivalCurves[i-1] )
				{
					// Don't bother recalculating if the curve is unchanged.
					for (int j = 0; j < pricers.Length; ++j)
						table[i,j] = table[0,j];
				}
				else
				{
					// we want the results with the ith curve bumped
					bumpedCurveIndex_ = i;
					// compute the prices
					for( int j = 0; j < pricers.Length; ++j )
						table[i,j] = pricers[j].FullPrice();
				}
			}

			// restore states
			wantSensitivity_ = false;
			bumpedCurveIndex_ = 0;

			timer.stop();
      logger.DebugFormat("Completed basket spread sensitivity deltas in {0} seconds", timer.getElapsed());
			
      // done
			return table;
		}

    /// <summary>
    ///   Fast calculation of the price values for a series of Synthetic CDO tranches,
    ///   with each of the survival curves replaced by its alternative.
    /// </summary>
    ///
    /// <param name="pricers">An array of CDO evaluators sharing this basket</param>
    /// <param name="altSurvivalCurves">Array alternative survival curves</param>
    /// <param name="includeRecoverySensitivity">Whether to include recovery sensitivity</param>
    ///
    /// <remarks>
    ///   <para>Recalculation is avoided if the basket and altSurvivalCurves are the same.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of price values represented by a two dimensional array.
    ///    Each column identifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
		internal protected override double [,] BumpedPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve [] altSurvivalCurves,
      bool includeRecoverySensitivity
      )
		{
      if (includeRecoverySensitivity || NeedExactJtD(pricers))
        return base.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);

		  // Sanity check
		  int basketSize = Count;
			if( altSurvivalCurves.Length != basketSize )
					throw new ArgumentException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length));
			for( int j = 0; j < pricers.Length; ++j )
				if( pricers[j].Basket != this )
					throw new ArgumentException(String.Format("Pricer #{0} is not using this basket pricer!", j));

			Timer timer = new Timer();
			timer.start();

			logger.Debug( "Computing spread sensitivity deltas for Homogeneous basket" );

			// compute the whole distributions
      logger.DebugFormat("Computing distributions for curves (time {0}s)", timer.getElapsed());
		  wantSensitivity_ = true;
			ComputeAndSaveSensitivities(altSurvivalCurves);

			// now create and fill the table of values
      logger.DebugFormat("Filling results table (time {0}s)", timer.getElapsed());
		  double [,] table = new double [basketSize + 1, pricers.Length];
			for( int i = 0; i <= basketSize; i++ )
			{
				if( i > 0 && SurvivalCurves[i-1] == altSurvivalCurves[i-1] )
				{
					// Don't bother recalculating if the curve is unchanged.
					for (int j = 0; j < pricers.Length; ++j)
						table[i,j] = table[0,j];
				}
				else
				{
					// we want the results with the ith curve bumped
					bumpedCurveIndex_ = i;
					// compute the prices
					for( int j = 0; j < pricers.Length; ++j )
						table[i,j] = pricers[j].Evaluate();
				}
			}

			// restore states
			wantSensitivity_ = false;
			bumpedCurveIndex_ = 0;

			timer.stop();
      logger.DebugFormat("Completed basket spread sensitivity deltas in {0} seconds", timer.getElapsed());
			
      // done
			return table;
		}

    #endregion // Methods

		#region Properties

    /// <summary>
		///   Recovery rate
		/// </summary>
    public double RecoveryRate
		{
      get { return this.AverageRecoveryRate; }
		}

    /// <summary>
		///   Recovery rate dispersion
		/// </summary>
    public double RecoveryDispersion
		{
			get {
				double sum = 0;
				double [] recoveryDispersions = RecoveryDispersions;
				for (int i = 0; i < recoveryDispersions.Length; ++i)
					sum += recoveryDispersions[i];
				return( sum / recoveryDispersions.Length );
			}
		}


    /// <summary>
		///   Principal for each name
		/// </summary>
    public double Principal
		{
			get { return TotalPrincipal / Count; }
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

		private Curve2D distribution_;
		private bool distributionComputed_;

		private bool wantSensitivity_;
		private int bumpedCurveIndex_;

    // intermediate values, set CalcLossLevels()
    private double loadingFactor_;
		#endregion // Data

	} // class HomogeneousBasketPricer

}
