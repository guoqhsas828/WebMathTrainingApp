//
// SwapFuture.cs
//  -2011. All rights reserved.
//

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Swap Futures product
  /// </summary>
  /// <remarks>
  ///   <para>A swap future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   swap on a specified future expiration date at a predetermined rate.</para>
  ///   <para>Common swap futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cmegroup.com/trading/interest-rates/deliverable-interest-rate-swap-futures.html">Deliverable Interest Rate Swap Futures (CME)</a></description></item>
  ///   </list>
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <seealso cref="FutureBase"/>
  /// <example>
  /// <para>The following example demonstrates constructing a swap future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var future = new SwapFuture(
  ///    expirationDate,                          // Expiration
  ///    0.02,                                    // 2pc notional coupon
  ///    100000,                                  // Contract size
  ///    0.01                                     // Tick size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class SwapFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, and Currency are unset.
    /// The SettlementType is Cash.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="notionalCoupon">Notional coupon of futures contract</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public SwapFuture(Dt lastDeliveryDate, double notionalCoupon, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      NotionalCoupon = notionalCoupon;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (NotionalCoupon <= 0 || NotionalCoupon > 2.0)
        InvalidValue.AddError(errors, this, "NotionalCoupon", "Invalid notional coupon");
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Notional coupon of futures contract
    /// </summary>
    public double NotionalCoupon { get; set; }

    #endregion Properties
  }
}
