/*
 * Copyright (c)    2002-2018. All rights reserved.
 */
namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Types of VolatilityBootstrapMethods
  /// </summary>
  public enum VolatilityBootstrapMethod
  {
    /// <summary>None</summary>
    None,

    /// <summary>Brigo's cascading calibration algorithm</summary>
    Cascading,

    /// <summary>Piecewise FitTime</summary>
    PiecewiseFitTime,

    /// <summary>Piecewise FitLength</summary>
    PiecewiseFitLength,

    /// <summary>Piecewise Quadratic</summary>
    PiecewiseQuadratic,

    /// <summary>Piecewise Constant</summary>
    PiecewiseConstant,

    /// <summary>Modified cascading calibration algorithm with maximum error control.</summary>
    IterativeCascading,
  }
}
