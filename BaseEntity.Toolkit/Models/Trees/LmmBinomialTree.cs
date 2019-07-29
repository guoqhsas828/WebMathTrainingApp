// 
// 
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Distribution = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;
using Ax = BaseEntity.Toolkit.Util.Collections.ListUtil;

namespace BaseEntity.Toolkit.Models.Trees
{
  public class LmmBinomialTree
  {
    #region Data members

    /// <summary>
    ///  A list of dates <m>T_i</m>, <m>i = 0, 1, \ldots, N</m>,
    ///  where <N>N</N> is the number of rates,
    ///  and for <m>i \gt 0</m>, <m>(T_{i-1}, T_{i})</m> are
    ///  the reset date and maturity date of the <m>i</m>th forward rate.
    /// </summary>
    private readonly IReadOnlyList<double> _tenors;

    /// <summary>
    ///  A list of the zero bond prices by maturity dates, in terminal measure
    /// </summary>
    private readonly IReadOnlyList<double> _initialZeroPrices;

    /// <summary>
    ///  The discount factor from 0 to the terminal date, in spot measure.
    /// </summary>
    private readonly double _terminalDiscountFactor;

    /// <summary>
    ///  Rate specific volatilities
    /// </summary>
    private readonly IReadOnlyList<double> _betas;

    /// <summary>
    ///  The binomial tree representation of the Brownian motion.
    /// </summary>
    private readonly PcvBinomialTree _tree;

    /// <summary>
    ///  The distribution of the underlying process
    /// </summary>
    private readonly Distribution _kind;

    #endregion

    #region Properties and simple queries

    /// <summary>
    /// Gets the betas, the rate specific volatility multipliers .
    /// </summary>
    /// <value>The betas.</value>
    public IReadOnlyList<double> Betas
    {
      get { return _betas; }
    }

    public Distribution Distribution
    {
      get { return _kind; }
    }

    public double GetResetTime(int rateIndex)
    {
      Debug.Assert(rateIndex >= 0);
      Debug.Assert(rateIndex < _tenors.Count - 1);
      return _tenors[rateIndex];
    }

    public double GetResetStepCount(int rateIndex)
    {
      Debug.Assert(rateIndex >= 0);
      Debug.Assert(rateIndex < _tenors.Count - 1);
      return _tree.StepMaps[rateIndex];
    }

    public int GetCurrentRateIndexAtTime(double time)
    {
      Debug.Assert(time >= 0);
      var tenors = _tenors;
      int n = tenors.Count;
      for (int i = 0; i < n; ++i)
        if (time < tenors[i]) return i - 1;
      return n - 1;
    }

    public int GetStepIndexAtTime(double time)
    {
      Debug.Assert(time >= 0);
      var maps = _tree.StepMaps;
      var tenors = _tenors;
      int n = tenors.Count - 1;
      Debug.Assert(n == maps.Count);
      for (int i = 0; i < n; ++i)
      {
        if (time >= tenors[i]) continue;
        if (i == 0)
        {
          return (int) Math.Floor(time*maps[0]/tenors[0] + 1E-6);
        }
        var s0 = maps[i - 1];
        var ns = maps[i] - s0;
        var t0 = tenors[i - 1];
        return s0 + (int) Math.Floor((time - t0)*ns/(tenors[i] - t0) + 1E-6);
      }
      return maps[n - 1];
    }

    #endregion

    #region Constructors

    private LmmBinomialTree(
      PcvBinomialTree tree,
      IReadOnlyList<double> tenors,
      IReadOnlyList<double> initialZeroPrices,
      double terminalDiscountFactor,
      IReadOnlyList<double> betas,
      Distribution kind)
    {
      _tree = tree;
      _tenors = tenors;
      _initialZeroPrices = initialZeroPrices;
      _terminalDiscountFactor = terminalDiscountFactor;
      _betas = betas;
      _kind = kind;

#if NotYet
      _annuities = new RateAnnuity[tree.TotalStepCount][];
      _driftsUs = new double[tree.TotalStepCount][];
      _currentRateIndex = -1;
      _currentStepIndex = -1;
#endif
    }

