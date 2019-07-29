//
// ABS.cs
//   2008. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  ///   Asset-backed Security
  /// </summary>
  /// <remarks>
  ///   <para>A simplified asset-backed security.</para>
  /// </remarks>
  [Serializable]
  public class ABS : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor for Asset-Backed Securities (ABS)
    /// </summary>
    /// <param name="effectiveDate">Effective date of security</param>
    /// <param name="issueDate">Issue date of security</param>
    /// <param name="firstCouponDate">First payment date of security</param>
    /// <param name="lastCouponDate">Last coupon payment date of security</param>
    /// <param name="maturityDate">Maturity date of security</param>
    /// <param name="currency">Currency denomination (e.g. USD)</param>
    /// <param name="freq">Payment periodicity (i.e. frequency) of security</param>
    /// <param name="bdconv">Business day convention</param>
    /// <param name="cal">Calendar</param>
    /// <param name="origBal">Original balance of security</param>
    /// <param name="Bal0">Outstanding balance of security as of pricing date</param>
    /// <param name="dc">Day count convenction</param>
    /// <param name="amort">Range of schedule amortization payments as percentage of original balance</param>
    /// <param name="rate">Fixed Coupon Rate or Floating Spread of ABS</param>
    /// <param name="flt">Flag to denote whether security pays a fixed- or floating-rate coupon (i.e. flt = TRUE for floating-rate security)</param>
    public ABS(
      Dt effectiveDate,
      Dt issueDate,
      Dt firstCouponDate,
      Dt lastCouponDate,
      Dt maturityDate,
      Currency currency,
      Frequency freq,
      BDConvention bdconv,
      Calendar cal,
      double origBal,
      double Bal0,
      DayCount dc,
      double[] amort,
      double rate,
      bool flt
      )
      : base(effectiveDate, maturityDate, currency)
    {
      OriginalBalance = origBal;
      OutstandingBalance = Bal0;
      Factor = Bal0 / origBal;
      Frequency = freq;
      PaySchedule = new Schedule(effectiveDate, issueDate, firstCouponDate, lastCouponDate, maturityDate, freq,
                            bdconv, cal, false, false);
      DayCount = dc;
      AmortizationSchedule = amort;
      IsFloating = flt ;
      Coupon = rate;
    }

    /// <summary>
    /// Clone class object
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      ABS obj = (ABS)base.Clone();
      obj.PaySchedule = this.PaySchedule; //need Clone() to be added to Schedule
      obj.AmortizationSchedule = CloneUtil.Clone(this.AmortizationSchedule);
      return obj;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Outstanding balance at issued date dividied by Original Balance (i.e. 100%)
    /// </summary>
    public int OriginalFactor { get; private set; }

    /// <summary>
    ///   Oustanding Balance as a percentage of Original Balance
    /// </summary>
    public double Factor { get; private set; }

    /// <summary>
    ///   Original Balance of ABS security
    /// </summary>
    public double OriginalBalance { get; private set; }

    /// <summary>
    ///   Outstanding Balance (as of pricing date) of ABS security
    /// </summary>
    public double OutstandingBalance { get; private set; }

    /// <summary>
    ///   Payment Frequency of ABS Security
    /// </summary>
    public Frequency Frequency { get; private set; }

    /// <summary>
    ///   Day Count convention for interest accrual of ABS security
    /// </summary>
    public DayCount DayCount { get; private set; }

    /// <summary>
    ///   Schedule of Payment Dates (from effective date to maturity date) of ABS security
    /// </summary>
    public Schedule PaySchedule { get; private set; }

    /// <summary>
    ///   Scheduled Amortization (as % of Original Balance) of ABS security.
    /// </summary>
    /// <remarks>
    ///   <para>The first element of the array corresponds to the first payment period in which
    ///   principal is scheduled to be paid.</para>
    ///   <para>The last element of the array corresponds to the maturity date of the ABS security.</para>
    /// </remarks>
    public double[] AmortizationSchedule { get; private set; }

    /// <summary>
    /// True if floating rate
    /// </summary>
    public bool IsFloating { get; private set; }

    /// <summary>
    /// Coupon rate
    /// </summary>
    public double Coupon { get; private set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Return the coupon rate given a LIBOR rate for that period
    /// </summary>
    /// <param name="LIBOR">Forward LIBOR backed out from Discount Curve by CashFlowGenerator</param>
    /// <returns>Coupon of Fix-Rate ABS or LIBOR + Spread of Floating ABS</returns>
    public double GetCoupon(double LIBOR)
    {
      if (this.IsFloating)
      {
        return LIBOR + this.Coupon;
      }
      else
      {
        return this.Coupon;
      }
    }

    #endregion Methods

  }
}
