using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Hull-White calibration settings
  /// </summary>
  public class HullWhiteCalibrationSettings
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public HullWhiteCalibrationSettings()
    {
      MaxEvaluation = 5000;
      MaxIteration = 5000;
      FToleranceFactor = 1E-3;
      ToleranceGrad = 1E-6;
      ToleranceX = 1E-6;
    }

    /// <summary>
    /// MaxEvaluation
    /// </summary>
    public int MaxEvaluation { get; set; }

    /// <summary>
    /// MaxIteration
    /// </summary>
    public int MaxIteration { get; set; }

    /// <summary>
    /// The factor for tolerance of funtion constraint
    /// </summary>
    public double FToleranceFactor { get; set; }

    /// <summary>
    /// The Tolerance for gradiant
    /// </summary>
    public double ToleranceGrad { get; set; }

    /// <summary>
    /// The Tolerance for variables
    /// </summary>
    public double ToleranceX { get; set; }
  }
}
