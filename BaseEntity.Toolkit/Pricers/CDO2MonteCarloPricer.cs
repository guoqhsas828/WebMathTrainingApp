/*
 * SyntheticCDO2MonteCarloPricer.cs
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
	/// <para>Price a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">CDO of CDOs</see> using the
	/// <see cref="BaseEntity.Toolkit.Models.MonteCarloCDOSquaredModel">Monte Carlo CDO2 Basket Model</see>.</para>
	/// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.SyntheticCDO" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.MonteCarloCDOSquaredModel" />
  /// </remarks>
	/// <seealso cref="BaseEntity.Toolkit.Products.SyntheticCDO">CDO of CDOs Tranche Product</seealso>
	/// <seealso cref="MonteCarloCDO2BasketPricer">Monte Carlo CDO2 Basket Pricer</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.MonteCarloCDOSquaredModel">Monte Carlo CDO2 Basket Model</seealso>
  [Serializable]
	public class CDO2MonteCarloPricer : SyntheticCDOPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CDO2MonteCarloPricer));

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
		/// <param name="product">CDO2 tranche to price</param>
		/// <param name="pricer">The CDO2 pricer to share the basket model</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		///
    public
		CDO2MonteCarloPricer(SyntheticCDO product,
												 CDO2MonteCarloPricer pricer,
												 DiscountCurve discountCurve)
			: base(product, pricer.Basket, discountCurve, 1.0, null)
		{}


    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="product">CDO2 tranche to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Principals of individual names in child CDOs</param>
		/// <param name="attachments">Attachment points of child CDOs</param>
		/// <param name="detachments">Detachment points of child CDOs</param>
		/// <param name="crossSubordination">If true, with cross subordination</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Correlation of the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		///
    public
		CDO2MonteCarloPricer( Dt asOf,
													Dt settle,
													SyntheticCDO product,
													DiscountCurve discountCurve,
													SurvivalCurve [] survivalCurves,
													RecoveryCurve[] recoveryCurves,
													double [] principals,
													double [] attachments,
													double [] detachments,
													bool crossSubordination,
													Copula copula,
													GeneralCorrelation correlation,
													int stepSize,
													TimeUnit stepUnit,
													int sampleSize )
			: base(product,
						 new MonteCarloCDO2BasketPricer(asOf, settle, product.Maturity,
																						survivalCurves, recoveryCurves,
																						principals, attachments, detachments, crossSubordination,
																						copula, correlation,
																						stepSize, stepUnit,
																						new double[] {product.Attachment, product.Detachment},
																						sampleSize),
						 discountCurve, 1.0, null
						 )
		{}

		#endregion // Constructors

	} // class CDO2MonteCarloPricer

}