    public static LmmBinomialTree Create(
      IReadOnlyList<double> tenorDates,
      IReadOnlyList<double> discountFactors,
      IReadOnlyList<int> stepCountPerResetIntervals,
      Distribution kind,
      IReadOnlyList<double> betas,
      Func<double, double> commonVolatilityFn = null)
    {
      // Require at least two dates to define a forward rate
      Debug.Assert(tenorDates != null && tenorDates.Count > 1);
      Debug.Assert(discountFactors != null &&
        discountFactors.Count == tenorDates.Count);
      Debug.Assert(stepCountPerResetIntervals != null &&
        stepCountPerResetIntervals.Count == tenorDates.Count - 1);
      Debug.Assert(betas != null && betas.Count == tenorDates.Count - 1);

      var rateCount = tenorDates.Count - 1;
      var terminalDiscount = discountFactors[rateCount];
      var intervals = new double[rateCount];
      var sigmas = new double[rateCount];
      var zeroPrices = new double[rateCount + 1];
      double t = 0, v = 0;
      for (int i = 0; i < rateCount; ++i)
      {
        double t0 = t, v0 = v;
        t = tenorDates[i];
        v = commonVolatilityFn == null ? t : Square(commonVolatilityFn(t))*t;
        var dt = intervals[i] = t - t0;
        sigmas[i] = Math.Sqrt((v - v0)/dt);
        zeroPrices[i] = discountFactors[i]/terminalDiscount;
      }
      zeroPrices[rateCount] = 1.0;

      var tree = PcvBinomialTree.Build(
        stepCountPerResetIntervals, intervals, sigmas);

      return new LmmBinomialTree(tree, tenorDates,
        zeroPrices, terminalDiscount, betas, kind);
    }

    #endregion

    #region Methods

    public IReadOnlyList<RateAnnuity> CalculateSwapRates(
      int startRateIndex,
      IReadOnlyList<double> weights)
    {
      // Do it backwards
      var maps = _tree.StepMaps;
      Debug.Assert(startRateIndex >= 0 && startRateIndex < maps.Count);

      int lastRateIndex = maps.Count - 1;

      var rates = CalculateTerminalRateAnnuities(lastRateIndex);
      var rateAnnuities = (IReadOnlyList<RateAnnuity>) Ax.MapList(
        rates, v => RateAnnuity.FromValue(v, 1.0));
      var swapRateAnnuities = (IReadOnlyList<RateAnnuity>) Ax.MapList(
        rates, v => RateAnnuity.FromValue(v, weights[lastRateIndex]));
      var sumUs = new double[lastRateIndex][];

      CalculateSwapRates(startRateIndex, lastRateIndex,
        rateAnnuities, swapRateAnnuities, sumUs, weights,
        out rateAnnuities, out swapRateAnnuities);
      return swapRateAnnuities;
    }

    public double CalculateExpectationAtExpiry(
      int rateIndex, Func<int, double> valueFn)
    {
      var tree = _tree;
      return _terminalDiscountFactor*tree.CalculateExpectation(
        tree.StepMaps[rateIndex], valueFn);
    }

    #endregion

    #region Forward rates calculation

    public IEnumerable<IReadOnlyList<RateAnnuity>> EnumerateRates()
    {
      // Do it backwards
      var maps = _tree.StepMaps;
      int currentRateIndex = maps.Count - 1;

      var rates = CalculateTerminalRateAnnuities(currentRateIndex);
      var currentRateAnnuities = (IReadOnlyList<RateAnnuity>) Ax
        .MapList(rates, v => RateAnnuity.FromValue(v, 1.0));
      yield return currentRateAnnuities;

      if (currentRateIndex <= 0) yield break;

      var sumUs = new double[currentRateIndex][];
      var tree = _tree;
      var kind = _kind;
      var betas = _betas;

      // Do it backwards
      var builder = new DriftFactorTreeBuilder(tree, kind);
      var integrator = new TreeIntegrator(tree);
      for (int t = currentRateIndex; --t >= 0;)
      {
        var stepIndex = maps[t];
        var ra = tree.PerformBackwardInduction(
          maps[t + 1], currentRateAnnuities,
          RateCalculations.StepExpectation,
          stepIndex);
        var driftUs = CalculateDriftUs(builder, integrator, t, ra);
        UpdateSumUs(t, betas[t], driftUs, sumUs);

        var values = CalculateTerminalRateAnnuities(t,
          Ax.CreateList(ra.Length, i => ra[i].Sum()), sumUs[t]);
        for (int i = 0; i <= stepIndex; ++i)
          ra[i] = RateAnnuity.FromValue(values[i], ra[i].Sum());
        yield return ra;

        currentRateAnnuities = ra;
      }
    }

