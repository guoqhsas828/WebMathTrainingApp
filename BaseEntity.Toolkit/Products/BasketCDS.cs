/*
 * BasketCDS.cs
 *
 *  -2008. All rights reserved.
 *
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///  BasketCDS product. This product is a basket of CDS products.
  ///  The contract terms mimic that of a CDS. The underlying is a basket of 
  ///  CDS with relative weight assigned to each.
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  public partial class BasketCDS : Product
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    protected
    BasketCDS()
    {
      aod_ = true;
    }

    /// <summary>
    ///   Constructor for a BasketCDS based on years to maturity
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The calendar is None.</description></item>
    ///     <item><description>The first premium payment date is the based on the IMM roll.</description></item>
    ///     <item><description>The last premium payment date is the maturity.</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturityInYears">Years to maturity or scheduled termination date from the effective date (eg 5)</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="cal">Calendar for premium payment</param>
    ///
    public BasketCDS(Dt effective, int maturityInYears, double premium, Calendar cal)
      : this(effective, Dt.CDSMaturity(effective, new Tenor(maturityInYears, TimeUnit.Years)), Currency.None,
      Dt.Empty, premium, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, cal, 0.0, effective)
    { }

    /// <summary>
    ///   Constructor for a BasketCDS based on maturity tenor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Premium DayCount of Actual360.</description></item>
    ///     <item><description>Premium payment frequency of Quarterly.</description></item>
    ///     <item><description>Premium payment business day convention of Following.</description></item>
    ///     <item><description>The calendar is None.</description></item>
    ///     <item><description>The first premium payment date is the based on the IMM roll.</description></item>
    ///     <item><description>The last premium payment date is the maturity.</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturityTenor">Maturity or scheduled temination tenor from the effective date</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="cal">Calendar for premium payment</param>
    ///
    public BasketCDS(Dt effective, Tenor maturityTenor, double premium, Calendar cal)
      : this(effective, Dt.CDSMaturity(effective, maturityTenor), Currency.None, Dt.Empty,
      premium, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, cal, 0.0, effective)
    { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Sets default first premium payment date based on bond maturity and premium
    ///   payment frequency.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturity">Maturity or scheduled temination date</param>
    /// <param name="ccy">Currency of premium and recovery payments</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    ///
    public BasketCDS(Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : this(effective, maturity, ccy, Dt.Empty, premium, dayCount, freq, roll, cal, 0.0, Dt.Empty)
    { }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturity">Maturity or scheduled temination date</param>
    /// <param name="ccy">Currency of premium and recovery payments</param>
    /// <param name="firstPrem">First premium payment date</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    ///
    public BasketCDS(Dt effective, Dt maturity, Currency ccy, Dt firstPrem, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : this(effective, maturity, ccy, firstPrem, premium, dayCount, freq, roll, cal, 0.0, effective)
    { }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date (date premium started accruing) </param>
    /// <param name="maturity">Maturity or scheduled temination date</param>
    /// <param name="ccy">Currency of premium and recovery payments</param>
    /// <param name="firstPrem">First premium payment date</param>
    /// <param name="premium">Annualised premium in percent (0.02 = 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="freq">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="cal">Calendar for premium payments</param>
    /// <param name="fee">Upfront fee in percent (0.1 =10pc)</param>
    /// <param name="feeSettle">Upfront fee payment date</param>
    ///
    public BasketCDS(Dt effective, Dt maturity, Currency ccy, Dt firstPrem, double premium, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal, double fee, Dt feeSettle)
      : base(effective, maturity, ccy)
    {
      firstPrem_ = firstPrem;
      lastPrem_ = Dt.Empty;
      premium_ = premium;
      dayCount_ = dayCount;
      freq_ = freq;
      roll_ = roll;
      cal_ = cal;
      fee_ = fee;
      feeSettle_ = feeSettle;
      aod_ = true; // accrual on default
      return;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      BasketCDS obj = (BasketCDS)base.Clone();

      obj.amortSched_ = CloneUtil.Clone(amortSched_);
      obj.premiumSched_ = CloneUtil.Clone(premiumSched_);

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

      // Validate first premium date
      if (!firstPrem_.IsEmpty())
      {
        if (!firstPrem_.IsValid())
          InvalidValue.AddError(errors, this, "FirstPrem", String.Format("Invalid first premium payment date {0}", firstPrem_));
        // On or after effective date
        if (firstPrem_ < Effective)
          InvalidValue.AddError(errors, this, "FirstPrem", String.Format("First premium {0} must be after effective {1}", firstPrem_, Effective));
        // On or before maturity date
        if (firstPrem_ > Maturity)
          InvalidValue.AddError(errors, this, "FirstPrem", String.Format("First premium {0} must be on or before maturity {1}", firstPrem_, Maturity));
      }

      // Validate last premium date
      if (!lastPrem_.IsEmpty())
      {
        if (!lastPrem_.IsValid())
          InvalidValue.AddError(errors, this, "LastPrem", String.Format("Invalid last premium payment date {0}", lastPrem_));
        // On or after effective date
        if (lastPrem_ < Effective)
          InvalidValue.AddError(errors, this, "LastPrem", String.Format("Last premium {0} must be after effective {1}", lastPrem_, Effective));
        // On or after first premium
        if (!firstPrem_.IsEmpty() && lastPrem_ < firstPrem_)
          InvalidValue.AddError(errors, this, "LastPrem", String.Format("Last premium {0} must be after first premium {1}", lastPrem_, firstPrem_));
        // On or before maturity date
        if (lastPrem_ > Maturity)
          InvalidValue.AddError(errors, this, "LastPrem", String.Format("Last premium {0} must be on or before maturity {1}", lastPrem_, Maturity));
      }

      if (!feeSettle_.IsEmpty())
      {
        // Fee settlement on or after effective date and on or before maturity date
        if (feeSettle_ < Effective)
          InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Fee settlement {0} must be on or after effective {1}", feeSettle_, Effective));
        if (feeSettle_ > Maturity)
          InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Fee settlement {0} must be on or before maturity {1}", feeSettle_, Maturity));
      }

      if (premium_ < 0.0 || premium_ > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", premium_));

      if (fee_ < -2.0 || fee_ > 2.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between -2 and 2, Not {0}", fee_));

      // Validate schedules
      AmortizationUtil.Validate(amortSched_, errors);
      CouponPeriodUtil.Validate(premiumSched_, errors);

      // if market measures will be enabled relating this BasketCDS to a CDX
      if(marketCDX_ != null)
      {
        if(marketCDX_.CdxType != CdxType.Unfunded)
          InvalidValue.AddError(errors, this, "MarketCDX", String.Format("Market CDX must be Unfunded. {0} is {1}", marketCDX_.Description, marketCDX_.CdxType));
      }

      return;
    }
    
    /// <summary>
    ///   Calculate the default first premium date.
    /// </summary>
    /// 
    /// <remarks>
    ///    The first premium date is simply the earlier date of CDSRoll(effective) and maturity.
    ///    Unlike the first coupon payment date, it never rolls on holidays.
    /// </remarks>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    ///
    /// <returns>Default first premium date</returns>
    /// <exclude/>
    public static Dt GetDefaultFirstPremiumDate(Dt effective, Dt maturity)
    {
      Dt dt = Dt.CDSRoll(effective, false);
      if (maturity < dt)
        return maturity;
      return dt;
    }


    /// <summary>
    ///   Calculate the next premium date after a reference date.
    /// </summary>
    ///
    /// <param name="date">Reference date</param>
    ///
    /// <returns>Next premium date</returns>
    ///
    public Dt GetNextPremiumDate(Dt date)
    {
      Dt firstPrem = FirstPrem;
      if (date < firstPrem)
        return firstPrem;
      return Dt.CDSMaturity(date, 1, TimeUnit.Days);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   premium as a number (100bp = 0.01).
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This premium is ignored if a step-up premium schedule exists</para>
    /// </remarks>
    ///
    [Category("Base")]
    public double Premium
    {
      get { return premium_; }
      set { premium_ = value; }
    }

    /// <summary>
    ///   Up-front fee in percent.
    /// </summary>
    [Category("Base")]
    public double Fee
    {
      get { return fee_; }
      set { fee_ = value; }
    }

    /// <summary>
    ///   Fee payment date
    /// </summary>
    [Category("Base")]
    public Dt FeeSettle
    {
      get { return feeSettle_; }
      set { feeSettle_ = value; }
    }

    /// <summary>
    ///   Daycount of premium
    /// </summary>
    [Category("Base")]
    public DayCount DayCount
    {
      get { return dayCount_; }
      set { dayCount_ = value; }
    }

    /// <summary>
    ///   Premium payment frequency (per year)
    /// </summary>
    [Category("Base")]
    public Frequency Freq
    {
      get { return freq_; }
      set { freq_ = value; }
    }

    /// <summary>
    ///   First premium payment date
    /// </summary>
    /// <remarks>
    ///   <para>If the first premium date has not been set, calculates the default on the fly.</para>
    /// </remarks>
    [Category("Base")]
    public Dt FirstPrem
    {
      get { return (!firstPrem_.IsEmpty()) ? firstPrem_ : GetDefaultFirstPremiumDate(Effective, Maturity); }
      set { firstPrem_ = value; }
    }

    /// <summary>
    ///   Last regular premium payment date
    /// </summary>
    [Category("Base")]
    public Dt LastPrem
    {
      get { return lastPrem_; }
      set { lastPrem_ = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    [Category("Base")]
    public CycleRule CycleRule
    {
      get { return cycleRule_; }
      set { cycleRule_ = value; }
    }

    /// <summary>
    ///   Calendar for premium payment schedule
    /// </summary>
    [Category("Base")]
    public Calendar Calendar
    {
      get { return cal_; }
      set { cal_ = value; }
    }

    /// <summary>
    ///   Roll convention for premium payment schedule
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention
    {
      get { return roll_; }
      set { roll_ = value; }
    }

    /// <summary>
    ///   CDS is in funded form (principal exchanged)
    /// </summary>
    [Category("Base")]
    public bool Funded
    {
      get { return (BasketCdsType != BasketCDSType.Unfunded); }
    }

    /// <summary>
    ///   Cds type
    /// </summary>
    [Category("Base")]
    public BasketCDSType BasketCdsType
    {
      get { return basketCdsType_; }
      set { basketCdsType_ = value; }
    }

    /// <summary>
    ///   True if premium accrued on default.
    /// </summary>
    [Category("Base")]
    public bool AccruedOnDefault
    {
      get { return aod_; }
      set { aod_ = value; }
    }

    /// <summary>
    ///   True if fixed recovery rate.
    /// </summary>
    [Category("Base")]
    public bool FixedRecovery
    {
      get { return fixedRecovery_; }
      set { fixedRecovery_ = value; }
    }

    /// <summary>
    ///   Fixed recovery rate (used if FixedRecovery is true)
    /// </summary>
    [Category("Base")]
    public double FixedRecoveryRate
    {
      get { return fixedRecoveryRate_; }
      set { fixedRecoveryRate_ = value; }
    }

    /// <summary>
    ///   Associated CDX for Market measures
    /// </summary>
    [Category("Base")]
    public CDX MarketCDX
    {
      get { return marketCDX_; }
      set { marketCDX_ = value; }
    }


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
    ///   True if CDS amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return (amortSched_ == null || amortSched_.Count == 0) ? false : true; }
    }

    /// <summary>
    ///   Premium schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<CouponPeriod> PremiumSchedule
    {
      get
      {
        if (premiumSched_ == null)
          premiumSched_ = new List<CouponPeriod>();
        return premiumSched_;
      }
    }

    /// <summary>
    ///   True if CDS has premium schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return (premiumSched_ == null || premiumSched_.Count == 0) ? false : true; }
    }

    /// <summary>
    ///  Weights for underlying CDS
    /// </summary>
    public double[] Weights
    {
      get { return weights_; }
      set { weights_ = value; }
    }

    /// <summary>
    ///   True if CDS contract regulates to pay recovery at maturity
    /// </summary>
    public bool PayRecoveryAtMaturity
    {
      get { return payRecoveryAtMaturity_; }
      set { payRecoveryAtMaturity_ = value; }
    }

    #endregion Properties

    #region Data

    private double fee_;
    private Dt feeSettle_;
    private double premium_;
    private DayCount dayCount_;
    private Frequency freq_;
    private Dt firstPrem_;
    private Dt lastPrem_;
    private CycleRule cycleRule_;
    private Calendar cal_;
    private BDConvention roll_;
    private bool aod_ = true;
    private CDX marketCDX_; 

    private bool fixedRecovery_;
    private double fixedRecoveryRate_;

    private List<Amortization> amortSched_;
    private List<CouponPeriod> premiumSched_;

    private BasketCDSType basketCdsType_;
    private double[] weights_;
    private bool payRecoveryAtMaturity_ = false;
    #endregion Data

    #region BulletConvention
    private BulletConvention bullet_;
    /// <summary>
    ///   Bullet payment convention (null means non-bullet)
    /// </summary>
    public BulletConvention Bullet
    {
      get { return bullet_; }
      set { bullet_ = value; }
    }
    #endregion BulletConvertion    
  } // class BasketCDS
}
