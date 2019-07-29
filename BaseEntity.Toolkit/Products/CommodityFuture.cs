//
// CommodityFuture.cs
//  -2012. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Products
{
  ///<summary>
  /// Commodity or Commodity Index Futures product
  ///</summary>
  /// <remarks>
  ///   <para>A commodity future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   commodity on a specified future expiration date at a predetermined price.</para>
  ///   <para>Common commodity futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cmegroup.com/trading/energy/crude-oil/light-sweet-crude.html">Light Sweet Crude Oil (WTI) Futures (CME)</a></description></item>
  ///   <item><description><a href="https://www.theice.com/productguide/ProductSpec.shtml?specId=219">Brent Crude Futures (ICE)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/energy/refined-products/rbob-gasoline_quotes_globex.html">RBOB Gasoline Futures (CME)</a></description></item>
  ///   <item><description><a href="https://www.theice.com/productguide/ProductSpec.shtml?specId=232">NYH (RBOB) Gasoline Futures (ICE)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/energy/natural-gas/natural-gas.html">Henry Hub Natural Gas Futures (CME)</a></description></item>
  ///   <item><description><a href="http://www.lme.com/aluminium.asp">3m Aluminium (LME)</a></description></item>
  ///   <item><description><a href="http://www.lme.com/copper.asp">3m Copper (LME)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/agricultural/grain-and-oilseed/corn.html">Corn Futures (CBOT)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/agricultural/grain-and-oilseed/wheat.html">Wheat Futures (CBOT)</a></description></item>
  ///   <item><description><a href="http://www.cmegroup.com/trading/agricultural/grain-and-oilseed/soybean.html">Soybeans Futures (CBOT)</a></description></item>
  ///   <item><description><a href="https://www.theice.com/productguide/ProductSpec.shtml?specId=15">Coffee C Futures (ICE)</a></description></item>
  ///   <item><description><a href="https://www.theice.com/productguide/ProductSpec.shtml?specId=23">Sugar No 11 Futures (ICE)</a></description></item>
  ///   <item><description><a href="https://www.theice.com/productguide/ProductSpec.shtml?specId=254">Cotton No 2 Futures (ICE)</a></description></item>
  ///   </list>
  ///   <para>A commodity index future is an exchange traded contract where the holder has the obligation to purchase or sell a
  ///   commodity index on a specified future expiration date at a predetermined price.</para>
  ///   <para>Common commodity index futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cmegroup.com/trading/agricultural/commodity-index/dow-jones-ubs-excess-return-commodity-index.html">Dow Jones-UBS Commodity Index Futures (CME)</a></description></item>
  ///   <item><description><a href="http://www.tocom.or.jp/guide/youkou/tocomindex/index.html">Tokyo Commodities Exchange Commodity Futures</a></description></item>
  ///   <item><description><a href="https://www.theice.com/productguide/ProductSpec.shtml?specId=34">Continuous Commodity Index Futures (ICE)</a></description></item>
  ///   </list>
  ///
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommodityFuturesPricer"/>
  /// <example>
  /// <para>The following example demonstrates constructing a commodity future.</para>
  /// <code language="C#">
  ///   Dt expirationDate = new Dt(16, 12, 2016); // Expiration is December 16, 2016
  /// 
  ///   var future = new CommodityFuture(
  ///    expirationDate,                          // Expiration
  ///    100000                                   // Contract size
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class CommodityFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, and Currency are unset.
    /// The TickSize is 0.01, the TickValue is TickSize*ContractSize and the SettlementType is Physical.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    public CommodityFuture(Dt lastDeliveryDate, double contractSize)
      : this(lastDeliveryDate, contractSize, 0.01)
    {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, Currency are unset, and
    /// the SettlementType is Physical.</para>
    /// <para>The tick value defaults to the <paramref name="contractSize"/> * <paramref name="tickSize"/>.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    public CommodityFuture(Dt lastDeliveryDate, double contractSize, double tickSize)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      SettlementType = SettlementType.Physical;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The FirstTradingDate, LastTradingDate, FirstNoticeDate, Currency are unset, and
    /// the SettlementType is Physical.</para>
    /// <para>The tick value defaults to the <paramref name="contractSize"/> * <paramref name="tickSize"/>.</para>
    /// </remarks>
    /// <param name="lastDeliveryDate">Last delivery date</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="referenceIndex">Reference index</param>
    public CommodityFuture(Dt lastDeliveryDate, double contractSize, double tickSize, CommodityPriceIndex referenceIndex)
      : base(lastDeliveryDate, contractSize, tickSize)
    {
      ReferenceIndex = referenceIndex;
      Calendar = referenceIndex.Calendar;
      BDConvention = referenceIndex.Roll;
      SettlementType = SettlementType.Physical;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Overridden resets
    /// </summary>
    public RateResets SettlementResets { get; set; }

    /// <summary>
    /// Reference index
    /// </summary>
    public CommodityPriceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Underlying rate reference index name
    /// </summary>
    public string IndexName { get { return ReferenceIndex != null ? ReferenceIndex.IndexName : ""; } }

    /// <summary>
    ///   Deposit payment calendar
    /// </summary>
    [Category("Base")]
    public Calendar Calendar { get; set; }

    /// <summary>
    ///   Deposit roll convention
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention { get; set; }

    #endregion Properties
  }
}
