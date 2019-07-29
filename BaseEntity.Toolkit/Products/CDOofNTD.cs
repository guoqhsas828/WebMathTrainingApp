/*
 * CDOofNTD.cs
 *
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
  ///   Synthetic CDO of Nth to default baskets (CDO of NTD)
  /// </summary>
  /// <remarks>
  ///   <para>Synthetic Collateralized Debt Obligations are OTC contracts which repackage the
  ///   risk of a pool of underlying credit products.</para>
  ///
  ///   <para>Similar in concept to a Collateralized Mortgage Obligation, the CSO tranches
  ///   the losses of an underlying pool of credit products.</para>
  ///
  ///   <para>For CDO of NTD, the underlying asset pool is a set of NTDs. Typically the
  ///   child NTDs have overlapping underlying reference assets. The child
  ///   NTDs pass through losses from the underlying asset pool. The parent CDO
  ///   is then exposed to the passed through losses between some 'attachment' and
  ///   'detachment' point. For example a 2%-7%
  ///   tranche is exposed to all losses above the first 2% of original face to a maximum of
  ///   7% of original face.</para>
  ///
  ///   <para><b>Pricing</b></para>
  ///   <para>To price or calculate sensitivities for this product, a Pricer must be created.
  ///   The primary pricing model for this product is a
  ///   <see cref="BaseEntity.Toolkit.Pricers.CDOofNTDPricer">Semi-Analytic Pricer</see>.</para>
  /// </remarks>
  ///
  /// <example>
  /// <para>The following example demonstrates constructing a CDO of NTD.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                                  // Effective date is today
  ///   Dt maturity = Dt.CDSMaturity(effectiveDate, 5, TimeUnit.Years); // Maturity date is standard 5Yr CDS maturity
  ///
  ///   CDOofNTD cdo =
  ///     new CDOofNTD( effectiveDate,                                  // Effective date
  ///                   maturityDate,                               // Maturity date
  ///                   Currency.EUR,                               // Premium and recovery Currency is Euros
  ///                   0.0,                                        // No up-front fee is paid
  ///                   0.02,                                       // Premium is 200bp per annum
  ///                   DayCount.Actual360,                         // Premium accrual Daycount is Actual/360
  ///                   Frequency.Quarterly,                        // Quarterly premium payments
  ///                   Calendar.TGT,                               // Calendar is Target
  ///                   0.07,                                       // Attachment point is 7%
  ///                   0.1                                         // Detachment point is 10%
  ///                 );
  /// </code>
  /// </example>
  ///
  [Serializable]
  public class CDOofNTD : Product
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
    protected
    CDOofNTD()
    {
      fee_ = 0.0;
      premium_ = 0.0;
      aod_ = true;
      attachment_ = 0.0;
      detachment_ = 1.0;
      amortizePremium_ = true;
      percentOfLosses_ = true;
      feeGuaranteed_ = false;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effectiveDate">Issue Date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="premium">Annualised premium in raw number (0.02 means 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="attachment">Attachment point for tranche in percent (0.1 = 10%)</param>
    /// <param name="detachment">Detachment point for tranche in percent (0.2 = 20%)</param>
    ///
    public
    CDOofNTD(Dt effectiveDate,
              Dt maturityDate,
              Currency currency,
              double fee,
              double premium,
              DayCount dayCount,
              Frequency frequency,
              BDConvention roll,
              Calendar calendar,
              double attachment,
              double detachment)
      : base(effectiveDate, maturityDate, currency)
    {
      // Use properties for assignment for data validation
      Fee = fee;
      Premium = premium;
      DayCount = dayCount;
      Freq = frequency;
      Calendar = calendar;
      BDConvention = roll;
      aod_ = true;
      Attachment = attachment;
      Detachment = detachment;
      amortizePremium_ = true;
      percentOfLosses_ = true;
      feeGuaranteed_ = false;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="firstPremiumDate">First Premium payment date</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="fee">Up-front fee in percent (0.1 = 10%)</param>
    /// <param name="premium">Annualised premium in raw number (0.02 means 200bp)</param>
    /// <param name="dayCount">Daycount of premium accrual payment</param>
    /// <param name="frequency">Frequency (per year) of premium payments</param>
    /// <param name="calendar">Calendar for premium payments</param>
    /// <param name="roll">ISDA business day convention for premium payments</param>
    /// <param name="attachment">Attachment point for tranche in percent (0.1 = 10%)</param>
    /// <param name="detachment">Detachment point for tranche in percent (0.2 = 20%)</param>
    ///
    public
    CDOofNTD(Dt effectiveDate,
              Dt firstPremiumDate,
              Dt maturityDate,
              Currency currency,
              double fee,
              double premium,
              DayCount dayCount,
              Frequency frequency,
              BDConvention roll,
              Calendar calendar,
              double attachment,
              double detachment)
      : base(effectiveDate, maturityDate, currency)
    {
      // Use properties for assignment for data validation
      FirstPrem = firstPremiumDate;
      Fee = fee;
      Premium = premium;
      DayCount = dayCount;
      Freq = frequency;
      Calendar = calendar;
      BDConvention = roll;
      aod_ = true;
      Attachment = attachment;
      Detachment = detachment;
      amortizePremium_ = true;
      percentOfLosses_ = true;
      feeGuaranteed_ = false;
    }

    #endregion

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

      // Invalid First Premium date
      if (!firstPrem_.IsEmpty() && !firstPrem_.IsValid())
        InvalidValue.AddError(errors, this, "FirstPrem", String.Format("Invalid first premium date. Must be empty or valid date, not {0}", firstPrem_));

      // First premium on or after effective date
      if( !firstPrem_.IsEmpty() && (firstPrem_ < Effective) )
        InvalidValue.AddError(errors, this, "FirstPrem", String.Format("First premium {0} must be after effective {1}", firstPrem_, Effective));

      // First premium on or before maturity date
      if( !firstPrem_.IsEmpty() && (firstPrem_ > Maturity) )
        InvalidValue.AddError(errors, this, "FirstPrem", String.Format("First premium {0} must be on or before maturity {1}", firstPrem_, Maturity));

      // Invalid last premium date
      if (!lastPrem_.IsEmpty() && !lastPrem_.IsValid())
        InvalidValue.AddError(errors, this, "LastPrem", String.Format("Invalid last premium date. Must be empty or valid date, not {0}", lastPrem_));

      // Last premium on or after effective date
      if( !lastPrem_.IsEmpty() && (lastPrem_ < Effective) )
        InvalidValue.AddError(errors, this, "LastPrem", String.Format("Last premium {0} must be after effective {1}", lastPrem_, Effective));

      // Last premium on or after first premium
      if( !lastPrem_.IsEmpty() && !firstPrem_.IsEmpty() && (lastPrem_ < firstPrem_) )
        InvalidValue.AddError(errors, this, "LastPrem", String.Format("Last premium {0} must be after first premium {1}", lastPrem_, firstPrem_));

      // Last premium on or before maturity date
      if( !lastPrem_.IsEmpty() && (lastPrem_ > Maturity) )
        InvalidValue.AddError(errors, this, "LastPrem", String.Format("Last premium {0} must be on or before maturity {1}", lastPrem_, Maturity));

      // Premium has to be within a [0.0 - 200.0] range
      if (premium_ < 0.0 || premium_ > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", premium_));

      // Upfront fee has to be within a [0.0 - 1.0] range 
      if (fee_ < 0.0 || fee_ > 1.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between 0 and 1, Not {0}", fee_));

      // Fee settle has to be a valid date 
      if (!feeSettle_.IsEmpty() && !feeSettle_.IsValid())
        InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Invalid fee settlement date. Must be empty or valid date, not {0}", feeSettle_));
      
      // Attachment and detachment point consistent
      if (attachment_ >= detachment_)
        InvalidValue.AddError(errors, this, "Attachment", String.Format("Attachment point {0} must be before detachment point {1}", attachment_, detachment_));
      if ((attachment_ < 0.0) || (attachment_ > 1.0))
        InvalidValue.AddError(errors, this, "Attachment", String.Format("Attachment point must be between 0 and 100%, not {0}", attachment_));
      if ((detachment_ < 0.0) || (detachment_ > 1.0))
        InvalidValue.AddError(errors, this, "Detachment", String.Format("Detachment point must be between 0 and 100%, not {0}", detachment_));


      return;
    }

    #endregion // Methods

    #region Properties

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
      set { fee_ = value;}
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
    [Category("Base")]
    public Dt FirstPrem
    {
      get { return firstPrem_; }
      set { firstPrem_ = value; }
    }


    /// <summary>
    ///   Last premium payment date
    /// </summary>
    [Category("Base")]
    public Dt LastPrem
    {
      get { return lastPrem_; }
      set { lastPrem_ = value; }
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
    ///   Attachment point for tranche
    /// </summary>
    [Category("Base")]
    public double Attachment
    {
      get { return attachment_; }
      set { detachment_ = value; }
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
    ///   True if premium amortizes on default.
    /// </summary>
    [Category("Base")]
    public bool AmortizePremium
    {
      get { return amortizePremium_; }
      set { amortizePremium_ = value; }
    }


    /// <summary>
    ///   True if CDO is percent of losses (false implies percent of defaulted face)
    /// </summary>
    [Category("Base")]
    public bool PercentOfLosses
    {
      get { return percentOfLosses_; }
      set { percentOfLosses_ = value; }
    }


    /// <summary>
    ///   True if CDO fee leg is guaranteed
    /// </summary>
    [Category("Base")]
    public bool FeeGuaranteed
    {
      get { return feeGuaranteed_; }
      set { feeGuaranteed_ = value; }
    }

    #endregion

    #region Data

    private double fee_;
    private Dt feeSettle_;
    private double premium_;
    private DayCount dayCount_;
    private Frequency freq_;
    private Dt firstPrem_;
    private Dt lastPrem_;
    private Calendar cal_;
    private BDConvention roll_;
    private bool aod_;
    private double attachment_;
    private double detachment_;
    private bool amortizePremium_;
    private bool percentOfLosses_;
    private bool feeGuaranteed_;

    #endregion

  } // class CDOofNTD
}
