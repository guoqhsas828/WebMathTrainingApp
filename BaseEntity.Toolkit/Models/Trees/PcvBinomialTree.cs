// 
// 
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ax = BaseEntity.Toolkit.Util.Collections.ListUtil;

namespace BaseEntity.Toolkit.Models.Trees
{
  /// <summary>
  ///  Represent a binomial tree with piecewise constant log-normal volatilities
  /// </summary>
  /// <remarks>
  ///  <para>This class implements a binomial tree approximation
  ///   to the process<math>
  ///     X(t) = \int_0^t \sigma(s)\, d W(s)
  ///   </math>
  ///   where <m>W(s)</m> is the standard Brownian motion
  ///   and <m>\sigma(s)</m> is the volatility which may vary across time.
  ///  </para>
  /// 
  ///  <para><b>Time grids and probabilities</b></para>
  /// 
  ///  <para>Given a time interval from <m>0</m> to <m>T</m>,
  ///   the binomial tree is built on a set of time grids
  ///   <m>\mathscr{P} \equiv \{t_m: m = 0, 1, \ldots, M\}</m>
  ///   such that <m>t_0 = 0</m>, <m>t_M = T</m>,
  ///   and <m>t_{m-1} \lt t_ { m }</m> for all <m> m \gt 0</m>.</para>
  /// 
  ///  <para>There is exactly one jump at each time step from
  ///   <m>t_{ m - 1}</m> to <m> t_m</m>, with the probability of an up jump
  ///   denoted by <m>p_m</m>.
  ///   We allow <m>p_m</m> to vary across time.</para>
  /// 
  ///  <para>Let <m>d</m> be the total jump size (the sum of up jump and down jump)
  ///   per step.  We choose <m>p_m</m> to match the volatility at the step <m>t_m.</m>
  ///   <math>
  ///     p_m\,(1-p_m)\,d^2 = \int_{t_{m-1}}^{t_m} \sigma^2(s)\, d s
  ///   </math></para>
  /// 
  ///  <para>There are two solutions to the above equation, as given by<math>
  ///     p_m = \frac{1}{2} \pm \frac{1}{2}\sqrt{1-\lambda_m^2}
  ///     ,\qquad\text{where}\;
  ///     \lambda^2_m \equiv \frac{4}{d^2}\int_{t_{m-1}}^{t_m} \sigma^2(s)\, d s
  ///  </math></para>
  /// 
  ///  <para>To ensure a solution exists for every steps, the jump size must
  ///   satisfies<math>
  ///     d \geq d^{\mathrm{max}} \equiv 2\sqrt{\max_{0 \lt m \leq M}
  ///     \int_{t_{m-1}}^{t_m} \sigma^2(s)\, d s}
  ///   </math></para>
  /// 
  ///  <para>To get a tree as symmetric as possible, we may pick <m>d = d^{\mathrm{max}}</m>.
  ///   For a skewed tree, we need to pick a suitable <m>d \gt d^{\mathrm{max}}</m>
  ///   and a solution <m>p_m \gt 0.5</m> or <m>p_m \lt 0.5</m> based on the
  ///   direction to skew. </para>
  /// 
  ///  <para>Let <m>P(m,k)</m> be the probability of exact <m>k</m> up jumps
  ///   in the period <m>(0, t_m]</m>.  We have the following recursions<math>
  ///    p(m, k) = p_m\,P(m-1, k-1) + (1-p_m) P(m-1,k)
  ///    \quad\text{ for } m \gt 0,\; 0 \leq k \leq m
  ///   </math><math>
  ///    p(0, 0) = 1
  ///    ,\quad
  ///    p(m, k) = 0
  ///    \quad\text{ for } k \lt 0 \;\text{or}\; k \gt M
  ///   </math>
  ///  </para>
  /// </remarks>
  public class PcvBinomialTree
  {
    private readonly int[] _stepMaps;
    private readonly double[] _variances;
    private readonly double[] _upJumpProbabilities;
    private readonly BandedList<double>[] _nodeProbabilities;

    private PcvBinomialTree(
      double jumpSize,
      int[] stepMaps, double[] variances,
      double[] upJumpProbabilities,
      BandedList<double>[] nodeProbabilities)
    {
      JumpSize = jumpSize;
      _stepMaps = stepMaps;
      _variances = variances;
      _upJumpProbabilities = upJumpProbabilities;
      _nodeProbabilities = nodeProbabilities;
    }

