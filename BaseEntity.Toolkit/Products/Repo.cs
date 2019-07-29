/*
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Repo product type
  /// </summary>
  public enum RepoType
  {
    /// <summary></summary>
    Repo,

    /// <summary></summary>
    ReverseRepo,

    /// <summary></summary>
    SellBuyBack,

    /// <summary></summary>
    BuySellBack,
    /// <summary>
    /// 
    /// </summary>
    SecurityLending,
    /// <summary>
    /// 
    /// </summary>
    SecurityBorrowing
  }

  /// <summary>
  ///   Fixed or floating repo loan
  /// </summary>
  /// <remarks>
  ///   <para>In a repo transaction one party will provide cash and will receive collateral
  ///   in return, as a form of protection in the event the cash is not returned before or at the
  ///   end of the repo agreement.</para>
  ///   <para>The repo rate may be either fixed or floating depending on the deal terms.  
  ///   For floating rate repos linked to an overnight interest rate index, interest is not paid 
  ///   during the repo but is accrued until the maturity date.  Daily interest is not compounded but 
  ///   an arithmetic rate is calculated.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.RepoLoanPricer"/>
  /// <example>
  /// <para>The following example demonstrates constructing an Interest Rate Swap Leg.</para>
  /// <code language="C#">
  ///   Dt effective = Dt.Today();                              // Effective date is today
  ///   Dt maturity = Dt.Add(effectiveDate, 1, TimeUnit.Months);    // Maturity date is 1 Month after effective
  ///
  ///   RepoLoan repoLoan =
  ///     new RepoLoan(effective,                                   // Effective date
  ///                  maturity,                                    // Maturity date
  ///                  0.04,                                        // Repo rate is 4%
  ///                  0.05,                                        // Collateral haircut is 5%
  ///                  Currency.EUR,                                // Currency is Euros
  ///                  DayCount.Actual360,                          // Acrual Daycount is Actual/360
  ///                  BDConvention.Following,                      // Roll convention is following
  ///                  Calendar.TGT                                 // Calendar is Target
  ///                );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class RepoLoan : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Constructor for fixed rate repoLoan
    /// </summary>
    /// <param name="repoType">Type of repo</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="repoRate">Coupon rate (annualised)</param>
    /// <param name="haircut">Collateral haircut</param>
    /// <param name="freq">Coupon payment frequency (per year)</param>
    /// <param name="ccy">Currency for fixed leg</param>
    /// <param name="dayCount">Daycount convention for fixed leg</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="cal">Calendar</param>
    public RepoLoan(RepoType repoType, Dt effective, Dt maturity,
      double repoRate, double haircut, Currency ccy, Frequency freq,
      DayCount dayCount, BDConvention roll, Calendar cal)
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq,
        roll, cal, CycleRule.None, CashflowFlag.None)
    {
      RepoType = repoType;
      RepoRate = repoRate;
      Haircut = haircut;
      DayCount = dayCount;
    }

    /// <summary>
    /// Constructor for fixed rate repoLoan
    /// </summary>
    /// <param name="repoType">Type of repo</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="repoRate">Coupon rate (annualised)</param>
    /// <param name="haircut">Collateral haircut</param>
    /// <param name="ccy">Currency for fixed leg</param>
    /// <param name="dayCount">Daycount convention for fixed leg</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="cal">Calendar</param>
    public RepoLoan(RepoType repoType, Dt effective, Dt maturity,
      double repoRate, double haircut, Currency ccy, DayCount dayCount,
      BDConvention roll, Calendar cal)
      : this(repoType, effective, maturity, repoRate, haircut,
        ccy, Frequency.None, dayCount, roll, cal)
    {
    }

    /// <summary>
    ///   Constructor for floating leg
    /// </summary>
    /// <param name="repoType">Type of repo</param>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="margin">Repo margin</param>
    /// <param name="haircut">Collateral haircut</param>
    /// <param name="index">Reference index </param>
    /// <param name="resetLag">Reset lag on index</param>
    /// <param name="currency">Currency of principal and coupon payments</param>
    /// <param name="dayCount">Coupon payment accrual daycount</param>
    /// <param name="roll">Coupon payment business day convention</param>
    /// <param name="calendar">Coupon payment calendar</param>
    /// <param name="pType"></param>
    /// <param name="compFreq"></param>
    /// <param name="compConv"></param>
    public RepoLoan(
      RepoType repoType,
      Dt effective, Dt maturity, double margin, double haircut, ReferenceIndex index, Tenor resetLag,
      Currency currency, DayCount dayCount, BDConvention roll, Calendar calendar,
      ProjectionType pType = ProjectionType.None,
      Frequency compFreq = Frequency.Daily,
      CompoundingConvention compConv = CompoundingConvention.None)
      : this(repoType, effective, maturity, currency, margin, haircut, dayCount, roll, calendar,
        CycleRule.None, MakeFlags(false), index?.IndexTenor ?? Tenor.Empty,
        (!string.IsNullOrEmpty(index?.IndexName)) ? index.IndexName : "ReferenceIndex", resetLag,
        pType, compFreq, compConv)
    {
      ReferenceIndex = index;
    }

    /// <summary>
    /// Private Constructor
    /// </summary>
    public RepoLoan(
      RepoType repoType,
      Dt effective, Dt maturity, Currency ccy, double repoRate, double haircut, DayCount dayCount,
      BDConvention bdc, Calendar cal, CycleRule cycleRule, CashflowFlag flags,
      Tenor tenor, string index, Tenor resetLag,
      ProjectionType pType = ProjectionType.None,
      Frequency compFreq = Frequency.Daily,
      CompoundingConvention compConv = CompoundingConvention.None)
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, 
          tenor == Tenor.OneDay ? Frequency.None : tenor.ToFrequency(), bdc, cal, cycleRule, flags)
    {
      RepoType = repoType;
      RepoRate = repoRate;
      Haircut = haircut;
      DayCount = dayCount;
      Index = index;
      if (string.IsNullOrEmpty(Index))
        ProjectionType = ProjectionType.None;
      else
      {
        // Projection type is defaulyed to arithmetic average rate for OIS rates and simple projection otherwise
        ProjectionType = pType != ProjectionType.None ? pType 
          : (tenor == Tenor.OneDay ? ProjectionType.ArithmeticAverageRate : ProjectionType.SimpleProjection);
        IndexTenor = tenor;
        CompoundingFrequency = compFreq == Frequency.None ? tenor.ToFrequency() : compFreq; // Set compounding frequency for average rate to daily for OIS
        CompoundingConvention = compConv; // No compounding convention by default
      }
      ResetLag = resetLag;
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
    public override void Validate(ArrayList errors)
    {
      // Effective date before maturity date
      if (!Maturity.IsEmpty() && Effective >= Maturity)
        InvalidValue.AddError(errors, this, String.Format("Effective date {0} must be before maturity date {1}", Effective, Maturity));
      // Notional >= 0
      if (Notional < 0)
        InvalidValue.AddError(errors, this, "Notional", String.Format("Invalid Principal. Must be +Ve, Not {0}", Notional));

      CouponPeriodUtil.Validate(_couponSched, errors);
      if (RepoRate < -2.0 || RepoRate > 2.0)
        InvalidValue.AddError(errors, this, "RepoRate", $"Invalid rate. Must be between -2 and 2, Not {RepoRate}");

      if (Math.Abs(Haircut) >= 1.0)
        InvalidValue.AddError(errors, this, "Haircut", $"Invalid haircut. Must be between -1 and 1, Not {Haircut}");
      
      if (DayCount == DayCount.None)
        InvalidValue.AddError(errors, this, "DayCount", "Invalid daycount. Can not be None");
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (RepoLoan)base.Clone();
      obj.ProjectionType = ProjectionType;
      obj.InArrears = InArrears;
      obj.ResetLag = ResetLag;
      obj._couponSched = CloneUtil.Clone(_couponSched);
      if (CustomPaymentSchedule != null)
      {
        obj.CustomPaymentSchedule = PaymentScheduleUtils.CreateCopy(CustomPaymentSchedule);
      }

      return obj;
    }

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
        refIndex = new InterestRateIndex(Index, IndexTenor, Ccy, DayCount, Calendar, BDConvention, 2) {HistoricalObservations = rateResets}; //default index
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
          rateResets, Schedule, CashflowFlag, RepoRate, null, Notional,
          null, false, DayCount, null, projectionParams, null, null, null,
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
    /// 
    /// </summary>
    /// <param name="accrueOnCycle"></param>
    /// <returns></returns>
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

    #endregion Methods

    #region Properties
    
    /// <summary>
    ///   Annualised repo rate as a number (3% = 0.03)
    /// </summary>
    [Category("Base")]
    public RepoType RepoType { get; set; }

    /// <summary>
    ///   Annualised repo rate as a number (3% = 0.03)
    /// </summary>
    [Category("Base")]
    public double RepoRate { get; set; }

    /// <summary>
    ///   daycount
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    /// <summary>
    ///   Haircut as a number (3% = 0.03)
    /// </summary>
    [Category("Base")]
    public double Haircut
    {
      get;
      set;
    }

    /// <summary>
    ///   Price
    /// </summary>
    [Category("Base")]
    public double InitialPrice => 1.0 / (1.0 - Haircut);

    /// <summary>
    ///  
    /// </summary>
    public QuotingConvention QuotingConvention { get; set; }

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
    /// Reference index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType ProjectionType
    {
      get { return _ptype; }
      set
      {
        _ptype = value;
        if (ReferenceIndex != null)
        {
          if (ProjectionType != ProjectionType.ParYield &&
              !Array.Exists(ReferenceIndex.ProjectionTypes, pt => pt == value))
            throw new ToolkitException(
              $"Projection type {Enum.GetName(typeof(ProjectionType), value)} incompatible to the swap leg's reference index");
        }
        if (_ptype == ProjectionType.GeometricAverageRate || _ptype == ProjectionType.ArithmeticAverageRate ||
            _ptype == ProjectionType.InflationRate)
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
    public bool Floating => !string.IsNullOrEmpty(Index);

    /// <summary>
    /// Reset lag is the calendar period between the rate's accrual start and the corresponding reset date of the reference index.
    /// </summary>
    [Category("Base")]
    public Tenor ResetLag { get; set; }


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
          return (int)ReferenceIndex.IndexTenor.ToFrequency() > (int)Freq && CompoundingConvention == CompoundingConvention.None;
        if (!IndexTenor.IsEmpty)
          return (int)IndexTenor.ToFrequency() > (int)Freq && CompoundingConvention == CompoundingConvention.None;
        return false;
      }
    }

    /// <summary>
    /// Is this a fixed zero coupon swap leg
    /// </summary>
    [Category("Base")]
    public bool IsZeroCoupon => true;

    /// <summary>
    /// If true,  when pricing a zero Coupon fixed leg the coupon is compounded at Freq?
    /// </summary>
    [Category("Base")]
    public CompoundingConvention CompoundingConvention { get; set; }

    /// <summary>
    /// Open repo
    /// </summary>
    [Category("Base")]
    public bool IsOpen { get; set; }

    /// <summary>
    ///  Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> CouponSchedule =>
      _couponSched ?? (_couponSched = new List<CouponPeriod>());

    /// <summary>
    /// All coupon schedule including user input and original reporate.
    /// </summary>
    public IList<CouponPeriod> RateSchedule 
    {
      get
      {
        if (_rateSched == null)
        {
          var rateSched = new List<CouponPeriod>();
          var cpnSched = CouponSchedule;
          var effective =Effective;
          if (cpnSched != null && cpnSched.Count > 0)
          {
            rateSched.AddRange(cpnSched);
            var dates = cpnSched.OrderBy(c => c.Date).Select(c => c.Date).ToList();
            //we respect the coupon schedule when the effetive date locates
            //in the middle of the coupon schedule.
            if (dates.FirstOrDefault() > effective || dates.LastOrDefault() < effective)
            {
              rateSched.Add(new CouponPeriod(effective, RepoRate));
            }
          }
          else // we don't have input coupon schedule, use reporate for all payments.
          {
            rateSched.Add(new CouponPeriod(effective, RepoRate));
          }
          _rateSched = rateSched.OrderBy(c => c.Date).DistinctBy(c => c.Date).ToList();
        }
        return _rateSched;
      }
      
    }


    #endregion

    #region Data

    private ProjectionType _ptype;
    private List<CouponPeriod> _couponSched;
    private List<CouponPeriod> _rateSched;

    #endregion Data

  } // class RepoLoan

}