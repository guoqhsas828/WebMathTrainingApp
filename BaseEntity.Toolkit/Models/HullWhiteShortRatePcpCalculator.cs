/*
 * 
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Numerics;
using static System.Math;
using static BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///  Hull-White short rate model with piecewise constant
  ///  mean reversion and volatility parameters.
  /// </summary>
  public class HullWhiteShortRatePcpCalculator
  {
    #region Constructor

    /// <summary>
    /// Creates the specified dates.
    /// </summary>
    /// <param name="dates">The dates.</param>
    /// <param name="sigmas">The sigmas.</param>
    /// <param name="meanReversions">The mean reversions.</param>
    /// <returns>HullWhiteShortRatePcpCalculator.</returns>
    public static HullWhiteShortRatePcpCalculator Create(
      IReadOnlyList<double> dates,
      IReadOnlyList<double> sigmas,
      IReadOnlyList<double> meanReversions)
    {
      var calc = new HullWhiteShortRatePcpCalculator();
      calc.Initialize(dates, sigmas, meanReversions);
      return calc;
    }

    // Intentionally make this private
    private HullWhiteShortRatePcpCalculator()
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of date intervals.
    /// </summary>
    /// <value>The count.</value>
    public int Count => _g?.Length ?? 0;

    #endregion

    #region Calculations

    #region Volatility only calculations

    /// <summary>
    /// Calculates <math>
    ///  g_i \equiv e^{\int_0^{t_i} a_u d u}
    ///  ,\quad i = -1, 0, \ldots, n
    ///  ,\; t_{-1} = 0
    /// </math>
    /// </summary>
    public double CalculateG(int i)
    {
      Debug.Assert(i >= -1 && i < Count);
      return i < 0 ? 1.0 : _g[i];
    }

    public double CalculateNu(int i)
    {
      Debug.Assert(i >= -1 && i < Count);
      return i < 0 ? 1.0 : _nu[i];
    }

    /// <summary>
    /// Calculates the affine volatility coefficient<math>
    ///  B_{i,j} \equiv B(t_i, t_j)
    ///   \equiv g_{t_i} \int_{t_i}^{t_j} \frac{d u}{g_u}
    ///  ,\quad i,j = -1, 0, \ldots, n
    ///  ,\;\text{ and }\; t_{-1} = 0
    /// </math>
    /// </summary>
    /// <param name="i">The index of the begin date</param>
    /// <param name="j">The index of the end date</param>
    public double CalculateB(int i, int j)
    {
      Debug.Assert(i >= -1 && j < Count);
      return i >= j ? 0.0 : _B[i + 1, j];
    }

    /// <summary>
    /// Calculates the unconditional variance of the short rate,
    /// <math>V_r(0, t_i)
    ///  \equiv \mathrm{var}\left[r(t_i)\right]
    ///  = \frac{1}{g_{t_i}}\int_{0}^{t_i} g_u^2 \sigma_u^2 \,d u</math>
    /// </summary>
    /// <param name="i">The index of the end date</param>
    /// <returns>System.Double.</returns>
    public double CalculateShortRateVariance(int i)
    {
      Debug.Assert(i >= -1 && i < Count);
      return (i < 0 ? 0.0 : _vr[i]);
    }

    /// <summary>
    /// Calculates the conditional variance of the short rate,
    /// <math>V_r(t_i,t_j)
    ///  \equiv \mathrm{var}\left[r(t_j)\,\mid\,\mathcal{F}_{t_i}\right]
    ///   = \frac{1}{g_{t_j}}\int_{t_i}^{t_j} g_u^2 \sigma_u^2 \,d u</math>
    /// </summary>
    /// <param name="i">The index of the begin date</param>
    /// <param name="j">The index of the end date</param>
    /// <returns>System.Double.</returns>
    public double CalculateShortRateVariance(int i, int j)
    {
      Debug.Assert(i >= -1 && j < Count);
      if (i >= j) return 0.0;

      var g = _g;
      var nu = _nu;
      var sum = 0.0;
      for (int k = i + 1; k <= j; ++k)
      {
        var v = g[k]*nu[k];
        sum += v*v;
      }
      var gj = g[j];
      return sum/(gj*gj);
    }

    /// <summary>
    /// Calculates the volatility of the forward rate
    ///   beginning with <m>t_i</m> and ending with <m>t_j</m>.
    /// <math>
    ///   \sigma_p(0, t_i, t_j) \equiv B(t_i, t_j) \sqrt{V_r(0, t_i)}
    /// </math>
    /// </summary>
    /// <param name="i">The index of the forward rate period begin date</param>
    /// <param name="j">The index of the forward rate period end date</param>
    /// <returns>System.Double.</returns>
    public double CalculateCapletVolatility(int i, int j)
    {
      var vr = CalculateShortRateVariance(-1, i);
      return Math.Sqrt(vr)*CalculateB(i, j);
    }

    #endregion

    #region Drift and option calculations

    /// <summary>
    /// Calculates the affine drift coefficient<math>
    ///  A_{i,j} \equiv A(t_i, t_j) = \log\frac{P(0,t_j)}{P(0,t_i)}
    ///   + B(t_i,t_j)\,f(0, t_i)
    ///   - \frac{1}{2}B(t_i, t_j)\,V_r(0, t_i)
    /// </math>where <m>i,j = -1, 0, \ldots, n</m>
    ///  and <m> t_{-1} = 0</m>.
    /// </summary>
    /// <param name="i">The index of the begin date</param>
    /// <param name="j">The index of the end date</param>
    /// <param name="zeroPrices">Current zero-bond prices</param>
    /// <param name="time">The time grid</param>
    /// <returns></returns>
    public double CalculateA(int i, int j,
      IReadOnlyList<double> zeroPrices,
      IReadOnlyList<double> time)
    {
      Debug.Assert(zeroPrices != null && zeroPrices.Count == Count);
      Debug.Assert(time != null && zeroPrices.Count == Count);
      Debug.Assert(i >= -1 && j < Count);
      if (i >= j) return 0;

      var p0 = i < 0 ? 1.0 : zeroPrices[i];
      var f0 = Log(p0/zeroPrices[i + 1])/(time[i + 1] - (i < 0 ? 0 : time[i]));
      var Bij = CalculateB(i, j);
      var Vr = CalculateShortRateVariance(i);
      return Log(zeroPrices[j]/p0) + Bij*(f0 - 0.5*Bij*Vr);
    }

    /// <summary>
    ///  Calculate the zero-bond put option value with strike <m>X</m> and 
    ///  price fixing at <m>T_i</m> on the bond and paying at <m>T_j</m>.
    ///  <math>\begin{align}
    ///   \mathrm{ZBP}(T_i, T_j, X) 
    ///     &amp;= \mathrm{E}\left[e^{\int_0^{T_i}r_s d s}(X - P(T_i, T_j))^+\right]
    ///     \\ &amp;= X\,P(0, X_i)\,\Phi(d_{+}) - P(0, T_j)\,\Phi(d_{-})
    ///     \\ d_{\pm} &amp;= \frac{1}{\sigma_p}\log\left(
    ///       \frac{P(0,T_i)X}{P(0,T_j)}\right) \pm \frac{1}{2}\sigma_p
    ///     \\ \sigma_p &amp;= \sqrt{V_p(0, T_i, T_j)}
    ///  \end{align}</math>
    /// </summary>
    /// <param name="i">The index of fixing time</param>
    /// <param name="j">The index of paying time</param>
    /// <param name="strike">The strike level</param>
    /// <param name="zeroPrices">Current zero-bond prices</param>
    /// <param name="time">Time grid</param>
    /// <returns>The option premium</returns>
    public double EvaluateZeroBondPut(int i, int j,
      double strike,
      IReadOnlyList<double> zeroPrices,
      IReadOnlyList<double> time)
    {
      Debug.Assert(zeroPrices != null && zeroPrices.Count == Count);
      Debug.Assert(time != null && zeroPrices.Count == Count);
      Debug.Assert(i >= -1 && i < j && j < Count);
      var sigma = CalculateCapletVolatility(i, j);
      if (sigma.AlmostEquals(0.0))
        return strike*zeroPrices[i] - zeroPrices[j];
      return Black(strike*(i < 0 ? 1.0 : zeroPrices[i]), zeroPrices[j], sigma);
    }

    /// <summary>
    ///  Evaluate the value of a caplet with strike <m>K</m>,
    ///  expiring at <m>T_i</m> and paying at <m>T_j</m>.
    ///  <math>\begin{align}
    ///   \mathrm{Caplet}(T_i, T_j, K) 
    ///     &amp;= \mathrm{E}\left[
    ///       e^{\int_0^{T_j}r_s d s}\delta(T_i,T_j)(F(T_i, T_j)- K)^+
    ///     \right]
    ///     \\ &amp;= \left(1 + K\delta(T_i,T_j)\right)\,
    ///       \mathrm{ZBP}\left(T_i, T_j, \frac{1}{1+K\delta(T_i,T_j)}\right)
    ///     \\ F(T_i,T_j) &amp;= \frac{1}{\delta(T_i,T_j)}
    ///       \left(\frac{1}{P(T_i, T_j)} - 1\right)
    ///  \end{align}</math>
    /// </summary>
    /// <param name="i">The index of fixing time</param>
    /// <param name="j">The index of paying time</param>
    /// <param name="coupon">The strike level</param>
    /// <param name="fraction">The day count fractions by end dates</param>
    /// <param name="zeroPrices">Current zero-bond prices</param>
    /// <param name="time">The grid of period end dates</param>
    /// <returns>The option premium</returns>
    public double EvaluateCaplet(int i, int j,
      double coupon, double fraction,
      IReadOnlyList<double> zeroPrices,
      IReadOnlyList<double> time)
    {
      Debug.Assert(zeroPrices != null && zeroPrices.Count == Count);
      Debug.Assert(time != null && zeroPrices.Count == Count);
      var a = 1 + coupon*fraction;
      return a*EvaluateZeroBondPut(i, j, 1/a, zeroPrices, time);
    }

    /// <summary>
    ///  Evaluate the value of a payer swaption with strike <m>K</m>,
    ///  expiry <m>T_i</m> and maturity <m>T_j</m>,
    ///  by Jamshidian's trick.
    ///  <math>\begin{align}
    ///   \mathrm{Swaption}&amp;\mathrm{Payer}(K, T_i, T_j)
    ///     = \sum_{k=i+1}^j c_k\,\mathrm{ZBP}(T_i, T_k, X_k)
    ///    \\ c_k &amp;= K\,\delta(T_{k-1},T_k)\quad k = i+1,\ldots, j-1
    ///    \\ c_j &amp;= 1 + K\,\delta(T_{j-1},T_j)
    ///    \\ X_k &amp;= \exp\!\left(A(T_i,T_k) - B(T_i, T_k)\,r^*\right)
    ///  \end{align}</math>
    ///  where <m>r^*</m> solves<math>
    ///     \sum_{k=i+1}^j c_k\,\exp\!\left(A(T_i,T_k) - B(T_i,T_k)\,r^*\right) = 1
    ///  </math>
    /// </summary>
    /// <param name="i">The index of the forward swap starting date</param>
    /// <param name="j">The index of the forward swap maturity date</param>
    /// <param name="strike">The fixed coupon level</param>
    /// <param name="zeroPrices">The array of zero prices by dates</param>
    /// <param name="time">The array of dates</param>
    /// <param name="fractions">The array of fraction by end dates</param>
    /// <param name="spreads">The integral of the basis spread</param>
    /// <returns>swaption value</returns>
    public double EvaluateSwaptionPayer(int i, int j,
      double strike,
      IReadOnlyList<double> zeroPrices,
      IReadOnlyList<double> time,
      IReadOnlyList<double> fractions,
      IReadOnlyList<double> spreads =null)
    {
      Debug.Assert(zeroPrices != null && zeroPrices.Count == Count);
      Debug.Assert(time != null && zeroPrices.Count == Count);
      Debug.Assert(fractions != null && fractions.Count == Count);
      Debug.Assert(i >= -1 && i < j && j < Count);

      int n = time.Count;
      if (spreads == null)
        spreads = Enumerable.Repeat(1.0, n).ToArray();
      var arrayA = new double[n];
      var arrayB = new double[n];
      var arrayC = new double[n];
      {
        for (int k = i + 1; k < j; ++k)
        {
          arrayB[k] = CalculateB(i, k);
          arrayA[k] = CalculateA(i, k, zeroPrices, time);
          arrayC[k] = strike * fractions[k] + 1 - spreads[k + 1];
        }
        arrayB[j] = CalculateB(i, j);
        arrayA[j] = CalculateA(i, j, zeroPrices, time);
        arrayC[j] = strike * fractions[j] + 1;
      }

      Func<double, double> f = r =>
      {
        var sum = 0.0;
        for (int k = i + 1; k <= j; ++k)
        {
          sum += arrayC[k]*Exp(arrayA[k] - arrayB[k]*r);
        }
        return sum;
      };

      var initialTrial = Log((i < 0 ? 1.0 : zeroPrices[i])/zeroPrices[i + 1])
        /(time[i + 1] - (i < 0 ? 0.0 : time[i]));

      var solver = new Brent2();
      solver.setToleranceF(1E-15);
      solver.setToleranceX(1E-15);
      var rstar = solver.solve(f, null, spreads[i + 1], initialTrial);

      var result = 0.0;
      for (int k = i + 1; k <= j; ++k)
      {
        var x = Exp(arrayA[k] - arrayB[k]*rstar);
        result += arrayC[k]*EvaluateZeroBondPut(i, k, x, zeroPrices, time);
      }
      return result;
    }

    #endregion

    #endregion

    #region public implementations

    /// <summary>
    ///  Initialize the intermediate volatility data  
    /// </summary>
    /// <param name="dates">Dates</param>
    /// <param name="sigmas">Instantaneous volatilities</param>
    /// <param name="meanReversions">Mean reversion parameters</param>
    /// <remarks>
    /// </remarks>
    public void Initialize(
      IReadOnlyList<double> dates,
      IReadOnlyList<double> sigmas,
      IReadOnlyList<double> meanReversions)
    {
      Debug.Assert(dates != null && dates.Count > 0);
      Debug.Assert(sigmas != null && sigmas.Count == dates.Count);
      Debug.Assert(meanReversions != null && meanReversions.Count == dates.Count);

      var n = dates.Count;
      InitializeArrays(n);

      var g = _g;
      var nu = _nu;
      var B = _B;

      Debug.Assert(nu != null && nu.Length == n);
      Debug.Assert(g != null && g.Length == n);
      Debug.Assert(B != null && B.GetLength(0) == n && B.GetLength(0) == n);

      for (int i = n; --i >= 0;)
      {
        double di = dates[i] - (i == 0 ? 0.0 : dates[i - 1]),
          dai = -di*meanReversions[i],
          ei = Exp(dai),
          bi = Expd1(dai)*di,
          si = sigmas[i],
          s2i = si*si,
          v2i = Expd1(2*dai)*di*s2i;

        nu[i] = Sqrt(v2i);

        // for element(i,i)
        g[i] = 1/ei;

        // for element(i,j) with j > i
        for (int j = i; ++j < n;)
        {
          g[j] /= ei;

          // calculate B(i-1, j)
          B[i, j] = bi + ei*B[i + 1, j];
        }

        // calculate B(i-1, i)
        B[i, i] = bi;
      }

      // calculate <m>V_r(0, t_i)</m>, for <m>i = 0, \dots, n-1</m>
      var vr = _vr;
      Debug.Assert(vr != null && vr.Length == n);
      var sum = 0.0;
      for (int i = 0; i < n; ++i)
      {
        var gi = g[i];
        var vi = gi*nu[i];
        sum += vi*vi;
        vr[i] = sum/(gi*gi);
      }

    }

    private void InitializeArrays(int n)
    {
      if (_g != null && _g.Length == n)
        return;
      _g = new double[n];
      _nu = new double[n];
      _vr = new double[n];
      _B = new double[n, n];
    }

    #endregion

    #region Data

    private double[] _g, _nu, _vr;
    private double[,] _B;

    #endregion
  }

}
