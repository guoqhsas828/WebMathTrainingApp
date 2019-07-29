using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Models
{

  #region ForwardModelUtils

  /// <summary>
  /// Utilities
  /// </summary>
  public static class ForwardModelUtils
  {
    #region Methods

    public delegate double ImpliedVol(double f, double time, double strike, RateModelParameters1D parameters, Dt forwardTenor);

    public delegate double OptionPrice(OptionType type, double f, double strike, double T, double v);
    
    /// <exclude></exclude>
    public static double IntrinsicValue(OptionType type, double f, double strike, double T)
    {
      return (type == OptionType.Call) ? Math.Max(f - strike, 0.0) : Math.Max(strike - f, 0.0);
    }

    /// <exclude></exclude>
    public static double BlackPrice(OptionType type, double f, double strike, double T, double v)
    {
      if (T <= 0 || v <= 0 || strike <= 0 || f <= 0.0)
        return IntrinsicValue(type, f, strike, T);
      return Black.P(type, T, f, strike, v);
    }

    /// <exclude></exclude>
    public static double NormalBlackPrice(OptionType type, double f, double strike, double T, double v)
    {
      if (T <= 0 || v <= 0)
        return IntrinsicValue(type, f, strike, T);
      return BlackNormal.P(type, T, 0.0, f, strike, v);
    }

    /// <exclude></exclude>
    private static double Integrate(Func<double, double> fn, double a, double b)
    {
      double[] points = {
                          -0.98527069794782141, -0.92320372252064353, -0.81480955060199456, -0.66549797721688464,
                          -0.48275291858847497, -0.27573720543552249, -0.054831227991764639, 0.168887928042681, 
                          0.38420200343920369, 0.58031405654687462, 0.7473896426133787, 0.87704891820146191, 
                          0.96277926997802465, 1.0000000000000002
                        };
      double[] weights = {
                           0.0377071632698966, 0.085940535442980345, 0.12993966873734225, 0.16742972789108659,
                           0.19652551845298261, 0.21576710060461873, 0.22418934800270807, 0.22136981149957063,
                           0.20744976333517542, 0.18312700212573038, 0.14962053935312125, 0.10860772274436245,
                           0.062122016907771034, 0.010204081632653088
                         };
      double xm = 0.5 * (b + a);
      double xl = 0.5 * (b - a);
      double res = 0.0;
      for (int i = 0; i < 14; ++i)
        res += weights[i] * fn(xm + xl * points[i]);
      return res * xl;
    }

    /// <exclude></exclude>
    public static double CalcSecondMoment(this RateModelParameters1D parameters, double f, double time, Dt tenor, ImpliedVol impliedVolatility, OptionPrice optionPrice)
    {
      if (time < 1e-3 || parameters == null)
        return f * f;
      Func<double, double> fn = x =>
                                {
                                  double y = Math.Tan(x);
                                  double k = f * y;
                                  double v = impliedVolatility(f, time, k, parameters, tenor);
                                  double p = optionPrice(OptionType.Call, f, k, time, v);
                                  return f * p * (1 + y * y);
                                };
      return 2 * Integrate(fn, 0.0, 0.5 * Math.PI - 1e-3);
    }

    /// <exclude></exclude>
    public static double CalcArithmeticAvgNormalVol(this RateModelParameters1D parameters, Dt asOf, List<double> weights, List<double> components, double t, List<Dt> tenors,
      ImpliedVol impliedNormalVol)
    {
      if (parameters == null)
        return 0.0;
      double v = 0.0, norm = 0.0;
      for (int i = 0; i < components.Count; ++i)
      {
        double wi = weights[i];
        norm += wi;
        if (asOf >= tenors[i])
          continue;
        double ci = components[i];
        double ti = Dt.FractDiff(asOf, tenors[i]) / 365.0;
        double si = impliedNormalVol(ci, ti, ci, parameters, tenors[i]);//assume perfect correlation
        v += si * wi;
      }
      return v / norm;
    }


    /// <exclude></exclude>
    public static double CalcGeometricAvgNormalVol(this RateModelParameters1D parameters, Dt asOf, List<double> weights, List<double> components, double t, List<Dt> tenors,
                                               ImpliedVol impliedNormalVol)
    {
      if (parameters == null)
        return 0.0;
      double v = 0.0, norm = 0.0;
      for (int i = 0; i < components.Count; ++i)
      {
        double wi = weights[i] / 365.0;
        norm += wi;
        if (asOf >= tenors[i])
          continue;
        double ci = components[i];
        double ti = Dt.FractDiff(asOf, tenors[i]) / 365.0;
        double si = impliedNormalVol(ci, ti, ci, parameters, tenors[i]); //assume perfect correlation
        v += wi / (1 + wi * ci) * si;
      }
      return v / norm;
    }


    public static double Interpolate(this RateModelParameters1D parameters, RateModelParameters.Param name,
                                       Dt maturity, double strike, ReferenceIndex referenceIndex)
    {
       if (parameters == null)
        throw new ToolkitException("Null rate model parameters passed.");
      IModelParameter param;
      if (!parameters.TryGetValue(name, out param) || param == null)
        throw new ToolkitException("Rate model parameter with the name " + name + " is missing.");
      var crv = param as Curve;
      if (crv != null && (crv.Points == null || crv.Points.Count == 0))
        throw new ToolkitException("The curve of the rate model parameter with the name " + name + " is empty.");
      double ret;
      try
      {
        ret = param.Interpolate(maturity, strike, referenceIndex);
      }
      catch (Exception ex)
      {
        var mess = string.Format(
          "Error interpolating rate model parameters: {0} for maturity {1}, strike {2} ", name, maturity, strike);
        if (referenceIndex != null && !string.IsNullOrEmpty(referenceIndex.IndexName))
          mess += (", reference index " + referenceIndex.IndexName + " ");
        mess += ex.Message;
        throw new ToolkitException(mess);
      }
      return ret;
    }

    #endregion
  }

  #endregion

  #region ForwardModel
  /// <summary>
  /// Forward Rate/Price models under their martingale measure
  /// </summary>
  [Serializable]
  public abstract class ForwardModel
  {
    /// <summary>
    /// Avg type
    /// </summary>
    public enum AverageType
    {
      /// <summary>
      /// Arithmetic average
      /// </summary>
      Arithmetic,

      /// <summary>
      /// Geometric average
      /// </summary>
      Geometric,
    }

    /// <summary>
    /// Black implied volatility
    /// </summary>
    /// <param name="f">Forward</param>
    /// <param name="t">Time to maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="parameters">ModelParameters</param>
    /// <param name="tenor">Forward tenor</param>
    /// <returns>Implied vol</returns>
    public abstract double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor);

    /// <summary>
    /// Normal Black implied volatility
    /// </summary>
    /// <param name="f">Forward</param>
    /// <param name="t">Time to maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="parameters">ModelParameters</param>
    /// <param name="tenor">Forward tenor</param>
    /// <returns>Implied vol</returns>
    public abstract double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                                     Dt tenor);

    /// <summary>
    /// Call/put on forward
    /// </summary>
    /// <param name="type">Option type</param>
    /// <param name="f">Forward</param>
    /// <param name="ca">Convexity adjustment <m>E^T(F_t(T)) - F_0(T)</m></param>
    /// <param name="t">Time to maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="parameters">Model parameters</param>
    /// <param name="tenor">Forward tenor</param>
    /// <returns>Option value</returns>
    public abstract double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor);


    /// <summary>
    /// Call/put on ratio of forwards given volatility of numerator and denominator
    /// </summary>
    /// <param name="type">Option type</param>
    /// <param name="f">Forward <m>F_t(T,U) = \frac{N_t(T)}{D_t(U)}</m></param>
    /// <param name="ca"> Convexity adjustment <m>E^T(F_t(T,U)) - F_0(T,U)</m></param>
    /// <param name="num">Numerator </param>
    /// <param name="den">Denominator</param>
    /// <param name="t">Time to maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="parameters">Model parameters</param>
    /// <param name="tenorNum">Forward tenor of numerator</param>
    /// <param name="tenorDen">Forward tenor of denominator</param>
    /// <returns>Option value</returns>
    /// <remarks>To price option on ratio, we imply the Black volatility of numerator and denominator, and then assume that both processes are perfectly correlated geometric Brownian motions.</remarks>
    public double OptionOnRatio(OptionType type, double f, double ca, double num, double den, double t,
                                  double strike, RateModelParameters1D parameters, Dt tenorNum, Dt tenorDen)
    {
      double vNum = ImpliedVolatility(num, t, num, parameters, tenorNum);//atm vol for num
      double vDen = ImpliedVolatility(den, t, den, parameters, tenorDen);//atm vol for den
      return ForwardModelUtils.BlackPrice(type, f + ca, strike, t, Math.Abs(vNum - vDen));
    }


    /// <summary>
    /// Raw second moment under the martingale measure
    /// </summary>
    /// <param name="f">Forward</param>
    /// <param name="t">Time</param>
    /// <param name="parameters">Model parameters</param>
    /// <param name="tenor">Forward tenor</param>
    /// <returns><m>E^T(F_t(T)^2)</m></returns>
    public abstract double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor);

    /// <summary>
    /// Option on average of forward rates
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="type">Option type</param>
    /// <param name="f">Forward average <m>F_t(T_1, T_N) = Avg(f_t(T_1),\dots,f_t(T_N))</m> </param>
    /// <param name="weights">Averaging weights</param>
    /// <param name="components">Average components <m>f_t(T_i)</m></param>
    /// <param name="ca">Convexity adjustment <m>E^{T_N}(F_t(T_1,T_N) - F_0(T_1, T_N)</m></param>
    /// <param name="t">Time to maturity</param>
    /// <param name="strike">Strike</param>
    /// <param name="parameters">Model parameters</param>
    /// <param name="tenors">Component tenors</param>
    /// <param name="averageType">Average type (arithmetic/geometric)(</param>
    /// <returns></returns>
    /// <remarks>If average is arithmetic, we imply normal Black volatility for each component and then assume each underlying process is simple Brownian motion </remarks>
    public double OptionOnAverage(Dt asOf, OptionType type, double f, List<double> weights, List<double> components, double ca, double t,
                                    double strike, RateModelParameters1D parameters, List<Dt> tenors, AverageType averageType)
    {
      if (averageType == AverageType.Arithmetic)
      {
        double normalVol = parameters.CalcArithmeticAvgNormalVol(asOf, weights, components, t, tenors, ImpliedNormalVolatility);
        return ForwardModelUtils.NormalBlackPrice(type, f + ca, strike, t, normalVol);
      }
      if (averageType == AverageType.Geometric)
      {
        double normalVol = parameters.CalcGeometricAvgNormalVol(asOf, weights, components, t, tenors, ImpliedNormalVolatility);
        return ForwardModelUtils.NormalBlackPrice(type, f + ca, strike, t, normalVol);
      }
      throw new ArgumentException("AverageType not supported.");
    }
  }
  #endregion

  #region Nested type: Normal

  /// <summary>
  /// BGM model.  
  /// In general, the Bgm model dynamics (under the appropriate probablity measure) for a family of processes indexed by <m>T</m> (i.e. family of Libor rates) are as follows:
  /// <m>dL^\delta_t(T) = \sigma(T) L^\delta_t(T)\,dW_t^T</m>
  /// </summary>
  [Serializable]
  public sealed class NormalBlack : ForwardModel
  {
    /// <exclude></exclude>
    public override double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor)
    {
      var alpha = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex);//atm
      return Sabr.ImpliedVolatility(f, t, strike, alpha, 0.0, 0.0, 0.0);//atm
    }

    public override double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters, Dt tenor)
    {
      return parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex); //atm
    }

    /// <exclude></exclude>
    public override double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor)
    {
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma,
                                        tenor, f, parameters.ReferenceIndex); //atm
      return ForwardModelUtils.NormalBlackPrice(type, f + ca, strike, t, v);
    }

    /// <exclude></exclude>
    public override double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor)
    {
      if (t < 1e-3)
        return f * f;
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex);
      return f * f + v * v * t;
    }
  }

  #endregion

  #region Nested type: LogNormal

  /// <summary>
  /// BGM model.  
  ///In general, the Bgm model dynamics (under the appropriate probablity measure) for a family of processes indexed by <m>T</m> (i.e. family of Libor rates) are as follows:
  ///<m>dL^\delta_t(T) = \sigma(T) L^\delta_t(T)\,dW_t^T</m>
  /// </summary>
  [Serializable]
  public sealed class LogNormalBlack : ForwardModel
  {
    /// <exclude></exclude>
    public override double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor)
    {
      return parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex); //atm
    }

    /// <exclude></exclude>
    public override double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                                     Dt tenor)
    {
      var alpha = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex); //atm
      return Sabr.ImpliedNormalVolatility(f, t, strike, alpha, 1.0, 0.0, 0.0);//atm
    }

    /// <exclude></exclude>
    public override double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor)
    {
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma,
                                        tenor, f, parameters.ReferenceIndex); //atm
      return ForwardModelUtils.BlackPrice(type, f + ca, strike, t, v);
    }

    /// <exclude></exclude>
    public override double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor)
    {
      if (t < 1e-3)
        return f * f;
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex);
      return f * f * Math.Exp(v * v * t);
    }
  }


  #endregion

  #region Nested type: ReplicationNormal

  /// <summary>
  /// Model-free based on normal vol surface
  /// </summary>
  [Serializable]
  public sealed class ReplicationNormal : ForwardModel
  {
    /// <exclude></exclude>
    public override double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor)
    {
      var alpha = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex); 
      return Sabr.ImpliedVolatility(f, t, strike, alpha, 0.0, 0.0, 0.0); 
    }

    /// <exclude></exclude>
    public override double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                                     Dt tenor)
    {
      return parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex);
    }

    /// <exclude></exclude>
    public override double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor)
    {
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex);
      return ForwardModelUtils.NormalBlackPrice(type, f + ca, strike, t, v);
    }

    /// <exclude></exclude>
    public override double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor)
    {
      return parameters.CalcSecondMoment(f, t, tenor, ImpliedNormalVolatility, ForwardModelUtils.NormalBlackPrice);
    }
  }
  #endregion

  #region Nested type: Replication
  /// <summary>
  /// Model-free based on lognormal vol surface
  /// </summary>
  [Serializable]
  public sealed class Replication : ForwardModel
  {
    /// <exclude></exclude>
    public override double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor)
    {
      return parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex);
    }

    /// <exclude></exclude>
    public override double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                                     Dt tenor)
    {
      var alpha = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex);
      return Sabr.ImpliedNormalVolatility(f, t, strike, alpha, 1.0, 0.0, 0.0);
    }

    /// <exclude></exclude>
    public override double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor)
    {
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex);
      return ForwardModelUtils.BlackPrice(type, f + ca, strike, t, v);
    }

    /// <exclude></exclude>
    public override double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor)
    {
      return parameters.CalcSecondMoment(f, t, tenor, ImpliedVolatility, ForwardModelUtils.BlackPrice);
    }
  }

  #endregion

  #region Nested type: SABR

  /// <summary>
  /// SABR model. 
  ///In general, the SABR model dynamics (under the appropriate probablity measure) for a family of processes indexed by <m>T</m> (i.e. family of Libor rates) are as follows
  /// <m>\\</m>
  /// <m>dL^\delta_t(T) = V_t(T) L_t(T)^\beta \,dW_t^T,\\</m>
  /// <m>dV_t(T) = \nu_t(T) V_t(T) \,d\hat{W}_t^T,\\ </m>
  /// <m>V_0(T) = \alpha(T)\\ </m>  
  /// where the driving brownian motions <m>W_t^T</m> and <m>\hat{W}_t^T</m> are typically correlated with correlation parameter <m>\rho(T)</m>. 
  /// </summary>
  [Serializable]
  public sealed class Sabr : ForwardModel
  {
    /// <exclude></exclude>
    public static double ImpliedVolatility(double f, double t, double strike, double alpha, double beta, double nu,
                                             double rho)
    {
      if (nu <= 0.0 && beta >= 0.9999)
        return alpha;
      double ros = f / strike;
      double rbs = f * strike;
      double logros = Math.Log(ros);
      if (beta.ApproximatelyEqualsTo(0.0))//normal SABR
      {
        double atmVol = alpha * (1 + (alpha * alpha / (24.0 * rbs) + (2.0 - 3.0 * rho * rho) / 24.0 * nu * nu) * t);
        if (f.ApproximatelyEqualsTo(strike))
          return atmVol / f;
        double ratio, z = nu / alpha * Math.Sqrt(rbs) * logros;
        if (z.ApproximatelyEqualsTo(0.0))
          ratio = 1.0;
        else
        {
          double xz = Math.Log((Math.Sqrt(1 - 2.0 * rho * z + z * z) + z - rho) / (1 - rho));
          ratio = z / xz;
        }
        return logros / (f - strike) * ratio * atmVol;
      }
      if (beta.ApproximatelyEqualsTo(1.0))//lognormal SABR
      {
        double atmVol = alpha * (1 + (rho * alpha * nu / 4.0 + (2.0 - 3.0 * rho * rho) / 24.0 * nu * nu) * t);
        if (f.ApproximatelyEqualsTo(strike))
          return atmVol;
        double ratio, z = nu / alpha * logros;
        if (z.ApproximatelyEqualsTo(0.0))
          ratio = 1.0;
        else
        {
          double xz = Math.Log((Math.Sqrt(1 - 2.0 * rho * z + z * z) + z - rho) / (1 - rho));
          ratio = z / xz;
        }
        return ratio * atmVol;
      }
      { //general case
        if (f.AlmostEquals(strike))
        {
          double term1 = alpha / (Math.Pow(f, 1.0 - beta));
          double term21 = (Math.Pow(1.0 - beta, 2.0) * alpha * alpha) / (24.0 * Math.Pow(f, 2.0 - 2.0 * beta));
          double term22 = (rho * beta * nu * alpha) / (4 * Math.Pow(f, 1 - beta));
          double term23 = ((2.0 - 3.0 * rho * rho) * nu * nu) / 24.0;
          double term2 = 1 + (term21 + term22 + term23) * t;
          return term1 * term2;
        }
        double logros2 = logros * logros;
        double logros4 = logros2 * logros2;
        double b1 = 1 - beta;
        double b2 = b1 * b1;
        double b4 = b2 * b2;
        double ratio, z = nu / alpha * Math.Pow(rbs, b1 / 2.0) * logros;
        if (z.ApproximatelyEqualsTo(0.0))
          ratio = 1.0;
        else
        {
          double xz = Math.Log((Math.Sqrt(1 - 2.0 * rho * z + z * z) + z - rho) / (1 - rho));
          ratio = z / xz;
        }
        double first = Math.Pow(rbs, b1 / 2.0) * (1 + b2 / 24.0 * logros2 + b4 / 1920.0 * logros4);
        double second = 1 + (b2 * alpha * alpha / (24.0 * Math.Pow(rbs, b1)) +
                             (rho * beta * alpha * nu) / (4.0 * Math.Pow(rbs, b1 / 2.0)) + nu * nu * (2.0 - 3.0 * rho * rho) / 24.0) * t;
        return (alpha / first) * ratio * second;
      }
    }

    /// <exclude></exclude>
    public static double ImpliedNormalVolatility(double f, double t, double strike, double alpha, double beta, double nu,
                                                   double rho)
    {
      if (nu <= 0.0 && beta <= 1e-4)
        return alpha;
      double ros = f / strike;
      double rbs = f * strike;
      double logros = Math.Log(ros);
      if (beta.ApproximatelyEqualsTo(0.0)) //normal SABR
      {
        double atmVol = alpha * Math.Sqrt(rbs) * (1 + (2.0 - 3.0 * rho * rho) / 24.0 * nu * nu * t);
        if (f.ApproximatelyEqualsTo(strike))
          return atmVol;
        double ratio, z = nu / alpha * Math.Sqrt(rbs) * logros;
        if (z.ApproximatelyEqualsTo(0.0))
          ratio = 1.0;
        else
        {
          double xz = Math.Log((Math.Sqrt(1 - 2.0 * rho * z + z * z) + z - rho) / (1 - rho));
          ratio = z / xz;
        }
        return ratio * atmVol;
      }
      if (beta.ApproximatelyEqualsTo(1.0)) //lognormal SABR
      {
        double atmVol = alpha * (1 + (-alpha * alpha / 24.0 + rho * alpha * nu / 4.0 + (2.0 - 3.0 * rho * rho) / 24.0 * nu * nu) * t);
        if (f.ApproximatelyEqualsTo(strike))
          return f*atmVol;
        double ratio, z = nu / alpha * logros;
        if (z.ApproximatelyEqualsTo(0.0))
          ratio = 1.0;
        else
        {
          double xz = Math.Log((Math.Sqrt(1 - 2.0 * rho * z + z * z) + z - rho) / (1 - rho));
          ratio = z / xz;
        }
        return (f - strike) / logros * ratio * atmVol;
      }
      {
        //general case
        if (f.ApproximatelyEqualsTo(strike))
        {
          double term1 = alpha * Math.Pow(f, beta);
          double term21 = (-beta * (2 - beta) * alpha * alpha) / (24.0 * Math.Pow(f, 2.0 - 2.0 * beta));
          double term22 = (rho * beta * nu * alpha) / (4 * Math.Pow(f, 1 - beta));
          double term23 = ((2.0 - 3.0 * rho * rho) * nu * nu) / 24.0;
          double term2 = 1 + (term21 + term22 + term23) * t;
          return term1 * term2;
        }
        double logros2 = logros * logros;
        double logros4 = logros2 * logros2;
        double b1 = 1 - beta;
        double b2 = b1 * b1;
        double b4 = b2 * b2;
        double ratio, z = nu / alpha * Math.Pow(rbs, b1 / 2.0) * logros;
        if (z.ApproximatelyEqualsTo(0.0))
          ratio = 1.0;
        else
        {
          double xz = Math.Log((Math.Sqrt(1 - 2.0 * rho * z + z * z) + z - rho) / (1 - rho));
          ratio = z / xz;
        }
        double first = Math.Pow(rbs, 0.5 * beta) * (1 + logros2 / 24.0 + logros4 / 1920.0) / (1 + b2 / 24.0 * logros2 + b4 / 1920.0 * logros4);
        double second = 1 + (-beta * (2 - beta) * alpha * alpha / (24.0 * Math.Pow(rbs, b1)) +
                             (rho * beta * alpha * nu) / (4.0 * Math.Pow(rbs, b1 / 2.0)) + nu * nu * (2.0 - 3.0 * rho * rho) / 24.0) * t;
        return alpha * first * second * ratio;
      }
    }

    /// <exclude></exclude>
    public override double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor)
    {
      double alpha = parameters.Interpolate(RateModelParameters.Param.Alpha, tenor, f, parameters.ReferenceIndex);
      double beta = parameters.Interpolate(RateModelParameters.Param.Beta, tenor, f, parameters.ReferenceIndex);
      double nu = parameters.Interpolate(RateModelParameters.Param.Nu, tenor, f, parameters.ReferenceIndex);
      double rho = parameters.Interpolate(RateModelParameters.Param.Rho, tenor, f, parameters.ReferenceIndex);
      return ImpliedVolatility(f, t, strike, alpha, beta, nu, rho);
    }

    /// <exclude></exclude>
    public override double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                                     Dt tenor)
    {
      double alpha = parameters.Interpolate(RateModelParameters.Param.Alpha, tenor, f, parameters.ReferenceIndex);
      double beta = parameters.Interpolate(RateModelParameters.Param.Beta, tenor, f, parameters.ReferenceIndex);
      double nu = parameters.Interpolate(RateModelParameters.Param.Nu, tenor, f, parameters.ReferenceIndex);
      double rho = parameters.Interpolate(RateModelParameters.Param.Rho, tenor, f, parameters.ReferenceIndex);
      return ImpliedNormalVolatility(f, t, strike, alpha, beta, nu, rho);
    }

    /// <exclude></exclude>
    public override double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor)
    {
      double v = ImpliedVolatility(f, t, strike, parameters, tenor);
      return ForwardModelUtils.BlackPrice(type, f + ca, strike, t, v);
    }

    /// <exclude></exclude>
    public override double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor)
    {
      return parameters.CalcSecondMoment(f, t, tenor, ImpliedVolatility, ForwardModelUtils.BlackPrice);
    }
  }

  #endregion

  #region Nested type: ShiftedLogNormal

  /// <summary>
  /// Shifted Bgm model.   
  /// In general, the shifted BGM model dynamics (under the appropriate probablity measure) for a family of processes indexed by <m>T</m> (i.e. family of Libor rates) are as follows:
  /// <m>dL^\delta_t(T) = \sigma(T)(L^\delta_t(T) - \kappa(T))\,dW_t^T</m> 
  /// </summary>
  [Serializable]
  public sealed class ShiftedLogNormal : ForwardModel
  {
    /// <exclude></exclude>
    public static double ImpliedVolatility(double f, double t, double strike, double sigma, double kappa)
    {
      if (f.ApproximatelyEqualsTo(strike))
      {
        double ratio = (f - kappa) / f;
        return sigma * ratio * (1.0 + sigma * sigma * t / 24.0 * (ratio * ratio - 1.0));
      }
      if ((kappa >= f) | (kappa >= strike))
        return sigma;
      {
        double ratio = Math.Log(f / strike) / (Math.Log(f - kappa) - Math.Log(strike - kappa));
        double fav = 0.5 * (f + strike);
        double term = (fav - kappa) / fav;
        return sigma * ratio * (1.0 + sigma * sigma * t / 24.0 * (term * term - 1.0));
      }
    }

    public static double ImpliedNormalVolatility(double f, double t, double strike, double sigma, double kappa)
    {
      if (f.ApproximatelyEqualsTo(strike))
      {
        double ratio = f - kappa;
        return sigma * ratio * (1 - sigma * sigma * t / 24.0);
      }
      {
        double ratio = (f - strike) / (Math.Log(f - kappa) - Math.Log(strike - kappa));
        return sigma * ratio * (1 - sigma * sigma * t / 24.0);
      }
    }

    /// <exclude></exclude>
    public override double ImpliedVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                               Dt tenor)
    {
      double sigma = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex);
      double kappa = parameters.Interpolate(RateModelParameters.Param.Kappa, tenor, f, parameters.ReferenceIndex);
      return ImpliedVolatility(f, t, strike, sigma, kappa);
    }

    /// <exclude></exclude>
    public override double ImpliedNormalVolatility(double f, double t, double strike, RateModelParameters1D parameters,
                                                     Dt tenor)
    {
      double sigma = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex);
      double kappa = parameters.Interpolate(RateModelParameters.Param.Kappa, tenor, f, parameters.ReferenceIndex);
      return ImpliedNormalVolatility(f, t, strike, sigma, kappa);
    }

    /// <exclude></exclude>
    public override double Option(OptionType type, double f, double ca, double t, double strike,
                                    RateModelParameters1D parameters, Dt tenor)
    {
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, strike, parameters.ReferenceIndex);
      double kappa = parameters.Interpolate(RateModelParameters.Param.Kappa, tenor, strike, parameters.ReferenceIndex);
      return ForwardModelUtils.BlackPrice(type, f - kappa + ca, strike - kappa, t, v);
    }

    /// <exclude></exclude>
    public override double SecondMoment(double f, double t, RateModelParameters1D parameters, Dt tenor)
    {
      if (t < 1e-3)
        return f * f;
      double v = parameters.Interpolate(RateModelParameters.Param.Sigma, tenor, f, parameters.ReferenceIndex);
      double kappa = parameters.Interpolate(RateModelParameters.Param.Kappa, tenor, f, parameters.ReferenceIndex);
      double init = f - kappa;
      return init * init * Math.Exp(v * v * t) + 2 * kappa * f - kappa * kappa;
    }
  }

  #endregion

}