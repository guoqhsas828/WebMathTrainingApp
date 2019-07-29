/*
 * InterestPeriod.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// A period that defines how interest was paid on a loan.
  /// </summary>
  [Serializable]
  public class InterestPeriod : BaseEntityObject, IComparable<InterestPeriod>
  {
    #region Data
    //logger
    private static ILog Log = LogManager.GetLogger(typeof(InterestPeriod));

    // Data
    private readonly Dt startDate_;
    private readonly Dt endDate_;
    private readonly double annualizedCoupon_;
    private readonly double percentageNotional_;
    private readonly Frequency freq_;
    private readonly DayCount dayCount_;
    private readonly BDConvention roll_;
    private readonly Calendar cal_;
    #endregion

    #region Constructors
    /// <summary>
		///   Constructor
		/// </summary>
		///
    /// <param name="startDate">start of call period</param>
    /// <param name="endDate">end of call period</param>
    /// <param name="annualizedCoupon">Annualized coupon for period</param>
    /// <param name="percentageNotional">Percentage Notional for period</param>
    /// <param name="freq">Frequency for period</param>
    /// <param name="dayCount">Frequency for period</param>
    /// <param name="roll">BD Convention for period</param>
		/// <param name="cal">Calendar</param>
		///
    public
		InterestPeriod(Dt startDate, Dt endDate, double annualizedCoupon,
							 double percentageNotional, Frequency freq, DayCount dayCount, 
               BDConvention roll, Calendar cal)
		{	
      startDate_ = startDate;
      endDate_ = endDate;
      annualizedCoupon_ = annualizedCoupon; 
      percentageNotional_ = percentageNotional;
      freq_ = freq;
      dayCount_ = dayCount;
      roll_ = roll;
      cal_ = cal;
		}

    #endregion

    #region Properties

    /// <summary>
    ///   Start of call period
    /// </summary>
    public Dt StartDate
    {
      get { return startDate_; }
    }

    /// <summary>
    ///   End of call period
    /// </summary>
    public Dt EndDate
    {
      get { return endDate_; }
    }

    /// <summary>
    ///   (Annualized) Coupon
    /// </summary>
    public double AnnualizedCoupon
    {
      get { return annualizedCoupon_; }
    }

    /// <summary>
    ///   Daycount of premium
    /// </summary>
    public DayCount DayCount
    {
      get { return dayCount_; }
    }

    /// <summary>
    ///   Premium payment frequency (per year)
    /// </summary>
    public Frequency Freq
    {
      get { return freq_; }
    }

    /// <summary>
    ///   Calendar for premium payment schedule
    /// </summary>
    public Calendar Calendar
    {
      get { return cal_; }
    }

    /// <summary>
    ///   Roll convention for premium payment schedule
    /// </summary>
    public BDConvention BDConvention
    {
      get { return roll_; }
    }

    /// <summary>
    /// Gets or sets the portion of the loan that the period covers.
    /// </summary>
    public double PercentageNotional
    {
      get { return percentageNotional_; }
    }

    #endregion

    #region IComparable<InterestPeriod> Members

    /// <summary>
    /// Compares 2 InterestPeriods end dates
    /// </summary>
    /// <param name="other">InterestPeriod to compare to</param>
    /// <returns></returns>
    public int CompareTo(InterestPeriod other)
    {
      //check start dates
      return Dt.Cmp(this.EndDate, other.EndDate);
    }

    #endregion IComparable<InterestPeriod> Methods
  }
}
