using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.calibrators
{

  /// <summary>
  ///  Hull White real world calibration function
  /// </summary>
  [Serializable]
  public class HullWhiteRealWorldCalibrator
  {
    /// <summary>
    ///  Sigma
    /// </summary>
    public double Sigma { get; set; }

    /// <summary>
    ///  Mean Reversion
    /// </summary>
    public double MeanReversion { get; set; }

    private readonly double[] _t;
    private readonly double[] _values;
    private readonly DiscountCurve _rateCurve;
    private double _sigma;
    private double _kappa;
    private readonly double _tol;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rateCurve">Interest rate curve</param>
    /// <param name="resets">Historic rate resets</param>
    /// <param name="sigma">Sigma initial guess</param>
    /// <param name="kappa">Kappa initial guess or kappa value if kappa is fixed</param>
    /// <param name="tol">Solver tolerance</param>
    public HullWhiteRealWorldCalibrator(DiscountCurve rateCurve, RateResets resets, double sigma, double kappa, double tol)
    {
      _rateCurve = rateCurve;
      Sigma = sigma;
      _kappa = kappa;
      _tol = tol;

      _t = new double[resets.AllResets.Count];
      _values = new double[resets.AllResets.Count];

      var i = 0;
      foreach (var r in resets.AllResets.OrderBy(o => o.Key))
      {
        _t[i] = r.Key.ToDateTime().ToOADate();
        _values[i] = r.Value;
        i++;
      }
    }

    /// <summary>
    ///  Find MLE estimates
    /// </summary>
    public void Optimise()
    {
      // Initial guess for sigma
      var av = _values.Average();
      var sum = _values.Sum(d => Math.Pow(d - av, 2));
      _sigma = Math.Sqrt(sum / (_values.Count() - 1));

      var f = new List<double>();
      var x = new List<double>(1);
      x.Add(_sigma);
      f.Add(double.MaxValue);
      EvaluateSigma(x, f, null);
      var mle = f[0];
      for (var j = 1; j < 20; j++)
      {
        x[0] = 0.005 * j;
        EvaluateSigma(x, f, null);
        if (f[0] < mle)
        {
          _sigma = 0.005 * j;
          mle = f[0];
        }
      }

      Sigma = OptimizeParameter(_sigma, EvaluateSigma);

      // Initial guess for kappa
      var g = new List<double>();
      g.Add(double.MaxValue);

      x[0] = 0.05;
      EvaluateKappa(x, g, null);
      var mleKappa = g[0];
      for (var j = 2; j < 20; j++)
      {
        x[0] = 0.05 * j;
        EvaluateKappa(x, g, null);
        if (g[0] < mleKappa)
        {
          _kappa = 0.01 * j;
          mleKappa = g[0];
        }
      }

      MeanReversion = OptimizeParameter(_kappa, EvaluateKappa);
    }

    private double OptimizeParameter(double guess, Action<IReadOnlyList<double>, IList<double>, IList<double>> func)
    {
      var opt = new BFGSB(1);
      opt.setToleranceF(_tol);
      opt.setMaxIterations(int.MaxValue);
      var optimizerFn = DelegateOptimizerFn.Create(1, 1, func, false); // derivative not implemented!

      opt.setInitialPoint(new[] { guess });
      opt.setLowerBounds(new[] { 0.001 }); // Sigma and Mean Reversion should not be zero or negative
      opt.Minimize(optimizerFn);
      return opt.CurrentSolution[0];
    }

    /// <summary>
    /// Returns rate model parameters
    /// </summary>
    /// <returns></returns>
    public RateModelParameters GetRateModelParameters()
    {
      return new RateModelParameters(RateModelParameters.Model.Hull,
        new[] { RateModelParameters.Param.Sigma, RateModelParameters.Param.MeanReversion },
        new IModelParameter[] { new Curve(_rateCurve.AsOf, Sigma), new Curve(_rateCurve.AsOf, MeanReversion) },
        Tenor.ThreeMonths, _rateCurve.Ccy);
    }

    /// <summary>
    ///  Evaluate maximum likelihood for a given input vector x
    /// </summary>
    /// <param name="x">[0] Sigma, [1] Kappa</param>
    /// <param name="f">Function return</param>
    /// <param name="g">Derivatives</param>
    public void EvaluateSigma(IReadOnlyList<double> x, IList<double> f, IList<double> g)
    {
      if (f.Count == 1)
      {
        f[0] = -MaximumLikelihoodEstimate(x[0], _kappa);
      }
    }

    /// <summary>
    ///  Evaluate maximum likelihood for a given input vector x
    /// </summary>
    /// <param name="x">[0] Sigma, [1] Kappa</param>
    /// <param name="f">Function return</param>
    /// <param name="g">Derivatives</param>
    public void EvaluateKappa(IReadOnlyList<double> x, IList<double> f, IList<double> g)
    {
      if (f.Count == 1)
      {
        f[0] = -MaximumLikelihoodEstimate(Sigma, x[0]);
      }
    }

    /// <summary>
    ///  Evaluate MLE for a given sigma and kappa
    /// </summary>
    /// <param name="sigma"></param>
    /// <param name="kappa"></param>
    /// <returns></returns>
    public double MaximumLikelihoodEstimate(double sigma, double kappa)
    {
      var sigmaSq = sigma * sigma;
      var kappaSq = kappa * kappa;

      var asOf = new Dt(DateTime.FromOADate(_t[0]));
      var shift = _rateCurve.AsOf.ToDateTime().ToOADate() - asOf.ToDateTime().ToOADate();

      var likelihood = 0.0;
      for (var i = 1; i < _t.Length; i++)
      {
        var sDt = new Dt(DateTime.FromOADate(_t[i - 1]));
        var tDt = new Dt(DateTime.FromOADate(_t[i]));
        var rs = _values[i - 1];
        var rt = _values[i];

        var dt = Dt.Years(sDt, tDt, DayCount.Actual365Fixed);
        var t = Dt.Years(asOf, tDt, DayCount.Actual365Fixed);
        var s = Dt.Years(asOf, sDt, DayCount.Actual365Fixed);

        var expkdt = Math.Exp(-kappa * dt);
        var expkdt2 = Math.Exp(-2.0 * kappa * dt);

        var variance = 0.5 * sigmaSq / kappa * (1 - expkdt2);
        var sF = _rateCurve.F(_rateCurve.AsOf, new Dt(DateTime.FromOADate(
          Math.Max(_rateCurve.AsOf.ToDateTime().ToOADate() + 1, sDt.ToDateTime().ToOADate() + shift))));
        var tF = _rateCurve.F(_rateCurve.AsOf, new Dt(DateTime.FromOADate(tDt.ToDateTime().ToOADate() + shift)));

        var tAlpha = tF + 0.5 * sigmaSq / kappaSq * Math.Pow(1 - Math.Exp(-kappa * t), 2);
        var sAlpha = sF + 0.5 * sigmaSq / kappaSq * Math.Pow(1 - Math.Exp(-kappa * s), 2);

        var mean = rs * expkdt + tAlpha - sAlpha * expkdt;

        likelihood += -Math.Log(Math.PI) - Math.Log(variance) - Math.Pow(rt - mean, 2) / variance;
      }
      return likelihood;
    }
  }

}
