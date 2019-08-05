//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  ///   Test basket with defaulted names
  /// </summary>
  [TestFixture("TestBasketDefaultedNames")]
  [TestFixture("TestCDOMaturityOnSettle")]
  [Smoke]
  public class TestBasketDefaultedNames : ToolkitTestBase
  {
    public TestBasketDefaultedNames(string name) : base(name) {}

    #region Data

    private double tolerance_ = 1E-8;

    //private Dt asOf_;
    //private Dt settle_;
    //private Dt effective_;
    //private Dt maturity_;
    private string tenor_ = "5Y";
    private double discountRate_ = 0.04;
    private double hazardRate_ = 0.0025;
    private double recoveryRate_ = 0.40;

    private double correlation_ = 0.3;
    private int basketSize_ = 100;
    private int defaultedNames_ = 10;
    private int principal_ = 1000000;
    private int notionalSign_ = 1;

    private double attachment_ = 0.03;
    private double detachment_ = 0.10;
    private double premium_ = 100;

    // calculated value
    private double prevTrancheLoss_ = 0;

    #endregion // Data

    #region SetUp

    /// <summary>
    ///   Create two CDO pricer: the first includes all the defaulted names,
    ///   the second excludes the defaulted names and manually adjust the
    ///   CDO subordination in the remaining basket.  Both pricers should
    ///   produce the same prices if the implemetation is correct.
    /// </summary>
    /// <param name="basketType">
    ///   Type of the basket
    /// </param>
    /// <returns>
    ///   An array of two pricers:
    ///   position 0 is a pricer with defaulted names;
    ///   position 1 is a pricer with defaulted names removed from the basket.
    /// </returns>
    private SyntheticCDOPricer[] CreatePricers(Type basketType)
    {
      // Get the user input dates
      Dt asOf, settle;
      if (this.PricingDate == 0 )
      {
        asOf = Dt.Today();
        settle = Dt.Add(asOf, 1);
      }
      else
      {
        asOf = new Dt(this.PricingDate);
        settle = this.SettleDate == 0 ? Dt.Add(asOf, 1) : new Dt(this.SettleDate);
      }
      Dt effective = this.EffectiveDate == 0 ? settle : new Dt(this.EffectiveDate);
      Dt maturity = this.MaturityDate == 0 ? Dt.CDSMaturity(effective, tenor_) : new Dt(this.MaturityDate);
      if (Dt.Cmp(maturity, settle) <= 0)
        maturity = Dt.CDSMaturity(settle, "1Y");

      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      // DiscountCurve
      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate_);

      // Survival curve
      SurvivalCurve survivalCurve = CreateSurvivalCurve(
        asOf, settle, ccy, dayCount, freq, roll, calendar);

      // Portfolio and made the last defaulted
      SurvivalCurve[] survivalCurves = new SurvivalCurve[basketSize_];
      for (int i = 0; i < basketSize_; ++i)
        survivalCurves[i] = (SurvivalCurve)survivalCurve.Clone();
      for (int i = 1; i <= defaultedNames_; ++i)
        survivalCurves[basketSize_ - i].SetDefaulted(Dt.Add(settle,-100), true);

      // Create CDO
      SyntheticCDO cdo = new SyntheticCDO(effective, maturity, ccy,
        premium_ / 10000.0, dayCount, freq, roll, calendar);
      cdo.Attachment = attachment_;
      cdo.Detachment = detachment_;
      double notional = cdo.TrancheWidth * basketSize_ * principal_ * notionalSign_;

      // Create pricers
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[2];
      pricers[0] = CreatePricer(basketType, cdo, asOf, settle,
        discountCurve, survivalCurves, this.QuadraturePoints, notional);

      // Adjust the detachment
      int adjBasketSize = basketSize_ - defaultedNames_;
      cdo = (SyntheticCDO)cdo.Clone();
      double lb = 1.0 * defaultedNames_ * (1 - recoveryRate_) / basketSize_;
      double ub = 1.0 - 1.0 * defaultedNames_ * recoveryRate_ / basketSize_;
      if (attachment_ < lb)
        cdo.Attachment = 0.0;
      else if (attachment_ > ub)
        cdo.Attachment = ub * basketSize_ / adjBasketSize;
      else
        cdo.Attachment = (attachment_ - lb) * basketSize_ / adjBasketSize;
      if (detachment_ < lb)
        cdo.Detachment = 0.0;
      else if (detachment_ > ub)
        cdo.Detachment = ub * basketSize_ / adjBasketSize;
      else
        cdo.Detachment = (detachment_ - lb) * basketSize_ / adjBasketSize;
      notional = cdo.TrancheWidth * adjBasketSize * principal_ * notionalSign_;

      if (lb < attachment_)
        prevTrancheLoss_ = 0;
      else if (lb >= detachment_)
        prevTrancheLoss_ = (detachment_ - attachment_) * basketSize_;
      else
        prevTrancheLoss_ = (lb - attachment_) * basketSize_ * principal_;

      // new survival curve
      SurvivalCurve[] adjSurvivalCurves = new SurvivalCurve[adjBasketSize];
      for (int i = 0; i < adjBasketSize; ++i)
        adjSurvivalCurves[i] = survivalCurves[i];

      // Create pricer, make sure both pricers use the same quadrature points
      pricers[1] = CreatePricer(basketType, cdo, asOf, settle,
        discountCurve, adjSurvivalCurves, pricers[0].Basket.IntegrationPointsFirst, notional);

      return pricers;
    }

    /// <summary>
    ///   Create a survival curve
    /// </summary>
    private SurvivalCurve CreateSurvivalCurve(
      Dt asOf, Dt settle, Currency ccy,
      DayCount dayCount, Frequency freq,
      BDConvention roll, Calendar calendar)
    {
      Dt maturity = Dt.CDSMaturity(settle, tenor_);
      SurvivalCurve survivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, ccy, "None",
        BaseEntity.Toolkit.Numerics.InterpMethod.Linear, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
        new Dt[] { maturity },
        new double[] { Math.Exp(-hazardRate_ * Dt.FractDiff(settle, maturity) / 365) },
        new string[] { tenor_ },
        new DayCount[] { dayCount }, new Frequency[] { freq },
        new BDConvention[] { roll }, new Calendar[] { calendar },
        new double[] { recoveryRate_ }, 0.0);
      return survivalCurve;
    }

    /// <summary>
    ///   Create a CDO Pricer
    /// </summary>
    private SyntheticCDOPricer CreatePricer(
      Type basketType, SyntheticCDO cdo,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      int quadPoints,
      double notional)
    {
      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      GetTimeGrid(ref stepSize, ref stepUnit);
      if (stepSize <= 0)
      {
        stepSize = 3;
        stepUnit = TimeUnit.Months;
      }

      SyntheticCDOPricer pricer;
      if (basketType == typeof(LargePoolBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerLargePool(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, new double[] { principal_ },
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, null, false)[0];
      }
      else if (basketType == typeof(UniformBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerUniform(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, new double[] { principal_ },
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, null, false)[0];
      }
      else if (basketType == typeof(HomogeneousBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerHomogeneous(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, new double[] { principal_ },
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, null, false)[0];
      }
      else if (basketType == typeof(HeterogeneousBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerHeterogeneous(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, new double[] { principal_ },
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, 0, null, false)[0];
      }
      else if (basketType == typeof(MonteCarloBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerMonteCarlo(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, new double[] { principal_ },
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, this.SampleSize, null, false, 0)[0];
      }
      else
      {
        SyntheticCDO cdo1 = (SyntheticCDO)cdo.Clone();
        cdo1.Detachment = 1.0;
        pricer = BasketPricerFactory.CDOPricerSemiAnalytic(
          new SyntheticCDO[] { cdo, cdo1 }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, new double[] { principal_ },
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, 0, null, false, false)[0];
      }
      pricer.Notional = notional;

      return pricer;
    }

    #endregion // SetUp

    #region TestHelpers

    private static void AreEqual(string label, double expect, double actual, double epsrel)
    {
      Assert.AreEqual(expect, actual, epsrel * (1 + Math.Abs(expect)), label);
    }

    private void DoTest(Type pricerType)
    {
      // Requirement:
      //    SyntheticCDOPricer.UseOriginalNotionalForFee == false;
      if (!Settings.SyntheticCDOPricer.UseOriginalNotionalForFee)
      {
        SyntheticCDOPricer[] pricers = CreatePricers(pricerType);
        double tranchAdjust = pricers[0].EffectiveNotional / pricers[0].Notional;

        // Just to display the original notionals for comparisions
        AreEqual("OriginalNotional", pricers[0].Notional, pricers[0].Notional, 1E-7);
        AreEqual("AdjustmentFactor", tranchAdjust, tranchAdjust, 1E-7);

        // Basic prce measures
        TestBasicPriceMeasures("Present Values", pricers, tranchAdjust);

        // Forward values one year from now
        {
          SyntheticCDO cdo0 = (SyntheticCDO)pricers[0].CDO.Clone();
          cdo0.Effective = Dt.Add(pricers[0].Settle, "1Y");

          if (Dt.Cmp(cdo0.Maturity, cdo0.Effective) < 0)
            cdo0.Maturity = Dt.CDSMaturity(cdo0.Effective, "3M");
          SyntheticCDO cdo1 = (SyntheticCDO)pricers[1].CDO.Clone();
          cdo1.Effective = cdo0.Effective;
          cdo1.Maturity = cdo0.Maturity;
          SyntheticCDOPricer pricer0 = pricers[0];
          pricers[0] = new SyntheticCDOPricer(cdo0, pricer0.Basket,
            pricer0.DiscountCurve, pricer0.Notional);
          SyntheticCDOPricer pricer1 = pricers[1];
          pricers[1] = new SyntheticCDOPricer(cdo1, pricer1.Basket,
            pricer1.DiscountCurve, pricer1.Notional);
          pricers[1] = (SyntheticCDOPricer)pricers[1].Clone(); // just to test clone
          TestBasicPriceMeasures("Forward Values", pricers, tranchAdjust);
          pricers[0] = pricer0;
          pricers[1] = pricer1;
        }
        //AreEqual("Subordination01", pricers[1].Subordination01(), pricers[0].Subordination01(), tolerance_);

        // Test Expected loss
        AreEqual("EL " + pricers[0].Settle.ToString(),
          pricers[1].LossToDate(pricers[0].Settle) + prevTrancheLoss_,
          pricers[0].LossToDate(pricers[0].Settle),
          tolerance_);
        for (Dt date = pricers[0].Settle; Dt.Cmp(date, pricers[0].Maturity) < 0; )
        {
          date = Dt.Add(date, pricers[0].StepSize, pricers[0].StepUnit);
          if (Dt.Cmp(date, pricers[0].Maturity) > 0)
            date = pricers[0].Maturity;
          AreEqual("EL " + date.ToString(),
            pricers[1].LossToDate(date) + prevTrancheLoss_,
            pricers[0].LossToDate(date),
            tolerance_
            );
        }

        // Test basket loss distributions
        {
          double prevLoss = pricers[0].Basket.AccumulatedLoss(pricers[0].Basket.Settle, 0.0, 1.0);
          double prevAmor = pricers[0].Basket.AmortizedAmount(pricers[0].Basket.Settle, 0.0, 1.0);
          double remainingBasket = 1 - prevLoss - prevAmor;
          int N = 10;
          double[] lossLevels0 = new double[N];
          double[] lossLevels1 = new double[N];
          for (int i = 0; i < N; ++i)
          {
            double level = 1.0 * i / 16.0 / N;
            lossLevels1[i] = level;
            lossLevels0[i] = level * remainingBasket + prevLoss;
          }
          // expected loss
          double[,] dist0 = pricers[0].Basket.CalcLossDistribution(false, pricers[0].Maturity, lossLevels0);
          double[,] dist1 = pricers[1].Basket.CalcLossDistribution(false, pricers[0].Maturity, lossLevels1);
          for (int i = 0; i < N; ++i)
            AreEqual(String.Format("EL[{0}]", dist0[i, 0]),
              dist1[i, 1] * remainingBasket + prevLoss, dist0[i, 1], tolerance_);
          // probability
          dist0 = pricers[0].Basket.CalcLossDistribution(true, pricers[0].Maturity, lossLevels0);
          dist1 = pricers[1].Basket.CalcLossDistribution(true, pricers[0].Maturity, lossLevels1);
          for (int i = 0; i < N; ++i)
            AreEqual(String.Format("Pr[{0}]", dist0[i, 0]), dist1[i, 1], dist0[i, 1], tolerance_);
        }
      }
    }

    private void TestBasicPriceMeasures(string groupName, SyntheticCDOPricer[] pricers, double adjust)
    {
      AreEqual(groupName, 0.0, 0.0, 0.0);

      // Test basic price measures
      AreEqual("CurrentNotional", pricers[1].EffectiveNotional, pricers[0].EffectiveNotional, tolerance_);
      AreEqual("Accrued", pricers[1].Accrued(), pricers[0].Accrued(), tolerance_);

      AreEqual("ExpectedLoss", pricers[1].ExpectedLoss() + prevTrancheLoss_, pricers[0].ExpectedLoss(), tolerance_);
      AreEqual("ExpectedSurvival", pricers[1].ExpectedSurvival() * adjust,
        pricers[0].ExpectedSurvival(), tolerance_);

      AreEqual("ProtectionPv", pricers[1].ProtectionPv(), pricers[0].ProtectionPv(), tolerance_);

      AreEqual("FeePv", pricers[1].FeePv(), pricers[0].FeePv(), tolerance_);
      AreEqual("FullFeePv", pricers[1].FullFeePv(), pricers[0].FullFeePv(), tolerance_);
      AreEqual("FlatFeePv", pricers[1].FlatFeePv(), pricers[0].FlatFeePv(), tolerance_);

      AreEqual("Pv", pricers[1].Pv(), pricers[0].Pv(), tolerance_);
      AreEqual("FullPrice", pricers[1].FullPrice(), pricers[0].FullPrice(), tolerance_);
      AreEqual("FlatPrice", pricers[1].FlatPrice(), pricers[0].FlatPrice(), tolerance_);

      // We should not excpect the risky duration of pricer with defaulted names to be same as 
      // that of the good pricer. According to case 23675 description by MF, the risky duration
      // with defaulted names should be computed by FlatFeePv()/Notional; The risky duration of
      // good pricer is FlatFeePv()/EffectiveNotional. 
      // Note FlatFeePv()/Notional = (FlatFeePv()/EffectiveNotional)(EffectiveNotional/Notional)
      AreEqual("RiskyDuration", pricers[1].RiskyDuration(), pricers[0].RiskyDuration()/pricers[1].Notional*pricers[0].Notional, tolerance_);
      AreEqual("BreakEvenFee", pricers[1].BreakEvenFee(),// * adjust,
        pricers[0].BreakEvenFee(), tolerance_);
      AreEqual("BreakEvenPremium", pricers[1].BreakEvenPremium(), pricers[0].BreakEvenPremium(), tolerance_);
      AreEqual("Premium01", pricers[1].Premium01(), pricers[0].Premium01(), tolerance_);
      AreEqual("Carry", pricers[1].Carry(), pricers[0].Carry(), tolerance_);
      AreEqual("MTMCarry", pricers[1].MTMCarry(), pricers[0].MTMCarry(), tolerance_);
    }

    #endregion // TestHelpers

    #region Tests

    [Test, Smoke]
    public void LargePoolPricer()
    {
      DoTest(typeof(LargePoolBasketPricer));
    }

    [Test, Smoke]
    public void UniformPricer()
    {
      DoTest(typeof(UniformBasketPricer));
    }

    [Test, Smoke]
    public void HomogeneousPricer()
    {
      DoTest(typeof(HomogeneousBasketPricer));
    }

    [Test, Smoke]
    public void HeterogeneousPricer()
    {
      DoTest(typeof(HeterogeneousBasketPricer));
    }

    [Test, Smoke]
    public void SemiAnalyticPricer()
    {
      DoTest(typeof(SemiAnalyticBasketPricer));
    }

    [Test, Smoke]
    public void MonteCarloPricer()
    {
      DoTest(typeof(MonteCarloBasketPricer));
    }

    #endregion // Tests

  } // class TestBasketDefaultedNames
}
