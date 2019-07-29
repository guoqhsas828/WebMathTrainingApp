/*
 * UniformBasketPricer.cs
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
	///   Pricing helper class for Uniform basket pricer
	/// </summary>
	///
	/// <remarks>
	///   This helper class sets up a basket and pre-calculates anything specific to the basket but
	///   independent of the product.
	/// </remarks>
	///
  [Serializable]
 	public class UniformBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(UniformBasketPricer));

    #region Config
    /// <exclude />
    private static bool adaptiveApproach_ = true;

    /// <exclude />
    public static bool AdaptiveApproach
    {
      get{return adaptiveApproach_;}
      set{adaptiveApproach_=value;UniformBasketModel.AdaptiveApproach(adaptiveApproach_);}
    }
    #endregion // Config

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
		/// <param name="correlation">Single factor correlation for all the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		UniformBasketPricer( Dt asOf,
												 Dt settle,
												 Dt maturity,
												 SurvivalCurve [] survivalCurves,
												 RecoveryCurve[] recoveryCurves,
												 double [] principals,
												 Copula copula,
												 Correlation correlation,
												 int stepSize,
												 TimeUnit stepUnit,
												 Array lossLevels )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							copula, correlation, stepSize, stepUnit, lossLevels )
		{
			logger.DebugFormat( "Creating Uniform Basket asof={0}, settle={1}, maturity={2}, principal={3}", asOf, settle, maturity, Principal );

			// Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			this.survivalCurveAlt_ = null;

			logger.Debug( "Uniform Basket created" );
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			UniformBasketPricer obj = (UniformBasketPricer)base.Clone();

      if (survivalCurveAlt_ != null)
        obj.survivalCurveAlt_ = (SurvivalCurve)survivalCurveAlt_.Clone();

			return obj;
		}

		#endregion // Constructors

		#region Methods

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
			logger.Debug( "Computing loss distribution for Uniform basket" );

      lossLevels = SetLossLevels(lossLevels, false).ToArray();

      int nBasket = Count;
      double lossRate = 1 - RecoveryRate;

      for (int i = 0; i < lossLevels.Length; ++i)
      {
        // By its nature the distribution is disrete. To avoid unexpected
        // results, we round numbers to nearest effective decimal points,
        // to make sure, for example,  2.0 does not become somthing like
        // 1.999999999999954
        decimal x = (decimal)(lossLevels[i] * nBasket / lossRate);
        lossLevels[i] = (double)Math.Round(x, this.EffectiveDigits);
        if (lossLevels[i] > nBasket)
          lossLevels[i] = nBasket;
      }

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      double initialBalance = this.InitialBalance;
      double defaultProbability = base.BasketLoss(start, date) / lossRate / initialBalance;
      double factor = GetFactor(UseNaturalSettlement ? start : AsOf, date, this.CorrelationTermStruct);

      double prevLoss = this.PreviousLoss;
			double [,] results = new double[lossLevels.Length, 2];
      for (int i = 0; i < lossLevels.Length; ++i)
      {
        results[i, 0] = lossLevels[i] * lossRate / nBasket * initialBalance + prevLoss;
        double dflts = UniformBasketModel.Cumulative(wantProbability,
                                                      defaultProbability,
                                                      nBasket,
                                                      this.CopulaType,
                                                      this.DfCommon,
                                                      this.DfIdiosyncratic,
                                                      factor,
                                                      this.IntegrationPointsFirst,
                                                      this.IntegrationPointsSecond,
                                                      0, lossLevels[i]);
        results[i, 1] = wantProbability ? dflts
          : (dflts * lossRate / nBasket * initialBalance + prevLoss);
      }

			timer.stop();
			logger.DebugFormat( "Completed loss distribution in {0} seconds", timer.getElapsed() );

			return results;
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
			double loss = 0.0;

			// Adjust for any defaulted credit
			AdjustTrancheLevels( false,
													 ref trancheBegin,
													 ref trancheEnd,
													 ref loss );

		  int nBasket = Count;
			double lossRate = 1 - RecoveryRate;
      if (lossRate <= 1E-15)
        return 0.0;

      double loDefault = trancheBegin * nBasket / lossRate;
			if (loDefault > nBasket) loDefault = nBasket;
			double hiDefault = trancheEnd * nBasket / lossRate;
			if (hiDefault > nBasket) hiDefault = nBasket;

      double initialBalance = this.InitialBalance;
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      double defaultProbability = base.BasketLoss(start, date) / lossRate / initialBalance;
      double factor = GetFactor(UseNaturalSettlement ? start : AsOf, date, this.CorrelationTermStruct);

			double newLoss;
      newLoss = UniformBasketModel.Cumulative(
        false, defaultProbability, nBasket,
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic,
        factor, this.IntegrationPointsFirst, this.IntegrationPointsSecond,
        loDefault, hiDefault );

			newLoss *= lossRate;
			loss += newLoss / nBasket * initialBalance;

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
    public override double
    AmortizedAmount(Dt date,
                    double trancheBegin,
                    double trancheEnd)
    {
      double recoveryRate = RecoveryRate;
      if (recoveryRate <= 1E-15)
        return 0.0;

      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;

      // Adjust for any defaulted credits
      AdjustTrancheLevels(true,
                           ref tBegin,
                           ref tEnd,
                           ref amortized);

      int nBasket = Count;

      double loDefault = tBegin * nBasket / recoveryRate;
      if (loDefault > nBasket) loDefault = nBasket;
      double hiDefault = tEnd * nBasket / recoveryRate;
      if (hiDefault > nBasket) hiDefault = nBasket;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      double defaultProbability = base.BasketAmortize(start, date) / recoveryRate / (1 - PreviousAmortized - PreviousLoss);
      double factor = GetFactor(UseNaturalSettlement ? start : AsOf, date, this.CorrelationTermStruct);

      double loss;
      loss = UniformBasketModel.Cumulative(
        false, defaultProbability, nBasket,
        this.CopulaType, this.DfCommon, this.DfIdiosyncratic,
        factor, this.IntegrationPointsFirst, this.IntegrationPointsSecond,
        loDefault, hiDefault);

      loss *= recoveryRate;
      amortized += loss / nBasket * this.InitialBalance;

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
		  survivalCurveAlt_ = null;
    }

#if Has_Own_BumpedPv
		/// <summary>
		///   Fast calculation of the MTM values for a series of Synthetic CDO tranches,
		///   with each of the survival curves replaced by its alternative.
		/// </summary>
 		///
		/// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
		/// <param name="altSurvivalCurves">Array alternative survival curves</param>
		///
		/// <returns>
		///    A table of MTM values represented by a two dimensional array.
		///    Each column indentifies a CDO tranche, while row 0 contains the base values
		///    and row i (i &gt; 0) contains the values when the curve i is replaced
		///    by its alternative
		/// </returns>
		public override double [,]
		BumpedPvs(
							SyntheticCDOPricer[] pricers,
							SurvivalCurve [] altSurvivalCurves
							)
		{
		  // Sanity check
		  int basketSize = Count;
			if( altSurvivalCurves.Length != basketSize )
				throw new System.ArgumentOutOfRangeException( String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length) );
			for( int j = 0; j < pricers.Length; ++j )
				if( pricers[j].Basket != this )
					throw new System.ArgumentOutOfRangeException( String.Format("Pricer #{0} is not using this basket pricer!", j) );

			Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing spread sensitivity deltas for Uniform basket" );

			// need SurvivalCurve
			SurvivalCurve survivalCurve = SurvivalCurves[0];

			// compute the whole distributions
			logger.DebugFormat( "Computing distributions for curves (time {0}s)", timer.getElapsed() );

			// now create and fill the table of values
			logger.DebugFormat( "Filling results table (time {0}s)", timer.getElapsed() );
		  double [,] table = new double [altSurvivalCurves.Length + 1, pricers.Length];

			// compute the base case
			survivalCurveAlt_ = null;
			for (int j = 0; j < pricers.Length; ++j)
			  table[0,j] = pricers[j].FullPrice();

			// for each curve
			int lastIdx = 0;
			for( int i = 1; i <= altSurvivalCurves.Length; i++ )
			{
			  SurvivalCurve altSurvivalCurve = altSurvivalCurves[i-1];
				if( survivalCurve == altSurvivalCurve )
				{
					// Don't bother recalculating if the curve is unchanged.
					for( int j = 0; j < pricers.Length; ++j )
						table[i,j] = table[0,j];
				}
				else if( survivalCurveAlt_ == altSurvivalCurve )
				{
					// Don't bother recalculating if the curve is unchanged.
					for( int j = 0; j < pricers.Length; ++j )
						table[i,j] = table[lastIdx,j];
				}
				else
				{
					// we want the results with the ith curve bumped
				  survivalCurveAlt_ = altSurvivalCurve;
					// compute the prices
					for( int j = 0; j < pricers.Length; ++j )
						table[i,j] = pricers[j].FullPrice();
					lastIdx = i;
				}
			}

			// restore states
			survivalCurveAlt_ = null;

      timer.stop();
			logger.DebugFormat( "Completed basket spread sensitivity deltas in {0} seconds", timer.getElapsed() );

			// done
			return table;
		}
#endif // Has_Own_BUmpedPv

    /// <summary>
    ///   Find average factor at a date
    /// </summary>
    /// <param name="start">start</param>
    /// <param name="date">date</param>
    /// <param name="ct">correlation term structure</param>
    /// <returns>factor value</returns>
    internal static double GetFactor(Dt start, Dt date, CorrelationTermStruct ct)
    {
      //
      int basketSize = ct.BasketSize;
      double[] corrData = ct.Correlations;
      int[] corrDates = ct.GetDatesAsInt(start);

			// calculate the size of factors
			int factorLength = Math.Max(corrData.Length / Math.Max(1,corrDates.Length), basketSize);

			// storage space
			double[] factors = new double[factorLength];

			// factor arrays
			BasketCorrelationModel.getFactorArray(date, factors, start, corrData, corrDates);

      // initialize
      double avgFactor = 0.0;
     
      // calculate the average factor
			int numFactors = factorLength / basketSize;
			if (numFactors <= 1)
			{
				for (int i = 0; i < factorLength; ++i)
					avgFactor += (factors[i] - avgFactor) / (1 + i);
				return avgFactor;
			}

			// the strange case of multi-factors
			for (int i = 0; i < basketSize; ++i)
			{
				double corr = 0;
				for(int j = 0; j < numFactors; ++j)
				{
					double f = factors[i + j*basketSize];
					corr += f * f;
				}
				avgFactor += (Math.Sqrt(corr) - avgFactor) / (1 + i);
			}
			return avgFactor;
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

		#endregion // Properties

		#region Data

		private SurvivalCurve survivalCurveAlt_;

		#endregion // Data

	} // class UniformBasketPricer

}
