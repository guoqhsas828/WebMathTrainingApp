using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Transport class to return sets of dates and their options from this utility.
  /// </summary>
  public interface IScheduleParams
  {
    /// <summary>
    /// Accrual Start Date
    /// </summary>
    Dt AccrualStartDate { get; }

    /// <summary>
    /// Maturity Date
    /// </summary>
    Dt Maturity { get; }

    /// <summary>
    /// First Coupon Date
    /// </summary>
    Dt FirstCouponDate { get; }

    /// <summary>
    /// Last coupon that falls before Maturity Date
    /// </summary>
    Dt NextToLastCouponDate { get; }

    /// <summary>
    /// Payment Frequency
    /// </summary>
    Frequency Frequency { get; }

    /// <summary>
    /// End of Month Rule
    /// </summary>
    CycleRule CycleRule { get; }

    /// <summary>
    /// Accrue on Cycle
    /// </summary>
    CashflowFlag CashflowFlag { get; }

    /// <summary>
    /// Method of rolling dates off of non-business days
    /// </summary>
    BDConvention Roll { get; }

    /// <summary>
    /// Holiday Calendar
    /// </summary>
    Calendar Calendar { get; }

  }
}


