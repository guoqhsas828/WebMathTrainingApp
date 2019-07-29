/*
 * CMCDS.cs
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
  ///   Constant Maturity Credit Default Swap product (CMCDS).
  /// </summary>
  /// <remarks>
  ///   <para>Constant Maturity CDS are an extension of the standard CDS where the premium paid
  ///   is a floating coupon proportional to the observed market spread on a CDS with a fixed
  ///   time to maturity.</para>
  ///   <para>For standard CMCDS, the floating coupon if set at the start of the coupon period
  ///   (advance) and paid at the end of the coupon period (arrears). The premium paid is
  ///   a factor (the participation rate) of the constant-maturity spread.</para>
  ///   <para>Usually a cap and/or floor is incorporated in the product where the maxium
  ///   or minimum floating coupon is specified. The cap and floor are on the reset spread
  ///   before the participation rate is applied.</para>
  ///   <h1 align="center"><img src="CMCDS.jpg" align="middle"/></h1>
  ///   <para>The fee payed is the participation rate * the constant-maturity spread</para>
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class CMCDS : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    CMCDS()
    {
      aod_ = true;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="years">Years to maturity from effective date</param>
    /// <param name="tenor">Tenor on which premium resets are based</param>
    /// <param name="ccy">Currency</param>
    /// <param name="participation">Participation rate (as a decimal - 40% = 0.4)</param>
    /// <param name="cap">Premium reset cap if any (as a decimal - 5% = 0.05)</param>
    /// <param name="floor">Premium reset floor if any (as a decimal - 5% = 0.05)</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    CMCDS(Dt effective, int years, Tenor tenor, Currency ccy, double participation, double cap, double floor, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, Dt.CDSMaturity(effective, new Tenor(years, TimeUnit.Years)), ccy)
    {
      // Use properties for assignment for data validation
      ResetTenor = tenor;
      Participation = participation;
      Cap = cap;
      Floor = floor;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      aod_ = true;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="tenor">Tenor on which premium resets are based</param>
    /// <param name="ccy">Currency</param>
    /// <param name="participation">Participation rate</param>
    /// <param name="cap">Premium reset cap (if any)</param>
    /// <param name="floor">Premium reset floor (if any)</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    CMCDS(Dt effective, Dt maturity, Tenor tenor, Currency ccy, double participation, double cap, double floor, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy)
    {
      // Use properties for assignment for data validation
      ResetTenor = tenor;
      Participation = participation;
      Cap = cap;
      Floor = floor;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      aod_ = true;
    }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="tenor">Tenor on which premium resets are based</param>
    /// <param name="ccy">Currency</param>
    /// <param name="firstPrem">First premium payment date</param>
    /// <param name="participation">Participation rate</param>
    /// <param name="cap">Premium reset cap (if any)</param>
    /// <param name="floor">Premium reset floor (if any)</param>
    /// <param name="dayCount">Daycount of premium</param>
    /// <param name="freq">Frequency of premium payment</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="cal">Calendar for coupon rolls</param>
    ///
    public
    CMCDS(Dt effective, Dt maturity, Tenor tenor, Currency ccy, Dt firstPrem, double participation, double cap, double floor, DayCount dayCount,
        Frequency freq, BDConvention roll, Calendar cal)
      : base(effective, maturity, ccy)
    {
      // Use properties for assignment for data validation
      FirstPrem = firstPrem;
      ResetTenor = tenor;
      Participation = participation;
      Cap = cap;
      Floor = floor;
      DayCount = dayCount;
      Freq = freq;
      BDConvention = roll;
      Calendar = cal;
      aod_ = true;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      CMCDS obj = (CMCDS)base.Clone();
      return obj;
    }

    #endregion // Constructors

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

      // Participation mustbe within [0-200] range
      if (participation_ < 0.0 || participation_ > 200.0)
        InvalidValue.AddError(errors, this, "Participation", String.Format("Participation  Must be between 0 and 200, Not {0}", participation_));

      // Cap must be within [0-200] range
      if (cap_ < 0.0 || cap_ > 200.0)
        InvalidValue.AddError(errors, this, "Cap", String.Format("Cap  Must be between 0 and 200, Not {0}", cap_));

      // Floor must be within [0-200] range
      if (floor_ < 0.0 || floor_ > 200.0)
        InvalidValue.AddError(errors, this, "Floor", String.Format("Cap  Must be between 0 and 200, Not {0}", floor_));

      //  Fee has to be within a [0.0 - 1.0] range 
      if (fee_ < 0.0 || fee_ > 1.0)
        InvalidValue.AddError(errors, this, "Fee", String.Format("Invalid fee. Must be between 0 and 1, Not {0}", fee_));

      // Fee settle has to be a valid date 
      if (!feeSettle_.IsEmpty() && !feeSettle_.IsValid())
        InvalidValue.AddError(errors, this, "FeeSettle", String.Format("Invalid fee settlement date. Must be empty or valid date, not {0}", feeSettle_));
      

      return;
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Participation rate
    /// </summary>
    [Category("Base")]
    public double Participation
    {
      get { return participation_; }
      set { participation_ = value; }
    }


    /// <summary>
    ///   Constant maturity tenor for premium resets.
    /// </summary>
    [Category("Base")]
    public Tenor ResetTenor
    {
      get { return tenor_; }
      set { tenor_ = value; }
    }


    /// <summary>
    ///   Reset premium cap as a number (100bp = 0.01).
    ///   Set to zero to indicate no cap.
    /// </summary>
    [Category("Base")]
    public double Cap
    {
      get { return cap_; }
      set { cap_ = value; }
    }


    /// <summary>
    ///   Reset premium floor as a number (100bp = 0.01).
    ///   Set to zero to indicate no floor.
    /// </summary>
    [Category("Base")]
    public double Floor
    {
      get { return floor_; }
      set { floor_ = value; }
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

    #endregion

    #region Data

    private double participation_;
    private Tenor tenor_;
    private double cap_;
    private double floor_;
    private double fee_;
    private Dt feeSettle_;
    private DayCount dayCount_;
    private Frequency freq_;
    private Dt firstPrem_;
    private Dt lastPrem_;
    private Calendar cal_;
    private BDConvention roll_;
    private bool aod_;

    #endregion

  } // class CMCDS

}
