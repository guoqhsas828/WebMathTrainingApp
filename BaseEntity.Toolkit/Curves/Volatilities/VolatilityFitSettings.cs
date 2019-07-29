using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  /// Class that holds the information regarding the SABR fit settings
  /// </summary>
  [Serializable]
  public class VolatilityFitSettings
  {
    /// <summary>
    /// Gets or sets the SABR parameter bounds.
    /// </summary>
    /// <value>The SABR parameter bounds.</value>
    public Bounds<AlphaBetaRhoNu> SabrBounds { get; set; }

    /// <summary>Sabr model parameters</summary>
    public Curve SabrBeta { get; set; }

    /// <summary>Sabr model parameters</summary>
    public Curve SabrAlpha { get; set; }

    /// <summary>Sabr model parameters</summary>
    public Curve SabrRho { get; set; }

    /// <summary>Sabr model parameters</summary>
    public Curve SabrNu { get; set; }

    /// <summary>
    /// Gets or sets the implied volatility accuracy.
    /// </summary>
    /// <value>The implied volatility accuracy.</value>
    public double ImpliedVolatilityAccuracy
    {
      get { return _ivolAccuracy; }
      set { if (value >= 0) _ivolAccuracy = value; }
    }
    private double _ivolAccuracy = 1E-8;

    /// <summary>
    /// Gets or sets the SABR model flags.
    /// </summary>
    /// <value>The SABR model flags.</value>
    public SabrModelFlags SabrModelFlags { get; set; }

    /// <summary>
    /// Gets or sets the shift value for shifted log-normal and related models
    /// </summary>
    /// <value>The shift value.</value>
    public double ShiftValue { get; set; }
  }

  /// <summary>
  /// Enum SabrModelFlags
  /// </summary>
  [Flags]
  public enum SabrModelFlags
  {
    /// <summary>
    /// No flag
    /// </summary>
    None = 0,
    /// <summary>
    /// The improved sabr initial values
    /// </summary>
    ImprovedSabrInitialValues = 1,
    /// <summary>
    /// The adjusted alpha
    /// </summary>
    AdjustedAlpha = 2,
    /// <summary>
    /// The boundary correction
    /// </summary>
    BoundaryCorrection = 4,
  }

}
