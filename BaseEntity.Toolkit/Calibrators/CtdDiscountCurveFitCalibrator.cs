//
// CtdDiscountCurveFitCalibrator.cs
// 
//

using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Cheapest-to-Deliver Discount Curve calibrator
  /// </summary>
  [Serializable]
  public class CtdDiscountCurveFitCalibrator : DiscountRateCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(CtdDiscountCurveFitCalibrator));

    #region Static Constructors

    /// <summary>
    ///  Construct CTD curve
    /// </summary>
    /// <param name="name">Curve name</param>
    /// <param name="asOfDate">As of date</param>
    /// <param name="curveDates">curve dates</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="domDiscountCurve">Domestic discount curve</param>
    /// <param name="discountCurves">Discount curves</param>
    /// <param name="fxCurves">FX curves</param>
    /// <param name="interpMethod">Interpolation method</param>
    /// <param name="extrapMethod">Extrapolation method</param>
    /// <param name="dc">Daycount convention</param>
    /// <param name="freq">Frequency</param>
    /// <returns></returns>
    public static CtdDiscountCurve FitCtdDiscountCurve(string name, Dt asOfDate, Dt[] curveDates, string[] tenorNames,
      DiscountCurve domDiscountCurve, DiscountCurve[] discountCurves, FxCurve[] fxCurves,
      string interpMethod, ExtrapMethod extrapMethod, DayCount dc, Frequency freq)
    {
      if (tenorNames.Any() && curveDates.Length != tenorNames.Length)
        throw new ToolkitException("Number of curve dates must be equal to number of tenor names for CTD discount curve");
      var cal = new CtdDiscountCurveFitCalibrator(asOfDate, domDiscountCurve, discountCurves, fxCurves);
      var ctd = new CtdDiscountCurve(asOfDate, cal, domDiscountCurve, discountCurves, fxCurves, curveDates, tenorNames)
      {
        Interp = InterpScheme.InterpFromName(interpMethod, extrapMethod, double.MinValue, double.MaxValue),
        Name = name,
        DayCount = dc,
        Frequency = freq
      };
      ctd.Fit();
      return ctd;
    }

    #endregion

    #region Constructors

    /// <summary>
    ///  Ctd calibrator constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="domDiscountCurve">Domestic discount curve</param>
    /// <param name="discountCurves">Foreign discount curves</param>
    /// <param name="fxCurves">FX curves</param>
    public CtdDiscountCurveFitCalibrator(Dt asOf, DiscountCurve domDiscountCurve, IEnumerable<DiscountCurve> discountCurves, IEnumerable<FxCurve> fxCurves)
      : base(asOf)
    {
      DomesticDiscountCurve = domDiscountCurve;
      DiscountCurves = discountCurves.Where(x => x != null).Distinct().ToArray();
      FxCurves = fxCurves.Where(x => x != null).Distinct().ToArray();
    }

    #endregion

    #region Calibration

    /// <summary>
    ///   Fit a curve from the specified tenor point
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    /// <remarks>
    ///   <para>Derived calibrated curves implement this to do the work of the
    ///     fitting</para>
    ///   <para>Called by Fit() and Refit(). Child calibrators can assume
    ///     that the tenors have been validated and the data curve has
    ///     been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      base.FitFrom(curve, fromIdx);
    }

    /// <summary>
    ///  Creates CTD curve tenors from input IR and FX curves
    /// </summary>
    /// <param name="ctd">CTD curve</param>
    /// <param name="asOfDate">As of date</param>
    /// <param name="curveDates">Curve dates</param>
    /// <param name="tenorNames">Tenor names</param>
    public static void AddCtdCurveYields(CtdDiscountCurve ctd, Dt asOfDate, Dt[] curveDates, string[] tenorNames)
    {
      ctd.Tenors.Clear();
      var dc = ctd.DayCount;
      var freq = ctd.Frequency;
      var domCcy = ctd.DomesticDiscountCurve.Ccy;

      // Create values
      var prevDate = asOfDate;
      var prevDf = 1.0;
      for (var j = 0; j < curveDates.Count(); j++)
      {
        var start = prevDate;
        var end = curveDates[j];
        var dt = Dt.Fraction(start, end, start, end, dc, freq);
        
        var maxRate = ctd.DomesticDiscountCurve.F(start, end, dc, freq);
        for (var i = 0; i < ctd.DiscountCurves.Count(); i++)
        {
          var forwardRate = ctd.DiscountCurves[i].F(start, end, dc, freq);

          // Ensure the FX rates are not before the spot date, dt should be calculated the same as above
          var fxImpliedRateSpread = 0.0;
          var fx = ctd.GetFxCurve(i);
          if (fx != null)
          {
            var fxStart = Dt.AddDays(start, fx.SpotDays, fx.SpotCalendar);
            var fxEnd = Dt.AddDays(end, fx.SpotDays, fx.SpotCalendar);
            var forCcy = fx.Ccy1 == ctd.Ccy ? fx.Ccy2 : fx.Ccy1;
            var fxRateStart = fx.FxRate(fxStart, forCcy, domCcy);
            var fxRateEnd = fx.FxRate(fxEnd, forCcy, domCcy);            
            fxImpliedRateSpread = -RateCalc.RateFromPrice(fxRateEnd / fxRateStart, fxStart, fxEnd, dc, freq);
          }

          var adjForwardRate = forwardRate + fxImpliedRateSpread;

          maxRate = Math.Max(maxRate, adjForwardRate);
          if (dc != DayCount.Actual365Fixed && freq != Frequency.Continuous)
            maxRate = RateCalc.RateConvert(maxRate, start, end, dc, freq, DayCount.Actual365Fixed, Frequency.Continuous);
        }

        var ctdDf = Math.Exp(-maxRate * dt) * prevDf;
        prevDf = ctdDf;
        prevDate = end;

        // Add Tenor to original curve (for sensitivities)
        ctd.AddZeroYield(end,
          RateCalc.RateFromPrice(ctdDf, asOfDate, end, DayCount.Actual365Fixed, Frequency.Continuous),
          DayCount.Actual365Fixed, Frequency.Continuous);
        if (tenorNames.Any())
            ctd.Tenors[j].Name = tenorNames[j];
      }
    }

    #endregion

    /// <summary>
    ///  Domestic discount factor
    /// </summary>
    public DiscountCurve DomesticDiscountCurve { get; set; }

    /// <summary>
    ///  Discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves { get; set; }

    /// <summary>
    ///  Fx curves
    /// </summary>
    public FxCurve[] FxCurves { get; set; }
    
  }
}
