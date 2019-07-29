using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Pricers.BGM
{
  /// <summary>
  ///   BGM Tree calculator.
  /// </summary>
  /// <remarks>
  ///  <inheritdoc cref="SwapBermudanBgmTreePricer"/>
  /// </remarks>
  [Serializable]
  public class BgmTreeCalculator
  {
    #region Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="BgmTreeCalculator"/> class.
    /// </summary>
    /// <param name="forwardRatesCurve">The forward rates curve.</param>
    /// <param name="forwardTenorDates">The tenor dates of the forward rates (null means to use the curve dates).</param>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <param name="nodeDates">The dates to calculate the rate distributions (null means to use the tenor dates).</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="tolerance">The tolerance.</param>
    public BgmTreeCalculator(
      DiscountCurve forwardRatesCurve,
      IList<Dt> forwardTenorDates,
      Object volatilityObject,
      IList<Dt> nodeDates,
      double stepSize, double tolerance)
    {
      if (volatilityObject == null)
      {
        throw new ToolkitException("Volatility cannot be null");
      }
      if (!(volatilityObject is BgmCalibratedVolatilities
        || volatilityObject is BgmForwardVolatilitySurface
        || volatilityObject is VolatilityCurve[]))
      {
        throw new ToolkitException("Invalid volatility object.");
      }
      volatilityObject_ = volatilityObject;
      fwdRateCurve_ = forwardRatesCurve;
      tenorDates_ = forwardTenorDates;
      nodeDates_ = nodeDates;
      stepSize_ = stepSize;
      tolerance_ = tolerance;
      distributions_ = null;
    }

    /// <summary>
    /// Resets this instance.
    /// </summary>
    public void Reset()
    {
      distributions_ = null;
    }

    /// <summary>
    /// Computes the distributions.
    /// </summary>
    private void ComputeDistributions()
    {
      distributions_ = BgmBinomialTree.CalculateRateSystem(
        stepSize_, tolerance_, fwdRateCurve_.AsOf, fwdRateCurve_,
        GetTenorDates(), VolatilityCurves, nodeDates_);
      return;
    }

    private IList<Dt> GetTenorDates()
    {
      if (tenorDates_ != null) return tenorDates_;
      var surface = volatilityObject_ as BgmForwardVolatilitySurface;
      var volObj = surface != null
        ? surface.CalibratedVolatilities as BgmCalibratedVolatilities
        : (volatilityObject_ as BgmCalibratedVolatilities);
      if (volObj != null)
      {
        Dt asOf = volObj.AsOf;
        return Array.ConvertAll(volObj.TenorDates, (t) => new Dt(asOf, t));
      }
      return ListUtil.CreateList(fwdRateCurve_.Count,
        (i) => fwdRateCurve_.GetDt(i)).ToArray();
    }

    private VolatilityCurve[] GetVolatilityCurves()
    {
      var surface = volatilityObject_ as BgmForwardVolatilitySurface;
      var volObj = surface != null
        ? surface.CalibratedVolatilities as BgmCalibratedVolatilities
        : (volatilityObject_ as BgmCalibratedVolatilities);
      if (volObj != null)
      {
        return volObj.BuildBlackVolatilityCurves();
      }
      return volatilityObject_ as VolatilityCurve[];
    }
    #endregion Methods

    #region Properties
    /// <summary>
    /// Gets as-of date.
    /// </summary>
    /// <value>As-of date.</value>
    public Dt AsOf
    {
      get { return fwdRateCurve_.AsOf; }
    }

    /// <summary>
    /// Gets the size of the step.
    /// </summary>
    /// <value>The size of the step.</value>
    public double StepSize
    {
      get { return stepSize_; }
    }

    /// <summary>
    /// Gets the tolerance.
    /// </summary>
    /// <value>The tolerance.</value>
    public double Tolerance
    {
      get { return tolerance_; }
    }

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    public DiscountCurve DiscountCurve
    {
      get { return fwdRateCurve_; }
    }

    /// <summary>
    /// Gets the volatility curves.
    /// </summary>
    /// <value>The volatility curves.</value>
    public VolatilityCurve[] VolatilityCurves
    {
      get { return GetVolatilityCurves(); }
    }

    /// <summary>
    /// Gets the rate distributions.
    /// </summary>
    /// <value>The rate distributions.</value>
    public IRateSystemDistributions RateDistributions
    {
      get
      {
        if (distributions_ == null)
          ComputeDistributions();
        return distributions_;
      }
      internal set { distributions_ = value; }
    }

    internal IList<Dt> NodeDates
    {
      get { return nodeDates_; }
      set { nodeDates_ = value; Reset();}
    }
    #endregion

    #region Data

    private readonly double stepSize_;
    private readonly double tolerance_;
    private readonly DiscountCurve fwdRateCurve_;
    private readonly IList<Dt> tenorDates_;
    private readonly Object volatilityObject_;
    private IRateSystemDistributions distributions_;

    private IList<Dt> nodeDates_;
    #endregion Data
  }
}
