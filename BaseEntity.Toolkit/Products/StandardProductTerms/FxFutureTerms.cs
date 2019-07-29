//
// FxFutureTerms.cs
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
  ///   Terms for a market-standard Fx Future
  /// </summary>
  /// <remarks>
  ///   <para>Defines the terms for a market-standard <see cref="FxFuture"/>.</para>
  ///   <para>Futures are identified uniquely by their futures contract code.</para>
  ///   <para>The function <see cref="GetFuture(Dt,string)"/> creates the Future from the Futures Terms.</para>
  ///   <inheritdoc cref="GetFuture(Dt,string)"/>
  ///   <example>
  ///   <para>The following example demonstrates creating a Future based on standard terms.</para>
  ///   <code language="C#">
  ///     // Define terms
  ///     var asOf = Dt.Today();
  ///     var contractCode = "AD";
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
  ///     var contractCode = "AD";
  ///     var expirationCode = "Z16";
  ///     // Create product
  ///     var future =  StandardProductTermsUtil.GetStandardFuture(contractCode, asOf, expirationCode);
  ///   </code>
  ///   </example>
  /// </remarks>
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="GetFuture(Dt,string)"/>
  /// <seealso cref="FxFuture"/>
  [DebuggerDisplay("Fx Future Terms")]
  [Serializable]
  public class FxFutureTerms : StandardFutureTermsBase<FxFuture>
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="exchange">Exchange</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="receiveCcy">Receive currency</param>
    /// <param name="payCcy">Currency of Fx</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="dayOfMonth">Reference day of month for last trading and last delivery dates</param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="dayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="dayOfMonth"/> for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    public FxFutureTerms(
      string exchange, string contractCode, string description, Currency receiveCcy, Currency payCcy,
      double contractSize, double tickSize, double tickValue,
      DayOfMonth dayOfMonth, int lastDeliveryDayOffset, int lastTradingDayOffset, Calendar calendar
      )
        : this(exchange, contractCode, description, receiveCcy.ToString() + payCcy.ToString(), 
          receiveCcy, payCcy, contractSize, tickSize, tickValue, 
          dayOfMonth, lastDeliveryDayOffset, lastTradingDayOffset, calendar)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="exchange">Exchange</param>
    /// <param name="contractCode">Futures contract code</param>
    /// <param name="description">Description</param>
    /// <param name="indexName">Currency pair index name</param>
    /// <param name="receiveCcy">Receive currency</param>
    /// <param name="payCcy">Currency of Fx</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    /// <param name="lastTradingDayOfMonth">Reference day of month for last trading and last delivery dates</param>
    /// <param name="lastDeliveryDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last delivery/settlement day</param>
    /// <param name="lastTradingDayOffset">Business days to add to <paramref name="lastTradingDayOfMonth"/> for last trading day</param>
    /// <param name="calendar">Calendar for Futures dates</param>
    public FxFutureTerms(
      string exchange, string contractCode, string description, string indexName, Currency receiveCcy, Currency payCcy,
      double contractSize, double tickSize, double tickValue,
      DayOfMonth lastTradingDayOfMonth, int lastDeliveryDayOffset, int lastTradingDayOffset, Calendar calendar
      )
      : base(exchange, contractCode, description, indexName, payCcy, contractSize, tickSize, tickValue, SettlementType.Cash, Dt.Empty,
        lastTradingDayOfMonth, lastDeliveryDayOffset, lastTradingDayOffset, calendar)
    {
      IndexName = indexName;
      ReceiveCcy = receiveCcy;
      PayCcy = payCcy;
    }

    #endregion

    #region Properties
    /// <summary>
    ///   FX index name
    /// </summary>
    public string IndexName { get; private set; }
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
      if (PayCcy == Currency.None)
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
    public Currency PayCcy { get; private set; }

    /// <summary>
    /// Receive currency
    /// </summary>
    public Currency ReceiveCcy { get; private set; }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Create standard Fx Future given a reference date and an Futures expiration code
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
    ///       <term><see cref="FxFuture.ReceiveCcy"/></term>
    ///       <description>Set to the Terms <see cref="FxFutureTerms.ReceiveCcy"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FxFuture.PayCcy"/></term>
    ///       <description>Set to the Terms <see cref="FxFutureTerms.PayCcy"/>.</description>
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
    /// <seealso cref="FxFuture"/>
    /// <param name="asOf">As-of date (only used to estimate year if expiration code has single year digit)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Standard <see cref="StirFuture"/></returns>
    public override FxFuture GetFuture(Dt asOf, string expirationCode)
    {
      // Get futures dates
      int month, year;
      Dt lastTrading, lastDelivery;
      GetDates(asOf, expirationCode, out month, out year, out lastTrading, out lastDelivery);
      var fut = new FxFuture(ReceiveCcy, PayCcy, lastDelivery, ContractSize, TickSize)
      {
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
