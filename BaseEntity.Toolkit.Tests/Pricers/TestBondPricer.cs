//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Models.BGM.Native;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Util;
using BgmBinomialTree = BaseEntity.Toolkit.Models.BGM.BgmBinomialTree;
using CashflowAdapter = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestBondPricer
  {

    [Test]
    public void TestDualCurveAswSimpleEqual()
    {
      var asOf = new Dt(20161003);
      var settlement = new Dt(20161003);
      var issue = new Dt(20161003);
      var maturity = new Dt(20180619);
      double coupon = 0.05;

      DiscountCurve projCurve = new DiscountCurve(asOf, 0.0);
      DiscountCurve discCurve = projCurve;
      var bond = GetBond(issue, maturity, coupon);
      var pricer = GetBondPricer(bond, asOf, settlement, discCurve, 
        null, QuotingConvention.FlatPrice);
      var asw = pricer.CalcDualCurveAsw(pricer, projCurve, 0.0, _freq, _dayCount);
      Assert.AreEqual(coupon, asw, 1E-14);
    }

    [TestCase(QuotingConvention.FlatPrice)]
    [TestCase(QuotingConvention.FullPrice)]
    public void TestDualCurveAswRoundTrip(QuotingConvention quoteType)
    {
      var asOf = new Dt(20161225);
      var settlement = asOf;
      var issue = new Dt(20161003);
      var maturity = new Dt(20180619);
      const double coupon = 0.05;

      const double currentRate = 0.001;
      //setup pricer
      DiscountCurve projCurve = new DiscountCurve(asOf, 0.02);
      DiscountCurve discCurve = new DiscountCurve(asOf, 0.03);

      var bond = GetBond(issue, maturity, coupon);
      BondPricer bondPricer = GetBondPricer(bond, asOf, settlement, discCurve, null, quoteType);

      var asw = bondPricer.CalcDualCurveAsw(bondPricer, projCurve, currentRate, _freq, _dayCount);

      var referenceRate = new InterestReferenceRate("floatingindex", "", _ccy, _dayCount, _cal, 2);
      Bond fltBond = new Bond(issue, maturity, _ccy, _type, referenceRate,
        new Tenor(_freq), asw, _dayCount, CycleRule.None, _freq, _roll, _cal);
      var fltPricer = new BondPricer(fltBond, asOf, settlement, discCurve, null,
        0, TimeUnit.None, -1)
      {
        ReferenceCurve = projCurve,
        CurrentRate = currentRate + asw
      };

      var pv1 = GetCleanPv(bondPricer);
      var pv2 = GetCleanPv(fltPricer);
      var diff = pv1 - pv2;
      var expect = bondPricer.FlatPrice() - 1;

      Assert.AreEqual(expect, diff, 3E-9);
    }


    [Test]
    public void TestBondVodWithPaymentPv()
    {
      const string filePath = @"toolkit\test\data\DefaultBond.xml";
      IPricer pricer = (IPricer)XmlSerialization.ReadXmlFile(filePath.GetFullPath()); 
      var bp = pricer as BondPricer;
      if (bp != null)
      {
        var settle = bp.Settle;
        var survivalCurve = bp.SurvivalCurve;
        survivalCurve.DefaultDate = settle;
        survivalCurve.Defaulted = Defaulted.WillDefault;
        pricer.Reset();
        var pv1 = bp.ProductPv()/bp.Notional;
        bp.UsePaymentSchedule = true;
        var pv2 = bp.BondCashflowAdapter.CalculatePvWithOptions(bp.AsOf, bp.ProtectionStart,
          bp.DiscountCurve, bp.SurvivalCurve, null, 0.0, OptionType.Put, 
          (bp.Bond as ICallable).ExerciseSchedule, new BusinessDays(), 
          bp.VolatilityObject, bp.CashflowModelFlags, 
          bp.StepSize, bp.StepUnit, bp.BgmTreeOptions);

        bp.UsePaymentSchedule = false;
        var pv3 =  bp.BondCashflowAdapter.CalculatePvWithOptions(bp.AsOf, bp.ProtectionStart,
          bp.DiscountCurve, bp.SurvivalCurve, null, 0.0, OptionType.Put,
          (bp.Bond as ICallable).ExerciseSchedule, new BusinessDays(),
          bp.VolatilityObject, bp.CashflowModelFlags,
          bp.StepSize, bp.StepUnit, bp.BgmTreeOptions);
        NUnit.Framework.Assert.AreEqual(pv1, pv2, 1E-14);
        NUnit.Framework.Assert.AreEqual(pv2, pv3, 1E-14);
      }
    }

    [TestCase("1")]
    [TestCase("2")]
    [TestCase("6")]
    [TestCase("9")]
    [TestCase("11")]
    [TestCase("21")]
    [TestCase("22")]
    [TestCase("23")]
    [TestCase("24")]
    [TestCase("Amort")]
    [TestCase("Amort2")]
    public void TestCallableBondPaymentScheduleMethod(string postFix)
    {
      var filePath = String.Format(@"toolkit\test\data\CallableBondPricers\CallableBondPricer{0}.xml", postFix);
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as BondPricer;
      if (pricer != null)
      {
        pricer.StepSize = 1;
        pricer.StepUnit = TimeUnit.Months;
        pricer.Reset();
        TestCfPsAreEquavalent(pricer);
        TestSwaptionsAreEqual(pricer);
      }
    }


    [TestCase("1")]
    [TestCase("2")]
    [TestCase("6")]
    [TestCase("9")]
    [TestCase("11")]
    [TestCase("21")]
    [TestCase("22")]
    [TestCase("23")]
    [TestCase("24")]
    public void TestBondZSpreadConvexity(string postFix)
    {
      var filePath = String.Format(@"toolkit\test\data\CallableBondPricers\CallableBondPricer{0}.xml", postFix);
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as BondPricer;
      if (pricer != null)
      {
        var upBump = pricer.ZorRSpreadSensitivity(false, 25, 0, false, true, false).Item1;
        var downBump = pricer.ZorRSpreadSensitivity(false, 0, 25, false, true, false).Item1;
        var expect = (upBump - downBump)/25*(-1.0);
        var convexity = pricer.ZSpreadConvexity();
        Assert.AreEqual(expect, convexity, 1E-14);
      }
    }


    [Test]
    public void TestCreditRiskToPaymentDate()
    {
      const double expect = 1023930.0431890365;
      var filePath = @"toolkit\test\data\CallableBondPricer0333.xml";
      var fullFilePath = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = XmlSerialization.ReadXmlFile(fullFilePath) as BondPricer;
      if (pricer != null)
      {
        var pv = pricer.Pv();
        Assert.AreEqual(expect, pv, 1E-15 * pricer.Notional);
      }
    }

    [Test]
    public void TestDefaultedCallableBond()
    {
      var asOf = new Dt(20161225);
      var settlement = asOf;
      var issue = new Dt(20161003);
      var maturity = new Dt(20180619);
      var defaultSettle = Dt.AddMonths(asOf, 12, CycleRule.None);
      const double coupon = 0.05;

      var start = Dt.AddMonths(asOf, 3, CycleRule.None);
      var end = Dt.AddMonths(asOf, 15, CycleRule.None);

      //setup pricer
      var bond = GetBond(issue, maturity, coupon);
      bond.CallSchedule.Add(new CallPeriod(start, end,
        1.01, 0.9, OptionStyle.American, 0));

      DiscountCurve discCurve = new DiscountCurve(asOf, 0.03);
      var survivalCurve = new SurvivalCurve(asOf, 0.04);
      survivalCurve.DefaultDate = defaultSettle;
      survivalCurve.Calibrator = new SurvivalFitCalibrator(asOf)
      {
        RecoveryCurve = new RecoveryCurve(asOf, 0.4)
        {
          JumpDate = defaultSettle
        }
      };

      var flatVol = new FlatVolatility();

      BondPricer bondPricer = GetBondPricer(bond, asOf, settlement, 
        discCurve, survivalCurve, QuotingConvention.FlatPrice);
      bondPricer.VolatilityObject = flatVol;

      bondPricer.UsePaymentSchedule = true;
      var pv = bondPricer.Pv();

      bondPricer.UsePaymentSchedule = false;
      var pv1 = bondPricer.Pv();
      Assert.AreEqual(pv, pv1, 1E-15);
    }

    [Test]
    public void TestExpectedAswWithBbg()
    {
      var currentRate = -0.00163; //get it from client input
      var expect = -0.0025; //bbg number
      var pricerFile = Path.Combine(SystemContext.InstallDir,
        @"toolkit\test\data\EU000A1G0BF3_bondPricer.xml");
      var pricer = XmlSerialization.ReadXmlFile(pricerFile) as BondPricer;
      if (pricer != null)
      {
        var asw = pricer.CalcDualCurveAsw(pricer, pricer.ReferenceCurve,
          currentRate, Frequency.SemiAnnual, DayCount.Actual360);

        //less than 1bp
        NUnit.Framework.Assert.AreEqual(expect, asw, 1E-4);
      }
    }

    private void TestCfPsAreEquavalent(BondPricer pricer)
    {
      var settle = pricer.Settle;
      var cf = pricer.Cashflow;
      var survivalCurve = pricer.SurvivalCurve;
      var ps = pricer.PaymentSchedule;

      var psAdapter = new CashflowAdapter(ps);
      var fixedPsAdapter = psAdapter.ClearNotionalPayment();
      var unitPsAdapter = psAdapter.CreateUnitCashflow();
      var lossPsAdapter = survivalCurve == null ? null : psAdapter.CreateLossCashflow();

      var cfAdater = new CashflowAdapter(cf);
      var fixedCfAdapter = cfAdater.ClearNotionalPayment();
      var unitCfAdpater = cfAdater.CreateUnitCashflow();
      var lossCfAdapter = survivalCurve == null ? null : cfAdater.CreateLossCashflow();

      var nps = new PaymentSchedule();
      nps.AddPayments((List<InterestPayment>) fixedPsAdapter.Data);
      var psFixedCf = PaymentScheduleUtils.FillCashflow(null, nps,
        settle, 1.0, pricer.RecoveryRate);

      //Test effective date
      var cfa = new CashflowAdapter(nps);
      Assert.AreEqual(cfa.Effective, psFixedCf.Effective);
      //Test cf and ps equal
      TestPaymentCashflowUtil.AssertEqualCashflows(fixedCfAdapter.Data as Cashflow, psFixedCf);

      nps.Clear();
      nps.AddPayments((List<InterestPayment>) unitPsAdapter.Data);
      var psUnitFixedCf = PaymentScheduleUtils.FillCashflow(null, nps,
        settle, 1.0, pricer.RecoveryRate);

      //Test effective date
      cfa = new CashflowAdapter(nps);
      Assert.AreEqual(cfa.Effective, psUnitFixedCf.Effective);
      //Test cf and ps equal
      TestPaymentCashflowUtil.AssertEqualCashflows(unitCfAdpater.Data as Cashflow, psUnitFixedCf);

      var rps = lossPsAdapter?.RecoveryPayments;
      var lossCf = lossCfAdapter?.Data as Cashflow;

      if (rps != null && lossCf != null && lossCf.Count == rps.Count)
      {
        for (int i = 0; i < rps.Count; i++)
        {
          var payDt = pricer.Bond.PaymentLagRule != null ? lossCf.GetDt(i) : lossCf.GetEndDt(i);
          Assert.AreEqual(lossCf.GetStartDt(i), rps[i].BeginDate, "Begin date");
          Assert.AreEqual(lossCf.GetEndDt(i), rps[i].EndDate, "End date");
          Assert.AreEqual(Dt.Cmp(payDt, rps[i].PayDt), 0, "Pay date");
          Assert.AreEqual(lossCf.GetDefaultAmount(i), rps[i].Amount, 1E-15, "Amount");
        }
      }
    }

    private void TestSwaptionsAreEqual(BondPricer pricer)
    {
      var cfAdapter = new CashflowAdapter(pricer.Cashflow);
      var cfEquivSwaptions = GetEquvalentSwaptions(cfAdapter, pricer);

      var psAdapter = new CashflowAdapter(pricer.PaymentSchedule);
      var psEquivSwaptions = GetEquvalentSwaptions(psAdapter, pricer);

      Assert.AreEqual(cfEquivSwaptions.Count, psEquivSwaptions.Count);

      //Test the equivalent swaptions are the same
      for (int i = 0; i < psEquivSwaptions.Count; i++)
      {
        var cfSwaption = cfEquivSwaptions[i];
        var psSwaption = psEquivSwaptions[i];
        AreEqual(cfSwaption, psSwaption);
      }
    }

    private static IList<SwaptionInfo> 
      GetEquvalentSwaptions(CashflowAdapter cfa, BondPricer pricer)
    {
      var bond = pricer.Bond;
      var exerciseSchedule = (bond as ICallable).ExerciseSchedule;
      var survivalCurve = pricer.SurvivalCurve;
      return cfa.BuildEquivalentSwaptions(pricer.AsOf, pricer.Settle, OptionType.Put,
        exerciseSchedule, bond.GetNotificationDays(), pricer.DiscountCurve, survivalCurve,
        pricer.VolatilityObject, pricer.StepSize, pricer.StepUnit, pricer.BgmTreeOptions,
        pricer.CashflowModelFlags);
    }

    private void AreEqual(SwaptionInfo s1,
      SwaptionInfo s2)
    {
      var tolerance = 2E-15;
      Assert.AreEqual(Dt.Cmp(s1.Date, s2.Date), 0, "Date");
      Assert.AreEqual(s1.Level, s2.Level, tolerance, "Level");
      Assert.AreEqual(s1.Rate, s2.Rate, tolerance, "Rate");
      Assert.AreEqual(s1.Coupon, s2.Coupon, tolerance, "Coupon");
      Assert.AreEqual(s1.Value, s2.Value, tolerance, "Value");
      Assert.AreEqual(s1.Volatility, s2.Volatility, tolerance, "Volatility");
      Assert.AreEqual(s1.Accuracy, s2.Accuracy, tolerance, "Accuracy");
      Assert.AreEqual(s1.Steps, s2.Steps, tolerance, "Step");
    }

   
    private static double GetCleanPv(BondPricer pricer)
    {
      return pricer.ProductPv() - pricer.Accrued();
    }

    private Bond GetBond(Dt effecitve, Dt maturity, double coupon)
    {
      return new Bond(effecitve, maturity, _ccy, _type,
        coupon, _dayCount, CycleRule.None, _freq, _roll, _cal)
      {
        Notional = 1.0,
        PeriodAdjustment = false
      };
    }

    private BondPricer GetBondPricer(Bond bond, Dt asOf, Dt settle, 
      DiscountCurve discountCurve, SurvivalCurve survivalCurve,  
      QuotingConvention quoteType)
    {
      
      return new BondPricer(bond, asOf, settle, discountCurve,
        survivalCurve, 0, TimeUnit.None, -1)
      {
        QuotingConvention = quoteType,
        MarketQuote = 1.0
      };
    }

    private DayCount _dayCount = DayCount.Actual365Fixed;
    private Calendar _cal = Calendar.NYB;
    private BDConvention _roll = BDConvention.Following;
    private Frequency _freq = Frequency.SemiAnnual;
    private Currency _ccy = Currency.USD;
    private BondType _type = BondType.USCorp;
  }
}
