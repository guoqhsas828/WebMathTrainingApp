/*
 * CurveWithMeasure.cs
 *
 *  -2010. All rights reserved.
 *
 */
using System;

using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  ///   Curve with measure.
  /// </summary>
  /// <remarks>
  ///   <para>A regular discount curve normalizes the zero price (discount factor)
  ///   to be one at the as-of date.
  ///   In BGM models, sometimes an different measure such as the terminal measure
  ///   is used, where the zero price (discount factor) at the terminal date is 
  ///   normalized to be one.
  ///   The <c>Measure</c> property of this class is used to convert the
  ///   regular as-of based discount factors into the proper measures.</para>
  /// 
  ///  <para>Mathematically speaking, let <m>X(t)</m> be the price of the numeraire asset.
  ///  Then the measure is defined as <m>X(0)/X(t)</m> as in the change of measure formula
  ///  </para>
  /// </remarks>
  /// <typeparam name="T">CalibratedCurve type, normally the DiscountCurve.</typeparam>
  [Serializable]
  public class CurveWithMeasure<T> where T : CalibratedCurve
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CurveWithMeasure&lt;T&gt;"/> class.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="measure">The measure.</param>
    public CurveWithMeasure(T curve, double measure)
    {
      curve_ = curve;
      measure_ = measure;
    }

    /// <summary>
    ///   Get the curve.
    /// </summary>
    public T Curve { get { return curve_; } }


    /// <summary>
    ///   Get the measure.
    /// </summary>
    public double Measure { get { return measure_; } }

    private T curve_;
    private double measure_;
  }
}