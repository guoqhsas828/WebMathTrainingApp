using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  /// Utility class containing SABR rate model methods
  /// </summary>
  public static class SabrRateModel
  {
    private static double StrikeLb = 1e-4;
    /// <summary>
    /// Compute volatility based on SABR model parameters
    /// </summary>
    /// <param name="alpha">Alpha</param>
    /// <param name="beta">Beta</param>
    /// <param name="rho">Rho</param>
    /// <param name="nu">Nu</param>
    /// <param name="F">Forward</param>
    /// <param name="K">Strike</param>
    /// <param name="T">Time to maturity</param>
    /// <returns>Volatility</returns>
    public static double SabrVol(double alpha, double beta, double rho, double nu, double F, double K, double T)
    {
      if (F == K)
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
      double logros = System.Math.Log(ros);
      double logros2 = logros * logros;
      double logros4 = logros2 * logros2;
      double b1 = 1 - beta;
      double b2 = b1 * b1;
      double b4 = b2 * b2;
      double z = nu / alpha * System.Math.Pow(rbs, b1 / 2) * logros;
      double xz = System.Math.Log((System.Math.Sqrt(1 - 2 * rho * z + z * z) + z - rho) / (1 - rho));
      double denom = System.Math.Pow(rbs, b1 / 2) * (1 + b2 / 24 * logros2 + b4 / 1920 * logros4);
      double num = 1 + (b2 * alpha * alpha / (24 * System.Math.Pow(rbs, b1)) +
        (rho * beta * alpha * nu) / (4 * System.Math.Pow(rbs, b1 / 2)) + nu * nu * (2 - 3 * rho * rho) / 24) *
                            T;
      return (alpha / denom) * z / xz * num;
    }

    /// <summary>
    /// Compute the first-order derivative to forward price based on SABR parameters
    /// </summary>
    /// <param name="alpha">Alpha</param>
    /// <param name="beta">Beta</param>
    /// <param name="rho">Rho</param>
    /// <param name="nu">Nu</param>
    /// <param name="F">Forward price</param>
    /// <param name="K">Strike</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="volatilityType">Type of the volatility</param>
    /// <returns></returns>
    public static double DeltaSabrDeltaF(double alpha, double beta, double rho, double nu, double F, double K, double T, VolatilityType volatilityType)
    {
      const double bumpSize = 0.0004;
      if (K < StrikeLb)
        K = StrikeLb;

      double delta;
      var sigmaU = SabrVol(alpha, beta, rho, nu, F + bumpSize, K, T);
      var sigmaD = SabrVol(alpha, beta, rho, nu, F - bumpSize, K, T);
      delta = (sigmaU - sigmaD) / (2 * bumpSize);
      if (volatilityType == VolatilityType.LogNormal)
      {
        return delta;
      }
      else
      {
        var sigmaNU = LogNormalToNormalConverter.ConvertCapletVolatility(F + bumpSize, K, T, sigmaU,
                                                                        VolatilityType.LogNormal, VolatilityType.Normal);
        var sigmaND = LogNormalToNormalConverter.ConvertCapletVolatility(F - bumpSize, K, T, sigmaD,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        return (sigmaNU - sigmaND) / (2 * bumpSize);
      }
    }



    /// <summary>
    /// Compute the first-order derivative to alpha based on SABR parameters.
    /// </summary>
    /// <param name="alpha">Alpha</param>
    /// <param name="beta">Beta</param>
    /// <param name="rho">Rho</param>
    /// <param name="nu">Nu</param>
    /// <param name="F">Forward price</param>
    /// <param name="K">Strike</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="volatilityType">Type of the volatility</param>
    /// <returns></returns>
    public static double DeltaSabrDeltaAlpha(double alpha, double beta, double rho, double nu, double F, double K, double T, VolatilityType volatilityType)
    {
      const double bumpSize = 0.0001;
      if (K < StrikeLb)
        K = StrikeLb;

      var sigmaU = SabrVol(alpha + bumpSize, beta, rho, nu, F, K, T);
      var sigmaD = SabrVol(alpha - bumpSize, beta, rho, nu, F, K, T);
      if (volatilityType == VolatilityType.LogNormal)
      {
        return (sigmaU - sigmaD) / (2 * bumpSize);
      }
      else
      {
        var sigmaNU = LogNormalToNormalConverter.ConvertCapletVolatility(F, K, T, sigmaU,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        var sigmaND = LogNormalToNormalConverter.ConvertCapletVolatility(F, K, T, sigmaD,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        return (sigmaNU - sigmaND) / (2 * bumpSize);
      }
    }

    /// <summary>
    /// Compute the first-order derivative to rho based on SABR parameters.
    /// </summary>
    /// <param name="alpha">Alpha</param>
    /// <param name="beta">Beta</param>
    /// <param name="rho">Rho</param>
    /// <param name="nu">Nu</param>
    /// <param name="F">Forward price</param>
    /// <param name="K">Strike</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="volatilityType">Type of the volatility</param>
    /// <returns></returns>
    public static double DeltaSabrDeltaRho(double alpha, double beta, double rho, double nu, double F, double K, double T, VolatilityType volatilityType)
    {
      const double bumpSize = 0.0004;
      if (K < StrikeLb)
        K = StrikeLb;

      var sigmaU = SabrVol(alpha, beta, rho + bumpSize, nu, F, K, T);
      var sigmaD = SabrVol(alpha, beta, rho - bumpSize, nu, F, K, T);
      if (volatilityType == VolatilityType.LogNormal)
      {
        return (sigmaU - sigmaD) / (2 * bumpSize);
      }
      else
      {
        var sigmaNU = LogNormalToNormalConverter.ConvertCapletVolatility(F, K, T, sigmaU,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        var sigmaND = LogNormalToNormalConverter.ConvertCapletVolatility(F, K, T, sigmaD,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        return (sigmaNU - sigmaND) / (2 * bumpSize);
      }

    }

    /// <summary>
    /// Compute the first-order derivative to nu based on SABR parameters.
    /// </summary>
    /// <param name="alpha">Alpha</param>
    /// <param name="beta">Beta</param>
    /// <param name="rho">Rho</param>
    /// <param name="nu">Nu</param>
    /// <param name="F">Forward price</param>
    /// <param name="K">Strike</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="volatilityType">Type of the volatility</param>
    /// <returns></returns>
    public static double DeltaSabrDeltaNu(double alpha, double beta, double rho, double nu, double F, double K, double T, VolatilityType volatilityType)
    {
      const double bumpSize = 0.0001;
      if (K < StrikeLb)
        K = StrikeLb;

      var sigmaU = SabrVol(alpha, beta, rho, nu + bumpSize, F, K, T);
      var sigmaD = SabrVol(alpha, beta, rho, nu - bumpSize, F, K, T);
      if (volatilityType == VolatilityType.LogNormal)
      {
        return (sigmaU - sigmaD) / (2.0 * bumpSize);
      }
      else
      {
        var sigmaNU = LogNormalToNormalConverter.ConvertCapletVolatility(F, K, T, sigmaU,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        var sigmaND = LogNormalToNormalConverter.ConvertCapletVolatility(F, K, T, sigmaD,
                                                                         VolatilityType.LogNormal, VolatilityType.Normal);
        return (sigmaNU - sigmaND) / (2 * bumpSize);
      }

    }


  }
}
