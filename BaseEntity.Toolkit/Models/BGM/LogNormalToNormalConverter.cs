using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  /// Converts a LogNormal Volatility to Normal Volatility and Vice Versa
  /// </summary>
  public static class LogNormalToNormalConverter
  {
    #region Cap/Floor
    /// <summary>
    /// Utility Function to convert caplet volatility from 
    /// a Lognormal vol to a Normal vol and viceversa
    /// </summary>
    /// <param name="forward">Forward rate</param>
    /// <param name="strike">Strike</param>
    /// <param name="tau">Time to maturity</param>
    /// <param name="vol">Vol</param>
    /// <param name="inputVolType">Vol type</param>
    /// <param name="outputVolType">Converted vol type</param>
    /// <returns></returns>
    public static double ConvertCapletVolatility(double forward, double strike, double tau, double vol,
      VolatilityType inputVolType, VolatilityType outputVolType)
    {
      if (inputVolType == outputVolType)
        return vol;

      if ((inputVolType == VolatilityType.Normal) && (outputVolType == VolatilityType.LogNormal))
        return NormalToLogNormal(forward, strike, tau, vol);
      return LogNormalToNormal(forward, strike, tau, vol);
    }

    /// <summary>
    /// Convert cap volatility 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="cap">Underlying cap</param>
    /// <param name="vol">Vol</param>
    /// <param name="resets">Reset</param>
    /// <param name="inputVolType">Input vol type</param>
    /// <param name="outputVolType">Output vol type</param>
    /// <returns>Converted vol</returns>
    public static double ConvertCapVolatility(Dt asOf, Dt settle, DiscountCurve discountCurve, CapBase cap, double vol, IList<RateReset> resets, VolatilityType inputVolType,
      VolatilityType outputVolType)
    {
      if (inputVolType == outputVolType)
        return vol;
      // Calc value
      var cube = new RateVolatilityCube(new RateVolatilityFlatCalibrator(asOf, new[] {asOf},
                                                                         inputVolType, cap.GetRateIndex(),
                                                                         new[] {vol}));
      cube.Fit();
      var capFloorPricer = CapFloorPricer.CreatePricer(cap, asOf, settle, discountCurve, discountCurve, cube);
      CollectionUtil.Add(capFloorPricer.Resets, resets);
      var pv = capFloorPricer.ProductPv();
      // Convert
      if ((inputVolType == VolatilityType.Normal) && (outputVolType == VolatilityType.LogNormal))
        return CapNormalToLogNormal(pv, vol, asOf, settle, discountCurve, cap, resets);
      return CapLogNormalToNormal(pv, vol, asOf, settle, discountCurve, cap, resets);
    }


    private static double CapNormalToLogNormal(double pv, double vol, Dt asOf, Dt settle, DiscountCurve discountCurve, CapBase cap, IList<RateReset> resets)
    {

      //Create a LogNormal Cube 
      var cube =
        new RateVolatilityCube(new RateVolatilityFlatCalibrator(asOf, new[] { asOf },
                                                                      VolatilityType.LogNormal, cap.GetRateIndex(),
                                                                      new[] { vol }));
      cube.Fit();
      //Create a pricer that uses the lognormal cube
      var capFloorPricer = CapFloorPricer.CreatePricer(cap, asOf, settle, discountCurve, discountCurve, cube);
      CollectionUtil.Add(capFloorPricer.Resets, resets);
      //Get the LogNormal Volatility implied by the normal pv
      return capFloorPricer.ImpliedVolatility(pv);
    }

    private static double CapLogNormalToNormal(double pv, double vol, Dt asOf, Dt settle, DiscountCurve discountCurve, CapBase cap, IList<RateReset> resets)
    {
      //Create a Normal Cube
      var cube =
             new RateVolatilityCube(new RateVolatilityFlatCalibrator(asOf, new[] { asOf },
                                                                           VolatilityType.Normal, cap.GetRateIndex(),
                                                                           new[] { vol }));
      cube.Fit();
      //Create a Pricer that uses the Normal Cube
      var capFloorPricer = CapFloorPricer.CreatePricer(cap, asOf, settle, discountCurve, discountCurve,cube);
      CollectionUtil.Add(capFloorPricer.Resets, resets);
      return capFloorPricer.ImpliedVolatility(pv);
    }

    internal static double NormalToLogNormal(double forward, double strike, double tau, double vol)
    { 
      double pv = BlackNormal.P(OptionType.Call, tau, 0.0, forward, strike, vol);
      return Black.ImpliedVolatility(OptionType.Call, tau, forward, strike, pv);
    }

    internal static double LogNormalToNormal(double forward, double strike, double tau, double vol)
    {
      double term1 = Math.Sqrt(forward * strike);
      var fok = Math.Log(forward / strike);
      var term2 = Math.Pow(fok, 2) / 24.0;
      var term3 = Math.Pow(fok, 4) / 1920;
      var nr = 1.0 + term2 + term3;
      var term4 = (1.0 / 24.0) * (1.0 - (Math.Pow(fok, 2) / 120.0)) * vol * vol * tau;
      var term5 = Math.Pow(vol, 4) * tau * tau / 5760.0;
      var dr = 1.0 + term4 + term5;
      return vol * term1 * nr / dr;
    }

    internal static InterestRateIndex GetRateIndex(this CapBase cap)
    {
      var swapIndex = cap.ReferenceRateIndex as SwapRateIndex;
      return swapIndex != null
        ? swapIndex.ForwardRateIndex
        : (InterestRateIndex)cap.ReferenceRateIndex;
    }

    #endregion

    #region Swaptions
    /// <summary>
    /// Converts the swaption volatility given to the output volatility type.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="swaption">The swaption.</param>
    /// <param name="vol">The vol.</param>
    /// <param name="resets">The resets.</param>
    /// <param name="inputType">Type of the input.</param>
    /// <param name="outputType">Type of the output.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public static double ConvertSwaption(
      Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve,
      Swaption swaption, double vol, RateResets resets, DistributionType inputType,
      DistributionType outputType)
    {
      // If we're not converting, then return
      if (inputType == outputType)
      {
        return vol;
      }
      // Calc pv
      var flatVol = new FlatVolatility { DistributionType = inputType, Volatility = vol };
      var pricer = new SwaptionBlackPricer(swaption, asOf, settle, referenceCurve, discountCurve, flatVol);
      pricer.RateResets.AllResets = resets.AllResets;
      var pv = pricer.ProductPv();

      // Calc output vol
      var outputCube = new FlatVolatility { DistributionType = outputType, Volatility = 0 };
      var outputPricer = new SwaptionBlackPricer(swaption, asOf, settle, referenceCurve, discountCurve, outputCube);

      // Done
      return outputPricer.IVol(pv, outputType);
    }
    #endregion
  }
}
