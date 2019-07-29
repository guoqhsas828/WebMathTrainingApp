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
  ///<summary>
  /// FX Futures product
  ///</summary>
  /// <remarks>
  ///   <para>A fx future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   currency in exchange for another currency on a specified future expiration date at a predetermined exchange rate.</para>
  ///   <para>Common fx futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cmegroup.com/trading/fx/">CME Fx Futures</a></description></item>
  ///   <item><description><a href="https://globalderivatives.nyx.com/fx/nyse-liffe">NYSE Euronext Liffe Fx Futures</a></description></item>
  ///   <item><description><a href="http://www.tfx.co.jp/en/products/forex.shtml">Tokyo Financial Exchange (TFX) Fx Futures</a></description></item>
  ///   </list>
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a CME Euro Fx future.</para>
  /// <code language="C#">
  ///   Dt lastTradingDate = new Dt(19, 12, 2016); // Last trading day is 19 Dec, 2016
  /// 
  ///   var future = new FxFuture(
  ///     Currency.EUR,                           // Receive EUR
  ///     Currency.USD,                           // Pay USD
  ///     lastTradingDate,                        // Last trading date
  ///     125000,                                 // Contract size
  ///     0.0001                                  // Tick size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class FxFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, and Currency are unset.
    ///   The SettlementType is Cash.</para>
    ///   <para>The tick value defaults to the contract size times the tick size.</para>
    /// </remarks>
    /// <param name="receiveCcy">Receive currency</param>
    /// <param name="payCcy">Currency of Fx</param>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract (in <paramref name="receiveCcy"/>)</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public FxFuture(Currency receiveCcy, Currency payCcy, Dt lastDeliveryDate, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      PayCcy = payCcy;
      ReceiveCcy = receiveCcy;
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
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if( PayCcy == Currency.None )
        InvalidValue.AddError(errors, this, "PayCcy", "Pay currency must be specified");
      if (ReceiveCcy == Currency.None)
        InvalidValue.AddError(errors, this, "ReceiveCcy", "Receive currency must be specified");
      if (PayCcy == ReceiveCcy)
        InvalidValue.AddError(errors, this, "PayCcy", String.Format("Pay currency {0} cannot be same as receive currency {1}", PayCcy, ReceiveCcy));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Pay currency
    /// </summary>
    public Currency PayCcy { get; set; }

    /// <summary>
    /// Receive currency
    /// </summary>
    public Currency ReceiveCcy
    {
      get { return Ccy; }
      set { Ccy = value; }
    }

    #endregion Properties
  }
}