    #endregion

    #region Bermudan evaluation

    public double EvaluateBermudanSwaption(
      int startRateIndex, int lastCallRateIndex,
      IReadOnlyList<int> signs,
      IReadOnlyList<double> strikes,
      IReadOnlyList<double> weights)
    {
      var tree = _tree;

      // Do it backwards
      var maps = tree.StepMaps;
      Debug.Assert(startRateIndex >= 0 && startRateIndex < maps.Count);

      int lastRateIndex = maps.Count - 1;

      var rates = CalculateTerminalRateAnnuities(lastRateIndex);
      var rateAnnuities = (IReadOnlyList<RateAnnuity>) Ax.MapList(
        rates, v => RateAnnuity.FromValue(v, 1.0));
      var swapRateAnnuities = (IReadOnlyList<RateAnnuity>) Ax.MapList(
        rates, v => RateAnnuity.FromValue(v, weights[lastRateIndex]));
      var values = GetExerciseValues(signs[lastRateIndex],
        strikes[lastRateIndex], swapRateAnnuities);

      var sumUs = new double[lastRateIndex][];
      for (int t = lastRateIndex; --t >= startRateIndex;)
      {
        var stepIndex = maps[t];
        var continueValues = t < lastCallRateIndex
          ? tree.PerformBackwardInduction(maps[t + 1], values,
            PcvBinomialTree.StepExpectation, stepIndex) : null;
#if DEBUG
        var swaps = Debugger.IsAttached
          ? CalculateSwapRates(t, weights)
          : null;
#endif
        CalculateSwapRates(t, t + 1,
          rateAnnuities, swapRateAnnuities, sumUs, weights,
          out rateAnnuities, out swapRateAnnuities);
        values = GetExerciseValues(signs[t], strikes[t],
          swapRateAnnuities, continueValues);
      }

#if DEBUG
      var coswpn = Debugger.IsAttached ? tree.CalculateExpectation(
        maps[startRateIndex], i => Math.Max(signs[startRateIndex]*(
          swapRateAnnuities[i].Value - strikes[startRateIndex]*
          swapRateAnnuities[i].Annuity),
          0.0)) : 0.0;
#endif

      return _terminalDiscountFactor*tree.CalculateExpectation(
        maps[startRateIndex], i => values[i]);
    }

    private IReadOnlyList<double> CalculateTerminalRateAnnuities(
      int rateIndex,
      IReadOnlyList<double> annuities = null,
      IReadOnlyList<double> sumUs = null)
    {
      var tree = _tree;
      var endStepIndex = tree.StepMaps[rateIndex];
      var beta = _betas[rateIndex];

      var initialValue = GetInitialValue(rateIndex);
      if (sumUs == null)
      {
        return _kind == Distribution.Normal
          ? tree.CalculateNormalTerminalValues(
            initialValue, endStepIndex, beta, -1)
          : tree.CalculateLogNormalTerminalValues(
            initialValue, endStepIndex, beta);
      }
      Debug.Assert(sumUs.Count == endStepIndex + 1);
      Debug.Assert(annuities != null && annuities.Count == endStepIndex + 1);
      return _kind == Distribution.Normal
        ? tree.CalculateNormalTerminalValues(initialValue,
          endStepIndex, beta, -1, sumUs, annuities)
        : tree.CalculateLogNormalTerminalValues(
          initialValue, endStepIndex, beta,
          i => Math.Log(annuities[i]) - beta*sumUs[i]);
    }

