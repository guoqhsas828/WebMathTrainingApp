/*
 * CDOofNTDPricer.cs
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
  ///
	/// <summary>
	///   <para>Price a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">CDO of NTDs</see> using the
	///   <see cref="BaseEntity.Toolkit.Models.FTDBasketModel">Semi-analytic Model</see>.</para>
	/// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.SyntheticCDO" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.FTDBasketModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.SyntheticCDO">CDO of NTDs Tranche Product</seealso>
	/// <seealso cref="BaseEntity.Toolkit.Models.FTDBasketModel">Semi-analytic CDO of NTD Basket Model</seealso>
	///
  [Serializable]
	public class CDOofNTDPricer : SyntheticCDOPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CDOofNTDPricer));

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
		/// <param name="product">CDO of NTD tranche to price</param>
		/// <param name="pricer">The CDO of NTD pricer to share the basket model</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		///
    public
		CDOofNTDPricer( SyntheticCDO product,
										CDOofNTDPricer pricer,
										DiscountCurve discountCurve)
			: base(product, pricer.Basket, discountCurve, 1.0, null)
		{}


    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="product">CDO of NTD tranche to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="survivalCurves">Array of Survival Curves of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="ftds">Array of child FTD products</param>
		/// <param name="lastIndices">Last indices of FTD child baskets</param>
		/// <param name="principals">Principals of individual names in FTDs</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Correlation of the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		///
    public
		CDOofNTDPricer( Dt asOf,
										Dt settle,
										SyntheticCDO product,
										DiscountCurve discountCurve,
										SurvivalCurve [] survivalCurves,
										RecoveryCurve[] recoveryCurves,
										FTD [] ftds,
										int [] lastIndices,
										double [] principals,
										Copula copula,
										FactorCorrelation correlation,
										int stepSize,
										TimeUnit stepUnit )
			: base(product,
						 new FTDBasketPricer( asOf, settle, product.Maturity,
																	survivalCurves, recoveryCurves,
																	ftds, lastIndices, principals,
																	copula, correlation,
																	stepSize, stepUnit,
																	new double[] {product.Attachment, product.Detachment} ),
						 discountCurve, 1.0, null
						 )
		{}

		#endregion // Constructors

	} // class CDOofNTDPricer

}
