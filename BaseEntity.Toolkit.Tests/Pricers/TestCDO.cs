//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.IO;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDO pricers based on external credit data
  /// </summary>
  [TestFixture("CDO0000015 Sensitivities1")]
  [TestFixture("CDO0000015 Generic Sensitivities1",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("CDO0000015 Sensitivities1 ByTenor")]
  [TestFixture("CDO0000015 Sensitivities2")]
  [TestFixture("CDO0000015 Sensitivities2 ByTenor")]
  [TestFixture("SemiAnalytic Sensitivities1 ByTenor")]
  [TestFixture("SemiAnalytic Sensitivities2 ByTenor")]
  [TestFixture("TestCDO Heterogeneous with Cashflow")]
  [TestFixture("TestCDO Heterogeneous with CashflowStream")]
  [TestFixture("TestCDO Pricer Clayton")]
  [TestFixture("TestCDO Pricer DoubleT")]
  [TestFixture("TestCDO Pricer Frank")]
  [TestFixture("TestCDO Pricer Gumbel")]
  [TestFixture("TestCDO Pricer Homoegeneous")]
  [TestFixture("TestCDO Pricer StudentT")]
  [TestFixture("TestCDO SemiAnalytic Sensitivities2")]
  [TestFixture("TestCDO SemiAnalytic with Cashflow")]
  [TestFixture("TestCDO SemiAnalytic with CashflowStream")]
  [TestFixture("TestCDO Squared Pricer")]
  [TestFixture("TestCDO Tonga Correlation Mixed")]
  [TestFixture("TestCDO0000015 CounterpartyRisks")]
  [TestFixture("TestCDO0000015 FixedRecovery")]
  [TestFixture("TestCDO0000015 FixedRecovery w QCR")]
  [TestFixture("TestCDO0000015 GenericSensitivity")]
  [TestFixture("TestCDO0000015 Heterogeneous")]
  [TestFixture("TestCDO0000015 Heterogeneous with Defaulted Name")]
  [TestFixture("TestCDO0000015 QCR")]
  [TestFixture("TestCDO0000015 SemiAnalytic")]
  [TestFixture("TestCDO0000015 SemiAnalytic with Defaulted Name")]
  [TestFixture("TestCDO0000015 SemiAnalytic with Unsettled Default")]
  [TestFixture("TestCDO0020 Heterogeneous")]
  [TestFixture("TestCDO0020 SemiAnalytic")]
  [TestFixture("TestCDOScenarios")]
  [TestFixture("TestMixedSurfaceSensitivity")]
  [TestFixture("TestMixedSurfaceSensitivity2")]
  [TestFixture("TestMixedSurfaceSensitivity3")]
  [TestFixture("TestMixedSurfaceSensitivity-4")]
  [TestFixture("TestMixedSurfaceSensitivity-5")]
  [TestFixture("TestMixedSurfaceSensitivity-6")]
  [TestFixture("TestMixedSurfaceSensitivity-7")]
  [TestFixture("TestMixedSurfaceSensitivity-8")]
  [TestFixture("TestMixedSurfaceSensitivity-9")]
  public class TestCDO : TestCdoBase
  {
    public TestCDO(string name) : base(name)
    {}

    #region SetUP
    /// <summary>
    ///   Create an array of CDO Pricers
    /// </summary>
    /// <returns>CDO Pricers</returns>
    [OneTimeSetUp]
    public void SetUp()
    {
      CreatePricers();
    }

    #endregion // SetUp

    #region PricingMethods

    [Test, Category("PricingMethods")]
    public void AccumulatedLossException()
    {
      Assert.Throws<ArgumentException>(() =>
      {
        double attachment = cdoPricers_[0].CDO.Attachment;
        double detachment = cdoPricers_[0].CDO.Detachment + 1e-4;
        double accumLoss = cdoPricers_[0].Basket.AccumulatedLoss(settle_, attachment, detachment);
      });
    }

    [Test, Category("CCRCdoPricerDeserialize")]
    public void CCRCdoPricerDeserialize()
    {

      CcrPricer[] lazyPricers = Array.ConvertAll(cdoPricers_, CcrPricer.Get);
      Dt settle = settle_;
      CcrPricer[] lazyPricerClones = Array.ConvertAll(lazyPricers,
        p=>CloneUtil.CloneObjectGraph(p,CloneMethod.Serialization));
      for (int j = 0; j < lazyPricers.Length; ++j)
      {
        double pv = lazyPricers[j].FastPv(settle);
        double cpv = lazyPricerClones[j].FastPv(settle);
        Assert.AreEqual(pv, cpv, 1e-7, String.Format("{0}-{1}", cdoPricers_[j].CDO.Description, settle));
      }
    }


    [Test, Category("CCRCdoPricerFastPv")]
    public void FastPv()
    {

      CcrPricer[] lazyPricers = Array.ConvertAll(cdoPricers_, CcrPricer.Get);
      Dt settle = settle_;
      for (int j = 0; j < lazyPricers.Length; ++j)
      {
        double pv = cdoPricers_[j].Pv(settle);
        double fpv = lazyPricers[j].FastPv(settle);
        Assert.AreEqual(0.0, (fpv - pv)/pv, 1e-4, String.Format("{0}-{1}", cdoPricers_[j].CDO.Description, settle));
      }
    }


    [Test, Category("PricingMethods")]
    public void ProtectionPv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p) {
          return ((SyntheticCDOPricer)p).ProtectionPv(); 
        });
    }

    [Test, Category("PricingMethods")]
    public void FeePvWithPremium()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FeePv(100.0);
        });
    }

    [Test, Category("PricingMethods")]
    public void FlatFeePvWithPremium()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FlatFeePv(100.0);
        });
    }

    [Test, Category("PricingMethods")]
    public void UpFrontFeePv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).UpFrontFeePv();
        });
    }

    [Test, Category("PricingMethods")]
    public void FeePv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p) {
          return ((SyntheticCDOPricer)p).FeePv();
        });
    }

    [Test, Category("PricingMethods")]
    public void FullFeePv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FullFeePv();
        });
    }

    [Test, Category("PricingMethods")]
    public void FlatFeePv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FlatFeePv();
        });
    }

    [Test, Category("PricingMethods")]
    public void Pv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).Pv();
        });
    }

    [Test, Category("PricingMethods")]
    public void FlatPrice()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FlatPrice();
        });
    }

    [Test, Category("PricingMethods")]
    public void FullPrice()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FullPrice();
        });
    }

    [Test, Category("PricingMethods")]
    public void Accrued()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).Accrued();
        });
    }

    [Test, Category("PricingMethods")]
    public void BreakEvenPremium()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).BreakEvenPremium();
        });
    }

    [Test, Category("PricingMethods")]
    public void BreakEvenFee()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).BreakEvenFee();
        });
    }

    [Test, Category("PricingMethods")]
    public void Premium01()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).Premium01();
        });
    }

    [Test, Category("PricingMethods")]
    public void LossToDate()
    {
      Dt date = maturity_;
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).LossToDate(date);
        });
    }

    [Test, Category("PricingMethods")]
    public void NotionalToDate()
    {
      Dt date = maturity_;
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).NotionalToDate(date);
        });
    }

    [Test, Category("PricingMethods")]
    public void FeeToDate()
    {
      Dt date = maturity_;
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FeeToDate(date);
        });
    }

    [Test, Category("PricingMethods")]
    public void AccrualToDate()
    {
      Dt date = maturity_;
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).AccrualToDate(date);
        });
    }

    [Test, Category("PricingMethods")]
    public void RiskyDuration()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).RiskyDuration();
        });
    }

    [Test, Category("PricingMethods")]
    public void Carry()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).Carry();
        });
    }

    [Test, Category("PricingMethods")]
    public void MTMCarry()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).MTMCarry();
        });
    }

    [Test, Category("PricingMethods")]
    public void ExpectedLoss()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).ExpectedLoss();
        });
    }

    [Test, Category("PricingMethods")]
    public void ExpectedSurvival()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).ExpectedSurvival();
        });
    }

    [Test, Category("PricingMethods")]
    public void UpFrontValue()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).UpFrontValue(0.04);
        });
    }

    [Test, Category("PricingMethods")]
    public void ImpliedVolatility()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).ImpliedVolatility();
        });
    }

    [Test, Category("PricingMethods")]
    public void ImpliedVolatilityLogNormal()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).ImpliedVolatility(0.0, 0.0, false);
        });
    }

    [Test, Category("PricingMethods")]
    public void VaR()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).VaR(maturity_, 0.95, 0.0);
        });
    }

    [Test, Category("PricingMethods")]
    public void Test_ReferenceDifferentFromDiscount()
    {

      SyntheticCDOPricer pricer = (SyntheticCDOPricer)cdoPricers_[0].Clone();
      pricer.CDO.CdoType = CdoType.FundedFloating;
      pricer.RateResets.Add(new RateReset(new Dt(1,1,2000),0.03));
      pricer.CDO.Premium = 0.005;
      double pv = pricer.Pv();
      
      pricer.ReferenceCurve = pricer.DiscountCurve;
      double pvR = pricer.Pv();
      Assert.AreEqual(pv, pvR, 1e-16, "pv");
    }
    #endregion // PricingMethods