    private double GetInitialValue(int rateIndex)
    {
      var dfs = _initialZeroPrices;
      return dfs[rateIndex] - dfs[rateIndex + 1];
    }

    private void CalculateSwapRates(
      int startRateIndex,

      int currentRateIndex,
      IReadOnlyList<RateAnnuity> currentRateAnnuities,
      IReadOnlyList<RateAnnuity> currentSwapRateAnnuities,
      double[][] sumUs,

      IReadOnlyList<double> weights,
      out IReadOnlyList<RateAnnuity> outRateAnnuities,
      out IReadOnlyList<RateAnnuity> outSwapRateAnnuities)
    {
      var tree = _tree;
      var kind = _kind;
      var betas = _betas;

      Debug.Assert(currentRateIndex >= 0);
      Debug.Assert(startRateIndex >= 0);
      if (currentRateIndex == startRateIndex)
      {
        outRateAnnuities = currentRateAnnuities;
        outSwapRateAnnuities = currentSwapRateAnnuities;
        return;
      }

      Debug.Assert(startRateIndex < currentRateIndex);
      Debug.Assert(currentRateAnnuities != null);
      Debug.Assert(currentSwapRateAnnuities == null ||
        currentSwapRateAnnuities.Count >= currentRateAnnuities.Count);

      // Do it backwards
      var maps = tree.StepMaps;
      Debug.Assert(currentRateIndex < maps.Count);
      Debug.Assert(startRateIndex >= 0);

      var rateAnnuities = currentRateAnnuities;
      var swapRateAnnuities = currentSwapRateAnnuities;
      var builder = new DriftFactorTreeBuilder(tree, kind);
      var integrator = new TreeIntegrator(tree);

      for (int t = currentRateIndex; --t >= startRateIndex;)
      {
        var weight = weights[t];
        var stepIndex = maps[t];
        var swaps = swapRateAnnuities == null
          ? null
          : tree.PerformBackwardInduction(
            maps[t + 1], swapRateAnnuities,
            RateCalculations.StepExpectation,
            stepIndex);
        var ra = tree.PerformBackwardInduction(
          maps[t + 1], rateAnnuities,
          RateCalculations.StepExpectation,
          stepIndex);
        var driftUs = CalculateDriftUs(builder, integrator, t, ra);
        UpdateSumUs(t, betas[t], driftUs, sumUs);

        var values = CalculateTerminalRateAnnuities(t,
          Ax.CreateList(ra.Length, i => ra[i].Sum()), sumUs[t]);
        if (swaps != null)
        {
          for (int i = 0; i <= stepIndex; ++i)
          {
            ra[i] = RateAnnuity.FromValue(values[i], ra[i].Sum());
            swaps[i] = RateAnnuity.FromValue(
              swaps[i].Value + ra[i].Value,
              swaps[i].Annuity + weight*ra[i].Annuity);
          }
        }
        else
        {
          for (int i = 0; i <= stepIndex; ++i)
            ra[i] = RateAnnuity.FromValue(values[i], ra[i].Sum());
        }
        rateAnnuities = ra;
        swapRateAnnuities = swaps;
      }

      outRateAnnuities = rateAnnuities;
      outSwapRateAnnuities = swapRateAnnuities;

      return;
    }

    #endregion

    #region Static tree methods

    private static IReadOnlyList<double> GetExerciseValues(
      int sign, double strike,
      IReadOnlyList<RateAnnuity> swaps,
      IReadOnlyList<double> continueValues)
    {
      if (continueValues == null)
        return GetExerciseValues(sign, strike, swaps);
      if (sign == 0)
        return continueValues;
      return Ax.CreateList(swaps.Count, i => Math.Max(
        sign*(swaps[i].Value - strike*swaps[i].Annuity),
        continueValues[i]));
    }

    private static IReadOnlyList<double> GetExerciseValues(
      int sign, double strike,
      IReadOnlyList<RateAnnuity> swaps)
    {
      if (sign == 0)
        return Ax.CreateList(swaps.Count, i => 0.0);
      return Ax.MapList(swaps,
        s => Math.Max(sign*(s.Value - strike*s.Annuity), 0.0));
    }

