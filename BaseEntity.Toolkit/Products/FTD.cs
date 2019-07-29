//
// FTD.cs
//  -2008. All rights reserved.
//

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Nth to default swap product (NTD)
  /// </summary>
  /// <remarks>
  ///   <para>First-to-Default swaps are OTC contracts where a protection buyer pays a periodic
  ///   fee (usually quarterly) to a protection seller for 'insurance' against the first
  ///   credit event on a pool of underlying names. Typically there are 4 to 12 underlying
  ///   names.</para>
  ///   <h1 align="center"><img src="NTD.png" /></h1>
  ///   <para>First to Default Baskets work essentially the same as a single name Credit Default
  ///   Swap except that the Credit Event Payment is triggered when a Credit Event occurs to the
  ///   first of a defined group of reference assets (the Basket).</para>
  ///   <para>Nth-to-Default swaps are an extention where the protection is on the 'nth' credit
  ///   event on the pool of underlying names.</para>
  ///   <para>The Protection Seller is exposed to each of the Reference Assets for the full notional
  ///   amount but ONLY for the First Credit Event among all of the Reference Assets. The Basket
  ///   Swap terminates after the first default.</para>
  ///   <para>Settlement options and procedures are the same as for single name Credit Default
  ///   Swaps / Options. The Premium can be paid as an annuity or as a single payment upfront.</para>
  /// 
  ///   <para><b>Example FTD default swap</b></para>
  ///   <para>An example of a typical default swap is:</para>
  ///   <list type="bullet">
  ///     <item><description>5 year at-market FTD swap on company Goldman, Citi, Deutche and JP.</description></item>
  ///     <item><description>Premium is 53 bp per annum paid quarterly.</description></item>
  ///     <item><description>$100m notional</description></item>
  ///     <item><description>Reference assets are four bonds for each company.</description></item>
  ///     <item><description>Settlement is cash settlement.</description></item>
  ///   </list>
  ///   <para>Until a credit event occurs, the protection buyer pays 53bp/4 * 100MM quarterly.</para>
  ///   <para>If any of the four companies default, the protection seller pays the face value value
  ///   of the reference asset plus accrued premium.</para>
  ///
  ///   <para><b>Pricing</b></para>
  ///   <para>To price or calculate sensitivities for this product, a Pricer must be created.
  ///   The primary pricing model for this product is the
  ///   <see cref="BaseEntity.Toolkit.Pricers.FTDHeterogeneousPricer">Semi-Analytic Pricer</see>.</para>
  ///
  ///   <para><b>Some Intuition</b></para>
  ///   <list type="bullet">
  ///     <item><description>A FTD on a single name is the same as a CDS.</description></item>
  ///     <item><description>Increasing the number of names increases the risk/premium. For uncorrelated
  ///     names, the required premium would be simply the sum of the individual names.</description></item>
  ///     <item><description>As the correlation increases, the risk/premium descreases. For totally
  ///     correlated names, the required premium would be simply the largest spread name.</description></item>
  ///   </list>
  ///
  ///   <para><b>Effect of Correlation on Value</b></para>
  ///   <para>Correlation effects value.</para>
  ///   <h1 align="center"><img src="NTDPremiumVsCorrelation.png"/></h1>
  ///   <para>For 0% correlation, an FTD could carry a premium matching the sum of the individual
  ///   default swaps. For 100% correlation, an FTD would carry a premium of the maximum default swap.</para>
  ///   <para>Increasing correlation increases the probability of extreme events (either good or bad).
  ///   For ITM ‘tranches’ (eg FTD), the increased correlation decreases risk. For an OTM ‘tranches’
  ///   (eg 4th-TD), the increased correlation increases risk.</para>
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class FTD : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    FTD()
    {
      first_ = 1;
      numCovered_ = 1;
      amortizePremium_ = true;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Premium rate</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Premium payment roll method (business day convention)</param>
    /// <param name="cal">Calendar for premium payment rolls</param>
    ///
    public FTD(Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount,
               Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      Premium = premium;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      first_ = 1;
      numCovered_ = 1;
      amortizePremium_ = true;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Premium rate</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Premium payment roll method (business day convention)</param>
    /// <param name="cal">Calendar for premium payment rolls</param>
    /// <param name="first">First name to cover</param>
    /// <param name="numCovered">Number of names to cover</param>
    ///
    public FTD(Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount,
               Frequency freq, BDConvention roll, Calendar cal, int first, int numCovered
               )
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      Premium = premium;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      first_ = first;
      numCovered_ = numCovered;
      amortizePremium_ = true;
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

      // Invalid Last Premium date
      if (!lastPrem_.IsEmpty() && !lastPrem_.IsValid())
        InvalidValue.AddError(errors, this, "LastPrem", String.Format("Invalid first premium date. Must be empty or valid date, not {0}", lastPrem_));


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

      // Upfront fee has to be within a [-2.0 - 2.0] range 
      if (fee_ < -2.0 || fee_ > 2.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between -2 and 2, Not {0}", fee_));

      // Fee settle has to be a valid date 
      if (!feeSettle_.IsEmpty() && !feeSettle_.IsValid())
        InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Invalid fee settlement date. Must be empty or valid date, not {0}", feeSettle_));

      // First name to be covered >= 1
      if (first_ < 1)
        InvalidValue.AddError(errors, this, "First", String.Format("Invalid first name to be covered. Must be at least 1, Not {0}", first_));

      // Number of names covered >= 1
      if (numCovered_ < 1)
        InvalidValue.AddError(errors, this, "First", String.Format("Invalid numbers of names to be covered. Must be at least 1, Not {0}", first_));


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
      set
      {
        premium_ = value;
      }
    }


    /// <summary>
    ///   Up-front fee in percent.
    /// </summary>
    [Category("Base")]
    public double Fee
    {
      get { return fee_; }
      set
      {
        fee_ = value;
      }
    }


    /// <summary>
    ///   Fee payment date
    /// </summary>
    [Category("Base")]
    public Dt FeeSettle
    {
      get { return feeSettle_; }
      set
      {
        feeSettle_ = value;
      }
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
      set
      {
        firstPrem_ = value;
      }
    }


    /// <summary>
    ///   Last premium payment date
    /// </summary>
    [Category("Base")]
    public Dt LastPrem
    {
      get { return lastPrem_; }
      set
      {
        lastPrem_ = value;
      }
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
    ///   First name (i.e. default) to be covered.
    /// </summary>
    [Category("Base")]
    public int First
    {
      get { return first_; }
      set
      {
        first_ = value;
      }
    }


    /// <summary>
    ///   Number of names to be covered.
    /// </summary>
    [Category("Base")]
    public int NumberCovered
    {
      get { return numCovered_; }
      set
      {
        numCovered_ = value;
      }
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
    ///   NTD type
    /// </summary>
    [Category("Base")]
    public NTDType NtdType
    {
      get { return ntdType_; }
      set { ntdType_ = value; }
    }

    /// <summary>
    ///   Bullet payment convention (null means non-bullet)
    /// </summary>
    public BulletConvention Bullet
    {
      get { return bullet_; }
      set { bullet_ = value; }
    }

    /// <summary>
    ///  Denomination currency of loss payment 
    /// </summary>
    ///
    [Category("Base")]
    public Currency RecoveryCcy
    {
      get
      {
        if (recoveryCcy_ == Currency.None) return Ccy;
        return recoveryCcy_;
      }
      set
      {
        recoveryCcy_ = value;
      }
    }
    
    /// <summary>
    ///  Spot FX rate (from Recovery Ccy to Numeraire Ccy) at inception 
    /// </summary>
    ///
    [Category("Base")]
    public double FxAtInception
    {
      get;set;
    }

    /// <summary>
    ///  True if CDS contract regulates to pay recovery at maturity
    /// </summary>
    public bool PayRecoveryAtMaturity
    {
      get
      {
        return payRecoveryAtMaturity_ == null
          ? (ntdType_ != NTDType.Unfunded)
          : payRecoveryAtMaturity_.Value;
      }
      set { payRecoveryAtMaturity_ = value; }
    }

    /// <summary>
    /// Gets or sets the fixed recoveries.
    /// </summary>
    /// <value>The fixed recoveries.</value>
    public double[] FixedRecovery
    {
      get { return fixedRecovery_; }
      set { fixedRecovery_ = value; }
    }
    #endregion

    #region Data

    private NTDType ntdType_ = NTDType.Unfunded;
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
    private int first_;
    private int numCovered_;
    private bool amortizePremium_;
    private BulletConvention bullet_ = null;
    private bool? payRecoveryAtMaturity_;
    private double[] fixedRecovery_;
    private Currency recoveryCcy_;
    #endregion data    
    
  } // class FTD
}
