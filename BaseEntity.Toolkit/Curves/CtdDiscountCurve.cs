//
// CtdDiscountCurve.cs
// 
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///  Cheapest-to-deliver discount curve functionality for multi-currency CSAs
  /// </summary>
  [Serializable]
  public class CtdDiscountCurve : DiscountCurve
  {
    /// <summary>
    ///  Cheapest-to-deliver discount curve constructor
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="calibrator"></param>
    /// <param name="domDiscountCurve"></param>
    /// <param name="discountCurves"></param>
    /// <param name="fxCurves"></param>
    /// <param name="curveDates"></param>
    /// <param name="tenorNames"></param>
    public CtdDiscountCurve(Dt asOf,
      Calibrator calibrator,
      DiscountCurve domDiscountCurve,
      IReadOnlyList<DiscountCurve> discountCurves,
      IReadOnlyList<FxCurve> fxCurves,
      Dt[] curveDates,
      string[] tenorNames)
      : base(asOf)
    {
      Ccy = domDiscountCurve.Ccy;
      Calibrator = calibrator;
      DomesticDiscountCurve = domDiscountCurve;
      DiscountCurves = discountCurves.Where(x => x != null).Distinct().ToArray();
      FxCurves = fxCurves.Where(x => x != null).Distinct().ToArray();

      if (tenorNames != null && curveDates.Length != tenorNames.Length)
        throw new ToolkitException($"Number of tenor names not equal to number of curve dates");
      CurveDates = curveDates;
      TenorNames = tenorNames;

      _fxIndices = new int[DiscountCurves.Count()];
      for (var i = 0; i < DiscountCurves.Count(); i++)
      {
        var curveCcy = DiscountCurves[i].Ccy;
        if (curveCcy == Ccy)
          _fxIndices[i] = -1; // local curve
        else if (FxCurves.Select(o => o.Ccy1 == curveCcy || o.Ccy2 == curveCcy).Any())
        {
          for (var j = 0; j < FxCurves.Length; j++)
          {
            if ((FxCurves[j].Ccy1 == curveCcy || FxCurves[j].Ccy2 == curveCcy)
              && (FxCurves[j].Ccy1 == Ccy || FxCurves[j].Ccy2 == Ccy))
              _fxIndices[i] = j;
          }
        }
        else
        {
          throw new Exception($"Fx curve required for currency pair {curveCcy} {Ccy} in cheapest to deliver curve");
        }
      }

      CtdDiscountCurveFitCalibrator.AddCtdCurveYields(this, AsOf, CurveDates, TenorNames);
    }

    /// <summary>
    ///  Fx Curve
    /// </summary>
    /// <param name="curveIndex"></param>
    /// <returns></returns>
    public FxCurve GetFxCurve(int curveIndex)
    {
      return _fxIndices[curveIndex] >= 0 ? FxCurves[_fxIndices[curveIndex]] : null;
    }

    /// <summary>
    ///  Domestic discount curve
    /// </summary>
    public DiscountCurve DomesticDiscountCurve { get; private set; }

    /// <summary>
    ///  Other discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves { get; private set; }

    /// <summary>
    ///  Fx Curves
    /// </summary>
    public FxCurve[] FxCurves { get; private set; }

    /// <summary>
    ///  Curve dates
    /// </summary>
    public Dt[] CurveDates { get; private set; }

    /// <summary>
    ///  Curve dates
    /// </summary>
    public string[] TenorNames { get; private set; }

    /// <summary>
    ///  FX indices
    /// </summary>
    private readonly int[] _fxIndices;
  }
}