    private static void UpdateSumUs(
      int rateIndex, double beta,
      double[][] driftUs, double[][] sumUs)
    {
      for (int t = 0; t <= rateIndex; ++t)
      {
        var u = driftUs[t];

        // BGM drifts must not be NaN
        Debug.Assert(u.All(v => !double.IsNaN(v)));

        var sumU = sumUs[t];
        if (sumU == null)
        {
          sumUs[t] = u;
          for (int i = 0; i < u.Length; ++i)
            u[i] *= beta;
          continue;
        }
        for (int i = 0; i < u.Length; ++i)
          sumU[i] += beta*u[i];
      }
    }


    /// <summary>
    /// Calculates <math>
    /// U_n(T_j) = \int_0^{T_j}
    /// \frac{\delta_n\,L_n(s)}{1+\delta_n\,L_n(s)}
    /// \sigma^2(s)\,d s
    /// ,\quad
    /// j = 1, \ldots, n
    /// </math>where <m>n</m> is the rate index.
    /// </summary>
    /// <param name="builder">The tree builder</param>
    /// <param name="integrator">The tree integrator</param>
    /// <param name="rateIndex">The rate index <m>n</m></param>
    /// <param name="rateAnnuities">The rate-annuity pairs <m>(L_n(T_n), A_n(T_n))</m> at time <m>T_n</m></param>
    /// <returns>The list of <m>U_n(T_j)</m> by states
    /// for <m>j = 1, \dots, n</m></returns>
    private static double[][] CalculateDriftUs(
      DriftFactorTreeBuilder builder,
      TreeIntegrator integrator,
      int rateIndex,
      IReadOnlyList<RateAnnuity> rateAnnuities)
    {
      var driftFactorTree = builder.Build(rateIndex, rateAnnuities);
      return integrator.Integrate(rateIndex, driftFactorTree);
    }

    private static RateAnnuity CalculateRateAnnuity(
      double p, RateAnnuity hi, RateAnnuity lo)
    {
      return RateAnnuity.FromValue(lo.Value + p*(hi.Value - lo.Value),
        lo.Annuity + p*(hi.Annuity - lo.Annuity));
    }


    #endregion

    #region Utilities

    private static double Square(double x)
    {
      return x*x;
    }

    private static void Fill<T>(T[] a, T v)
    {
      for (int i = 0, n = a.Length; i < n; ++i)
        a[i] = v;
    }

    private static T[] NewCopy<T>(T[] source, int count)
    {
      Debug.Assert(count >= 0);
      Debug.Assert(source != null && source.Length >= count);
      var a = new T[count];
      Array.Copy(source, a, count);
      return a;
    }

    #endregion

    #region Nested type: TreeIntegrator

    private class TreeIntegrator
    {
      private readonly PcvBinomialTree _tree;
      private readonly double[] _workspace1, _workspace2;

      public TreeIntegrator(PcvBinomialTree tree)
      {
        var size = tree.TotalStepCount + 1;
        _workspace1 = new double[size];
        _workspace2 = new double[size];
        _tree = tree;
      }

      public double[][] Integrate(
        int rateIndex,
        BandedList<double>[] incrementTree)
      {
#if DEBUG
        // Set workspace to huge negative values,
        // hence if anything wrong, it manifests in the
        // final values.
        Fill(_workspace1, -9999);
        Fill(_workspace2, -9999);
#endif
        return Integrate(_tree, rateIndex, incrementTree,
          _workspace1, _workspace2);
      }

