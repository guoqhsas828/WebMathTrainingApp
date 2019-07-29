/*
 * SyntheticCDO.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Synthetic CDO Tranche (CDO or CSO)
  /// </summary>
  /// <remarks>
  ///   <para>Synthetic Collateralized Debt Obligations are OTC contracts which repackage the
  ///   risk of a pool of underlying credit products.</para>
  ///   <para>Similar in concept to a Collateralized Mortgage Obligation, the CSO tranches
  ///   the losses of an underlying pool of credit products.</para>
  ///   <para>For a particular tranche, the buyer is exposed to the losses of the underlying
  ///   portfolio between some 'attachment' and 'detachment' point. For example a 2%-7%
  ///   tranche is exposed to all losses above the first 2% of original face to a maximum of
  ///   7% of original face.</para>
  ///   <h1 align="center"><img src="CDO.png" width="50%"/></h1>
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <para>A variety of pricing models are supported for this product including
  ///   <see cref="BaseEntity.Toolkit.Pricers.SyntheticCDOMonteCarloPricer">Monte Carlo</see>,
  ///   <see cref="BaseEntity.Toolkit.Pricers.SyntheticCDOHomogeneousPricer">Homogeneous Semi-Analytic</see>,
  ///   <see cref="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer">Semi-Analytic</see>,
  ///   <see cref="BaseEntity.Toolkit.Pricers.SyntheticCDOBaseCorrelationPricer">Base Correlation</see>, and
  ///   <see cref="BaseEntity.Toolkit.Pricers.SyntheticCDOUniformPricer">Large Pool</see>.</para>
  ///
  ///   <para><h2>Some Intuition</h2></para>
  ///   <para>A CDO Tranche may be thought of as an option on the
  ///   underlying portfolio loss. Here the Underlying is the expected loss of the
  ///   portfolio and Volatility is the variance of that loss determined primarily
  ///   by correlation.</para>
  ///   <h1 align="center"><img src="CDOPortfolioLoss.png" width="50%"/></h1>
  /// 
  ///   <para><h2>Effect of Correlation on Value</h2></para>
  ///   <para>Like a NTD, the value of CDOs are effected by the correlation between the default
  ///   probabilities of the underlying names. Increasing correlation increases the probability
  ///   of extreme events (either good or bad).</para>
  ///   <para>For ITM tranches (eg 0-3%), the increased correlation decreases risk. For an OTM
  ///   tranches (eg 7-10%), the increased correlation increases risk.</para>
  ///   <h1 align="center"><img src="CDOProbVsLoss.png" width="50%"/></h1>
  ///   <para>Increasing Correlation increases the variability of the loss distribution.
  ///   Correlation does not effect the expected loss of the portfolio, but it effects the
  ///   distribution of the loss.</para>
  ///   <h1 align="center"><img src="CDOExpectedLossVsTranche.png" width="50%"/></h1>
  ///   <para>For higher correlated portfolios, the expected loss is less concentrated on
  ///   any single loss bucket.</para>
  ///   <h1 align="center"><img src="CDOCorrelationVsSpread.png" width="50%"/></h1>
  ///   <para>For subordinate tranches, spreads decrease as correlation increases. For senior
  ///   tranches, spreads increase as correlation increases.
  ///   Ie. Increasing correlation pushes losses up the capital structure.</para>
  ///   <para>To work with Interest Only (IO), Principal Only (PO), or funded variations, explicitly set the
  ///   <see cref="BaseEntity.Toolkit.Base.CdoType">CdoType</see> property after constructing the CDO.</para>
  /// </remarks>
  /// 
  /// <example>
  /// <para>The following example demonstrates constructing a Synthetic CDO tranche.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                                  // Effective date is today
  ///   Dt maturity = Dt.CDSMaturity(effectiveDate, 5, TimeUnit.Years); // Maturity date is standard 5Yr CDS maturity
  ///
  ///   SyntheticCDO cdo =
  ///     new SyntheticCDO( effectiveDate,                              // Effective date
  ///                       maturityDate,                           // Maturity date
  ///                       Currency.EUR,                           // Premium and recovery Currency is Euros
  ///                       DayCount.Actual360,                     // Premium accrual Daycount is Actual/360
  ///                       Frequency.Quarterly,                    // Quarterly premium payments
  ///                       Calendar.TGT,                           // Calendar is Target
  ///                       0.02,                                   // Premium is 200bp per annum
  ///                       0.0,                                    // No up-front fee is paid
  ///                       0.07,                                   // Attachment point is 7%
  ///                       0.1                                     // Detachment point is 10%
  ///                     );
  /// </code>
  /// </example>
  ///
  [Serializable]
  public class SyntheticCDO : Product
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Defaults to 0-100% tranche.</para>
    /// </remarks>
    ///
    protected SyntheticCDO()
    {
      premium_ = 0.0;
      aod_ = true;
      attachment_ = 0.0;
      detachment_ = 1.0;
      amortizePremium_ = true;
      feeGuaranteed_ = false;
      return;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Defaults to 0-100% tranche.</para>
    /// </remarks>
    ///
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="premium">Annualised premium in raw number (0.02 means 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    ///
    public SyntheticCDO(
      Dt effectiveDate, Dt maturityDate, Currency currency, double premium, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar
      )
      : this(effectiveDate, Dt.Empty, maturityDate, currency, dayCount, frequency, roll, calendar, premium, 0.0, 0.0, 1.0)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Defaults to 0-100% tranche.</para>
    /// </remarks>
    ///
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="premium">Annualised premium in raw number (0.02 means 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    ///
    public SyntheticCDO(
      Dt effectiveDate, Dt maturityDate, Currency currency, double fee, double premium, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar
      )
      : this(effectiveDate, Dt.Empty, maturityDate, currency, dayCount,
      frequency, roll, calendar, premium, fee, 0.0, 1.0)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="premium">Annualised premium in raw number (0.02 means 200bp)</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="attachment">Attachment point for tranche in percent (0.1 = 10%)</param>
    /// <param name="detachment">Detachment point for tranche in percent (0.2 = 20%)</param>
    ///
    public SyntheticCDO(
      Dt effectiveDate, Dt maturityDate, Currency currency, DayCount dayCount, Frequency frequency,
      BDConvention roll, Calendar calendar, double premium, double fee, double attachment, double detachment
      )
      : this(effectiveDate, Dt.Empty, maturityDate, currency, dayCount,
      frequency, roll, calendar, premium, fee, attachment, detachment)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effectiveDate">Issue Date (date premium started accruing)</param>
    /// <param name="firstPremiumDate">First Premium payment date</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="premium">Annualised premium in raw number (0.02 means 200bp)</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="attachment">Attachment point for tranche in percent (0.1 = 10%)</param>
    /// <param name="detachment">Detachment point for tranche in percent (0.2 = 20%)</param>
    ///
    public SyntheticCDO(
      Dt effectiveDate, Dt firstPremiumDate, Dt maturityDate, Currency currency, DayCount dayCount,
      Frequency frequency, BDConvention roll, Calendar calendar, double premium, double fee,
      double attachment, double detachment
      )
      : base(effectiveDate, maturityDate, currency)
    {
      firstPrem_ = firstPremiumDate;
      lastPrem_ = Dt.Empty;
      fee_ = fee;
      premium_ = premium;
      dayCount_ = dayCount;
      freq_ = frequency;
      cal_ = calendar;
      roll_ = roll;
      aod_ = true;
      attachment_ = attachment;
      detachment_ = detachment;
      amortizePremium_ = true;
      feeGuaranteed_ = false;
      return;
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
        // Fee settlement on or after effective date
        if (feeSettle_ < Effective)
          InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Fee settlement {0} must be on or after effective {1}", feeSettle_, Effective));
        // Fee settlement on or before maturity
        if (feeSettle_ > Maturity)
          InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Fee settlement {0} must be on or before maturity {1}", feeSettle_, Maturity));
      }

      if ((attachment_ > 1.0) || (attachment_ < 0.0))
        InvalidValue.AddError(errors, this, "Attachment", String.Format("Attachment point must be between 0 and 100%, not {0}", attachment_));
      if ((detachment_ < 0.0) || (detachment_ > 1.0))
        InvalidValue.AddError(errors, this, "Detachment", String.Format("Detachment point must be between 0 and 100%, not {0}", detachment_));

      // Attachment and detachment point consistent
      if( attachment_ >= detachment_ )
        InvalidValue.AddError(errors, this, "Attachment", String.Format("Attachment point {0} must be before detachment point {1}", attachment_, detachment_));

      if (cdoType_ == CdoType.Po && premium_ != 0.0)
        InvalidValue.AddError(errors, this, "Premium", "PO tranches cannot have running premium");
      if (premium_ < 0.0 || premium_ > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", premium_));
      if (fee_ < -2.0 || fee_ > 2.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between -2.0 and 2.0, Not {0}", fee_));
      if (!feeSettle_.IsEmpty() && !feeSettle_.IsValid())
        InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Invalid fee settlement date. Must be empty or valid date, not {0}", feeSettle_));
      if (fee_ != 0.0 && feeSettle_.IsEmpty() )
        InvalidValue.AddError(errors, this, "FeeSettle", "Have fee but missing fee settlement date.");

      return;
    }

    /// <summary>
    ///   Return average value of fixed recovery rates
    /// </summary>
    /// <returns>Average fix recovery rate</returns>
    private double AverageFixedRecoveryRate()
    {
      if (fixedRecoveryRates_ == null || fixedRecoveryRates_.Length == 0)
        return double.NaN;

      double avg = 0;
      for (int i = 0, idx = 0; i < fixedRecoveryRates_.Length; ++i)
        if (fixedRecoveryRates_[i] >= 0)
          avg += (fixedRecoveryRates_[i] - avg) / (++idx);
      return avg;
    }

    /// <summary>
    ///   Set fixed recovery rates to a single value
    /// </summary>
    /// <param name="rate"></param>
    private void SetFixedRecoveryRates(double rate)
    {
      fixedRecoveryRates_ = new double[] { rate };
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Cdo type
    /// </summary>
    [Category("Base")]
    public CdoType CdoType
    {
      get { return cdoType_; }
      set { cdoType_ = value; }
    }

    /// <summary>
    ///   Attachment point for tranche
    /// </summary>
    [Category("Base")]
    public double Attachment
    {
      get { return attachment_; }
      set { attachment_ = value; }
    }

    /// <summary>
    ///   Detachment point for tranche
    /// </summary>
    [Category("Base")]
    public double Detachment
    {
      get { return detachment_; }
      set { detachment_ = value; }
    }

    /// <summary>
    ///   Tranche width
    /// </summary>
    public double TrancheWidth
    {
      get { return detachment_ - attachment_; }
    }

    /// <summary>
    ///   premium as a number (100bp = 0.01).
    /// </summary>
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
      get { return (!firstPrem_.IsEmpty()) ? firstPrem_ : Schedule.DefaultFirstCouponDate(Effective, freq_, Maturity, false); }
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
    ///   True if premium accrued on default.
    /// </summary>
    [Category("Base")]
    public bool AccruedOnDefault
    {
      get { return aod_; }
      set { aod_ = value; }
    }
    
    /// <summary>
    ///   True if premium amortizes on default.
    /// </summary>
    [Category("Base")]
    public bool AmortizePremium
    {
      get { return amortizePremium_; }
      set { amortizePremium_ = value; }
    }

    /// <summary>
    ///  BulletConvention
    /// </summary>
    [Category("Base")]
    public BulletConvention Bullet
    {
      get { return bullet_; }
      set { bullet_ = value; }
    }
    /// <summary>
    ///   True if CDO fee leg is guaranteed.
    /// </summary>
    [Category("Base")]
    public bool FeeGuaranteed
    {
      get { return feeGuaranteed_; }
      set { feeGuaranteed_ = value; }
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
      get { return AverageFixedRecoveryRate(); }
      set { SetFixedRecoveryRates(value); }
    }

    /// <summary>
    ///   Array of fixed recovery rates (used if FixedRecovery is true)
    /// </summary>
    [Category("Base")]
    public double[] FixedRecoveryRates
    {
      get { return fixedRecoveryRates_; }
      set { fixedRecoveryRates_ = value; }
    }

    #endregion Properties

    #region Data

    private CdoType cdoType_ = CdoType.Unfunded;
    private double attachment_;
    private double detachment_;
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
    private bool aod_;

    private bool amortizePremium_;
    private bool feeGuaranteed_;
    private bool fixedRecovery_;
    private double[] fixedRecoveryRates_;
    private BulletConvention bullet_ = null;
    #endregion Data

  } // class SyntheticCDO
}
