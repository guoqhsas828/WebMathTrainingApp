/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Flags to deal with cashflows.
  /// </summary>
  [System.Flags]
  public enum CashflowFlag
  {
    /// <summary>
    ///   No flag.
    /// </summary>
    None = 0x00000,

    /// <summary>
    ///   Calculate the accrual based on cycle dates.
    /// </summary>
    AccrueOnCycle = 0x0001,

    /// <summary>
    ///   Roll the last date to a business day.
    /// </summary>
    AdjustLast = 0x0002,

    /// <summary>
    ///   Whether to roll the last payment date.
    /// </summary>
    RollLastPaymentDate = 0x0004,

    /// <summary>
    ///   Whether to roll the first cycle start date to a business day.
    /// </summary>
    RollFirstCycleBegin = 0x0008,

    /// <summary>
    ///   Put irregular accrual period at the end.
    /// </summary>
    StubAtEnd = 0x0010,

    /// <summary>
    ///   Make the irregular accrual period to span at least one regular period.
    ///   <exclude/>
    ///   <prelimnary>Currently not used.</prelimnary>
    /// </summary>
    LongStub = 0x0020,

    /// <summary>
    ///   Include the maturity date in accrual calculation.
    /// </summary>
    IncludeMaturityAccrual = 0x0040,

    /// <summary>
    ///   Accrued paid on default.
    /// </summary>
    AccruedPaidOnDefault = 0x0080,

    /// <summary>
    ///   Include the default date in accrual calculation.
    /// </summary>
    IncludeDefaultDate = 0x0100,

    /// <summary>
    ///   Floating coupons are based on simple compunding rates.
    /// </summary>
    SimpleProjection = 0x0200,

    /// <summary>
    ///   Don't discard last coupon date in any case.
    ///   <exclude/>
    ///   <preliminary>  </preliminary>
    /// </summary>
    RespectLastCoupon = 0x0400,

    /// <summary>
    ///  Notional for computation of accrued includes amortization to pay date.
    ///  If not set it will include amortization to start date
    /// </summary>
    NotionalResetAtPay = 0x0800,

    /// <summary>
    ///  Initial exchange of notional at effective
    /// </summary>
    InitialExchange = 0x1000,

    /// <summary>
    ///  Final notional exchange for computation of accrued includes amortization to pay date.
    ///  If not set it will include amortization to start date
    /// </summary>
    MaturityExchange = 0x2000,

    /// <summary>
    ///  Schedule generation is based entirely on user inputs without using the generated first and last coupon dates.
    /// </summary>
    RespectAllUserDates = 0x4000
  }
}
