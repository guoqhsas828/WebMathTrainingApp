using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Transport class to return sets of dates and their options from this utility.
  /// </summary>
  [Serializable]
  public class ScheduleParams : BaseEntityObject, IScheduleParams
  {

    /// <summary>
    /// Schedule Definition
    /// </summary>
    /// <param name="accrualStartDate">Accrual Start Date</param>
    /// <param name="firstCoupon">First Coupon Date</param>
    /// <param name="nextToLastCoupon">Next to Last Coupon Date</param>
    /// <param name="maturity">Maturity Date</param>
    /// <param name="freq">Payment Frequency</param>
    /// <param name="roll">Business Day Convention</param>
    /// <param name="cal">Holiday Calendar</param>
    /// <param name="cycleRule">Cycle rule</param>
    /// <param name="cashflowFlag">Cashflow flag</param>

    public ScheduleParams(Dt accrualStartDate, Dt firstCoupon, Dt nextToLastCoupon, Dt maturity, Frequency freq, BDConvention roll, Calendar cal, CycleRule cycleRule, CashflowFlag cashflowFlag)
    {
      AccrualStartDate = accrualStartDate;
      FirstCouponDate = firstCoupon;
      NextToLastCouponDate = nextToLastCoupon;
      Maturity = maturity;
      Frequency = freq;
      CycleRule = cycleRule;
      CashflowFlag = cashflowFlag;
      Roll = roll;
      Calendar = cal;
    }

    /// <summary>
    /// Accrual Start Date
    /// </summary>
    public Dt AccrualStartDate { get; set; }

    /// <summary>
    /// Maturity Date
    /// </summary>
    public Dt Maturity { get; set; }

    /// <summary>
    /// First Coupon Date
    /// </summary>
    public Dt FirstCouponDate { get; set; }

    /// <summary>
    /// Last coupon that falls before Maturity Date
    /// </summary>
    public Dt NextToLastCouponDate { get; set; }
    /// <summary>
    /// Payment Frequency
    /// </summary>
    public Frequency Frequency { get; set; }

    /// <summary>
    /// End of Month Rule
    /// </summary>
    public CycleRule CycleRule { get; set; }

    /// <summary>
    /// Accrue on Cycle
    /// </summary>
    public CashflowFlag CashflowFlag { get; set; }

    /// <summary>
    /// Method of rolling dates off of non-business days
    /// </summary>
    public BDConvention Roll { get; set; }

    /// <summary>
    /// Holiday Calendar
    /// </summary>
    public Calendar Calendar { get; set; }

  }
}


