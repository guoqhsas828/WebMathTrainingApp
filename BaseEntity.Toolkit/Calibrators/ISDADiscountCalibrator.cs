//
//   2015. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using System.Collections.Generic;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// 
  /// </summary>
  public class ISDADiscountCalibrator : DiscountBootstrapCalibrator
  {
    /// <summary>
    ///   Constructor for ISDA curve constructor
    /// </summary>
    /// <param name="asOf"></param>
    public ISDADiscountCalibrator(Dt asOf) : base(asOf, asOf)
    {}

    /// <summary>
    ///   ISDA discount curve fitting
    /// </summary>
    /// <param name="curveName"></param>
    /// <param name="currency"></param>
    /// <param name="category"></param>
    /// <param name="asOfDate">As of date</param>
    /// <param name="spotDate">Curve spot date</param>
    /// <param name="targetRate">Target index</param>
    /// <param name="targetIndexTenor">Target index tenor</param>
    /// <param name="curveQuotes">Collection of curve quotes</param>
    /// <param name="interp">Curve interp scheme</param>
    /// <param name="swapInterp">Swap rate interp scheme</param>
    /// <returns></returns>
    public static DiscountCurve Fit(
      string curveName,
      Currency currency,
      string category,
      Dt asOfDate,
      Dt spotDate,
      ISDAInterestReferenceRate targetRate,
      Tenor targetIndexTenor,
      IEnumerable<CurveTenor> curveQuotes,
      Interp interp,
      Interp swapInterp
      )
    {
      var targetIndex = new InterestRateIndex(targetRate);
      var calibrator = new DiscountBootstrapCalibrator(spotDate, spotDate)
      {
        SwapInterp = swapInterp,
        SwapCalibrationMethod = SwapCalibrationMethod.Solve
      };

      var curve = new DiscountCurve(calibrator)
      {
        Name = curveName,
        Interp = interp,
        Ccy = currency
      };

      var tenors = curve.Tenors = new CurveTenorCollection();
      foreach (var tenor in curveQuotes)
      {
        if (tenor == null) continue;
        tenor.UpdateProduct(asOfDate);
        if (!MatchIndex(tenor, targetIndex)) continue;
        tenors.Add(tenor);
      }

      curve.Fit();
      return curve;
    }

    private static bool MatchIndex(CurveTenor tenor, ReferenceIndex targetIndex)
    {
      if (tenor.Product.Ccy != targetIndex.Currency)
        return false;
      var index = tenor.ReferenceIndex;
      if (index == null)
        return true; // Empty means match any
      return (tenor.Product is Swap) || targetIndex.IsEqual(index);
    }
  }
}
