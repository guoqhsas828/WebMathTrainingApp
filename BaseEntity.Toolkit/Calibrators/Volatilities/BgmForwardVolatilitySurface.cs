/*
 * BgmForwardVolatilitySurface.cs
 *
 *  -2010. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BgmCorrelation = BaseEntity.Toolkit.Models.BGM.BgmCorrelation;
using BgmCalibrationMethod = BaseEntity.Toolkit.Base.VolatilityBootstrapMethod;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///  Forward Volatility Surface calibrated by BGM
  /// </summary>
  [Serializable]
  public class BgmForwardVolatilitySurface : CalibratedVolatilitySurface, IVolatilityCalculatorProvider, IModelParameter
  {
    private BgmForwardVolatilitySurface(Dt asOf,
      PlainVolatilityTenor[] tenors,
      IVolatilitySurfaceCalibrator calibrator,
      IVolatilitySurfaceInterpolator interpolator)
      : base(asOf, tenors, calibrator, interpolator)
    { }

    /// <summary>
    /// Creates the specified calibration method.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="parameters">The calibration parameters.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="cycleRule">The cycle rule.</param>
    /// <param name="businessDayConvention">The business day convention.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="correlations">The correlations.</param>
    /// <param name="swpnVolatilities">The SWPN volatilities.</param>
    /// <param name="volatilityModel">The volatility model.</param>
    /// <returns></returns>
    public static BgmForwardVolatilitySurface Create(
      Dt asOf,
      BgmCalibrationParameters parameters,
      DiscountCurve discountCurve,
      string[] expiries,
      string[] tenors,
      CycleRule cycleRule,
      BDConvention businessDayConvention,
      Calendar calendar,
      BgmCorrelation correlations,
      double[,] swpnVolatilities,
      DistributionType volatilityModel)
    {
      var calibrator = new BgmCalibrator(
        parameters.CalibrationMethod,
        parameters.Tolerance, parameters.ShapeControl,
        discountCurve, correlations, volatilityModel);
      var interp = new BgmInterpolator();
      var bs = new BgmForwardVolatilitySurface(asOf, null, calibrator, interp)
      {
        expiries_ = expiries,
        tenors_ = tenors,
        swpnVolatilities_ = swpnVolatilities,
        cycleRule_ = cycleRule,
        bdc_ = businessDayConvention,
        cal_ = calendar
      };
      bs.Fit();
      return bs;
    }

    /// <summary>
    /// Wraps bgm forward volatilities.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="tenors">The tenors.</param>
    /// <param name="cycleRule">The cycle rule.</param>
    /// <param name="businessDayConvention">The business day convention.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="correlations">The correlations.</param>
    /// <param name="fwdVolatilityCurves">The FWD volatility curves.</param>
    /// <param name="volatilityModel">The volatility model.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static BgmForwardVolatilitySurface Wrap(
      Dt asOf,
      DiscountCurve discountCurve,
      string[] tenors,
      CycleRule cycleRule,
      BDConvention businessDayConvention,
      Calendar calendar,
      BgmCorrelation correlations,
      VolatilityCurve[] fwdVolatilityCurves,
      DistributionType volatilityModel)
    {
      var calibrator = new BgmForwardCalibrator(
        Array.ConvertAll(tenors, s => Dt.Roll(
          Dt.Add(asOf, s), businessDayConvention, calendar)),
        discountCurve, correlations,
        fwdVolatilityCurves, volatilityModel);
      var interp = new BgmInterpolator();
      var bs = new BgmForwardVolatilitySurface(asOf, null, calibrator, interp)
        {
          expiries_ = tenors,
          tenors_ = tenors,
          swpnVolatilities_ = null,
          cycleRule_ = cycleRule,
          bdc_ = businessDayConvention,
          cal_ = calendar
        };
      bs.Fit();
      return bs;
    }

    /// <summary>
    /// Gets the forward volatilities.
    /// </summary>
    /// <returns>System.Double[][].</returns>
    /// <remarks>The value at index (i, j) is <m>\sigma_i(t)</m> for <m>t \in [T_{j-1},T_j]</m> with <m>T_{-1} =</m> asOf date,
    ///  where i is the index of the foirward rate.  The dates are based <c>CalibratedVolatilities.ResetDates</c>. </remarks>
    public double[,] GetForwardVolatilities()
    {
      return CalibratedVolatilities.GetForwardVolatilities();
    }

    /// <summary>
    /// Gets the forward swaption volatility interpolator.
    /// </summary>
    /// <param name="expiry">The expiry date of the swaption.</param>
    /// <param name="maturity">The maturity date of the swaption.</param>
    /// <returns></returns>
    public IForwardVolatilityInterpolator GetSwaptionVolatilityInterpolator(
      Dt expiry, Dt maturity)
    {
      // Dumy index and dumy product.
      //TODO: revisit this later.
      var fixedLeg = new SwapLeg(expiry, maturity, Currency.None, 1.0,
                                 DayCount.None, Frequency.None, BDConvention.None, Calendar.None, false);
      var flotingLeg = new SwapLeg(expiry, maturity, Currency.None, 1.0,
                                   DayCount.None, Frequency.None, BDConvention.None, Calendar.None,
                                   false, new Tenor(), "3M");
      var swpn = new Swaption(AsOf, expiry, Currency.None, fixedLeg, flotingLeg,
                              0, PayerReceiver.Payer, OptionStyle.European, 0.0);
      return new BgmSwaptionVolatilityInterpolator(swpn,
                                                   ((IBgmCalibrator)Calibrator).DiscountCurve,
                                                   forwardVolatilities_, forwardVolatilityCurves_);
    }

    Func<Dt, double> IVolatilityCalculatorProvider.GetCalculator(IProduct product)
    {
      {
        var swpn = product as Swaption;
        if (swpn != null)
        {
          var interp = new BgmSwaptionVolatilityInterpolator(swpn,
            ((IBgmCalibrator)Calibrator).DiscountCurve,
            forwardVolatilities_, forwardVolatilityCurves_);
          return (asOf) => interp.Interpolate(asOf, swpn.Strike);
        }
      }
      throw new ToolkitException(String.Format(
        "Don't known how to calculate volatility of {0}",
        product.GetType().FullName));
    }

    /// <summary>
    /// Gets the calibrated forward volatilities.
    /// </summary>
    /// <value>The calibrated forward volatilities.</value>
    public IForwardVolatilityInfo CalibratedVolatilities
    {
      get { return forwardVolatilities_; }
    }

    /// <summary>
    /// Gets the type of the underlying distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    public DistributionType DistributionType
    {
      get { return forwardVolatilities_.DistributionType; }
    }

    ///<summary>
    /// Expiry list
    ///</summary>
    public string[] SwaptionExpiries
    {
      get { return expiries_; }
    }

    ///<summary>
    /// Forward tenor list
    ///</summary>
    public string[] SwaptionTenors
    {
      get { return tenors_; }
    }

    ///<summary>
    /// Swaption market volatilities
    ///</summary>
    public double[,] SwaptionVolatilities
    {
      get { return swpnVolatilities_; }
    }

    internal DiscountCurve DiscountCurve
    {
      get { return ((IBgmCalibrator) Calibrator).DiscountCurve;  }
    }
    private string[] expiries_, tenors_;
    private double[,] swpnVolatilities_;
    private CycleRule cycleRule_;
    private BDConvention bdc_;
    private Calendar cal_;
    private IForwardVolatilityInfo forwardVolatilities_;
    private VolatilityCurve[] forwardVolatilityCurves_;

    #region Calibrator
    private interface IBgmCalibrator
    {
      DiscountCurve DiscountCurve { get; }
    }
    ///<summary>
    /// BGM model based calibrator
    ///</summary>
    [Serializable]
    public class BgmCalibrator : IBgmCalibrator, IVolatilitySurfaceCalibrator
    {
      private VolatilityBootstrapMethod calibrationMethod_;
      private double tolerance_;
      private double[] shapeControls_;
      private DiscountCurve discountCurve_;
      private BgmCorrelation correlations_;
      private DistributionType volatilityModel_;

      ///<summary>
      /// Discount curve
      ///</summary>
      public DiscountCurve DiscountCurve { get { return discountCurve_; } }

      ///<summary>
      /// Calibration method
      ///</summary>
      public VolatilityBootstrapMethod CalibrationMethod
      {
        get { return calibrationMethod_; }
      }

      internal BgmCalibrator(VolatilityBootstrapMethod calibrationMethod,
        double tolerance,
        double[] shapeControls,
        DiscountCurve discountCurve,
        BgmCorrelation correlations,
        DistributionType volatilityModel)
      {
        calibrationMethod_ = calibrationMethod;
        tolerance_ = tolerance;
        shapeControls_ = shapeControls;
        discountCurve_ = discountCurve;
        correlations_ = correlations;
        volatilityModel_ = volatilityModel;
      }

      void IVolatilitySurfaceCalibrator.FitFrom(
        CalibratedVolatilitySurface surface, int fromTenorIdx)
      {
        var bs = (BgmForwardVolatilitySurface)surface;
        switch (calibrationMethod_)
        {
        case VolatilityBootstrapMethod.Cascading:
          bs.forwardVolatilities_ = BgmCalibratedVolatilities.CascadeCalibrate(
            surface.AsOf, discountCurve_, bs.expiries_, bs.tenors_,
            bs.cycleRule_, bs.bdc_, bs.cal_,
            correlations_, bs.swpnVolatilities_, volatilityModel_);
          break;
        case VolatilityBootstrapMethod.IterativeCascading:
          if (volatilityModel_ != DistributionType.LogNormal)
            goto case VolatilityBootstrapMethod.Cascading;
          bs.forwardVolatilities_ = BgmCalibrations.CascadingCalibrate(
            surface.AsOf, discountCurve_, bs.expiries_, bs.tenors_,
            bs.cycleRule_, bs.bdc_, bs.cal_,
            correlations_, bs.swpnVolatilities_, volatilityModel_);
          break;
        case VolatilityBootstrapMethod.PiecewiseConstant:
          if (volatilityModel_ != DistributionType.LogNormal)
            goto case VolatilityBootstrapMethod.PiecewiseFitTime;
          bs.forwardVolatilities_ = BgmCalibrations.PiecewiseConstantFit(
            calibrationMethod_ == VolatilityBootstrapMethod.PiecewiseFitLength,
            correlations_ == null,
            tolerance_, shapeControls_,
            surface.AsOf, discountCurve_, bs.expiries_, bs.tenors_,
            bs.cycleRule_, bs.bdc_, bs.cal_,
            correlations_, bs.swpnVolatilities_, volatilityModel_);
          break;
        case VolatilityBootstrapMethod.PiecewiseFitLength:
        case VolatilityBootstrapMethod.PiecewiseFitTime:
          bs.forwardVolatilities_ = BgmCalibratedVolatilities.PiecewiseConstantFit(
            calibrationMethod_ == VolatilityBootstrapMethod.PiecewiseFitLength,
            correlations_ == null,
            tolerance_, shapeControls_,
            surface.AsOf, discountCurve_, bs.expiries_, bs.tenors_, correlations_,
            bs.swpnVolatilities_, volatilityModel_);
            break;
        default:
          throw new ToolkitException(String.Format(
            "Calibration method not supported: {0}",
            calibrationMethod_));
        }
        bs.forwardVolatilityCurves_ = bs.forwardVolatilities_.ForwardVolatilityCurves;
      }
    }
    ///<summary>
    /// BGM model based calibrator
    ///</summary>
    [Serializable]
    public class BgmForwardCalibrator : IBgmCalibrator, IVolatilitySurfaceCalibrator, IForwardVolatilityInfo
    {
      private DiscountCurve discountCurve_;
      private BgmCorrelation correlations_;
      private DistributionType volatilityModel_;
      private VolatilityCurve[] fwdVolatilityCurves_;
      private Dt[] resetDates_;

      ///<summary>
      /// Discount curve
      ///</summary>
      public DiscountCurve DiscountCurve { get { return discountCurve_; } }

      internal BgmForwardCalibrator(
        Dt[] resetDates,
        DiscountCurve discountCurve,
        BgmCorrelation correlations,
        VolatilityCurve[] fwdVolatilityCurves,
        DistributionType volatilityModel)
      {
        discountCurve_ = discountCurve;
        correlations_ = correlations;
        fwdVolatilityCurves_ = fwdVolatilityCurves;
        volatilityModel_ = volatilityModel;
        resetDates_ = resetDates;
      }

      void IVolatilitySurfaceCalibrator.FitFrom(
        CalibratedVolatilitySurface surface, int fromTenorIdx)
      {
        var bs = (BgmForwardVolatilitySurface)surface;
        bs.forwardVolatilities_ = this;
        bs.forwardVolatilityCurves_ = ForwardVolatilityCurves;
      }

      #region IForwardVolatilityInfo Members

      /// <summary>
      /// Forward volatiilty curves
      /// </summary>
      public VolatilityCurve[] ForwardVolatilityCurves
      {
        get { return fwdVolatilityCurves_; }
      }

      /// <summary>
      /// Distribution type
      /// </summary>
      public DistributionType DistributionType
      {
        get { return volatilityModel_; }
      }

      /// <summary>
      /// Correlations
      /// </summary>
      public BgmCorrelation Correlation
      {
        get { return correlations_; }
      }

      /// <summary>
      /// ResetDates
      /// </summary>
      public Dt[] ResetDates
      {
        get { return resetDates_; }
      }

      #endregion
    }
    #endregion Calibrator

    #region Simple Interpolator
    [Serializable]
    class BgmInterpolator : IVolatilitySurfaceInterpolator
    {
      double IVolatilitySurfaceInterpolator.Interpolate(
        VolatilitySurface surface, Dt expiry, double strike)
      {
        throw new NotImplementedException();
      }
    }
    #endregion Simple Interpolator

    #region IModelParameter Members
    private static double CapletVolatility(Dt asOf, Dt expiry, Dt maturity, DiscountCurve discountCurve, Dt[] resets, VolatilityCurve[] fwdVols, BgmCorrelation correlation)
    {
      if (expiry <= resets[0])
        return 0.0;
      if (expiry >= resets[resets.Length - 1])
      {
        //all following rates have identical vol and are perfectly correlated
        var v = fwdVols[fwdVols.Length - 1];
        return Math.Sqrt(Curve.Integrate(asOf, expiry, v, v) / Dt.Years(asOf, expiry, DayCount.Actual365Fixed));
      }
      var start = Array.BinarySearch(resets, expiry);
      start = (start < 0) ? (~start - 1) : start;
      var factors = new List<double>();
      var vols = new List<VolatilityCurve>();
      var idx = new List<int>();
      double df0, df1 = discountCurve.Interpolate(expiry);
      Dt startDt, endDt = expiry;
      double tot = 1.0;
      for (int i = start + 1; i < resets.Length; ++i)
      {
        startDt = endDt;
        if (startDt >= maturity)
          break;
        endDt = (resets[i] < maturity) ? resets[i] : maturity;
        df0 = df1;
        df1 = discountCurve.Interpolate(endDt);
        double factor = df0 / df1;
        factors.Add(factor);
        vols.Add(fwdVols[i - 1]);
        idx.Add(i - 1);
        tot *= factor;
      }
      double delta = Dt.Fraction(expiry, maturity, discountCurve.DayCount);
      double fwd = (tot - 1.0) / delta;
      tot /= delta;
      double retVal = 0.0;
      for (int i = 0; i < vols.Count; ++i)
      {
        int ii = idx[i];
        double li = (factors[i] - 1.0) / factors[i];
        for (int j = 0; j <= i; ++j)
        {
          double lj = (factors[j] - 1.0) / factors[j];
          int ij = idx[j];
          retVal += (i == j)
                      ? Curve.Integrate(asOf, expiry, vols[i], vols[i]) * li * li
                      : 2 * correlation[ii, ij] * li * lj *
                        Curve.Integrate(asOf, expiry, vols[i], vols[j]);
        }
      }
      double time = Dt.FractDiff(asOf, expiry) / 365.0;
      return tot/fwd*Math.Sqrt(retVal/time);
    }
    
    
    /// <summary>
    /// Use BgmForwardVolatilitySurface as model parameter
    /// </summary>
    /// <param name="maturity">Maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="referenceIndex">ReferenceIndex</param>
    /// <returns>Index volatility</returns>
    double IModelParameter.Interpolate(Dt maturity, double strike, ReferenceIndex referenceIndex)
    {
      if (referenceIndex is SwapRateIndex)
      {
        var swapRateIndex = referenceIndex as SwapRateIndex;
        Dt expiry = maturity;
        maturity = Dt.Add(expiry, swapRateIndex.IndexTenor);
        var fixedLeg = new SwapLeg(expiry, maturity, swapRateIndex.Currency, 1.0, swapRateIndex.DayCount,
                                   swapRateIndex.IndexFrequency, swapRateIndex.Roll, swapRateIndex.Calendar, false);
        var floatingLeg = new SwapLeg(expiry, maturity, swapRateIndex.IndexFrequency, 0.0,
                                      swapRateIndex.ForwardRateIndex);
        var swpn = new Swaption(AsOf, expiry, Currency.None, fixedLeg, floatingLeg,
                                0, PayerReceiver.Payer, OptionStyle.European, 0.0);
        var interpolator = new BgmSwaptionVolatilityInterpolator(swpn, ((IBgmCalibrator) Calibrator).DiscountCurve,
                                                                 forwardVolatilities_, forwardVolatilityCurves_);
        return interpolator.Interpolate(AsOf, strike);
      }
      if (referenceIndex is InterestRateIndex)
      {
        var expiry = maturity;
        maturity = Dt.Add(expiry, referenceIndex.IndexTenor);
        var discountCurve = ((IBgmCalibrator) Calibrator).DiscountCurve;
        var resets = forwardVolatilities_.ResetDates;
        var fwdVols = forwardVolatilityCurves_;
        var correlations = forwardVolatilities_.Correlation;
        return CapletVolatility(AsOf, expiry, maturity, discountCurve, resets, fwdVols, correlations);
      }
      if (referenceIndex == null)
      {
        // Just returns the volatility of the single forward rate expired on specified maturity
        var discountCurve = ((IBgmCalibrator)Calibrator).DiscountCurve;
        var resets = forwardVolatilities_.ResetDates;
        var fwdVols = forwardVolatilityCurves_;
        var correlations = forwardVolatilities_.Correlation;
        var expiry = maturity;
        for (int i = 0; i < resets.Length; ++i)
        {
          if (resets[i] > expiry)
            return CapletVolatility(AsOf, expiry, resets[i], discountCurve, resets, fwdVols, correlations);
        }
        return CapletVolatility(AsOf, expiry, expiry + 7, discountCurve, resets, fwdVols, correlations);
      }
      throw new ToolkitException("ReferenceIndex not supported");
    }
    #endregion
  }
}
