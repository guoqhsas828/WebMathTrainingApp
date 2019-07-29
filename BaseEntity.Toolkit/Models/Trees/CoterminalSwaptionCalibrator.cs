// 
// 
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.BGM.Native;
using BaseEntity.Toolkit.Numerics;
using Distribution = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;
using Ax = BaseEntity.Toolkit.Util.Collections.ListUtil;

namespace BaseEntity.Toolkit.Models.Trees
{
  public class CoterminalSwaptionCalibrator
  {
    #region Data

    private readonly LmmBinomialTree _tree;
    private readonly IReadOnlyList<SwaptionInfo> _coterminalSwaptions;
    private readonly double[] _fractions;
    private readonly double _accuracy;

    #endregion

    #region Constructor

    public CoterminalSwaptionCalibrator(
      IEnumerable<SwaptionInfo> coterminalSwaptions,
      Dt asOf, Dt maturity,
      Distribution kind,
      int initialSteps = 0, int middleSteps = 0, double accuracy = 0)
    {
      var ctswpn = _coterminalSwaptions = coterminalSwaptions.ToArray();

      int count = ctswpn.Count;
      double[] resets = new double[count],
        rates = new double[count],
        fractions = new double[count];
      var expiryDiscountFactor = Fill(ctswpn,
        asOf, maturity, resets, rates, fractions);

      var tenorDates = new double[count + 1];
      Array.Copy(resets, tenorDates, count);
      tenorDates[count] = (maturity - asOf)/DaysPerYear;

      var scaledRates = new double[count];
      var terminalDiscountFactor = expiryDiscountFactor;
      for (int i = 0; i < count; ++i)
      {
        var scaledRate = scaledRates[i] = fractions[i]*rates[i];
        terminalDiscountFactor /= 1 + scaledRate;
      }

      var discountFactors = new double[count + 1];
      discountFactors[count] = terminalDiscountFactor;
      for (int i = count; --i >= 0;)
      {
        discountFactors[i] = discountFactors[i + 1]*(1 + scaledRates[i]);
      }

      // Setup the tree
      var steps = Ax.NewArray(count, i => ctswpn[i].Steps);
      CheckSteps(steps, tenorDates, initialSteps, middleSteps);

      var betas = new double[count];
      _tree = LmmBinomialTree.Create(tenorDates, discountFactors,
        steps, kind, betas);
      _fractions = fractions;
      _accuracy = accuracy > 0 ? accuracy : 1E-9;
    }

    private static void CheckSteps(int[] steps,
      double[] tenorDates, int initialSteps, int middleSteps)
    {
      int count = steps.Length;

      if (initialSteps > 0)
        steps[0] = initialSteps;
      if (middleSteps > 0)
      {
        for (int i = 1; i < count; ++i)
          steps[i] = middleSteps;
        if (initialSteps > 0) return;
      }

      // If all steps are specified, no more check.
      var nonZeroCount = steps.Count(v => v > 0);
      if (nonZeroCount == count) return;

      // If steps are not specified or partially specified,
      // we calculate the average time per steps and use it
      // to normalize all the steps.
      double timePerStep;
      if (nonZeroCount == 0)
      {
        timePerStep = tenorDates[count - 1]/Math.Max(5*count, 100);
      }
      else
      {
        int sumSteps = 0;
        double sumDuration = 0;
        for (int i = 0; i < count; ++i)
        {
          if (steps[i] <= 0) continue;
          sumSteps += steps[i];
          sumDuration += tenorDates[i] - (i == 0 ? 0 : tenorDates[i - 1]);
        }
        timePerStep = Math.Min(sumDuration/sumSteps,
          tenorDates[count - 1]/(sumSteps + 5*(count - nonZeroCount)));
      }
      if (timePerStep < 1.0/365)
      {
        timePerStep = 1.0/365;
      }

      for (int i = 0; i < count; ++i)
      {
        var n = (tenorDates[i] - (i == 0 ? 0 : tenorDates[i - 1]))/timePerStep;
        steps[i] = Math.Max((int)n, 5);
      }
      return;
    }

    #endregion

    #region Properties

    public IReadOnlyList<SwaptionInfo> CoterminalSwaptions
    {
      get { return _coterminalSwaptions; }
    }

    public IReadOnlyList<double> Fractions
    {
      get { return _fractions; }
    }

