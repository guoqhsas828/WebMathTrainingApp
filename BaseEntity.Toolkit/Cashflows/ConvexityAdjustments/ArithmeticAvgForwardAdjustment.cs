using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Arithmetic avg convexity adj
  /// </summary>
  [Serializable]
  public class ArithmeticAvgForwardAdjustment : ForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ArithmeticAvgForwardAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
      base(asOf, discountCurve, fwdModelParams)
    {}

    #endregion

    #region Methods

    /// <summary>
    /// Convexity adjustment under the payDt forward measure for the arithmetic average of <m>F(X_{t_i}(U_i))\,\,\,i=1,\dots,N</m>
    /// where <m>X_t(U)</m>is a martingale under the U forward measure
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Fixing schedule</param>
    /// <param name="fixing">Recorded fixings</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="rateModelParameters">Model parameters</param>
    /// <param name="derivatives"><m>\frac{d}{dx}F</m> and <m>\frac{d^2}{dx^2}F</m></param>
    /// <returns>Convexity adjustment</returns>
    /// <remarks>The family of forward measures are relative to the numeraire associated to the funding curve.</remarks>
    private static double TavgAdjustment(Dt asOf, Dt payDt, FixingSchedule fixingSchedule, Fixing fixing,
                                                         DiscountCurve discountCurve,
                                                         RateModelParameters rateModelParameters,
                                                         MapDerivatives derivatives)
    {
      var sched = (AverageRateFixingSchedule)fixingSchedule;
      var averagedRateFixing = (AveragedRateFixing)fixing;
      if (averagedRateFixing.RateResetState != RateResetState.IsProjected)
        return 0.0;
      int start = averagedRateFixing.ResetStates.IndexOf(RateResetState.IsProjected);
      if (start < 0)
        return 0.0;
      int end = averagedRateFixing.ResetStates.Count - 1;
      double f0 = Tadjustment(asOf, sched.StartDates[start], sched.EndDates[start], payDt,
                              averagedRateFixing.Forward, discountCurve, rateModelParameters, derivatives);
      double f1 = Tadjustment(asOf, sched.StartDates[end], sched.EndDates[end], payDt,
                              averagedRateFixing.Forward, discountCurve, rateModelParameters, derivatives);
      return 0.5 * (f0 + f1) * Dt.FractDiff(sched.StartDates[start], sched.EndDates[end]) /
             Dt.FractDiff(sched.StartDates[0], sched.EndDates[end]); //simple trapezoid rule
    }

    /// <summary>
    /// Derivative of target rate as function of libor rate  
    /// </summary>
    /// <param name="liborRate">Libor rate</param>
    /// <param name="start">Period start</param>
    /// <param name="end">Period end</param>
    /// <returns><m>\frac{d}{dL}F(L)</m></returns>
    protected virtual double RateFnDerivative(double liborRate, Dt start, Dt end)
    {
      return 1.0;
    }


    /// <summary>
    /// Convexity adjustment due to delay in average rate
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>Convexity adjustment</returns>
    public override double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
        return RateModelParameters.Interpolate(payDt, fixing.Forward, RateModelParameters.Param.Custom, RateModelParameters.Process.Projection);
      return TavgAdjustment(AsOf, payDt, fixingSchedule, fixing, DiscountCurve, RateModelParameters,
                                          RateFnDerivative);
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
      var sched = (AverageRateFixingSchedule)fixingSchedule;
      var fix = (AveragedRateFixing)fixing;
      return
        -RateModelParameters.OptionOnAverage(RateModelParameters.Process.Projection, AsOf, OptionType.Call, fix.Forward,
                                             sched.Weights, fix.Components, cvxyAdj, cap - coupon, sched.ResetDate,
                                             sched.ResetDates, ForwardModel.AverageType.Arithmetic);
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
    public override double FloorValue(FixingSchedule fixingSchedule, Fixing fixing, double floor, double coupon,
                                      double cvxyAdj)
    {
      var sched = (AverageRateFixingSchedule)fixingSchedule;
      var fix = (AveragedRateFixing)fixing;
      return RateModelParameters.OptionOnAverage(RateModelParameters.Process.Projection, AsOf, OptionType.Put,
                                                 fix.Forward,
                                                 sched.Weights, fix.Components, cvxyAdj, floor - coupon, sched.ResetDate,
                                                 sched.ResetDates, ForwardModel.AverageType.Arithmetic);
    }

    #endregion
  }


  /// <summary>
  /// Cp forward convexity adjustment
  /// </summary>
  [Serializable]
  public class ArithmeticAvgCpForwardAdjustment : ArithmeticAvgForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ArithmeticAvgCpForwardAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
      base(asOf, discountCurve, fwdModelParams)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Derivative of target rate as function of libor rate  
    /// </summary>
    /// <param name="liborRate">Libor rate</param>
    /// <param name="start">Period start</param>
    /// <param name="end">Period end</param>
    /// <returns><m>\frac{d}{dL}F(L)</m></returns>
    protected override double RateFnDerivative(double liborRate, Dt start, Dt end)
    {
      double a = (1.0 - liborRate/12.0);
      return 1.0/(a*a);
    }

    #endregion
  }


  /// <summary>
  /// Cp forward convexity adjustment
  /// </summary>
  [Serializable]
  public class ArithmeticAvgTBillForwardAdjustment : ArithmeticAvgForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ArithmeticAvgTBillForwardAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams)
      :
        base(asOf, discountCurve, fwdModelParams)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Derivative of target rate as function of libor rate  
    /// </summary>
    /// <param name="liborRate">Libor rate</param>
    /// <param name="start">Period start</param>
    /// <param name="end">Period end</param>
    /// <returns><m>\frac{d}{dL}F(L)</m></returns>
    protected override double RateFnDerivative(double liborRate, Dt start, Dt end)
    {
      double dt = Dt.Diff(start, end);
      double a = (liborRate*dt - 360);
      return 131400.0/(a*a);
    }

    #endregion
  }
}