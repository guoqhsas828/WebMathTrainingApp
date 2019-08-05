//
// Compare various basket loss distribution results
// Copyright (c)    2002-2018. All rights reserved.
//

// Enable this test efficiency
//#define TEST_TIMING

using System;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture("SA basket")]
  [TestFixture("SA basket qcr")]
  [TestFixture("SA basket refi")]
  [TestFixture("SA basket refi+qcr")]
  public class TestSemiAnalyticBasket : ToolkitTestBase
  {
    public TestSemiAnalyticBasket(string name)
      : base(name)
    { }

    #region Basket Wrapper
    [Serializable]
    class MyBasket : SemiAnalyticBasketPricer
    {
      public MyBasket(
        Dt asOf, Dt settle, Dt maturity,
        SurvivalCurve[] survivalCurves,
        SurvivalCurve[] refinanceCurves,
        double[] refinanceCorrelations,
        RecoveryCurve[] recoveryCurves,
        double[] principals,
        Copula copula, Correlation correlation,
        int stepSize, TimeUnit stepUnit,
        Array lossLevels)
        : base(asOf, settle, maturity, survivalCurves,
           refinanceCurves, refinanceCorrelations,
           recoveryCurves, principals, copula, correlation,
           stepSize, stepUnit, lossLevels)
      { }

      public double[,] InvokeBumpedPvs(
        Toolkit.Sensitivity.PricerEvaluator[] pricers,
        SurvivalCurve[] altSurvivalCurves,
        bool includeRecoverySensitivity)
      {
        return BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);
      }
    }
    #endregion Basket Wrapper

    #region SetUp

    [OneTimeSetUp]
    public void Init()
    {
      BasketPricer basket = CreateBasket();
      pricer_ = CreateCdoPricer(basket);
    }

    private BasketPricer CreateBasket()
    {
      const double hmin = 0.0, hmax = 0.7, recovery = 0.4;
      Dt asOf = PricingDate == 0 ? Dt.Today() : new Dt(PricingDate);
      Dt settle = SettleDate == 0 ? Dt.Add(asOf, 1) : new Dt(SettleDate);
      Dt maturity = MaturityDate == 0 ? Dt.CDSMaturity(settle, "5Y") : new Dt(MaturityDate);
      int N = basketSize_;

      SurvivalCurve[] scurves = new SurvivalCurve[N];
      altSurvivalCurves_ = new SurvivalCurve[N];
      for (int i = 0; i < N; ++i)
      {
        double rate = hmin + (hmax - hmin) * i / N;
        scurves[i] = new SurvivalCurve(asOf, rate);
        altSurvivalCurves_[i] = new SurvivalCurve(asOf, rate + 0.001);
      }

      SurvivalCurve[] refinances = null;
      double[] reficorrs = null;
      if (WithRefinance)
      {
        refinances = new SurvivalCurve[N];
        reficorrs = ArrayUtil.NewArray(N, -1.0);
        for (int i = 0; i < N; ++i)
        {
          double h = hmin + (hmax - hmin) * i / N;
          double probDflt = 1 - Math.Exp(-h * 10);
          double probNoRefi = probDflt < 0.01 ? 0.01 : probDflt;
          double rate = -Math.Log(probNoRefi) / 10;
          refinances[i] = new SurvivalCurve(asOf, rate);
        }
      }

      RecoveryCurve[] rcurves = new RecoveryCurve[N];
      for (int i = 0; i < N; ++i)
        rcurves[i] = new RecoveryCurve(asOf, recovery);

      double[] principals = ArrayUtil.NewArray(N, 1000.0);
      SingleFactorCorrelation corr = new SingleFactorCorrelation(new string[N], 0.0);

      int stepSize = 3; TimeUnit stepUnit = TimeUnit.Months;
      GetTimeGrid(ref stepSize, ref stepUnit);
      if (stepSize == 0 && stepUnit == TimeUnit.None)
      {
        stepSize = 3; stepUnit = TimeUnit.Months;
      }

      MyBasket basket = new MyBasket(
        asOf, settle, maturity, scurves,refinances,reficorrs,
        rcurves, principals, GetCopula(), corr,
        stepSize, stepUnit, new double[] { 0, 1 });
      basket.IntegrationPointsFirst = QuadraturePoints;
      basket.AccuracyLevel = Accuracy;
      basket.WithCorrelatedRecovery = WithQCR;

      return basket;
    }

    private SyntheticCDOPricer CreateCdoPricer(BasketPricer basket)
    {
      Dt effective = basket.Settle;
      Dt mauturity = basket.Maturity;
      SyntheticCDO cdo = new SyntheticCDO(effective, mauturity,
        Currency, 1, DayCount, Frequency, Roll, Calendar);
      return new SyntheticCDOPricer(cdo, basket,
        new DiscountCurve(basket.AsOf, discountRate_));
    }

    #endregion SetUp

    #region Helpers

    private static double BasketLoss(BasketPricer basket, Dt date)
    {
      double loss = 0;
      int N = basket.Count;
      for (int i = 0; i < N; ++i)
      {
        loss += (1 - basket.RecoveryRates[i]) * basket.Principals[i]
          * (1 - basket.SurvivalCurves[i].Interpolate(basket.Settle, date));
      }
      loss /= basket.TotalPrincipal;
      return loss;
    }
    private static double BasketAmor(BasketPricer basket, Dt date)
    {
      double amor = 0;
      int N = basket.Count;
      for (int i = 0; i < N; ++i)
      {
        amor += basket.RecoveryRates[i] * basket.Principals[i]
          * (1 - basket.SurvivalCurves[i].Interpolate(basket.Settle, date));
        if (basket.RefinanceCurves != null)
          amor += basket.Principals[i] * (1 -
            basket.RefinanceCurves[i].Interpolate(basket.Settle, date));
      }
      amor /= basket.TotalPrincipal;
      return amor;
    }

    private void CheckCumulativeLoss(double factor)
    {
      SyntheticCDOPricer pricer = pricer_;
      BasketPricer basket = pricer.Basket;
      basket.SetFactor(factor);
      pricer.Reset();

      Dt date = basket.Maturity;
      double expect = BasketLoss(basket, date);
      double actual = basket.AccumulatedLoss(date, 0.0, 1.0);
      Assert.AreEqual(expect, actual,
        factor < 1 ? 1E-6 : 1E-12, "Loss@" + factor);
    }

    void CheckCumulativeAmor(double factor)
    {
      SyntheticCDOPricer pricer = pricer_;
      BasketPricer basket = pricer.Basket;
      basket.SetFactor(factor);
      pricer.Reset();

      Dt date = basket.Maturity;
      double expect = BasketAmor(basket, date);
      double actual = basket.AmortizedAmount(date, 0.0, 1.0);
      Assert.AreEqual(expect, actual,
        factor < 1 ? 1E-6 : 1E-12, "Amor@" + date.ToInt());
    }

    void CheckBumpedLosses(double factor)
    {
      SyntheticCDOPricer pricer = pricer_;
      MyBasket basket = (MyBasket)pricer_.Basket;
      basket.SetFactor(factor);
      pricer.Reset();

      SurvivalCurve[] altSurvivalCurves = altSurvivalCurves_;
      Dt date = basket.Maturity;
      int N = basket.Count;
      double[] expects = ArrayUtil.NewArray(
        N + 1, BasketLoss(basket, date));
      for (int i = 0; i < N; ++i)
      {
        double loss0 = (1 - basket.RecoveryRates[i]) * basket.Principals[i]
          * (1 - basket.SurvivalCurves[i].Interpolate(basket.Settle, date));
        double loss1 = (1 - basket.RecoveryRates[i]) * basket.Principals[i]
          * (1 - altSurvivalCurves[i].Interpolate(basket.Settle, date));
        expects[i + 1] += (loss1 - loss0) / basket.TotalPrincipal;
      }

      PricerEvaluator evaluator = new PricerEvaluator(pricer_,
        delegate(IPricer p)
        {
          return ((SyntheticCDOPricer)p).Basket.AccumulatedLoss(date, 0, 1);
        });
      double[,] actuals = basket.InvokeBumpedPvs(new PricerEvaluator[] { evaluator },
        altSurvivalCurves, false);

      double tolerance = factor < 1 ? 1E-6 : 1E-12;
      for (int i = 0; i <= N; ++i)
        Assert.AreEqual(expects[i], actuals[i,0],
          tolerance, "Loss" + i + '@' + factor);

      return;
    }

    void CheckBumpedAmorts(double factor)
    {
      SyntheticCDOPricer pricer = pricer_;
      MyBasket basket = (MyBasket)pricer_.Basket;
      basket.SetFactor(factor);
      pricer.Reset();

      SurvivalCurve[] altSurvivalCurves = altSurvivalCurves_;
      Dt date = basket.Maturity;
      int N = basket.Count;
      double[] expects = ArrayUtil.NewArray(
        N + 1, BasketAmor(basket, date));
      for (int i = 0; i < N; ++i)
      {
        double amor0 = basket.RecoveryRates[i] * basket.Principals[i]
          * (1 - basket.SurvivalCurves[i].Interpolate(basket.Settle, date));
        double amor1 = basket.RecoveryRates[i] * basket.Principals[i]
          * (1 - altSurvivalCurves[i].Interpolate(basket.Settle, date));
        expects[i + 1] += (amor1 - amor0) / basket.TotalPrincipal;
      }

      PricerEvaluator evaluator = new PricerEvaluator(pricer_,
        delegate(IPricer p)
        {
          return ((SyntheticCDOPricer)p).Basket.AmortizedAmount(date, 0, 1);
        });
      double[,] actuals = basket.InvokeBumpedPvs(new PricerEvaluator[] { evaluator },
        altSurvivalCurves, false);

      double tolerance = factor < 1 ? 5E-6 : 1E-12;
      for (int i = 0; i <= N; ++i)
        Assert.AreEqual(expects[i], actuals[i, 0],
          tolerance, "Amor" + i + '@' + factor);

      return;
    }

    #endregion Helpers

    #region Tests

    [Test]
    public void Losses()
    {
      for (int i = 0; i <= 6; ++i)
        CheckCumulativeLoss(0.25 * i);
    }

    [Test]
    public void Amortizations()
    {
      for (int i = 0; i <= 6; ++i)
        CheckCumulativeAmor(0.25 * i);
    }

    [Test]
    public void BumpedLosses()
    {
      CheckBumpedLosses(0.8);
      CheckBumpedLosses(1.25);
    }

    [Test]
    public void BumpedAmortizations()
    {
      CheckBumpedAmorts(0.8);
      CheckBumpedAmorts(1.25);
    }

    #endregion Tests

    #region Properties
    public bool WithRefinance { get; set; } = false;

    public bool WithQCR { get; set; } = false;

    #endregion Properties

    #region Data
    private int basketSize_ = 100;
    private double discountRate_ = 0.03;

    //private bool withEarlyMaturity = false;

    private SyntheticCDOPricer pricer_;
    private SurvivalCurve[] altSurvivalCurves_;
    #endregion Data
  }
}