#if Not_Yet
    #region BasketMethods
    [Test, Category("BasketMethods")]
    public ResultData LossProbabilityDistribution()
    {
      Dt date = GetDistributionDate();
      double[] levels = GetDistributionLevels();
      Timer timer = new Timer();
      timer.Start();
      double[,] result = cdoPricers_[0].Basket.CalcLossDistribution(true, date, levels);
      timer.Stop();
      ResultData rd = ToResultData("LossProbabilityDistribution", result, null, timer.Elapsed);
      rd.Results[0].Name = "Levels";
      rd.Results[1].Name = "Probabilities";
      return rd;
    }

    [Test, Category("BasketMethods")]
    public ResultData BaseLossDistribution()
    {
      Dt date = GetDistributionDate();
      double[] levels = GetDistributionLevels();
      Timer timer = new Timer();
      timer.Start();
      double[,] result = cdoPricers_[0].Basket.CalcLossDistribution(false, date, levels);
      timer.Stop();
      ResultData rd = ToResultData("LossProbabilityDistribution", result, null, timer.Elapsed);
      rd.Results[0].Name = "Levels";
      rd.Results[1].Name = "Base Losses";
      return rd;
    }

    private Dt GetDistributionDate()
    {
      if (distributionDate_ == 0)
        return cdoPricers_[0].Maturity;
      else
        return new Dt(distributionDate_);
    }

    private double[] GetDistributionLevels()
    {
      if (distributionLevels_ == null || distributionLevels_.Length == 0)
      {
        return new double[] { cdoPricers_[0].CDO.Detachment };
      }

      return distributionLevels_;
    }

    public double[] DistributionLevels
    {
      get { return distributionLevels_; }
      set { distributionLevels_ = value; }
    }

    public int DistributionDate
    {
      get { return distributionDate_; }
      set { distributionDate_ = value; }
    }

    private double[] distributionLevels_ = null;
    private int distributionDate_ = 0;
    #endregion // BasketMethods
