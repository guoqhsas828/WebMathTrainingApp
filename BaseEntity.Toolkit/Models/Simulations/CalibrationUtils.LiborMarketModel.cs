// 
// 
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models.Simulations
{
  partial class CalibrationUtils
  {
    /// <summary>
    /// Gets the LIBOR market calibration model.
    /// </summary>
    /// <param name="separable">True to calibrate instantaneous vol of the form <m>\phi(T)\psi(t)</m> where <m>\phi(T)</m> is a maturity dependent constant 
    /// and <m>\psi(t)</m> is a time dependent function common to all tenors. This functional specification in a one factor framework results in a rank one 
    /// covariance matrix.  
    /// 
    /// If false, calibrated vol is of the form \phi(T)\psi(T-t). This parametric form preserves the shape of the vol surface over running time.</param>
    /// <returns>IRateCalibrationModel.</returns>
    internal static IRateVolatilityCalibrationModel GetLiborMarketCalibrationModel(
      bool separable)
    {
      return new LiborMarketCalibrationModel(
        separable, null, null, null);
    }

    /// <summary>
    /// Gets the LIBOR market calibration model.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="separable">True to calibrate instantaneous vol of the form <m>\phi(T)\psi(t)</m> where <m>\phi(T)</m> is a maturity dependent constant 
    /// and <m>\psi(t)</m> is a time dependent function common to all tenors. This functional specification in a one factor framework results in a rank one 
    /// covariance matrix.  
    /// 
    /// If false, calibrated vol is of the form \phi(T)\psi(T-t). This parametric form preserves the shape of the vol surface over running time.</param>
    /// <param name="swapRateEffectives">The swap rate effective dates</param>
    /// <param name="swapRateMaturities">The swap rate maturity dates</param>
    /// <param name="swapRateFactorLoadings">The swap rate factor loadings.</param>
    /// <returns>IRateCalibrationModel.</returns>
    internal static IRateVolatilityCalibrationModel GetLiborMarketCalibrationModel(
      Dt asOf,
      bool separable,
      Dt[] swapRateEffectives,
      Dt[] swapRateMaturities,
      double[][] swapRateFactorLoadings)
    {
      int rows = swapRateFactorLoadings.Length,
        cols = swapRateFactorLoadings.Max(v => v.Length);
      return new LiborMarketCalibrationModel(separable,
        swapRateEffectives.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
        swapRateMaturities.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
        swapRateFactorLoadings.ToArray2D(rows, cols));
    }

    private class LiborMarketCalibrationModel : IRateVolatilityCalibrationModel
    {
      internal LiborMarketCalibrationModel(
        bool separable,
        double[] swapEffective,
        double[] swapMaturity,
        double[,] swapRateFactorLoadings)
      {
        Separable = separable;
        if (swapRateFactorLoadings == null) return;

        Debug.Assert(swapEffective != null);
        Debug.Assert(swapMaturity != null);
        SwapEffectives = swapEffective;
        SwapMaturities = swapMaturity;
        SwapRateFactorLoadings = swapRateFactorLoadings;
      }

      private double[] SwapEffectives { get; }
      private double[] SwapMaturities { get; }

      private double[,] SwapRateFactorLoadings { get; }

      private bool Separable { get; }

      public VolatilityCurve[] FromSwaptionVolatility(
        DiscountCurve discountCurve,
        DiscountCurve projectionCurve,
        double[,] swaptionVol,
        DistributionType distribution,
        IEnumerable<Tenor> swaptionExpiries,
        IEnumerable<Tenor> swaptionTenors,
        Dt[] bespokeCapletTenors,
        ref double[,] bespokeFactors)
      {
        var asOf = discountCurve.AsOf;
        var bespokeVols = bespokeCapletTenors.Select((dt, i) =>
        {
          var retVal = new Curve(asOf);
          if (i == 0)
          {
            retVal.Add(asOf, 0.0);
            return retVal;
          }
          for (int j = 0; j < i; ++j)
            retVal.Add(bespokeCapletTenors[j], 0.0);
          return retVal;
        }).ToArray();

        var swaptionEffective = swaptionExpiries.Select(
          t => Dt.FractDiff(asOf, Dt.Add(asOf, t))).ToArray();
        var swaptionTenor = swaptionTenors.Select(t => (double) t.Days).ToArray();
        var bespokeTenors = bespokeCapletTenors
          .Select(dt => Dt.FractDiff(asOf, dt)).ToArray();
        if (SwapRateFactorLoadings != null)
        {
          bespokeFactors = new double[bespokeVols.Length, SwapRateFactorLoadings.GetLength(1)];
          Native.CalibrationUtils.CalibrateFromSwaptionVolatility(
            discountCurve, projectionCurve,
            SwapEffectives, SwapMaturities, SwapRateFactorLoadings,
            swaptionVol, swaptionEffective, swaptionTenor,
            bespokeTenors, bespokeVols, bespokeFactors,
            distribution == DistributionType.Normal, Separable);
        }
        else
        {
          if (bespokeFactors == null)
          {
            throw new ArgumentNullException(nameof(bespokeFactors));
          }
          if (bespokeTenors.GetLength(0) != bespokeVols.Length)
          {
            var expect = bespokeVols.Length;
            var actual = bespokeTenors.GetLength(0);
            throw new ArgumentException(
              $"Expect {nameof(bespokeFactors)} to have {expect} rows, but got {actual}");
          }
          Native.CalibrationUtils.CalibrateFromSwaptionVolatility(
            discountCurve, swaptionVol, swaptionEffective, swaptionTenor,
            bespokeTenors, bespokeVols, bespokeFactors,
            distribution == DistributionType.Normal, Separable);
        }

        return bespokeVols.Select(c =>
        {
          var retVal = new VolatilityCurve(c.AsOf)
          {
            DistributionType = distribution,
            Interp = new SquareLinearVolatilityInterp(),
          };
          for (int i = 0; i < c.Count; ++i)
            retVal.AddVolatility(c.GetDt(i), c.GetVal(i));
          retVal.Fit();
          return retVal;
        }).ToArray();
      }

      public VolatilityCurve[] FromCapletVolatility(
        DiscountCurve discountCurve,
        Dt[] standardCapletTenors,
        Curve[] fwdFwdVols,
        DistributionType distributionType,
        Dt[] bespokeCapletTenors,
        Dt[] curveDates,
        out double[,] bespokeFactors)
      {
        Dt asOf = discountCurve.AsOf;
        var bespokeVols = bespokeCapletTenors.Select(dt =>
        {
          var retVal = new Curve(asOf);
          foreach (var curveDt in curveDates)
          {
            if (curveDt <= retVal.AsOf)
              continue;
            if (curveDt >= dt)
              break;
            retVal.Add(curveDt, 0.0);
          }
          retVal.Add(dt, 0.0);
          return retVal;
        }).ToArray();
        bespokeFactors = new double[bespokeVols.Length, SwapRateFactorLoadings.GetLength(1)];
        try
        {
          Native.CalibrationUtils.CalibrateLiborFactors(discountCurve,
            SwapEffectives, SwapMaturities, SwapRateFactorLoadings,
            standardCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
            fwdFwdVols,
            bespokeCapletTenors.Select(dt => Dt.FractDiff(asOf, dt)).ToArray(),
            bespokeVols, bespokeFactors,
            distributionType == DistributionType.Normal);
        }
        catch (Exception)
        {

          throw new ArgumentException(
            String.Format("Calibration of {0} Libor Rate factor loadings from {0} Swap rate factor loading has failed.",
              discountCurve.Ccy));
        }
        var bespokeCapletVols = bespokeVols.Select(c =>
        {
          var retVal = new VolatilityCurve(c.AsOf) {DistributionType = distributionType};
          for (int i = 0; i < c.Count; ++i)
            retVal.AddVolatility(c.GetDt(i), c.GetVal(i));
          retVal.Fit();
          return retVal;
        }).ToArray();
        return bespokeCapletVols;
      }

      /// <summary>
      ///  Calibrate the rate volatilities from the direct volatility input
      /// </summary>
      /// <param name="discountCurve">The discount curve.</param>
      /// <param name="rateModelParameters">The rate model parameters.</param>
      /// <param name="distributionType">Type of the distribution</param>
      /// <param name="bespokeTenors">The curve dates</param>
      /// <param name="factors">The output factor loadings.</param>
      /// <returns>Volatility curve</returns>
      public VolatilityCurve[] FromSimpleVolatility(DiscountCurve discountCurve,
        RateModelParameters rateModelParameters,
        DistributionType distributionType,
        Dt[] bespokeTenors,
        out double[,] factors)
      {
        throw new NotImplementedException();
      }
      
    }
  }
}
