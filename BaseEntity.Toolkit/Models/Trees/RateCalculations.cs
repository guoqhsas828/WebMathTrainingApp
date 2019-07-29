// 
// 
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.BGM.Native;
using Distribution = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;

namespace BaseEntity.Toolkit.Models.Trees
{
  public static class RateCalculations
  {
    #region Simple rate and annuity calculations

    /// <summary>
    /// Gets the sum of the annuity and the floating value
    /// </summary>
    /// <param name="r">The rate-annuity.</param>
    /// <returns>The sum.</returns>
    /// <remarks>
    ///  <para>For forward swap rate, the sum is simply the discount factor
    ///  at the period begin, <m>B_{i-1} = B_i + \delta_i\,L_i\,B_i</m></para>
    /// </remarks>
    public static double Sum(this RateAnnuity r)
    {
      return Math.Max(r.Annuity + r.Value, 1E-30);
    }

    /// <summary>
    /// Gets the drift factor of the forward rate
    /// under the specified distribution type.
    /// </summary>
    /// <param name="r">The rate-annuity.</param>
    /// <param name="kind">The distribution type</param>
    /// <param name="alpha">The lower bond of the forward rate,
    /// only required under the drifted log-normal distribution</param>
    /// <returns>System.Double.</returns>
    public static double GetFactor(this RateAnnuity r,
      Distribution kind, double alpha = double.NaN)
    {
      switch (kind)
      {
      case Distribution.LogNormal:
        return r.Value/r.Sum();
      case Distribution.Normal:
        return r.Annuity/r.Sum();
      case Distribution.ShiftedLogNormal:
        Debug.Assert(!double.IsNaN(alpha));
        return (r.Value + alpha*r.Annuity)/r.Sum();
      }
      return 0.0;
    }

    /// <summary>
    ///  Calculate the intrinsic value <m>\phi (V-cA)</m>
    ///  where <m>c</m> is the strike rate,
    ///  <m>\phi = 1</m> for call and <m>\phi = -1</m> for put
    /// </summary>
    /// <param name="r">The rate-annuity.</param>
    /// <param name="strikeRate">The strike rate</param>
    /// <param name="sign">The sign, 1 for call and -1 for put.</param>
    /// <returns>System.Double.</returns>
    public static double Intrinsic(this RateAnnuity r,
      double strikeRate, int sign)
    {
      var v = sign*(r.Value - strikeRate*r.Annuity);
      return v > 0 ? v : 0;
    }

    #endregion

    #region Tree methods

    public static RateAnnuity StepExpectation(
      int stepIndex, int stateIndex, double upJumpProbability,
      RateAnnuity hi, RateAnnuity lo)
    {
      return RateAnnuity.FromValue(
        lo.Value + upJumpProbability*(hi.Value - lo.Value),
        lo.Annuity + upJumpProbability*(hi.Annuity - lo.Annuity));
    }

    /// <summary>
    /// Calculates the log normal terminal values.
    /// </summary>
    /// <param name="tree">The binomial tree</param>
    /// <param name="expectation">The expectation to match</param>
    /// <param name="endStepIndex">The index of the end step</param>
    /// <param name="beta">The flat volatility factor of the rate</param>
    /// <param name="driftFn">The function to calculate the drift</param>
    /// <returns>IReadOnlyList&lt;System.Double&gt;.</returns>
    /// <exception cref="OverflowException">Unable to match the expectation</exception>
    /// <exception cref="System.OverflowException">Unable to match the expectation</exception>
    public static BandedList<double> CalculateLogNormalTerminalValues(
      this PcvBinomialTree tree,
      double expectation,
      int endStepIndex,
      double beta = 1.0,
      Func<int, double> driftFn = null)
    {
      //! For each node <m>k</m>, we calculate <m>d\beta(k - n p)</m>
      var dbeta = tree.JumpSize*beta;
      var commonDrift = endStepIndex*tree.GetJumpProbability(endStepIndex)*dbeta;
      var banded = tree.NodeProbabilities[endStepIndex];
      var start = banded.BeginIndex;
      var probs = banded.Data;
      var n = probs.Count;
      var values = new double[n];
      double mean = 0.0, sump = 0.0;
      if (driftFn != null)
      {
        for (int i = 0; i < n; ++i)
        {
          var k = i + start;
          var p = probs[i];
          sump += p;
          mean += p*(values[i] = Math.Exp(driftFn(k) + k*dbeta - commonDrift));
        }
      }
      else
      {
        for (int i = 0; i < n; ++i)
        {
          var p = probs[i];
          sump += p;
          mean += p*(values[i] = Math.Exp((i + start)*dbeta - commonDrift));
        }
      }
      if (mean < double.Epsilon)
      {
        throw new OverflowException("Unable to match the expectation");
      }
      var scale = expectation*sump/mean;
      for (int i = 0; i < n; ++i)
        values[i] *= scale;
      return new BandedList<double>(endStepIndex + 1, start, values);
    }

