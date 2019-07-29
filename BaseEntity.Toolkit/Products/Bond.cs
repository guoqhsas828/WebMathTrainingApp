//
// Bond.cs
//  -2014. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Bond product definition
  /// </summary>
  /// <remarks>
  /// <para>A wide range of global sovereign and corporate bonds are supported including:</para>
  /// <list type="bullet">
  ///   <item><description>Global sovereign bonds including US, Canadian, UK, Germany, French, Italy, Norway, Sweden, Japan and Australia.</description></item>
  ///   <item><description>Fixed and floating rate</description></item>
  ///   <item><description>Callables (fixed and floaters)</description></item>
  ///   <item><description>Convertables</description></item>
  ///   <item><description>Amortizing</description></item>
  ///   <item><description>Step-up</description></item>
  ///   <item><description>Sinkers</description></item>
  /// </list>
  /// <para><h2>Convertibles</h2></para>
  /// <para>Convertible bonds are financial products that combine features of both bonds and equities.
  /// A convertible bond holder has the right but not the obligation to convert the bond
  /// into a certain number of stocks should the conversion become favorable and allowable.
  /// Additionally, convertible bonds have either a call (issuer can call bond to retire)
  /// and/or put (holder can put bond back) options built in. Some of the factors
  /// considered in valuing these instruments include interest rates, equity prices,
  /// credit quality, correlations, conversion, and call/put options. The two primary
  /// risk factors modeled are interest rates and equity prices.</para>
  /// </remarks>
  /// <seealso cref="BondPricer">Bond Pricer</seealso>
  [Serializable]
  [ReadOnly(true)]
  public partial class Bond : ProductWithSchedule, ICallable
  {
    #region Constructors

    /// <summary>
    ///   Constructor for bond
    /// </summary>
    /// <remarks>
    ///   <para>Sets default first and last coupon payment dates based on bond maturity and coupon
    ///   payment frequency.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="type">Bond type</param>
    /// <param name="coupon">Coupon of bond</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="cycleRule">End of Month Rule</param>
    /// <param name="freq">Payment frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="calendar">Calendar for coupon rolls</param>
    /// <param name="accrualDayCount">Daycount for intra-period accrued interest</param>
    private Bond(
      Dt effective, Dt maturity, Currency ccy, BondType type, double coupon,
      DayCount dayCount, CycleRule cycleRule, Frequency freq, BDConvention roll, Calendar calendar, DayCount accrualDayCount
      )
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, roll, calendar, cycleRule, CashflowFlag.AccrueOnCycle)
    {
      BondType = type;
      Coupon = coupon;
      DayCount = dayCount;
      AccrualDayCount = accrualDayCount;
      Isin = "";
      Cusip = "";
      SoftCallTrigger = -1;
      QuotingConvention = QuotingConvention.FullPrice;
    }

    /// <summary>
    /// Constructor for bond
    /// </summary>
    /// <remarks>
    ///   <para>Sets default first and last coupon payment dates based on bond maturity and coupon
    ///   payment frequency.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="type">Bond type</param>
    /// <param name="coupon">Coupon of bond</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="cycleRule">End of Month Rule</param>
    /// <param name="freq">Payment frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="calendar">Calendar for coupon rolls</param>
    public Bond(
     Dt effective, Dt maturity, Currency ccy, BondType type, double coupon,
     DayCount dayCount, CycleRule cycleRule, Frequency freq, BDConvention roll, Calendar calendar
     )
      : this(effective, maturity, ccy, type, coupon, dayCount, cycleRule, freq, roll, calendar, dayCount)
    {
    }

    /// <summary>
    /// Constructor for floating rate bond
    /// </summary>
    /// <remarks>
    ///   <para>Sets default first and last coupon payment dates based on bond maturity and coupon
    ///   payment frequency.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="type">Bond type</param>
    /// <param name="referenceIndex">Floating rate index</param>
    /// <param name="referenceTenor">Floating rate tenor</param>
    /// <param name="spread">Floating rate spread</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="cycleRule">End of Month Rule</param>
    /// <param name="freq">Payment frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="calendar">Calendar for coupon rolls</param>
    public Bond(
     Dt effective, Dt maturity, Currency ccy, BondType type, InterestReferenceRate referenceIndex, Tenor referenceTenor, double spread,
     DayCount dayCount, CycleRule cycleRule, Frequency freq, BDConvention roll, Calendar calendar
     )
      : this(effective, maturity, ccy, type, spread, dayCount, cycleRule, freq, roll, calendar, dayCount)
    {
      Tenor = referenceTenor;
      ReferenceIndex = new InterestRateIndex(referenceIndex, referenceTenor);
      Index = ReferenceIndex.IndexName;
      ResetLag = new Tenor(referenceIndex.DaysToSpot, TimeUnit.Days);
    }

    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      Bond obj = (Bond)base.Clone();

      obj.amortSched_ = CloneUtil.Clone(amortSched_);
      obj.couponSched_ = CloneUtil.Clone(couponSched_);
      obj.callSched_ = CloneUtil.Clone(callSched_);
      obj.putSched_ = CloneUtil.Clone(putSched_);

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    /// This tests only relationships between fields of the product that
    /// cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Frequency cannot be none
      if (Freq == Frequency.None)
        InvalidValue.AddError(errors, this, "Freq", String.Format("Frequency must be set"));

      // Valid bond coupon, note for FRN, coupon might be spread below or over LIBOR
      if (Coupon > 2.0)
        InvalidValue.AddError(errors, this, "Coupon", String.Format("Invalid coupon"));

      // Validate schedules
      AmortizationUtil.Validate(amortSched_, errors);
      CouponPeriodUtil.Validate(couponSched_, errors);
      CallPeriodUtil.Validate(callSched_, errors);
      PutPeriodUtil.Validate(putSched_, errors);

      if (Callable && Floating)
        InvalidValue.AddError(errors, this, "Callable FRN currently is not supported.");

      if (HasExDivSchedule && (PaymentLagRule != null && PaymentLagRule.PaymentLagDays !=0))
        InvalidValue.AddError(errors, this, "Bond with both ex-div and payment gap features not supported currently");
    }

    /// <summary>
    /// Get call price by date from a list of call periods.
    /// </summary>
    /// <remarks>
    /// <para>Returns <c>NaN</c> if no call is active.</para>
    /// </remarks>
    /// <param name="date">Date at which call price is requested.</param>
    public double GetCallPriceByDate(Dt date)
    {
      if (CallSchedule != null)
      {
        foreach (CallPeriod c in CallSchedule)
        {
          if ((c.StartDate <= date) && (c.EndDate >= date))
            return c.CallPrice;
        }
      }
      return Double.NaN;
    }

    
    /// <summary>
    /// Get the payment schedule regardless of the pricing date; this won't use the interest rate curves and credit curves,
    /// but just generate the dates without computing the actual cash flow amounts.
    /// </summary>
    public PaymentSchedule GetPaymentSchedule()
    {
      var ps = BondCashflowHelpers.GetPaymentSchedule(this, null, Effective, Dt.Empty, null, null, null, Dt.Empty, Dt.Empty, 0.4, false);
      return ps;
    }

    /// <summary>
    /// Get reset info for rate resets
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="rateResets"></param>
    /// <returns>Reset info</returns>
    public IDictionary<Dt, RateResets.ResetInfo> GetResetInfo(Dt asOf, RateResets rateResets)
    {
      IDictionary<Dt, RateResets.ResetInfo> allInfo = new Dictionary<Dt, RateResets.ResetInfo>();
      var dc = new DiscountCurve(Effective, 0.0);
      var ps = this.GetPaymentSchedule(null, Effective, Dt.Empty, dc, dc, null, Dt.Empty, Dt.Empty, 0.0, false);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        RateResetState state;
        Dt reset = ip.ResetDate;
        double rate;
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

    /// <summary>
    /// Get default first coupon date
    /// </summary>
    /// <returns>Default first coupon date</returns>
    protected override Dt GetDefaultFirstCoupon()
    {
      return Schedule.CreateSchedule(this, Effective, false).
        GetNextCouponDate(Effective);
    }

    /// <summary>
    /// Get default last coupon date
    /// </summary>
    /// <returns>Default last coupon date</returns>
    protected override Dt GetDefaultLastCoupon()
    {
      return Schedule.CreateSchedule(this, Effective, false).
        GetPrevCouponDate(Maturity);
    }

    /// <summary>
    ///  Returns true if bond is cum-dividend
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="tradeSettle"></param>
    /// <returns>True if bond is cum-dividend</returns>
    public bool CumDiv(Dt settle, Dt tradeSettle)
    {
      var nextCpnDate = Schedule.GetNextCouponDate(settle);
      Dt exDivDate = nextCpnDate.IsEmpty()? Dt.Empty : BondModelUtil.ExDivDate(this, nextCpnDate);
      bool cumDiv = (!tradeSettle.IsEmpty()) && (!exDivDate.IsEmpty()) && (tradeSettle < exDivDate);
      return cumDiv;
    }

    /// <summary>
    /// Check if pricing a trade settled in prior period with unsettled coupon/principal payment delayed as of pricing settle date
    /// </summary>
    /// <param name="settle">Pricing settle date</param>
    /// <param name="tradeSettle">Trade settle date</param>
    /// <returns>True if pricing a trade settled in prior period with unsettled coupon/principal payment delayed as of pricing settle date</returns>
    public bool HasUnSettledLagPayment(Dt settle, Dt tradeSettle)
    {
      if (PaymentLagRule == null || tradeSettle.IsEmpty())
        return false;

      Dt prevPeriodEnd = Schedule.GetPrevCouponDate(settle);
      Dt prevPayDate = prevPeriodEnd.IsEmpty() ? Effective : PayLagRule.CalcPaymentDate(prevPeriodEnd, PaymentLagRule.PaymentLagDays, PaymentLagRule.PaymentLagBusinessFlag, BDConvention, Calendar);
      
      bool cumPaylag = !tradeSettle.IsEmpty() && !prevPeriodEnd.IsEmpty() && (tradeSettle < prevPeriodEnd && settle < prevPayDate);
      return cumPaylag;
    }

    public double GetEffectiveNotionalFromCustomizedScheduleBasedOnPayDate(Dt dt)
    {
      // For a bond with customized schedule, find the notional in effect as of date dt (based on Payment Date)
      // This can be used for example if the bond has defaulted on this date.
      double ret = PaymentScheduleUtils.GetEffectiveNotionalFromCustomizedScheduleBasedOnPayDate(CustomPaymentSchedule, dt);
      return ret;
    }

    public double GetEffectiveNotionalFromCustomizedSchedule(Dt dt)
    {
      // For a bond with customized schedule, find the notional in effect as of date dt.
      double ret = PaymentScheduleUtils.GetEffectiveNotionalFromCustomizedSchedule(CustomPaymentSchedule, dt);
      return ret;
    }

    /// <summary>
    /// calculate notional factor at a sprecific date
    /// </summary>
    /// <param name="asOf">the date</param>
    /// <returns></returns>
    public double NotionalFactorAt(Dt asOf)
    {
      PaymentSchedule ps = CustomPaymentSchedule;
      double not = 1.0;
      if (ps != null && ps.Count > 0)
      {
        not = PaymentScheduleUtils.GetEffectiveNotionalFromCustomizedSchedule(ps, asOf);
      }
      else if (AmortizationSchedule != null && AmortizationSchedule.Count > 0)
      {
        not = AmortizationUtil.PrincipalAt(AmortizationSchedule, 1.0, asOf);
      }
      return not;
    }

    /// <summary>
    /// Utility function to transform a list of amortization dates and amounts into the amortization schedule of type PercentOfInitialNotional
    /// </summary>
    /// <param name="amortizationDates">Dates list of the amortization schedule</param>
    /// <param name="amortizationAmts">Actual principal redemption amount list</param>
    /// <param name="origPrincipal">Original principal amount, if not specified, assuming the amortization amounts sum up to the original principal</param>
    public void ProcessAmortizations(Dt[] amortizationDates, double[] amortizationAmts, double origPrincipal = double.NaN)
    {
      if (amortizationDates == null || amortizationAmts == null) return;

      if (amortizationDates.Length != amortizationAmts.Length)
        throw new ArgumentException("Amortization dates and Amortization amounts need to be the same size");

      if (double.IsNaN(origPrincipal))
      {
        origPrincipal = amortizationAmts.Sum();
      }

      var mapping = new Dictionary<Dt, double>();
      for (int idx = 0; idx < amortizationDates.Length; ++idx)
      {
        mapping[amortizationDates[idx]] = amortizationAmts[idx];
      }

      AmortizationSchedule.Clear();

      foreach (var amortizationDt in amortizationDates.OrderBy(d => d))
      {
        if (amortizationDt >= Maturity) continue;
        
        AmortizationSchedule.Add(new Amortization(amortizationDt, mapping[amortizationDt] / origPrincipal));
      }
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// The ISIN
    /// </summary>
    public string Isin { get; set; }

    /// <summary>
    /// The CUSIP
    /// </summary>
    public string Cusip { get; set; }

    /// <summary>
    /// Annualised coupon as a number (10% = 0.10)
    /// </summary>
    [Category("Base")]
    public double Coupon { get; set; }

    /// <summary>
    /// Day count for calculating full coupon payments
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    /// <summary>
    /// Day count for accrued interest within period
    /// Rarely differs from DayCount.
    /// </summary>
    [Category("Base")]
    public DayCount AccrualDayCount { get; set; }

    /// <summary>
    ///   Bond rate tenor
    /// </summary>
    [Category("Base")]
    public Tenor Tenor { get; set; }

    /// <summary>
    /// Name of Floating rate index
    /// </summary>
    /// <remarks>
    /// Bond <see cref="ReferenceIndex">ReferenceIndex</see> value needs to be set whenever the Index has value 
    /// if configuration has <BondPricer>BackwardCompatibleCashflows="False"</BondPricer>.
    /// </remarks>
    [Category("Base")]
    public string Index { get; set; }

    /// <summary>
    /// Reference index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Reset lag is the calendar period between the rate's accrual start and the corresponding reset date of the reference index
    /// </summary>
    [Category("Base")]
    public Tenor ResetLag { get; set; }

    /// <summary>
    /// True if floating rate bond
    /// </summary>
    [Category("Base")]
    public bool Floating
    {
      get { return !String.IsNullOrEmpty(Index); }
    }

    /// <summary>
    /// Bond type
    /// </summary>
    [Category("Base")]
    public BondType BondType { get; set; }

    /// <summary>
    /// Set to true when coupon payments on actual daycount bases adjust based on rolled period start/end dates
    /// </summary>
    [Category("Base")]
    public bool PeriodAdjustment
    {
      get { return !AccrueOnCycle; }
      set
      {
        AccrueOnCycle = !value;
        // for bonds, if adjusting end dates for intermediate periods, do the same
        // for the final period too. Conversely, if not adjusting for intermediate periods, make sure
        // we don't for the last period either.
        if (value)
          CashflowFlag |= CashflowFlag.AdjustLast;
        else
          CashflowFlag &= ~CashflowFlag.AdjustLast;
      }
    }

    /// <summary>
    /// Convertable bond parameters (or null if not convertable)
    /// </summary>
    public ConvertibleBondParams ConvertParams { get; set; }

    /// <summary>
    /// Bond quoting convention
    /// </summary>
    [Category("Base")]
    public QuotingConvention QuotingConvention { get; set; }

    /// <summary>
    /// Amortization schedule (or null of not amortizing)
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get { return amortSched_ ?? (amortSched_ = new List<Amortization>()); }
    }

    /// <summary>
    /// True if bond amortizes, has step up schedule or a totally custom schedule
    /// </summary>
    public bool IsCustom
    {
      get { return Amortizes || StepUp || CustomPaymentSchedule != null; }
    }

    /// <summary>
    /// True if bond amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return !(amortSched_ == null || amortSched_.Count == 0); }
    }

    /// <summary>
    /// Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> CouponSchedule
    {
      get { return couponSched_ ?? (couponSched_ = new List<CouponPeriod>()); }
    }

    /// <summary>
    /// True if bond has coupon schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return !(couponSched_ == null || couponSched_.Count == 0); }
    }

    /// <summary>
    /// True is the bond is convertible
    /// </summary>
    public bool Convertible
    {
      get { return (ConvertRatio > 0); }
    }

    /// <summary>
    /// Get and set the conversion ratio for convertible bond
    /// </summary>
    public double ConvertRatio { get; set; }

    /// <summary>
    /// Get and set the par amount of convertible bond 
    /// </summary>
    public double ParAmount { get; set; }

    /// <summary>
    /// Get and set convertion start date
    /// </summary>
    public Dt ConvertStartDate { get; set; }

    /// <summary>
    /// Get and set convertion start date
    /// </summary>
    public Dt ConvertEndDate { get; set; }

    /// <summary>
    /// Get and set the convertible bond soft-call triggering price
    /// </summary>
    public double SoftCallTrigger { get; set; }

    /// <summary>
    /// Get and set the convertible bond soft-call trigger end period 
    /// </summary>
    public Dt SoftCallEndDate
    {
      get
      {
        if (softCallEndDate_ == Dt.Empty)
          softCallEndDate_ = Maturity;
        return softCallEndDate_;
      }
      set
      {
        softCallEndDate_ = value;
      }
    }

    /// <summary>
    /// Call schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public List<CallPeriod> CallSchedule
    {
      get
      {
        if (callSched_ == null)
          callSched_ = new List<CallPeriod>();
        return callSched_;
      }
    }

    /// <summary>
    /// True if bond callable
    /// </summary>
    [Category("Schedule")]
    public bool Callable
    {
      get { return !(callSched_ == null || callSched_.Count == 0); }
    }

    /// <summary>
    /// Gets the notification days for callable bond.
    /// </summary>
    /// <value>The notification days.</value>
    public int NotificationDays { get; set; }

    /// <summary>
    /// Put schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public List<PutPeriod> PutSchedule
    {
      get
      {
        if (putSched_ == null)
          putSched_ = new List<PutPeriod>();
        return putSched_;
      }
    }

    /// <summary>
    /// True if bond putable
    /// </summary>
    [Category("Schedule")]
    public bool Putable
    {
      get { return !(putSched_ == null || putSched_.Count == 0); }
    }

    /// <summary>
    /// Helper method used to get the Amort Dates as a separate array
    /// This is useful when we need to make calls to the native C++ code
    /// </summary>
    public Dt[] AmortDates
    {
      get
      {
        if (amortDates_ == null)
        {
          Dt[] amortDates;
          double[] amortAmounts;
          AmortizationUtil.FromSchedule(AmortizationSchedule, out amortDates, out amortAmounts);
          amortDates_ = amortDates;
          amortAmounts_ = amortAmounts;
        }
        return amortDates_;
      }
    }

    /// <summary>
    /// Helper method used to get the Amort Amounts as a separate array
    /// This is useful when we need to make calls to the native C++ code
    /// </summary>
    public double[] AmortAmounts
    {
      get
      {
        if (amortAmounts_ == null)
        {
          Dt[] amortDates;
          double[] amortAmounts;
          AmortizationUtil.FromSchedule(AmortizationSchedule, out amortDates, out amortAmounts);
          amortDates_ = amortDates;
          amortAmounts_ = amortAmounts;
        }
        return amortAmounts_;
      }
    }

    /// <summary>
    /// The details of bond ex-div schedule parameters
    /// </summary>
    public ExDivRule BondExDivRule { get; set; }

    /// <summary>
    /// The details of bond pay-lag schedule parameters
    /// </summary>
    public PayLagRule PaymentLagRule { get; set; }

    /// <summary>
    /// Final date the product keeps to be active
    /// </summary>
    public override Dt EffectiveMaturity
    {
      get
      {
        if (effecitveMaturity_ == null)
        {
          if (PaymentLagRule == null)
            effecitveMaturity_ = Maturity;
          else if (!Maturity.IsEmpty() )
          {
            effecitveMaturity_ = PayLagRule.CalcPaymentDate(Maturity, PaymentLagRule.PaymentLagDays,
                                                            PaymentLagRule.PaymentLagBusinessFlag, BDConvention,
                                                            Calendar);
          }
          else
          {
            effecitveMaturity_ = Dt.Roll(Dt.Add(Maturity, PaymentLagRule.PaymentLagDays), BDConvention, Calendar);
          }
        }
        return effecitveMaturity_.Value;
      }
    }

    /// <summary>
    ///  Gets the product maturity date
    /// </summary>
    public override Dt Maturity
    {
      get { return base.Maturity; }
      set
      {
        base.Maturity = value;
        effecitveMaturity_ = null;
      }
    }

    /// <summary>
    /// True if the bond has ex-div feature
    /// </summary>
    public bool HasExDivSchedule
    {
      get
      {
        return (BondExDivRule != null && BondExDivRule.ExDivDays != 0)
               || BondType == BondType.UKGilt || BondType == BondType.AUSGovt;
      }
    }

    public double FinalRedemptionValue
    {
      get { return _finalRedemption.HasValue ? _finalRedemption.Value : 1.0; }
      set { _finalRedemption = value; }
    }

    #endregion Properties

    #region ICallable Interfaces

    IList<IOptionPeriod> ICallable.ExerciseSchedule
    {
      get
      {
        return Callable ? CallSchedule.Cast<IOptionPeriod>().ToList()
          : (Putable ? PutSchedule.Cast<IOptionPeriod>().ToList() : null);
      }
    }

    OptionRight ICallable.OptionRight
    {
      get
      {
        return Callable ? OptionRight.RightToCancel
          : (Putable ? OptionRight.RightToEnter : OptionRight.None);
      }
    }

    /// <summary>
    ///  Gets or sets the indicator that whether the exercise strikes
    ///   are in full (dirty) price.
    /// </summary>
    public bool FullExercisePrice { get; set; }

    #endregion ICallable Interfaces

    #region Data

    private List<Amortization> amortSched_;
    private List<CouponPeriod> couponSched_;

    private Dt softCallEndDate_;

    private List<CallPeriod> callSched_;
    private List<PutPeriod> putSched_;

    private Dt[] amortDates_;
    private double[] amortAmounts_;

    /// <summary>
    /// Configuration
    /// </summary>
    public bool BackwardCompatibleCashflow
    {
      get
      {
        if (overrideBackwardCompatibleCF_.HasValue)
          return overrideBackwardCompatibleCF_.Value;
        return settings_.BondPricer.BackwardCompatibleCashflows;
      }
    }
    private ToolkitConfigSettings settings_ => ToolkitConfigurator.Settings;

    // A private property for public use.
    // This should only be set in Unit tests through reflection.
    private bool EnableNewCashflow
    {
      get { return enableNewCashflow_; }
    }
    private bool enableNewCashflow_ = true;
    private bool? overrideBackwardCompatibleCF_;
    private Dt? effecitveMaturity_;

    private double? _finalRedemption;
    #endregion Data

    #region convertible bond call and put schedule classes

    /// <summary>
    /// public inner class to hold the call schedule information of convertible bond 
    /// </summary>
    public class ConvertibleBondCallSchedule
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="start"></param>
      /// <param name="end"></param>
      /// <param name="prices"></param>
      public ConvertibleBondCallSchedule(Dt[] start, Dt[] end, double[] prices)
      {
        CallStartDates = start;
        CallEndDates = end;
        CallPrices = prices;
      }

      /// <summary>
      /// Get call start dates
      /// </summary>
      public Dt[] CallStartDates { get; private set; }

      /// <summary>
      /// Get call end dates
      /// </summary>
      public Dt[] CallEndDates { get; private set; }

      /// <summary>
      /// Get call prices
      /// </summary>
      public double[] CallPrices { get; private set; }
    }

    /// <summary>
    /// public inner class to hold the put schedule information of convertible bond 
    /// </summary>
    public class ConvertibleBondPutSchedule
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="start"></param>
      /// <param name="end"></param>
      /// <param name="prices"></param>
      public ConvertibleBondPutSchedule(Dt[] start, Dt[] end, double[] prices)
      {
        PutStartDates = start;
        PutEndDates = end;
        PutPrices = prices;
      }

      /// <summary>
      ///  Get put start dates
      /// </summary>
      public Dt[] PutStartDates { get; private set; }

      /// <summary>
      ///  Get put end dates
      /// </summary>
      public Dt[] PutEndDates { get; private set; }

      /// <summary>
      ///  Get put prices
      /// </summary>
      public double[] PutPrices { get; private set; }
    }

    #endregion convertible bond call and put schedule classes

  }

  public static partial class BondCashflowHelpers
  {
    #region Interface to new Cashflow 
    public static PaymentSchedule GetBondPayments(
      this Bond bond, Dt filterDate, Dt from,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve, RateResets rateResets,
      double recoveryRate, Dt defaultDate, Dt dfltSettle, bool ignoreExDivs)
    {
      return bond.GetPaymentSchedule(null, from, Dt.Empty,
        discountCurve, referenceCurve, rateResets,
        defaultDate, dfltSettle, recoveryRate, ignoreExDivs)
        .FilterPayments(filterDate)
        .AddRecoveryPayments(bond, dt => recoveryRate);
    }

    /// <summary>
    /// Get the payment schedule regardless of the pricing date; this won't use the interest rate curves and credit curves,
    /// but just generate the dates without computing the actual cash flow amounts.
    /// </summary>
    public static PaymentSchedule GetPaymentSchedule(this Bond bond)
    {
      PaymentSchedule ps = bond.GetPaymentSchedule(null, bond.Effective, Dt.Empty, null, null, null, Dt.Empty, Dt.Empty, 0.4, false);
      return ps;
    }

    /// <summary>
    /// Gets the payment schedule of the specified fixed rate bond,
    ///  adds the contingent recovery payments if necessary,
    ///  with recovery rate in default settlement determined
    ///  by the survival curve and the AccruedFractionAtDefault being zero.
    /// </summary>
    /// <param name="bond">The bond</param>
    /// <param name="survivalCurve">The survival curve</param>
    /// <returns>PaymentSchedule</returns>
    public static PaymentSchedule GetPaymentSchedule(
      this Bond bond, SurvivalCurve survivalCurve)
    {
      if (survivalCurve == null)
      {
        return bond.GetPaymentSchedule(null,
          bond.Effective, Dt.Empty, null, null, null,
          Dt.Empty, Dt.Empty, 0.0, false);
      }

      var rc = survivalCurve.SurvivalCalibrator?.RecoveryCurve;
      var defaultDate = survivalCurve.DefaultDate;
      if (!defaultDate.IsEmpty())
      {
        return bond.GetPaymentSchedule(null, bond.Effective,
          Dt.Empty, null, null, null, Dt.Empty, Dt.Empty,
          rc?.Interpolate(defaultDate) ?? 0.0, false);
      }

      // Add the contingent recovery payments.
      return bond.GetPaymentSchedule(null, bond.Effective,
        Dt.Empty, null, null, null, Dt.Empty, Dt.Empty, 0.0, false)
        .AddRecoveryPayments(bond,
          survivalCurve.SurvivalCalibrator?.RecoveryCurve);
    }

    /// <summary>
    /// Adds the contingent recovery payments contingent
    ///  to the specified bond payment schedule
    /// </summary>
    /// <param name="payments">The payments.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="recoveryFunction">The recovery function.</param>
    /// <returns>PaymentSchedule.</returns>
    public static PaymentSchedule AddRecoveryPayments(
      this PaymentSchedule payments,
      Bond bond,
      Func<Dt, double> recoveryFunction)
    {
      if (payments == null) return null;

      var creditRiskToPaymentDate = bond?.PaymentLagRule != null;
      List<Payment> list = null;
      Dictionary<Dt, Dt> creditRiskDates = null;
      foreach (var payment in payments)
      {
        var ip = payment as InterestPayment;
        if (ip == null) continue;

        // No accrual on default.
        ip.AccruedFractionAtDefault = 0;

        if (!creditRiskToPaymentDate && ip.AccrualEnd != ip.PayDt)
        {
          ip.CreditRiskEndDate = ip.AccrualEnd;
          creditRiskDates = creditRiskDates.AddDates(ip.PayDt, ip.AccrualEnd);
        }
        if (recoveryFunction == null) continue;

        // Now add the contingent recovery payment
        Dt end = creditRiskToPaymentDate
          ? ip.PayDt : ip.AccrualEnd;
        var recovery = recoveryFunction(end);
        if (recovery.AlmostEquals(0.0)) continue;

        Dt begin = creditRiskToPaymentDate
          ? ip.PreviousPaymentDate : ip.AccrualStart;
        if (list == null) list = new List<Payment>();
        list.Add(new RecoveryPayment(begin, end, recovery, ip.Ccy)
        {
          Notional = ip.Notional,
          IsFunded = true,
        });
      }

      if (creditRiskDates != null)
      {
        foreach (var payment in payments.OfType<PrincipalExchange>())
        {
          Dt date;
          if (creditRiskDates.TryGetValue(payment.PayDt, out date))
            payment.CreditRiskEndDate = date;
        }
      }


      if (list != null)
      {
        payments.AddPayments(list);
      }
      else if (bond != null && recoveryFunction != null)
      {
        var lastPrincipal = payments.OfType<PrincipalExchange>()
          .OrderBy(p => p.PayDt).LastOrDefault();
        if (lastPrincipal == null)
          return payments;
        var lastPayDt = lastPrincipal.PayDt;
        var lastRecovery = recoveryFunction(lastPayDt);
        if (!lastRecovery.AlmostEquals(0.0))
        {
          payments.AddPayment(new RecoveryPayment(
            bond.EffectiveMaturity, lastPayDt,
            lastRecovery, lastPrincipal.Ccy)
          {
            Notional = lastPrincipal.Notional,
            IsFunded = true,
          });
        }
      }

      return payments;
    }

    private static Dictionary<Dt, Dt> AddDates(
      this Dictionary<Dt, Dt> map, Dt payDt, Dt riskEndDate)
    {
      if (map == null) map = new Dictionary<Dt, Dt>();
      map.Add(payDt, riskEndDate);
      return map;
    }


    public static PaymentSchedule GetPaymentSchedule(
      this Bond bond,
      PaymentSchedule ps, Dt from, Dt to,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      RateResets rateResets,
      Dt defaultDate, Dt dfltSettle, double recoveryRate, bool ignoreExDivs)
    {
      if (from > bond.EffectiveMaturity && !(dfltSettle.IsValid() && from <= dfltSettle || !dfltSettle.IsValid() && defaultDate.IsValid()))
        return null;

      var exDivCalculator = ignoreExDivs ? null : new BondExDivCalculator(bond);
      if (bond.CustomPaymentSchedule != null && bond.CustomPaymentSchedule.Count > 0)
      {
        if (ps == null)
          ps = new PaymentSchedule();
        else
          ps.Clear();

        foreach (Dt d in bond.CustomPaymentSchedule.GetPaymentDates())
        {
          if (d >= from)
          {
            if (!defaultDate.IsEmpty() && (to.IsEmpty() || defaultDate <= to) && d >= defaultDate)
            {
              // No more payments on or after default date. However, include the default settlement payment.
              if (dfltSettle.IsEmpty() || dfltSettle >= from)
              {
                double notionalAtDefault = bond.GetEffectiveNotionalFromCustomizedScheduleBasedOnPayDate(defaultDate);
                var defSettlement = new DefaultSettlement(defaultDate, dfltSettle, bond.Ccy, notionalAtDefault, recoveryRate);
                defSettlement.IsFunded = true;
                ps.AddPayment(defSettlement);
              }
              break;
            }
            else if (to.IsValid() && d > to)
              break;
            IEnumerable<Payment> paymentsOnDate = bond.CustomPaymentSchedule.GetPaymentsOnDate(d);
            var usedPaymentsOnDate = new List<Payment>(); // Not all the items with pay date >= from will be taken - see below.
            foreach (Payment pmt in paymentsOnDate)
            {
              if (pmt is InterestPayment)
              {
                if (((InterestPayment)pmt).PeriodEndDate >= from)
                {
                  var ip = (InterestPayment)pmt.Clone();
                  ip.ExDivDate = exDivCalculator != null ? exDivCalculator.Calc(ip.PeriodEndDate) : Dt.Empty;
                  usedPaymentsOnDate.Add(ip);
                }
              }
              else if (pmt is PrincipalExchange)
              {
                var bpmt = (PrincipalExchange)pmt;
                if (!bpmt.CutoffDate.IsEmpty())
                {
                  if (bpmt.CutoffDate >= from)
                    usedPaymentsOnDate.Add((PrincipalExchange)pmt.Clone());
                }
                else
                {
                  if (bpmt.PayDt >= from)
                    usedPaymentsOnDate.Add((PrincipalExchange)pmt.Clone());                
                }
              }
            }
            ps.AddPayments(usedPaymentsOnDate);
          }
        }
        // Update rate resets in floating interest payment objects at this point, taking into account the pricing date:
        if (bond.Floating && rateResets != null)
        {
          ProjectionParams projParams = bond.GetProjectionParams();
          IRateProjector rateProjector = bond.GetRateProjector(discountCurve, referenceCurve, projParams);
          CouponCalculator projector = (CouponCalculator)rateProjector;
          FloatingInterestPayment[] arrFlt = ps.ToArray<FloatingInterestPayment>(null);  // These will be sorted by the pay date.
          if (arrFlt != null && arrFlt.Length > 0)
          {
            for (int i = 0; i < arrFlt.Length; i++)
            {
              FloatingInterestPayment flp = arrFlt[i];
              flp.RateProjector = rateProjector;
              flp.ExDivDate = exDivCalculator != null ? exDivCalculator.Calc(flp.PeriodEndDate) : Dt.Empty;
              bool isCurrent = false;
              if (!rateResets.HasAllResets && rateResets.HasCurrentReset)
              {
                if (flp.ResetDate <= projector.AsOf && (i >= arrFlt.Length - 1 || arrFlt[i + 1].ResetDate > projector.AsOf))
                  isCurrent = true;
              }
              rateResets.UpdateResetsInCustomCashflowPayments(flp, isCurrent, false);
            }
          }
        }
        return ps;
      }
      var amorts = bond.NormalizeAmortizations(from, bond.Notional,
        bond.AmortizationSchedule, bond.BackwardCompatibleCashflow);
      var coupons = bond.NormalizeCouponSchedule(bond.CouponSchedule,
        bond.BackwardCompatibleCashflow);
      const bool includeTradeSettle = false;
      if (!bond.Floating)
      {
        ps = PaymentScheduleUtils.FixedRatePaymentSchedule(from, to, bond.Ccy, bond.Schedule, bond.CashflowFlag,
                                                           bond.Coupon, coupons, bond.Notional,
                                                           amorts, false, bond.DayCount,
                                                           bond.Freq /*CompoundingFrequency*/,
                                                           null /*FxCurve*/, includeTradeSettle, defaultDate, dfltSettle,
                                                           bond.PaymentLagRule, exDivCalculator);
      }
      else
      {
        ProjectionParams projParams = bond.GetProjectionParams();
        IRateProjector rateProjector = bond.GetRateProjector(discountCurve, referenceCurve, projParams);
        IForwardAdjustment forwardAdjustment = bond.GetForwardAdjustment(from, discountCurve, projParams);
        RateResets normalizedRateResets = null;
        if (referenceCurve != null && rateResets != null)
        {
          normalizedRateResets = bond.NormalizeRateResets(referenceCurve.AsOf, rateResets, bond.BackwardCompatibleCashflow);
        }
        ps = PaymentScheduleUtils.FloatingRatePaymentSchedule(
          from, to, bond.Ccy, rateProjector, forwardAdjustment,
          normalizedRateResets,
          bond.Schedule, bond.CashflowFlag,
          bond.Coupon, coupons,
          bond.Notional, amorts, false,
          bond.DayCount, null /*FxCurve*/, projParams,
          null, null, includeTradeSettle, defaultDate, dfltSettle, bond.PaymentLagRule, exDivCalculator);
      }
      // So far, ps includes only the interest payments. We need to add principal payments (at maturity or amortizing principal), and,
      // in case of default, the default settlement payment.
      Dt effectiveMaturity = ps.Count == 0 ? bond.EffectiveMaturity : ps.GetPaymentDates().Max(p => p);
         // Careful here: if the bond has defaulted, the last entry will be the last coupon paid before default.
         // But we process the defaulted case separately below.
      double endNotional = double.NaN;
      if (amorts != null && amorts.Count != 0)
      {
        var ipList = ps.GetPaymentsByType<InterestPayment>().ToArray();
        for (int i = 0; i < ipList.Length; i++)
        {
          if (!defaultDate.IsEmpty() && (to.IsEmpty() || defaultDate <= to) && ipList[i].PayDt >= defaultDate)
            break; // If defaulted, no more payments (interest or principals) on or after the default date.
          if (!to.IsEmpty() && ipList[i].PayDt > to)
            break; // If to date specified, we do not include cash flows after the to date.
          if (bond.BackwardCompatibleCashflow)
          {
            double notEnd = amorts.PrincipalAt(bond.Notional, ipList[i].PayDt);
            double notStart = amorts.PrincipalAt(bond.Notional, ipList[i].PreviousPaymentDate);
            double amort = notStart - notEnd;
            if (Math.Abs(amort) > 0)
            {
              ps.AddPayment(new PrincipalExchange(ipList[i].PayDt, amort, bond.Ccy) { CutoffDate = ipList[i].AccrualEnd });
            }
          }
          else
          {
            if (i == ipList.Length - 1)
            {
              endNotional = ipList[i].Notional;
              break;
            }
            // The change in notional (amortization) takes effect as of the start of accrual period.
            // Also, the InterestPayment items by this time already have the notional in effect updated
            // in the FixedRatePaymentSchedule or FloatingRatePaymentSchedule function above.
            double notEnd = ipList[i+1].Notional;
            double notStart = ipList[i].Notional;
            double amort = notStart - notEnd;
            if (Math.Abs(amort) > AmortizationUtil.NotionalTolerance && ipList[i].AccrualEnd >= from)
            {
              ps.AddPayment(new PrincipalExchange(ipList[i].PayDt, amort, bond.Ccy) { CutoffDate = ipList[i].AccrualEnd });
            }           
          }
        }
      }
      if (!defaultDate.IsEmpty() && (to.IsEmpty() || defaultDate <= to))
      {
        // If defaulted, include the default settlement payment.
        if (dfltSettle.IsEmpty() || dfltSettle >= from)
        {
          double notionalAtDefault = amorts.PrincipalAt(bond.Notional, defaultDate);
          var defSettlement = new DefaultSettlement(defaultDate, dfltSettle, bond.Ccy, notionalAtDefault, recoveryRate) {IsFunded = true, CutoffDate  = dfltSettle};
          ps.AddPayment(defSettlement);
        }
      }
      else if (to.IsEmpty() || effectiveMaturity <= to)
      {
        if (double.IsNaN(endNotional))
        {
          endNotional = amorts.PrincipalAt(bond.Notional, effectiveMaturity);
        }
        if (!endNotional.ApproximatelyEqualsTo(0.0))
          ps.AddPayment(new PrincipalExchange(effectiveMaturity, endNotional, bond.Ccy)
          {
            FXCurve = null,
            CutoffDate = effectiveMaturity
          });
      }
      return ps;
    }

    private static ProjectionParams GetProjectionParams(this Bond bond)
    {
      ProjectionFlag flags = ProjectionFlag.None;
      //if (ApproximateForFastCalculation)
      //  flags |= ProjectionFlag.ApproximateProjection;
      var par = new ProjectionParams
      {
        ProjectionType = ProjectionType.SimpleProjection,
        ProjectionFlags = flags,
        ResetLag = bond.BackwardCompatibleCashflow ? Tenor.Empty : bond.ResetLag
      };
      if ((bond.ScheduleParams.CashflowFlag & CashflowFlag.SimpleProjection) == 0)
        par.CompoundingFrequency = bond.Freq;
      return par;
    }

    private static IRateProjector GetRateProjector(
      this Bond bond,
      DiscountCurve discountCurve,
      CalibratedCurve referenceCurve,
      ProjectionParams projectionParams)
    {
      //Note: we don't assume cashflow start is the trade date (today).
      // Instead we use the referenceCurve.AsOf as today to determine
      // whether to project rates or to look at the resets.
      var rateProjector = CouponCalculator.Get(
        referenceCurve == null ? Dt.Empty : referenceCurve.AsOf,
        bond.BackwardCompatibleCashflow ? bond.GetReferenceIndex() : bond.ReferenceIndex, 
        referenceCurve, discountCurve,
        projectionParams);
      if (bond.BackwardCompatibleCashflow && rateProjector is ForwardRateCalculator)
      {
        // this is for the backward compatible setting only.
        ((ForwardRateCalculator) rateProjector).EndSetByIndexTenor = false;
      }
      rateProjector.CashflowFlag = bond.CashflowFlag;
      return rateProjector;
    }

    private static IForwardAdjustment GetForwardAdjustment(
      this Bond bond,
      Dt asOf,
      DiscountCurve discountCurve,
      ProjectionParams projectionParams)
    {
      return ForwardAdjustment.Get(asOf, discountCurve, null/*FwdRateModelParameters*/,
        projectionParams);
    }

    #endregion

    #region Backward compatibles

    private static ReferenceIndex GetReferenceIndex(this Bond bond)
    {
      //Note: This can be removed once the ReferenceIndex property is added to the bond.
      var index = bond.Index;
      if (index == null) return null;
      return new InterestRateIndex(index, bond.Freq, bond.Ccy, bond.DayCount,
        bond.Calendar, 0);
    }

    /// <summary>
    ///  Find the reset rate at a given date in backward compatible mode.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static double ResetAt(this IEnumerable<KeyValuePair<Dt, double>> schedule, Dt date)
    {
      double rate = 0.0;
      if (schedule != null)
      {
        foreach (var r in schedule)
        {
          if (r.Key <= date)
            rate = r.Value;
          else
            break;
        }
      }
      return rate;
    }

    /// <summary>
    ///  Conver to backward compatible list with the current resets included as the reset on effective.
    /// </summary>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="effective">The effective.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static IList<RateReset> ToBackwardCompatibleList(this RateResets rateResets, Dt effective)
    {
      var list = new List<RateReset>();
      if (rateResets == null) return list;
      if (rateResets.HasCurrentReset && !effective.IsEmpty())
      {
        list.Add(new RateReset(effective, rateResets.CurrentReset));
        if (rateResets.AllResets == null) return list;
        foreach (var p in rateResets.AllResets)
        {
          if (p.Key > effective) list.Add(new RateReset(p.Key, p.Value));
        }
        return list;
      }
      // No effective date provided
      if (rateResets.AllResets == null) return list;
      foreach (var p in rateResets.AllResets)
      {
        list.Add(new RateReset(p.Key, p.Value));
      }
      return list;
    }

    private static RateResets NormalizeRateResets(this ProductWithSchedule product,
      Dt asOf, RateResets rateResetsObj, bool backwardCompatible)
    {
      if (rateResetsObj == null || !backwardCompatible) return rateResetsObj;

      // In backward compatible mode, we make rate resets consistent with the schedule
      var sched = product.Schedule;
      var schedParams = product.ScheduleParams;
      var resets = new RateResets();
      var rateResets = rateResetsObj.ToBackwardCompatibleList(product.Effective);
      if (rateResets.Count > 0)
      {
        int count = sched.Count;
        int j = 0; // counter through schedule
        double rate = 0;

        var all = resets.AllResets;
        foreach (RateReset r in rateResets)
        {
          Dt resetDate = r.Date;
          rate = r.Rate;

          // if reset was captured for a rolled period start then pass to the cashflow model
          // as the unadjusted period start; FillFloat only looks for most recent resets, <= period start
          for (; j < count; j++)
          {
            Dt periodStart = sched.GetPeriodStart(j);
            Dt adjPeriodStart = Dt.Roll(periodStart, schedParams.Roll,
              schedParams.Calendar);
            if (Dt.Cmp(resetDate, adjPeriodStart) == 0)
            {
              all.Add(periodStart, rate);
              ++j; // start at next period for next rate reset
              break;
            }
            if (Dt.Cmp(adjPeriodStart, resetDate) > 0)
            {
              break;
            }
            all.Add(periodStart, rate);
          }
        }

        for (; j < count; j++)
        {
          Dt periodStart = sched.GetPeriodStart(j);
          if (periodStart >= asOf) break;
          all.Add(periodStart, rate);
        }
      }
      return resets;
    }

    private static IList<Amortization> NormalizeAmortizations(
      this ProductWithSchedule product,
      Dt asOf, double origPrincipal, IList<Amortization> amorts,
      bool backwardCompatible)
    {
      if (!backwardCompatible || amorts == null || amorts.Count == 0) return amorts;

      var sched = product.Schedule;
      int schedCount = sched.Count;
      if (schedCount == 0) return amorts;

      bool accrueOnCycle = (product.CashflowFlag & CashflowFlag.AccrueOnCycle) != 0;
      int firstIdx = 0;
      for (; firstIdx < schedCount; firstIdx++)
      {
        Dt accrualStart = (accrueOnCycle || firstIdx <= 0)
          ? sched.GetPeriodStart(firstIdx) : sched.GetPaymentDate(firstIdx - 1);
        if (accrualStart >= asOf)
          break;
      }
      if (firstIdx > 0)
        firstIdx--;


      int amortCount = amorts.Count, nextAmort = 0;
      if (origPrincipal == 0) origPrincipal = 1.0;
      var remainingPrincipal = origPrincipal;
      {
        while (nextAmort < amortCount && amorts[nextAmort].Date < asOf)
        {
          remainingPrincipal -= amorts[nextAmort].AmortizingAmount(
            origPrincipal, remainingPrincipal);
          nextAmort++;
        }
      }

      var results = new List<Amortization>();
      bool notionalResetAtPay = (product.CashflowFlag & CashflowFlag.NotionalResetAtPay) != 0;
      if (nextAmort > 0 && remainingPrincipal != origPrincipal)
      {
        Dt date = (firstIdx <= 0 || !notionalResetAtPay)
          ? sched.GetPeriodStart(firstIdx) : sched.GetPaymentDate(firstIdx - 1);
        results.Add(new Amortization(date,
          AmortizationType.RemainingNotionalLevels, remainingPrincipal));
      }

      for (int i = firstIdx; i < schedCount; i++)
      {
        Dt payDate = sched.GetPaymentDate(i);
        Dt date = notionalResetAtPay ? payDate : sched.GetPeriodEnd(i);

        // Any amortizations between this coupon and the last one
        //Note: C++ CashflowFactory always uses payDate, so we do it.
        double amount = 0.0;
        while( nextAmort < amortCount && amorts[nextAmort].Date <= payDate )
        {
          if (remainingPrincipal > 0.0)
          {
            double amort = amorts[nextAmort].AmortizingAmount(origPrincipal,
              remainingPrincipal);
            amount += amort;
            remainingPrincipal -= amort;
          }
          nextAmort++;
        }
        if (amount != 0.0)
        {
          results.Add(new Amortization(date,
            AmortizationType.PercentOfInitialNotional, amount/origPrincipal));
        }
      }

      return results;
    }

    private static double AmortizingAmount(this Amortization a,
      double initialPrincipal, double current)
    {
      switch (a.AmortizationType)
      {
        case AmortizationType.PercentOfInitialNotional:
          return a.Amount * initialPrincipal;
        case AmortizationType.PercentOfCurrentNotional:
          return a.Amount * current;
        case AmortizationType.RemainingNotionalLevels:
          return current - a.Amount * initialPrincipal;
      }
      throw new ToolkitException("Unknown amortization type {0}",
        a.AmortizationType);
    }

    private static IList<CouponPeriod> NormalizeCouponSchedule(
      this ProductWithSchedule product,
      IList<CouponPeriod> couponSchedule,
      bool backwardCompatible)
    {
      if (!backwardCompatible || couponSchedule == null
        || couponSchedule.Count == 0 || product.Schedule.Count == 0)
      {
        return couponSchedule;
      }

      // In backward compatible mode, we set coupon on period start date
      // based on backward compatible algorithm.
      var sched = product.Schedule;
      int schedCount = sched.Count;
      Dt asOf = sched.GetPeriodStart(0);
      var cpnList = new List<CouponPeriod>();
      int nextCpn = 0, lastCpnIndex = couponSchedule.Count - 1;
      while (nextCpn < lastCpnIndex && (Dt.Cmp(couponSchedule[nextCpn + 1].Date, asOf) < 0))
        nextCpn++;

      // Generate the normalized coupon schedule.
      for (int i = 0; i < schedCount; i++)
      {
        Dt start = sched.GetPeriodStart(i);
        Dt end = sched.GetPeriodEnd(i);

        // Get current coupon
        // Assume coupon steps on scheduled dates for now. TBD: Revisit this. RTD Feb'06
        while (nextCpn < lastCpnIndex && (Dt.Cmp(couponSchedule[nextCpn + 1].Date, end) < 0))
          nextCpn++;
        cpnList.Add(new CouponPeriod(start, couponSchedule[nextCpn].Coupon));
      }

      return cpnList;
    }
    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public class BondExDivCalculator : IExDivCalculator
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="b"></param>
    public BondExDivCalculator(Bond b)
    {
      Bond = b;
    }

    /// <summary>
    /// 
    /// </summary>
    public Bond Bond { get; set; }

    /// <summary>
    /// Get ex-div date before a coupon date
    /// </summary>
    /// <param name="couponDate">The coupon date</param>
    /// <returns></returns>
    public Dt Calc(Dt couponDate)
    {
      return BondModelUtil.ExDivDate(Bond, couponDate);
    }
  }
}
