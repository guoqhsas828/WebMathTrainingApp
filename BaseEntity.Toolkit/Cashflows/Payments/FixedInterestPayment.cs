
/*
 * FixedInterestPayment.cs
*/



using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Fixed Interest Payment Object
  /// </summary>
  [Serializable]
  public class FixedInterestPayment : InterestPayment
  {
    #region Constructors

    /// <summary>
    /// Create a simple fixed payment
    /// </summary>
    /// <param name="prevPayDt">Previous Payment Date or Accrual Start</param>
    /// <param name="payDt">Payment Date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Start of payment cycle</param>
    /// <param name="cycleEnd">End of payment cycle</param>
    /// <param name="periodStart">Start of accrual period</param>
    /// <param name="periodEnd">End of accrual period</param>
    /// <param name="exDivDt">Ex dividend date</param>
    /// <param name="notional">notional for this payment</param>
    /// <param name="coupon">fixed coupon for this payment</param>
    /// <param name="dc">Daycount convention</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    public FixedInterestPayment(Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart,
                                Dt periodEnd, Dt exDivDt,
                                double notional, double coupon, DayCount dc, Frequency compoundingFreq)
      : base(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart, periodEnd, exDivDt, notional, dc, compoundingFreq)
    {
      FixedCoupon = coupon;
    }

    #endregion

    #region Properties
    /// <summary>
    /// Zero coupon
    /// </summary>
    public bool ZeroCoupon { get; set; }

    /// <summary>
    /// Rate for the period
    /// </summary>
    public override double EffectiveRate
    {
      get { return CalculateCoupon(); }
      set { FixedCoupon = value; }
    }

    #endregion

    #region Methods 

    private double CalculateCoupon()
    {
      if (ZeroCoupon)
      {
        if (CompoundingFrequency != Frequency.None)
        {
          return (Math.Pow(1 + FixedCoupon/(double) CompoundingFrequency,
                AccrualFactor*(double) CompoundingFrequency)
              - 1.0)/
            AccrualFactor;
        }
        return FixedCoupon;
      }

      if (RateSchedule == null)
      {
        return FixedCoupon;
      }
      // calculate average coupon
      return CouponPeriodUtil.CalculateAverageCoupon(PeriodStartDate,
        PeriodEndDate, DayCount, RateSchedule);
    }

    /// <summary>
    /// Add to data table
    /// </summary>
    /// <param name="collection">Column collection</param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      
      if (!collection.Contains("Coupon"))
        collection.Add(new DataColumn("Coupon", typeof (double)));
      base.AddDataColumns(collection);
      if (!collection.Contains("Amount"))
        collection.Add(new DataColumn("Amount", typeof (double)));
    }

    /// <summary>
    /// Add to data table
    /// </summary>
    /// <param name="row">Row</param>
    /// <param name="dtFormat">Format</param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Coupon"] = FixedCoupon;
      row["Amount"] = Amount;
    }

    #endregion
  }
}