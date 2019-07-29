/*
 * CCCPricer.cs
 *
 *
 */
using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  ///
	/// <summary>
	///   Abstract parent for cross-currency contingent pricers
	/// </summary>
	///
  [Serializable]
 	public abstract class CCCPricer : PricerBase, IPricer
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		///
		protected
		CCCPricer(IProduct product)
			: base(product)
		{}


		/// <summary>
		///   Constructor.
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="ccy">Currency</param>
		/// <param name="fxCcy">Foreign currency</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		/// <param name="r0">Initial level for domestic short rate</param>
		/// <param name="rKappa">Mean reversion for domestic interest rate</param>
		/// <param name="rTheta">Long run mean for domestic interest rate</param>
		/// <param name="rSigma">Volatility curve of domestic interest rate</param>
		/// <param name="rf0">Initial level for foreign short rate</param>
		/// <param name="rfKappa">Mean reversion for foreign interest rate</param>
		/// <param name="rfTheta">Long run mean for foreign interest rate</param>
		/// <param name="rfSigma">Volatility curve of foreign interest rate</param>
		/// <param name="fx0">Initial level of fx rate</param>
		/// <param name="fxSigma">Volatility curve of fx</param>
		/// <param name="l0">Initial level for default rate intensity</param>
		/// <param name="lKappa">Mean reversion for default rate intensity</param>
		/// <param name="lTheta">Long run mean for default rate intensity</param>
		/// <param name="lSigma">Volatility of default rate intensity</param>
		/// <param name="correlation">Correlation coefficient for interest rates and default intensity</param>
		///
		protected
		CCCPricer(IProduct product, Dt asOf, Dt settle, Currency ccy, Currency fxCcy,
							int stepSize, TimeUnit stepUnit,
							double r0, double rKappa, Curve rTheta, Curve rSigma,
							double rf0, double rfKappa, Curve rfTheta, Curve rfSigma,
							double fx0, Curve fxSigma,
							double l0, double lKappa, double lTheta, double lSigma,
							double [,] correlation
							)
			: base(product, asOf, settle)
		{
			// Set data using properties for validation
			Ccy = ccy;
			FxCcy = fxCcy;
			StepSize = stepSize;
			StepUnit = stepUnit;
			R0 = r0;
			RKappa = rKappa;
			RTheta = rTheta;
			RSigma = rSigma;
			Rf0 = rf0;
			RfKappa = rfKappa;
			RfTheta = rfTheta;
			RfSigma = rfSigma;
			Fx0 = fx0;
			FxSigma = fxSigma;
			L0 = l0;
			LKappa = lKappa;
			LTheta = lTheta;
			LSigma = lSigma;
			Correlation = correlation;
		}


    /// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			CCCPricer obj = (CCCPricer)base.Clone();

			obj.RTheta = (Curve)rTheta_.Clone();
			obj.RSigma = (Curve)rSigma_.Clone();
			obj.RfTheta = (Curve)rfTheta_.Clone();
			obj.RfSigma = (Curve)rfSigma_.Clone();
			obj.FxSigma = (Curve)fxSigma_.Clone();

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

      // Invalid initial domestic rate
      if (r0_ <= 0.0)
        InvalidValue.AddError(errors, this, "R0", String.Format("Invalid initial domestic rate {0}. Must be positive", r0_));

      // Invalid initial hazrad rate
      if (l0_ <= 0.0)
        InvalidValue.AddError(errors, this, "L0", String.Format("Invalid initial hazard rate {0}. Must be positive", r0_));

      // Invalid initial foreign rate
      if (rf0_ <= 0.0)
        InvalidValue.AddError(errors, this, "Rf0", String.Format("Invalid foreign domestic rate {0}. Must be positive", rf0_));

      // Invalid initial exchange rate
      if (fx0_ <= 0.0)
        InvalidValue.AddError(errors, this, "Fx0", String.Format("Invalid exchange rate {0}. Must be positive", rf0_));

      // Invalid correlation
      if ((correlation_.GetLength(0) != 4) || (correlation_.GetLength(1) != 4))
        InvalidValue.AddError(errors, this, "Correlation", String.Format("Correlation matrix must be 4x4"));
      for (int i = 0; i < 4; i++)
      {
        if (correlation_[i, i] != 1.0)
          InvalidValue.AddError(errors, this, "Correlation", String.Format("Out of range error: correlation with self ({0}) must be 1", correlation_[i, i]));
        for (int j = 0; j < i; j++)
        {
          if (correlation_[j, i] != correlation_[i, j])
            InvalidValue.AddError(errors, this, "Correlation", String.Format("Out of range error: correlation matrix must be symmetric"));
          if (Math.Abs(correlation_[j, i]) > 1)
            InvalidValue.AddError(errors, this, "Correlation", String.Format("Out of range error: correlation ({0}) must be between +1 and -1", correlation_[i, j]));
        }
      }

      return;
    }

    /// <summary>
		///   Return risky (zero recovery) discount factor for cross-currency contingent cashflow
		/// </summary>
		///
		/// <param name="settle">Settlement date</param>
		/// <param name="date">Date for discount factor</param>
		/// <param name="ccy">Currency of discount factor</param>
		///
		/// <returns>Discount factor for cross-currency contingent cashflow</returns>
		///
    public double
		Df(Dt settle, Dt date, Currency ccy)
		{
			// The correlations
			double rho12 = Correlation[0,1];
			double rho13 = Correlation[0,2];
			double rho14 = Correlation[0,3];
			double rho23 = Correlation[1,2];
			double rho24 = Correlation[1,3];
			double rho34 = Correlation[2,3];

			// Weighting for the stochastic vol piece
			double alpha = 0.5 * (1.0 / RTheta.Interpolate(AsOf));
			double beta = 0.5 * (1.0 / RfTheta.Interpolate(AsOf));

			// Return df
			return CCCModel.Df(settle, date, (ccy == Ccy) ? 1 : 2,
												 StepSize, StepUnit,
												 R0, Rf0, Fx0, L0,
												 alpha, beta,
												 rho12, rho13, rho14, rho23, rho24, rho34,
												 RKappa, RTheta, RSigma,
												 RfKappa, RfTheta, RfSigma,
												 FxSigma,
												 LKappa, LTheta, LSigma);
		}

		#endregion // Methods

		#region Properties

		/// <summary>
		///   Domestic Currency
		/// </summary>
		public Currency Ccy
		{
			get { return ccy_; }
			set { ccy_ = value; }
		}


		/// <summary>
		///   Foreign Currency
		/// </summary>
		public Currency FxCcy
		{
			get { return fxCcy_; }
			set { fxCcy_ = value; }
		}


		/// <summary>
		///   Step size for pricing grid
		/// </summary>
		public int StepSize
		{
			get { return stepSize_; }
			set { stepSize_ = value; }
		}


		/// <summary>
		///   Step unit for pricing grid
		/// </summary>
		public TimeUnit StepUnit
		{
			get { return stepUnit_; }
			set { stepUnit_ = value; }
		}


		/// <summary>
		///   Initial domestic interest rate
		/// </summary>
		public double R0
		{
			get { return r0_; }
			set { r0_ = value; }
		}


		/// <summary>
		///   Mean reversion for home interest rate
		/// </summary>
		public double RKappa
		{
			get { return rKappa_; }
			set { rKappa_ = value; }
		}


		/// <summary>
		///   Long run mean for home interest rate
		/// </summary>
		public Curve RTheta
		{
			get { return rTheta_; }
			set { rTheta_ = value; }
		}


		/// <summary>
		///   Volatility curve of home interest rate
		/// </summary>
		public Curve RSigma
		{
			get { return rSigma_; }
			set { rSigma_ = value; }
		}


		/// <summary>
		///   Initial foreign interest rate
		/// </summary>
		public double Rf0
		{
			get { return rf0_; }
			set { rf0_ = value; }
		}


		/// <summary>
		///   Mean reversion for foreign interest rate
		/// </summary>
		public double RfKappa
		{
			get { return rfKappa_; }
			set { rfKappa_ = value; }
		}


		/// <summary>
		///   Long run mean for foreign interest rate
		/// </summary>
		public Curve RfTheta
		{
			get { return rfTheta_; }
			set { rfTheta_ = value; }
		}


		/// <summary>
		///   Volatility curve of foreign interest rate
		/// </summary>
		public Curve RfSigma
		{
			get { return rfSigma_; }
			set { rfSigma_ = value; }
		}


		/// <summary>
		///   Fx rate
		/// </summary>
		public double Fx0
		{
			get { return fx0_; }
			set { fx0_ = value; }
		}


		/// <summary>
		///   Volatility curve of foreign interest rate
		/// </summary>
		public Curve FxSigma
		{
			get { return fxSigma_; }
			set { fxSigma_ = value; }
		}


		/// <summary>
		///   Initial hazard rate
		/// </summary>
		public double L0
		{
			get { return l0_; }
			set { l0_ = value; }
		}


		/// <summary>
		///   Mean reversion for default rate intensity
		/// </summary>
		public double LKappa
		{
			get { return lKappa_; }
			set { lKappa_ = value; }
		}


		/// <summary>
		///   Long run mean for default rate intensity
		/// </summary>
		public double LTheta
		{
			get { return lTheta_; }
			set { lTheta_ = value; }
		}


		/// <summary>
		///   Volatility of default rate intensity
		/// </summary>
		public double LSigma
		{
			get { return lSigma_; }
			set { lSigma_ = value; }
		}


		/// <summary>
		///   Correlation coefficient between the two interest rates
		/// </summary>
		public double [,] Correlation
		{
			get { return correlation_; }
			set
			{
				if ( (value.GetLength(0) != 4) || (value.GetLength(1) != 4) )
					throw new ToolkitException("Correlation matrix must be 4x4");
				for( int i = 0; i < 4; i++ )
				{
					if( value[i,i] != 1.0 )
						throw new ToolkitException(String.Format("Out of range error: correlation with self ({0}) must be 1", value[i,i]));
					for( int j = 0; j < i; j++ )
					{
						if( value[j,i] != value[i,j] )
							throw new ToolkitException("Out of range error: correlation matrix must be symmetric");
						if( Math.Abs(value[j,i]) > 1 )
							throw new ToolkitException(String.Format("Out of range error: correlation ({0}) must be between +1 and -1", value[i,j]));
					}
				}
				correlation_ = value;
			}
		}

		/// <summary>
		///   string representation of pricer
		/// </summary>
		public override string ToString()
		{
      string str =
        "  Product = " + Product.Description +
        "; AsOf = " + AsOf +
        "; Settle = " + Settle +
        "; Ccy = " + ccy_ +
        "; FxCcy = " + fxCcy_ +
				"; StepSize = " + stepSize_ +
				"; StepUnit = " + stepUnit_ +
				"; R0 = " + r0_ +
				"; RKappa = " + rKappa_ +
				"; RTheta = " + rTheta_ +
				"; RSigma = " + rSigma_ +
				"; Rf0 = " + rf0_ +
				"; RfKappa = " + rfKappa_ +
				"; RfTheta = " + rfTheta_ +
				"; RfSigma = " + rfSigma_ +
				"; Fx0 = " + fx0_ +
				"; FxSigma = " + fxSigma_ +
				"; L0 = " + l0_ +
				"; LKappa = " + lKappa_ +
				"; LTheta = " + lTheta_ +
				"; LSigma = " + lSigma_;

      return str;
		}

		#endregion // Properties

		#region Data

		private Currency ccy_;
		private Currency fxCcy_;
		private int stepSize_;
		private TimeUnit stepUnit_;
		private double r0_;
		private double rKappa_;
		private Curve  rTheta_;
		private Curve  rSigma_;
		private double rf0_;
		private double rfKappa_;
		private Curve  rfTheta_;
		private Curve  rfSigma_;
		private double fx0_;
		private Curve  fxSigma_;
		private double l0_;
		private double lKappa_;
		private double lTheta_;
		private double lSigma_;
		private double [,] correlation_;

		#endregion // Data

	} // class CCCPricer

}
