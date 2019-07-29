// 
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a FX Forward
  /// </summary>
  /// <remarks>
  /// <para>FX Forward options are options where the underlying asset is a FX forward.</para>
  /// <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  /// to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  /// based on the likely difference between the reference price and the price of the underlying asset on the expiration
  /// date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  /// 
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a fx forward option.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var option = new FxForwardOption(
  ///     valueDate,                              // Date of future exchange
  ///     Currency.EUR,                           // Receive EUR
  ///     Currency.USD,                           // Pay USD
  ///     1.3,                                    // Exchange rate
  ///     expirationDate,                         // Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     1.3                                     // Strike is 1.3
  ///   );
  /// </code>
  /// </example>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.FxForwardOptionBlackPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class FxForwardOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="valueDate">Date of future exchange (settlement)</param>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <param name="fxRate">Exchange rate (receiveCcy/payCcy - cost of 1 unit of receiveCcy in payCcy</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public FxForwardOption(Dt valueDate, Currency ccy1, Currency ccy2, double fxRate,
      Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(new FxForward(valueDate, ccy1, ccy2, fxRate), expiration, type, style, strike)
    {}
    
    #endregion Constructors

    #region Properties

    /// <summary>
    /// Base (domestic/base/unit/transaction/source/to) currency. Same as pay currency of underlying forward
    /// </summary>
    [Category("Option")]
    public Currency Ccy1
    {
      get { return FxForward.PayCcy; }
      set { FxForward.PayCcy = value; }
    }

    /// <summary>
    /// Quoting (foreign/quote/price/payment/destination/from) currency. Same as receive currency of underlying forward
    /// </summary>
    [Category("Option")]
    public Currency Ccy2
    {
      get { return FxForward.ReceiveCcy; }
      set { FxForward.ReceiveCcy = value; }
    }

    /// <summary>
    /// Underlying Fx Forward
    /// </summary>
    public FxForward FxForward { get { return (FxForward)Underlying; } }

    #endregion Properties

  }
}
