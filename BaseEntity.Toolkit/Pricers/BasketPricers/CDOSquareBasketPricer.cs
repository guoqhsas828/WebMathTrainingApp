/*
 * CDOSquaredBasketPricer.cs
 *
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{


	/// <summary>
	///   Base class for all CDO squared basket pricers
	/// </summary>
	[Serializable]
	public abstract class CDOSquaredBasketPricer : BasketPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CDOSquaredBasketPricer));

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
		/// <param name="cdoMaturities">Same of different CDO maturities</param>
		/// <param name="crossSubordination">If true, with cross subordination</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Correlation of the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    public
		CDOSquaredBasketPricer( Dt asOf,
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
														Correlation correlation,
														int stepSize,
														TimeUnit stepUnit,
														Array lossLevels )
			: base( asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
							copula, correlation, stepSize, stepUnit, lossLevels )
		{
      logger.DebugFormat("Creating CDO^2 Basket asof={0}, settle={1}, maturity={2}",
                          asOf, settle, maturity);
			
			this.Attachments = attachments;
			this.Detachments = detachments;
      this.CdoMaturities = cdoMaturities;
      if(cdoMaturities==null || cdoMaturities.Length==0)
      {
        this.CdoMaturities = new Dt[attachments.Length];
        for (int i = 0; i < attachments.Length; i++)
          this.CdoMaturities[i] = Maturity;
      }

			this.LossDistribution = new Curve2D();
			this.distributionComputed_ = false;
			this.crossSubordination_ = crossSubordination ;
      this.RawLossLevels = UniqueSequence<double>.From(lossLevels); // don't add complement levels

			// important: forced to recalculate the total principal
			this.Principals = principals;

			logger.Debug( "Basket created" );
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			CDOSquaredBasketPricer obj = (CDOSquaredBasketPricer)base.Clone();

			obj.lossDistribution_ = lossDistribution_.clone();
      obj.Attachments = CloneUtil.Clone(Attachments);
      obj.Detachments = CloneUtil.Clone(Detachments);

			return obj;
		}

    /// <summary>
    /// Duplicate a basket pricer
    /// <preliminary/>
    /// </summary>
    /// <returns>Duplicated basket pricer</returns>
    /// <remarks>
    /// 	<para>Duplicate() differs from Clone() in that it copies by references all the
    /// basic data and numerical options defined in the BasketPricer class.  Both
    /// the original basket and the duplicated basket share the same underlying curves,
    /// principals and correlation object.  Unlike the MemberwiseClone() function, however,
    /// it does not copy by reference any intermediate computational data such as
    /// loss distributions and computed detachment/attachment correlations.  These data
    /// are held separately in the duplicated and original baskets.
    /// </para>
    /// 	<para>In this way, it provides an easy way to construct objects performing
    /// independent calculations on the same set of input data.</para>
    /// </remarks>
    public override BasketPricer Duplicate()
    {
      var obj = (CDOSquaredBasketPricer)base.Duplicate();
      obj.lossDistribution_ = lossDistribution_ == null
        ? null : lossDistribution_.clone();
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

      // Invalid Attachments
      if (attachments_ == null)
        InvalidValue.AddError(errors, this, "Attachments", String.Format("Invalid list of attachments. Cannot be null."));

      // Invalid Detachments
      if (detachments_ == null)
        InvalidValue.AddError(errors, this, "Detachments", String.Format("Invalid list of attachments. Cannot be null."));
	

      return;
    }

		/// <summary>
		///   Compute transformation parameters, 
		///   transform attachments and loss levels arrays
		///   for cross sub-ordination.
		/// </summary>
		private void CrossSubsTransform( double[] lossLevels,
																		 out double[] att,
																		 out double[] levels )
		{
		  baseLevel_ = 0.0;
		  scaleFactor_ = 1.0;
			att = Attachments;
			levels = lossLevels;
			if( CrossSubordination )
			{
			  att = new double[ attachments_.Length ];
				double newTotalPrincipal = 0.0;
				double deductAmount = 0.0;
				int nBasket = Count;
				double [] principals = Principals;
				for (int i = 0, idx = 0; i < attachments_.Length; i++)
				{
					double sum = 0;
					for (int j = 0; j < nBasket; idx++, j++)
					{
					  sum += principals[idx];
					}
					newTotalPrincipal += sum * detachments_[i] ;
					deductAmount += sum * attachments_[i] ;
				  att[i] = 0; // new attachment point is zero
				}
				baseLevel_ = deductAmount / newTotalPrincipal;
				scaleFactor_ = TotalPrincipal / newTotalPrincipal;

				// transform loss levels
				levels = new double[ lossLevels.Length ];
				for( int i = 0; i < levels.Length; ++i )
				{
					double newLevel = baseLevel_ + scaleFactor_ * lossLevels[i];
					levels[i] = Math.Min( 1.0, newLevel );
				}
				levels = SetLossLevels(levels, false).ToArray(); // don't add complement levels
			}
			return ;
		}

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		protected abstract void ComputeDistribution( double[] attachments,
																								 double[] lossLevels,
																								 Dt maturity,
																								 int stepSize,
																								 TimeUnit stepUnit,
																								 bool wantProbability,
																								 Curve2D lossDistribution
																								 );

		/// <summary>
		///   Compute the whole distribution, save the result for later use
		/// </summary>
		private void ComputeAndSaveDistribution()
		{
			double[] attachs = null; 
			double[] levels = null;
			CrossSubsTransform( CookedLossLevels.ToArray(), out attachs, out levels );
			ComputeDistribution( attachs, levels,
													 this.Maturity,
													 this.StepSize, this.StepUnit,
													 false, this.LossDistribution );
			distributionComputed_ = true;
		}


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
		public override double [,]
		CalcLossDistribution( bool wantProbability,
													Dt date, double [] lossLevels )
		{
		  Timer timer = new Timer();
			timer.start();
			logger.Debug( "Computing loss distribution for CDO Squared basket" );

			if( Dt.Cmp(Settle, date) > 0 )
				throw new ArgumentOutOfRangeException("date", "date is before settlement");
			if( Dt.Cmp(Maturity, date) < 0 )
				throw new ArgumentOutOfRangeException("date", "date is after maturity");

			for (int i = 0; i < lossLevels.Length; ++i) {
			  // By its nature the distribution is discrete To avoid unexpected
			  // results, we round numbers to nearest effective decimal points,
			  // to make sure, for example,  2.0 does not become something like
			  // 1.999999999999954
			  decimal x = (decimal) lossLevels[i] ;
				lossLevels[i] = (double) Math.Round(x, EffectiveDigits);
				if (lossLevels[i] > 1.0)
					lossLevels[i] = 1.0;
			}

			bool savedDistributionComputed = distributionComputed_;
		  double savedBaseLevel = baseLevel_ ;
		  double savedScaleFactor = scaleFactor_ ;
			double [,] results = null;

			try {
				double[] attachs = null; 
				double[] levels = null;
				CrossSubsTransform( lossLevels, out attachs, out levels );
			  Curve2D lossDistribution = new Curve2D();
				int stepSize = (int)( 1 + Dt.TimeInYears(AsOf,Maturity) );
				TimeUnit stepUnit = TimeUnit.Years;
				ComputeDistribution( attachs, levels,
														 date, stepSize, stepUnit,
														 true,
														 lossDistribution );
				double totalPrincipal = TotalPrincipal;;
				int N = lossDistribution.NumLevels();
				results = new double[ N, 2 ];
				for( int i = 0; i < N; ++i ) {
			    double level = lossDistribution.GetLevel(i);
					results[i,1] = lossDistribution.Interpolate( date, level );
					if( !wantProbability )
						results[i,1] /= totalPrincipal;
					results[i,0] = (level - baseLevel_) / scaleFactor_ ;
				}
			}
			finally {
				distributionComputed_ = savedDistributionComputed;
				baseLevel_ = savedBaseLevel;
				scaleFactor_ = savedScaleFactor;
			}

			timer.stop();
      logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());

			return results;
		}

		/// <summary>
		///   Compute the accumulated loss on a tranche
		/// </summary>
		public override double
		AccumulatedLoss(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  if (!distributionComputed_)
				ComputeAndSaveDistribution();

			double tBegin = Math.Min( 1.0, baseLevel_ + scaleFactor_ * trancheBegin );
			double tEnd = Math.Min( 1.0, baseLevel_ + scaleFactor_ * trancheEnd );
			double loss = LossDistribution.Interpolate(date, tBegin, tEnd);

      // This TotalPrincipal assumes all underlying sub-CDO's maturities are same as CDO2 maturity
      // If sub-CDOs have different maturities, this TotalPrincipal should depend on current date    
		  loss /= TotalPrincipal;
			return loss;
		}

    /// <summary>
    ///  Calculate the total principal at current date
    ///  due to fact that underlying sub-CDOs may have
    ///  different maturities
    /// </summary>
    /// <param name="date">Current date</param>
    /// <returns>Current total principal</returns>
    public double CurrentTotalPrincipal(Dt date)
    {
      double currentTotalPrincipal = 0.0;
      int nBasket = Count;
      for (int i = 0, idx = 0; i < Attachments.Length; i++)
      {
        double sum = 0;
        if (CdoMaturities[i] >= date)
        {
          for (int j = 0; j < nBasket; idx++, j++)
          {
            sum += Principals[idx];
          }
        }
        currentTotalPrincipal += sum * (detachments_[i] - attachments_[i]);
      }
      return currentTotalPrincipal;      
    }


		/// <summary>
		///   Compute the amortized recovery on a tranche
		/// </summary>
		public override double
		AmortizedAmount(Dt date,
										double trancheBegin,
										double trancheEnd)
		{
		  // no amortization on the CDO squared
		  return 0.0;
		}


		///
		/// <summary>
		///   Reset the pricer such that in the next request for AccumulatedLoss()
		///   or AmortizedAmount(), it recomputes everything.
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
		  if( attachments_ == null || detachments_ == null )
				return base.OnSetPrincipals( principals );

		  double totalPrincipal = 0.0;
			int nBasket = Count;
			for (int i = 0, idx = 0; i < attachments_.Length; i++)
			{
				double sum = 0;
				for (int j = 0; j < nBasket; idx++, j++)
				{
					sum += principals[idx];
				}
				totalPrincipal += sum * (detachments_[i] - attachments_[i]);
			}
			return totalPrincipal ;
		}

    internal static Dt[] ConstructDateArray(
      Dt start, Dt stop, int stepSize, TimeUnit stepUnit)
    {
      ArrayList a = new ArrayList();
      while (Dt.Cmp(start, stop) < 0)
      {
        a.Add(start);
        start = Dt.Add(start, stepSize, stepUnit);
      }
      a.Add(stop);

      Dt[] result = new Dt[a.Count];
      for (int i = 0; i < a.Count; ++i)
        result[i] = (Dt)a[i];

      return result;
    }

    #endregion // Methods

		#region Properties

    /// <summary>
    ///   Cross subordination enabled or not
		/// </summary>
		public bool CrossSubordination
		{
			get { return crossSubordination_; }
			set { crossSubordination_ = value; }
		}

    /// <summary>
    ///   Principal or face values for each name in the basket
		/// </summary>
		public double [] Attachments
		{
			// TBD mef 22Apr2004 Improve data validation
			get { return attachments_; }
			set { attachments_ = value; }
		}


    /// <summary>
    ///   Principal or face values for each name in the basket
		/// </summary>
		public double [] Detachments
		{
			// TBD mef 22Apr2004 Improve data validation
			get { return detachments_; }
			set { detachments_ = value; }
		}

    /// <summary>
    ///  Same or different CDO maturities, used to compute current date total principal
    /// </summary>
	  public Dt[] CdoMaturities
	  {
      get { return cdoMaturities_; }
      set { cdoMaturities_ = value;}
	  }

		/// <summary>
		///   Computed distribution for basket
		/// </summary>
		public Curve2D LossDistribution
		{
			get { return lossDistribution_; }
			set { lossDistribution_ = value; }
		}


		/// <summary>
		///   Distribution computed
		/// </summary>
		public bool DistributionComputed
		{
			get { return distributionComputed_; }
			set { distributionComputed_ = value; }
		}
		#endregion // Properties

		#region Data

		private double[] attachments_;
		private double[] detachments_;
	  private Dt[] cdoMaturities_;
		private Curve2D lossDistribution_;
		private bool distributionComputed_;
		private bool crossSubordination_;

		private double baseLevel_;
		private double scaleFactor_;

		#endregion Data

	} // class CDOSquaredBasketPricer

}
