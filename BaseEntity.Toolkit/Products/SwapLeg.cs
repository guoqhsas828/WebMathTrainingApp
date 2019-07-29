/*
 *  -2012. All rights reserved.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{

  #region config

  /// <summary>
  /// Config settings class for the Swap Leg pricer 
  /// </summary>
  [Serializable]
  public class SwapLegConfig
  {
    /// <exclude />
    [ToolkitConfig(
      "backward compatibility flag , determines direction of cashflow date generation when first/last coupon dates not set explicitly"
      )] public readonly bool StubAtEnd = false;
  }

  #endregion

  /// <summary>
  ///   Fixed or floating IR swap leg
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="Swap" />
  /// </remarks>
  /// <seealso cref="Swap"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.SwapPricer"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.SwapLegPricer"/>
  /// <example>
  /// <para>The following example demonstrates constructing an Interest Rate Swap Leg.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                              // Effective date is today
  ///   Dt maturity = Dt.Add(effectiveDate, 5, TimeUnit.Years);     // Maturity date is 5Yrs after effective
  ///
  ///   SwapLeg swapLeg =
  ///     new SwapLeg( effectiveDate,                               // Effective date
  ///                  maturityDate,                                // Maturity date
  ///                  Currency.EUR,                                // Currency is Euros
  ///                  0.04,                                        // Coupon is 4%
  ///                  DayCount.Actual360,                          // Acrual Daycount is Actual/360
  ///                  BDConvention.Following,
  ///                  Frequency.SemiAnnual,                        // Semi-annual payment frequency
  ///                  Calendar.TGT                                 // Calendar is Target
  ///                );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class SwapLeg : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Private Constructor
    /// </summary>
    private SwapLeg(
      Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
      Frequency freq, BDConvention bdc, Calendar cal, CycleRule cycleRule, CashflowFlag flags,
      Tenor tenor, string index)
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, bdc, cal, cycleRule, flags)
    {
      Coupon = coupon;
      DayCount = dayCount;
      Index = index;
      IsZeroCoupon = Freq == Frequency.None && Schedule.Count == 1;
      if (string.IsNullOrEmpty(Index))
        ProjectionType = ProjectionType.None;
      else
      {
        ProjectionType = ProjectionType.SimpleProjection;
        IndexTenor = (tenor == Tenor.Empty) ? new Tenor(freq) : tenor;
      }
      ResetLag = Tenor.Empty;
      NotionalResetsInArrears = false;
    }

    /// <summary>
    ///   Constructor for fixed leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of principal and coupon payments</param>
    /// <param name="coupon">Coupon rate (annualised)</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="freq">Coupon payment frequency</param>
    /// <param name="bdc">Coupon payment business day convention</param>
    /// <param name="cal">Coupon payment calendar</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates, rather than adjust dates for calendar and BDConventions</param>
    public SwapLeg(Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
                   Frequency freq, BDConvention bdc, Calendar cal, bool accrueOnCycle)
      : this(effective, maturity, ccy, coupon, dayCount, freq, bdc, cal, accrueOnCycle, Tenor.Empty, null)
    {
    }

    /// <summary>
    /// Constructor for fixed swap leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="coupon">Coupon rate (annualised)</param>
    /// <param name="ccy">Currency for fixed leg</param>
    /// <param name="dayCount">Daycount convention for fixed leg</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    /// <param name="principalExchange">Principal exchange occurs</param>
    public SwapLeg(Dt effective, Dt maturity, double coupon, Currency ccy, DayCount dayCount,
      Frequency freq, BDConvention roll, Calendar cal, Frequency compoundingFreq,
      bool principalExchange
      )
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, roll, cal, CycleRule.None, MakeFlags(false))
    {
      Coupon = coupon;
      DayCount = dayCount;
      Index = null;
      ProjectionType = ProjectionType.None;
      ResetLag = Tenor.Empty;
      NotionalResetsInArrears = false;
      FinalExchange = principalExchange;
      IsZeroCoupon = (freq == Frequency.None);
      CompoundingFrequency = compoundingFreq;
      //var psp = (IScheduleParams) Schedule;
      //CycleRule = psp.CycleRule;
      //Maturity = psp.Maturity;
    }

    /// <summary>
    ///   Constructor for floating leg with terms matching the reference index.
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency : could be different than index frequency</param>  
    /// <param name="coupon">Coupon</param>
    /// <param name="index">Reference index </param>
    public SwapLeg(
      Dt effective, Dt maturity, Frequency freq, double coupon, ReferenceIndex index)
      : this(effective, maturity, freq, coupon, index, index.Currency, index.DayCount, index.Roll, index.Calendar)
    {
    }

    /// <summary>
    /// Constructor for floating swap leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="coupon">Premium over projected rate</param>
    /// <param name="freq">Frequency of payment, default is index frequency</param>
    /// <param name="index">Reference index</param>
    /// <param name="indexType">Floating index projection type</param>
    /// <param name="compoundingConvention">Compounding convention</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    /// <param name="principalExchange">Principal exchange occurs</param>
    public SwapLeg(Dt effective, Dt maturity, double coupon, Frequency freq,
      ReferenceIndex index, ProjectionType indexType,
      CompoundingConvention compoundingConvention, Frequency compoundingFreq,
      bool principalExchange
      )
      : base(effective, maturity, Dt.Empty, Dt.Empty, index.Currency, freq, index.Roll, 
      index.Calendar, CycleRule.None, MakeFlags(false))
    {
      Coupon = coupon;
      DayCount = index.DayCount;
      Index = index.IndexName;
      IsZeroCoupon = false;
      NotionalResetsInArrears = false;
      ReferenceIndex = index;
      IndexTenor = index.IndexTenor;
      ProjectionType = indexType;
      CompoundingConvention = compoundingConvention;
      FinalExchange = principalExchange;
      CompoundingFrequency = compoundingFreq;
      ResetLag = new Tenor(index.SettlementDays, TimeUnit.Days);
      InArrears = (indexType == ProjectionType.ArithmeticAverageRate ||
                   indexType == ProjectionType.GeometricAverageRate ||
                   indexType == ProjectionType.TBillArithmeticAverageRate ||
                   indexType == ProjectionType.CPArithmeticAverageRate);
      //var rsp = (IScheduleParams) Schedule;
      //CycleRule = rsp.CycleRule;
      //Maturity = rsp.Maturity;
    }

    /// <summary>
    ///   Constructor for floating leg 
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of principal and coupon payments</param>
    /// <param name="coupon">Coupon rate (annualised)</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="freq">Coupon payment frequency</param>
    /// <param name="bdc">Coupon payment business day convention</param>
    /// <param name="cal">Coupon payment calendar</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates, rather than adjust dates for calendar and BDConventions</param>
    /// <param name="tenor">Floating rate tenor</param>
    /// <param name="index">Floating rate index</param>
    ///TODO: can we change tenor in availabilityLag without destroying backward compatibility
    public SwapLeg(
      Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
      Frequency freq, BDConvention bdc, Calendar cal, bool accrueOnCycle, Tenor tenor,
      string index)
      : this(effective, maturity, ccy, coupon, dayCount, freq, bdc, cal, accrueOnCycle, false, tenor, index)
    {
    }

    /// <summary>
    ///   Constructor for floating leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of principal and coupon payments</param>
    /// <param name="coupon">Coupon rate (annualised)</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="freq">Coupon payment frequency</param>
    /// <param name="bdc">Coupon payment business day convention</param>
    /// <param name="cal">Coupon payment calendar</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates, rather than adjust dates for calendar and BDConventions</param>
    /// <param name="eomRule">Set to true to force coupon payment dates to be on the last day of the month if the maturity date is the last day of the month</param>
    /// <param name="tenor">Floating rate tenor</param>
    /// <param name="index">Floating rate index</param>
    public SwapLeg(
      Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
      Frequency freq, BDConvention bdc, Calendar cal, bool accrueOnCycle, bool eomRule, Tenor tenor,
      string index)
      : this(effective, maturity, ccy, coupon, dayCount, freq, bdc, cal,
             eomRule ? CycleRule.EOM : CycleRule.None, MakeFlags(accrueOnCycle), tenor, index)
    {
    }

    /// <summary>
    ///   Constructor for floating leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency : could be different than index frequency</param>  
    /// <param name="coupon">Coupon</param>
    /// <param name="index">Reference index </param>
    /// <param name="currency">Currency of principal and coupon payments</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="roll">Coupon payment business day convention</param>
    /// <param name="calendar">Coupon payment calendar</param>
    public SwapLeg(
      Dt effective, Dt maturity, Frequency freq, double coupon, ReferenceIndex index, Currency currency, DayCount dayCount, BDConvention roll, Calendar calendar)
      : this(effective, maturity, currency, coupon, dayCount, freq, roll, calendar,
             CycleRule.None, MakeFlags(false), (index == null ? Tenor.Empty : index.IndexTenor),
             (index != null && !string.IsNullOrEmpty(index.IndexName)) ? index.IndexName : "ReferenceIndex")
    {
      ReferenceIndex = index;
      if (index != null)
      {
        Index = index.IndexName;
        IndexTenor = index.IndexTenor;
        ProjectionType = ReferenceIndex.ProjectionTypes[0];
      }
    }

    private static CashflowFlag MakeFlags(bool accrueOnCycle)
    {
      CashflowFlag flag = CashflowFlag.None;
      if (accrueOnCycle)
        flag |= CashflowFlag.AccrueOnCycle;
      flag |= CashflowFlag.RollLastPaymentDate | CashflowFlag.RespectLastCoupon | CashflowFlag.AdjustLast;
      if (ToolkitConfigurator.Settings.SwapLeg.StubAtEnd)
        flag |= CashflowFlag.StubAtEnd;
      return flag;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SwapLeg) base.Clone();
      obj.ProjectionType = ProjectionType;
      obj.InArrears = InArrears;
      obj.ResetLag = ResetLag;
      obj.amortSched_ = CloneUtil.Clone(amortSched_);
      obj.couponSched_ = CloneUtil.Clone(couponSched_);
      obj.indexMultiplierSched_ = CloneUtil.Clone(indexMultiplierSched_);
      if (CustomPaymentSchedule != null)
      {
        obj.CustomPaymentSchedule = PaymentScheduleUtils.CreateCopy(CustomPaymentSchedule);
      }
      obj.ReferenceIndex = CloneUtil.Clone(ReferenceIndex);
      return obj;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Annualised coupon as a number (10% = 0.10)
    /// </summary>
    [Category("Base")]
    public double Coupon { get; set; }

    /// <summary>
    ///   daycount
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    /// <summary>
    ///   Floating rate tenor
    /// </summary>
    [Category("Base")]
    public Tenor IndexTenor { get; set; }

    /// <summary>
    ///   Floating rate reference index name
    /// </summary>
    [Category("Base")]
    public string Index { get; set; }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> IndexMultiplierSchedule
    {
      get { return indexMultiplierSched_ ?? (indexMultiplierSched_ = new List<CouponPeriod>()); }
      set
      {
        if (value == null)
        {
          if (!indexMultiplierSched_.IsNullOrEmpty())
          {
            indexMultiplierSched_.Clear();
          }
          return;
        }
        if (indexMultiplierSched_ != null) indexMultiplierSched_.Clear();
        else indexMultiplierSched_ = new List<CouponPeriod>();
        indexMultiplierSched_.AddRange(value);
      }
    }

    /// <summary>
    /// Reference index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType ProjectionType
    {
      get { return ptype_; }
      set
      {
        ptype_ = value;
        if (ReferenceIndex != null)
        {
          if (ProjectionType != ProjectionType.ParYield &&
            !Array.Exists(ReferenceIndex.ProjectionTypes, pt => pt == value))
            throw new ToolkitException(
              String.Format("Projection type {0} incompatible to the swap leg's reference index",
                            Enum.GetName(typeof(ProjectionType), value)));
        }
        if (ptype_ == ProjectionType.GeometricAverageRate || ptype_ == ProjectionType.ArithmeticAverageRate ||
            ptype_ == ProjectionType.InflationRate)
          //Update if we create different projection types
          InArrears = true;
      }
    }

    /// <summary>
    /// Compounding frequency for fixed zero coupon swaps
    /// </summary>
    public Frequency CompoundingFrequency { get; set; }

    /// <summary>
    ///   True if floating rate
    /// </summary>
    [Category("Base")]
    public bool Floating
    {
      get { return !String.IsNullOrEmpty(Index); }
    }

    /// <summary>
    /// Reset lag is the calendar period between the rate's accrual start and the corresponding reset date of the reference index.
    /// </summary>
    [Category("Base")]
    public Tenor ResetLag { get; set; }

    /// <summary>
    ///   Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get { return amortSched_ ?? (amortSched_ = new List<Amortization>()); }
    }

    /// <summary>
    ///   True if bond amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return !(amortSched_ == null || amortSched_.Count == 0); }
    }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<CouponPeriod> CouponSchedule
    {
      get { return couponSched_ ?? (couponSched_ = new List<CouponPeriod>()); }
    }
    
    /// <summary>
    ///   True if bond has coupon schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return !(couponSched_ == null || couponSched_.Count == 0); }
    }

    /// <summary>
    /// Cap on rate fixing + coupon
    /// </summary>
    public double? Cap { get; set; }

    /// <summary>
    /// Floor on rate fixing + coupon
    /// </summary>
    public double? Floor { get; set; }

    /// <summary>
    /// True if floating coupon is fixed at the end of the coupon period. Default value is false
    /// </summary>
    [Category("Base")]
    public bool InArrears { get; set; }

    /// <summary>
    /// True if floating coupon is fixed at the end of the coupon period. Default value is false
    /// </summary>
    [Category("Base")]
    public bool WithDelay
    {
      get
      {
        if (ReferenceIndex != null)
        {
          // Handle OIS swaps up to 1 year
          if (ReferenceIndex.IndexTenor == Tenor.OneDay && IsZeroCoupon && CompoundingConvention == CompoundingConvention.None)
            return true;
          return (int)ReferenceIndex.IndexTenor.ToFrequency() > (int)Freq && CompoundingConvention == CompoundingConvention.None;
        }
        if (!IndexTenor.IsEmpty)
          return (int)IndexTenor.ToFrequency() > (int)Freq && CompoundingConvention == CompoundingConvention.None;
        return false;
      }
    }

    /// <summary>
    /// Is this a fixed zero coupon swap leg
    /// </summary>
    [Category("Base")]
    public bool IsZeroCoupon { get; set; }

    /// <summary>
    /// If true,  when pricing a zero Coupon fixed leg the coupon is compounded at Freq?
    /// </summary>
    [Category("Base")]
    public CompoundingConvention CompoundingConvention { get; set; }

    /// <summary>
    ///   Principal exchanges at Effective Date
    /// </summary>
    [Category("Base")]
    public bool InitialExchange { get; set; }

    /// <summary>
    ///   Principal exchanges everytime notional change
    /// </summary>
    [Category("Base")]
    public bool IntermediateExchange { get; set; }

    /// <summary>
    ///   Principal exchanges at Maturity
    /// </summary>
    [Category("Base")]
    public bool FinalExchange { get; set; }

    /// <summary>
    /// Determines whether the notional amount at period start is used for computation for generation the cashflow
    /// </summary>
    [Category("Base")]
    public bool NotionalResetsInArrears
    {
      get { return (CashflowFlag & CashflowFlag.NotionalResetAtPay) != 0; }
      set
      {
        if (value)
          CashflowFlag |= CashflowFlag.NotionalResetAtPay;
        else
          CashflowFlag &= ~CashflowFlag.NotionalResetAtPay;
      }
    }

    /// <summary>
    ///   Next break date
    /// </summary>
    /// <remarks>
    ///   <para>This is the next date at which parties can terminate at current
    ///   market value. This is typically used as a mechanism to mitigate
    ///   counterparty risk for long-dated swaps.</para>
    /// </remarks>
    public Dt NextBreakDate { get; set; }

    /// <summary>
    /// Set if payment date lags end of period
    /// </summary>
    public PayLagRule PaymentLagRule { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public bool IncludeMaturityAccrual
    {
      get { return (CashflowFlag & CashflowFlag.IncludeMaturityAccrual) != 0; }
      set
      {
        if (value)
          CashflowFlag |= CashflowFlag.IncludeMaturityAccrual;
        else
          CashflowFlag &= ~CashflowFlag.IncludeMaturityAccrual;
      }
    }


    /// <summary>
    ///   Gets and sets the CouponFunction
    /// </summary>
    /// <preliminary>For internal use only</preliminary>
    public Func<IPeriod, IRateCalculator, double> CouponFunction { get; set; }

    /// <summary>
    /// Final date the product keeps to be active
    /// </summary>
    public override Dt EffectiveMaturity
    {
      get
      {
        if (effecitveMaturity_ == null)
        {
          if (PaymentLagRule == null || PaymentLagRule.PaymentLagDays ==0)
            effecitveMaturity_ = Maturity;
          else if (!Maturity.IsEmpty())
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

    #endregion Properties

    #region Methods

    /// <summary>
    /// Access resets information for cashflow generation
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="rateResets">Historical resets</param>
    /// <param name="refIndex">Reference index</param>
    /// <returns>Dictionary containing past and projected resets indexed by date</returns>
    public IDictionary<Dt, RateResets.ResetInfo> GetResetInfo(
      Dt asOf, RateResets rateResets, ReferenceIndex refIndex)
    {
      if (!Floating)
        return null;
      IDictionary<Dt, RateResets.ResetInfo> allInfo = new SortedDictionary<Dt, RateResets.ResetInfo>();
      ProjectionParams projectionParams = GetProjectionParams();
      if (refIndex == null)
        refIndex = new InterestRateIndex(Index, IndexTenor, Ccy, DayCount, Calendar, BDConvention, 2) { HistoricalObservations = rateResets }; //default index
      else if (refIndex.HistoricalObservations == null)
        refIndex.HistoricalObservations = rateResets;
      CouponCalculator projector = CouponCalculator.Get(asOf, refIndex, projectionParams);
      PaymentSchedule ps;
      if (CustomPaymentSchedule != null)
      {
        ps = PaymentScheduleUtils.CreateCopy(CustomPaymentSchedule);
        if (rateResets != null)
        {
          var arrFlt = ps.ToArray<FloatingInterestPayment>(null);
          if (arrFlt != null && arrFlt.Length > 0)
          {
            for (int i = 0; i < arrFlt.Length; i++)
            {
              var flp = arrFlt[i];
              flp.RateProjector = projector;
              bool isCurrent = false;
              if (projector != null && !rateResets.HasAllResets && rateResets.HasCurrentReset)
              {
                if (flp.ResetDate <= projector.AsOf && (i >= arrFlt.Length - 1 || arrFlt[i + 1].ResetDate > projector.AsOf))
                  isCurrent = true;
              }
              rateResets.UpdateResetsInCustomCashflowPayments(flp, isCurrent, false);
            }
          }
        }
      }
      else
      {
        ps = PaymentScheduleUtils.FloatingRatePaymentSchedule(Dt.Empty, Dt.Empty, Ccy, projector, null,
          rateResets, Schedule, CashflowFlag, Coupon, CouponSchedule, Notional,
          AmortizationSchedule, false, DayCount, null, projectionParams, null, null, IndexMultiplierSchedule,
          false, Dt.Empty, Dt.Empty, null, null);
      }

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
    /// Projection params for the swap leg;
    /// </summary>
    /// <returns></returns>
    private ProjectionParams GetProjectionParams()
    {
      ProjectionFlag flags = ProjectionFlag.None;
      if (InArrears)
        flags |= ProjectionFlag.ResetInArrears;
      if (IsZeroCoupon)
        flags |= ProjectionFlag.ZeroCoupon;
      var retVal = new ProjectionParams
                     {
                       ProjectionType = ProjectionType,
                       CompoundingFrequency = CompoundingFrequency,
                       CompoundingConvention = CompoundingConvention,
                       ResetLag = ResetLag,
                       ProjectionFlags = flags
                     };
      return retVal;
    }

    /// <summary>
    ///   Indicates whether a custom index multiplier is present
    /// </summary>
    public bool HasIndexMultiplier()
    {
      return (IndexMultiplierSchedule != null && IndexMultiplierSchedule.Any());
    }

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

      if (Coupon < -2.0 || Coupon > 2.0)
        InvalidValue.AddError(errors, this, "Coupon",
                              String.Format("Invalid Coupon. Must be between -2.0 and 2.0, not ({0})", Coupon));

      if (CompoundingFrequency == Frequency.Continuous)
        InvalidValue.AddError(errors, this, "CompoundingFrequency", "A continuous compounding frequency within an acrual period is not valid");

      // Validate schedules
      AmortizationUtil.Validate(amortSched_, errors);
      CouponPeriodUtil.Validate(couponSched_, errors);
      CouponPeriodUtil.Validate(indexMultiplierSched_, errors, false);
      if (NextBreakDate.IsValid() && (NextBreakDate > Maturity))
        InvalidValue.AddError(errors, this, "EarlyTerminationDate", "EarlyTerminationDate must precede Maturity");
      return;
    }

    #endregion

    #region Serialize delegates
    [OnSerializing]
    void WrapDelegates(StreamingContext context)
    {
      CouponFunction = CouponFunction.WrapSerializableDelegate();
    }

    [OnSerialized, OnDeserialized]
    void UnwrapDelegates(StreamingContext context)
    {
      CouponFunction = CouponFunction.UnwrapSerializableDelegate();
    }

    #endregion

    #region Data

    private List<Amortization> amortSched_;
    private List<CouponPeriod> couponSched_;
    private ProjectionType ptype_;
    [Mutable] private List<CouponPeriod> indexMultiplierSched_;
    [Mutable] private Dt? effecitveMaturity_;

    #endregion Data
  } // class SwapLeg

  /// <summary>
  /// Floating inflation swap leg
  /// </summary>
  [Serializable]
  public class InflationSwapLeg : SwapLeg
  {
    /// <summary>
    ///   Constructor for inflation leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of principal and coupon payments</param>
    /// <param name="coupon">Coupon rate (annualised)</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="freq">Coupon payment frequency</param>
    /// <param name="bdc">Coupon payment business day convention</param>
    /// <param name="cal">Coupon payment calendar</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates, rather than adjust dates for calendar and BDConventions</param>
    public InflationSwapLeg(Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
                   Frequency freq, BDConvention bdc, Calendar cal, bool accrueOnCycle)
      : base(effective, maturity, ccy, coupon, dayCount, freq, bdc, cal, accrueOnCycle, false, Tenor.Empty, null)
    {
    }

    /// <summary>
    ///   Constructor for inflation floating leg with terms matching the reference index.
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency : could be different than index frequency</param>  
    /// <param name="coupon">Coupon</param>
    /// <param name="index">Reference index </param>
    public InflationSwapLeg(
      Dt effective, Dt maturity, Frequency freq, double coupon, ReferenceIndex index)
      : base(effective, maturity, freq, coupon, index, index.Currency, index.DayCount, index.Roll, index.Calendar)
    {
    }

    /// <summary>
    ///   Constructor for inflation floating leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency : could be different than index frequency</param>  
    /// <param name="coupon">Coupon</param>
    /// <param name="index">Reference index </param>
    /// <param name="currency">Currency of principal and coupon payments</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="roll">Coupon payment business day convention</param>
    /// <param name="calendar">Coupon payment calendar</param>
    public InflationSwapLeg(
      Dt effective, Dt maturity, Frequency freq, double coupon, ReferenceIndex index, Currency currency, DayCount dayCount, BDConvention roll, Calendar calendar)
      : base(effective, maturity, freq, coupon, index, currency, dayCount, roll, calendar)
    {
    }

    /// <summary>
    ///   Constructor for inflation floating leg
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of principal and coupon payments</param>
    /// <param name="coupon">Coupon rate (annualised)</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="freq">Coupon payment frequency</param>
    /// <param name="bdc">Coupon payment business day convention</param>
    /// <param name="cal">Coupon payment calendar</param>
    /// <param name="accrueOnCycle">Accrue on cycle dates, rather than adjust dates for calendar and BDConventions</param>
    /// <param name="eomRule">Set to true to force coupon payment dates to be on the last day of the month if the maturity date is the last day of the month</param>
    /// <param name="tenor">Floating rate tenor</param>
    /// <param name="index">Floating rate index</param>
    public InflationSwapLeg(
      Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
      Frequency freq, BDConvention bdc, Calendar cal, bool accrueOnCycle, bool eomRule, Tenor tenor,
      string index)
      : base(effective, maturity, ccy, coupon, dayCount, freq, bdc, cal, accrueOnCycle, eomRule, tenor, index)
    {
    }

    #region Properties
    /// <summary>
    /// Indexation method
    /// </summary>
    public IndexationMethod IndexationMethod { get; set; }
    #endregion
  }
}