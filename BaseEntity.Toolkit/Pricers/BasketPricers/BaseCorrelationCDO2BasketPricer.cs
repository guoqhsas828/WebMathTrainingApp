/*
 * BaseCorrelationCDO2BasketPricer.cs
 *
 *
 */

using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

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
  ///   <para>Each sub-basket in the CDO squared has its own uniform correlation factor,
  ///   which is calculated from a BaseCorrelation object. </para>
  /// </remarks>
	[Serializable]
	public class BaseCorrelationCDO2BasketPricer : TrancheCorrelationCDO2BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(BaseCorrelationCDO2BasketPricer));

		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="maturity">Maturity date</param>
		/// <param name="discountCurve">Discount curve</param>
		/// <param name="survivalCurves">Array of Survival Curves of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Principals of individual names in child CDOs</param>
		/// <param name="attachments">Attachment points of child CDOs</param>
		/// <param name="detachments">Detachment points of child CDOs</param>
		/// <param name="cdoMaturities">Same of different underlying CDO maturities</param>
		/// <param name="crossSubordination">If true, with cross subordination</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="baseCorrelation">Base correlation for the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		///
		/// <remarks>
		///   <para>This is obsolete.</para>
		/// </remarks>
    public
		BaseCorrelationCDO2BasketPricer( Dt asOf,
																		 Dt settle,
																		 Dt maturity,
																		 DiscountCurve discountCurve,
																		 SurvivalCurve [] survivalCurves,
																		 RecoveryCurve[] recoveryCurves,
																		 double [] principals,
																		 double [] attachments,
																		 double [] detachments,
                                     Dt[] cdoMaturities,
																		 bool crossSubordination,
																		 Copula copula,
																		 BaseCorrelationObject baseCorrelation,
																		 int stepSize,
																		 TimeUnit stepUnit,
																		 Array lossLevels,
																		 int sampleSize )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, 
							principals, attachments, detachments, cdoMaturities, crossSubordination,
							copula, new double[ attachments.Length * attachments.Length ],
							stepSize, stepUnit, lossLevels, sampleSize )
		{
      logger.DebugFormat("Creating BaseCorrelation CDO^2 Basket asof={0}, settle={1}, maturity={2}",
                          asOf, settle, maturity);
     
			baseCorrelation_ = baseCorrelation;
			rescaleStrike_ = false;
			discountCurve_ = discountCurve;
			factorUpdated_ = false;

			logger.Debug( "Basket created" );
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			BaseCorrelationCDO2BasketPricer obj = (BaseCorrelationCDO2BasketPricer)base.Clone();
			obj.baseCorrelation_ = (BaseCorrelationObject) baseCorrelation_.Clone();
			obj.discountCurve_ = (DiscountCurve) discountCurve_.Clone();
			obj.cdoTerm_ = (SyntheticCDO) cdoTerm_.Clone();
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
      
      if (baseCorrelation_ == null)
        InvalidValue.AddError(errors, this, "BaseCorrelation", String.Format("base correlation cannot be null"));
      
      return;
    }

		/// <summary>
		///   Recompute the detachment/attachment correlation
		/// </summary>
		private void
		UpdateFactors()
		{
		  ComputeFactors( this.Maturity );
		}

		private void
		ComputeFactors( Dt maturity )
		{
		  //BaseCorrelation baseCorrelation = (BaseCorrelation) baseCorrelation_;
		  if( baseCorrelation_ == null )
				throw new ToolkitException( "BaseCorrelation is null" );

			double[] attachments = this.Attachments;
			double[] detachments = this.Detachments;
			double[] principals = this.Principals;
			RecoveryCurve[] recovCurves = this.RecoveryCurves;
			SurvivalCurve[] survCurves = this.SurvivalCurves;
			int poolSize = survCurves.Length;
			int nCDOs = attachments.Length;
			double[] corrs = new double[nCDOs];
			for( int i = 0; i < nCDOs; ++i )
			{
        if (attachments[i] < 1E-8 && detachments[i] > 1 - 1E-8)
        {
          corrs[i] = 0;
          continue;
        }

			  int baseIdx = i * poolSize ;
				int basketSize = 0;
				for( int j = 0, k = baseIdx; j < poolSize; ++k, ++j )
					if( principals[k] != 0.0 ) ++basketSize;

				double[] prins = new double[basketSize];
				RecoveryCurve[] rc = new RecoveryCurve[basketSize];
				SurvivalCurve[] sc = new SurvivalCurve[basketSize];
				for( int j = 0, k = baseIdx, m = 0; j < poolSize; ++k, ++j )
					if( principals[k] != 0.0 )
					{
					  prins[m] = principals[k];
						sc[m] = survCurves[j];
						rc[m] = recovCurves[j];
						++m;
					}

        SyntheticCDO cdo = 
				cdoTerm_ == null ?
				new SyntheticCDO( Settle, maturity, Currency.None, 0.0,
													DayCount.None, Frequency.Quarterly,
													BDConvention.Following, Calendar.None )
				:
				new SyntheticCDO( Settle, maturity, cdoTerm_.Ccy, 0.0,
													cdoTerm_.DayCount, cdoTerm_.Freq,
													cdoTerm_.BDConvention, cdoTerm_.Calendar )
				;
				cdo.Attachment = attachments[i];
				cdo.Detachment = detachments[i];

        BasketPricer basket = new SemiAnalyticBasketPricer(
          AsOf, Settle, maturity, sc, rc, prins,
          Copula, new SingleFactorCorrelation(new string[basketSize], 0.0),
          StepSize, StepUnit, null, false);
        basket = new BaseCorrelationBasketPricer(basket, discountCurve_,
          baseCorrelation_, rescaleStrike_, attachments[i], detachments[i]);
        basket.NoAmortization = true;
        basket.IsUnique = true;
        SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basket, discountCurve_, 1.0, null);

        double targetPv = pricer.ProductPv();
        pricer = new SyntheticCDOPricer(cdo,
          ((BaseCorrelationBasketPricer)basket).CreateDetachmentBasketPricer(false),
          discountCurve_, pricer.Notional, pricer.RateResets);
        double toleranceF = 0, toleranceX = 0;
        double min = 0.0, max = 1.0;
        CheckTolerance(pricer, ref toleranceF, ref toleranceX);
        corrs[i] = CorrelationSolver.Solve(
          PriceMeasure.Pv, targetPv, pricer, toleranceF, toleranceX, min, max);
			}

			// We use the simple average of two correlations for
			// the intersection of two baskets
			double[] factors =  new double[ nCDOs * nCDOs ];
			for( int i = 0, idx = 0; i < nCDOs; ++i )
				for( int j = 0; j < nCDOs; ++j )
				  factors[idx++] = Math.Sqrt( (corrs[i] + corrs[j]) / 2 );
			this.TrancheFactors = factors;
			factorUpdated_ = true;

			return;
		}

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		protected override void	
		ComputeDistribution( double[] attachs, double[] levels,
												 Dt maturity, int stepSize, TimeUnit stepUnit,
												 bool wantProbability, Curve2D lossDistribution )
		{
			if( rescaleStrike_ || !factorUpdated_ )
				ComputeFactors( maturity );

			base.ComputeDistribution( attachs, levels,
																maturity, stepSize, stepUnit,
																wantProbability, lossDistribution );

			return;
		}

    // check tolerance
    private static void CheckTolerance(SyntheticCDOPricer pricer,
      ref double toleranceF, ref double toleranceX)
    {
      if (toleranceF <= 0)
        toleranceF = 1.0 / Math.Abs(pricer.TotalPrincipal);
      if (toleranceX <= 0)
      {
        toleranceX = 100 * toleranceF;
        if (toleranceX > 0.0001)
          toleranceX = 0.0001;
      }
    }

		#endregion // Methods

		#region Properties
		/// <summary>
		///   Base correlation
		/// </summary>
		public BaseCorrelationObject BaseCorrelation
		{
			get { return baseCorrelation_; }
			set {
			  if( baseCorrelation_ != value )
				{
					baseCorrelation_ = value;
					factorUpdated_ = false; // reset correlation factors
				}
			}
		}

		/// <summary>
		///   Re-scale strike points every time we price.
		/// </summary>
		public bool RescaleStrike
		{
			get { return rescaleStrike_; }
			set { rescaleStrike_ = value; }
		}

		/// <summary>
		///   CDO terms (Day count, rolls, etc., used to calculate base correlation
		/// </summary>
		public SyntheticCDO BaseCDOTerm
		{
			get { return cdoTerm_; }
			set { cdoTerm_ = value; }
		}
		#endregion // Properties

		#region Data
		private BaseCorrelationObject baseCorrelation_;
		private bool rescaleStrike_;
		private bool factorUpdated_;
		private DiscountCurve discountCurve_;
		private SyntheticCDO cdoTerm_;
		#endregion Data

	} // class BaseCorrelationCDO2BasketPricer

}
