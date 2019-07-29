/*
 * Convertible.cs
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{

  ///
  /// <summary>
  ///   Convertible Bond product
  /// </summary>
  ///
  /// <preliminary>
  ///   This class is preliminary and not supported for customer use. This may be
  ///   removed or moved to a separate product at a future date.
  /// </preliminary>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class Convertible : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    Convertible()
    { }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="coupon">Coupon of bond</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="freq">Frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    Convertible(Dt effective, Dt maturity, Currency ccy, double coupon,
                DayCount dayCount, Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy)
    {
      Coupon = coupon;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      Convertible obj = (Convertible)base.Clone();

      obj.couponSched_ = CloneUtil.Clone(couponSched_);
      if (callSched_ == null)
        obj.callSched_ = null;
      else
      {
        obj.callSched_ = new ArrayList();
        foreach (CallPeriod c in callSched_)
          obj.callSched_.Add((CallPeriod)c.Clone());
      }
      if (putSched_ == null)
        obj.putSched_ = null;
      else
      {
        obj.putSched_ = new ArrayList();
        foreach (PutPeriod c in putSched_)
          obj.putSched_.Add((PutPeriod)c.Clone());
      }
      if (convSched_ == null)
        obj.convSched_ = null;
      else
      {
        obj.convSched_ = new ArrayList();
        foreach (ConversionPeriod c in convSched_)
          obj.convSched_.Add((ConversionPeriod)c.Clone());
      }
      if (refixSched_ == null)
        obj.refixSched_ = null;
      else
      {
        obj.refixSched_ = new ArrayList();
        foreach (RefixPeriod c in refixSched_)
          obj.refixSched_.Add((RefixPeriod)c.Clone());
      }

      return obj;
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
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Invalid First Coupon date
      if (!firstCoupon_.IsEmpty() && !firstCoupon_.IsValid())
        InvalidValue.AddError(errors, this, "FirstCoupon", String.Format("Invalid first premium date. Must be empty or valid date, not {0}", firstCoupon_));

      // First coupon on or after effective date
      if( !firstCoupon_.IsEmpty() && (firstCoupon_ < Effective) )
        InvalidValue.AddError(errors, this, "FirstCoupon", String.Format("First coupon {0} must be after effective {1}", firstCoupon_, Effective));

      // First coupon on or before maturity date
      if( !firstCoupon_.IsEmpty() && (firstCoupon_ > Maturity) )
        InvalidValue.AddError(errors, this, "FirstCoupon", String.Format("First coupon {0} must be on or before maturity {1}", firstCoupon_, Maturity));

      // Invalid last coupon date
      if (!lastCoupon_.IsEmpty() && !lastCoupon_.IsValid())
        InvalidValue.AddError(errors, this, "LastCoupn", String.Format("Invalid last premium date. Must be empty or valid date, not {0}", lastCoupon_));

      // Coupon has to be within a [0.0 - 20] range
      if (coupon_ < 0.0 || coupon_ > 20.0)
        InvalidValue.AddError(errors, this, "Coupon", String.Format("Invalid Coupon. Must be between 0 and 20, Not {0}", coupon_));


      CouponPeriodUtil.Validate(couponSched_, errors);

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Annualised coupon as a number (10% = 0.10)
    /// </summary>
    [Category("Base")]
    public double Coupon
    {
      get { return coupon_; }
      set { coupon_ = value; }
    }  

    /// <summary>
    ///   daycount
    /// </summary>
    [Category("Base")]
    public DayCount DayCount
    {
      get { return dayCount_; }
      set { dayCount_ = value; }
    }
    
    /// <summary>
    ///   Coupon payment frequency (per year)
    /// </summary>
    [Category("Base")]
    public Frequency Freq
    {
      get { return freq_; }
      set { freq_ = value; }
    }
    
    /// <summary>
    ///   First coupon payment date
    /// </summary>
    [Category("Base")]
    public Dt FirstCoupon
    {
      get { return firstCoupon_; }
      set { firstCoupon_ = value; }
    } 

    /// <summary>
    ///   Last coupon payment date
    /// </summary>
    [Category("Base")]
    public Dt LastCoupon
    {
      get { return lastCoupon_; }
      set { lastCoupon_ = value; }
    }
    
    /// <summary>
    ///   Roll convention for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention
    {
      get { return roll_; }
      set { roll_ = value; }
    }
    
    /// <summary>
    ///   Calendar for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public Calendar Calendar
    {
      get { return cal_; }
      set { cal_ = value; }
    }
    
    /// <summary>
    ///   Ticker for underlying equity
    /// </summary>
    [Category("Base")]
    public string Ticker
    {
      get { return ticker_; }
      set { ticker_ = value; }
    }
    
    /// <summary>
    ///   Issue Strike price
    /// </summary>
    [Category("Base")]
    public double IssueStrike
    {
      get { return issueStrike_; }
      set { issueStrike_ = value; }
    }
    
    /// <summary>
    ///   Current Strike price
    /// </summary>
    [Category("Base")]
    public double CurrentStrike
    {
      get { return currentStrike_; }
      set { currentStrike_ = value; }
    }
    
    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> CouponSchedule
    {
      get
      {
        if (couponSched_ == null)
          couponSched_ = new List<CouponPeriod>();
        return couponSched_;
      }
    }
    
    /// <summary>
    ///   Call schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public ArrayList CallSchedule
    {
      get
      {
        if (callSched_ == null)
          callSched_ = new ArrayList();
        return callSched_;
      }
    }
    
    /// <summary>
    ///   Put schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public ArrayList PutSchedule
    {
      get
      {
        if (putSched_ == null)
          putSched_ = new ArrayList();
        return putSched_;
      }
    }
    
    /// <summary>
    ///   Conversion schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public ArrayList ConversionSchedule
    {
      get
      {
        if (convSched_ == null)
          convSched_ = new ArrayList();
        return convSched_;
      }
    }
    
    /// <summary>
    ///   Refixing schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public ArrayList RefixSchedule
    {
      get
      {
        if (refixSched_ == null)
          refixSched_ = new ArrayList();
        return refixSched_;
      }
    }

    #endregion Properties

    #region Data

    private double coupon_;                                            // Coupon rate
    private DayCount dayCount_;                                        // Coupon daycount
    private Frequency freq_;                                           // Coupon frequency
    private Dt firstCoupon_;                                           // First coupon date
    private Dt lastCoupon_;                                            // Last coupon date
    private BDConvention roll_;                                        // Coupon roll
    private Calendar cal_;                                             // Calendar for coupon roll
    private string ticker_ = null;                                     // Equity ticker
    private double issueStrike_ = 0.0;                                 // Issue strike
    private double currentStrike_ = 0.0;                               // Current strike
    private List<CouponPeriod> couponSched_;                           // Coupon schedule
    private ArrayList callSched_;                                      // Call schedule
    private ArrayList putSched_;                                       // Put schedule
    private ArrayList convSched_;                                      // Conversion schedule
    private ArrayList refixSched_;                                     // Refixing schedule

    #endregion Data

  } // class Convertible

}
