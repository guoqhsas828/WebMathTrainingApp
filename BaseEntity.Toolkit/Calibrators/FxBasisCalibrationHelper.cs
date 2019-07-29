using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Utility class to support backward-compatible rate curve in new fx curve calibration interface
  /// </summary>
  public static class FxBasisCalibrationHelper
  {
    /// <summary>
    /// Helper function for calibrating fx curve, in support for backward-compatible rate curves
    /// </summary>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="spot">Spot fx date</param>
    /// <param name="ccy1">Ccy1 (base/foreign/source/from) currency</param>
    /// <param name="ccy2">Ccy2 (quoting/domestic/destination/to) currency</param>
    /// <param name="spotFxRate">Spot fx rate for one unit of ccy1 in terms of ccy2 (ccy1/ccy2)</param>
    /// <param name="fwdTenors">Tenor of fx fwd quotes</param>
    /// <param name="fwdDates">Dates of fx fwd quotes</param>
    /// <param name="fwdQuotes">Fx fwd quotes</param>
    /// <param name="swapTenors">Tenor of basis swap quotes</param>
    /// <param name="swapDates">Dates of basis swap quotes</param>
    /// <param name="swapQuotes">Basis swap quotes</param>
    /// <param name="swapCal">Calendar for basis swap settlement (default is calendar for each currency and LNB)</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <param name="ccy1DiscountCurve">The ccy1 (base/foreign/source/from) discount or projection curve</param>
    /// <param name="ccy2DiscountCurve">The ccy2 (quoting/domestic/destination/to) discount or projection curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters or null for defaults</param>
    /// <param name="curveName">Name of the curve.</param>
    /// <param name="foreignIndex">Foreign ccy rate index, only needed for backward-compatible foreign rate curve </param>
    /// <param name="domesticIndex">Discount ccy rate index, only needed for backward-compatible domestic rate curve </param>
    /// <returns>Fx forward curve created</returns>
    public static FxCurve FxCurveCreate(
      Dt asOf, Dt spot, Currency ccy1, Currency ccy2, double spotFxRate,
      string[] fwdTenors, Dt[] fwdDates, double[] fwdQuotes,
      string[] swapTenors, Dt[] swapDates, double[] swapQuotes,
      Calendar swapCal, BasisSwapSide swapSpreadLeg,
      DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
      CurveFitSettings fitSettings, string curveName,
      ReferenceIndex foreignIndex, ReferenceIndex domesticIndex)
    {
      var fxSpot = new FxRate(asOf, spot, ccy1, ccy2, spotFxRate);
      return FxCurveCreate(fxSpot,fwdTenors, fwdDates, fwdQuotes, swapTenors, 
        swapDates, swapQuotes, swapCal, swapSpreadLeg, ccy1DiscountCurve, 
        ccy2DiscountCurve, fitSettings, curveName, foreignIndex, domesticIndex);
    }



    /// <summary>
    /// Helper function for calibrating fx curve, in support for backward-compatible rate curves
    /// </summary>
    /// <param name="spotFx">Fx rate object</param>
    /// <param name="fwdTenors">Tenor of fx fwd quotes</param>
    /// <param name="fwdDates">Dates of fx fwd quotes</param>
    /// <param name="fwdQuotes">Fx fwd quotes</param>
    /// <param name="swapTenors">Tenor of basis swap quotes</param>
    /// <param name="swapDates">Dates of basis swap quotes</param>
    /// <param name="swapQuotes">Basis swap quotes</param>
    /// <param name="swapCal">Calendar for basis swap settlement (default is calendar for each currency and LNB)</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <param name="ccy1DiscountCurve">The ccy1 (base/foreign/source/from) discount or projection curve</param>
    /// <param name="ccy2DiscountCurve">The ccy2 (quoting/domestic/destination/to) discount or projection curve</param>
    /// <param name="fitSettings">Curve fitting tuning parameters or null for defaults</param>
    /// <param name="curveName">Name of the curve.</param>
    /// <param name="foreignIndex">Foreign ccy rate index, only needed for backward-compatible foreign rate curve </param>
    /// <param name="domesticIndex">Discount ccy rate index, only needed for backward-compatible domestic rate curve </param>
    /// <returns>Fx forward curve created</returns>
    public static FxCurve FxCurveCreate(FxRate spotFx,
      string[] fwdTenors, Dt[] fwdDates, double[] fwdQuotes,
      string[] swapTenors, Dt[] swapDates, double[] swapQuotes,
      Calendar swapCal, BasisSwapSide swapSpreadLeg,
      DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
      CurveFitSettings fitSettings, string curveName,
      ReferenceIndex foreignIndex, ReferenceIndex domesticIndex)
    {
      using (var foreign = new ReferenceIndexBinder(ccy1DiscountCurve, foreignIndex))
      using (var domestic = new ReferenceIndexBinder(ccy2DiscountCurve, domesticIndex))
      {
        return FxCurve.Create(spotFx, fwdTenors, fwdDates, fwdQuotes,
          swapTenors, swapDates, swapQuotes, swapCal, swapSpreadLeg, foreign.Curve,
          domestic.Curve, fitSettings, curveName);
      }
    }

    private class ReferenceIndexBinder : IDisposable
    {
      private readonly DiscountBootstrapCalibrator _calibrator;
      private readonly ReferenceIndex _originalIndex;

      public DiscountCurve Curve { get; private set; }

      public ReferenceIndexBinder(DiscountCurve curve, ReferenceIndex index)
      {
        Curve = curve;
        if (index == null) return;
        var cal = curve.Calibrator as DiscountBootstrapCalibrator;
        if (cal == null) return;
        _calibrator = cal;
        _originalIndex = cal.InterestRateIndex;
        _calibrator.InterestRateIndex = index;
      }

      #region IDisposable Members

      public void Dispose()
      {
        if (_calibrator != null)
          _calibrator.InterestRateIndex = _originalIndex;
      }

      #endregion
    }
  }
}
