//
//  -2012. All rights reserved.
//

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option to enter into a Commodity Future or Commodity Index Future
  /// </summary>
  /// <remarks>
  /// <para>Commodity future options are exchange traded options where the underlying asset is a commodity future or commodity index future.</para>
  /// <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  /// to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  /// based on the likely difference between the reference price and the price of the underlying asset on the expiration
  /// date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  ///
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a commodity future option.</para>
  /// <code language="C#">
  ///   Dt deliveryDate = new Dt(16, 12, 2016);   // Delivery date is December 16, 2016
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is june 16, 2016
  ///
  ///   var option = new CommodityFutureOption(
  ///     deliveryDate,                           // Date of futures delivery
  ///     expirationDate,                         // Expiration
  ///     10000                                   // Contract size
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     125.0                                   // Strike is 125.0
  ///   );
  /// </code>
  /// </example>
  /// <seealso cref="SingleAssetOptionBase"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommodityFutureOptionBlackPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class CommodityFutureOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="underlying">Underlying commodity future</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike, in basis points</param>
    public CommodityFutureOption(CommodityFuture underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(underlying, expiration, type, style, strike )
    {}

    /// <summary>
    /// Constructor for vanilla option
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike, in basis points</param>
    public CommodityFutureOption(Dt lastDeliveryDate, double contractSize, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(new CommodityFuture(lastDeliveryDate, contractSize), expiration, type, style, strike)
    {}

    /// <summary>
    /// Constructor for barrier option
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="barrier1Type">First barrier type</param>
    /// <param name="barrier1Level">First barrier level</param>
    /// <param name="barrier2Type">Second barrier type</param>
    /// <param name="barrier2Level">Second barrier level</param>
    public CommodityFutureOption(
      Dt lastDeliveryDate, double contractSize, Dt expiration, OptionType type, OptionStyle style, double strike,
      OptionBarrierType barrier1Type, double barrier1Level,
      OptionBarrierType barrier2Type, double barrier2Level
      )
      : base(new CommodityFuture(lastDeliveryDate, contractSize), expiration, type, style, strike, barrier1Type, barrier1Level, barrier2Type, barrier2Level)
    {}

    /// <summary>
    /// Constructor for digital option
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="rebate">Rebate</param>
    public CommodityFutureOption(
      Dt lastDeliveryDate, double contractSize, Dt expiration, OptionType type, OptionStyle style, double strike,
      double rebate)
      : base(new CommodityFuture(lastDeliveryDate, contractSize), expiration, type, style, strike, rebate)
    {}

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (Expiration > CommodityFuture.LastDeliveryDate)
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Expiration {0} must be before underlying product last trading date {1}",
          Expiration, CommodityFuture.LastDeliveryDate));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Underlying Commodity Future
    /// </summary>
    public CommodityFuture CommodityFuture { get { return (CommodityFuture)Underlying; } }

    #endregion Properties
  }
}
