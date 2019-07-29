//
//   2015. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;
using Param = BaseEntity.Toolkit.Models.RateModelParameters.Param;

namespace BaseEntity.Toolkit.Numerics
{
  #region Payoff2DFn

  /// <summary>
  /// Payoff 2d function
  /// </summary>
  public class Payoff2DFn
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="payoffFn">Payoff function</param>
    public Payoff2DFn(Func<double, double, double> payoffFn)
    {
      PayoffFn = payoffFn;
    }

    /// <summary>
    /// Generic payoff function: the first parameter is the level of the first underlying, 
    /// the second parameter is the level of the second underlying 
    /// </summary>
    public Func<double, double, double> PayoffFn { get; set; }

    /// <summary>
    /// Points of discontinuity/non differentiablity of the payoff in the variable vX. The first and last point
    /// should be start point and end point of total region of integration
    /// </summary>
    public double[] PayoffKinksF { get; set; }

    /// <summary>
    /// Points of discountinuity/non differentiability of the payoff in the variable vY. The first and last point 
    /// should be start point and end point of total region of integration
    /// </summary>
    public double[] PayoffKinksL { get; set; }

    /// <summary>
    /// Specify the integration region over the state-space of stock/rate process. 
    /// If not initialized integration is over the whole support of the process
    /// </summary>
    public double[] IntegrationRegionF { get; set; }

