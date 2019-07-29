using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using RateProcess = BaseEntity.Toolkit.Models.RateModelParameters.Process;
using RateParam = BaseEntity.Toolkit.Models.RateModelParameters.Param;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Ed futures forward rate adjustment
  /// </summary>
  [Serializable]
  public sealed class FuturesAdjustment : ForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="fwdModelParams"> Parameters of the forward rate/price model</param>
    public FuturesAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters fwdModelParams) :
      base(asOf, discountCurve, fwdModelParams)
    {
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fast internal function for computation of the ED convexity adjustment 
    ///  (as applied to the future rate) in the hull framework 
    /// </summary>
    /// <param name="rate">Forward</param>
    /// <param name="asOf">As of date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="depositMaturity">deposity maturity</param>
    /// <param name="pars">Model parameters</param>
    /// <returns>The eurodollar convexity adjustment in the hull framework</returns>
    internal static double HullEdFutureConvexityAdjustment(
      double rate, Dt asOf, Dt maturity, Dt depositMaturity,
      RateModelParameters pars)
    {
      if (pars == null)
        return 0;
      double mat = Dt.Years(asOf, maturity, DayCount.Actual365Fixed);
      double term = Dt.Years(maturity, depositMaturity, DayCount.Actual365Fixed);
      const RateProcess process = RateProcess.Projection;
      double sigma = pars.Interpolate(maturity, rate, RateParam.Sigma, process);

      IModelParameter ip;
      const RateParam meanReversion = RateParam.MeanReversion;
      if (pars.TryGetValue(process, meanReversion, out ip))
      {
        double speed = pars.Interpolate(maturity, rate, meanReversion,process);
        return ConvexityAdjustments.EDFutures(rate, mat, term, sigma, speed);
      }

      return ConvexityAdjustments.EDFutures(rate, mat, term, sigma, FuturesCAMethod.Hull);
    }


    /// <summary>
    /// Convexity adjustment applied to the forward rate due to marking to market
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>Convexity adjustment</returns>
    public override double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
        return RateModelParameters.Interpolate(payDt, fixing.Forward, RateModelParameters.Param.Custom,
                                               RateModelParameters.Process.Projection);
      var sched = (ForwardRateFixingSchedule)fixingSchedule;
      if (RateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Hull)
      {
        // HullEdFutureConvexityAdjustment calculates the CA as applied to the future.
        // Take negative to apply it to the forwards instead.
        return -HullEdFutureConvexityAdjustment(fixing.Forward, AsOf, sched.StartDate, sched.EndDate, RateModelParameters);
      }
      return Qadjustment(AsOf, sched.StartDate, sched.EndDate, payDt, fixing.Forward, DiscountCurve,
                         RateModelParameters);
    }

    #endregion Methods
  }
}