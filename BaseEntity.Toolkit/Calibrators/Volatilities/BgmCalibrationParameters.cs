//
// BgmCalibrationParameters.cs
//  -2010. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   A wrapper of BGM calibration parameters.
  /// </summary>
  [Serializable]
  public class BgmCalibrationParameters
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BgmCalibrationParameters"/> class.
    /// </summary>
    public BgmCalibrationParameters()
    {
      CalibrationMethod = VolatilityBootstrapMethod.Cascading;
      VolatilityModel = DistributionType.LogNormal;
      Tolerance = 0.01;
      PsiUpperBound = 1.0;
      PsiLowerBound = 0.0;
      PhiUpperBound = 1.002;
      PhiLowerBound = 0.9;
    }
    /// <summary>
    /// Gets or sets the calibration method.
    /// </summary>
    /// <value>The calibration method.</value>
    public VolatilityBootstrapMethod CalibrationMethod { get; set; }
    /// <summary>
    /// Gets or sets the volatility model.
    /// </summary>
    /// <value>The volatility model.</value>
    public DistributionType VolatilityModel { get; set; }
    /// <summary>
    /// Gets or sets the tolerance.
    /// </summary>
    /// <value>The tolerance.</value>
    public double Tolerance { get; set; }
    /// <summary>
    /// Gets or sets the psi upper bound.
    /// </summary>
    /// <value>The psi upper bound.</value>
    public double PsiUpperBound { get; set; }
    /// <summary>
    /// Gets or sets the psi lower bound.
    /// </summary>
    /// <value>The psi lower bound.</value>
    public double PsiLowerBound { get; set; }
    /// <summary>
    /// Gets or sets the phi upper bound.
    /// </summary>
    /// <value>The phi upper bound.</value>
    public double PhiUpperBound { get; set; }
    /// <summary>
    /// Gets or sets the phi lower bound.
    /// </summary>
    /// <value>The phi lower bound.</value>
    public double PhiLowerBound { get; set; }

    ///<summary>
    ///</summary>
    public double[] ShapeControl
    {
      get
      {
        return new[]
          {
            PsiUpperBound, PsiLowerBound,
            PhiUpperBound, PhiLowerBound
          };
      }
    }
  }

}
