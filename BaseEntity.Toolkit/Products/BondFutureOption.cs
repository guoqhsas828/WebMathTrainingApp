// 
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a Bond Future
  /// </summary>
  /// <remarks>
  /// <para>Bond future options are options where the underlying asset is a Bond Future.</para>
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a bond future option.</para>
  /// <code language="C#">
  ///   Dt deliveryDate = new Dt(16, 12, 2016);   // Delivery date is December 16, 2016
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is june 16, 2016
  ///
  ///   var option = new BondFutureOption(
  ///     deliveryDate,                           // Date of futures delivery
  ///     10000                                   // Contract size
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     95.0                                    // Strike is 95.0
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class BondFutureOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="underlying">Underlying BondFuture</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public BondFutureOption(BondFuture underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(underlying, expiration, type, style, strike)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="nominalCoupon">Nominal coupon of futures contract</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public BondFutureOption(Dt lastDeliveryDate, double nominalCoupon, double contractSize,
      Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(new BondFuture(lastDeliveryDate, nominalCoupon, contractSize, 0.01), expiration, type, style, strike)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Bond Future
    /// </summary>
    public BondFuture BondFuture { get { return (BondFuture)Underlying; } }

    #endregion Properties

  }
}