    public const double Cutoff = 1E-22;

    #region Properties

    /// <summary>
    ///  Get the total jump size per step 
    /// </summary>
    public double JumpSize { get; private set; }

    /// <summary>
    ///  Gets the total number of steps in the tree
    /// </summary>
    public int TotalStepCount
    {
      get { return _stepMaps[_stepMaps.Length - 1]; }
    }

    /// <summary>
    ///  Gets the array which maps each flat volatility regions
    ///  to the end step index.
    /// </summary>
    /// <value>The step maps</value>
    public IReadOnlyList<int> StepMaps
    {
      get { return _stepMaps; }
    }

    /// <summary>
    ///  Gets the variances per step by flat volatility regions
    /// </summary>
    /// <value>The variances per step</value>
    public IReadOnlyList<double> Variances
    {
      get { return _variances; }
    }

    /// <summary>
    ///  Gets the per step up jump probabilities by flat volatility regions
    /// </summary>
    /// <value>Per step up jump probabilities</value>
    public IReadOnlyList<double> UpJumpProbabilities
    {
      get { return _upJumpProbabilities; }
    }

    /// <summary>
    /// Gets the node probabilities.
    /// </summary>
    /// <value>The node probabilities.</value>
    public BandedList<double>[] NodeProbabilities
    {
      get { return _nodeProbabilities; }
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Get the probabilities by states at the specified step index.
    /// </summary>
    /// <param name="stepIndex">Step index</param>
    /// <returns>The probabilities by states</returns>
    public IReadOnlyList<double> GetProbabilities(int stepIndex)
    {
      return _nodeProbabilities[stepIndex];
    }

    /// <summary>
    ///   Build the binomial tree distribution from the specified data.
    /// </summary>
    /// <param name="steps"></param>
    /// <param name="periods"></param>
    /// <param name="volatilities"></param>
    /// <returns></returns>
    public static PcvBinomialTree Build(
      IReadOnlyList<int> steps,
      IReadOnlyList<double> periods,
      IReadOnlyList<double> volatilities)
    {
      Debug.Assert(steps != null && steps.Count > 0);
      Debug.Assert(periods != null && periods.Count >= steps.Count);
      Debug.Assert(volatilities != null && volatilities.Count >= steps.Count);

      var count = steps.Count;
      var maps = Ax.PartialSums(steps, (s, e) => s + e, 0);
      var variancePerStep = Ax.NewArray(count,
        i => Square(volatilities[i])*periods[i]/steps[i]);
      double maxVar = variancePerStep.Max(), d = 2*Math.Sqrt(maxVar);

      var jmpProbs = Ax.NewArray(count,
        i => 0.5*(1 - Math.Sqrt(1 - variancePerStep[i]/maxVar)));

      // Generate probabilities on all tree nodes
      int totalSteps = maps[count - 1];
      var ptree = new BandedList<double>[totalSteps + 1];
      var workspace = new double[totalSteps + 1];
      var prevProbs = ptree[0] = new BandedList<double>(1, 0, new[] {1.0});
      for (int t = 0, stepIndex = 1; t < count; ++t)
      {
        double p = jmpProbs[t], q = 1 - p;
        var endStepIndex = maps[t];
        for (; stepIndex <= endStepIndex; ++stepIndex)
        {
          var probs = workspace;
          var baseProbs = prevProbs.Data;
          int n = baseProbs.Count, start = 0;
          var beginP = probs[0] = baseProbs[0]*q;
          if (beginP < Cutoff) ++start;
          for (int i = 1; i < n; ++i)
            probs[i] = baseProbs[i] + p*(baseProbs[i - 1] - baseProbs[i]);
          var endP = probs[n] = baseProbs[n - 1]*p;
          if (endP < Cutoff) --n;

          var data = new double[n + 1 - start];
          Array.Copy(workspace, start, data, 0, data.Length);
          prevProbs = ptree[stepIndex] = new BandedList<double>(
            stepIndex + 1, start + prevProbs.BeginIndex, data);
        }
      }

      return new PcvBinomialTree(d, maps, variancePerStep, jmpProbs, ptree);
    }

    /// <summary>
    /// Calculates the expectation.
    /// </summary>
    /// <param name="stepIndex">Index of the step.</param>
    /// <param name="valueFn">The value function.</param>
    /// <returns>System.Double.</returns>
    public double CalculateExpectation(
      int stepIndex, Func<int, double> valueFn)
    {
      var probs = GetProbabilities(stepIndex);
      double mean = 0, sump = 0;
      for (int i = 0; i <= stepIndex; ++i)
      {
        var p = probs[i];
        if (p < 1E-24) continue;
        mean += p*valueFn(i);
        sump += p;
      }
      return mean/sump;
    }


    /// <summary>
    /// Delegate StepExpectationFn
    /// </summary>
    /// <param name="stepIndex">The total number of steps to this time point</param>
    /// <param name="stateIndex">The total number of up jumps at this time point</param>
    /// <param name="upJumpProbability">Up jump probability at this time point</param>
    /// <param name="valueIfUpJumped">The forward value if an up jump happens at this time point</param>
    /// <param name="valueIfDownJumped">The forward value if a down jump happens at this time point</param>
    /// <returns>System.Double.</returns>
    public delegate T StepExpectationFn<T>(
      int stepIndex, int stateIndex, double upJumpProbability,
      T valueIfUpJumped, T valueIfDownJumped);

    /// <summary>
    /// Performs the backward induction.
    /// </summary>
    /// <param name="terminalStepIndex">Index of the terminal step.</param>
    /// <param name="terminalValues">The terminal values.</param>
    /// <param name="stepExpectationFn">The step expectation function.</param>
    /// <param name="recordStepIndex">Index of the record step.</param>
    /// <returns>System.Double[].</returns>
    /// <exception cref="InvalidOperationException">Should never be here</exception>
    public T[] PerformBackwardInduction<T>(
      int terminalStepIndex,
      IReadOnlyList<T> terminalValues,
      StepExpectationFn<T> stepExpectationFn,
      int recordStepIndex)
    {
      Debug.Assert(recordStepIndex >= 0);
      Debug.Assert(recordStepIndex < terminalStepIndex);

      var jmpProbs = _upJumpProbabilities;
      var maps = _stepMaps;

      var workspace1 = new T[terminalStepIndex + 1];
      var workspace2 = new T[terminalStepIndex + 1];
      var fwds = terminalValues;

      // Find the terminal index in the map
      var mapIndex = GetMapIndexFromStepIndex(terminalStepIndex, maps);

      int startIndex = terminalStepIndex;
      for (int t = mapIndex; t >= 0; --t)
      {
        double p = jmpProbs[t], q = 1 - p;
        int stopIndex = t > 0 ? maps[t - 1] : 0;
        for (int stepIndex = startIndex; --stepIndex >= stopIndex;)
        {
          var values = fwds == workspace1 ? workspace2 : workspace1;
          for (int i = 0; i <= stepIndex; ++i)
            values[i] = stepExpectationFn(stepIndex, i, p, fwds[i + 1], fwds[i]);
          if (stepIndex == recordStepIndex)
          {
            return Ax.NewArray(stepIndex + 1, i => values[i]);
          }
          fwds = values;
        }
        startIndex = stopIndex;
      }
      throw new InvalidOperationException("Should never be here");
    }

    /// <summary>
    /// Gets the map index from the end step index
    /// </summary>
    /// <param name="endStepIndex">End index of the step</param>
    /// <returns>System.Int32</returns>
    public int GetMapIndex(int endStepIndex)
    {
      return GetMapIndexFromStepIndex(endStepIndex, StepMaps);
    }

    #region Accumulator

    /// <summary>
    ///  Calculates the values <m>x_m(k)</m>, <m>k = 0, \ldots, m</m>, 
    ///  from the last step values, <m>x_{m-1}(k)</m>'s, by taking
    ///  the conditional means in each state.
    /// </summary>
    /// <param name="stepIndex">The step index <m>m</m></param>
    /// <param name="upJumpProbability">The up jump probability at step <m>m</m></param>
    /// <param name="lastStepValues">The function to get values at the last step,
    ///   <m>f(k) = x_{m-1}(k)</m></param>
    /// <param name="values">The output values</param>
    /// <remarks>
    ///  <para>This function calculates the conditional means<math>
    ///    x_m(k) = q_{m,k}\,x_{m-1}(k-1) + (1-q_{m,k})\,x_{m-1}(k)
    ///  </math>
    ///  where <m>m</m> is the step index, <m>k</m> is the state index,
    ///  <m>q_{m,k}</m> is the conditional probability that,
    ///  given the system is in <m>(m,k)</m>, it reaches here by an up
    ///  jump at step <m>m-1</m>.</para>
    /// 
    ///  <para>Let <m>P_m(k)</m> be the unconditional probability that
    ///  the system reaches the state <m>k</m> at step <m>m</m>.
    ///  Let <m>p_m</m> be the up jump probability at step <m>m</m>.
    ///  Then <m>q_{m,k}</m> is given by
    ///  <math>
    ///   q_{m,k} = \frac{p_m\,P_{m-1}(k-1)}{
    ///     p_m\,P_{m-1}(k-1) + (1-p_m)\,P_{m-1}(k)}
    ///  </math>
    ///  </para>
    /// </remarks>
    public void EvolveOneStep(
      int stepIndex,
      double upJumpProbability,
      Func<int, double> lastStepValues,
      IList<double> values)
    {
      Debug.Assert(stepIndex > 0);
      Debug.Assert(upJumpProbability > 0);
      Debug.Assert(lastStepValues != null);
      Debug.Assert(stepIndex <= TotalStepCount);
      Debug.Assert(values != null && values.Count > stepIndex);


      var prevProbs = GetProbabilities(stepIndex - 1);
      var u = values[0] = lastStepValues(0);
      double p = upJumpProbability, prob = prevProbs[0];
      for (int i = 1; i < stepIndex; ++i)
      {
        var u0 = u;
        u = lastStepValues(i);
        var prob0 = prob;
        prob = prevProbs[i];
        if (prob0 > Cutoff)
        {
          var q = p*prob0/(p*prob0 + (1 - p)*prob);
          values[i] = q*(u0 - u) + u;
          continue;
        }
        values[i] = u;
      }
      values[stepIndex] = u;
    }

    public double GetLowerOriginProbability(
      int stepIndex, int stateIndex, double upJumpProbability)
    {
      Debug.Assert(stepIndex > 0);

      if (stateIndex <= 0)
        return 0.0; // always come from a down jump
      if (stateIndex >= stepIndex)
        return 1.0; // always come from an up jump
      var probs = GetProbabilities(stepIndex - 1);
      double p0 = upJumpProbability*probs[stateIndex - 1],
        p1 = (1 - upJumpProbability)*probs[stateIndex];
      if (p0 < Cutoff) return p0 < p1 ? 0.0 : 1.0;
      if (p1 < Cutoff) return 1.0;
      return p0/(p0 + p1);
    }

    public double StepAccumulate(
      int stepIndex, int stateIndex, double upJumpProbability,
      double preValueIfUpJumped, double preValueIfDownJumped)
    {
      double d = preValueIfUpJumped, u = preValueIfDownJumped,
        q = GetLowerOriginProbability(stepIndex, stateIndex, upJumpProbability);
      return q >= 1 ? d : (q <= 0 ? u : (q < 0.5
        ? (q*(d - u) + u) : ((1 - q)*(u - d) + d)));
    }

    #endregion

    public static double StepExpectation(
      int stepIndex, int stateIndex, double upJumpProbability,
      double valueIfUpJumped, double valueIfDownJumped)
    {
      return valueIfDownJumped +
        upJumpProbability*(valueIfUpJumped - valueIfDownJumped);
    }

    #endregion

    #region Utility methods

    /// <summary>
    /// Gets the up jump probability from step <c>(m-1)</c> to <c>m</c>,
    /// where <c>m</c> is the end step index.
    /// </summary>
    /// <param name="endStepIndex">End index of the step.</param>
    /// <returns>System.Double.</returns>
    public double GetJumpProbability(int endStepIndex)
    {
      var m = GetMapIndexFromStepIndex(endStepIndex, StepMaps);
      return UpJumpProbabilities[m];
    }

    private static int GetMapIndexFromStepIndex(
      int endStepIndex, IReadOnlyList<int> maps)
    {
      // Find position enclosing the step index
      var mapIndex = -1;
      for (int i = maps.Count; --i >= 0;)
      {
        if (maps[i] < endStepIndex) break;
        mapIndex = i;
      }
      Debug.Assert(mapIndex >= 0);
      return mapIndex;
    }

    #endregion

    #region Simple Utilities

    private static double Square(double x)
    {
      return x * x;
    }

    #endregion
  }
}
