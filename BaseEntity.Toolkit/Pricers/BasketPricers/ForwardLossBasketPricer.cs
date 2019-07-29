/*
 * ForwardLossBasketPricer.cs
 *
 */
//#define Debug
//#define IncludeExtraDebug

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
	/// <summary>
	///   Pricing helper class for forward loss basket pricer
	/// </summary>
	///
	/// <remarks>
	///   This helper class sets up a basket and pre-calculates anything specific to the basket but
	///   independent of the product.
	/// </remarks>
	///
  [Serializable]
	public class ForwardLossBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(BasketPricer));

    #region Constructors
		/// <exclude />
		public ForwardLossBasketPricer()
		{
		}

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
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="indexNotes">Array of index notes by tenors</param>
		/// <param name="indexSpreads">Array of quoted index spreads</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="stateLosses">An array of losses in different states.</param>
		/// <param name="transitionCoefs">Transition coefficients</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="stateIndices">Array of segment indices</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		ForwardLossBasketPricer( Dt asOf,
														 Dt settle,
														 Dt maturity,
														 SurvivalCurve [] survivalCurves,
														 RecoveryCurve [] recoveryCurves,
														 double [] principals,
														 int stepSize,
														 TimeUnit stepUnit,
														 CDX [] indexNotes,
														 double [] indexSpreads,
														 DiscountCurve discountCurve,
														 double [] stateLosses,
														 double [,] transitionCoefs,
														 double [] scalingFactors,
														 double [] baseLevels,
														 int [] stateIndices )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							new Copula(), new SingleFactorCorrelation( new string[]{"dummy"}, 0.0 ),
							stepSize, stepUnit, stateLosses )
		{
      logger.DebugFormat("Creating Forward Loss Basket asof={0}, settle={1}, maturity={2}, principal={3}",
                          asOf, settle, maturity, principals[0]);
      
			// Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			this.Distribution = new Curve2D();
			this.distributionComputed_ = false;

			this.indexNotes_ = indexNotes;
			this.indexSpreads_ = indexSpreads;
			this.discountCurve_ = discountCurve;
			CalibrateRefSurvivalCurve();

			Dt [] tenors = new Dt[ indexNotes.Length ];
			for( int i = 0; i < indexNotes.Length; ++i )
				tenors[i] = indexNotes[i].Maturity;
			this.tenors_ = tenors;

			this.tenorTransitionRates_ = null;
			this.TransitionRates = new double[ stateLosses.Length ];
			this.stateIndices_ = stateIndices;
			this.transitionCoefs_ = transitionCoefs;
			this.scalingFactors_ = scalingFactors;
			this.baseLevels_ = baseLevels;

			this.StateLosses = stateLosses;
			this.probabilities_ = new double[ stateLosses.Length ];
			initDistribution( createDateArray() );

      this.recalcStart_ = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
			this.recalcStop_ = Maturity;

			logger.Debug( "Forward Loss Basket created" );

			return;
		}

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
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="stateLosses">An array of losses in different states.</param>
		/// <param name="transitionCoefs">Transition coefficients</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="tenors">Array of tenor dates</param>
		/// <param name="stateIndices">Array of segment indices</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		ForwardLossBasketPricer( Dt asOf,
														 Dt settle,
														 Dt maturity,
														 SurvivalCurve [] survivalCurves,
														 RecoveryCurve [] recoveryCurves,
														 double [] principals,
														 int stepSize,
														 TimeUnit stepUnit,
														 double [] stateLosses,
														 double [,] transitionCoefs,
														 double [] scalingFactors,
														 double [] baseLevels,
														 Dt [] tenors,
														 int [] stateIndices )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							new Copula(), new SingleFactorCorrelation( new string[]{"dummy"}, 0.0 ),
							stepSize, stepUnit, stateLosses )
		{
      logger.DebugFormat("Creating Forward Loss Basket asof={0}, settle={1}, maturity={2}, principal={3}",
                          asOf, settle, maturity, principals[0]);
      
			// Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			this.Distribution = new Curve2D();
			this.distributionComputed_ = false;

			this.tenorTransitionRates_ = null;
			this.TransitionRates = new double[ stateLosses.Length ];
			this.tenors_ = tenors;
			this.stateIndices_ = stateIndices;
			this.transitionCoefs_ = transitionCoefs;
			this.scalingFactors_ = scalingFactors;
			this.baseLevels_ = baseLevels;

			this.StateLosses = stateLosses;
			this.probabilities_ = new double[ stateLosses.Length ];
			initDistribution( createDateArray() );

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      this.recalcStart_ = start;
			this.recalcStop_ = Maturity;

			logger.Debug( "Forward Loss Basket created" );

			return;
		}

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
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="stateLosses">An array of losses in different states.</param>
		/// <param name="transitionRates">Transition rates</param>
		/// <param name="tenors">Array of tenors</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    protected
		ForwardLossBasketPricer( Dt asOf,
														 Dt settle,
														 Dt maturity,
														 SurvivalCurve [] survivalCurves,
														 RecoveryCurve [] recoveryCurves,
														 double [] principals,
														 int stepSize,
														 TimeUnit stepUnit,
														 double [] stateLosses,
														 double [,] transitionRates,
														 Dt [] tenors )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							new Copula(), new SingleFactorCorrelation( new string[]{"dummy"}, 0.0 ),
							stepSize, stepUnit, stateLosses )
		{
      logger.DebugFormat("Creating Forward Loss Basket asof={0}, settle={1}, maturity={2}, principal={3}",
                          asOf, settle, maturity, principals[0]);
      
			// Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			this.Distribution = new Curve2D();
			this.distributionComputed_ = false;

			this.tenorTransitionRates_ = transitionRates;
			this.tenors_ = tenors;
			this.TransitionRates = new double[ stateLosses.Length ];
			this.stateIndices_ = null;
			this.transitionCoefs_ = null;

			this.StateLosses = stateLosses;
			this.probabilities_ = new double[ stateLosses.Length ];
			initDistribution( createDateArray() );

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      this.recalcStart_ = start;
			this.recalcStop_ = Maturity;

			logger.Debug( "Forward Loss Basket created" );

			return;
		}

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
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="stateLosses">An array of losses in different states.</param>
		/// <param name="transitionRates">Transition rates.</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    protected
		ForwardLossBasketPricer( Dt asOf,
														 Dt settle,
														 Dt maturity,
														 SurvivalCurve [] survivalCurves,
														 RecoveryCurve [] recoveryCurves,
														 double [] principals,
														 int stepSize,
														 TimeUnit stepUnit,
														 double [] stateLosses,
														 double [] transitionRates )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							new Copula(), new SingleFactorCorrelation( new string[]{"dummy"}, 0.0 ),
							stepSize, stepUnit, stateLosses )
		{
      logger.DebugFormat("Creating Forward Loss Basket asof={0}, settle={1}, maturity={2}, principal={3}",
                          asOf, settle, maturity, principals[0]);
      
			// Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			this.Distribution = new Curve2D();
			this.distributionComputed_ = false;

			this.tenorTransitionRates_ = null;
			this.tenors_ = null;
			this.TransitionRates = transitionRates;
			this.stateIndices_ = null;
			this.transitionCoefs_ = null;

			this.StateLosses = stateLosses;
			this.probabilities_ = new double[ stateLosses.Length ];
			initDistribution( createDateArray() );

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      this.recalcStart_ = start;
			this.recalcStop_ = Maturity;

			logger.Debug( "Forward Loss Basket created" );

			return;
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			ForwardLossBasketPricer obj = (ForwardLossBasketPricer)base.Clone();

      obj.stateLosses_ = CloneUtil.Clone(stateLosses_);
      if (probabilities_ != null) obj.probabilities_ = CloneUtil.Clone(probabilities_);
      obj.transitionRates_ = CloneUtil.Clone(transitionRates_);
			obj.distribution_ = distribution_.clone();
			obj.tenors_ = Dt.CloneArray( tenors_ );

			if( refSurvivalCurve_ != null )
				obj.refSurvivalCurve_ = (SurvivalCurve) refSurvivalCurve_.Clone();
			if( discountCurve_ != null )
				obj.discountCurve_ = (DiscountCurve) discountCurve_.Clone();

			if( indexNotes_ != null )
			{
				CDX [] notes = new CDX[ indexNotes_.Length ];
				for( int i = 0; i < notes.Length; ++i )
					notes[i] = (CDX)indexNotes_[i].Clone();
				obj.indexNotes_ = notes;
			}
      obj.indexSpreads_ = CloneUtil.Clone(indexSpreads_);

			obj.tenorTransitionRates_ = CloneUtil.Clone( tenorTransitionRates_ );

			if( stateIndices_ != null )
			{
				int [] stateIndices = new int[ stateIndices_.Length ];
				for( int i = 0; i < stateIndices_.Length; ++i )
					stateIndices[i] = stateIndices_[i];
				obj.stateIndices_ = stateIndices;
			}

      obj.transitionCoefs_ = CloneUtil.Clone(transitionCoefs_);
      obj.scalingFactors_ = CloneUtil.Clone(scalingFactors_);
      obj.baseLevels_ = CloneUtil.Clone(baseLevels_);

			return obj;
		}
		#endregion // Constructors

		#region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      
      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      if (stateLosses_ == null)
        InvalidValue.AddError(errors, this, "StateLosses", String.Format("Invalid state losses. Cannot be null"));

      if (transitionRates_ == null)
        InvalidValue.AddError(errors, this, "TransitionRates", String.Format("Invalid transition rates. Cannot be null"));


      return;
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
		  if( !distributionComputed_ )
				computeDistribution();

		  // find the start level
			lossLevels = SetLossLevels(lossLevels, false).ToArray();
			double [,] result = new double[ lossLevels.Length, 2 ];

			if( wantProbability )
			{
				int nLevels = distribution_.NumLevels();
				int k = 0;
				double cumulative = 0.0;
				for( int j = 0; j < lossLevels.Length; ++j )
				{
					double xj = lossLevels[j];
					for( ; k < nLevels && stateLosses_[k] <= xj; ++k )
					{
						cumulative += distribution_.Interpolate( date, (double)k );
					}
					result[j,0] = xj + this.PreviousLoss;
					result[j,1] = cumulative;
				}
			}
			else
			{
				int nLevels = distribution_.NumLevels();
				int k = 0;
				double probHigh = 1.0;
				double cumulative = 0.0;
				double lossRate = Math.Max(0.0, 1 - this.AverageRecoveryRate);
				for( int j = 0; j < lossLevels.Length; ++j )
				{
					double xj = lossLevels[j];
					for( ; k < nLevels && stateLosses_[k] <= xj; ++k )
					{
					  double probability = distribution_.Interpolate( date, (double)k );
						cumulative += probability * stateLosses_[k] * lossRate;
						probHigh -= probability;
					}
					result[j,0] = xj + this.PreviousLoss;
					result[j,1] = cumulative;
					if( probHigh > 0.0 )
						result[j,1] += probHigh * xj;
				}
			}

			return result;
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
		  if( !distributionComputed_ )
				computeDistribution();
				
			double loss = 0;
			AdjustTrancheLevels( false,
													 ref trancheBegin,
													 ref trancheEnd,
													 ref loss );
			loss += CalculateAccumulatedLoss( date, trancheBegin, trancheEnd );
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
			double amortized = 0 ;
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

		#endregion // Methods

		#region FLM_Specific
		/// <summary>
		///   Create the default scaling factors
		/// </summary>
		public static double [] GetDefaultScalingFactors( int basketSize, double rate )
		{
		  double [] scalingFactors = new double[ basketSize + 1 ];
			for( int i = 0; i <= basketSize; ++i )
				scalingFactors[i] = Math.Exp(rate*i) * (basketSize - i); // basketSize ;
			return scalingFactors;
		}

		/// <summary>
		///   Create the default base levels
		/// </summary>
		public static double [] GetDefaultBaseLevels( int basketSize )
		{
		  double [] baseLevels = new double[ basketSize + 1 ];
			for( int i = 0; i <= basketSize; ++i )
				baseLevels[i] = 0.0;
			return baseLevels;
		}

		/// <summary>
		///   Create the default state losses
		/// </summary>
		public static double [] GetDefaultStateLosses( int basketSize )
		{
			double [] stateLosses = new double[ basketSize + 1 ];
			for( int i = 0; i <= basketSize; ++i )
				stateLosses[i] = ((double) i) / basketSize;
			return stateLosses;
		}
		#endregion // FLM_Specific

		#region Helpers
		/// <summary>
		///   Create an array of sorted hazard rates
		/// </summary>
		private static double []
		sortedHazardRates( SurvivalCurve [] sc,
											 Dt start, Dt end )
		{
			int N = sc.Length;
		  double [] result = new double[ N ];
			for( int i = 0; i < N; ++i )
				result[i] = sc[i].HazardRate(start, end);
			Array.Sort( result );
			for( int i = 0, j = N - 1; j > i; ++i, --j )
			{
				double tmp = result[i];
				result[i] = result[j];
				result[j] = tmp;
			}
			return result ;
		}

		/// <summary>
		///   Create an array of transformed scaling factors
		/// </summary>
		private static double []
		transformScalingFactors( double [] scalingFactors,
														 double [] sortedHazardRates,
														 double alpha, double beta, int flat )
		{
		  int N = sortedHazardRates.Length;
			double [] result = new double[ N + 1 ];
			result[N] = 0;

			double [] weights = new double[ N ];
			double [] tmp = new double[ N ];
			tmp[0] = 0.0;
			weights[0] = 0.0;
			for( int i = 1; i < N; ++i )
			{
				tmp[i] = alpha * tmp[i-1] + sortedHazardRates[i-1];
				weights[i] = alpha * weights[i-1] + 1;
			}

			double sum = 0;
			double sumWeight = 0;
			for( int k = N - 1; k >= 0; --k )
			{
			  double wk = (k > flat ? 1.0 : beta);
				sum = wk * sum + sortedHazardRates[k];
				sumWeight = wk * sumWeight + 1;
				result[k] = scalingFactors[k] * (sum + tmp[k]);// / (sumWeight + weights[k]);
			}

			//sum /= sumWeight;
			//if( sum > 1E-14 )
			//	for( int k = 0; k < N; ++k )
			//	  result[k] /= sum;

			return result;
		}

		/// <summary>
		///   Create an array of transformed scaling factors
		/// </summary>
		private static double []
		transformScalingFactors( double [] scalingFactors,
														 SurvivalCurve [] sc,
														 Dt start, Dt end )
		{
		  double [] hazards = sortedHazardRates( sc, start, end );
			return transformScalingFactors( scalingFactors, hazards, Alpha, Beta, Flat );
		}

		/// <summary>
		///   Create an array of transformed scaling factors
		/// </summary>
		private static double []
		transformScalingFactors( double [] scalingFactors,
														 SurvivalCurve sc,
														 Dt start, Dt end )
		{
		  return scalingFactors;
		}

		// Create an array of dates as time grid
		// NOTE: It might be better to let the user specify the time grid
		private Dt[] createDateArray()
		{
		  int nDates = 1;
			Dt maturity = Maturity;
      Dt current = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      if (!current.IsValid()) current = Settle;
      while( Dt.Cmp(current, maturity) < 0 )
      {
        current = Dt.Add(current, StepSize, StepUnit);
        if( Dt.Cmp(current, maturity) > 0 )
          current = maturity;
        ++nDates;
      }
			Dt [] dates = new Dt[ nDates ];
      current = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      if (!current.IsValid()) current = Settle;
			dates[0] = current;
			for( int i = 1; i < nDates; ++i )
      {
        current = Dt.Add(current, StepSize, StepUnit);
        if( Dt.Cmp(current, maturity) > 0 )
          current = maturity;
				dates[i] = current;
      }
			
			return dates;
		}

		// initialize distribution
		private void initDistribution( Dt [] dates )
		{
			int nDates = dates.Length;
			int nLevels = probabilities_.Length;
      distribution_.Initialize(nDates, nLevels, 1);
      distribution_.SetAsOf(PortfolioStart.IsEmpty() ? Settle : PortfolioStart);
			for( int i = 0; i < nDates; ++i )
				distribution_.SetDate( i, dates[i] );
      for( int i = 0; i < nLevels; ++i )
			{
        distribution_.SetLevel( i, (double)i );
				distribution_.SetValue( i, 0.0 );
			}
			distribution_.SetValue( 0, 1.0 ); // start with no default at portfolio start date
			return;
		}

		// recompute the distribution for the whole period
		private void computeDistribution()
		{
		  if( stateIndices_ != null || transitionCoefs_ != null ) {
			  CalibrateRefSurvivalCurve();
        tenorTransitionRates_ = TransitionRatesFromCoefs(
          this.PortfolioStart.IsEmpty() ? Settle : PortfolioStart,
          this.tenors_, this.stateIndices_, this.transitionCoefs_,
          this.scalingFactors_, this.baseLevels_, this.RefSurvivalCurve);
			}
		  if( tenors_ == null || tenorTransitionRates_ == null ) {
				computeDistribution( recalcStart_, recalcStop_ );
			}
			else
			{
        Dt start = this.PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
				for( int j = 0; j < tenors_.Length; ++j )
				{
					Dt stop = tenors_[j];
					if( Dt.Cmp(stop, this.Maturity) > 0 )
						stop = this.Maturity;
					if( Dt.Cmp(stop, start) <= 0 )
						break;
					for( int i = 0; i < transitionRates_.Length; ++i )
						transitionRates_[i] = tenorTransitionRates_[i,j];
					computeDistribution( start, stop );
					start = stop;
				}
			}
			return;
		}

		// recompute the distribution for the period from the start date to stop date
		private void computeDistribution(Dt start, Dt stop)
		{
		  int nDates = distribution_.NumDates();

			// find the start index in Curve2D
		  int startIdx = -1;
			for( int i = 0; i < nDates; ++i )
			{
				Dt date = distribution_.GetDate(i);
				int cmp = Dt.Cmp(date, start);
				if( cmp == 0 )
				{
					startIdx = i;
					break;
				}
				else if( cmp > 0 )
				{
					startIdx = (i > 0 ? i - 1 : 0);
					break;
				}
			}
			if( startIdx < 0 )
				throw new ArgumentException("Invalid start date");

			// find the stop index in Curve2D
		  int stopIdx = -1;
			for( int i = startIdx; i < nDates; ++i )
			{
				Dt date = distribution_.GetDate(i);
				int cmp = Dt.Cmp( date, stop );
				if( cmp >= 0 )
				{
					stopIdx = i;
					break;
				}
			}
			if( stopIdx < 0 )
				throw new ArgumentException("Invalid stop date");

			// get the initial distribution
			int nLevels = distribution_.NumLevels();
			for( int k = 0; k < nLevels; ++k )
				probabilities_[k] = distribution_.GetValue( startIdx, k );

			#if Debug
			CheckProbabilities( probabilities_ );
			#endif

			// find time step
			double deltaT = CalculateTimeStep( transitionRates_ );
			// now calculate the distributions
			Dt prevDate = distribution_.GetDate( startIdx );
			for( int iDate = startIdx + 1; iDate <= stopIdx; ++iDate )
			{
				Dt date = distribution_.GetDate( iDate );
				double T = Dt.TimeInYears( prevDate, date );

				if( Math.Abs(transitionRates_[ transitionRates_.Length - 1 ]) >  1.0e-10 )
				  throw new ArgumentException("Non-zero transition rates");	

				double expectedDefaults = CalculateExpectedDefault( date );
				#if Debug
				double [] initProbabilities = BaseEntityObject.CloneArray( probabilities_ );
				#endif
				ForwardLossBasketModel.CalculateProbabilities( nLevels,
																											 T,
																											 Math.Min( T, deltaT ),
																											 probabilities_,
																											 transitionRates_,
																											 expectedDefaults,
																											 probabilities_ );

				#if Debug
				CheckProbabilities( probabilities_ );
				#endif

				for( int k = 0; k < nLevels; ++k )
					distribution_.SetValue( iDate, k, probabilities_[k] );

				// prepare for the next loop
				prevDate = date;
			}

			// everything is done
			distributionComputed_ = true;

			return;
		}

		// calculate the maximum time step dt
		private static double
		CalculateTimeStep( double [] transitionRates )
		{
		  const double Upper = 0.025;
		  double dt = 1.0;
			foreach( double a in transitionRates )
			{
				if( a * dt > Upper )
					dt = Upper / a;
			}
			return dt;
		}

		// calculated the expected defaults
		private double CalculateExpectedDefault( Dt date )
		{
		  SurvivalCurve [] sc = this.SurvivalCurves;
      Dt start = this.PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
			double expectedDefault = 0;
			int N = sc.Length;

		  if( refSurvivalCurve_ != null )
			{
				double prob = 1 - refSurvivalCurve_.Interpolate(start, date);
				expectedDefault = prob * N;
			}
			else
			{
				for( int i = 0; i < N; ++i )
				{
			    // default probability of name i
				  double prob = 1 - sc[i].Interpolate(start, date);
					expectedDefault += prob ;
				}
			}
			return expectedDefault;
		}

		// calculated the expected cumulative losses
		private double
		CalculateAccumulatedLoss( Dt date,
															double trancheBegin,
															double trancheEnd )
		{
		  // find the start level
		  int nLevels = distribution_.NumLevels();
			int startLevel = -1;
			double probHigh = 1.0;
			double lossRate = Math.Max( 0.0, 1 - this.AverageRecoveryRate );
			for( int k = 0; k < nLevels; ++k )
			{
				if( stateLosses_[k] * lossRate > trancheBegin )
				{
					startLevel = k;
					break;
				}
				double probability = distribution_.Interpolate( date, (double)k );
				probHigh -= probability;
			}
			if( startLevel < 0 )
				throw new ArgumentException("Invalid attachment");

			double trancheWidth = trancheEnd - trancheBegin;
			double loss = 0;			
			for( int k = startLevel; k < nLevels; ++k )
			{
				double probability = distribution_.Interpolate( date, (double)k );
				double trancheLoss = stateLosses_[k]*lossRate - trancheBegin;
				if( trancheLoss >= trancheWidth )
					break;

				loss += probability * trancheLoss ;
				probHigh -= probability;
			}

			if( probHigh > 0.0 )
				loss += probHigh * trancheWidth;

			return loss;
		}

		private static void
		CheckProbabilities( double [] p )
		{
		  double sum = 0;
			int N = p.Length - 1;
			for( int i = 0; i <= N; ++i )
				sum += p[i];
			if( Math.Abs( sum - 1.0 ) > 1.0e-6 )
			{
				if( sum < 1.0 )
					p[N] += 1 - sum;
				//else
				//	throw new ToolkitException("Internal error: probaility not sum up to one");
			}
			return;
		}
 
		/// <summary>
		///   Find a state corresponding to the attachment point
		/// </summary>
		/// <exclude />
		public static int FindStateIndex( double adjustedAttachment, double [] stateLosses )
		{
			for( int k = 0; k < stateLosses.Length; ++k )
			{
			  double xk = stateLosses[k];
				if( xk >= adjustedAttachment )
					return k ;
			}
			return stateLosses.Length;
		}

		// calculate the vaerage hazard rate for a period
		private double AverageHazardRate( Dt start, Dt end )
		{
			return AverageHazardRate( start, end,  this.SurvivalCurves );
		}

		// calculate the vaerage hazard rate for a period
		private static double
		AverageHazardRate( Dt start, Dt end, SurvivalCurve[] sc )
		{
		  double result = 0;
			int N = sc.Length; 
			for( int i = 0; i < N; ++i )
				result += sc[i].HazardRate(start, end);
			return result / N;
		}

		private void SetTransitionRates( int start, double x,
																		 double [] scalings,
																		 double [] bases )
		{
		  double[] transitionRates = this.TransitionRates;
			int N = transitionRates.Length - 1;
			for( int k = start; k < N; ++k )
			{
				transitionRates[k] = scalings[k] * x + bases[k];
			}
			transitionRates[N] = 0;
			return;
		}

		// set transition rates for all the states from index start
		private static void SetTransitionRates( double[] transitionRates, 
																						int start, double x,
																						double [] scalings,
																						double [] bases )
		{
			int N = transitionRates.Length - 1;
			for( int k = start; k < N; ++k )
			{
				transitionRates[k] = scalings[k] * x + bases[k];
			}
			transitionRates[N] = 0;
			return;
		}

		// reset calculation period
		private void SetRecalcPeriod( Dt start, Dt stop )
		{
		  recalcStart_ = start;
			recalcStop_ = stop;
		}

		#endregion // Helpers

		#region Properties
		/// <summary>
		///   Array of losses in states
		/// </summary>
		public double [] StateLosses
		{
			get { return stateLosses_; }
			set {
			  stateLosses_ = value; 
			}
		}

		/// <summary>
		///   Matrix of transition rates arranged in an array
		/// </summary>
		public double [] TransitionRates
		{
			get { return transitionRates_; }
			set {
			  transitionRates_ = value; 
			}
		}

		/// <summary>
		///   The result distribution of basket
		/// </summary>
		public Curve2D Distribution
		{
			get { return distribution_; }
			set { distribution_ = value; }
		}

		/// <summary>
		///   Reference survival curve
		/// </summary>
		public SurvivalCurve RefSurvivalCurve
		{
		  get { return refSurvivalCurve_; }
		  set { refSurvivalCurve_ = value; }
		}

		/// <summary>
		///   CDX notes
		/// </summary>
		public CDX [] IndexNotes
		{
		  get { return indexNotes_; }
		}

		/// <summary>
		///   CDX quotes
		/// </summary>
		public double [] IndexSpreads
		{
		  get { return indexSpreads_; }
		}

		/// <summary>
		///   Index dicount curve
		/// </summary>
		public DiscountCurve DiscountCurve 
		{
		  get { return discountCurve_; }
		}
		#endregion // Properties

		#region Data
		private double [] stateLosses_;
		private double [] probabilities_;

		private SurvivalCurve refSurvivalCurve_;
		private DiscountCurve discountCurve_;
		private CDX [] indexNotes_;
		private double [] indexSpreads_;

		private double [] transitionRates_;
		private double [,] tenorTransitionRates_;
		private Dt [] tenors_;
		private int [] stateIndices_;
		private double [,] transitionCoefs_;
		private double [] scalingFactors_;
		private double [] baseLevels_;

		private Curve2D distribution_;
		private bool distributionComputed_;
		private Dt recalcStart_;
		private Dt recalcStop_;

		/// <exclude />
		public static double Alpha = 0.985;
		/// <exclude />
		public static double Beta  = 0.995;
		/// <exclude />
		public static int Flat = 200;
		#endregion Data

		#region Optimal_Calibration
		/// <summary>
		///   Calibrate reference curve
		/// </summary>
    private void CalibrateRefSurvivalCurve()
		{
		  NegSPTreatment nspTreatment = NegSPTreatment.Allow;
			bool forceFit = false;
			Interp interp = null;
			if( refSurvivalCurve_ != null ) {
			  interp = refSurvivalCurve_.Interp;
		    SurvivalFitCalibrator calibrator
					= (SurvivalFitCalibrator) refSurvivalCurve_.SurvivalCalibrator;
				if( calibrator != null )
				{
					nspTreatment = calibrator.NegSPTreatment;
					forceFit = calibrator.ForceFit;
				}
			}

			refSurvivalCurve_ = CalibrateRefSurvivalCurve( this.AsOf,
																										 this.IndexNotes,
																										 this.IndexSpreads,
																										 this.discountCurve_,
																										 interp,
																										 nspTreatment,
																										 forceFit );
			return;
		}

		/// <summary>
		///   Calibrate reference curve
		/// </summary>
    public static SurvivalCurve
		CalibrateRefSurvivalCurve(
															Dt asOf,
															CDX [] notes,
															double [] quotedSpreads,
															DiscountCurve discountCurve,
															Interp interp,
															NegSPTreatment nspTreatment,
															bool forceFit
															)
		{
			// Validate
			if( notes == null || notes.Length <= 0 )
				throw new ArgumentException("Must specify at least one CDX note");
			if( quotedSpreads == null || quotedSpreads.Length <= 0 )
				throw new ArgumentException("Must specify at least one spread quote");
      if( quotedSpreads.Length != notes.Length )
        throw new ArgumentException("Number of quoted spreads must match number of CDX notes");

			double recoveryRate = 0.40;
			RecoveryCurve recoveryCurve = new RecoveryCurve( asOf, recoveryRate );

      SurvivalFitCalibrator calibrator =
				new SurvivalFitCalibrator( asOf, asOf, recoveryCurve, discountCurve );
			calibrator.NegSPTreatment = nspTreatment;
			calibrator.ForceFit = forceFit;

			SurvivalCurve curve = new SurvivalCurve(calibrator);
      curve.Interp = ( interp != null ? interp :
											 InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const) );

      for( int i = 0; i < notes.Length; i++ )
			{
				CDX note = notes[i];
				CurveTenor tenor = curve.AddCDS( note.Description,
																				 note.Maturity, 0.0, note.Premium,
																				 note.DayCount, note.Freq,
																				 note.BDConvention, note.Calendar);
        CDXPricer cdxPricer = new CDXPricer( note, asOf, asOf, discountCurve, discountCurve, null, quotedSpreads[i] );
        double marketPv = cdxPricer.MarketPrice() - 1;
				tenor.MarketPv = marketPv; // For survival curve, we cannot use the fee argument in curve.AddCDS()
			}

      curve.ReFit(0);

			return curve;
		}


		/// <summary>
		///   Calculate a table of the implied transition coefficients
		/// </summary>
		///
		/// <remarks>
		///   Each column represents the implied transition coefficients for a tenor.
		/// </remarks>
		///
		/// <param name="tranches">Array of CDO tranches</param>
		/// <param name="maturityDates">Array of maturity or scheduled termination dates</param>
		/// <param name="premia">Array of annualised premium in basis points (200 = 200bp)</param>
		/// <param name="fees">Array of up-front fee in percent (0.1 = 10%)</param>
		///
		/// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurve">Calibrated reference survival Curve</param>
		/// <param name="principals">Array of original face amounts for each basket name or one face amount for all names</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="stateLosses">Losses in different states.</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="stateIndices">Array of segment indices</param>
		/// <param name="initialX">Starting values values</param>
		/// <param name="weights">weights on tranches</param>
		///
		/// <returns>Transition coefficients</returns>
		///
    public static double [,]
		ImpliedTransitionCoefs(
													 SyntheticCDO [] tranches,
													 Dt [] maturityDates,
													 double [,] premia,
													 double [,] fees,
													 Dt asOfDate,
													 Dt settleDate,
													 DiscountCurve discountCurve,
													 SurvivalCurve survivalCurve,
													 double [] principals,
													 int stepSize,
													 TimeUnit stepUnit,
													 double [] stateLosses,
													 double [] scalingFactors,
													 double [] baseLevels,
													 int [] stateIndices,
													 double [] initialX,
													 double [] weights
													 )
    {
			// important counts
			int nCDOs = premia.GetLength(0);
			int nTenors = premia.GetLength(1);
			int basketSize = principals.Length;

			// dumy curves
			RecoveryCurve recoveryCurve = new RecoveryCurve( asOfDate, 0.40 );
			SurvivalCurve [] survivalCurves = new SurvivalCurve[ basketSize ];
			RecoveryCurve [] recoveryCurves = new RecoveryCurve[ basketSize ];
			for( int i = 0; i < basketSize; ++i )
			{
				survivalCurves[i] = survivalCurve;
				recoveryCurves[i] = recoveryCurve;
			}

			// Create basket pricer
			Dt maturity = maturityDates[ nTenors - 1 ];
			ForwardLossBasketPricer basket =
				new ForwardLossBasketPricer( asOfDate,
																		 settleDate,
																		 maturity,
																		 survivalCurves,
																		 recoveryCurves,
																		 principals,
																		 stepSize,
																		 stepUnit,
																		 stateLosses,
																		 new double[ stateLosses.Length ] // for transition rates
																		 );
			basket.refSurvivalCurve_ = survivalCurve;

			// Create pricers
			int nFits = initialX.Length;
			double [,] result = new double[ nFits, nTenors ];

			// creates pricers
			SyntheticCDOPricer [] pricers = new SyntheticCDOPricer[ nCDOs ];
			for( int i = 0; i < nCDOs; ++i )
			{
				SyntheticCDO cdo = (SyntheticCDO) tranches[i].Clone();
				cdo.Maturity = maturity;
				pricers[i] = new SyntheticCDOPricer(cdo, basket, discountCurve, 1.0, null);
			}

			// Setup the optimizers
			if( null == weights || 0 == weights.Length )
			{
				weights = new double[ nCDOs ];
				for( int i = 0; i < nCDOs; ++i )
					weights[i] = 1.0;
			}

			// Save original scaling factors
			double [] origScalingFactors = scalingFactors;

			Optimizer calibrator = new Optimizer( pricers,
																						weights,
																						stateIndices );

			// Compute the implied transitions
      Dt settle = basket.PortfolioStart.IsEmpty() ? basket.Settle : basket.PortfolioStart;
			Dt prevDate = settle;
			for( int j = 0; j < nTenors; ++j )
			{
				maturity = maturityDates[ j ];

				// Set correct fees and spreads
				for( int i = 0; i < nCDOs; ++i )
				{
				  SyntheticCDO cdo = pricers[i].CDO;
				  cdo.Maturity = maturity;
					cdo.Fee = fees[i,j];
					cdo.Premium = premia[i,j] / 10000;
				}

				// initialized the scaling factors
				scalingFactors = transformScalingFactors( origScalingFactors,
																									survivalCurve,
																									settle, maturity );

				// initialize the transition rates
				double hazardRate = survivalCurve.HazardRate( prevDate, maturity );
				if( null == initialX || 0 == initialX.Length )
				{
					initialX = new double[ nFits ];
					for( int i = 0; i < nFits; ++i )
						initialX[i] = hazardRate;
				}
				else {
					if( initialX.Length != nFits )
						throw new ToolkitException( String.Format("Initial x (Length:{0}) not match segments (Length:{1})",
																				initialX.Length, stateIndices.Length) );
					for( int i = 0; i < nFits; ++i )
						initialX[i] *= hazardRate;
				}

				basket.SetTransitionRates( 0, hazardRate, scalingFactors, baseLevels );
				double [] tmp = calibrator.Fit( initialX, null,
																				prevDate, maturity,
																				scalingFactors,
																				baseLevels );

				for( int i = 0; i < nFits; ++i )
				{
				  result[i,j] = tmp[i] / hazardRate ;
				}

				prevDate = maturity;
				initialX = tmp;
			}

			return result;
		}


		/// <summary>
		///   Calculate a table of the implied transition coefficients
		/// </summary>
		///
		/// <remarks>
		///   Each column represents the implied transition coefficients for a tenor.
		/// </remarks>
		///
		/// <param name="tranches">Array of CDO tranches</param>
		/// <param name="maturityDates">Array of maturity or scheduled termination dates</param>
		/// <param name="premia">Array of annualised premium in basis points (200 = 200bp)</param>
		/// <param name="fees">Array of up-front fee in percent (0.1 = 10%)</param>
		///
		/// <param name="asOfDate">Pricing as-of date</param>
		/// <param name="settleDate">Settlement date for pricing</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Array of original face amounts for each basket name or one face amount for all names</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="stateLosses">Losses in different states.</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="stateIndices">Array of segment indices</param>
		/// <param name="initialX">Starting values values</param>
		/// <param name="weights">weights on tranches</param>
		///
		/// <returns>Transition coefficients</returns>
		///
    public static double [,]
		ImpliedTransitionCoefs(
													 SyntheticCDO [] tranches,
													 Dt [] maturityDates,
													 double [,] premia,
													 double [,] fees,
													 Dt asOfDate,
													 Dt settleDate,
													 DiscountCurve discountCurve,
													 SurvivalCurve [] survivalCurves,
													 RecoveryCurve [] recoveryCurves,
													 double [] principals,
													 int stepSize,
													 TimeUnit stepUnit,
													 double [] stateLosses,
													 double [] scalingFactors,
													 double [] baseLevels,
													 int [] stateIndices,
													 double [] initialX,
													 double [] weights
													 )
    {
			// important counts
			int nCDOs = premia.GetLength(0);
			int nTenors = premia.GetLength(1);

			// Create basket pricer
			Dt maturity = maturityDates[ nTenors - 1 ];
			ForwardLossBasketPricer basket =
				new ForwardLossBasketPricer( asOfDate,
																		 settleDate,
																		 maturity,
																		 survivalCurves,
																		 recoveryCurves,
																		 principals,
																		 stepSize,
																		 stepUnit,
																		 stateLosses,
																		 new double[ stateLosses.Length ] // for transition rates
																		 );

			// Create pricers
			int nFits = initialX.Length;
			double [,] result = new double[ nFits, nTenors ];

			// creates pricers
			SyntheticCDOPricer [] pricers = new SyntheticCDOPricer[ nCDOs ];
			for( int i = 0; i < nCDOs; ++i )
			{
				SyntheticCDO cdo = (SyntheticCDO) tranches[i].Clone();
				cdo.Maturity = maturity;
				pricers[i] = new SyntheticCDOPricer(cdo, basket, discountCurve, 1.0, null);
			}

			// Setup the optimizers
			if( null == weights || 0 == weights.Length )
			{
				weights = new double[ nCDOs ];
				for( int i = 0; i < nCDOs; ++i )
					weights[i] = 1.0;
			}

			// Save original scaling factors
			double [] origScalingFactors = scalingFactors;

			Optimizer calibrator = new Optimizer( pricers,
																						weights,
																						stateIndices );

			// Compute the implied transitions
      Dt settle = basket.PortfolioStart.IsEmpty() ? basket.Settle : basket.PortfolioStart;
      if (!settle.IsValid())
        settle = basket.Settle;
			Dt prevDate = settle;
			for( int j = 0; j < nTenors; ++j )
			{
				maturity = maturityDates[ j ];

				// Set correct fees and spreads
				for( int i = 0; i < nCDOs; ++i )
				{
				  SyntheticCDO cdo = pricers[i].CDO;
				  cdo.Maturity = maturity;
					cdo.Fee = fees[i,j];
					cdo.Premium = premia[i,j] / 10000;
				}

				// initialized the scaling factors
				scalingFactors = transformScalingFactors( origScalingFactors,
																									basket.SurvivalCurves,
																									settle, maturity );

				// initialize the transition rates
				double hazardRate = 1.0;//basket.AverageHazardRate( prevDate, maturity );
				if( null == initialX || 0 == initialX.Length )
				{
					initialX = new double[ nFits ];
					for( int i = 0; i < nFits; ++i )
						initialX[i] = hazardRate;
				}
				else {
					if( initialX.Length != nFits )
						throw new ToolkitException( String.Format("Initial x (Length:{0}) not match segments (Length:{1})",
																				initialX.Length, stateIndices.Length) );
					for( int i = 0; i < nFits; ++i )
						initialX[i] *= hazardRate;
				}

				basket.SetTransitionRates( 0, hazardRate, scalingFactors, baseLevels );
				double [] tmp = calibrator.Fit( initialX, null,
																				prevDate, maturity,
																				scalingFactors,
																				baseLevels );

				for( int i = 0; i < nFits; ++i )
				{
				  result[i,j] = tmp[i] / hazardRate ;
				}

				prevDate = maturity;
				initialX = tmp;
			}

			return result;
		}

		/// <summary>
		///   Calculate transition rates from coefficients
		/// </summary>
		///
		/// <remarks>
		///   Each column represents the implied transition coefficients for a tenor.
		/// </remarks>
		///
		/// <param name="settle">Settlement date for pricing</param>
		/// <param name="maturities">Array of maturity or scheduled termination dates</param>
		/// <param name="stateIndices">Array of segment indices</param>
		/// <param name="coefs">Transiyion coefficients</param>
		/// <param name="scalingFactors">Scaling factors for transition rates</param>
		/// <param name="baseLevels">Base levels for transition rates</param>
		/// <param name="survivalCurve">Array of Survival Curves for each basket name</param>
		///
		/// <returns>Transition rates</returns>
		///
		public static double [,]
		TransitionRatesFromCoefs( Dt settle,
															Dt [] maturities,
															int [] stateIndices,
															double [,] coefs,
															double [] scalingFactors,
															double [] baseLevels,
															SurvivalCurve survivalCurve )
		{
		  // Sanity check
		  if( stateIndices.Length != coefs.GetLength(0) )
				throw new ToolkitException( String.Format("Number of state indices {0} not match rows of coef {1}",
																									stateIndices.Length, coefs.GetLength(0)) );
		  //if( 0 != stateIndices[0] )
			//	throw new ToolkitException( String.Format("State indices start with nonzero values {0}",
			//                              stateIndices[0]) );

			// Save original scaling factors
			double [] origScalingFactors = scalingFactors;

			int nSegs = stateIndices.Length;
		  int nStates = scalingFactors.Length;
		  int nTenors = maturities.Length;
		  double [,] result = new double[ nStates, nTenors ];
			double [] tmp = new double[ nStates ];
			Dt start = settle;
			for( int j = 0; j < nTenors; ++j ) {
			  Dt stop = maturities[j];
				scalingFactors = transformScalingFactors( origScalingFactors,
																									survivalCurve,
																									settle, stop );
				double avgHazardRate = survivalCurve.HazardRate(start, stop);
				SetTransitionRates( tmp, 0, avgHazardRate, scalingFactors, baseLevels );
				for( int r = 0; r < nSegs; ++r )
				{
				  int stateIdx = stateIndices[ r ];
					double x = coefs[r,j] * avgHazardRate;
					SetTransitionRates( tmp, stateIdx, x, scalingFactors, baseLevels );
				}
				for( int i = 0; i < nStates; ++i )
					result[i,j] = tmp[i];
				start = stop;
			}
			return result;
		}


		//-
		// Helper class optimizers
		//-
		unsafe class Optimizer {
			public Optimizer( SyntheticCDOPricer [] pricers,
												double [] weights,
												int [] stateIndices )
			{
			  pricers_ = pricers;
				weights_ = weights;
				stateIndices_ = stateIndices;
				proportions_ = null;
				bases_ = null;
			  basket_ = (ForwardLossBasketPricer) pricers[0].Basket;
				return;
			}

			private double
			Evaluate( double* x )
			{
				basket_.Reset();

				int N = stateIndices_.Length;
				for( int i = 0; i < N; ++i ) {
				  basket_.SetTransitionRates( stateIndices_[i],
																			x[i],
																			proportions_,
																			bases_ );
				}

				double diff = 0.0;
				for( int i = 0; i < pricers_.Length; ++i )
				{
					// we use the infinite norm
					double pv = pricers_[i].ProductPv() / pricers_[i].Notional;
					//diff = Math.Max( Math.Abs( weights_[i] * pv ), diff );
					//diff += Math.Abs( weights_[i] * pv ) ;
					diff += weights_[i] * pv * pv ;
				}
				return diff;
			}

			internal double [] Fit( double [] initialX, 
															double [] deltaX,
															Dt startDate,
															Dt stopDate,
															double [] scalingFactors,
															double [] baseLevels )
			{
				proportions_ = scalingFactors;
				bases_ = baseLevels;

				basket_.SetRecalcPeriod( startDate, stopDate );

			  // Set up optimizer
			  int numFit = stateIndices_.Length;
				NelderMeadeSimplex opt = new NelderMeadeSimplex(numFit);
				opt.setInitialPoint(initialX);

				if( deltaX != null )
					opt.setInitialDeltaX(deltaX);

				double tolF_ = 1.0E-5 ;
				double tolX_ = 1.0E-3 ;
				opt.setToleranceF(tolF_);
				opt.setToleranceX(tolX_);

				opt.setMaxEvaluations(8000);
				opt.setMaxIterations(3200);

				fn_ = new Double_Vector_Fn( this.Evaluate );
				objFn_ = new DelegateOptimizerFn( numFit, fn_, null);

				double* x = null;
				int repeat = 2;
				do {
				  // Fit
				  opt.minimize(objFn_);

				  // Save results
					x = opt.getCurrentSolution();
					double f = Math.Sqrt( Evaluate( x ) );
					if( f < tolF_ )
						repeat = 1;
				} while( --repeat > 0 );

#if IncludeExtraDebug
				// Extra debug info
				if( logger.IsDebugEnabled )
				{
					basket_.Reset();

					int N = stateIndices_.Length;
					for( int i = 0; i < N; ++i ) {
				    basket_.SetTransitionRates( stateIndices_[i],
																				x[i],
																				proportions_,
																				bases_ );
					}

					logger.DebugFormat("Break Even Fee/Premium:");
					for( int i = 0; i < pricers_.Length; ++i )
					{
						SyntheticCDOPricer p = pricers_[i];
						double be = ( p.CDO.Attachment <= 1E-10 ? p.BreakEvenFee() : p.BreakEvenPremium() );
						logger.DebugFormat("  [{0}]: {1}", i, be);
					}
				}
#endif

				double[] result = new double[numFit];
				for( int i = 0; i < numFit; ++i )
					result[i] = x[i];

				fn_ = null;
				objFn_ = null;

				return result;
			}

			private ForwardLossBasketPricer basket_;
			private SyntheticCDOPricer [] pricers_;
			//private int stateIdx_;
			//private int cdoIdx_;
			private double [] proportions_;
			private double [] bases_;
			private double [] weights_;
			private int [] stateIndices_;

			private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
			private Double_Vector_Fn fn_;
		};

		#endregion // Optimal_Calibration


	} // class ForwardLossBasketPricer

}
