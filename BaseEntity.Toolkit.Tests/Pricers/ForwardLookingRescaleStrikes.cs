//
// Copyright (c)    2015. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class ForwardLookingRescaleStrikes : ToolkitTestBase
  {
    #region util
    private static double AdjustTrancheLevel(
    bool forAmotize,
    double level, double prevLoss_, double prevAmor_)
    {
      double prevBasketLoss = forAmotize ? prevAmor_ : prevLoss_;
      level -= prevBasketLoss;
      if (level <= 0)
        return 0.0;
      {
        double remainingBasket = 1 - prevAmor_ - prevLoss_;
        if (remainingBasket < 1E-15)
          return 0.0;
        level /= remainingBasket;
        if (level > 1) level = 1;
      }

      return level;
    }
    #endregion

    #region data

    private double[,] data ={{102.21,103.95,104.05,104.62,105.8,106.19,101.19}, 
 {133.78,140.55,140.37,149.43,148.78,141.66,138.4}, 
 {308.51,308.42,296.77,290.85,286.95,280.69,275.38}, 
 {93.57,95.65,97.82,95.69,95.63,92.34,87.62}, 
 {103.68,99.77,100.47,98.03,96.32,94.53,92.23}, 
 {550.7,593.78,621.67,608.57,574.17,534.03,482.8}, 
 {107.79,98.77,97.81,97.04,95.42,93.79,92.28}, 
 {62.1,63.89,65.63,67.93,70.26,72.33,75.02}, 
 {266.2,256.45,248.32,242.67,238.35,233.5,227.55}, 
 {160.88,157.14,155.26,151.66,149.04,147.3,144.48}, 
 {42.55,43.44,44.57,47.45,50.08,51.22,51.12}, 
 {161.68,158.24,155.55,153.76,152.14,151.03,148.65}, 
 {50.34,54.84,56.13,55.62,54.94,53.78,50.88}, 
 {81.67,85.57,86.53,87.02,86.81,85.12,80.26}, 
 {59.64,67.14,71.1,71.26,74.41,70.94,69.13}, 
 {87.74,92.32,94.11,95.45,96.93,96.28,96.36}, 
 {274.69,279.04,280.48,283.83,285.07,286.9,283.86}, 
 {135.52,174.52,184.84,183.29,180.79,167.19,158.69}, 
 {64.02,64.5,69.06,71.89,75.52,75.42,76.48}, 
 {174.68,184.02,189.86,193.78,197.69,201.16,203.82}, 
 {59.2386446,63.94675307,66.01571647,67,67.5058177,66.2826437666667,66.71153708}, 
 {60.11,62.41,63.62,64.23,64.79,63.6,60.17}, 
 {48.84,54.96,56.77,59.24,61.41,61.29,61.07}, 
 {35.25,36.86,39.88,42.59,45.13,46.69,47.9}, 
 {62.56,70.51,72.22,72.78,72.73,70.31,64.94}, 
 {37.27,39.52,40.31,40.06,39.77,37.99,36.23}, 
 {286.29,327.96,326.61,308.16,292.25,272.79,251.96}, 
 {102.56,106.89,108.07,108.76,109.45,108.96,108.64}, 
 {1276.48,1088.12,1003.47,906.72,853.57,788.68,724.81}, 
 {38.83,43.57,50.36,53.76,56.05,56.99,57.17}, 
 {111.38,109.46,107.11,105.38,109.19,109.48,110.2}, 
 {140.38,169.68,179.6,180.1,180.58,167.09,156.88}, 
 {105.05,85.12,91.15,96.07,97.15,93.81,94.35}, 
 {47.88,50.74,53.74,55.11,57.51,59.17,57.66}, 
 {60.68,62.47,66.48,67.76,70.38,69.01,70.75}, 
 {55.7,56.41,56.77,56.3,56.86,53.18,48.18}, 
 {805.96,825.88,804.39,780.09,750.25,699.38,647.83}, 
 {97.46,108.04,114.57,116,118.21,120.72,125.19}, 
 {150,154.77,162.83,160.66,155.45,146.15,136.49}, 
 {65.99,68.18,69.13,69.69,70.3,68.74,65.47}, 
 {80.43,89.91,92.32,92.48,93.2,89.62,90.68}, 
 {128.95,141.35,144.52,138.91,131.87,123.95,117.36}, 
 {71.09,79.06,88.91,89.83,91.88,88.89,85.96}, 
 {156.03,168.42,171.13,163.78,154.75,142.88,134.26}, 
 {73.62,76.46,78.08,78.53,78.52,78.22,79.06}, 
 {139.5,133.96,128.15,126.75,128.17,123.77,123.88}, 
 {40.4,42.34,45.46,47.13,49.63,47.93,51.75}, 
 {102.17,115.22,111.27,106.1,104.96,96.14,86.61}, 
 {63.6,68.6,68.16,68.95,68.8,67.51,66.66}, 
 {42.17,45.47,50.01,52.33,55.8,59.8,63.29}, 
 {213.49,229.87,234.82,227.47,220.15,211.03,202.99}, 
 {48.86,51.43,51.68,51.46,52.4,51.1,47.75}, 
 {356.82,365.83,371,357.26,343.56,313.32,299.67}, 
 {806.68,826.22,813.47,771.97,738.9,686.65,641.47}, 
 {87.55,108.97,118.41,125.43,127.08,125.09,125.43}, 
 {82.16,79.63,77.92,75.26,74.14,71.91,70.13}, 
 {179.92,175.83,172.28,170.63,169.9,167.96,165}, 
 {75.93,81.4,82.77,80.82,79.62,75.67,70.09}, 
 {108.61,117.36,123.4,119.83,118.11,112.83,109.22}, 
 {28.4,31.16,31.24,31.66,31.19,33.72,35.68}, 
 {73.8,76.04,76.52,76,75.85,72.88,69.94}, 
 {166.27,185.43,186.91,181.75,175.15,171.43,164.79}, 
 {54.96,58.94,63.51,67.37,70.12,72.99,75.6}, 
 {312.69,325.36,346.19,334.55,316.53,290.18,269.1}, 
 {74.78,75.66,78.73,79.41,80.95,77.3,73.2}, 
 {169.36,172.86,179.87,178.56,180.11,171.87,161.43}, 
 {214.99,225.06,224.31,224.88,226.07,221.6,219.41}, 
 {179.78,193.77,201.79,198.09,191.75,178.98,157.63}, 
 {191.82,194.91,205.39,210.63,213.68,209.61,207.26}, 
 {52.93,68.26,71.84,72.76,76.36,74.39,73.54}, 
 {77.86,87.07,85.41,84.88,86.64,85.32,84.33}, 
 {59.25,55.8,51.51,50.95,51.51,50.93,49.1}, 
 {55.67,57.54,60.22,61.7,64.6,62.88,64.82}, 
 {136.71,146.25,144.33,140.72,139.75,136.12,133.8}, 
 {141.23,140.25,146.54,154.77,157.21,148.64,146.47}, 
 {96.99,99.28,105.06,108.93,110.89,113.91,117.49}, 
 {308.47,328.86,324.8,314.14,306.34,278.86,256.95}, 
 {103.39,112.67,116.71,118.29,118.37,116.09,115.88}, 
 {96.56,101.49,105.89,109.67,112.89,114.78,118.98}, 
 {284.38,319.17,313.61,310.57,305.33,295.16,287.62}, 
 {58.87,60.41,64.79,66.72,68.49,71,70.53}, 
 {196.87,193.09,191.84,188.9,190,185.76,181.65}, 
 {57.16,58.58,61.63,63.84,65.7,68.96,72.18}, 
 {387.16,404.99,402.19,394.43,385.45,348.83,331.96}, 
 {144.17,155.78,158.24,153.41,151.9,146.19,133.82}, 
 {228.59,238.82,238.64,227.7,223.45,212.09,197.8}, 
 {18.83,23.09,24.36,25.87,26.77,27.21,27.7}, 
 {55.6,56.98,58.57,59.45,60.66,59.64,62.88}, 
 {63.09,66.71,67.36,69.28,71.53,72.13,72.88}, 
 {88.27,89.46,91.86,93.81,96.25,94.96,94.19}, 
 {60.01,63.55,64.71,65.77,67.04,64.32,60.94}, 
 {74.32,80.21,85.56,87.92,89.84,87.21,86.37}, 
 {118.24,124.6,128.31,125.97,122.2,112.39,102.95}, 
 {309.91,310.83,301.98,296.32,289.94,282.52,272.27}, 
 {126,125.16,128.76,124.67,124.63,120.74,118.02}, 
 {96.27,98.17,98.94,101.56,103.57,104.5,103.2}, 
 {159.72,176.68,185.68,181.74,180,164.72,156.06}, 
 {89.76,102.94,105.87,104.67,100.52,101.21,101.58}, 
 {89.6,97.98,105.64,105.37,109.38,105.76,105.19}, 
 {137.06,142.91,149.24,149.38,149.88,142.57,136.49}, 
 {98.37,105.99,111.94,113.76,115.83,118.36,121.03}, 
 {135.29,138.63,141.77,143.05,145.78,147.43,149}, 
 {192.41,202.99,211.92,216.54,222.23,226.41,229.62}, 
 {79.53,84.92,88.96,90.92,92.55,95.79,95.62}, 
 {433.78,471.77,490.23,478.13,468.82,443.64,420.75}, 
 {49.86,55.97,59.73,62.66,65.34,68.75,71.15}, 
 {96.45,99.51,98.26,96.85,95.14,89.09,85.58}, 
 {86.79,91.67,93.45,96.25,99.41,98.62,101.29}, 
 {145.92,142.21,139.84,136.41,135.66,135.46,135.06}, 
 {37.4,37.36,39.19,40.96,42.61,39.54,37.67}, 
 {193.9,208.69,216.98,208.7,201.77,192.3,184.72}, 
 {127.66,121.43,119.78,119.65,120.91,121.11,119.69}, 
 {55.84,57.52,59.3,60.2,60.68,60.21,59.74}, 
 {57.62,60.01,64.98,65.33,65.61,64.98,64}, 
 {128.89,134.46,138.61,137.17,135.72,134.01,132.83}, 
 {162.61,155.58,151.69,146.81,145.07,142.03,140.53}, 
 {124.38,135.07,142.55,139.3,133.42,122.33,115.22}, 
 {120.03,124.04,127.79,132.48,130.69,129.47,127.54}, 
 {259.43,272.44,265.01,257.42,250.55,226.71,207.28}, 
 {399.93,412.09,406.43,389.52,376.49,354.55,335.06}, 
 {88.79,93.9,95.06,97.15,100.02,102.58,105.4}, 
 {180.61,195.42,204.33,202.13,203.51,193.44,181.9}, 
 {49.8,54.45,58.88,58.99,60.28,63.41,65.6}, 
 {252.77,259.59,275.22,278.71,285.02,287.12,286.01}};
    #endregion

    #region Test
    [Test]
    public void EqualsNewIssueStrike()
    {
      int N = data.GetLength(0);
      Dt asOf = new Dt(29, 5, 2009);
      Dt settle = new Dt(30, 5, 2009);
      string[] tenorNames = { "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y" };
      Dt[] tenorDates = new Dt[tenorNames.Length];
      for (int i = 0; i < tenorDates.Length; i++)
        tenorDates[i] = Dt.Add(Dt.ImmNext(asOf), Tenor.Parse(tenorNames[i]));
      SurvivalCurve[] sc = new SurvivalCurve[N];
      DiscountCurve df = new DiscountCurve(asOf, 0.03);
      Copula copula = new Copula(CopulaType.Gauss);
      double[] s = new double[] { 0.05, 1.57, 2.10, 2.15, 2.3 };
      double[] c = new double[] { 0.0946, 0.35, 0.4739, 0.5852, 0.7575 };
      double[] not = new double[N];
      double[] quotes = new double[tenorDates.Length];
      double[] recoveries = new double[tenorDates.Length];
      string[] names = new string[N];
      for (int i = 0; i < N; i++)
      {
        not[i] = 1.0;
        for (int j = 0; j < quotes.Length; j++)
        {
          quotes[j] = data[i, j];
          recoveries[j] = 0.4;
        }
        SurvivalCurveParameters curveParams = new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly,
                                                                          BDConvention.Modified, Calendar.None,
                                                                          InterpMethod.Weighted,
                                                                          ExtrapMethod.Const,
                                                                          NegSPTreatment.Allow);
        sc[i] = SurvivalCurve.FitCDSQuotes("cds" + i, asOf, asOf, Currency.USD, "none", CDSQuoteType.ParSpread,
                                           100, curveParams, df, tenorNames, tenorDates, quotes, recoveries, 0,
                                           new Dt[] { }, null, 0, true);
        names[i] = sc[i].Name;
      }
      BaseCorrelation bc = new BaseCorrelation(BaseCorrelationMethod.ArbitrageFree,
                                         BaseCorrelationStrikeMethod.ProtectionPv, s, c);

      int[] defaulted = new int[] { 45, 65, 89 };
      int nDef = defaulted.Length;
      double attach = 0.05;
      double detach = 0.1;
      SyntheticCDO cdo = new SyntheticCDO(new Dt(1, 9, 2005), new Dt(20, 12, 2015), Currency.USD,
                                           DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
                                           Calendar.NYB, 0.05, 0, attach, detach);
      foreach (int idx in defaulted)
      {
        sc[idx].Defaulted = Defaulted.HasDefaulted;
      }
      SurvivalCurve[] scNew = new SurvivalCurve[N - nDef];
      double[] notNew = new double[N - nDef];
      int jj = 0;
      for (int i = 0; i < N; i++)
      {
        if (!(sc[i].Defaulted == Defaulted.HasDefaulted))
        {
          scNew[jj] = sc[i];
          notNew[jj] = 1.0;
          jj++;
        }
        else
          continue;
      }

      SyntheticCDOPricer pricer = BasketPricerFactory.CDOPricerSemiAnalytic(
          cdo, asOf, asOf, settle,
          df, sc, not, copula, bc,
          3, TimeUnit.Months, 50, 0, 1, true, false, null);
      BaseCorrelationBasketPricer bcbp = (BaseCorrelationBasketPricer)pricer.Basket;
      double pastLoss = pricer.Basket.AccumulatedLoss(asOf, 0, detach);
      double pastAmort = pricer.Basket.AmortizedAmount(asOf, 0, 1);
      double cookedAttach = AdjustTrancheLevel(false, attach, pastLoss, pastAmort);
      double cookedDetach = AdjustTrancheLevel(false, detach, pastLoss, pastAmort);
      SyntheticCDO cdo1 = new SyntheticCDO(new Dt(1, 9, 2005), new Dt(20, 12, 2015), Currency.USD,
                                           DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
                                           Calendar.NYB, 0.05, 0, cookedAttach, cookedDetach);
      SyntheticCDOPricer pricer1 = BasketPricerFactory.CDOPricerSemiAnalytic(
          cdo1, asOf, asOf, settle,
          df, scNew, notNew, copula, bc,
          3, TimeUnit.Months, 50, 0, 1, true, false, null);
      BaseCorrelationBasketPricer bcbp1 = (BaseCorrelationBasketPricer)pricer1.Basket;
      double strike, expectedstrike;

      bc.StrikeMethod = BaseCorrelationStrikeMethod.UnscaledForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.Unscaled;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("Unscaled", expectedstrike, strike, 1e-8);


      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLoss;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("ExpectedLoss", expectedstrike, strike, 1e-8);

      bc.StrikeMethod = BaseCorrelationStrikeMethod.ProtectionForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.Protection;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("Protection", expectedstrike, strike, 1e-8);

      bc.StrikeMethod = BaseCorrelationStrikeMethod.EquityProtectionForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.EquityProtection;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("EquityProtection", expectedstrike, strike, 1e-8);

      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossRatioForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossRatio;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("ExpectedLossRatio", expectedstrike, strike, 1e-8);
      //protection based methods

      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPvRatio;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPvRatio;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("ExpectedLossPvRatio", expectedstrike, strike, 1e-8);

      bc.StrikeMethod = BaseCorrelationStrikeMethod.ProtectionPvForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.ProtectionPv;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("ProtectionPv", strike, expectedstrike);

      bc.StrikeMethod = BaseCorrelationStrikeMethod.EquityProtectionPvForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.EquityProtectionPv;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("EquityProtectionPv", strike, expectedstrike, 1e-8);
      //
      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPVForward;
      bcbp.BaseCorrelation = bc;
      bcbp.Reset();
      strike = bcbp.DPStrike;
      bc.StrikeMethod = BaseCorrelationStrikeMethod.ExpectedLossPV;
      bcbp1.BaseCorrelation = bc;
      bcbp1.Reset();
      expectedstrike = bcbp1.DPStrike;
      //Console.WriteLine("{0}\t{1}", strike, expectedstrike);
      AssertEqual("ExpectedLossPv", strike, expectedstrike, 1e-8);
    }

    [Test]
    public void TestSensitivityNewStrikes()
    {
      double tol = 1e-3;
      int N = data.GetLength(0);
      Dt asOf = new Dt(29, 5, 2009);
      Dt settle = new Dt(30, 5, 2009);
      string[] tenorNames = { "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y" };
      Dt[] tenorDates = new Dt[tenorNames.Length];
      for (int i = 0; i < tenorDates.Length; i++)
        tenorDates[i] = Dt.Add(Dt.ImmNext(asOf), Tenor.Parse(tenorNames[i]));
      SurvivalCurve[] sc = new SurvivalCurve[N];
      DiscountCurve df = new DiscountCurve(asOf, 0.03);
      Copula copula = new Copula(CopulaType.Gauss);
      double[] s = new double[] { 0.05, 1.57, 2.10, 2.15, 2.3 };
      double[] c = new double[] { 0.0946, 0.35, 0.4739, 0.5852, 0.7575 };
      double[] not = new double[N];
      double[] quotes = new double[tenorDates.Length];
      double[] recoveries = new double[tenorDates.Length];
      string[] names = new string[N];
      for (int i = 0; i < N; i++)
      {
        not[i] = 1.0;
        for (int j = 0; j < quotes.Length; j++)
        {
          quotes[j] = data[i, j];
          recoveries[j] = 0.4;
        }
        sc[i] = SurvivalCurve.FitCDSQuotes("cds" + i, asOf, asOf, Currency.USD, "none",
                                           CDSQuoteType.ParSpread, 100,
                                           new SurvivalCurveParameters(DayCount.Actual360, Frequency.Quarterly,
                                                                       BDConvention.Modified, Calendar.None,
                                                                       InterpMethod.Weighted, ExtrapMethod.Const,
                                                                       NegSPTreatment.Allow), df,
                                           tenorNames, tenorDates, quotes, recoveries, 0, new Dt[] { }, null, 0,
                                           true);
        names[i] = sc[i].Name;
      }


      BaseCorrelation bc = new BaseCorrelation(BaseCorrelationMethod.ArbitrageFree,
                                               BaseCorrelationStrikeMethod.ProtectionPv, s, c);

      int[] defaulted = new int[] { 45, 65, 89 };
      int nDef = defaulted.Length;
      double attach = 0.0;
      double detach = 0.1;
      SyntheticCDO cdo = new SyntheticCDO(new Dt(1, 9, 2005), new Dt(20, 12, 2015), Currency.USD,
                                          DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
                                          Calendar.NYB, 0.05, 0, attach, detach);
      foreach (int el in defaulted)
      {
        sc[el].Defaulted = Defaulted.HasDefaulted;
      }
      BaseCorrelationStrikeMethod[] methods = new BaseCorrelationStrikeMethod[]
                                                    {
                                                      BaseCorrelationStrikeMethod.UnscaledForward,
                                                      BaseCorrelationStrikeMethod.ExpectedLossForward,
                                                      BaseCorrelationStrikeMethod.ExpectedLossPVForward,
                                                      BaseCorrelationStrikeMethod.ExpectedLossRatioForward,
                                                      BaseCorrelationStrikeMethod.ProtectionForward,
                                                      BaseCorrelationStrikeMethod.ProtectionPvForward,
                                                      BaseCorrelationStrikeMethod.ExpectedLossRatioForward,
                                                      BaseCorrelationStrikeMethod.EquityProtectionForward
                                                    };

      SyntheticCDOPricer pricer = BasketPricerFactory.CDOPricerSemiAnalytic(
        cdo, asOf, asOf, settle,
        df, sc, not, copula, bc,
        3, TimeUnit.Months, 50, 0, 1, true, false, null);
      BaseCorrelationBasketPricer bcbp = (BaseCorrelationBasketPricer)pricer.Basket;

      for (int i = 0; i < methods.Length; i++)
      {
        bc.StrikeMethod = methods[i];
        bcbp.BaseCorrelation = bc;
        string name = Enum.GetName(typeof(BaseCorrelationStrikeMethod), methods[i]);
        bcbp.Reset();
        int idx = 30;
        int ten = 0;
        int tenJ = 2;
        int n = bcbp.SurvivalCurves[idx].Tenors.Count;
        int nT = n + n * (n + 1) / 2 + 2;
        double[] retVal = new double[bcbp.SurvivalCurves.Length * nT];
        double h = 1e-4;
        bc.CorrelationDerivatives(bcbp.CreateDetachmentBasketPricer(true), pricer.DiscountCurve,
                                  cdo, retVal);
        double p = bcbp.DPCorrelation;
        double u = bcbp.SurvivalCurves[idx].GetVal(ten);
        bcbp.SurvivalCurves[idx].SetVal(ten, u + h);
        bcbp.Reset();
        double pp = bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(ten, u - h);
        bcbp.Reset();
        double pm = bcbp.DPCorrelation;
        AssertEqual(name, (pp - pm) / (2 * h), retVal[idx * nT + ten], tol);
        AssertEqual(name, (pp - 2 * p + pm) / (h * h), retVal[idx * nT + n + ten * (ten + 1) / 2 + ten], tol);
        double v = bcbp.SurvivalCurves[idx].GetVal(tenJ);
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v - h);
        bcbp.Reset();
        double pmm = bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v + h);
        bcbp.Reset();
        double pmp = bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(ten, u + h);
        bcbp.Reset();
        double ppp = bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v - h);
        bcbp.Reset();
        double ppm = bcbp.DPCorrelation;
        int min = Math.Min(ten, tenJ);
        int max = Math.Max(ten, tenJ);
        AssertEqual(name, 0.25 * (ppp - ppm - pmp + pmm) / (h * h), retVal[idx * nT + n + max * (max + 1) / 2 + min], tol);
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v);
        bcbp.SurvivalCurves[idx].SetVal(ten, u);
      }
    }

    #endregion
  }
}
