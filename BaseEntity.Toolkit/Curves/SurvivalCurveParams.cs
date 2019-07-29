/*
 * SurvivalCurveParams.cs
 *
 *   2005-2008. All rights reserved.
 *
 */
using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Survival curve parameters
  /// </summary>
  [Serializable]
  public class SurvivalCurveParameters
  {
    /// <summary>
    ///   Construct survival curve parameters
    /// </summary>
    /// <param name="dayCount">Day count</param>
    /// <param name="frequency">Frequency</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="calendar">calendar</param>
    /// <param name="interpMethod">Interpolation method</param>
    /// <param name="extrapMethod">Extrapolation method</param>
    /// <param name="nspTreatment">Negative probability treatment</param>
    /// <param name="allowNegativeCdsSpreads">Allow negative CDS spreads</param>
    /// <param name="stressed">Whether to fit as stressed</param>
    public SurvivalCurveParameters(
      DayCount dayCount,
      Frequency frequency,
      BDConvention roll,
      Calendar calendar,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      bool allowNegativeCdsSpreads = false,
      bool stressed = false)
    {
      dayCount_ = dayCount;
      frequency_ = frequency;
      roll_ = roll;
      calendar_ = calendar;
      interpMethod_ = interpMethod;
      extrapMethod_ = extrapMethod;
      nspTreatment_ = nspTreatment;
      AllowNegativeCdsSpreads = allowNegativeCdsSpreads;
      Stressed = stressed;
    }

    #region Properties

    /// <summary>
    ///   Gets the day count.
    /// </summary>
    /// <value>The day count.</value>
    public DayCount DayCount
    {
      get { return dayCount_; }
    }

    /// <summary>
    /// Gets the frequency.
    /// </summary>
    /// <value>The frequency.</value>
    public Frequency Frequency
    {
      get { return frequency_; }
    }

    /// <summary>
    /// Gets the business day convention.
    /// </summary>
    /// <value>The business day convention.</value>
    public BDConvention Roll
    {
      get { return roll_; }
    }

    /// <summary>
    /// Gets the calendar.
    /// </summary>
    /// <value>The calendar.</value>
    public Calendar Calendar
    {
      get { return calendar_; }
    }

    /// <summary>
    /// Gets the interpolation method.
    /// </summary>
    /// <value>The interpolation method.</value>
    public InterpMethod InterpMethod
    {
      get { return interpMethod_; }
    }

    /// <summary>
    /// Gets the extrapolation method.
    /// </summary>
    /// <value>The extrapolation method.</value>
    public ExtrapMethod ExtrapMethod
    {
      get { return extrapMethod_; }
    }

    /// <summary>
    /// Gets the method for negative survival probability treatment.
    /// </summary>
    /// <value>The method for negative survival probability treatment.</value>
    public NegSPTreatment NegSPTreatment
    {
      get { return nspTreatment_; }
    }

    /// <summary>
    /// Gets the AllowNegativeCDSSpreads flag.
    /// </summary>
    public bool AllowNegativeCdsSpreads
    {
      get { return (fitFlag_ & AllowNegSpreadFlag) != 0; }
      private set
      {
        if (value)
          fitFlag_ |= AllowNegSpreadFlag;
        else
          fitFlag_ &= ~AllowNegSpreadFlag;
      }
    }

    /// <summary>
    ///  Gets a value indicating whether to force fit curve.
    /// </summary>
    /// <value><c>true</c> if to force fit the curve; otherwise, <c>false</c>.</value>
    public bool Stressed
    {
      get { return (fitFlag_ & StressedFlag) != 0; }
      private set
      {
        if (value)
          fitFlag_ |= StressedFlag;
        else
          fitFlag_ &= ~StressedFlag;
      }
    }


    /// <summary>
    ///  If true, the negative hazard rates are not allowed between two tenor dates
    /// </summary>
    /// <value><c>true</c> if to forbid negative hazard rates; otherwise, <c>false</c>.</value>
    public bool ForbidNegativeHazardRates
    {
      get { return (fitFlag_ & ForbidNegativeHazardFlag) != 0; }
      set
      {
        if (value)
          fitFlag_ |= ForbidNegativeHazardFlag;
        else
          fitFlag_ &= ~ForbidNegativeHazardFlag;
      }
    }


    #endregion Properties

    /// <summary>
    /// Gets the default parameters.
    /// </summary>
    /// <returns>The default parameters.</returns>
    public static SurvivalCurveParameters GetDefaultParameters()
    {
      return new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified, Calendar.None,
                                  InterpMethod.Weighted, ExtrapMethod.Const, NegSPTreatment.Allow);
    }

    #region Data

    private DayCount dayCount_;
    private Frequency frequency_;
    private BDConvention roll_;
    private Calendar calendar_;
    private InterpMethod interpMethod_;
    private ExtrapMethod extrapMethod_;
    private NegSPTreatment nspTreatment_;
    private int fitFlag_ = 0;
    private const int AllowNegSpreadFlag = 1;
    private const int StressedFlag = 2;
    private const int ForbidNegativeHazardFlag = 4;

    #endregion Data

  } // class SurvivalCurveParams

  #region Obsolete

  /// <summary>
  /// Survival curve parameters
  /// </summary>
  [Serializable, Obsolete("Replaced by SurvivalCurveParameters.")]
  public class SurvivalCurveParams
  {
    /// <summary>
    ///   Construct survival curve parameters
    /// </summary>
    /// <param name="cdsStandardContract">CDS Standard Convention Name</param>
    /// <param name="quoteType">CDS quote type (ParSpread, Upfront or ConvSpread)</param>
    /// <param name="actualCoupon">
    ///   Actual running premium. 
    ///   <para>If quote type is Upfront, then this is the running premiums, while market quote is upfront fee;</para>
    ///   <para>If quote type is ParSpread and this is NaN, then the old style par spread quote is assumed;</para>
    ///   <para>If quote type is ParSpread and this is not NaN, then this is a upfront trade with
    ///    the actual coupon given by this parameter, while the market quote is par spread.</para>
    /// </param>
    /// <param name="dayCount">Day count</param>
    /// <param name="frequency">Frequency</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="calendar">calendar</param>
    /// <param name="interpMethod">Interpolation method</param>
    /// <param name="extrapMethod">Extrapolation method</param>
    /// <param name="nspTreatment">Negative probability treatment</param>
    /// <param name="forceFit">Whether to force fit the curves</param>
    public SurvivalCurveParams(
      string cdsStandardContract,
      CDSQuoteType quoteType,
      double actualCoupon,
      DayCount dayCount,
      Frequency frequency,
      BDConvention roll,
      Calendar calendar,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      NegSPTreatment nspTreatment,
      bool forceFit)
    {
      cdsStandardContract_ = cdsStandardContract;
      quoteType_ = quoteType;
      actualCoupon_ = actualCoupon;
      dayCount_ = dayCount;
      frequency_ = frequency;
      roll_ = roll;
      calendar_ = calendar;
      interpMethod_ = interpMethod;
      extrapMethod_ = extrapMethod;
      nspTreatment_ = nspTreatment;
      forceFit_ = forceFit;
    }

    #region Properties

    /// <summary>
    ///   Gets the type of the CDS contract.
    /// </summary>
    /// <value>The type of the CDS contract.</value>
    public string CdsStandardContract
    {
      get { return cdsStandardContract_; }
    }

    /// <summary>
    ///   Gets the type of the CDS quote.
    /// </summary>
    /// <value>The type of the CDS quote.</value>
    public CDSQuoteType CdsQuoteType
    {
      get { return quoteType_; }
    }

    /// <summary>
    ///   Actual coupon
    /// </summary>
    /// <remarks>
    ///   <para>If quote type is ParSpread and ActualCoupon is NaN,
    ///   then this is the old style par spread quote.</para>
    ///   <para>If quote type is ParSpread and ActualCoupon is a legitimate number,
    ///   then this is a trade with upfront running (= ActualCoupon), quoted in par spread.
    ///  </para>
    /// </remarks>
    public double ActualCoupon
    {
      get { return actualCoupon_; }
    }

    /// <summary>
    ///   Gets the day count.
    /// </summary>
    /// <value>The day count.</value>
    public DayCount DayCount
    {
      get { return dayCount_; }
    }

    /// <summary>
    /// Gets the frequency.
    /// </summary>
    /// <value>The frequency.</value>
    public Frequency Frequency
    {
      get { return frequency_; }
    }

    /// <summary>
    /// Gets the business day convention.
    /// </summary>
    /// <value>The business day convention.</value>
    public BDConvention Roll
    {
      get { return roll_; }
    }

    /// <summary>
    /// Gets the calendar.
    /// </summary>
    /// <value>The calendar.</value>
    public Calendar Calendar
    {
      get { return calendar_; }
    }

    /// <summary>
    /// Gets the interpolation method.
    /// </summary>
    /// <value>The interpolation method.</value>
    public InterpMethod InterpMethod
    {
      get { return interpMethod_; }
    }

    /// <summary>
    /// Gets the extrapolation method.
    /// </summary>
    /// <value>The extrapolation method.</value>
    public ExtrapMethod ExtrapMethod
    {
      get { return extrapMethod_; }
    }

    /// <summary>
    /// Gets the method for negative survival probability treatment.
    /// </summary>
    /// <value>The method for negative survival probability treatment.</value>
    public NegSPTreatment NegSPTreatment
    {
      get { return nspTreatment_; }
    }

    /// <summary>
    ///  Gets a value indicating whether to force fit curve.
    /// </summary>
    /// <value><c>true</c> if to force fit the curve; otherwise, <c>false</c>.</value>
    public bool ForceFit
    {
      get { return forceFit_; }
    }

    #endregion Properties

    /// <summary>
    /// Convert to the survival curve parameters.
    /// </summary>
    /// <returns></returns>
    public SurvivalCurveParameters ToSurvivalCurveParameters()
    {
      return new SurvivalCurveParameters(DayCount, Frequency, Roll, Calendar, InterpMethod, ExtrapMethod, NegSPTreatment);
    }

    #region Data

    private string cdsStandardContract_;
    private CDSQuoteType quoteType_;
    private double actualCoupon_;
    private DayCount dayCount_;
    private Frequency frequency_;
    private BDConvention roll_;
    private Calendar calendar_;
    private InterpMethod interpMethod_;
    private ExtrapMethod extrapMethod_;
    private NegSPTreatment nspTreatment_;
    private bool forceFit_;

    #endregion Data

  } // class SurvivalCurveParams

  #endregion Obsolete
}
