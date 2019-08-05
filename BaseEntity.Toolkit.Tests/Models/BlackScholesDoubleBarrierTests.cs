//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;

namespace BaseEntity.Toolkit.Tests.Models
{
  using NUnit.Framework;

  [TestFixture]
  public class BlackScholesDoubleBarrierTests
  {
    [Test]
    public static void TouchProbabilityBaug()
    {
      double scale = 10;
      double S = 100, T = 0.25, r = 0.05, d = 0.02;
      double[] sigmas = {0.1, 0.2, 0.3, 0.5};
      double[,] barriers =
      {
        {80, 120},
        {85, 115},
        {90, 110},
        {95, 105},
      };
      double[,] expects =
      {
        {9.8716, 8.9307, 6.3272, 1.9094},
        {9.7961, 7.2300, 3.7100, 0.4271},
        {8.9054, 3.6752, 0.7960, 0.0059},
        {3.6323, 0.0911, 0.0002, 0.0000},
      };

      int rows = barriers.GetLength(0), cols = sigmas.Length;
      double[,] actuals = new double[rows, cols];
      for (int i = 0; i < rows; ++i)
      {
        double L = barriers[i, 0], U = barriers[i, 1];
        for (int j = 0; j < cols; ++j)
        {
          var sigma = sigmas[j];
          var p = DoubleBarrierOptionPricer.NoTouchProbability(
            T, S, r, d, L, U, sigma)*Math.Exp(-r*T);
          actuals[i, j] = scale*p;
        }
      }
      Assert.That(actuals, Is.EqualTo(expects).Within(1E-4));
    }

    [Test]
    public static void TouchPayAtHitBaug()
    {
      double scale = 10;
      double S = 100, T = 0.25, r = 0.05, d = 0.02;
      double[] sigmas = { 0.1, 0.2, 0.3, 0.5 };
      double[,] barriers =
      {
        {80, 120},
        {85, 115},
        {90, 110},
        {95, 105},
      };
      double[,] expects =
      {
        {0.0000, 0.2402, 1.4076, 3.8160},
        {0.0075, 0.9910, 2.8098, 4.6612},
        {0.2656, 2.7954, 4.4024, 4.9266},
        {2.6285, 4.7523, 4.9096, 4.9675},
      };

      int rows = barriers.GetLength(0), cols = sigmas.Length;
      double[,] actuals = new double[rows, cols];
      for (int i = 0; i < rows; ++i)
      {
        double L = barriers[i, 0], U = barriers[i, 1];
        for (int j = 0; j < cols; ++j)
        {
          var sigma = sigmas[j];
          var p = DoubleBarrierOptionPricer.OneSideTouchPayAtHit(
            T, S, r, d, L, U, sigma);
          actuals[i, j] = scale * p;
        }
      }
      Assert.That(actuals, Is.EqualTo(expects).Within(1E-4));
    }

    /// <summary>
    ///  Check the consistency with single barrier with no touch probability
    /// </summary>
    [Test]
    public static void AsymptoticTouchProbability()
    {
      double S = 100, T = 0.25, rd = 0.05, rf = 0.02;
      double[] sigmas = { 0.1, 0.2, 0.3, 0.5 };
      double[] expects = new double[sigmas.Length];
      double[] actuals = new double[sigmas.Length];

      // Lower barrier far away: up-in probabilities
      {
        double U = 105;
        for (int i = 0; i < sigmas.Length; ++i)
        {
          var sigma = sigmas[i];
          expects[i] = TimeDependentBarrierOption.CalculateUpInProbability(
            S, U, T, sigma, rd, rf);
          actuals[i] = 1 - DoubleBarrierOptionPricer.NoTouchProbability(
            T, S, rd, rf, S/10, U, sigma);
        }
      }
      Assert.That(actuals, Is.EqualTo(expects).Within(1E-14), "Up/In");

      // Upper barrier far away: down-in probabilities
      {
        double L = 95;
        for (int i = 0; i < sigmas.Length; ++i)
        {
          var sigma = sigmas[i];
          expects[i] = TimeDependentBarrierOption.CalculateDownInProbability(
            S, L, T, sigma, rd, rf);
          actuals[i] = 1 - DoubleBarrierOptionPricer.NoTouchProbability(
            T, S, rd, rf, L, S*10, sigma);
        }
      }
      Assert.That(actuals, Is.EqualTo(expects).Within(1E-14), "Down/In");
    }

    /// <summary>
    ///  Check the consistency with single barrier with pay at hit
    /// </summary>
    [Test]
    public static void AsymptoticPayAtHit()
    {
      double S = 100, T = 0.25, rd = 0.05, rf = 0.02;
      double[] sigmas = { 0.1, 0.2, 0.3, 0.5 };
      double[] expects = new double[sigmas.Length];
      double[] actuals = new double[sigmas.Length];

      const int oneTouchPayAtHit = (int) (
        OptionBarrierFlag.OneTouch | OptionBarrierFlag.PayAtBarrierHit);

      // Lower barrier far away: up-in values
      {
        double U = 105;
        for (int i = 0; i < sigmas.Length; ++i)
        {
          var sigma = sigmas[i];
          expects[i] = TimeDependentBarrierOption.Price(
            OptionType.None, OptionBarrierType.OneTouch,
            T, S, 0, U, 1.0, rd, rf, sigma, oneTouchPayAtHit);
          actuals[i] = DoubleBarrierOptionPricer.OneSideTouchPayAtHit(
            T, S, rd, rf, U, S/10, sigma);
        }
      }
      Assert.That(actuals, Is.EqualTo(expects).Within(1E-6), "Up/In");

      // Upper barrier far away: down-in values
      {
        double L = 95;
        for (int i = 0; i < sigmas.Length; ++i)
        {
          var sigma = sigmas[i];
          expects[i] = TimeDependentBarrierOption.Price(
            OptionType.None, OptionBarrierType.OneTouch,
            T, S, 0, L, 1.0, rd, rf, sigma, oneTouchPayAtHit);
          actuals[i] = DoubleBarrierOptionPricer.OneSideTouchPayAtHit(
            T, S, rd, rf, L, S*10, sigma);
        }
      } 
      Assert.That(actuals, Is.EqualTo(expects).Within(1E-6), "Down/In");
    }

    public static void Kortze_Joseph()
    {
      double[] time = {0.5041, 0.4192, 0.3370, 0.2521, 0.1671, 0.0849, 0.0027};
      double[] spot = {85.5, 90, 92.5, 95, 97.5, 100, 102.5, 105, 107.5, 110, 114.5};
      double[,] expects =
      {
        {2.64, 24.34, 33.51, 39.86, 43.14, 43.33, 40.63, 35.43, 28.21, 19.51, 1.98},
        {4.67, 42.96, 59.16, 70.38, 76.16, 76.49, 71.74, 62.56, 49.80, 34.44, 3.50},
        {8.09, 74.47, 102.55, 121.99, 132.02, 132.59, 124.36, 108.44, 86.32, 59.71, 6.07},
        {14.29, 131.49, 181.05, 215.38, 233.08, 234.09, 219.55, 191.44, 152.40, 105.41, 10.72},
        {25.23, 232.19, 319.68, 380.27, 411.49, 413.27, 387.58, 337.96, 269.04, 186.09, 18.93},
        {44.25, 405.32, 555.68, 658.52, 710.96, 713.82, 670.51, 586.37, 468.39, 324.97, 33.14},
        {250.95, 997.98, 999.79, 999.79, 999.79, 999.79, 999.79, 999.79, 999.56, 984.57, 188.08},
      };

    }
  }
}