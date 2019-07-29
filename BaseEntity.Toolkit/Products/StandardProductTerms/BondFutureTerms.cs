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
  ///   Terms for a market-standard Bond Future
  /// </summary>
  /// <remarks>
  ///   <para>Defines the terms for a market-standard <see cref="BondFuture"/>.</para>
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
  [DebuggerDisplay("Bond Future Terms")]
  [Serializable]
  public class BondFutureTerms : StandardFutureTermsBase<BondFuture>
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="exchange">Exchange</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="coupon">Nominal coupon of futures contract</param>
    /// <param name="convention">Futures quoting convention</param>
    /// <param name="term">Term in years of the nominal underlying bond (for ASX IndexYield quoted Futures)</param>
    /// <param name="ccy">Futures currency</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="lastTradingDayOfMonth">Reference day of month for last trading and last delivery dates</param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    public BondFutureTerms(
      string exchange, string contractCode, string description, Currency ccy, 
      double coupon, FuturesQuotingConvention convention, int term,
      double contractSize, double tickSize, double tickValue,
      DayOfMonth lastTradingDayOfMonth, int lastDeliveryDayOffset, int lastTradingDayOffset, Calendar calendar
      )
      : base(exchange, contractCode, description, "", ccy, contractSize, tickSize, tickValue, SettlementType.Cash, Dt.Empty,
      lastTradingDayOfMonth, lastDeliveryDayOffset, lastTradingDayOffset, calendar)
    {
      NominalCoupon = coupon;
      NominalTerm = term;
      QuoteConvention = convention;
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
    }

    #endregion Methods

    #region Properties
    /// <summary>
    /// Nominal coupon of futures contract
    /// </summary>
    public double NominalCoupon { get; set; }

    /// <summary>
    /// Term in years of the nominal underlying bond (for ASX IndexYield quoted Futures)
    /// </summary>
    public int NominalTerm { get; set; }

    /// <summary>
    /// Futures quoting convention
    /// </summary>
    public FuturesQuotingConvention QuoteConvention { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Create standard Bond Future given a date and an Futures expiration code
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
    /// </remarks>
    /// <param name="asOf">As-of date (only used to estimate year if expiration code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Standard <see cref="BondFuture"/></returns>
    public override BondFuture GetFuture(Dt asOf, string expirationCode)
    {
      // Get futures dates
      int month, year;
      Dt lastTrading, lastDelivery;
      GetDates(asOf, expirationCode, out month, out year, out lastTrading, out lastDelivery);

      BondFuture fut;
      if (QuoteConvention == FuturesQuotingConvention.Price)
      {
        // Price quoted futures (eg CME)
        double tickValue = (TickValue <= 0) ? ContractSize * TickSize / 100.0 : TickValue;
        fut = new BondFuture(lastDelivery, NominalCoupon, ContractSize, TickSize / 100.0)
        { TickValue = tickValue };
      }
      else
      {
        // Index yield quoted futures (eg ASX)
        fut = new BondFuture(lastDelivery, NominalCoupon, NominalTerm, ContractSize, TickSize / 100.0);
      }

      fut.Ccy = Currency;
      //fut.ContractCode = ContractCode;
      fut.SettlementType = SettlementType;
      fut.FirstTradingDate = FirstTradingDate;
      fut.LastTradingDate = lastTrading;
      fut.Validate();
      return fut;
    }

    #endregion Methods
  }
}
