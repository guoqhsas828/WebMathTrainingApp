using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Base class for products taking schedule parameters.
  /// </summary>
  [Serializable]
  public class ProductWithSchedule : Product, IScheduleParams
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductWithSchedule"/> class.
    /// </summary>
    private ProductWithSchedule(){}

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductWithSchedule"/> class.
    /// </summary>
    /// <param name="effective">The effective (accrual start) date.</param>
    /// <param name="maturity">The maturity date.</param>
    /// <param name="firstCpnDate">The first coupon date.</param>
    /// <param name="lastCpnDate">The last coupon date.</param>
    /// <param name="ccy">The currency.</param>
    /// <param name="freq">The frequency.</param>
    /// <param name="bdc">The business day convention.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="cycleRule">The cycle rule.</param>
    /// <param name="flags">The flags.</param>
    protected ProductWithSchedule(
      Dt effective, Dt maturity, Dt firstCpnDate, Dt lastCpnDate,
      Currency ccy, 
      Frequency freq, BDConvention bdc, Calendar calendar,
      CycleRule cycleRule, CashflowFlag flags)
      :base(effective,maturity,ccy)
    {
      firstCpn_ = firstCpnDate;
      lastCpn_ = lastCpnDate;
      freq_ = freq;
      bdc_ = bdc;
      cal_ = calendar;
      rule_ = cycleRule;
      flags_ = flags;
      if(!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        flags_ |= CashflowFlag.RespectLastCoupon;
    }

    /// <summary>
    /// Copies all the schedule parameterss from another object.
    /// </summary>
    /// <param name="sp">The schedule parameterss to copy from</param>
    internal void CopyScheduleParams(IScheduleParams sp)
    {
      base.Effective = sp.AccrualStartDate;
      base.Maturity = sp.Maturity;
      firstCpn_ = sp.FirstCouponDate;
      lastCpn_ = sp.NextToLastCouponDate;
      freq_ = sp.Frequency;
      bdc_ = sp.Roll;
      cal_ = sp.Calendar;
      rule_ = sp.CycleRule;
      flags_ = sp.CashflowFlag;
      ResetSchedules();
    }

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

      // Valid first coupon date
      if (!ScheduleParams.FirstCouponDate.IsEmpty())
      {
        if (!ScheduleParams.FirstCouponDate.IsValid())
          InvalidValue.AddError(errors, this, "FirstCoupon",
            String.Format("Invalid first coupon payment date {0}",
              ScheduleParams.FirstCouponDate));
        // On or after effective date
        if (ScheduleParams.FirstCouponDate <= Effective)
          InvalidValue.AddError(errors, this, "FirstCoupon",
            String.Format("First payment {0} must be after effective {1}",
              ScheduleParams.FirstCouponDate, Effective));
        // On or before maturity date
        if (ScheduleParams.FirstCouponDate > Maturity)
          InvalidValue.AddError(errors, this, "FirstCoupon",
            String.Format(
              "First payment {0} must be on or before maturity {1}",
              ScheduleParams.FirstCouponDate, Maturity));
      }

      // Validate last coupon date
      if (!ScheduleParams.NextToLastCouponDate.IsEmpty())
      {
        if (!ScheduleParams.NextToLastCouponDate.IsValid())
          InvalidValue.AddError(errors, this, "NextToLastCoupon",
            String.Format("Invalid next to last coupon payment date {0}",
              ScheduleParams.NextToLastCouponDate));
        // On or after effective date
        if (ScheduleParams.NextToLastCouponDate <= Effective)
          InvalidValue.AddError(errors, this, "NextToLastCoupon",
            String.Format(
              "Next to last coupon {0} must be after effective {1}",
              ScheduleParams.NextToLastCouponDate, Effective));
        // On or after first premium
        if (!ScheduleParams.FirstCouponDate.IsEmpty() &&
          ScheduleParams.NextToLastCouponDate < ScheduleParams.FirstCouponDate)
          InvalidValue.AddError(errors, this, "NextToLastCoupon",
            String.Format(
              "Next to last coupon {0} must be on or after first coupon {1}",
              ScheduleParams.NextToLastCouponDate,
              ScheduleParams.FirstCouponDate));
        // On or before maturity date
        if (ScheduleParams.NextToLastCouponDate > Maturity)
          InvalidValue.AddError(errors, this, "NextToLastCoupon",
            String.Format(
              "Next to last coupon {0} must be on or before maturity {1}",
              ScheduleParams.NextToLastCouponDate, Maturity));
      }
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Coupon payment frequency (per year)
    /// </summary>
    [Category("Base")]
    public Frequency Freq
    {
      get { return freq_; }
      set
      {
        freq_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///   Effective date (date accrual and protection start)
    /// </summary>
    [Category("Base")]
    public override Dt Effective
    {
      get { return base.Effective; }
      set
      {
        base.Effective = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///   Maturity date
    /// </summary>
    [Category("Base")]
    public override Dt Maturity
    {
      get { return base.Maturity; }
      set
      {
        base.Maturity = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///   First coupon payment date
    /// </summary>
    /// <remarks>
    ///   <para>If the first coupon date has not been set, calculates the default on the fly.</para>
    /// </remarks>
    [Category("Base")]
    public Dt FirstCoupon
    {
      get { return firstCpn_.IsEmpty() ? GetDefaultFirstCoupon() : firstCpn_; }
      set
      {
        firstCpn_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///   Last regular coupon payment date
    /// </summary>
    /// <remarks>
    ///   <para>If the last coupon date has not been set, calculates the default on the fly.</para>
    /// </remarks>
    [Category("Base")]
    public Dt LastCoupon
    {
      get { return lastCpn_.IsEmpty() ? GetDefaultLastCoupon() : lastCpn_; }
      set
      {
        lastCpn_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///  Cycle Rule
    /// </summary>
    [Category("Base")]
    public CycleRule CycleRule
    {
      get { return rule_; }
      set
      {
        rule_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///   True if CycleRule=EOM, otherwise false
    /// </summary>
    [Category("Base")]
    public bool EomRule
    {
      get { return (CycleRule == CycleRule.EOM); }
    }

    /// <summary>
    ///  Cashflow flags.
    /// </summary>
    [Category("Base")]
    public CashflowFlag CashflowFlag
    {
      get { return flags_; }
      set
      {
        flags_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    /// Use cycle dates as accrual start/end dates
    /// </summary>
    [Category("Base")]
    public bool AccrueOnCycle
    {
      get { return (CashflowFlag & CashflowFlag.AccrueOnCycle) != 0; }
      set
      {
        if (value)
          CashflowFlag |= CashflowFlag.AccrueOnCycle;
        else
          CashflowFlag &= ~CashflowFlag.AccrueOnCycle;
      }
    }

    /// <summary>
    /// Use cycle dates as accrual start/end dates
    /// </summary>
    [Category("Base")]
    public bool AdjustLast
    {
      get { return (CashflowFlag & CashflowFlag.AdjustLast) != 0; }
      set
      {
        if (value)
          CashflowFlag |= CashflowFlag.AdjustLast;
        else
          CashflowFlag &= ~CashflowFlag.AdjustLast;
      }
    }

    /// <summary>
    ///   Calendar for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public Calendar Calendar
    {
      get { return cal_; }
      set
      {
        cal_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    ///   Roll convention for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention
    {
      get { return bdc_; }
      set
      {
        bdc_ = value;
        ResetSchedules();
      }
    }

    /// <summary>
    /// Parameterization of this product's schedule.
    /// </summary>
    internal IScheduleParams ScheduleParams
    {
      get { return this; }
    }

    /// <summary>
    /// Return a Schedule instance for this product.
    /// </summary>
    public Schedule Schedule
    {
      get
      {
        if (schedule_ == null)
        {
          CheckScheduleParams();
          schedule_ = Schedule.CreateSchedule(this, Effective, true);
        }
        return schedule_;
      }
    }

    /// <summary>
    /// Construct a schedule appropriate for use by CashflowFactory
    /// </summary>
    /// <remarks>
    /// If the backwards compatible setting is enabled, and if FRN convention
    /// does not apply, then this schedule will differ from the regular one.
    /// </remarks>
    internal Schedule CashflowFactorySchedule
    {
      get
      {
        if (cashflowFactorySchedule_ == null)
        {
          CheckScheduleParams();
          cashflowFactorySchedule_ =
            Schedule.CreateScheduleForCashflowFactory(this);
        }
        return cashflowFactorySchedule_;
      }
    }

    #endregion Properties

    #region Data

    private Dt firstCpn_, lastCpn_;
    private Frequency freq_;
    private BDConvention bdc_;
    private Calendar cal_;
    private CycleRule rule_;
    private CashflowFlag flags_;
    [Mutable] private Schedule schedule_;
    [Mutable] private Schedule cashflowFactorySchedule_;

    #endregion Data

    #region Schedule Generation
    /// <summary>
    /// Clear out any cached schedules
    /// </summary>
    private void ResetSchedules()
    {
      schedule_ = null;
      cashflowFactorySchedule_ = null;
    }

    /// <summary>
    /// Checks the schedule parameters and sets the first and last
    /// coupon to the default values if they are not set.
    /// </summary>
    private void CheckScheduleParams()
    {
      if ((flags_ & CashflowFlag.RespectAllUserDates) == 0)
      {
        firstCpn_ = firstCpn_.IsEmpty() ? GetDefaultFirstCoupon() : firstCpn_;
        lastCpn_ = lastCpn_.IsEmpty() ? GetDefaultLastCoupon() : lastCpn_;
      }
    }

    /// <summary>
    /// Gets the default first coupon date.
    /// </summary>
    /// <remarks>
    /// This function is called when the first coupon is not set
    /// and the property FirstCoupon or Schedule is accessed.
    /// The default implementation simply returns Dt.Empty to allow
    /// the schedule to generate the date.  Derived classes may
    /// override this function to provide an appropriate date.
    /// </remarks>
    /// <returns>The defaul first coupon.</returns>
    protected virtual Dt GetDefaultFirstCoupon()
    {
      return Dt.Empty;
    }

    /// <summary>
    /// Gets the default last coupon date.
    /// </summary>
    /// <remarks>
    /// This function is called when the last coupon is not set
    /// and the property LastCoupon or Schedule is accessed.
    /// The default implementation simply returns Dt.Empty to allow
    /// the schedule to generate the date.  Derived classes may
    /// override this function to provide an appropriate date.
    /// </remarks>
    /// <returns>The defaul last coupon.</returns>
    protected virtual Dt GetDefaultLastCoupon()
    {
      return Dt.Empty;
    }
    #endregion Schedule Generation

    #region IScheduleParams Members

    Dt IScheduleParams.AccrualStartDate
    {
      get { return base.Effective; }
    }

    Dt IScheduleParams.Maturity
    {
      get { return base.Maturity; }
    }

    Dt IScheduleParams.FirstCouponDate
    {
      get { return firstCpn_; }
    }

    Dt IScheduleParams.NextToLastCouponDate
    {
      get { return lastCpn_; }
    }

    Frequency IScheduleParams.Frequency
    {
      get { return freq_; }
    }

    BDConvention IScheduleParams.Roll
    {
      get{ return bdc_;}
    }

    CycleRule IScheduleParams.CycleRule
    {
      get { return rule_; }
    }

    CashflowFlag IScheduleParams.CashflowFlag
    {
      get { return flags_; }
    }

    Calendar IScheduleParams.Calendar
    {
      get { return cal_; }
    }

    #endregion

  }
}
