// 
// 
// 
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  /// <summary>
  /// Class Calibration.
  /// </summary>
  public static class Calibration
  {
    #region Calibration

    /// <summary>
    /// Calibrates the Hull-White short rate volatilities
    ///  from swaption data set
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="interestRateCurve">The interest rate curve</param>
    /// <param name="expiries">The list of expiries</param>
    /// <param name="tenors">The list of swap tenors</param>
    /// <param name="volatilities">The volatility data</param>
    /// <param name="strikeSkews">The strike skews data</param>
    /// <param name="index">The interest rate index for the swap value calculations</param>
    /// <param name="settings">The calibration settings</param>
    /// <returns>IReadOnlyList&lt;System.Double&gt;</returns>
    public static HullWhiteShortRateVolatility Calibrate(
      Dt asOf,
      DiscountCurve interestRateCurve,
      IReadOnlyList<Tenor> expiries,
      IReadOnlyList<Tenor> tenors,
      IReadOnlyList<IReadOnlyList<double>> volatilities,
      IReadOnlyList<IReadOnlyList<double>> strikeSkews = null,
      ReferenceIndex index = null,
      CalibrationSettings settings = null)
    {
      var data = SwaptionDataSet.Create(asOf, interestRateCurve,
        expiries, tenors, volatilities, settings?.ScaleFactor ?? 1.0,
        strikeSkews, settings?.VolatilitySpec ?? new VolatilitySpec(),
        index);
      var eval = new SwaptionsEvaluator(data);
      var opt = GetOptimizer(eval, settings);
      opt.Minimize(eval.Evaluate, eval.DimensionX, eval.DimensionF);
      return new HullWhiteShortRateVolatility(asOf, eval, settings);
    }

    /// <summary>
    /// Calibrates the Hull-White short rate volatilities
    ///  from swaption data set
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="interestRateCurve">The interest rate curve</param>
    /// <param name="volTimes">Times in years</param>
    /// <param name="volatilities">The volatility data</param>
    /// <param name="meanReversionTimes">Times in years</param>
    /// <param name="meanReversions">The mean reversion levels</param>
    /// <param name="settings">The calibration settings</param>
    /// <returns>IReadOnlyList&lt;System.Double&gt;</returns>
    public static HullWhiteShortRateVolatility Calibrate(
      Dt asOf,
      DiscountCurve interestRateCurve,
      double[] volTimes,
      double[] volatilities,
      double[] meanReversionTimes,
      double[] meanReversions,
      CalibrationSettings settings = null)
    {
      var eval = new SwaptionsEvaluator(volTimes, volatilities, meanReversionTimes, meanReversions);
      return new HullWhiteShortRateVolatility(asOf, eval, settings);
    }

    /// <summary>
  /// Calibrates the short rate volatilities
  ///  using the specified swaption evaluator.
  /// </summary>
  /// <param name="eval">The swaption evaluator</param>
  /// <param name="numericalOptions">The numerical options</param>
  /// <returns>IReadOnlyList&lt;System.Double&gt;.</returns>
  internal static IReadOnlyList<double> Calibrate(
      SwaptionsEvaluator eval,
      CalibrationSettings numericalOptions = null)
    {
      var opt = GetOptimizer(eval, numericalOptions);
      opt.Minimize(eval.Evaluate, eval.DimensionX, eval.DimensionF);
      return opt.CurrentSolution;
    }

    /// <summary>
    /// Gets the optimizer.
    /// </summary>
    /// <param name="eval">The swaption evaluator</param>
    /// <param name="options">The options.</param>
    /// <returns>Optimizer.</returns>
    private static Optimizer GetOptimizer(
      SwaptionsEvaluator eval,
      CalibrationSettings options)
    {
      var lower = eval.GetLowerBounds(options);
      var upper = eval.GetUpperBounds(options);
      var initials = eval.GetIntialPoints(lower, upper);

      var opt = new NLS(eval.DimensionX);
      opt.setLowerBounds(lower);
      opt.setUpperBounds(upper);
      opt.setInitialPoint(initials);

      var toleranceF = Pick(options?.ToleranceF, 5E-4*eval.DimensionF);
      opt.setToleranceF(toleranceF);
      var toleranceGrad = Pick(options?.ToleranceGrad, toleranceF/100);
      opt.setToleranceGrad(toleranceGrad);
      var toleranceX = Pick(options?.ToleranceX, 1E-6);
      opt.setToleranceX(toleranceX);
      var maxIters = Pick(options?.MaxIterations, 120);
      opt.setMaxIterations(maxIters);
      var maxEvals = Pick(options?.MaxEvaluations, 240);
      opt.setMaxEvaluations(maxEvals);

      return opt;
    }

    /// <summary>
    /// Picks the specified v.
    /// </summary>
    /// <param name="v">The v.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>System.Double.</returns>
    private static double Pick(double? v, double defaultValue)
    {
      return v.HasValue && v.Value > 0 ? v.Value : defaultValue;
    }

    /// <summary>
    /// Picks the specified v.
    /// </summary>
    /// <param name="v">The v.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>System.Int32.</returns>
    private static int Pick(int? v, int defaultValue)
    {
      return v.HasValue && v.Value > 0 ? v.Value : defaultValue;
    }

    #endregion
  }
}
