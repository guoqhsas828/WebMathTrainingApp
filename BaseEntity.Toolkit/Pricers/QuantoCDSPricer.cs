/*
 * QuantoCDSPricer.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
	/// Price a <see cref="BaseEntity.Toolkit.Products.QuantoCDS">Quanto CDO</see>
	/// using the <see cref="BaseEntity.Toolkit.Models.QuantoCredit">Quanto Credit Model</see>.
	/// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.QuantoCDS" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.QuantoCredit" />
  /// </remarks>
	/// <seealso cref="BaseEntity.Toolkit.Products.QuantoCDS">Quanto CDO Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.QuantoCredit">Quanto Credit Model</seealso>
  [Serializable]
 	public partial class QuantoCDSPricer : PricerBase, IPricer
	{
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="qcds">Quanto CDS to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="discountCurveR">Discount Curve for pricing</param>
		/// <param name="quantoFactor">Quanto factor</param>
		/// <param name="spotFx">Spot fx</param>
		/// <param name="fxVolatility">fx volatility</param>
		/// <param name="lambda0">lambda at time 0</param>
		/// <param name="kappa">kappa (mean reversion)</param>
		/// <param name="theta">theta (drift)</param>
		/// <param name="sigma">volatility</param>
		/// <param name="recovery">Recovery rate</param>
		///
		public
		QuantoCDSPricer(QuantoCDS qcds,
										Dt asOf,
										Dt settle,
										DiscountCurve discountCurve,
										DiscountCurve discountCurveR,
										double quantoFactor,
										double spotFx,
										double fxVolatility,
										double lambda0,
										double kappa,
										double theta,
										double sigma,
										double recovery)
			: base(qcds, asOf, settle)
		{
			// Use properties for validation
			DiscountCurve = discountCurve;
			DiscountCurveR = discountCurveR;
			QuantoFactor = quantoFactor;
			SpotFx = spotFx;
			FxVolatility = fxVolatility;
			Lambda0 = lambda0;
			Kappa = kappa;
			Theta = theta;
			Sigma = sigma;
			RecoveryRate = recovery;
		}


    /// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			QuantoCDSPricer obj = (QuantoCDSPricer)base.Clone();

			obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
			obj.discountCurveR_ = (DiscountCurve)discountCurveR_.Clone();

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
      if (!IsActive())
        return;

      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      if (discountCurveR_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurveR", String.Format("Invalid discount curve r. Cannot be null"));

      if (spotFx_ < 0.0)
        InvalidValue.AddError(errors, this, "SpotFx", String.Format("Invalid fx rate. Must be non negative, Not {0}", spotFx_));

      if (fxVolatility_ < 0.0)
        InvalidValue.AddError(errors, this, "FxVolatility", String.Format("Invalid fx volatility. Must be non negative, Not {0}", fxVolatility_));

      if (lambda0_ < 0.0)
        InvalidValue.AddError(errors, this, "Lambda0", String.Format("Invalid lambda0. Must be non negative, Not {0}", lambda0_));

      if (theta_ < 0.0)
        InvalidValue.AddError(errors, this, "Theta", String.Format("Invalid theta. Must be non negative, Not {0}", theta_));

      if (sigma_ < 0.0)
        InvalidValue.AddError(errors, this, "Sigma", String.Format("Invalid sigma. Must be non negative, Not {0}", sigma_));

      if (recovery_ < 0.0 || recovery_ > 1.0)
        InvalidValue.AddError(errors, this, "RecoveryRate", String.Format("Invalid recovery rate {0}. Must be >= 0 and <= 1", recovery_));


      return;
    }

    /// <summary>
		///   Calculates present value of cash flows
		/// </summary>
		///
		/// <returns>Pv of cashflows</returns>
		///
    public override double ProductPv()
    {
      return _usePaymentSchedule ? PsProductPv() : CfProductPv();
    }

	  private double PsProductPv()
	  {
	    double[] X0 = {spotFx_, lambda0_};
	    QuantoCDS qcds = (QuantoCDS) Product;
	    CashflowAdapter cfa = new CashflowAdapter(PaymentSchedule);

	    var dates = new Dt[cfa.Count];
      var amounts = new double[cfa.Count];
	    for (int i = 0; i < cfa.Count; ++i)
	    {
	      dates[i] = cfa.GetDt(i);
	      amounts[i] = cfa.GetAccrued(i) + cfa.GetAmount(i);
	    }
	    double pv = QuantoCredit.PremiumPv(AsOf, dates, amounts,
	      discountCurve_,
	      discountCurveR_,
	      18, X0, qcds.RecoveryFx, recovery_,
	      quantoFactor_, fxVolatility_,
	      kappa_, theta_, sigma_);
	    pv -= QuantoCredit.ProtectionPv(AsOf, cfa,
	      discountCurve_,
	      discountCurveR_,
	      18, X0, qcds.RecoveryFx, recovery_,
	      quantoFactor_, fxVolatility_,
	      kappa_, theta_, sigma_);
	    return pv*Notional;
	  }

	  /// <summary>
		///   Calculates present value of fee
		/// </summary>
		///
		/// <returns>Pv of fee leg of quanto CDS</returns>
		///
    public double FeePv()
    {
      return _usePaymentSchedule ? PsFeePv() : CfFeePv();
    }

	  private double PsFeePv()
	  {
	    double[] X0 = new double[] { spotFx_, lambda0_ };

	    QuantoCDS qcds = (QuantoCDS)Product;
	    CashflowAdapter cfa = new CashflowAdapter(PaymentSchedule);

	    var dates = new Dt[cfa.Count];
	    var amounts = new double[cfa.Count];
	    for (int i = 0; i < cfa.Count; ++i)
	    {
	      dates[i] = cfa.GetDt(i);
	      amounts[i] = cfa.GetAccrued(i) + cfa.GetAmount(i);
	    }

      double pv = QuantoCredit.PremiumPv(AsOf, dates, amounts,
	      discountCurve_,
	      discountCurveR_,
	      18, X0, qcds.RecoveryFx, recovery_,
	      quantoFactor_, fxVolatility_,
	      kappa_, theta_, sigma_);
	    return pv * Notional;
    }

    /// <summary>
		///   Calculates present value of protection
		/// </summary>
		///
		/// <returns>Pv of protection leg of quanto CDS</returns>
		///
    public double ProtectionPv()
    {
      return _usePaymentSchedule ? PsProtectionPv() : CfProtectionPv();
    }

	  private double PsProtectionPv()
	  {
	    double[] X0 = new double[] { spotFx_, lambda0_ };

	    QuantoCDS qcds = (QuantoCDS)Product;
	    CashflowAdapter cfa = new CashflowAdapter(PaymentSchedule);
	    double pv = QuantoCredit.ProtectionPv(AsOf, cfa,
	      discountCurve_,
	      discountCurveR_,
	      18, X0, qcds.RecoveryFx, recovery_,
	      quantoFactor_, fxVolatility_,
	      kappa_, theta_, sigma_);
	    return pv * Notional;
    }

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <param name="ps">payment schedule</param>
    /// <param name="from">from date</param>
    /// <returns></returns>
	  public override PaymentSchedule GetPaymentSchedule(
      PaymentSchedule ps, Dt from)
	  {
	    return GetPaymentSchedule(ps, QuantoCDS, from, RecoveryRate);
	  }

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <param name="ps">payment schedule</param>
    /// <param name="qcds">quanto cds</param>
    /// <param name="from">from date</param>
    /// <param name="recoveryRate">recovery rate</param>
    /// <returns></returns>
	  public PaymentSchedule GetPaymentSchedule(PaymentSchedule ps,
	    QuantoCDS qcds, Dt from, double recoveryRate)
	  {
	    if(ps == null)
        ps = new PaymentSchedule();

	    const CycleRule cycleRule = CycleRule.None;

	    CashflowFlag flags = CashflowFlag.IncludeDefaultDate;
	    if (qcds.AccruedOnDefault)
	      flags |= CashflowFlag.AccruedPaidOnDefault;

	    var schedParams = new ScheduleParams(qcds.Effective, 
        qcds.FirstPrem, Dt.Empty, qcds.Maturity, qcds.Freq,
	      qcds.BDConvention, qcds.Calendar, cycleRule, flags);

	    var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);

	    const double principal = 0.0;

	    ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from,
	      Dt.Empty, schedule, qcds.Ccy, qcds.DayCount,
	      new PaymentGenerationFlag(flags, false, false), qcds.Premium,
	      null, principal, null, null, null, null, true));

	    ps.GetRecoveryPayments(Settle, false, dt => recoveryRate, false, flags);

	    return ps;
	  }

		#endregion // Methods

		#region Properties

		/// <summary>
		///   QuantoCDS
		/// </summary>
		public QuantoCDS QuantoCDS
		{
			get { return (QuantoCDS)Product; }
		}

    /// <summary>
    /// Payment schedule
    /// </summary>
	  public PaymentSchedule PaymentSchedule
	  {
      get { return GetPaymentSchedule(_paymentSchedule, AsOf); }
	  }


    /// <summary>
    ///   Discount Curve for premium currency
    /// </summary>
		public DiscountCurve DiscountCurve
		{
			get { return discountCurve_; }
			set {
				discountCurve_ = value;
			}
		}


    /// <summary>
    ///   Discount Curve for insured-debt (recovery) currency
    /// </summary>
		public DiscountCurve DiscountCurveR
		{
			get { return discountCurveR_; }
			set {
				discountCurveR_ = value;
			}
		}


		/// <summary>
		///   Dependence parameter linking FX rate to default rate
		/// </summary>
		public double QuantoFactor
		{
			get { return quantoFactor_; }
			set { quantoFactor_ = value; }
		}


		/// <summary>
		///   Current spot FX rate, converting insured-debt currency to premium currency
		/// </summary>
		public double SpotFx
		{
			get { return spotFx_; }
			set {
				spotFx_ = value;
			}
		}


		/// <summary>
		///   Volatility of the FX rate (percentage)
		/// </summary>
		public double FxVolatility
		{
			get { return fxVolatility_; }
			set {
				fxVolatility_ = value;
			}
		}


		/// <summary>
		///   Current level of the default rate process
		/// </summary>
		public double Lambda0
		{
			get { return lambda0_; }
			set {
				lambda0_ = value;
			}
		}


		/// <summary>
		///   Mean reversion parameter for CIR model for default rate
		/// </summary>
		public double Kappa
		{
			get { return kappa_; }
			set { kappa_ = value; }
		}


		/// <summary>
		///   Long run mean parameter for CIR model for default rate
		/// </summary>
		public double Theta
		{
			get { return theta_; }
			set {
				theta_ = value;
			}
		}


		/// <summary>
		///   Volatility parameter for CIR model for default rate
		/// </summary>
		public double Sigma
		{
			get { return sigma_; }
			set {
				sigma_ = value;
			}
		}


		/// <summary>
    ///   Recovery rate
    /// </summary>
    public double RecoveryRate
		{
			get { return recovery_; }
			set {
				recovery_ = value;
			}
		}

		#endregion // Properties

		#region Data

	  private PaymentSchedule _paymentSchedule = null;
    private DiscountCurve discountCurve_;
		private DiscountCurve discountCurveR_;
		private double quantoFactor_;
		private double spotFx_;
		private double fxVolatility_;
		private double lambda0_;
		private double kappa_;
		private double theta_;
		private double sigma_;
    private double recovery_;
	  private bool _usePaymentSchedule;

		#endregion // Data

		#region Calibrator
		/// <summary>
		///   Calculate the implied model coefficients
		/// </summary>
		///
		/// <param name="pricers">Array of Quanto CDS pricers</param>
		/// <param name="weights">Array of weights</param>
		/// <param name="initialX">Starting X points</param>
		/// <param name="deltaX">Searching steps</param>
		/// <param name="upperX">Upper bounds</param>
		/// <param name="lowerX">Lower bounds</param>
		///
		/// <returns>Model coefficients</returns>
		///
		/// <remarks>
		///   This function returns an array of five elements,
		///   <c>{fxVolatility, lambda0, kappa, theta, sigma}</c>.
		/// </remarks>
    public static double []
		ImpliedParams( QuantoCDSPricer [] pricers,
									 double [] weights,
									 double [] initialX,
									 double [] deltaX,
									 double [] upperX,
									 double [] lowerX )
		{
		  Optimizer calibrator = new Optimizer( pricers, weights );
			double [] result = calibrator.Fit( initialX, deltaX, upperX, lowerX );
			return result;
		}

		//-
		// Helper class optimizers
		//-
		unsafe class Optimizer {
			public Optimizer( QuantoCDSPricer [] pricers,
												double [] weights )
			{
			  pricers_ = pricers;
				weights_ = weights;
				return;
			}

			private double
			Evaluate( double* x )
			{
				double diff = 0.0;
				int N = pricers_.Length;
				for( int i = 0; i < N; ++i ) {
					QuantoCDSPricer pricer = pricers_[i];
					pricer.FxVolatility = x[0];
					pricer.Lambda0 = x[1];
					pricer.Kappa = x[2];
					pricer.Theta = x[3];
					pricer.Sigma = x[4];
					double pv = pricer.ProductPv();
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
			  int numFit = initialX.Length;
				NelderMeadeSimplex opt = new NelderMeadeSimplex(numFit);
				opt.setInitialPoint(initialX);

				if( deltaX != null )
					opt.setInitialDeltaX(deltaX);
				if( upperX != null )
					opt.setUpperBounds( upperX );
				if( lowerX != null )
					opt.setLowerBounds( lowerX );

				double tolF_ = 1.0E-6 ;
				double tolX_ = 1.0E-6 ;
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
					double f = Math.Sqrt( Evaluate( x ) );
					if( f < tolF_ )
						repeat = 1;
				} while( --repeat > 0 );

				double[] result = new double[numFit];
				for( int i = 0; i < numFit; ++i )
					result[i] = x[i];

				fn_ = null;
				objFn_ = null;

				return result;
			}

			private QuantoCDSPricer [] pricers_;
			private double [] weights_;

			//private double fxVolatility_;
			//private double lambda0_;
			//private double kappa_;
			//private double theta_;
			//private double sigma_;

			private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
			private Double_Vector_Fn fn_;
		};
		#endregion // Calibrator

	} // class QuantoCDSPricer

}
