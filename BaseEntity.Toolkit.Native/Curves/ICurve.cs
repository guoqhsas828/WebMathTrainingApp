using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Interface IFactorCurve
  /// </summary>
  public interface IFactorCurve
  {
    /// <summary>
    /// Interpolates the factor value from the specified begin date to the end date.
    /// </summary>
    /// <param name="begin">The begin date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>System.Double.</returns>
    double Interpolate(Dt begin, Dt end);
  }

  /// <summary>
  /// Interface ISpotCurve
  /// </summary>
  public interface ISpotCurve
  {
    /// <summary>
    /// Interpolates the value on the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>System.Double.</returns>
    double Interpolate(Dt date);
  }
}
