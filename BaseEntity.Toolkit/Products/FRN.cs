/*
 * FRN.cs
 *
 *  -2008. All rights reserved.
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
  ///   Floating Rate Note product
  /// </summary>
  ///
  /// <preliminary>
  ///   This class is preliminary and not supported for customer use. This may be
  ///   removed or moved to a separate product at a future date.
  /// </preliminary>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class FRN : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    FRN()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date of underlying bond</param>
    /// <param name="maturity">Maturity date of underlying bond</param>
    /// <param name="ccy">Currency of underlying bond and option</param>
    /// <param name="coupon">Coupon rate</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="freq">Frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <param name="tenor">Floating rate tenor</param>
    /// <param name="index">Floating rate index</param>
    ///
    public FRN(Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
               int freq, BDConvention roll, Calendar cal, int tenor, string index)
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      Coupon = coupon;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      Tenor = tenor;
      Index = index;
    }
    
    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      FRN obj = (FRN)base.Clone();

#if NOT_SUPPORTED_YET // RTD Apr'08
      obj.amortSched_ = CloneUtil.Clone(amortSched_);
      obj.couponSched_ = CloneUtil.Clone(couponSched_);
#endif

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

      // Coupon has to be within a [0.0 - 2] range
      if (coupon_ < 0.0 || coupon_ > 2.0)
        InvalidValue.AddError(errors, this, "Coupon", String.Format("Invalid Coupon. Must be between 0 and 2, Not {0}", coupon_));

      // Invalid Coupon Frequency
      if (!(freq_ == 0 || freq_ == 1 || freq_ == 2 || freq_ == 3 || freq_ == 4 || freq_ == 12 || freq_ == 365))
        InvalidValue.AddError(errors, this, "Frequency", String.Format("Invalid Coupon Frequency"));

      // Invalid Rate Tenor
      if (!(tenor_ == 0 || tenor_ == 1 || tenor_ == 2 || tenor_ == 3 || tenor_ == 4 || tenor_ == 12 || tenor_ == 365))
        InvalidValue.AddError(errors, this, "Tenor", String.Format("Invalid Rate Tenor"));


      // Invalid index
      if (index_ == null || index_ == "")
        InvalidValue.AddError(errors, this, "Index", String.Format("Invalid Floating Index. Can not be {0}", index_));

      // Principal >= 0
      if (principal_ < 0)
        InvalidValue.AddError(errors, this, "Principal", String.Format("Invalid Principal. Must be +Ve, Not {0}", principal_));


#if NOT_SUPPORTED_YET // RTD Apr'08
      AmortizationUtil.Validate(amortSched_, errors);
      CouponPeriodUtil.Validate(couponSched_, errors);
#endif

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
      set
      {
        coupon_ = value;
      }
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
    public int Freq
    {
      get { return freq_; }
      set
      {
        freq_ = value;
      }
    }
    
    /// <summary>
    ///   First coupon payment date
    /// </summary>
    [Category("Base")]
    public Dt FirstCoupon
    {
      get { return firstCoupon_; }
      set
      {
        firstCoupon_ = value;
      }
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
    ///   Roll convention for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention
    {
      get { return roll_; }
      set { roll_ = value; }
    }
    
    /// <summary>
    ///   Floating rate rate tenor
    /// </summary>
    [Category("Base")]
    public int Tenor
    {
      get { return tenor_; }
      set
      {
        tenor_ = value;
      }
    }
    
    /// <summary>
    ///   Floating rate index
    /// </summary>
    [Category("Base")]
    public string Index
    {
      get { return index_; }
      set
      {
        index_ = value;
      }
    }

#if NOT_SUPPORTED_YET // RTD Apr'08
    /// <summary>
    ///   Amortization schedule
    /// </summary>
    [Category("Base")]
    public List<Amortization> AmortizationSchedule
    {
      get
      {
        if (amortSched_ == null)
          amortSched_ = new List<Amortization>();
        return amortSched_;
      }
    }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Base")]
    public List<CouponPeriod> CouponSchedule
    {
      get
      {
        if (couponSched_ == null)
          couponSched_ = new List<CouponPeriod>();
        return couponSched_;
      }
    }
#endif

    /// <summary>
    ///    Principal amount
    /// </summary>
    [Category("Base")]
    public double Principal
    {
      get { return principal_; }
      set
      {
        principal_ = value;
      }
    }

    #endregion Methods

    #region Data

    private double coupon_;
    private DayCount dayCount_;
    private int freq_;
    private Dt firstCoupon_;
    private Calendar cal_;
    private BDConvention roll_;
    private int tenor_;
    private string index_;
#if NOT_SUPPORTED_YET // RTD Apr'08
    private List<Amortization> amortSched_;
    private List<CouponPeriod> couponSched_;
#endif
    private double principal_ = 1.0;

    #endregion Data

  } // class FRN

}
