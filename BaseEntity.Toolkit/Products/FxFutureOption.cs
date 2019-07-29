// 
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a FX Future
  /// </summary>
  /// <remarks>
  /// <para>FX future options are options where the underlying asset is a FX future.</para>
  /// <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  /// to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  /// based on the likely difference between the reference price and the price of the underlying asset on the expiration
  /// date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  ///
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a fx future option.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var option = new FxFutureOption(
  ///     Currency.EUR,                           // Receive EUR
  ///     Currency.USD,                           // Pay USD
  ///     expirationDate,                         // Futures Expiration
  ///     10000                                   // Contract size
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     1.3                                     // Strike is 1.3
  ///   );
  /// </code>
  /// </example>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.FxFutureOptionBlackPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class FxFutureOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="underlying">Underlying Fx future</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public FxFutureOption(FxFuture underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(underlying, expiration, type, style, strike)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="receiveCcy">Fx future receive currency</param>
    /// <param name="payCcy">Fx future pay currency</param>
    /// <param name="lastDeliveryDate">Fx future last delivery date</param>
    /// <param name="contractSize">Fx future notional of each contract</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public FxFutureOption(Currency receiveCcy, Currency payCcy, Dt lastDeliveryDate, double contractSize,
      Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(new FxFuture(receiveCcy, payCcy, lastDeliveryDate, contractSize, 0.01), expiration, type, style, strike)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Fx Future
    /// </summary>
    public FxFuture FxFuture { get { return (FxFuture)Underlying; } }

    #endregion Properties

  }
}