#endif

    #region SummaryRiskMethods
    [Test, Category("SummaryRiskMethods")]
    public void Spread01()
    {
      Spread01(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Spread01_RescaleStrikes()
    {
      Spread01(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadGamma()
    {
      SpreadGamma(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadGamma_RescaleStrikes()
    {
      SpreadGamma(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadHedge()
    {
      SpreadHedge(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void SpreadHedge_RescaleStrikes()
    {
      SpreadHedge(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void IR01()
    {
      IR01(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void IR01_RescaleStrikes()
    {
      IR01(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Recovery01()
    {
      Recovery01(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Recovery01_RescaleStrikes()
    {
      Recovery01(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void VOD()
    {
      VOD(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void VOD_RescaleStrikes()
    {
      VOD(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Theta()
    {
      Theta(cdoPricers_, cdoNames_);
    }

    [Test, Category("SummaryRiskMethods")]
    public void Theta_RescaleStrikes()
    {
      Theta(cdoPricers_, cdoNames_, rescaleStrikesArray_);
    }
    #endregion //SummaryRiskMethods

    #region RiskMethods
    [Test, Category("RiskMethods")]
    public void SpreadSensitivity()
    {
      Spread(cdoPricers_);
    }

    [Test, Category("SARiskMethods")]
    public void SpreadSemiAnalyticSensitivity()
    {
      bool canDo = SemiAnalyticDerProvider(cdoPricers_);
      if (!canDo) return;
      SpreadSemiAnalytic(cdoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void SpreadSensitivity_RescleStrikes()
    {
      Spread(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("SARiskMethods")]
    public void SpreadSensitivitySemiAnalytic_RescleStrikes()
    {
      bool canDo = SemiAnalyticDerProvider(cdoPricers_);
      if (!canDo) return;
      SpreadSemiAnalytic(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("SARiskMethods")]
    public void FiniteDifferenceCompareSA()
    {
      const int idx = 10;

      var cdoPricer = CloneUtil.CloneObjectGraph(cdoPricers_[0],CloneMethod.Serialization);
      var basket = cdoPricer.Basket as BaseCorrelationBasketPricer;
      if (basket != null)
        basket.RescaleStrike = false;
      var p = cdoPricer as IAnalyticDerivativesProvider;
      if (p.HasAnalyticDerivatives == false) return;
      var bcpb = cdoPricer.Basket;
      SurvivalCurve[] curves = bcpb.SurvivalCurves;
      int ten = 0;
      foreach (CurveTenor tenor in curves[idx].Tenors)
      {
        if (tenor.Maturity >= cdoPricers_[0].CDO.Maturity || ten == curves[idx].Tenors.Count - 1)
        {
          break;
        }
        ten++;
      }
      var ders = CreditAnalyticSensitivities.SemiAnalyticSensitivities(new[] {cdoPricer}, new[] {false});
      double pv = cdoPricer.Pv()/cdoPricer.Notional;
      bcpb.SurvivalCurves[idx].BumpQuote(ten, QuotingConvention.None, 1, BumpFlags.RefitCurve);
      bcpb.Reset();
      double pvp = cdoPricer.Pv()/cdoPricer.Notional;
      bcpb.SurvivalCurves[idx].BumpQuote(ten, QuotingConvention.None, -2, BumpFlags.RefitCurve);
      bcpb.Reset();
      double pvm = cdoPricer.Pv()/cdoPricer.Notional;
      double fd = (pvp - pvm)/(2*1e-4);
      double sd = (pvp - 2*pv + pvm)/(1e-8);
      Assert.AreEqual(0, (fd - ders[0].GetDerivatives(idx).Gradient[ten])/fd, 1e-2);
      Assert.AreEqual(0, (sd - ders[0].GetDerivatives(idx).Hessian[ten*(ten + 1)/2 + ten])/sd, 1e-1);
      bcpb.SurvivalCurves[idx].BumpQuote(ten, QuotingConvention.None, 1, BumpFlags.RefitCurve);
      bcpb.Reset();
    }

    [Test, Category("SARiskMethods")]
    public void FiniteDifferenceCompareSARS()
    {

      int idx = 10;
      var cdoPricer = CloneUtil.CloneObjectGraph(cdoPricers_[0],CloneMethod.Serialization);
      var basket = cdoPricer.Basket as BaseCorrelationBasketPricer;
      if (basket == null)
        return;
      basket.RescaleStrike = true;
      var p = cdoPricer as IAnalyticDerivativesProvider;
      if (p.HasAnalyticDerivatives == false) return;
      var bcpb = basket;
      SurvivalCurve[] curves = bcpb.SurvivalCurves;
      int ten = 0;
      foreach(CurveTenor tenor in curves[idx].Tenors)
      {
        if(tenor.Maturity >= cdoPricers_[0].CDO.Maturity || ten == curves[idx].Tenors.Count-1)
        {
          break;
        }
        ten++;
      }
      var ders = CreditAnalyticSensitivities.SemiAnalyticSensitivities(new[] {cdoPricer}, new[] {true});
      double pv = cdoPricer.Pv()/cdoPricer.Notional;
      bcpb.SurvivalCurves[idx].BumpQuote(ten, QuotingConvention.None, 1, BumpFlags.RefitCurve);
      bcpb.Reset();
      double pvp = cdoPricer.Pv()/cdoPricer.Notional;
      bcpb.SurvivalCurves[idx].BumpQuote(ten, QuotingConvention.None, -2, BumpFlags.RefitCurve);
      bcpb.Reset();
      double pvm = cdoPricer.Pv()/cdoPricer.Notional;
      double fd = (pvp - pvm)/(2*1e-4);
      double sd = (pvp - 2*pv + pvm)/(1e-8);
      Assert.AreEqual(0,(fd - ders[0].GetDerivatives(idx).Gradient[ten]) / fd, 1e-2);
      Assert.AreEqual(0,(sd - ders[0].GetDerivatives(idx).Hessian[ten * (ten + 1) / 2 + ten]) / sd, 1e-1);
      bcpb.SurvivalCurves[idx].BumpQuote(ten, QuotingConvention.None, 1, BumpFlags.RefitCurve);
      bcpb.Reset();
    }

    [Test, Category("SARiskMethods")]
    public void FiniteDifferenceCompareCorrelationSARS()
    {

      double tol = 5e-4;
      int idx = 45;
      var cdoPricer = CloneUtil.CloneObjectGraph(cdoPricers_[0],CloneMethod.Serialization);
      var basket = cdoPricer.Basket as BaseCorrelationBasketPricer;
      if (basket == null)
        return;
      basket.RescaleStrike = true;
      var pricer = cdoPricer as IAnalyticDerivativesProvider;
      if (pricer.HasAnalyticDerivatives == false) return;
      var bcbp = basket;
      SurvivalCurve[] curves = bcbp.SurvivalCurves;
      int ten = 0;
      foreach (CurveTenor tenor in curves[idx].Tenors)
      {
        if (tenor.Maturity >= cdoPricers_[0].CDO.Maturity || ten == curves[idx].Tenors.Count - 1)
        {
          break;
        }
        ten++;
      }
      int nObl = curves.Length;
      int tenJ = ten > 0 ? ten - 1 : (ten < curves[idx].Tenors.Count - 1) ? ten + 1 : ten;
      var bc = (BaseCorrelationObject) bcbp.Correlation;
      int n = curves[idx].Tenors.Count;
      int nT = n + n*(n + 1)/2 + 2;
      var retVal = new double[nObl*nT];
      double h = 1e-4;
      for (int i = 0; i < 2; i++)
      {
        var cdo = (SyntheticCDO) cdoPricer.CDO.Clone();
        if (i == 0)
        {
          cdo.Detachment = cdo.Attachment;
          cdo.Attachment = 0;
        }
        else
        {
          cdo.Attachment = 0;

        }
        bc.CorrelationDerivatives(bcbp.CreateDetachmentBasketPricer(true), cdoPricer.DiscountCurve,
                                  cdo, retVal);
        double p = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        double u = bcbp.SurvivalCurves[idx].GetVal(ten);
        bcbp.SurvivalCurves[idx].SetVal(ten, u + h);
        bcbp.Reset();
        double pp = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(ten, u - h);
        bcbp.Reset();
        double pm = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        Assert.AreEqual((pp - pm)/(2*h), retVal[idx*nT + ten], tol);
        Assert.AreEqual((pp - 2*p + pm)/(h*h), retVal[idx*nT + n + ten*(ten + 1)/2 + ten], tol);
        double v = bcbp.SurvivalCurves[idx].GetVal(tenJ);
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v - h);
        bcbp.Reset();
        double pmm = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v + h);
        bcbp.Reset();
        double pmp = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(ten, u + h);
        bcbp.Reset();
        double ppp = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v - h);
        bcbp.Reset();
        double ppm = (i == 0) ? bcbp.APCorrelation : bcbp.DPCorrelation;
        int min = Math.Min(ten, tenJ);
        int max = Math.Max(ten, tenJ);
        Assert.AreEqual(0.25*(ppp - ppm - pmp + pmm)/(h*h), retVal[idx*nT + n + max*(max + 1)/2 + min], tol);
        bcbp.SurvivalCurves[idx].SetVal(tenJ, v);
        bcbp.SurvivalCurves[idx].SetVal(ten, u);
        bcbp.Reset();
      }
    }


    [Test, Category("RiskMethods")]
    public void RateSensitivity()
    {
      Rate(cdoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivityWithMultipleDiscountCurves()
    {

      var pr = cdoPricers_[0];
      var foreign = pr.DiscountCurve.CloneObjectGraph(CloneMethod.FastClone);
      foreign.Name = "EURIBOR";
      pr.SurvivalCurves[0].SurvivalCalibrator.DiscountCurve = foreign;
      Rate(cdoPricers_, rescaleStrikesArray_);
      CreatePricers(); // Why we have this???
    }

    [Test, Category("RiskMethods")]
    public void RateSensitivity_RescaleStrikes()
    {
      Rate(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(cdoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity_RescaleStrikes()
    {
      Default(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("SARiskMethods")]
    public void DefaultSemiAnalyticSensitivity()
    {
      bool canDo = SemiAnalyticDerProvider(cdoPricers_);
      if (!canDo) return;
      DefaultSemiAnalytic(cdoPricers_);
    }

    [Test, Category("SARiskMethods")]
    public void DefaultSemiAnalyticSensitivity_RescaleStrikes()
    {

      bool canDo = SemiAnalyticDerProvider(cdoPricers_);
      if (!canDo) return;
      DefaultSemiAnalytic(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity()
    {
      Recovery(cdoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void RecoverySensitivity_RescaleStrikes()
    {
      Recovery(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("SARiskMethods")]
    public void RecoverySemiAnalyticSensitivity()
    {

      bool canDo = SemiAnalyticDerProvider(cdoPricers_);
      if (!canDo) return;
      RecoverySemiAnalytic(cdoPricers_);
    }

    [Test, Category("SARiskMethods")]
    public void RecoverySemiAnalyticSensitivity_RescaleStrikes()
    {
      bool canDo = SemiAnalyticDerProvider(cdoPricers_);
      if (!canDo) return;
      RecoverySemiAnalytic(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("RiskMethods")]
    public void CorrelationSensitivity()
    {
      Correlation(cdoPricers_);
    }

    // Specifically test the scenario should be 0 when a curve is 
    // defaulted in the portfolio and the scenario resets the same default
    [Test, Category("RiskMethods")]
    public void ScenarioWithDefault()
    {

      SyntheticCDOPricer CdoPricer = (SyntheticCDOPricer)cdoPricers_[0].Clone();
      CdoPricer.CDO.Attachment = 0.0;
      CdoPricer.CDO.Detachment = 0.1;      
      SurvivalCurve[] survCurves = CdoPricer.SurvivalCurves;
      Defaulted[] def = new Defaulted[survCurves.Length];
      
      // Let the first curve default
      Dt defaultDate = Dt.Add(asOf_, -20);
      Dt dfltSettle = Dt.Add(asOf_, -10);
      survCurves[0].SetDefaulted(defaultDate, true);
      survCurves[0].SurvivalCalibrator.RecoveryCurve.JumpDate = dfltSettle;
      CdoPricer.Reset();      
      
      // Do the scenario specifically use "defaulted" for the same curve
      var shift = new ScenarioShiftDefaults(new SurvivalCurve[] { survCurves[0] });
      var results = Scenarios.CalcScenario(new[] { CdoPricer }, "Pv", new[] { shift }, true, true);

      Assert.AreEqual(0, results[0], 1e-5,
        "Failed scenario =0 test for setting same defaulted curve default");


      // Test the failure of scenario analysis for ByName combined correlation surface
      // Load and get the discount curve and credit curves
      DiscountCurve discountCurve = LoadDiscountCurve("data/IRCurveScenarioWithDefault.xml");
      SurvivalCurve[] survivalCurves = LoadCreditCurves("data/CreditDataScenarioWithDefault.xml", discountCurve);
      // Load and get the base correlation term struct; and ByName combine them      
      BaseCorrelationObject corrObj1 = (BaseCorrelationObject)LoadCorrelationObject("data/BaseCorrelation_1_ScenarioWithDefault.xml");
      BaseCorrelationObject corrObj2 = (BaseCorrelationObject)LoadCorrelationObject("data/BaseCorrelation_2_ScenarioWithDefault.xml");
      BaseCorrelationObject corrObj3 = (BaseCorrelationObject)LoadCorrelationObject("data/BaseCorrelation_3_ScenarioWithDefault.xml");
      BaseCorrelationObject bco = BaseCorrelationJoinSurfaceUtil.CombineBaseCorrelations(
        new BaseCorrelationObject[] { corrObj1, corrObj2, corrObj3 }, new double[] { 0.55, 0.35, 0.10 },
        BaseEntity.Toolkit.Numerics.InterpMethod.PCHIP, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
        BaseEntity.Toolkit.Numerics.InterpMethod.PCHIP, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const, Double.NaN, Double.NaN,
        BaseCorrelationCombiningMethod.ByName, new string[] { "TSCO", "UBS", "ULVR" },
        new BaseCorrelationObject[] { corrObj1, corrObj2, corrObj3 });

      // Build a CDO product and a CDO pricer with the ByName combined base correlation surface
      Dt pricingDate = discountCurve.AsOf;
      Dt effectiveDate = new Dt(1, 9, 2005), maturity = new Dt(20, 12, 2015);
      SyntheticCDO cdo = new SyntheticCDO(effectiveDate, Dt.Empty, maturity, Currency.USD, DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB, 500/10000.0, 0.55, 0, 0.1);
      SyntheticCDOPricer cdoPricer = BasketPricerFactory.CDOPricerSemiAnalytic(
        new SyntheticCDO[]{cdo}, Dt.Empty, pricingDate, Dt.Add(pricingDate, 1),
        discountCurve, new Dt[]{maturity}, survivalCurves, null, new Copula(CopulaType.Gauss, 2, 2),
        bco, 3, TimeUnit.Months, 100, 0, new double[] { 10000000.0 }, false, false, null)[0];

      // Calculate some measures before doing scenario 
      double pv = cdoPricer.Pv();
      double riskyDuration = cdoPricer.RiskyDuration();

      // Do the scenario specifically use "defaulted" for the same curve
      ArgumentException exception = null;
      try
      {
        var shift2 = new ScenarioShiftDefaults(new SurvivalCurve[] { survCurves[0] });
        results = Scenarios.CalcScenario(new[] { CdoPricer }, "Pv", new[] { shift2 }, true, true);
      }
      catch (Exception e)
      {
        exception = e as ArgumentException;
      }
      finally
      {
        if (exception == null)
        {
          // Calculate some measures after doing scenario
          double pv2 = cdoPricer.Pv();
          double riskyDuration2 = cdoPricer.RiskyDuration();
          //Assert.AreEqual(Double.NaN, Double.NaN, "passed");
          Assert.AreEqual( pv, pv2, 1e-5, "failed");
          Assert.AreEqual( riskyDuration, riskyDuration2, 1e-5, "failed");
        }
        else
          Assert.AreEqual("no exception", exception.ToString());
      }

      return;
    }

    #endregion // RiskMethods
  }


  [TestFixture]
  public class TestCDOEx
  {
    [Test]
    public void TestMergeVsJoint()
    {
      var pathJoint = Path.Combine(SystemContext.InstallDir, @"toolkit\test\data\37pricerjoint.xml");
      var pathMerge = Path.Combine(SystemContext.InstallDir, @"toolkit\test\data\37pricermerge.xml");
      
      var pricerJ = XmlSerialization.ReadXmlFile(pathJoint) as SyntheticCDOPricer;
      var pricerM = XmlSerialization.ReadXmlFile(pathMerge) as SyntheticCDOPricer;
      
      if (pricerJ != null && pricerM != null)
      {
        var bcJoint = GetMixedBaseCorrelation(BaseCorrelationCombiningMethod.JoinSurfaces);
        var pricerJoint = GetPricers(pricerJ, bcJoint);

        var bcMerge = GetMixedBaseCorrelation(BaseCorrelationCombiningMethod.MergeSurfaces);
        var pricerMerge = GetPricers(pricerM, bcMerge);

        var prot1 = pricerJoint[0].ProtectionPv();
        var prot2 = pricerMerge[0].ProtectionPv();
        Assert.AreEqual(prot1, prot2, 1E-14,"Protection Pv");

        var pvJoint = pricerJoint[0].Pv();
        var pvMerge = pricerMerge[0].Pv();
        Assert.AreEqual(pvJoint, pvMerge, 1E-14, "Pv");

        var bepJoint = pricerJoint[0].BreakEvenPremium();
        var bepMerge = pricerMerge[0].BreakEvenPremium();
        Assert.AreEqual(bepJoint, bepMerge, 1E-14, "Break even premium");
      }
    }


    private SyntheticCDOPricer[] GetPricers(SyntheticCDOPricer pricer, 
      BaseCorrelationObject corr)
    {
      return BasketPricerFactory.CDOPricerSemiAnalytic(
        new []{pricer.CDO}, pricer.Basket.PortfolioStart, pricer.AsOf, pricer.Settle,
        pricer.DiscountCurve, pricer.ReferenceCurve, null, pricer.SurvivalCurves, 
        pricer.Principals, pricer.Copula, corr, pricer.StepSize, pricer.StepUnit, 
        4, pricer.Basket.GridSize, new []{pricer.Notional},
         false, false, null);
    }

    private BaseCorrelationObject GetMixedBaseCorrelation(
      BaseCorrelationCombiningMethod combinedMethod)
    {
      var correlationPath = Path.Combine(SystemContext.InstallDir,
        @"toolkit\test\data\BaseCorrelationIG25.xml");
      var bc = XmlSerialization.ReadXmlFile(correlationPath) as BaseCorrelationTermStruct;
      if (bc != null)
      {
        BaseCorrelationObject bco = BaseCorrelationJoinSurfaceUtil
          .CombineBaseCorrelations( new[] {bc}, new[] {1.0},
          InterpMethod.PCHIP, ExtrapMethod.Const,
          InterpMethod.PCHIP, ExtrapMethod.Const,
          double.NaN, double.NaN, combinedMethod, null, null);
        bco.Name = "BaseCorrelation" + combinedMethod.ToString();
        return bco;
      }

      return null;
    }
  } 
} // namespace
