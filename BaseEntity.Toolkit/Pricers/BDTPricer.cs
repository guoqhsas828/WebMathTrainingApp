/*
 * BDTPricer.cs
 *
 *
 */
using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Pricers
{
  ///
  /// <summary>
  ///   Black-Derman-Toy model abstract pricer class
  /// </summary>
  ///
  /// <remarks>
  ///   <para>See "A one-factor model of interest
  ///   rates and its application to treasury bond options," Financial
  ///   Analysts Journal, Jan-Feb 1990, pp. 33-39.</para>
  ///   <para>This is a log-normally distributed process with time-varying mean and
  ///   volatility.  Used with bonds (and options on bonds) as well as
  ///   with hazard rate modeling.</para>
  ///   <para>This model covers processes with dynamics described by
  ///   <formula>
  ///     dx_t = \mu(t) dt + \sigma(t) dW_t
  ///   </formula>
  ///   where <formula inline="true">x_t = \log(r_t)</formula> and
  ///   <formula inline="true">r_t</formula> is the short rate or hazard rate of interest.</para>
  ///
  ///   <para>Requires as input a term structure of values (<formula inline="true">\mu(t)</formula>)
  ///   for a grid of evenly spaced maturities) and associated volatilities
  ///   (<formula inline="true">\sigma(t)</formula>).</para>
  /// </remarks>
	///
  [Serializable]
 	public abstract class BDTPricer : PricerBase
	{
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(BDTPricer));

		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		///
		protected
		BDTPricer(IProduct product)
		: base(product)
		{
			deltaT_ = 1.0/52.0;
		}


		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount curve</param>
		/// <param name="survivalCurve">Survival curve</param>
		/// <param name="recoveryRate">Recovery rate</param>
		/// <param name="volatilityCurve">Short rate volatility term structure</param>
		///
		protected
		BDTPricer(IProduct product, Dt asOf, Dt settle, DiscountCurve discountCurve,
							SurvivalCurve survivalCurve, double recoveryRate,
							VolatilityCurve volatilityCurve)
			: base(product, asOf, settle)
		{
			// Set data, using properties to include validation
			DiscountCurve = discountCurve;
			SurvivalCurve = survivalCurve;
			RecoveryCurve = new RecoveryCurve(asOf, recoveryRate);
			VolatilityCurve = volatilityCurve;
			deltaT_ = 1.0/52.0;
		}


		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount curve</param>
		/// <param name="survivalCurve">Survival curve</param>
		/// <param name="recoveryCurve">Recovery curve</param>
		/// <param name="volatilityCurve">Short rate volatility term structure</param>
		///
		protected
		BDTPricer(IProduct product, Dt asOf, Dt settle, DiscountCurve discountCurve,
							SurvivalCurve survivalCurve, RecoveryCurve recoveryCurve,
							VolatilityCurve volatilityCurve)
			: base(product, asOf, settle)
		{
			// Set data, using properties to include validation
			DiscountCurve = discountCurve;
			SurvivalCurve = survivalCurve;
			RecoveryCurve = recoveryCurve;
			VolatilityCurve = volatilityCurve;
			deltaT_ = 1.0/52.0;
		}


		/// <summary>
		///   Clone
		/// </summary>
		///
		public override object Clone()
		{
			BDTPricer obj = (BDTPricer)base.Clone();
			obj.discountCurve_ = (discountCurve_ != null) ? (DiscountCurve)discountCurve_.Clone() : null;
			obj.survivalCurve_ = (survivalCurve_ != null) ? (SurvivalCurve)survivalCurve_.Clone() : null;
			obj.recoveryCurve_ = (recoveryCurve_ != null) ? (RecoveryCurve)recoveryCurve_.Clone() : null;
			obj.volatilityCurve_ = (volatilityCurve_ != null) ? (VolatilityCurve)volatilityCurve_.Clone() : null;

			return obj;
		}

		#endregion // Constructors

		#region Methods

    # region Validate
    
    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      if (recoveryCurve_ == null)
        InvalidValue.AddError(errors, this, "RecoveryCurve", String.Format("Invalid recovery curve. Cannot be null"));

      if (volatilityCurve_ == null)
        InvalidValue.AddError(errors, this, "VolatilityCurve", String.Format("Invalid volatility curve. Cannot be null"));
     
      return;
    }

    # endregion
    /// <summary>
    ///   Retrieves the rate at node (i,j).
    /// </summary>
    ///
    /// <param name="timeSlice">Time index into tree (1...)</param>
    /// <param name="level">Volatility index in timeSlice (0...).</param>
    ///
    /// <returns>Interest rate at associated node</returns>
    ///
    public double
		Rate(int timeSlice, int level)
		{
			if( rates_ == null )
				throw new ArgumentException("BDT rates tree not yet constructed");
			if( timeSlice < 0 || timeSlice >= rates_.Length )
				throw new ArgumentOutOfRangeException("timeSlice", String.Format( "Invalid timeslice. Must be 0-{0}, not {1}", rates_.Length, timeSlice) );
			if( level < 0 || level > timeSlice )
				throw new ArgumentOutOfRangeException("level", String.Format( "Invalid level. Must be 0-{0}, not {1}", timeSlice, level) );
			int b = timeSlice*(timeSlice-1)/2;
			return rates_[b + level];
		}


    /// <summary>
    ///   Generate BDT rate trees
    /// </summary>
    ///
    /// <param name="maturity">Maturity date of product to price</param>
    ///
		public void
		Generate(Dt maturity)
		{
			double now = Settle.ToDouble();
			double end = maturity.ToDouble();
			double tDiff = end - now;
			// Set up the delta to be as close to the requested delta as possible.
			double myDelta = (tDiff > deltaT_) ? tDiff / Math.Round( tDiff / deltaT_ ) : tDiff;
			// Number of time slices we'll use
			int n = (int) ( tDiff / myDelta );

			// Create BDT rates tree
			logger.Debug( String.Format(" Generating BDT tree. now={0}, end={1}, decltaT={2}, myDelta={3}, n={4}", now, end, deltaT_, myDelta, n) );

			// Create schedules for BDT model
			dates_ = new Dt[n];
			T_ = new double[n];
			df_ = new double[n];
			sigma_ = new double[n];
			rates_ = new double[n*(n-1)/2 + n];
			double[] survival = new double[n];
			double[] rfdf = new double[n];
			double[] recovery = new double[n];

			// Fill in rate and volatility tables
			for( int i=0; i < n; i++ )
			{
				T_[i] = now + (i+1)*myDelta;
				dates_[i] = new Dt(T_[i]);
				sigma_[i] = volatilityCurve_.Volatility(dates_[i]);
				survival[i] = (survivalCurve_ != null) ? survivalCurve_.SurvivalProb(dates_[i]) : 1.0;
				rfdf[i] = discountCurve_.DiscountFactor(dates_[i]);
				recovery[i] = (recoveryCurve_ != null) ? recoveryCurve_.RecoveryRate(dates_[i]) : 0.0;
			}
			// Adjust the last date for pricing comparisons.
			dates_[n-1] = maturity;

			// Generate risky discount factors
			if( survivalCurve_ != null )
				Bootstrap.SurvivalToRdf(survival, rfdf, recovery, df_);
			else
				df_ = rfdf;

			//for( int i=0; i < n; i++ )
			//{
			//	logger.Debug( String.Format(" {0}: T={1}, df={2}, sigma={3}", i, T_[i], df_[i], sigma_[i]) );
			//}

			// Generate rate tree
			BDT.Tree(df_, sigma_, myDelta, rates_);

			return;
		} // Generate


		#endregion // Methods

    #region Properties

    /// <summary>
    ///   Discount curve used for pricing
    /// </summary>
		public DiscountCurve DiscountCurve
		{
			get { return discountCurve_; }
			set {
				discountCurve_ = value;
			}
		}


    /// <summary>
    ///   Survival curve used for pricing. May be null.
    /// </summary>
		public SurvivalCurve SurvivalCurve
		{
			get { return survivalCurve_; }
			set { survivalCurve_ = value; }
		}


    /// <summary>
    ///   Recovery curve used for pricing
    /// </summary>
		public RecoveryCurve RecoveryCurve
		{
			get { return recoveryCurve_; }
			set {
				recoveryCurve_ = value;
			}
		}


    /// <summary>
    ///   Risky zero coupon rate volatilities for pricing.
    /// </summary>
		public VolatilityCurve VolatilityCurve
		{
			get { return volatilityCurve_; }
			set {
				volatilityCurve_ = value;
			}
		}


		/// <summary>
		///   Term structure of risky zero coupon prices
		/// </summary>
		protected double [] Df
		{
			get { return df_; }
		}


		/// <summary>
		///   Volatilities for term structure (length matches the length of mu)
		/// </summary>
		protected double [] Sigma
		{
			get { return sigma_; }
		}


		///
    /// <summary>
		///   Time between nodes, in years
		/// </summary>
		protected double DeltaT
		{
			get { return deltaT_; }
		}


		///
    /// <summary>
		///   Time to node, in years (matching the mu/sigma inputs)
		/// </summary>
		protected double [] T
		{
			get { return T_; }
		}


		///
    /// <summary>
		///   Dates to node (matching the mu/sigma inputs)
		/// </summary>
		protected Dt [] Dates
		{
			get { return dates_; }
		}


    #endregion // Properties

		#region Data

    private DiscountCurve discountCurve_;
    private SurvivalCurve survivalCurve_;
		private RecoveryCurve recoveryCurve_;
		private VolatilityCurve volatilityCurve_;

		private double [] rates_;
		private double [] df_;
		private double [] sigma_;
		private double deltaT_;
		private double [] T_;
		private Dt [] dates_;

		#endregion // Data

	} // class BDTPricer

}
