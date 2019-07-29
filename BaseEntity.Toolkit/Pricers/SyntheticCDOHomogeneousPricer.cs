/*
 * SyntheticCDOHomogeneousPricer.cs
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
	///   <see cref="BaseEntity.Toolkit.Models.HomogeneousBasketModel">Homogeneous Basket Model</see>.</para>
	/// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.SyntheticCDO" />
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Models.HomogeneousBasketModel" />
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
	///   // Initialise cdo, surviavl curves and discountCurve
	///   // ...
	///
	///   // Construct Gaussian copula
	///   Copula copula = new Copula(CopulaType.Gauss);
	///
	///   // Construct single factor correlation
	///   FactorCorrelation correlation =
	///     new SingleFactorCorrelation( names,                       // List of underlying names
	///                                  0.30 );                      // Single correlation factor of 30%
	///
	///   // Create pricer for SyntheticCDO
	///   SyntheticCDOHomogeneousPricer pricer =
	///     new SyntheticCDOHomogeneousPricer( asOf,                  // Pricing date
	///                                        settle,                // Settlement date
	///                                        cdo,                   // CDO Tranche to price
	///                                        discountCurve,         // Discount curve
	///                                        survivalCurves,        // Array of survival curves for each underlying name
	///                                        recoveryCurves,        // Array of matching recovery curves for each survival curve
	///                                        principals,            // Principals for each underlying name
	///                                        copula,                // Copula to use
	///                                        correlation,           // Correlation structure to use
	///                                        1,                     // Time unit size to use in pricing grid
	///                                        TimeUnit.Month );      // Time unit to use in pricing grid
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
	/// <seealso cref="SyntheticCDOHeterogeneousPricer">Heterogeneous CDO Pricer</seealso>
	/// <seealso cref="SyntheticCDOMonteCarloPricer">Monte Carlo CDO Pricer</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.HomogeneousBasketModel">Homogeneous Basket Model</seealso>
	///
  [Serializable]
	public class SyntheticCDOHomogeneousPricer : SyntheticCDOPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(SyntheticCDOHomogeneousPricer));

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
		SyntheticCDOHomogeneousPricer(SyntheticCDO product,
																	SyntheticCDOHomogeneousPricer pricer,
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
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Factor correlations for the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		///
    public
		SyntheticCDOHomogeneousPricer( Dt asOf,
																		 Dt settle,
																		 SyntheticCDO product,
																		 DiscountCurve discountCurve,
																		 SurvivalCurve [] survivalCurves,
																		 RecoveryCurve[] recoveryCurves,
																		 double [] principals,
																		 Copula copula,
																		 FactorCorrelation correlation,
																		 int stepSize,
																		 TimeUnit stepUnit )
			: base(product,
						 new HomogeneousBasketPricer(asOf, settle, product.Maturity,
																				 survivalCurves, recoveryCurves,
																				 principals, copula, correlation,
																				 stepSize, stepUnit,
																				 new double[] {product.Attachment, product.Detachment}),
						 discountCurve, 1.0, null
						 )
		{}

		#endregion // Constructors

	} // class SyntheticCDOHomogeneousPricer

}
