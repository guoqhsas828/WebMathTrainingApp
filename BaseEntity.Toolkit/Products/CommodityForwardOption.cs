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
  ///   Option to enter into a Commodity Forward
  /// </summary>
  /// <remarks>
  /// <para>A commodity forward option is an OTC contract where the holder has the right (but not the obligation) to enter into
  /// a forward contract on a commodity.</para>
  /// <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  /// to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  /// based on the likely difference between the reference price and the price of the underlying asset on the expiration
  /// date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  ///
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a commodity forward option.</para>
  /// <code language="C#">
  ///   Dt fixingDate = new Dt(14, 12, 2016);     // Fixing date is is December 14, 2016
  ///   Dt deliveryDate = new Dt(16, 12, 2016);   // Delivery date is December 16, 2016
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is june 16, 2016
  ///
  ///   var option = new CommodityForwardOption(
  ///     fixingDate,                             // Date of future exchange
  ///     deliveryDate,                           // Date of forward delivery
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
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommodityForwardOptionBlackPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class CommodityForwardOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="underlying">Underlying commodity forward</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike, in basis points</param>
    public CommodityForwardOption(
      CommodityForward underlying,
      Dt expiration,
      OptionType type,
      OptionStyle style,
      double strike
      )
      : base(underlying, expiration, type, style, strike)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="fixingDate">Forward fixing date</param>
    /// <param name="deliveryDate">Forward delivery date (Maturity)</param>
    /// <param name="expiration">Option expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Option strike, in basis points</param>
    public CommodityForwardOption(
      Dt fixingDate,
      Dt deliveryDate,
      Dt expiration,
      OptionType type,
      OptionStyle style,
      double strike
      )
      : base(new CommodityForward(fixingDate, deliveryDate, 0.0, BDConvention.None, Calendar.None), expiration, type, style, strike)
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
      if (Expiration > CommodityForward.LastDeliveryDate)
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Expiration {0} must be before underlying product last trading date {1}",
          Expiration, CommodityForward.LastDeliveryDate));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Underlying Fx Forward
    /// </summary>
    public CommodityForward CommodityForward { get { return (CommodityForward)Underlying; } }

    #endregion Properties

  }
}