    /// <summary>
    /// Specify the integration region over the state-space of stock/rate process. 
    /// If not initialized integration is over the whole support of the process
    /// </summary>
    public double[] IntegrationRegionL { get; set; }
  }

  #endregion

  #region TransitionKernel2D

  /// <summary>
  /// TransitionKernel
  /// </summary>
  public abstract class TransitionKernel2D : ICloneable
  {
    #region Data

    /// <summary>
    /// Quadrature rule
    /// </summary>
    protected MultiDimensionalQuadrature quadrature_;

    /// <summary>
    /// State space of process 
    /// </summary>
    protected double[] support_;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="parameters">Model Parameters</param>
    /// <param name="adaptive">true for adaptive quadrature</param>
    /// <param name="rule">If adaptive Gauss-Konrod rule, else number of quadrature points</param>
    /// <remarks>Gauss-Konrod rules are 0 (15 points rule) to 5 to (61 points rule) </remarks>
    protected TransitionKernel2D(double[] parameters, bool adaptive, int rule)
    {
      Parameters = parameters;
      if (adaptive)
        quadrature_ = new GaussKonrodAdaptive(rule, 50, 1e-4);
      else
        quadrature_ = new MultiDimensionalGaussLegendre(Math.Max(rule, 25));
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    protected TransitionKernel2D()
    {
      Parameters = null;
      quadrature_ = null;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Brackets the interval of integration based on a step size determined by the process volatility 
    /// It stops if the pdf is below a given threshold 
    /// </summary>
    /// <param name="initial">Initial state of the process</param>
    /// <param name="lower">Lower limit of search</param>
    /// <param name="upper">Upper limit of search</param>
    /// <param name="func">Pdf of the </param>
    /// <param name="tol">Tolerance</param>
    /// <param name="direction">Direction of the search</param>
    /// <param name="step">Step size</param>
    /// <returns>A bound for the integration</returns>
    protected double Bracket(double initial, double lower, double upper, Func<double, double> func, double tol,
                             int direction, double step)
    {
      const int maxIter = 1000;
      double xnew = initial;
      double funcOld, funcNew = func(initial);
      int iter = 0;
      while (iter < maxIter)
      {
        if (direction == 1)
        {
          xnew += step;
          if (xnew >= upper)
            return upper;
        }
        if (direction == -1)
        {
          xnew -= step;
          if (xnew <= lower)
            return lower;
        }
        funcOld = funcNew;
        funcNew = func(xnew);
        if (funcNew < tol || Math.Abs(funcNew - funcOld) < tol)
          return xnew;
      }
      return xnew;
    }

    /// <summary>
    /// Get strike for volatility
    /// </summary>
    /// <param name="index"></param>
    /// <param name="f"></param>
    /// <returns></returns>
    protected double GetStrike(int index, double f)
    {
      if (index == 1)
        return (Barrier.HasValue) ? Barrier.Value : f;
      return (Barrier.HasValue && Strike.HasValue) ? Strike.Value : f;
    }

    /// <summary>
    /// Implied vol 
    /// </summary>
    /// <param name="process">process index 0 = F, 1 = L</param>
    /// <param name="f">initial</param>
    /// <param name="t">time</param>
    /// <returns></returns>
    internal abstract double ImpliedVol(int process, double f, double t);

    /// <summary>
    /// Rate model parameters
    /// </summary>
    /// <param name="process">0 = F, 1 = L</param>
    /// <returns>Rate model parameters</returns>
    /// <param name="index">Reference index</param>
    internal abstract RateModelParameters RateModelParameters(int process, ReferenceIndex index);

    /// <summary>
    /// Parameters of each process
    /// </summary>
    /// <param name="index">process index</param>
    /// <returns>process parameters position in Parameters array</returns>
    internal abstract int[] MarginalParametersIndex(int index);


    /// <summary>
    /// Integrate a given payoff 
    /// </summary>
    /// <param name="payoff">Payoff function object</param>
    /// <param name="initial">Initial state of the two dimensional process</param>
    /// <param name="t">Time to maturity</param>
    /// <returns>The expectation of the payoff given the initial state and the given parameters</returns>
    public abstract double Integrate(Payoff2DFn payoff, double[] initial, double t);

    /// <summary>
    /// Clone method
    /// </summary>
    /// <returns></returns>
    public abstract object Clone();

    #endregion

    #region Properties

    /// <summary>
    /// Parameters of the model
    /// </summary>
    public double[] Parameters { get; protected set; }

    /// <summary>
    /// For knock-out/in bivariate options, 
    /// strike is used to determine the right implied volatility for the <m>F_t</m> process. 
    /// This can be viewed as a parameter of the model
    /// </summary>
    internal double? Strike
    {
      get;
      set;
    }

    /// <summary>
    /// For knock-out/in bivariate options, 
    /// barrier is used to determine the right implied volatility for the <m>L_t</m> process.
    /// This can be viewed as a parameter of the model
    /// </summary>
    internal double? Barrier
    {
      get;
      set;
    }

    #endregion
  }

  #endregion

  #region ShiftedBGM2D

  /// <summary>
  /// <m>\\dL^\delta_t = \sigma_L (L_t + \kappa_L)\,dW^1_t</m>
  /// <m>\\dF_t = \sigma_F (F_t + \kappa_F) \,dW^2_t, </m>
  /// with <m>\langle W^1_t,W^2_t \rangle = \rho t</m> 
  /// </summary>
  public class ShiftedBgm2DPdf : TransitionKernel2D
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="parameters"> Parameters[0] = volatility of process F, Parameters[1] = shift of process F, Parameters[2] = volatility of process L, 
    /// Parameters[3] = shift of process L, Parameters[4] = Correlation between F and L</param>
    /// <param name="adaptive">True for adaptive quadrature</param>
    /// <param name="rule">If adaptive Gauss-Konrod rule, else number of quadrature points</param>
    /// <remarks>Gauss-Konrod rules are 0 (15 points rule) to 5 to (61 points rule) </remarks> 
    public ShiftedBgm2DPdf(double[] parameters, bool adaptive, int rule) :
      base(parameters, adaptive, rule)
    {
      if (parameters.Length != 5)
        throw new ArgumentException("Wrong parameter vector size. The model requires 5 parameters");
      if (parameters[0] <= 0)
        throw new ArgumentException("Invalid parameter at index 0: Diffusion coefficient should be strictly positive");
      if (parameters[2] <= 0)
        throw new ArgumentException("Invalid parameter at index 2: Diffusion coefficient should be strictly positive");
      if (parameters[4] >= 1.0 || parameters[4] <= -1.0)
        throw new ArgumentException(
          "Invalid parameter at index 4: Correlation parameter should be strictly between - 1 and + 1");
      support_ = new[] { Parameters[1], double.MaxValue, Parameters[3], double.MaxValue };
    }

    #endregion

    #region Methods

    private double Pdf(double vX, double vY, double t)
    {
      double sqrdt = Math.Sqrt(t);
      double correlation = Parameters[4];
      return BivariateNormal.density(vX, 0.0, sqrdt, vY, 0.0, sqrdt, correlation);
    }

    private double ShiftedPdf(double vF, double vL, double t, double f, double l)
    {
      double sigmaF = Parameters[0];
      double kappaF = Parameters[1];
      double sigmaL = Parameters[2];
      double kappaL = Parameters[3];
      double X = InverseTransformVariable(f, vF, sigmaF, kappaF, t);
      double Y = InverseTransformVariable(l, vL, sigmaL, kappaL, t);
      return Pdf(X, Y, t); //complete
    }

    private static double InverseTransformVariable(double x, double vX, double sigma, double kappa, double t)
    {
      double v = (vX - kappa) / (x - kappa);
      v = Math.Max(v, 1e-8);
      return (Math.Log(v) + 0.5 * sigma * sigma * t) / sigma;
    }

    private static double TransformVariable(double x, double vX, double sigma, double kappa, double t)
    {
      return kappa + (x - kappa) * Math.Exp(sigma * vX - 0.5 * sigma * sigma * t);
    }

    /// <summary>
    /// Integrate a given payoff 
    /// </summary>
    /// <param name="payoffFn">Payoff function object</param>
    /// <param name="initial">Initial state of the two dimensional process</param>
    /// <param name="t">Time to maturity</param>
    /// <returns>The expectation of the payoff given the initial state and the given parameters</returns>
    public override double Integrate(Payoff2DFn payoffFn, double[] initial, double t)
    {
      double aF, bF, aL, bL;
      int nF = (payoffFn.PayoffKinksF != null) ? payoffFn.PayoffKinksF.Length : 0;
      int nL = (payoffFn.PayoffKinksL != null) ? payoffFn.PayoffKinksL.Length : 0;
      double sigmaF = Parameters[0];
      double kappaF = Parameters[1];
      double sigmaL = Parameters[2];
      double kappaL = Parameters[3];
      GetIntegrationRegion(initial, payoffFn.IntegrationRegionF, payoffFn.IntegrationRegionL, t, out aF, out bF, out aL,
                           out bL);
      var limitsF = new UniqueSequence<double>();
      var limitsL = new UniqueSequence<double>();
      limitsF.Add(aF);
      for (int i = 0; i < nF; ++i)
      {
        double a = payoffFn.PayoffKinksF[i];
        if (a > aF && a < bF)
          limitsF.Add(a);
      }
      limitsF.Add(bF);
      limitsL.Add(aL);
      for (int i = 0; i < nL; ++i)
      {
        double a = payoffFn.PayoffKinksL[i];
        if (a > aL && a < bL)
          limitsL.Add(a);
      }
      limitsL.Add(bL);
      double retVal = 0.0;
      double f = initial[0];
      double l = initial[1];
      for (int i = 0; i < limitsF.Count - 1; i++)
      {
        aF = InverseTransformVariable(f, limitsF[i], sigmaF, kappaF, t);
        bF = InverseTransformVariable(f, limitsF[i + 1], sigmaF, kappaF, t);
        for (int j = 0; j < limitsL.Count - 1; j++)
        {
          aL = InverseTransformVariable(l, limitsL[j], sigmaL, kappaL, t);
          bL = InverseTransformVariable(l, limitsL[j + 1], sigmaL, kappaL, t);
          retVal += quadrature_.Integrate2D((vX, vY) =>
                                              {
                                                double vF = TransformVariable(f, vX, sigmaF, kappaF, t);
                                                double vL = TransformVariable(l, vY, sigmaL, kappaL, t);
                                                return payoffFn.PayoffFn(vF, vL) * Pdf(vX, vY, t);
                                              }, aF, bF, aL, bL);
        }
      }
      return retVal;
    }

    private void GetStep(double[] initial, double t, out double s0, out double s1)
    {
      double f = initial[0];
      double l = initial[1];
      double sigmaF = Parameters[0];
      double kappaF = Parameters[1];
      double varF = (f - kappaF) * (f - kappaF) * Math.Exp(sigmaF * sigmaF * t) + 2 * kappaF * f - kappaF * kappaF - f * f;
      s0 = Math.Sqrt(varF);
      double sigmaL = Parameters[2];
      double kappaL = Parameters[3];
      double varL = (l - kappaL) * (l - kappaL) * Math.Exp(sigmaL * sigmaL * t) + 2 * kappaL * l - kappaL * kappaL - l * l;
      s1 = Math.Sqrt(varL);
    }

    private void GetIntegrationRegion(double[] initial, double[] boundsF, double[] boundsL, double t, out double a0,
                                      out double b0, out double a1, out double b1)
    {
      double s0, s1;
      GetStep(initial, t, out s0, out s1);
      Func<double, double> bnd0Fn = x => ShiftedPdf(x, initial[1], t, initial[0], initial[1]);
      Func<double, double> bnd1Fn = x => ShiftedPdf(initial[0], x, t, initial[0], initial[1]);
      double minF = (boundsF != null && boundsF.Length > 0) ? Math.Max(support_[0], boundsF[0]) : support_[0];
      double maxF = (boundsF != null && boundsF.Length > 1) ? Math.Min(support_[1], boundsF[1]) : support_[1];
      double minL = (boundsL != null && boundsL.Length > 0) ? Math.Max(support_[2], boundsL[0]) : support_[2];
      double maxL = (boundsL != null && boundsL.Length > 1) ? Math.Min(support_[3], boundsL[1]) : support_[3];
      a0 = Bracket(initial[0], minF, maxF, bnd0Fn, 1e-12, -1, s0);
      b0 = Bracket(initial[0], minF, maxF, bnd0Fn, 1e-12, 1, s0);
      a1 = Bracket(initial[1], minL, maxL, bnd1Fn, 1e-12, -1, s1);
      b1 = Bracket(initial[1], minL, maxL, bnd1Fn, 1e-12, 1, s1);
    }

    /// <summary>
    /// Implied vol for f process
    /// </summary>
    /// <param name="index">index</param>
    /// <param name="f">f</param>
    /// <param name="t">t</param>
    /// <returns>IVol</returns>
    internal override double ImpliedVol(int index, double f, double t)
    {
      double sigma, kappa;
      if (index == 0)
      {
        sigma = Parameters[0];
        kappa = Parameters[1];
      }
      else
      {
        sigma = Parameters[2];
        kappa = Parameters[3];
      }
      return ShiftedLogNormal.ImpliedVolatility(f, t, GetStrike(index, f), sigma, kappa);
    }

    /// <summary>
    /// Rate model parameters
    /// </summary>
    /// <param name="process">0 = F, 1 = L</param>
    /// <param name="index">Reference index</param>
    /// <returns>Rate model parameters</returns>
    internal override RateModelParameters RateModelParameters(int process, ReferenceIndex index)
    {
      ConstParameter sigma, kappa;
      if (process == 0)
      {
        sigma = Parameters[0];
        kappa = Parameters[1];
      }
      else
      {
        sigma = Parameters[2];
        kappa = Parameters[3];
      }
      return new RateModelParameters(BaseEntity.Toolkit.Models.RateModelParameters.Model.ShiftedBGM, new[] { Param.Sigma, Param.Kappa },
                                     new[] { sigma, kappa }, index);
    }

    /// <summary>
    /// Index of parameters of each process
    /// </summary>
    /// <param name="index">process index</param>
    /// <returns>{sigma, kappa}</returns>
    internal override int[] MarginalParametersIndex(int index)
    {
      return (index == 0) ? new[] { 0, 1 } : new[] { 2, 3 };
    }

    /// <summary>
    /// Clone method
    /// </summary>
    /// <returns>A deep copy of the object</returns>
    public override object Clone()
    {
      var parameters = (double[])Parameters.Clone();
      bool adaptive = quadrature_ is GaussKonrodAdaptive;
      return new ShiftedBgm2DPdf(parameters, adaptive, quadrature_.Rule);
    }

    #endregion
  }

  #endregion

  #region LogNormalSabrApproxPdf

  /// <summary>
  /// <m>\\dL^\delta_t = \sigma V_t L^{\gamma}_t\,dW^1_t</m>
  /// <m>\\dV^L_t = \nu^L V^L_t \,dW^2_t, </m> 
  /// <m>\\dF_t = \alpha V_t F^{\beta}_t \,dW^3_t, </m>
  /// <m>\\dV^F_t = \nu^F V^F_t \,dW^4_t, </m>
  /// with <m>\langle W^1_t,W^2_t \rangle = \rho_{12} t</m>, <m>\langle W^3_t,W^4_t \rangle = \rho_{34} t</m> and  <m>\langle W^1_t,W^3_t \rangle = \rho_{13} t</m> 
  /// </summary>
  ///<remarks>This model should be used only if one knows exactly what implied volatility to use for stock and rate processes</remarks>
  public class LogNormalSabrApproxPdf : TransitionKernel2D
  {
    #region Data

    private readonly ShiftedBgm2DPdf kernel_;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="parameters">Parameters:  parameters[0] = volatility of F (alpha), Parameters[1] = elasticity of F(beta), Parameters[2] = vol vol (nu), 
    /// parameters[3] = correlation between F process and stochastic volatility (rho) , Parameters[4] = volatility of L, Parameters[5] = elasticity of L (gamma),
    /// Parameters[6] = vol vol of rate, Parameters[7] = correlation between brwonian motion driving rate process and its own volatility, 
    /// Parameters[8] = Correlation between  brownian motions driving F and L</param>
    /// <param name="adaptive">True for adaptive quadrature</param>
    /// <param name="rule">If adaptive Gauss-Konrod rule, else number of quadrature points</param>
    /// <remarks>Gauss-Konrod rules are 0 (15 points) to 5 (61 points) </remarks>
    public LogNormalSabrApproxPdf(double[] parameters, bool adaptive, int rule)
    {
      if (parameters.Length != 8)
        throw new ArgumentException("Wrong parameter vector size. The model requires 8 parameters");
      if (parameters[0] <= 0)
        throw new ArgumentException("Invalid parameter at index 0: Diffusion coefficient should be strictly positive");
      if (parameters[1] < 0 || parameters[1] > 1)
        throw new ArgumentException(
          "Invalid parameter at index 1: Elasticity coefficient should be be between zero and one inclusive");
      if (parameters[2] <= 0)
        throw new ArgumentException(
          "Invalid parameter at index 2 : Stochastic volatility diffusion coefficient should be strictly positive");
      if (parameters[3] >= 1.0 || parameters[3] <= -1.0)
        throw new ArgumentException(
          "Invalid parameter at index 3: Correlation parameter should be strictly between - 1 and + 1");
      if (parameters[4] <= 0)
        throw new ArgumentException("Invalid parameter at index 4: Diffusion coefficient should be strictly positive");
      if (parameters[5] < 0 || parameters[5] > 1)
        throw new ArgumentException(
          "Invalid parameter at index 5 : Elasticity coefficient should be be between zero and one inclusive");
      if (parameters[6] >= 1.0 || parameters[6] <= -1.0)
        throw new ArgumentException(
          "Invalid parameter at index 6: Correlation parameter should be strictly between - 1 and + 1");
      if (parameters[7] >= 1.0 || parameters[7] <= -1.0)
        throw new ArgumentException(
          "Invalid parameter at index 7: Correlation parameter should be strictly between - 1 and + 1");
      support_ = new[] { 0.0, double.MaxValue, 0.0, double.MaxValue };
      double rhoFL = parameters[7];
      Parameters = parameters;
      kernel_ = new ShiftedBgm2DPdf(new[] { 1.0, 0.0, 1.0, 0.0, rhoFL }, adaptive, rule);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Integrate a given payoff 
    /// </summary>
    /// <param name="payoffFn">Payoff function object</param>
    /// <param name="initial">Initial state of the two dimensional process</param>
    /// <param name="t">Time to maturity</param>
    /// <returns>The expectation of the payoff given the initial state and the given parameters</returns>
    public override double Integrate(Payoff2DFn payoffFn, double[] initial, double t)
    {
      //create a shifted bgm pdf and redirect 
      if (Strike == null || Barrier == null)
        throw new ArgumentException("Strike and Barrier values need to be set before calling the Integrate function");
      double sigmaF = ImpliedVol(0, initial[0], t);
      double sigmaL = ImpliedVol(1, initial[1], t);
      kernel_.Parameters[0] = sigmaF;
      kernel_.Parameters[2] = sigmaL;
      return kernel_.Integrate(payoffFn, initial, t);
    }

    /// <summary>
    /// Implied vol for f process
    /// </summary>
    /// <param name="index">process index</param>
    /// <param name="f">f</param>
    /// <param name="t">t</param>
    /// <returns>IVol</returns>
    internal override double ImpliedVol(int index, double f, double t)
    {
      double alpha, beta, nu, rho;
      if (index == 0)
      {
        alpha = Parameters[0];
        beta = Parameters[1];
        nu = Parameters[2];
        rho = Parameters[3];
      }
      else
      {
        alpha = Parameters[4];
        beta = Parameters[5];
        nu = Parameters[2];
        rho = Parameters[6];
      }
      return Sabr.ImpliedVolatility(f, t, GetStrike(index, f), alpha, beta, nu, rho);
    }

    /// <summary>
    /// Rate model parameters
    /// </summary>
    /// <param name="process">0 = F, 1 = L</param>
    /// <param name="index">Reference index</param>
    /// <returns>Rate model parameters</returns>
    internal override RateModelParameters RateModelParameters(int process, ReferenceIndex index)
    {
      ConstParameter alpha, beta, nu, rho;
      if (process == 0)
      {
        alpha = Parameters[0];
        beta = Parameters[1];
        nu = Parameters[2];
        rho = Parameters[3];
      }
      else
      {
        alpha = Parameters[4];
        beta = Parameters[5];
        nu = Parameters[2];
        rho = Parameters[6];
      }
      return new RateModelParameters(BaseEntity.Toolkit.Models.RateModelParameters.Model.SABR,
                                     new[] { Param.Alpha, Param.Beta, Param.Nu, Param.Rho },
                                     new[] { alpha, beta, nu, rho }, index);
    }

    /// <summary>
    /// Parameters of each process
    /// </summary>
    /// <param name="index">process index</param>
    /// <returns>{alpha, beta, nu, rho}</returns>
    internal override int[] MarginalParametersIndex(int index)
    {
      return (index == 0) ? new[] { 0, 1, 2, 3 } : new[] { 4, 5, 2, 6 };
    }

    /// <summary>
    /// Clone method
    /// </summary>
    /// <returns>A deep copy of the object</returns>
    public override object Clone()
    {
      var parameters = (double[])Parameters.Clone();
      bool adaptive = quadrature_ is GaussKonrodAdaptive;
      var retVal = new LogNormalSabrApproxPdf(parameters, adaptive, quadrature_.Rule);
      retVal.Barrier = Barrier;
      retVal.Strike = Strike;
      return retVal;
    }

    #endregion
  }

  #endregion
}
