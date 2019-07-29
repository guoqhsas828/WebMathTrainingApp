/*
 * SwaptionVegaSensitivity.cs
 *
 *   2005-2011. All rights reserved.
 * 
 */

using System;
using System.Data;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// Utility class to calculate vega sensitivity and hedges for swaption
  /// </summary>
  public static class SwaptionVegaSensitivity
  {

    #region public methods 

    /// <summary>
    /// Creates the Vega Sensitiviites Data Set
    /// </summary>
    /// <param name="expiryList">List of hedge instruments expiry</param>
    /// <param name="tenorList">List of hedge instruments forward tenor</param>
    /// <param name="pricer">Swaption pricer</param>
    /// <param name="measure">Vega measure</param>
    /// <param name="method">Vega allocation method</param>
    /// <param name="calcHedge">Calculate hedging amount</param>
    /// <param name="vega">The vega sensitivity to be allocated</param>
    /// <returns>Result summary table</returns>
    public static DataTable VegaSensitivities(string[] expiryList, Tenor[] tenorList, SwaptionBlackPricer pricer,
      CapVegaMeasure measure, VegaAllocationMethod method, bool calcHedge, double vega)
    {
      var expiries = ArrayUtil.Convert(expiryList, expiry => RateVolatilityUtil.SwaptionStandardExpiry(pricer.AsOf, (InterestRateIndex)pricer.ReferenceIndex, Tenor.Parse(expiry))).ToArray();

      var fwdTenors =
        ArrayUtil.Convert(tenorList,
                          hedgeTenor => SwaptionVolatilityCube.ConvertForwardTenor(hedgeTenor)).ToArray();
      var vegaExposures = BucketSwaptionExposures(pricer, fwdTenors, expiries, vega, method);
      double[,] hedgeNotionals = null;

      if (calcHedge)
      {
        hedgeNotionals = CalculateSwaptionHedgeNotionals(vegaExposures, tenorList, expiries, pricer, 
          GetVegaCalculatorDelegate(pricer,Enum.GetName(typeof (CapVegaMeasure), measure)));

      }

      DataTable dt = ToDataTable(pricer, vegaExposures, hedgeNotionals, tenorList, expiryList);

      return dt;
    }

    #endregion 

    #region internal methods 

    /// <summary>
    /// Buckets the volatility exposures allocated the adjacent points
    /// </summary>
    /// <param name="pricer">Swaption pricer</param>
    /// <param name="fwdTenors">Forward tenor list</param>
    /// <param name="expiries">Expiry list</param>
    /// <param name="vegaAllocationMethod">Allocation method of vega risk</param>
    /// <param name="vega">Vega sensitivity of the pricer</param>
    /// <returns>Allocation of vega risk on ATM surface</returns>
    internal static double[,] BucketSwaptionExposures(SwaptionBlackPricer pricer, double[] fwdTenors, Dt[] expiries, double vega,
      VegaAllocationMethod vegaAllocationMethod)
    {
      VolatilityLookupBucket expiryBucket;
      VolatilityLookupBucket tenorBucket;
      var dd = RateVolatilityUtil.EffectiveSwaptionDuration(pricer);
      Dt expiry = pricer.Swaption.GetExpiration(dd.Date);
      double effectiveDuration = dd.Value;
      FindSwaptionHedgeBuckets(expiry,
                               effectiveDuration,
                               expiries, fwdTenors, out expiryBucket, out tenorBucket);
      var atmSurfaceExposures = new double[expiries.Length, fwdTenors.Length];
      AllocateVegaIntoSwaptionBuckets(pricer.AsOf,expiry, effectiveDuration, fwdTenors, expiries,atmSurfaceExposures, expiryBucket, tenorBucket , vegaAllocationMethod, vega);
      return atmSurfaceExposures;
    }

    internal static void FindSwaptionHedgeBuckets(Dt expiry, double duration, Dt[] hedgeExpiries, double[] fwdTenors, out VolatilityLookupBucket expiryBucket, out VolatilityLookupBucket tenorBucket)
    {
      expiryBucket = VolatilityLookupBucket.FindBucket(hedgeExpiries, expiry);
      tenorBucket = VolatilityLookupBucket.FindBucket(fwdTenors, duration);
    }

    internal static Func<SwaptionBlackPricer ,Dt, Tenor, double> GetVegaCalculatorDelegate(SwaptionBlackPricer pricer, string funcName)
    {
      switch (funcName)
      {
        case "Vega":
          {
            return SwaptionBlackPricer.SwaptionBumpVega;
          }
        default:
          {
            throw new ArgumentException("Invalid swaption vega sensitivity method");
          }
      }
    }
    #endregion 

    #region private methods


    private static void AllocateVegaIntoSwaptionBuckets(Dt asOf, Dt expiry, double effectiveDuration, double[] fwdTenors, 
      Dt[] expiries, double[,] result, VolatilityLookupBucket expiryBucket, VolatilityLookupBucket tenorBucket, 
      VegaAllocationMethod method, double vega)
    {

      int rowIdx = Array.FindIndex(expiries, i => (i >= expiry ? true : false));
      rowIdx = (rowIdx == -1) ? ((expiries[expiries.Length - 1] < expiry) ? expiries.Length - 1 : 0) : rowIdx;

      //search for the first strike that is just greater than the given strike
      int colIdx = Array.FindIndex(fwdTenors, i => (i >= effectiveDuration ? true : false));
      colIdx = (colIdx == -1) ? ((fwdTenors[fwdTenors.Length - 1] == effectiveDuration) ? fwdTenors.Length - 1 : 0) : colIdx;

      //first check if we got an exactmatch or a bracket
      if ((expiryBucket.ExactMatchFound || expiryBucket.IsExtrapolationPoint) && (tenorBucket.ExactMatchFound || tenorBucket.IsExtrapolationPoint))
      {
        result[rowIdx, colIdx] += vega;
      }
      else if (expiryBucket.ExactMatchFound || expiryBucket.IsExtrapolationPoint)
      {
        var tenor1 = fwdTenors[tenorBucket.StartPoint];
        var tenor2 = fwdTenors[tenorBucket.EndPoint];
         
        double wt1, wt2;
        CalculateBracketWeights(vega, vega, vega, effectiveDuration,tenor1, tenor2, method, out wt1, out wt2);
        result[rowIdx, colIdx - 1] += wt1 * vega;
        result[rowIdx, colIdx] += wt2 * vega;
      }
      else if (tenorBucket.ExactMatchFound || tenorBucket.IsExtrapolationPoint)
      {
        var expiry1 = Dt.Diff(asOf, expiries[expiryBucket.StartPoint])/365.0;

        var expiry2 = Dt.Diff(asOf, expiries[expiryBucket.EndPoint])/365.0;
        double wt1, wt2;
        CalculateBracketWeights(vega, vega, vega, Dt.Diff(asOf, expiry)/365.0, expiry1, expiry2, method, out wt1, out wt2);
        result[rowIdx-1, colIdx] += wt1 * vega;
        result[rowIdx, colIdx] += wt2 * vega;
      }
      else
      {
        var vegaLL = vega;
        var vegaLU = vega;
        var vegaUL = vega;
        var vegaUU = vega;
        var expiryL = Dt.Diff(asOf, expiries[expiryBucket.StartPoint])/365.0;
        var expiryU = Dt.Diff(asOf, expiries[expiryBucket.EndPoint])/365.0;
        var tenorL = fwdTenors[tenorBucket.StartPoint];
        var tenorU = fwdTenors[tenorBucket.EndPoint];

        double we1,we2, wt1,wt2;
        CalculateBracketWeights(vega, vegaLL, vegaUL, Dt.Diff(asOf, expiry)/365.0, expiryL,
                                expiryU, method, out we1, out we2);
        CalculateBracketWeights(vega, vegaUL, vegaUU, effectiveDuration, tenorL,
                                tenorU, method, out wt1, out wt2);
        result[rowIdx - 1, colIdx - 1] += we1*wt1*vegaLL;
        result[rowIdx - 1, colIdx] += we1*wt2*vegaLU;
        result[rowIdx, colIdx - 1] += we2*wt1*vegaUL;
        result[rowIdx, colIdx] = we2*wt2*vegaUU;
      }
    }


    private static DataTable ToDataTable(PricerBase pricer, double[,] vegaExposures, double[,] hedgeNotionals, Tenor[] hedgeTenors, string[] expiries)
    {
      // Setup table
      var result = new DataTable("VolatilitySensitivity");
      result.Columns.Add("Pricer", typeof(string));
      result.Columns.Add("Expiry", typeof(string));
      result.Columns.Add("Tenor", typeof(string));
      result.Columns.Add("Vega", typeof(double));
      if (hedgeNotionals != null)
        result.Columns.Add("HedgeNotional", typeof(double));
      // Add rows
      for (int j = 0; j < hedgeTenors.Length; j++)
      {
        for (int i = 0; i < expiries.Length; i++)
        {
          DataRow row = result.NewRow();
          row["Pricer"] = pricer.Product.Description;
          row["Expiry"] = expiries[i];
          row["Tenor"] =  hedgeTenors[j].ToString("S", null);
          row["Vega"] = vegaExposures[i, j];
          if (hedgeNotionals != null)
            row["HedgeNotional"] = hedgeNotionals[i, j];
          result.Rows.Add(row);
        }
      }

      // Done
      return result;
    }

    private static double[,] CalculateSwaptionHedgeNotionals(double[,] vegaExposures, Tenor[] tenors, Dt[] expiries, SwaptionBlackPricer pricer,
      Func<SwaptionBlackPricer, Dt, Tenor, double> vegaFn )
    {
      var result = new double[expiries.Length, tenors.Length];

      int rows = vegaExposures.GetUpperBound(0) + 1;
      int cols = vegaExposures.GetUpperBound(1) + 1;
      if (rows != expiries.Length || cols != tenors.Length)
        throw new ToolkitException("Vega exposures and hedge instruments do not match on size");

      //Find the nearest Buckets on the strike dimension 
      for (int i = 0; i < cols; i++)
      {
        for (int j = 0; j < rows; j++)
        {
          if (vegaExposures[j, i] == 0.0)
          {
            result[j, i] = 0.0;
          }
          else
          {
            var hedgeVega = vegaFn(pricer, expiries[j], tenors[i]);
            result[j, i] = vegaExposures[j, i]*pricer.Notional/hedgeVega;
          }
        }
      }
      return result; 
    }

    /// <summary>
    /// Calculates the bracket weights.
    /// </summary>
    /// <param name="vega">The swaption vega.</param>
    /// <param name="vegaL">The vega at low point.</param>
    /// <param name="vegaU">The vega at high point.</param>
    /// <param name="x">The swaption measure to be interpolated on.</param>
    /// <param name="xL">The measure at low point.</param>
    /// <param name="xU">The measure at high point.</param>
    /// <param name="method">The allocation method.</param>
    /// <param name="wt1">The weight 1.</param>
    /// <param name="wt2">The weight 2.</param>
    private static void CalculateBracketWeights(double vega, double vegaL, double vegaU, double x, double xL, double xU, VegaAllocationMethod method, out double wt1, out double wt2)
    {

      switch (method)
      {
        case VegaAllocationMethod.Weighted:
          {
            if (vegaU != 0.0)
            {
              double scaleRatio = (xU - x) / (xU - xL);
              wt1 = (scaleRatio * vega) / (vegaL * scaleRatio + vegaU * (1 - scaleRatio));
              wt2 = ((1 - scaleRatio) * vega) / (scaleRatio * vegaL + vegaU * (1 - scaleRatio));
            }
            else
            {
              wt1 = 1.0;
              wt2 = 0.0;
            }
            break;

          }
        case VegaAllocationMethod.Flat:
          {
            if (vegaU != 0.0)
            {
              wt1 = 0.0;
              wt2 = vega / vegaU;
            }
            else
            {
              wt1 = 0.0;
              wt2 = 1.0;
            }

            break;
          }
        default:
          {
            throw new ArgumentException("Invalid Vega allocation method ");
          }
      }

    }

    #endregion


  }
}