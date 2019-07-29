//
// FRA.cs
//  -2008. All rights reserved.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Forward Rate Agreement (FRA) product
  /// </summary>
  /// <remarks>
  /// <para>A forward rate agreement (FRA) is an OTC contract between parties that determines the rate of interest,
  /// to be paid or received on an obligation beginning at a future start date. The contract will determine the rates 
  /// to be used along with the termination date and notional value. On this type of agreement, it is only the
  /// differential that is paid on the notional amount of the contract. It is paid on the effective date. The reference
  /// rate is fixed one or two days before the effective date, dependent on the market convention for the particular
  /// currency. FRAs are over-the counter derivatives. FRAs are very similar to swaps except that in a FRA a payment
  /// is only made once at maturity. Instruments such as interest rate swap could be viewed as a chain of FRAs.</para>
  /// <para>Many banks and large corporations will use FRAs to hedge future interest or exchange rate exposure.
  /// The buyer hedges against the risk of rising interest rates, while the seller hedges against the risk of falling
  /// interest rates. Other parties that use Forward Rate Agreements are speculators purely looking to make bets on
  /// future directional changes in interest rates.[citation needed] The development swaps in the 1980s provided
  /// organisations with an alternative to FRAs for hedging and speculating.</para>
  /// <para>In other words, a forward rate agreement (FRA) is a tailor-made, over-the-counter financial futures contract
  /// on short-term deposits. A FRA transaction is a contract between two parties to exchange payments on a deposit,
  /// called the Notional amount, to be determined on the basis of a short-term interest rate, referred to as the
  /// Reference rate, over a predetermined time period at a future date. FRA transactions are entered as a hedge against
  /// interest rate changes. The buyer of the contract locks in the interest rate in an effort to protect against an
  /// interest rate increase, while the seller protects against a possible interest rate decline. At maturity, no funds
  /// exchange hands; rather, the difference between the contracted interest rate and the market rate is exchanged.
  /// The buyer of the contract is paid if the reference rate is above the contracted rate, and the buyer pays to the seller
  /// if the reference rate is below the contracted rate. A company that seeks to hedge against a possible increase in
  /// interest rates would purchase FRAs, whereas a company that seeks an interest hedge against a possible decline of
  /// the rates would sell FRAs.</para>
  /// 
  /// <para><b>Payoff formula</b></para>
  /// <para>The netted payment made at the effective date is as follows:</para>
  /// <math>
  ///  \mbox{Payment} = \mbox{Notional Amount} * \left( \frac{(\mbox{Reference Rate}-\mbox{Fixed Rate}) * \alpha }{ 1 + \mbox{Reference Rate} * \alpha } \right)
  /// </math>
  /// <para>Where:</para>
  /// <list type="bullet">
  /// <item>The Fixed Rate is the rate at which the contract is agreed.</item>
  /// <item>The Reference Rate is typically Euribor or LIBOR.</item>
  /// <item><m>\alpha </m> is the ''day count fraction'', i.e. the portion of a year over which the rates are
  ///   calculated, using the [[day count convention]] used in the money markets in the underlying currency.
  ///   For EUR and USD this is generally the number of days divided by 360, for GBP it is the number of days
  ///   divided by 365 days.</item>
  /// <item>The Fixed Rate and Reference Rate are rates that should accrue over a period starting on the
  ///   effective date, and then paid at the end of the period (termination date). However, as the payment is
  ///   already known at the beginning of the period, it is also paid at the beginning. This is why the discount
  ///   factor is used in the denominator.</item>
  /// </list>
  ///
  /// <para><b>Notation</b></para>
  /// <para>FRAs are described in terms of the settlement date and final maturity.</para>
  /// <list type="table">
  ///   <listHeader><term>Notation</term><term>Settle Date</term><term>Maturity date</term><term>Underlying index</term></listHeader>
  ///   <item><description>1 x 4</description> <description>1 month</description> <description>4 months</description> <description>3 month LIBOR</description></item>
  ///   <item><description>1 x 7</description> <description>1 month</description> <description>7 months</description> <description>6 month LIBOR</description></item>
  ///   <item><description>3 x 6</description> <description>3 months</description> <description>6 months</description> <description>3 month LIBOR</description></item>
  ///   <item><description>3 x 9</description> <description>3 months</description> <description>9 months</description> <description>6 month LIBOR</description></item>
  ///   <item><description>6 x 12</description> <description>6 months</description> <description>1 year</description> <description>6 month LIBOR</description></item>
  ///   <item><description>12 x 18</description> <description>1 year</description> <description>18 months</description> <description>6 month LIBOR</description></item>
  /// </list>
  /// <para><i> .</i></para>
  /// </remarks>
  /// <seealso href="http://en.wikipedia.org/wiki/Forward_rate_agreement">Credit default swap. Wikipedia</seealso>
  /// <example>
  /// <para>The following example demonstrates constructing a Forward Rate Agreement.</para>
  /// <code language="C#">
  ///   // Create FRA
  ///   var fra = new FRA(
  ///     Dt.Today(),                // Effective date
  ///     Tenor.Parse("1m"),        // Settle tenor
  ///     Tenor.Parse("4m"),        // Maturity tenor (1 x 4)
  ///     0.04,                     // Fixed rate or strike
  ///     index                     // Index
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class FRA : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    ///<param name="effective">Effective date</param>
    ///<param name="maturity">Maturity of contract</param>
    ///<param name="freq">Frequency</param>
    ///<param name="strike">Fixed Rate</param>
    ///<param name="index">Settlement Rate Index</param>
    ///<param name="contractTenor">Deposit maturity tenor</param>
    ///<param name="ccy">Currency</param>
    ///<param name="dayCount">Day count</param>
    ///<param name="cal">Calendar</param>
    ///<param name="roll">BD Convention</param>
    public FRA(Dt effective, Dt maturity, Frequency freq, double strike, ReferenceIndex index,
      Tenor contractTenor, Currency ccy, DayCount dayCount, Calendar cal, BDConvention roll)
      : this(effective, maturity, freq, strike, index, Dt.LiborMaturity(maturity, contractTenor, cal, roll), ccy, dayCount, cal, roll)
    {}

    ///<summary>
    /// General constructor for a FRA contract
    ///</summary>
    ///<param name="effective">Effective date</param>
    ///<param name="maturity">Maturity of contract</param>
    ///<param name="freq">Frequency</param>
    ///<param name="strike">Fixed Rate</param>
    ///<param name="index">Settlement Rate Index</param>
    ///<param name="contractMaturity">Deposit maturity</param>
    ///<param name="ccy">Currency</param>
    ///<param name="dayCount">Day count</param>
    ///<param name="cal">Calendar</param>
    ///<param name="roll">BD Convention</param>
    public FRA(Dt effective, Dt maturity, Frequency freq, double strike, ReferenceIndex index,
               Dt contractMaturity, Currency ccy, DayCount dayCount, Calendar cal, BDConvention roll)
      : base(effective, Dt.Roll(maturity, roll, cal), Dt.Empty, Dt.Empty, ccy, freq, roll, cal, CycleRule.None, CashflowFlag.None)
    {
      ContractMaturity = contractMaturity;
      ReferenceIndex = index;
      Strike = strike;
      ContractPeriodDayCount = dayCount;
      Freq = freq;
      Calendar = cal;
      BDConvention = roll;
    }

    ///<summary>
    /// Constructor
    ///</summary>
    ///<param name="effective">Spot date of FRA</param>
    ///<param name="settlementTenor">Tenor between spot date and settlement date</param>
    ///<param name="freq">Floating payment frequency</param>
    ///<param name="strike">Fixing rate</param>
    ///<param name="index">Reference rate index</param>
    ///<param name="overallTenor">Tenor between spot date and maturity date of contract</param>
    ///<param name="ccy">Currency</param>
    ///<param name="dayCount">Day count</param>
    ///<param name="cal">Calendar</param>
    ///<param name="roll">BD convention</param>
    public FRA(Dt effective, Tenor settlementTenor, Frequency freq, double strike, ReferenceIndex index,
               Tenor overallTenor, Currency ccy, DayCount dayCount, Calendar cal, BDConvention roll)
      : this(effective, Dt.Roll(Dt.Add(effective, settlementTenor), roll, cal), freq, strike, index,
         new Tenor(overallTenor.N - settlementTenor.N, settlementTenor.Units), ccy, dayCount, cal, roll)
      // Above should be replaced with call to base() when we have time to test. RTD Mar'14
    {}
 
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective</param>
    /// <param name="settleTenor">Tenor to FRA settlement</param>
    /// <param name="maturityTenor">Tenor to FRA maturity</param>
    /// <param name="fixedrate">Fixed rate</param>
    /// <param name="index">Settlement index</param>
    /// <example>
    /// <para>The following example demonstrates constructing a Forward Rate Agreement.</para>
    /// <code language="C#">
    ///   // Create FRA
    ///   var fra = new FRA(
    ///     Dt.Today(),                // Effective date
    ///     Tenor.Parse("1m"),        // Settle tenor
    ///     Tenor.Parse("4m"),        // Maturity tenor (1 x 4)
    ///     0.04,                     // Fixed rate or strike
    ///     index                     // Index
    ///   );
    /// </code>
    /// </example>
    public FRA(Dt effective, Tenor settleTenor, Tenor maturityTenor, double fixedrate, ReferenceIndex index)
      : this(effective, Dt.Roll(Dt.Add(effective, settleTenor), index.Roll, index.Calendar), index.IndexTenor.ToFrequency(), fixedrate, index,
         new Tenor(maturityTenor.N - settleTenor.N, settleTenor.Units), index.Currency, index.DayCount, index.Calendar, index.Roll)
    {
      FixingLag = new Tenor(index.SettlementDays, TimeUnit.Days);
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <param name="errors"></param>
    /// <remarks>
    /// This tests only relationships between fields of the product that
    /// cannot be validated in the property methods.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Invalid index
      if (ReferenceIndex == null)
        InvalidValue.AddError(errors, this, "Index", String.Format("Invalid Floating Index. Can not be null"));

      if (ReferenceIndex != null && ReferenceIndex.IndexTenor == Tenor.Empty)
        InvalidValue.AddError(errors, this, "index_.IndexTenor", "Invalid index tenor, can not be empty");
    }

    /// <summary>
    ///   Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="asOf"></param>
    /// <param name="ps">Payment schedule</param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <param name="referenceCurve"></param>
    /// <param name="discountCurve"></param>
    /// <param name="settlementRate"></param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    public PaymentSchedule GetPaymentSchedule(Dt asOf, PaymentSchedule ps, Dt from, CalibratedCurve referenceCurve, DiscountCurve discountCurve, double? settlementRate)
    {
      if (from > Maturity)
        return ps ?? new PaymentSchedule();
      if (CustomPaymentSchedule != null && CustomPaymentSchedule.Count > 0)
      {
        if (ps == null)
          ps = new PaymentSchedule();
        foreach (var d in CustomPaymentSchedule.GetPaymentDates())
        {
          if (d >= from)
            ps.AddPayments(CustomPaymentSchedule.GetPaymentsOnDate(d));
        }
        return ps;
      }
      var projectionParams = GetProjectionParams();
      var rateProjector =  (ForwardRateCalculator) CouponCalculator.Get(asOf, ReferenceIndex, referenceCurve, discountCurve, projectionParams);
      // For FRA the projection period start/end dates are completely determined
      // by the product and they may be different than index tenors.
      rateProjector.EndSetByIndexTenor = false;
      if (ps == null)
        ps = new PaymentSchedule();
      var pmt = new FraInterestPayment(Maturity, Ccy, Maturity, ContractMaturity,
                                       Maturity, ContractMaturity, Notional, 0.0, Strike,
                                       ContractPeriodDayCount, rateProjector, null)
      {
        AccrualFactor = Dt.Fraction(Maturity, ContractMaturity, ContractPeriodDayCount)
      };

      if (settlementRate.HasValue)
        pmt.EffectiveRate = settlementRate.Value - Strike;
      ps.AddPayment(pmt);
      return ps;
    }

    /// <summary>
    /// Access resets information for cashflow generation
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="rateResets">Historical resets</param>
    /// <returns>Dictionary containing past and projected resets indexed by date</returns>
    public IDictionary<Dt, RateResets.ResetInfo> GetResetInfo(Dt asOf, RateResets rateResets)
    {
      IDictionary<Dt, RateResets.ResetInfo> allInfo = new SortedDictionary<Dt, RateResets.ResetInfo>();
      RateResetState state;
      double rate = RateResetUtil.FindRate(FixingDate, asOf, rateResets, true, out state);
      if (state == RateResetState.Missing && RateResetUtil.ProjectMissingRateReset(FixingDate, asOf, ContractMaturity))
      {
        // prefer a reset for expiry when expiry == asOf (or in the time window up to start of period)
        // but will take a projection otherwise
        state = RateResetState.IsProjected;
      }

      PaymentSchedule ps = GetPaymentSchedule(asOf, null, Dt.Empty, null, null, state == RateResetState.ResetFound ? rate : (double?)null);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Dt reset = ip.ResetDate;
        double? effectiveRate = null;
        if (ip.IsProjected)
        {
          rate = 0;
          state = RateResetState.IsProjected;
        }
        else
        {
          rate = RateResetUtil.FindRateAndReportState(reset, asOf, rateResets, out state);
          if (ip.RateResetState == RateResetState.ResetFound || ip.RateResetState == RateResetState.ObservationFound)
            effectiveRate = ip.EffectiveRate;
        }

        var rri = new RateResets.ResetInfo(reset, rate, state)
        {
          AccrualStart = ip.AccrualStart,
          AccrualEnd = ip.AccrualEnd,
          Frequency = ip.CompoundingFrequency,
          PayDt = ip.PayDt,
          IndexTenor = new Tenor(ip.CompoundingFrequency),
          ResetInfos = ip.GetRateResetComponents()
        };
        if (effectiveRate.HasValue)
          rri.EffectiveRate = effectiveRate;

        allInfo[reset] = rri;
      }
      return allInfo;
    }

    internal static ProjectionParams GetProjectionParams()
    {
      return new ProjectionParams
      {
        ProjectionType = ProjectionType.SimpleProjection
      };
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Strike rate
    /// </summary>
    [Category("Base")]
    public double Strike { get; set; }

    /// <summary>
    ///   daycount for computing the payment period fraction
    /// </summary>
    [Category("Base")]
    public DayCount ContractPeriodDayCount { get; set; }


    /// <summary>
    ///   Floating rate index
    /// </summary>
    [Category("Base")]
    public ReferenceIndex ReferenceIndex { get; private set; }

    ///<summary>
    /// Maturity of the underlying contract
    ///</summary>
    [Category("Base")]
    public Dt ContractMaturity { get; set; }


    ///<summary>
    ///The lag between fixing date and settlement date
    ///</summary>
    public Tenor FixingLag { get; set; }

    ///<summary>
    /// Settlement rate determined from the rate index on fixing date
    ///</summary>
    public double? SettlementRate { get; set; }

    ///<summary>
    /// The date on which the reference rate is determined.
    ///</summary>
    public Dt FixingDate
    {
      get { return Maturity == Dt.Empty ? Dt.Empty : Dt.AddDays(Maturity, -FixingLag.N, Calendar); }
    }

    #endregion Properties
  }
}