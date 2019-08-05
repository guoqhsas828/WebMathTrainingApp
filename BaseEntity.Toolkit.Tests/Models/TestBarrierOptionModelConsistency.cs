//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Diagnostics;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  ///   Check consistency between the Time Dependent model 
  ///   and the simple model of barrier options in the cases
  ///   with flat rates and flat volatiilities.
  /// </summary>
  [TestFixture]
  public class TestBarrierOptionModelConsistency
  {
    private const double //time = 1.9986310746064340,
      //stock = 195.00000000000000,
      strike = 190.00000000000000,
      barrier = 200.00000000000000,
      rebate = 100.00000000000000,
      rd = 0.050000000000000031,
      rf = 0.029999999999999999,
      sigma = 0.29999999999999999;

    [Test]
    public void NoRebate()
    {
      Dt settle = Dt.Today();
      Dt maturity = Dt.Add(settle, "2Y");
      double time = (maturity - settle) / 365.25;
      var barrierTypes = new[]
      {
        OptionBarrierType.DownIn, OptionBarrierType.DownOut,
        OptionBarrierType.UpIn, OptionBarrierType.UpOut
      };
      foreach (int stock in new[] {195, 205})
      {
        string lab = stock.ToString();
        foreach (OptionType type in new[] {OptionType.Call, OptionType.Put})
          foreach (OptionBarrierType btype in barrierTypes)
          {
            double fv1 = BarrierOption.P(type, btype,
              time, stock, strike, barrier, 0.0, rd, rf, sigma);
            double fv2 = TimeDependentBarrierOption.Price(
              type, btype, time, stock, strike, barrier, 0.0, rd, rf, sigma, 0);
            AssertEqual(lab + btype.ToString() + '.' + type.ToString(),
              fv1, fv2, 1E-12);
          }
      }
    }

    [Test]
    public void OutRebate()
    {
      Dt settle = Dt.Today();
      Dt maturity = Dt.Add(settle, "2Y");
      double time = (maturity - settle) / 365.0;
      var barrierTypes = new[]
      {
        OptionBarrierType.DownOut, OptionBarrierType.UpOut
      };
      foreach (int stock in new[] {195, 205})
      {
        string lab = stock.ToString();
        foreach (OptionType type in new[] {OptionType.Call, OptionType.Put})
          foreach (OptionBarrierType btype in barrierTypes)
          {
            double fv1 = BarrierOption.P(type, btype,
              time, stock, strike, barrier, rebate, rd, rf, sigma);
            double fv2 = TimeDependentBarrierOption.Price(
              type, btype, time, stock, strike, barrier, rebate, rd, rf, sigma,
              (int)OptionBarrierFlag.PayAtBarrierHit);
            AssertEqual(lab + btype.ToString() + '.' + type.ToString(),
              fv1, fv2, 1E-12);
          }
      }
    }

    [Test]
    public void InRebate()
    {
      Dt settle = Dt.Today();
      Dt maturity = Dt.Add(settle, "2Y");
      double time = (maturity - settle) / 365.0;
      var barrierTypes = new[]
      {
        OptionBarrierType.DownIn, OptionBarrierType.UpIn
      };
      foreach (int stock in new[] {195, 205})
      {
        string lab = stock.ToString();
        foreach (OptionType type in new[] {OptionType.Call, OptionType.Put})
          foreach (OptionBarrierType btype in barrierTypes)
          {
            double fv1 = BarrierOption.P(type, btype,
              time, stock, strike, barrier, rebate, rd, rf, sigma);
            double fv2 = TimeDependentBarrierOption.Price(
              type, btype, time, stock, strike, barrier, rebate, rd, rf, sigma, 0);
            AssertEqual(lab + btype.ToString() + '.' + type.ToString(),
              fv1, fv2, 1E-12);
          }
      }
    }

    [Test]
    public void PerformanceNoRebate()
    {
      const int count = 10000;
      const double time = 1.99998656446;
      var barrierTypes = new[]
      {
        OptionBarrierType.DownIn, OptionBarrierType.DownOut,
        OptionBarrierType.UpIn, OptionBarrierType.UpOut
      };
      var optionTypes = new[] { OptionType.Call, OptionType.Put };
      var timer1 = new Stopwatch();
      var timer2 = new Stopwatch();
      double avg1 = 0, avg2 = 0;
      GC.Collect(); // make sure no memory allocation.
      System.Threading.Thread.Sleep(20);

      timer1.Start();
      for (int i = 0, n = 0; i < count; ++i)
      {
        double stock = 195 + (205.0 - 195) * i / count;
        foreach (OptionType type in optionTypes)
          foreach (OptionBarrierType btype in barrierTypes)
          {
            double fv = BarrierOption.P(type, btype,
              time, stock, strike, barrier, 0.0, rd, rf, sigma);
            avg1 += (fv - avg1) / (++n);
          }
      }
      timer1.Stop();

      timer2.Start();
      for (int i = 0, n = 0; i < count; ++i)
      {
        double stock = 195 + (205.0 - 195) * i / count;
        foreach (OptionType type in optionTypes)
          foreach (OptionBarrierType btype in barrierTypes)
          {
            var fv = TimeDependentBarrierOption.Price(
              type, btype, time, stock, strike, barrier, 0.0, rd, rf, sigma, 0);
            avg2 += (fv - avg2) / (++n);
          }
      }
      timer2.Stop();

      // Both models produces the same number?
      AssertEqual("Average Price", avg1, avg2, 1E-12);
      // Make sure that TimeDependentBarrierOption is 15% faster.
      Assert.Greater(1.20, 1.0 * timer2.ElapsedTicks / timer1.ElapsedTicks, "Timing");
    }

    [Test]
    public void PerformanceOutRebate()
    {
      const int count = 10000;
      const double time = 1.99998656446;
      var barrierTypes = new[]
      {
        OptionBarrierType.DownOut,
        OptionBarrierType.UpOut
      };
      var optionTypes = new[] { OptionType.Call, OptionType.Put };
      var timer1 = new Stopwatch();
      var timer2 = new Stopwatch();
      double avg1 = 0, avg2 = 0;
      GC.Collect(); // make sure no memory allocation.

      timer1.Start();
      for (int i = 0, n = 0; i < count; ++i)
      {
        double stock = 195 + (205.0 - 195) * i / count;
        foreach (OptionType type in optionTypes)
          foreach (OptionBarrierType btype in barrierTypes)
          {
            double fv = BarrierOption.P(type, btype,
              time, stock, strike, barrier, rebate, rd, rf, sigma);
            avg1 += (fv - avg1) / (++n);
          }
      }
      timer1.Stop();

      timer2.Start();
      var flag = (int)OptionBarrierFlag.PayAtBarrierHit;
      for (int i = 0, n = 0; i < count; ++i)
      {
        double stock = 195 + (205.0 - 195) * i / count;
        foreach (OptionType type in optionTypes)
          foreach (OptionBarrierType btype in barrierTypes)
          {
            var fv = TimeDependentBarrierOption.Price(type, btype, time,
              stock, strike, barrier, rebate, rd, rf, sigma, flag);
            avg2 += (fv - avg2) / (++n);
          }
      }
      timer2.Stop();

      // Both models produces the same number?
      AssertEqual("Average Price", avg1, avg2, 1E-12);
      // Make sure that TimeDependentBarrierOption is not much slower.
      Assert.Greater(1.35, timer2.ElapsedTicks * 1.0 / timer1.ElapsedTicks, "Timing");
    }

    [Test]
    public void PerformanceInRebate()
    {
      const int count = 10000;
      const double time = 1.99998656446;
      var barrierTypes = new[]
      {
        OptionBarrierType.DownIn,
        OptionBarrierType.UpIn
      };
      var optionTypes = new[] { OptionType.Call, OptionType.Put };
      var timer1 = new Stopwatch();
      var timer2 = new Stopwatch();
      double avg1 = 0, avg2 = 0;
      GC.Collect(); // make sure no memory allocation.

      timer1.Start();
      for (int i = 0, n = 0; i < count; ++i)
      {
        double stock = 195 + (205.0 - 195) * i / count;
        foreach (OptionType type in optionTypes)
          foreach (OptionBarrierType btype in barrierTypes)
          {
            double fv = BarrierOption.P(type, btype, time,
              stock, strike, barrier, 0, rd, rf, sigma);
            avg1 += (fv - avg1) / (++n);
          }
      }
      timer1.Stop();

      timer2.Start();
      for (int i = 0, n = 0; i < count; ++i)
      {
        double stock = 195 + (205.0 - 195) * i / count;
        foreach (OptionType type in optionTypes)
          foreach (OptionBarrierType btype in barrierTypes)
          {
            var fv = TimeDependentBarrierOption.Price(type, btype, time,
              stock, strike, barrier, 0, rd, rf, sigma, 0);
            avg2 += (fv - avg2) / (++n);
          }
      }
      timer2.Stop();

      // Both models produces the same number?
      AssertEqual("Average Price", avg1, avg2, 1E-12);
      // Make sure that TimeDependentBarrierOption is 15% faster.
      Assert.Greater(1.00, timer2.ElapsedTicks * 1.0 / timer1.ElapsedTicks, "Timing");
    }


    [TestCase(OptionType.Call, OptionBarrierType.DownIn, 95)]
    [TestCase(OptionType.Call, OptionBarrierType.DownOut, 95)]
    [TestCase(OptionType.Call, OptionBarrierType.UpIn, 95)]
    [TestCase(OptionType.Call, OptionBarrierType.UpOut, 95)]
    [TestCase(OptionType.Call, OptionBarrierType.DownIn, 105)]
    [TestCase(OptionType.Call, OptionBarrierType.DownOut, 105)]
    [TestCase(OptionType.Call, OptionBarrierType.UpIn, 105)]
    [TestCase(OptionType.Call, OptionBarrierType.UpOut, 105)]
    [TestCase(OptionType.Put, OptionBarrierType.DownIn, 95)]
    [TestCase(OptionType.Put, OptionBarrierType.DownOut, 95)]
    [TestCase(OptionType.Put, OptionBarrierType.UpIn, 95)]
    [TestCase(OptionType.Put, OptionBarrierType.UpOut, 95)]
    [TestCase(OptionType.Put, OptionBarrierType.DownIn, 105)]
    [TestCase(OptionType.Put, OptionBarrierType.DownOut, 105)]
    [TestCase(OptionType.Put, OptionBarrierType.UpIn, 105)]
    [TestCase(OptionType.Put, OptionBarrierType.UpOut, 105)]
    public void TestBarrierOptionWithSmallVolatility(OptionType oType,
      OptionBarrierType bType, double B)
    {
      Dt settle = new Dt(20160620);
      Dt maturity = Dt.Add(settle, "2Y");
      var T = (maturity - settle)/365.25;
      const double volatility = 0.0001;
      const double S0 = 100;
      var K = 0.0;
      if (oType == OptionType.Call)
      {
        var min = Math.Min(S0, B);
        K = min - 5;
      }
      else
      {
        var max = Math.Max(S0, B);
        K = max + 5;
      }

      var option = new StockBasketOption(new Dt(1, 1, 1990), Currency.USD,
        new[] {1.0}, maturity, oType, OptionStyle.European,
        K, bType, B, bType, 0.0)
      {
        PayoffType = OptionPayoffType.Regular,
        SettlementType = SettlementType.Cash,
        Rebate = 0.0,
        StrikeDetermination = OptionStrikeDeterminationMethod.Fixed,
        UnderlyingDetermination = OptionUnderlyingDeterminationMethod.Regular,
        BarrierMonitoringFrequency = Frequency.Continuous,
        Description = "StockBasketOption"
      };

      var pricer = new StockBasketOptionPricer(option, settle, settle, new[] {S0},
        0.0, new[] {0.0}, null, new[] {volatility}, new[,] {{1.0}})
      {Notional = 1.0};

      var fp = pricer.FairValue();

      if (oType == OptionType.Call)
      {
        if (B > S0)
        {
          if (bType == OptionBarrierType.DownOut || bType == OptionBarrierType.UpIn)
            Assert.AreEqual(0.0, fp, 1E-14);
          else
            Assert.AreEqual(S0 - K, fp, 1E-14);
        }
        else
        {
          if (bType == OptionBarrierType.DownOut || bType == OptionBarrierType.UpIn)
            Assert.AreEqual(S0 - K, fp, 1E-14);
          else
            Assert.AreEqual(0.0, fp, 1E-14);
        }
      }
      else
      {
        if (B > S0)
        {
          if (bType == OptionBarrierType.DownOut || bType == OptionBarrierType.UpIn)
            Assert.AreEqual(0.0, fp, 1E-14);
          else
            Assert.AreEqual(K - S0, fp, 1E-14);
        }
        else
        {
          if (bType == OptionBarrierType.DownIn || bType == OptionBarrierType.UpOut)
            Assert.AreEqual(0.0, fp, 1E-14);
          else
            Assert.AreEqual(K - S0, fp, 1E-14);
        }
      }
    }
  }
}