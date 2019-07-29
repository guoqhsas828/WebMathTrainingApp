/*
 * SyntheticCDOBaseCorrelationPricer.cs
 *
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
	/// <summary>
	///   <para>Price a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO</see> using the
	///   base correlations and the <see cref="BaseEntity.Toolkit.Models.HeterogeneousBasketModel">Heterogeneous Basket Model</see>.</para>
	/// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.SyntheticCDO" />
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Models.HeterogeneousBasketModel" />
  /// </remarks>
	/// <example>
	/// <para>The following example demonstrates constructing a pricer for a Synthetic CDO.</para>
	/// <code language="C#">
	///   Dt asOf = Dt.Today();                                       // Pricing as of today
	///   Dt settle = Dt.Add(asOf, 1);                                // Settle T+1
	///   SyntheticCDO cdo;                                           // CDO to price
	///   string [] names;                                            // List of underlying names
	///   double [] principals;                                       // Array of principals for each underlying name
	///   SurvivalCurve [] survivalCurves;                            // Array of survival curves for each underlying name
	///   RecoveryCurve [] recoveryCurves;                            // Array of recovery curves for each underlying name
	///   DiscountCurve discountCurve;                                // Discount curve to use
	///
	///   // Initialise cdo, survival curves and discountCurve
	///   // ...
	///
	///   // Create pricer for SyntheticCDO
	///   SyntheticCDOBaseCorrelationPricer pricer =
	///     new SyntheticCDOBaseCorrelationPricer( asOf,                // Pricing date
	///                                            settle,              // Settlement date
	///                                            cdo,                 // CDO Tranche to price
	///                                            discountCurve,       // Discount curve
	///                                            survivalCurves,      // Array of survival curves for each underlying name
	///                                            recoveryCurves,      // Array of matching recovery curves for each survival curve
	///                                            principals,          // Principals for each underlying name
	///                                            0.20,                // 20% attachment base correlation
	///                                            0.30,                // 30% detachment base correlaction
	///                                            1,                   // Time unit size to use in pricing grid
	///                                            TimeUnit.Month );    // Time unit to use in pricing grid
	///
	///   // Calculate and print results
	///   Console.WriteLine( "Protection PV = {0}, Fee PV = {1}, PV = {2}, Accrued = {3},
	///                      pricer.ProtectionPv(), pricer.FeePv(), pricer.Pv(), pricer.Accrued() );
	///   Console.WriteLine( "Break even premium = {0}, Duration = {1}",
	///                      pricer.BreakEvenPremium(), pricer.RiskyDuration() );
  /// </code>
	/// </example>
	///
	/// <seealso cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche Product</seealso>
	/// <seealso cref="BaseCorrelationBasketPricer">BaseCorrelation Basket Pricer</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.HeterogeneousBasketModel">Heterogeneous Basket Model</seealso>
	///
  [Serializable]
	public class SyntheticCDOBaseCorrelationPricer : SyntheticCDOPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(SyntheticCDOBaseCorrelationPricer));

		#region Constructors

    /// <summary>
		///   Constructor from a basket model
		/// </summary>
		///
		/// <remarks>
		///   <para>This allows construction of a tranche pricer based on a shared basket model.</para>
		///
		///   <para>This is useful when pricing a full structure of tranches on the same underlying
		///   assets and provides some efficiencies at the cost of flexibility when using the pricer.</para>
		/// </remarks>
		///
		/// <param name="product">CDO tranche to price</param>
		/// <param name="pricer">The CDO pricer to share the basket model</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		///
    public
		SyntheticCDOBaseCorrelationPricer(SyntheticCDO product,
																			SyntheticCDOBaseCorrelationPricer pricer,
																			DiscountCurve discountCurve)
			: base(product, pricer.Basket, discountCurve, 1.0, null)
		{}


    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="product">CDO tranche to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Principals (face values) associated with individual names</param>
		/// <param name="correlation">Base correlations</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		///
    public
		SyntheticCDOBaseCorrelationPricer( Dt asOf,
																			 Dt settle,
																			 SyntheticCDO product,
																			 DiscountCurve discountCurve,
																			 SurvivalCurve [] survivalCurves,
																			 RecoveryCurve[] recoveryCurves,
																			 double [] principals,
																			 BaseCorrelationObject correlation,
																			 int stepSize,
																			 TimeUnit stepUnit )
			: base(product,
						 new BaseCorrelationBasketPricer(asOf, settle, product.Maturity,
																						 discountCurve, survivalCurves, recoveryCurves,
																						 principals,
																						 correlation,
																						 product.Attachment, product.Detachment,
																						 stepSize, stepUnit),
						 discountCurve, 1.0, null
						 )
		{}

		#endregion // Constructors

		#region properties

		/// <summary>
		///   Re-scale strike points every time we price.
		/// </summary>
		public bool RescaleStrike
		{
			get { return ((BaseCorrelationBasketPricer)Basket).RescaleStrike; }
			set { ((BaseCorrelationBasketPricer)Basket).RescaleStrike = value; }
		}

		#endregion // Properties

	} // class SyntheticCDOBaseCorrelationPricer

}
