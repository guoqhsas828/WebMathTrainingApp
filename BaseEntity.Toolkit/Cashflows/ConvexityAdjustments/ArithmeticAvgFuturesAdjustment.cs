using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Convexity adjustment
  /// </summary>
  [Serializable]
  public class ArithmeticAvgFuturesAdjustment : ForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ArithmeticAvgFuturesAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
      base(asOf, discountCurve, fwdModelParams)
    {
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Convexity adj under the risk neutral measure for the arithmetic average of <m>F(X_{t_i}(U_i)),\,\,i=1,\dots,N</m> where $X_t(U)$
    /// is a martingale under the U forward measure
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
    private static double QAvgAdjustment(Dt asOf, Dt payDt, FixingSchedule fixingSchedule, Fixing fixing,
                                           DiscountCurve discountCurve,
                                           RateModelParameters rateModelParameters,
                                           MapDerivatives derivatives)
    {
      var sched = (AverageRateFixingSchedule)fixingSchedule;
      var averagedRateFixing = (AveragedRateFixing)fixing;
      int start = averagedRateFixing.ResetStates.IndexOf(RateResetState.IsProjected);
      if (start < 0)
        return 0.0;
      int end = averagedRateFixing.ResetStates.Count - 1;
      double f0 = Qadjustment(asOf, sched.StartDates[start], sched.EndDates[start], payDt,
                              averagedRateFixing.Forward, discountCurve, rateModelParameters, derivatives);
      double f1 = Qadjustment(asOf, sched.StartDates[end], sched.EndDates[end], payDt,
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
    /// Convexity adjustment due to marking to market in average rate
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>Convexity adjustment</returns>
    public override double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
        return RateModelParameters.Interpolate(payDt, fixing.Forward, RateModelParameters.Param.Custom, RateModelParameters.Process.Projection);
      return QAvgAdjustment(AsOf, payDt, fixingSchedule, fixing, DiscountCurve, RateModelParameters,
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

    #endregion Methods
  }

  /// <summary>
  /// Cp forward convexity adjustment
  /// </summary>
  [Serializable]
  public class ArithmeticAvgCpFuturesAdjustment : ArithmeticAvgFuturesAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ArithmeticAvgCpFuturesAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
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
  public class ArithmeticAvgTBillFuturesAdjustment : ArithmeticAvgFuturesAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ArithmeticAvgTBillFuturesAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams)
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