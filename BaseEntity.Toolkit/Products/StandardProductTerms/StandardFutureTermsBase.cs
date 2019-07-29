//
// StandardFutureTermsBase.cs
//   2015. All rights reserved.
//
using System;
using System.Collections;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{ 
  /// <summary>
  /// Standard Future terms interface
  /// </summary>
  public interface IStandardFutureTerms : IStandardProductTerms
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="expirationCode"></param>
    /// <returns></returns>
    FutureBase GetProduct(Dt asOf, string expirationCode);

    /// <summary>
    /// Index name 
    /// </summary>
    /// <returns></returns>
    string GetIndexName();

    /// <summary>
    /// Exchange Code for future
    /// </summary>
    /// <returns></returns>
    string GetExchange();

    /// <summary>
    /// Contract Code for future
    /// </summary>
    string GetContractCode();

    /// <summary>
    /// Has tenor
    /// </summary>
    /// <returns></returns>
    bool HasTenor();
    
    /// <summary>
    /// Get Index Tenor for future
    /// </summary>
    Tenor GetTenor();
  }

  /// <summary>
  ///   Common terms for a market-standard Future
  /// </summary>
  /// <remarks>
  ///   <para>Defines the terms for a market-standard Future. Specific futures terms derive from this class.</para>
  ///   <para>Futures are identified uniquely by the <see cref="StandardFutureTermsBase{T}.ContractCode">Futures Contract Code</see>.</para>
  ///   <para>The function <see cref="GetProduct(Dt,string)"/> creates the Future from the Futures Terms.</para>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="GetProduct(Dt,string)"/>
  /// <seealso cref="FutureBase"/>
  [DebuggerDisplay("Future Terms Base")]
  [Serializable]
  public abstract class StandardFutureTermsBase<T> : StandardProductTermsBase, IStandardFutureTerms where T : FutureBase
  {
    #region Constructors

    /// <summary>
    ///    Constructor when no month offset
    /// </summary>
    /// <param name="exchange">Exchange name</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="indexName">Index name (if required)</param>
    /// <param name="ccy">Futures currency</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="settlementType">Settlement type</param>
    /// <param name="firstTradingDate">First trading date</param>
    /// <param name="lastTradingDayOfMonth">Reference day of month for last trading and last delivery dates</param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    protected StandardFutureTermsBase(
      string exchange,
      string contractCode, string description, string indexName,
      Currency ccy, double contractSize, double tickSize, double tickValue,
      SettlementType settlementType, Dt firstTradingDate,
      DayOfMonth lastTradingDayOfMonth, int lastDeliveryDayOffset, int lastTradingDayOffset, 
      Calendar calendar
      ) : this(exchange, contractCode, description, indexName, ccy, 
        contractSize, tickSize, tickValue, settlementType, firstTradingDate,
        lastTradingDayOfMonth, lastTradingDayOffset, 0, // Assume no month difference
        lastTradingDayOfMonth, lastDeliveryDayOffset, 0, // Assume no month difference  
        calendar)
    { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="exchange">Exchange name</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="indexName">Index name (if required)</param>
    /// <param name="ccy">Futures currency</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="settlementType">Settlement type</param>
    /// <param name="firstTradingDate">First trading date</param>
    /// <param name="lastTradingDayOfMonth">Reference day of month for last trading and last delivery dates</param>
    /// <param name="lastDeliveryDayOfMonth"></param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastDeliveryMonthOffset">Months to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="lastTradingMonthOffset">Months to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    protected StandardFutureTermsBase(
      string exchange,
      string contractCode, string description, string indexName, 
      Currency ccy, double contractSize, double tickSize, double tickValue,
      SettlementType settlementType, Dt firstTradingDate,
      DayOfMonth lastTradingDayOfMonth, int lastTradingDayOffset, int lastTradingMonthOffset,
      DayOfMonth lastDeliveryDayOfMonth, int lastDeliveryDayOffset, int lastDeliveryMonthOffset, 
      Calendar calendar
      )
      : base(description)
    {
      Exchange = exchange;
      ContractCode = contractCode;
      _indexName = indexName;
      Currency = ccy;
      ContractSize = contractSize;
      TickSize = tickSize;
      TickValue = tickValue;
      FirstTradingDate = firstTradingDate;
      SettlementType = settlementType;
      LastTradingDayOfMonth = lastTradingDayOfMonth;
      LastTradingDayOffset = lastTradingDayOffset;
      LastTradingMonthOffset = lastTradingMonthOffset;
      LastDeliveryDayOfMonth = lastDeliveryDayOfMonth;
      LastDeliveryDayOffset = lastDeliveryDayOffset;
      LastDeliveryMonthOffset = lastDeliveryMonthOffset;
      Calendar = calendar;
    }

    #endregion

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

      if (Currency == Currency.None)
        InvalidValue.AddError(errors, this, "Currency", "Currency must be specified for a discount rate future");
    }

    /// <summary>
    /// Index Name
    /// </summary>
    /// <returns></returns>
    public string GetIndexName()
    {
      return _indexName;
    }

    /// <summary>
    /// Has index tenor
    /// </summary>
    /// <returns></returns>
    public virtual bool HasTenor()
    {
      return false;
    }

    /// <summary>
    /// Returns index tenor
    /// </summary>
    /// <returns></returns>
    public virtual Tenor GetTenor()
    {
      return Tenor.Empty;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public override string Key => GetKey(Exchange, ContractCode);

    /// <summary>
    ///   Futures exchange
    /// </summary>
    public string Exchange { get; private set; }

    /// <summary>
    ///   Futures contract code
    /// </summary>
    public string ContractCode { get; private set; }

    /// <summary>
    ///   Currency of underlying deposit
    /// </summary>
    public Currency Currency { get; private set; }

    /// <summary>
    ///   Contract size
    /// </summary>
    public double ContractSize { get; private set; }

    /// <summary>
    ///   Size of a tick (minimum price move)
    /// </summary>
    public double TickSize { get; private set; }

    /// <summary>
    ///   Value of a tick for contracts with a fixed tick value, otherwise undefined
    /// </summary>
    public double TickValue { get; private set; }

    /// <summary>
    ///   Cash or physical settlement
    /// </summary>
    public SettlementType SettlementType { get; private set; }

    #region Futures Contract Dates

    /// <summary>
    ///   First trading date
    /// </summary>
    public Dt FirstTradingDate { get; private set; }

    /// <summary>
    ///   Referency day of month for last trading date
    /// </summary>
    public DayOfMonth LastTradingDayOfMonth { get; private set; }

    /// <summary>
    ///   Referency day of month for last delivery date
    /// </summary>
    public DayOfMonth LastDeliveryDayOfMonth { get; private set; }


    /// <summary>
    ///   Business days to add to <see cref="LastTradingDayOfMonth"/> for settlement/last delivery day
    /// </summary>
    public int LastDeliveryDayOffset { get; private set; }

    /// <summary>
    ///   Months to add to <see cref="LastTradingDayOfMonth"/> for last delivery day
    /// </summary>
    public int LastDeliveryMonthOffset { get; private set; }

    /// <summary>
    ///   Months to add to <see cref="LastTradingDayOfMonth"/> for last trading day
    /// </summary>
    public int LastTradingMonthOffset { get; private set; }

    /// <summary>
    ///   Business days to add to <see cref="LastTradingDayOfMonth"/> for last trading day
    /// </summary>
    public int LastTradingDayOffset { get; private set; }

    /// <summary>
    ///   Calendar for Futures dates
    /// </summary>
    public Calendar Calendar { get; private set; }

    #endregion Futures Contract Dates

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Create standard product given a date and a tenor
    /// </summary>
    /// <param name="asOf">As-of date (only used to estimate year if expiration code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Standard Future</returns>
    [ProductBuilder]
    public FutureBase GetProduct(Dt asOf, string expirationCode)
    {
      return GetFuture(asOf, expirationCode) as FutureBase;
    }

    /// <summary>
    ///   Create standard product given a date and a tenor
    /// </summary>
    /// <param name="asOf">As-of date (only used to estimate year if expiration code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Standard Future</returns>
    [ProductBuilder]
    public abstract T GetFuture(Dt asOf, string expirationCode);

    /// <summary>
    ///   Fill futures contract dates
    /// </summary>
    /// <remarks>
    ///   <para>The <paramref name="lastTradingDate"/> and  <paramref name="lastDeliveryDate"/> are calculated as business days
    ///   from a base reference date calculated from the futures month and year and the specified <see cref="LastTradingDayOfMonth"/>.</para>
    /// </remarks>
    /// <param name="asOf">As-of date (only used to estimate year if code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <param name="month">Returned contract month</param>
    /// <param name="year">Returned contract year</param>
    /// <param name="lastTradingDate">Returned last trading date</param>
    /// <param name="lastDeliveryDate">Returned last delivery date</param>
    protected void GetDates(Dt asOf, string expirationCode, out int month, out int year, out Dt lastTradingDate, out Dt lastDeliveryDate)
    {
      // Get exchange month and year from tenor name
      string contractCode;
      if (!Dt.ParseMonthYearFromExchangeCode(asOf, expirationCode, out contractCode, out month, out year))
        throw new ArgumentException($"Invalid futures contract identifier: {expirationCode}");

      // Settlement/last delivery
      var deliveryDate = Dt.DayOfMonth(month, year, LastDeliveryDayOfMonth, BDConvention.None, Calendar.None);
      var settleMonth = Dt.AddMonths(deliveryDate, LastDeliveryMonthOffset, CycleRule.None);
      lastDeliveryDate = Dt.Roll(Dt.AddDays(settleMonth, LastDeliveryDayOffset, Calendar), BDConvention.ModPreceding, Calendar);
      
      // Last trading date
      var tradingDate = Dt.DayOfMonth(month, year, LastTradingDayOfMonth, BDConvention.None, Calendar.None);
      var lastTradingMonth = Dt.Roll(Dt.AddMonths(tradingDate, LastTradingMonthOffset, CycleRule.None), BDConvention.ModPreceding, Calendar);
      lastTradingDate = Dt.Roll(Dt.AddDays(lastTradingMonth, LastTradingDayOffset, Calendar), BDConvention.ModPreceding, Calendar);
    }

    /// <summary>
    /// Create unique key for Futures Terms
    /// </summary>
    /// <param name="exchange">Futures Exchange</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <returns>Unique key</returns>
    public static string GetKey(string exchange, string contractCode)
    {
      return string.IsNullOrEmpty(exchange) ? $"{contractCode}.FUT"
        : $"{exchange}.{contractCode}.FUT";
    }

    /// <summary>
    /// Returns contract code
    /// </summary>
    /// <returns></returns>
    public string GetContractCode()
    {
      return ContractCode;
    }

    /// <summary>
    /// Returns exchange code
    /// </summary>
    /// <returns></returns>
    public string GetExchange()
    {
      return Exchange;
    }

    /// <summary>
    /// Returns the base name of a quote
    /// </summary>
    /// <returns></returns>
    public override string GetQuoteName(string tenor)
    {
      return Key;
    }

    #endregion Methods

    #region data
    private string _indexName = "";
    #endregion

  }
}
