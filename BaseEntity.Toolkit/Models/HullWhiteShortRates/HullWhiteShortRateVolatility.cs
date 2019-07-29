using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  /// <summary>
  /// Class HullWhiteShortRateVolatility.
  /// </summary>
  public class HullWhiteShortRateVolatility
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="HullWhiteShortRateVolatility" /> class.
    /// </summary>
    /// <param name="asOf">The date as of today</param>
    /// <param name="eval">The swaption evaluator</param>
    /// <param name="settings">The calibration settings</param>
    public HullWhiteShortRateVolatility(
      Dt asOf,
      SwaptionsEvaluator eval,
      CalibrationSettings settings)
    {
      AsOf = asOf;
      SwaptionsEvaluator = eval;
      CalibrationSettings = settings;
    }

    #region Simple methods

    /// <summary>
    /// Gets the volatility curve.
    /// </summary>
    /// <returns>VolatilityCurve.</returns>
    public VolatilityCurve GetVolatilityCurve()
      => SwaptionsEvaluator.GetVolatilityCurve(AsOf);

    /// <summary>
    /// Gets the mean reversion curve.
    /// </summary>
    /// <returns>VolatilityCurve.</returns>
    public VolatilityCurve GetMeanReversionCurve()
      => SwaptionsEvaluator.GetMeanReversionCurve(AsOf);

    public VolatilityCurve[] ToVolatilityArray()
    {
      return CreateVolatilityArray(GetMeanReversionCurve(), GetVolatilityCurve());
    }

    #endregion

    #region Data and properties

    /// <summary>
    /// Gets the swaption data
    /// </summary>
    public SwaptionDataSet SwaptionData => SwaptionsEvaluator.Data;

    /// <summary>
    /// Gets the swaptions evaluator
    /// </summary>
    public SwaptionsEvaluator SwaptionsEvaluator { get; }

    /// <summary>
    /// Gets as-of date.
    /// </summary>
    /// <value>As-of date</value>
    public Dt AsOf { get; }

    /// <summary>
    /// Gets the calibration settings.
    /// </summary>
    /// <value>The calibration settings.</value>
    public CalibrationSettings CalibrationSettings { get; }

    #endregion

    #region Methods to create and query volatilities/mean reversions

    /// <summary>
    /// Creates the volatility array.
    /// </summary>
    /// <param name="meanReversionCurve">The mean reversion curve.</param>
    /// <param name="sigmaCurve">The sigma curve.</param>
    /// <returns>VolatilityCurve[].</returns>
   public static VolatilityCurve[] CreateVolatilityArray(
      VolatilityCurve meanReversionCurve,
      VolatilityCurve sigmaCurve)
    {
      return new[] { meanReversionCurve, sigmaCurve };
    }

    /// <summary>
    /// Gets the mean reversion.
    /// </summary>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <returns>VolatilityCurve.</returns>
    public static VolatilityCurve GetMeanReversion(
      VolatilityCurve[] volatilityObject)
    {
      Debug.Assert(volatilityObject != null && volatilityObject.Length == 2);
      return volatilityObject[0];
    }

    /// <summary>
    /// Gets the volatility.
    /// </summary>
    /// <param name="volatilityObject">The volatility object.</param>
    /// <returns>VolatilityCurve.</returns>
    public static VolatilityCurve GetVolatility(
      VolatilityCurve[] volatilityObject)
    {
      Debug.Assert(volatilityObject != null && volatilityObject.Length == 2);
      return volatilityObject[1];
    }

    #endregion
  }
}
