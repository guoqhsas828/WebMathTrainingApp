/*
 * 
 */

using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models.HullWhiteShortRates
{
  /// <summary>
  /// Hull-White short rate volatility calibration settings
  /// </summary>
  public class CalibrationSettings
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CalibrationSettings" /> class.
    /// </summary>
    /// <param name="distributionType">Type of the distribution.</param>
    /// <param name="scaleFactor">The volatility scale factor</param>
    /// <param name="shift">The shift.</param>
    /// <param name="volatilityLowerBounds">The volatility lower bounds.</param>
    /// <param name="volatilityUpperBounds">The volatility upper bounds.</param>
    /// <param name="meanReversionLowerBounds">The mean reversion lower bounds.</param>
    /// <param name="meanReversionUpperBounds">The mean reversion upper bounds.</param>
    /// <param name="toleranceF">The tolerance f.</param>
    /// <param name="toleranceX">The tolerance x.</param>
    /// <param name="toleranceGrad">The tolerance grad.</param>
    /// <param name="maxIterations">The maximum iterations.</param>
    /// <param name="maxEvaluations">The maximum evaluations.</param>
    public CalibrationSettings(
      DistributionType distributionType = DistributionType.LogNormal,
      double scaleFactor = 1.0,
      double shift = 0.0,
      Curve volatilityLowerBounds = null,
      Curve volatilityUpperBounds = null,
      Curve meanReversionLowerBounds = null,
      Curve meanReversionUpperBounds = null,
      double toleranceF = 0,
      double toleranceX = 0,
      double toleranceGrad = 0,
      int maxIterations = 0,
      int maxEvaluations = 0)
    {
      DistributionType = distributionType;
      ScaleFactor = scaleFactor;
      Shift = shift;
      VolatilityLowerBounds = volatilityLowerBounds;
      VolatilityUpperBounds = volatilityUpperBounds;
      MeanReversionLowerBounds = meanReversionLowerBounds;
      MeanReversionUpperBounds = meanReversionUpperBounds;
      ToleranceF = toleranceF;
      ToleranceX = toleranceX;
      ToleranceGrad = toleranceGrad;
      MaxIterations = maxIterations;
      MaxEvaluations = maxEvaluations;
    }

    internal VolatilitySpec VolatilitySpec
      => new VolatilitySpec(DistributionType, Shift);

    /// <summary>
    /// Gets the type of the distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    public DistributionType DistributionType { get; }

    /// <summary>
    /// Gets the shift value.  This applies only to shifted log-normal distribution
    /// </summary>
    /// <value>The shift.</value>
    public double Shift { get; }

    /// <summary>
    /// Gets the mean reversion lower bounds.
    /// </summary>
    /// <value>The mean reversion lower bounds.</value>
    public Curve MeanReversionLowerBounds { get; }

    /// <summary>
    /// Gets the mean reversion upper bounds.
    /// </summary>
    /// <value>The mean reversion upper bounds.</value>
    public Curve MeanReversionUpperBounds { get; }

    /// <summary>
    /// Gets the volatility lower bounds.
    /// </summary>
    /// <value>The volatility lower bounds.</value>
    public Curve VolatilityLowerBounds { get; }

    /// <summary>
    /// Gets the volatility upper bounds.
    /// </summary>
    /// <value>The volatility upper bounds.</value>
    public Curve VolatilityUpperBounds { get; }

    /// <summary>
    /// Gets the tolerance f.
    /// </summary>
    /// <value>The tolerance f.</value>
    public double ToleranceF { get; }

    /// <summary>
    /// Gets the tolerance x.
    /// </summary>
    /// <value>The tolerance x.</value>
    public double ToleranceX { get; }

    /// <summary>
    /// Gets the tolerance grad.
    /// </summary>
    /// <value>The tolerance grad.</value>
    public double ToleranceGrad { get; }

    /// <summary>
    /// Gets the maximum iterations.
    /// </summary>
    /// <value>The maximum iterations.</value>
    public int MaxIterations { get; }

    /// <summary>
    /// Gets the maximum evaluations.
    /// </summary>
    /// <value>The maximum evaluations.</value>
    public int MaxEvaluations { get; }

    /// <summary>
    /// Gets the volatility scale factor
    /// </summary>
    /// <value>The volatility scale factor</value>
    public double ScaleFactor { get; }
  }

}
