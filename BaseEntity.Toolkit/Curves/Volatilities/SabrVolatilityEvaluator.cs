using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using static System.Math;
using static BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   Represent <abbr>SABR</abbr> smile at a given date.
  /// </summary>
  /// <remarks>
  ///  <para>In the regular <abbr>SABR</abbr> model, the dynamics of the forward
  ///   <m>F_t</m> is given by the following stochastic differential
  ///   equation<math>\begin{align}
  ///       dF_t &amp;= \hat{\alpha}_t\,C(F_t)\,d W_{1t}
  ///    \\ d\hat{\alpha}_t &amp;= \nu\,\hat{\alpha}_t\,d W_{2t}
  ///    \\ d\langle W_{1t}, W_{2t} \rangle &amp; = \rho\,dt
  ///       ,\quad F_0 = F,\; \hat{\alpha}_0 = \alpha
  ///   \end{align}</math>where the function <m>C(\cdot)</m> takes several forms
  ///   <math>\begin{align}
  ///     C(f) &amp;= f^\beta &amp; \text{regular SABR}
  ///    \\ C(f) &amp;= (f + b)^\beta &amp; \text{shifted SABR}
  ///    \\ C(f) &amp;= 1 &amp; \text{normal SABR}
  ///    \\ C(f) &amp;= |f|^\beta &amp; \text{free SABR}
  ///   \end{align}</math></para>
  /// 
  /// <b>Normal approximation</b>
  /// <inheritdoc cref="CalculateNormalVolatility(double, double, double, double, double, double, double)" />
  /// 
  /// <b>Log-normal approximation</b>
  /// <inheritdoc cref="CalculateVolatility(double, double, double, double, double, double)" />
  ///
  /// <inheritdoc cref="ChiRatio(double, double)" />
  /// </remarks>
  [Serializable]
  public class SabrVolatilitySmile
  {
    private static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SabrVolatilitySmile));

    #region Static methods

    #region Shifted SABR

    /// <summary>
    /// Calculates the log-normal volatility.
    /// </summary>
    /// <param name="alpha">The alpha.</param>
    /// <param name="beta">The beta.</param>
    /// <param name="nu">The nu.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="time">The time.</param>
    /// <returns>System.Double.</returns>
    public static double CalculateVolatility(
      double alpha, double beta, double nu, double rho,
      double shift, double forward, double strike, double time)
    {
      forward += shift;
      strike += shift;
      return CalculateVolatility(alpha*Math.Pow(forward, beta - 1),
        beta, nu, rho, strike/forward, time);
    }



    #endregion

    /// <summary>
    /// Calculates the log-normal volatility.
    /// </summary>
    /// <param name="alpha">The alpha.</param>
    /// <param name="beta">The beta.</param>
    /// <param name="nu">The nu.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="time">The time.</param>
    /// <returns>System.Double</returns>
    public static double CalculateVolatility(double alpha, double beta,
      double nu, double rho, double forward, double strike, double time)
    {
      return CalculateVolatility(alpha*Math.Pow(forward, beta - 1),
        beta, nu, rho, strike/forward, time);
    }

    /// <summary>
    /// Calculates the normal volatility.
    /// </summary>
    /// <param name="alpha">The alpha.</param>
    /// <param name="beta">The beta.</param>
    /// <param name="nu">The nu.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="time">The time.</param>
    /// <returns>The volatility</returns>
    /// <remarks>
    ///  The implied normal volatility is approximated by<math>
    ///   \sigma_n = \nu\,\frac{F-K}{\chi(\xi)}\left\{1 + \left[
    ///     \frac{2\gamma_2 - \gamma_1^2}{24}\left(
    ///       \alpha\,C(F_{\mathrm{mid}})
    ///     \right)^2
    ///    + \frac{\rho\gamma_1}{4}\alpha C(F_{\mathrm{mid}})\nu
    ///    + \frac{2 - 3\rho^2}{24}\nu^2
    ///   \right]T \right\}
    ///  </math>where <m>F_{\mathrm{mid}} = \sqrt{F K}</m>, and<math>\begin{align}
    ///    \xi &amp;\equiv \frac{\nu}{\alpha}\frac{F^{1-\beta} - K^{1-\beta}}{1-\beta}
    ///   \\ \chi(\xi) &amp;\equiv \log\left(\frac{\sqrt{1-2\rho\xi +\xi^2}+\xi-\rho}{1-\rho}\right)
    ///   \\ \gamma_1 &amp;\equiv \frac{C^\prime(F_{\mathrm{mid}})}{C(F_{\mathrm{mid}})}
    ///      = \frac{\beta}{F_{\mathrm{mid}}}
    ///   \\ \gamma_2 &amp;\equiv  \frac{C^{\prime\prime}(F_{\mathrm{mid}})}{C(F_{\mathrm{mid}})}
    ///      = \frac{\beta(\beta-1)}{F_{\mathrm{mid}}^2}
    ///  \end{align}</math>
    /// </remarks>
    public static double CalculateNormalVolatility(double alpha, double beta,
      double nu, double rho, double forward, double strike, double time)
    {
      double m = strike/forward, logm = Log(m),
        //dd = (Pow(forward, 1 - beta) - Pow(strike, 1 - beta))/(1 - beta),
        d = -Expd1(logm*(1 - beta))*logm*Pow(forward, 1 - beta),
        z = d*nu/alpha;
      double fm = Sqrt(forward*strike),
        gamma1 = beta/fm,
        gamma2 = gamma1*(beta - 1)/fm,
        aC = alpha*Pow(fm, beta);
      double //dr0 = (Pow(forward, 1 - beta) - Pow(strike, 1 - beta))/(forward - strike),
        dr = Pow(forward, -beta)*Powd1(m - 1, 1 - beta);
      double //term00 = nu*(forward - strike)/Log((Sqrt(1 - 2*rho*z + z*z) + z - rho)/(1 - rho)),
        term0 = alpha*(1 - beta)/dr/ChiRatio(z, rho);
      var term1 = (2*gamma2 - gamma1*gamma1)*aC*aC/24
        + rho*gamma1*aC*nu/4 + (2 - 3*rho*rho)*nu*nu/24;
      return term0*(1 + term1*time);
    }

    /// <summary>
    ///   Calculates the volatility.
    /// </summary>
    /// <param name="zeta">The instantaneous local volatility at the expiry.</param>
    /// <param name="beta">The parameter <m>\beta</m>.</param>
    /// <param name="nu">The vol-vol parameter <m>\nu</m>.</param>
    /// <param name="rho">The correlation parameter <m>\rho</m>.</param>
    /// <param name="moneyness">The moneyness (strike divided by the forward price).</param>
    /// <param name="time">The time to expiry.</param>
    /// <returns>The volatility</returns>
    /// <remarks>
    ///   Let <m>m = K/F</m> be the measure of moneyness and <m>\zeta = \alpha F^{\beta-1}</m>
    ///   be the instantaneous local volatility,
    ///   where <m>F</m> is the forward price and <m>K</m> is the strike.  Then SABR volatility
    ///   can be written as<math>
    ///     \sigma_B(m) = \Xi(m)\,\frac{z}{\chi(z)}\,\Psi(m)
    ///   </math>where<math>
    ///     \Xi(m) = \frac{\zeta}{m^{\frac{1-\beta}{2}}\left[
    ///     1 + \frac{(1-\beta)^2}{24}\log^2(m) + \frac{(1-\beta)^4}{1920}\log^4(m)
    ///     \right]}
    ///   </math><math>
    ///     \Psi(m) = 1 + \left[\frac{(1-\beta)^2 \zeta^2}{24 m^{1-\beta}}
    ///     + \frac{\rho\beta\nu\zeta}{4 m^{\frac{1-\beta}{2}}}
    ///     + \nu^2\frac{2-3\rho^2}{24}
    ///     \right] T
    ///   </math><math>
    ///     z = -\frac{\nu}{\zeta}m^{\frac{1-\beta}{2}}\log(m)
    ///   </math><math>
    ///     \chi(z) = \log\left(\frac{\sqrt{1-2\rho z +z^2}+z-\rho}{1-\rho}\right)
    ///   </math>
    ///   If we define <m>\tilde{m}=m^{\frac{1-\beta}{2}}</m>, the above formula can be simplified
    ///   a little further<math>
    ///     \Xi(m) = \frac{\zeta}{\tilde{m}\left[
    ///     1 + \frac{\log^2(\tilde{m})}{6} + \frac{\log^4(\tilde{m})}{120}
    ///     \right]}
    ///   </math><math>
    ///     \Psi(m) = 1 + \left[\frac{(1-\beta)^2 \zeta^2}{24 \tilde{m}^2}
    ///     + \frac{\rho\beta\nu\zeta}{4 \tilde{m}}
    ///     + \nu^2\frac{2-3\rho^2}{24}
    ///     \right] T
    ///   </math>
    ///   It might be more stable to calibrate in the parameter space <m>(\zeta,\beta,\nu,\rho)</m>.
    /// </remarks>
    public static double CalculateVolatility(double zeta, double beta,
      double nu, double rho, double moneyness, double time)
    {
      var b1 = 1 - beta;
      var logm = Math.Log(moneyness);
      var logmt = b1*logm/2.0;
      var mt = Math.Exp(logmt);
      var logmt2 = logmt*logmt;
      var denom = mt*(1 + (1 + logmt2/20)*logmt2/6);
      var num = 1 + (b1*b1*zeta*zeta/24/mt/mt +
        rho*beta*nu*zeta/4/mt + nu*nu*(2 - 3*rho*rho)/24)*time;
      var z = -nu/zeta*mt*logm;
      return zeta/denom/ChiRatio(z, rho)*num;
    }

    /// <summary>
    ///   Calibrates the SABR parameters with normal volatilities.
    /// </summary>
    /// <param name="time">The time to expiry</param>
    /// <param name="forward">The forward value</param>
    /// <param name="strikes">The array of strike values</param>
    /// <param name="volatilities">The array of volatilities by strikes</param>
    /// <param name="upperBounds">The upper bounds of <c>[alpha, beta, nu, rho]</c></param>
    /// <param name="lowerBounds">The lower bounds of <c>[alpha, beta, nu, rho]</c></param>
    /// <returns>An array of 4 elements, <c>[alpha, beta, nu, rho]</c>, in that order.</returns>
    public static double[] CalibrateParametersWithNormalVolatilities(
      double time, double forward,
      double[] strikes, double[] volatilities,
      double[] lowerBounds, double[] upperBounds)
    {
      if (strikes == null || strikes.Length == 0)
        throw new ArgumentException("The array of strikes is empty");
      if (volatilities == null || volatilities.Length != strikes.Length)
        throw new ArgumentException("volatilities and strikes not match");
      if (forward < 1E-16)
        throw new ArgumentException("Negative or near zero forward not supported yet");
      var moneyness = new double[strikes.Length];
      var vols = new double[strikes.Length];
      for (int i = 0; i < vols.Length; ++i)
      {
        var k = strikes[i];
        moneyness[i] = k/forward;
        vols[i] = LogNormalToNormalConverter.NormalToLogNormal(
          forward, k, time, volatilities[i]);
      }

      var lb = lowerBounds;
      if (lb.Length > 0) lb = (double[]) lowerBounds.Clone();

      var ub = upperBounds;
      if (ub.Length > 0) ub = (double[]) upperBounds.Clone();

      var beta = 2.0;
      if (lb.Length > 1 && ub.Length > 1 && lb[1].AlmostEquals(ub[1]))
        beta = 0.5*(lb[1] + ub[1]);

      double[] x = null;
      for (int i = 0; i < 10; ++i)
      {
        if (lb.Length > 0 && lb[0] > 0)
        {
          lb[0] *= Math.Pow(forward, beta - 1);
        }

        if (ub.Length > 0 && ub[0] < double.MaxValue)
        {
          ub[0] *= Math.Pow(forward, beta - 1);
        }

        x = CalibrateParameters(time, moneyness, vols, lb, ub,
          SabrModelFlags.ImprovedSabrInitialValues);
        beta = x[1];
        var alpha = x[0]*Math.Pow(forward, 1 - beta);
        x[0] = alpha;
        if (double.IsNaN(alpha) || (
          (lowerBounds.Length == 0 || lowerBounds[0] <= alpha)
          && (upperBounds.Length == 0 || upperBounds[0] >= alpha)))
        {
          break;
        }
      }
      return x;
    }

    /// <summary>
    ///   Calibrates the SABR parameters.
    /// </summary>
    /// <param name="time">The time to expiry</param>
    /// <param name="moneyness">The array of moneyness</param>
    /// <param name="volatilities">The array of volatilities</param>
    /// <param name="upperBounds">The upper bounds of <c>[zeta, beta, nu, rho]</c></param>
    /// <param name="lowerBounds">The lower bounds of <c>[zeta, beta, nu, rho]</c></param>
    /// <returns>An array of 4 elements, <c>[zeta, beta, nu, rho]</c>, in that order.</returns>
    public static double[] CalibrateParameters(
      double time, double[] moneyness, double[] volatilities,
      double[] lowerBounds, double[] upperBounds)
    {
      return CalibrateParameters(time, moneyness, volatilities,
        lowerBounds, upperBounds, SabrModelFlags.ImprovedSabrInitialValues);
    }

    /// <summary>
    ///   Calibrates the SABR parameters.
    /// </summary>
    /// <param name="time">The time to expiry</param>
    /// <param name="moneyness">The array of moneyness</param>
    /// <param name="volatilities">The array of volatilities</param>
    /// <param name="upperBounds">The upper bounds of <c>[zeta, beta, nu, rho]</c></param>
    /// <param name="lowerBounds">The lower bounds of <c>[zeta, beta, nu, rho]</c></param>
    /// <param name="sabrModelFlags">The SABR model flags.</param>
    /// <returns>An array of 4 elements, <c>[zeta, beta, nu, rho]</c>, in that order.</returns>
    public static double[] CalibrateParameters(
      double time, double[] moneyness, double[] volatilities,
      double[] lowerBounds, double[] upperBounds,
      SabrModelFlags sabrModelFlags)
    {
      if (!(time > 0))
        throw new Exception("Time to expiry must be positive");

      // Sanity check of volatilities
      if (moneyness == null || moneyness.Length == 0)
        throw new ToolkitException("moneyness cannot be empty");
      if (volatilities == null || volatilities.Length == 0)
        throw new ToolkitException("volatilities cannot be empty");
      if (moneyness.Length != volatilities.Length)
        throw new ToolkitException("volatilities and moneyness not match");
#if DEBUG
      if (moneyness.Any(m => m <= 0))
        throw new ToolkitException("moneyness must be positive");
      if (volatilities.Any(v => v <= 0))
        throw new ToolkitException("volatilities must be positive");
#endif

      // Sanity check for the lower bounds
      UniqueArray<double> lowBounds = lowerBounds, uppBounds = upperBounds;
      if (lowBounds.Length == 0)
        lowBounds = DefaultLowerBounds;
      else if (lowBounds.Length != 4)
        throw new ToolkitException("lower bounds must have length 4");
      else
      {
        for (int i = 0; i < 4; ++i)
        {
          if (lowBounds[i] >= DefaultLowerBounds[i]) continue;
          lowBounds[i] = DefaultLowerBounds[i];
        }
      }

      // Sanity check for the upper bounds
      if (uppBounds.Length == 0)
        uppBounds = DefaultUpperBounds;
      else if (uppBounds.Length != 4)
        throw new ToolkitException("upper bounds must have length 4");
      else
      {
        for (int i = 0; i < 4; ++i)
        {
          if (uppBounds[i] <= DefaultUpperBounds[i]) continue;
          uppBounds[i] = DefaultUpperBounds[i];
        }
      }

      // Find the at the money volatility
      var atmVol = moneyness.Select((m, i) => new { D = Math.Abs(m - 1), V = volatilities[i] })
        .OrderBy(d => d.D).Select(d => d.V).FirstOrDefault();
      if (atmVol.IsSameAs(0.0)) atmVol = volatilities[volatilities.Length / 2];

      // The starting values
      var x0 = new[] { atmVol, 1.0, 0.0, 0.0 };

      if ((sabrModelFlags & SabrModelFlags.ImprovedSabrInitialValues) == 0)
      {
        // Check the special cases
        switch (moneyness.Length)
        {
        case 1:
          // no need to calibrate.
          return x0;
        case 2:
          // don't calibrate nu and rho
          uppBounds[2] = lowBounds[2] = uppBounds[3] = lowBounds[3] = 0;
          break;
        case 3:
          // don't calibrate rho
          uppBounds[3] = lowBounds[3] = 0;
          break;
        }
      }
      else
      {
        // check free variables
        var freedom = 0;
        var free = new bool[4];
        for (int i = 0; i < 4; ++i)
        {
          // Check the case with the same lower and upper bounds.
          if (lowBounds[i] + 1E-15 >= uppBounds[i])
          {
            x0[i] = 0.5 * (lowBounds[i] + uppBounds[i]);
            continue;
          }

          // Make sure the initial value is within the boundary
          if (x0[i] > uppBounds[i] || x0[i] < lowBounds[i])
          {
            x0[i] = 0.5 * (lowBounds[i] + uppBounds[i]);
          }

          free[i] = true;
          ++freedom;
        }
        if (freedom == 0) return x0;


        // Check the freedom
        while (moneyness.Length < freedom)
        {
          PickOneFreeVariableAndFixIt(x0, free, upperBounds, lowerBounds);
          --freedom;
        }
      }

      var opt = new NLS(x0.Length);
      opt.setToleranceF(1e-32);
      opt.setToleranceGrad(1e-32);
      opt.setToleranceX(5e-17);
      opt.setInitialPoint(x0);
      opt.setMaxEvaluations(10000);
      opt.setMaxIterations(10000);

      var sabrFn = new SabrFn(time, moneyness, volatilities,
        lowBounds.Data, uppBounds.Data);

      while (true)
      {
        opt.setLowerBounds(lowBounds.Data);
        opt.setUpperBounds(uppBounds.Data);
        var optimizerFn = DelegateOptimizerFn.Create(
          4, volatilities.Length, sabrFn.Evaluate, true);
        try
        {
          opt.Minimize(optimizerFn);
          return opt.CurrentSolution.ToArray();
        }
        catch (ApplicationException e)
        {
          if (!e.Message.StartsWith("qn::math::nls::"))
            throw; // not from the solver, don't handle it.

          var solution = opt.CurrentSolution;
#if DEBUG
          var s_view = solution.ToArray();
#endif
          // If not converge, let's restrict the parameters
          // in some resonable region and try again.
          if (solution[0] < 0.001 || solution[3] < -0.999)
          {
            lowBounds[0] = 0.001;
            lowBounds[3] = -0.999;
            continue;
          }
          if (solution[0] > 10 || solution[2] > 10)
          {
            uppBounds[0] = 10;
            uppBounds[2] = 10;
            continue;
          }
          if (solution[1] < 0)
          {
            lowBounds[1] = 0;
            continue;
          }
          if (solution[1] > 1)
          {
            uppBounds[1] = 1;
            continue;
          }
          // If all atempts fail, return the best fit we know.
          logger.Debug("Minimization not converged, returns the best fit.");
          return sabrFn.BestX;
        }
      }
    }

    private static void PickOneFreeVariableAndFixIt(
      double[] x0, bool[] free,
      UniqueArray<double> upperBounds,
      UniqueArray<double> lowerBonds)
    {
      // Candidates are rho, nu, beta, zeta, in that order.
      for (int i = 3; i >= 0; --i)
      {
        if (!free[i]) continue;

        upperBounds[i] = lowerBonds[i] = x0[i];
        free[i] = false;
        return;
      }
    }

    private static readonly double[]
      DefaultLowerBounds = new[] { 0.0, Double.MinValue, 0.0, -1.0 },
      DefaultUpperBounds = new[] { Double.MaxValue, Double.MaxValue, Double.MaxValue, 0.999 };

    #region Nested type: UniqueArray

    /// <summary>
    ///  Wraps an array which clones itself on the first attempt to write on it,
    ///  so the the original copy is never modified.
    /// </summary>
    /// <remarks>
    ///  This is a simple wrapper for an array which should be treated as read-only but which
    ///  occasionally may need to be modified (after cloning).  It makes sure the original
    ///  array is never modified and a clone is made only when it is necessary.
    /// </remarks>
    /// <typeparam name="T">The type of the array element</typeparam>
    private struct UniqueArray<T>
    {
      private readonly T[] _original;
      private T[] _a;

      private UniqueArray(T[] original)
      {
        _original = _a = original;
      }

      /// <summary>
      /// Gets or sets the value at the specified index.
      /// </summary>
      /// <param name="index">The index.</param>
      /// <returns>`0.</returns>
      public T this[int index]
      {
        get { return _a[index]; }
        set
        {
          if (ReferenceEquals(_a, _original))
            _a = (T[]) _original.Clone();
          _a[index] = value;
        }
      }

      /// <summary>
      /// Gets the length.
      /// </summary>
      /// <value>The length.</value>
      public int Length
      {
        get { return _a == null ? 0 : _a.Length; }
      }

      /// <summary>
      /// Gets the underlying array data.
      /// </summary>
      /// <value>The data.</value>
      public T[] Data
      {
        get { return _a; }
      }

      /// <summary>
      /// Performs an implicit conversion from T[] to UniqueArray&lt;T&gt;
      /// </summary>
      /// <param name="a">A</param>
      /// <returns>The result of the conversion</returns>
      public static implicit operator UniqueArray<T>(T[] a)
      {
        return new UniqueArray<T>(a);
      }
    }

    #endregion

    #region Nested type: SabrFn

    private class SabrFn
    {
      private double _maxError, _a, _b, _v, _r;
      private readonly bool[] _fixed;
      private readonly double _time;
      private readonly double[] _moneyness, _volatilities;
#if DEBUG
      private double[] _fview, _gview;
#endif

      internal double[] BestX
      {
        get { return new[] {_a, _b, _v, _r}; }
      }

      internal SabrFn(double time, double[] moneyness, double[] volatilties,
        double[] lowerBounds, double[] upperBounds)
      {
        _maxError = Double.MaxValue;
        _time = time;
        _moneyness = moneyness;
        _volatilities = volatilties;
        const int n = 4;
        Debug.Assert(lowerBounds.Length == n);
        Debug.Assert(upperBounds.Length == n);
        _fixed = new bool[n];
        for (int i = 0; i < n; ++i)
          _fixed[i] = Math.Abs(upperBounds[i] - lowerBounds[i]) < 1E-12;
      }

      internal void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        Debug.Assert(x.Count == 4);
        Debug.Assert(f.Count == _moneyness.Length ||
          (f.Count == 0 && g.Count == 4*_moneyness.Length));
        double a = x[0], b = x[1], v = x[2], r = x[3];

        int m = f.Count;
        if (m != 0)
        {
          double maxerr = 0;
          for (int i = 0; i < m; ++i)
          {
            f[i] = CalculateVolatility(a, b, v, r, _moneyness[i], _time) - _volatilities[i];
            var e = Math.Abs(f[i]);
            if (!(e <= maxerr)) maxerr = e;
          }
          if (maxerr < _maxError)
          {
            _maxError = maxerr;
            _a = a;
            _b = b;
            _v = v;
            _r = r;
          }
#if DEBUG
          _fview = f.ToArray();
#endif
        }

        int n = (g == null ? 0 : g.Count);
        if (n != 0)
        {
          const double h = 1E-5;
          int rows = _moneyness.Length;
          for (int i = 0, j = -1; i < rows; ++i)
          {
            double ms = _moneyness[i], tm = _time, vs = _volatilities[i];
            var f0 = m != 0
              ? f[i] + vs
              : CalculateVolatility(a, b, v, r, ms, tm);
            g[++j] = _fixed[0]
              ? 0.0
              : ((CalculateVolatility(a + h, b, v, r, ms, tm) - f0)/h);
            g[++j] = _fixed[1]
              ? 0.0
              : ((CalculateVolatility(a, b - h, v, r, ms, tm) - f0)/(-h));
            g[++j] = _fixed[2]
              ? 0.0
              : ((CalculateVolatility(a, b, v + h, r, ms, tm) - f0)/h);
            if (_fixed[3])
            {
              g[++j] = 0.0;
              continue;
            }
            if (r < h/2 && r > -h/2)
            {
              g[++j] = (CalculateVolatility(a, b, v, r + h/2, ms, tm) -
                CalculateVolatility(a, b, v, r - h/2, ms, tm))/h;
              continue;
            }
            double dr = r > 0 ? (-h) : h;
            g[++j] = (CalculateVolatility(a, b, v, r + dr, ms, tm) - f0)/dr;
          }
#if DEBUG
          _gview = g.ToArray();
#endif
        }
        return;
      }
    }

    #endregion

    /// <summary>
    ///   Evaluate the expression <m>\chi(z, \rho)/z</m> and handles the case when <m>z</m> is close the zero.
    /// </summary>
    /// <param name="z">The z.</param>
    /// <param name="rho">The rho.</param>
    /// <returns><m>\chi(z, \rho)/z</m></returns>
    /// <remarks>
    ///  <para>The expression <m>\chi(z, \rho)/z</m> is evaluated
    ///  with greate care to handle the numerical instability
    ///  issues when <m>\rho</m> close to one and/or <m>z</m> is close to zero, where<math>
    ///     \chi(z, \rho) = \log\left(\frac{\sqrt{1 - 2 \rho z + z^2} + z - \rho}{1-\rho}\right)
    ///   </math></para>
    /// </remarks>
    public static double ChiRatio(double z, double rho)
    {
      if (Math.Abs(z) < 2E-4)
      {
        return 1 + (0.5*rho + (3*rho*rho - 1)*z/6)*z;
      }
      if (rho > z || Math.Abs(rho - 1) < 1E-6)
      {
        return Math.Log((1 + rho)/(Math.Sqrt(1 - (2*rho - z)*z) + rho - z))/z;
      }
      return Math.Log((Math.Sqrt(1 - (2*rho - z)*z) + z - rho)/(1 - rho))/z;
    }

    #endregion

    #region Data and properties

    private readonly double _time, _forward, _zeta, _beta, _nu, _rho;

    /// <summary>
    ///   The time to expiry.
    /// </summary>
    public double Time
    {
      get { return _time; }
    }

    /// <summary>
    ///   The at the money forward.
    /// </summary>
    public double Forward
    {
      get { return _forward; }
    }

    /// <summary>
    ///  Gets the volatility parameter alpha.
    /// </summary>
    /// <remarks></remarks>
    public double Alpha
    {
      get { return _zeta*Math.Pow(_forward, 1 - _beta); }
    }

    /// <summary>
    ///  Gets the logarithm of the volatility parameter alpha.
    /// </summary>
    public double LogAlpha
    {
      get { return Math.Log(_zeta) + (1 - _beta)*Math.Log(_forward); }
    }

    /// <summary>
    ///  Gets the local volatility parameter zeta.
    /// </summary>
    /// <remarks></remarks>
    public double Zeta
    {
      get { return _zeta; }
    }

    /// <summary>
    /// Gets the skew parameter beta.
    /// </summary>
    /// <remarks></remarks>
    public double Beta
    {
      get { return _beta; }
    }

    /// <summary>
    /// Gets the volatility of volatility parameter nu.
    /// </summary>
    /// <remarks></remarks>
    public double Nu
    {
      get { return _nu; }
    }

    /// <summary>
    /// Gets the correlation parameter rho.
    /// </summary>
    /// <remarks></remarks>
    public double Rho
    {
      get { return _rho; }
    }

    #endregion

    #region Internal Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SabrVolatilitySmile"/> class.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="beta">The beta.</param>
    /// <param name="nu">The nu.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="alphaIsLocalVolatility">if set to <c>true</c> [alpha is local volatility].</param>
    public SabrVolatilitySmile(double time, double forward,
      double alpha, double beta, double nu, double rho,
      bool alphaIsLocalVolatility)
    {
      _time = time;
      _forward = forward;
      _zeta = alphaIsLocalVolatility ? alpha : alpha*Math.Pow(forward, beta - 1);
      _beta = beta;
      _nu = nu;
      _rho = rho;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Calculates the volatility at specified strike.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    /// <remarks></remarks>
    public double EvaluateStrike(double strike)
    {
      return CalculateVolatility(_zeta, _beta, _nu, _rho, strike/_forward, _time);
    }

    /// <summary>
    /// Calculates volatility at the specified moneyness.
    /// </summary>
    /// <param name="moneyness">The moneyness.</param>
    /// <returns>The volatility.</returns>
    /// <remarks></remarks>
    public double EvaluateMoneyness(double moneyness)
    {
      return CalculateVolatility(_zeta, _beta, _nu, _rho, moneyness, _time);
    }

    /// <summary>
    /// Calculates volatility at the specified input level.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <param name="xkind">The input kind.</param>
    /// <returns>System.Double.</returns>
    public double Evaluate(double x, SmileInputKind xkind)
    {
      switch (xkind)
      {
      case SmileInputKind.Strike:
        return EvaluateStrike(x);
      case SmileInputKind.Moneyness:
        return EvaluateMoneyness(x);
      case SmileInputKind.LogMoneyness:
        return EvaluateMoneyness(Math.Exp(x));
      }
      throw new ToolkitException(String.Format(
        "Invalid SmileInputKind {0}", xkind));
    }

    #endregion
  }

  /// <summary>
  ///   Evaluate SABR smile based on the time series of parameters. 
  /// </summary>
  /// <remarks>
  /// </remarks>
  [Serializable]
  public class SabrVolatilityEvaluator
  {
    private readonly bool _alphaIsLocalVolatility;
    private readonly Curve _alpha, _beta, _nu, _rho;
    private readonly Func<Dt, double> _timeCalculator; 

    /// <summary>
    ///   Initializes a new instance of the <see cref="SabrVolatilityEvaluator" /> class.
    /// </summary>
    /// <param name="alpha">The curve of alpha parameters</param>
    /// <param name="beta">The curve of beta parameters</param>
    /// <param name="nu">The curve of nu parameters.</param>
    /// <param name="rho">The curve of rho parameters</param>
    /// <param name="time">The curve to calculate the time to expiry by date</param>
    /// <remarks>
    /// </remarks>
    public SabrVolatilityEvaluator(Curve alpha, Curve beta, Curve nu, Curve rho, Curve time)
    {
      Debug.Assert(alpha != null);
      Debug.Assert(beta != null);
      Debug.Assert(nu != null);
      Debug.Assert(rho != null);
      Debug.Assert(time != null);
      _alpha = alpha;
      _beta = beta;
      _nu = nu;
      _rho = rho;
      _timeCalculator = time.Interpolate;
      _alphaIsLocalVolatility = false;
    }

    internal SabrVolatilityEvaluator(bool alphaIsLoclaVolatility,
      Curve alpha, Curve beta, Curve nu, Curve rho,
      Func<Dt,double> timeCalculator)
    {
      Debug.Assert(alpha != null);
      Debug.Assert(beta != null);
      Debug.Assert(nu != null);
      Debug.Assert(rho != null);
      Debug.Assert(timeCalculator != null);
      _alpha = alpha;
      _beta = beta;
      _nu = nu;
      _rho = rho;
      _timeCalculator = timeCalculator;
      _alphaIsLocalVolatility = alphaIsLoclaVolatility;
    }
   
    /// <summary>
    ///   Evaluates the specified time.
    /// </summary>
    /// <param name="expiry">The time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    /// <remarks>
    /// </remarks>
    public double Evaluate(Dt expiry, double forward, double strike)
    {
      var alpha = _alpha.Interpolate(expiry);
      var beta = _beta.Interpolate(expiry);
      var nu = _nu.Interpolate(expiry);
      var rho = _rho.Interpolate(expiry);
      var time = _timeCalculator(expiry);
      return _alphaIsLocalVolatility
        ? SabrVolatilitySmile.CalculateVolatility(alpha, beta, nu, rho, strike / forward, time)
        : CalculateVolatility(alpha, beta, rho, nu, forward, strike, time);
    }

    internal double EvaluateMoneyness(Dt expiry, double moneyness)
    {
      if (!_alphaIsLocalVolatility)
        throw new ToolkitException("Must calibrate local volatility to use moneyness");
      var alpha = _alpha.Interpolate(expiry);
      var beta = _beta.Interpolate(expiry);
      var nu = _nu.Interpolate(expiry);
      var rho = _rho.Interpolate(expiry);
      var time = _timeCalculator(expiry);
      return SabrVolatilitySmile.CalculateVolatility(alpha, beta, rho, nu, moneyness, time);
    }

    /// <summary>
    ///   Calculates the volatility.
    /// </summary>
    /// <param name="alpha">The alpha.</param>
    /// <param name="beta">The beta.</param>
    /// <param name="rho">The rho.</param>
    /// <param name="nu">The nu.</param>
    /// <param name="F">The F.</param>
    /// <param name="K">The K.</param>
    /// <param name="T">The T.</param>
    /// <returns></returns>
    /// <remarks>
    /// </remarks>
    public static double CalculateVolatility(double alpha, double beta,
      double nu, double rho, double F, double K, double T)
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
      double denom = Math.Pow(rbs, b1 / 2) * (1 + b2 / 24 * logros2 + b4 / 1920 * logros4);
      double num = 1 + (b2 * alpha * alpha / (24 * Math.Pow(rbs, b1))
        + (rho * beta * alpha * nu) / (4 * Math.Pow(rbs, b1 / 2))
          + nu * nu * (2 - 3 * rho * rho) / 24) * T;
      return (alpha / denom) / SabrVolatilitySmile.ChiRatio(
        nu / alpha * Math.Pow(rbs, b1 / 2) * logros, rho) * num;
    }
  }
}



