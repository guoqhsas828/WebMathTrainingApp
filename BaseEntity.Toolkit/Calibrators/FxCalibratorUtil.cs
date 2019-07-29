/*
 *  -2012. All rights reserved.
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Utility class for FX Curve calibration
  /// </summary>
  class FxCalibratorUtil
  {
    /// <summary>
    /// Construct list of instruments to calibrate a FX curve
    /// </summary>
    /// <param name="asOf">AsOf (trade/pricing/horizon) date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="ccy1">Base (domestic/source/from) currency</param>
    /// <param name="ccy2">Quoting (foreign/destination/to) currency</param>
    /// <param name="fxRate">Spot fx rate for one unit of ccy1 in terms of ccy2 (ccy1/ccy2)</param>
    /// <param name="instruments">Type of financial instrument matching market quote (default is fx forward)</param>
    /// <param name="tenors">Tenor of product matching market quotes</param>
    /// <param name="dates">Dates matching market quotes or null to calculate from tenor, settle, dateCal, and dateRoll</param>
    /// <param name="quotes">Market quotes for the calibration</param>
    /// <param name="dateCal">Calendar for maturity date determination</param>
    /// <param name="dateRoll">Business day convention for maturity date determination</param>
    /// <param name="spreadOnCcy2Leg">True if the basis swap spread is paid by the ccy2 side swap leg (default is the non-USD, then non-EUR leg)</param>
    /// <param name="ccy1RefIndex">The ccy1 (base/foreign/source/from) reference index</param>
    /// <param name="ccy2RefIndex">The ccy2 (quoting/domestic/destination/to) reference index</param>
    /// <param name="ccy1Freq">The ccy1 (base/foreign/source/from) floating rate payment frequency</param>
    /// <param name="ccy2Freq">The ccy2 (quoting/domestic/destination/to) floating rate payment frequency</param>
    /// <param name="instrumentsToCalibrate">Returned list of instruments to calibrate</param>
    /// <param name="instrumentsPv">Returned list of calibration target pvs</param>
    /// <returns>None</returns>
    public static void FxCurveInstruments(
      Dt asOf,
      Dt settle,
      Currency ccy1,
      Currency ccy2,
      double fxRate,
      string[] instruments,
      string[] tenors,
      Dt[] dates,
      double[] quotes,
      Calendar dateCal,
      BDConvention dateRoll,
      bool spreadOnCcy2Leg,
      Frequency ccy1Freq,
      Frequency ccy2Freq,
      ReferenceIndex ccy1RefIndex,
      ReferenceIndex ccy2RefIndex,
      List<Product> instrumentsToCalibrate,
      List<double> instrumentsPv
      )
    {
      if( tenors.Length == 0 && dates.Length == 0 )
        throw new ArgumentException("Either tenors or maturities must be specified");
      for (int i = 0; i < quotes.Length; i++)
      {
        if (Math.Abs(quotes[i]) > 1e-8)
        {
          Dt maturity = (dates.Length != 0) ? dates[i] : Dt.Roll(Dt.Add(settle, Tenor.Parse(tenors[i])), dateRoll, dateCal);
          string tenor = (tenors.Length != 0) ? tenors[i] : String.Format("{0}", dates[i]);
          string instrument = (instruments.Length > 1) ? instruments[i] : (instruments.Length == 1) ? instruments[0] : "FxFwd";
          if (String.Compare(instrument, "FxFwd", StringComparison.OrdinalIgnoreCase) == 0 ||
              String.Compare(instrument, "Fwd", StringComparison.OrdinalIgnoreCase) == 0)
          {
            // Fx forward
            string tenorName = ccy1 + "/" + ccy2 + "." + tenor;
            var fwd = new FxForward(maturity, ccy1, ccy2, quotes[i]) { Description = tenorName };
            fwd.Validate();
            instrumentsToCalibrate.Add(fwd);
            instrumentsPv.Add(0.0);
          }
          else if (String.Compare(instrument, "BasisSwap", StringComparison.OrdinalIgnoreCase) == 0 ||
                   String.Compare(instrument, "Basis", StringComparison.OrdinalIgnoreCase) == 0)
          {
            // fx basis swap
            string tenorName = ccy2RefIndex.IndexName + "." + ccy1RefIndex.IndexName + "." + "BasisSwap_" + tenor;
            var swap = new Swap(settle, maturity, quotes[i], ccy1Freq, ccy2Freq,
              ccy1RefIndex, ccy2RefIndex, spreadOnCcy2Leg, true) { Description = tenorName };
            swap.Validate();
            instrumentsToCalibrate.Add(swap);
            instrumentsPv.Add(0.0);
          }
          else
            throw new ArgumentException(String.Format("Invalid instrument [{0}]", instrument));
        }
      }
    }
  }
}
