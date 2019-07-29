/*
 *  -2015. All rights reserved.
 */

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  static class FxRateEvaluation
  {
    internal static Evaluable FxRate(FxCurve fxCurve,
      Dt date, Currency fromCcy, Currency toCcy)
    {
      var fdi = fxCurve.FxInterpolator as FxDiscountInterpolator;
      if (fdi != null)
      {
        return GetFxRate(fdi, date,
          fxCurve.SpotFxRate.IsInverse(fromCcy, toCcy));
      }

      //TODO: handle triangulation and pure forward curve.
      // For those curve we don't know how to handle, just return
      // the expression which call interpolate directly.
      return Evaluable.MakeForwardFxRate(fxCurve, date, fromCcy, toCcy);
    }

    private static Evaluable GetFxRate(FxDiscountInterpolator fdi,
      Dt date, bool inverse)
    {
      var spot = Evaluable.SpotRate(fdi.SpotFxRate);
      var foreignDiscount = fdi.ForeignDiscount;
      var domesticDiscount = fdi.DomesticDiscount;
      var basisCurve = fdi.BasisCurve;
      if (inverse && basisCurve != null)
      {
        var cal = basisCurve.Calibrator as FxBasisFitCalibrator;
        if (cal != null && cal.InverseFxBasisCurve != null)
        {
          return Df(cal.InverseFxBasisCurve, spot, date) / spot
            * Df(domesticDiscount, spot, date) / Df(foreignDiscount, spot, date);
        }
      }
      var s = spot * Df(basisCurve, spot, date)
        * Df(foreignDiscount, spot, date) / Df(domesticDiscount, spot, date);
      return inverse ? (1 / s) : s;
    }

    private static Evaluable Df(Curve curve, Evaluable spot, Dt date)
    {
      if (curve == null) return Evaluable.Constant(1.0);
      return Evaluable.Interpolate(curve, date) / Evaluable.Interpolate(curve, spot);
    }
  }
}
