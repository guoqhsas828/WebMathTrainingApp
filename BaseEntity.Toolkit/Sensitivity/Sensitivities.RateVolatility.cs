/*
 * Sensitivities.RateVolatility.cs
 *
 *   2005-2010. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BGM;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary> 
  /// </summary>
  // Methods for calculating generalized sensitivity measures
  public static partial class Sensitivities
  {
    #region Cap/Floor Specific Vegas

    /// <summary>
    /// Calculates the Vega sensitivity for a Cap/Floor
    /// </summary>
    /// <param name="pricers"></param>
    /// <param name="hedgestrikes"></param>
    /// <param name="hedgeTenors"></param>
    /// <param name="measure"></param>
    /// <param name="method"></param>
    /// <param name="calcHedge"></param>
    /// <param name="calcVanna"></param>
    /// <param name="calcVolga"></param>
    /// <param name="numHedgers"></param>
    /// <returns></returns>
    public static DataTable VegaCapFloor(
      CapFloorPricerBase[] pricers,
      double[] hedgestrikes,
      Tenor[] hedgeTenors,
      CapVegaMeasure measure,
      VegaAllocationMethod method,
      bool calcHedge,
      bool calcVanna,
      bool calcVolga,
      int numHedgers)
    {
      DataTable result = null;
      foreach (var p in pricers)
      {
        // Reverse bootstrap into market instruments
        var pricerResults = CapFloorVegaSensitivity.VegaSensitivities(hedgestrikes, hedgeTenors, p, measure, method,
          calcHedge, numHedgers, calcVanna, calcVolga);
        
        // Merge
        if(result == null)
          result = pricerResults;
        else
          result.Merge(pricerResults);
      }
      // Done
      return result ?? new DataTable();
    }

    ///<summary>
    ///</summary>
    ///<param name="pricers"></param>
    ///<param name="hedgeStrikes"></param>
    ///<param name="hedgeTenors"></param>
    ///<param name="measure"></param>
    ///<param name="method"></param>
    ///<param name="calcHedge"></param>
    ///<param name="calcVanna"></param>
    ///<param name="calcVolga"></param>
    ///<param name="numHedgers"></param>
    ///<returns></returns>
    public static DataTable VegaCapFloor(
      IPricer[] pricers,
      double[] hedgeStrikes,
      Tenor[] hedgeTenors,
      CapVegaMeasure measure,
      VegaAllocationMethod method,
      bool calcHedge,
      bool calcVanna,
      bool calcVolga,
      int numHedgers)
    {
      var inputPricers = new List<IPricer>();
      foreach (IPricer inputPricer in pricers)
      {
        if (inputPricer == null)
          continue;
        if (inputPricer is SwaptionBlackPricer || inputPricer is CapFloorPricerBase)
        {
          inputPricers.Add(inputPricer);
          //pricerVegas.Add(((SwaptionBlackPricer)inputPricer).Vega());
        }
        else if (inputPricer is SwapBermudanBgmTreePricer)
        {
          var bgmTreePricer = inputPricer as SwapBermudanBgmTreePricer;
          double[] vegas = bgmTreePricer.CalcCoTerminalSwaptionVegas(0.0001);
          foreach (double vega in vegas)
          {
            //pricerVegas.Add(vega * 100.0);
          }

          foreach (SwaptionBlackPricer pricer in bgmTreePricer.CoTerminalSwaptionPricers())
          {
            inputPricers.Add(pricer);
          }
        }
        else
          throw new ArgumentException(
            "Only SwaptionBlackPricer, SwapBermudanBgmTreePricer and CapFloorPricer accepted for rate volatility sensitivitity calculation");
      }

      var convertedPricers = new CapFloorPricerBase[pricers.Length];
      for (int i = 0; i < inputPricers.Count; i++)
      {
        if (inputPricers[i] is CapFloorPricerBase)
        {
          convertedPricers[i] = (CapFloorPricerBase)inputPricers[i];
          continue;
        }

        var swaptionPricer = (SwaptionBlackPricer)inputPricers[i];
        var swaptionVega = swaptionPricer.Vega(0.01);
        if (!(swaptionPricer.VolatilityObject is SwaptionVolatilityCube && (((SwaptionVolatilityCube)swaptionPricer.VolatilityObject).RateVolatilityCalibrator is RateVolatilityCapFloorBasisAdjustCalibrator)))
          throw new ArgumentException("Basis adjustment on Cap/Floor vol cube is required for Cap/Floor hedging");

        convertedPricers[i] = RateVolatilityUtil.CreateEquivalentCapFloorPricer(swaptionPricer);
        var capFloorVega = convertedPricers[i].Vega();
        if (capFloorVega != 0.0)
          convertedPricers[i].Notional *= swaptionVega / capFloorVega;
      }
      var dt = Sensitivities.VegaCapFloor(convertedPricers, hedgeStrikes, hedgeTenors, measure,
                                      method, calcHedge, calcVanna, calcVolga, numHedgers);
      return dt;
    }

    ///<summary>
    /// Calculate the vega sensitivity for swaptions
    ///</summary>
    ///<param name="pricers">Swaption pricers</param>
    ///<param name="hedgeExpiries">List of expiry tenors of hedging swaptions</param>
    ///<param name="hedgeTenors">List of forward tenors of hedging swaptions</param>
    ///<param name="measure">Vega measure</param>
    ///<param name="method">Allocation method</param>
    ///<param name="calcHedge">Calculate hedge</param>
    ///<param name="vegas">The vega sensitivity of each pricers</param>
    ///<returns>Summary data for vega sensitivity</returns>
    public static DataTable VegaSwaption(SwaptionBlackPricer[] pricers, string[] hedgeExpiries, string[] hedgeTenors, CapVegaMeasure measure,
      VegaAllocationMethod method, bool calcHedge, double[] vegas)
    {
      DataTable result = null;
      for (var i = 0; i < pricers.Length; i++)
      {
        var fwdTenors = ArrayUtil.Convert(hedgeTenors, Tenor.Parse).ToArray();
        var pricerResults = SwaptionVegaSensitivity.VegaSensitivities(hedgeExpiries, fwdTenors, pricers[i], measure, method,
                                                               calcHedge, vegas[i]);
        // Merge
        if (result == null)
          result = pricerResults;
        else
          result.Merge(pricerResults);
      }

      // Done
      return result ?? new DataTable();
    }

    /// <summary>
    /// Calculates the Vega sensitivity for a Cap/Floor
    /// </summary>
    /// <param name="pricers"></param>
    /// <param name="hedgestrikes"></param>
    /// <param name="hedgeTenors"></param>
    /// <param name="measure"></param>
    /// <param name="method"></param>
    /// <param name="calcHedge"></param>
    /// <param name="calcVanna"></param>
    /// <param name="calcVolga"></param>
    /// <param name="thresHold"></param>
    /// <returns></returns>
    public static DataTable VegaCapFloor(
      CapFloorPricerBase[] pricers,
      double[] hedgestrikes,
      Tenor[] hedgeTenors,
      CapVegaMeasure measure,
      VegaAllocationMethod method,
      bool calcHedge,
      bool calcVanna,
      bool calcVolga,
      double thresHold)
    {
      DataTable result = null;
      for (int i = 0; i < pricers.Length; i++)
      {
        // Reverse bootstrap into market instruments
        var pricerResults = CapFloorVegaSensitivity.VegaSensitivities(hedgestrikes, hedgeTenors, pricers[i], measure, method, 
                                                               calcHedge, thresHold, calcVanna, calcVolga);

        // Merge
        if (result == null)
          result = pricerResults;
        else
          result.Merge(pricerResults);
      }

      // Done
      return result;
    }

    #endregion
  }
}
