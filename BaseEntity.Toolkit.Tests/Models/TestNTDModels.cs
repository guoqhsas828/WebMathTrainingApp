//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  ///   Test basket with short names
  /// </summary>
  
  [TestFixture("TestNTDMaturityOnSettle")]
  [TestFixture("TestNTDModels")]
  [Smoke]
  public class TestNTDModels : ToolkitTestBase
  {
    public TestNTDModels(string name) : base(name) {} 

    #region Data
    private string tenor_ = "5Y";
    private double discountRate_ = 0.04;
    private double hazardRate_ = 0.0025;
    private double recoveryRate_ = 0.40;

    private double premium_ = 100;
    private double fee_ = 0;
    private int feeSettleDate_ = 0;

    private double correlation_ = 0.3;
    private double principal_ = 1000000;
    int basketSize_ = 10;

    // Calculated
    private Dt asOf_, settle_;
    IPricer[,] pricers_;
    #endregion // Data

    #region SetUp
    [OneTimeSetUp]
    public void Initialize()
    {
      // Get the user input dates
      if (this.PricingDate == 0)
      {
        asOf_ = Dt.Today();
        settle_ = Dt.Add(asOf_, 1);
      }
      else
      {
        asOf_ = new Dt(this.PricingDate);
        settle_ = this.SettleDate == 0 ? Dt.Add(asOf_, 1) : new Dt(this.SettleDate);
      }

      pricers_ = CreatePricers();
    }

    /// <summary>
    ///   Create a CDS pricer
    /// </summary>
    /// <returns>CDS pricer</returns>
    private IPricer[,] CreatePricers()
    {
      Dt asOf = asOf_;
      Dt settle = settle_;
      Dt effective = this.EffectiveDate == 0 ?
        Dt.CDSMaturity(Dt.Add(settle, -270), "3M") : new Dt(this.EffectiveDate);
      Dt maturity = this.MaturityDate == 0 ? Dt.CDSMaturity(effective, tenor_) : new Dt(this.MaturityDate);

      SurvivalCurve[] survivalCurves = new SurvivalCurve[basketSize_];
      for (int i = 0; i < basketSize_; ++i)
        survivalCurves[i] = CreateSurvivalCurve(asOf, settle, hazardRate_ + 0.001 * i);
      double[] principal = new double[] { principal_ };

      int N = basketSize_ * (basketSize_ + 1) / 2;
      FTD[] ntds = new FTD[N];
      SyntheticCDO[] pcdos = new SyntheticCDO[N];
      SyntheticCDO[] acdos = new SyntheticCDO[N];
      for (int nCover = 1, idx = 0; nCover <= basketSize_; ++nCover)
      {
        int nTop = basketSize_ - nCover + 1;
        for (int iStart = 1; iStart <= nTop; ++idx, ++iStart)
        {
          ntds[idx] = CreateNTD(effective, maturity, iStart, nCover);
          pcdos[idx] = CreateProtectionTranche(effective, maturity, iStart, nCover);
          acdos[idx] = CreateAmortizationTranche(effective, maturity, iStart, nCover);
        }
      }

      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate_);

      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      GetTimeGrid(ref stepSize, ref stepUnit);
      if (stepSize <= 0)
      {
        stepSize = 3;
        stepUnit = TimeUnit.Months;
      }
      Copula copula = GetCopula();
      CorrelationObject corr = new SingleFactorCorrelation(
        new string[survivalCurves.Length], Math.Sqrt(correlation_));

      FTDPricer[] ntdPricers = BasketPricerFactory.NTDPricerSemiAnalytic(
        ntds, asOf, settle, discountCurve, survivalCurves, principal,
        copula, corr, stepSize, stepUnit, this.QuadraturePoints, null);
      SyntheticCDOPricer[] pcdoPricers = BasketPricerFactory.CDOPricerSemiAnalytic(
        pcdos, new Dt(), asOf, settle, discountCurve, survivalCurves, principal,
        copula, corr, stepSize, stepUnit, this.QuadraturePoints, 0, null, false, false);
      SyntheticCDOPricer[] acdoPricers = BasketPricerFactory.CDOPricerSemiAnalytic(
        acdos, new Dt(), asOf, settle, discountCurve, survivalCurves, principal,
        copula, corr, stepSize, stepUnit, this.QuadraturePoints, 0, null, false, false);

      IPricer[,] pricers = new IPricer[N, 3];
      for (int i = 0; i < N; ++i)
      {
        pricers[i, 0] = ntdPricers[i];
        pricers[i, 1] = pcdoPricers[i];
        pricers[i, 2] = acdoPricers[i];
      }
      return pricers;
    }

    /// <summary>
    ///   Create a FTD product
    /// </summary>
    private FTD CreateNTD(Dt effective, Dt maturity, int firstCover, int numCover)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      FTD ntd = new FTD(effective, maturity, ccy,
        premium_ / 10000, dayCount, freq, roll, calendar,
        firstCover, numCover);
      if (fee_ > 0)
      {
        ntd.Fee = fee_;
        ntd.FeeSettle = new Dt(feeSettleDate_);
      }
      ntd.Description = "NTD " + firstCover + '/' + numCover;

      return ntd;
    }

    /// <summary>
    ///   Create a CDO product equivalent for protection calculation
    /// </summary>
    private SyntheticCDO CreateProtectionTranche(
      Dt effective, Dt maturity, int firstCover, int numCover)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      double attachment = (1 - recoveryRate_) * (firstCover - 1) / basketSize_;
      double detachment = attachment + (1 - recoveryRate_) * numCover / basketSize_;

      SyntheticCDO cdo = new SyntheticCDO(effective, maturity, ccy,
        dayCount, freq, roll, calendar, premium_ / 10000, 0.0, attachment, detachment);
      if (fee_ > 0)
      {
        cdo.Fee = fee_;
        cdo.FeeSettle = new Dt(feeSettleDate_);
      }
      cdo.Description = "CDO p" + firstCover + '/' + numCover;

      return cdo;
    }

    /// <summary>
    ///   Create a CDO product equivalent for amortization calculation
    /// </summary>
    private SyntheticCDO CreateAmortizationTranche(
      Dt effective, Dt maturity, int firstCover, int numCover)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      double detachment = 1.0 - recoveryRate_ * (firstCover - 1) / basketSize_;
      double attachment = detachment - recoveryRate_ * numCover / basketSize_;

      SyntheticCDO cdo = new SyntheticCDO(effective, maturity, ccy,
        dayCount, freq, roll, calendar, premium_ / 10000, 0.0, attachment, detachment);
      if (fee_ > 0)
      {
        cdo.Fee = fee_;
        cdo.FeeSettle = new Dt(feeSettleDate_);
      }
      cdo.Description = "CDO a" + firstCover + '/' + numCover;

      return cdo;
    }
    
    /// <summary>
    ///   Create a survival curve
    /// </summary>
    private SurvivalCurve CreateSurvivalCurve(Dt asOf, Dt settle, double hazardRate)
    {
      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      Dt maturity = Dt.CDSMaturity(settle, tenor_);
      SurvivalCurve survivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, ccy, "None",
        BaseEntity.Toolkit.Numerics.InterpMethod.Linear, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
        new Dt[] { maturity },
        new double[] { Math.Exp(-hazardRate * Dt.FractDiff(settle, maturity) / 365) },
        new string[] { tenor_ },
        new DayCount[] { dayCount }, new Frequency[] { freq },
        new BDConvention[] { roll }, new Calendar[] { calendar },
        new double[] { recoveryRate_ }, 0.0);
      return survivalCurve;
    }
    #endregion // SetUp

    #region TestHelpers

    void TestPrice(string measure, double tolerance)
    {
      Double_Pricer_Fn ntdFn = DoublePricerFnBuilder.CreateDelegate(typeof(FTDPricer), measure);
      Double_Pricer_Fn cdoFn = DoublePricerFnBuilder.CreateDelegate(typeof(SyntheticCDOPricer), measure);

      int N = pricers_.GetLength(0);
      for (int i = 0; i < N; ++i)
        Assert.AreEqual(
          ntdFn(pricers_[i, 0]), cdoFn(pricers_[i, 1]) + cdoFn(pricers_[i, 2]),
          tolerance * ((FTDPricer)pricers_[i, 0]).Notional * (i == 0 ? 2 : 1),
          pricers_[i, 0].Product.Description);
      return;
    }

    #endregion TestHelpers

    #region Tests

    /// <summary>
    ///   Test the case First + Covers > Issuers for SemiAna;yticPricer.
    ///   Expect an exception.
    /// </summary>
    [Test]
    public void CoverMoreThanIssuersSA()
    {
      Assert.Throws<ValidationException>(() =>
      {
        FTDPricer ntdPricer = (FTDPricer) pricers_[0, 0];
        var basket = ntdPricer.Basket as SemiAnalyticBasketForNtdPricer;
        FTD ntd = (FTD) ntdPricer.FTD.Clone();
        ntd.NumberCovered = basket.Count - ntd.First + 2;
        FTDPricer[] pricers = BasketPricerFactory.NTDPricerSemiAnalytic(
          new FTD[] {ntd}, asOf_, settle_, ntdPricer.DiscountCurve, basket.SurvivalCurves,
          basket.Principals, basket.Copula, basket.Correlation,
          basket.StepSize, basket.StepUnit,
          basket.IntegrationPointsFirst,
          new double[] {ntdPricer.Notional});
      });
    }

    /// <summary>
    ///   Test the case First less than 1 for SemiAna;yticPricer.
    ///   Expect an exception.
    /// </summary>
    [Test]
    public void FirstLessThanOneSA()
    {
      Assert.Throws<ValidationException>(() =>
      {
        FTDPricer ntdPricer = (FTDPricer) pricers_[0, 0];
        var basket = ntdPricer.Basket as SemiAnalyticBasketForNtdPricer;
        FTD ntd = (FTD) ntdPricer.FTD.Clone();
        ntd = new FTD(ntd.Effective, ntd.Maturity, ntd.Ccy, ntd.Premium, ntd.DayCount,
          ntd.Freq, ntd.BDConvention, ntd.Calendar, 0, ntd.NumberCovered);
        FTDPricer[] pricers = BasketPricerFactory.NTDPricerSemiAnalytic(
          new FTD[] {ntd}, asOf_, settle_, ntdPricer.DiscountCurve, basket.SurvivalCurves,
          basket.Principals, basket.Copula, basket.Correlation,
          basket.StepSize, basket.StepUnit,
          basket.IntegrationPointsFirst,
          new double[] {ntdPricer.Notional});
      });
    }

    /// <summary>
    ///   Test the case First + Covers > Issuers for Monte Carlo Pricer.
    ///   Expect an exception.
    /// </summary>
    [Test]
    public void CoverMoreThanIssuersMC()
    {
      Assert.Throws<ValidationException>(() =>
      {
        FTDPricer ntdPricer = (FTDPricer) pricers_[0, 0];
        BasketForNtdPricer basket = ntdPricer.Basket;
        FTD ntd = (FTD) ntdPricer.FTD.Clone();
        ntd.NumberCovered = basket.Count - ntd.First + 2;
        FTDPricer[] pricers = BasketPricerFactory.NTDPricerMonteCarlo(
          new FTD[] {ntd}, asOf_, settle_, ntdPricer.DiscountCurve, basket.SurvivalCurves,
          basket.Principals, basket.Copula, basket.Correlation,
          basket.StepSize, basket.StepUnit,
          10000, new double[] {ntdPricer.Notional}, 0);
      });
    }

    /// <summary>
    ///   Test the case First less than 1 for Monte Carlo Pricer.
    ///   Expect an exception.
    /// </summary>
    [Test]
    public void FirstLessThanOneMC()
    {
      Assert.Throws<ValidationException>(() =>
      {
        FTDPricer ntdPricer = (FTDPricer) pricers_[0, 0];
        BasketForNtdPricer basket = ntdPricer.Basket;
        FTD ntd = (FTD) ntdPricer.FTD.Clone();
        ntd = new FTD(ntd.Effective, ntd.Maturity, ntd.Ccy, ntd.Premium, ntd.DayCount,
          ntd.Freq, ntd.BDConvention, ntd.Calendar, 0, ntd.NumberCovered);
        FTDPricer[] pricers = BasketPricerFactory.NTDPricerMonteCarlo(
          new FTD[] {ntd}, asOf_, settle_, ntdPricer.DiscountCurve, basket.SurvivalCurves,
          basket.Principals, basket.Copula, basket.Correlation,
          basket.StepSize, basket.StepUnit,
          10000, new double[] {ntdPricer.Notional}, 0);
      });
    }

    /// <summary>
    ///   The protection values of an NTD should be the same as an
    ///   equivalent CDO
    /// </summary>
    [Test, Smoke]
    public void ProtectionPvNTDvsCDO()
    {
      TestPrice("ProtectionPv", 1E-8);
    }

    [Test, Smoke]
    public void FeePvNTDvsCDO()
    {
      TestPrice("FeePv", 2E-6);
    }

    [Test, Smoke]
    public void PvNTDvsCDO()
    {
      TestPrice("Pv", 2E-6);
    }

    [Test, Smoke]
    public void AccruedNTDvsCDO()
    {
      TestPrice("Accrued", 1E-8);
    }
    #endregion // Tests

  }
}
