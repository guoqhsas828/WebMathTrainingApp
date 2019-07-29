/*
 * RateVolatilitySabrCalibrator.cs
 *
 *   2005-2011. All rights reserved.
 * 
 */

using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models.BGM;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Base class for calibrators using the SABR model
  /// </summary>
  [Serializable]
  public abstract class RateVolatilitySabrCalibrator
    : RateVolatilityCalibrator, IVolatilitySurfaceInterpolator
  {
    #region Constructors

    /// <summary>
    /// Constructor for the Forward Volatility Cube SABR Calibrator
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="projectionCurve">The target projection curve.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="dates">The dates.</param>
    /// <param name="strikes">The strikes.</param>
    protected RateVolatilitySabrCalibrator(Dt asOf, DiscountCurve dc, DiscountCurve projectionCurve, InterestRateIndex rateIndex, VolatilityType volatilityType,
                                            Dt[] expiries, Dt[] dates, double[] strikes)
      : base(asOf, dc, projectionCurve, rateIndex, volatilityType, dates, expiries, strikes)
    {
    }

    #endregion Constructors

    #region Methods
    /// <summary>
    /// Interpolates the specified surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    public override double Interpolate(CalibratedVolatilitySurface surface,
      Dt expiry, double forwardRate, double strike)
    {
      return CapletVolatility(expiry, forwardRate, strike);
    }


    /// <summary>
    /// Interpolates the specified expiry.
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    double IVolatilitySurfaceInterpolator.Interpolate(VolatilitySurface cube, Dt expiry, double strike)
    {
      return CapletVolatility(expiry, strike);
    }

    /// <summary>
    /// Compute the SABR Caplet Volatility given a set of parameters
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    protected double CapletVolatility(Dt expiry, double forwardRate, double strike)
    {
      if (strike < 1e-4) strike = 1e-4;
      double tau = Dt.Fraction(AsOf, expiry, DayCount.Actual365Fixed);
      double alpha = alphaCurve_.Interpolate(expiry);
      double beta = betaCurve_.Interpolate(expiry);
      double rho = rhoCurve_.Interpolate(expiry);
      double nu = nuCurve_.Interpolate(expiry);
      double logNormalVol = SabrVol(alpha, beta, rho, nu, forwardRate, strike, tau);

      var vol = (VolatilityType == VolatilityType.LogNormal)
        ? logNormalVol
        : LogNormalToNormalConverter.ConvertCapletVolatility(forwardRate, strike, tau, logNormalVol, VolatilityType.LogNormal, VolatilityType.Normal);

      return vol;
    }

    /// <summary>
    /// Compute the SABR Caplet Volatility using parameters consistent with the
    /// calibration.
    /// </summary>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    protected double CapletVolatility(Dt expiry, double strike)
    {
      // Calc forward date
      var liborStart = Dt.AddDays(expiry, RateIndex.SettlementDays, RateIndex.Calendar);
      var tenorDate = Dt.Add(liborStart, RateIndex.IndexTenor);
      tenorDate = Dt.Roll(tenorDate, RateIndex.Roll, RateIndex.Calendar);

      // Get forward rate
      double fwdRate = RateProjectionCurve.F(liborStart, tenorDate, RateIndex.DayCount, Frequency.None);

      // Calc
      return CapletVolatility(expiry, fwdRate, strike);
    }

    /// <summary>
    /// SABR Volatility Function
    /// </summary>
    /// <param name = "alpha">The alpha.</param>
    /// <param name = "beta">The beta.</param>
    /// <param name = "rho">The rho.</param>
    /// <param name = "nu">The nu.</param>
    /// <param name = "F">The F.</param>
    /// <param name = "K">The K.</param>
    /// <param name = "T">The T.</param>
    /// <returns></returns>
    public static double SabrVol(double alpha, double beta, double rho, double nu, double F, double K, double T)
    {
      if (F.AlmostEquals(K))
      {
        double term1 = alpha / (Math.Pow(F, 1 - beta));
        double term21 = (Math.Pow(1.0 - beta, 2) * alpha * alpha) / (24.0 * Math.Pow(F, 2.0 - 2.0 * beta));
        double term22 = (rho * beta * nu * alpha) / (4 * Math.Pow(F, 1 - beta));
        double term23 = ((2.0 - 3.0 * rho * rho) * nu * nu) / 24.0;
        double term2 = 1 + (term21 + term22 + term23) * T;
        return term1 * term2;
      }

      double ros = F / K;
      double rbs = F * K;
      double logros = Math.Log(ros);
      double logros2 = logros * logros;
      double logros4 = logros2 * logros2;
      double b1 = 1 - beta;
      double b2 = b1 * b1;
      double b4 = b2 * b2;
      double z = nu / alpha * Math.Pow(rbs, b1 / 2) * logros;
      double xz = Math.Log((Math.Sqrt(1 - 2 * rho * z + z * z) + z - rho) / (1 - rho));
      double denom = Math.Pow(rbs, b1 / 2) * (1 + b2 / 24 * logros2 + b4 / 1920 * logros4);
      double num = 1 +
                   (b2 * alpha * alpha / (24 * Math.Pow(rbs, b1)) + (rho * beta * alpha * nu) / (4 * Math.Pow(rbs, b1 / 2)) +
                    nu * nu * (2 - 3 * rho * rho) / 24) * T;
      return (alpha / denom) * z / xz * num;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Get the Calibrated Parameters
    /// </summary>
    public Curve[] CalibratedParams
    {
      get { return new[] { betaCurve_, alphaCurve_, rhoCurve_, nuCurve_ }; }
    }

    /// <summary>
    /// Term structure of alpha parameters.
    /// </summary>
    public Curve Alpha
    {
      get { return alphaCurve_; }
    }

    /// <summary>
    /// Term structure of beta parameters.
    /// </summary>
    public Curve Beta
    {
      get { return betaCurve_; }
    }

    /// <summary>
    /// Term structure of rho parameters.
    /// </summary>
    public Curve Rho
    {
      get { return rhoCurve_; }
    }

    /// <summary>
    /// Term structure of nu parameters.
    /// </summary>
    public Curve Nu
    {
      get { return nuCurve_; }
    }

    #endregion Properties

    #region Data

    /// <summary>
    /// Alpha
    /// </summary>
    protected Curve alphaCurve_;

    /// <summary>
    /// Beta
    /// </summary>
    protected Curve betaCurve_;

    /// <summary>
    /// Nu
    /// </summary>
    protected Curve nuCurve_;

    /// <summary>
    /// Rho
    /// </summary>
    protected Curve rhoCurve_;

    #endregion Data
  }
}