//
// FTDMonteCarloPricer.cs
//  -2008. All rights reserved.     
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
	/// <summary>
	/// <para>Price a <see cref="BaseEntity.Toolkit.Products.FTD">Synthetic CDO</see> using Monte Carlo.</para>
	/// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FTD" />
  /// </remarks>
	/// <example>
	/// <para>The following example demonstrates constructing a pricer for a FTD.</para>
	/// <code language="C#">
	///   Dt asOf = Dt.Today();                                       // Pricing as of today
	///   Dt settle = Dt.Add(asOf, 1);                                // Settle T+1
	///   FTD ftd;                                                    // FTD to price
	///   string [] names;                                            // List of underlying names
	///   double [] principals;                                       // Array of principals for each underlying name
	///   SurvivalCurve [] survivalCurves;                            // Array of survival curves for each underlying name
	///   RecoveryCurve [] recoveryCurves;                            // Array of recovery curves for each underlying name
	///   DiscountCurve discountCurve;                                // Discount curve to use
	///
	///   // Initialise FTD, survival curves and discount curve
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
	///   // Create pricer for FTD
	///   FTDMonteCarloPricer pricer =
	///     new FTDMonteCarloPricer( asOf,                         // Pricing date
	///                                 settle,                       // Settlement date
	///                                 ftd,                          // FTD to price
	///                                 discountCurve,                // Discount curve
	///                                 survivalCurves,               // Array of survival curves for each underlying name
	///                                 recoveryCurves,               // Array of matching recovery curves for each survival curve
	///                                 principals,                   // Principals for each underlying name
	///                                 copula,                       // Copula to use
	///                                 correlation,                  // Correlation structure to use
	///                                 1,                            // Time unit size to use in pricing grid
	///                                 TimeUnit.Month );             // Time unit to use in pricing grid
	///
	///   // Calculate and print results
	///   Console.WriteLine( "Protection PV = {0}, Fee PV = {1}, PV = {2}, Accrued = {3},
	///                      pricer.ProtectionPv(), pricer.FeePv(), pricer.Pv(), pricer.Accrued() );
	///   Console.WriteLine( "Break even premium = {0}, Duration = {1}",
	///                      pricer.BreakEvenPremium(), pricer.RiskyDuration() );
  /// </code>
	/// </example>
	/// <seealso cref="BaseEntity.Toolkit.Products.FTD">Synthetic CDO Tranche Product</seealso>
	/// <seealso cref="HeterogeneousBasketPricer">Heterogeneous Basket Pricer</seealso>
  [Serializable]
	public class FTDMonteCarloPricer : FTDPricer, IPricer
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(FTDMonteCarloPricer));

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
    /// <param name="product">FTD tranche to price</param>
    /// <param name="basket">The basket model used to price the FTD</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///
    public
    FTDMonteCarloPricer(FTD product,
                        BasketForNtdPricer basket,
                        DiscountCurve discountCurve) : this(product, basket, discountCurve, null)
    {}
    
    
    
    
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
		/// <param name="product">FTD tranche to price</param>
		/// <param name="basket">The basket model used to price the FTD</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Reference curve for floating payments forecast</param>
	
    public
		FTDMonteCarloPricer(FTD product,
												BasketForNtdPricer basket,
												DiscountCurve discountCurve, DiscountCurve referenceCurve)
			: base(product, basket, discountCurve, referenceCurve, 1.0)
		{
		}


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
		/// <param name="sampleSize">Sample size of simulation</param>
		///
    public
		FTDMonteCarloPricer( Dt asOf,
												 Dt settle,
												 FTD product,
												 DiscountCurve discountCurve,
												 SurvivalCurve [] survivalCurves,
												 RecoveryCurve[] recoveryCurves,
												 double [] principals,
												 Copula copula,
												 GeneralCorrelation correlation,
												 int stepSize,
												 TimeUnit stepUnit,
												 int sampleSize) : this(asOf, settle, product, discountCurve, null, survivalCurves, recoveryCurves, principals, copula, correlation, stepSize, stepUnit, sampleSize)
    {
      
    }



    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="product">CDO tranche to price</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Reference curve for floating payments forecast</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Principals (face values) associated with individual names</param>
		/// <param name="copula">Copula structure</param>
		/// <param name="correlation">Factor correlations for the names in the basket</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
		/// <param name="sampleSize">Sample size of simulation</param>
		///
    public
    FTDMonteCarloPricer(Dt asOf,
                         Dt settle,
                         FTD product,
                         DiscountCurve discountCurve,
                         DiscountCurve referenceCurve,
                         SurvivalCurve[] survivalCurves,
                         RecoveryCurve[] recoveryCurves,
                         double[] principals,
                         Copula copula,
                         GeneralCorrelation correlation,
                         int stepSize,
                         TimeUnit stepUnit,
                         int sampleSize)
      : base(product,
             new MonteCarloBasketForNtdPricer(asOf, settle, product.Maturity,
                                              survivalCurves, recoveryCurves,
                                              principals, copula, correlation,
                                              stepSize, stepUnit),
             discountCurve, referenceCurve, 1.0
             )
    {
      var basket = Basket as MonteCarloBasketForNtdPricer;
      if (basket != null && sampleSize > 0)
        basket.SampleSize = sampleSize;
    }

    #endregion // Constructors

	} // class FTDMonteCarloPricer

}
