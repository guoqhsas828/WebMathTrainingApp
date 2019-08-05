//
// Copyright (c)    2018. All rights reserved.
//

//#define ZeroBreakEvenPremiumForFunded

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  
  [TestFixture, Smoke]
  public class TestBonusPayment : ToolkitTestBase
  {    
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
      //PushConfiguration();

      string filename_1 = GetTestFilePath(USD_LIBOR_DataFile_);
      USD_LIBOR_Data = (DiscountData)XmlLoadData(filename_1, typeof(DiscountData));
      string filename_2 = GetTestFilePath(EUR_LIBOR_DataFile_);
      EUR_LIBOR_Data = (DiscountData)XmlLoadData(filename_2, typeof(DiscountData));

      discountCurve_USD = USD_LIBOR_Data.GetDiscountCurve();
      pricingDate_ = Dt.FromStr(USD_LIBOR_Data.AsOf, "%D");
      settleDate_ = Dt.Add(pricingDate_, 1);

      string fileName_3 = GetTestFilePath(CreditDataFile_);
      creditData = (CreditData)XmlLoadData(fileName_3, typeof(CreditData));
      survCurves_ = creditData.GetSurvivalCurves(discountCurve_USD);

      return;
    }

    [OneTimeTearDown]
    public void Clean()
    {
      // Restore original configuration
//      PopConfiguration();
    }
    #endregion

    #region test
    [Test, Smoke]
    public void Test_CDS_Bonus()
    {
      CreateCDS(CdsType.Unfunded, 0.05);
      CreateCDSPricer();
      double feePv_1 = CDSPricers_[0].FeePv();
      double breakEvenPremium05 = CDSPricers_[0].BreakEvenPremium() * 10000.0;

      CreateCDS(CdsType.Unfunded, 0.00);
      CreateCDSPricer();
      double feePv_2 = CDSPricers_[0].FeePv();
      double breakEvenPremium00 = CDSPricers_[0].BreakEvenPremium() * 10000.0;

      double discountFactor = discountCurve_USD.DiscountFactor(pricingDate_, maturity_);
      double survivalProb = survCurves_[0].SurvivalProb(settleDate_,
        CDSPricers_[0].IncludeMaturityProtection ? Dt.Add(maturity_, 1) : maturity_);
      
      double bonus = 0.05 * CDSPricers_[0].Notional * discountFactor * survivalProb;
      AssertEqual("Bonus ", bonus, feePv_1 - feePv_2, 1e-5);

      // Bonus should not affect BE
      AssertEqual("BreakEven ", breakEvenPremium05, breakEvenPremium00);

      // Test breakeven premium roundtrip with and without bonus 
      double savedDealPremium = dealPremium_;
      dealPremium_ = breakEvenPremium00;
      CreateCDS(CdsType.Unfunded, 0.00);
      CreateCDSPricer();
      double cleanPv = CDSPricers_[0].Pv() - CDSPricers_[0].Accrued();
      AssertEqual("BreakEvenPremium Roundtrip without bonus", cleanPv, 0, 1e-5);

      CreateCDS(CdsType.Unfunded, 0.05);
      CreateCDSPricer();
      cleanPv = CDSPricers_[0].Pv() - CDSPricers_[0].Accrued();
      AssertEqual("BreakEvenPremium Roundtrip with bonus", bonus, cleanPv, 1e-5);
      
      //reset dealPremium
      dealPremium_ = savedDealPremium;
      
      // Test cashflow table with bonus
      double bonusRate = 0.05;
      CreateCDS(CdsType.Unfunded, 0.0);
      CreateCDSPricer();
      Cashflow cf_no_bonus = CDSPricers_[0].GenerateCashflow(null, CDSPricers_[0].AsOf);
      CreateCDS(CdsType.Unfunded, bonusRate);
      CreateCDSPricer();
      Cashflow cf_bonus = CDSPricers_[0].GenerateCashflow(null, CDSPricers_[0].AsOf);
      double cf_diff = cf_bonus.GetAmount(cf_bonus.Count - 1) - cf_no_bonus.GetAmount(cf_no_bonus.Count - 1);
      Assert.AreEqual(bonusRate, cf_diff, 1e-9, "cashflow wrong with bonus");
    }

    [Test, Smoke]
    public void Test_BasketCDS_Bonus()
    {
      CreateBasketCDS(BasketCDSType.Unfunded, 0.05);
      CreateBasketCDSPricer();
      double feePv_1 = basketCDSPricer_.FeePv();
      double breakEvenPremium05 = basketCDSPricer_.BreakEvenPremium() * 10000.0;

      CreateBasketCDS(BasketCDSType.Unfunded, 0.00);
      CreateBasketCDSPricer();
      double feePv_2 = basketCDSPricer_.FeePv();
      double breakEvenPremium00 = basketCDSPricer_.BreakEvenPremium() * 10000.0;

      double discountFactor = discountCurve_USD.DiscountFactor(pricingDate_, maturity_);
      double survivalProb = 0;
      for (int i = 0; i < survCurves_.Length; i++)
        survivalProb += survCurves_[i].SurvivalProb(settleDate_,
         ToolkitConfigurator.Settings.CDSCashflowPricer.IncludeMaturityProtection ? Dt.Add(maturity_, 1) : maturity_);
      survivalProb /= survCurves_.Length;

      double bonus = 0.05 * basketCDSPricer_.Notional * discountFactor * survivalProb;
      AssertEqual("Bonus ", bonus, feePv_1 - feePv_2, 1e-5);

      // Bonus should not affect BE
      AssertEqual("BreakEven ", breakEvenPremium05, breakEvenPremium00);

      // Test breakeven premium roundtrip with and without bonus 
      double savedDealPremium = dealPremium_;
      dealPremium_ = breakEvenPremium00;
      CreateBasketCDS(BasketCDSType.Unfunded, 0.00);
      CreateBasketCDSPricer();
      double cleanPv = basketCDSPricer_.Pv() - basketCDSPricer_.Accrued();
      AssertEqual("BreakEvenPremium Roundtrip without bonus", cleanPv, 0, 1e-5);
      
      CreateBasketCDS(BasketCDSType.Unfunded, 0.05);
      CreateBasketCDSPricer();
      cleanPv = basketCDSPricer_.Pv() - basketCDSPricer_.Accrued();
      AssertEqual("BreakEvenPremium Roundtrip with bonus", bonus, cleanPv, 1e-5);
 
      //reset dealPremium
      dealPremium_ = savedDealPremium;
      
    }

    [Test, Smoke]
    public void Test_NTD_Bonus()
    {
      // Test 1/1
      CreateNtd(NTDType.Unfunded, 1, 1, 0.05);
      CreateFtdPricer();
      double feePv_1 = ftdPricer_.FeePv();

      CreateNtd(NTDType.Unfunded, 1, 1, 0.00);
      CreateFtdPricer();
      double feePv_2 = ftdPricer_.FeePv();

      double discountFactor = discountCurve_USD.DiscountFactor(pricingDate_, maturity_);
      SurvivalCurve curve = ftdPricer_.NthSurvivalCurve(1);
      double survivalProb = curve.SurvivalProb(pricingDate_, maturity_);

      double bonus = 0.05 * ftdPricer_.Notional * discountFactor * survivalProb;
      AssertEqual("Bonus ", bonus, feePv_1 - feePv_2, 1e-5);

      // Test 1/2
      CreateNtd(NTDType.Unfunded, 1, 2, 0.05);
      CreateFtdPricer();
      feePv_1 = ftdPricer_.FeePv();

      CreateNtd(NTDType.Unfunded, 1, 2, 0.00);
      CreateFtdPricer();
      feePv_2 = ftdPricer_.FeePv();
      
      SurvivalCurve curve1 = ftdPricer_.NthSurvivalCurve(1);
      SurvivalCurve curve2 = ftdPricer_.NthSurvivalCurve(2);
      survivalProb = (curve1.SurvivalProb(pricingDate_, maturity_) + 
        curve2.SurvivalProb(pricingDate_, maturity_))/2;

      bonus = 0.05 * ftdPricer_.Notional * discountFactor * survivalProb;
      AssertEqual("Bonus ", bonus, feePv_1 - feePv_2, 1e-5);

      // Test the breakeven premium roundtrip with bonus
      CreateNtd(NTDType.Unfunded, 1, 1, 0.05);
      CreateFtdPricer();
      double bep05 = ftdPricer_.BreakEvenPremium() * 10000;
      CreateNtd(NTDType.Unfunded, 1, 1, 0.00);
      CreateFtdPricer();
      double bep00 = ftdPricer_.BreakEvenPremium() * 10000;

      // Bonus should not affect BE
      AssertEqual("Bonus doesn't affect BEP", bep05, bep00);

      // Roundtrip BEP with and without bonus
      double savedDealPremium = dealPremium_;
      dealPremium_ = bep00;
      CreateNtd(NTDType.Unfunded, 1, 1, 0.00);
      CreateFtdPricer();
      double cleanPv = ftdPricer_.Pv() - ftdPricer_.Accrued();
      Assert.AreEqual(0, cleanPv, 5E-10, "bep roundtrip failed");
      
      CreateNtd(NTDType.Unfunded, 1, 1, 0.05);
      CreateFtdPricer();
      cleanPv = ftdPricer_.Pv() - ftdPricer_.Accrued();
      curve = ftdPricer_.NthSurvivalCurve(1);
      survivalProb = curve.SurvivalProb(pricingDate_, maturity_);
      bonus = 0.05 * ftdPricer_.Notional * discountFactor * survivalProb;
      Assert.AreEqual(bonus, cleanPv, 1e-5, "bep roundtrip failed");
 
      // reset deal prem
      dealPremium_ = savedDealPremium;
      
      // Funded NTD should calc BEP as if Unfunded
      CreateNtd(NTDType.FundedFixed, 1, 1, 0.05);
      CreateFtdPricer();
      double bep_FundedFixed = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(bep00, bep_FundedFixed, 1e-5, "bep fundedfixed failed");

      CreateNtd(NTDType.FundedFloating, 1, 1, 0.05);
      CreateFtdPricer(new double[] { 0.05 });
      double bep_FundedFloating = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(bep00, bep_FundedFloating, 1e-5, "bep fundedfloating failed");
    }

    [Test, Smoke]
    public void Test_CDO_Bonus()
    {
      CreateCDO(CdoType.Unfunded, 0.05, 0, 0.03);
      CreateCDOPricer();
      double feePv_1 = cdoPricer_.FeePv();

      CreateCDO(CdoType.Unfunded, 0.00, 0, 0.03);
      CreateCDOPricer();
      double feePv_2 = cdoPricer_.FeePv();

      double discountFactor = discountCurve_USD.DiscountFactor(pricingDate_, maturity_);       
      double tranche = 0.03 - 0.00;
      double loss = Math.Min(1.0, cdoPricer_.Basket.AccumulatedLoss(maturity_, 0, 0.03) / tranche);
      double survivalProb = Math.Max(1 - loss, 0);
      double bonus = 0.05 * cdoPricer_.Notional * discountFactor * survivalProb;
      AssertEqual("Bonus ", bonus, feePv_1 - feePv_2, 1e-5);

      // Test the breakeven premium with and without bonus
      CreateCDO(CdoType.Unfunded, 0.05, 0, 0.03);
      CreateCDOPricer();
      double bep05 = cdoPricer_.BreakEvenPremium() * 10000;

      CreateCDO(CdoType.Unfunded, 0.00, 0, 0.03);
      CreateCDOPricer();
      double bep00 = cdoPricer_.BreakEvenPremium() * 10000;

      // Bonus should not affect BE
      AssertEqual("Bonus doesn't affect BEP", bep05, bep00);

      // BEP roundtrip without bonus
      double savedDealPremium = dealPremium_;
      dealPremium_ = bep00;
      CreateCDO(CdoType.Unfunded, 0.00, 0, 0.03);
      CreateCDOPricer();
      double target = cdoPricer_.Accrued() * cdoPricer_.DiscountCurve.DiscountFactor(cdoPricer_.AsOf, cdoPricer_.Settle);
      double calculated = cdoPricer_.Pv();
      Assert.AreEqual(target, calculated, 1e-2, "BEP roundtrip failed");

      // BEP roundtrip with bonus
      CreateCDO(CdoType.Unfunded, 0.05, 0, 0.03);
      CreateCDOPricer();
      double cleanPv = cdoPricer_.Pv() - (cdoPricer_.Accrued() * cdoPricer_.DiscountCurve.DiscountFactor(cdoPricer_.AsOf, cdoPricer_.Settle));
      Assert.AreEqual(cleanPv, bonus, 1e-2, "BEP roundtrip failed");

      dealPremium_ = savedDealPremium;

      // Test cashflow with bonus
      double bonusRate = 0.05;
      CreateCDO(CdoType.Unfunded, bonusRate, 0.03, 0.07);
      CreateCDOPricer();
      Cashflow cf_bonus = cdoPricer_.GenerateCashflow(null, cdoPricer_.AsOf);
      CreateCDO(CdoType.Unfunded, 0, 0.03, 0.07);
      CreateCDOPricer();
      Cashflow cf_no_bonus = cdoPricer_.GenerateCashflow(null, cdoPricer_.AsOf);
      double cf_diff = cf_bonus.GetAmount(cf_bonus.Count - 1) - cf_no_bonus.GetAmount(cf_no_bonus.Count - 1);
      Assert.AreEqual(bonusRate, cf_diff, 1e-5, "cashflow with bonus wrong");
    }
    #endregion test

    #region helpers
    private void CreateCDS()
    {
      CreateCDS(CdsType.Unfunded);
    }
    private void CreateCDS(CdsType type)
    {
      double bonusRate = 0;
      CreateCDS(type, bonusRate);
    }
    private void CreateCDS(CdsType type, double bonusRate)
    {
      bool payRecoveryAtMaturity = false;
      CreateCDS(type, bonusRate, payRecoveryAtMaturity);
    }
    private void CreateCDS(CdsType type, double bonusRate, bool payRecoveryMaturity)
    {
      CDS_ = new CDS[survCurves_.Length];
      for (int i = 0; i < survCurves_.Length; i++)
      {
        CDS_[i] = new CDS(effective_, maturity_, ccy_, dealPremium_ / 10000.0, dct_, freq_, roll_, cal_);
        CDS_[i].CdsType = type;
        CDS_[i].FirstPrem = CDS.GetDefaultFirstPremiumDate(effective_, maturity_);
        if (bonusRate > 0)
        {
          CDS_[i].Bullet = new BulletConvention(bonusRate);
        }
        CDS_[i].PayRecoveryAtMaturity = payRecoveryMaturity;
        CDS_[i].Description = "CDS " + type.ToString();
        //Since BE can be < 0 with bonus, do not call valiadte() 
        //CDS_[i].Validate();
      }
      return;
    }

    private void CreateCDSPricer()
    {
      CreateCDSPricer(0, null);
    }
    private void CreateCDSPricer(double currentCoupon)
    {
      CreateCDSPricer(currentCoupon, null);
    }
    private void CreateCDSPricer(SwapLeg recovFundCost)
    {
      CreateCDSPricer(0, recovFundCost);
    }
    private void CreateCDSPricer(double currentCoupon, SwapLeg recovFundCost)
    {
      CDSPricers_ = new CDSCashflowPricer[survCurves_.Length];
      for (int i = 0; i < survCurves_.Length; i++)
      {
        double correlation = 0;
        SurvivalCurve counterpartySurvivalCurve = null;
        if (survCurves_[i].SurvivalCalibrator != null)
        {
          counterpartySurvivalCurve = survCurves_[i].SurvivalCalibrator.CounterpartyCurve;
          correlation = survCurves_[i].SurvivalCalibrator.CounterpartyCorrelation;
        }
        CDSPricers_[i] = new CDSCashflowPricer(CDS_[i], pricingDate_, settleDate_, discountCurve_USD, survCurves_[i],
          counterpartySurvivalCurve, correlation, 0, TimeUnit.None);
        CDSPricers_[i].Notional = notional_;
        if (CDS_[i].CdsType == CdsType.FundedFloating)
        {
          //CDSPricers_[i].RateResets.Add(new RateReset(CDS_.Effective, Double.NaN));
          CDSPricers_[i].CurrentRate = currentCoupon;
        }
        CDSPricers_[i].RecoveryFunded = recovFundCost != null ? true : false;
        CDSPricers_[i].SwapLeg = recovFundCost;
        //Since BE can be < 0 with bonus, do not call valiadte() 
        // CDSPricers_[i].Validate();
      }
    }

    private void CreateBasketCDS()
    {
      CreateBasketCDS(BasketCDSType.Unfunded);
    }

    private void CreateBasketCDS(BasketCDSType type)
    {
      double bonusRate = 0;
      CreateBasketCDS(type, bonusRate);
    }

    private void CreateBasketCDS(BasketCDSType type, double bonusRate)
    {
      bool payRecovMaturity = false;
      CreateBasketCDS(type, bonusRate, payRecovMaturity);
    }

    private void CreateBasketCDS(BasketCDSType type, double bonusRate, bool payRecoveryMaturity)
    {
      // Create the basket cds
      basketCDS_ = new BasketCDS(effective_, maturity_, ccy_, dealPremium_ / 10000.0, dct_, freq_, roll_, cal_);
      basketCDS_.BasketCdsType = type;
      basketCDS_.FirstPrem = CDS.GetDefaultFirstPremiumDate(effective_, maturity_);
      if (bonusRate > 0)
      {
        basketCDS_.Bullet = new BulletConvention(bonusRate);
      }
      basketCDS_.PayRecoveryAtMaturity = payRecoveryMaturity;
      basketCDS_.Description = "BasketCDS " + type.ToString();
      //Since BE can be < 0 with bonus, do not call valiadte() 
      //basketCDS_.Validate();

      CdsType cdsType = CdsType.Unfunded;
      if (type == BasketCDSType.FundedFixed)
        cdsType = CdsType.FundedFixed;
      if (type == BasketCDSType.FundedFloating)
        cdsType = CdsType.FundedFloating;
      CreateCDS(cdsType, bonusRate, payRecoveryMaturity);
      return;
    }

    private void CreateBasketCDSPricer()
    {
      CreateBasketCDSPricer(0, null);
    }

    private void CreateBasketCDSPricer(double currentCoupon)
    {
      CreateBasketCDSPricer(currentCoupon, null);
    }

    private void CreateBasketCDSPricer(SwapLeg recovFundCost)
    {
      CreateBasketCDSPricer(0, recovFundCost);
    }

    private void CreateBasketCDSPricer(double currentCoupon, SwapLeg recovFundCost)
    {
      // Create basket cds pricer
      if (basketCDS_.BasketCdsType == BasketCDSType.FundedFloating && (discountCurve_USD == null || currentCoupon <= 0.0))
        throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate bond");

      basketCDSPricer_ = new BasketCDSPricer(basketCDS_, pricingDate_, settleDate_, discountCurve_USD, survCurves_);
      basketCDSPricer_.Notional = notional_;
      if (basketCDS_.BasketCdsType == BasketCDSType.FundedFloating)
        basketCDSPricer_.CurrentRate = currentCoupon;
      basketCDSPricer_.RecoveryFunded = recovFundCost != null ? true : false;
      basketCDSPricer_.SwapLeg = recovFundCost;
      if (basketCDS_.BasketCdsType == BasketCDSType.FundedFloating)
        basketCDSPricer_.CurrentRate = currentCoupon;

      double[] Weights = Array.ConvertAll<SurvivalCurve, double>
        (survCurves_, delegate(SurvivalCurve s) { return 1.0 / survCurves_.Length; });
      basketCDSPricer_.BasketCDS.Weights = Weights;
      //Since BE can be < 0 with bonus, do not call valiadte() 
      //basketCDSPricer_.Validate();

      CreateCDSPricer(currentCoupon, recovFundCost);
    }

    private void CreateNtd(NTDType type, int first, int numCovered, double bonusRate)
    {            
      ftd_ = new FTD(effective_, maturity_, ccy_, dealPremium_/10000,dct_, freq_, roll_, cal_);

      ftd_.First = first;
      ftd_.NumberCovered = numCovered;
      ftd_.Bullet = new BulletConvention(bonusRate);
      ftd_.Description = "NTD " + first.ToString() + " " + numCovered.ToString();
      ftd_.NtdType = type;
      ftd_.Validate();

      return;
    }

    private void CreateFtdPricer(params double[] lastResetCoupon)
    {
      Dt portfolioStart = Dt.Empty;      
      Copula copula = new Copula(CopulaType.Gauss, 2, 2);
      SurvivalCurve[] survivalCurves = new SurvivalCurve[10];
      for (int i = 0; i < 10; i++)
        survivalCurves[i] = survCurves_[i];
      string[] names = Array.ConvertAll<SurvivalCurve, string>(survivalCurves, delegate(SurvivalCurve c) { return c.Name; });
      SingleFactorCorrelation corr = new SingleFactorCorrelation(names, 0.5);

      // Create pricer
      List<RateReset> rateReset = null;
      if (ftd_.NtdType == NTDType.FundedFloating)
      {
        rateReset = new List<RateReset>();
        rateReset.Add(new RateReset(ftd_.Effective, lastResetCoupon[0]));
      }
      ftdPricer_ = BasketPricerFactory.NTDPricerSemiAnalytic(
        new FTD[] { ftd_ }, portfolioStart, pricingDate_, settleDate_,
        discountCurve_USD, survivalCurves, null, copula, corr,
        3, TimeUnit.Months, 50, new double[] { notional_ }, rateReset)[0];
      ftdPricer_.Validate();
    }

    private void CreateCDO(CdoType type, double bonusRate, double attachment, double detachment, params double[] fees)
    {
      cdo_ = new SyntheticCDO(effective_, maturity_,
        ccy_, dealPremium_ / 10000.0, dct_, freq_, roll_, cal_);
                  
      cdo_.Attachment = attachment;
      cdo_.Detachment = detachment;
      if (fees != null && fees.Length == 1 && fees[0] != 0)
      {
        cdo_.Fee = fees[0];
        cdo_.FeeSettle = effective_;
      }
      cdo_.CdoType = type;      
      cdo_.Bullet = new BulletConvention(bonusRate);
      cdo_.Description = "CDO " + attachment.ToString() + " " + detachment.ToString();
      cdo_.Validate();
    }

    private void CreateCDOPricer()
    {
      double accuracy = 0;
      int quadPoints = BasketPricerFactory.GetQuadraturePoints(ref accuracy);
      string[] names = Array.ConvertAll<SurvivalCurve, string>(survCurves_, delegate(SurvivalCurve c) { return c.Name; });
      SingleFactorCorrelation corr = new SingleFactorCorrelation(names, 0.5);
      Copula copulaObj = new Copula(CopulaType.Gauss, 2, 2);

      Dt portfolioStart = new Dt();
      cdoPricer_ =
        BasketPricerFactory.CDOPricerSemiAnalytic(
        new SyntheticCDO[] { cdo_ }, portfolioStart, pricingDate_, settleDate_,
        discountCurve_USD, new Dt[] { maturity_ }, survCurves_, null, copulaObj, corr,
        0, TimeUnit.Months, quadPoints, 0, new double[] { notional_ },
        false, false, null)[0];
      cdoPricer_.CounterpartySurvivalCurve = null;
      cdoPricer_.Basket.AccuracyLevel = accuracy;
      cdoPricer_.Validate();
    }

    private DiscountCurve GetFlatDiscountCurve(DiscountCurve discCurve, bool maturityDiscount)
    {
      DiscountCurve curve = null;
      if (!maturityDiscount)
      {
        curve = new DiscountCurve(pricingDate_, 0);
        return curve;
      }
      double dfMaturity = discCurve.DiscountFactor(maturity_);
      ArrayList tenorDates = new ArrayList();

      // Get the tenor dates
      tenorDates.Add(pricingDate_);
      tenorDates.Add(settleDate_);
      Cashflow cashflow = CDSPricers_[0].Cashflow;
      if (cashflow.Count > 0)
      {
        for (int i = 0; i < cashflow.Count; ++i)
          tenorDates.Add(cashflow.GetDt(i));
      }
      else
      {
        tenorDates.Add(maturity_);
        tenorDates.Add(Dt.Add(maturity_, 1));
      }
      Dt[] tenors = (Dt[])tenorDates.ToArray(typeof(Dt));

      // Set discount factors to be same as maturity discount factor
      double[] dfsMaturity = new double[tenors.Length];
      for (int i = 0; i < tenors.Length; ++i)
        dfsMaturity[i] = dfMaturity;

      DayCount dayCount = discCurve.DayCount;
      Frequency freq = discCurve.Frequency;

      DiscountRateCalibrator calibrator = new DiscountRateCalibrator(pricingDate_, pricingDate_);

      curve = new DiscountCurve(calibrator);
      curve.Interp = discCurve.Interp;
      curve.Ccy = discCurve.Ccy;
      curve.Category = discCurve.Category;
      curve.DayCount = dayCount;
      curve.Frequency = freq;

      curve.Set(tenors, dfsMaturity);

      return curve;
    }
    #endregion helpers

    #region data
    private string USD_LIBOR_DataFile_ = "data/Test_BasketCDS_USD_LIBOR_DataFile.xml";
    private string EUR_LIBOR_DataFile_ = "data/Test_BasketCDS_EUR_LIBOR_DataFile.xml";
    private DiscountData USD_LIBOR_Data = null;
    private DiscountData EUR_LIBOR_Data = null;
    protected DiscountCurve discountCurve_USD = null;
    protected DiscountCurve discountCurve_EUR = null;

    private string CreditDataFile_ = "data/Test_BasketCDS_Credits_DataFile.xml";
    private CreditData creditData = null;
    private SurvivalCurve[] survCurves_ = null;

    #region BasketCDS parameters
    private Dt effective_ = new Dt(28, 3, 2007);
    private Currency ccy_ = Currency.USD;
    private DayCount dct_ = DayCount.Actual360;
    private Frequency freq_ = Frequency.Quarterly;
    private BDConvention roll_ = BDConvention.Following;
    private Calendar cal_ = Calendar.NYB;
    private Dt maturity_ = new Dt(20, 6, 2012);
    private double dealPremium_ = 60.0;
    private CDS[] CDS_ = null;
    private BasketCDS basketCDS_ = null;
    private FTD ftd_ = null;
    private SyntheticCDO cdo_ = null;

    private double notional_ = 20000000;
    private Dt pricingDate_ = Dt.Empty;
    private Dt settleDate_ = Dt.Empty;
    private CDSCashflowPricer[] CDSPricers_ = null;
    private BasketCDSPricer basketCDSPricer_ = null;
    private FTDPricer ftdPricer_ = null;
    private SyntheticCDOPricer cdoPricer_ = null;
    #endregion BasketCS parameters
    #endregion data
  }
}