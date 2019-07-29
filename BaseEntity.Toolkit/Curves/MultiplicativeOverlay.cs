using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using System;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Curves
{
  [Serializable]
  public class MultiplicativeOverlay : BaseEntityObject
    , ICurveInterpolator, IEvaluator<Dt, double>, IEvaluator<double, double>
  {
    private readonly NativeCurve base_;
    private readonly Curve over_;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiplicativeOverlay"/> class.
    /// </summary>
    /// <param name="baseCurve">The base curve.</param>
    /// <param name="overlay">The overlay.</param>
    /// <remarks></remarks>
    public MultiplicativeOverlay(
      NativeCurve baseCurve, Curve overlay)
    {
      base_ = baseCurve;
      over_ = overlay;
    }

    /// <summary>
    /// Gets the overlay curve.
    /// </summary>
    /// <remarks></remarks>
    public Curve OverlayCurve
    {
      get { return over_; }
    }

    /// <summary>
    /// Gets the base curve.
    /// </summary>
    /// <remarks></remarks>
    public NativeCurve BaseCurve
    {
      get { return base_; }
    }

    #region ICurveInterpolator Members

    public void Initialize(NativeCurve curve)
    {
      // do nothing;
    }

    public double Evaluate(NativeCurve curve, double t, int index)
    {
      var date = new Dt(curve.GetAsOf(), t/365);
      return base_.Interpolate(date)*over_.Interpolate(date);
    }

    public double Evaluate(Dt date)
    {
      return base_.Interpolate(date)*over_.Interpolate(date);
    }

    public double Evaluate(double date)
    {
      return base_.Interpolate(date)*over_.Interpolate(date);
    }

    #endregion
  }

  [Serializable]
  internal sealed class OverlayCalibrator : DiscountCurveFitCalibrator
  {
    internal static bool IsInverse(CalibratedCurve curve)
    {
      var cal = curve.Calibrator as DiscountRateCalibrator;
      return cal != null && cal.Inverse;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayCalibrator"/> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="baseCurve">The base curve.</param>
    /// <param name="overlay">The overlay.</param>
    /// <remarks></remarks>
    public OverlayCalibrator(Dt asOf, CalibratedCurve baseCurve, CalibratedCurve overlay)
      : base(asOf)
    {
      baseCurve_ = baseCurve;
      overlay_ = overlay;
    }

    /// <summary>
    /// Fit a curve from the specified tenor point
    /// </summary>
    /// <param name="curve">Curve to calibrate</param>
    /// <param name="fromIdx">Index to start fit from</param>
    /// <remarks></remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var inverse = IsInverse(baseCurve_);
      var dates = new Dt[curve.Tenors.Count];
      var vals = new double[curve.Tenors.Count];
      CurveTenorCollection tenors = curve.Tenors;
      for (int i = 0; i < tenors.Count; i++)
      {
        var tenor = tenors[i];
        dates[i] = tenor.Product.Maturity;
        var quote = tenor.CurrentQuote.Value;
        var note = tenor.Product as Note;
        if (note == null)
        {
          vals[i] = 1.0;
          continue;
        }
        var ratio = RateCalc.PriceFromRate(quote, note.Effective, note.Maturity,
          note.DayCount, note.CompoundFreq)/RateCalc.PriceFromRate(
            tenor.OriginalQuote.Value, note.Effective, note.Maturity,
            note.DayCount, note.CompoundFreq);
        vals[i] = inverse ? (1/ratio):ratio;
      }
      curve.Clear();
      curve.Set(dates, vals);
      return;
    }

    /// <summary>
    /// Parent curves
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (baseCurve_ != null) yield return baseCurve_;
      if (overlay_ != null) yield return overlay_;
    }

    /// <summary>
    /// Gets the overlay curve.
    /// </summary>
    /// <remarks></remarks>
    public CalibratedCurve OverlayCurve
    {
      get { return overlay_; }
    }

    /// <summary>
    /// Gets the base curve.
    /// </summary>
    /// <remarks></remarks>
    public CalibratedCurve BaseCurve
    {
      get { return baseCurve_; }
    }

    private readonly CalibratedCurve baseCurve_;
    private readonly CalibratedCurve overlay_;
  }

}
