//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestFundedNtd : ToolkitTestBase
  {
    #region SetUP
    [OneTimeSetUp]
    public void Initialize()
    {
      //PushConfiguration();

      string filename_1 = GetTestFilePath(USD_LIBOR_DataFile_);
      USD_LIBOR_Data = (DiscountData) XmlLoadData(filename_1, typeof (DiscountData));
      string filename_2 = GetTestFilePath(EUR_LIBOR_DataFile_);
      EUR_LIBOR_Data = (DiscountData) XmlLoadData(filename_2, typeof (DiscountData));

      discountCurve_USD = USD_LIBOR_Data.GetDiscountCurve();
      pricingDate_ = Dt.FromStr(USD_LIBOR_Data.AsOf, "%D");
      settleDate_ = Dt.Add(pricingDate_, 1);

      string fileName_3 = GetTestFilePath(CreditDataFile_);
      creditData = (CreditData) XmlLoadData(fileName_3, typeof (CreditData));
      survCurves_ = creditData.GetSurvivalCurves(discountCurve_USD);
      savedSurvCurves_ = ArrayUtil.Generate<SurvivalCurve>(survCurves_.Length, (i) => (SurvivalCurve)survCurves_[i].Clone());
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
    public void TestRecoveryAtMaturity()
    {
      TestRecoveryAtMaturity(true, 1, 1);
      TestRecoveryAtMaturity(false, 1, 1);
    }

    private void TestRecoveryAtMaturity(bool payAtMaturity, int start, int ncover)
    {
      const double recovery = 0.75;

      CreateNtd(NTDType.FundedFixed, start, ncover, 0);
      ftd_.PayRecoveryAtMaturity = payAtMaturity;
      ftd_.FixedRecovery = new[] { recovery };
      ftd_.Premium = 0.0;
      CreateFtdPricer();
      var fundedPv = ftdPricer_.Pv();

      CreateNtd(NTDType.FundedFixed, start, ncover, 0);
      ftd_.PayRecoveryAtMaturity = payAtMaturity;
      ftd_.FixedRecovery = new[] { 0.0 };
      ftd_.Premium = 0.0;
      CreateFtdPricer();
      var notionalXchgPv = ftdPricer_.Pv();

      CreateNtd(NTDType.Unfunded, start, ncover, 0);
      ftd_.PayRecoveryAtMaturity = payAtMaturity;
      ftd_.FixedRecovery = new[] { 1 - recovery };
      ftd_.Premium = 0.0;
      CreateFtdPricer();
      var recoveryPv = -ftdPricer_.ProtectionPv();

      const double tol = 1E-8;
      if (Math.Abs(notionalXchgPv + recoveryPv - fundedPv) > tol)
      {
        AssertEqual("RecoveryAt" +
          (payAtMaturity ? "Maturity(" : "Default(") + start + ',' + ncover,
          fundedPv, notionalXchgPv + recoveryPv, tol);
      }
    }

    [Test, Smoke]
    public void TestIOPOFundedFixed()
    {
      // Test Pv(IO + PO) = Pv(FundedFixed), Accrual(IO+PO) = Accrual(FundedFixed), ect.
      CreateNtd(NTDType.FundedFixed, 1, 2, 0);
      CreateFtdPricer();
      double Pv = ftdPricer_.Pv();
      double accrual = ftdPricer_.Accrued();
      double expectedLoss = ftdPricer_.ExpectedLoss();
      double expectedSurvival = ftdPricer_.ExpectedSurvival();

      CreateNtd(NTDType.IOFundedFixed, 1, 2, 0);
      CreateFtdPricer();
      double Pv_IO = ftdPricer_.Pv();
      double accrual_IO = ftdPricer_.Accrued();
      double expectedLoss_IO = ftdPricer_.ExpectedLoss();
      double expectedSurvival_IO = ftdPricer_.ExpectedSurvival();

      CreateNtd(NTDType.PO, 1, 2, 0);
      CreateFtdPricer();
      double Pv_PO = ftdPricer_.Pv();
      double accrual_PO = ftdPricer_.Accrued();
      double expectedLoss_PO = ftdPricer_.ExpectedLoss();
      double expectedSurvival_PO = ftdPricer_.ExpectedSurvival();

      Assert.AreEqual(Pv_IO + Pv_PO, Pv, 1e-5, "Pv(IO)+Pv(PO) <> Pv(FundedFixed)");
      Assert.AreEqual(
        accrual_IO + accrual_PO, accrual, 1e-5, "Accrual(IO)+Accrual(PO) <> Accrual(FundedFixed)");
      Assert.AreEqual( 
        expectedLoss_IO + expectedLoss_PO, expectedLoss * 2.0, "ExpectedLoss(IO)+ExpectedLoss(PO)<>2*ExpectedLoss(FundedFixed)");
      Assert.AreEqual(
        expectedSurvival_IO + expectedSurvival_PO, expectedSurvival * 2.0, "ExpectedSurvival(IO)+ExpectedSurvival(PO)<>2*ExpectedSurvival(FundedFixed)");
    }

    [Test, Smoke]
    public void TestFundedFixedFeePv()
    {
      // The feepv for FundedFixed ntd is the sum of feepv for 
      // Unfunded ntd and the discounted survival notional.
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      double unfundedFeePvToSettle = ftdPricer_.FeePv() / discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
      
      // Calculate the discounted survival notional
      Curve nthLossCurve = ftdPricer_.NthLossCurve(1);
      double survProb = 1.0 - nthLossCurve.Interpolate(maturity_);
      double remainedNotional = notional_ * survProb * discountCurve_USD.DiscountFactor(settleDate_, maturity_);
      double feePv = (remainedNotional + unfundedFeePvToSettle) * discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);

      CreateNtd(NTDType.FundedFixed, 1, 1, 0);
      CreateFtdPricer();
      double feePvFundedFixed = ftdPricer_.FeePv();
      AssertEqual("FeePv FundedFixed", feePv, feePvFundedFixed, 1e-5);
    }

    [Test, Smoke]
    public void TestUnfundedFeePv()
    {
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      Cashflow cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);

      int count = cashFlow.Count;
      Dt startDate = cashFlow.GetStartDt(0);
      Dt[] dates = new Dt[count];
      double[] discountFactors = new double[count];      
      for (int i = 0; i < count; i++)
      {
        dates[i] = cashFlow.GetDt(i);
        discountFactors[i] = discountCurve_USD.DiscountFactor(pricingDate_, dates[i]);
      }      

      double[] survProbs = new double[count];
      double[] averageSurvProbs = new double[count];
      SurvivalCurve curve = ftdPricer_.NthSurvivalCurve(1);
      for (int i = 0; i < count; i++)
      {
        survProbs[i] = curve.SurvivalProb(pricingDate_, dates[i]);
        averageSurvProbs[i] = ((i == 0 ? 1 : survProbs[i - 1]) + survProbs[i]) / 2.0;
      }

      double[] dayFraction = new double[count];
      double[] premia = new double[count];
      dayFraction[0] = Dt.Diff(startDate, dates[0]) / 360.0;
      premia[0] = ftd_.Premium * dayFraction[0] - ftdPricer_.Accrued() / ftdPricer_.Notional;      
      for (int i = 1; i < count-1; i++)
      {
        dayFraction[i] = Dt.Diff(dates[i - 1], dates[i]) / 360.0;
        premia[i] = ftd_.Premium * dayFraction[i];
      }
      // Add one more day to last date to calc day fraction and premium
      dayFraction[count-1] = (Dt.Diff(dates[count - 2], dates[count-1])+1.0) / 360.0;
      premia[count-1] = ftd_.Premium * dayFraction[count-1];
      
      double fee = 0;

      for (int i = 0; i < count; i++)
      {
        fee += premia[i] * discountFactors[i] * averageSurvProbs[i];
      }

      fee /= discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
      fee += ftdPricer_.Accrued() / ftdPricer_.Notional;
      fee *= (ftdPricer_.Notional * discountCurve_USD.DiscountFactor(pricingDate_, settleDate_));

      Assert.AreEqual(fee, ftdPricer_.FeePv(), 1e-5);
    }

    [Test, Smoke]
    public void TestFundedFloatingFeePv()
    {
      // The feepv for FundedFloating ntd is the sum of feepv for
      // Unfunded ntd (with floating premium) and discounted survial notional      
      CreateNtd(NTDType.FundedFloating, 1, 1, 0);
      CreateFtdPricer(0.05);
      double feePv = ftdPricer_.FeePv();

      Cashflow cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);

      int count = cashFlow.Count;
      Dt startDate = cashFlow.GetStartDt(0);
      Dt[] dates = new Dt[count];
      double[] discountFactors = new double[count];
      for (int i = 0; i < count; i++)
      {
        dates[i] = cashFlow.GetDt(i);
        discountFactors[i] = discountCurve_USD.DiscountFactor(pricingDate_, dates[i]);
      }

      double[] survProbs = new double[count];
      double[] averageSurvProbs = new double[count];
      SurvivalCurve curve = ftdPricer_.NthSurvivalCurve(1);
      for (int i = 0; i < count; i++)
      {
        survProbs[i] = curve.SurvivalProb(pricingDate_, dates[i]);
        averageSurvProbs[i] = ((i == 0 ? 1 : survProbs[i - 1]) + survProbs[i]) / 2.0;
      }
      
      double[] premia = new double[count];      
      for (int i = 0; i < count; i++)
        premia[i] = cashFlow.GetAccrued(i);
      premia[0] -= ftdPricer_.Accrued() / ftdPricer_.Notional;

      double fee = 0;
      for (int i = 0; i < count; i++)
        fee += premia[i] * discountFactors[i] * averageSurvProbs[i];

      fee /= discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
      fee += ftdPricer_.Accrued() / ftdPricer_.Notional;
      fee *= ftdPricer_.Notional;

      // Calculate the discounted survival notional
      Curve nthLossCurve = ftdPricer_.NthLossCurve(1);
      double survProb = 1.0 - nthLossCurve.Interpolate(maturity_);
      double remainedNotional = notional_ * survProb * discountCurve_USD.DiscountFactor(settleDate_, maturity_);
      fee += remainedNotional;
      fee *= discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);

      Assert.AreEqual(fee, feePv, 1e-5, "FundedFloating feepv is wrong");
    }

    [Test, Smoke]
    public void TestIOFundedFixedFeePv()
    {
      // For IOFundedFixed, the feepv should be the same to UnFunded
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      double feePvUnfunded = ftdPricer_.FeePv();
      CreateNtd(NTDType.IOFundedFixed, 1, 1, 0);
      CreateFtdPricer();
      double feePvIOFundedFixed = ftdPricer_.FeePv();
      Assert.AreEqual(feePvIOFundedFixed, feePvUnfunded, 1e-5, "IOFundedFixed feepv is wrong");
    }

    [Test, Smoke]
    public void TestIOFundedFloatingFeePv()
    {
      // The feepv for IOFundedFloating ntd is the feepv for Unfunded ntd (with floating premium)
      CreateNtd(NTDType.IOFundedFloating, 1, 1, 0);
      CreateFtdPricer(0.05);
      double feePv = ftdPricer_.FeePv();

      Cashflow cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);

      int count = cashFlow.Count;
      Dt startDate = cashFlow.GetStartDt(0);
      Dt[] dates = new Dt[count];
      double[] discountFactors = new double[count];
      for (int i = 0; i < count; i++)
      {
        dates[i] = cashFlow.GetDt(i);
        discountFactors[i] = discountCurve_USD.DiscountFactor(pricingDate_, dates[i]);
      }

      double[] survProbs = new double[count];
      double[] averageSurvProbs = new double[count];
      SurvivalCurve curve = ftdPricer_.NthSurvivalCurve(1);
      for (int i = 0; i < count; i++)
      {
        survProbs[i] = curve.SurvivalProb(pricingDate_, dates[i]);
        averageSurvProbs[i] = ((i == 0 ? 1 : survProbs[i - 1]) + survProbs[i]) / 2.0;
      }

      double[] premia = new double[count];
      for (int i = 0; i < count; i++)
        premia[i] = cashFlow.GetAccrued(i);
      premia[0] -= ftdPricer_.Accrued() / ftdPricer_.Notional;

      double fee = 0;
      for (int i = 0; i < count; i++)
        fee += premia[i] * discountFactors[i] * averageSurvProbs[i];

      fee /= discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);
      fee += ftdPricer_.Accrued() / ftdPricer_.Notional;
      fee *= ftdPricer_.Notional;

      fee *= discountCurve_USD.DiscountFactor(pricingDate_, settleDate_);

      Assert.AreEqual(fee, feePv, 1e-5, "IOFundedFloating feepv");
    }
    
    [Test, Smoke]
    public void TestPOFundedFeePv()
    {
      CreateNtd(NTDType.PO, 1, 1, 0);
      CreateFtdPricer();
      double feePv = ftdPricer_.FeePv();

      // The feepv for PO funded should be equal to discounted survival principal
      Curve nthLossCurve = ftdPricer_.NthLossCurve(1);
      double survProb = 1.0 - nthLossCurve.Interpolate(maturity_);
      double discountFac = discountCurve_USD.DiscountFactor(pricingDate_, maturity_);
      double fee = survProb * discountFac * ftdPricer_.Notional;
      Assert.AreEqual(fee, feePv, 1e-5, "PO funded feepv");
    }    
    

    [Test, Smoke]
    public void TestAccrual()
    {
      // Cehck the accrual for different types
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      Cashflow cashFlow = new Cashflow(pricingDate_); 
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);
      int diffDays = Dt.Diff(cashFlow.GetStartDt(0), settleDate_);
      Assert.AreEqual(diffDays, ftdPricer_.AccrualDays(), 0, "Unfunded Accrual days");
      Assert.AreEqual( 
        diffDays * ftd_.Premium / 360.0 * ftdPricer_.Notional, ftdPricer_.Accrued(), 1e-5, " Unfunded Accrual");

      CreateNtd(NTDType.FundedFixed, 1, 1, 0);
      CreateFtdPricer();
      cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);
      diffDays = Dt.Diff(cashFlow.GetStartDt(0), settleDate_);
      Assert.AreEqual(diffDays, ftdPricer_.AccrualDays(), 0, "FundedFixed Accrual Days");
      Assert.AreEqual(diffDays * ftd_.Premium / 360.0 * ftdPricer_.Notional, ftdPricer_.Accrued(),
        1e-5, "FundedFixed Accrual amount");
      
      CreateNtd(NTDType.FundedFloating, 1, 1, 0);
      CreateFtdPricer(0.05);
      cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);
      diffDays = Dt.Diff(cashFlow.GetStartDt(0), settleDate_);
      Assert.AreEqual(diffDays, ftdPricer_.AccrualDays(), 0, "FundedFloating Accrual Days");
      Assert.AreEqual(diffDays * 0.05 / 360.0 * ftdPricer_.Notional, ftdPricer_.Accrued(),
        1e-5, "FundedFloating Accrual amount");

      CreateNtd(NTDType.IOFundedFixed, 1, 1, 0);
      CreateFtdPricer();
      cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);
      diffDays = Dt.Diff(cashFlow.GetStartDt(0), settleDate_);
      Assert.AreEqual(diffDays, ftdPricer_.AccrualDays(), 0, "IOFundedFixed Accrual Days");
      Assert.AreEqual(diffDays * ftd_.Premium / 360.0 * ftdPricer_.Notional, ftdPricer_.Accrued(), 
        1e-5, "IOFundedFixed Accrual amount");

      CreateNtd(NTDType.IOFundedFloating, 1, 1, 0);
      CreateFtdPricer(0.05);
      cashFlow = new Cashflow(pricingDate_);
      cashFlow = ftdPricer_.GenerateCashflow(cashFlow, pricingDate_);
      diffDays = Dt.Diff(cashFlow.GetStartDt(0), settleDate_);
      Assert.AreEqual(diffDays, ftdPricer_.AccrualDays(), 0, "IOFundedFloating Accrual Days");
      Assert.AreEqual(diffDays * 0.05 / 360.0 * ftdPricer_.Notional, ftdPricer_.Accrued(), 1e-5, "IOFundedFloating Accrual amount");

      CreateNtd(NTDType.PO, 1, 1, 0);
      CreateFtdPricer();
      Assert.AreEqual(0, ftdPricer_.AccrualDays(), 0, "PO Accrual Days");
      Assert.AreEqual(0, ftdPricer_.Accrued(), 0, "PO Accrual Amount");
    }

    [Test, Smoke]
    public void TestBreakEvenPremium()
    {
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      double BE = ftdPricer_.BreakEvenPremium() * 10000;

      double savedDealPermium = dealPremium_;
      dealPremium_ = BE;
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      double Pv = ftdPricer_.Pv();
      double accrual = ftdPricer_.Accrued();
      dealPremium_ = savedDealPermium;
      Assert.AreEqual(Pv, accrual, 
        1e-5, "Unfunded breakeven premium roundtrip");

      CreateNtd(NTDType.FundedFixed, 1, 1, 0);
      CreateFtdPricer();
      double BE_FundedFixed = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(BE, BE_FundedFixed, 1e-2, "FundedFixed breakeven premium");

      CreateNtd(NTDType.FundedFloating, 1, 1, 0);
      CreateFtdPricer();
      double BE_FundedFloating = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(BE, BE_FundedFloating, 1e-2, "FundedFloating breakeven premium");

      CreateNtd(NTDType.PO, 1, 1, 0);
      CreateFtdPricer();
      double BE_PO = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(BE, BE_PO, 1e-5, "PO funded NTD breakeven premium");

      CreateNtd(NTDType.IOFundedFixed, 1, 1, 0);
      CreateFtdPricer();
      double BE_IOFundedFixed = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(BE, BE_IOFundedFixed, 1e-5, "IO FundedFixed NTD breakeven premium");

      CreateNtd(NTDType.IOFundedFloating, 1, 1, 0);
      CreateFtdPricer();
      double BE_IOFundedFloating = ftdPricer_.BreakEvenPremium() * 10000;
      Assert.AreEqual(BE, BE_IOFundedFloating, 1e-5, "IO FundedFloating NTD breakeven premium");

      NTDType[] types = new NTDType[] { NTDType.FundedFixed, NTDType.FundedFloating, NTDType.IOFundedFixed, NTDType.IOFundedFloating, NTDType.PO };
      for (int i = 0; i < types.Length; i++)
      {
        CreateNtd(types[i], 1, 1, 0);
        CreateFtdPricer(2, 0);
        try
        {
          double BE_Defaulted = ftdPricer_.BreakEvenPremium() * 10000;
          Assert.AreEqual(0, BE_Defaulted, 1e-5, 
            types[i].ToString()+" NTD breakeven premium wrong");
        }
        finally
        {
          for (int j = 0; j < survCurves_.Length; j++)
          {
            survCurves_[j].Copy(savedSurvCurves_[j]);
          }
        }
      }
    }

    [Test, Smoke]
    public void TestBreakEvenFee()
    {
      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      CreateFtdPricer();
      double BE = ftdPricer_.BreakEvenFee();

      CreateNtd(NTDType.Unfunded, 1, 1, 0);
      ftd_.Fee = BE;
      ftd_.FeeSettle = Dt.Add(pricingDate_, 3);
      CreateFtdPricer();
      double Pv = ftdPricer_.Pv();
      double accrual = ftdPricer_.Accrued();
      Assert.AreEqual(Pv, accrual,
        1e-5, "Unfunded breakeven fee roundtrip");

      CreateNtd(NTDType.FundedFixed, 1, 1, 0);
      CreateFtdPricer();
      double BE_FundedFixed = ftdPricer_.BreakEvenFee();
      Assert.AreEqual(BE, BE_FundedFixed, 1e-2, "FundedFixed breakeven fee");

      CreateNtd(NTDType.FundedFloating, 1, 1, 0);
      CreateFtdPricer();
      double BE_FundedFloating = ftdPricer_.BreakEvenFee();
      Assert.AreEqual(BE, BE_FundedFloating, 1e-2, "FundedFloating breakeven fee");

      CreateNtd(NTDType.PO, 1, 1, 0);
      CreateFtdPricer();
      double BE_PO = ftdPricer_.BreakEvenFee();
      Assert.AreEqual(BE, BE_PO, 1e-5, "PO funded NTD breakeven fee");

      CreateNtd(NTDType.IOFundedFixed, 1, 1, 0);
      CreateFtdPricer();
      double BE_IOFundedFixed = ftdPricer_.BreakEvenFee();
      Assert.AreEqual(BE, BE_IOFundedFixed, 1e-5, "IO FundedFixed NTD breakeven fee");

      CreateNtd(NTDType.IOFundedFloating, 1, 1, 0);
      CreateFtdPricer();
      double BE_IOFundedFloating = ftdPricer_.BreakEvenFee();
      Assert.AreEqual(BE, BE_IOFundedFloating, 1e-5, "IO FundedFloating NTD breakeven fee");

      NTDType[] types = new NTDType[] { NTDType.FundedFixed, NTDType.FundedFloating, NTDType.IOFundedFixed, NTDType.IOFundedFloating, NTDType.PO };
      for (int i = 0; i < types.Length; i++)
      {
        CreateNtd(types[i], 1, 1, 0);
        CreateFtdPricer(2, 0);
        try
        {
          double BE_Defaulted = ftdPricer_.BreakEvenFee();
          Assert.AreEqual(0, BE_Defaulted, 1e-5,
            types[i].ToString() + " NTD breakeven fee");
        }
        finally
        {
          for (int j = 0; j < survCurves_.Length; j++)
          {
            survCurves_[j].Copy(savedSurvCurves_[j]);
          }
        }
      }
    }
    #endregion tests

    #region helpers
    private void CreateNtd(NTDType type, int first, int numCovered, double bonusRate)
    {
      ftd_ = new FTD(effective_, maturity_, ccy_, dealPremium_ / 10000, dct_, freq_, roll_, cal_);

      ftd_.First = first;
      ftd_.NumberCovered = numCovered;
      ftd_.Bullet = bonusRate > 0 ? new BulletConvention(bonusRate) : null;
      ftd_.Description = "NTD " + first.ToString() + " " + numCovered.ToString();
      ftd_.NtdType = type;
      ftd_.Validate();

      return;
    }

    private void CreateFtdPricer()
    {
      CreateFtdPricer(0);
    }
    private void CreateFtdPricer(double currentRate)
    {
      Dt portfolioStart = Dt.Empty;
      Copula copula = new Copula(CopulaType.Gauss, 2, 2);
      SurvivalCurve[] survivalCurves = new SurvivalCurve[10];
      for (int i = 0; i < 10; i++)
        survivalCurves[i] = survCurves_[i];
      string[] names = Array.ConvertAll<SurvivalCurve, string>(survivalCurves, delegate(SurvivalCurve c) { return c.Name; });
      SingleFactorCorrelation corr = new SingleFactorCorrelation(names, 0.5);

      // Create pricer
      RateReset rateReset = null;
      if(ftd_.NtdType == NTDType.FundedFloating || ftd_.NtdType == NTDType.IOFundedFloating)
        rateReset = new RateReset(effective_, currentRate);
      List<RateReset> rateResetList = new List<RateReset>();
      if (rateReset != null)
        rateResetList.Add(rateReset);
      ftdPricer_ = BasketPricerFactory.NTDPricerSemiAnalytic(
        new FTD[] { ftd_ }, portfolioStart, pricingDate_, settleDate_,
        discountCurve_USD, survivalCurves, null, copula, corr,
        3, TimeUnit.Months, 50, new double[] { notional_ }, rateResetList)[0];
      
      ftdPricer_.Validate();
    }

    private void CreateFtdPricer(int numDefaults, double currentRate)
    {
      Dt portfolioStart = Dt.Empty;
      Copula copula = new Copula(CopulaType.Gauss, 2, 2);
      SurvivalCurve[] survivalCurves = new SurvivalCurve[10];
      // Save the survival curves
      for (int i = 0; i < 10; i++)
        survivalCurves[i] = survCurves_[i];
      string[] names = Array.ConvertAll<SurvivalCurve, string>(survivalCurves, delegate(SurvivalCurve c) { return c.Name; });
      SingleFactorCorrelation corr = new SingleFactorCorrelation(names, 0.5);

      // Make the first numDefaults curves default
      for(int i = 0; i < numDefaults; i++)
      {
        survivalCurves[i].DefaultDate = pricingDate_;
      }

      // Create pricer
      RateReset rateReset = null;
      if (ftd_.NtdType == NTDType.FundedFloating || ftd_.NtdType == NTDType.IOFundedFloating)
        rateReset = new RateReset(effective_, currentRate);
      List<RateReset> rateResetList = new List<RateReset>();
      if (rateReset != null)
        rateResetList.Add(rateReset);
      ftdPricer_ = BasketPricerFactory.NTDPricerSemiAnalytic(
        new FTD[] { ftd_ }, portfolioStart, pricingDate_, settleDate_,
        discountCurve_USD, survivalCurves, null, copula, corr,
        3, TimeUnit.Months, 50, new double[] { notional_ }, rateResetList)[0];

      ftdPricer_.Validate();
    }
    #endregion

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
    private SurvivalCurve[] savedSurvCurves_ = null;

    private FTD ftd_ = null;
    private FTDPricer ftdPricer_ = null;

    #region BasketCDS parameters
    private Dt effective_ = new Dt(28, 3, 2007);
    private Currency ccy_ = Currency.USD;
    private DayCount dct_ = DayCount.Actual360;
    private Frequency freq_ = Frequency.Quarterly;
    private BDConvention roll_ = BDConvention.Following;
    private Calendar cal_ = Calendar.NYB;
    private Dt maturity_ = new Dt(20, 6, 2012);
    private double dealPremium_ = 60.0;

    private double notional_ = 20000000;
    private Dt pricingDate_ = Dt.Empty;
    private Dt settleDate_ = Dt.Empty;

    #endregion BasketCS parameters
    #endregion data
  }
}