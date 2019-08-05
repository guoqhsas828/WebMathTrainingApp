//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  [TestFixture, Smoke]
  public class PartBondTests: ToolkitTestBase
  {
    #region Data
    private const double TOLERANCE = 0.001;
    private const double CONVEXITY_TOLERANCE = 0.5;
    #endregion

    #region UKGilt ExDiv Date Tests

    /// <summary>
    /// Tests for a UK Gilt bond where the settle date is after the ex div date 
    /// i.e., the buyer wont get the first coupon
    /// </summary>
    [Test]
    public void UKGiltInEXDivDateNoOverride()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(08, 01, 2010);
      Dt settlement = new Dt(08, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve =TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      double fullPrice = pricer.FullPrice();
      Assert.AreEqual(-7, pricer.AccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(-0.00095108696, pricer.AccruedInterest(), 1E-9, "Accrued Interest does not match");
      Assert.AreEqual(0.936634, pricer.FullPrice(), 1E-9, "Full Price does not match");
      Assert.AreEqual(0.05327312636, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.05010204717, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.07343251, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, pricer.FullPrice());
      quoteDict.Add(QuotingConvention.Yield, yield);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);


      }


    }

    /// <summary>
    /// Tests for a UK Gilt bond where the settle date is before the ex0div date (regular period) 
    /// </summary>
    [Test]
    public void UKGiltRegularPeriodNoOverride()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(06, 01, 2010);
      Dt settlement = new Dt(06, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      double fullPrice = pricer.FullPrice();
      Assert.AreEqual(175, pricer.AccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0.02377717391, pricer.AccruedInterest(), 1E-9, "Accrued Interest does not match");
      Assert.AreEqual(0.961362, pricer.FullPrice(), 1E-9, "Full Price does not match");
      Assert.AreEqual(0.05318542451, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.05002851736, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.07337831, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, pricer.FullPrice());
      quoteDict.Add(QuotingConvention.Yield, yield);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);


      }

    }

    /// <summary>
    /// Tests for a UK Gilt bond where the settle date is after the ex-div date and there is a override flag set 
    /// </summary>
    [Test]
    public void UKGiltInExDivDateWithOverride()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(08, 01, 2010);
      Dt settlement = new Dt(08, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.IgnoreExDivDateInCashflow = true;
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;


      double fullPrice = pricer.FullPrice();
      Assert.AreEqual(177, pricer.AccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0.02404891304, pricer.AccruedInterest(), 1E-9, "Accrued Interest does not match");
      Assert.AreEqual(0.961634, pricer.FullPrice(), 1E-9, "Full Price does not match");
      Assert.AreEqual(0.05326276439, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.05010181749, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.07341913, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, pricer.FullPrice());
      quoteDict.Add(QuotingConvention.Yield, yield);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);


      }

    }

    /// <summary>
    /// Tests for a UK Gilt bond where the settle date is before the ex-div date and there si a overruide flag set 
    /// </summary>
    [Test]
    public void UKGiltRegularPeriodWithOverride()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(06, 01, 2010);
      Dt settlement = new Dt(06, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.IgnoreExDivDateInCashflow = true;
      double fullPrice = pricer.FullPrice();
      Assert.AreEqual(175, pricer.AccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0.02377717391, pricer.AccruedInterest(), 1E-9, "Accrued Interest does not match");
      Assert.AreEqual(0.961362, pricer.FullPrice(), 1E-9, "Full Price does not match");
      Assert.AreEqual(0.05318542451, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.05002851736, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.07337831, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, pricer.FullPrice());
      quoteDict.Add(QuotingConvention.Yield, yield);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);


      }
    }

    /// <summary>
    /// Tests the transition for the UK Gilt from the ExDiv period to a cum div period . No Override has been set 
    /// </summary>
    [Test]
    public void UKGiltTransitionFromExDivToCumDiv1()
    {


      //First we price the UKGilt on a regular date Jan 6th 2010
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(06, 01, 2010);
      Dt settlement = new Dt(06, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      //now we check for round trip pricing for all the quoting conventions 

      double yield_1 = pricer.YieldToMaturity();
      double pv01_1 = pricer.PV01();
      double convexity_1 = pricer.Convexity();
      double duration_1 = pricer.ModDuration();
      double irr_1 = pricer.Irr();
      double asw_1 = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);

      double zspread_1 = pricer.ImpliedZSpread();
      double cdsBasis_1 = pricer.ImpliedCDSSpread();
      double cdsLevel_1 = pricer.ImpliedCDSLevel();

      double spread01_1 = pricer.Spread01();

      double zspread01_1 = pricer.ZSpread01();

      double ir01_1 = pricer.Rate01();



      //Now edit asof and settle dates 
      Dt exDivDate = new Dt(07, 01, 2010);
      asOf = exDivDate;
      settlement = exDivDate;
      pricer.AsOf = exDivDate;
      pricer.Settle = exDivDate;
      pricer.TradeSettle = exDivDate;
      DiscountCurve dc2 = TestBond.CreateDiscountCurveForAmortBond(exDivDate);
      SurvivalCurve sc2 = TestBond.CreateSurvivalCurveForAmortBond(exDivDate, dc2, 0.4);
      pricer.DiscountCurve = dc2;
      pricer.SurvivalCurve = sc2;
      pricer.Reset();

      double yield_2 = pricer.YieldToMaturity();
      double pv01_2 = pricer.PV01();

      double irr_2 = pricer.Irr();
      double asw_2 = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);

      double zspread_2 = pricer.ImpliedZSpread();
      double cdsBasis_2 = pricer.ImpliedCDSSpread();
      double cdsLevel_2 = pricer.ImpliedCDSLevel();

      double spread01_2 = pricer.Spread01();

      double zspread01_2 = pricer.ZSpread01();

      double ir01_2 = pricer.Rate01();


      Assert.AreEqual(yield_1, yield_2, 0.001, "yield does not match");
      Assert.AreEqual(pv01_1, pv01_2, 0.001, "pv01 does not match");
      Assert.AreEqual(irr_1, irr_2, 0.001, "irr does not match");
      Assert.AreEqual(asw_1, asw_2, 0.001, "asw does not match");
      Assert.AreEqual(zspread_1, zspread_2, 0.001, "Zspread does not match");
      Assert.AreEqual(cdsBasis_1, cdsBasis_2, 0.001, "cds basis does not match");
      Assert.AreEqual(cdsLevel_1, cdsLevel_2, 0.001, "cds level does not match");
      Assert.AreEqual(spread01_1, spread01_2, 0.001, "spread 01 does not match");
      Assert.AreEqual(zspread01_1, zspread01_2, 0.001, "zspread01 dpes not matcj");
      Assert.AreEqual(ir01_1, ir01_2, 0.001, "rateor does not match");


    }


    /// <summary>
    /// Tests the transition for a UK Gilt from a Ex Div period to a cum div period . Override flag has been set 
    /// </summary>
    [Test]
    public void UKGiltTransitionFromExDivToCumDiv2()
    {
      //First we price the UKGilt on a regular date Jan 6th 2010
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(06, 01, 2010);
      Dt settlement = new Dt(06, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.IgnoreExDivDateInCashflow = true;

      //now we check for round trip pricing for all the quoting conventions 
      double fullPrice_1 = pricer.FullPrice();
      double yield_1 = pricer.YieldToMaturity();
      double pv01_1 = pricer.PV01();
      double convexity_1 = pricer.Convexity();
      double duration_1 = pricer.ModDuration();
      double irr_1 = pricer.Irr();
      double asw_1 = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);

      double zspread_1 = pricer.ImpliedZSpread();
      double cdsBasis_1 = pricer.ImpliedCDSSpread();
      double cdsLevel_1 = pricer.ImpliedCDSLevel();

      double spread01_1 = pricer.Spread01();

      double zspread01_1 = pricer.ZSpread01();

      double ir01_1 = pricer.Rate01();



      //Now edit asof and settle dates 
      Dt exDivDate = new Dt(07, 01, 2010);
      asOf = exDivDate;
      settlement = exDivDate;
      pricer.AsOf = exDivDate;
      pricer.Settle = exDivDate;
      DiscountCurve dc2 = TestBond.CreateDiscountCurveForAmortBond(exDivDate);
      SurvivalCurve sc2 = TestBond.CreateSurvivalCurveForAmortBond(exDivDate, dc2, 0.4);
      pricer.DiscountCurve = dc2;
      pricer.SurvivalCurve = sc2;
      pricer.Reset();

      double fullPrice_2 = pricer.FullPrice();
      double yield_2 = pricer.YieldToMaturity();
      double pv01_2 = pricer.PV01();

      double irr_2 = pricer.Irr();
      double asw_2 = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);

      double zspread_2 = pricer.ImpliedZSpread();
      double cdsBasis_2 = pricer.ImpliedCDSSpread();
      double cdsLevel_2 = pricer.ImpliedCDSLevel();

      double spread01_2 = pricer.Spread01();

      double zspread01_2 = pricer.ZSpread01();

      double ir01_2 = pricer.Rate01();


      Assert.AreEqual(yield_1, yield_2, 0.001, "yield does not match");
      Assert.AreEqual(pv01_1, pv01_2, 0.001, "pv01 does not match");
      Assert.AreEqual(irr_1, irr_2, 0.001, "irr does not match");
      Assert.AreEqual(asw_1, asw_2, 0.001, "asw does not match");
      Assert.AreEqual(zspread_1, zspread_2, 0.001, "Zspread does not match");
      Assert.AreEqual(cdsBasis_1, cdsBasis_2, 0.001, "cds basis does not match");
      Assert.AreEqual(cdsLevel_1, cdsLevel_2, 0.001, "cds level does not match");
      Assert.AreEqual(spread01_1, spread01_2, 0.001, "spread 01 does not match");
      Assert.AreEqual(zspread01_1, zspread01_2, 0.001, "zspread01 dpes not matcj");
      Assert.AreEqual(ir01_1, ir01_2, 0.001, "rateor does not match");
      Assert.AreEqual(fullPrice_1, fullPrice_2, 0.001, "full price does not match");
    }

    /// <summary>
    /// Tests the pricing and analytics for a UK Gilt which settles on a exdiv date
    /// </summary>
    [Test]
    public void UKGiltSettleOnExDiv()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(07, 01, 2010);
      Dt settlement = new Dt(07, 01, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.937585087;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      double fullPrice = pricer.FullPrice();
      Assert.AreEqual(-8, pricer.AccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(-0.00108695652, pricer.AccruedInterest(), 1E-9, "Accrued Interest does not match");
      Assert.AreEqual(0.936498, pricer.FullPrice(), 1E-9, "Full Price does not match");
      Assert.AreEqual(0.05323588101, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.05006539836, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.07341397, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, pricer.FullPrice());
      quoteDict.Add(QuotingConvention.Yield, yield);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
      }
    }

    // Tests the pricing and analytics for a UK Gilt which settles within exdiv period vs the equivalent customized ex-div schedule
    [Test]
    public void CustomizedExDivVsUKGilt()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(10, 06, 2010);
      Dt settlement = new Dt(10, 06, 2010);
      double coupon = 0.10;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      //setup Bond
      Bond bUKGilt = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal)
        { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(bUKGilt, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1000000.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      //now we check that for all the common quoting conventions the two Ex-div valuations match each other
      double yield = pricer.YieldToMaturity();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, pricer.FullPrice());
      quoteDict.Add(QuotingConvention.Yield, yield);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        customDivPricer.MarketQuote = kvp.Value;
        customDivPricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(0, pricer.AccruedInterest() - customDivPricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.AccrualDays() - customDivPricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.FullPrice() - customDivPricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.ImpliedZSpread() - customDivPricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(0,
          pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly) -
          customDivPricer.AssetSwapSpread(Toolkit.Base.DayCount.Actual360, Frequency.Quarterly), TOLERANCE,
          "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.PV01() - customDivPricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.Pv() - customDivPricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.ModDuration() - customDivPricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.Convexity() - customDivPricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.Irr() - customDivPricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.FullModelPrice() - customDivPricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.ImpliedCDSSpread() - customDivPricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.ImpliedCDSLevel() - customDivPricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.Spread01() - customDivPricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(0, pricer.SpreadDuration() - customDivPricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.SpreadConvexity() - customDivPricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.ZSpread01() - customDivPricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.ZSpreadDuration() - customDivPricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.Rate01() - customDivPricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(0, pricer.RateDuration() - customDivPricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
      }
    }

    // Tests the pricing and analytics for customized ex-div bond
    [Test]
    public void CustomizedExDivBeforeExDivDate()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(4, 06, 2010);
      Dt settlement = new Dt(04, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal)
        { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(17100, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(1.71, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(171, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(101.71, customDivPricer.FullPrice() * 100.00,
        "FUll price does not match ");
      Assert.AreEqual(958231.723, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond on ex-div date and not cum div
    [Test]
    public void CustomizedExDivOnExDivDateNonCumDiv()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(7, 06, 2010);
      Dt settlement = new Dt(07, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(-800, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(-0.08, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(-8, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(99.92, customDivPricer.FullPrice() * 100.00,
        "FUll price does not match ");
      Assert.AreEqual(940632.178, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond on ex-div date but trade settles cum div
    [Test]
    public void CustomizedExDivOnExDivDateCumDiv()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(7, 06, 2010);
      Dt settlement = new Dt(7, 06, 2010);
      Dt tradeSettle = new Dt(04, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4) { TradeSettle = tradeSettle };
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(17400, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(-0.08, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(-8, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(99.92, customDivPricer.FullPrice() * 100.00,
        "FUll price does not match ");
      Assert.AreEqual(958816.473, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond after ex-div date and not cum div
    [Test]
    public void CustomizedExDivAfterExDivDateNonCumDiv()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(8, 06, 2010);
      Dt settlement = new Dt(8, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(-700, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(-0.07, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(-7, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(99.93, customDivPricer.FullPrice()*100.00, 1E-13,
        "FUll price does not match ");
      Assert.AreEqual(940825.933, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond after ex-div date but trade settles cum div
    [Test]
    public void CustomizedExDivAfterExDivDateCumDiv()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(8, 06, 2010);
      Dt settlement = new Dt(08, 06, 2010);
      Dt tradeSettle = new Dt(04, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4)
        { TradeSettle = tradeSettle };
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(17500, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(-0.07, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(-7, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(99.93, customDivPricer.FullPrice()*100.00, 1E-13,
        "FUll price does not match ");
      Assert.AreEqual(959012.195, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond after ex-div date but trade settles cum div, plus the trade is priced at the last coupon period of the bond
    [Test]
    public void BondLastPeriodExDivPricedAfterExDivDateCumDiv()
    {
      var maturity = new Dt(15, 06, 2010);
      var issue = new Dt(15, 06, 2008);
      var asOf = new Dt(8, 06, 2010);
      var settlement = new Dt(08, 06, 2010);
      var tradeSettle = new Dt(04, 06, 2010);
      const double coupon = 0.0365;
      const DayCount dayCount = DayCount.Actual365Fixed;
      var cal = Calendar.LNB;
      const BDConvention roll = BDConvention.Following;
      const Frequency freq = Frequency.SemiAnnual;
      const Currency ccy = Currency.USD;

      var bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      var sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4)
      {
        TradeSettle = tradeSettle,
        Notional = 1000000.0,
        MarketQuote = 1,
        QuotingConvention = QuotingConvention.FlatPrice
      };

      Assert.AreEqual(17500, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(-0.07, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(-7, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(99.93, customDivPricer.FullPrice() * 100.00, 1E-13,
        "FUll price does not match ");
      Assert.AreEqual(1017686.918, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond after ex-div date and the trade settles after ex-div date, but the priced in the last coupon period thus its cashflow shall include principal payback.
    [Test]
    public void BondLastPeriodExDivPricedAfterExDivDateNonCumDiv()
    {
      var maturity = new Dt(15, 06, 2010);
      var issue = new Dt(15, 06, 2008);
      var asOf = new Dt(8, 06, 2010);
      var settlement = new Dt(08, 06, 2010);
      var tradeSettle = new Dt(08, 06, 2010);
      const double coupon = 0.0365;
      const DayCount dayCount = DayCount.Actual365Fixed;
      var cal = Calendar.LNB;
      const BDConvention roll = BDConvention.Following;
      const Frequency freq = Frequency.SemiAnnual;
      const Currency ccy = Currency.USD;

      var bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      var sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4)
      {
        TradeSettle = tradeSettle,
        Notional = 1000000.0,
        MarketQuote = 1,
        QuotingConvention = QuotingConvention.FlatPrice
      };

      Assert.AreEqual(-700, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(-0.07, customDivPricer.AccruedInterest()*100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(-7, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(99.93, customDivPricer.FullPrice()*100.00, 1E-13,
        "FUll price does not match ");
      Assert.AreEqual(999501.362, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond reaching next coupon date and not cum div
    [Test]
    public void CustomizedExDivOnNextCpnDateNonCumDiv()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(15, 06, 2010);
      Dt settlement = new Dt(15, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(0, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(0.0, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(0, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(100, customDivPricer.FullPrice() * 100.00,
        "FUll price does not match ");
      Assert.AreEqual(942179.482, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    // Tests the pricing and analytics for customized ex-div bond reaching next coupon date but trade settles cum div
    [Test]
    public void CustomizedExDivOnNextCpnDateCumDiv()
    {
      Dt maturity = new Dt(15, 06, 2013);
      Dt issue = new Dt(15, 06, 2008);
      Dt asOf = new Dt(15, 06, 2010);
      Dt settlement = new Dt(15, 06, 2010);
      Dt tradeSettle = new Dt(04, 06, 2010);
      double coupon = 0.0365;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.LNB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;

      Bond bExDiv = new Bond(issue, maturity, ccy, BondType.USCorp, coupon, dayCount, CycleRule.None, freq, roll, cal) { BondExDivRule = new ExDivRule(6, true) };
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);

      var customDivPricer = new BondPricer(bExDiv, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4) { TradeSettle = tradeSettle };
      customDivPricer.Notional = 1000000.0;
      customDivPricer.MarketQuote = 1;
      customDivPricer.QuotingConvention = QuotingConvention.FlatPrice;

      Assert.AreEqual(0, customDivPricer.Accrued(), TOLERANCE, "Accrued does not match ");
      Assert.AreEqual(0, customDivPricer.AccruedInterest() * 100.0, TOLERANCE, "AI does not match");
      Assert.AreEqual(0, customDivPricer.AccrualDays(),
        "Accrual days does not match ");
      Assert.AreEqual(100, customDivPricer.FullPrice() * 100.00,
        "FUll price does not match ");
      Assert.AreEqual(942179.482, customDivPricer.Pv(), TOLERANCE, "Pv does not match");
      Assert.AreEqual(0.0365, customDivPricer.YieldToMaturity(), TOLERANCE,
        "yield does not match");
    }

    #endregion 

    #region Bond vs Swap

    [Test]
    public void FloatingBondvsSwapWithReset()
    {
      Dt maturity = new Dt(11, 06, 2016);
      Dt issue = new Dt(11, 06, 2011);
      Dt resetDate = new Dt(09, 06, 2011);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(07, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      var refIndex = new InterestRateIndex("Test_Index", freq, ccy, dayCount, cal, 2);
      var bond = new Bond(issue, maturity, ccy, type, coupon, dayCount, CycleRule.None, freq, roll, cal)
      {
        Index = refIndex.IndexName,
        ReferenceIndex = refIndex,
        ResetLag = new Tenor(2, TimeUnit.Days)
      };
      bond.ReferenceIndex = refIndex;

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      var bondPricer = new BondPricer(bond, asOf, settlement, irCurve, null, 0, TimeUnit.None, 0.0)
      {
        Notional = 1.0,
        ReferenceCurve = irCurve,
        EnableZSpreadAdjustment = false
      };
      bondPricer.RateResets.Add(new RateReset(resetDate, 0.0071));
      var swapLeg = new SwapLeg(issue, maturity, freq, coupon, refIndex) { FinalExchange = true, ResetLag = bond.ResetLag };
      var swapPricer = new SwapLegPricer(swapLeg, asOf, settlement, 1, irCurve, refIndex, irCurve, null, null,
        null);
      swapPricer.RateResets = new RateResets();
      swapPricer.RateResets.Add(new RateReset(resetDate, 0.0071));
      Assert.AreEqual(0.0,
        bondPricer.Pv() - swapPricer.Pv(), TOLERANCE,
        "The Pv difference between same bond and swap should be 0.0");

      Assert.AreEqual(0.0,
        bondPricer.Accrued() - swapPricer.Accrued(), TOLERANCE,
        "The Accrued difference between same bond and swap should be 0.0");

      var scheme = InterpScheme.FromString("Weighted", ExtrapMethod.Const, ExtrapMethod.Const);
      var swapRate01 = Sensitivities.SwapIR01(swapPricer, "Pv", 1, 1,
        true, scheme, "Bootstrap", null);
      Assert.AreEqual(0.0,
        bondPricer.Rate01(1) - swapRate01, TOLERANCE,
        "The Rate01 difference between same bond and swap should be 0.0");


    }

    [Test]
    public void FloatingBondvsSwapNoReset()
    {
      Dt maturity = new Dt(07, 07, 2016);
      Dt issue = new Dt(07, 07, 2011);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(07, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      var refIndex = new InterestRateIndex("Test_Index", freq, ccy, dayCount, cal, 2);
      var bond = new Bond(issue, maturity, ccy, type, coupon, dayCount, CycleRule.None, freq, roll, cal)
      {
        Index = refIndex.IndexName,
        ReferenceIndex = refIndex,
        ResetLag = new Tenor(2, TimeUnit.Days)
      };
      bond.ReferenceIndex = refIndex;
      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      var bondPricer = new BondPricer(bond, asOf, settlement, irCurve, null, 0, TimeUnit.None, 0.0)
      {
        Notional = 1.0,
        ReferenceCurve = irCurve,
        EnableZSpreadAdjustment = false
      };
      var swapLeg = new SwapLeg(issue, maturity, freq, coupon, refIndex) { FinalExchange = true, ResetLag = bond.ResetLag };
      var swapPricer = new SwapLegPricer(swapLeg, asOf, settlement, 1, irCurve, refIndex, irCurve, null, null,
        null);
      swapPricer.RateResets = new RateResets();
      Assert.AreEqual(0.0,
        bondPricer.Pv() - swapPricer.Pv(), TOLERANCE,
        "The Pv difference between same bond and swap should be 0.0");

      Assert.AreEqual(0.0,
        bondPricer.Accrued() - swapPricer.Accrued(), TOLERANCE,
        "The Accrued difference between same bond and swap should be 0.0");
      var scheme = InterpScheme.FromString("Weighted", ExtrapMethod.Const, ExtrapMethod.Const);
      var swapRate01 = Sensitivities.SwapIR01(swapPricer, "Pv", 1, 1,
        true, scheme, "Bootstrap", null);
      Assert.AreEqual(0.0,
        bondPricer.Rate01(1) - swapRate01, TOLERANCE,
        "The Rate01 difference between same bond and swap should be 0.0");

    }

    [Test]
    public void FloatingBondvsSwapWithCycleRule()
    {
      Dt maturity = new Dt(07, 08, 2013);
      Dt issue = new Dt(07, 07, 2011);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(07, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      var refIndex = new InterestRateIndex("Test_Index", freq, ccy, dayCount, cal, 2);
      var bond = new Bond(issue, maturity, ccy, type, coupon, dayCount, CycleRule.None, freq, roll, cal)
      {
        Index = refIndex.IndexName,
        ReferenceIndex = refIndex,
        ResetLag = new Tenor(2, TimeUnit.Days),
        CycleRule = CycleRule.Seventh
      };

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      var bondPricer = new BondPricer(bond, asOf, settlement, irCurve, null, 0, TimeUnit.None, 0.0)
      {
        Notional = 1.0,
        ReferenceCurve = irCurve,
        EnableZSpreadAdjustment = false
      };
      var swapLeg = new SwapLeg(issue, maturity, freq, coupon, refIndex)
      {
        FinalExchange = true,
        CycleRule = CycleRule.Seventh,
        ResetLag = bond.ResetLag
      };
      var swapPricer = new SwapLegPricer(swapLeg, asOf, settlement, 1, irCurve, refIndex, irCurve, null, null,
        null);
      swapPricer.RateResets = new RateResets();
      Assert.AreEqual(0.0,
        bondPricer.Pv() - swapPricer.Pv(), TOLERANCE,
        "The Pv difference between same bond and swap should be 0.0");

      Assert.AreEqual(0.0,
        bondPricer.Accrued() - swapPricer.Accrued(), TOLERANCE,
        "The Accrued difference between same bond and swap should be 0.0");
      var scheme = InterpScheme.FromString("Weighted", ExtrapMethod.Const, ExtrapMethod.Const);
      var swapRate01 = Sensitivities.SwapIR01(swapPricer, "Pv", 1, 1,
        true, scheme, "Bootstrap", null);
      Assert.AreEqual(0.0,
        bondPricer.Rate01(1) - swapRate01, TOLERANCE,
        "The Rate01 difference between same bond and swap should be 0.0");

    }

    #endregion

    #region Bond Accrued Interest Tests with different DayCount and Accrual DayCount

    /// <summary>
    /// Tests the accrued interest calculations for some special bonds 
    /// with daycount for accrued different from coupon daycount
    /// </summary>
    /// <remarks>
    /// Test is related to feature request in FB24379
    /// </remarks>
    [Test]
    public void BondAccruedDayCountNotSameAsDayCountFRN()
    {
      //setup FRN
      Bond b = new Bond(
        new Dt(28, 08, 2007),
        new Dt(28, 06, 2015),
        Currency.USD,
        BondType.USCorp,
        0.01,
        DayCount.Actual360,
        CycleRule.None,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.Tenor = new Tenor(3, TimeUnit.Months);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = true;
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);

      //setup pricer
      var asOf = new Dt(9, 6, 2011);
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 4000000.0, ReferenceCurve = irCurve };
      pricer.CurrentRate = 0.03421;
      pricer.MarketQuote = 1.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      //get values
      double accrued = pricer.AccruedInterest() * pricer.Notional;
      var copy = (Bond)b.Clone();
      copy.AccrualDayCount = DayCount.Thirty360;

      var newPricer = new BondPricer(copy, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 4000000.0, ReferenceCurve = irCurve };
      newPricer.MarketQuote = 1.0;
      newPricer.QuotingConvention = QuotingConvention.FlatPrice;
      newPricer.CurrentRate = 0.03421;
      var newAccrued = newPricer.AccruedInterest() * newPricer.Notional;

      //test against two copies of pricer, only diference is AccralDayCount
      Assert.AreEqual(26987.89, accrued, 0.01, "The accrued interest is incorrect");
      Assert.AreEqual(26227.67, newAccrued, 0.01, "The accrued interest is incorrect");
      Assert.AreEqual(0.0, pricer.Pv() - newPricer.Pv(), "The pv difference shall be zero");
      Assert.AreNotEqual(0.0, pricer.Duration() - newPricer.Duration(),"The Duration difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.Convexity() - newPricer.Convexity(), "The Convexity difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.PV01() * 10000 - newPricer.PV01() * 10000, "The dP/dY difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.YieldToMaturity() - newPricer.YieldToMaturity(), "The dP/dY difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.ImpliedZSpread() - newPricer.ImpliedZSpread(),"The ZSpread difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.ImpliedCDSLevel() - newPricer.ImpliedCDSLevel(), "The Implied CDS level difference shall not be zero");
    }

    /// <summary>
    /// Tests the accrued interest calculations for some special bonds 
    /// with daycount for accrued different from coupon daycount
    /// </summary>
    /// <remarks>
    /// Test is related to feature request in FB24379
    /// </remarks>
    [Test]
    public void BondAccruedDayCountNotSameAsDayCountFixedCpn()
    {
      //setup Bond
      Bond b = new Bond(
        new Dt(15, 07, 2008),
        new Dt(15, 01, 2015),
        Currency.USD,
        BondType.USCorp,
        0.05,
        DayCount.Actual360,
        CycleRule.None,
        Frequency.SemiAnnual,
        BDConvention.Following,
        Calendar.NYB);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      var asOf = new Dt(09, 06, 2011);
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);

      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 4000000.0 };

      //get values
      double accrued = pricer.AccruedInterest() * pricer.Notional;
      var copy = (Bond)b.Clone();
      copy.AccrualDayCount = DayCount.Thirty360;
      var newPricer = new BondPricer(copy, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 4000000.0 };
      var newAccrued = newPricer.AccruedInterest() * newPricer.Notional;
      pricer.MarketQuote = 1.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      newPricer.MarketQuote = 1.0;
      newPricer.QuotingConvention = QuotingConvention.FlatPrice;

      //test against two copies of pricer, only diference is AccralDayCount
      Assert.AreEqual(80555.56, accrued, 0.01, "The accrued interest is incorrect");
      Assert.AreEqual(80000.00, newAccrued, 0.01, "The accrued interest is incorrect");
      Assert.AreEqual(0.0, pricer.Pv() - newPricer.Pv(), 1E-9, "The pv difference shall be zero");
      Assert.AreNotEqual(0.0, pricer.Duration() - newPricer.Duration(), "The Duration difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.Convexity() - newPricer.Convexity(), "The Convexity difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.PV01() * 10000 - newPricer.PV01() * 10000, "The dP/dY difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.YieldToMaturity() - newPricer.YieldToMaturity(),  "The dP/dY difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.ImpliedZSpread() - newPricer.ImpliedZSpread(), "The ZSpread difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.ImpliedCDSLevel() - newPricer.ImpliedCDSLevel(), "The Implied CDS level difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.SemiAnnual) - newPricer.AssetSwapSpread(DayCount.Actual360, Frequency.SemiAnnual), "The ASW difference shall not be zero");
    }

    /// <summary>
    /// Tests the accrued interest calculations for some special bonds 
    /// with daycount for accrued different from coupon daycount
    /// </summary>
    /// <remarks>
    /// Test is related to feature request in FB24379
    /// </remarks>
    [Test]
    public void BondAccruedDayCountNotSameAsDayCountFixedCpn2()
    {
      //setup Bond
      Bond b = new Bond(
        new Dt(15, 07, 2008),
        new Dt(15, 01, 2015),
        Currency.USD,
        BondType.USCorp,
        0.05,
        DayCount.Actual360,
        CycleRule.None,
        Frequency.SemiAnnual,
        BDConvention.Following,
        Calendar.NYB);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = true;

      //setup pricer
      var asOf = new Dt(09, 06, 2011);
      var irCurve =TestBond.CreateDiscountCurveForAmortBond(asOf);

      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 4000000.0 };

      //get values
      double accrued = pricer.AccruedInterest() * pricer.Notional;
      var copy = (Bond)b.Clone();
      copy.AccrualDayCount = DayCount.Thirty360;
      var newPricer = new BondPricer(copy, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 4000000.0 };
      var newAccrued = newPricer.AccruedInterest() * newPricer.Notional;
      pricer.MarketQuote = 1.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      newPricer.MarketQuote = 1.0;
      newPricer.QuotingConvention = QuotingConvention.FlatPrice;

      //test against two copies of pricer, only diference is AccralDayCount
      Assert.AreEqual(78888.89, accrued, 0.01, "The accrued interest is incorrect");
      Assert.AreEqual(78333.33, newAccrued, 0.01, "The accrued interest is incorrect");
      Assert.AreEqual(0.0, pricer.Pv() - newPricer.Pv(), 1E-9, "The pv difference shall be zero");
      Assert.AreNotEqual(0.0, pricer.Duration() - newPricer.Duration(), "The Duration difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.Convexity() - newPricer.Convexity(), "The Convexity difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.PV01() * 10000 - newPricer.PV01() * 10000, "The dP/dY difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.YieldToMaturity() - newPricer.YieldToMaturity(), "The dP/dY difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.ImpliedZSpread() - newPricer.ImpliedZSpread(), "The ZSpread difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.ImpliedCDSLevel() - newPricer.ImpliedCDSLevel(), "The Implied CDS level difference shall not be zero");
      Assert.AreNotEqual(0.0, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.SemiAnnual) - newPricer.AssetSwapSpread(DayCount.Actual360, Frequency.SemiAnnual), "The ASW difference shall not be zero");
    }

    #endregion

    #region Forward-Issuing Bond Tests 

    /// <summary>
    /// Tests for a forward-issue bond where the settle date is before the bond effective date, make sure the calculation outputs match with expected values and agree in different quoting conventions
    /// </summary>
    [Test]
    public void USForwardIssueBondTest()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2010);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(08, 07, 2010);
      double coupon = 0.064;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = issue;
      pricer.ForwardSettle = issue;
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.ForwardFlatPrice;

      double fullPrice = pricer.SpotFullPrice();
      Assert.AreEqual(0, pricer.AccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0, pricer.AccruedInterest(), "Accrued Interest does not match");
      Assert.AreEqual(0.99991806, RoundingUtil.Round(fullPrice, 8), "Full Price does not match");
      Assert.AreEqual(0.046104328, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.045637204, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.064, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, fullPrice);
      quoteDict.Add(QuotingConvention.ASW_Par, asw);
      quoteDict.Add(QuotingConvention.ZSpread, pricer.ImpliedZSpread());

      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, RoundingUtil.Round(pricer.FullPrice(), 6), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);


      }

    }
    #endregion

    #region Forward-Settle Bond Trade
    /// <summary>
    /// Tests for a forward-settle bond where the trade settles beyond the bond product settlement date and falls right on one of it's coupon date,
    /// the bond cash flow is not supposed to include that coupon payment on trade settle
    /// </summary>
    [Test]
    public void USForwardSettleBondTestOnCpnDay()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(08, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      RateQuoteCurve repoCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(15, 07, 2010);
      pricer.ForwardSettle = new Dt(15, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.ForwardFlatPrice;
      pricer.RepoCurve = repoCurve;
      double fullPrice = pricer.SpotFullPrice();
      Assert.AreEqual(174, pricer.SpotAccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0.024033, RoundingUtil.Round(pricer.AccruedInterest(pricer.ProductSettle, pricer.ProductSettle), 6), "Accrued Interest does not match");
      Assert.AreEqual(1.024868, RoundingUtil.Round(fullPrice, 6), "Full Price does not match");
      Assert.AreEqual(0.02180046, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.031877869, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.05, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");


    }

    /// <summary>
    /// Tests for a regular bond where the trade settles beyond the bond product settlement date and falls right on one of it's coupon date,
    /// the bond cash flow is not supposed to include that coupon payment on trade settle
    /// </summary>
    [Test]
    public void USRegularBondTestOnCpnDay()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(12, 07, 2010);
      Dt settlement = new Dt(15, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      RateQuoteCurve repoCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(15, 07, 2010);
      pricer.ForwardSettle = new Dt(15, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.RepoCurve = repoCurve;
      double fullPrice = pricer.SpotFullPrice();
      Assert.AreEqual(0, pricer.SpotAccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0.0, RoundingUtil.Round(pricer.AccruedInterest(pricer.ProductSettle, pricer.ProductSettle), 6), "Accrued Interest does not match");
      Assert.AreEqual(1.0, RoundingUtil.Round(fullPrice, 6), "Full Price does not match");
      Assert.AreEqual(0.03227794, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.03215161663, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.05, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");


    }

    /// <summary>
    /// Tests for a forward-settle bond where the trade settles beyond the bond product settlement date and falls 3 days before one of it's coupon date,
    /// the bond cash flow is supposed to include the next coupon payment close to trade settle
    /// </summary>
    [Test]
    public void USForwardSettleBondTestBeforeCpnDay()
    {
      Dt maturity = new Dt(15, 01, 2013);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(08, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(12, 01, 2011);
      pricer.ForwardSettle = new Dt(12, 01, 2011);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.ForwardFlatPrice;
      pricer.RepoCurve = rateQuoteCurve;
      double fullPrice = pricer.SpotFullPrice();
      Assert.AreEqual(174, pricer.SpotAccrualDays(), "Accrual days do not match ");
      Assert.AreEqual(0.024033, RoundingUtil.Round(pricer.AccruedInterest(pricer.ProductSettle, pricer.ProductSettle), 6), "Accrued Interest does not match");
      Assert.AreEqual(1.042619, RoundingUtil.Round(fullPrice, 6), "Full Price does not match");
      Assert.AreEqual(0.01454634, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.0275588, pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.05, pricer.YieldToMaturity(), TOLERANCE, "ytm does not match");

    }

    /// <summary>
    /// Tests for a forward-settle bond where the trade settles beyond the bond product settlement date and setles after one of it's coupon date,
    /// the bond cash flow is not supposed to include the coupon payment before trade settle, the risk outputs shall be consistent with different quoting convention
    /// </summary>
    [Test]
    public void USForwardSettleBondTestTradeSettleAfterCpnDay()
    {
      Dt maturity = new Dt(18, 01, 2012);
      Dt issue = new Dt(18, 07, 2009);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(05, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = TestBond.CreateSurvivalCurveForAmortBond(asOf, irCurve, 0.4);
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(05, 07, 2010);
      pricer.ForwardSettle = new Dt(05, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 0.97;
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.RepoCurve = rateQuoteCurve;
      double fullPrice = pricer.SpotFullPrice();
      double spotYield = pricer.YieldToMaturity();
      double zSpread = pricer.ImpliedZSpread();
      double ASW = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double flatPrice = pricer.SpotFlatPrice();

      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, fullPrice);
      quoteDict.Add(QuotingConvention.FlatPrice, flatPrice);
      quoteDict.Add(QuotingConvention.ASW_Par, ASW);
      quoteDict.Add(QuotingConvention.ZSpread, zSpread);
      quoteDict.Add(QuotingConvention.Yield, spotYield);

      Assert.AreEqual(0.023204, RoundingUtil.Round(pricer.AccruedInterest(pricer.ProductSettle, pricer.ProductSettle), 6), "Accrued Interest does not match");
      Assert.AreEqual(0.97, RoundingUtil.Round(fullPrice, 2), "Full Price does not match");
      Assert.AreEqual(0.07317, RoundingUtil.Round(pricer.ImpliedZSpread(), 5), TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(0.06946, RoundingUtil.Round(pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly), 5),
        TOLERANCE, "ASW does not match");
      Assert.AreEqual(0.08776, RoundingUtil.Round(pricer.YieldToMaturity(), 5), TOLERANCE, "ytm does not match");

      pricer.TradeSettle = new Dt(21, 07, 2010);
      pricer.ForwardSettle = new Dt(21, 07, 2010);

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double yield = pricer.YieldToMaturity();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double asw = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double asw_par = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double irCOnvexity = pricer.RateConvexity();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();
      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        var spotFullPrice = pricer.SpotFullPrice();
        Assert.AreEqual(fullPrice, RoundingUtil.Round(spotFullPrice, 6), "Full price does not match for qc " + kvp.Key);
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(), "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE, "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(asw, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly), TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE, "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE, "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE, "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE, "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE, "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE, "Spread01 does not match for qc " + kvp.Key);

        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE, "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE, "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE, "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE, "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE, "rate Duration does not match for qc " + kvp.Key);
      }

    }


    /// <summary>
    /// Tests for a US bond where the product has empty DaysToSettle and the trade settles later than trade date, priced between trade date and trade settle date
    /// This case also tests if the next coupon is included in bond cashflow
    /// </summary>
    [Test]
    public void USForwardSettleBondTestZeroDaysToSettleTheta()
    {
      Dt maturity = new Dt(15, 01, 2011);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(05, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = null;
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(08, 07, 2010);
      pricer.ForwardSettle = new Dt(08, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.RepoCurve = rateQuoteCurve;
      double theta = Sensitivities.Theta(pricer, null, Dt.AddDays(asOf, 1, Calendar.NYB), pricer.ForwardSettle,
        ThetaFlags.None, SensitivityRescaleStrikes.UsePricerSetting);
      Assert.AreEqual(49.37487857 / 1000000, theta, 1E-9, "Theta does not match ");
      Assert.AreEqual(1.043213853, pricer.Pv(), 1E-9, "Pv does not match");
    }


    /// <summary>
    /// Tests for a US bond for PV calculation where trade settles after a coupon date, see if the coupon has been excluded from cashflow
    /// </summary>
    [Test]
    public void USForwardSettleBondRiskFreePvAfterCpnDay()
    {
      Dt maturity = new Dt(15, 01, 2011);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(05, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = null;
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(19, 07, 2010);
      pricer.ForwardSettle = new Dt(19, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.RepoCurve = rateQuoteCurve;
      Assert.AreEqual(1.018215902, pricer.Pv(), 1E-9, "Pv does not match");
    }

    /// <summary>
    /// Tests for a forward settle ex-div bond where the trade settlement date is within ex-Div range
    /// </summary>
    [Test]
    public void USForwardSettleBondTestPVSettleWithinExDivRange()
    {
      Dt maturity = new Dt(15, 01, 2011);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(05, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = null;
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(14, 07, 2010);
      pricer.ForwardSettle = new Dt(14, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.RepoCurve = rateQuoteCurve;
      Assert.AreEqual(1.018215902, pricer.Pv(), 1E-9, "Pv does not match");
    }

    /// <summary>
    /// Tests for a forward settle ex-div bond where the trade settlement date is close but before ex-Div range
    /// </summary>
    [Test]
    public void USForwardSettleBondTestPVSettleOutsideExDivRange()
    {
      Dt maturity = new Dt(15, 01, 2011);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(05, 07, 2010);
      Dt settlement = new Dt(05, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = null;
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(06, 07, 2010);
      pricer.ForwardSettle = new Dt(06, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.RepoCurve = rateQuoteCurve;
      Assert.AreEqual(1.043213853, pricer.Pv(), 1E-9, "Pv does not match");
    }

    /// <summary>
    /// Tests for a forward settle ex-div bond where the pricing as of date is within ex-Div range, this is to make sure the next coupon not included in repo cash flow
    /// </summary>
    [Test]
    public void USForwardSettleBondTestPVAsOfDateWithinExDivRange()
    {
      Dt maturity = new Dt(15, 01, 2011);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(08, 07, 2010);
      Dt settlement = new Dt(08, 07, 2010);
      double coupon = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.UKGilt;


      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      DiscountCurve irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = null;
      RateQuoteCurve rateQuoteCurve = TestBond.CreateRepoCurveForForwardSettleBond(asOf, Currency.USD);
      //setup pricer
      BondPricer pricer = new BondPricer(b, asOf, settlement, irCurve, sc, 0, TimeUnit.None, -0.4);
      pricer.TradeSettle = new Dt(8, 07, 2010);
      pricer.ForwardSettle = new Dt(16, 07, 2010);
      pricer.Notional = 1.0;
      pricer.MarketQuote = 1;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.RepoCurve = rateQuoteCurve;
      var spotFullPrice = pricer.SpotFullPrice();
      Assert.AreEqual(0.999033, RoundingUtil.Round(spotFullPrice, 6), "Full price does not match");
    }

    #endregion

    #region Implied CDS Spread Test
    /// <summary>
    /// Test for solving Implied CDS Spreads against distressed bond prices. 
    /// </summary>
    /// <remarks>
    /// <para>Test arose from FB 15118 reported by Jacques from Orchard Group during the eval process.</para>
    /// <para>Basically when a bond has a distressed price the solving mechanism for Implied CDS Spreads 
    /// was range bound between 0 and 5000. This needed to change to handle distressed market environments.</para>
    /// <para>This test case comes from data obtained from Jacques.</para>
    /// </remarks>
    /// 
    [Test, Smoke]
    public void DistressedBondImpliedCDSSpreadTest()
    {
      Dt asOf = new Dt(4, 3, 2009);
      Dt settle = new Dt(4, 4, 2009);
      DiscountCurve dc = new DiscountCurve(asOf, 0.0204);
      double recoveryRate = 0;
      Bond bond = new Bond(asOf, new Dt(1, 1, 2012), Currency.USD, BondType.USCorp, 0.11, DayCount.Thirty360, CycleRule.None, Frequency.Quarterly, BDConvention.None, Calendar.None);
      BondPricer pricer = new BondPricer(bond, asOf, settle, dc, null, 0, TimeUnit.None, recoveryRate);

      // Setup pricer
      pricer.Notional = 10000000.0;
      pricer.MarketQuote = 0.23;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      // Calculate implied spread
      SurvivalCurve impliedCurve = pricer.ImpliedFlatCDSCurve(recoveryRate);

      // Test
      Assert.IsNotNull(impliedCurve, "Implied Curve was NULL!");
    }
    #endregion

    #region Krgin Tests

    #region Settlementdate in first coupon period related tests 

    /// <summary>
    /// This tests the scenario wheer the first coupon is of short length 
    /// ref Krigin Pg 9
    /// </summary>
    [Test]
    public void ShortFirstCouponPeriod()
    {
      Dt issue = new Dt(1, 9, 2000);
      Dt maturity = new Dt(1, 6, 2004);
      Dt settlement = new Dt(1, 10, 2000);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Dt normalIssueDate = Dt.Subtract(firstCoupon, freq, true);

      double NIF = Dt.Diff(normalIssueDate, firstCoupon, dayCount);


      //Difference between the Dated date and settlemenrt 
      double DDS = Dt.Diff(issue, settlement, dayCount);

      //Difference between the Dated date and the first coupon date
      double DDF = Dt.Diff(issue, firstCoupon, dayCount);

      //Difference between the settlement date and the first coupon date 
      double SF = Dt.Diff(settlement, firstCoupon, dayCount);

      double AI = (DDS / NIF) * (coupon / (int)freq);

      double FCF = DDF / NIF;

      double DF = SF / NIF;

      Dt nextCoupon = Dt.Add(firstCoupon, freq, true);


      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(FCF,
        BondModelUtil.FirstCouponFactor(settlement, issue, firstCoupon, freq, true, dayCount, type), TOLERANCE,
        "FCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");
    }

    /// <summary>
    /// This tests the scenario where the first coupon operiod is of normal length and the settle date falls between the dated date and the first
    /// coupon date 
    /// ref Krigin Pg 9 
    /// </summary>
    [Test]
    public void NormalFirstCouponPeriod()
    {
      Dt issue = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 6, 2004);
      Dt settlement = new Dt(1, 9, 2000);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Dt normalIssueDate = Dt.Subtract(firstCoupon, freq, true);
      Dt pseudoIssueDate = Dt.Subtract(normalIssueDate, freq, true);

      //Difference between the Dated date and settlemenrt 
      double DDS = Dt.Diff(issue, settlement, dayCount);

      //Difference between the Dated date and the first coupon date
      double DDF = Dt.Diff(issue, firstCoupon, dayCount);

      //Difference between the settlement date and the first coupon date 
      double SF = Dt.Diff(settlement, firstCoupon, dayCount);

      double AI = (DDS / DDF) * (coupon / (int)freq);

      double FCF = 1;
      double DF = SF / DDF;

      Dt nextCoupon = Dt.Add(firstCoupon, freq, true);


      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(FCF,
        BondModelUtil.FirstCouponFactor(settlement, issue, firstCoupon, freq, true, dayCount, type), TOLERANCE,
        "FCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");
    }

    /// <summary>
    /// This tests the scenario wheer the settlement date lies in between the dated date and the normal issue date 
    /// ref :Krigin Pg 10 
    /// </summary>
    [Test]
    public void LongFirstCouponPeriodCase1()
    {
      Dt issue = new Dt(1, 3, 2000);
      Dt maturity = new Dt(1, 6, 2004);
      Dt settlement = new Dt(1, 4, 2000);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Dt normalIssueDate = Dt.Subtract(firstCoupon, freq, true);
      Dt pseudoIssueDate = Dt.Subtract(normalIssueDate, freq, true);


      //Difference between the issue date and settle date
      double DDS = Dt.Diff(issue, settlement, dayCount);

      //Difference between the Pseudo Issue and Normal Issue date 
      double PINI = Dt.Diff(pseudoIssueDate, normalIssueDate, dayCount);

      //Difference between the Dated date and normal issue date 
      double DDNI = Dt.Diff(issue, normalIssueDate, dayCount);

      //Difference between the Settlement date and normal issue date 
      double SNI = Dt.Diff(settlement, normalIssueDate, dayCount);

      double AI = (DDS / PINI) * (coupon / (int)freq);
      double FCF = (DDNI / PINI) + 1;
      double DF = (SNI / PINI) + 1;


      Dt nextCoupon = Dt.Add(firstCoupon, freq, true);


      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(FCF,
        BondModelUtil.FirstCouponFactor(settlement, issue, firstCoupon, freq, true, dayCount, type), TOLERANCE,
        "FCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");
    }

    /// <summary>
    /// This tests the scenario where the Settlement date lies in between the normal issue date and the first coupon date 
    /// ref Krigin pg 11
    /// </summary>
    [Test]
    public void LongFirstCouponPeriodCase2()
    {
      Dt issue = new Dt(1, 3, 2000);
      Dt maturity = new Dt(1, 6, 2004);
      Dt settlement = new Dt(1, 9, 2000);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Dt normalIssueDate = Dt.Subtract(firstCoupon, freq, true);
      Dt pseudoIssueDate = Dt.Subtract(normalIssueDate, freq, true);

      //Difference between the dated date and the normal issue date 
      double DDNI = Dt.Diff(issue, normalIssueDate, dayCount);

      //Difference between the Pseudo issue date and normal issue date 
      double PINI = Dt.Diff(pseudoIssueDate, normalIssueDate, dayCount);

      //Difference between the Normal issue date and settlement date 
      double NIS = Dt.Diff(normalIssueDate, settlement, dayCount);

      //Difference betweent he Normal Issue date and First Coupon date 
      double NIF = Dt.Diff(normalIssueDate, firstCoupon, dayCount);

      //Difference between Settle date and firstcoupon date 
      double SF = Dt.Diff(settlement, firstCoupon, dayCount);

      double AI = ((DDNI / PINI) + (NIS / NIF)) * (coupon / (int)freq);
      double FCF = 1 + (DDNI / PINI);
      double DF = (SF / NIF);

      Dt nextCoupon = Dt.Add(firstCoupon, freq, true);
      Dt prevCoupon = Dt.Subtract(firstCoupon, freq, true);

      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(FCF,
        BondModelUtil.FirstCouponFactor(settlement, issue, firstCoupon, freq, true, dayCount, type), TOLERANCE,
        "FCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");
    }
    #endregion 

    #region Settlementdate in last coupon period tests

    /// <summary>
    /// This test case handles the scenario where the Last Coupon Period is short and the settlement date falls in the last coupon period
    /// </summary>
    [Test]
    public void ShortLastCouponPeriod()
    {
      Dt issue = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 4, 2004);
      Dt settlement = new Dt(1, 2, 2004);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double LS = Dt.Diff(lastCoupon, settlement, dayCount);
      double LNM = Dt.Diff(lastCoupon, Dt.Add(lastCoupon, freq, true), dayCount);
      double LM = Dt.Diff(lastCoupon, maturity, dayCount);
      double SM = Dt.Diff(settlement, maturity, dayCount);
      double AI = (LS / LNM) * (coupon / (int)freq);
      double LCF = (LM / LNM);
      double DF = SM / LNM;
      Dt prevCoupon = pricer.PreviousCycleDate();
      if (pricer.Bond.PeriodAdjustment)
        prevCoupon = Dt.Roll(prevCoupon, pricer.Bond.BDConvention, pricer.Bond.Calendar);


      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(LCF,
        BondModelUtil.LastCouponFactor(lastCoupon, maturity, freq, true, dayCount, type), TOLERANCE,
        "LCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");

    }

    /// <summary>
    /// This test case handles the case where the Last coupon period is of Normal Length 
    /// </summary>
    [Test]
    public void NormalLastCouponPeriodtest()
    {
      Dt issue = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 6, 2004);
      Dt settlement = new Dt(1, 2, 2004);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      //Difference between lastcoupon date and settlement date 
      double LS = Dt.Diff(lastCoupon, settlement, dayCount);
      //Difference between the lastcoupon date and the maturity date 
      double LM = Dt.Diff(lastCoupon, maturity, dayCount);

      //Difference between the settlement date and the maturity date 
      double SM = Dt.Diff(settlement, maturity, dayCount);
      double AI = (LS / LM) * (coupon / (int)freq);
      double LCF = 1.0;
      double DF = SM / LM;

      Dt prevCoupon = pricer.PreviousCycleDate();
      if (pricer.Bond.PeriodAdjustment)
        prevCoupon = Dt.Roll(prevCoupon, pricer.Bond.BDConvention, pricer.Bond.Calendar);

      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(LCF,
        BondModelUtil.LastCouponFactor(lastCoupon, maturity, freq, true, dayCount, type), TOLERANCE,
        "LCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");
    }

    /// <summary>
    /// Test case for the scenario where the settle date falls in between the lastcoupon date and the normal Maturity dat 
    /// </summary>
    [Test]
    public void LongLastCouponPeriodCase1()
    {
      Dt issue = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 8, 2004);
      Dt settlement = new Dt(1, 2, 2004);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      Dt normalMaturity = Dt.Add(lastCoupon, freq, true);
      Dt psuedoMaturity = Dt.Add(normalMaturity, freq, true);

      //Difference between lastcoupon date and settlement date 
      double LS = Dt.Diff(lastCoupon, settlement, dayCount);
      //Difference between the lastcoupon date and the normal maturity date 
      double LNM = Dt.Diff(lastCoupon, normalMaturity, dayCount);
      //Difference between the Normal maturity and the maturity date 
      double NMM = Dt.Diff(normalMaturity, maturity, dayCount);
      //Difference between the normal maturity date and the pseudo maturity date 
      double NMPM = Dt.Diff(normalMaturity, psuedoMaturity, dayCount);
      //Difference between the lastcoupon date and the maturity date 
      double LM = Dt.Diff(lastCoupon, maturity, dayCount);
      //Difference between the settlement date and the normal maturity date 
      double SNM = Dt.Diff(settlement, normalMaturity, dayCount);
      double AI = (LS / LNM) * (coupon / (int)freq);
      double LCF = 1 + (NMM / NMPM);
      double DF = (SNM / LNM) + (NMM / NMPM);


      Dt prevCoupon = pricer.PreviousCycleDate();
      if (pricer.Bond.PeriodAdjustment)
        prevCoupon = Dt.Roll(prevCoupon, pricer.Bond.BDConvention, pricer.Bond.Calendar);

      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(LCF,
        BondModelUtil.LastCouponFactor(lastCoupon, maturity, freq, true, dayCount, type), TOLERANCE,
        "LCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");

    }

    /// <summary>
    /// This test case handles the scenario where the Settlement date falls in between the Normal Maturity and the Pseudomaturity date 
    /// </summary>
    [Test]
    public void LongLastCouponPeriodCase2()
    {
      Dt issue = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 8, 2004);
      Dt settlement = new Dt(1, 7, 2004);
      Dt firstCoupon = new Dt(1, 12, 2000);
      Dt lastCoupon = new Dt(1, 12, 2003);
      double coupon = 0.05;
      double quotedYield = 0.04;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      Dt normalMaturity = Dt.Add(lastCoupon, freq, true);
      Dt psuedoMaturity = Dt.Add(normalMaturity, freq, true);

      //Difference between the normal maturity and settlement
      double NMS = Dt.Diff(normalMaturity, settlement, dayCount);

      //Difference between the Normal maturity and the maturity date 
      double NMM = Dt.Diff(normalMaturity, maturity, dayCount);
      //Difference between the normal maturity date and the pseudo maturity date 
      double NMPM = Dt.Diff(normalMaturity, psuedoMaturity, dayCount);

      //Difference between the settlement date and the normal maturity date 
      double SM = Dt.Diff(settlement, maturity, dayCount);

      double AI = (1 + (NMS / NMPM)) * (coupon / (int)freq);
      double LCF = 1 + (NMM / NMPM);
      double DF = (SM / NMPM);

      Dt prevCoupon = pricer.PreviousCycleDate();
      if (pricer.Bond.PeriodAdjustment)
        prevCoupon = Dt.Roll(prevCoupon, pricer.Bond.BDConvention, pricer.Bond.Calendar);

      Assert.AreEqual(AI, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(LCF,
        BondModelUtil.LastCouponFactor(lastCoupon, maturity, freq, true, dayCount, type), TOLERANCE,
        "LCF does not match");
      Assert.AreEqual(DF,
        BondModelUtil.DiscountFactor(settlement, pricer.Bond, dayCount), TOLERANCE, "Df Does not match");
    }
    #endregion

    #region Sample Calculations - Krgin Ch. 4
    /// <summary>
    /// Sample bond calculations from Krgin that are explained in detail. 
    /// </summary>
    [Test]
    public void Ch4SampleCalculation()
    {
      Dt maturity = new Dt(15, 1, 2008);
      Dt issue = new Dt(20, 6, 2001);
      Dt settlement = new Dt(10, 8, 2001);
      Dt firstCoupon = new Dt(15, 11, 2001);
      Dt lastCoupon = new Dt(15, 11, 2007);
      double coupon = 0.08;
      double quotedYield = 0.09;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.0110869565, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(51, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.9520380596, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.6577473495, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.0537011279, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(4.8360776344, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(29.5199525063, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
      Assert.AreEqual(0.0465774735, Math.Abs(pricer.PV01()) * 100, TOLERANCE, "The PV01 was incorrect.");
    }

    #endregion

    #region CAD Gov't Bonds
    /// <summary>
    /// Tests a Canadian govt bond with a normal last coupon period and the settlement date in neither the first nor last coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 258, Example 1
    /// </remarks>
    [Test]
    public void CanadianExample1Test()
    {
      Dt maturity = new Dt(15, 3, 2002);
      Dt issue = new Dt(15, 3, 1999);
      Dt settlement = new Dt(16, 7, 2000);
      double coupon = 0.1075;
      double price = 1.074;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.CAD;
      BondType type = BondType.CADGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = price;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      //test against known values
      Assert.AreEqual(0.036226027397, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(123, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.0600414098, pricer.YieldToMaturity(), TOLERANCE, "The yield to maturity is incorrect.");
      Assert.AreEqual(price, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(1.643007, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(1.524313, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(1.479885, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(3.046319, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
      Assert.AreEqual(0.016430, pricer.PV01() * 100, TOLERANCE, "The PV01 was incorrect.");
    }

    /// <summary>
    /// Tests a Canadian govt bond with a short last coupon period and the settlement date in neither the first nor last coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 261, Example 2
    /// </remarks>
    [Test]
    [Ignore("Uses a last coupon. Known to fail. Needs targets updating")]
    public void CanadianExample2Test()
    {
      Dt maturity = new Dt(12, 10, 2005);
      Dt issue = new Dt(1, 9, 2000);
      Dt settlement = new Dt(31, 8, 2001);
      Dt firstCoupon = new Dt(1, 3, 2001);
      Dt lastCoupon = new Dt(1, 9, 2005);
      double coupon = 0.045;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.TOB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.CAD;
      BondType type = BondType.CADGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.LastCoupon = lastCoupon;
      b.FirstCoupon = firstCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.02237671233, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(183, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.7894387145, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(-2.8089514, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.650391, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.4600863, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.0280895, -pricer.PV01() * 100, TOLERANCE, "The PV01 was incorrect.");
      Assert.AreEqual(14.6764039, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }


    /// <summary>
    /// Tests a Canadian govt bond with a normal last coupon period and the settlement date in neither 
    /// the first nor last coupon period but is in a leap year.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 263, Example 3
    /// </remarks>
    [Test]
    [Ignore("Uses a last coupon. Known to fail. Needs targets updating")]
    public void CanadianExample3Test()
    {
      Dt maturity = new Dt(1, 5, 2010);
      Dt issue = new Dt(1, 5, 1996);
      Dt settlement = new Dt(10, 3, 2000);
      double coupon = 0.08;
      double quotedYield = 0.085;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.TOB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.CAD;
      BondType type = BondType.CADGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.02849315068, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(130, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.9662912176, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(-6.561077, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(6.875784, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(6.595476, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.065611, -pricer.PV01() * 100, TOLERANCE, "The PV01 was incorrect.");
      Assert.AreEqual(58.74765, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }


    /// <summary>
    /// Tests a Canadian govt bond with a short first coupon period, a normal last coupon period and 
    /// the settlement date in the first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 264, Example 4
    /// </remarks>
    [Test]
    [Ignore("Uses a last coupon. Known to fail. Needs targets updating")]
    public void CanadianExample4Test()
    {
      Dt issue = new Dt(6, 7, 2000);
      Dt settlement = new Dt(5, 10, 2000);
      Dt firstCoupon = new Dt(31, 12, 2000);
      Dt maturity = new Dt(30, 6, 2004);
      double coupon = 0.14875;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.TOB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.CAD;
      BondType type = BondType.CADGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.FirstCoupon = firstCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.0370856164, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(91, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(1.115725191, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(-3.221430, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(2.948107, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(2.794414, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.032214, -pricer.PV01() * 100, TOLERANCE, "The PV01 was incorrect.");
      Assert.AreEqual(10.395528, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a Canadian govt bond with a normal last coupon period and 
    /// the settlement date is in the last coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 264, Example 5
    /// </remarks>
    [Test]
    [Ignore("Known to fail. Needs targets updating")]
    public void CanadianExample5Test()
    {
      Dt issue = new Dt(1, 2, 2000);
      Dt settlement = new Dt(27, 12, 2001);
      Dt maturity = new Dt(1, 2, 2002);
      double coupon = 0.0875;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.TOB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.CAD;
      BondType type = BondType.CADGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.03547945205, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(148, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.997068114, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(-0.10074727, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(0.09863014, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(0.09757155, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.00100747, -pricer.PV01() * 100, TOLERANCE, "The PV01 was incorrect.");
      Assert.AreEqual(0.01966013, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a Canadian govt bond with a long first coupon period and 
    /// the settlement date is in the first coupon period after the normal issue date. Additionally, 
    /// the number of accrued-interest days is greater than 182.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 264, Example 5
    /// </remarks>
    [Test]
    [Ignore("Known to fail. Needs targets updating")]
    public void CanadianExample6Test()
    {
      Dt issue = new Dt(5, 1, 2002);
      Dt settlement = new Dt(31, 8, 2002);
      Dt firstCoupon = new Dt(1, 9, 2002);
      Dt maturity = new Dt(1, 9, 2006);
      double coupon = 0.085;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.TOB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.CAD;
      BondType type = BondType.CADGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.FirstCoupon = firstCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.055308219, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(238, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.9207648783, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(-3.010565776, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.254005158, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.084365079, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.03010566, -pricer.PV01() * 100, TOLERANCE, "The PV01 was incorrect.");
      Assert.AreEqual(12.4963652, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }
    #endregion

    #region US Corp

    /// <summary>
    /// Tests a US Corporate bond with a normal first and last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 178, Example 1
    /// </remarks>
    [Test]
    public void USCorpExample1Test()
    {
      Dt issue = new Dt(30, 11, 2000);
      Dt settlement = new Dt(30, 11, 2000);
      Dt maturity = new Dt(31, 5, 2007);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.9530321351, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.555402014, pricer.PV01() * 10000.0, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.018899089, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(4.779903894, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(28.99989745, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short first and normal last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 179, Example 2
    /// </remarks>
    [Test]
    public void USCorpExample2Test()
    {
      Dt issue = new Dt(29, 9, 2000);
      Dt settlement = new Dt(29, 9, 2000);
      Dt maturity = new Dt(30, 6, 2007);
      Dt firstCoupon = new Dt(31, 12, 2000);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9519936038, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.674909931, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.156185302, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(4.910652668, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(30.76355268, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and normal last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 180, Example 3
    /// </remarks>
    [Test]
    public void USCorpExample3Test()
    {
      Dt issue = new Dt(7, 6, 2000);
      Dt settlement = new Dt(7, 6, 2000);
      Dt maturity = new Dt(20, 7, 2010);
      Dt firstCoupon = new Dt(20, 1, 2001);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9369426861, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(6.009195762, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(6.734302582, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(6.413621506, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(55.45428482, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }


    /// <summary>
    /// Tests a US Corporate bond with a normal first and short last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 181, Example 4
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon.")]
    public void USCorpExample4Test()
    {
      Dt issue = new Dt(30, 4, 2000);
      Dt settlement = new Dt(30, 4, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(31, 10, 2000);
      Dt lastCoupon = new Dt(30, 4, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9453485322, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.276990941, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.861161570, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.582058638, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(40.59121113, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short first and short last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 183, Example 5
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon.")]
    public void USCorpExample5Test()
    {
      Dt issue = new Dt(20, 7, 2000);
      Dt settlement = new Dt(20, 7, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(31, 10, 2000);
      Dt lastCoupon = new Dt(30, 4, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9465933656, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.184224320, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.750553230, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.476717362, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(38.97363026, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and short last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 184, Example 6
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon.")]
    public void USCorpExample6Test()
    {
      Dt issue = new Dt(22, 3, 2000);
      Dt settlement = new Dt(22, 3, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(31, 10, 2000);
      Dt lastCoupon = new Dt(30, 4, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9446161858, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.32367043530822, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.91771927583181, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.63592311983982, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(41.3250524275044, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a normal first and long last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 186, Example 7
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon.")]
    public void USCorpExample7Test()
    {
      Dt issue = new Dt(31, 10, 2000);
      Dt settlement = new Dt(31, 10, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(30, 4, 2001);
      Dt lastCoupon = new Dt(31, 10, 2007);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9473535374, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.068837419, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.618049736, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.350523558, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(36.98523486, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short first and long last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 187, Example 8
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon.")]
    public void USCorpExample8Test()
    {
      Dt issue = new Dt(20, 7, 2000);
      Dt settlement = new Dt(20, 7, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(31, 10, 2000);
      Dt lastCoupon = new Dt(31, 10, 2007);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9463380309, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.184809168, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.752753718, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.478813065, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(39.00872589, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and long last coupon period and settlement date coincides with 
    /// issue/dated date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 189, Example 9
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon.")]
    public void USCorpExample9Test()
    {
      Dt issue = new Dt(22, 3, 2000);
      Dt settlement = new Dt(22, 3, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(31, 10, 2000);
      Dt lastCoupon = new Dt(31, 10, 2007);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9443688216, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.32416004318725, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.91981774234352, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.63792165937478, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(41.5982558186933, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a normal first and normal last coupon period and settlement date in the  
    /// first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 190, Example 10
    /// </remarks>
    [Test]
    public void USCorpExample10Test()
    {
      Dt issue = new Dt(31, 10, 2000);
      Dt settlement = new Dt(15, 12, 2000);
      Dt maturity = new Dt(30, 4, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(45, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01125, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9484870363, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.961713607, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.428361198, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.169867807, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(34.75937694, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short first and normal last coupon period and settlement date in the  
    /// first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 191, Example 11
    /// </remarks>
    [Test]
    public void USCorpExample11Test()
    {
      Dt issue = new Dt(15, 6, 2000);
      Dt settlement = new Dt(31, 8, 2000);
      Dt maturity = new Dt(31, 10, 2002);
      Dt firstCoupon = new Dt(31, 10, 2000);
      double coupon = .1125;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(76, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.02375, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.004613561, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(1.888996191, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(1.937924540, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(1.836895299, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(4.500244269, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and normal last coupon period and settlement date in the  
    /// first coupon period before normal issue date (?).
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 193, Example 12
    /// </remarks>
    [Test]
    public void USCorpExample12Test()
    {
      Dt issue = new Dt(15, 2, 2001);
      Dt settlement = new Dt(1, 3, 2001);
      Dt maturity = new Dt(31, 10, 2005);
      Dt firstCoupon = new Dt(31, 10, 2001);
      double coupon = 0.09875;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(16, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.00438888889, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9589500975, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(3.49152069349197, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.82385938000518, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.62451126066841, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(16.49895191, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and normal last coupon period and settlement date is on 
    /// the normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 194, Example 13
    /// </remarks>
    [Test]
    public void USCorpExample13Test()
    {
      Dt issue = new Dt(1, 9, 2002);
      Dt settlement = new Dt(15, 11, 2002);
      Dt maturity = new Dt(15, 5, 2010);
      Dt firstCoupon = new Dt(15, 5, 2003);
      double coupon = 0.08;
      double quotedYield = 0.075;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(74, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01644444444, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.027693863, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.765028838, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.728376573, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.521326817, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(38.715899505, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and normal last coupon period and settlement date in the  
    /// first coupon period after normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 195, Example 14
    /// </remarks>
    [Test]
    [Ignore("Known to fail.")]
    public void USCorpExample14Test()
    {
      Dt issue = new Dt(15, 3, 2000);
      Dt settlement = new Dt(10, 7, 2000);
      Dt maturity = new Dt(30, 11, 2003);
      Dt firstCoupon = new Dt(30, 11, 2000);
      double coupon = 0.09875;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(115, pricer.AccrualDays(), 0, "The accrual days are incorrect."); //<- Typo in book states 116, believe this is incorrect and should be 115?
      Assert.AreEqual(0.03181944444, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.967797179, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(2.721642223, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(2.872433769, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(2.722686037, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(9.537853627, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a normal first and short last coupon period and settlement date in the  
    /// first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 197, Example 15
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon date.")]
    public void USCorpExample15Test()
    {
      Dt issue = new Dt(31, 10, 2000);
      Dt settlement = new Dt(13, 12, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt lastCoupon = new Dt(30, 4, 2008);
      Dt firstCoupon = new Dt(30, 4, 2001);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.LastCoupon = lastCoupon;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(43, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01075, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9479754456, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.018522102, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.496305779, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.234576932, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(35.692042065, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short first and short last coupon period and settlement date is in the 
    /// first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 198, Example 16
    /// </remarks>
    [Test]
    [Ignore("Known to fail.")]
    public void USCorpExample16Test()
    {
      Dt issue = new Dt(20, 2, 2001);
      Dt settlement = new Dt(31, 3, 2001);
      Dt maturity = new Dt(1, 11, 2003);
      Dt firstCoupon = new Dt(1, 6, 2001);
      Dt lastCoupon = new Dt(1, 6, 2003);
      double coupon = 0.13375;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(41, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01523263889, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.052487069, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(2.268113691, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(2.241093731, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(2.124259460, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(5.934609807, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and short last coupon period and settlement date 
    /// is in the first coupon period before the normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 199, Example 17
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon date.")]
    public void USCorpExample17Test()
    {
      Dt issue = new Dt(15, 2, 2001);
      Dt settlement = new Dt(1, 3, 2001);
      Dt maturity = new Dt(20, 7, 2005);
      Dt firstCoupon = new Dt(15, 10, 2001);
      Dt lastCoupon = new Dt(15, 4, 2005);
      double coupon = 0.09875;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(16, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.004388888889, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9612109221, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(3.329221819, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.637458272, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.447827746, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(14.92715586, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and short last coupon period and settlement date 
    /// is on the normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 201, Example 18
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon date.")]
    public void USCorpExample18Test()
    {
      Dt issue = new Dt(1, 9, 2002);
      Dt settlement = new Dt(15, 11, 2002);
      Dt maturity = new Dt(1, 4, 2010);
      Dt firstCoupon = new Dt(15, 5, 2003);
      Dt lastCoupon = new Dt(15, 11, 2009);
      double coupon = 0.08;
      double quotedYield = 0.075;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(74, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01644444444, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.027426175, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.692595286, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.657854049, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.4533533, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(37.71052363, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and short last coupon period and settlement date 
    /// is in the first coupon period after the normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 202, Example 19
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon date.")]
    public void USCorpExample19Test()
    {
      Dt issue = new Dt(15, 2, 2001);
      Dt settlement = new Dt(11, 7, 2001);
      Dt maturity = new Dt(20, 7, 2005);
      Dt firstCoupon = new Dt(15, 10, 2001);
      Dt lastCoupon = new Dt(15, 4, 2005);
      double coupon = 0.11;
      double quotedYield = 0.0975;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(146, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.04461111, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.040254147, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(3.354129434, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.242470176, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.091747487, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(12.488453, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a normal first and long last coupon period and settlement date 
    /// in the first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 204, Example 20
    /// </remarks>
    [Test]
    //[Ignore("Uses a last coupon date.")]
    public void USCorpExample20Test()
    {
      Dt issue = new Dt(31, 10, 2000);
      Dt settlement = new Dt(15, 12, 2000);
      Dt maturity = new Dt(15, 6, 2008);
      Dt firstCoupon = new Dt(30, 4, 2001);
      Dt lastCoupon = new Dt(31, 10, 2007);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(45, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01125, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9477296821, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.016879132, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.493049736, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.231475939, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(35.66878367, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short first and long last coupon period and settlement date 
    /// in the first coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 205, Example 21
    /// </remarks>
    [Test]
    public void USCorpExample21Test()
    {
      Dt maturity = new Dt(15, 6, 2009);
      Dt settlement = new Dt(13, 9, 2001);
      Dt issue = new Dt(20, 7, 2001);
      Dt firstCoupon = new Dt(31, 10, 2001);
      Dt lastCoupon = new Dt(31, 10, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(53, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01325, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9467812390, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.125224140, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.605531496, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.338601425, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(37.42523101, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and long last coupon period and settlement date 
    /// in the first coupon period before the normal issue.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 207, Example 22
    /// </remarks>
    [Test]
    public void USCorpExample22Test()
    {
      Dt maturity = new Dt(15, 6, 2009);
      Dt settlement = new Dt(11, 4, 2001);
      Dt issue = new Dt(22, 3, 2001);
      Dt firstCoupon = new Dt(31, 10, 2001);
      Dt lastCoupon = new Dt(31, 10, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(19, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.00475, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9444949353, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.30393168658969, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.86703272454044, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.58765021384804, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(41.0121403458943, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and long last coupon period and settlement date 
    /// on the normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 208, Example 23
    /// </remarks>
    [Test]
    public void USCorpExample23Test()
    {
      Dt maturity = new Dt(1, 7, 2010);
      Dt settlement = new Dt(15, 11, 2002);
      Dt issue = new Dt(1, 9, 2002);
      Dt firstCoupon = new Dt(15, 5, 2003);
      Dt lastCoupon = new Dt(15, 11, 2009);
      double coupon = 0.08;
      double quotedYield = 0.075;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(74, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.016444444, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.027917253, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.840407119, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.802034295, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.592322212, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(39.78343722, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a long first and long last coupon period and settlement date 
    /// in the first coupon period after the normal issue date.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 210, Example 24
    /// </remarks>
    [Test]
    [Ignore("Known to fail.")]
    public void USCorpExample24Test()
    {
      Dt maturity = new Dt(15, 6, 2009);
      Dt settlement = new Dt(8, 6, 2001);
      Dt issue = new Dt(22, 3, 2001);
      Dt firstCoupon = new Dt(31, 10, 2001);
      Dt lastCoupon = new Dt(31, 10, 2008);
      double coupon = 0.09;
      double quotedYield = 0.10;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(76, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.019, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9450248596, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.239591660, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.706876942, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.435120898, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(39.09456370, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a normal last coupon period and settlement date 
    /// in neither the first or last coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 211, Example 25
    /// </remarks>
    [Test]
    public void USCorpExample25Test()
    {
      Dt maturity = new Dt(15, 3, 2004);
      Dt settlement = new Dt(10, 5, 2001);
      Dt issue = new Dt(15, 3, 2000);
      double coupon = 0.0975;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(55, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.01489583333, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9698613494, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(2.346058485, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(2.513403046, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(2.382372555, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(7.266758784, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// Tests a US Corporate bond with a short last coupon period and settlement date 
    /// in neither the first or last coupon period.
    /// </summary>
    /// <remarks>
    /// Krgin, pg. 212, Example 26
    /// </remarks>
    [Test]
    public void USCorpExample26Test()
    {
      Dt maturity = new Dt(20, 7, 2003);
      Dt settlement = new Dt(3, 1, 2001);
      Dt issue = new Dt(15, 8, 1998);
      Dt lastCoupon = new Dt(15, 2, 2003);
      Dt firstCoupon = new Dt(15, 2, 1999);
      double coupon = 0.0975;
      double quotedYield = 0.11;
      DayCount dayCount = DayCount.Thirty360Isma;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.LastCoupon = lastCoupon;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(138, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.037375, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.972757196, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(2.133787682, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(2.228565739, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(2.112384587, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(5.893043595, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }
    #endregion

    #region US Treasury Bonds
    /// <summary>
    /// US Treasury with a normal last coupon period and a settlement date neither in the first or last coupon period.
    /// </summary>
    [Test]
    public void USTreasExample1Test()
    {
      Dt issue = new Dt(15, 2, 2000);
      Dt settlement = new Dt(5, 6, 2001);
      Dt maturity = new Dt(15, 2, 2010);
      double coupon = 0.05625;
      double quotedYield = 0.07;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.None,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);

      //get values
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(110, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.0170925414, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9114396435, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(6.0777659998, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(6.7746578009, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(6.5455630926, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(53.52252275, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// US Treasury with a long first coupon a normal coupon period and a settlement date in the first coupon period after the normal issue date.
    /// </summary>
    [Test]
    [Ignore("Known to fail.Needs targets updating")]
    public void USTreasExample2Test()
    {
      Dt issue = new Dt(7, 10, 2001);
      Dt settlement = new Dt(10, 3, 2002);
      Dt maturity = new Dt(15, 11, 2021);
      Dt firstCoupon = new Dt(15, 5, 2002);
      double coupon = 0.1575;
      double quotedYield = 0.12;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);

      //get values
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(154, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.0667261065, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.2800883773, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(9.2557467957, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(7.2846644593, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(6.8723249616, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(82.99493648, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// US Treasury with a normal last coupon period and a settlement date in the last coupon period.
    /// </summary>
    [Test]
    [Ignore("Known to fail. Needs targets updating")]
    public void USTreasExample3Test()
    {
      Dt issue = new Dt(30, 11, 1995);
      Dt settlement = new Dt(5, 6, 2001);
      Dt maturity = new Dt(30, 11, 2001);
      double coupon = 0.05375;
      double quotedYield = 0.055;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USGovt;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);

      //get values
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(5, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(0.0007342896, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9993887845, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(0.4737271007, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(0.4863387978, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(0.4736688044, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.4487242725, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// US Treasury Bill with less than 182 days from settlement to maturity.
    /// </summary>
    /// <remarks>
    /// The derivative related calculations are known to be different than Krgin's given values. We believe this is based 
    /// on the fact that we continue to use the standard approximations for these calculations while Krgin uses specific 
    /// closed form formulas. This will be true across all Treasury Bills.
    /// </remarks>
    [Test]
    public void USTBillExample1Test()
    {
      Dt issue = new Dt(8, 7, 2001);
      Dt settlement = new Dt(6, 8, 2001);
      Dt maturity = new Dt(8, 1, 2002);
      double coupon = 0.0;
      double quotedDiscountRate = 0.0518;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USTBill;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);

      //get values
      pricer.MarketQuote = quotedDiscountRate;
      pricer.QuotingConvention = QuotingConvention.DiscountRate;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9776972222, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(0.05371749, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(0.405927, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(0.424658, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(0.4151868, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.3447596, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }

    /// <summary>
    /// US Treasury Bill with less than 182 days from settlement to maturity.
    /// </summary>
    [Test]
    public void USTBillExample2Test()
    {
      Dt issue = new Dt(23, 7, 2001);
      Dt settlement = new Dt(6, 8, 2001);
      Dt maturity = new Dt(23, 7, 2002);
      double coupon = 0.0;
      double quotedDiscountRate = 0.0523;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USTBill;

      //setup FRN
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        CycleRule.EOM,
        freq,
        roll,
        cal);

      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      BondPricer pricer = new BondPricer(b, settlement);

      //get values
      pricer.MarketQuote = quotedDiscountRate;
      pricer.QuotingConvention = QuotingConvention.DiscountRate;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.9490075, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(0.055145688, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(0.88899865, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(0.9616438, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(0.9367667, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(1.3982668, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");
    }
    #endregion

    #region German Bonds Tests 

    /// <summary>
    /// Krigin Pg 242 , this example checks for all the calculations for the German bond 
    /// </summary>
    [Test]
    public void TestGermanBondExample1()
    {
      Dt issueDate = new Dt(16, 2, 2001);
      Dt settleDate = new Dt(15, 6, 2001);
      Dt maturity = new Dt(16, 2, 2006);
      double couponRate = 0.06;
      double quotedYield = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Annual;
      Currency ccy = Currency.EUR;
      BondType type = BondType.DEMGovt;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.019561644, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.040461435, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.191355328, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(4.151723846, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.95402271, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(20.5964963, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");



    }

    /// <summary>
    /// Krigin Pg 48 , This example just checks for the Accrued Interest Calculations  
    /// </summary>
    [Test]
    public void TestGermanBondExample2()
    {
      Dt issueDate = new Dt(4, 7, 2000);
      Dt settleDate = new Dt(20, 3, 2001);
      Dt maturity = new Dt(4, 7, 2008);
      double couponRate = 0.0475;
      double quotedYield = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Annual;
      Currency ccy = Currency.EUR;
      BondType type = BondType.DEMGovt;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      //test against known values
      Assert.AreEqual(0.033705479, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");

    }
    #endregion 

    #region Japanese bonds tests
    /// <summary>
    /// Test case where 
    /// </summary>
    [Test]
    public void JapaneseBondExample1()
    {
      Dt issueDate = new Dt(20, 3, 1999);
      Dt settleDate = new Dt(10, 11, 1999);
      Dt maturity = new Dt(20, 3, 2009);
      double couponRate = 0.02;
      double quotedYield = 0.03;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(3415, BondModelUtil.JGBSettleToMaturity(settleDate, maturity), "TSm is wrong");
      Assert.AreEqual(0.002794520, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.92694, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(6.771877118, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(7.392922525, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(7.283667507, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(106.4230379, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }

    /// <summary>
    /// Test for Normal last coupon period and settle date falling in neither the first nor the last coupon period
    /// </summary>
    [Test]
    public void JapaneseBondExample2()
    {
      Dt issueDate = new Dt(20, 6, 1999);
      Dt settleDate = new Dt(2, 4, 2000);
      Dt maturity = new Dt(20, 6, 2005);
      double couponRate = 0.04;
      double quotedYield = 0.07;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.011397260, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.88536, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(3.383110325, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.904645483, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.772604332, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(28.83133438, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }


    /// <summary>
    /// Test for Normal last coupon period and settle date falling on a Feb 29th and less than 1 yr from maturity date
    /// </summary>
    [Test]
    public void JapaneseBondExample3()
    {
      Dt issueDate = new Dt(20, 9, 1999);
      Dt settleDate = new Dt(29, 2, 2000);
      Dt maturity = new Dt(20, 3, 2000);
      double couponRate = 0.026;
      double quotedYield = 0.005;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.011539726, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.00115, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(0.05484253, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(0.05429070, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(0.05415531, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(0.00593320, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }

    /// <summary>
    ///Test case for Japanese bonds where the settle date falls in the first coupon period 
    /// </summary>
    [Test]
    public void JapaneseBondExample4()
    {
      Dt issueDate = new Dt(20, 10, 2000);
      Dt settleDate = new Dt(18, 11, 2000);
      Dt maturity = new Dt(21, 12, 2010);

      Dt firstCoupon = new Dt(20, 12, 2000);
      Dt lastCoupon = new Dt(20, 6, 2010);
      double couponRate = 0.08;
      double quotedYield = 0.094;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.LastCoupon = lastCoupon;

      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.006575342, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.92750, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.803112549, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.383782881, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.142103994, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(53.25735588, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }

    /// <summary>
    ///Test case for Japanese bonds for the long last coupon period case and the settle date is neither in first/last 
    /// coupon period
    /// </summary>
    [Test]
    public void JapaneseBondExample5()
    {
      Dt issueDate = new Dt(20, 3, 2000);
      Dt settleDate = new Dt(14, 2, 2001);
      Dt maturity = new Dt(21, 3, 2011);


      Dt lastCoupon = new Dt(20, 9, 2010);
      double couponRate = 0.067;
      double quotedYield = 0.1;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      b.LastCoupon = lastCoupon;

      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.026983561, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.83421, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(4.190966219, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.109785682, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(4.866462554, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(48.89683575, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }

    /// <summary>
    ///test for Japanese bond where the last coupon period is normal and the settlement date coincides with a coupon date 
    /// </summary>
    [Test]
    public void JapaneseBondExample6()
    {
      Dt issueDate = new Dt(20, 6, 2000);
      Dt settleDate = new Dt(20, 6, 2001);
      Dt maturity = new Dt(20, 6, 2005);
      double couponRate = 0.011;
      double quotedYield = 0.01;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;


      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.0, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.00384, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(3.860946746, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(3.865408311, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(3.846177424, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(29.58598019, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }

    /// <summary>
    ///test for Japanese bond where the issue date is after 3/1/2001 
    /// </summary>
    [Test]
    public void JapaneseBondExample7()
    {
      Dt issueDate = new Dt(20, 3, 2001);
      Dt settleDate = new Dt(18, 4, 2001);
      Dt maturity = new Dt(20, 3, 2007);
      double couponRate = 0.007;
      double quotedYield = 0.006;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;


      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;
      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.000556164, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(1.00571, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(5.75013467, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(5.73147073, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(5.71432775, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(65.34271479, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");


    }

    /// <summary>
    /// Thsi example tests for the number of accrual days for a Japanese bond
    /// </summary>
    [Test]
    public void TestJapaneseBondExample8()
    {
      Dt issueDate = new Dt(20, 1, 2000);
      Dt settleDate = new Dt(29, 2, 2000);
      Dt maturity = new Dt(20, 1, 2001);
      double couponRate = 0.04;
      double quotedYield = 0.006;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Assert.AreEqual(0.004383561644, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
    }

    /// <summary>
    /// Accrued Interest test where the settle date is in the short first coupon period 
    /// </summary>
    [Test]
    public void TestJapaneseBondExample9()
    {
      Dt issueDate = new Dt(25, 1, 2001);
      Dt settleDate = new Dt(12, 7, 2001);
      Dt maturity = new Dt(20, 1, 2003);
      double couponRate = 0.032;
      double quotedYield = 0.03;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Assert.AreEqual(0.014816438, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
    }

    /// <summary>
    /// Test the case where the number of accrual days is 183 
    /// </summary>
    [Test]
    public void TestJapaneseBondExample10()
    {
      Dt issueDate = new Dt(20, 3, 2001);
      Dt settleDate = new Dt(19, 9, 2001);
      Dt maturity = new Dt(20, 3, 2002);
      double couponRate = 0.012;
      double quotedYield = 0.03;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Assert.AreEqual(0.006, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");

    }

    [Test]
    public void TestJapaneseBondExample11()
    {
      Dt issueDate = new Dt(20, 10, 1998);
      Dt settleDate = new Dt(19, 6, 1999);
      Dt maturity = new Dt(22, 12, 2008);
      Dt firstCoupon = new Dt(20, 6, 1999);
      double couponRate = 0.009;
      double quotedYield = 0.03;
      DayCount dayCount = DayCount.Actual365Fixed;
      Calendar cal = Calendar.TGT;
      BDConvention roll = BDConvention.None;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.JPY;
      BondType type = BondType.JGB;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.CashflowFlag |= CashflowFlag.StubAtEnd;
      b.Notional = 1.0;
      b.FirstCoupon = firstCoupon;
      b.PeriodAdjustment = false;
      b.AccrueOnCycle = false;

      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      Assert.AreEqual(0.005991780, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
    }


    #endregion 

    #region French Bond tests 
    [Test]
    public void TestFrenchOAT()
    {
      Dt issueDate = new Dt(25, 10, 2000);
      Dt settleDate = new Dt(9, 8, 2001);
      Dt maturity = new Dt(25, 10, 2016);
      double couponRate = 0.05;
      double quotedYield = 0.07;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Annual;
      Currency ccy = Currency.EUR;
      BondType type = BondType.FRFGovt;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      double pv = pricer.FlatPrice();

      //test against known values
      Assert.AreEqual(0.03945, pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(0.81609, pv, TOLERANCE, "The pv is incorrect.");
      Assert.AreEqual(quotedYield, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(8.01327914, pricer.PV01() * 10000, TOLERANCE, "The first derivative is incorrect.");
      Assert.AreEqual(10.02195145, pricer.Duration(), TOLERANCE, "The duration is incorrect.");
      Assert.AreEqual(9.36630977, pricer.ModDuration(), TOLERANCE, "The modified duration is incorrect.");
      Assert.AreEqual(123.5488528, pricer.Convexity(), CONVEXITY_TOLERANCE, "The convexity is incorrect.");



    }
    #endregion 

    #region Italian Govt Bond tests

    [Test]
    public void TestItalianGovtBondExample1()
    {
      Dt issueDate = new Dt(1, 9, 1999);
      Dt settleDate = new Dt(8, 9, 2000);
      Dt maturity = new Dt(1, 9, 2002);
      double couponRate = 0.0375;
      double quotedYield = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.EUR;
      BondType type = BondType.ITLGovt;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      double yield = pricer.Irr();
      //test against known values
      Assert.AreEqual(0.977686545, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect");
      Assert.AreEqual(0.0007251, pricer.AccruedInterest(), TOLERANCE,
        "the accrued interest is incorrect ");

      BondPricer pricer2 = new BondPricer(b, settleDate);
      pricer2.MarketQuote = pricer.FlatPrice();
      pricer2.QuotingConvention = QuotingConvention.FlatPrice;
      Assert.AreEqual(quotedYield, pricer2.YieldToMaturity(), TOLERANCE, "The yield is incorrect");


    }

    [Test]
    public void TestItalianGovtBondExample2()
    {
      Dt issueDate = new Dt(1, 3, 1997);
      Dt settleDate = new Dt(20, 9, 2000);
      Dt maturity = new Dt(1, 3, 2002);
      double couponRate = 0.0625;
      double quotedYield = 0.05;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.EUR;
      BondType type = BondType.ITLGovt;

      Bond b = new Bond(issueDate, maturity, ccy, type, couponRate, dayCount, CycleRule.None, freq, roll, cal);
      b.Notional = 1.0;
      BondPricer pricer = new BondPricer(b, settleDate);
      pricer.MarketQuote = quotedYield;
      pricer.QuotingConvention = QuotingConvention.Yield;

      double yield = pricer.Irr();
      //test against known values
      Assert.AreEqual(1.018055748, pricer.FlatPrice(), TOLERANCE, "The pv is incorrect");
      Assert.AreEqual(0.0032804, pricer.AccruedInterest(), TOLERANCE,
        "The accrued interest is incorrect");

      BondPricer pricer2 = new BondPricer(b, settleDate);
      pricer2.MarketQuote = pricer.FlatPrice();
      pricer2.QuotingConvention = QuotingConvention.FlatPrice;
      Assert.AreEqual(quotedYield, pricer2.YieldToMaturity(), TOLERANCE, "The yield is incorrect");
    }


    #endregion

    #endregion Krgin Tests

    #region Other Tests

    public void YieldOnCouponDate()
    {
      Dt issue = new Dt(15, 3, 2006);
      Dt maturity = new Dt(15, 3, 2011);

      const double coupon = 0.03141593;
      Calendar cal = Calendar.NYB;
      const BDConvention roll = BDConvention.Following;
      const Currency ccy = Currency.USD;
      foreach (var dc in new[] { DayCount.Thirty360, DayCount.Thirty360Isma, DayCount.ThirtyE360, DayCount.Actual360, DayCount.Actual365Fixed, Toolkit.Base.DayCount.Actual365L, DayCount.Actual366, DayCount.ActualActual, DayCount.ActualActualBond, DayCount.ActualActualEuro })
      {
        foreach (var freq in new[] { Frequency.SemiAnnual, Frequency.Quarterly, Frequency.Annual })
        {
          foreach (BondType type in Enum.GetValues(typeof(BondType)))
          {
            // skip some special cases where the general test doesn't apply

            if (type == BondType.ITLGovt || type == BondType.USTBill)
              continue;

            Bond b = new Bond(
              issue,
              maturity,
              ccy,
              type,
              coupon,
              dc,
              CycleRule.None,
              freq,
              roll,
              cal)
            {
              Notional = 1,
              PeriodAdjustment = false
            };

            Dt settle = issue;
            do
            {
              BondPricer pricer = new BondPricer(b, settle)
              {
                MarketQuote = 1.0,
                QuotingConvention = QuotingConvention.FlatPrice
              };
              Assert.AreEqual(coupon, pricer.YieldToMaturity(), TOLERANCE,
                dc + "/" + freq + "/" + type + "/" + settle);
            } while ((settle = b.Schedule.GetNextCouponDate(settle)) < maturity);
          }
        }
      }
    }

    [Test, Smoke]
    public void TestNegativeYield()
    {
      Dt issue = new Dt(15, 9, 2005);
      Dt maturity = new Dt(1, 10, 2010);

      const double coupon = 0.05125;
      Calendar cal = Calendar.NYB;
      const BDConvention roll = BDConvention.Following;
      const Currency ccy = Currency.USD;
      var type = BondType.USCorp;
      var dc = DayCount.Thirty360;
      var freq = Frequency.SemiAnnual;

      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dc,
        CycleRule.None,
        freq,
        roll,
        cal)
      {
        Notional = 1,
        PeriodAdjustment = false
      };

      Dt asOf = new Dt(20, 5, 2010);
      Dt settle = new Dt(25, 5, 2010);
      var discountCurve =TestBond.CreateDiscountCurveForAmortBond(asOf);

      BondPricer pricer = new BondPricer(b, asOf, settle)
      {
        ProductSettle = settle,
        TradeSettle = settle,
        MarketQuote = 1.025,
        QuotingConvention = QuotingConvention.FlatPrice,
        DiscountCurve = discountCurve
      };
      double h = 0.0;
      double recovery = 0.4;
      SurvivalCurve flatHcurve = new SurvivalCurve(asOf, h);
      flatHcurve.Calibrator = new SurvivalFitCalibrator(asOf, settle, recovery, discountCurve);
      //flatHcurve.Fit();
      pricer.SurvivalCurve = flatHcurve;
      // find flat curve to match market quote
      pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve(recovery);

      // Setup curve name
      if (pricer.SurvivalCurve != null)
        pricer.SurvivalCurve.Name = pricer.Product.Description + "_Curve";

      double yield = pricer.YieldToMaturity();
      double fullPrice = pricer.FullPrice();
      double ASW = pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);

      double zSpread = pricer.ImpliedZSpread();
      Assert.AreEqual(-0.029857, ASW, TOLERANCE,
        "ASW: " + dc + "/" + freq + "/" + type + "/" + settle);
      Assert.AreEqual(-0.01951, yield, TOLERANCE,
        "Yield: " + dc + "/" + freq + "/" + type + "/" + settle);
      Assert.AreEqual(1.032688, fullPrice, TOLERANCE,
        "Full price: " + dc + "/" + freq + "/" + type + "/" + settle);
      Assert.AreEqual(-0.02948, zSpread, TOLERANCE,
        "Z-spread: " + dc + "/" + freq + "/" + type + "/" + settle);
      var quoteDict = new Dictionary<QuotingConvention, double>();
      quoteDict.Add(QuotingConvention.FullPrice, fullPrice);
      quoteDict.Add(QuotingConvention.ASW_Par, ASW);
      quoteDict.Add(QuotingConvention.ZSpread, zSpread);

      //now we check for round trip pricing for all the quoting conventions 
      double accrued = pricer.Accrued();
      double ai = pricer.AccruedInterest();
      double pv01 = pricer.PV01();
      double pv = pricer.Pv();
      double convexity = pricer.Convexity();
      double duration = pricer.ModDuration();
      double irr = pricer.Irr();
      double accrualDays = pricer.AccrualDays();
      double modelFulLprice = pricer.FullModelPrice();
      double cdsBasis = pricer.ImpliedCDSSpread();
      double cdsLevel = pricer.ImpliedCDSLevel();
      double spread01 = pricer.Spread01();
      double spreadDuration = pricer.SpreadDuration();
      double spreadConvexity = pricer.SpreadConvexity();
      double zspread01 = pricer.ZSpread01();
      double zspreadDuration = pricer.ZSpreadDuration();
      double ir01 = pricer.Rate01();
      double irDuration = pricer.RateDuration();
      double fullprice = pricer.FullPrice();
      double zspread = pricer.ImpliedZSpread();
      foreach (var kvp in quoteDict)
      {
        pricer.MarketQuote = kvp.Value;
        pricer.QuotingConvention = kvp.Key;
        var spotFullPrice = pricer.SpotFullPrice();
        Assert.AreEqual(fullPrice, RoundingUtil.Round(spotFullPrice, 6),
          "Full price does not match for qc " + kvp.Key);
        Assert.AreEqual(ai, pricer.AccruedInterest(), TOLERANCE, "AI does not match for qc " + kvp.Key);
        Assert.AreEqual(accrualDays, pricer.AccrualDays(),
          "Accrual days does not match for qc " + kvp.Key);
        Assert.AreEqual(fullprice, pricer.FullPrice(),
          "FUll price does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread, pricer.ImpliedZSpread(), TOLERANCE,
          "ZSpread does not match for qc " + kvp.Key);
        Assert.AreEqual(ASW, pricer.AssetSwapSpread(DayCount.Actual360, Toolkit.Base.Frequency.Quarterly),
          TOLERANCE, "ASW does not match for qc " + kvp.Key);
        Assert.AreEqual(pv01, pricer.PV01(), TOLERANCE, "Pv01 does not match for qc " + kvp.Key);
        Assert.AreEqual(pv, pricer.Pv(), TOLERANCE, "Pv does not match for qc " + kvp.Key);
        Assert.AreEqual(duration, pricer.ModDuration(), TOLERANCE,
          "Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(convexity, pricer.Convexity(), TOLERANCE,
          "Convexity does not match for qc " + kvp.Key);
        Assert.AreEqual(irr, pricer.Irr(), TOLERANCE, "Irr does not match for qc " + kvp.Key);
        Assert.AreEqual(modelFulLprice, pricer.FullModelPrice(), TOLERANCE,
          "model full price  does not match for qc " + kvp.Key);
        Assert.AreEqual(cdsBasis, pricer.ImpliedCDSSpread(), TOLERANCE,
          "CDSBasisdoes not match for qc " + kvp.Key);
        Assert.AreEqual(cdsLevel, pricer.ImpliedCDSLevel(), TOLERANCE,
          "CDs Leveldoes not match for qc " + kvp.Key);
        Assert.AreEqual(accrued, pricer.Accrued(), TOLERANCE,
          "Accrued does not match for qc " + kvp.Key);
        Assert.AreEqual(spread01, pricer.Spread01(), TOLERANCE,
          "Spread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadDuration, pricer.SpreadDuration(), TOLERANCE,
          "Spread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(spreadConvexity, pricer.SpreadConvexity(), TOLERANCE,
          "Spread Convexty does not match for qc " + kvp.Key);
        Assert.AreEqual(zspread01, pricer.ZSpread01(), TOLERANCE,
          "ZSpread01 does not match for qc " + kvp.Key);
        Assert.AreEqual(zspreadDuration, pricer.ZSpreadDuration(), TOLERANCE,
          "ZSpread Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(ir01, pricer.Rate01(), TOLERANCE, "Rate01 does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE,
          "rate Duration does not match for qc " + kvp.Key);
        Assert.AreEqual(irDuration, pricer.RateDuration(), TOLERANCE,
          "rate Duration does not match for qc " + kvp.Key);
      }

    }

    [Test, Smoke]
    public void TestPaymentLagNotInBondCF()
    {
      //setup Bond
      Bond b = new Bond(
        new Dt(15, 07, 2008),
        new Dt(15, 01, 2015),
        Currency.USD,
        BondType.USCorp,
        0.05,
        DayCount.ActualActualBond,
        CycleRule.None,
        Frequency.SemiAnnual,
        BDConvention.Following,
        Calendar.NYB);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      var asOf = new Dt(09, 06, 2011);
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);

      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 1000000.0 };
      pricer.MarketQuote = 1.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      //get values
      double accrued = pricer.Accrued();

      var noLagPricer = (BondPricer)pricer.Clone();
      b.PaymentLagRule = new PayLagRule(-30, false);

      double yield = pricer.YieldToMaturity();
      double fullPrice = pricer.FullPrice();

      double irr = pricer.Irr();
      Assert.AreEqual(20027.624, accrued, TOLERANCE,
        "The accrued should match value 20027.624, not [" + accrued + "]");
      Assert.AreEqual(0.049915, yield, TOLERANCE,
        "The yield should match value 0.049915, not [" + yield + "]");
      Assert.AreEqual(102.0028, fullPrice * 100.0, TOLERANCE,
        "The full price should match value 102.0028, not [" + fullPrice * 100.0 + "]");
      Assert.AreEqual(0.0485, irr, TOLERANCE,
        "The Irr should match value 0.0485, not [" + irr + "]");

      Assert.AreEqual(0.0, accrued - noLagPricer.Accrued(), TOLERANCE,
        "The accrued diff should match value 0 ");
      Assert.AreEqual(0.0, yield - noLagPricer.YieldToMaturity(), TOLERANCE,
        "The yield diff should match value 0.0");
      Assert.AreEqual(0.0, fullPrice - noLagPricer.FullPrice(), TOLERANCE,
        "The full price diff should match value 0");
      Assert.AreNotEqual(0.0, irr - noLagPricer.Irr(),
        "The Irr diff should be different from 0.0");
    }


    [Test, Smoke]
    public void TestPaymentLagInBondCF()
    {
      //setup Bond
      Bond b = new Bond(
        new Dt(15, 07, 2008),
        new Dt(15, 01, 2015),
        Currency.USD,
        BondType.USCorp,
        0.05,
        DayCount.ActualActualBond,
        CycleRule.None,
        Frequency.SemiAnnual,
        BDConvention.Following,
        Calendar.NYB);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      var asOf = new Dt(09, 08, 2011);
      var irCurve =TestBond.CreateDiscountCurveForAmortBond(asOf);

      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 1000000.0 };
      pricer.MarketQuote = 1.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.TradeSettle = new Dt(09, 07, 2011);

      var noLagPricer = (BondPricer)pricer.Clone();
      b.PaymentLagRule = new PayLagRule(-30, false);

      double yield = pricer.YieldToMaturity();
      double fullPrice = pricer.FullPrice();
      double accrued = pricer.Accrued();
      double modelPv = pricer.FullModelPrice();

      double irr = pricer.Irr();
      Assert.AreEqual(28396.739, accrued, TOLERANCE,
        "The accrued should match value 28396.739, not [" + accrued + "]");
      Assert.AreEqual(0.049988, yield, TOLERANCE,
        "The yield should match value 0.049988, not [" + yield + "]");
      Assert.AreEqual(100.3397, fullPrice * 100.0, TOLERANCE,
        "The full price should match value 100.3397, not [" + fullPrice * 100.0 + "]");
      Assert.AreEqual(0.04852, irr, TOLERANCE,
        "The Irr should match value 0.0485, not [" + irr + "]");

      Assert.AreEqual(25000.0, accrued - noLagPricer.Accrued(), TOLERANCE,
        "The accrued diff should match value 25000 ");
      Assert.AreEqual(0.0, yield - noLagPricer.YieldToMaturity(), TOLERANCE,
        "The yield diff should match value 0.0");
      Assert.AreEqual(0.0, fullPrice - noLagPricer.FullPrice(), TOLERANCE,
        "The full price diff should match value 0");
      Assert.IsTrue(irr - noLagPricer.Irr() < 0,
        "The Irr of lagged payment cf shall be smaller than regular cf");
      Assert.IsTrue(modelPv - noLagPricer.FullModelPrice() < 0,
        "The model price of lagged payment cf shall be smaller than regular cf");
    }


    [Test, Smoke]
    public void TestPaymentLagInBondCF2()
    {
      //setup Bond
      Bond b = new Bond(
        new Dt(15, 07, 2008),
        new Dt(15, 01, 2015),
        Currency.USD,
        BondType.USCorp,
        0.05,
        DayCount.ActualActualBond,
        CycleRule.None,
        Frequency.SemiAnnual,
        BDConvention.Following,
        Calendar.NYB);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      var asOf = new Dt(09, 08, 2011);
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);

      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 1000000.0 };
      pricer.MarketQuote = 1.0;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      var noLagPricer = (BondPricer)pricer.Clone();
      b.PaymentLagRule = new PayLagRule(-30, false);

      double yield = pricer.YieldToMaturity();
      double fullPrice = pricer.FullPrice();
      double accrued = pricer.Accrued();
      double modelPv = pricer.FullModelPrice();

      double irr = pricer.Irr();
      Assert.AreEqual(3396.739, accrued, TOLERANCE,
        "The accrued should match value 3396.739, not [" + accrued + "]");
      Assert.AreEqual(0.049914, yield, TOLERANCE,
        "The yield should match value 0.049914, not [" + yield + "]");
      Assert.AreEqual(100.3397, fullPrice * 100.0, TOLERANCE,
        "The full price should match value 100.3397, not [" + fullPrice * 100.0 + "]");
      Assert.AreEqual(0.0485, irr, TOLERANCE,
        "The Irr should match value 0.0485, not [" + irr + "]");

      Assert.AreEqual(0.0, accrued - noLagPricer.Accrued(), TOLERANCE,
        "The accrued diff should match value 0 ");
      Assert.AreEqual(0.0, yield - noLagPricer.YieldToMaturity(), TOLERANCE,
        "The yield diff should match value 0.0");
      Assert.AreEqual(0.0, fullPrice - noLagPricer.FullPrice(), TOLERANCE,
        "The full price diff should match value 0");
      Assert.IsTrue(irr - noLagPricer.Irr() < 0,
        "The Irr of lagged payment cf shall be smaller than regular cf");
      Assert.IsTrue(modelPv - noLagPricer.FullModelPrice() < 0,
        "The model price of lagged payment cf shall be smaller than regular cf");
    }


    [Test, Smoke]
    public void TestPaymentLagBondIrr()
    {
      //setup a Bond that only has the final principal payment as cashflow item
      Bond b = new Bond(
        new Dt(14, 07, 2008),
        new Dt(14, 07, 2011),
        Currency.USD,
        BondType.USCorp,
        0.00,
        DayCount.Actual365Fixed,
        CycleRule.None,
        Frequency.SemiAnnual,
        BDConvention.Following,
        Calendar.NYB);
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      var asOf = new Dt(09, 06, 2011);
      var irCurve = TestBond.CreateDiscountCurveForAmortBond(asOf);

      var pricer = new BondPricer(b, asOf, asOf, irCurve, null, 0, TimeUnit.None, 0.0) { Notional = 1000000.0 };
      pricer.MarketQuote = 0.980;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;

      var noLagPricer = (BondPricer)pricer.Clone();
      b.PaymentLagRule = new PayLagRule(-Dt.Diff(asOf, b.Maturity, b.DayCount), false);

      double irr = noLagPricer.Irr();
      double irrWithLag = pricer.Irr();
      // the irr from calculation result shall guarantee that discounted principal matches the current price quote
      Assert.AreEqual(noLagPricer.MarketQuote, RateCalc.PriceFromRate(irr, asOf, b.Maturity, b.DayCount, b.Freq), TOLERANCE,
        "The expected price derived from no-payment-gap Irr should match value 0.98, not [" + RateCalc.PriceFromRate(irr, asOf, b.Maturity, b.DayCount, b.Freq) + "]");
      Assert.AreEqual(pricer.MarketQuote, RateCalc.PriceFromRate(irrWithLag, asOf, b.EffectiveMaturity, b.DayCount, b.Freq), TOLERANCE,
        "The expected price derived from payment-gap Irr should match value 0.98, not [" + RateCalc.PriceFromRate(irrWithLag, asOf, b.EffectiveMaturity, b.DayCount, b.Freq) + "]");
    }

    #region WAL Tests

    /// <summary>
    /// Test case for WAL for Bullet fixed rate bond
    /// </summary>
    [Test, Smoke]
    public void TestWALBulletBond()
    {
      var asOf = new Dt(26, 6, 2008);
      var settle = new Dt(28, 6, 2008);
      var maturity = new Dt(28, 6, 2017);
      // Bullet
      var b = new Bond(new Dt(28, 06, 2007), maturity, Currency.USD, BondType.USCorp, 0.05,
        DayCount.Actual360, CycleRule.None, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      var curve = new DiscountCurve(asOf, 0.01);
      var pricer = new BondPricer(b, asOf, settle, curve, null, 0, TimeUnit.None, 0.4)
      {
        Notional = 1e7,
        QuotingConvention = QuotingConvention.FullPrice,
        MarketQuote = 1.008
      };

      var wal = pricer.WAL();
      Assert.AreEqual(Dt.TimeInYears(settle, maturity), wal, TOLERANCE, "WAL does not match");
    }

    /// <summary>
    /// Test case for WAL for Amortizing fixed rate bond
    /// </summary>
    [Test, Smoke]
    public void TestWALAmortizingFixedBond()
    {
      var asOf = new Dt(26, 6, 2009);
      var settle = new Dt(29, 6, 2012);
      var maturity = new Dt(28, 6, 2017);
      var cal = Calendar.NYB;
      var roll = BDConvention.Following;
      // Regular amortizing fixed rate bond
      var b = new Bond(new Dt(28, 06, 2007), maturity, Currency.USD, BondType.USCorp, 0.05,
        DayCount.Actual360, CycleRule.None, Frequency.Quarterly, roll, cal)
      {
        Notional = 1.0,
        PeriodAdjustment = false
      };
      Dt[] amortizingDates = { new Dt(28, 12, 2009), new Dt(28, 6, 2012), new Dt(28, 12, 2014) };
      double[] amortizingAmounts = { 0.25, 0.25, 0.25 };
      for (var j = 0; j < amortizingDates.Length; j++)
        b.AmortizationSchedule.Add(new Amortization(amortizingDates[j], amortizingAmounts[j]));

      var curve = new DiscountCurve(asOf, 0.01);
      var pricer = new BondPricer(b, asOf, settle, curve, null, 0, TimeUnit.None, 0.4)
      {
        Notional = 1e7,
        QuotingConvention = QuotingConvention.FullPrice,
        MarketQuote = 1.008
      };

      var wal = pricer.WAL();
      var manual = Dt.TimeInYears(settle, maturity) * 0.5 +
                   Dt.TimeInYears(settle, Dt.Roll(amortizingDates[2], roll, cal)) * 0.5;
      Assert.AreEqual(manual, wal, TOLERANCE, "WAL does not match");
    }

    #endregion WAL Test

    #endregion Other Tests
  }
}
