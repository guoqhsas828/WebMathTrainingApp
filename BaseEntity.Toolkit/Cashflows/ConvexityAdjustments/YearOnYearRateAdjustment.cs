using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// The pricer assumes that the forward libor family and 
  /// forward inflation families are driven by correlated diffusion. 
  /// (Lagged)Forward inflation prices <m>I(T)</m> and forward Libors, <m>L^{\Delta}(T-\Delta)</m> are martingales under the T forward measure with correlation <m>\varphi(T)</m>.
  /// We assume that the inflation family I(T) is a one factor family, i.e there is perfect correlation among the forward inflation prices. 
  /// </summary>
  /// <remarks>The tenor of the libor rate <m>\Delta</m> should equal the tenor of the year on year rate</remarks>
  [Serializable]
  public class YearOnYearRateAdjustment : ForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="fwdModelParams">Forward inflation price parameters</param>
    public YearOnYearRateAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
      base(asOf, discountCurve, fwdModelParams)
    {
      if (fwdModelParams.Count == 1)
        throw new ToolkitException("A 2 dimensional model is required to compute convexity adjustments for YoY rate");
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Convexity adjustment due time lag in year on year rate
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>The convexity adjustment for the forward reference inflation rate <m>R_0^\Delta(d_1) = \frac{1}{\Delta}\left(\frac{I_0(d_1)}{I_0(d_0)}-1\right),</m>
    /// where <m>d_0</m> and <m>d_1</m> are two successive coupon payment dates</returns>
    ///<remarks> The convexity adjustment arises from the fact that the forward inflation rates are not martingales under the 
    /// forward measure associated to the payment times, thus the asOf forward inflation rate is not equal to the expected 
    /// future inflation rate.</remarks>
    public override double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
        return RateModelParameters.Interpolate(payDt, fixing.Forward, RateModelParameters.Param.Custom, RateModelParameters.Process.Projection);
      var sched = (InflationRateFixingSchedule)fixingSchedule;
      var yoyFixing = (InflationRateFixing)fixing;
      if (yoyFixing.DenominatorResetState != RateResetState.IsProjected)
        return 0.0;
      //we ignore convexity due to indexation lag
      double i0 = yoyFixing.InflationAtPreviousPayDt;
      double i1 = yoyFixing.InflationAtPayDt;
      double delta = sched.Frac;
      double mu0 = Tadjustment(AsOf, sched.StartDate, sched.StartDate, payDt, i0, DiscountCurve, RateModelParameters);
      double vI0 =
        RateModelParameters.SecondMoment(RateModelParameters.Process.Projection, AsOf, i0, sched.StartDate,
                                         sched.StartDate) - i0 * i0;
      //approx by variance under its natural martingale measure
      double vI1 =
        RateModelParameters.SecondMoment(RateModelParameters.Process.Projection, AsOf, i1, sched.StartDate,
                                         sched.EndDate) - i1 * i1;
      double ca = 1.0 / delta * i1 / i0 * (-mu0 / i0 + vI0 / (i0 * i0) - Math.Sqrt(vI0 * vI1) / (i0 * i1));
      //second order taylor expansion around forward YoY
      return ca;
    }

    /// <summary>
    /// Value of the floor embedded in the payment (if applicable)
    /// </summary>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <param name="floor">Floor on fixing</param>
    /// <param name="coupon">Spread over floating rate</param>
    /// <param name="cvxyAdj">Convexity adjustment</param>
    /// <returns>Floor value</returns>
    public override double FloorValue(FixingSchedule fixingSchedule, Fixing fixing, double floor,
                                      double coupon, double cvxyAdj)
    {
      var sched = (InflationRateFixingSchedule)fixingSchedule;
      var yoyFixing = (InflationRateFixing)fixing;
      double i0 = yoyFixing.InflationAtPreviousPayDt;
      double i1 = yoyFixing.InflationAtPayDt;
      double delta = sched.Frac;
      double f = (i1 / i0) / delta;
      double strike = floor + 1.0 / delta - coupon;
      return RateModelParameters.OptionOnRatio(RateModelParameters.Process.Projection, AsOf, OptionType.Put, f, i1, i0,
                                               cvxyAdj, strike, sched.EndDate,
                                               sched.EndDate, sched.StartDate);
    }


    /// <summary>
    /// Value of the cap embedded in the payment (if applicable)
    /// </summary>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <param name="cap">Cap on fixing</param>
    /// <param name="coupon">Spread over floating rate</param>
    /// <param name="cvxyAdj">Convexity adjustment</param>
    /// <returns>Floor value</returns>
    public override double CapValue(FixingSchedule fixingSchedule, Fixing fixing, double cap, double coupon,
                                    double cvxyAdj)
    {
      var sched = (InflationRateFixingSchedule)fixingSchedule;
      var yoyFixing = (InflationRateFixing)fixing;
      double i0 = yoyFixing.InflationAtPreviousPayDt;
      double i1 = yoyFixing.InflationAtPayDt;
      double delta = sched.Frac;
      double f = (i1 / i0) / delta;
      double strike = cap + 1.0 / delta - coupon;
      return
        -RateModelParameters.OptionOnRatio(RateModelParameters.Process.Projection, AsOf, OptionType.Call, f, i1, i0,
                                           cvxyAdj, strike, sched.EndDate,
                                           sched.EndDate, sched.StartDate);
    }

    #endregion Methods
  }
}