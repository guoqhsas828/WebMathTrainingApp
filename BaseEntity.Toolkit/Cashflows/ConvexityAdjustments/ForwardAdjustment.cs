using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Cashflows
{

  #region ForwardAdjustment

  /// <summary>
  /// Abstract base class for the ForwardRateAdjustments
  /// </summary>
  /// <remarks>Each new implementation of ForwardAdjustment should be reflected in the Factory method Get </remarks>
  [Serializable]
  public class ForwardAdjustment : BaseEntityObject, IForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="rateParams">Model parameters for the forward curve</param>
    protected ForwardAdjustment(Dt asOf, DiscountCurve discountCurve, RateModelParameters rateParams)
    {
      AsOf = asOf;
      DiscountCurve = discountCurve;
      RateModelParameters = rateParams;
    }

    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="rateModelParameters">Rate model parameters</param>
    /// <param name="projectionParams">Projection specifications</param>
    /// <returns>IForwardAdjustment</returns>
    public static IForwardAdjustment Get(Dt asOf, DiscountCurve discountCurve, RateModelParameters rateModelParameters,
                                         ProjectionParams projectionParams)
    {
      if (rateModelParameters == null)
        return null;
      ProjectionFlag flags = projectionParams.ProjectionFlags;
      ProjectionType type = projectionParams.ProjectionType;
      IForwardAdjustment forwardAdjustment = Get(asOf, discountCurve, rateModelParameters, flags, type);
      return forwardAdjustment;
    }

    /// <summary>
    /// Since in the above function we are only using the ProjectionType and ProjectionFlags properties of ProjectionParams,
    /// we are creating this version for convenience, and delegating the above function here.
    /// </summary>
    private static IForwardAdjustment Get(Dt asOf, DiscountCurve discountCurve, RateModelParameters rateModelParameters,
                                         ProjectionFlag flags, ProjectionType type)
    {
      IForwardAdjustment forwardAdjustment;
      switch (type)
      {
        case ProjectionType.SimpleProjection:
          if ((flags & ProjectionFlag.ResetInArrears) != 0 || (flags & ProjectionFlag.ResetWithDelay) != 0)
            forwardAdjustment = new ForwardRateAdjustment(asOf, discountCurve, rateModelParameters);
          else if ((flags & ProjectionFlag.MarkedToMarket) != 0)
            forwardAdjustment = new FuturesAdjustment(asOf, discountCurve, rateModelParameters);
          else
            forwardAdjustment = new ForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        case ProjectionType.SwapRate:
          forwardAdjustment = new SwapRateAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        case ProjectionType.ArithmeticAverageRate:
          if ((flags & ProjectionFlag.MarkedToMarket) != 0)
            forwardAdjustment = new ArithmeticAvgFuturesAdjustment(asOf, discountCurve, rateModelParameters);
          else
            forwardAdjustment = new ArithmeticAvgForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        case ProjectionType.CPArithmeticAverageRate:
          if ((flags & ProjectionFlag.MarkedToMarket) != 0)
            forwardAdjustment = new ArithmeticAvgCpFuturesAdjustment(asOf, discountCurve, rateModelParameters);
          else
            forwardAdjustment = new ArithmeticAvgCpForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        case ProjectionType.TBillArithmeticAverageRate:
          if ((flags & ProjectionFlag.MarkedToMarket) != 0)
            forwardAdjustment = new ArithmeticAvgTBillFuturesAdjustment(asOf, discountCurve, rateModelParameters);
          else
            forwardAdjustment = new ArithmeticAvgTBillForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        case ProjectionType.GeometricAverageRate:
          if ((flags & ProjectionFlag.MarkedToMarket) != 0)
            forwardAdjustment = new GeometricAvgFuturesAdjustment(asOf, discountCurve, rateModelParameters);
          else
            forwardAdjustment = new ForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        case ProjectionType.InflationRate:
          if ((flags & ProjectionFlag.ZeroCoupon) == 0)
            forwardAdjustment = new YearOnYearRateAdjustment(asOf, discountCurve, rateModelParameters);
          else
            forwardAdjustment = new ForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
        default:
          forwardAdjustment = new ForwardAdjustment(asOf, discountCurve, rateModelParameters);
          break;
      }
      return forwardAdjustment;
    }

    /// <summary>
    /// The Rate Model Parameters are needed to compute the convexity adjustment to the computed projected forward/swap rate.
    /// When the type of the forward adjustment object is ForwardAdjustment, they are in fact not used, and 0 is returned
    /// as the convexity adjustment.
    /// </summary>
    /// <param name="flag">The ProjectionFlags property from the ProjectionParams</param>
    /// <param name="type">The ProjectionType property from the ProjectionParams</param>
    public static bool NeedsRateModelParameters(ProjectionFlag flag, ProjectionType type)
    {
      // We re-use the above function with dummy parameters to identify the specific type of ForwardAdjustment object
      // that will be used (to avoid the logic replication).
      IForwardAdjustment adj = Get(Dt.Today(), null, null, flag, type);
      if (adj!= null && adj.GetType() == typeof(ForwardAdjustment))
        return false;
      return true;
    }

    #endregion

    #region Properties

    /// <summary>
    /// As of date 
    /// </summary>
    public Dt AsOf { get; internal set; }

    /// <summary>
    /// Accessor for parameters of the projection rate process
    /// </summary>
    public RateModelParameters RateModelParameters { get; protected internal set; }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; protected set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Convexity adjustment
    /// </summary>
    /// <param name="payDt">Payment date</param>
    /// <param name="fixingSchedule">Schedule for determination of the fixing</param>
    /// <param name="fixing">Fixing information</param>
    /// <returns>Convexity adjustment</returns>
    public virtual double ConvexityAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing)
    {
      return 0.0;
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
    public virtual double FloorValue(FixingSchedule fixingSchedule, Fixing fixing, double floor, double coupon,
                                     double cvxyAdj)
    {
      return RateModelParameters.Option(RateModelParameters.Process.Projection, AsOf, OptionType.Put, fixing.Forward,
                                        cvxyAdj, floor - coupon,
                                        fixingSchedule.ResetDate, fixingSchedule.ResetDate);
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
    public virtual double CapValue(FixingSchedule fixingSchedule, Fixing fixing, double cap, double coupon,
                                   double cvxyAdj)
    {
      return
        -RateModelParameters.Option(RateModelParameters.Process.Projection, AsOf, OptionType.Call, fixing.Forward,
                                    cvxyAdj, cap - coupon,
                                    fixingSchedule.ResetDate, fixingSchedule.ResetDate);
    }

    /// <summary>
    /// Convexity adjustment due to delay / pay-reset mismatch for a (possibly lagged) process which is a martingale under the payDt - forward measure 
    /// </summary>
    /// <param name="payDt">Payment date </param>
    /// <param name="fixingSchedule">Fixing schedule</param>
    /// <param name="fixing">Process fixing</param>
    /// <param name="lag">Non empty if we model the lagged process (i.e in inflation, we ignore 3m reset lag)</param>
    /// <returns>Convexity adjustment</returns>
    public double DelayAdjustment(Dt payDt, FixingSchedule fixingSchedule, Fixing fixing, Tenor lag)
    {
      Dt effective = fixingSchedule.ResetDate;
      if (!lag.IsEmpty)
        effective = Dt.Add(effective, lag);
      if (Diff(effective, payDt) <= 7.0)
        return 0.0;
      return Tadjustment(AsOf, effective, effective, payDt, fixing.Forward, DiscountCurve, RateModelParameters);
    }

    #endregion

    #region Utils

    private static double Diff(Dt dt0, Dt dt1)
    {
      if (dt1 > dt0)
        return Dt.FractDiff(dt0, dt1);
      return -Dt.FractDiff(dt1, dt0);
    }

    /// <summary>
    /// Convexity adjustment under the payDt forward measure for a 
    /// process whose natural martingale measure is the fixingEnd forward measure
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fixingStart">Fixing start date</param>
    /// <param name="fixingEnd">Fixing end date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="f">fixing</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="rateModelParameters">Model parameters</param>
    /// <returns>Convexity adjustment</returns>
    /// <remarks>The family of forward measures are relative to the numeraire associated to the funding curve.</remarks>
    internal static double Tadjustment(Dt asOf, Dt fixingStart, Dt fixingEnd, Dt payDt, double f, DiscountCurve discountCurve, RateModelParameters rateModelParameters)
    {
      if (fixingStart <= asOf)
        return 0.0;
      double days = Diff(payDt, fixingEnd);
      if (Math.Abs(days) < 7)
        return 0.0;
      if (rateModelParameters.Count == 1)
      {
        Tenor fundingTenor = rateModelParameters.Tenor(RateModelParameters.Process.Projection);
        if (fundingTenor.IsEmpty)
          throw new ArgumentException("Funding rate tenor must be specified in RateModelParameters");
        double n = days/fundingTenor.Days;
        double vfw =
          rateModelParameters.SecondMoment(RateModelParameters.Process.Projection, asOf, f, fixingStart, fixingStart) -
          f*f;
        double factor = 1.0 + fundingTenor.Years*f;
        return n/factor*fundingTenor.Years*vfw;
      }
      else
      {
        Tenor fundingTenor = rateModelParameters.Tenor(RateModelParameters.Process.Funding);
        if (fundingTenor.IsEmpty)
          throw new ArgumentException("Funding rate tenor must be specified in RateModelParameters");
        double n = days/fundingTenor.Days;
        Curve correlation = rateModelParameters.Correlation;
        double l = discountCurve.F(fixingStart, Dt.Add(fixingStart, fundingTenor));
        double vfw = rateModelParameters.SecondMoment(RateModelParameters.Process.Projection, asOf, f, fixingStart, fixingStart) - f*f;
        double vfn = rateModelParameters.SecondMoment(RateModelParameters.Process.Funding, asOf, l, fixingStart, fixingStart) - l * l;
        double factor = (1.0 + fundingTenor.Years*l);
        double rho = (correlation == null) ? 1.0 : correlation.Interpolate(0);
        return n/factor*fundingTenor.Years*rho*Math.Sqrt(vfw*vfn);
      }
    }

    /// <summary>
    /// Convexity adjustment under the risk neutral measure for a process whose natural martingale measure is the
    /// fixingEnd forward measure 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fixingStart">Fixing start date</param>
    /// <param name="fixingEnd">Fixing end date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="f">Fixing </param>
    /// <param name="discCurve">Funding curve</param>
    /// <param name="rateModelParameters">Model parameters</param>
    /// <returns>Convexity adjustment</returns>
    /// <remarks>The family of forward measures are relative to the numeraire associated to the funding curve.</remarks>
    internal static double Qadjustment(Dt asOf, Dt fixingStart, Dt fixingEnd, Dt payDt, double f, DiscountCurve discCurve, RateModelParameters rateModelParameters)
    {
      if (fixingStart <= asOf)
        return 0.0;
      double days = Dt.Diff(asOf, fixingEnd);
      if (rateModelParameters.Count == 1)
      {
        Tenor fundingTenor = rateModelParameters.Tenor(RateModelParameters.Process.Projection);
        if (fundingTenor.IsEmpty)
          throw new ArgumentException("Funding rate tenor must be specified in RateModelParameters");
        double n = days/fundingTenor.Days;
        double vfw =
          rateModelParameters.SecondMoment(RateModelParameters.Process.Projection, asOf, f, fixingStart, fixingStart) -
          f*f;
        double factor = 1.0 + fundingTenor.Years*f;
        return n/factor*fundingTenor.Years*vfw;
      }
      else
      {
        RateModelParameters.Process funding = (rateModelParameters.Count == 1)
                                                ? RateModelParameters.Process.Projection
                                                : RateModelParameters.Process.Funding;
        Tenor fundingTenor = rateModelParameters.Tenor(funding);
        if (fundingTenor.IsEmpty)
          throw new ArgumentException("Funding rate tenor must be specified in RateModelParameters");
        Curve correlation = rateModelParameters.Correlation;
        double n = days/fundingTenor.Days;
        double l = discCurve.F(fixingStart, Dt.Add(fixingStart, fundingTenor));
        double vfw = rateModelParameters.SecondMoment(RateModelParameters.Process.Projection, asOf, f, fixingStart, fixingStart) - f * f;
        double vfn = rateModelParameters.SecondMoment(funding, asOf, l, fixingStart, fixingStart) - l*l;
        double rho = (correlation == null) ? 1.0 : correlation.Interpolate(Math.Abs(0));
        return n*fundingTenor.Years/(1 + l*fundingTenor.Years)*rho*Math.Sqrt(vfw*vfn);
      }
    }


    /// <summary>
    /// Convexity adjustment under the payDt forward measure for a function, F, of a
    /// process whose natural martingale measure is the fixingEnd forward measure
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fixingStart">Fixing start date</param>
    /// <param name="fixingEnd">Fixing end date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="f">fixing</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="rateModelParameters">Model parameters</param>
    /// <param name="derivative"><m>\frac{d}{dx}F</m> </param>
    /// <returns>Convexity adjustment</returns>
    /// <remarks>The family of forward measures are relative to the numeraire associated to the funding curve.</remarks>
    protected static double Tadjustment(Dt asOf, Dt fixingStart, Dt fixingEnd, Dt payDt, double f, DiscountCurve discountCurve, RateModelParameters rateModelParameters,
                                                 MapDerivatives derivative)
    {
      double dh = Tadjustment(asOf, fixingStart, fixingEnd, payDt, f, discountCurve, rateModelParameters);
      if (derivative == null)
        return dh;
      return derivative(f, fixingStart, fixingEnd)*dh;
    }

    /// <summary>
    /// Convexity adjustment under the risk neutral measure for a function, F, of a
    /// process whose natural martingale measure is the fixingEnd forward measure
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fixingStart">Fixing start date</param>
    /// <param name="fixingEnd">Fixing end date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="f">fixing</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="rateModelParameters">Model parameters</param>
    /// <param name="derivative"><m>\frac{d}{dx}F</m> </param>
    /// <returns>Convexity adjustment</returns>
    /// <remarks>The family of forward measures are relative to the numeraire associated to the funding curve.</remarks>
    protected static double Qadjustment(Dt asOf, Dt fixingStart, Dt fixingEnd, Dt payDt, double f,
                                                 DiscountCurve discountCurve, RateModelParameters rateModelParameters,
                                                 MapDerivatives derivative)
    {
      double dh = Qadjustment(asOf, fixingStart, fixingEnd, payDt, f, discountCurve, rateModelParameters);
      if (derivative == null)
        return dh;
      return derivative(f, fixingStart, fixingEnd)*dh;
    }

    /// <summary>
    /// First and second derivatives of underlying wrt libor rate L_0(start, end) 
    /// </summary>
    /// <param name="liborRate">L_0(start,end)</param>
    /// <param name="start">deposit start</param>
    /// <param name="end">deposit end</param>
    /// <returns><m>\frac{d}{dL}F(L)</m></returns>
    protected delegate double MapDerivatives(double liborRate, Dt start, Dt end);

    #endregion
  }

  #endregion

  #region IHasForwardAdjustment

  internal interface IHasForwardAdjustment
  {
    IForwardAdjustment ForwardAdjustment { get; set; }
  }

  #endregion
}