using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Pricer for swaps on libor (regular or in arrears)
  /// 
  /// The parameter curves are <m>(\beta(T), \alpha(T), \rho(T), \nu(T)) </m>, where <m> \beta(T) </m> is the process exponent, <m>\alpha(T) </m> is the initial volatility value, <m>\rho(T)</m> is the correlation parameter 
  ///  and <m>\nu(T)</m> is the stochastic volatility diffusion coefficient.
  /// </summary>
  [Serializable]
  public class ForwardRateAdjustment : ForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate model</param>
    public ForwardRateAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
      base(asOf, discountCurve, fwdModelParams)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Convexity adjustment due to delay in libor rate
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>Convexity adjustment</returns>
    /// <remarks>
    /// Typically the reference rate fixing occurs at the beginning of the accrual period and is payed at its end. If this is the case there is no convexity adjustment, i.e. 
    /// <m>\mathbb{E}^{T} (L^{\delta}_{T}(T)) = L^{\delta}_0(T).</m>
    /// If the rate fixing occurs at payment time, however, there is a discrepancy between expected future libor rate and forward libor rate(under the convenient pricing measure). 
    /// The convexity adjustment is then defined as:<math>
    ///   CA(T_f) = \mathbb{E}^{T} (L^{\delta}_{T}(T)) - L^{\delta}_0(T),
    /// </math> where <m>T</m> is the payment date, <m>\delta</m> is the reset lag.
    /// The approximate formula for this is given by:<math>
    ///  CA(T) \approx  \frac{1}{1 + \delta L^{\delta}_0(T)} \mathbb{E}^{T + \delta}L^{\delta}_{T}(T)^2
    /// </math> where  <m>\mathbb{E}^{T}</m> is the expectation under the forward measure associated to maturity <m>T</m> </remarks>
    public override double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
        return RateModelParameters.Interpolate(payDt, fixing.Forward, RateModelParameters.Param.Custom, RateModelParameters.Process.Projection);
      var sched =  (ForwardRateFixingSchedule)fixingSchedule;
      return Tadjustment(AsOf, sched.StartDate, sched.EndDate, payDt, fixing.Forward, DiscountCurve,
                         RateModelParameters);
    }

    #endregion Methods
  }
}