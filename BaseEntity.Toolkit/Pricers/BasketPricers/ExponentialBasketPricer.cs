/*
 * ExponentialBasketPricer.cs
 *
 */
//#define DARIUSZ_OLD_SPEC
//#define SECOND_LARGER_THAN_FIRST

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;
using System.Xml;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
	/// <summary>
	///   Pricing helper class for pricers based on multivariate exponential models
	/// </summary>
	///
	/// <remarks>
	///   This helper class sets up a basket and pre-calculates anything specific to the basket but
	///   independent of the product.
	/// </remarks>
	///
	public class ExponentialBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(ExponentialBasketPricer));

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
		/// <param name="tenorDates">Tenor dates</param>
		/// <param name="firstNumber">First number</param>
		/// <param name="skew">Skew parameter</param>
		/// <param name="secondNumber">Second number</param>
		/// <param name="correlation">Correlation parameter</param>
		/// <param name="fatalCoef">Fatal shock coefficient</param>
		/// <param name="scaling">Scaling factor</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		ExponentialBasketPricer( Dt asOf,
														 Dt settle,
														 Dt maturity,
														 SurvivalCurve [] survivalCurves,
														 RecoveryCurve[] recoveryCurves,
														 double [] principals,
														 Dt [] tenorDates,
														 double [] firstNumber,
														 double [] skew,
														 double [] secondNumber,
														 double [] correlation,
														 double [] fatalCoef,
														 double [] scaling,
														 int stepSize,
														 TimeUnit stepUnit,
														 Array lossLevels )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							new Copula(), new SingleFactorCorrelation( new string[]{"none"}, 0.0 ), stepSize, stepUnit, lossLevels )
		{
			logger.DebugFormat( "Creating Exponential Basket asof={0}, settle={1}, maturity={2}, principal={3}", asOf, settle, maturity, Principal );

      // Do not add complements to loss levels
      this.LossLevelAddComplement = false;

			// Check all notionals are the same
			for( int i = 1; i < principals.Length; i++ )
				if( principals[i] != principals[0] )
					throw new ToolkitException( String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]) );

			tenorDates_ = tenorDates;
			firstNumber_ = firstNumber;
			skew_ = skew;
			secondNumber_ = secondNumber;
			correlation_ = correlation;
			fatalCoef_ = fatalCoef;
			scaling_ = scaling;

			int nBasket = this.Count;
			log1mHs_ = new double[ nBasket ];
			log1mGs_ = new double[ nBasket ];

			systemCurves_ = null;
			idiosynCurves_ = null;
			usePrecomputedCurves_ = false;
			calculateSysCurves_ = false;

			sortedSurvivalCurves_ = null;
			sortedIndices_ = null;

			logger.Debug( "Exponential Basket created" );
		}

    /// <summary>
		///   Construct a copy of the basket without cloning the underlying curves
		/// </summary>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public ExponentialBasketPricer Copy()
		{
		  ExponentialBasketPricer bp
				= new ExponentialBasketPricer( this.AsOf,
																			 this.Settle,
																			 this.Maturity,
																			 this.SurvivalCurves,
																			 this.RecoveryCurves,
																			 this.Principals,
																			 this.tenorDates_,
																			 this.firstNumber_,
																			 this.skew_,
																			 this.secondNumber_,
																			 this.correlation_,
																			 this.fatalCoef_,
																			 this.scaling_,
																			 this.StepSize,
																			 this.StepUnit,
																			 this.CookedLossLevels.ToArray() );
			return bp;
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			ExponentialBasketPricer obj = (ExponentialBasketPricer)base.Clone();

			obj.tenorDates_ = Dt.CloneArray( tenorDates_ );
      obj.firstNumber_ = CloneUtil.Clone(firstNumber_);
      obj.skew_ = CloneUtil.Clone(skew_);
      obj.secondNumber_ = CloneUtil.Clone(secondNumber_);
      obj.correlation_ = CloneUtil.Clone(correlation_);
      obj.fatalCoef_ = CloneUtil.Clone(fatalCoef_);
      obj.scaling_ = CloneUtil.Clone(scaling_);
			obj.distribution_ = distribution_.clone();
			
			obj.systemCurves_ = CloneArray( systemCurves_ );
			obj.idiosynCurves_ = CloneArray( idiosynCurves_ );
      obj.log1mHs_ = CloneUtil.Clone(log1mHs_);
      obj.log1mGs_ = CloneUtil.Clone(log1mGs_);

			obj.sortedSurvivalCurves_ = null;
			obj.sortedIndices_ = null;

			return obj;
		}

		private static SurvivalCurve [] CloneArray( SurvivalCurve [] a )
		{
		  if( null == a )
				return null;
		  int N = a.Length;
			SurvivalCurve [] b = new SurvivalCurve[ N ]; 
			for( int i = 0; i < N; ++i )
				b[i] = (SurvivalCurve)a[i].Clone();
			return b;
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
		  double lossRate = (1 - RecoveryRate);
			double [] levels = new double [lossLevels.Length];
			for (int i = 0; i < lossLevels.Length; ++i) {
			  // By its nature the distribution is disrete. To avoid unexpected
			  // results, we round numbers to nearest effective decimal points,
			  // to make sure, for example,  2.0 does not become somthing like
			  // 1.999999999999954
			  decimal x = (decimal) ( lossLevels[i] / lossRate );
				levels[i] = (double) Math.Round(x, EffectiveDigits);
				if (levels[i] > 1.0)
					levels[i] = 1.0;
			}
			levels = SetLossLevels(levels, false).ToArray();

			// initialize the distribution surface
			Curve2D distribution = initDistribution( wantProbability,
																							 new Dt[]{this.Settle, date},
																							 levels );

			// initialize the storage intermediate results
			int nBasket = this.Count;
			for( int i = 0; i < nBasket; ++i )
				log1mHs_[i] = log1mGs_[i] = 0.0;

			// initialize the systematic and idiosyncratic curves
			if( calculateSysCurves_ && !usePrecomputedCurves_ )
			{
			  Dt asOf = this.Settle;
			  // Dt settle = this.Settle;
				systemCurves_ = new SurvivalCurve[ nBasket ];
				idiosynCurves_ = new SurvivalCurve[ nBasket ];
				for( int i = 0; i < nBasket; ++i )
				{
					SurvivalCurve sysCurve = new SurvivalCurve( asOf );
					systemCurves_[i] = sysCurve;
					SurvivalCurve idioCurve = new SurvivalCurve( asOf );
					idiosynCurves_[i] = idioCurve;
				}
			}

			// Compute the distribution
			ComputeAndSaveDistribution( wantProbability,
																	this.Settle, date,
																	distribution );

			// Get the results
			double principal = 1.0 / Count;
			int N = distribution.NumLevels();
			double [,] results = new double[ N, 2 ];
			for( int i = 0; i < N; ++i ) {
			  double level = distribution.GetLevel(i);
				results[i,0] = level * lossRate;
				results[i,1] = distribution.Interpolate( date, level );
				if( false == wantProbability )
					results[i,1] *= lossRate * principal;
			}

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
		  if( !distributionComputed_ )
				  ComputeAndSaveDistribution();

			double lossRate = 1 - RecoveryRate;
			if( lossRate < 1.0E-12 )
				return 0.0;
			trancheBegin /= lossRate;
			if (trancheBegin > 1.0) trancheBegin = 1.0;
			trancheEnd /= lossRate;
			if (trancheEnd > 1.0) trancheEnd = 1.0;
			double defaults = distribution_.Interpolate( date,
																									 trancheBegin,
																									 trancheEnd );
			//double loss = defaults * lossRate * Principal;
			double loss = defaults * lossRate / Count;
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
		AmortizedAmount( Dt date,
										 double trancheBegin,
										 double trancheEnd )
		{
		  if( 0 == RecoveryRate )
				return 0.0;

			if( !distributionComputed_ )
				  ComputeAndSaveDistribution();

			if( RecoveryRate < 1.0E-12 )
				return 0.0;
			double multiplier = 1.0 / RecoveryRate;
			double tBegin = (1-trancheEnd) * multiplier ;
			if (tBegin > 1.0) tBegin = 1.0;
			double tEnd = (1-trancheBegin) * multiplier ;
			if (tEnd > 1.0) tEnd = 1.0;
			double defaults = distribution_.Interpolate( date,
																									 tBegin,
																									 tEnd );
			//double amortized = defaults * RecoveryRate * Principal;
			double amortized = defaults * RecoveryRate / Count ;
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
			sortedSurvivalCurves_ = null;
			sortedIndices_ = null;
		}

		/// <summary>
		///   Calculate the system and idiosyncratic curves
		/// </summary>
		/// <remarks>
		///   If the basket is set up to use some predefined system and idiosyncratic curves,
		///   nothing is done.  Otherwise, calculate the curves.
		/// </remarks>
		public void CalculateIntermediateCurves()
		{
		  if( !usePrecomputedCurves_ && !(calculateSysCurves_ && distributionComputed_) )
			{
				bool savedCalculateSysCurves = calculateSysCurves_;
				try {
				  calculateSysCurves_ = true;
					ComputeAndSaveDistribution();
				}
				finally {
				  calculateSysCurves_ = savedCalculateSysCurves;
				}
			}
		}

		/// <summary>
		///   Calculate the system and idiosyncratic curves
		/// </summary>
		///
		/// <param name="systemCurves">Survival Curves representing the systematic shocks</param>
		/// <param name="idiosynCurves">Survival Curves representing the idiosyncratic shocks</param>
		///
		public void UsePrecomputedCurves( SurvivalCurve [] systemCurves,
																			SurvivalCurve [] idiosynCurves )
		{
		  int nBasket = this.Count;
			SortSurvivalCurves( systemCurves );
			systemCurves_ = Mapping( systemCurves, sortedIndices_, true );
			idiosynCurves_ = Mapping( idiosynCurves, sortedIndices_, true );
			usePrecomputedCurves_ = true;
			calculateSysCurves_ = false;
		}
		#endregion // Methods

		#region Sensitivities

		private const double tolerance = 1.0E-15;

    /// <summary>
		///   Bump curves
		/// </summary>
		static private void BumpSpreads( Curve [] curves,
																		 double [] bumps,
																		 int[] mapping,
																		 bool add )
		{
		  if( null == bumps || bumps.Length == 0 )
				return;

			int N = curves.Length;
			if( add ) {
				for( int i = 0; i < N; ++i )
				{
					if( null != curves[i] )
					  curves[i].Spread += bumps[ mapping[i] ];
				}
			} else {
				for( int i = 0; i < N; ++i )
				{
					if( null != curves[i] )
					  curves[i].Spread -= bumps[ mapping[i] ];
				}
			}
			return;
		}

    /// <summary>
		///   Save spreads
		/// </summary>
		static private double [] SaveSpreads( Curve [] curves )
		{
		  if( null == curves || curves.Length == 0 )
				return null;

			int N = curves.Length;
			double [] result = new double[ N ];
			for( int i = 0; i < N; ++i )
				result[i] = curves[i].Spread;

			return result;
		}


    /// <summary>
		///   Save spreads
		/// </summary>
		static private void RestoreSpreads( Curve [] curves,
																				double [] spreads )
		{
		  if( null == curves || curves.Length == 0 )
				return ;

			int N = curves.Length;
			for( int i = 0; i < N; ++i )
				curves[i].Spread = spreads[i];

			return;
		}

    /// <summary>
		///   Bump curves
		/// </summary>
		static private void BumpCurves( Curve [] curves,
																		double [] bumps,
																		int[] mapping,
																		bool add )
		{
		  if( null == bumps || bumps.Length == 0 )
				return;

			int N = curves.Length;
			if( add ) {
				for( int i = 0; i < N; ++i )
				{
					if( null != curves[i] )
					  curves[i].Spread += bumps[ mapping[i] ] / 10000;
				}
			} else {
				for( int i = 0; i < N; ++i )
				{
					if( null != curves[i] )
					  curves[i].Spread -= bumps[ mapping[i] ] / 10000;
				}
			}
			return;
		}


		/// <summary>
		///   Fast calculation of the MTM values for a series of Synthetic CDO tranches
		/// </summary>
 		///
		/// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
		/// <param name="sysBumps">An array of bump sizes to systematic spreads</param>
		/// <param name="idioBumps">An array of bump sizes to idiosyncratic spreads</param>
		///
		/// <remarks>
		///   <para>Each of the systematic and idiosyncratic spreads are bumped by the
		///   correponding bump sizes.</para>
		/// </remarks>
		///
		/// <returns>
		///    A table of MTM values represented by a two-dimensional array.
		///    Each column identifies a CDO tranche, while row 0 contains the base values
		///    and row 1 contains the values when all the spreads are bumped.
		/// </returns>
		///
		public static double [,]
		BumpedPvsAtOnce( SyntheticCDOPricer [] pricers,
										 double [] sysBumps,
										 double [] idioBumps )
		{
		  double [,] result = null;
		  if( null == pricers || pricers.Length == 0 )
				return result;

			// Check if all the pricers use the same basket
			int nCDOs = pricers.Length;
			ExponentialBasketPricer basket = (ExponentialBasketPricer) pricers[0].Basket;
			for( int i = 1; i < nCDOs; ++i )
			{
				if( (BasketPricer)basket != pricers[i].Basket )
					throw new ToolkitException( "The pricers are not using the same bakset" );
			}

			// Save states
			double [] savedSystemSpreads = SaveSpreads( basket.systemCurves_ );
			double [] savedIdiosynSpreads = SaveSpreads( basket.idiosynCurves_ );
			bool savedCalculateSysCurves = basket.calculateSysCurves_;
			bool savedUsePrecomputedCurves = basket.usePrecomputedCurves_;

			try {
				// Calculate the base values
				basket.calculateSysCurves_ = true;
				result = new double[ 2, nCDOs ];
				for( int j = 0; j < nCDOs; ++j )
					result[0,j] = pricers[j].ProductPv();
				basket.calculateSysCurves_ = false;

				// Bump the systematic and idiosyncratic curves
				SurvivalCurve [] systemCurves = basket.systemCurves_;
				SurvivalCurve [] idiosynCurves = basket.idiosynCurves_;
				int [] map = basket.sortedIndices_;
				BumpSpreads( systemCurves, sysBumps, map, true );
				BumpSpreads( idiosynCurves, idioBumps, map, true );

				// Calculate the bumped PVs
				basket.ShallowReset();
				basket.usePrecomputedCurves_ = true;
				for( int j = 0; j < nCDOs; ++j )
					result[1,j] = pricers[j].ProductPv();
				basket.ShallowReset();
			}
			finally {
				// Clear the curves
			  RestoreSpreads( basket.systemCurves_, savedSystemSpreads );
			  RestoreSpreads( basket.idiosynCurves_, savedIdiosynSpreads );
				basket.usePrecomputedCurves_ = savedUsePrecomputedCurves;
				basket.calculateSysCurves_ = savedCalculateSysCurves;
				basket.ShallowReset();
			}

			return result;
		}

		/// <summary>
		///   Fast calculation of the MTM values for a series of Synthetic CDO tranches
		/// </summary>
 		///
		/// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
		/// <param name="sysBumps">An array of bump sizes to systematic spreads</param>
		/// <param name="idioBumps">An array of bump sizes to idiosyncratic spreads</param>
		///
		/// <remarks>
		///   <para>Each of the systematic and idiosyncratic spreads is bumped by the
    ///   corresponding bump sizes.
		///   Recalculation is avoided if both bump sizes are zero.</para>
		/// </remarks>
		///
		/// <returns>
		///    A table of MTM values represented by a two-dimensional array.
		///    Each column indentifies a CDO tranche, while row 0 contains the base values
		///    and row i (i &gt; 0) contains the values when the spreads of name i are bumped
    ///    by the corresponding bump sizes.
		/// </returns>
		///
		public static double [,]
		BumpedPvsByName( SyntheticCDOPricer [] pricers,
										 double [] sysBumps,
										 double [] idioBumps )
		{
		  double [,] table = null;
		  if( null == pricers || pricers.Length == 0 )
				return null;

			// Check if all the pricers use the same basket
			int nCDOs = pricers.Length;
			ExponentialBasketPricer basket = (ExponentialBasketPricer) pricers[0].Basket;
			for( int j = 1; j < nCDOs; ++j )
			{
				if( (BasketPricer)basket != pricers[j].Basket )
					throw new ToolkitException( "The pricers are not using the same bakset" );
			}

			// Save options
			bool savedCalculateSysCurves = basket.calculateSysCurves_;
			bool savedUsePrecomputedCurves = basket.usePrecomputedCurves_;

			try {
			  // now create and fill the table of values
			  int basketSize = basket.Count;
				table = new double [basketSize + 1, pricers.Length];

				// Calculate the base values
				basket.calculateSysCurves_ = true;
				for( int j = 0; j < nCDOs; ++j )
					table[0,j] = pricers[j].ProductPv();
				basket.calculateSysCurves_ = false;

				int [] mapping = basket.sortedIndices_;
				SurvivalCurve [] systemCurves = basket.systemCurves_;
				SurvivalCurve [] idiosynCurves = basket.idiosynCurves_;
				// Calculate the bumped values by names
				for( int ii = 0; ii < basketSize; ++ii )
				{
				  int i = mapping[ ii ];
					double sysBump = 0, idioBump = 0;
					if( null != sysBumps && 0 != sysBumps.Length )
						sysBump = sysBumps[i] / 10000;
					if( null != idioBumps && 0 != idioBumps.Length )
						idioBump = idioBumps[i] / 10000;
					if( sysBump < tolerance && idioBump < tolerance )
					{
						for( int j = 0; j < nCDOs; ++j )
							table[i+1,j] = table[0,j];
					}
					else
					{
					  double savedSysSpread = systemCurves[ii].Spread;
					  double savedIdioSpread =  idiosynCurves[ii].Spread;
						try{
					    systemCurves[ii].Spread += sysBump;
						  idiosynCurves[ii].Spread += idioBump;
						  basket.ShallowReset();
							basket.usePrecomputedCurves_ = true;
							for( int j = 0; j < nCDOs; ++j )
								table[i + 1,j] = pricers[j].ProductPv();
							basket.usePrecomputedCurves_ = false;
						}
						finally {
						  idiosynCurves[ii].Spread = savedIdioSpread;
							systemCurves[ii].Spread = savedSysSpread;
						}
					}
				} // next name
			}
			finally {
			  basket.usePrecomputedCurves_ = savedUsePrecomputedCurves;
				basket.calculateSysCurves_ = savedCalculateSysCurves;
				basket.ShallowReset();
			}

			return table;
		}

		#endregion // Sensitivities

		#region Properties

    /// <summary>
		///   Recovery rate
		/// </summary>
    public double RecoveryRate
		{
			get {
				// use the average recovery rate
				double sum = 0;
				double [] recoveryRates = RecoveryRates;
				for (int i = 0; i < recoveryRates.Length; ++i)
					sum += recoveryRates[i];
				return ( sum / recoveryRates.Length );
			}
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
		///   Get distribution for basket
		/// </summary>
		public Curve2D Distribution
		{
			get { return distribution_; }
		}

		/// <summary>
		///   Get the systematic shocks
		/// </summary>
		public SurvivalCurve [] SystematicCurves
		{
			get { return Mapping(systemCurves_,sortedIndices_,false); }
		}

		/// <summary>
		///   Get the idiosyncratic shocks
		/// </summary>
		public SurvivalCurve [] IdiosyncraticCurves
		{
			get { return Mapping(idiosynCurves_,sortedIndices_,false); }
		}

		#endregion // Properties

		#region Data

		//private Dt calcStartDate_;
		//private Dt calcStopDate_;
		private Dt [] tenorDates_;
		private double [] firstNumber_;
		private double [] skew_;
		private double [] secondNumber_;
		private double [] correlation_;
		private double [] fatalCoef_;
		private double [] scaling_;
		private double [] log1mHs_;     // size: N;   definition: sum_{i+1}^N -log(1-Hi)
		private double [] log1mGs_;     // size: N;   definition: -log(1-Gi)

		private SurvivalCurve [] systemCurves_;  // size: N; survival curves representing systematic shocks
		private SurvivalCurve [] idiosynCurves_; // size: N; survival curves representing idiosyncratic shocks

		private bool distributionComputed_;
		private bool usePrecomputedCurves_;
		private bool calculateSysCurves_;

		private SurvivalCurve [] sortedSurvivalCurves_;
		private int [] sortedIndices_;

		private Curve2D distribution_;

		#endregion // Data
		

		#region Implementation
		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		private double[]
		CalcLossLevels()
		{
		  double recoveryRate = this.RecoveryRate;
		  double factorLoss = (recoveryRate > (1 - 1.0E-12) ? 0.0 : 1 / (1 - recoveryRate) );
		  double factorAmor = (recoveryRate < 1.0E-12 ? 0.0 : 1 / recoveryRate) ;
			int N = CookedLossLevels.Count;
			double [] lossLevels = new double [2*N];
			for (int i = 0; i < N; ++i) {
			  lossLevels[i] = CookedLossLevels[i] * factorLoss;
				if (lossLevels[i] > 1.0)
					lossLevels[i] = 1.0;
			  lossLevels[i+N] = (1- CookedLossLevels[i]) * factorAmor;
				if (lossLevels[i+N] > 1.0)
					lossLevels[i+N] = 1.0;
			}
			lossLevels = SetLossLevels( lossLevels, false ).ToArray();
			return lossLevels;
		}

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		private void	
		ComputeAndSaveDistribution()
		{
		  initialize();

			ComputeAndSaveDistribution( false,
																	this.Settle, this.Maturity,
																	distribution_ );

			distributionComputed_ = true;
		}

		/// <summary>
		///   Combine two arraya of dates, sort and remove repeated values
		/// </summary>
		private static Dt []
		CombineAndSortDt( Array x0, Array x1 )
		{
		  ArrayList list = new ArrayList();

			Array x = x0;
			while( null != x )
			{
				foreach (object xi in x)
				{
					int dp = ((Dt)xi).ToInt();
					int pos = list.BinarySearch(dp);
					if (pos < 0)
						list.Insert(~pos, dp);
				}
				x = (x == x0 ? x1 : null);
			}

			Dt [] y = new Dt[ list.Count ];

			for (int i = 0; i < list.Count; ++i)
				y[i] = new Dt( (int)list[i] );

			return y;
		}

		/// <summary>
		///   Initialize the distribution surface
		/// </summary>
		private static Curve2D
		initDistribution( bool wantProbability,
											Dt asOf,
											Dt [] dates,
											double [] lossLevels )
		{
			int nDates = dates.Length;
			int nLevels = lossLevels.Length;
			Curve2D distribution = new Curve2D();
      distribution.Initialize(nDates, nLevels, 1);
			distribution.SetAsOf( asOf );
			for( int i = 0; i < nDates; ++i )
				distribution.SetDate( i, dates[i] );
      for( int i = 0; i < nLevels; ++i )
			{
        distribution.SetLevel( i, lossLevels[i] );
				distribution.SetValue( i, 0.0 );
			}
			if( wantProbability ) {
			  // start with the probability of no default equals to 1
				distribution.SetValue( 0, 1.0 );
			}
			return distribution;
		}

		/// <summary>
		///   Initialize the distribution surface
		/// </summary>
		private Curve2D
		initDistribution( bool wantProbability,
											Dt [] dates,
											double [] lossLevels )
		{
		  dates = CombineAndSortDt( dates, tenorDates_ );
			Curve2D distribution = initDistribution( wantProbability,
																							 this.Settle,
																							 dates,
																							 lossLevels );
			return distribution;
		}

		/// <summary>
		///   Initialize the distribution surface
		/// </summary>
		private Curve2D
		initDistribution( bool wantProbability,
											double [] lossLevels )
		{
		  Dt [] dates = createDateArray();
			Curve2D distribution = initDistribution( wantProbability,
																							 dates,
																							 lossLevels );
			return distribution;
		}

		/// <summary>
		///   Initialize the calculation
		/// </summary>
		private void initialize()
		{
		  // determine the loss levels
			double[] lossLevels = CalcLossLevels();

			// initialize the distribution surface
			distribution_ = initDistribution( false, lossLevels );

			// initialize the storage intermediate results
			int nBasket = this.Count;
			for( int i = 0; i < nBasket; ++i )
				log1mHs_[i] = log1mGs_[i] = 0.0;

			// initialize the systematic and idiosyncratic curves
			if( calculateSysCurves_ && !usePrecomputedCurves_ )
			{
			  Dt asOf = this.Settle;
			  // Dt settle = this.Settle;
				systemCurves_ = new SurvivalCurve[ nBasket ];
				idiosynCurves_ = new SurvivalCurve[ nBasket ];
				for( int i = 0; i < nBasket; ++i )
				{
					SurvivalCurve sysCurve = new SurvivalCurve( asOf );
					systemCurves_[i] = sysCurve;
					SurvivalCurve idioCurve = new SurvivalCurve( asOf );
					idiosynCurves_[i] = idioCurve;
				}
			}

			return;
		}


		/// <summary>
		///   Create an array of dates as time grid
		/// </summary>
		private Dt[] createDateArray()
		{
		  int nDates = 1;
			Dt maturity = Maturity;
      Dt current = Settle;
      while( Dt.Cmp(current, maturity) < 0 )
      {
        current = Dt.Add(current, StepSize, StepUnit);
        if( Dt.Cmp(current, maturity) > 0 )
          current = maturity;
        ++nDates;
      }
			Dt [] dates = new Dt[ nDates ];
      current = Settle;
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

		/// <summary>
		///   Compute the whole distribution
		/// </summary>
		/// <remarks>
		///   The distribution is calculated using a single set of parameters
		///   identified by <c>tenorIndex</c>.
		/// </remarks>
		private void	
		ComputeAndSaveDistribution( bool wantProbability,
																Dt start, Dt stop, int tenorIndex,
																Curve2D distributions )
		{
			if( tenorIndex < 0 || tenorIndex >= tenorDates_.Length )
				throw new ArgumentOutOfRangeException("tenorIndex");

			if( null == sortedSurvivalCurves_ )
				SortSurvivalCurves();

			if( usePrecomputedCurves_ )
			{
				ComputeDistributions( wantProbability,
															start,
															stop,
															idiosynCurves_ ,
															systemCurves_,
															distributions
															);
			}
			else
			{
				ComputeDistributions( wantProbability,
															start,
															stop,
															this.firstNumber_[tenorIndex],
															this.skew_[tenorIndex],
															this.secondNumber_[tenorIndex],
															this.correlation_[tenorIndex],
															this.fatalCoef_[tenorIndex],
															this.scaling_[tenorIndex],
															sortedSurvivalCurves_,
															distributions,
															log1mHs_,
															log1mGs_,
															calculateSysCurves_ ? idiosynCurves_ : null,
															calculateSysCurves_ ? systemCurves_ : null
															);
			}

			distributionComputed_ = true;

			return;
		}

		/// <summary>
		///   Compute the whole distribution
		/// </summary>
		/// <remarks>
		///   The distribution is calculated using a single set of parameters
		///   identified by <c>tenorIndex</c>.
		/// </remarks>
		private void	
		ComputeAndSaveDistribution( Dt start, Dt stop, int tenorIndex )
		{
			ComputeAndSaveDistribution( false, start, stop, tenorIndex, distribution_ );
			return;
		}

		/// <summary>
		///   Compute the whole distribution from begin to end
		/// </summary>
		/// <remarks>
		///   The distribution is calculated using different parameters
		///   for different tenors.
		/// </remarks>
		private void	
		ComputeAndSaveDistribution( bool wantProbability,
																Dt begin, Dt end,
																Curve2D distributions )
		{
		  // find the tenor period.
		  // we make not assumption about the relationship between 
		  // the basket [Settle, Marurity] and the tenors. 
		  Dt start = begin;
			int lastTenorIndex = tenorDates_.Length - 1;
			int tenorIndex = lastTenorIndex;
			for( int i = 0; i < lastTenorIndex; ++i )
			{
				if( Dt.Cmp( start, tenorDates_[i] ) < 0 )
				{
					tenorIndex = i;
					break;
				}
			}
			if( tenorIndex < 0 )
				throw new ArgumentException("Invalid tenor array");

			if( Dt.Cmp( end, this.Maturity ) > 0 )
				throw new ArgumentOutOfRangeException("end");

			for( ; tenorIndex < lastTenorIndex; ++tenorIndex )
			{
				Dt stop = tenorDates_[ tenorIndex ];
				if( Dt.Cmp( stop, end ) > 0 )
					stop = end;
				ComputeAndSaveDistribution( wantProbability,
																		start, stop, tenorIndex,
																		distributions );
				start = stop;
			}
			if( Dt.Cmp( end, start ) > 0 )
				ComputeAndSaveDistribution( wantProbability,
																		start, end, tenorIndex,
																		distributions );

			distributionComputed_ = true;

			return;
		}

		/// <summary>
		///   Sort survival curves by the hazard rates over the whole horizon from settle to maturity
		/// </summary>
		private void SortSurvivalCurves( SurvivalCurve [] curves )
		{
		  sortedIndices_ = SortByHazardRate( curves, this.Settle, this.Maturity );
			sortedSurvivalCurves_ = Mapping( curves, sortedIndices_, true );
			return;
		}

		/// <summary>
		///   Sort survival curves by the hazard rates over the whole horizon from settle to maturity
		/// </summary>
		private void SortSurvivalCurves()
		{
		  SortSurvivalCurves( this.SurvivalCurves );
		}

		/// <summary>
		///   Duplicate an array with index mapping
		/// </summary>
		/// <remarks>
		///   The parameter <c>map</c> contains an one to one mapping of indices.
		///   If inverse is false, element <c>i</c> in the original array 
		///   is mapped to the element <c>map[i]</c> in the new array;
		///   Otherwise, element <c>map[i]</c> in the original array 
		///   is mapped to the element <c>i</c> in the new array.
		/// </remarks>
		private static SurvivalCurve [] Mapping( SurvivalCurve [] curves,
																						 int[] map,
																						 bool inverse )
		{
		  int N = curves.Length;
			SurvivalCurve [] result = new SurvivalCurve[ N ];
			if( inverse )
			{
				for( int i = 0; i < N; ++i )
					result[i] = curves[ map[i] ];
			}
			else
			{
				for( int i = 0; i < N; ++i )
					result[ map[i] ] = curves[ i ];
			}
			return result;
		}

		///
		/// <summary>
		///   Reset the pricer such that in the next request for AccumulatedLoss()
		///   or AmortizedAmount(), it recompute the distribution
		/// </summary>
		/// <remarks>
		///   This function does not reset the sorting results
		/// </remarks>
		private void ShallowReset()
		{
		  distributionComputed_ = false;
		}

		#endregion // Implementation


		#region Calibration
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
		/// <param name="weights">Array of weights</param>
		/// <param name="initialX">Starting X points</param>
		/// <param name="deltaX">Searching steps</param>
		/// <param name="upperX">Upper bounds</param>
		/// <param name="lowerX">Lower bounds</param>
		///
		/// <returns>Transition coefficients</returns>
		///
    public static double [,]
		ImpliedParams(
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
									double [] weights,
									double [] initialX,
									double [] deltaX,
									double [] upperX,
									double [] lowerX )
		{
			// important counts
			int nCDOs = premia.GetLength(0);
			int nTenors = premia.GetLength(1);
			int nBasketSize = survivalCurves.Length;

			// create paramters array
			double [] firstNumber = new double[ nTenors ];
			double [] skew = new double[ nTenors ];
			double [] secondNumber = new double[ nTenors ];
			double [] correlation = new double[ nTenors ];
			double [] fatalCoef = new double[ nTenors ];
			double [] scaling = new double[ nTenors ];
			for( int j = 0; j < nTenors; ++j )
			{
				firstNumber[j]  = initialX[0];
				skew[j]         = initialX[1];
				secondNumber[j] = initialX[2];
				correlation[j]  = initialX[3];
				fatalCoef[j]    = initialX[4];
				scaling[j]      = initialX[5];
			}
			initialX[0] /= nBasketSize;
			initialX[2] /= nBasketSize;
		#if SECOND_LARGER_THAN_FIRST
			initialX[2] -= initialX[0];
		#endif
			if( null != upperX ) {
			  upperX[0] /= nBasketSize;
			  upperX[2] /= nBasketSize;
			}
			if( null != lowerX ) {
			  lowerX[0] /= nBasketSize;
			  lowerX[2] /= nBasketSize;
			}

			// create loss levels
			double [,] lossLevels = new double[ nCDOs, 2 ];
			for( int i = 0; i < nCDOs; ++i )
			{
				lossLevels[i,0] = tranches[i].Attachment;
				lossLevels[i,1] = tranches[i].Detachment;
			}

			// Create basket pricer
			Dt maturity = maturityDates[ nTenors - 1 ];
			ExponentialBasketPricer basket = new ExponentialBasketPricer( asOfDate,
																																		settleDate,
																																		maturity,
																																		survivalCurves,
																																		recoveryCurves,
																																		principals,
																																		maturityDates,
																																		firstNumber,
																																		skew,
																																		secondNumber,
																																		correlation,
																																		fatalCoef,
																																		scaling,
																																		stepSize,
																																		stepUnit,
																																		lossLevels );
			basket.initialize();
			double [] log1mHs = new double[ nBasketSize ];
			double [] log1mGs = new double[ nBasketSize ];

			// Create pricers
			double [,] result = new double[ nCDOs, nTenors ];

			// creates pricers
			SyntheticCDOPricer [] pricers = new SyntheticCDOPricer[ nCDOs ];
			for( int i = 0; i < nCDOs; ++i )
			{
				SyntheticCDO cdo = (SyntheticCDO) tranches[i].Clone();
				cdo.Maturity = maturity;
				pricers[i] = new SyntheticCDOPricer(cdo, basket, discountCurve, 1.0, null);
			}

			// Compute the implied transitions
			Dt prevDate = basket.Settle;
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

				double [] tmp = Calibrate( prevDate, maturity, j,
																	 pricers,
																	 weights,
																	 initialX, 
																	 deltaX,
																	 upperX,
																	 lowerX,
																	 log1mHs, log1mGs );
				if( j != nTenors - 1)
				{
				  // update start X
					for( int i = 0; i < initialX.Length; ++i )
						initialX[i] = tmp[i];
				}

				tmp[0] *= nBasketSize;
				tmp[2] *= nBasketSize;
		#if SECOND_LARGER_THAN_FIRST
				tmp[2] += tmp[0];
		#endif
				for( int i = 0; i < initialX.Length; ++i )
				{
				  result[i,j] = tmp[i];
				}

				prevDate = maturity;
			}

			return result;
		}


		internal void SetParams( int idxStart,
														 double firstNumber,
														 double skew,
														 double secondNumber,
														 double correlation,
														 double fatalCoef,
														 double scaling )
		{
		#if SECOND_LARGER_THAN_FIRST
		  secondNumber += firstNumber;
		#endif
		  int nTenors = tenorDates_.Length;
		  for( int idx = idxStart; idx < nTenors; ++idx )
			{
				firstNumber_[idx]  = firstNumber;
				skew_[idx]         = skew;
				secondNumber_[idx] = secondNumber;
				correlation_[idx]  = correlation;
				fatalCoef_[idx]    = fatalCoef;
				scaling_[idx]      = scaling;
			}
		}

		private void SetHazards( double [] log1mHs,
								 double [] log1mGs )
		{
			int nBasket = this.Count;
			for( int i = 0; i < nBasket; ++i )
			{
				log1mHs_[i] = log1mHs[i];
				log1mGs_[i] = log1mGs[i];
			}

			return;
		}

		private void GetHazards( double [] log1mHs,
												double [] log1mGs )
		{
			int nBasket = this.Count;
			for( int i = 0; i < nBasket; ++i )
			{
				log1mHs[i] = log1mHs_[i];
				log1mGs[i] = log1mGs_[i];
			}

			return;
		}

		unsafe class Calibrator {
			public Calibrator( Dt start, Dt stop, int tenorIndex,
												 SyntheticCDOPricer [] pricers,
												 double [] weights,
												 double [] log1mHs, double [] log1mGs )
			{
		    basket_ = (ExponentialBasketPricer) pricers[0].Basket;
				pricers_ = pricers;
				weights_ = weights;
				start_ = start;
				stop_ = stop;
				tenorIndex_ = tenorIndex;
				log1mHs_ = log1mHs;
				log1mGs_ = log1mGs;
			}

			private double
			Evaluate( double* x )
			{
			  // should change
			  int idx = tenorIndex_;

		    int nBasketSize = basket_.Count;
				basket_.SetParams( idx, x[0]*nBasketSize, x[1], x[2]*nBasketSize, x[3], x[4], x[5] );
				basket_.SetHazards( log1mHs_, log1mGs_ );
				basket_.ComputeAndSaveDistribution( start_, stop_, idx );

				double diff = 0;
				for( int i = 0; i < pricers_.Length; ++i )
				{
					// we use the infinite norm
					double pv = pricers_[i].ProductPv() ;
					//diff = Math.Max( Math.Abs( weights_[i] * pv ), diff );
					//diff += Math.Abs( weights_[i] * pv ) ;
					diff += weights_[i] * pv * pv ;
				}
				return diff;
			}

			internal double [] Fit( double [] initialX, 
															double [] deltaX,
															double [] upperX,
															double [] lowerX )
			{
			  // Set up optimizer
			  const int numFit = 6;
				NelderMeadeSimplex opt = new NelderMeadeSimplex(numFit);
				opt.setInitialPoint(initialX);

				if( deltaX != null )
					opt.setInitialDeltaX(deltaX);

				if( lowerX == null )
					lowerX = new double[]{ 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
				opt.setLowerBounds(lowerX);

				if( upperX == null )
					upperX = new double[]{ 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 };
				opt.setUpperBounds(upperX);

				double tolF_ = 1.0E-6;
				double tolX_ = 1.0E-6;
				opt.setToleranceF(tolF_);
				opt.setToleranceX(tolX_);

				opt.setMaxEvaluations(8000);
				opt.setMaxIterations(3200);

				fn_ = new Double_Vector_Fn( this.Evaluate );
				objFn_ = new DelegateOptimizerFn( numFit, fn_, null);

				double* x = null;
				int repeat = 4;
				do {
				  // Fit
				  opt.minimize(objFn_);

				  // Save results
					x = opt.getCurrentSolution();
					double f = Math.Sqrt( Evaluate( x ) ) / basket_.TotalPrincipal;
					if( f < tolF_ )
						repeat = 1;
				} while( --repeat > 0 );
				double[] result = new double[]{ x[0], x[1], x[2], x[3], x[4], x[5] };
				basket_.GetHazards( log1mHs_, log1mGs_ );

				fn_ = null;
				objFn_ = null;

				return result;
			}

			private ExponentialBasketPricer basket_;
			private SyntheticCDOPricer [] pricers_;
			private double [] weights_;
			private Dt start_, stop_;
			private int tenorIndex_;
			private double [] log1mHs_;
			private double [] log1mGs_;

			private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
			private Double_Vector_Fn fn_;
		};

		///
		/// <summary>
		///   Calibrate the parameters
		/// </summary>
		///
		/// <param name="start">Start index</param>
		/// <param name="stop">Stop index</param>
		/// <param name="tenorIndex">Tenor to calibrate</param>
		/// <param name="pricers">Array of tranche pricers</param>
		/// <param name="weights">Array of weights</param>
		/// <param name="initialX">Starting X points</param>
		/// <param name="deltaX">Searching steps</param>
		/// <param name="upperX">Upper bounds</param>
		/// <param name="lowerX">Lower bounds</param>
		/// <param name="log1mHs">Idiosyncratic hazards</param>
		/// <param name="log1mGs">Systematic hazards</param>
		/// 
		private static double []
		Calibrate( Dt start, Dt stop, int tenorIndex,
							 SyntheticCDOPricer [] pricers,
							 double [] weights,
							 double [] initialX,
							 double [] deltaX,
							 double [] upperX,
							 double [] lowerX,
							 double [] log1mHs,
							 double [] log1mGs )
		{
		  Calibrator cal = new Calibrator( start, stop, tenorIndex, pricers, weights,
																			 log1mHs, log1mGs );
			double [] result = cal.Fit( initialX, deltaX, upperX, lowerX );
			return result;
		}
		#endregion // Calibration


		#region CPP_Model_Implementation
    //-
    // Distribution calculator
    //-
    class PoissonDistribution {
      //-
      // Constructor
      //-
      public PoissonDistribution( int basketSize,
																	double firstNumber,
																	double skew,
																	double secondNumber,
																	double correlation,
																	double fatalCoef,
																	double scaling,
																	double [] log1mHs,
																	double [] log1mGs )
      {
			  basketSize_ = basketSize;
				firstNumber_ = firstNumber;
				skew_ = skew;
				secondNumber_ = secondNumber;
				correlation_ = correlation;
				fatalCoef_ = fatalCoef;
				scaling_ = scaling;
				probs_ = new double[ basketSize + 1 ];
        PNj_ = new double[ basketSize + 1 ];
        hazardRates_ = new double[ basketSize ];
        systemRates_ = new double[ basketSize ];
        log1mHs_ = log1mHs;
        log1mGs_ = log1mGs;
				sumLog1mHs_ = new double[ basketSize ];
				sortedIndices_ = new int[ basketSize ];
      }

      //-
      // Constructor
      //-
      public PoissonDistribution( int basketSize )
      {
			  basketSize_ = basketSize;
				probs_ = new double[ basketSize + 1 ];
        PNj_ = new double[ basketSize + 1 ];
        log1mHs_ = new double[ basketSize ];
        log1mGs_ = new double[ basketSize ];
				sumLog1mHs_ = new double[ basketSize ];
				sortedIndices_ = new int[ basketSize ];

				// For safety, set all unused data mebers to NaN or null
				firstNumber_ = Double.NaN;
				skew_ =  Double.NaN;
				secondNumber_ = Double.NaN;
				correlation_ = Double.NaN;
				fatalCoef_ = Double.NaN;
				scaling_ = Double.NaN;
        hazardRates_ = null;
        systemRates_ = null;
      }

      //-
      // Initialize
      //-
      void initialize()
      {
        int N = basketSize_;
        for( int i = 0; i < N; ++i )
          log1mHs_[i] = log1mGs_[i] = 0.0;
      }

      //-
      // Calculate the ditribution surface
      //-
      public void iterateOverTime( bool wantProbability,
																	 Curve [] survCurves,
																	 double [] lossLevels,
																	 Dt start, Dt stop,
																	 Curve2D distributions,
																	 Curve [] idiosynCurves,
																	 Curve [] systemCurves )
      {
			  int nLevels = lossLevels.Length;
        double [] levels = lossLevels;
        int dateStride = distributions.DateStride();
        int levelStride = distributions.LevelStride();

        //initialize();
        
        Dt prevDate = distributions.GetAsOf();
        int nDates = distributions.NumDates();
        int iDate = 0;

        // find the start index
        for( ; iDate < nDates; ++iDate )
        {
          Dt current = distributions.GetDate(iDate);
          if( Dt.Cmp(current,start) > 0 )
            break;
          prevDate = current;
        }

        // calculate the distribution
        for( ; iDate < nDates; ++iDate )
        {
          Dt current = distributions.GetDate(iDate);
          if( Dt.Cmp(stop, current) < 0 )
            break;
          
          double deltaT = Dt.TimeInYears( prevDate, current );
					if( deltaT < 1.0E-7 )
						continue;

          calcHazardRates( prevDate, current, survCurves );
          CalcLogHAndLogGs( deltaT );
					if( null != systemCurves )
						UpdateCurves( current, idiosynCurves, systemCurves );
          prevDate = current;
          
          double [] result = useHullWhiteApproach_ ?
						CalcDistributionHullWhite( wantProbability, nLevels, levels )
						: CalcDistribution( wantProbability, nLevels, levels );
        
          int valueIdx = iDate * dateStride;
          valueIdx -= levelStride;
          for (int i = 0; i < nLevels; ++i)
          {
            double ri = result[ i ];
            distributions.SetValue( (valueIdx += levelStride), ri );
          }
        }

        return;
      }

      //-
      // Calculate the ditribution surface
      //-
      public void iterateOverTime( bool wantProbability,
																	 Curve [] idioCurves,
																	 Curve [] sysCurves,
																	 double [] lossLevels,
																	 Dt start, Dt stop,
																	 Curve2D distributions )

      {
			  int nLevels = lossLevels.Length;
        double [] levels = lossLevels;
        int dateStride = distributions.DateStride();
        int levelStride = distributions.LevelStride();

        //initialize();
				Dt asOf = distributions.GetAsOf();
        int nDates = distributions.NumDates();

        // find the start index
        int iDate = 0;
        for( ; iDate < nDates; ++iDate )
        {
          Dt current = distributions.GetDate(iDate);
          if( Dt.Cmp(current,start) >= 0 )
            break;
        }

        // calculate the distribution
        for( ; iDate < nDates; ++iDate )
        {
          Dt current = distributions.GetDate(iDate);
          if( Dt.Cmp(stop, current) < 0 )
            break;
          
          double T = Dt.TimeInYears( asOf, current );
					if( T < 1.0E-7 )
						continue;

          CalcLogHAndLogGs( T, current, idioCurves, sysCurves );
          
          double [] result = useHullWhiteApproach_ ?
						CalcDistributionHullWhite( wantProbability, nLevels, levels )
						: CalcDistribution( wantProbability, nLevels, levels );
        
          int valueIdx = iDate * dateStride;
          valueIdx -= levelStride;
          for (int i = 0; i < nLevels; ++i)
          {
            double ri = result[ i ];
            distributions.SetValue( (valueIdx += levelStride), ri );
          }
        }

        return;
      }

      private int basketSize_; // basket size
      private double firstNumber_; // 0-base instead of 1-based
      private double skew_;
      private double secondNumber_; // 0-base instead of 1-based
      private double correlation_;
      private double fatalCoef_;
      private double scaling_;
      
      double [] hazardRates_; // size: N;
      double [] systemRates_; // size: N;
      double [] sumLog1mHs_;     // size: N;   definition: sum_{i+1}^N -log(1-Hi)
      double [] log1mHs_;     // size: N;   definition: -log(1-Hi)
      double [] log1mGs_;     // size: N;   definition: -log(1-Gi)
      double [] PNj_;         // size: N+1; definition: P(Nj(t)=m)
      double [] probs_;       // size: N+1; definition: P(N(t)=m)
			int [] sortedIndices_;

      //-
      // Calculate the hazard rates for [start, stop]
      //-
      void calcHazardRates( Dt start, Dt stop, Curve [] curves )
      {
        double [] hazardRates = hazardRates_;
        double scaling = scaling_;
        int N = basketSize_;
        for( int i = 0; i < N; ++i )
          hazardRates[i] = curves[i].F( start, stop ) * scaling;
        return;
      }
      
      //-
      // Calculate the systematic components
      // Input:
      //   hazard rates and other parameters
      //-
      void calcSystemRates()
      {
        double firstNumber = firstNumber_;
        double secondNumber = secondNumber_;
        double fatalCoef = fatalCoef_;
        
        double hazardAvg = 0;
        double [] hazardRates = hazardRates_;
        int N = basketSize_;
        for( int i = 0; i < N; ++i )
          hazardAvg += hazardRates[i];
        hazardAvg /= N;       
        double hazardMax = hazardAvg; //hazardRates[0];
        double hazardMin = hazardAvg; //hazardRates[N - 1];

			#if DARIUSZ_OLD_SPEC
			#else
				int seconda = (int)secondNumber;
				double sec = secondNumber - seconda;
			#endif
        double [] result = systemRates_;
        for(int i = 0; i < N; ++i )
        {
          double h = 0;
          if( i <= firstNumber )
            h += hazardMax * skew_ * (1.0 - i / firstNumber);
			#if DARIUSZ_OLD_SPEC
          if( i <= secondNumber )
            h += hazardAvg * correlation_ * (1.0 - i / secondNumber);
			#else
          if( i <= seconda )
            h += hazardAvg * correlation_ ;
					else if( i == seconda + 1 )
            h += hazardAvg * correlation_ * sec;
			#endif
					
          h += hazardMin * fatalCoef;
          result[i] = Math.Min(h, hazardRates[i]);
        }

        return ;
      }

      // Calculate
      //   (1)  -log(1 - G_j(t))
      //   (2)  sum_{i > j} -log(1 - H_i(t)) 
      //-
      void CalcLogHAndLogGs( double deltaT )
      {
        calcSystemRates();
        
        double [] hazardRates = hazardRates_;
        double [] systemRates = systemRates_;

        // calculate system and idiosyncratic components
        double [] log1mGs = log1mGs_;
        double [] log1mHs = log1mHs_;
        int N = basketSize_;
        for( int i = 0; i < N; ++i )
        {
				  double sys = systemRates[i];
          log1mGs[i] += sys * deltaT;
          log1mHs[i] += (hazardRates[i] - sys) * deltaT;
        }

				// sort the systematic factor
				SortIndices();

        // calculate the sum of indiosyncratic components
				int [] indices = sortedIndices_;
        double [] sumLog1mHs = sumLog1mHs_;
        double sum = 0.0;
        for( int i = N - 1; i >= 0; --i )
        {
				  int ii = indices[i];
					sum += log1mHs[ii];
          sumLog1mHs[i] = sum;
        }

        return;
      }

      // Calculate
      //   (1)  -log(1 - G_j(t))
      //   (2)  sum_{i > j} -log(1 - H_i(t))
			//-
      // using pre-calculated curves as inputs
			//-
      void CalcLogHAndLogGs( double T, Dt date,
														 Curve [] idioCurves,
														 Curve [] sysCurves )
      {
        // calculate system components
        double [] log1mGs = log1mGs_;
        double [] log1mHs = log1mHs_;
        int N = basketSize_;
        for( int i = 0; i < N; ++i )
        {
          log1mGs[i] = - Math.Log( sysCurves[i].Interpolate(date) );
          log1mHs[i] = - Math.Log( idioCurves[i].Interpolate(date) );
        }

				// sort the systematic factor
				SortIndices();

        // calculate the sum of indiosyncratic components
				int [] indices = sortedIndices_;
        double [] sumLog1mHs = sumLog1mHs_;
        double sum = 0.0;
        for( int i = N - 1; i >= 0; --i )
        {
				  int ii = indices[i];
					sum += log1mHs[ii];
          sumLog1mHs[i] = sum;
        }

        return;
      }

			// Update the survival curves
			//-
			void UpdateCurves( Dt date,
												 Curve [] idiosynCurves,
												 Curve [] systemCurves )
			{
        double [] log1mGs = log1mGs_;
        double [] log1mHs = log1mHs_;
        int N = basketSize_;
        for( int i = N-1; i >= 0; --i )
        {
				  Curve curve = systemCurves[i];
					curve.Add( date, Math.Exp(-log1mGs[i]) );
				  curve = idiosynCurves[i];
					curve.Add( date, Math.Exp(-log1mHs_[i]) );
        }
				return;
			}

			// Sort indices
			//   use insertion sort since the array is almost sorted
			//-
			void SortIndices()
			{
        double [] G = log1mGs_;
				int [] indices = sortedIndices_;
				int N = indices.Length;
				for( int i = 0; i < N; ++i )
				{
					double g = G[i];
					int j = i;
					for( ; j > 0 && g > G[indices[j-1]]; --j )
						indices[j] = indices[j-1];
					indices[j] = i;
				}
				return;
			}

      // m (j,m-j)
      // ---------------------
      // 0 (0,0)
      // 1 (0,1)  (1,0)
      // 2 (0,2)  (1,1)   (2,0)
      // 3 (0,3)  (1,2)   (2,1)  (3,0)
      //-
      // N (0,N)  (1,N-1) (2,N-2) ...  (N,0)
      //-
      static void
      CalcPoissons( int stopIndex,
                    double sumLog1mH,
                    double [] result )
      {
        result[0] = Math.Exp( -sumLog1mH );
        for( int m = 1; m <= stopIndex; ++m )
        {
          result[m] = result[m-1] * sumLog1mH / m;
        }
        return;
      }

      //-
      // Calculate the probability distribution:
      //  P(N(t) = m), m = 0, 1, ..., N
      //-
      double []
      CalcDistribution( bool wantProbability,
                        int nLevels, double [] levels )
      {
        double [] log1mGs = log1mGs_;
        double [] log1mHs = log1mHs_;
        double [] sumLog1mHs = sumLog1mHs_;
        double [] PNj = PNj_;
        double [] probs = probs_;
        
        // intialize
        int N = basketSize_;
        for( int i = 0; i <= N; ++i )
          probs[i] = 0.0;
				int [] indices = sortedIndices_;

        // loop through j
        double prev1mG = 0.0;
        for(int j = 0; j < N; ++j )
        {
          // compute G_j(t) - G_{j+1}(t)
          double next1mG = Math.Exp(-log1mGs[ indices[j] ]);
          double dGj = next1mG - prev1mG;
          prev1mG = next1mG;

          // compute P(N(t) = m), m = 0,...,N
          int Nmj = N - j;
          CalcPoissons( Nmj, sumLog1mHs[j], PNj );
          for( int i = 0, idx = j; i <= Nmj; ++idx, ++i )
            probs[idx] += dGj * PNj[i];
        }
        probs[N] += 1 - prev1mG;

        if( wantProbability )
          cumulative( N, probs, nLevels, levels, PNj );
        else
          expectation( N, probs, nLevels, levels, PNj );
        
        return PNj;
      }

      //-
      // Calculate the probability distribution based on Hull-White approach:
      //  P(N(t) = m), m = 0, 1, ..., N
      //-
      double [] 
			CalcDistributionHullWhite( bool wantProbability,
																 int nLevels, double [] levels )
      {
        // intialize
        int N = basketSize_;
				int [] indices = sortedIndices_;

				// systematic probability distribution
				double[] dG = new double[N + 1];
				{
          double [] log1mGs = log1mGs_;
          double prev1mG = 0.0;
					for(int j = 0; j < N; ++j )
					{
						double next1mG = Math.Exp(-log1mGs[ indices[j] ]);
						dG[j] = next1mG - prev1mG;
						prev1mG = next1mG;
					}
					dG[N] = 1 - prev1mG;
				}

				// idiosyncratic default probabilities
				double[] pi = new double[N];
				{
          double [] log1mHs = log1mHs_;
					for(int j = 0; j < N; ++j )
						pi[j] = 1 - Math.Exp(-log1mHs[ indices[j] ]);
				}

				// Calculate probabilities
        double [] probs = probs_;

				// workspace
				//double[] workspace = new double[2*(N+1)];
        //ExponentialBasketModel.HullWhiteProbabilities(dG, pi, workspace, probs);
				HullWhiteDistribution.calculateProbabilities(dG, pi, probs);

				// Calculate distribution
        double [] PNj = PNj_;
        if( wantProbability )
          cumulative( N, probs, nLevels, levels, PNj );
        else
          expectation( N, probs, nLevels, levels, PNj );
        
        return PNj;
      }

      //-
      // Calculate the expectation a sorted sequence of levels
      // E(x) = E [ k I(k < x) + x I(k >= x) ]
      //      = sum_{k < x} k P(k) + x sum_{k >= x} P(k)
      //-
      static
      void expectation( int n, double [] probs,
                        int m, double [] levels,
                        double [] results )
      {
			  const double TOLERANCE = 1.0E-15;
        double probHigh = 1.0;
        double sum = 0;
        int k = 0;
        for( int i = 0; i < m; ++i ) {
          double x = levels[i];
          int kx = Math.Min((int)(Math.Floor(x)),n);
          while( k <= kx ) {
            double Pk = probs[ k ];
            sum += k * Pk;
            probHigh -= Pk;
            ++k;
          }
          if( probHigh > TOLERANCE )
            results[i] = sum + x * probHigh;
          else {
            for ( ; i < m; ++i)
              results[i] = sum;
            break;
          }
        }
        return;
      }
      
      //-
      // Calculate the cumulative distributions for a sorted sequence of levels
      //   P(x) = E [ I(k <= x) ]
      //-
      static
      void cumulative( int n, double [] probs,
											 int m, double [] levels,
											 double [] results )
      {
        double sum = 0;
        int k = 0;
        for( int i = 0; i < m; ++i ) {
          int x = Math.Min( (int)(levels[i]), n );
          for( ; k <= x; ++k )
            sum += probs[ k ];
          results[i] = sum ;
        }
        return;
      }
      
			//-
			// Configuration
			//-
			private static readonly bool useHullWhiteApproach_ = true;

    }; // class PoissonDistribution

		void ComputeDistributions( bool wantProbability,
															 Dt startDate,
															 Dt stopDate,
															 double firstNumber,
															 double skew,
															 double secondNumber,
															 double correlation,
															 double fatalCoef,
															 double scaling,
															 Curve [] survCurves,
															 Curve2D distributions,
															 double [] log1mHs,
															 double [] log1mGs,
															 Curve [] idiosynCurves,
															 Curve [] systemCurves )
		{
      int nBasket = survCurves.Length;
		
			// setup an array of loss levels
			int nLevels = distributions.NumLevels();
			if( nLevels <= 0 )
				throw new ToolkitException( String.Format("Invalid distribution surface: 0 levels") );
			
			double [] lossLevels = new double[ nLevels ];
			for( int i = 0; i < nLevels; ++i )
				lossLevels[i] = distributions.GetLevel(i) * nBasket ;

			// construct a calculator
			PoissonDistribution calculator = new PoissonDistribution( nBasket,
																																firstNumber,
																																skew,
																																secondNumber,
																																correlation,
																																fatalCoef,
																																scaling,
																																log1mHs,
																																log1mGs );
			calculator.iterateOverTime( wantProbability, survCurves, lossLevels,
																	startDate, stopDate, distributions,
																	idiosynCurves, systemCurves );
			
			return;    
		}

		void ComputeDistributions( bool wantProbability,
															 Dt startDate,
															 Dt stopDate,
															 Curve [] idiosynCurves,
															 Curve [] systemCurves,
															 Curve2D distributions )
		{
      int nBasket = systemCurves.Length;
		
			// setup an array of loss levels
			int nLevels = distributions.NumLevels();
			if( nLevels <= 0 )
				throw new ToolkitException( String.Format("Invalid distribution surface: 0 levels") );
			
			double [] lossLevels = new double[ nLevels ];
			for( int i = 0; i < nLevels; ++i )
				lossLevels[i] = distributions.GetLevel(i) * nBasket ;

			// construct a calculator
			PoissonDistribution calculator = new PoissonDistribution( nBasket );
			calculator.iterateOverTime( wantProbability, idiosynCurves, systemCurves, lossLevels,
																	startDate, stopDate, distributions );
			
			return;    
		}

    //-
    // Distribution calculator
    //-
    class HullWhiteDistribution {
      // This function calculates for m = 0, 1, 2, ..., N
      //-
      //  P(m) = sum_{j = 0 to N} Psys(j) * Pj(m-j)
      //-
      // where Pj(k) is the probability of k defaults
      // in the sub basket {j+1, j+2, ..., N} based on
      // idiosyncratic probabilities.
      //-
      // We loop from m = N down to m = 0.
      //-
      // m (j,m-j)
      // <-------------------------------<
      // 0 (0,0)
      // 1 (0,1)  (1,0)
      // 2 (0,2)  (1,1)   (2,0)
      // 3 (0,3)  (1,2)   (2,1)  (3,0)
      //-
      // N (0,N)  (1,N-1) (2,N-2) ...  (N,0)
      //-
      private static
      void updateProbabilities(
        int j,
        double pmj,
        double[] pnj,
        int start,
        int size,
        double[] result )
      {
        for( int k = 0; k < size; ++k )
          result[j+k+start] += pnj[k] * pmj;
      }
      
      public static
      void calculateProbabilities(
        double[] sysProbs,
        double[] idioProbs,
        double[] resultProbs )
      {
        int N = idioProbs.Length;

        // Initialize
        for( int i = 0; i <= N; ++i )
          resultProbs[i] = 0;

        HomoDistribution dist = new HomoDistribution(N+1);
        HomoDistribution work = new HomoDistribution(N+1);
        resultProbs[N] = sysProbs[N];
        
        for( int j = N - 1; j >= 0; --j )
        {
          HomoDistribution.update(dist, idioProbs[j], work);
          dist.swap(work);
          updateProbabilities( j, sysProbs[j],
                               dist.data(), dist.start(), dist.size(),
                               resultProbs );
        }
        return;
      }      
    }; // HullWhiteDistributions

    // A class for distribution data of homogeneous basket.
    // It is designed to ignore very small (< toleranceP) probabilities
    // and therefore to be efficient for large basket.
    class HomoDistribution {
			private HomoDistribution()
      {
			  start_ = 0; size_ = 0; data_ = null;
			}

      public HomoDistribution(int N)
      {
			  start_ = 0; size_ = 0; data_ = new double[N];
        initialize();
      }
      
      public double[] data() { return data_; }
      public int size() { return size_; }
      public int start() { return start_; }
      private void setStart(int i) { start_ = i; }
      private void setSize(int i) { size_ = i; }

      public void initialize()
      {
        if( data_ != null ) {
          start_ = 0; size_ = 1; data_[0] = 1.0;
          return;
        }
        start_ = size_ = 0;
      }
      
      public void swap(HomoDistribution d)
      {
			  { int tmp = start_; start_ = d.start_; d.start_ = tmp; }
				{ int tmp = size_; size_ = d.size_; d.size_ = tmp; }
				{ double[] tmp = data_; data_ = d.data_; d.data_ = tmp; }
      }

      //-
      //  Let p_i be the probability of default by name i.
      //  Let P(k,n) be the probability of exact k defaults out of n names.
      //  Then
      //-
      //    P(k,n) = p_n P(k-1,n-1) + (1 - p_n) P(k, n-1)
      //     
      //  We iterate through n and k to compute P(k,N) for k = 1, .., K,
      //  here K <= N, N is the size of the basket.
      //-
      //  --- Hehui Jin 06/30/2004
      //-
      public static
      void update( HomoDistribution old,
                   double p_n,
                   HomoDistribution result )
      {
			  const double toleranceP = 2.0e-16 ;

        double q_n = 1 - p_n;

        double[] P0 = old.data();
        int size = old.size();
      
        double[] P = result.data();
        double P0_km1 = P[0] = 0.0;
        result.setStart(0);

        // find the first significant probability
        int k0 = 0 ;
        for( int k = 0; k < size; ++k ) {
          double Pk = p_n * P0_km1 + q_n * P0[k];
          if (Pk > toleranceP) {
            P[0] = Pk;
            k0 = k;
            result.setStart(k + old.start());
            P0_km1 = P0[k];
            break;
          }
          P0_km1 = P0[k];
        }

        // update untill the probability become insignificant
        for( int k = k0 + 1, idx = 1; k < size; ++k ) {
          double Pk = p_n * P0_km1 + q_n * P0[k];
          if (Pk <= toleranceP) {
            result.setSize(k - k0);
            return ;
          }
          P[idx++] = Pk;
          P0_km1 = P0[k];
        }
        
        // if we are here, check the last probability
        int lastIdx = size - k0 ;
        P0_km1 *= p_n;
        if( P0_km1 > toleranceP )
          P[lastIdx++] = P0_km1;
        result.setSize( lastIdx );
        
        return ;
      }

			private int start_; // starting number of defaults
      private int size_;  // size of effective probabilities
      private double[] data_;
    }; // class HomoDistribution

		#endregion // CPP_Model_Implementation

		#region Sorting
		// The comparison method
		class Comparer : IComparer  {
			public Comparer( Array data )
			{
			  data_ = data;
			}

			public int Compare( Object x, Object y )  {
			  return System.Collections.Comparer.Default.Compare( data_.GetValue( (int)y ),
																														data_.GetValue( (int)x ) );
			}

			private Array data_;
		};

		/// <summary>
		///   Sort an array
		/// </summary>
		/// <remarks>
		///   The original array is not modified and the sorting result is returned in the array of indices.
		/// </remarks>
		private static void SortIndices( Array array, int [] indices )
		{
			// Sanity check
		  if( null == array || array.Length == 0 )
				return ;
			if( indices.Length != array.Length )
				throw new ToolkitException( String.Format( "array[Length={0}] and indices[Length={1}] not match",
																									 array.Length, indices.Length ) );

			// Comparision method
			Comparer comparer = new Comparer( array );

		  // initialize the array of indices
			int nElems = array.Length;
			for( int i = 0; i < nElems; ++i )
				indices[i] = i;

			// Sort the index array
			Array.Sort( indices, comparer );

			// Check indices
			for( int i = 1; i < nElems; ++i )
				if( System.Collections.Comparer.Default.Compare( array.GetValue(indices[i]),
																												 array.GetValue(indices[i-1]) ) > 0 )
				  throw new ToolkitException( "Sorting Error!!!" );

			return ;
		}


		/// <summary>
		///   Sort curves by hazard rates
		/// </summary>
		/// <return>
		///   An index array
		/// </return>
		/// <remarks>
		///   The original array is not modified and the sorting result is returned as an array of indices.
		/// </remarks>
		private static int [] SortByHazardRate( Curve [] curves, Dt start, Dt end )
		{
		  if( null == curves || curves.Length == 0 )
				return null;

		  // initialize the array of hazard rates
			int nCurves = curves.Length;
			double [] hazardRates = new double[ nCurves ];
			for( int i = 0; i < nCurves; ++i )
				hazardRates[i] = curves[i].F(start,end);

			// return sorted index
			int [] indices = new int[ nCurves ];
			SortIndices( hazardRates, indices );
			return indices;
		}
		#endregion // Sorting

	} // class ExponentialBasketPricer

}
