/*
 * SpreadScaling.cs
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
  internal class SpreadScaling : SurvivalCurveScaling
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SpreadScaling));
    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="curveToScale">The curve to scale, modified on output</param>
    /// <param name="curvePoints">
    ///   Optional curve points to replace all the curve points inside the curve
    /// </param>
    /// <param name="lastStartIndex">Last start index</param>
    internal SpreadScaling(SurvivalCurve curveToScale, CurvePoint[] curvePoints, int lastStartIndex)
      : base(curveToScale, curvePoints, lastStartIndex)
    {
      savedQuotes_ = ArrayUtil.Generate<double>(curveToScale.Count,
        delegate(int i) { return CurveUtil.MarketQuote(curveToScale.Tenors[i]); });
      //tenors_ = ScaledCurve.Tenors;
      //ScaledCurve.Tenors = new CurveTenorCollection();
    }

    internal SpreadScaling(SurvivalCurve curveNotToScale) : base(curveNotToScale) { }
    private double[] savedQuotes_;
    //private CurveTenorCollection tenors_;
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Bump on spreads
    /// </summary>
    /// <param name="x">Size to bump</param>
    /// <param name="relative">Relative or absolute</param>
    /// <param name="refitSpread">
    ///   If false, only the quotes are bumped, but curve is not fit;
    ///   If true, fit the curve after bumping.
    /// </param>
    internal override void Bump(double x, bool relative, bool refitSpread)
    {
      if (CurvePoints == null || CurvePoints.Length == 0)
        return;
      SurvivalCurve scaledCurve = ScaledCurve;
      BumpFlags flags = relative ? BumpFlags.BumpRelative : 0;
      for (int i = StartIndex; i < EndIndex; ++i)
      {
        CurveUtil.SetMarketQuote(scaledCurve.Tenors[i], savedQuotes_[i]);
        scaledCurve.Tenors[i].BumpQuote(x, flags);
      }
      if (refitSpread) scaledCurve.ReFit(StartIndex);
      return;
    }

    #endregion Methods
  }

}
