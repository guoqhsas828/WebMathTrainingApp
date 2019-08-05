//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Numerics;
using System.Collections.Generic;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test Bond Calculations.
  /// </summary>
  [TestFixture, Smoke]
  public class TestConvertibleBond : ToolkitTestBase
  {
    #region SetUP and Clean
    [OneTimeSetUp]
    public void Initialize()
    {
      BuildIrCurve();
    }
    #endregion SetUp and Clean

    #region tests

    [Test, Smoke]
    public void TestStockTreeMartingale()
    {
      // This test the expected stock price equals S0: E[S(t)*B(t)] = S0
      const double kappaR = 0.1;
      const double sigmaR = 0.1;
      const double rho = 0;
      const int n = 100;
      const double S0 = 50;
      const double sigmaS = 0.2;

      if(discountCurve_ == null)
        BuildIrCurve();
      
      // Build Black-Karasinski rate binomial tree 
      var rateModel = new BlackKarasinskiBinomialTreeModel(kappaR, sigmaR, asOf_, maturity_, n, discountCurve_);
      List<double[]> rateTree = rateModel.RateTree;

      // Build correlated stock price tree given rate tree 
      var stockModel = 
        new ConvertibleBondIntersectingTreesModel.StockCorrelatedModel(
          S0, sigmaS, new ConvertibleBondIntersectingTreesModel.StockCorrelatedModel.StockDividends(null, new double[]{0}), 
          asOf_, maturity_, n, rho);
      stockModel.BuildStockTree(rateModel);

      double dt = rateModel.DeltaT;
      for (int t = 1; t <= n; t++)
      {
        var expectedDiscountedPrice = stockModel.GetStockPrices(t);
        for (int k = t - 1; k >= 0; k--)
        {
          for (int i = 0; i <= k; i++)
          {
            for (int j = 0; j <= k; j++)
            {
              double a = expectedDiscountedPrice[i, j]*Math.Exp(-dt*(rateTree[k][i]));
              double b = expectedDiscountedPrice[i, j + 1]*Math.Exp(-dt*(rateTree[k][i]));
              double c = expectedDiscountedPrice[i + 1, j]*Math.Exp(-dt*(rateTree[k][i]));
              double d = expectedDiscountedPrice[i + 1, j + 1]*Math.Exp(-dt*(rateTree[k][i]));
              expectedDiscountedPrice[i, j] = 0.25*(a + b + c + d);
            }
          }
        }
        Assert.AreEqual(0, Math.Abs(expectedDiscountedPrice[0, 0] - S0)/S0, 5e-4,
                        "Expected stock price violated Martingale");
      }
      return;
    }

    [Test, Smoke]
    public void TestEuropeanOption()
    {
      // This test that the European stock option prices computed
      // using binomial stock tree match the Black Sholes formula
      const double sigma = 0.2;
      const double divYield = 0.0;
      const double rate = 0.1;
      var initS0 = new double[] {45};
      var initK = new double[] {20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70};
      var Num = new int[] {40, 60, 80, 100};

      // Test call options (True means call)
      double[,,] options = ComputeEuropeanOptions(initS0, initK, Num, sigma, divYield, rate, true);
      double T = maturity_.ToDouble() - asOf_.ToDouble();
      double[,] blackScholesOptions = BlackSholesOptions(initS0, initK, sigma, rate, divYield, T, true);      
      // Compare the results
      for(int i = 0; i < initS0.Length; i++)
      {
        for(int j = 0; j < initK.Length; j++)
        {
          for(int k = 0; k < Num.Length; k++)
          {
            double diff = Math.Abs(blackScholesOptions[i, j] - options[i, j, k])/blackScholesOptions[i, j];
            string str = "S0=" + initS0[0].ToString() + " K=" + initK[j].ToString() + " N=" + Num[k].ToString();
            Assert.AreEqual(0, diff, 1.5e-2, "binomial call does not pass for " + str + " Diff=" + diff.ToString());
          }
        }
      }

      // Test put options (false means put)
      initK = new double[] {45, 50, 55, 60, 65, 70, 75, 80, 85, 90 };
      options = ComputeEuropeanOptions(initS0, initK, Num, sigma, divYield, rate, false);
      T = maturity_.ToDouble() - asOf_.ToDouble();
      blackScholesOptions = BlackSholesOptions(initS0, initK, sigma, rate, divYield, T, false);
      // Compare the results
      for (int i = 0; i < initS0.Length; i++)
      {
        for (int j = 0; j < initK.Length; j++)
        {
          for (int k = 0; k < Num.Length; k++)
          {
            double diff = Math.Abs(blackScholesOptions[i, j] - options[i, j, k]) / blackScholesOptions[i, j];
            string str = "S0=" + initS0[0].ToString() + " K=" + initK[j].ToString() + " N=" + Num[k].ToString();
            Assert.AreEqual(0, diff, 1.5e-2, "binomial put does not pass for "+str+" Diff="+diff.ToString());
          }
        }
      }
      return;
    }

    [Test, Smoke]
    public void TestBlackKarasinskiTree()
    {
      // [1]. Test flat tree when volatility is 0 and no mean reverting
      BuildIrCurve();
      double sigma = 0.0;
      double kappa = 0.0;
      var model = new BlackKarasinskiBinomialTreeModel(kappa, sigma, asOf_, maturity_, 20, discountCurve_);

      double[,] rateBounds = model.RatesBounds;
      double[] initForwardrates = model.ForwardRates;
      double maxDiff = 1e5;
      for(int i = 0; i < initForwardrates.Length; i++)
      {
        double a = Math.Abs(rateBounds[0, i] - initForwardrates[i]) / initForwardrates[i];
        if (a < maxDiff)
          maxDiff = a;
        a = Math.Abs(rateBounds[1, i] - initForwardrates[i]) / initForwardrates[i];
        maxDiff = a;
      }
      Assert.AreEqual(0, maxDiff, 5e-3, "rate tree is not flat for 0 vol and 0 mean reversion");

      // [2] This test verifies the upper and lower bound a binomial tree for
      //     Black Karasinski rate mode sandwiches the initial forward rates
      sigma = 0.1;
      kappa = 0.1;
      model = new BlackKarasinskiBinomialTreeModel(kappa, sigma, asOf_, maturity_, 20, discountCurve_);
      rateBounds = model.RatesBounds;
      initForwardrates = model.ForwardRates;
      bool sandwich = true;
      for(int i = 0; i < initForwardrates.Length; i++)
      {
        sandwich = sandwich &&
                   ((Math.Round(rateBounds[0, i], 8) <= Math.Round(initForwardrates[i], 8))) &&
                   ((Math.Round(rateBounds[1, i], 8) >= Math.Round(initForwardrates[i], 8)));        
      }
      Assert.IsTrue(sandwich, "Binomial rate tree does not sandwich initial term structure");
      return;
    }

    [Test, Smoke]
    public void TestZeroCouponBond()
    {
      if(discountCurve_ == null)
        BuildIrCurve();

      const double kappa = 0.1;
      const double sigma = 0.1;
      const int n = 20;
      var model = new BlackKarasinskiBinomialTreeModel(kappa, sigma, asOf_, maturity_, n, discountCurve_);
      var rateTree = model.RateTree;
      var zeroCouponBonds = new double[n];      
      double dt = (maturity_.ToDouble() - asOf_.ToDouble())/n;
      zeroCouponBonds[0] = Math.Exp(-dt*rateTree[0][0]);
      var Q = new List<double[]>();
      for (int i = 0; i < n; i++)
      {
        Q.Add(Array.ConvertAll<double, double>(rateTree[i], x => Math.Exp(-dt*x)));
      }
      for (int k = 1; k < n; k++)
      {
        var inter = (double[])Q[k].Clone(); // there are k+1 elements
        for (int i = k - 1; i >= 0; i--)
        {
          for(int j = 0; j <= i; j++)
          {
            inter[j] = (inter[j] + inter[j+1])*Q[i][j]*0.5;            
          }
        }
        zeroCouponBonds[k] = inter[0];
      }
      
      // [1] Test decreasing monotonicity of zero-coupon bond
      bool decrease = true;
      for(int i = 1; i < zeroCouponBonds.Length; i++)
      {
        decrease = decrease && (zeroCouponBonds[i - 1] >= zeroCouponBonds[i]);        
      }
      Assert.IsTrue(decrease, "Zero coupon bond does not monotonically decrease");

      // [2] Test zero coupon bond tie out the discount factor from discount curve
      var discountFactor = new double[zeroCouponBonds.Length];
      double start = asOf_.ToDouble();
      for(int i = 0; i < zeroCouponBonds.Length; i++)
      {
        discountFactor[i] = discountCurve_ == null
                              ? 1
                              : discountCurve_.DiscountFactor(asOf_, new Dt(start + (i + 1)*dt));
        Assert.AreEqual(0, Math.Abs(discountFactor[i] - zeroCouponBonds[i])/discountFactor[i], 1e-4,
                        "Cannot tie out zero coupon bond for time " + (new Dt(start + (i + 1)*dt)));
      }
    }

    [Test, Smoke]
    public void TestCallableTieOut()
    {
      if(discountCurve_ == null)
        BuildIrCurve();
      if(creditCurve_ == null)
        BuildCreditCurve();

      // [1] Test no call, no put, no conversion
      var callStart = new Dt[] {new Dt(15, 1, 2008), new Dt(15, 1, 2009)};
      var callEnd = new Dt[] {new Dt(14, 1, 2009), new Dt(15, 1, 2013)};
      var callPrices = new double[] { 1000, 1000 };
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      const double s0 = 1, sigmaS = 0, yield = 0, convRatio = 1, rho = 0, kappa = 0.1, sigmaR = 0.1;
      const int n = 100;

      // Build callable bond and pricer
      var callableBond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices,
                                              putStart, putEnd, putPrices, effectiveDate_, maturity_, true);
      var callablePricer = BuildConvertibleBondPricer(callableBond, s0, sigmaS, yield, rho,
                                                      kappa, sigmaR, n, Double.NaN, true);

      // Build convertible bond and pricer (without conversion by setting S0=1)
      var convertilbeBond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices,
                                              putStart, putEnd, putPrices, effectiveDate_, maturity_, false);
      var convertiblePricer = BuildConvertibleBondPricer(convertilbeBond, s0, sigmaS, yield, rho,
                                                      kappa, sigmaR, n, Double.NaN, false);
      double pvCallable = callablePricer.FullModelPrice();
      double pvConvertible = convertiblePricer.FullModelPrice();

      Assert.AreEqual(0, Math.Abs(pvCallable - pvConvertible)/pvCallable, 1e-3,
                      "No-Call, No-Put, No-Conversion does not pass");

      // [2] Test a call schedule with reasonable call price
      callPrices = new double[]{1000, 108};

      callableBond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices,
                                              putStart, putEnd, putPrices, effectiveDate_, maturity_, true);
      callablePricer = BuildConvertibleBondPricer(callableBond, s0, sigmaS, yield, rho,
                                                      kappa, sigmaR, n, Double.NaN, true);
      convertilbeBond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices,
                                              putStart, putEnd, putPrices, effectiveDate_, maturity_, false);
      convertiblePricer = BuildConvertibleBondPricer(convertilbeBond, s0, sigmaS, yield, rho,
                                                      kappa, sigmaR, n, Double.NaN, false);
      pvCallable = callablePricer.FullModelPrice();
      pvConvertible = convertiblePricer.FullModelPrice();

      Assert.AreEqual(0, Math.Abs(pvCallable - pvConvertible) / pvCallable, 1e-3,
                      "Call, No-Put, No-Conversion does not pass");

      // [3] Test another call schedule with reasonable call price
      callPrices = new double[] { 1000, 102 };
      callableBond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices,
                                              putStart, putEnd, putPrices, effectiveDate_, maturity_, true);
      callablePricer = BuildConvertibleBondPricer(callableBond, s0, sigmaS, yield, rho,
                                                      kappa, sigmaR, n, Double.NaN, true);
      convertilbeBond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices,
                                              putStart, putEnd, putPrices, effectiveDate_, maturity_, false);
      convertiblePricer = BuildConvertibleBondPricer(convertilbeBond, s0, sigmaS, yield, rho,
                                                      kappa, sigmaR, n, Double.NaN, false);
      pvCallable = callablePricer.FullModelPrice();
      pvConvertible = convertiblePricer.FullModelPrice();

      Assert.AreEqual(0, Math.Abs(pvCallable - pvConvertible) / pvCallable, 1e-3,
                      "Call, No-Put, No-Conversion does not pass");
      return;
    }                
    
    [Test, Smoke]
    public void TestVeryHighConversion()
    {
      // This test when stock price is very high, the price should be close to parity
      if (discountCurve_ == null)
        BuildIrCurve();
      if (creditCurve_ == null)
        BuildCreditCurve();

      Dt[] callStart = null;
      Dt[] callEnd = null;
      double[] callPrices = null;
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      const double s0 = 200,
                   sigmaS = 0.2,
                   yield = 0,
                   convRatio = 20,
                   rho = 0,
                   kappa = 0.1,
                   sigmaR = 0.1;
      const int n = 100;

      var convertibleBond = BuildConvertibleBond(
        convRatio, callStart, callEnd, callPrices, putStart, putEnd, putPrices, effectiveDate_, maturity_, false);

      var convertiblePricer = BuildConvertibleBondPricer(
        convertibleBond, s0, sigmaS, yield, rho, kappa, sigmaR, n, Double.NaN, false);

      double pvConvertible = convertiblePricer.FullModelPrice();
      double parity = convertiblePricer.Parity() / 100;

      Assert.AreEqual(0, Math.Abs(parity - pvConvertible)/parity, 0.01,
                      "Conversion at very high stock does not pass");
    }

    [Test, Smoke]
    public void TestConvertibleTrend()
    {
      // This test convertible bond prices should increase with initial stock price
      if (discountCurve_ == null)
        BuildIrCurve();
      if (creditCurve_ == null)
        BuildCreditCurve();

      Dt[] callStart = null;
      Dt[] callEnd = null;
      double[] callPrices = null;
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      const double sigmaS = 0.2, yield = 0, convRatio = 20, rho = 0, kappa = 0.1, sigmaR = 0.1;
      const int n = 100;
      
      var prices = new List<double>();
      bool aboveBondFloor = true;
      for (int s0 = 4; s0 <= 140; s0 += 4)
      {
        var bond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices, putStart,
                                        putEnd, putPrices, effectiveDate_, maturity_, false);
        var convertiblePricer = BuildConvertibleBondPricer(bond, s0, sigmaS, yield, rho, kappa, sigmaR, n, Double.NaN, false);
        double p = convertiblePricer.FullModelPrice();
        prices.Add(p);
        aboveBondFloor = aboveBondFloor && (convertiblePricer.BondFloor()/100 <= p);
      }
      bool increase = true;
      for (int i = 1; i < prices.Count; i++)
      {
        increase = increase && (prices[i] >= prices[i - 1]);        
      }
      Assert.IsTrue(increase, "Pure conversion increasing monotonicity does not hold");
      Assert.IsTrue(aboveBondFloor, "Pure conversion being above bond floor does not hold");
      return;
    }

    [Test, Smoke]
    public void TestHedgeRatio()
    {
      // This tests round trip of hedge ratio
      if (discountCurve_ == null)
        BuildIrCurve();
      if (creditCurve_ == null)
        BuildCreditCurve();

      Dt[] callStart = null;
      Dt[] callEnd = null;
      double[] callPrices = null;
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      const double sigmaS = 0.2, yield = 0, convRatio = 20, rho = 0, kappa = 0.1, sigmaR = 0.1;
      const int n = 100;      
      var ratios = new List<double>();

      int i = -1;
      for (int s0 = 45; s0 <= 100; s0 += 5)
      {
        i++;
        var bond = BuildConvertibleBond(convRatio, callStart, callEnd, callPrices, 
          putStart, putEnd, putPrices, effectiveDate_, maturity_, false);
        var convertiblePricer = BuildConvertibleBondPricer(bond, s0, sigmaS, yield, rho, kappa, sigmaR, n, Double.NaN, false);
        ratios.Add(convertiblePricer.HedgeRatio());

        convertiblePricer = BuildConvertibleBondPricer(bond, s0*1.05, sigmaS, yield, rho, kappa, sigmaR, n, Double.NaN, false);
        double pUp = convertiblePricer.FullModelPrice()*1000;

        convertiblePricer = BuildConvertibleBondPricer(bond, s0 * 0.95, sigmaS, yield, rho, kappa, sigmaR, n, Double.NaN, false);
        double pDown = convertiblePricer.FullModelPrice()*1000;

        double hedgeRatio = (pUp - pDown)/(0.1*s0);
        Assert.AreEqual(0, Math.Abs(hedgeRatio-ratios[i])/hedgeRatio, 1e-4, "hedge ratio does not pass manual calculation");
      }
      return;
    }
    /*
    [Test, Smoke]
    public void TestZspread()
    {
      // This tests round trip of zspread
      if (discountCurve_ == null)
        BuildIrCurve();
      if (creditCurve_ == null)
        BuildCreditCurve();

      Dt[] callStart = null;
      Dt[] callEnd = null;
      double[] callPrices = null;
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      double sigmaS = 0.2, yield = 0, convRatio = 20, rho = 0, kappa = 0.1, sigmaR = 0.1, spread = 0;
      int n = 100;
      ConvertibleBondParams param = null;
      List<double> ratios = new List<double>();

      double S0 = 45;
      param = BuildConvertibleParams(
        n, kappa, sigmaR, S0, sigmaS, yield, convRatio, rho, callStart,
        callEnd, callPrices, putStart, putEnd, putPrices, spread);

      Bond convertilbeBond = BuildBond(param, false);
      BondPricer convertiblePricer = BuildBondPricer(convertilbeBond, Double.NaN);
      convertiblePricer.MarketQuote = 1.05;
      convertiblePricer.QuotingConvention = QuotingConvention.FlatPrice;
      double fullPrice = convertiblePricer.FullPrice();

      // Compute the zpread
      double zspread = convertiblePricer.ImpliedZSpread(false);

      // Build the new discoutn curve with zspread
      BuildIrCurve(zspread);
      BuildCreditCurve(Array.ConvertAll<double, double>(premiums_, delegate(double p) { return 0.1; }));
      convertiblePricer = BuildBondPricer(convertilbeBond, Double.NaN);
      discountCurve_ = null;
      creditCurve_ = null;

      // tie the full price
      double pv = convertiblePricer.Pv();

      Assert.AreEqual("Test zspread", 0, Math.Abs(pv-fullPrice)/fullPrice, 1e-3, "Fail the zspread tie out");
      return;
    }

    [Test, Smoke]
    public void TestRspread()
    {
      // This tests round trip of rspread
      if (discountCurve_ == null)
        BuildIrCurve();
      if (creditCurve_ == null)
        BuildCreditCurve();

      Dt[] callStart = null;
      Dt[] callEnd = null;
      double[] callPrices = null;
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      double sigmaS = 0.2, yield = 0, convRatio = 20, rho = 0, kappa = 0.1, sigmaR = 0.1, spread = 0;
      int n = 100;
      ConvertibleBondParams param = null;
      List<double> ratios = new List<double>();

      double S0 = 45;
      param = BuildConvertibleParams(
        n, kappa, sigmaR, S0, sigmaS, yield, convRatio, rho, callStart,
        callEnd, callPrices, putStart, putEnd, putPrices, spread);

      Bond convertilbeBond = BuildBond(param, false);
      BondPricer convertiblePricer = BuildBondPricer(convertilbeBond, Double.NaN);
      convertiblePricer.MarketQuote = 1.05;
      convertiblePricer.QuotingConvention = QuotingConvention.FlatPrice;
      double fullPrice = convertiblePricer.FullPrice();

      // Compute the rpread
      double rspread = convertiblePricer.CalcRSpread(false);

      // Build the new discoutn curve with zspread
      BuildIrCurve(rspread);      
      convertiblePricer = BuildBondPricer(convertilbeBond, Double.NaN);
      discountCurve_ = null;
      creditCurve_ = null;

      // tie the full price
      double pv = convertiblePricer.Pv();

      Assert.AreEqual("Test rspread", 0, Math.Abs(pv - fullPrice) / fullPrice, 1e-3, "Fail the rspread tie out");
      return;
    }

    [Test, Smoke]
    public void TesZsspreadTieOutCallable()
    {
      if (discountCurve_ == null)
        BuildIrCurve();
      if (creditCurve_ == null)
        BuildCreditCurve();

      // [1] Test no call, no put, no conversion
      Dt[] callStart = new Dt[] { new Dt(15, 1, 2008), new Dt(15, 1, 2009) };
      Dt[] callEnd = new Dt[] { new Dt(14, 1, 2009), new Dt(15, 1, 2013) };
      double[] callPrices = new double[] { 1000, 1000 };
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      double S0 = 1, sigmaS = 0, yield = 0, convRatio = 1, rho = 0, kappa = 0.1, sigmaR = 0.1, spread = 0;
      int n = 100;

      ConvertibleBondParams param = BuildConvertibleParams(
        n, kappa, sigmaR, S0, sigmaS, yield, convRatio, rho, callStart, callEnd, callPrices, putStart, putEnd, putPrices, spread);

      // Build callable bond and pricer
      Bond callableBond = BuildBond(param, true);
      BondPricer callablePricer = BuildBondPricer(callableBond, Double.NaN);
      // Build convertible bond and pricer (without conversion by setting S0=1)
      Bond convertilbeBond = BuildBond(param, false);
      BondPricer convertiblePricer = BuildBondPricer(convertilbeBond, Double.NaN);
      double market = callablePricer.Pv() - 0.02;
      callablePricer.MarketQuote = market;
      callablePricer.QuotingConvention = QuotingConvention.FlatPrice;
      convertiblePricer.MarketQuote = market;
      convertiblePricer.QuotingConvention = QuotingConvention.FlatPrice;
      double zspread_callable = callablePricer.ImpliedZSpread();
      double zspread_convertible = convertiblePricer.ImpliedZSpread(false);

      Assert.AreEqual(0, Math.Abs(zspread_callable - zspread_convertible) / zspread_callable, 2e-3,
                      "No-Call, No-Put, No-Conversion does not pass");

      return;
    }                

    [Test, Smoke]
    public void Mergetest()
    {
      // This get some number for test after merging
      if(discountCurve_ == null)
        BuildIrCurve();
      if(creditCurve_ == null)
        BuildCreditCurve();
      Dt[] callStart = null;
      Dt[] callEnd = null;
      double[] callPrices = null;
      Dt[] putStart = null;
      Dt[] putEnd = null;
      double[] putPrices = null;
      double S0 = 50, sigmaS = 0.2, yield = 0, convRatio = 20, rho = 0, kappa = 0.1, sigmaR = 0.1, spread = 0;
      int n = 100;
      ConvertibleBondParams param = null;
      param = BuildConvertibleParams(
        n, kappa, sigmaR, S0, sigmaS, yield, convRatio, rho, callStart,
        callEnd, callPrices, putStart, putEnd, putPrices, spread);
      Bond bond = BuildBond(param, false);
      BondPricer pricer = BuildBondPricer(bond, 119);

      double pv_0 = 1.26074328434001;
      double hedgeRatio_0 = 9.80882720867885;
      double delta_0 = 4.90441360433942;
      double gamma_0 = -0.330907272513286;
      double parity_0 = 100;
      double premium_0 = 26.0743284340016;

      double pv = pricer.FullModelPrice();
      double hedgeRatio = pricer.HedgeRatio();
      double delta = pricer.ConvertibleBondDelta();
      double gamma = pricer.ConvertibleBondGamma();
      double parity = pricer.Parity();
      double premium = pricer.ConvertibleBondPremium();            

      Assert.AreEqual(pv_0, pv, 1e-5, "pv not equal after merge");
      Assert.AreEqual(hedgeRatio_0, hedgeRatio, 1e-5, "Hedge ratio not equal after merge");
      Assert.AreEqual(delta_0, delta, 1e-5, "Delta not equal after merge");
      Assert.AreEqual(gamma_0, gamma, 1e-5, "Gamma not equal after merge");
      Assert.AreEqual(parity_0, parity, 1e-5, "Parity not equal after merge");
      Assert.AreEqual(premium_0, premium, 1e-5, "Premium not equal after merge");

      callStart = new Dt[] { new Dt(15, 1, 2008), new Dt(15, 1, 2009), new Dt(15, 1, 2011) };
      callEnd = new Dt[] { new Dt(14, 1, 2009), new Dt(14, 1, 2011), new Dt(15, 1, 2013) };
      callPrices = new double[] { 1000, 105, 102};
      param.CallStartDates = callStart;
      param.CallEndDates = callEnd;
      param.CallPrices = callPrices;
      bond = BuildBond(param, false);
      pricer = BuildBondPricer(bond, 119);
      pv = pricer.FullModelPrice();
      hedgeRatio = pricer.HedgeRatio();
      delta = pricer.ConvertibleBondDelta();
      gamma = pricer.ConvertibleBondGamma();
      parity = pricer.Parity();
      premium = pricer.ConvertibleBondPremium();
      pv_0 = 1.08516574585635;
      hedgeRatio_0 = 0;
      delta_0 = 0;
      gamma_0 = 0;
      parity_0 = 100.0;
      premium_0 = 8.51657458563536;
      Assert.AreEqual(pv_0, pv, 1e-5, "pv not equal after merge");
      Assert.AreEqual(hedgeRatio_0, hedgeRatio, 1e-5, "Hedge ratio not equal after merge");
      Assert.AreEqual(delta_0, delta, 1e-5, "Delta not equal after merge");
      Assert.AreEqual(gamma_0, gamma, 1e-5, "Gamma not equal after merge");
      Assert.AreEqual(parity_0, parity, 1e-5, "Parity not equal after merge");
      Assert.AreEqual(premium_0, premium, 1e-5, "Premium not equal after merge");

      return;
    }
    */
    #endregion tests

    #region helpers

    // Build the discount curve
    private void BuildIrCurve()
    {
      const DayCount mmDayCount = DayCount.Actual360;
      const DayCount swapDayCount = DayCount.Thirty360;
      const Frequency swapFreq = Frequency.SemiAnnual;
      const InterpMethod interpMethod = InterpMethod.Weighted;
      const ExtrapMethod extrapMethod = ExtrapMethod.Const;
      const InterpMethod swapInterp = InterpMethod.Cubic;
      const ExtrapMethod swapExtrap = ExtrapMethod.Const;
      var calibrator = new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = FuturesCAMethod.Hull;
      discountCurve_ = new DiscountCurve(calibrator);
      discountCurve_.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      discountCurve_.Ccy = Currency.USD;
      discountCurve_.Category = "None";
      for (int i = 0; i < mmTenorDates_.Length; i++)
        discountCurve_.AddMoneyMarket(mmTenors_[i], mmTenorDates_[i], mmRates_[i], mmDayCount);
      for (int i = 0; i < swapTenors_.Length; i++)
        discountCurve_.AddSwap(swapTenors_[i], swapTenorDates_[i], swapRates_[i], swapDayCount,
                        swapFreq, BDConvention.None, Calendar.None);
      discountCurve_.Fit();
      return;
    }

    // Build the discount curve with spread
    private void BuildIrCurve(double spread)
    {
      const DayCount mmDayCount = DayCount.Actual360;
      const DayCount swapDayCount = DayCount.Thirty360;
      const Frequency swapFreq = Frequency.SemiAnnual;
      const InterpMethod interpMethod = InterpMethod.Weighted;
      const ExtrapMethod extrapMethod = ExtrapMethod.Const;
      const InterpMethod swapInterp = InterpMethod.Cubic;
      const ExtrapMethod swapExtrap = ExtrapMethod.Const;
      var calibrator = new DiscountBootstrapCalibrator(asOf_, asOf_);
      calibrator.SwapInterp = BaseEntity.Toolkit.Numerics.InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = FuturesCAMethod.Hull;
      discountCurve_ = new DiscountCurve(calibrator);
      discountCurve_.Interp = BaseEntity.Toolkit.Numerics.InterpFactory.FromMethod(interpMethod, extrapMethod);
      discountCurve_.Ccy = Currency.USD;
      discountCurve_.Category = "None";
      for (int i = 0; i < mmTenorDates_.Length; i++)
        discountCurve_.AddMoneyMarket(mmTenors_[i], mmTenorDates_[i], mmRates_[i]+spread, mmDayCount);
      for (int i = 0; i < swapTenors_.Length; i++)
        discountCurve_.AddSwap(swapTenors_[i], swapTenorDates_[i], swapRates_[i]+spread, swapDayCount,
                        swapFreq, BDConvention.None, Calendar.None);
      discountCurve_.Fit();
      return;
    }

    // Build the survival curve
    private void BuildCreditCurve()
    {
      if(discountCurve_ == null)
        BuildIrCurve();
      Currency ccy = Currency.USD;
      string category = "";
      DayCount dayCount = DayCount.Actual360;
      Frequency freq = Frequency.Quarterly;
      BDConvention roll = BDConvention.Following;
      Calendar cal = Calendar.NYB;
      InterpMethod interpMethod = InterpMethod.Weighted;
      ExtrapMethod extrapMethod = ExtrapMethod.Const;
      NegSPTreatment nspTreatment = NegSPTreatment.Allow;
      tenorDates = Array.ConvertAll<string, Dt>(tenorNames, x => Dt.Roll(Dt.Add(asOf_, x), roll, cal));
      creditCurve_ = SurvivalCurve.FitCDSQuotes(asOf_, ccy, category, dayCount, freq , roll , cal,
        interpMethod, extrapMethod, nspTreatment, discountCurve_, tenorNames, tenorDates, null, premiums_,
        recoveries, 0, true, null);
      creditCurve_.Name = "CreditCurve";
      return;   
    }

    // Build the survival curve
    private void BuildCreditCurve(double[] premiums)
    {
      if (discountCurve_ == null)
        BuildIrCurve();
      Currency ccy = Currency.USD;
      string category = "";
      DayCount dayCount = DayCount.Actual360;
      Frequency freq = Frequency.Quarterly;
      BDConvention roll = BDConvention.Following;
      Calendar cal = Calendar.NYB;
      InterpMethod interpMethod = InterpMethod.Weighted;
      ExtrapMethod extrapMethod = ExtrapMethod.Const;
      NegSPTreatment nspTreatment = NegSPTreatment.Allow;
      tenorDates = Array.ConvertAll<string, Dt>(tenorNames, x => Dt.Roll(Dt.Add(asOf_, x), roll, cal));
      creditCurve_ = SurvivalCurve.FitCDSQuotes(asOf_, ccy, category, dayCount, freq, roll, cal,
        interpMethod, extrapMethod, nspTreatment, discountCurve_, tenorNames, tenorDates, null, premiums,
        recoveries, 0, true, null);
      creditCurve_.Name = "CreditCurve";
      return;
    }

    /// <summary>
    ///  Build a convertible bond product
    /// </summary>
    /// <param name="ratio">Conversion ratio</param>
    /// <param name="callStart">Call start dates</param>
    /// <param name="callEnd">Call end dates</param>
    /// <param name="callPrices">Clean call prices</param>
    /// <param name="putStart">Put start dates</param>
    /// <param name="putEnd">Put end dates</param>
    /// <param name="putPrices">Clean put prices</param>
    /// <param name="convStart">Conversion start date</param>
    /// <param name="convEnd">Conversion end date</param>
    /// <param name="buildCallable">True to build callable bond</param>
    /// <returns>Convertible bond</returns>
    private Bond BuildConvertibleBond(      
      double ratio, Dt[] callStart, Dt[] callEnd, double[] callPrices, 
      Dt[] putStart, Dt[] putEnd, double[] putPrices, Dt convStart, Dt convEnd, bool buildCallable)
    {
      if(! buildCallable)
        if (ratio <= 0)
          throw new ArgumentException("Conversion ratio must be positive.");

      var p = new Bond(effectiveDate_, maturity_, Currency.USD, BondType.USCorp, couponRate_,
                           bondDayCount_, CycleRule.None, bondFreq_, bondRoll_, bondCal_);
      //p.FirstCoupon = Schedule.DefaultFirstCouponDate(p.Effective, p.Freq, p.Maturity, false);

      if (callStart != null && callStart.Length > 0 && callEnd != null && callEnd.Length > 0)
        for (int j = 0; j < callStart.Length; j++)
          if (callPrices[j] > 0.0)
            p.CallSchedule.Add(new CallPeriod(callStart[j], callEnd[j], callPrices[j] / 100.0, 1000.0, OptionStyle.American, 0));
      if (putStart != null && putStart.Length > 0 && putEnd != null && putEnd.Length > 0)
        for (int j = 0; j < putStart.Length; j++)
          if (putPrices[j] > 0.0)
            p.PutSchedule.Add(new PutPeriod(putStart[j], putEnd[j], putPrices[j] / 100.0, OptionStyle.American));

      if (!buildCallable)
      {
        p.ConvertRatio = ratio;
        p.ParAmount = 1000.0;
        p.ConvertStartDate = convStart;
        p.ConvertEndDate = convEnd;
        }

      p.Validate();
      return p;
      }

    /// <summary>
    ///  Build convertible bond pricer
    /// </summary>
    /// <param name="bond">Bond</param>
    /// <param name="s0">Current stock price</param>
    /// <param name="sVol">Stock volatility</param>
    /// <param name="div">Continuous dividend yield</param>
    /// <param name="rho">Correlation between stock and rate</param>
    /// <param name="kappa">Mean reversion speed for rate</param>
    /// <param name="rVol">Short rate volatility</param>
    /// <param name="n">Tree steps</param>
    /// <param name="marketQuote">Market quote</param>
    /// <param name="buildCallable">True to build a callable bond pricer</param>
    /// <returns></returns>
    private BondPricer BuildConvertibleBondPricer(
      Bond bond, double s0, double sVol, double div, 
      double rho, double kappa, double rVol, int n, double marketQuote, bool buildCallable)
      {
      var dividends = new
        ConvertibleBondIntersectingTreesModel.StockCorrelatedModel.StockDividends(null, new double[] {div});
      var pricer = new BondPricer(bond, asOf_, settle_, discountCurve_, creditCurve_, 0,
                                  TimeUnit.None, 0.4, s0, sVol, dividends, rho, kappa, rVol, n);
      pricer.Notional = 1000000;

      if (!buildCallable)
      {
        pricer.RedemptionPrice = 100.0;
        pricer.WithAccrualOnCall = false;
      }      

      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      if(!Double.IsNaN(marketQuote))
        pricer.MarketQuote = marketQuote / 100;      
            
      pricer.Validate();

      return pricer;
    }

    /// <summary>
    ///  Compute european call/put option prices
    /// </summary>
    /// <param name="S0">Array of current stock prices</param>
    /// <param name="K">Array of strike</param>
    /// <param name="num">Array of number of steps</param>
    /// <param name="sigma">Stock volatility</param>
    /// <param name="divYield">Continuous dividend rate</param>
    /// <param name="rate">Risk free rate</param>
    /// <param name="isCall">True to compute call option</param>
    /// <returns>3-Dimensional option prices</returns>
    private double[,,] ComputeEuropeanOptions(
      double[] S0, double[] K, int[] num, double sigma, double divYield, double rate, bool isCall)
    {
      // Get a flat ir curve
      DiscountCurve irCurve = new DiscountCurve(asOf_, rate);
      double kappaR = 0;
      double sigmaR = 0;
      double rho = 0;

      BlackKarasinskiBinomialTreeModel rateModel = null;
      ConvertibleBondIntersectingTreesModel.StockCorrelatedModel stockModel = null;     

      int iS = S0.Length;
      int iK = K.Length;
      int iN = num.Length;
      double[,,] options = new double[iS,iK,iN];

      for(int i = 0; i < iS; i++)
      {
        for(int j = 0; j < iK; j++)
        {
          for(int k =0; k < iN; k++)
          {
            rateModel = new BlackKarasinskiBinomialTreeModel(kappaR, sigmaR, asOf_, maturity_, num[k], irCurve);
            stockModel = new ConvertibleBondIntersectingTreesModel.StockCorrelatedModel(
              S0[i], sigma, 
              new ConvertibleBondIntersectingTreesModel.StockCorrelatedModel.StockDividends(null, new double[]{0.0}), 
              asOf_, maturity_, num[k], rho);
            stockModel.BuildStockTree(rateModel);
            options[i, j, k] = ComputeEuropeanOption(stockModel, rateModel, K[j], isCall);
          }
        }
      }
      return options;
    }

    /// <summary>
    ///  Compute european option prices
    /// </summary>
    /// <param name="stockModel">Stock model</param>
    /// <param name="rateModel">Rate model</param>
    /// <param name="strike">Steike price</param>
    /// <param name="isCall">True to compute call option</param>
    /// <returns></returns>
    private static double ComputeEuropeanOption(
      ConvertibleBondIntersectingTreesModel.StockCorrelatedModel stockModel,
      BlackKarasinskiBinomialTreeModel rateModel, double strike, bool isCall)
    {
      int n = rateModel.N-1;
      double dt = rateModel.DeltaT;
      List<double[]> rateTree = rateModel.RateTree;
      double[,] finalStockPrices = stockModel.GetStockPrices(n);
      double[,] optionPrice = new double[n + 1, n + 1];
      for (int i = 0; i <= n; i++)
      {
        for (int j = 0; j <= n; j++)
        {
          optionPrice[i, j] = isCall
                                ? Math.Max(finalStockPrices[i, j] - strike, 0)
                                : Math.Max(strike - finalStockPrices[i, j], 0);
        }
      }
      // Loop back
      for (int k = n - 1; k >= 0; k--)
      {
        for (int i = 0; i <= k; i++)
        {
          for (int j = 0; j <= k; j++)
          {
            double a = optionPrice[i, j] * Math.Exp(-dt * (rateTree[k][i]));
            double b = optionPrice[i, j + 1] * Math.Exp(-dt * (rateTree[k][i]));
            double c = optionPrice[i + 1, j] * Math.Exp(-dt * (rateTree[k][i]));
            double d = optionPrice[i + 1, j + 1] * Math.Exp(-dt * (rateTree[k][i]));

            optionPrice[i, j] = 0.25 * (a + b + c + d);
          }
        }
      }
      return optionPrice[0, 0]; 
    }

    /// <summary>
    ///  Compute Black-Scholes option price
    /// </summary>
    /// <param name="S0">Array of stock prices</param>
    /// <param name="K">Array of strike prices</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">Risk free rate</param>
    /// <param name="divYield">Continuous dividend yield</param>
    /// <param name="T">Time to maturity</param>
    /// <param name="isCall">True to compute call option</param>
    /// <returns></returns>
    private static double[,] BlackSholesOptions(double[] S0, double[] K, double sigma, double r, double divYield, double T, bool isCall)
    {
      int iS = S0.Length;
      int iK = K.Length;
      var options = new double[iS, iK];
      for(int i = 0; i < iS; i++)
      {
        for(int j = 0; j < iK; j++)
        {
          options[i,j] = BlackScholes.P(OptionStyle.European, isCall?OptionType.Call:OptionType.Put, T, S0[i], K[j], r, divYield, sigma);          
        }
      }
      return options;
    }

    #endregion helpers

    #region data

    private Dt asOf_ = new Dt(29, 5, 2009);
    private Dt settle_ = new Dt(29, 5, 2009);
    private Dt maturity_ = new Dt(15, 1, 2013);
    private DiscountCurve discountCurve_ = null;

    #region ir curve data
    Dt[] mmTenorDates_ = new Dt[]
                            {
                              new Dt(30, 5, 2009), new Dt(5, 6, 2009), new Dt(12, 6, 2009), new Dt(29, 6, 2009),
                              new Dt(29, 7, 2009), new Dt(29, 8, 2009), new Dt(29, 9, 2009), new Dt(29, 10, 2009),
                              new Dt(29, 11, 2009), new Dt(28, 2, 2010), new Dt(29, 5, 2010)
                            };
    string[] mmTenors_ = new string[]
                            {
                              "1 Days", "1 Weeks", "2 Weeks", "1 Months", "2 Months", "3 Months",
                              "4 Months", "5 Months", "6 Months", "9 Motnhs", "1 Yr"
                            };
    double[] mmRates_ = new double[]
                           {
                             0.00261, 0.00289, 0.00299, 0.00318, 0.00475, 0.00629, 0.00875, 0.01049, 0.01180, 0.01368,
                             0.01548
                           };
    string[] swapTenors_ = new string[] { "2 Yr", "3 Yr", "4 Yr", "5 Yr", "6 Yr", "7 Yr", "8 Yr", "9 Yr", "10 Yr" };
    Dt[] swapTenorDates_ = new Dt[]
                              {
                                new Dt(29, 5, 2011), new Dt(29, 5, 2012), new Dt(29, 5, 2013), new Dt(29, 5, 2014),
                                new Dt(29, 5, 2015),
                                new Dt(29, 5, 2016), new Dt(29, 5, 2017), new Dt(29, 5, 2018), new Dt(29, 5, 2019)
                              };
    double[] swapRates_ = new double[]{
        0.01384, 0.02016, 0.02569, 0.02985, 0.0329, 0.03524, 0.03697, 0.03833, 0.0395
      };
    #endregion ir curve data

    #region credit curve data

    private SurvivalCurve creditCurve_ = null;
    string[] tenorNames = new string[]{"6 Months","1 Year","2 Year", "3 Year", "4 Year", "5 Year", "7 Year"};
    private Dt[] tenorDates = null;
    private double[] premiums_ = new double[] {223.00, 223.00, 296.00, 365.00, 413.00, 510.00, 532.00};
    private double[] recoveries = new double[] {0.4};
    #endregion credit curve data

    #region bond data

    private Dt effectiveDate_ = new Dt(15, 1, 2008);
    private DayCount bondDayCount_ = DayCount.ActualActualBond;
    private Frequency bondFreq_ = Frequency.SemiAnnual;
    private BDConvention bondRoll_ = BDConvention.Following;
    private Calendar bondCal_ = Toolkit.Base.Calendar.NYB;
    private double couponRate_ = 0.035;

    #endregion bond data

    #endregion data
  }
}
