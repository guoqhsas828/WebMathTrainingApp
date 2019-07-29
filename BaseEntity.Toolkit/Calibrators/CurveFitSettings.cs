/*
 * CurveFitSettings.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

namespace BaseEntity.Toolkit.Calibrators
{
  #region InstrumentType

  /// <summary>
  ///   Instrument type for supported quotes for curve fitting
  /// </summary>
  public enum InstrumentType
  {
    /// <summary>
    ///   None
    /// </summary>
    None = 0,

    /// <summary>
    ///   Money Market
    /// </summary>
    MM = 1,

    /// <summary>
    /// Futures
    /// </summary>
    FUT = 2,

    /// <summary>
    /// Swap 
    /// </summary>
    Swap = 3,

    /// <summary>
    /// Basis swap 
    ///  </summary>
    BasisSwap = 4,

    /// <summary>
    /// Funding MM
    /// </summary>
    FUNDMM = 5,

    ///<summary>
    ///  Forward Rate Agreement : pays Fra rate
    ///</summary>
    FRA = 6,

    ///<summary>
    ///  Forward FX Rate
    ///</summary>
    FxForward = 7,

    /// <summary>
    ///  Risk-free Bond
    /// </summary>
    Bond = 8,

    /// <summary>
    /// Forward contract on equity
    /// </summary>
    Forward = 9
  }

  #endregion

  #region PaymentSettings

  /// <summary>
  /// Class that contains properties of payment features
  /// </summary>
  public class PaymentSettings
  {
    #region Constructors

    /// <summary>
    /// Setting for the payment
    /// </summary>
    public PaymentSettings()
    {
      RecProjectionType = ProjectionType.SimpleProjection;
      PayProjectionType = ProjectionType.SimpleProjection;
      RecCompoundingConvention = CompoundingConvention.None;
      RecCompoundingFreq = Frequency.None;
      PayCompoundingFreq = Frequency.None;
      PrincipalExchange = false;
      SpreadOnReceiver = false;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType RecProjectionType { get; set; }

    /// <summary>
    /// Projection type
    /// </summary>
    public ProjectionType PayProjectionType { get; set; }

    /// <summary>
    /// Compounding convention for target index
    /// </summary>
    public CompoundingConvention RecCompoundingConvention { get; set; }

    /// <summary>
    /// Compounding convention for other index
    /// </summary>
    public CompoundingConvention PayCompoundingConvention { get; set; }

    /// <summary>
    /// Compounding frequency target index floating leg
    /// </summary>
    public Frequency RecCompoundingFreq { get; set; }

    /// <summary>
    /// Compounding frequency for projection index floating leg
    /// </summary>
    public Frequency PayCompoundingFreq { get; set; }

    /// <summary>
    /// Cashflow flags
    /// </summary>
    public bool PrincipalExchange { get; set; }

    /// <summary>
    /// Spread on target
    /// </summary>
    public bool SpreadOnReceiver { get; set; }

    /// <summary>
    /// Bond coupon rate
    /// </summary>
    public double Coupon { get; set; }

    /// <summary>
    /// Bond type
    /// </summary>
    public BondType BondType { get; set; }

    /// <summary>
    /// Market quoting convention for the calibration quote if more than one choice (like bond quotes)
    /// </summary>
    public QuotingConvention QuoteConvention { get; set; }

    #endregion

    #region Methods

    ///<summary>
    ///</summary>
    ///<param name="other"></param>
    ///<returns></returns>
    public bool Equals(PaymentSettings other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return Equals(other.RecProjectionType, RecProjectionType) &&
             Equals(other.PayProjectionType, PayProjectionType) &&
             Equals(other.RecCompoundingConvention, RecCompoundingConvention) &&
             other.PrincipalExchange.Equals(PrincipalExchange) &&
             other.SpreadOnReceiver.Equals(SpreadOnReceiver);
    }

    /// <summary>
    /// Equals operator
    /// </summary>
    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      var other = obj as PaymentSettings;
      return other != null && Equals(other);
    }

    /// <summary>
    /// Get hash code
    /// </summary>
    public override int GetHashCode()
    {
      unchecked
      {
        int result = RecProjectionType.GetHashCode();
        result = (result * 397) ^ PayProjectionType.GetHashCode();
        result = (result * 397) ^ RecCompoundingConvention.GetHashCode();
        result = (result * 397) ^ PrincipalExchange.GetHashCode();
        result = (result * 397) ^ SpreadOnReceiver.GetHashCode();
        return result;
      }
    }

    #endregion
  }

  #endregion

  #region CurveFitSettings

  /// <summary>
  ///   Numerical settings for curve fitting.
  /// </summary>
  [Serializable]
  public class CurveFitSettings
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public CurveFitSettings()
    {
      Method = CurveFitMethod.Bootstrap;
      InterpScheme = InterpScheme.FromString("WeightedTensionC1", ExtrapMethod.Smooth, ExtrapMethod.Const);
      FutureWeight = 1;
      MarketWeight = 1;
      BasisQuotesInterpMethod = InterpMethod.Linear;
    }

    /// <summary>
    ///   Default Constructor
    /// </summary>
    /// <remarks>
    ///   <para>Bootstrap method, WeightedTensionC1 interpolation, Smooth extrapolation, AllSecurities true, FuturesOverSwaps false, FuturesOverMM true
    ///   </para>
    /// </remarks>
    /// <param name = "asOf">As of date</param>
    public CurveFitSettings(Dt asOf)
      : this()
    {
      CurveAsOf = asOf;
    }

    /// <summary>
    ///   Constructor with specified tenor overlapping resolve priority order
    /// </summary>
    /// <param name = "asOf">As of</param>
    /// <param name = "overlapResolve">String-type representation of overlap resolving priority order among instruments</param>
    public CurveFitSettings(Dt asOf, string overlapResolve)
      : this(asOf)
    {
      OverlapTreatmentOrder = CreateOverlapTreatmentPriorityOrder(overlapResolve);
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Gets or sets the curve fitting method.
    /// </summary>
    /// <value>The method.</value>
    public CurveFitMethod Method
    {
      get { return method_; }
      set { method_ = value; }
    }

    /// <summary>
    ///   Gets or sets the interp scheme.
    /// </summary>
    /// <value>The interp scheme.</value>
    public InterpScheme InterpScheme
    {
      get { return interpScheme_; }
      set { interpScheme_ = value; }
    }

    /// <summary>
    ///   Gets the interp method.
    /// </summary>
    /// <value>The interp method.</value>
    public InterpMethod InterpMethod
    {
      get { return interpScheme_ != null ? interpScheme_.Method : InterpMethod.Linear; }
    }

    /// <summary>
    ///   Gets the extrap method.
    /// </summary>
    /// <value>The extrap method.</value>
    public ExtrapMethod ExtrapMethod
    {
      get { return interpScheme_ != null ? interpScheme_.UpperExtrapScheme.Method : ExtrapMethod.Const; }
    }

    /// <summary>
    /// Pricing date
    /// </summary>
    internal Dt PricingDate
    {
      get { return pricingDate_.IsEmpty() ? CurveAsOf : pricingDate_; }
      set { pricingDate_ = value; }
    }

    /// <summary>
    ///   Gets or sets the curve settle date.
    /// </summary>
    /// <value>The curve settle.</value>
    public Dt CurveAsOf
    {
      get { return curveAsOf_; }
      set { curveAsOf_ = value; }
    }

    /// <summary>
    ///  Number of business days between the trade date and the curve as-of date.
    /// </summary>
    /// <value>The spot days.</value>
    public int CurveSpotDays { get; set; }

    /// <summary>
    ///  The holiday calendar for spot days.
    /// </summary>
    /// <value>The spot calendar.</value>
    public Calendar CurveSpotCalendar { get; set; }

    /// <summary>
    ///   Gets or sets the tolerance.
    /// </summary>
    /// <value>The tolerance.</value>
    public double Tolerance
    {
      get { return tolerance_; }
      set { tolerance_ = value; }
    }

    /// <summary>
    /// Gets or sets the maximum iterations.
    /// </summary>
    /// <value>The maximum iterations.</value>
    public int MaximumIterations
    {
      get { return maximumIterations_; }
      set { maximumIterations_ = value; }
    }

    /// <summary>
    ///   Enables or disables the automatic tension selection.
    /// </summary>
    public bool EnableAutomaticTension
    {
      get { return HasFlag(EnableAutomaticTensionFlag); }
      set { SetFlag(EnableAutomaticTensionFlag, value); }
    }

    /// <summary>
    ///   Gets or sets the tension factor.
    /// </summary>
    /// <value>The tension factor.</value>
    public double[] TensionFactors
    {
      get { return tensionFactors_; }
      set { tensionFactors_ = value; }
    }

    /// <summary>
    ///   Sets the slope weight.
    /// </summary>
    /// <value>The slope weight.</value>
    public double SlopeWeight
    {
      set { slopeWeight_ = value; }
    }

    /// <summary>
    ///   Sets the curvature weight.
    /// </summary>
    /// <value>The curvature weight.</value>
    public double CurvatureWeight
    {
      set { curvatureWeight_ = value; }
    }

    /// <summary>
    ///   Sets the market weight.
    /// </summary>
    /// <value>The market weight.</value>
    public double MarketWeight
    {
      get { return weightFactor_; }
      set
      {
        if (value < 0.0 || value > 1.0) throw new ToolkitException("Market Weight has to be between 0.0 and 1.0");
        weightFactor_ = value;
      }
    }

    /// <summary>
    ///   Sets the future weight.
    /// </summary>
    /// <value>The future weight.</value>
    public double FutureWeight
    {
      get { return futureWeight_; }
      set
      {
        if (value < 0.0 || value > 1.0) throw new ToolkitException("Future Weight has to be between 0.0 and 1.0");
        futureWeight_ = value;
      }
    }

    /// <summary>
    ///   Gets or sets the slope weight curve.
    /// </summary>
    /// <value>The slope weight curve.</value>
    public Curve SlopeWeightCurve
    {
      get
      {
        if (slopeWeightCurve_ == null) return CreateWeightCurve(curveAsOf_, slopeWeight_, weightFactor_);
        return slopeWeightCurve_;
      }
      set { slopeWeightCurve_ = value; }
    }

    /// <summary>
    ///   Gets or sets the curvature weight curve.
    /// </summary>
    /// <value>The curvature weight curve.</value>
    public Curve CurvatureWeightCurve
    {
      get
      {
        if (curvatureWeightCurve_ == null) return CreateWeightCurve(curveAsOf_, curvatureWeight_, WeightFactor);
        return curvatureWeightCurve_;
      }
      set { curvatureWeightCurve_ = value; }
    }

    /// <summary>
    ///   Gets the weight factor.
    /// </summary>
    /// <value>The weight factor.</value>
    public double WeightFactor
    {
      get { return 0.6 + weightFactor_ * 0.4; }
    }

    /// <summary>
    ///   Gets the future weight factor.
    /// </summary>
    /// <value>The future weight factor.</value>
    public double FutureWeightFactor
    {
      get { return 0.5 + futureWeight_ * 0.5; }
    }

    /// <summary>
    ///   True to select futures over money market
    /// </summary>
    /// <value>
    ///   <c>true</c> if [futures over money market]; otherwise, <c>false</c>.
    /// </value>
    public bool FuturesOverMoneyMarket
    {
      get { return HasFlag(FuturesOverMoneyMarketFlag); }
      set { SetFlag(FuturesOverMoneyMarketFlag, value); }
    }

    /// <summary>
    ///   True to select futures over swaps
    /// </summary>
    public bool FuturesOverSwaps
    {
      get { return HasFlag(FuturesOverSwapsFlag); }
      set { SetFlag(FuturesOverSwapsFlag, value); }
    }

    /// <summary>
    ///   True to calibrate all securities
    /// </summary>
    public bool AllSecurities
    {
      get { return HasFlag(AllSecurityFlag);}
      set { SetFlag(AllSecurityFlag, value); }
    }

    ///<summary>
    /// Use approximate calculation in the averaging process of rate projection
    ///</summary>
    public bool ApproximateRateProjection
    {
      get { return HasFlag(ApproximateRateProjectionFlag); }
      set { SetFlag(ApproximateRateProjectionFlag, value); }
    }

    ///<summary>
    ///  The priority list of all included instrument types in case of overlapping
    ///</summary>
    public InstrumentType[] OverlapTreatmentOrder
    {
      get { return overlapTreatmentOrder_; }
      set { overlapTreatmentOrder_ = value; }
    }

    /// <summary>
    ///   Create fake quotes when needed
    /// </summary>
    public bool CreateQuotes
    {
      get { return HasFlag(CreateQuotesFlag); }
      set { SetFlag(CreateQuotesFlag, value); }
    }

    /// <summary>
    /// Boolean for using the chained swap approach
    /// </summary>
    public bool ChainedSwapApproach
    {
      get { return HasFlag(ChainedSwapApproachFlag); }
      set { SetFlag(ChainedSwapApproachFlag, value);}
    }

    /// <summary>
    /// Boolean for using the chained swap approach
    /// </summary>
    public bool AllSwaps
    {
      get { return HasFlag(AllSwapsFlag); }
      set { SetFlag(AllSwapsFlag, value); }
    }

    /// <summary>
    /// Interpolation method for basis swap quotes
    /// </summary>
    public InterpMethod BasisQuotesInterpMethod
    {
      get { return basisSwapInterpMethod_; }
      set { basisSwapInterpMethod_ = value; }
    }

    internal Interp GetInterp()
    {
      Interp interp = InterpScheme.ToInterp();
      if (HasFlag(EnableAutomaticTensionFlag) && tensionFactors_ == null) return interp;
      if (InterpMethod == InterpMethod.Tension)
      {
        interp.SetTensionFactors(TensionFactors);
      }
      return interp;
    }

    /// <summary>
    /// True to create the curve as a basis on the given parent curve.
    /// </summary>
    public bool CreateAsBasis
    {
      get { return HasFlag(CreateAsBasisFlag); }
      set { SetFlag(CreateAsBasisFlag, value); }
    }

    /// <summary>
    /// Gets or sets the curve day count.
    /// </summary>
    /// <value>The curve day count.</value>
    /// <remarks></remarks>
    public DayCount? CurveDayCount { get; set; }

    /// <summary>
    ///  Internal flags
    /// </summary>
    internal int Flags
    {
      get { return _flags; }
      set { _flags = value;}
    }
    #endregion Properties

    #region Methods

    /// <summary>
    /// Creates a flat curve.
    /// </summary>
    private static Curve CreateFlatCurve(Dt asOf, double val)
    {
      var curve = new Curve(asOf, DayCount.None, Frequency.None);
      curve.Add(asOf, val);
      return curve;
    }

    /// <summary>
    /// Creates a weighted curve.
    /// </summary>
    private static Curve CreateWeightCurve(Dt asOf, double val, double factor)
    {
      if (!Double.IsNaN(val)) return CreateFlatCurve(asOf, val);

      var curve = new Curve(asOf, DayCount.None, Frequency.None);

      // For now weight = .001 first year
      // 0 .00
      // 10 .01 //linear interpolation between 0-1, year 10 on out 0.01
      curve.Interp = new Linear(new Const(), new Const());
      curve.Add(Dt.Add(asOf, Frequency.Annual, 1, false), 0.001 * factor);
      curve.Add(Dt.Add(asOf, Frequency.Annual, 10, false), 0.01 * factor);
      return curve;
    }

    ///<summary>
    /// Utility method to convert the string-based overlapping treatment order into instrument type array
    ///</summary>
    ///<param name="overlapPriorityKey">String-based overlapping treatment order</param>
    ///<returns>Instrument type array</returns>
    public static InstrumentType[] CreateOverlapTreatmentPriorityOrder(string overlapPriorityKey)
    {
      if (!StringUtil.HasValue(overlapPriorityKey))
      {
        return new InstrumentType[0];
      }
      else
      {
        var overlapPriorityOrder = new List<InstrumentType>();
        string[] keys = overlapPriorityKey.Split('+');
        foreach (string key in keys)
        {
          InstrumentType type = RateCurveTermsUtil.ConvertInstrumentType(key, true, InstrumentType.None);
          if (type != InstrumentType.None && type != InstrumentType.BasisSwap && type != InstrumentType.FUNDMM)
            overlapPriorityOrder.Add(type);
        }

        return overlapPriorityOrder.ToArray();
      }
    }

    ///<summary>
    ///  This method convert the overlap treatment instrument order into a formatted string representation
    ///</summary>
    ///<param name = "types">instrument priority order</param>
    ///<returns>Formatted representation in string</returns>
    public static string CreateOverlapTreatmentPriorityOrderStr(InstrumentType[] types)
    {
      string overlapTreatment = "";
      if (types == null) return overlapTreatment;
      foreach (InstrumentType type in types)
      {
        overlapTreatment += (string.IsNullOrEmpty(overlapTreatment) ? "" : "+") + type;
      }

      return overlapTreatment;
    }

    #endregion Methods

    #region data

    //private bool allSecurities_ = true; //By default it will try to calibrate all securities unless otherwise stated
    //private bool createQuotes_;
    private InterpMethod basisSwapInterpMethod_;
    private double curvatureWeight_ = Double.NaN;
    private Curve curvatureWeightCurve_;
    private Dt curveAsOf_;
    //private bool enableAutomaticTension_ = true;
    //private bool futuresOverMoneyMarket_;
    //private bool futuresOverSwaps_;
    private double futureWeight_ = 1.0;
    private InterpScheme interpScheme_;
    private CurveFitMethod method_ = CurveFitMethod.Bootstrap;
    private InstrumentType[] overlapTreatmentOrder_ = new InstrumentType[0]; //include all securities
    private Dt pricingDate_;
    private double slopeWeight_ = Double.NaN;
    private Curve slopeWeightCurve_;
    private double[] tensionFactors_;
    private double tolerance_ = 1E-14;
    private double weightFactor_ = 1.0;
    //private bool createAsBasis_;
    private int maximumIterations_ = -1;

    private int _flags = AllSecurityFlag | EnableAutomaticTensionFlag;
    private const int AllSecurityFlag = 1,
      CreateQuotesFlag = 2,
      EnableAutomaticTensionFlag = 4,
      FuturesOverMoneyMarketFlag = 8,
      FuturesOverSwapsFlag = 16,
      CreateAsBasisFlag = 32,
      ApproximateRateProjectionFlag = 64,
      ChainedSwapApproachFlag = 128,
      AllSwapsFlag = 256;

    private bool HasFlag(int bit)
    {
      return (_flags & bit) != 0;
    }

    private void SetFlag(int bit, bool value)
    {
      if (value) _flags |= bit;
      else _flags &= ~bit;
    }

    #endregion Data
  }

  #endregion

  #region CalibratorSettings

  ///<summary>
  ///  Extended calibration settings with both static data and dynamic components
  ///</summary>
  [Serializable]
  public class CalibratorSettings : CurveFitSettings
  {
    #region Constructor

    ///<summary>
    ///  Default Constructor
    ///</summary>
    public CalibratorSettings()
    {}

    ///<summary>
    ///  Copy from static data
    ///</summary>
    ///<param name = "settings"></param>
    public CalibratorSettings(CurveFitSettings settings)
    {
      InterpScheme = settings.InterpScheme;
      Method = settings.Method;
      Tolerance = settings.Tolerance;
      MaximumIterations = settings.MaximumIterations;
      MarketWeight = settings.MarketWeight;
      FutureWeight = settings.FutureWeight;
      CurveAsOf = settings.CurveAsOf;
      CurveSpotDays = settings.CurveSpotDays;
      CurveSpotCalendar = settings.CurveSpotCalendar;
      TensionFactors = settings.TensionFactors;
      OverlapTreatmentOrder = CloneUtil.Clone(settings.OverlapTreatmentOrder);
      Flags = settings.Flags;

      var cs = settings as CalibratorSettings;
      if (cs == null) return;
      FwdModelParameters = cs.FwdModelParameters;
      OverlayCurve = cs.OverlayCurve;
      OverlayAfterCalibration = cs.OverlayAfterCalibration;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Gets or sets the forward rate model parameters used for convexity adjustments.
    /// </summary>
    /// <value>The model parameters used for convexity adjustments.</value>
    public RateModelParameters FwdModelParameters { get; set; }

    /// <summary>
    ///   Gets or sets the overlay curve.
    /// </summary>
    /// <value>The overlay curve.</value>
    public Curve OverlayCurve { get; set; }

    /// <summary>
    ///   Gets or sets the indicator that the overlay should apply after calibration,
    ///   i.e., on the top of the calibrated curve.
    /// </summary>
    /// <value>The overlay curve.</value>
    public bool OverlayAfterCalibration { get; set; }

    #endregion
  }

  #endregion
}