      private static double[][] Integrate(
        PcvBinomialTree tree,
        int rateIndex,
        IReadOnlyList<BandedList<double>> incrementTree,
        double[] workspace1,
        double[] workspace2)
      {
        var jmpProbs = tree.UpJumpProbabilities;
        var variances = tree.Variances;
        var maps = tree.StepMaps;
        var terminalIndex = maps[rateIndex];
        var results = new double[rateIndex + 1][];
        var values = workspace2;
        values[0] = 0.0;
        for (int t = 0, stepIndex = 0, tEnd = maps.Count; t < tEnd; ++t)
        {
          double p = jmpProbs[t], vdt = variances[t];
          var stopIndex = maps[t];
          if (stopIndex > terminalIndex) stopIndex = terminalIndex;
          while (++stepIndex <= stopIndex)
          {
            var increments = incrementTree[stepIndex - 1];
            var prev = values;
            values = prev == workspace1 ? workspace2 : workspace1;
            tree.EvolveOneStep(stepIndex, p,
              i => prev[i] + increments[i]*vdt, values);
          }
          results[t] = NewCopy(values, stopIndex + 1);
          if (stopIndex >= terminalIndex) break;
          stepIndex = stopIndex;
        }
        return results;
      }
    }

    #endregion

    #region Nested type: DriftFactorTreeBuilder

    private class DriftFactorTreeBuilder
    {
      private readonly PcvBinomialTree _tree;
      private readonly Distribution _kind;
      private readonly RateAnnuity[] _workspace1, _workspace2;

      public DriftFactorTreeBuilder(PcvBinomialTree tree, Distribution kind)
      {
        _kind = kind;
        _tree = tree;
        var size = tree.TotalStepCount + 1;
        _workspace1 = new RateAnnuity[size];
        _workspace2 = new RateAnnuity[size];
      }

      public BandedList<double>[] Build(
        int rateIndex,
        IReadOnlyList<RateAnnuity> rateAnnuities)
      {
#if DEBUG
        // Set workspace to huge negative values,
        // hence if anything wrong, it manifest in the
        // final values.
        Fill(_workspace1, RateAnnuity.FromValue(-99, -9999));
        Fill(_workspace2, RateAnnuity.FromValue(-99, -9999));
#endif
        return Build(_tree, _kind, rateIndex, rateAnnuities,
          _workspace1, _workspace2);
      }

      private static BandedList<double>[] Build(
        PcvBinomialTree tree,
        Distribution kind,
        int rateIndex,
        IReadOnlyList<RateAnnuity> rateAnnuities,
        RateAnnuity[] workspace1,
        RateAnnuity[] workspace2)
      {
        var endStepIndex = tree.StepMaps[rateIndex];
        Debug.Assert(rateAnnuities.Count == endStepIndex + 1);

        var jmpProbs = tree.UpJumpProbabilities;
        var maps = tree.StepMaps;
        Debug.Assert(maps[maps.Count - 1] >= endStepIndex);

        //
        // Backward induction to build all the instantaneous drifts
        //
        var dftree = new BandedList<double>[endStepIndex + 1];
        int startIndex = endStepIndex;
        for (int t = tree.GetMapIndex(endStepIndex); t >= 0; --t)
        {
          double p = jmpProbs[t], q = 1 - p;
          int stopIndex = t > 0 ? maps[t - 1] : 0;
          for (int stepIndex = startIndex; --stepIndex >= stopIndex;)
          {
            var xvalues = rateAnnuities == workspace1 ? workspace2 : workspace1;
            var probs = tree.GetProbabilities(stepIndex);
            int start = 0;
            const double cutoff = PcvBinomialTree.Cutoff;
            for (; start <= stepIndex && probs[start] < cutoff; ++start)
            {
              xvalues[start] = new RateAnnuity();
            }

            var stop = stepIndex + 1;
            for (int i = start; i <= stepIndex; ++i)
            {
              if (probs[i] < cutoff)
              {
                xvalues[i] = new RateAnnuity();
                stop = i;
                break;
              }
              xvalues[i] = CalculateRateAnnuity(
                p, rateAnnuities[i + 1], rateAnnuities[i]);
            }

            var data = new double[stop - start];
            for (int i = start; i < stop; ++i)
              data[i - start] = xvalues[i].GetFactor(kind);
            dftree[stepIndex] = new BandedList<double>(
              stepIndex + 1, start, data);

            // prepare the next loop
            rateAnnuities = xvalues;
          }
          startIndex = stopIndex;
        }

        return dftree;
      }
    }

    #endregion
  }
}
