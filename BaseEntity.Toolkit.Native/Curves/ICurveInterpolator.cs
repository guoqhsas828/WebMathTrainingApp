/*
 * ICurveInterpolator.cs
 *
 * Copyright (c)   2002-2011. All rights reserved.
 *
 */

using System;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Interface ICurveInterpolator for implementing custom curve interpolator.
  /// </summary>
  public interface ICurveInterpolator : ICloneable
  {
    /// <summary>
    /// Initializes this interpolator based on the specified curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <remarks>
    ///   This method is called before interpolation whenever the curve points are modified.
    ///   It can be used to prepare any precalculated data for interpolation.
    /// </remarks>
    void Initialize(NativeCurve curve);

    /// <summary>
    /// Evaluates the curve value at the specified time point.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="t">The time in days from the curve as-of date to the interpolation date.</param>
    /// <param name="index">The index of the date intervals where t locates.</param>
    /// <returns>The curve value at t.</returns>
    double Evaluate(NativeCurve curve, double t, int index);
  }
}
