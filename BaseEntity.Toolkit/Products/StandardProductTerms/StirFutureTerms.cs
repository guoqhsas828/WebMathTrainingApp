//
// StirFutureTerms.cs
//  -2014. All rights reserved.
//
using System;
using System.Collections;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Terms for a market-standard STIR Future
  /// </summary>
  /// <remarks>
  ///   <para>Defines the terms for a market-standard <see cref="StirFuture"/>. There are two types of Stir Futures - those based on deposits
  ///   (eg Eurodollar Futures), and those based on discount prices (eg Bill Futures).</para>
  ///   <para>Futures are identified uniquely by their futures contract code.</para>
  ///   <para>The function <see cref="GetFuture(Dt,string)"/> creates the Future from the Futures Terms.</para>
  ///   <inheritdoc cref="GetFuture(Dt,string)"/>
  ///   <example>
  ///   <para>The following example demonstrates creating a Future based on standard terms.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var contractCode = "ED";
  ///     var expirationCode = "Z16";
  ///     // Look up product terms
  ///     var terms = StandardProductTermsUtil.GetFutureTerms(contractCode);
  ///     // Create product
  ///     var future = terms.GetProduct(asOf, expirationCode);
  ///   </code>
  ///   <para>A convenience function is provided to simplify creating a standard product directly.
  ///   The following example demonstrates creating a Future using this convenience function.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var contractCode = "ED";
  ///     var expirationCode = "Z16";
  ///     // Create product
  ///     var future =  StandardProductTermsUtil.GetStandardFuture(contractCode, asOf, expirationCode);
  ///   </code>
  ///   </example>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="GetFuture(Dt,string)"/>
  /// <seealso cref="StirFuture"/>
  [DebuggerDisplay("STIR Future Terms")]
  [Serializable]
  public class StirFutureTerms : StandardFutureTermsBase<StirFuture>
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="exchange">Exchange</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="rateFutureType">Rate Future type</param>
    /// <param name="indexName">Reference index name</param>
    /// <param name="tenor">Underlying deposit tenor</param>
    /// <param name="ccy">Futures currency</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="lastTradingDayOfMonth">Reference day of month for last trading and last delivery dates</param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    public StirFutureTerms(
      string exchange,
      string contractCode, string description, RateFutureType rateFutureType,
      string indexName, Tenor tenor, Currency ccy,
      double contractSize, double tickSize, double tickValue,
      DayOfMonth lastTradingDayOfMonth, int lastDeliveryDayOffset, int lastTradingDayOffset, Calendar calendar
      )
      : base(exchange, contractCode, description, indexName, ccy, contractSize, tickSize, tickValue, SettlementType.Cash, Dt.Empty,
      lastTradingDayOfMonth, lastDeliveryDayOffset, lastTradingDayOffset, calendar)
    {
      RateFutureType = rateFutureType;
      IndexName = indexName;
      Tenor = tenor;
    }

    #endregion

    #region Validate

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

      if (RateFutureType.IsDiscountRateFutureType())
      {
        if (!String.IsNullOrEmpty(IndexName))
          InvalidValue.AddError(errors, this, "IndexName", "IndexName must NOT be specified for a discount rate future");
      }
      else
      {
        if (String.IsNullOrEmpty(IndexName))
          InvalidValue.AddError(errors, this, "IndexName", "IndexName must be specified for a deposit based future");
      }
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Rate future type
    /// </summary>
    public RateFutureType RateFutureType { get; private set; }

    /// <summary>
    ///   Rate index name
    /// </summary>
    public string IndexName { get; private set; }

    /// <summary>
    ///   Underlying deposit tenor
    /// </summary>
    public Tenor Tenor { get; private set; }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Create standard STIR Future given a date and an Futures expiration code
    /// </summary>
    /// <remarks>
    ///   <para>Given the futures terms, the future is created given a date and a futures expiration code.
    ///   The date and expiration code are used to identify the futures expiration month and year. Valid formats for the
    ///   expiration code include Z6, Z16, Z2016, and DEC16. The future is derived from the terms as follows:</para>
    ///   <para>All futures dates are calculated relative to a base reference date implied from the month and year of expiration and the specified
    ///   Terms <see cref="StandardFutureTermsBase{T}.LastTradingDayOfMonth"/> and the specified Terms <see cref="StandardFutureTermsBase{T}.Calendar"/>.</para>
    ///   <list type="table">
    ///     <listheader><term>Future Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="FutureBase.LastDeliveryDate"/></term>
    ///       <description>Calculated as <see cref="StandardFutureTermsBase{T}.LastDeliveryDayOffset"/> business days after the base reference date
    ///       with the specified Terms <see cref="StandardFutureTermsBase{T}.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FutureBase.LastTradingDate"/></term>
    ///       <description>Calculated as <see cref="StandardFutureTermsBase{T}.LastTradingDayOffset"/> business days after the base reference date
    ///       with the specified Terms <see cref="StandardFutureTermsBase{T}.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FutureBase.ContractSize"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.ContractSize"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FutureBase.TickSize"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.TickSize"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FutureBase.TickValue"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.TickValue"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FutureBase.SettlementType"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.SettlementType"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.Currency"/>.</description>
    ///     </item>
    ///   </list>
    /// 
    ///   <para><b>Deposit rate futures</b></para>
    ///   <para>For deposit-based futures, a <see cref="StirFutureTerms.IndexName"/> must be specified and identifies the underlying
    ///   reference <see cref="InterestReferenceRate">Interest Rate Index</see>.</para>
    ///   <para>For futures where the <see cref="StandardFutureTermsBase{T}.LastTradingDayOfMonth">base reference day</see> is the last day of the month,
    ///   the underlying deposit is taken as settling on the first business day of the month and maturing on the last business day of the month.</para>
    ///   <para>Otherwise, the underlying deposit settles on the <see cref="StirFuture.LastDeliveryDate">Last delivery date</see>
    ///   and matures at a date defined by the tenor, roll, and calendar of the <see cref="InterestReferenceRate">Rate Index</see>.</para>
    /// 
    ///   <para><b>Discount rate futures</b></para>
    ///   <para>For discount futures, <see cref="StirFutureTerms.IndexName"/> is ignored.</para>
    ///   <para>The underlying deposit settles on the <see cref="StirFuture.LastDeliveryDate">Last delivery date</see>
    ///   and matures on a date specified by the <see cref="StirFutureTerms.Tenor"/>.</para>
    /// </remarks>
    /// <param name="asOf">As-of date (only used to estimate year if expiration code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Standard <see cref="StirFuture"/></returns>
    public override StirFuture GetFuture(Dt asOf, string expirationCode)
    {
      // Get futures dates
      int month, year;
      Dt lastTrading, lastDelivery;
      GetDates(asOf, expirationCode, out month, out year, out lastTrading, out lastDelivery);

      StirFuture fut;
      if (RateFutureType.IsDiscountRateFutureType())
      {
        // Discount future
        var depositAccrualStart = lastDelivery;
        var depositAccrualEnd = Dt.Add(lastDelivery, Tenor);
        fut = new StirFuture(RateFutureType, lastDelivery, depositAccrualStart, depositAccrualEnd, null,
          ContractSize, TickSize, TickValue);
      }
      else
      {
        // Deposit rate future
        // Find index matching name, tenor and currency
        var rateIndex = InterestReferenceRate.Get(IndexName);
        // Underlying deposit accrual period
        // Note: Calendar and roll convention is taken from underlying rate index.
        var depositAccrualStart = (LastTradingDayOfMonth == DayOfMonth.Last) ? Dt.Roll(new Dt(1, month, year), rateIndex.BDConvention, rateIndex.Calendar) : lastDelivery;
        var depositAccrualEnd = (LastTradingDayOfMonth == DayOfMonth.Last) ? lastDelivery : Dt.Roll(Dt.Add(lastDelivery, Tenor), rateIndex.BDConvention, rateIndex.Calendar);
        var referenceIndex = new BaseEntity.Toolkit.Base.ReferenceIndices.InterestRateIndex(rateIndex, Tenor);
        fut = new StirFuture(RateFutureType, lastDelivery, depositAccrualStart, depositAccrualEnd, referenceIndex,
          ContractSize, TickSize, TickValue);
      }
      fut.Ccy = Currency;
      //fut.ContractCode = ContractCode;
      fut.SettlementType = SettlementType;
      fut.FirstTradingDate = FirstTradingDate;
      fut.LastTradingDate = lastTrading;
      fut.Validate();
      return fut;
    }

    /// <summary>
    /// Has index tenor
    /// </summary>
    /// <returns></returns>
    public override bool HasTenor()
    {
      return true;
    }

    /// <summary>
    /// Returns index tenor
    /// </summary>
    /// <returns></returns>
    public override Tenor GetTenor()
    {
      return Tenor;
    }

    #endregion Methods

  }
}
