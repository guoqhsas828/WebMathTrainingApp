// 
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a Stock Index Future or Single Stock Futures (SSF)
  /// </summary>
  /// <remarks>
  /// <para>Stock future options are options where the underlying asset is a stock index future or single stock future.</para>
  /// <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  /// to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  /// based on the likely difference between the reference price and the price of the underlying asset on the expiration
  /// date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  ///
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a stock index future option.</para>
  /// <code language="C#">
  ///   Dt deliveryDate = new Dt(16, 12, 2016);   // Delivery date is December 16, 2016
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is june 16, 2016
  ///
  ///   var option = new StockFutureOption(
  ///     deliveryDate,                           // Date of futures delivery
  ///     10000                                   // Contract size
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     125.0                                   // Strike is 125.0
  ///   );
  /// </code>
  /// </example>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.StockFutureOptionBlackPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class StockFutureOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="underlying">Underlying StockFuture</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public StockFutureOption(StockFuture underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base( underlying, expiration, type, style, strike)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public StockFutureOption(Dt lastDeliveryDate, double contractSize, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(new StockFuture(lastDeliveryDate, contractSize, 0.01), expiration, type, style, strike)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Stock Future
    /// </summary>
    public StockFuture StockFuture { get { return (StockFuture)Underlying; } }

    #endregion Properties

  }
}
