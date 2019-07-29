//
// CommodityFutureTerms.cs
//   2015. All rights reserved.
//
using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Terms for a market-standard Commodity Future
  /// </summary>
  /// <remarks>
  ///   <para>Defines the terms for a market-standard <see cref="CommodityFuture"/>.</para>
  ///   <para>Futures are identified uniquely by their futures contract code.</para>
  ///   <para>The function <see cref="GetFuture(Dt,string)"/> creates the Future from the Futures Terms.</para>
  ///   <inheritdoc cref="GetFuture(Dt,string)"/>
  ///   <example>
  ///   <para>The following example demonstrates creating a Future based on standard terms.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var contractCode = "CL";
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
  ///     var contractCode = "CL";
  ///     var expirationCode = "Z16";
  ///     // Create product
  ///     var future =  StandardProductTermsUtil.GetStandardFuture(contractCode, asOf, expirationCode);
  ///   </code>
  ///   </example>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="GetFuture(Dt,string)"/>
  /// <seealso cref="CommodityFuture"/>
  [DebuggerDisplay("Commodity Future Terms")]
  [Serializable]
  public class CommodityFutureTerms : StandardFutureTermsBase<CommodityFuture>
  {
    #region Constructors

    /// <summary>
    ///   Constructor with month offset
    /// </summary>
    /// <param name="exchange">Exchange</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="ccy">Futures currency</param>
    /// <param name="indexName">Commodity price index</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="lastTradingDayOfMonth">Reference day of month for last trading date</param>
    /// <param name="lastDeliveryDayOfMonth">Reference day of month for last delivery date</param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="lastTradingMonthOffset">Months to add for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    public CommodityFutureTerms(
      string exchange, string contractCode, string description, Currency ccy, string indexName,
      double contractSize, double tickSize, double tickValue,
      DayOfMonth lastDeliveryDayOfMonth, int lastDeliveryDayOffset,
      DayOfMonth lastTradingDayOfMonth, int lastTradingDayOffset, int lastTradingMonthOffset, Calendar calendar
      )
      : base(exchange, contractCode, description, indexName, ccy, contractSize, tickSize, tickValue, SettlementType.Cash, Dt.Empty,
      lastTradingDayOfMonth, lastTradingDayOffset, lastTradingMonthOffset, 
      lastDeliveryDayOfMonth, lastDeliveryDayOffset, 0, // No month offset for now
      calendar)
    {
      IndexName = indexName;
    }

    #endregion

    #region Properties
    /// <summary>
    ///   Commodity index name
    /// </summary>
    public string IndexName { get; private set; }
    #endregion

    #region Methods

    /// <summary>
    ///   Create standard Commodity Future given a reference date and an Futures expiration code
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
    ///       <term><see cref="FutureBase.SettlementType"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.SettlementType"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to the Terms <see cref="StandardFutureTermsBase{T}.Currency"/>.</description>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <seealso cref="CommodityFuture"/>
    /// <param name="asOf">As-of date (only used to estimate year if expiration code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Standard <see cref="CommodityFuture"/></returns>
    public override CommodityFuture GetFuture(Dt asOf, string expirationCode)
    {
      // Get futures dates
      int month, year;
      Dt lastTrading, lastDelivery;
      GetDates(asOf, expirationCode, out month, out year, out lastTrading, out lastDelivery);

      // Find index matching name
      var commodityIndex = CommodityReferenceRate.Get(IndexName);
      var priceIndex = new BaseEntity.Toolkit.Base.ReferenceIndices.CommodityPriceIndex(commodityIndex);
      var fut = new CommodityFuture(lastDelivery, ContractSize, TickSize, priceIndex)
      {
        Ccy = Currency,
        //ContractCode = ContractCode,
        SettlementType = SettlementType,
        FirstTradingDate = FirstTradingDate,
        LastTradingDate = lastTrading,
      };
      fut.Validate();
      return fut;
    }

    #endregion Methods

  }
}
