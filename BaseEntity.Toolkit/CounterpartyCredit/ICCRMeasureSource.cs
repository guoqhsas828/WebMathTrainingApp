/*
 * ICCRMeasureSource.cs
 *
 *   2010. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Common interface for various CCR objects that can compute CCRMeasures
  /// </summary>
  public interface ICCRMeasureSource
  {
    /// <summary>
    /// Compute the specified measure
    /// </summary>
    /// <param name="measure">the CCRMeasure</param>
    /// <param name="date">the date. Ignored if not applicable to this measure</param>
    /// <param name="ci">the confidence interval 1.0 = 100%. Ignored if not applicable to this measure</param>
    /// <exception cref="NotSupportedException">may be thrown for some CCRMeasure values depending on underlying model implementation</exception>
    double GetMeasure(CCRMeasure measure, Dt date, double ci);


    /// <summary>
    /// Compute the specified measure, and marginally allocate by trade
    /// </summary>
    /// <param name="measure">the CCRMeasure</param>
    /// <param name="date">the date. Ignored if not applicable to this measure</param>
    /// <param name="ci">the confidence interval 1.0 = 100%. Ignored if not applicable to this measure</param>
    /// <exception cref="NotSupportedException">may be thrown for some CCRMeasure values depending on underlying model implementation</exception>
    double[] GetMeasureMarginal(CCRMeasure measure, Dt date, double ci);
  }



}