//
// ExtrapMethod.cs
// Copyright (c)   2002-2008. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  /// Enumeration of defined extrapolation methods
  /// Custom interpolation extrapolation methods may be
  /// defined outside of this enumeration.
  /// </summary>
  public enum ExtrapMethod
  {
    /// <summary>
    /// Constant
    /// <para>Constant extrapolation holds constant the last known data point.</para>
    /// <para>For curve extrapolation, if the curve <see cref="DayCount"/> is not
    /// <see cref="DayCount.None"/> then the time weighted log
    /// (<formula inline="true">S = log(Y)/T</formula>) of the endpoint
    /// is held constant. This is the equivalent of holding the forward rate constant
    /// in the case of curves like DiscountCurves or the forward hazard rate constant
    /// for SurvivalCurves.</para>
    /// </summary>
    Const,
    /// <summary>
    /// Smooth
    /// <para>Also known as linear extrapolation.</para>
    /// <para>Smooth extrapolation maintains the first derivatve.
    /// This effectively extends a tangent line at the end of the data.</para>
    /// <para>If the two data points nearest the point <formula inline="true">x</formula>
    /// to be extrapolated are <formula inline="true">(x_{k-1},y_{k-1})</formula> and
    /// <formula inline="true">(x_k, y_k)</formula>, linear extrapolation gives the function:</para>
    /// <formula>
    ///   y(x) = y_{k-1} + \frac{x - x_{k-1}}{x_{k}-x_{k-1}}(y_{k} - y_{k-1})
    /// </formula>
    /// <para>which is identical to linear interpolation if
    /// <formula inline="true">x_{k-1} &lt; x &lt; x_k</formula>).</para>
    /// <para>For curve extrapolation, if the curve DayCount is not DayCount.None then
    /// </para>
    /// <para>For curve extrapolation, if the curve <see cref="DayCount"/> is not
    /// <see cref="DayCount.None"/> then the derivative of the time weighted log
    /// (<formula inline="true">S = log(Y)/T</formula>) of the endpoint
    /// is held constant. This is the equivalent of holding the derivative of the forward rate constant
    /// in the case of curves like DiscountCurves or the derivative of the forward hazard rate constant
    /// for SurvivalCurves.</para>
    /// </summary>
    Smooth,
    /// <summary>
    /// None
    /// <para>No extrapolation method</para>
    /// <para>The interpolation formula is applied to the points in the extrapolation range.
    /// When the interpolation formula itself guarantees the flat forward rates, then apply
    /// it to the extrapolation range should yield the constant forward rate.</para>
    /// </summary>
    None,
  }

  /// <summary>
  ///   Extrapolator parameters.
  /// </summary>
  [Serializable]
  public class ExtrapScheme
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExtrapScheme"/> class.
    /// </summary>
    public ExtrapScheme()
    {
      Method = ExtrapMethod.Const;
      MaxValue = Double.MaxValue;
      MinValue = Double.MinValue;
    }

    /// <summary>
    ///  Convert to an Extrap object.
    /// </summary>
    /// <returns></returns>
    public Extrap ToExtrap()
    {
      if (!(MaxValue > Double.MinValue))
        throw new ApplicationException("Extrap.MaxValue must be larger than Double.MinValue ");
      if (!(MinValue < Double.MaxValue))
        throw new ApplicationException("Extrap.MinValue must be less than Double.MaxValue ");
      return ExtrapFactory.FromMethod(Method, MinValue, MaxValue);
    }

    /// <summary>
    ///  Convert to an Extrap object.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="min">The min.</param>
    /// <param name="max">The max.</param>
    /// <returns>An Extrap object.</returns>
    public static Extrap ToExtrap(ExtrapMethod method, double min, double max)
    {
      if (!(max > Double.MinValue))
        throw new ApplicationException("Extrap.Max must be larger than Double.MinValue ");
      if (!(min < Double.MaxValue))
        throw new ApplicationException("Extrap.Min must be less than Double.MaxValue ");
      return ExtrapFactory.FromMethod(method, min, max);
    }

    /// <summary>
    ///  Retrieves the extrapolator parameters from an Extrap object.
    /// </summary>
    /// <param name="extrap">The extrap object.</param>
    /// <returns>Extrap scheme</returns>
    public static ExtrapScheme FromExtrap(Extrap extrap)
    {
      ExtrapScheme es = null;
      if (extrap is Const)
      {
        es = new ExtrapScheme {Method = ExtrapMethod.Const};
      }
      else if (extrap is Smooth)
      {
        Smooth sm = (Smooth) extrap;
        es = new ExtrapScheme
        {
          Method = ExtrapMethod.Smooth,
          MinValue = sm.GetpublicData_Min(),
          MaxValue = sm.GetpublicData_Max()
        };
      }
      else if (extrap != null)
        throw new ArgumentException("Invalid interpolation obj");
      return es;
    }

    /// <summary>
    /// Gets or sets the extrap method.
    /// </summary>
    /// <value>The method.</value>
    public ExtrapMethod Method { get; set; }

    /// <summary>
    /// Gets or sets the min value.
    /// </summary>
    /// <value>The min value.</value>
    public double MinValue { get; set; }

    /// <summary>
    /// Gets or sets the max value.
    /// </summary>
    /// <value>The max value.</value>
    public double MaxValue { get; set; }
  }

}  
