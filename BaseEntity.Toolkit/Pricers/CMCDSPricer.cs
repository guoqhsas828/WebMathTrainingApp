/*
 * CMCDSPricer.cs
 *
 *
 */

using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{

  ///
	/// <summary>
	///   Price a <see cref="BaseEntity.Toolkit.Products.CMCDS">Constant Maturity CDS Product</see>
	///   using the <see cref="BaseEntity.Toolkit.Models.CMCDSModel">CMCDS Model</see>.
	/// </summary>
	///
	/// <seealso cref="BaseEntity.Toolkit.Products.CMCDS">Constant Maturity CDS Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.CMCDSModel">CMCDS Model</seealso>
	///
  [Serializable]
 	public class CMCDSPricer : PricerBase, IPricer
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="cmcds">CMCDS to price</param>
		///
		public
		CMCDSPricer(CMCDS cmcds)
			: base(cmcds)
		{}


		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="cmcds">Constant Maturity CDS product</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="currentCoupon">Premium for next payment (needed for PV after effective date)</param>
		/// <param name="discountCurve">Discount curve for pricing</param>
		/// <param name="kappa">Hazard rate mean reversion</param>
		/// <param name="theta">Hazard rate long term mean</param>
		/// <param name="sigma">Hazard rate volatility</param>
		/// <param name="lambda0">Initial level of hazard rate</param>
		/// <param name="recovery">Recovery rate</param>
		///
		public
		CMCDSPricer(CMCDS cmcds,
								Dt asOf,
								Dt settle,
								double currentCoupon,
								DiscountCurve discountCurve,
								double kappa,
								double theta,
								double sigma,
								double lambda0,
								double recovery)
			: base(cmcds, asOf, settle)

		{
			// Use properties for validation
			CurrentCoupon = currentCoupon;
			DiscountCurve = discountCurve;
			Kappa = kappa;
			Theta = theta;
			Sigma = sigma;
			Lambda0 = lambda0;
			RecoveryRate = recovery;
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			CMCDSPricer obj = (CMCDSPricer)base.Clone();

			obj.discountCurve_ = (discountCurve_ != null) ? (DiscountCurve)discountCurve_.Clone() : null;

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

      if (currentCoupon_ < 0.0)
        InvalidValue.AddError(errors, this, "CurrentCoupon", String.Format("Invalid current coupon. Must be non negative, Not {0}", currentCoupon_));

      if (lambda0_ < 0.0)
        InvalidValue.AddError(errors, this, "Lambda0", String.Format("Invalid current coupon. Must be non negative, Not {0}", lambda0_));

      if (theta_ < 0.0)
        InvalidValue.AddError(errors, this, "Theta", String.Format("Invalid theta. Must be non negative, Not {0}", theta_));

      if (sigma_ < 0.0)
        InvalidValue.AddError(errors, this, "Sigma", String.Format("Invalid sigma. Must be non negative, Not {0}", sigma_));

      if (recovery_ < 0.0 || recovery_ > 1.0)
        InvalidValue.AddError(errors, this, "RecoveryRate", String.Format("Invalid recovery rate {0}. Must be >= 0 and <= 1", recovery_));


      return;
    }

    /// <summary>
		///   Calculates Fair market participation rate for CMCDS
		/// </summary>
		///
		/// <returns>Participation rate for CMCDS</returns>
		///
    public double
		Participation()
		{
			double p = Toolkit.Models.CMCDSModel.Participation(Settle,
																													CMCDS.Effective,
																													CMCDS.ResetTenor,
																													CMCDS.FirstPrem,
																													CMCDS.Maturity,
																													CMCDS.DayCount,
																													CMCDS.Freq,
																													CMCDS.BDConvention,
																													CMCDS.Calendar,
																													CMCDS.AccruedOnDefault,
																													RecoveryRate,
																													DiscountCurve,
																													CMCDS.Cap,
																													CMCDS.Floor,
																													Kappa,
																													Theta,
																													Sigma,
																													Lambda0);
			return p;
		}

    /// <summary>
		///   Calculates Fair market value for existing CMCDS
		/// </summary>
		///
		/// <returns>Market value of existing CMCDS</returns>
		///
    public override double
		ProductPv()
		{
			double p = Toolkit.Models.CMCDSModel.Pv(Settle,
																							 CMCDS.Effective,
																							 CMCDS.Participation,
																							 CurrentCoupon,
																							 CMCDS.ResetTenor,
																							 CMCDS.FirstPrem,
																							 CMCDS.Maturity,
																							 CMCDS.DayCount,
																							 CMCDS.Freq,
																							 CMCDS.BDConvention,
																							 CMCDS.Calendar,
																							 CMCDS.AccruedOnDefault,
																							 RecoveryRate,
																							 DiscountCurve,
																							 CMCDS.Cap,
																							 CMCDS.Floor,
																							 Kappa,
																							 Theta,
																							 Sigma,
																							 Lambda0);
			return p * Notional;
		}


    /// <summary>
		///   Calculates Fair market value for protection leg of existing CMCDS
		/// </summary>
		///
		/// <returns>Market value of protection leg of existing CMCDS</returns>
		///
    public double
		Protection()
		{
			double p = Toolkit.Models.CMCDSModel.Protection(Settle,
																											 CMCDS.Effective,
																											 CurrentCoupon,
																											 CMCDS.ResetTenor,
																											 CMCDS.FirstPrem,
																											 CMCDS.Maturity,
																											 CMCDS.DayCount,
																											 CMCDS.Freq,
																											 CMCDS.BDConvention,
																											 CMCDS.Calendar,
																											 CMCDS.AccruedOnDefault,
																											 RecoveryRate,
																											 DiscountCurve,
																											 CMCDS.Cap,
																											 CMCDS.Floor,
																											 Kappa,
																											 Theta,
																											 Sigma,
																											 Lambda0);
			return p * Notional;
		}


    /// <summary>
		///   Calculates Fair market value for fee leg of existing CMCDS
		/// </summary>
		///
		/// <returns>Market value of fee leg of existing CMCDS</returns>
		///
    public double
		Fee()
		{
			double p = Toolkit.Models.CMCDSModel.Fee(Settle,
																								CMCDS.Effective,
																								CMCDS.Participation,
																								CurrentCoupon,
																								CMCDS.ResetTenor,
																								CMCDS.FirstPrem,
																								CMCDS.Maturity,
																								CMCDS.DayCount,
																								CMCDS.Freq,
																								CMCDS.BDConvention,
																								CMCDS.Calendar,
																								CMCDS.AccruedOnDefault,
																								RecoveryRate,
																								DiscountCurve,
																								CMCDS.Cap,
																								CMCDS.Floor,
																								Kappa,
																								Theta,
																								Sigma,
																								Lambda0);
			return p * Notional;
		}

		#endregion // Methods

		#region Properties

		/// <summary>
		///   Product
		/// </summary>
		public CMCDS CMCDS
		{
			get { return (CMCDS)Product; }
			set { Product = value; }
		}


		/// <summary>
		///   Coupon (premium) set for next payment.  Used in
		///   MTM and PV calculations after the effective date.
		/// </summary>
		public double CurrentCoupon
		{
			get { return currentCoupon_; }
			set { currentCoupon_ = value; }
		}


		/// <summary>
		///   Initial level for the hazard rate process.
		/// </summary>
		public double Lambda0
		{
			get { return lambda0_; }
			set { lambda0_ = value; }
		}


		/// <summary>
		///   Hazard rate mean reversion
		/// </summary>
		public double Kappa
		{
			get { return kappa_; }
			set { kappa_ = value; }
		}


		/// <summary>
		///   Hazard rate long term mean
		/// </summary>
		public double Theta
		{
			get { return theta_; }
			set { theta_ = value; }
		}


		/// <summary>
		///   Hazard rate volatility
		/// </summary>
		public double Sigma
		{
			get { return sigma_; }
			set { sigma_ = value; }
		}


		/// <summary>
		///   Discount curve for pricing.
		/// </summary>
		public DiscountCurve DiscountCurve
		{
			get { return discountCurve_; }
			set { discountCurve_ = value; }
		}


		/// <summary>
		///   Recovery rate
		/// </summary>
		public double RecoveryRate
		{
			get { return recovery_; }
			set { recovery_ = value; }
		}

		#endregion // Properties

		#region Data

		private double currentCoupon_;
		private DiscountCurve discountCurve_;
		private double kappa_;
		private double theta_;
		private double sigma_;
		private double lambda0_;
		private double recovery_;

		#endregion // Data

	} // class CMCDSPricer

} 