    /// <summary>
    /// Calculates the normal terminal values.
    /// </summary>
    /// <param name="tree">The binomial tree</param>
    /// <param name="expectation">The expectation</param>
    /// <param name="endStepIndex">End index of the step</param>
    /// <param name="beta">The beta.</param>
    /// <param name="forceLowerBound">The lower bound of the variable, or <c>NaN</c> if not bounded below</param>
    /// <param name="sumUs">The other drift factors</param>
    /// <param name="annuities">The multiplicative annuities</param>
    /// <returns>IReadOnlyList&lt;System.Double&gt;.</returns>
    /// <exception cref="ArgumentException">Cannot combine negative expectation with force non-negative</exception>
    /// <exception cref="InvalidOperationException">No positive values</exception>
    public static BandedList<double> CalculateNormalTerminalValues(
      this PcvBinomialTree tree,
      double expectation,
      int endStepIndex,
      double beta = 1.0,
      double forceLowerBound = Double.NaN,
      IReadOnlyList<double> sumUs = null,
      IReadOnlyList<double> annuities = null)
    {
      const double tiny = 1E-8;
      if (!Double.IsNaN(forceLowerBound) && expectation - tiny < forceLowerBound)
      {
        throw new ArgumentException(String.Format(
          "Lower bound {0} inconsistent with expectation {1}",
          forceLowerBound, expectation));
      }
      var dbeta = tree.JumpSize*beta;
      var commonDrift = endStepIndex*tree.GetJumpProbability(endStepIndex)*dbeta;
      var banded = tree.NodeProbabilities[endStepIndex];
      var start = banded.BeginIndex;
      var probs = banded.Data;
      var n = probs.Count;
      var rates = new double[n];
      double mean = 0.0, sump = 0.0, suma = 1;
      if (annuities != null)
      {
        Debug.Assert(sumUs != null);
        suma = 0;
        for (int i = 0; i < n; ++i)
        {
          var k = i + start;
          var p = probs[i];
          sump += p;
          suma += p*annuities[i];
          mean += p*annuities[i]*(rates[i] = -beta*sumUs[i] + k*dbeta - commonDrift);
        }
      }
      else
      {
        for (int i = 0; i < n; ++i)
        {
          var p = probs[i];
          sump += p;
          mean += p*(rates[i] = (i + start)*dbeta - commonDrift);
        }
      }
      var shift = (expectation - mean/sump)/suma;
      for (int i = 0; i < n; ++i)
        rates[i] += shift;

      if (!Double.IsNaN(forceLowerBound) && rates.Any(v => v < forceLowerBound))
      {
        mean = 0;
        sump = 0;
        for (int i = 0; i < n; ++i)
        {
          var v = rates[i] = rates[i] - forceLowerBound;
          if (v < 0) v = rates[i] = 0;
          var p = probs[i]*(annuities != null ? annuities[i] : 1.0);
          sump += p;
          mean += p*v;
        }
        if (mean <= 0)
        {
          throw new InvalidOperationException("No positive values");
        }
        var multiplier = (expectation - forceLowerBound*sump)/mean;
        for (int i = 0; i < n; ++i)
          rates[i] = rates[i]*multiplier + forceLowerBound;
      }

      if (annuities != null)
      {
        for (int i = 0; i < n; ++i)
          rates[i] *= annuities[i];
      }

      return new BandedList<double>(endStepIndex + 1, start, rates);
    }

    #endregion

    #region Bermudan Evaluations

    public static double CalculateBermudanPv(
      SwaptionInfo[] swpns,
      Dt asOf, Dt maturity,
      Distribution distribution,
      bool isAmericanOption = false)
    {
      var calibrator = new CoterminalSwaptionCalibrator(
        swpns, asOf, maturity, distribution);
      calibrator.Fit();
      return calibrator.CalculateBermudanValue();
    }

    #endregion
  }
}
