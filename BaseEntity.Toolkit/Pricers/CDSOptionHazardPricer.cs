/*
 * CDSOptionHazardPricer.cs
 *
 *
 */

using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{

  ///
	/// <summary>
	///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CDSOption">CDS Option</see> using the
	///   <see cref="BaseEntity.Toolkit.Models.CDSOptionHazardModel">CDS Option Hazard Rate Model</see>.</para>
	/// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDSOption" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.CDSOptionHazardModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDSOption">CDS Option Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.CDSOptionHazardModel">CDS Option Hazard Rate Model</seealso>
  [Serializable]
 	public class CDSOptionHazardPricer : PricerBase, IPricer
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="option">CDS Option to price</param>
		///
		public
		CDSOptionHazardPricer(CDSOption option)
			: base(option)
		{}


		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="option">CDS option to price</param>
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount curve for pricing.</param>
		/// <param name="recovery">Recovery rate</param>
		/// <param name="kappa">Hazard rate mean reversion</param>
		/// <param name="theta">Hazard rate long term mean</param>
		/// <param name="sigma">Hazard rate volatility</param>
		/// <param name="lambda0">Hazard rate initial level</param>
		///
		public
		CDSOptionHazardPricer(CDSOption option,
													Dt asOf,
													Dt settle,
													DiscountCurve discountCurve,
													double recovery,
													double kappa,
													double theta,
													double sigma,
													double lambda0)
			: base( option, asOf, settle)
		{
			// Use properties for validation
			DiscountCurve = discountCurve;
			RecoveryRate = recovery;
			Kappa = kappa;
			Theta = theta;
			Sigma = sigma;
			Lambda0 = lambda0;
		}


    /// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
			CDSOptionHazardPricer obj = (CDSOptionHazardPricer)base.Clone();

			obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();

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
      
      // Invalid discount curve
      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      
      // Invalid hazard rate
      if (lambda0_ < 0.0)
        InvalidValue.AddError(errors, this, "Lambda0", String.Format("Invalid Lambda0 ({0}). Must be >= 0", lambda0_));
      
      // Invalid recovery rate
      if (recovery_ < 0.0 || recovery_ > 1.0)
        InvalidValue.AddError(errors, this, "RecoveryRate", String.Format("Invalid recovery rate. Must be between 0 and 1, not {0}", recovery_));

      // Invalid hazard rate longterm mean
      if (theta_ < 0.0)
        InvalidValue.AddError(errors, this, "Theta", String.Format("Invalid Lambda0 ({0}). Must be >= 0", theta_));

      // Invalid hazard rate volatility
      if (sigma_ < 0.0)
        InvalidValue.AddError(errors, this, "Sigma", String.Format("Invalid sigma ({0}). Must be >= 0", sigma_));
      

      return;
    }


    /// <summary>
		///   Calculates Fair value for CDS Option using a CIR model for the hazard rate
		/// </summary>
		///
		/// <returns>Pv of CDS Option</returns>
		///
		public override double ProductPv()
		{
			if( Dt.Cmp(CDSOption.Expiration, AsOf) < 0 )
				throw new ToolkitException(String.Format("Out of range error: As-of ({0}) must be prior to expiration ({1})", AsOf, CDSOption.Expiration));
			double pv = Toolkit.Models.CDSOptionHazardModel.Pv(AsOf,
																													CDSOption.Style,
																													CDSOption.Type,
																													CDSOption.Expiration,
																													CDSOption.Strike,
																													CDSOption.CDS.FirstPrem,
																													CDSOption.CDS.Maturity,
																													CDSOption.CDS.DayCount,
																													CDSOption.CDS.Freq,
																													CDSOption.CDS.BDConvention,
																													CDSOption.CDS.Calendar,
																													CDSOption.CDS.AccruedOnDefault,
																													RecoveryRate,
																													DiscountCurve,
																													Kappa,
																													Theta,
																													Sigma,
																													Lambda0);
			return pv * Notional;
		}

		#endregion // Methods

		#region Properties

		/// <summary>
		///   Product, as CDSOption
		/// </summary>
		public CDSOption CDSOption
		{
			get { return (CDSOption)Product; }
			set { Product = value; }
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

		#endregion // Properties

		#region Data

		private DiscountCurve discountCurve_;
		private double recovery_;
		private double kappa_;
		private double theta_;
		private double sigma_;
		private double lambda0_;

		#endregion // Data

	} // class CDSOptionHazardPricer

}
