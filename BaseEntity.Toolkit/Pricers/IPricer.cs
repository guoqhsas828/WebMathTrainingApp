/*
 * IPricer.cs
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Defines the public interface for Pricers
  /// </summary>
  /// <remarks>
  ///   <para>Pricers provide the join between products and models.
  ///   Models are stateless calculations while Pricers add state and apply
  ///   models to specific products.</para>
  ///   <para>An example of a model would be a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black Scholes Model</see>.
  ///   This model is implemented in a generic way with inputs that are
  ///   independent of any particular pricing application.</para>
  ///   <para>An example of a Pricer would be <see cref="StockOptionPricer">Stock Option Black Scholes Pricer</see> 
  ///   which implements the BlackScholes model for pricing Stock Options.
  ///   The Pricer adds state and product specific adjustments to calling
  ///   the underlying BlackScholes model.</para>
  ///   <para>Typically multiple pricers are implemented for particular products.</para>
  /// </remarks>
  public interface IPricer : ICloneable
  {
    #region Methods

    /// <summary>
    /// Present value (including accrued) for product to pricing as-of date given pricing arguments
    /// </summary>
    /// <returns>Present value</returns>
    double Pv();

    /// <summary>
    /// Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    double Accrued();

    /// <summary>
    /// Get cashflows for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the specified date.</para>
    /// </remarks>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date</returns>
    Cashflow GenerateCashflow(Cashflow cashflow, Dt from);

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some internal state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    /// </remarks>
    void Reset();

    #endregion Methods

    #region Properties

    /// <summary>
    /// As-of or pricing date for pricing
    /// </summary>
    /// <remarks>
    ///   <para>The as-of or pricing date is the date that the pricing
    ///   is for and is the date that cashflows/etc. will be present
    ///   valued to.</para>
    ///   <para>The as-of date should be on or before the Settle date.</para>
    /// </remarks>
    /// <example>
    /// <code language="C#">
    ///   IPricer pricer;
    ///
    ///   // Initialise pricer of your choice
    ///   // ...
    ///
    ///   // Print out the as-of or pricing date
    ///   Console.WriteLine( "Pricing as-of {0}, settling {1}", pricer.AsOf, pricer.Settle );
    ///
    ///   // Test pricing date on or before settlement date
    ///   if( Dt.cmp(pricer.AsOf, pricer.Settle) > 0 )
    ///     Console.WriteLine( "Oops... the pricing date is after the settlement date" );
    /// </code>
    /// </example>
    Dt AsOf { get; set; }

    /// <summary>
    /// Settlement date for pricing
    /// </summary>
    /// <remarks>
    ///   <para>The settlement date is the date any up-front payments are exchanged.</para>
    ///   <para>Cashflows before the settlement date are ignored in the pricing.</para>
    ///   <para>The settlement date is also the date that interest starts accruing.</para>
    ///   <para>The settlement date should be on or after the as-of date.</para>
    /// </remarks>
    /// <example>
    /// <code language="C#">
    ///   IPricer pricer;
    ///
    ///   // Initialise pricer of your choice
    ///   // ...
    ///
    ///   // Print out the as-of or pricing date
    ///   Console.WriteLine( "Pricing as-of {0}, settling {1}", pricer.AsOf, pricer.Settle );
    ///
    ///   // Test pricing date on or before settlement date
    ///   if( Dt.cmp(pricer.AsOf, pricer.Settle) > 0 )
    ///     Console.WriteLine( "Oops... the pricing date is after the settlement date" );
    /// </code>
    /// </example>
    Dt Settle { get; set; }

    /// <summary>
    /// Underlying Product being priced
    /// </summary>
    /// <remarks>
    ///   <para>Pricers provide specialised implementations of models for a
    ///   particular product.</para>
    ///   <para>This method provides access to the product being priced
    ///   in this IPricer</para>
    ///   <para>The Product returned may be null if not yet set.</para>
    /// </remarks>
    /// <example>
    /// <code language="C#">
    ///   IPricer pricer;
    ///
    ///   // Initialise pricer of your choice
    ///   // ...
    ///
    ///   // Print out details of the product in the pricer
    ///   Product product = pricer.Product;
    ///
    ///   if( product == null )
    ///     Console.WriteLine( "No product set yet for this pricer" );
    ///   else
    ///     Console.WriteLine( "Product {0} being priced. Effective on {1}, maturing on {2}",
    ///                        product.Description, product.Effective, product.Maturity );
    /// </code>
    /// </example>
    IProduct Product { get; }

    /// <summary>
    /// Payment pricer
    /// </summary>
    IPricer PaymentPricer { get; }

    /// <summary>
    /// Currency of Pv 
    /// </summary>
    Currency ValuationCurrency { get; }

    #endregion Properties
  }

  /// <summary>
  ///   Generic version of IPricer for better type inference and type safety.
  /// </summary>
  /// <typeparam name="T">The product type.</typeparam>
  /// <remarks></remarks>
  public interface IPricer<out T> : IPricer where T : IProduct
  {
    /// <summary>
    ///  Get the underlying product being priced.
    /// </summary>
    new T Product { get; }
  }

  /// <summary>
  ///   Interface to get a pricer in which all the rate fixings with the reset dates on
  ///   or before the anchor date are fixed at the current projected values.
  /// </summary>
  public interface ILockedRatesPricerProvider
  {
    /// <summary>
    ///   Get a pricer in which all the rate fixings with the reset dates on
    ///   or before the anchor date are fixed at the current projected values.
    /// </summary>
    /// <param name="anchorDate">The anchor date.</param>
    /// <returns>IPricer{`0}.</returns>
    /// <remarks>This method must never modify the original pricer,
    ///  whose states and behaviors remain exactly the same before
    ///  and after calling this method.</remarks>
    IPricer LockRatesAt(Dt anchorDate);

    /// <summary>
    ///   Get a pricer in which the rate fixing on the pricing date
    ///   can be replaced into the current projected values.
    /// </summary>
    /// <param name="asOfDate">The pricing date.</param>
    /// <param name="otherPricer">The other pricer to derive fixing rate from </param>
    /// <returns>IPricer{`0}.</returns>
    /// <remarks>This method must never modify the original pricer,
    ///  whose states and behaviors remain exactly the same before
    ///  and after calling this method.</remarks>
    IPricer LockRateAt(Dt asOfDate, IPricer otherPricer);
  }

  /// <summary>
  ///  Interface for Repo securities
  /// </summary>
  public interface IRepoAssetPricer : IPricer
  {
    /// <summary>
    ///  Market value of a repo security for current exposure purposes
    /// </summary>
    /// <returns></returns>
    double SecurityMarketValue();
  }
}
