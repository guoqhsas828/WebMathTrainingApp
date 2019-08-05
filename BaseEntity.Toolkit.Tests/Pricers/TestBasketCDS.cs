//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestBasketCDS : ToolkitTestBase
  {      
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
//      PushConfiguration();

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
      //PopConfiguration();
    }
    #endregion

    #region tests
    [Test, Smoke]
    public void Test_BasketCDS_Pv()
    {
      TestCDSBasketCDSEquivalency(
        delegate(IPricer p) { return ((BasketCDSPricer)p).Pv(); }, 
        delegate(IPricer p) { return ((CDSCashflowPricer)p).Pv(); }, "Pv");
    }

    [Test, Smoke]
    public void Test_BasketCDS_ProtectionPv()
    {
      TestCDSBasketCDSEquivalency(
       delegate(IPricer p) { return ((BasketCDSPricer)p).ProtectionPv(); },
       delegate(IPricer p) { return ((CDSCashflowPricer)p).ProtectionPv(); }, "ProtectionPv");
    }

    [Test, Smoke]
    public void Test_BasketCDS_FeePv()
    {
      TestCDSBasketCDSEquivalency(
       delegate(IPricer p) { return ((BasketCDSPricer)p).FeePv(); },
       delegate(IPricer p) { return ((CDSCashflowPricer)p).FeePv(); }, "FeePv");

      TestCDSBasketCDSEquivalency(
             delegate(IPricer p) { return ((BasketCDSPricer)p).FeePv(); },
             delegate(IPricer p) { return ((CDSCashflowPricer)p).FeePv(); }, "FeePv", true);
    }

    [Test, Smoke]
    public void Test_BasketCDS_Accrued()
    {
      TestCDSBasketCDSEquivalency(
       delegate(IPricer p) { return ((BasketCDSPricer)p).Accrued(); },
       delegate(IPricer p) { return ((CDSCashflowPricer)p).Accrued(); }, "Accrued");
    }

    [Test, Smoke]
    public void Test_BasketCDS_Duration()
    {
      TestCDSBasketCDSEquivalency(
       delegate(IPricer p) { return ((BasketCDSPricer)p).RiskyDuration(); },
       delegate(IPricer p) { return ((CDSCashflowPricer)p).RiskyDuration(); }, "RiskyDuration");
    }

    [Test, Smoke]
    public void Test_BasketCDS_BreakevenFee()
    {
      upfrontFee_ = 0.01;
      CreateBasketCDS(BasketCDSType.Unfunded);
      CreateBasketCDSPricer();

      double BEF_Unfunded = basketCDSPricer_.BreakevenFee(Double.NaN);
      double savedUpfrontFee = basketCDS_.Fee;
      upfrontFee_ = BEF_Unfunded;
      CreateBasketCDS(BasketCDSType.Unfunded);
      CreateBasketCDSPricer();
      upfrontFee_ = 0.0;
      Assert.AreEqual(0, 
        (basketCDSPricer_.Pv()-basketCDSPricer_.Accrued())/notional_, 1e-6, "Unfunded BEF roundtrip failed");
      
      // Test FundedFixed BEF is same as Unfunded
      upfrontFee_ = 0.01;
      CreateBasketCDS(BasketCDSType.FundedFixed);
      CreateBasketCDSPricer();
      upfrontFee_ = 0.0;
      double BEF_FundedFixed = basketCDSPricer_.BreakevenFee(Double.NaN);
      Assert.AreEqual(BEF_Unfunded, BEF_FundedFixed, 1e-6, "FundedFixed BEF failed to be same as Unfunded");

      // Test FundedFloating BEF is same as Unfunded
      CreateBasketCDS(BasketCDSType.FundedFloating);
      CreateBasketCDSPricer(0.05);
      upfrontFee_ = 0.0;
      double BEF_FundedFloating = basketCDSPricer_.BreakevenFee(Double.NaN);
      Assert.AreEqual(BEF_Unfunded, BEF_FundedFloating, 1e-6, "FundedFloating BEF failed to be same as Unfunded");
    }

    [Test, Smoke]
    public void Test_BasketCDS_BreakEvenPremium()
    {
      CreateBasketCDS(BasketCDSType.Unfunded);
      CreateBasketCDSPricer();

      double duration = 0, sumDuration = 0, sumBE = 0;
      for (int i = 0; i < survCurves_.Length; i++)
      {
        duration = CDSPricers_[i].RiskyDuration();
        sumDuration += (basketCDS_.Weights == null ? (1.0 / survCurves_.Length) : basketCDS_.Weights[i]) * duration;
        sumBE += (basketCDS_.Weights == null ? (1.0 / survCurves_.Length) : basketCDS_.Weights[i]) * 
          CDSPricers_[i].BreakEvenPremium() * duration;
      }
      double theBE = sumBE / sumDuration * 10000;
      double BE = basketCDSPricer_.BreakEvenPremium()*10000;
      AssertEqual("Unfunded duration-weighted BE", BE, theBE, 1e-5);
      theBE = -basketCDSPricer_.ProtectionPv() / basketCDSPricer_.RiskyDuration() / basketCDSPricer_.Notional * 10000;
      AssertEqual("Unfunded BE", BE, theBE, 1e-5);

      double savedDealPremium = dealPremium_;
      dealPremium_ = BE;
      CreateBasketCDS(BasketCDSType.Unfunded);
      CreateBasketCDSPricer();
      double accrual = basketCDSPricer_.Accrued();
      double pv = basketCDSPricer_.Pv();
      dealPremium_ = savedDealPremium;
      AssertEqual("Roundtrip unfunded BE", accrual, pv, 1e-5);

      // Test the FundedFixed BEP is same as Unfunded case
      CreateBasketCDS(BasketCDSType.FundedFixed);
      CreateBasketCDSPricer();
      double BE_FundedFixed = basketCDSPricer_.BreakEvenPremium() * 10000;
      AssertEqual("FundedFixed BEP", BE_FundedFixed, BE, 1e-5);

      // Test the FundedFloating BEP is same as Unfunded case
      CreateBasketCDS(BasketCDSType.FundedFixed);
      CreateBasketCDSPricer(0.05);
      double BE_FundedFloating = basketCDSPricer_.BreakEvenPremium() * 10000;
      AssertEqual("FundedFloating BEP", BE_FundedFloating, BE, 1e-5);
    }
    
    [Test, Smoke]
    public void Test_BasketCDS_Carry()
    {
      TestCDSBasketCDSEquivalency(
       delegate(IPricer p) { return ((BasketCDSPricer)p).Carry(); },
       delegate(IPricer p) { return ((CDSCashflowPricer)p).Carry(); }, "Carry");
    }

    [Test, Smoke]
    public void Test_BasketCDS_PayRecovAtMaturity()
    {
      bool payRecovAtMaturity = true;
      CreateBasketCDS(BasketCDSType.Unfunded, 0, payRecovAtMaturity);
      CreateBasketCDSPricer();
      double protectionPv_BasketCDS = basketCDSPricer_.ProtectionPv();
      double protectionPv_CDS = 0;
      double[] weights = basketCDSPricer_.BasketCDS.Weights == null ? 
        Array.ConvertAll<SurvivalCurve, double>(survCurves_, delegate(SurvivalCurve c){return 1.0/survCurves_.Length;})
        : basketCDSPricer_.BasketCDS.Weights;
      for (int i = 0; i < survCurves_.Length; i++)
        protectionPv_CDS += CDSPricers_[i].ProtectionPv()*weights[i];

      AssertEqual("ProtectionPv pay recov at maturity", protectionPv_CDS, protectionPv_BasketCDS, 1e-5);

      // Test using flat discount curve
      payRecovAtMaturity = true;
      CreateBasketCDS(BasketCDSType.Unfunded, 0, payRecovAtMaturity);
      CreateCDSPricer();
      double[] protectionPvs_1 = new double[survCurves_.Length];
      for (int i = 0; i < survCurves_.Length; i++)
        protectionPvs_1[i] = CDSPricers_[i].ProtectionPv();

      payRecovAtMaturity = false;
      CreateBasketCDS(BasketCDSType.Unfunded, 0, payRecovAtMaturity);
      DiscountCurve flatDiscCurve = GetFlatDiscountCurve(discountCurve_USD, false);
      double maturityDiscountFactor = discountCurve_USD.DiscountFactor(pricingDate_, maturity_);
      DiscountCurve savedDiscountCurve = (DiscountCurve)discountCurve_USD.Clone();
      discountCurve_USD = flatDiscCurve;
      CreateCDSPricer();
      double[] protectionPvs_2 = new double[survCurves_.Length];
      for (int i = 0; i < survCurves_.Length; i++)
        protectionPvs_2[i] = CDSPricers_[i].ProtectionPv()*maturityDiscountFactor;
      discountCurve_USD = savedDiscountCurve;

      for (int i = 0; i < survCurves_.Length; i++)
        AssertEqual("Protection Pv " + (i + 1).ToString(), protectionPvs_2[i], protectionPvs_1[i], 1e-5);
    }

    [Test, Smoke]
    public void Test_CDS_BasketCDS_FundedCostOfRecovery()
    {
      // The type must be funded
      CreateCDS(CdsType.FundedFixed, 0, false);
      CreateCDSPricer();

      int pricerIndex = 1;
      CDSCashflowPricer cdsPricer = CDSPricers_[pricerIndex];

      Cashflow cf = cdsPricer.Cashflow;
      Dt[] dates = new Dt[cf.Count];

      Dt[] swapEffectiveDates = new Dt[cf.Count];
      double[] discountFactors = new double[cf.Count];
      double[] defaultProbs = new double[cf.Count];
      double[] discountFactorToSettle = new double[cf.Count];
      double[] swapLegPv = new double[cf.Count];
      double[] defaultAmount = new double[cf.Count];

      // Set up the effective dates for swaplegs and compute discount factors up to effective dates
      swapEffectiveDates[0] = pricingDate_;
      discountFactors[0] = discountCurve_USD.DiscountFactor(pricingDate_, swapEffectiveDates[0]);
      for (int i = 1; i < swapEffectiveDates.Length; i++)
      {
        swapEffectiveDates[i] = cf.GetDt(i - 1);
        discountFactors[i] = discountCurve_USD.DiscountFactor(pricingDate_, swapEffectiveDates[i]);
      }

      // Get average discount factors, from settlement date
      double dis1 = 1;
      double dis2 = 1;
      for (int i = 0; i < discountFactorToSettle.Length; i++)
      {
        dis2 = discountCurve_USD.DiscountFactor(pricingDate_, cf.GetDt(i)) /
          discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
        discountFactorToSettle[i] = (dis1 + dis2) / 2;
        dis1 = dis2;
      }

      // Get default probability, conditioned on survival up to settlement date
      double survToSettle = survCurves_[pricerIndex].SurvivalProb(pricingDate_, settleDate_);
      double prob1 = survToSettle;
      double prob2 = 0;
      for (int i = 0; i < defaultProbs.Length; i++)
      {
        Dt dat = cf.GetDt(i);
        if (i == defaultProbs.Length - 1 && cdsPricer.IncludeMaturityProtection)
          dat = Dt.Add(dat, 1);
        prob2 = survCurves_[1].SurvivalProb(pricingDate_, dat);
        defaultProbs[i] = (prob1 - prob2) / survToSettle;
        prob1 = prob2;
      }

      double FundingSpread = 75.0;
      SwapLeg[] swapLegs = CreateSwapLeg(swapEffectiveDates, maturity_, FundingSpread, true);
      SwapLegPricer[] swapLegPricers = CreateSwapLegPricer(swapLegs);
      swapLegPv[0] = swapLegPricers[0].Pv();
      for (int i = 1; i < swapLegs.Length; i++)
      {
        swapLegPv[i] = swapLegPricers[i].Pv();
        defaultAmount[i - 1] = (swapLegPv[i - 1] + swapLegPv[i]) / (discountFactors[i - 1] + discountFactors[i]);
        defaultAmount[i - 1] *= cf.GetDefaultAmount(i - 1);
      }
      defaultAmount[swapLegPricers.Length - 1] = swapLegPv[swapLegPricers.Length - 1] / discountFactors[swapLegPricers.Length - 1];
      defaultAmount[swapLegPricers.Length - 1] *= cf.GetDefaultAmount(swapLegPricers.Length - 1);

      double sum = 0;
      for (int i = 0; i < defaultAmount.Length; i++)
        sum += defaultAmount[i] * discountFactorToSettle[i] * defaultProbs[i];
      sum *= discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
      sum *= notional_;

      // Get the SwapLeg and set it into cds pricer
      SwapLeg swap = CreateSwapLeg(cdsPricer.CDS.Effective, maturity_, FundingSpread, true);
      CreateCDSPricer(swap);
      cdsPricer = CDSPricers_[pricerIndex];
      cdsPricer.RecoveryFunded = true;
      double protectionPv = cdsPricer.ProtectionPv();

      // Assert.AreEqual("CDS FundedCost", sum, protectionPv, 1, "CDS FundedCost failed");

      // Test the BasketCDS protection with FundedCost
      sum = 0;
      CreateBasketCDS(BasketCDSType.FundedFixed, 0, false);
      CreateBasketCDSPricer(swap);
      double[] weights = basketCDS_.Weights;
      for (int i = 0; i < survCurves_.Length; i++)
        sum += (CDSPricers_[i].ProtectionPv() *
          (weights == null || weights.Length == 0 ? 1.0 / survCurves_.Length : weights[i]));
      Assert.AreEqual(sum, basketCDSPricer_.ProtectionPv(), 1, "Basket CDS FundedCost failed");
    }

    [Test, Smoke]
    public void Test_CDS_BasketCDS_AccruedOnDefault()
    {
      // This test the FeePv under cases accruedOnDefault = true and false.
      // The FeePv = clear feepv + accrued on default
      // If AccruedOnDefault= true, accrued on default is added
      // If AccruedOnDefault= true, accrued on default is 0

      Dt savedEffective = effective_;
      effective_ = settleDate_;

      int index = 2;
      CreateCDS(CdsType.Unfunded, 0, false);

      // Set the AccruedOnDefault flag to be false for clear feepv
      CDS_[index].AccruedOnDefault = false;
      CreateCDSPricer();
      double clearFeePv = CDSPricers_[index].FeePv();
      Cashflow cf = CDSPricers_[index].Cashflow;

      // Set the AccruedOnDefault falg to be true 
      CDS_[index].AccruedOnDefault = true;
      CreateCDSPricer();
      double FeePv = CDSPricers_[index].FeePv();
      double diff = FeePv - clearFeePv;

      // Calculate the accrual on default from cashflow
      double discountFactorSettle = discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
      double survivalProbSettle = survCurves_[index].SurvivalProb(pricingDate_, settleDate_);
      double[] discountFactors = new double[cf.Count];
      double[] survivalProbs = new double[cf.Count];
      double[] avgDiscountFactors = new double[cf.Count];
      double[] defaultProbs = new double[cf.Count];
      double[] fraction = new double[cf.Count];
      double[] accrual = new double[cf.Count];
      double[] accrualOnDefaultAmount = new double[cf.Count];
      double sum = 0;
      Dt[] paymentDate = new Dt[cf.Count];
      Dt[] accrualStart = new Dt[cf.Count];
      for (int i = 0; i < cf.Count; i++)
      {
        paymentDate[i] = cf.GetDt(i);
        accrualStart[i] = i == 0 ? cf.Effective : paymentDate[i - 1];
        fraction[i] = ((int)(1.0 + 0.5 * Dt.Diff(accrualStart[i], paymentDate[i]))) /
          (double)Dt.Diff(accrualStart[i], paymentDate[i]);
        if (i == cf.Count - 1)
          fraction[i] = ((int)(1.0 + 0.5 * (1 + Dt.Diff(accrualStart[i], paymentDate[i])))) /
          ((double)Dt.Diff(accrualStart[i], paymentDate[i]) + 1);
        discountFactors[i] = discountCurve_USD.DiscountFactor(pricingDate_, paymentDate[i]) / discountFactorSettle;
        avgDiscountFactors[i] = ((i == 0 ? 1 : discountFactors[i - 1]) + discountFactors[i]) / 2;
        int j = 0;
        if (i == cf.Count - 1 && CDSPricers_[index].IncludeMaturityProtection)
          j = 1;
        survivalProbs[i] = survCurves_[index].SurvivalProb(pricingDate_, Dt.Add(paymentDate[i], j)) / survivalProbSettle;
        defaultProbs[i] = (i == 0 ? 1 : survivalProbs[i - 1]) - survivalProbs[i];
        accrual[i] = cf.GetAccrued(i);
        accrualOnDefaultAmount[i] = accrual[i] * avgDiscountFactors[i] * defaultProbs[i] * fraction[i];
        sum += accrualOnDefaultAmount[i];
      }

      sum *= (discountFactorSettle * CDSPricers_[index].Notional);
      //effective_ = savedEffective;
      Assert.AreEqual(diff, sum, 1e-5, "CDS AccruedOnDefault Failed");
      effective_ = savedEffective;
    }

    [Test, Smoke]
    public void Test_BasketCDS_Premium01()
    {
      // Premium01 is the change of pv: Pv(Premium + delta) - Pv(Premium)
      CreateBasketCDS();
      CreateBasketCDSPricer();
      double Pv0 = basketCDSPricer_.Pv();
      double premium01 = basketCDSPricer_.FwdPremium01(settleDate_);
      double savedDealPremium = dealPremium_;
      dealPremium_ += 1.0;
      CreateBasketCDS();
      CreateBasketCDSPricer();
      dealPremium_ = savedDealPremium;
      double Pv1 = basketCDSPricer_.Pv();      
      Assert.AreEqual(1, (Pv1 - Pv0)/premium01, 1e-3, "premium01 failed");

      CreateBasketCDS(BasketCDSType.FundedFixed);
      CreateBasketCDSPricer();
      Pv0 = basketCDSPricer_.Pv();
      premium01 = basketCDSPricer_.FwdPremium01(settleDate_);
      savedDealPremium = dealPremium_;
      dealPremium_ += 1.0;
      CreateBasketCDS(BasketCDSType.FundedFixed);
      CreateBasketCDSPricer();
      dealPremium_ = savedDealPremium;
      Pv1 = basketCDSPricer_.Pv();
      Assert.AreEqual(1, (Pv1 - Pv0) / premium01, 1e-3, "premium01 failed");

      CreateBasketCDS(BasketCDSType.FundedFloating);
      CreateBasketCDSPricer(0.05);
      Pv0 = basketCDSPricer_.Pv();
      premium01 = basketCDSPricer_.FwdPremium01(settleDate_);
      savedDealPremium = dealPremium_;
      dealPremium_ += 1.0;
      CreateBasketCDS(BasketCDSType.FundedFloating);
      CreateBasketCDSPricer(0.05);
      dealPremium_ = savedDealPremium;
      Pv1 = basketCDSPricer_.Pv();
      Assert.AreEqual(1, (Pv1 - Pv0) / premium01, 1e-3, "premium01 failed");
    }

    [Test, Smoke]
    public void Test_BasketCDS_VOD()
    {
      BasketCDSType[] types = new BasketCDSType[3];
      types[0] = BasketCDSType.Unfunded;
      types[1] = BasketCDSType.FundedFixed;
      types[2] = BasketCDSType.FundedFloating;

      for (int t = 0; t < types.Length; t++)
      {
        CreateBasketCDS(types[t], 0, false);
        CreateBasketCDSPricer(0.05);

        double basketcds_vod_target = Sensitivities.VOD(basketCDSPricer_, "Pv");
        double basketcds_pv = basketCDSPricer_.Pv();
        double basketcds_vod_calced = 0;

        double[] cds_vod_target = new double[survCurves_.Length];
        double[] cds_pv = new double[survCurves_.Length];
        double[] cds_vod_calced = new double[survCurves_.Length];

        SurvivalCurve[] savedSurvCurves = (SurvivalCurve[])survCurves_.Clone();

        // Get the riskest curve index
        int riskestCurveIndex = GetRiskestCurveIndex();
        SurvivalCurve savedRiskestCurve = (SurvivalCurve)survCurves_[riskestCurveIndex].Clone();
        survCurves_[riskestCurveIndex].DefaultDate = settleDate_;
        CreateBasketCDSPricer(0.05);
        basketcds_vod_calced = basketCDSPricer_.Pv() - basketcds_pv;
        try
        {
          Assert.AreEqual(basketcds_vod_target, basketcds_vod_calced,
            1e-5, types[t].ToString() + "BasketCDS VOD test failed");
        }
        finally
        {
          survCurves_[riskestCurveIndex] = savedRiskestCurve;
        }

        for (int i = 0; i < survCurves_.Length; i++)
        {
          cds_vod_target[i] = Sensitivities.VOD(CDSPricers_[i], "Pv");
          cds_pv[i] = CDSPricers_[i].Pv();
          survCurves_[i].DefaultDate = settleDate_;
        }

        CreateBasketCDSPricer(0.05);

        try
        {
          for (int i = 0; i < survCurves_.Length; i++)
          {
            cds_vod_calced[i] = CDSPricers_[i].Pv() - cds_pv[i];
            Assert.AreEqual(cds_vod_target[i], cds_vod_calced[i],
              1e-5, types[t].ToString() + "VOD test failed");
          }
        }
        finally
        {
          survCurves_ = savedSurvCurves;
        }
      }
    }

    [Test, Smoke]
    public void Test_BasketCDS_Theta()
    {
      BasketCDSType[] types = new BasketCDSType[] { 
        BasketCDSType.Unfunded, BasketCDSType.FundedFixed, BasketCDSType.FundedFloating };

      double[] cds_pv = new double[survCurves_.Length];
      double[] cds_theta_target = new double[survCurves_.Length];
      double[] cds_theta_caled = new double[survCurves_.Length];      

      for (int t = 0; t < types.Length; t++)
      {
        double sum_cds_theta = 0;
        CreateBasketCDS(types[t], 0, false);
        CreateBasketCDSPricer(0.05);

        Dt toAsOf = Dt.Add(pricingDate_, 1);
        Dt toSettle = Dt.Add(settleDate_, 1);

        double basketcds_theta_target = Sensitivities.Theta(basketCDSPricer_, null, toAsOf, toSettle, ThetaFlags.None, SensitivityRescaleStrikes.No);
        double basketcds_pv = basketCDSPricer_.Pv();

        double[] weights = basketCDSPricer_.BasketCDS.Weights;
        for (int i = 0; i < survCurves_.Length; i++)
        {
          cds_theta_target[i] = Sensitivities.Theta(CDSPricers_[i], null, toAsOf, toSettle, ThetaFlags.None, SensitivityRescaleStrikes.No);
          sum_cds_theta += ((weights == null || weights.Length==0)?1.0/survCurves_.Length:weights[i])*cds_theta_target[i];
          cds_pv[i] = CDSPricers_[i].Pv();
        }
        // Test basketcds and cds sum equivalency
        Assert.AreEqual(sum_cds_theta, basketcds_theta_target, 1e-5,
          types[t].ToString() + "cs-basketcds sum equivalency failed");

        discountCurve_USD.AsOf = toAsOf;
        for (int i = 0; i < survCurves_.Length; i++)
        {
          survCurves_[i].AsOf = toAsOf;
          survCurves_[i].SurvivalCalibrator.RecoveryCurve.AsOf = toAsOf;
        }

        pricingDate_ = toAsOf;
        settleDate_ = toSettle;

        CreateBasketCDSPricer(0.05);

        pricingDate_ = Dt.Add(toAsOf, -1);
        settleDate_ = Dt.Add(toSettle, -1);

        double basketcs_theta_calced = basketCDSPricer_.Pv() - basketcds_pv;
        Assert.AreEqual(basketcds_theta_target, basketcs_theta_calced,
          1e-5, types[t].ToString() + "basketcds theta failed");

        for (int i = 0; i < survCurves_.Length; i++)
        {
          cds_theta_caled[i] = CDSPricers_[i].Pv() - cds_pv[i];
          Assert.AreEqual(cds_theta_target[i], cds_theta_caled[i],
            1e-5, types[t].ToString() + "cds theta failed");
        }
        discountCurve_USD.AsOf = pricingDate_;
        for (int i = 0; i < survCurves_.Length; i++)
        {
          survCurves_[i].AsOf = pricingDate_;
          survCurves_[i].SurvivalCalibrator.RecoveryCurve.AsOf = pricingDate_;
        }
      }
    }

    [Test, Smoke]
    public void Test_BasketCDS_Spread01()
    {
      BasketCDSType[] types = new BasketCDSType[]{
        BasketCDSType.Unfunded, BasketCDSType.FundedFixed, BasketCDSType.FundedFloating};
      for (int t = 0; t < types.Length; t++)
      {
        CreateBasketCDS(types[0], 0, false);
        CreateBasketCDSPricer(0.05);
        double basketcds_spread01 = Sensitivities.Spread01(basketCDSPricer_, 1e-4, 0);
        double cds_spread01 = 0;
        double[] weights = basketCDSPricer_.BasketCDS.Weights;
        for (int i = 0; i < survCurves_.Length; i++)
        {
          cds_spread01 += Sensitivities.Spread01(CDSPricers_[i], 1e-4, 0) *
            (weights == null || weights.Length == 0 ? 1.0 / survCurves_.Length : weights[i]);
        }
        Assert.AreEqual(cds_spread01, basketcds_spread01,
          1e-5, types[t].ToString() + "basketcds=sum(cds) spread01");
      }
    }

    [Test, Smoke]
    public void Test_BasketCDS_Rate01()
    {
      BasketCDSType[] types = new BasketCDSType[]{
        BasketCDSType.Unfunded, BasketCDSType.FundedFixed, BasketCDSType.FundedFloating};
      for (int t = 0; t < types.Length; t++)
      {
        CreateBasketCDS(types[0], 0, false);
        CreateBasketCDSPricer(0.05);
        double basketcds_ir01 = Sensitivities.IR01(basketCDSPricer_, 1e-2, 0, false);
        double cds_ir01 = 0;
        double[] weights = basketCDSPricer_.BasketCDS.Weights;
        for (int i = 0; i < survCurves_.Length; i++)
        {
          cds_ir01 += Sensitivities.IR01(CDSPricers_[i], 1e-2, 0, false) *
            (weights == null || weights.Length == 0 ? 1.0 / survCurves_.Length : weights[i]);
        }
        Assert.AreEqual(cds_ir01, basketcds_ir01, 1e-5,
          types[t].ToString() + "basketcds=sum(cds) IR01");
      }
    }

    [Test, Smoke]
    public void Test_ReferenceCurveDifferentFromDiscountCurve()
    {
      CreateBasketCDS(BasketCDSType.FundedFloating);
      CreateBasketCDSPricer(0.05);
      double pv = basketCDSPricer_.Pv();
      basketCDSPricer_.ReferenceCurve = basketCDSPricer_.DiscountCurve;
      double pvR = basketCDSPricer_.Pv();
      Assert.AreEqual(pv, pvR, 1e-16, "pv");
    }



    [Test, Smoke]
    public void Test_BasketCDS_Recovery01()
    {
      BasketCDSType[] types = new BasketCDSType[]{
        BasketCDSType.Unfunded, BasketCDSType.FundedFixed, BasketCDSType.FundedFloating};
      for (int t = 0; t < types.Length; t++)
      {
        CreateBasketCDS(types[0], 0, false);
        CreateBasketCDSPricer(0.05);
        double basketcds_recovery01 = Sensitivities.Recovery01(basketCDSPricer_, 1e-2, 0, false);
        double cds_recovery01 = 0;
        double[] weights = basketCDSPricer_.BasketCDS.Weights;
        for (int i = 0; i < survCurves_.Length; i++)
        {
          cds_recovery01 += Sensitivities.Recovery01(CDSPricers_[i], 1e-2, 0, false) *
            (weights == null || weights.Length == 0 ? 1.0 / survCurves_.Length : weights[i]);
        }
        Assert.AreEqual(cds_recovery01, basketcds_recovery01,
          1e-5, types[t].ToString() + "basketcds=sum(cds) Recovery01");
      }
    }

    #endregion tests

    #region helpers
    private SwapLeg CreateSwapLeg(Dt effectiveDate, Dt maturityDate, double coupon, bool floating)
    {           
      SwapLeg p = new SwapLeg( effectiveDate, maturityDate, ccy_,
        floating ? coupon / 10000 : coupon, dct_, freq_, roll_, cal_, false);

      if( floating )
        p.Index = "LIBOR";
      p.InitialExchange = true;
      p.IntermediateExchange = true;
      p.FinalExchange = true;
      p.Description = effectiveDate.ToStr("%D");
      p.Validate();

      return p;
    }

    private SwapLegPricer CreateSwapLegPricer(SwapLeg swapLeg)
    {
      SwapLegPricer pricer;
      var refIndex = new InterestRateIndex("LIBOR", freq_, ccy_, dct_, cal_, 2);
      pricer = new SwapLegPricer(swapLeg, pricingDate_, settleDate_,
        1.0, discountCurve_USD, refIndex, discountCurve_USD, 
        new RateResets(pricingDate_, 0.0), null, null);      

      pricer.Validate();
      return pricer;
    }

    private SwapLeg[] CreateSwapLeg(Dt[] effectiveDates, Dt maturityDate, double coupon, bool floating)
    {
      SwapLeg[] swapLegs = new SwapLeg[effectiveDates.Length];
      for (int i = 0; i < effectiveDates.Length; i++)
        swapLegs[i] = CreateSwapLeg(effectiveDates[i], maturityDate, coupon, floating);
      return swapLegs;
    }

    private SwapLegPricer[] CreateSwapLegPricer(SwapLeg[] swapLegs)
    {
      SwapLegPricer[] swapLegPricers = new SwapLegPricer[swapLegs.Length];
      for (int i = 0; i < swapLegs.Length; i++)
        swapLegPricers[i] = CreateSwapLegPricer(swapLegs[i]);
      return swapLegPricers;
    }

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
      double fee = 0;
      Dt feeSettle = Dt.Add(settleDate_, 2);
      CreateCDS(type, bonusRate, payRecoveryMaturity, fee, feeSettle);
    }
    private void CreateCDS(CdsType type, double bonusRate, bool payRecoveryMaturity, double fee, Dt feeSettle)
    {
      CDS_ = new CDS[survCurves_.Length];
      for (int i = 0; i < survCurves_.Length; i++)
      {
        CDS_[i] = new CDS(effective_, maturity_, ccy_, dealPremium_ / 10000.0, dct_, freq_, roll_, cal_);
        CDS_[i].CdsType = type;
        CDS_[i].FirstPrem = CDS.GetDefaultFirstPremiumDate(effective_, maturity_);
        CDS_[i].Fee = fee;
        CDS_[i].FeeSettle = feeSettle;
        if (bonusRate > 0)
        {
          CDS_[i].Bullet = new BulletConvention(bonusRate);
        }
        CDS_[i].PayRecoveryAtMaturity = payRecoveryMaturity;
        CDS_[i].Description = "CDS " + type.ToString();
        CDS_[i].Validate();
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
        CDSPricers_[i].Validate();
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
      Dt feeSettle = Dt.Add(pricingDate_, 3);
      CreateBasketCDS(type, bonusRate, payRecoveryMaturity, upfrontFee_, feeSettle);
    }

    private void CreateBasketCDS(BasketCDSType type, double bonusRate, bool payRecoveryMaturity, double fee, Dt feeSettle)
    {
      // Create the basket cds
      basketCDS_ = new BasketCDS(effective_, maturity_, ccy_, dealPremium_ / 10000.0, dct_, freq_, roll_, cal_);
      basketCDS_.BasketCdsType = type;
      basketCDS_.FirstPrem = CDS.GetDefaultFirstPremiumDate(effective_, maturity_);
      basketCDS_.Fee = fee;
      basketCDS_.FeeSettle = feeSettle;
      if (bonusRate > 0)
      {
        basketCDS_.Bullet = new BulletConvention(bonusRate);
      }
      basketCDS_.PayRecoveryAtMaturity = payRecoveryMaturity;
      basketCDS_.Description = "BasketCDS "+type.ToString();
      basketCDS_.Validate();

      CdsType cdsType = CdsType.Unfunded;
      if(type == BasketCDSType.FundedFixed)
        cdsType = CdsType.FundedFixed;
      if(type == BasketCDSType.FundedFloating)
        cdsType = CdsType.FundedFloating;
      CreateCDS(cdsType, bonusRate, payRecoveryMaturity, fee, feeSettle); 
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
      basketCDSPricer_.Validate();

      CreateCDSPricer(currentCoupon, recovFundCost);
    }

    private int GetRiskestCurveIndex()
    {
      double defaultProbability = 0;
      int index = 0;
      for (int i = 0; i < survCurves_.Length; i++)
      {
        if (!survCurves_[i].DefaultDate.IsValid())
        {
          double prob = 1.0 - survCurves_[i].SurvivalProb(pricingDate_, maturity_);
          if (defaultProbability < prob)
          {
            defaultProbability = prob;
            index = i;
          }
        }
      }
      return index;
    }

    private void TestCDSBasketCDSEquivalency(PricerDelegate Func1, PricerDelegate Func2, string measure)
    {
      bool withFee = false;
      TestCDSBasketCDSEquivalency(Func1, Func2, measure, withFee);
    }

    private void TestCDSBasketCDSEquivalency(PricerDelegate Func1, PricerDelegate Func2, string measure, bool withFee)
    {
      double measure_basketcds = 0, measure_cds = 0;
      double[] weights = null;

      if (withFee == true)
        CreateBasketCDS(BasketCDSType.Unfunded, 0, false, 0.1, Dt.Add(settleDate_, 2));
      else
        CreateBasketCDS();
      CreateBasketCDSPricer();
      weights = basketCDS_.Weights;
      measure_basketcds = Func1(basketCDSPricer_);
      measure_cds = 0;
      for (int i = 0; i < survCurves_.Length; i++)
        measure_cds += Func2(CDSPricers_[i]) * weights[i];
      AssertEqual("Unfunded " + measure + (withFee ? " with upfront" : ""), 0, measure_basketcds - measure_cds, 1e-5);

      if (withFee == true)
        CreateBasketCDS(BasketCDSType.FundedFixed, 0, false, 0.1, Dt.Add(settleDate_, 2));
      else
        CreateBasketCDS(BasketCDSType.FundedFixed);
      CreateBasketCDSPricer();
      weights = basketCDS_.Weights;
      measure_basketcds = Func1(basketCDSPricer_);
      measure_cds = 0;
      for (int i = 0; i < survCurves_.Length; i++)
        measure_cds += Func2(CDSPricers_[i]) * weights[i];
      AssertEqual("FundedFixed " + measure + (withFee ? " with upfront" : ""), 0, measure_basketcds - measure_cds, 1e-5);

      if (withFee == true)
        CreateBasketCDS(BasketCDSType.FundedFloating, 0, false, 0.1, Dt.Add(settleDate_, 2));
      else
        CreateBasketCDS(BasketCDSType.FundedFloating);
      CreateBasketCDSPricer(0.05);
      weights = basketCDS_.Weights;
      measure_basketcds = Func1(basketCDSPricer_);
      measure_cds = 0;
      for (int i = 0; i < survCurves_.Length; i++)
        measure_cds += Func2(CDSPricers_[i]) * weights[i];
      AssertEqual("FundedFloating " + measure + (withFee ? " with upfront" : ""), 0, measure_basketcds - measure_cds, 1e-5);
    }
    
    private delegate double PricerDelegate(IPricer pricer);

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
    private double upfrontFee_ = 0.0;
    private CDS[] CDS_ = null;
    private BasketCDS basketCDS_ = null;

    private double notional_ = 20000000;
    private Dt pricingDate_ = Dt.Empty;
    private Dt settleDate_ = Dt.Empty;
    private CDSCashflowPricer[] CDSPricers_ = null;
    private BasketCDSPricer basketCDSPricer_ = null;

    #endregion BasketCS parameters
    #endregion data
  }
}
