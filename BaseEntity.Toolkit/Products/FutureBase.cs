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
  ///   <para>Abstract base class for listed futures products.</para>
  ///   <para>Futures need not derive from this class and it is simply provided as a helpful class with a superset of standard
  ///   terms often found in futures.</para>
  /// </summary>
  /// <remarks>
  ///   <para>A future is an exchange traded contract where the holder has the obligation to purchase or sell an
  ///   asset on a specified future expiration date at a predetermined price.
  ///   Unlike an option, the buyer has the obligation (not the right) to settle a futures contract.
  ///   Futures can be cash for physically settled.</para>
  ///   <para>Futures exist on a wide variety of underlying assets including:</para>
  ///   <list type="number">
  ///     <item><description><see cref="StirFuture">Rates</see></description></item>
  ///     <item><description><see cref="FxFuture">Foreign Exchange</see></description></item>
  ///     <item><description><see cref="BondFuture">Bonds</see></description></item>
  ///     <item><description><see cref="StockFuture">Equities and Equity Indices</see></description></item>
  ///     <item><description><see cref="CommodityFuture">Commodities and Commodity Indices</see></description></item>
  ///   </list>
  ///   <para>A future is closely related to a forward contract. The primary different is that futures are traded on exchanges.
  ///   Forward trades are over the counter and customized between two counterparties.</para>
  ///   <para>Futures are settled through clearing houses and to mitigate counterparty exposure, a margin is posted or received
  ///   each day based on movements in the futures price. An initial margin is posted when the contract is entered into and a
  ///   daily variation margin is posted or received going forward.</para>
  ///   <para>Futures are traded through a broking member of the exchange. The one futures trade between Party A and Party B is
  ///   implemented as two trades one between Party A and the clearinghouse and one between Party B and the clearinghouse.
  ///   Party A and B do not carry counterparty exposure to each other. Counterparty exposure to the clearinghouse is
  ///   mitigated using margining.</para>
  ///
  ///   <para><b>Margining</b></para>
  ///   <para>Before trading, the broker collects and initial margin as a deposit. This may be cash or securities. This margin
  ///   is held in a margin account by the broker. The initial margin is determined by the exchange and may depend on if the
  ///   purpose is hedging or speculation. The goal of the initial margin is to cover any potential one-day loss on the position.</para>
  ///   <para>Each day, the profit or loss in the outstanding futures position is calculated. If there is a loss, that loss is
  ///   transferred from your margin account. If there is a profit, the profit is transferred to your margin account. Note that
  ///   the clearinghouse has only matched trades and has zero net cash flows.</para>
  ///   <para>The margining process effectively means futures settle every day, unlike forwards where settlement is at expiration.
  ///   This mitigates credit risk for futures.</para>
  ///   <para>The daily calculation of profit or loss for the margining is as follows:</para>
  ///   <para>For the first day:</para>
  ///     <formula>margin=Notional * \left( SettlementPrice_{today} - TradedPrice \right)</formula>
  ///   <para>For subsequent days:</para>
  ///     <formula>margin=Notional * \left( SettlementPrice_{today} - SettlementPrice_{yesterday} \right)</formula>
  ///   <para>Settlement prices are calculated by the exchange and may vary on trading activity. Generally, settlement
  ///   prices are calculated as an average of trades at or before the close of trading.</para>
  ///   <para>If the amount in the margin account falls below some threshold, the broker will request an additional deposit
  ///   called a margin call. If a deposit is not made the broker has the right to liquidate some or all of the outstanding
  ///   positions.</para>
  ///   <para>Note that margin payments are not collateral. Unlike collateral posted for OTC trades, margin payments to
  ///   clearinghouses legally changes hands. The margin account amount held by the broker is collateral and legally
  ///   belongs to the client.</para>
  ///   <para>Futures contracts can be closed out by either a) entering into an offsetting transaction, b) delivery
  ///   of there underlying per the exchange rules, c) privately negotiated physical settlement or d) cash settlement.</para>
  ///
  ///   <para><b>Cash Settlement</b></para>
  ///   <para>Futures contracts have a last trade date and a delivery period specified by the exchange. for cash
  ///   settled future, the delivery period is the last trade date. On that date, the settlement price is set equal
  ///   to the cash price of the underlying asset. There is a final margining based on that settlement price, and
  ///   then the contract expires.</para>
  ///
  ///   <para><b>Physical Settlement</b></para>
  ///   <para>For physical settlement, rules depend on the exchange and underlying asset. Usually, there is a
  ///   delivery month when delivery may occur. The last trading day for the future falls towards the end of that month.
  ///   The party short the future may elect to deliver the underlier on any business day in the delivery month. Typically,
  ///   notice of delivery must be made to the exchange two business days prior to delivery. The date on which notice is given
  ///   is called the notice date. The first possible date for notice comes towards the end of the month preceding the delivery
  ///   month. It is called the first notice date. Upon receiving notice of delivery, the exchange selects a party that is long
  ///   the future to take the delivery. This may be the party with the largest long position in the future. Alternatively, the
  ///   party to take delivery may be selected by lot.</para>
  ///   <para>The vast majority of futures contracts are traded by hedgers or speculators with no interest in taking or delivering the
  ///   underlier. Such parties holding long futures will offset them prior to the first notice date. Those with short positions
  ///   will offset them by the last trade date. Most futures are closed out by offset.</para>
  ///   <para>Exchanges specify conditions of delivery. These include acceptable locations for delivery, in the case of commodities or
  ///   energies. It includes specifics about the quality, grade or nature of the underlier to be delivered. For example, only
  ///   certain Treasury bonds may be delivered under the Chicago Board of Trade's Treasury bond future. Only certain growths of
  ///   coffee may be delivered under the Coffee, Sugar and Cocoa Exchange's coffee future.</para>
  ///   <para>In many commodity or energy markets, parties want to settle futures by delivery, but exchange rules are too restrictive for
  ///   their needs. For example, the New York Mercantile Exchange requires that natural gas be delivered at the Henry Hub in
  ///   Louisiana. Suppose two parties need to buy/sell gas at some other hub and have transacted futures to hedge against price
  ///   movements prior to the transaction. What should they do?</para>
  ///   <para>One answer is that they could privately negotiate the trade and then reverse their futures positions by offset. This
  ///   requires that they take price risk during the period between closing the physical trade and offsetting their respective
  ///   futures positions. Many exchanges offer an alternative called exchange for physicals (EFP). The mechanics of EFP vary
  ///   by exchange. Generally, the parties privately negotiate their physical trade. Then, instead of offsetting their futures
  ///   hedges with trades on the exchange, they inform the exchange that they want to transfer the futures from one party to
  ///   the other, closing out their respective positions. Essentially, EFP is customizable physical delivery.</para>
  ///   <para>Common futures contracts trade on the following exchanges:</para>
  ///   <list type="number">
  ///     <item><description><a href="http://www.cmegroup.com/">CME Group</a> - formerly CBOT, CME - Currencies, Various Interest Rate derivatives (including US Bonds), Agricultural (Corn, Soybeans, Soy Products, Wheat, Pork, Cattle, Butter, Milk), Index (Dow Jones Industrial Average), Metals (Gold, Silver), Index (NASDAQ, S+P, etc.)</description></item>
  ///     <item><description><a href="http://www.theice.com/">Intercontinental Exchange</a> - ICE Futures Europe, formerly the International Petroleum Exchange trades energy including crude oil, heating oil, gas oil (diesel), refined petroleum products, electric power, coal, natural gas, and emissions</description></item>
  ///     <item><description><a href="http://www.nyx.com/">NYSE Euronext</a> - which absorbed Euronext into which London International Financial Futures and Options Exchange or LIFFE was merged. (LIFFE had taken over London Commodities Exchange ("LCE") in 1996) - softs: grains and meats. Inactive market in Baltic Exchange shipping. Index futures include EURIBOR, FTSE 100, CAC 40, AEX index</description></item>
  ///     <item><description><a href="http://www.safex.co.za/">South African Futures Exchange</a> - SAFEX</description></item>
  ///     <item><description><a href="http://www.asx.com.au/">Sydney Futures Exchange</a></description></item>
  ///     <item><description><a href="http://www.tse.or.jp/english/">Tokyo Stock Exchange</a> - TSE, JGB Futures, TOPIX Futures</description></item>
  ///     <item><description><a href="http://www.tocom.or.jp/">Tokyo Commodity Exchange</a> - TOCO</description></item>
  ///     <item><description><a href="http://www.tfx.co.jp/en/">Tokyo Financial Exchange</a> - TFX, Euroyen Futures, OverNight CallRate Futures, SpotNext RepoRate Futures</description></item>
  ///     <item><description><a href="http://www.ose.or.jp/e/">Osaka Securities Exchange</a> - OSE, Nikkei Futures, RNP Futures</description></item>
  ///     <item><description><a href="http://www.lme.com/">London Metal Exchange</a> - metals: copper, aluminium, lead, zinc, nickel, tin and steel</description></item>
  ///     <item><description><a href="http://www.cmegroup.com/company/nymex.html">New York Mercantile Exchange CME Group</a> - energy and metals: crude oil, gasoline, heating oil, natural gas, coal, propane, gold, silver, platinum, copper, aluminum and palladium</description></item>
  ///     <item><description><a href="http://www.dubaimerc.com/">Dubai Mercantile Exchange</a></description></item>
  ///     <item><description><a href="http://eng.krx.co.kr/">Korea Exchange</a> - KRX</description></item>
  ///     <item><description><a href="http://www.sgx.com">Singapore Exchange</a> - SGX - into which merged Singapore International Monetary Exchange (SIMEX)</description></item>
  ///     <item><description><a href="http://www.rofex.com.ar/">Rosario Futures Exchange</a> - ROFEX, Argentina</description></item>
  ///     <item><description><a href="http://www.ncdex.com/">National Commodity and Derivatives Exchange</a> - NCDEX, India</description></item>
  ///   </list>
  /// </remarks>
  // Docs note: remarks are inherited so only include docs suitable for derived classes. RD Mar'14
  [Serializable]
  [ReadOnly(true)]
  public class FutureBase : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>The FirstTradingDate and FirstNoticeDate are unset, Currency is none,
    ///   FirstDeliveryDate, LastTradingDate, and SettlementDate are set to lastDelivery date,
    ///   the SettlementType is Cash.</para>
    ///   <para>The tick value defaults to the <paramref name="contractSize"/> times the
    ///   <paramref name="tickSize"/>.</para>
    /// </remarks>
    /// <param name="lastDelivery">Last delivery date or equivalent</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    protected FutureBase(Dt lastDelivery, double contractSize, double tickSize)
      : base(Dt.Empty, lastDelivery, Currency.None)
    {
      SettlementType = SettlementType.Cash;
      FirstDeliveryDate = lastDelivery;
      // FirstTradingDate = Effective = Dt.Empty
      LastTradingDate = lastDelivery;
      SettlementDate = lastDelivery;
      FirstNoticeDate = Dt.Empty;
      ContractSize = contractSize;
      TickSize = tickSize;
      TickValue = contractSize * tickSize;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      // Validate dates
      if (!FirstTradingDate.IsEmpty() && !FirstTradingDate.IsValid())
        InvalidValue.AddError(errors, this, "FirstTradingDate", $"Invalid first trading date {FirstTradingDate}");
      if (!LastTradingDate.IsEmpty() && !LastTradingDate.IsValid())
        InvalidValue.AddError(errors, this, "LastTradingDate", $"Invalid last trading date {LastTradingDate}");
      if (FirstTradingDate.IsValid() && LastTradingDate.IsValid() && Dt.Cmp(FirstTradingDate, LastTradingDate) >= 0)
        InvalidValue.AddError(errors, this, "FirstTradingDate", $"First trading date {FirstTradingDate} must be before last trading date {LastTradingDate}");

      if (!SettlementDate.IsEmpty() && !SettlementDate.IsValid())
        InvalidValue.AddError(errors, this, "SettlementDate", $"Invalid settlement date {SettlementDate}");
      if (SettlementDate.IsValid() && LastTradingDate.IsValid() && Dt.Cmp(SettlementDate, LastTradingDate) < 0)
        InvalidValue.AddError(errors, this, "FirstTradingDate", $"Settlement date {SettlementDate} must be on or after last trading date {LastTradingDate}");

      if (!FirstDeliveryDate.IsEmpty() && !FirstDeliveryDate.IsValid())
        InvalidValue.AddError(errors, this, "FirstDeliveryDate", $"Invalid first delivery date {FirstDeliveryDate}");
      if (!LastDeliveryDate.IsEmpty() && !LastDeliveryDate.IsValid())
        InvalidValue.AddError(errors, this, "LastDeliveryDate", $"Invalid last delivery date {LastDeliveryDate}");
      if (LastDeliveryDate.IsValid() && FirstDeliveryDate.IsValid() && Dt.Cmp(LastDeliveryDate, FirstDeliveryDate) < 0)
        InvalidValue.AddError(errors, this, "LastDeliveryDate",
          $"First delivery date {FirstDeliveryDate} must be on or before last delivery date {LastDeliveryDate}");

      if (LastDeliveryDate.IsValid() && LastTradingDate.IsValid() && Dt.Cmp(LastDeliveryDate, LastTradingDate) < 0)
        InvalidValue.AddError(errors, this, "LastDeliveryDate",
          $"Last delivery date {LastDeliveryDate} must be on or after last trading date {LastTradingDate}");

      // Validate sizes
      if (ContractSize <= 0.0)
        InvalidValue.AddError(errors, this, "ContractSize", $"Contract size must be positive, not {ContractSize}");
      if (TickSize <= 0.0)
        InvalidValue.AddError(errors, this, "TickSize", $"Tick size must be positive, not {TickSize}");
    }

    /// <summary>
    ///   Is future active
    /// </summary>
    /// <remarks>
    ///   <para>A physical settled future is active if the pricing as-of date is before
    ///   the <see cref="LastDeliveryDate">last delivery date</see>.</para>
    ///   <para>A cash settled future is active if the pricing as-of date is before
    ///   the <see cref="SettlementDate">settlement date</see>.</para>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date</param>
    /// <returns>True if product active</returns>
    public override bool IsActive(Dt asOf)
    {
      return (SettlementType == SettlementType.Cash) ? (asOf < SettlementDate) : (asOf < LastDeliveryDate);
    }

    /// <summary>
    /// Scaling factor for Point Value.
    /// </summary>
    /// <returns></returns>
    protected virtual double PointValueScalingFactor()
    {
      return 1.0; // 1 by default
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Futures contract month (last trading date month)
    /// </summary>
    public int ContractMonth => LastDeliveryDate.Month;

    /// <summary>
    ///   Futures contract year (last trading date year)
    /// </summary>
    public int ContractYear => LastDeliveryDate.Year;

    /// <summary>
    ///   Futures code (from last trading date)
    /// </summary>
    public string FuturesCode => Dt.ExchangeDateCode(ContractMonth, ContractYear);

    /// <summary>
    ///   Contract size
    /// </summary>
    /// <remarks>
    ///   <para>A proxy for <see cref="Product.Notional">Notional</see>.</para>
    /// </remarks>
    public double ContractSize { get { return Notional; } set { Notional = value; } }

    /// <summary>
    ///   First trading date
    /// </summary>
    /// <remarks>
    ///   <para>A proxy for <see cref="Product.Effective">Effective Date</see>. Dt.Empty if not specified.</para>
    /// </remarks>
    public Dt FirstTradingDate { get { return Effective; } set { Effective = value; } }

    /// <summary>
    ///   Last trading date
    /// </summary>
    /// <remarks>
    ///   Dt.Empty if not specified
    /// </remarks>
    public Dt LastTradingDate { get; set; }

    /// <summary>
    ///   Cash or physical settlement
    /// </summary>
    public SettlementType SettlementType { get; set; }

    /// <summary>
    ///   Futures settlement date for cash settlement
    /// </summary>
    /// <remarks>
    ///   <para>This is the date the final futures cash settlement.</para>
    /// </remarks>
    [Category("CashSettled")]
    public Dt SettlementDate { get; set; }

    /// <summary>
    ///   First notice date for physical settlement
    /// </summary>
    [Category("PhysicalSettled")]
    public Dt FirstNoticeDate { get; set; }

    /// <summary>
    ///   First delivery date for physical settlement
    /// </summary>
    /// <remarks>Dt.Empty if not specified</remarks>
    [Category("PhysicalSettled")]
    public Dt FirstDeliveryDate { get; set; }

    /// <summary>
    ///   Last delivery date for physical settlement
    /// </summary>
    /// <remarks>
    ///   <para>A proxy for <see cref="Product.Maturity">Maturity date</see>.</para>
    /// </remarks>
    [Category("PhysicalSettled")]
    public Dt LastDeliveryDate { get { return Maturity; } set { Maturity = value; } }

    /// <summary>
    ///   Size of a tick (minimum price move)
    /// </summary>
    public double TickSize { get; set; }

    /// <summary>
    ///   Value of a tick for contracts with a fixed tick value, otherwise undefined
    /// </summary>
    public double TickValue { get; set; }

    /// <summary>
    ///   Value of a point for contracts with a fixed tick value, otherwise undefined
    /// </summary>
    public double PointValue => TickValue / TickSize / PointValueScalingFactor();

    #endregion Properties
  }
}
