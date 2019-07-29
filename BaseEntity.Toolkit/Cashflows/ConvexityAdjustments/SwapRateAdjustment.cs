using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Convexity adjustment for CMS swaps
  /// </summary>
  [Serializable]
  public class SwapRateAdjustment : ForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="rateModelParams">Model parameters</param>
    public SwapRateAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters rateModelParams) :
      base(asOf, discountCurve, rateModelParams)
    {
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Computes the convexity adjustment to be applied to the forward swap rate corresponding to reset date. 
    /// This method depends on both product specifications and model used and should be overloaded by all swapleg pricers.
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>The convexity adjustment for the swap rate at the reset date</returns>
    /// <remarks>The convexity adjustment is defined as:<math>
    ///   CA(T_f) = \mathbb{E}^{T_f + \delta} (S^{\Delta}_{T_f}(T_f)) - S^{\Delta}_0(T_f),
    /// </math> where <m>T_f</m> is the fixing date, <m>\delta</m> is the reset lag and <m> \Delta</m> is the swap rate tenor
    /// The approximate formula for this is given by:<math> \\
    ///  CA(T_f) \approx S^{\Delta}_0(T_f)\theta(\delta, \tau, \Delta)\Big(\mathbb{E}^{T_f,\Delta} \frac{S^{\Delta}_{T_f}(T_f)^2}{S^{\Delta}_{0}(T_f)^2} - 1 \Big)
    /// </math> where <math>
    ///  \theta(\delta, \tau, \Delta) = 1 - \frac{\tau S^{\Delta}_0(T_f)}{1 + \tau S^{\Delta}_0(T_f)}\Big(\frac{\delta}{\tau} + \frac{n}{(1 + \tau S^{\Delta}_0(T_f))^{n}-1}\Big)
    /// </math>
    /// <m>\tau </m> is the coupon payment tenor associated to the par swap rate and
    /// <m>\mathbb{E}^{T_f,\Delta}</m> is the expectation under the swap measure associated to <m>S^{\Delta}_{T_f}(T_f)</m>
    /// </remarks>
    public override double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      if (fixingSchedule.ResetDate <= AsOf)
        return 0.0;
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
        return RateModelParameters.Interpolate(payDt, fixing.Forward, RateModelParameters.Param.Custom,
                                               RateModelParameters.Process.Projection);
      var swapFixingSchedule = (SwapRateFixingSchedule)fixingSchedule;
      var schedule = swapFixingSchedule.FixedLegSchedule;
      double f = fixing.Forward;
      double delta = Dt.FractDiff(swapFixingSchedule.ResetDate, payDt)/365.0;
      double n = schedule.Count;
      double rateTen = Dt.FractDiff(schedule.GetPeriodStart(0), schedule.GetPeriodEnd(schedule.Count - 1))/365.0;
      double tau = rateTen/n; //avg fraction
      double taufrw0 = tau*f;
      double constant = 1 - taufrw0/(1 + taufrw0)*(delta/tau + n/(Math.Pow(1 + taufrw0, n) - 1.0));
      double var = RateModelParameters.SecondMoment(RateModelParameters.Process.Projection, AsOf, f,
                                                    swapFixingSchedule.ResetDate,
                                                    swapFixingSchedule.ResetDate);
      return (f > 0) ? f*constant*(var/(f*f) - 1.0) : 0.0;
    }

    #endregion
  }
}