    public LmmBinomialTree Tree
    {
      get { return _tree; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Calibrate tree volatilities to match the co-terminal swaptions
    /// </summary>
    /// <exception cref="Exception">unable to match co-terminal swaptions</exception>
    public void Fit()
    {
      int count = CoterminalSwaptions.Count, stop = count;
      for (int t = count; --t >= 0;)
      {
        double volatility;
        if (TrySolveVolatility(t, stop, out volatility))
        {
          SetVolatility(volatility, t, stop);
          stop = t;
        }
        else
        {
          if (t == 0)
            throw new Exception("unable to match co-terminal swaptions");
        }
      }
    }

    /// <summary>
    /// Calculates the Bermudan swaption value from the calibrated binomial tree.
    /// </summary>
    /// <param name="beginRateIndex">Index of the begin rate</param>
    /// <param name="lastCallRateIndex">Index of the last rate to call</param>
    /// <returns>System.Double.</returns>
    public double CalculateBermudanValue(
      int beginRateIndex = 0,
      int lastCallRateIndex = -1)
    {
      if (lastCallRateIndex < 0)
      {
        lastCallRateIndex = CoterminalSwaptions.Count - 1;
      }
      return _tree.EvaluateBermudanSwaption(
        beginRateIndex, lastCallRateIndex,
        Ax.MapList(_coterminalSwaptions,
          s => s.OptionType == OptionType.Call ? 1
            : (s.OptionType == OptionType.Put ? -1 : 0)),
        Ax.MapList(_coterminalSwaptions, s => s.Coupon),
        _fractions);
    }

    /// <summary>
    /// Sets the volatility of the forward rates between
    ///  the <c>startIndex</c> inclusive and the <c>stopIndex</c> exclusive.
    /// </summary>
    /// <param name="beta">The beta, representing volatility</param>
    /// <param name="startIndex">The start index</param>
    /// <param name="stopIndex">The stop index</param>
    public void SetVolatility(double beta, int startIndex, int stopIndex)
    {
      var betas = (double[]) _tree.Betas;
      for (int i = startIndex; i < stopIndex; ++i)
        betas[i] = beta;
    }

    /// <summary>
    /// Calculates the co-terminal swaption value starting
    ///  with the forward rate at <c>startRateIndex</c>.
    /// </summary>
    /// <param name="startRateIndex">Start rate index</param>
    /// <returns>System.Double.</returns>
    public double CalculateSwaptionValue(int startRateIndex)
    {
      var tree = _tree;
      var swaps = _tree.CalculateSwapRates(startRateIndex, _fractions);
      var swpn = _coterminalSwaptions[startRateIndex];
#if DEBUG
      var level = tree.CalculateExpectationAtExpiry(
        startRateIndex, i => swaps[i].Annuity);
      var floatValue = tree.CalculateExpectationAtExpiry(
        startRateIndex, i => swaps[i].Value);
      var rate = floatValue/level;
      Debug.Assert(Math.Abs(level - swpn.Level) < 1E-4*(1 + level));
      Debug.Assert(Math.Abs(rate - swpn.Rate) < 1E-5);
#endif
      var strike = swpn.Coupon;
      var sgn = swpn.OptionType == OptionType.Call ? 1 : -1;
      return tree.CalculateExpectationAtExpiry(startRateIndex,
        i => Math.Max(sgn*(swaps[i].Value - strike*swaps[i].Annuity), 0));
    }

    /// <summary>
    /// Try to solve a tree volatility matching the specified swaption value.
    /// </summary>
    /// <param name="startRateIndex">Index of the first rate</param>
    /// <param name="stopRateIndex">Index of the last rate</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns><c>true</c> if a solution found, <c>false</c> otherwise.</returns>
    public bool TrySolveVolatility(
      int startRateIndex, int stopRateIndex,
      out double volatility)
    {
      double target = _coterminalSwaptions[startRateIndex].Value;
      Func<double, double> f = x =>
      {
        SetVolatility(x, startRateIndex, stopRateIndex);
        return CalculateSwaptionValue(startRateIndex);
      };

      double lower = 0.3, upper = 0.6;
      if (_tree.Distribution == Distribution.Normal)
      {
        lower = 0.001;
        upper = 0.01;
      }
      try
      {
        var solver = new Brent2();
        solver.setToleranceF(_accuracy);
        solver.setToleranceX(_accuracy);
        volatility = solver.solve(f, null, target, lower, upper);
        return true;
      }
      catch (SolverException)
      {
        volatility = double.NaN;
        return false;
      }
    }

    /// <summary>
    /// Calculates the implied forward rates and the discount factor
    ///  at the first reset date from the specified co-terminal swaptions.
    /// Also fills the arrays of reset times and fractions by rates.
    /// </summary>
    /// <param name="coterminalSwaptions">The co-terminal swaptions</param>
    /// <param name="asOf">Today</param>
    /// <param name="maturity">The maturity date</param>
    /// <param name="resets">The array of reset times</param>
    /// <param name="rates">The array of forward rates</param>
    /// <param name="fractions">The array of fractions</param>
    /// <returns>System.Double</returns>
    private static double Fill(
      IReadOnlyList<SwaptionInfo> coterminalSwaptions,
      Dt asOf, Dt maturity,
      double[] resets, double[] rates, double[] fractions)
    {
      var df = ImplyDiscountFactor1(coterminalSwaptions[0], maturity);
      double lastLevel = 0.0, lastFloatValue = 0.0;
      for (int i = coterminalSwaptions.Count; --i >= 0;)
      {
        var swpn = coterminalSwaptions[i];
        resets[i] = (swpn.Date - asOf)/DaysPerYear;
        var annuity = swpn.Level - lastLevel;
        var fraction = fractions[i] = annuity/df;
        var floatValue = swpn.Level*swpn.Rate;
        var rate = rates[i] = (floatValue - lastFloatValue)/annuity;

        // update discount factor
        df *= (1 + fraction*rate);
        lastLevel = swpn.Level;
        lastFloatValue = floatValue;
      }

      RoundtripCoterminalSwaptions(coterminalSwaptions, df, rates, fractions);
      return df;
    }

    /// <summary>
    /// Check that the forward rates can back out the co-terminal swaps exactly.
    /// </summary>
    /// <param name="coterminalSwaptions">The co-terminal swaptions.</param>
    /// <param name="expiryDiscountFactor">The discount factor on the first expiry date</param>
    /// <param name="rates">The forward rates.</param>
    /// <param name="fractions">The fractions associated with the forward rates</param>
    [Conditional("DEBUG")]
    private static void RoundtripCoterminalSwaptions(
      IReadOnlyList<SwaptionInfo> coterminalSwaptions,
      double expiryDiscountFactor,
      IReadOnlyList<double> rates,
      IReadOnlyList<double> fractions)
    {
      Debug.Assert(coterminalSwaptions.Count == rates.Count);
      Debug.Assert(coterminalSwaptions.Count == fractions.Count);

      int count = coterminalSwaptions.Count;
      var terminalDiscountFactor = expiryDiscountFactor;
      for (int i = 0; i < count; ++i)
        terminalDiscountFactor /= 1 + fractions[i]*rates[i];

      double annuity = 1, swapLevel = 0, floatValue = 0;
      for (int i = count; --i >= 0;)
      {
        var mrate = fractions[i]*rates[i];
        floatValue += mrate*annuity;
        swapLevel += fractions[i]*annuity;
        annuity *= (1 + mrate);

        var rate = floatValue/swapLevel;
        var level = swapLevel*terminalDiscountFactor;
        var swpn = coterminalSwaptions[i];
        Debug.Assert(Math.Abs(level - swpn.Level) < 1E-6);
        Debug.Assert(Math.Abs(rate - swpn.Rate) < 1E-6);
      }
    }

    private static double ImplyDiscountFactor(
      SwaptionInfo swpn, Dt maturity)
    {
      return swpn.Level/(maturity - swpn.Date)*DaysPerYear;
    }

    private static double ImplyDiscountFactor1(
      SwaptionInfo swpn, Dt maturity)
    {
      var duration = maturity - swpn.Date;
      var nYears = duration/365;
      double fraction = duration/DaysPerYear;
      if (nYears > 1)
      {
        var dt = fraction/nYears;
        var r = swpn.Rate*dt;
        fraction = dt*(Math.Exp(nYears*r) - 1)/(Math.Exp(r) - 1);
      }
      return swpn.Level/fraction;
    }

    private const double DaysPerYear = 365.25;

    #endregion
  }
}
