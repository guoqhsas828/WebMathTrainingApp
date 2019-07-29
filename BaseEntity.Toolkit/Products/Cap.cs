//
// Cap.cs
//  -2008. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Interest rate cap or floor
  /// </summary>
  /// <remarks>
  /// <para>Interest Rate Caps/Floors are OTC contracts where counterparties exchange the
  /// excess of a floating rate interest payments.</para>
  /// <para>The buyer of an interest rate cap pays the seller a premium in return for the right
  /// to receive the difference in the interest cost on some notional principal amount
  /// any time a specified index of market interest rates rises above a stipulated "cap rate."
  /// The buyer bears no obligation or liability if interest rates fall below the
  /// cap rate, however. Thus, a cap resembles an option in that it represents a right
  /// rather than an obligation to the buyer.</para>
  ///
  /// <para><b>Market Conventions</b></para>
  /// <para>An interest rate cap is characterized by:</para>
  /// <list type="bullet">
  ///   <item>a notional principal amount upon which interest payments are based;</item>
  ///   <item>an interest rate index, typically some specified maturity of LIBOR;</item>
  ///   <item>a cap rate, which is equivalent to a strike or exercise price on an option; and</item>
  ///   <item>the period of the agreement, including payment dates and interest rate reset dates.</item>
  /// </list>
  /// <para>Payment schedules for interest rate caps follow conventions in the interest
  /// rate swap market. Payment amounts are determined by the value of the index
  /// rate on a series of interest rate reset dates. Intervals between interest rate reset
  /// dates and scheduled payment dates typically coincide with the term of the
  /// interest rate index. Thus, interest rate reset dates for a cap indexed to six-
  /// month LIBOR would occur ever six months with payments due six months
  /// later. Cap buyers typically schedule interest rate reset and payment intervals to
  /// coincide with interest payments on outstanding variable-rate debt. Interest rate
  /// caps cover periods ranging from one to ten years with interest rate reset and
  /// payment dates most commonly set either three or six months apart.
  /// If the specified market index is above the cap rate, the seller pays the buyer
  /// the difference in interest cost on the next payment date. The amount of the
  /// payment is determined by the formula:</para>
  /// <math>
  ///   N*max(0, r-r_c)(d_t / 360)
  /// </math>
  /// <para>where:</para>
  /// <list type="bullet">
  ///   <item><m> N </m> is the notional</item>
  ///   <item><m> r </m> is the current rate</item>
  ///   <item><m> r_c </m> is the cap rate (strike)</item>
  ///   <item><m> d_t </m> is the das from the interest rate reset date to the payment date</item>
  /// </list>
  /// <para>The payoff of a one period cap is similar to a call option.</para>
  /// <h1 align="center"><img src="CapPayoff.png" width="70%" align="middle"/></h1>
  /// <para>The buyer of an interest rate floor pays the seller a premium in return for
  /// the right to receive the difference in interest payable on a notional principal
  /// amount when a specified index interest rate falls below a stipulated minimum,
  /// or "floor rate." Buyers use floors to fix a minimum interest rate on an asset
  /// paying a variable interest rate indexed to some maturity of LIBOR. Like an
  /// interest rate cap, a floor is an option-like agreement in that it represents a right
  /// rather than an obligation to the buyer. The buyer of an interest rate floor incurs
  /// no obligation if the index interest rate rises above the floor rate, so the most a
  /// buyer can lose is the premium paid to the seller at the outset of the agreement.
  /// The payment received by the buyer of an interest rate floor is determined
  /// by the formula:</para>
  /// <math>
  ///   N*max(0, r_f-r)(d_t / 360)
  /// </math>
  /// <para>where:</para>
  /// <list type="bullet">
  ///   <item><m> N </m> is the notional</item>
  ///   <item><m> r_f </m> is the floor rate (strike)</item>
  ///   <item><m> r </m> is the current rate</item>
  ///   <item><m> d_t </m> is the das from the interest rate reset date to the payment date</item>
  /// </list>
  /// <para><i>Source: Kuprianov</i></para>
  /// </remarks>
  /// <seealso cref="CapFloorPricer"/>
  /// <seealso href="http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.195.4578&amp;rep=rep1&amp;type=pdf">Over-the-Counter Interest Rate Derivatives. Kuprianov</seealso>
  /// <example>
  /// <para>The following example demonstrates constructing an Interest Rate Cap.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                                  // Effective date is today
  ///   Dt maturity = Dt.Add(effectiveDate, 5, TimeUnit.Years);         // Maturity date is 5Yrs after effective
  ///
  ///   Cap cap =
  ///     new Cap( effectiveDate,                               // Effective date
  ///              maturityDate,                                // Maturity date
  ///              Currency.EUR,                                // Currency is Euros
  ///              0.04,                                        // Strike is 4%
  ///              DayCount.Actual360,                          // Acrual Daycount is Actual/360
  ///              Frequency.Quarterly,                         // Quarterly payment frequency
  ///              Calendar.TGT )                               // Calendar is Target
  ///             );
  /// </code>
  /// </example>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class Cap : CapBase
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="type">Cap or floor</param>
    /// <param name="strike">Strike rate</param>
    /// <param name="dayCount">Daycount</param>
    /// <param name="freq">Frequency</param>
    /// <param name="bdc">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <remarks>Notes: The rate reset settlement defaults to two business days.
    /// The floating rate tenor defaults to matching the payment frequency.</remarks>
    public Cap(Dt effective, Dt maturity, Currency ccy,
      CapFloorType type, double strike,
      DayCount dayCount, Frequency freq, BDConvention bdc, Calendar cal)
      : base(new InterestRateIndex("LIBOR", new Tenor(freq), ccy, dayCount, cal, bdc, 2),
        effective, maturity, ccy, type, strike, dayCount, freq, bdc, cal)
    {
    }

    /// <summary>
    ///   Interest rate index that is capped/floored.
    /// </summary>
    [Category("Base")]
    public InterestRateIndex RateIndex
    {
      get { return (InterestRateIndex) ReferenceRateIndex; }
      set { ReferenceRateIndex = value; }
    }
  }

  /// <summary>
  ///  Base class for Caps and Floors
  /// </summary>
  [Serializable]
  public abstract partial class CapBase : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="referenceRateIndex">Index of the reference rate.</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="type">Cap or floor</param>
    /// <param name="strike">Strike rate</param>
    /// <param name="dayCount">Daycount</param>
    /// <param name="freq">Frequency</param>
    /// <param name="bdc">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <remarks>Notes: The rate reset settlement defaults to two business days.
    /// The floating rate tenor defaults to matching the payment frequency.</remarks>
    protected CapBase(ReferenceIndex referenceRateIndex,
      Dt effective, Dt maturity, Currency ccy, CapFloorType type, double strike,
      DayCount dayCount, Frequency freq, BDConvention bdc, Calendar cal)
      : base(effective, maturity, Dt.Empty, Dt.Empty,
        ccy, freq, bdc, cal, CycleRule.None, CashflowFlag.None)
    {
      // Use properties to get validation
      Type = type;
      Strike = strike;
      DayCount = dayCount;
      ReferenceRateIndex = referenceRateIndex;
      RateResetOffset = 2;
      AmortizationSchedule = new List<Amortization>();
      StrikeSchedule = new List<CouponPeriod>();
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (CapBase)base.Clone();

      obj.AmortizationSchedule = CloneUtil.CloneToGenericList(AmortizationSchedule);
      obj.StrikeSchedule = CloneUtil.CloneToGenericList(StrikeSchedule);
      obj._indexMultiplierSched = CloneUtil.Clone(_indexMultiplierSched);

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Strike has to be btw 0.0 and 2.0
      if (Strike < -1.0 || Strike > 2.0)
        InvalidValue.AddError(errors, this, "Strike",
          String.Format("Invalid strike. Must be between -1 and 2, Not {0}", Strike));

      // Rate Reset Offset has to be non-negative
      if (RateResetOffset < 0)
        InvalidValue.AddError(errors, this, "RateResetOffset",
          String.Format("Invalid rate reset offset. Must be +ve, not ({0})", RateResetOffset));

      // Collections
      AmortizationUtil.Validate(AmortizationSchedule, errors);
      CouponPeriodUtil.Validate(StrikeSchedule, errors);
      CouponPeriodUtil.Validate(IndexMultiplierSchedule, errors);
    }

    

    /// <summary>
    /// Generates the payment schedule for a given date.
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="resets"></param>
    /// <returns></returns>
    public PaymentSchedule GetPaymentSchedule(Dt asOf, RateResets resets)
    {
      PaymentSchedule pmtSchedule;
      if (HasCustomSchedule())
      {
        pmtSchedule = PaymentScheduleUtils.CreateCopy(CustomPaymentSchedule);
        // Update the rate reset state in the items. The reset rates themselves have been populated by this time (if available) in
        // CapletRiskPayment.ToToolkitPayment(); however, the rate reset STATE needs to be updated at this point since it depends on the
        // asOf date.
        foreach (CapletPayment cPmt in pmtSchedule.GetPaymentsByType<CapletPayment>())
        {
          Dt expiry = cPmt.Expiry;
          Dt forwardDate = cPmt.RateFixing;
          // replicate the logic for the rate reset state from below.
          RateResetState state;
          double rate = RateResetUtil.FindRate(expiry, asOf, resets, true, out state);
          if (state == RateResetState.Missing && RateResetUtil.ProjectMissingRateReset(expiry, asOf, forwardDate))
          {
            // prefer a reset for expiry when expiry == asOf (or in the time window up to start of period)
            // but will take a projection otherwise
            state = RateResetState.IsProjected;
          }
          else
          {
            cPmt.Rate = rate;
          }
          cPmt.OptionDigitalType = OptionDigitalType;
          cPmt.RateResetState = state;
        }
      }
      else
      {
        // Go through caplet schedule
        pmtSchedule = new PaymentSchedule();
        var schedule = Schedule;
        for (int i = 0; i < schedule.Count; i++)
        {
          // Calc expiry
          // rateReset date is the date the RateReset is physically published 
          Dt forwardDate = schedule.GetPeriodStart(i);
          Dt expiry = Dt.AddDays(schedule.GetPeriodStart(i), -RateResetOffset, Calendar);
          Dt payDate = schedule.GetPaymentDate(i);

          RateResetState state;
          double rate = RateResetUtil.FindRate(expiry, asOf, resets, true, out state);
          if (state == RateResetState.Missing && RateResetUtil.ProjectMissingRateReset(expiry, asOf, forwardDate))
          {
            // prefer a reset for expiry when expiry == asOf (or in the time window up to start of period)
            // but will take a projection otherwise
            state = RateResetState.IsProjected;
          }

          var capletPmt = new CapletPayment
          {
            Expiry = expiry,
            PayDt = payDate,
            Strike = CouponPeriodUtil.CouponAt(StrikeSchedule, Strike, payDate),
            OptionDigitalType = OptionDigitalType,
            DigitalFixedPayout = OptionDigitalType != OptionDigitalType.None
              ? (DigitalFixedPayoutSchedule.LastOrDefault(d => d.Item1 <= forwardDate) ??
                new Tuple<Dt, double>(payDate, DigitalFixedPayout)).Item2
              : 0.0,
            Rate = rate,
            RateResetState = state,
            Notional = AmortizationSchedule.PrincipalAt(Notional, payDate),
            Ccy = Ccy,
            Type = Type,
            PeriodFraction = schedule.Fraction(i, DayCount),
            RateFixing = forwardDate,
            IndexMultiplier = CouponPeriodUtil.CouponAt(this.IndexMultiplierSchedule, 1.0, payDate)
          };

          // Add
          pmtSchedule.AddPayment(capletPmt);
        }
      }

      if (pmtSchedule != null)
      {
        foreach (var pmt in pmtSchedule.OfType<CapletPayment>())
        {
          // For custom payments the Amount will now appear to have been 
          // overridden to zero - reset that, since the RateResetState has changed,
          // and the amount needs to be recomputed
          pmt.ResetAmountOverride();

          Dt tenorDate = Dt.Add(pmt.RateFixing, ReferenceRateIndex.IndexTenor);
          tenorDate = Dt.Roll(tenorDate, BDConvention, Calendar);
          pmt.TenorDate = tenorDate;
        }
      }

      // Done
      return pmtSchedule;
    }

    /// <summary>
    ///   True if the payment schedule is custom
    /// </summary>
    /// <returns></returns>
    public bool HasCustomSchedule()
    {
      return CustomPaymentSchedule != null && CustomPaymentSchedule.Count > 0;
    }

    /// <summary>
    ///   Indicates whether a custom index multiplier is present
    /// </summary>
    public bool HasIndexMultiplier()
    {
      return (IndexMultiplierSchedule != null && IndexMultiplierSchedule.Any());
    }

    /// <summary>
    ///   Returns reset information for all caplets
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="rateResets"></param>
    /// <param name="interestRateIndex"></param>
    /// <returns></returns>
    public IDictionary<Dt, RateResets.ResetInfo> GetResetInfo(Dt asOf,
      RateResets rateResets, ReferenceIndex interestRateIndex)
    {
      var allInfo = new SortedDictionary<Dt, RateResets.ResetInfo>();
      var ps = GetPaymentSchedule(asOf, rateResets);

      foreach (var payment in ps.GetPaymentsByType<CapletPayment>())
      {
        RateResetState state;
        Dt reset = payment.Expiry;
        double rate = RateResetUtil.FindRateAndReportState(reset, asOf, rateResets, out state);
        var rri = new RateResets.ResetInfo(reset, rate, state);
        allInfo[reset] = rri;
      }
      return allInfo;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Digital option type,
    ///   None,
    ///   Cash <m>C 1_{\{S_T > K\}}, </m>
    ///   Asset <m>S_T  1_{\{S_T > K\}}.</m>
    /// </summary>
    [Category("Base")]
    public OptionDigitalType OptionDigitalType { get; set; }

    /// <summary>
    ///   Option type
    /// </summary>
    [Category("Base")]
    public OptionType OptionType
    {
      get { return (Type == CapFloorType.Cap ? OptionType.Call : OptionType.Put); }
    }

    /// <summary>
    ///   Option type
    /// </summary>
    [Category("Base")]
    public CapFloorType Type { get; set; }

    /// <summary>
    ///   Strike rate
    /// </summary>
    [Category("Base")]
    public double Strike { get; set; }

    /// <summary>
    ///   The single payoff rate if the pay-off type is digital
    /// </summary>
    [Category("Base")]
    public double DigitalFixedPayout { get; set; }

    /// <summary>
    ///   Daycount
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    /// <summary>
    ///   Interest rate index that is capped/floored.
    /// </summary>
    [Category("Base")]
    public ReferenceIndex ReferenceRateIndex { get; set; }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<CouponPeriod> IndexMultiplierSchedule
    {
      get { return _indexMultiplierSched ?? (_indexMultiplierSched = new List<CouponPeriod>()); }
      set
      {
        if (value == null)
        {
          if (!_indexMultiplierSched.IsNullOrEmpty())
          {
            _indexMultiplierSched.Clear();
          }
          return;
        }
        if (_indexMultiplierSched != null) _indexMultiplierSched.Clear();
        else _indexMultiplierSched = new List<CouponPeriod>();
        _indexMultiplierSched.AddRange(value);
      }
    }

    /// <summary>
    ///   Rate Reset offset
    /// </summary>
    [Category("Base")]
    public int RateResetOffset { get; set; }

    /// <summary>
    ///   Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule { get; set; }

    /// <summary>
    ///   True if cap amortizes
    /// </summary>
    [Category("Schedule")]
    public bool IsAmortizing
    {
      get { return !(AmortizationSchedule == null || AmortizationSchedule.Count == 0); }
    }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<CouponPeriod> StrikeSchedule { get; set; }

    /// <summary>
    ///   True if cap has strike schedule
    /// </summary>
    [Category("Schedule")]
    public bool IsStepUp
    {
      get { return !(StrikeSchedule == null || StrikeSchedule.Count == 0); }
    }

    /// <summary>
    /// Schedule of payout rates for digital Cap
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<Tuple<Dt, double>> DigitalFixedPayoutSchedule
    {
      get { return _digitalFixedPayoutSchedule ?? (_digitalFixedPayoutSchedule = new List<Tuple<Dt, double>>()); }
      set { _digitalFixedPayoutSchedule = value; }
    }

    #endregion Properties

    #region Standard Calculations

    /// <summary>
    /// Calculates the settlement date for a standard market cap trade.
    /// </summary>
    /// <returns>Dt</returns>
    public static Dt StandardSettle(Dt pricingDate, InterestRateIndex rateIndex)
    {
      return StandardSettle(pricingDate, rateIndex.SettlementDays, rateIndex.Calendar);
    }

    /// <summary>
    /// Calculates the settlement date for a standard market cap trade.
    /// </summary>
    /// <returns>Dt</returns>
    public static Dt StandardSettle(Dt pricingDate, int spotDays, Calendar calendar)
    {
      return Dt.AddDays(pricingDate, spotDays, calendar);
    }

    /// <summary>
    /// Standard cap effective.
    /// </summary>
    /// <param name="pricingDate"></param>
    /// <param name="rateIndex"></param>
    /// <returns>Dt</returns>
    public static Dt StandardEffective(Dt pricingDate, InterestRateIndex rateIndex)
    {
      return Dt.Add(StandardSettle(pricingDate, rateIndex), rateIndex.IndexTenor);
    }

    /// <summary>
    /// Calculates the last payment date for a standard market cap trade.
    /// </summary>
    /// <returns></returns>
    public static Dt StandardLastPayment(Dt pricingDate, Tenor lastPmtTenor, InterestRateIndex rateIndex)
    {
      return StandardLastPayment(pricingDate, lastPmtTenor, rateIndex.SettlementDays, rateIndex.Calendar, rateIndex.Roll);
    }

    /// <summary>
    /// Calculates the last payment date for a standard market cap trade.
    /// </summary>
    /// <returns></returns>
    public static Dt StandardLastPayment(Dt pricingDate, string lastPmtTenorName, InterestRateIndex rateIndex)
    {
      Tenor lastPmtTenor;
      if (!Tenor.TryParse(lastPmtTenorName, out lastPmtTenor))
        throw new ArgumentException("Invalid Tenor Name " + lastPmtTenorName);

      return StandardLastPayment(pricingDate, lastPmtTenor, rateIndex.SettlementDays, rateIndex.Calendar, rateIndex.Roll);
    }

    /// <summary>
    /// Calculates the last payment date for a standard market cap trade.
    /// </summary>
    /// <returns>Dt</returns>
    public static Dt StandardLastPayment(Dt pricingDate, Tenor lastPmtTenor, int spotDays, Calendar calendar,
      BDConvention roll)
    {
      Dt date = StandardSettle(pricingDate, spotDays, calendar);
      return Dt.Add(date, lastPmtTenor);
    }

    #endregion

    #region Data

    private List<CouponPeriod> _indexMultiplierSched; 
    private IList<Tuple<Dt, double>> _digitalFixedPayoutSchedule;

    #endregion Data

  } // class Cap


  /// <summary>
  ///  Caps and Floors based on the constant maturity swap rate
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  public class CmsCap : CapBase
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CmsCap"/> class.
    /// </summary>
    /// <param name="swapRateIndex">Index of the swap rate</param>
    /// <param name="effective">The effective date</param>
    /// <param name="maturity">The maturity date</param>
    /// <param name="type">The Cap/Floor type</param>
    /// <param name="strike">The strike</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="freq">The frequency</param>
    /// <param name="bdc">The business day roll convention</param>
    /// <param name="cal">The business day calendar</param>
    public CmsCap(SwapRateIndex swapRateIndex,
      Dt effective, Dt maturity, CapFloorType type, double strike,
      DayCount dayCount, Frequency freq, BDConvention bdc, Calendar cal)
      : base(swapRateIndex, effective, maturity, swapRateIndex.Currency,
        type, strike, dayCount, freq, bdc, cal)
    {
    }

    /// <summary>
    ///   Interest rate index that is capped/floored.
    /// </summary>
    [Category("Base")]
    public SwapRateIndex SwapRateIndex
    {
      get { return (SwapRateIndex) ReferenceRateIndex; }
      set { ReferenceRateIndex = value; }
    }
  }
}