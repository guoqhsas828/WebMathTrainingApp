/*
 * QuantoCDS.cs
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{

  ///
  /// <summary>
  ///   Quanto Credit Default Swap product.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>A quanto credit default swap is a credit default swap where the payment
  ///   on a default event is linked to an exchange rate.</para>
  ///
  ///   <para>The CDS fee is in the principal currency. The recovery amount is paid
  ///   in a foreign currency converted to the principal currency at a pre-agreed
  ///   exchange rate.</para>
  /// </remarks>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class QuantoCDS : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    QuantoCDS()
    { }
    
    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium">Annualised premium</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    /// <param name="recoveryCcy">Currency of recovery payment</param>
    /// <param name="recoveryFx">Agreed exchange rate for recovery payment</param>
    ///
    public
    QuantoCDS(Dt effective, Dt maturity, Currency ccy, double premium, DayCount dayCount,
              Frequency freq, BDConvention roll, Calendar cal, Currency recoveryCcy, double recoveryFx)
      : base(effective, maturity, ccy)
    {
      // Use properties for assignment for data validation
      Premium = premium;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      RecoveryCcy = recoveryCcy;
      RecoveryFx = recoveryFx;
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

      if (premium_ < 0.0 || premium_ > 20.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", premium_));

      if (!firstPrem_.IsEmpty() && !firstPrem_.IsValid())
        InvalidValue.AddError(errors, this, "FirstPrem", String.Format("Invalid first premium payment date. Must be empty or valid date, not {0}", firstPrem_));
      
      if (recoveryFx_ < 0)
        InvalidValue.AddError(errors, this, "RecoveryFx", String.Format("Invalid Recovery Exchange Rate. Must be +Ve, not {0}", recoveryFx_));

      return;
    }

    #endregion Methods

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
    ///   Currency of recovery amount
    /// </summary>
    [Category("Base")]
    public Currency RecoveryCcy
    {
      get { return recoveryCcy_; }
      set { recoveryCcy_ = value; }
    }
    
    /// <summary>
    ///   Exchange range for recovery amount.
    /// </summary>
    [Category("Base")]
    public double RecoveryFx
    {
      get { return recoveryFx_; }
      set
      {
        recoveryFx_ = value;
      }
    }

    #endregion Properties

    #region Data

    private double premium_;
    private DayCount dayCount_;
    private Frequency freq_;
    private Dt firstPrem_;
    private Calendar cal_;
    private BDConvention roll_;
    private bool aod_ = true;
    private Currency recoveryCcy_;
    private double recoveryFx_;

    #endregion Data

  } // class QuantoCDS

}
