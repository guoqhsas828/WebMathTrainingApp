using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Observable market index. 
  /// </summary>
  ///<remarks>Whenever this class is implemented, should update the Factory method Get</remarks> 
  [Serializable]
  public abstract class CouponCalculator : BaseEntityObject, IRateProjector
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    ///<param name="referenceIndex">Reference index object</param>
    ///<param name="referenceCurve">Reference rate curve used for the calculation of the rate. </param>
    /// <param name="discount">Discount curve (if different than projection) used in the calculation of the rate. 
    /// For instance this enters in the computation of the swap rate when discount and projection are not the same. </param>
    protected CouponCalculator(Dt asOf, ReferenceIndex referenceIndex, CalibratedCurve referenceCurve,
                               DiscountCurve discount)
    {
      AsOf = asOf;
      ReferenceIndex = referenceIndex;
      ReferenceCurve = referenceCurve;
      DiscountCurve = discount;
      ResetLag = Tenor.Empty;
      EndIsFixingDate = false;
      UseFutureResets = false;
      UseAsOfResets = true;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    ///<param name="referenceIndex">Reference index object</param>
    ///<param name="referenceCurve">Reference rate curve used for the calculation of the rate. 
    /// For instance for Libor rate or CMS, this is a DiscountCurve object</param>
    protected CouponCalculator(Dt asOf, ReferenceIndex referenceIndex, CalibratedCurve referenceCurve) :
      this(asOf, referenceIndex, referenceCurve, null)
    {
    }

    /// <summary>
    /// Static constructor 
    /// </summary>
    /// <param name="asOf">As of date </param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="discount">Discount curve</param>
    /// <param name="projectionParams">Projection parameters</param>
    /// <returns>A coupon calculator object</returns>
    public static CouponCalculator Get(Dt asOf, ReferenceIndex referenceIndex, CalibratedCurve referenceCurve,
                                       DiscountCurve discount, ProjectionParams projectionParams)
    {
      if (referenceIndex == null)
        return null;
      return Create(asOf, referenceIndex, referenceCurve, discount, projectionParams);
    }

    /// <summary>
    /// Static constructor of a coupon calculator with null reference curve and discount. Used for calculations that 
    /// do not involve the curve such as display of rate/price reset information
    /// </summary>
    /// <param name="asOf">As of date </param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="projectionParams">Projection parameters</param>
    /// <returns>A coupon calculator object</returns>
    public static CouponCalculator Get(Dt asOf, ReferenceIndex referenceIndex, ProjectionParams projectionParams)
    {
      return Create(asOf, referenceIndex, null, null, projectionParams);
    }


    private static CouponCalculator Create(Dt asOf, ReferenceIndex referenceIndex, CalibratedCurve referenceCurve,
                                           DiscountCurve discount, ProjectionParams projectionParams)
    {
      CouponCalculator couponCalculator;
      ProjectionType type = projectionParams.ProjectionType;
      switch (type)
      {
        case ProjectionType.SimpleProjection:
          couponCalculator = new ForwardRateCalculator(asOf, (InterestRateIndex) referenceIndex,
                                                       (DiscountCurve) referenceCurve)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag
                               };
          break;
        case ProjectionType.SwapRate:
          couponCalculator = new SwapRateCalculator(asOf, (SwapRateIndex) referenceIndex, (DiscountCurve) referenceCurve,
                                                    discount)
                               {
                                 ResetLag = projectionParams.ResetLag
                               };
          break;
        case ProjectionType.InflationForward:
          couponCalculator = new InflationForwardCalculator(asOf, (InflationIndex) referenceIndex,
                                                            (InflationCurve) referenceCurve,
                                                            projectionParams.IndexationMethod)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag
                               };
          break;
        case ProjectionType.InflationRate:
          couponCalculator = new InflationRateCalculator(asOf, (InflationIndex) referenceIndex, projectionParams.YoYRateTenor,
                                                         (InflationCurve) referenceCurve, projectionParams.IndexationMethod)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag,
                                 ZeroCoupon = (projectionParams.ProjectionFlags & ProjectionFlag.ZeroCoupon) != 0
                               };
          break;
        case ProjectionType.ArithmeticAverageRate:
          couponCalculator = new FedFundsRateCalculator(asOf, (InterestRateIndex) referenceIndex,
                                                        (DiscountCurve) referenceCurve)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag,
                                 Approximate =
                                   (projectionParams.ProjectionFlags & ProjectionFlag.ApproximateProjection) != 0
                               };
          break;
        case ProjectionType.CPArithmeticAverageRate:
          couponCalculator = new CpRateCalculator(asOf, (InterestRateIndex) referenceIndex,
                                                  (DiscountCurve) referenceCurve)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag,
                                 Approximate =
                                   (projectionParams.ProjectionFlags & ProjectionFlag.ApproximateProjection) != 0
                               };
          break;
        case ProjectionType.TBillArithmeticAverageRate:
          couponCalculator = new TBillRateCalculator(asOf, (InterestRateIndex) referenceIndex,
                                                     (DiscountCurve) referenceCurve)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag,
                                 Approximate =
                                   (projectionParams.ProjectionFlags & ProjectionFlag.ApproximateProjection) != 0
                               };
          break;
        case ProjectionType.GeometricAverageRate:
          couponCalculator = new OisRateCalculator(asOf, (InterestRateIndex) referenceIndex,
                                                   (DiscountCurve) referenceCurve)
                               {
                                 DiscountCurve = discount,
                                 ResetLag = projectionParams.ResetLag,
                                 Approximate =
                                   (projectionParams.ProjectionFlags & ProjectionFlag.ApproximateProjection) != 0
                               };
          break;
        case ProjectionType.ParYield:
          couponCalculator = new ForwardYieldCalculator(asOf, (ForwardYieldIndex) referenceIndex,
                                                        (DiscountCurve) referenceCurve, discount)
                               {
                                 ResetLag = projectionParams.ResetLag
                               };
          break;
        default:
          throw new ArgumentException("Index type not supported");
      }
      couponCalculator.EndIsFixingDate = (projectionParams.ProjectionFlags & ProjectionFlag.ResetInArrears) != 0;
      return couponCalculator;
    }

    #endregion Constructors

    #region Implementation of IRateProjector

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>Fixing for the period</returns>
    public abstract Fixing Fixing(FixingSchedule fixingSchedule);

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="prevPayDt">Previous payment date</param>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="payDt">Payment date</param>
    /// <returns>Fixing schedule</returns>
    public abstract FixingSchedule GetFixingSchedule(Dt prevPayDt, Dt periodStart, Dt periodEnd, Dt payDt);

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns> Reset info for each component of the fixing</returns>
    public abstract List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule);

    /// <summary>
    /// Historical reset fixings
    /// </summary>
    public RateResets HistoricalObservations
    {
      get
      {
        if (ReferenceIndex == null)
          return null;
        return ReferenceIndex.HistoricalObservations;
      }
    }

    #endregion Implementation of IRateProjector

    #region Properties

    /// <summary>
    /// If provided resets are for dates after AsOf then use them
    /// </summary>
    public bool UseFutureResets { get; set; }

    /// <summary>
    /// AsOf Date
    /// </summary>
    public Dt AsOf { get; set; }

    /// <summary>
    ///   Reference index 
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Reference curve
    /// </summary>
    public CalibratedCurve ReferenceCurve { get; set; }

    /// <summary>
    /// Discount curve (if different than projection the swap rate needs to be computed differently). 
    /// Can also be set externally for compounding the floating rate. 
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// False if the period end coincides with the fixing of the rate. This is the case if the reset lag is the same 
    /// as the natural reset lag of the rate, i.e it equals the number of settlement dates 
    /// </summary>
    internal bool EndIsFixingDate { get; set; }

    /// <summary>
    /// If provided reset matches AsOf then use it
    /// </summary>
    public bool UseAsOfResets { get; set; }

    /// <summary>
    /// Custom reset lag. If empty, the reset lag is driven by the days to settle specified in the reference index, or by the reset day rule specified in the index
    /// </summary>
    public Tenor ResetLag { get; set; }


    /// <summary>
    /// Name of the index
    /// </summary>
    public String IndexName
    {
      get { return ReferenceIndex.IndexName; }
    }

    internal CashflowFlag CashflowFlag
    {
      get { return cfFlags_; }
      set { cfFlags_ = value; }
    }
    private CashflowFlag cfFlags_ = CashflowFlag.SimpleProjection;
    #endregion Properties

    #region Methods

    /// <summary>
    /// Validate the Coupon calculators
    /// </summary>
    /// <param name="errors">Array of errors</param>
    public override void Validate(ArrayList errors)
    {
      if (ReferenceIndex == null)
        InvalidValue.AddError(errors, this, "ReferenceIndex cannot be null");
      if (ReferenceCurve == null)
        InvalidValue.AddError(errors, this, "ReferenceCurve cannot be null");
      base.Validate(errors);
    }

    #endregion
  }
}