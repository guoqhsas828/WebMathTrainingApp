// 
// 
// 

using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Interface for calibrating rate volatility from swaption and caplet volatilities
  /// </summary>
  public interface IRateVolatilityCalibrationModel
  {
    /// <summary>
    /// Calibrate volatilities and factor loadings from the swaption volatilities.
    /// </summary>
    /// <param name="discountCurve">The discount curve</param>
    /// <param name="projectionCurve">The projection curve</param>
    /// <param name="swaptionVol">The swaption vol.</param>
    /// <param name="distribution">The distribution.</param>
    /// <param name="swaptionExpiries">The swaption expiries.</param>
    /// <param name="swaptionTenors">The swaption tenor.</param>
    /// <param name="bespokeTenors">The bespoke tenors.</param>
    /// <param name="outputFactorLoadings">The output factor loadings.</param>
    /// <returns>VolatilityCurve[].</returns>
    VolatilityCurve[] FromSwaptionVolatility(
      DiscountCurve discountCurve,
      DiscountCurve projectionCurve,
      double[,] swaptionVol,
      DistributionType distribution,
      IEnumerable<Tenor> swaptionExpiries,
      IEnumerable<Tenor> swaptionTenors,
      Dt[] bespokeTenors,
      ref double[,] outputFactorLoadings);

    /// <summary>
    ///  Calibrate the rate volatilities from the forward rate volatilities
    /// </summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="standardCapletTenors">The standard caplet tenors.</param>
    /// <param name="fwdFwdVols">The forward volatilities of the forward rates</param>
    /// <param name="distributionType">Type of the distribution</param>
    /// <param name="bespokeCapletTenors">The bespoke caplet tenors</param>
    /// <param name="curveDates">The curve dates</param>
    /// <param name="bespokeFactors">The bespoke factors.</param>
    /// <returns>VolatilityCurve[].</returns>
    VolatilityCurve[] FromCapletVolatility(
      DiscountCurve discountCurve,
      Dt[] standardCapletTenors,
      Curve[] fwdFwdVols,
      DistributionType distributionType,
      Dt[] bespokeCapletTenors,
      Dt[] curveDates,
      out double[,] bespokeFactors);

    /// <summary>
    ///  Calibrate the rate volatilities from the direct volatility input
    /// </summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="rateModelParameters">The rate model parameters.</param>
    /// <param name="distributionType">Type of the distribution</param>
    /// <param name="bespokeTenors">The curve dates</param>
    /// <param name="factors">The output factor loadings.</param>
    /// <returns>Volatility curve</returns>
    VolatilityCurve[] FromSimpleVolatility(DiscountCurve discountCurve,
      RateModelParameters rateModelParameters,
      DistributionType distributionType,
      Dt[] bespokeTenors,
      out double[,] factors);
  }
}
