/*
 *   2016. All rights reserved.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;
using BTree = BaseEntity.Toolkit.Models.BinomialEnumerations;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// A simple symmetric binomial tree of the Hull-White short rate model,
  /// to be used in the convertible bond evaluation.
  /// </summary>
  /// <seealso cref="IBinomialShortRateTreeModel" />
  [Serializable]
  public class HullWhiteBinomialTreeModel : IBinomialShortRateTreeModel
  {
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="HullWhiteBinomialTreeModel" /> class.
    /// </summary>
    /// <param name="kappa">Mean reversion speed</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="start">Start date of the interest rate binomial tree</param>
    /// <param name="maturity">Maturity of the tree</param>
    /// <param name="stepCount">Number of layers of the tree</param>
    /// <param name="discountCurve">Initial discount curve used to adjust rate tree</param>
    public HullWhiteBinomialTreeModel(double kappa, double sigma,
      Dt start, Dt maturity, int stepCount, DiscountCurve discountCurve)
    {
      if (stepCount <= 0)
        throw new ArgumentException($"Step count must be positive, not {stepCount}");
      if (sigma < 0)
        throw new ArgumentException($"Sigma must be non-negative, not {sigma}");
      Kappa = kappa;
      Sigma = sigma;
      Start = start;
      Maturity = maturity;
      StepCount = stepCount;
      DiscountCurve = discountCurve;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Builds the tree from the scratch.
    /// </summary>
    private void BuildTree()
    {
      var dt = (Maturity - Start)/365/StepCount;
      _rateTree = BuildRateStarTree(Kappa, Sigma, dt, StepCount);
      _dfTree = FitDiscountCurve(_rateTree, dt, Start, DiscountCurve);
    }

    /// <summary>
    /// Builds the rate star tree.
    /// </summary>
    /// <param name="kappa">The kappa.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="dt">The length in years of each step</param>
    /// <param name="stepCount">Number of steps</param>
    /// <returns>System.Double[][].</returns>
    public static double[][] BuildRateStarTree(
      double kappa, double sigma, double dt, int stepCount)
    {
      var g = Math.Exp(-kappa*dt);
      var d = Math.Sqrt(QMath.Expd1(-2*kappa*dt)*dt)*sigma;

      var tree = new double[stepCount + 1][];
      var x0 = tree[0] = new[] {0.0};
      for (int k = 1; k <= stepCount; ++k)
      {
        var xk = tree[k] = new double[k + 1];
        var variance = 0.0;

        var sumbp = BinomialEnumerations.ForEachNodeTrimmed(k, (j, bp) =>
        {
          double xkj;
          if (j == 0)
          {
            xkj = xk[j] = g*x0[0] - d;
          }
          else if (j == k)
          {
            xkj = xk[j] = g*x0[k - 1] + d;
          }
          else
          {
            double p = (k - j)/(double)k, q = j/(double)k;
            xkj = xk[j] = q*(g*x0[j - 1] + d) + p*(g*x0[j] - d);
          }
          variance += bp*xkj*xkj;
        });
        variance /= sumbp;

        var t = k*dt;
        var scale = sigma < 1E-64 ? 0.0 :
          Math.Sqrt(QMath.Expd1(-2*kappa*t)*t/variance)*sigma;
        for (uint j = 0; j <= k; ++j)
          xk[j] *= scale;

        x0 = xk;
      }

      return tree;
    }

    /// <summary>
    /// Fits the discount curve.
    /// </summary>
    /// <param name="rateStarTree">The rate star tree.</param>
    /// <param name="dt">The dt.</param>
    /// <param name="begin">The begin.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>System.Double[][].</returns>
    public static double[][] FitDiscountCurve(
      double[][] rateStarTree, double dt,
      Dt begin, DiscountCurve discountCurve)
    {
      Debug.Assert(rateStarTree.Length > 0);
      int n = rateStarTree.Length - 1;
      var dfTree = new double[n + 1][];
      dfTree[0] = new[] {1.0};

      var curve = discountCurve.NativeCurve;
      var asOf = discountCurve.AsOf;
      var t0 = begin - asOf;
      var df = curve.Evaluate(t0);
      for (int k = 1; k <= n; ++k)
      {
        var df0 = df;

        var mean = 0.0;
        var x = rateStarTree[k];
        var sumbp = BinomialEnumerations.ForEachNodeTrimmed(k, (j, bp) =>
        {
          mean += bp*Math.Exp(-x[j]*dt);
        });
        mean /= sumbp;

        df = curve.Evaluate(t0 + k*365*dt);
        var drift = Math.Log(df/df0/mean)/dt;

        var p = dfTree[k] = new double[k + 1];
        for (int j = 0; j <= k; ++j)
        {
          var r = (x[j] -= drift);
          p[j] = Math.Exp(-r*dt);
        }
      }
      return dfTree;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    public DiscountCurve DiscountCurve { get; }

    /// <summary>
    /// Gets the start date
    /// </summary>
    /// <value>The start.</value>
    public Dt Start { get; }

    /// <summary>
    /// Gets the maturity date
    /// </summary>
    /// <value>The maturity.</value>
    public Dt Maturity { get; }

    /// <summary>
    /// Gets the mean reversion parameter
    /// </summary>
    /// <value>The kappa.</value>
    public double Kappa { get; }

    /// <summary>
    /// Gets the volatility parameter
    /// </summary>
    /// <value>The sigma.</value>
    public double Sigma { get; }

    /// <summary>
    /// Gets the number of steps
    /// </summary>
    /// <value>The step count.</value>
    public int StepCount { get; }

    #endregion

    #region Data

    /// <summary>
    /// The rate tree and discount factor tree
    /// </summary>
    private double[][] _rateTree, _dfTree;

    #endregion

    #region IBinomialShortRateTreeModel members

    /// <summary>
    /// Create a new tree with the short rate volatility
    /// bumped uniformly by the specified size
    /// </summary>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <returns>IBinomialShortRateTreeModel.</returns>
    public IBinomialShortRateTreeModel BumpSigma(double bumpSize)
    {
      return new HullWhiteBinomialTreeModel(Kappa, Sigma + bumpSize,
        Start, Maturity, StepCount, DiscountCurve);
    }

    /// <summary>
    /// Gets the discount factor tree.
    /// </summary>
    /// <returns>IReadOnlyList&lt;System.Double[]&gt;.</returns>
    public IReadOnlyList<double[]> GetDiscountFactorTree()
    {
      if (_dfTree == null) BuildTree();
      return _dfTree;
    }

    /// <summary>
    /// Gets the short rate tree.
    /// </summary>
    /// <returns>IReadOnlyList&lt;System.Double[]&gt;.</returns>
    public IReadOnlyList<double[]> GetRateTree()
    {
      if (_rateTree == null) BuildTree();
      return _rateTree;
    }

    /// <summary>
    /// Sets the rate tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    public void SetRateTree(IReadOnlyList<double[]> tree)
    {
      var n = tree.Count - 1;
      var dt = (Maturity - Start)/n/365;
      var rateTree = new double[n + 1][];
      rateTree[0] = new[] {tree[0][0]};

      var dfTree = new double[n + 1][];
      dfTree[0] = new[] {1.0};

      for (int k = 1; k <= n; k++)
      {
        var r = rateTree[k] = (double[]) tree[k].Clone();
        var p = dfTree[k] = new double[k + 1];
        for (int j = 0; j <= k; ++j)
        {
          p[j] = Math.Exp(-r[j]*dt);
        }
      }
      return;
    }

    #endregion
  }

  static class BinomialEnumerations
  {
    public static double ForEachNodePlain(
      int n, Action<int, double> action)
    {
      var sum = 0.0;
      for (int j = 0; j <= n; ++j)
      {
        var bp = Binomial(j, n);
        action(j, bp);
        sum += bp;
      }
      return sum;
    }

    public static double ForEachNodeTrimmed(
      int n, Action<int, double> action)
    {
      const double CutOff = 1E-20;

      int mid = n/2;
      var midProb = Binomial(mid, n);
      action(mid, midProb);
      var sum = midProb;

      // move left
      var prob = midProb;
      for (int j = mid; --j >= 0;)
      {
        //! <math>
        //!   \binom{j}{n} = \frac{n!}{j!\,(n-j)!}
        //!   = \frac{j+1}{n-j}\binom{j+1}{n}
        //! </math>
        prob *= (j + 1.0)/(n - j);
        if (prob < CutOff) break;
        action(j, prob);
        sum += prob;
      }

      // move right
      prob = midProb;
      for (int j = mid; ++j <= n;)
      {
        //! <math>
        //!   \binom{j}{k} = \frac{k!}{j!\,(k-j)!}
        //!   = \frac{k-j+1}{j}\binom{j-1}{k}
        //! </math>
        prob *= (n - j + 1.0)/j;
        if (prob < CutOff) break;
        action(j, prob);
        sum += prob;
      }

      return sum;
    }

    private static double Binomial(int j, int k)
    {
      Debug.Assert(j>=0);
      Debug.Assert(k>=0);
      return QMath.BinomialPdf((uint)j, (uint)k, 0.5);
    }

  }
}
