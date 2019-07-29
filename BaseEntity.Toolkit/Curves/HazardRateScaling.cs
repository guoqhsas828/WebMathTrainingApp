/*
 * HazardRateScaling.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using CurvePoint = BaseEntity.Toolkit.Base.DateAndValue<double>;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Helper class to bump on survival hazard rates directly
  /// </summary>
  internal class HazardRateScaling : SurvivalCurveScaling
  {
    #region Constructors    
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="curveToScale">The curve to scale, modified on output</param>
    /// <param name="curvePoints">
    ///   Optional curve points to replace all the curve points inside the curve
    /// </param>
    /// <param name="lastStartIndex">Last start date</param>
    internal HazardRateScaling(SurvivalCurve curveToScale, CurvePoint[] curvePoints, int lastStartIndex)
      : base(curveToScale, curvePoints, lastStartIndex)
    {
    }

    internal HazardRateScaling(SurvivalCurve curveNotToScale) : base(curveNotToScale) { }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Bump on spot hazard rates
    /// </summary>
    /// <param name="x">Size to bump</param>
    /// <param name="relative">Relative or absolute</param>
    /// <param name="refitSpread">This parameter is not used.</param>
    internal override void Bump(double x, bool relative, bool refitSpread)
    {
      if (CurvePoints == null || CurvePoints.Length == 0)
        return;
      Dt asOf = ScaledCurve.AsOf;
      for (int i = StartIndex; i < EndIndex; ++i)
      {
        double hT = -Math.Log(CurvePoints[i].Value);
        double dhT;
        if (relative)
          dhT = hT * x;
        else
        {
          double T = Dt.Fraction(asOf, ScaledCurve.GetDt(i), DayCount.Actual365Fixed);
          dhT = T * x;
        }
        ScaledCurve.SetVal(i, Math.Exp(-(hT + dhT)));
      }
      return;
    }

    #endregion Methods
  }

}
