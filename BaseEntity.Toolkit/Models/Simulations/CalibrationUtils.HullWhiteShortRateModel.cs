// 
// 
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  partial class CalibrationUtils
  {
    /// <summary>
    /// Gets the Hull-White short rate calibration model
    /// </summary>
    /// <returns>IRateCalibrationModel.</returns>
    internal static IRateVolatilityCalibrationModel GetHullWhiteShortRateCalibrationModel()
    {
      return HullWhiteShortRateCalibrationModel.Instance;
    }

    #region Nested type: HullWhiteShortRateCalibraionModel

    private class HullWhiteShortRateCalibrationModel
      : SingletonBase<HullWhiteShortRateCalibrationModel>
        , IRateVolatilityCalibrationModel
    {
      public VolatilityCurve[] FromCapletVolatility(
        DiscountCurve discountCurve,
        Dt[] standardCapletTenors,
        Curve[] fwdFwdVols,
        DistributionType distributionType,
        Dt[] bespokeCapletTenors,
        Dt[] curveDates,
        out double[,] bespokeFactors)
      {
        throw new NotImplementedException();
      }

      public VolatilityCurve[] FromSwaptionVolatility(
        DiscountCurve discountCurve,
        DiscountCurve projectionCurve,
        double[,] swaptionVol,
        DistributionType distribution,
        IEnumerable<Tenor> swaptionExpiries,
        IEnumerable<Tenor> swaptionTenors,
        Dt[] bespokeTenors,
        ref double[,] factors)
      {
        var expiries = (swaptionExpiries as IReadOnlyList<Tenor>)
                       ?? swaptionExpiries.ToList();
        var tenors = (swaptionTenors as IReadOnlyList<Tenor>)
                     ?? swaptionTenors.ToList();
        var hw = HullWhiteShortRates.Calibration.Calibrate(
          discountCurve.AsOf, discountCurve,
          expiries, tenors, swaptionVol.ToRows(),
          null, null, null);

        factors = new double[1, GetColumnCount(factors)];
        factors[0, 0] = 1;

        return hw.ToVolatilityArray();
      }

      /// <summary>
      ///  Use model params from rate model
      /// </summary>
      /// <param name="liborCurve">Curve</param>
      /// <param name="rateModelParams">Rate model params</param>
      /// <param name="distributionType">Distribution type</param>
      /// <param name="bespokeTenors">Bespoke tenors</param>
      /// <param name="factors">Factor loadings</param>
      /// <returns>Volatility Curves</returns>
      public VolatilityCurve[] FromSimpleVolatility(DiscountCurve liborCurve,
        RateModelParameters rateModelParams,
        DistributionType distributionType,
        Dt[] bespokeTenors,
        out double[,] factors)
      {
        if (rateModelParams.ModelName(RateModelParameters.Process.Funding) != RateModelParameters.Model.Hull)
          throw new NotImplementedException("Only Hull model params may be used with Hull White Short Rate Model calibration");
        var meanReversion = rateModelParams[RateModelParameters.Param.MeanReversion] as Curve;
        var volatility = rateModelParams[RateModelParameters.Param.Sigma] as Curve;

        var hw = HullWhiteShortRates.Calibration.Calibrate(
          liborCurve.AsOf, liborCurve,
          volatility?.Select(o => (o.Date - liborCurve.AsOf) / 365.0).ToArray(),
          volatility?.Select(o => o.Value).ToArray(),
          meanReversion?.Select(o => (o.Date - liborCurve.AsOf) / 365.0).ToArray(),
          meanReversion?.Select(o => o.Value).ToArray());

        factors = new double[1, volatility?.Select(o => (o.Date - liborCurve.AsOf) / 365.0).ToArray().Length ?? 1];
        factors[0, 0] = 1;

        return hw.ToVolatilityArray();
      }
    }

    private static int GetColumnCount(double[,] data)
    {
      return Math.Max(data == null ? 1 : data.GetLength(1), 1);
    }
    #endregion
  }
}
