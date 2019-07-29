// 
// StockFuture.cs
//  -2013. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Equity index future and Single Stock Futures (SSF) product
  /// </summary>
  /// <remarks>
  ///   <para>A stock future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   stock on a specified future expiration date at a predetermined price.</para>
  ///   <para>A equity index future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   stock index on a specified future expiration date at a predetermined price.</para>
  ///   <para>Common equity index futures contracts include:</para>
  ///   <list type="number">
  ///   <item><see href="http://www.cmegroup.com/trading/equity-index/us-index/e-mini-sandp500_contract_specifications.html">E-mini S&amp;P 500 Index Futures (CME)</see></item>
  ///   <item><see href="http://www.eurexchange.com/trading/products/IDX/STX/BLC/FESX_en.html">Euro Stoxx 50 Futures (Eurex)</see></item>
  ///   <item><see href="http://www.rts.ru/s759">RTS Index Futures (RTS)</see></item>
  ///   <item><see href="http://www.nseindia.com/content/fo/fo_niftyfutures.htm">S&amp;P CNX Nifty Index Futures, NSE India</see></item>
  ///   <item><see href="http://www.ose.or.jp/e/derivative/225mini/">Nikkei 225 Mini Futures (OSE)</see></item>
  ///   <item><see href="http://eng.krx.co.kr/m3/m3_3/m3_3_1/m3_3_1_1/UHPENG03003_01_01.html">Kospi 200 Futures (KRX)</see></item>
  ///   <item><see href="http://www.cmegroup.com/trading/equity-index/us-index/e-mini-nasdaq-100_contract_specifications.html">E-mini Nasdaq 100 Futures (CME)</see></item>
  ///   </list>
  ///
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a stock index future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var future = new StockFuture(
  ///    expirationDate,                          // Expiration (last Delivery date)
  ///    10000,                                   // Contract size
  ///    0.01,                                    // Tick size
  ///   );
  /// </code>
  /// </example>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.StockFuturePricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class StockFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    ///   <para>The tick value defaults to the contract size times the tick size.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public StockFuture(Dt lastDeliveryDate, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Reference index
    /// </summary>
    public EquityPriceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Underlying rate reference index name
    /// </summary>
    public string IndexName { get { return ReferenceIndex != null ? ReferenceIndex.IndexName : ""; } }

    /// <summary>
    ///   Calendar
    /// </summary>
    [Category("Base")]
    public Calendar Calendar { get; set; }
   
    #endregion
  }
}
