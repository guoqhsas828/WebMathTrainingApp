using System;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  ///<summary>
  /// Static functions for calculation of Backtesting statistical Tests
  ///</summary>
  public static class BacktestUtil
  {
    /// <summary>
    /// </summary>
    /// <param name="confidenceInterval"></param>
    /// <param name="observations"></param>
    /// <param name="exceptions"></param>
    /// <returns></returns>
    public static bool ProportionOfFailuresTest(double confidenceInterval, int observations, int exceptions) 
    {
      double p = 1.0 - confidenceInterval; 
      double T = observations;
      double x = exceptions;
      double expected = Math.Pow(1.0 - p, T - x) * Math.Pow(p, x);
      double actual = Math.Pow(1.0 - (x/T), T - x) * Math.Pow(x/T, x);
      return ChiSquaredLikelihoodRationTest(confidenceInterval, expected, actual); 
    }

    /// <summary>
    /// </summary>
    /// <param name="confidenceInterval"></param>
    /// <param name="violations"></param>
    /// <returns></returns>
    public static bool ChristoffersenIntervalForecastTest(double confidenceInterval, bool[] violations)
    {
      double n00, n01, n10, n11;
      n00 = n01 = n10 = n11 = 0.0;
      for (int i = 1; i < violations.Length; ++i)
      {
        if(violations[i - 1] && violations[i])
        {
          n11++; 
        }
        else if (violations[i - 1] && !violations[i])
        {
          n10++;
        }
        else if (!violations[i - 1] && violations[i])
        {
          n01++;
        }
        else if (!violations[i - 1] && !violations[i])
        {
          n00++; 
        }
      }
      double pi0 = (n00 + n01) == 0.0 ? 0.0 : n01 / (n00 + n01);
      double pi1 = (n10 + n11) == 0.0 ? 0.0 : n11 / (n10 + n11);
      double pi = (n01 + n11) / (n00 + n01 + n10 + n11);
      double expected = Math.Pow(1 - pi, n00 + n10) * Math.Pow(pi, n01 + n11);
      double actual = Math.Pow(1 - pi0, n00) * Math.Pow(pi0, n01) * Math.Pow(1-pi1, n10) * Math.Pow(pi1, n11);

      return ChiSquaredLikelihoodRationTest(confidenceInterval, expected, actual);

    }

    /// <summary>
    /// </summary>
    /// <param name="confidenceInterval"></param>
    /// <param name="expected"></param>
    /// <param name="actual"></param>
    /// <returns></returns>
    public static bool ChiSquaredLikelihoodRationTest(double confidenceInterval, double expected, double actual) 
    {
      if (expected.ApproximatelyEqualsTo(0.0) && actual.ApproximatelyEqualsTo(0.0))
        return true;
      if (actual == 0.0)
        actual = double.Epsilon; 
      double likelihoodRatio = -2.0 * Math.Log(expected / actual); 
      double[] confidenceIntervals = new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.975, 0.99, 0.995, 1.0 };
      double[] criticalValues =      new[] { 0.000, 0.00, 0.00, 0.00, 0.02, 0.10, 0.45, 1.32, 2.71, 3.84, 5.02, 6.63, 7.88, 10.0};
      int idx = Array.BinarySearch<double>(confidenceIntervals, confidenceInterval);
      double criticalValue = 0.0;
      if (idx >= 0)
      {
        criticalValue = criticalValues[idx];
      }
      else
      {
        idx = ~idx;
        double dp = confidenceIntervals[idx] - confidenceIntervals[idx-1];
        criticalValue = criticalValues[idx - 1] + (criticalValues[idx] - criticalValues[idx - 1]) * ((confidenceInterval - confidenceIntervals[idx - 1]) / dp);
      }

      return  likelihoodRatio <= criticalValue; 
    }
  }
}