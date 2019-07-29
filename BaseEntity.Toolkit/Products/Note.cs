/*
 * Note.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Note product.
  /// </summary>
  /// <remarks>
  ///   The Note product supports a wide range of fixed rate
  ///   money market products such as CDs and ED deposits.
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class Note : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    Note()
    {
      CompoundFreq = Frequency.None;
    }
    
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Issue date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="coupon">Coupon rate</param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="freq">Frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    Note(Dt effective, Dt maturity, Currency ccy, double coupon, DayCount dayCount,
         Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy)
    {
      Coupon = coupon;
      DayCount = dayCount;
      Freq= freq;
      BDConvention = roll;
      Calendar = cal;
      CompoundFreq = freq;
      ZeroCoupon = coupon;
      return;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      Note obj = (Note)base.Clone();
      obj.amortSched_ = CloneUtil.Clone(amortSched_);
      obj.couponSched_ = CloneUtil.Clone(couponSched_);

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

      // First coupon on or after effective date
      if( !FirstCoupon.IsEmpty() && (FirstCoupon < Effective) )
        InvalidValue.AddError(errors, this, "FirstCoupon", String.Format("First coupon {0} must be after effective {1}", FirstCoupon, Effective));

      // First coupon on or before maturity date
      if( !FirstCoupon.IsEmpty() && (FirstCoupon > Maturity) )
        InvalidValue.AddError(errors, this, "FirstCoupon", String.Format("First coupon {0} must be on or before maturity {1}", FirstCoupon, Maturity));

      // Coupon has to be within a [0.0 - 20.0] range
      if (Coupon < -1.0 || Coupon > 20.0) // allow negative interest rate.
        InvalidValue.AddError(errors, this, "Coupon", String.Format("Invalid Coupon. Must be between 0 and 20, Not {0}", Coupon));


      // Validate schedules
      AmortizationUtil.Validate(amortSched_, errors);
      CouponPeriodUtil.Validate(couponSched_, errors);

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Annualised coupon as a number (10% = 0.10)
    /// </summary>
    [Category("Base")]
    public double Coupon{ get; set;}
    
    /// <summary>
    ///   daycount
    /// </summary>
    [Category("Base")]
    public DayCount DayCount{ get; set;}
    
    /// <summary>
    ///   Coupon payment frequency (per year)
    /// </summary>
    [Category("Base")]
    public Frequency Freq{ get; set;}

    /// <summary>
    ///   Coupon compounding frequency (None for MM instrument, Continuous for zero rate instrument)
    /// </summary>
    [Category("Base")]
    public Frequency CompoundFreq { get; set; }

    /// <summary>
    ///   First coupon payment date
    /// </summary>
    [Category("Base")]
    public Dt FirstCoupon{ get; set;}
    
    /// <summary>
    ///   Calendar for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public Calendar Calendar{ get; set;}
    
    /// <summary>
    ///   Roll convention for coupon payment schedule
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention{ get; set;}

    /// <summary>
    ///   Zero-coupon yield
    /// </summary>
    [Category("Base")]
    public double ZeroCoupon { get; set; }

    /// <summary>
    ///   Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get
      {
        if (amortSched_ == null)
          amortSched_ = new List<Amortization>();
        return amortSched_;
      }
    }
    
    /// <summary>
    ///   True if bond amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return (amortSched_ == null || amortSched_.Count == 0) ? false : true; }
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
    ///   True if bond has coupon schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return (couponSched_ == null || couponSched_.Count == 0) ? false : true; }
    }

    #endregion Properties

    #region Data

    private List<Amortization> amortSched_;
    private List<CouponPeriod> couponSched_;

    #endregion Data

  } // class Note

}
