//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Numerics;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util.Configuration;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test Bond Calculations.
  /// </summary>
  [TestFixture, Smoke]
  public class TestBond : ToolkitTestBase
  {
    #region Data
    private const double TOLERANCE = 0.001;
    private const double CONVEXITY_TOLERANCE = 0.5;
    #endregion

    #region FRN tests
    private DiscountCurve CreateFRNDiscountCurveForDate(Dt asOf)
    {
      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf,asOf);
      calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);
      calibrator.SwapCalibrationMethod = SwapCalibrationMethod.Extrap;
      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.DayCount = DayCount.Actual365Fixed;
      curve.Frequency = Frequency.Continuous;
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.AddSwap("10Y", Dt.Add(asOf, "10Y"), 0.028, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None,
                    Calendar.None);
      curve.Fit();
      return curve;

    }
    
   
    /// <summary>
    /// Test case for a Floatuing rate note in its last coupon periof where the FRN is quoted as a full price 
    /// </summary>
    [Test]
    public void TestFRNLastCouponQuotedAsPrice()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);   

      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2017));
      BondPricer pricer = new BondPricer(b, new Dt(11, 6, 2017), new Dt(16, 6, 2017), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.MarketQuote = 1.008;
   
      double yield = pricer.YieldToMaturity();

      Assert.AreEqual(0.0661375634775186, yield, 0.0005, "YIeld does not match");
      Assert.AreEqual(386.938699653048, pricer.DiscountMargin() * 10000, TOLERANCE, "Discount MArgin does not match");
      Assert.AreEqual(0.0335124509931847, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.032967355845702, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.00219298909120596, pricer.Convexity(), TOLERANCE, "Convexity does not match");
      Assert.AreEqual(0.0332310946924676, pricer.PV01() * 10000, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(0.0338099428001665, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(0.0332600088141149, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");

        
      
    }

    [Test]
    public void TestFRNLastManualDuration()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2017));
      BondPricer pricer = new BondPricer(b, new Dt(11, 6, 2017), new Dt(16, 6, 2017), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.Yield;
      pricer.MarketQuote = 0.0595188673030954;
      double duration = pricer.ModDuration();

      double p = pricer.FlatPrice();
      double Ai = pricer.AccruedInterest();

      pricer.MarketQuote = 0.0594188673030954;
      double pd = pricer.FlatPrice();
      double dpdy = (p - pd)/(0.0001);
      double man_duration = (-dpdy)/(p + Ai);
      Assert.AreEqual(man_duration, duration, 0.005,
                      "Duration does nto match within the 5% tolerance");
      

        
    }

    /// <summary>
    /// Test case for a Floating rate note in its last coupon period 
    /// where the FRN is quoted as a Yield. The idea is ot check for round trip pricing
    /// </summary>
    [Test]
    public void TestFRNLastCouponQuotedAsYield()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2017));
      BondPricer pricer = new BondPricer(b, new Dt(11, 6, 2017), new Dt(16, 6, 2017), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.Yield;
      pricer.MarketQuote = 0.0661375634775186;

      
      Assert.AreEqual(1.008, pricer.FullPrice(), TOLERANCE, "Full price does not match");
      Assert.AreEqual(386.938699653048, pricer.DiscountMargin() * 10000, TOLERANCE, "Discount MArgin does not match");
      Assert.AreEqual(0.0335124509931847, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.032967355845702, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.00219298909120596, pricer.Convexity(), TOLERANCE, "Convexity does not match");
      Assert.AreEqual(0.0332310946924676, pricer.PV01() * 10000, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(0.0338099428001665, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(0.0332600088141149, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(0.0661375634775186, pricer.YieldToMaturity(), 0.0005, "YIeld does not match");

    }

    /// <summary>
    /// Test case for the floating rate note where the market quyote is a Discountmargin. The FRN is in its last period
    /// </summary>
    [Test]
    public void TestFRNLastCouponQuotedAsDM()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2017));
      BondPricer pricer = new BondPricer(b, new Dt(11, 6, 2017), new Dt(16, 6, 2017), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer.MarketQuote = 0.0386938699653048;


      Assert.AreEqual(1.008, pricer.FullPrice(), TOLERANCE, "Full price does not match");
      Assert.AreEqual(386.938699653048, pricer.DiscountMargin() * 10000, TOLERANCE, "Discount MArgin does not match");
      Assert.AreEqual(0.0335124509931847, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.032967355845702, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.00219298909120596, pricer.Convexity(), TOLERANCE, "Convexity does not match");
      Assert.AreEqual(0.0332310946924676, pricer.PV01() * 10000, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(0.0338099428001665, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(0.0332600088141149, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");

      
      
    }

    [Test]
    public void TestFRNLastCouponHistoricalAmort()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      Dt[] amortDates = { new Dt(15, 11, 2008) };
      double[] amortAmounts = { 0.5 };
      AmortizationUtil.ToSchedule(amortDates,amortAmounts,b.AmortizationSchedule);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2017));
      BondPricer pricer = new BondPricer(b, new Dt(11, 6, 2017), new Dt(16, 6, 2017), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer.MarketQuote = 0.0392822851354784;


      Assert.AreEqual(1.008, pricer.FullPrice(), TOLERANCE, "Full price does not match");
      Assert.AreEqual(0.0667259786476904, pricer.YieldToMaturity(), 0.0005, "YIeld does not match");
      Assert.AreEqual(0.0336845565096001, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.0331318674397641, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.00220037858480112, pricer.Convexity(), TOLERANCE, "Convexity does not match");
      Assert.AreEqual(0.016698461, pricer.PV01() * 10000, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(0.0337058711706433, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(0.0331528323743628, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
    }

    [Test]
    public void TestFRNDurationOnResetDate()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";

      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(28, 12, 2016));
      BondPricer pricer = new BondPricer(b, new Dt(28, 12, 2016), new Dt(28,12, 2016), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer.MarketQuote = 0.0392822851354784;

      
      Assert.AreEqual(2.42180656466068E-05, pricer.PV01(), TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(0.250000002832738, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.245888907073645, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      
    }

    
    [Test]
    public void TestFRNDurationOneDayAfterReset1()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);

      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(29, 12, 2016));
      BondPricer pricer = new BondPricer(b, new Dt(29, 12, 2016), new Dt(29, 12, 2016), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer.MarketQuote = 0.0392822851354784;
      

      Assert.AreEqual(0.247692053484973, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.243618910528553, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");

    }

  [Test]
   public void TestFRNDurationOneDayAfterReset2()
  {
    //setup FRN
    Bond b = new Bond(new Dt(28, 06, 2007),
                      new Dt(28, 06, 2037),
                      Currency.USD,
                      BondType.USCorp,
                      0.005,
                      DayCount.Actual360,
                      CycleRule.None,
                      Frequency.Quarterly,
                      BDConvention.Following,
                      Calendar.NYB);
    b.Index = "USDLIBOR";
    b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);

    DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(29, 12, 2016));
    BondPricer pricer = new BondPricer(b, new Dt(29, 12, 2016), new Dt(29, 12, 2016), curve, null, 0, TimeUnit.None,
                                       -0.04, 0.0, 0.0, true);
    pricer.ReferenceCurve = curve;
    pricer.CurrentRate = 0.04;
    pricer.QuotingConvention = QuotingConvention.DiscountMargin;
    pricer.MarketQuote = 0.0392822851354784;
    

    Assert.AreEqual(0.247944414943511, pricer.Duration(), TOLERANCE, "Duration does not match");
    Assert.AreEqual(0.243867122058651, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
  }



    
    /// <summary>
    /// Test case for the generic floating rate 
    /// </summary>
    [Test]
    public void TestFRNQuotedAsPrice()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.MarketQuote =1.048087;
      

      
      Assert.AreEqual(0.0276174824847913, pricer.YieldToMaturity(), 0.0005, "YIeld does not match");
      Assert.AreEqual(0.0332484789585614, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.033020493235171, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000346083496933707, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(7.9224801363503, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(7.86815547484675, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(1.73788972577535, pricer.DiscountMargin() * 10000, TOLERANCE, "Discount margin does not match");

      
    }

    /// <summary>
    /// Test case for the Floating rate note qhich has been quoted as a Yield 
    /// </summary>
    [Test]
    public void TestFRNQuotedAsYield()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.Yield;
      pricer.MarketQuote = 0.0276174824847913;



      Assert.AreEqual(1.048087, pricer.FullPrice(), TOLERANCE, "Fullprice does not match");
      Assert.AreEqual(0.0332484789585614, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.033020493235171, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000346083496933707, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(7.9224801363503, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(7.86815547484675, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(1.73788972577535, pricer.DiscountMargin() * 10000, TOLERANCE, "Discount margin does not match");
      Assert.AreEqual(0.0276174824847913, pricer.YieldToMaturity(), 0.0005, "YIeld does not match");
    }

    /// <summary>
    /// Test case for the floating rate note which has been quopted as a discount margin
    /// </summary>
    [Test]
    public void TestFRNQuotedAsDM()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer.MarketQuote = 0.000173788972577535;

      Assert.AreEqual(1.048087, pricer.FullPrice(), TOLERANCE, "Fullprice does not match");
      Assert.AreEqual(0.0332484789585614, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.033020493235171, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000346083496933707, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(7.9224801363503, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(7.86815547484675, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(1.73788972577535, pricer.DiscountMargin() * 10000, TOLERANCE, "Discount margin does not match");

    }
    


    	
  
    /// <summary>
    /// Test case for an Amortizing Floating Rate Note whichi is quoted as price
    /// The FRN has Amortized already before the Settle date
    /// </summary>
    [Test]
    public void TestAmortizingFRNQuotedAsPrice()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      Dt[] amortDates = {new Dt(28, 09, 2007), new Dt(28, 06, 2010), new Dt(28, 06, 2011)};
      double[] amortAmounts = {0.5, 0.1, 0.1};
      AmortizationUtil.ToSchedule(amortDates,amortAmounts,b.AmortizationSchedule);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.MarketQuote = 1.048087;

      Assert.AreEqual(0.0258097717206475, pricer.YieldToMaturity(), TOLERANCE, "Yield does not match");
      Assert.AreEqual(-16.3392179156627, pricer.DiscountMargin()*10000, TOLERANCE,
                      "Discount margin does not match");
      Assert.AreEqual(0.0332355408821575, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.0330224653093358, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000173052082993329, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(5.78798208870462, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(5.75087489663558, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(-16.3772605556541, pricer.ImpliedZSpread() * 10000, TOLERANCE, "ZSpread does not match");
     
    }

    /// <summary>
    /// Test case for an Amortizing FRN where the market quote is a yield 
    /// </summary>
    [Test]
    public void TestAmortizingFRNQuotedAsYield()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      Dt[] amortDates = { new Dt(28, 09, 2007), new Dt(28, 06, 2010), new Dt(28, 06, 2011) };
      double[] amortAmounts = { 0.5, 0.1, 0.1 };
      AmortizationUtil.ToSchedule(amortDates, amortAmounts, b.AmortizationSchedule);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.Yield;
      pricer.MarketQuote = 0.0258097717206475;

      Assert.AreEqual(0.0258097717206475, pricer.YieldToMaturity(), TOLERANCE, "Yield does not match");
      Assert.AreEqual( -16.3392179156627, pricer.DiscountMargin() * 10000, TOLERANCE,
                      "Discount margin does not match");
      Assert.AreEqual(0.0332355408821575, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.0330224653093358, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000173052082993329, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(5.78798208870462, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(5.75087489663558, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(-16.3772605556541, pricer.ImpliedZSpread() * 10000, TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(1.048087, pricer.FullPrice(), TOLERANCE, "Full price does not match");
     
    }
    
    /// <summary>
    /// Test case for an Amortizing FRn where the market quote is a Discount margin 
    /// </summary>
    [Test]
    public void TestAmortizingFRNQuotedAsDM()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      Dt[] amortDates = { new Dt(28, 09, 2007), new Dt(28, 06, 2010), new Dt(28, 06, 2011) };
      double[] amortAmounts = { 0.5, 0.1, 0.1 };
      AmortizationUtil.ToSchedule(amortDates, amortAmounts, b.AmortizationSchedule);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer.MarketQuote = -0.00163392179156627;

      Assert.AreEqual(0.0258097717206475, pricer.YieldToMaturity(), TOLERANCE, "Yield does not match");
      Assert.AreEqual(-16.3392179156627, pricer.DiscountMargin() * 10000, TOLERANCE,
                      "Discount margin does not match");
      Assert.AreEqual(0.0332355408821575, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.0330224653093358, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000173052082993329, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(5.78798208870462, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(5.75087489663558, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(-16.3772605556541, pricer.ImpliedZSpread() * 10000, TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(1.048087, pricer.FullPrice(), TOLERANCE, "Full price does not match");
      
    }

    /// <summary>
    /// Test case for an Amortizing FRN where the market quote is a ZSpread
    /// </summary>
    [Test]
    public void TestAmortizingFRNQuotedAsZSpread()
    {
      //setup FRN
      Bond b = new Bond(new Dt(28, 06, 2007),
                        new Dt(28, 06, 2017),
                        Currency.USD,
                        BondType.USCorp,
                        0.005,
                        DayCount.Actual360,
                        CycleRule.None,
                        Frequency.Quarterly,
                        BDConvention.Following,
                        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);
      b.PeriodAdjustment = false;
      Dt[] amortDates = { new Dt(28, 09, 2007), new Dt(28, 06, 2010), new Dt(28, 06, 2011) };
      double[] amortAmounts = { 0.5, 0.1, 0.1 };
      AmortizationUtil.ToSchedule(amortDates, amortAmounts, b.AmortizationSchedule);
      DiscountCurve curve = CreateFRNDiscountCurveForDate(new Dt(11, 06, 2008));
      BondPricer pricer = new BondPricer(b, new Dt(11, 06, 2008), new Dt(16, 06, 2008), curve, null, 0, TimeUnit.None,
                                         -0.04, 0.0, 0.0, true);
      pricer.ReferenceCurve = curve;
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.ZSpread;
      pricer.MarketQuote = -0.00163772605556541;

      Assert.AreEqual(0.0258097717206475, pricer.YieldToMaturity(), TOLERANCE, "Yield does not match");
      Assert.AreEqual(-16.3392179156627, pricer.DiscountMargin() * 10000, TOLERANCE,
                      "Discount margin does not match");
      Assert.AreEqual(0.0332355408821575, pricer.Duration(), TOLERANCE, "Duration does not match");
      Assert.AreEqual(0.0330224653093358, pricer.ModDuration(), TOLERANCE, "Mod Duration does not match");
      Assert.AreEqual(0.000173052082993329, pricer.PV01() * 100, TOLERANCE, "Pv01 does not match");
      Assert.AreEqual(5.78798208870462, pricer.MarketSpreadDuration(), TOLERANCE,
                      "Market Spread Duration odes not match");
      Assert.AreEqual(5.75087489663558, pricer.MarketSpreadModDuration(), TOLERANCE,
                      "Market Spread Mod duration does not match");
      Assert.AreEqual(-16.3772605556541, pricer.ImpliedZSpread() * 10000, TOLERANCE, "ZSpread does not match");
      Assert.AreEqual(1.048087, pricer.FullPrice(), TOLERANCE, "Full price does not match");
    }
    #endregion 

    #region Bloomberg Tests

    #region FRN Tests
    /// <summary>
    /// Tests the accrued interest calculations for a State Bank of India FRN whose last coupon is paid on a weekend. Uses the 
    /// BondPricer.
    /// </summary>
    /// <remarks>
    /// This test failed in v8.7.10 and prior as the accrual days were calculated as starting from the weekend day 
    /// (the actual coupond date if it had been valid) instead of the 1st business day after the weekend (the 1st 
    /// valid settlement date, assuming FOLLOWING BDConvention).
    /// </remarks>
    [Test]
    public void INDGovtCouponDateOnWeekendAccruedInterest()
    {
      //setup FRN
      Bond b = new Bond(
        new Dt(15, 12, 2006),
        new Dt(15, 12, 2011),
        Currency.USD,
        BondType.None,
        0.0619438,
        DayCount.Actual360,
        CycleRule.None,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.Tenor = new Tenor(3, TimeUnit.Months);

      //setup other bond props
      b.Notional = 40000000.0;
      b.FirstCoupon = new Dt(15, 3, 2007);
      b.PeriodAdjustment = true;

      //setup pricer
      Dt asOf = new Dt(5, 10, 2007);
      BondPricer pricer = new BondPricer(b, asOf);
      //pricer.RateResets.Add(new RateReset(new Dt(17, 9, 2007), 0.0619438));
      pricer.CurrentRate = 0.0619438;

      //get values
      double accrued = pricer.AccruedInterest() * b.Notional;

      //test against bloomber values
      Assert.AreEqual(123887.60, accrued, 0.01, "The accrued interest is incorrect");
    }

    /// <summary>
    /// Tests the accrual days remaining calculations for a State Bank of India FRN whose last coupon is paid on a weekend. Uses the 
    /// BondPricer.
    /// </summary>
    /// <remarks>
    /// This test failed in v8.7.10 and prior as the accrual days were calculated as starting from the weekend day 
    /// (the actual coupond date if it had been valid) instead of the 1st business day after the weekend (the 1st 
    /// valid settlement date, assuming FOLLOWING BDConvention).
    /// </remarks>
    [Test]
    public void INDGovtCouponDateOnWeekendAccrualDaysRemaining()
    {
      //setup FRN
      Bond b = new Bond(
        new Dt(15, 12, 2006),
        new Dt(15, 12, 2011),
        Currency.USD,
        BondType.None,
        0.0619438,
        DayCount.Actual360,
        CycleRule.None,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.Tenor = new Tenor(3, TimeUnit.Months);

      //setup other bond props
      b.Notional = 40000000.0;
      b.FirstCoupon = new Dt(15, 3, 2007);
      b.PeriodAdjustment = true;

      //setup pricer
      Dt asOf = new Dt(5, 10, 2007);
      BondPricer pricer = new BondPricer(b, asOf);
      //pricer.RateResets.Add(new RateReset(new Dt(17, 9, 2007), 0.0619438));
      pricer.CurrentRate = 0.0619438;

      //get values
      int accrualDays = pricer.AccrualDays();

      //test against bloomber values
      Assert.AreEqual(18, accrualDays, 0, "The accrual days are incorrect.");
    }

    /// <summary>
    /// Tests the accrued interest calculations using the BondCashFlow Pricer for a State Bank of India FRN whose last coupon was paid on a weekend.
    /// </summary>
    /// <remarks>
    /// This test failed in v8.7.10 and prior as the Cashflows were calculated as starting from the weekend day 
    /// (the actual coupond date if it had been valid) instead of the 1st business day after the weekend (the 1st 
    /// valid settlement date, assuming FOLLOWING BDConvention).
    /// </remarks>
    [Test]
    [Ignore("Failure likely to be cuased by Rohan's changes to the way bond special cases are handled (lon/short first/last coupon.")]
    public void INDGovtCouponDateOnWeekendAccruedInterestBondCashflow()
    {
      //setup FRN
      Bond b = new Bond(
        new Dt(15, 12, 2006),
        new Dt(15, 12, 2011),
        Currency.USD,
        BondType.None,
        0.0619438,
        DayCount.Actual360,
        CycleRule.EOM,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      b.Index = "USDLIBOR";
      b.Tenor = new Tenor(3, TimeUnit.Months);

      //setup other bond props
      b.Notional = 40000000.0;
      b.FirstCoupon = new Dt(15, 3, 2007);
      b.PeriodAdjustment = true;

      //setup pricer
      Dt asOf = new Dt(7, 10, 2007);
      BaseEntity.Toolkit.Curves.DiscountCurve discCurve = CalibratorUtil.BuildBootstrapDiscountCurve(asOf,
                                                                                  asOf,
                                                                                  DayCount.Actual360,
                                                                                  new string[] { "6 Months" },
                                                                                  new double[] { 0.05 },
                                                                                  DayCount.Actual360,
                                                                                  Frequency.SemiAnnual,
                                                                                  new string[] { "2 Years" },
                                                                                  new double[] { 0.05 });
      BondPricer pricer = new BondPricer(b, asOf, asOf, discCurve, null, 3, TimeUnit.Months, 1.0);
      pricer.ReferenceCurve = discCurve;
      //pricer.RateResets.Add(new RateReset(new Dt(17, 9, 2007), 0.0619438));
      pricer.CurrentRate = 0.0619438;

      //calc carry to use in expected value
      Dt asOf2 = new Dt(18, 9, 2007);
      BondPricer pricer2 = new BondPricer(b, asOf2, asOf2);
      //pricer2.RateResets.Add(new RateReset(new Dt(17, 9, 2007), 0.0619438));
      pricer2.CurrentRate = 0.0619438;
      double carry = pricer2.Accrued() * b.Notional;

      //get values
      Cashflow cashFlows = new Cashflow(new Dt(7, 10, 2007));
      pricer.GenerateCashflow(cashFlows, new Dt(7, 10, 2007));
      double accruedCF = cashFlows.GetAccrued(0) * b.Notional;
      double expected = carry * Dt.FractionDays(new Dt(17, 9, 2007), new Dt(17, 12, 2007), b.AccrualDayCount);// +6882.644444 * 2;

      //test against bloomberg values
      Assert.AreEqual(expected, accruedCF, 0.01, "The accrued interest is incorrect");
    }
    #endregion

    #region 7Jan08 Tests

    private DiscountCurve Create7Jan08DiscountCurve(Dt asof, Dt settlement)
    {
      //setup curves
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asof, settlement);
      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.AddMoneyMarket("6 Months", Dt.AddMonth(settlement, 6, true), 0.043637, DayCount.Thirty360);
      ircurve.AddMoneyMarket("1 Years", Dt.Add(settlement, 1, TimeUnit.Years), 0.039425, DayCount.Thirty360);
      ircurve.AddSwap("2 Years", Dt.Add(settlement, 2, TimeUnit.Years), 0.035495, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("3 Years", Dt.Add(settlement, 3, TimeUnit.Years), 0.03619, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("5 Years", Dt.Add(settlement, 5, TimeUnit.Years), 0.039215, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("7 Years", Dt.Add(settlement, 7, TimeUnit.Years), 0.04193, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("10 Years", Dt.Add(settlement, 10, TimeUnit.Years), 0.04475, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.Fit();
      return ircurve;
    }

    private DiscountCurve Create7Jan08DiscountCurve_New(Dt asOf)
    {
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asOf, asOf);
      ircal.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);
      ircal.SwapCalibrationMethod = SwapCalibrationMethod.Extrap;
      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      ircurve.AddMoneyMarket("6 Months", Dt.AddMonth(asOf, 6, true), 0.043637, DayCount.Actual360);
      ircurve.AddMoneyMarket("1 Years", Dt.Add(asOf, 1, TimeUnit.Years), 0.039425, DayCount.Actual360);
      ircurve.AddSwap("2 Years", Dt.Add(asOf, 2, TimeUnit.Years), 0.035495, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("3 Years", Dt.Add(asOf, 3, TimeUnit.Years), 0.03619, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("5 Years", Dt.Add(asOf, 5, TimeUnit.Years), 0.039215, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("7 Years", Dt.Add(asOf, 7, TimeUnit.Years), 0.04193, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.AddSwap("10 Years", Dt.Add(asOf, 10, TimeUnit.Years), 0.04475, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);
      ircurve.Fit();
      return ircurve;
      
    }

    private SurvivalCurve CreateFlatSpreadCurve(Dt asof, Dt settlement, DiscountCurve ircurve, double spread, double recovery)
    {
      SurvivalFitCalibrator survcal = new SurvivalFitCalibrator(asof, settlement, recovery, ircurve);
      SurvivalCurve curve = new SurvivalCurve(survcal);
      curve.AddCDS("5 Years", Dt.CDSRoll(Dt.Add(settlement, 5, TimeUnit.Years), false), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.Fit();
      return curve;
    }

    /// <summary>
    /// Tests bond calculations against Bloomberg values for "C 5.3 Jan16" corporate bond. 
    /// </summary>
    /// <remarks>
    /// Bloomberg screenshots and XL comparison are stored in spreadsheet "Test Bloomberg 7Jan08.xls" for this bond.
    /// </remarks>
    [Test]
    public void C5_3Jan16Test()
    {
      Dt maturity = new Dt(7, 1, 2016);
      Dt issue = new Dt(12, 8, 2005);
      Dt asOf = new Dt(7, 1, 2008);
      Dt settlement = new Dt(10, 1, 2008);
      double coupon = 0.053;
      DayCount dayCount = DayCount.Thirty360;
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

      //setup pricer
      DiscountCurve ircurve = Create7Jan08DiscountCurve(asOf, settlement);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, settlement, ircurve, 0.01555, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 3, TimeUnit.Months, -1);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.99267;

      //test against known values
      Assert.AreEqual(0.044167, 100.0 * pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(3, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(5.414, pricer.YieldToMaturity() * 100, 0.001, "The ytm is incorrect.");
      Assert.AreEqual(6.43, pricer.ModDuration(), 0.01, "The modified duration is incorrect.");
      Assert.AreEqual(0.5, pricer.Convexity() / 100, 0.01, "The convexity is incorrect.");
      Assert.AreEqual(113.1, 10000.0 * pricer.ImpliedZSpread(), 0.05 * 113.1, "The z-spread is incorrect.");
    }

    /// <summary>
    /// Tests bond calculations against Bloomberg values for "C 5.35 Sep20 CB" corporate callable bond. 
    /// </summary>
    /// <remarks>
    /// Bloomberg screenshots and XL comparison are stored in spreadsheet "Test Bloomberg 7Jan08.xls" for this bond.
    /// </remarks>
    [Test]
    [Ignore("Known to fail.")]
    public void C5_35Sep20CBTest()
    {
      Dt maturity = new Dt(15, 8, 2020);
      Dt issue = new Dt(12, 8, 2005);
      Dt asOf = new Dt(7, 1, 2008);
      Dt settlement = new Dt(10, 1, 2008);
      Dt firstCoupon = new Dt(15, 2, 2006);
      double coupon = 0.0535;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
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
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 8, 2008), new Dt(15, 8, 2020), 1, 1, OptionStyle.American, 1));

      //setup pricer
      DiscountCurve ircurve = Create7Jan08DiscountCurve(asOf, settlement);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, settlement, ircurve, 0.02901, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 3, TimeUnit.Months, 0.4, 0.1, 0.235, false);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.93943;

      //test against known values
      Assert.AreEqual(0.817361, pricer.AccruedInterest() * 100, TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(55, pricer.AccrualDays(), 0, "The accrual days are incorrect.");
      Assert.AreEqual(6.085, pricer.YieldToMaturity() * 100, 6.085 * 0.05, "The ytm is incorrect.");
      Assert.AreEqual(8.78, pricer.ModDuration(), 0.05 * 8.78, "The modified duration is incorrect.");
      Assert.AreEqual(0.99, pricer.Convexity() / 100, 0.02, "The convexity is incorrect.");
      Assert.AreEqual(145.3, 10000.0 * pricer.ImpliedZSpread(), 0.05 * 145.3, "The z-spread is incorrect.");
    }


    /// <summary>
    /// Tests bond calculations against Bloomberg values for "C Jan09 FRN" corporate FRN. 
    /// </summary>
    /// <remarks>
    /// Bloomberg screenshots and XL comparison are stored in spreadsheet "Test Bloomberg 7Jan08.xls" for this bond.
    /// </remarks>
    [Test]
    public void CJan09FRNTest()
    {
      Dt maturity = new Dt(30, 1, 2009);
      Dt issue = new Dt(31, 1, 2006);
      Dt asOf = new Dt(7, 1, 2008);
      Dt settlement = new Dt(10, 1, 2008);
      double coupon = 0.0502375;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
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
      b.Index = "USDLIBOR";
      b.Coupon = 0.0004;
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 2);

      //setup pricer
      DiscountCurve ircurve = Create7Jan08DiscountCurve_New(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, settlement, ircurve, 0.02901, 0.4);
      BondPricer pricer = new BondPricer(b,asOf,settlement);
     
      pricer.Notional = 1000000.0;
      pricer.ReferenceCurve = ircurve;
      pricer.DiscountCurve = ircurve;
      pricer.SurvivalCurve = curve;
      //pricer.RateResets.Add(new RateReset(new Dt(30, 10, 2007), 0.0502375));
      pricer.CurrentRate = 0.0502375;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.99682;

      
      //test against known values
      Assert.AreEqual(10047.5, pricer.Accrued(), TOLERANCE, "The accrued interest is incorrect");

      //test against our values
      Assert.AreEqual(0.0471723291366921, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(0.00377598498939699, pricer.DiscountMargin(), TOLERANCE, "The Discount Margin is incorrect");
      Assert.AreEqual(37.7703258177584, 10000.0 * pricer.ImpliedZSpread(), 0.05 * 37.77, "The z-spread is incorrect.");

      //Test round trip pricing by supplying Yield as the Market Quote 
      BondPricer pricer2 = new BondPricer(b, asOf, settlement, ircurve, curve, 3, TimeUnit.Months, 0.4);
      pricer2.Notional = 1000000.0;
      pricer2.ReferenceCurve = ircurve;
      //pricer.RateResets.Add(new RateReset(new Dt(30, 10, 2007), 0.0502375));
      pricer2.CurrentRate = 0.0502375;
      pricer2.QuotingConvention = QuotingConvention.Yield;
      pricer2.MarketQuote = 0.0471723291366921;
      Assert.AreEqual(0.99682, pricer2.FlatPrice(), TOLERANCE, "The round trip flat price is incoorect");
      Assert.AreEqual(0.00377598498939699, pricer2.DiscountMargin(), TOLERANCE, "The Discount Margin is incorrect");
      Assert.AreEqual(37.7703258177584, 10000.0 * pricer2.ImpliedZSpread(), 0.05 * 37.77, "The z-spread is incorrect.");

      //Test round trip pricing by supplying the Discount margin as the market quote 
      BondPricer pricer3 = new BondPricer(b, asOf, settlement, ircurve, curve, 3, TimeUnit.Months, 0.4);
      pricer3.Notional = 1000000.0;
      pricer3.ReferenceCurve = ircurve;
      //pricer.RateResets.Add(new RateReset(new Dt(30, 10, 2007), 0.0502375));
      pricer3.CurrentRate = 0.0502375;
      pricer3.QuotingConvention = QuotingConvention.DiscountMargin;
      pricer3.MarketQuote = 0.00345887884732742;
      Assert.AreEqual(0.99682, pricer3.FlatPrice(), TOLERANCE, "The round trip flat price is incoorect");
      Assert.AreEqual(0.0468552229946225, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(37.7703258177584, 10000.0 * pricer.ImpliedZSpread(), 0.05 * 37.77, "The z-spread is incorrect.");
    }
    #endregion

    #region AUD BB Tests

    #region 25Mar08 Tests
    private DiscountCurve Create25Mar08DiscountCurve(Dt asof)
    {
      //setup curves
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asof, asof);
      ircal.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      ircurve.Ccy = Currency.AUD;
      ircurve.Category = "None";

      Dt shiftAsOf = Dt.AddDays(asof, 3, Calendar.SYB);

      //DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.AddMoneyMarket("1 Wk", Dt.Roll(Dt.Add(shiftAsOf, "1 Wk"), BDConvention.Following, Calendar.SYB), 0.07245, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("1 Months", Dt.Roll(Dt.Add(shiftAsOf, "1 Months"), BDConvention.Following, Calendar.SYB), 0.0754, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("2 Months", Dt.Roll(Dt.Add(shiftAsOf, "2 Months"), BDConvention.Following, Calendar.SYB), 0.076517, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("3 Months", Dt.Roll(Dt.Add(shiftAsOf, "3 Months"), BDConvention.Following, Calendar.SYB), 0.07725, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("4 Months", Dt.Roll(Dt.Add(shiftAsOf, "4 Months"), BDConvention.Following, Calendar.SYB), 0.077833, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("5 Months", Dt.Roll(Dt.Add(shiftAsOf, "5 Months"), BDConvention.Following, Calendar.SYB), 0.078533, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("6 Months", Dt.Roll(Dt.Add(shiftAsOf, "6 Months"), BDConvention.Following, Calendar.SYB), 0.07955, DayCount.Actual365Fixed);
      ircurve.AddMoneyMarket("9 Months", Dt.Roll(Dt.Add(shiftAsOf, "9 Months"), BDConvention.Following, Calendar.SYB), 0.0779, DayCount.Actual365Fixed);

      ircurve.AddSwap("1 Years", Dt.Roll(Dt.Add(shiftAsOf, "1 Years"), BDConvention.Following, Calendar.SYB), 0.07598, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("2 Years", Dt.Roll(Dt.Add(shiftAsOf, "2 Years"), BDConvention.Following, Calendar.SYB), 0.0737, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("3 Years", Dt.Roll(Dt.Add(shiftAsOf, "3 Years"), BDConvention.Following, Calendar.SYB), 0.0729, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("4 Years", Dt.Roll(Dt.Add(shiftAsOf, "4 Years"), BDConvention.Following, Calendar.SYB), 0.0736, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("5 Years", Dt.Roll(Dt.Add(shiftAsOf, "5 Years"), BDConvention.Following, Calendar.SYB), 0.0734, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("6 Years", Dt.Roll(Dt.Add(shiftAsOf, "6 Years"), BDConvention.Following, Calendar.SYB), 0.0727, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("7 Years", Dt.Roll(Dt.Add(shiftAsOf, "7 Years"), BDConvention.Following, Calendar.SYB), 0.07225, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("10 Years", Dt.Roll(Dt.Add(shiftAsOf, "10 Years"), BDConvention.Following, Calendar.SYB), 0.0714, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("15 Years", Dt.Roll(Dt.Add(shiftAsOf, "15 Years"), BDConvention.Following, Calendar.SYB), 0.0699, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("20 Years", Dt.Roll(Dt.Add(shiftAsOf, "20 Years"), BDConvention.Following, Calendar.SYB), 0.06735, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("25 Years", Dt.Roll(Dt.Add(shiftAsOf, "25 Years"), BDConvention.Following, Calendar.SYB), 0.06515, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);
      ircurve.AddSwap("30 Years", Dt.Roll(Dt.Add(shiftAsOf, "30 Years"), BDConvention.Following, Calendar.SYB), 0.0624, DayCount.Actual365Fixed, Frequency.Quarterly, BDConvention.None, Calendar.None);

      ircurve.Fit();
      return ircurve;
    }

    private SurvivalCurve CreateFlatSpreadCurveAUD(Dt asof, DiscountCurve ircurve, double spread, double recovery)
    {
      Dt settle = Settings.SurvivalCalibrator.UseNaturalSettlement ? Dt.Add(asof, 1) : asof;
      SurvivalFitCalibrator survcal = new SurvivalFitCalibrator(asof, settle, recovery, ircurve);
      SurvivalCurve curve = new SurvivalCurve(survcal);
      curve.AddCDS("6 Months", Dt.CDSMaturity(settle, "6 Months"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("1 Years", Dt.CDSMaturity(settle, "1 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("2 Years", Dt.CDSMaturity(settle, "2 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("3 Years", Dt.CDSMaturity(settle, "3 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("5 Years", Dt.CDSMaturity(settle, "5 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("7 Years", Dt.CDSMaturity(settle, "7 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("10 Years", Dt.CDSMaturity(settle, "10 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.Fit();
      return curve;
    }

    /// <summary>
    /// Tests bond calculations against BBW Values values for "AUDGvtB Aug08". 
    /// </summary>
    /// <remarks>
    /// BBW screenshots and XL comparison are stored in spreadsheet "Bond Tests", Sheet:Fixed Rate AUDGvtB Pricing for this bond.
    /// </remarks>
    [Test]
    public void AUD_Gvt_Aug08_DiscountingAccruedTrue()
    {
      Dt maturity = new Dt(15, 8, 2008);
      Dt issue = new Dt(11, 9, 1995);
      Dt asOf = new Dt(25, 3, 2008);
      Dt settlement = new Dt(28, 3, 2008);
      double coupon = 0.0875;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.SYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.AUD;
      BondType type = BondType.AUSGovt;

      double tol = 0.00001;

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

      //setup other bond props

      //setup pricer
      DiscountCurve ircurve = Create25Mar08DiscountCurve(asOf);
      //SurvivalCurve curve = CreateFlatSpreadCurveAUD(asOf, ircurve, 0.02000, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -1, 0.1, 0.1, false);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.00752574;

      //test against known values 
      // values below depend on the DiscountAccrued flag: Results below are for the flag set to "TRUE"
      Assert.AreEqual(1.01, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(42, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(6.700143, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual( 0.384616, pricer.Duration(), tol, "The yield duration is incorrect.");
      Assert.AreEqual(0.3721486, pricer.ModDuration(), tol, "The yield mod duration is incorrect.");

      Assert.AreEqual( 6.7188558665, 100 * pricer.Irr(), tol, "The IRR is incorrect.");
      Assert.AreEqual(0.3798622, pricer.RateDuration(), tol, "The effective duration is incorrect.");
      Assert.AreEqual(0.003865, pricer.Rate01() / pricer.Notional * 100.0, tol, "The effective DV01 is incorrect.");
      Assert.AreEqual(0.0029019961, pricer.RateConvexity() / 100, tol, "The effective convexity is incorrect.");
      Assert.AreEqual(-115.072973885, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
    }

    /// <summary>
    /// Tests bond calculations against BBW Values values for "AUDGvtB Aug08". 
    /// </summary>
    /// <remarks>
    /// BBW screenshots and XL comparison are stored in spreadsheet "Bond Tests", Sheet:Fixed Rate AUDGvtB Pricing for this bond.
    /// </remarks>
    [Test]
    public void AUD_Gvt_Aug08_DiscountingAccruedFalse()
    {
      Dt maturity = new Dt(15, 8, 2008);
      Dt issue = new Dt(11, 9, 1995);
      Dt asOf = new Dt(25, 3, 2008);
      Dt settlement = new Dt(28, 3, 2008);
      double coupon = 0.0875;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.SYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.AUD;
      BondType type = BondType.AUSGovt;

      double tol = 0.00001;

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

      //setup other bond props

      //setup pricer
      DiscountCurve ircurve = Create25Mar08DiscountCurve(asOf);
      //SurvivalCurve curve = CreateFlatSpreadCurveAUD(asOf, ircurve, 0.02000, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -1, 0.1, 0.1, false);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.00752574;

      Type objectType = pricer.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued",BindingFlags.Instance|BindingFlags.Public |BindingFlags.NonPublic);
      property.SetValue((object)pricer,false,null);
      
      //test against known values 
      // values below depend on the DiscountAccrued flag: Results below are for the flag set to "FALSE"
      Assert.AreEqual(1.01, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual( 42, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual( 6.700143, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual( 0.384616, pricer.Duration(), tol, "The yield duration is incorrect.");
      Assert.AreEqual(0.3721486, pricer.ModDuration(), tol, "The yield mod duration is incorrect.");
      
      Assert.AreEqual( 6.78644434, 100 * pricer.Irr(), tol, "The IRR is incorrect.");
      Assert.AreEqual( 0.3760882, pricer.RateDuration(), tol, "The effective duration is incorrect.");
      Assert.AreEqual(0.0038271, pricer.Rate01() / pricer.Notional * 100.0, tol, "The effective DV01 is incorrect.");
      Assert.AreEqual( 0.00287362, pricer.RateConvexity() / 100, tol, "The effective convexity is incorrect.");
      Assert.AreEqual( -108.31412545, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
    }

    /// <summary>
    /// Tests bond calculations against BBW Values values for "NAB 7.75 Nov10". " fixed rate corporate bond. 
    /// </summary>
    /// <remarks>
    /// BBW screenshots and XL comparison are stored in spreadsheet ""Bond Tests", Sheet:NAB 7.75 Nov10 for this bond.
    /// </remarks>
    [Test]
    public void NAB_775_Nov10_DiscountingAccruedFalse()
    {
      // TODO: fix duration inaccuracy
      double durationInaccuracy = 10;

      Dt maturity = new Dt(26, 11, 2010);
      Dt issue = new Dt(20, 11, 2007);
      Dt asOf = new Dt(25, 3, 2008);
      Dt settlement = new Dt(28, 3, 2008);
      double coupon = 0.0775;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.SYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.AUD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

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
      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      DiscountCurve ircurve = Create25Mar08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurveAUD(asOf, ircurve, 0.02000, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.98888;
 
      Type objectType = pricer.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue((object)pricer, false, null);
      //test against known values 
      // values below depend on the DiscountAccrued flag: Results below are for the flag set to "FALSE"
      Assert.AreEqual(2.618818681, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(123, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual( 8.216267, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual( 2.3939504, pricer.Duration(), tol, "The yield duration is incorrect.");
      Assert.AreEqual(2.2994846, pricer.ModDuration(), tol, "The yield mod duration is incorrect.");

      Assert.AreEqual(8.2194481, 100 * pricer.Irr(), tol, "The IRR is incorrect.");
      Assert.AreEqual(2.3145744, pricer.RateDuration(), durationInaccuracy * tol, "The effective duration is incorrect.");
      Assert.AreEqual(0.0234945, pricer.Rate01() / pricer.Notional * 100.0, tol, "The effective DV01 is incorrect.");
      Assert.AreEqual(0.0637755341, pricer.RateConvexity() / 100, tol, "The effective convexity is incorrect.");
      Assert.AreEqual(85.8000296, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
      
     
    }

    /// <summary>
    /// Tests bond calculations against BBW Values values for "NAB 7.75 Nov10". " fixed rate corporate bond. 
    /// </summary>
    /// <remarks>
    /// BBW screenshots and XL comparison are stored in spreadsheet ""Bond Tests", Sheet:NAB 7.75 Nov10 for this bond.
    /// </remarks>
    [Test]
    public void NAB_775_Nov10_DiscountingAccruedTrue()
    {
      // TODO: fix duration inaccuracy
      

      Dt maturity = new Dt(26, 11, 2010);
      Dt issue = new Dt(20, 11, 2007);
      Dt asOf = new Dt(25, 3, 2008);
      Dt settlement = new Dt(28, 3, 2008);
      double coupon = 0.0775;
      DayCount dayCount = DayCount.ActualActualBond;
      Calendar cal = Calendar.SYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.AUD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

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
      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      DiscountCurve ircurve = Create25Mar08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurveAUD(asOf, ircurve, 0.02000, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.98888;

      
      //test against known values 
      // values below depend on the DiscountAccrued flag: Results below are for the flag set to "TRUE"
      Assert.AreEqual(2.618818681, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(123, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(8.216267, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(2.3939504, pricer.Duration(), tol, "The yield duration is incorrect.");
      Assert.AreEqual(2.2994846, pricer.ModDuration(), tol, "The yield mod duration is incorrect.");

      Assert.AreEqual(8.2049513383, 100 * pricer.Irr(), tol, "The IRR is incorrect.");
      Assert.AreEqual(2.31993658512271, pricer.RateDuration(), tol, "The effective duration is incorrect.");
      Assert.AreEqual(0.0235489338958734, pricer.Rate01() / pricer.Notional * 100.0, tol, "The effective DV01 is incorrect.");
      Assert.AreEqual(0.0638209233793444, pricer.RateConvexity() / 100, tol, "The effective convexity is incorrect.");
      Assert.AreEqual(84.2818091, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");


    }
    #endregion

    #endregion

    #endregion Bloomberg Tests

    #region Lehman Tests

    #region 11Jan08 Tests

    private DiscountCurve Create11Jan08DiscountCurve(Dt asof)
    {
      //setup curves
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asof, asof);
      ircal.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      ircurve.Ccy = Currency.USD;
      ircurve.Category = "None";

      //DiscountCurve ircurve = new DiscountCurve(ircal);

      ircurve.AddMoneyMarket("6 Months", Dt.Add(asof, "6 Months"), 0.040812, DayCount.Actual360);
      ircurve.AddMoneyMarket("1 Years", Dt.Add(asof, "1 Years"), 0.037150, DayCount.Actual360);
      ircurve.AddSwap("2 Years", Dt.Add(asof, "2 Years"), 0.032878, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("3 Years", Dt.Add(asof, "3 Years"), 0.034038, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("5 Years", Dt.Add(asof, "5 Years"), 0.037559, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("7 Years", Dt.Add(asof, "7 Years"), 0.040774, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("10 Years", Dt.Add(asof, "10 Years"), 0.044136, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.Fit();
      return ircurve;
    }

    private SurvivalCurve CreateFlatSpreadCurve(Dt asof, DiscountCurve ircurve, double spread, double recovery)
    {
      Dt settle = Settings.SurvivalCalibrator.UseNaturalSettlement ? Dt.Add(asof, 1) : asof;
      SurvivalFitCalibrator survcal = new SurvivalFitCalibrator(asof, settle, recovery, ircurve);
      SurvivalCurve curve = new SurvivalCurve(survcal);
      curve.AddCDS("6 Months", Dt.CDSMaturity(settle, "6 Months"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("1 Years", Dt.CDSMaturity(settle, "1 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("2 Years", Dt.CDSMaturity(settle, "2 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("3 Years", Dt.CDSMaturity(settle, "3 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("5 Years", Dt.CDSMaturity(settle, "5 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("7 Years", Dt.CDSMaturity(settle, "7 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("10 Years", Dt.CDSMaturity(settle, "10 Years"), spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.Fit();
      return curve;
    }

    /// <summary>
    /// Tests bond calculations against Lehman Values values for "GM 8.1 Jun24" callable corporate bond. 
    /// </summary>
    /// <remarks>
    /// Lehman screenshots and XL comparison are stored in spreadsheet "Bond Tests GM", Sheet:8.1 Jun24 for this bond.
    /// </remarks>
    [Test, Smoke, Ignore("Unknown, To investigate")]
    public void GM_81_Jun24()
    {
      Dt maturity = new Dt(15, 6, 2024);
      Dt issue = new Dt(5, 6, 1996);
      Dt asOf = new Dt(11, 1, 2008);
      Dt settlement = new Dt(16, 1, 2008);
      double coupon = 0.081;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double tol = 0.00001;

      //setup Callable Bond
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
      //b.Callable = true;
      b.Notional = 1.0;
      Dt[] callStartDates = { new Dt(5, 6, 1996), new Dt(15, 6, 2008), new Dt(15, 6, 2009), new Dt(15, 6, 2010), 
        new Dt(15, 6, 2011), new Dt(15, 6, 2012), new Dt(15, 6, 2013), new Dt(15, 6, 2014), new Dt(15, 6, 2015)};

      Dt[] callEndDates = { new Dt(15, 6, 2008), new Dt(15, 6, 2009), new Dt(15, 6, 2010), new Dt(15, 6, 2011), 
        new Dt(15, 6, 2012), new Dt(15, 6, 2013), new Dt(15, 6, 2014), new Dt(15, 6, 2015), new Dt(15, 6, 2016)};

      double[] callPrices = { 103.089996, 102.704002, 102.318001, 101.931, 101.544998, 101.158997, 100.773003, 100.386002, 100.000 };

      // Add calls
      if (callStartDates != null && callStartDates.Length > 0 && callEndDates != null && callEndDates.Length > 0)
        for (int j = 0; j < callStartDates.Length; j++)
          if (callPrices[j] > 0.0)
            b.CallSchedule.Add(new CallPeriod(callStartDates[j], callEndDates[j], callPrices[j] / 100.0, 1000.0, OptionStyle.American, 0));

      /*
      b.CallSchedule.Add(new CallPeriod(new Dt(5, 6, 1996),  new Dt(15, 6, 2008), 1.03090, 1.03090, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2008), new Dt(15, 6, 2009), 1.02704, 1.02704, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2009), new Dt(15, 6, 2010), 1.02318, 1.02318, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2010), new Dt(15, 6, 2011), 1.01931, 1.01931, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2011), new Dt(15, 6, 2012), 1.01545, 1.01545, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2012), new Dt(15, 6, 2013), 1.01159, 1.01159, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2013), new Dt(15, 6, 2014), 1.00773, 1.00773, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2014), new Dt(15, 6, 2015), 1.00386, 1.00386, OptionStyle.American, 1));
      b.CallSchedule.Add(new CallPeriod(new Dt(15, 6, 2015), new Dt(15, 6, 2016), 1.00000, 1.00000, OptionStyle.American, 1));
      */

      //setup pricer
      DiscountCurve ircurve = Create11Jan08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1, 0.1, 0.1, false);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.999976;

      //test against known values 
      //test against known values 
      Assert.AreEqual(0.6975, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(31, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(8.099, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(6.580418, pricer.RateDuration(), tol, "The effective duration is incorrect.");
      Assert.AreEqual(-0.06626158, pricer.Rate01() / pricer.Notional * 100.0, tol, "The effective DV01 is incorrect.");
      Assert.AreEqual(-2.883808, pricer.RateConvexity() / 100, tol, "The effective convexity is incorrect.");
      Assert.AreEqual(349.0438209, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
      //Assert.AreEqual("asset swap-spread", 349.0438209, 10000.0 * pricer.AssetSwapSpread(b.DayCount,b.Freq), tol, "The asset swap-spread is incorrect.");

    }

    /// <summary>
    /// Tests bond calculations against Lehman Values values for "GM 6.75 May28" fixed rate corporate bond. 
    /// </summary>
    /// <remarks>
    /// Lehman screenshots and XL comparison are stored in spreadsheet "Bond Tests GM", Sheet:6.75 May28 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void GM_675_May28_DiscountingAccruedFalse()
    {
      Dt maturity = new Dt(1, 5, 2028);
      Dt issue = new Dt(21, 4, 1998);
      Dt asOf = new Dt(11, 1, 2008);
      Dt settlement = new Dt(16, 1, 2008);
      double coupon = 0.0675;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      DiscountCurve ircurve = Create11Jan08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.02000, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.000083;

      Type objectType = pricer.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue(pricer, false, null);
      //test against known values 
      Assert.AreEqual(0.014063, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(75, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(6.748, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(11.183717266, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(1.7129874543, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(237.133592, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
      Assert.AreEqual(233.2154345816, 10000.0 * pricer.AssetSwapSpread(b.AccrualDayCount, b.Freq), tol, "The asset swap-spread is incorrect.");
    }

    /// <summary>
    /// Tests bond calculations against Lehman Values values for "GM 6.75 May28" fixed rate corporate bond. 
    /// </summary>
    /// <remarks>
    /// Lehman screenshots and XL comparison are stored in spreadsheet "Bond Tests GM", Sheet:6.75 May28 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void GM_675_May28_DiscountingAccruedTrue()
    {
      Dt maturity = new Dt(1, 5, 2028);
      Dt issue = new Dt(21, 4, 1998);
      Dt asOf = new Dt(11, 1, 2008);
      Dt settlement = new Dt(16, 1, 2008);
      double coupon = 0.0675;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      DiscountCurve ircurve = Create11Jan08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.02000, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.000083;

      Assert.AreEqual(0.014063, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(75, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(6.748, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(11.183717266, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(1.7129874543, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(236.894309850507, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
      Assert.AreEqual(233.130402260991, 10000.0 * pricer.AssetSwapSpread(b.AccrualDayCount, b.Freq), tol, "The asset swap-spread is incorrect.");
      
      
    }
    /// <summary>
    /// Tests bond calculations against Lehman Values values for "GM 6.75 May28" fixed rate corporate bond. 
    /// </summary>
    /// <remarks>
    /// Lehman screenshots and XL comparison are stored in spreadsheet "Bond Tests GM", Sheet:6.75 May28 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void GM_575_Jan17_DiscountingAccruedFalse()
    {
      Dt maturity = new Dt(12, 1, 2017);
      Dt issue = new Dt(5, 9, 2006);
      Dt asOf = new Dt(11, 1, 2008);
      Dt settlement = new Dt(16, 1, 2008);
      double coupon = 0.0575;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      DiscountCurve ircurve = Create11Jan08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1);
      
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.999991;
      Type objectType = pricer.GetType();
      PropertyInfo property = objectType.GetProperty("DiscountingAccrued", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      property.SetValue(pricer, false, null);

      //test against known values 
      Assert.AreEqual(0.000639, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(4, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(5.75, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(7.139285494, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.5889739974, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(146.542182917769, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
      Assert.AreEqual(143.025171796201, 10000.0 * pricer.AssetSwapSpread(b.AccrualDayCount, b.Freq), tol, "The asset swap-spread is incorrect.");
    }

    /// <summary>
    /// Tests bond calculations against Lehman Values values for "GM 6.75 May28" fixed rate corporate bond. 
    /// </summary>
    /// <remarks>
    /// Lehman screenshots and XL comparison are stored in spreadsheet "Bond Tests GM", Sheet:6.75 May28 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void GM_575_Jan17_DiscountingAccruedTrue()
    {
      Dt maturity = new Dt(12, 1, 2017);
      Dt issue = new Dt(5, 9, 2006);
      Dt asOf = new Dt(11, 1, 2008);
      Dt settlement = new Dt(16, 1, 2008);
      double coupon = 0.0575;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      //setup pricer
      DiscountCurve ircurve = Create11Jan08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, curve, 0, TimeUnit.None, -1);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.999991;
      

      //test against known values 
      Assert.AreEqual(0.000639, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(4, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(5.75, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(7.139285494, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.5889739974, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(146.517380993099, 10000.0 * pricer.ImpliedZSpread(), tol, "The z-spread is incorrect.");
      Assert.AreEqual(143.012064741333, 10000.0 * pricer.AssetSwapSpread(b.AccrualDayCount, b.Freq), tol, "The asset swap-spread is incorrect.");
    }

    #endregion

    #region Floating rate note tests 

    private DiscountCurve CreateFRNDiscountCurve(Dt asof)
    {
      //setup curves
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asof, asof);
      ircal.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      ircurve.Ccy = Currency.USD;
      ircurve.Category = "None";

      //DiscountCurve ircurve = new DiscountCurve(ircal);
      
      ircurve.AddSwap("10 Years", Dt.Add(asof, "10 Years"), 0.0280, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.Fit();
      return ircurve;
    }

    private SurvivalCurve CreateFRNSurvivalCurve(Dt asof, DiscountCurve ircurve, double recovery)
    {
      Dt settle = Settings.SurvivalCalibrator.UseNaturalSettlement ? Dt.Add(asof, 1) : asof;
      SurvivalFitCalibrator survcal = new SurvivalFitCalibrator(asof, settle, recovery, ircurve);
      SurvivalCurve curve = new SurvivalCurve(survcal);
      curve.AddCDS("3 Months", Dt.CDSMaturity(settle, "3 Months"), 0.0228, DayCount.Actual360, Frequency.Quarterly,BDConvention.Following, Calendar.NYB);
      curve.AddCDS("6 Months", Dt.CDSMaturity(settle, "6 Months"), 0.0228, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("1 Years", Dt.CDSMaturity(settle, "1 Years"), 0.0301, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("2 Years", Dt.CDSMaturity(settle, "2 Years"), 0.0370, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("3 Years", Dt.CDSMaturity(settle, "3 Years"), 0.0418, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("5 Years", Dt.CDSMaturity(settle, "5 Years"), 0.0515, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("10 Years", Dt.CDSMaturity(settle, "10 Years"), 0.0537, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.Fit();
      return curve;
    }
   

    [Test, Smoke]
    [Ignore("update the test case later")]
    public void TestFloatingRateNote()
    {
      Dt maturity = new Dt(28,6,2017);
      Dt issue = new Dt(28,6,2007);
      Dt asOf = new Dt(11, 1, 2008);
      Dt settlement = new Dt(16, 1, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double tol = 0.00001;

      //setup Bond
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
      b.Index = "USDLIBOR";
      b.Coupon = 0.005;
      //setup pricer
      DiscountCurve ircurve = CreateFRNDiscountCurve(asOf);
      SurvivalCurve curve = CreateFRNSurvivalCurve(asOf, ircurve, 0.4);

      BondPricer pricer = new BondPricer(b, asOf, settlement);

      pricer.Notional = 1000000.0;
      pricer.ReferenceCurve = ircurve;
      pricer.DiscountCurve = ircurve;
      
     
      pricer.CurrentRate = 0.04;
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.MarketQuote = 1.048087;
      Assert.AreEqual(0.0276500966676577, pricer.YieldToMaturity(), tol, "Yield to maturity does not match");
      
    }
    #endregion 

    #endregion Lehman Tests

    #region Special Cases Tests

    /// <summary>
    /// This test will catch: if provided survival curve is none, the bond full price should match 
    /// full model price, we can get a CDS level and 0 CDS basis, we can imply a CDS curve that 
    /// match the full model price to market price. 
    /// </summary>
    [Test, Smoke]
    public void TestNullSurvivalCurve()
    {
      Dt asOfDate = new Dt(30, 9, 2008);
      #region Create discount curve
      Currency ccy = Currency.USD;
      string category = "NONE";
      string[] mmTenors = new string[]
      {
        "1 Days", "1 Weeks", "2 Weeks", "1 Months", "2 Months", "3 Months", 
        "4 Months", "5 Months", "6 Months", "9 Months", "1 Years",
      };
      double[] mmRates = new double[]
      {
        0.00250, 0.00250, 0.00250, 0.03030, 0.03050, 0.03060, 0.03150, 0.03210, 0.03250, 0.03240, 0.03230
      };
      Dt[] mmDates = new Dt[]
      {
        new Dt( 1, 10, 2008), new Dt(7, 10, 2008), new Dt(14, 10, 2008), new Dt(30, 10, 2008), new Dt(30, 11, 2008),
        new Dt(30, 12, 2008), new Dt(30, 1, 2009), new Dt(28,  2, 2009), new Dt(30,  3, 2009), new Dt(30,  6, 2009), 
        new Dt(30, 9, 2009)
      };

      string[] edNames = new string[] {"DEC08", "MAR09", "JUN09" };
      double[] edPrices = new double[] { 0.96825, 0.9728, 0.9741};
      double[] volatilities = new double[] { 0.25, 0.25, 0.25 };
      FuturesCAMethod convexityAdj = FuturesCAMethod.Hull;
      string[] swapTenors = new string[]
      {
        "2 Years", "3 Years", "4 Years", "5 Years", "6 Years", "7 Years", "8 Years", "9 Years", "10 Years"
      };
      double[] swapRates = new double[]
      {
        0.0287, 0.0318, 0.0340, 0.0355, 0.0370, 0.0383, 0.0393, 0.0401, 0.0409
      };
      Dt[] swapDates = new Dt[]
      {
        new Dt(30, 9, 2010), new Dt(30, 9, 2011), new Dt(30, 9, 2012), new Dt(30, 9, 2013), new Dt(30, 9, 2014), 
        new Dt(30, 9, 2015), new Dt(30, 9, 2016), new Dt(30, 9, 2017), new Dt(30, 9, 2018)
      };

      InterpMethod interpMethod = InterpMethod.Weighted;
      ExtrapMethod extrapMethod = ExtrapMethod.Const;
      InterpMethod swapInterp = InterpMethod.Cubic;
      ExtrapMethod swapExtrap = ExtrapMethod.Const;

      DiscountBootstrapCalibrator calibrator =
        new DiscountBootstrapCalibrator(asOfDate, asOfDate);
      calibrator.SwapInterp = InterpFactory.FromMethod(swapInterp, swapExtrap);
      calibrator.FuturesCAMethod = convexityAdj;

      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod);
      curve.Ccy = ccy;
      curve.Category = category;
      curve.Name = "USDLIBOR";

      for (int i = 0; i < mmTenors.Length; i++)
        if (mmRates[i] > 0.0)
          curve.AddMoneyMarket(mmTenors[i], mmDates[i], mmRates[i], DayCount.Actual360);


      Dt[] edMat = new Dt[edNames.Length];
      for (int i = 0; i < edNames.Length; i++)
        edMat[i] = String.IsNullOrEmpty(edNames[i]) ? Dt.Empty : Dt.ImmDate(asOfDate, edNames[i]);
      for (int i = 0; i < edNames.Length; i++)
        if (edPrices[i] > 0.0)
          curve.AddEDFuture(edNames[i], edMat[i], DayCount.Actual360, edPrices[i]);

      Dt[] swapMat = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMat[i] = String.IsNullOrEmpty(swapTenors[i]) ? Dt.Empty : Dt.Add(asOfDate, swapTenors[i]);
        for (int i = 0; i < swapTenors.Length; i++)
          if (swapRates[i] > 0.0)
            curve.AddSwap(swapTenors[i], swapMat[i], swapRates[i], DayCount.Actual360,
                           Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      calibrator.VolatilityCurve = new VolatilityCurve(asOfDate, volatilities[0]);
      curve.Fit();
      #endregion create dicount curve

      #region create the bond
      BondType bondType = BondType.USCorp;
      Dt effectiveDate = new Dt(15, 7, 2001);
      Dt bondMaturity = new Dt(15, 1, 2013);
      DayCount bondPriceDayCount = DayCount.Thirty360;
      Frequency bondFreq = Frequency.SemiAnnual;
      Calendar bondCal = Calendar.NYB;
      BDConvention bondRoll = BDConvention.Following;
      double bondCoupon = 0.095;

      Bond p = new Bond(effectiveDate, bondMaturity, Currency.USD, bondType,
        bondCoupon, bondPriceDayCount, CycleRule.None, bondFreq, bondRoll, bondCal);
            
      CouponPeriodUtil.ToSchedule(new Dt[]{}, new double[]{}, p.CouponSchedule);

      // Add amortizations
      AmortizationUtil.ToSchedule(new Dt[]{}, new double[]{}, p.AmortizationSchedule);

      p.Description = "Bond";
      p.Validate();
      #endregion create the bond

      #region create bond pricer
      Dt settleDate = new Dt(3, 10, 2008);
      double recovery = 0.4;
      double notional = 10000000;
      //double currentCoupon = 0.095;
      double meanReversion = 0; 
      double sigma = 0;
      double marketQuote = 100;
      QuotingConvention quoteType = QuotingConvention.FlatPrice;
      bool useZapreadInSensitivity = true;
      bool forceSurvivalCalibration = false;
      bool ignoreCall = false;
      BondPricer pricer = new BondPricer(p, asOfDate, settleDate, curve, null, 0, TimeUnit.None,
        recovery, meanReversion, sigma, ignoreCall);
      pricer.Notional = notional;
      pricer.QuotingConvention = quoteType;
      pricer.MarketQuote = marketQuote / 100;

      // Imply a survival curve if one is not specified and we have enough information
      if (recovery >= 0.0 && marketQuote > 0.0)
      {
        // Initialize flat hazard rate curve at h = 0.0 @ R = recovery rate
        double h = 0.0;
        SurvivalCurve flatHcurve = new SurvivalCurve(asOfDate, h);
        flatHcurve.Calibrator = new SurvivalFitCalibrator(asOfDate, settleDate, recovery, curve);
        //flatHcurve.Fit();
        pricer.SurvivalCurve = flatHcurve;
        // find flat curve to match market quote
        pricer.SurvivalCurve = pricer.ImpliedFlatCDSCurve(recovery);

        // Force survival curve to be calibrated
        if (forceSurvivalCalibration && pricer.SurvivalCurve == null)
          throw new Exception("A survival curve could not be calibrated for this bond against the market price!");

        // Setup curve name
        if (pricer.SurvivalCurve != null)
          pricer.SurvivalCurve.Name = pricer.Product.Description + "_Curve";
      }
      pricer.EnableZSpreadAdjustment = useZapreadInSensitivity;
      pricer.Validate();
      #endregion

      double fullPrice = pricer.FullPrice();
      double fullModelPrice = pricer.FullModelPrice();
      Assert.AreEqual(fullPrice, fullModelPrice, 1e-5, "FullPrice failed to be same as FullModelPrice");

      double CDSLevel = pricer.ImpliedCDSLevel();
      Assert.AreNotEqual(0, CDSLevel, "CDSLevel = 0, wrong");

      double CDSBasis = pricer.ImpliedCDSSpread();
      Assert.AreEqual(0, CDSBasis, 1e-5, "CDSBasis != 0, wrong");

      SurvivalCurve impliedCDSCurve = pricer.ImpliedCDSCurve();
      pricer.SurvivalCurve = impliedCDSCurve;
      fullPrice = pricer.FullPrice();
      fullModelPrice = pricer.FullModelPrice();
      Assert.AreEqual(fullPrice, fullModelPrice, 1e-5, "FullPrice failed to be same as FullModelPrice");
    }

    #region 1Aug08 Tests
    private DiscountCurve Create1Aug08DiscountCurve(Dt asof)
    {
      //setup curves
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asof, asof);
      ircal.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      ircurve.Ccy = Currency.USD;
      ircurve.Category = "None";

      //DiscountCurve ircurve = new DiscountCurve(ircal);

      ircurve.AddMoneyMarket("6 Months", Dt.Add(asof, "6 Months"), 0.0546, DayCount.Actual360);
      ircurve.AddMoneyMarket("1 Years", Dt.Add(asof, "1 Years"), 0.0512, DayCount.Actual360);
      ircurve.AddSwap("2 Years", Dt.Add(asof, "2 Years"), 0.0473, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("3 Years", Dt.Add(asof, "3 Years"), 0.0473, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("5 Years", Dt.Add(asof, "5 Years"), 0.0486, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("7 Years", Dt.Add(asof, "7 Years"), 0.0498, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.AddSwap("10 Years", Dt.Add(asof, "10 Years"), 0.0514, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.Fit();
      return ircurve;

    }

    private BondPricer Create1Aug08BondPricer(Dt asOf, Dt settle, Dt issue, Dt maturity, double coupon,
      DayCount dayCount, Calendar cal, BDConvention roll, Frequency freq, Currency ccy, BondType type,
      QuotingConvention quotingConvention, double marketQuote, double flatCDSSpread, double recovery)
    {
      CycleRule cycleRule = issue.IsLastDayOfMonth() ? CycleRule.EOM : CycleRule.None;
      //setup Bond
      Bond b = new Bond(
        issue,
        maturity,
        ccy,
        type,
        coupon,
        dayCount,
        cycleRule,
        freq,
        roll,
        cal);
      b.PeriodAdjustment = false;

      //setup other bond props
      b.Notional = 1.0;

      //setup pricer
      DiscountCurve ircurve = Create1Aug08DiscountCurve(asOf);
      SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, flatCDSSpread, recovery);
      BondPricer pricer = new BondPricer(b, asOf, settle, ircurve, curve, 0, TimeUnit.None, -1);
      pricer.QuotingConvention = quotingConvention;
      pricer.MarketQuote = marketQuote;

      return pricer;
    }

    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle = maturity and yield quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleEqualsMaturity_YieldQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(4, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 0.09493;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.Yield, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(100.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(9.493, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }

    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle = maturity and ZSpread quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleEqualsMaturity_ZSpreadQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(4, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 0.09493;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.ZSpread, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(100.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(0.0, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }

    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle = maturity and AssetSwap spread quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleEqualsMaturity_AssetSwapSpreadQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(4, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 0.09493;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.ASW_Par, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(100.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(0.0, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }


    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle = maturity and flat price quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleEqualsMaturity_FlatPriceQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(4, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 1.0;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.FlatPrice, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(100.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(0.0, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duartion is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }


    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle > maturity and yield quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleGreaterThanMaturity_YieldQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(6, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 0.09493;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.Yield, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(0.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(9.493, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }

    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle > maturity and ZSpread quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleGreaterThanMaturity_ZSpreadQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(6, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 0.09493;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.ZSpread, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(0.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(0.0, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }

    /// <summary>
    /// Tests fixed rate bond calculations for special case in which settle > maturity and AssetSwap spread quote. 
    /// </summary>
    [Test, Smoke]
    public void SettleGreaterThanMaturity_AssetSwapSpreadQuote_Aug01()
    {
      Dt maturity = new Dt(4, 8, 2008);
      Dt issue = new Dt(15, 7, 2001);
      Dt asOf = new Dt(1, 8, 2008);
      Dt settlement = new Dt(6, 8, 2008);
      Dt fwdSettlement = new Dt(31, 8, 2008);
      double coupon = 0.095;
      DayCount dayCount = DayCount.Thirty360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      double marketQuote = 0.09493;
      // CDS quotes
      double flatCDSSpread = 0.0275;
      double recovery = 0.4;

      double tol = 0.00001;

      BondPricer pricer = Create1Aug08BondPricer(asOf, settlement, issue, maturity, coupon,
        dayCount, cal, roll, freq, ccy, type, QuotingConvention.ASW_Par, marketQuote,
        flatCDSSpread, recovery);

      //test against known values 

      // Market Pricing
      Assert.AreEqual(0.0, pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(0.0, pricer.FullPrice() * 100, tol, "The full price is incorrect.");
      Assert.AreEqual(0.0, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.0, pricer.PV01() * 100, tol, "The pv01 is incorrect.");
      Assert.AreEqual(0.0, pricer.Duration(), tol, "The macauley duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Convexity() / 100, tol, "The convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.ModDuration(), tol, "The convexity is incorrect.");

      // Model Pricing
      Assert.AreEqual(0.0, pricer.Irr(), tol, "The irr is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.FullModelPrice() * 100, tol, "The model full price is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
        tol, "The asset-swap-spread is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSSpread(), tol, "The cds basis is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.ImpliedCDSLevel(), tol, "The cds level is incorrect.");
      Assert.AreEqual(0.0, 10000.0 * pricer.Vega() / pricer.Notional * 10000.0, tol, "Vega is incorrect.");

      // Model Risk
      Assert.AreEqual(0.0, pricer.Spread01() / pricer.Notional * 100.0, tol, "The spread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadConvexity(), tol, "The spread convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.SpreadDuration(), tol, "The spread duration is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpread01() / pricer.Notional * 100.0, tol, "ZSpread01 is incorrect.");
      Assert.AreEqual(0.0, pricer.ZSpreadDuration(), tol, "ZSpread Duration is incorrect.");
      Assert.AreEqual(0.0, pricer.Rate01() / pricer.Notional * 100.0, tol, "IR01 is incorrect.");
      Assert.AreEqual(0.0, pricer.RateConvexity(), tol, "IR Convexity is incorrect.");
      Assert.AreEqual(0.0, pricer.RateDuration(), tol, "IR Duration is incorrect.");
    }

    #endregion

    #endregion

    #region StepUp and Amortization Tests

    #region 11Jun08 Tests

    private DiscountCurve Create11Jun08DiscountCurve(Dt asof)
    {
      //setup curves
      DiscountBootstrapCalibrator ircal = new DiscountBootstrapCalibrator(asof, asof);
      ircal.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      ircurve.Ccy = Currency.USD;
      ircurve.Category = "None";

      //DiscountCurve ircurve = new DiscountCurve(ircal);
      ircurve.AddSwap("10 Years", Dt.Add(asof, "10 Years"), 0.0280, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      ircurve.Fit();
      return ircurve;
    }

    private void TestBondCashflowTable(BondPricer pricer, int[] periodStartDate, int[] periodEndDate,
      int[] paymentDate, double[] currentCoupon, double[] accrualDays, double[] periodFractions, double[] loss,
      double[] interest, double[] principals, double[] survivalProbs, double[] discountFactors, double[] totalPayments,
      double[] discountedCashflows)
    {

      int[] arrayLength = new int[13];
      arrayLength[0] = periodStartDate.Length;
      arrayLength[1] = periodEndDate.Length;
      arrayLength[2] = paymentDate.Length;
      arrayLength[3] = currentCoupon.Length;
      arrayLength[4] = accrualDays.Length;
      arrayLength[5] = periodFractions.Length;
      arrayLength[6] = loss.Length;
      arrayLength[7] = interest.Length;
      arrayLength[8] = principals.Length;
      arrayLength[9] = survivalProbs.Length;
      arrayLength[10] = discountFactors.Length;
      arrayLength[11] = totalPayments.Length;
      arrayLength[12] = discountedCashflows.Length;

      Cashflow cf = pricer.GenerateCashflow(null, pricer.Settle);
      DiscountCurve dc = pricer.DiscountCurve;
      SurvivalCurve sc = pricer.SurvivalCurve;
      Bond bond = pricer.Bond;
      double notional = pricer.Notional;

      int j = 0; int length = cf.Count;
      while (j < arrayLength.Length)
      {
        if (arrayLength[j] != length)
        {
          throw new ArgumentException("Dimensions of input arrays do not match");
        }
        j++;
      }

      IList<CouponPeriod> bondCouponSchedule = new List<CouponPeriod>();
      foreach (CouponPeriod cp in bond.CouponSchedule)
        bondCouponSchedule.Add(cp);

      // Add current coupon to the coupon schedule
      // Clone bond coupon schedule 
      Dt nextCouponDate = ScheduleUtil.NextCouponDate(pricer.AsOf, bond.Effective, bond.FirstCoupon, bond.Freq, bond.EomRule);
      CouponPeriod currentCpn = new CouponPeriod(nextCouponDate, bond.Coupon);
      bondCouponSchedule.Insert(0, currentCpn);
      bool accrueOnCycle = !bond.PeriodAdjustment;

      int count = paymentDate.Length;
      int numPeriods = cf.Count;
      Assert.AreEqual(count, numPeriods, String.Format("Expected {0} payments, got {1}", count, numPeriods));
      double tolerance = 0.00001;
      for (int i = 0; i < count; i++)
      {
        Dt pStart = cf.GetStartDt(i);
        Dt pEnd = cf.GetEndDt(i);
        Dt payDate = cf.GetDt(i);
        double periodFraction = cf.GetPeriodFraction(i);
        double discountFactor = dc.DiscountFactor(payDate);
        double coupon = cf.GetCoupon(i);

        Assert.AreEqual(periodStartDate[i],
                         pStart.ToInt(),
                         String.Format("{0}: Expected accrual start date {1}, got {2}", i, periodStartDate[i], pStart));

        Assert.AreEqual(periodEndDate[i],
                         pEnd.ToInt(),
                         String.Format("{0}: Expected accrual end date {1}, got {2}", i, periodEndDate[i], pEnd));

        Assert.AreEqual(paymentDate[i],
                         payDate.ToInt(), //psched.GetPaymentDate(i).ToInt(),
                         String.Format("{0}: Expected payment date {1}, got {2}", i, paymentDate[i], payDate));

        Assert.AreEqual(currentCoupon[i],
                         coupon,
                         tolerance,
                         String.Format("{0}: Expected current coupon {1}, got {2}", i, currentCoupon[i], coupon));

        Assert.AreEqual(periodFractions[i],
                         cf.GetPeriodFraction(i),
                         tolerance,
                         String.Format("{0}: Expected period fraction {1}, got {2}", i, periodFractions[i], periodFraction));

        Assert.AreEqual(loss[i],
                         notional * cf.GetDefaultAmount(i),
                         tolerance,
                         String.Format("{0}: Expected default payment {1}, got {2}", i, loss[i], cf.GetDefaultAmount(i)));

        Assert.AreEqual(interest[i],
                         notional * cf.GetAccrued(i),
                         tolerance,
                         String.Format("{0}: Expected interest {1}, got {2}", i, interest[i], notional * cf.GetAccrued(i)));

        Assert.AreEqual(principals[i],
                         notional * cf.GetAmount(i),
                         tolerance,
                         String.Format("{0}: Expected principal {1}, got {2}", i, principals[i], pricer.Notional * cf.GetAmount(i)));

        Assert.AreEqual(totalPayments[i],
                         notional * (cf.GetAmount(i) + cf.GetAccrued(i)),
                         tolerance,
                         String.Format("{0}: Expected total payment {1}, got {2}", i, totalPayments[i], pricer.Notional * (cf.GetAmount(i) + cf.GetAccrued(i))));

        Assert.AreEqual(discountFactors[i],
                         discountFactor,
                         tolerance,
                         String.Format("{0}: Expected discount factor {1}, got {2}", i, discountFactors[i], dc.DiscountFactor(payDate)));

        Assert.AreEqual(survivalProbs[i],
                        (sc == null ? 1.0 : sc.SurvivalProb(payDate)),
                         tolerance,
                         String.Format("{0}: Expected survival prob {1}, got {2}", i, survivalProbs[i], (sc == null ? 1.0 : sc.SurvivalProb(payDate))));

        Assert.AreEqual(discountedCashflows[i],
                         discountFactor * notional * (cf.GetAmount(i) + cf.GetAccrued(i)),
                         tolerance,
                         String.Format("{0}: Expected discounted (total) cashflow {1}, got {2}", i, discountedCashflows[i], dc.DiscountFactor(payDate) * pricer.Notional * (cf.GetAmount(i) + cf.GetAccrued(i))));
      }
    }

    /// <summary>
    ///   Return the contingent cashflows for a Bond
    /// </summary>
    ///
    /// <param name="pricer">Pricer for Bond</param>
    /// <param name="from">Date to get cashflows on or after. Default is settlement date</param>
    ///
    /// <returns>Contingent cashflows of the Bond on or after the settlement date or specified
    /// from date.</returns>
    ///
    protected DataTable
    BondCashflows(
      BondPricer pricer,
      Dt from
      )
    {
      if (from.IsEmpty())
        from = pricer.Settle;
      Dt settle = pricer.Settle;
      Cashflow cf = pricer.GenerateCashflow(null, from); // need cashflows to extract loss and principal amts
      DiscountCurve dc = pricer.DiscountCurve;
      SurvivalCurve sc = pricer.SurvivalCurve;

      //- Create the payments schedule. (this replicates schedule used in CashflowFactory.cpp)
      Schedule psched;
      Bond bond = pricer.Bond;
      if (bond.Effective == bond.Maturity)
      {
        // If effective and maturity are the same day, we construct a schedule
        // with a single period such that:
        //   periodStart = periodEnd = paymentDate = effective
        //
        psched = new Schedule(pricer.AsOf, bond.Effective, bond.Effective, bond.Maturity,
          new Dt[1] { bond.Effective }, new Dt[1] { bond.Effective });
      }
      else
      {
        psched = new Schedule(from, bond.Effective, bond.FirstCoupon, bond.LastCoupon,
                        bond.Maturity, bond.Freq, bond.BDConvention, bond.Calendar,
                        bond.PeriodAdjustment, false, bond.EomRule);
      }

      IList<CouponPeriod> bondCouponSchedule = new List<CouponPeriod>();
      foreach (CouponPeriod cp in bond.CouponSchedule)
        bondCouponSchedule.Add(cp);

      // Add current coupon to the coupon schedule
      // Clone bond coupon schedule
      Dt nextCouponDate = ScheduleUtil.NextCouponDate(pricer.AsOf, bond.Effective, bond.FirstCoupon, bond.Freq, bond.EomRule);
      CouponPeriod currentCpn = new CouponPeriod(nextCouponDate, bond.Coupon);
      bondCouponSchedule.Insert(0, currentCpn);
      bool accrueOnCycle = !bond.PeriodAdjustment;

      DataTable dataTable = new DataTable("Cashflow table");
      dataTable.Columns.Add(new DataColumn("Period Start", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Period End", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Payment Date", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Current Coupon", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Accrual Days", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Loss", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Period Fraction", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Interest", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Principal", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Total Payment", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Discount Factor", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Survival Prob", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Discounted Cashflows", typeof(double)));

      for (int i = 0; i <= psched.Count - 1; i++)
      {
        DataRow row = dataTable.NewRow();
        double accrual = cf.GetAccrued(i);
        Dt pStart = psched.GetPeriodStart(i);
        Dt pEnd = psched.GetPeriodEnd(i);

        row["Period Start"] = pStart;
        row["Period End"] = pEnd;
        row["Payment Date"] = psched.GetPaymentDate(i);
        row["Current Coupon"] = CouponPeriodUtil.CouponAt(bondCouponSchedule, bond.Coupon, pEnd);
        row["Accrual Days"] = Dt.Diff(pStart, pEnd);
        row["Period Fraction"] = psched.Fraction(i, bond.DayCount, accrueOnCycle);
        row["Loss"] = cf.GetDefaultAmount(i);
        row["Interest"] = pricer.Notional * accrual;
        row["Principal"] = pricer.Notional * cf.GetAmount(i);
        row["Total Payment"] = pricer.Notional * (cf.GetAmount(i) + accrual);
        row["Discount Factor"] = dc.DiscountFactor(psched.GetPaymentDate(i));
        if (sc == null)
          row["Survival Prob"] = 1.0;
        else
          row["Survival Prob"] = sc.SurvivalProb(psched.GetPaymentDate(i));
        row["Discounted Cashflows"] = dc.DiscountFactor(psched.GetPaymentDate(i)) * pricer.Notional * (cf.GetAmount(i) + accrual);

        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    #region StepUp Fixed Coupon Bond Tests 
    [Test, Smoke]
    public void TestStepupFixedCashflows()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
#if Support_Changing_Flags
      // set discounting accrued to true to tie out to Excel
      bool origDiscountingAccruedFlag = BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued;
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = true;
#endif
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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      Dt[] stepUpDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] stepUpCoupons = { 0.05, 0.06, 0.07 };

      // Add step ups
      if (stepUpDates != null && stepUpDates.Length > 0)
        for (int j = 0; j < stepUpDates.Length; j++)
          if (stepUpCoupons[j] > 0.0)
            b.CouponSchedule.Add(new CouponPeriod(stepUpDates[j], stepUpCoupons[j]));


      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      Timer timer = new Timer();
      timer.Start();

      pricer.Notional = 1000000.0;
      Schedule psched = new Schedule(pricer.Settle, b.Effective, b.FirstCoupon, b.LastCoupon,
                        b.Maturity, b.Freq, b.BDConvention, b.Calendar,
                        b.PeriodAdjustment, false, b.EomRule);
      int rows = psched.Count;
      // Test against hardcoded values
      int[] periodStartDate = new int[rows];
      int[] periodEndDate = new int[rows];
      int[] paymentDate = new int[rows];
      double[] currentCoupon = new double[rows];
      double[] accrualDays = new double[rows];
      double[] periodFractions = new double[rows];
      double[] loss = new double[rows];
      double[] interest = new double[rows];
      double[] principals = new double[rows];
      double[] totalPayments = new double[rows];
      double[] discountFactors = new double[rows];
      double[] survivalProbs = new double[rows];
      double[] discountedCashflows = new double[rows];

      periodStartDate = new int[] { 20080328, 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328 };

      periodEndDate = new int[] { 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628  };

      paymentDate = new int[] { 20080630, 20080929, 20081229,
                          20090330, 20090629, 20090928, 20091228,
                          20100329, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130930, 20131230,
                          20140328, 20140630, 20140929, 20141229,
                          20150330, 20150629, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628 };

      currentCoupon = new double[] { 0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.05, 0.05, 0.05,
                          0.05, 0.05, 0.05, 0.05,
                          0.06, 0.06, 0.06, 0.06,
                          0.06, 0.06, 0.06, 0.06,
                          0.06, 0.07, 0.07, 0.07,
                          0.07 };

      accrualDays = new double[] {92, 92, 91, 90, 92, 92, 91, 90, 92,
                          92, 91, 90, 92, 92, 91, 91, 92, 92, 91, 90, 
                          92, 92, 91, 90, 92, 92, 91, 90, 92, 92, 91, 
                          91, 92, 92, 91, 90, 92};

      periodFractions = new double[] {0.2555556 , 0.2555556 , 0.2527778 , 0.2500000, 
                         0.2555556, 0.2555556, 0.2527778, 0.2500000, 0.2555556, 
                         0.2555556 , 0.2527778 , 0.2500000 , 0.2555556 , 0.2555556, 
                         0.2527778 , 0.2527778 , 0.2555556 , 0.2555556 ,0.2527778, 
                         0.2500000, 0.2555556, 0.2555556, 0.2527778, 0.2500000, 
                         0.2555556, 0.2555556 , 0.2527778 , 0.2500000 , 0.2555556,
                         0.2555556, 0.2527778, 0.2527778 , 0.2555556 , 0.2555556, 
                         0.2527778, 0.2500000, 0.2555556};

      loss = new double[] { 0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0 };

      interest = new double[] { 
      10222.222222, 10222.222222, 10111.111111, 10000.0,
      10222.222222, 10222.222222, 10111.111111, 10000.0,
      10222.222222, 10222.222222, 10111.111111, 10000.0,
      10222.222222, 10222.222222, 10111.111111, 10111.111111,
      10222.222222, 12777.777778, 12638.888889, 12500.0,
      12777.777778, 12777.777778, 12638.888889, 12500.0,
      15333.333333, 15333.333333, 15166.666667, 15000.0,
      15333.333333, 15333.333333, 15166.666667, 15166.666667,
      15333.333333, 17888.888889, 17694.444444, 17500.0,
      17888.88889 };

      principals = new double[] { 0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0,  
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0, 
                            1000000.0 };

      totalPayments = new double[] { 
      10222.222222, 10222.222222, 10111.111111, 10000.0,
      10222.222222, 10222.222222, 10111.111111, 10000.0,
      10222.222222, 10222.222222, 10111.111111, 10000.0,
      10222.222222, 10222.222222, 10111.111111, 10111.111111,
      10222.222222, 12777.777778, 12638.888889, 12500.0,
      12777.777778, 12777.777778, 12638.888889, 12500.0,
      15333.333333, 15333.333333, 15166.666667, 15000.0,
      15333.333333, 15333.333333, 15166.666667, 15166.666667,
      15333.333333, 17888.888889, 17694.444444, 17500.0,
      1017888.88889 };

      discountFactors = new double[] { 0.998558, 0.991678, 0.984842, 0.978039,
                              0.971283, 0.964574, 0.957911, 0.951294,
                              0.944723, 0.938125, 0.931645, 0.925280,
                              0.918818, 0.912402, 0.906099, 0.899840,
                              0.893556, 0.887316, 0.881187, 0.875167,
                              0.869055, 0.862855, 0.856895, 0.851170,
                              0.845097, 0.839259, 0.833462, 0.827705,
                              0.821987, 0.816309, 0.810670, 0.805070,
                              0.799448, 0.793866, 0.788382, 0.782996,
                              0.777528};

      survivalProbs = new double[] {  1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0 };

      discountedCashflows = new double[] { 
                  10207.477385, 10137.151709, 9957.847421, 9780.390645,
                  9928.671375, 9860.087146, 9685.542146, 9512.938068,
                  9657.163944, 9589.724707, 9419.965711, 9252.799194,
                  9392.365635, 9326.775575, 9161.671350, 9098.385323,
                  9134.132273, 11337.931929, 11137.225860, 10939.584881,
                  11104.594289, 11025.367379, 10830.194383, 10639.622740,
                  12958.154881, 12868.643905, 12640.840903, 12415.571046,
                  12603.803808, 12516.740581, 12295.167034, 12210.235776,
                  12258.208978, 14201.373394, 13949.978180, 13702.422156,
                  791436.734842 };

      TestBondCashflowTable(pricer, periodStartDate, periodEndDate, paymentDate,
        currentCoupon, accrualDays, periodFractions, loss, interest, principals,
        survivalProbs, discountFactors, totalPayments, discountedCashflows);

      timer.Stop();
#if Support_Changing_Flags
      // restore flag
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = origDiscountingAccruedFlag;
      // return rd;
#endif
    }

    /// <summary>
    /// Test case for a step coupon bond (the market quote is a yield ) and we see if the price looks ok
    /// </summary>
    [Test]
    public void TestStepUpFixedYieldFromPrice()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double tol = 0.00001;
      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
#if Support_Changing_Flags
      // set discounting accrued to true to tie out to Excel
      bool origDiscountingAccruedFlag = BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued;
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = true;
#endif
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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      Dt[] stepUpDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] stepUpCoupons = { 0.05, 0.06, 0.07 };

      // Add step ups
      if (stepUpDates != null && stepUpDates.Length > 0)
        for (int j = 0; j < stepUpDates.Length; j++)
          if (stepUpCoupons[j] > 0.0)
            b.CouponSchedule.Add(new CouponPeriod(stepUpDates[j], stepUpCoupons[j]));


      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      // SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 1.177299;

      
      //test against known values 
      Assert.AreEqual(0.8888889, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(0.027560898, pricer.YieldToMaturity(), TOLERANCE, "Yield To Maturity is incorrect");
      Assert.AreEqual(7.70588151595881, pricer.Duration(), TOLERANCE, "Duration is incorrect");
      Assert.AreEqual(7.65314959697953, pricer.ModDuration(), TOLERANCE, "Modified Duration is incorrect");
      Assert.AreEqual(66.9173330739134, pricer.Convexity(), CONVEXITY_TOLERANCE, "Convexity is incorrect");
      
    }

    /// <summary>
    /// Test case for Stepup fixed coupon bond given a market quote as yield
    /// </summary>
    [Test]
    public void TestStepUpFixedPriceFromYield()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double tol = 0.00001;
      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
#if Support_Changing_Flags
      // set discounting accrued to true to tie out to Excel
      bool origDiscountingAccruedFlag = BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued;
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = true;
#endif
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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      Dt[] stepUpDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] stepUpCoupons = { 0.05, 0.06, 0.07 };

      // Add step ups
      if (stepUpDates != null && stepUpDates.Length > 0)
        for (int j = 0; j < stepUpDates.Length; j++)
          if (stepUpCoupons[j] > 0.0)
            b.CouponSchedule.Add(new CouponPeriod(stepUpDates[j], stepUpCoupons[j]));


      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      // SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      pricer.QuotingConvention = QuotingConvention.Yield;
      pricer.MarketQuote = 0.027560898;


      //test against known values 
      Assert.AreEqual(0.8888889, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(1.177299, pricer.FlatPrice(), TOLERANCE, "The Flat Price is not correct");
      Assert.AreEqual(7.70588151595881, pricer.Duration(), TOLERANCE, "Duration is incorrect");
      Assert.AreEqual(7.65314959697953, pricer.ModDuration(), TOLERANCE, "Modified Duration is incorrect");
      Assert.AreEqual(66.9173330739134, pricer.Convexity(), CONVEXITY_TOLERANCE, "Convexity is incorrect");
    }
    #endregion 

    #region Tests for Bonds which both Amortize and have step coupons
    #endregion

    /// <summary>
    /// Tests bond calculations against manually calculated values for "StepUpFixed" bond. 
    /// </summary>
    /// <remarks>
    /// Model vs Manual cashflows comparison are stored in spreadsheet "Bond Step Up Test Fixed", Sheet:8.1 Jun24 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void StepUpFixedNonCallable()
    {
      /*
      //
      // Test expected cashflow structure
      //
      Timer timer = new Timer();
      timer.Start();

      pricer.Notional = 1000000.0;
      DataTable dataTable = BondCashflows(pricer, pricer.Settle);
      
      timer.Stop();
      ResultData rd = ToResultData(dataTable, timer.Elapsed);   
       */

      
    }

    #region Fixed Amortizing Bonds 

    [Test, Smoke]
    public void TestAmortizingFixedNonCallableCashflows()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      Dt[] amortizingDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] amortizingAmounts = { 0.1, 0.2, 0.3 };

      // Add step ups
      if (amortizingDates != null && amortizingDates.Length > 0)
        for (int j = 0; j < amortizingDates.Length; j++)
          if (amortizingAmounts[j] > 0.0)
            b.AmortizationSchedule.Add(new Amortization(amortizingDates[j], amortizingAmounts[j]));

      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      // SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);

      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      //Yield from Price Tests 
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.MarketQuote = 1.09435928;
      //test against 

      Timer timer = new Timer();
      timer.Start();

      /*
      pricer.Notional = 1000000.0;
      DataTable dataTable = BondCashflows(pricer, pricer.Settle);

      timer.Stop();
      ResultData rd = ToResultData(dataTable, timer.Elapsed);
      */

      pricer.Notional = 1000000.0;
      Schedule psched = new Schedule(pricer.Settle, b.Effective, b.FirstCoupon, b.LastCoupon,
                        b.Maturity, b.Freq, b.BDConvention, b.Calendar,
                        b.PeriodAdjustment, false, b.EomRule);
      int rows = psched.Count;
      // Test against hardcoded values
      int[] periodStartDate = new int[rows];
      int[] periodEndDate = new int[rows];
      int[] paymentDate = new int[rows];
      double[] currentCoupon = new double[rows];
      double[] accrualDays = new double[rows];
      double[] periodFractions = new double[rows];
      double[] loss = new double[rows];
      double[] interest = new double[rows];
      double[] principals = new double[rows];
      double[] totalPayments = new double[rows];
      double[] discountFactors = new double[rows];
      double[] survivalProbs = new double[rows];
      double[] discountedCashflows = new double[rows];

      periodStartDate = new int[] { 20080328, 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328 };

      periodEndDate = new int[] { 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628  };

      paymentDate = new int[] { 20080630, 20080929, 20081229,
                          20090330, 20090629, 20090928, 20091228,
                          20100329, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130930, 20131230,
                          20140328, 20140630, 20140929, 20141229,
                          20150330, 20150629, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628 };

      currentCoupon = new double[] { 0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04, 0.04, 0.04, 0.04,
                          0.04 };

      accrualDays = new double[] {92, 92, 91, 90, 92, 92, 91, 90, 92,
                          92, 91, 90, 92, 92, 91, 91, 92, 92, 91, 90, 
                          92, 92, 91, 90, 92, 92, 91, 90, 92, 92, 91, 
                          91, 92, 92, 91, 90, 92};

      periodFractions = new double[] {0.2555556 , 0.2555556 , 0.2527778 , 0.2500000, 
                         0.2555556, 0.2555556, 0.2527778, 0.2500000, 0.2555556, 
                         0.2555556 , 0.2527778 , 0.2500000 , 0.2555556 , 0.2555556, 
                         0.2527778 , 0.2527778 , 0.2555556 , 0.2555556 ,0.2527778, 
                         0.2500000, 0.2555556, 0.2555556, 0.2527778, 0.2500000, 
                         0.2555556, 0.2555556 , 0.2527778 , 0.2500000 , 0.2555556,
                         0.2555556, 0.2527778, 0.2527778 , 0.2555556 , 0.2555556, 
                         0.2527778, 0.2500000, 0.2555556};

      loss = new double[] { 0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0 };

      interest = new double[] { 
            10222.222222, 10222.222222, 10111.111111, 10000.0,
            10222.222222, 10222.222222, 10111.111111, 10000.0,
            10222.222222, 10222.222222, 10111.111111, 10000.0,
            10222.222222, 10222.222222, 10111.111111, 10111.111111,
            10222.222222, 9200.0, 9100.0, 9000.0, 9200.0, 
            9200.0, 9100.0, 9000.0, 7155.555556 , 7155.555556 ,
            7077.777778 ,7000.0 ,7155.555556, 7155.555556 ,
            7077.777778, 7077.777778, 7155.555556, 4088.888889,
            4044.444444, 4000.0, 4088.888889 };


      principals = new double[] { 0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            100000.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 200000.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            300000.0, 0.0, 0.0, 0.0, 
                            400000.0};

      totalPayments = new double[] { 
            10222.222222, 10222.222222, 10111.111111, 10000.0,
            10222.222222, 10222.222222, 10111.111111, 10000.0,
            10222.222222, 10222.222222, 10111.111111, 10000.0,
            10222.222222, 10222.222222, 10111.111111, 10111.111111,
            110222.222222, 9200.0, 9100.0, 9000.0, 
            9200.0, 9200.0, 9100.0, 209000.0, 
            7155.555556 , 7155.555556 , 7077.777778 ,7000.0 ,
            7155.555556, 7155.555556 , 7077.777778, 7077.777778,
            307155.555556, 4088.888889, 4044.444444, 4000.0, 
            404088.888889 };


      discountFactors = new double[] { 0.998558, 0.991678, 0.984842, 0.978039,
                              0.971283, 0.964574, 0.957911, 0.951294,
                              0.944723, 0.938125, 0.931645, 0.925280,
                              0.918818, 0.912402, 0.906099, 0.899840,
                              0.893556, 0.887316, 0.881187, 0.875167,
                              0.869055, 0.862855, 0.856895, 0.851170,
                              0.845097, 0.839259, 0.833462, 0.827705,
                              0.821987, 0.816309, 0.810670, 0.805070,
                              0.799448, 0.793866, 0.788382, 0.782996,
                              0.777528};

      survivalProbs = new double[] {  1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0 };

      discountedCashflows = new double[] { 
                  10207.477385, 10137.151709, 9957.847421, 9780.390645,
                  9928.671375, 9860.087146, 9685.542146, 9512.938068,
                  9657.163944, 9589.724707, 9419.965711, 9252.799194,
                  9392.365635, 9326.775575, 9161.671350, 9098.385323,
                  98489.774070, 8163.310989, 8018.802619, 7876.501114,
                  7995.307888, 7938.264513, 7797.739956, 177894.492207,
                  6047.138944, 6005.367156, 5899.059088, 5793.933155,
                  5881.775110, 5841.145604, 5737.744616, 5698.110029,
                  245555.021013, 3246.028204, 3188.566441, 3131.982207,
                  314190.275873 };

      TestBondCashflowTable(pricer, periodStartDate, periodEndDate, paymentDate,
        currentCoupon, accrualDays, periodFractions, loss, interest, principals,
        survivalProbs, discountFactors, totalPayments, discountedCashflows);

      timer.Stop();


    }
    /// <summary>
    /// Tests bond calculations against manually calculated values for "StepUpFixed" bond. 
    /// </summary>
    /// <remarks>
    /// Model vs Manual cashflows comparison are stored in spreadsheet "Bond Step Up Test Fixed", Sheet:8.1 Jun24 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void AmortizingFixedNonCallableYieldFromPrice()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
#if Support_Changing_Flags
  // set discounting accrued to true to tie out to Excel
      bool origDiscountingAccruedFlag = BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued;
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = true;
#endif
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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      Dt[] amortizingDates = {new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016)};
      double[] amortizingAmounts = {0.1, 0.2, 0.3};

      // Add step ups
      if (amortizingDates != null && amortizingDates.Length > 0)
        for (int j = 0; j < amortizingDates.Length; j++)
          if (amortizingAmounts[j] > 0.0)
            b.AmortizationSchedule.Add(new Amortization(amortizingDates[j], amortizingAmounts[j]));

      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      // SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);

      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);

      //Yield from Price Tests 
      pricer.QuotingConvention = QuotingConvention.FullPrice;
      pricer.MarketQuote = 1.09435928;
      //test against known values 
      Assert.AreEqual(0.000433589571477169,
                      pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly)*10000, TOLERANCE,
                      "Asset Swap Spread is not correct");
      Assert.AreEqual(0.8888889, 100*pricer.AccruedInterest(), TOLERANCE,
                      "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), TOLERANCE, "The accrual days are incorrect.");
      Assert.AreEqual(0.02756978280, pricer.YieldToMaturity(), TOLERANCE, "The ytm is incorrect.");
      Assert.AreEqual(6.64036258802629, pricer.Duration(), TOLERANCE, "Duration is incorrect ");
      Assert.AreEqual(6.59490754586986, pricer.ModDuration(), TOLERANCE, "Mod Duration is incorrect");
      Assert.AreEqual(7.21719569992896, pricer.PV01()*10000, TOLERANCE, "Pv01 is incorrect");
      Assert.AreEqual(51.60918438, pricer.Convexity(), CONVEXITY_TOLERANCE, "COnvexity is incorrect");


#if Support_Changing_Flags
  // restore flag
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = origDiscountingAccruedFlag;
      //return rd;
#endif
    }

    [Test]
    public void AmortizingFixedPriceFromYield()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      Dt[] amortizingDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] amortizingAmounts = { 0.1, 0.2, 0.3 };

      // Add step ups
      if (amortizingDates != null && amortizingDates.Length > 0)
        for (int j = 0; j < amortizingDates.Length; j++)
          if (amortizingAmounts[j] > 0.0)
            b.AmortizationSchedule.Add(new Amortization(amortizingDates[j], amortizingAmounts[j]));

      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      // SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);

      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      //Yield from Price Tests 
      pricer.QuotingConvention = QuotingConvention.Yield;
      pricer.MarketQuote = 0.0275684955716653;
      pricer.Notional = 1.0;
      
      Assert.AreEqual(1.094371, pricer.FullPrice(), TOLERANCE, "The round trip full price is not correct");
      Assert.AreEqual(-0.017018720,
                      pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly) * 10000, TOLERANCE,
                      "Asset Swap Spread is not correct");
      //test against known values 
      Assert.AreEqual(0.8888889, 100 * pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), TOLERANCE, "The accrual days are incorrect.");
      Assert.AreEqual(6.64077031794453, pricer.Duration(),TOLERANCE, "Duration is incorrect ");
      Assert.AreEqual(6.59531459, pricer.ModDuration(),TOLERANCE, "Mod Duration is incorrect");
      Assert.AreEqual(7.217721026083, pricer.PV01() * 10000, TOLERANCE, "Pv01 is incorrect");
      Assert.AreEqual(51.470723244079, pricer.Convexity(), CONVEXITY_TOLERANCE, "COnvexity is incorrect");

    }


    [Test]
    public void AmortizingFixedNonCallableQuotedAsASW()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.04;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;

      Dt[] amortizingDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] amortizingAmounts = { 0.1, 0.2, 0.3 };

      // Add step ups
      if (amortizingDates != null && amortizingDates.Length > 0)
        for (int j = 0; j < amortizingDates.Length; j++)
          if (amortizingAmounts[j] > 0.0)
            b.AmortizationSchedule.Add(new Amortization(amortizingDates[j], amortizingAmounts[j]));

      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      // SurvivalCurve curve = CreateFlatSpreadCurve(asOf, ircurve, 0.033510, 0.4);

      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      //Yield from Price Tests 
      pricer.QuotingConvention = QuotingConvention.ASW_Par;
      pricer.MarketQuote = 0.000433589571477169/10000;
      pricer.Notional = 1.0;
      Assert.AreEqual(1.09435928, pricer.FullPrice(), TOLERANCE, "The round trip full price is not correct");
      Assert.AreEqual(0.0275700872566062, pricer.YieldToMaturity(), TOLERANCE,
                      "The Yield to Maturity is not correct");
      Assert.AreEqual(0.000433589571477169,
                      pricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly) * 10000, TOLERANCE,
                      "Asset Swap Spread is not correct");
      //test against known values 
      Assert.AreEqual(0.8888889, 100 * pricer.AccruedInterest(), TOLERANCE, "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), TOLERANCE, "The accrual days are incorrect.");
      Assert.AreEqual(6.64036258802629, pricer.Duration(), TOLERANCE, "Duration is incorrect ");
      Assert.AreEqual(6.59490754586986, pricer.ModDuration(), TOLERANCE, "Mod Duration is incorrect");
      Assert.AreEqual(7.21719569992896, pricer.PV01() * 10000, TOLERANCE, "Pv01 is incorrect");
      Assert.AreEqual(51.60918438, pricer.Convexity(), CONVEXITY_TOLERANCE, "COnvexity is incorrect");
    }

#region set of tests for amortizing bonds with a historical amort
    
    internal static RateQuoteCurve CreateRepoCurveForForwardSettleBond(Dt asOf, Currency ccy)
    {
      var dates = new List<Dt>();
      var rates = new List<double>();
      dates.Add(Dt.Add(asOf, "1 Months"));
      rates.Add(0.0075);
      dates.Add(Dt.Add(asOf, "3 Months"));
      rates.Add(0.01);
      dates.Add(Dt.Add(asOf, "6 Months"));
      rates.Add(0.0125);
      dates.Add(Dt.Add(asOf, "1Y"));
      rates.Add(0.013);
      dates.Add(Dt.Add(asOf, "2Y"));
      rates.Add(0.0135);
      dates.Add(Dt.Add(asOf, "5Y"));
      rates.Add(0.015);
      dates.Add(Dt.Add(asOf, "10Y"));
      rates.Add(0.025);
      return RepoUtil.GenerateRepoCurve(asOf, ccy, dates.ToArray(), rates.ToArray(), new Dt[]{}, new double[]{},
                                        DayCount.Actual360, InterpMethod.Linear, ExtrapMethod.Smooth, null, null);
    }

    /// <summary>
    /// Helper function that creates a discount curve for an Amortizing bond 
    /// </summary>
    /// <param name="asOf"></param>
    /// <returns></returns>
    internal static DiscountCurve CreateDiscountCurveForAmortBond(Dt asOf)
    {
      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf, asOf);
      calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);
      calibrator.SwapCalibrationMethod = SwapCalibrationMethod.Extrap;
      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.DayCount = DayCount.Actual365Fixed;
      curve.Frequency = Frequency.Continuous;
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      curve.Ccy = Currency.USD;
      curve.AddMoneyMarket("1D", Dt.Add(asOf, "1D"), 0.00261, DayCount.Actual360);
      curve.AddMoneyMarket("1W", Dt.Add(asOf, "1W"), 0.00289, DayCount.Actual360);
      curve.AddMoneyMarket("2W", Dt.Add(asOf, "2W"), 0.00299, DayCount.Actual360);
      curve.AddMoneyMarket("1M", Dt.Add(asOf, "1M"), 0.00318, DayCount.Actual360);
      curve.AddMoneyMarket("2M", Dt.Add(asOf, "2M"), 0.00475, DayCount.Actual360);
      curve.AddMoneyMarket("3M", Dt.Add(asOf, "3M"), 0.00629, DayCount.Actual360);
      curve.AddMoneyMarket("4M", Dt.Add(asOf, "4M"), 0.00875, DayCount.Actual360);
      curve.AddMoneyMarket("5M", Dt.Add(asOf, "5M"), 0.01049, DayCount.Actual360);
      curve.AddMoneyMarket("6M", Dt.Add(asOf, "6M"), 0.01180, DayCount.Actual360);
      curve.AddMoneyMarket("9M", Dt.Add(asOf, "9M"), 0.01368, DayCount.Actual360);
      curve.AddMoneyMarket("1Y", Dt.Add(asOf, "1Y"), 0.01548, DayCount.Actual360);

      curve.AddSwap("2 Years", Dt.Add(asOf, "2 Years"), 0.01384, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("3 Years", Dt.Add(asOf, "3 Years"), 0.02016, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("4 Years", Dt.Add(asOf, "4 Years"), 0.02569, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("5 Years", Dt.Add(asOf, "5 Years"), 0.02985, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("6 Years", Dt.Add(asOf, "6 Years"), 0.03290, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("7 Years", Dt.Add(asOf, "7 Years"), 0.03524, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("8 Years", Dt.Add(asOf, "8 Years"), 0.03697, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("9 Years", Dt.Add(asOf, "9 Years"), 0.03833, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("10 Years", Dt.Add(asOf, "10 Years"), 0.03950, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("11 Years", Dt.Add(asOf, "11 Years"), 0.04047, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("12 Years", Dt.Add(asOf, "12 Years"), 0.04119, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("15 Years", Dt.Add(asOf, "15 Years"), 0.04257, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("20 Years", Dt.Add(asOf, "20 Years"), 0.04298, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("25 Years", Dt.Add(asOf, "25 Years"), 0.04313, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.AddSwap("30 Years", Dt.Add(asOf, "30 Years"), 0.04332, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      curve.Fit();
      return curve;
    
    }

    internal static SurvivalCurve CreateSurvivalCurveForAmortBond(Dt asof, DiscountCurve ircurve, double recovery)
    {
      Dt settle = ToolkitConfigurator.Settings.SurvivalCalibrator.UseNaturalSettlement ? Dt.Add(asof, 1) : asof;
      SurvivalFitCalibrator survcal = new SurvivalFitCalibrator(asof, settle, recovery, ircurve);
      SurvivalCurve curve = new SurvivalCurve(survcal);
      curve.AddCDS("6 Months", Dt.CDSMaturity(settle, "6 Months"), 0.0228, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("1 Years", Dt.CDSMaturity(settle, "1 Years"), 0.0228, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("2 Years", Dt.CDSMaturity(settle, "2 Years"), 0.0301, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("3 Years", Dt.CDSMaturity(settle, "3 Years"), 0.0370, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("5 Years", Dt.CDSMaturity(settle, "5 Years"), 0.0418, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("7 Years", Dt.CDSMaturity(settle, "7 Years"), 0.0515, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      curve.AddCDS("10 Years", Dt.CDSMaturity(settle, "10 Years"), 0.0537, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);

      curve.Fit();
      return curve;
    }
    
    /// <summary>
    /// Test case for an Fixed rate Amortizing bond which has got a historical amortization 
    /// </summary>
    [Test]
    public void TestAmortFixed_HistoricalAmort()
    {
      
      //general bond properties 
      Dt maturity = new Dt(15,07,2015);
      Dt issue = new Dt(15,07,2008);
      Dt asOf = new Dt(10,11,2009);
      Dt settlement = new Dt(10,11,2009);
      double coupon = 0.072430556;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup Bond
      Bond nonAmortBond = new Bond(
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
      nonAmortBond.Notional = 1.0;

      Bond amortBond = new Bond(
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
      amortBond.Notional = 1.0;
      Dt[] amortDates = { new Dt(15,11,2008)};
      double[] amortAmounts = { 0.5 };
      AmortizationUtil.ToSchedule(amortDates, amortAmounts, amortBond.AmortizationSchedule);

      //setup pricer
      DiscountCurve ircurve = CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = CreateSurvivalCurveForAmortBond(asOf, ircurve, 0.4);
      
      //Create the Bond Pricers for the amort bond and the non amort bond 
      BondPricer amortbondpricer = new BondPricer(amortBond,asOf,settlement,ircurve,sc,0,TimeUnit.None,-0.4) {Notional = 100};
      BondPricer nonAmortBondPricer = new BondPricer(nonAmortBond, asOf, settlement, ircurve, sc, 0, TimeUnit.None, -0.4) { Notional = 50};

      QuotingConvention[] conventions = new QuotingConvention[]{QuotingConvention.FlatPrice,
                                                                QuotingConvention.FullPrice,
                                                                QuotingConvention.Yield,
                                                                QuotingConvention.ASW_Par,
                                                                QuotingConvention.ZSpread};
      nonAmortBondPricer.QuotingConvention = QuotingConvention.FlatPrice;
      nonAmortBondPricer.MarketQuote = 1.0;
      nonAmortBondPricer.EnableZSpreadAdjustment = false;


      amortbondpricer.QuotingConvention = QuotingConvention.FlatPrice;
      amortbondpricer.MarketQuote = 1.0;
      amortbondpricer.EnableZSpreadAdjustment = false;

      Dt fwdSettle = new Dt(10, 12, 2009);
      
      
      //Get the Initial Set of calcs to tie out against
      double flatPrice = nonAmortBondPricer.FlatPrice();
      double fullPrice = nonAmortBondPricer.FullPrice();
      double yield = nonAmortBondPricer.YieldToMaturity();
      double pv01 = nonAmortBondPricer.PV01();
      double macDuration = nonAmortBondPricer.Duration();
      double modDuration = nonAmortBondPricer.ModDuration();
      double irr = nonAmortBondPricer.Irr();
      double modelFullprice = nonAmortBondPricer.FullModelPrice();
      double accruedInterest = nonAmortBondPricer.AccruedInterest();
      double zSpread = nonAmortBondPricer.ImpliedZSpread();
      double cdsBasis = nonAmortBondPricer.ImpliedCDSSpread();
      double cdsLevel = nonAmortBondPricer.ImpliedCDSLevel();
      double asw_par = nonAmortBondPricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly);
      double fwdAccrued = nonAmortBondPricer.AccruedInterest(fwdSettle,fwdSettle);
      double fwdFullPrice = nonAmortBondPricer.FwdFullPrice(fwdSettle);
      double spread01 = nonAmortBondPricer.Spread01();
      double spreadDuration = nonAmortBondPricer.SpreadDuration();
      double spreadConvexity = nonAmortBondPricer.SpreadConvexity();
      double zspread01 = nonAmortBondPricer.ZSpread01();
      double zspreadDuration = nonAmortBondPricer.ZSpreadDuration();
      double ir01 = nonAmortBondPricer.Rate01();
      double irDuration = nonAmortBondPricer.RateDuration();
      double irCOnvexity = nonAmortBondPricer.RateConvexity();
      double amortYield = amortbondpricer.YieldToMaturity();

      
      var dictionary = new Dictionary<QuotingConvention, double>();
      dictionary.Add(QuotingConvention.FlatPrice, flatPrice);
      dictionary.Add(QuotingConvention.FullPrice, fullPrice);
      dictionary.Add(QuotingConvention.Yield, yield);
      dictionary.Add(QuotingConvention.ASW_Par, asw_par);
      dictionary.Add(QuotingConvention.ZSpread, zSpread);

      foreach(QuotingConvention qc in conventions)
      {
        double  marketQuote;
        dictionary.TryGetValue(qc, out marketQuote);
        amortbondpricer.QuotingConvention = qc;
        amortbondpricer.MarketQuote = (qc==QuotingConvention.Yield)?amortYield:marketQuote;
        
        nonAmortBondPricer.QuotingConvention = qc;
        nonAmortBondPricer.MarketQuote = marketQuote;

        //First test if the Actual values are all in synch 
        Assert.AreEqual(amortbondpricer.FullPrice(), nonAmortBondPricer.FullPrice(),
                        "Full price does not match");
        Assert.AreEqual(amortbondpricer.FlatPrice(), nonAmortBondPricer.FlatPrice(),
                        "Flat price does nto match");
        Assert.AreEqual(amortbondpricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),
                        nonAmortBondPricer.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly),"Asset Swap Spread does not match");
        Assert.AreEqual(amortbondpricer.ImpliedZSpread(), nonAmortBondPricer.ImpliedZSpread(),
                        "Z Spread does not match");
        Assert.AreEqual(amortbondpricer.YieldToMaturity(), nonAmortBondPricer.YieldToMaturity(),
                        amortbondpricer.YieldToMaturity()*0.05, "yield does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.Irr(), nonAmortBondPricer.Irr(), "Irr does not match");
        Assert.AreEqual(amortbondpricer.AccruedInterest(), nonAmortBondPricer.AccruedInterest(),
                        "AI does not match");

        Assert.AreEqual(amortbondpricer.ModDuration(), nonAmortBondPricer.ModDuration(),
                        amortbondpricer.ModDuration()*0.05, "Mod Duration does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.Duration(), nonAmortBondPricer.Duration(),
                        amortbondpricer.Duration()*0.05, "Macaulay duration does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.PV01()/amortbondpricer.EffectiveNotional, nonAmortBondPricer.PV01()/nonAmortBondPricer.EffectiveNotional, amortbondpricer.PV01()*0.05,
                        "Pv01 does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.Convexity(), nonAmortBondPricer.Convexity(),
                        amortbondpricer.Convexity()*0.05, "Convexity des not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.FullModelPrice(), nonAmortBondPricer.FullModelPrice(),
                        "Full MOdel price does not match");
        Assert.AreEqual(amortbondpricer.ImpliedCDSSpread(), nonAmortBondPricer.ImpliedCDSSpread(),
                        "Implied Csd Spread does not match");
        Assert.AreEqual(amortbondpricer.ImpliedCDSLevel(), nonAmortBondPricer.ImpliedCDSLevel(),
                        "CDS Level does not match");
        Assert.AreEqual(amortbondpricer.AccruedInterest(fwdSettle,fwdSettle),
                        nonAmortBondPricer.AccruedInterest(fwdSettle,fwdSettle), "Fwd Accrued does not match");
        Assert.AreEqual(amortbondpricer.FwdFullPrice(fwdSettle),
                        nonAmortBondPricer.FwdFullPrice(fwdSettle), "Fwd full price does not match");

        Assert.AreEqual(amortbondpricer.Spread01(), nonAmortBondPricer.Spread01(), "Spread01 does not match");
        Assert.AreEqual(amortbondpricer.SpreadDuration(), nonAmortBondPricer.SpreadDuration(),
                        "Spread Duration does not match");
        Assert.AreEqual(amortbondpricer.SpreadConvexity(), nonAmortBondPricer.SpreadConvexity(),
                        "Spread Convexity does not match");
        
        Assert.AreEqual(amortbondpricer.ZSpread01(), nonAmortBondPricer.ZSpread01(),
                        "ZSpread01 does not match");
        Assert.AreEqual(amortbondpricer.ZSpreadDuration(), nonAmortBondPricer.ZSpreadDuration(),
                        "z spread duration does not match");
        Assert.AreEqual(amortbondpricer.ZSpreadScenario(0.1), nonAmortBondPricer.ZSpreadScenario(0.1),
                        "z spread 10% scenario does not match");

        Assert.AreEqual(amortbondpricer.Rate01(), nonAmortBondPricer.Rate01(), "IR01 does not match");
        Assert.AreEqual(amortbondpricer.RateDuration(), nonAmortBondPricer.RateDuration(),
                        "Rate Duration does not match");
        Assert.AreEqual(amortbondpricer.RateConvexity(), nonAmortBondPricer.RateConvexity(),
                        "Rate Convexity does not match");


        
      }

    }

    [Test]
    public void TestAmortFixedHistoricalAmortManualDuration()
    {
      //general bond properties 
      Dt maturity = new Dt(15, 07, 2015);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(10, 11, 2009);
      Dt settlement = new Dt(10, 11, 2009);
      double coupon = 0.072430556;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;
      Bond amortBond = new Bond(
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
      amortBond.Notional = 1.0;
      Dt[] amortDates = { new Dt(15, 11, 2008) };
      double[] amortAmounts = { 0.5 };
      AmortizationUtil.ToSchedule(amortDates, amortAmounts, amortBond.AmortizationSchedule);

      //setup pricer
      DiscountCurve ircurve = CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = CreateSurvivalCurveForAmortBond(asOf, ircurve, 0.4);

      //Create the Bond Pricers for the amort bond and the non amort bond 
      BondPricer amortbondpricer = new BondPricer(amortBond, asOf, settlement, ircurve, sc, 0, TimeUnit.None, -0.4);
      amortbondpricer.QuotingConvention = QuotingConvention.Yield;
      amortbondpricer.MarketQuote = 0.072370556;
      double p = amortbondpricer.FullPrice();
      double duration = amortbondpricer.ModDuration();

      amortbondpricer.MarketQuote = 0.072360556;
      double pu = amortbondpricer.FullPrice();
      double dpdy = (pu - p)/(0.00001);
      double man_duration = dpdy/p;
      Assert.AreEqual(man_duration, duration, 0.05*man_duration,
                      "Duration does not match manual values ");


    }

    [Test]
    public void TestAmortFloat_HistoricalAmort()
    {
      //general bond properties 
      Dt maturity = new Dt(15, 07, 2015);
      Dt issue = new Dt(15, 07, 2008);
      Dt asOf = new Dt(10, 11, 2009);
      Dt settlement = new Dt(10, 11, 2009);
      double coupon = 0.0118;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.SemiAnnual;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      //setup Bond
      Bond nonAmortBond = new Bond(
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
      nonAmortBond.Notional = 1.0;
      nonAmortBond.Index = "USDLIBOR";
      nonAmortBond.Coupon = 0.0001;
      nonAmortBond.ReferenceIndex = new InterestRateIndex(nonAmortBond.Index, nonAmortBond.Freq, nonAmortBond.Ccy, nonAmortBond.DayCount, nonAmortBond.Calendar, 0);

      Bond amortBond = new Bond(
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
    
      amortBond.Notional = 1.0;
      Dt[] amortDates = { new Dt(15, 11, 2008) };
      double[] amortAmounts = { 0.5 };
      AmortizationUtil.ToSchedule(amortDates, amortAmounts, amortBond.AmortizationSchedule);

      amortBond.Index = "USDLIBOR";
      amortBond.Coupon = 0.0001;
      amortBond.ReferenceIndex = new InterestRateIndex(amortBond.Index, amortBond.Freq, amortBond.Ccy, amortBond.DayCount, amortBond.Calendar, 0);

      //setup pricer
      DiscountCurve ircurve = CreateDiscountCurveForAmortBond(asOf);
      SurvivalCurve sc = CreateSurvivalCurveForAmortBond(asOf, ircurve, 0.4);

    

      //Create the Bond Pricers for the amort bond and the non amort bond 
      BondPricer amortbondpricer = new BondPricer(amortBond, asOf, settlement, ircurve, sc, 0, TimeUnit.None, -0.4) {Notional = 100};
      BondPricer nonAmortBondPricer = new BondPricer(nonAmortBond, asOf, settlement, ircurve, sc, 0, TimeUnit.None, -0.4) {Notional = 50};

      amortbondpricer.CurrentRate = 0.0118;
      amortbondpricer.ReferenceCurve = ircurve;
      nonAmortBondPricer.CurrentRate = 0.0118;
      nonAmortBondPricer.ReferenceCurve = ircurve;

      QuotingConvention[] conventions = new QuotingConvention[]{QuotingConvention.FlatPrice,
                                                                QuotingConvention.FullPrice,
                                                                QuotingConvention.Yield,
                                                                QuotingConvention.DiscountMargin,
                                                                QuotingConvention.ZSpread};
      nonAmortBondPricer.QuotingConvention = QuotingConvention.FlatPrice;
      nonAmortBondPricer.MarketQuote = 1.0;
      nonAmortBondPricer.EnableZSpreadAdjustment = false;


      amortbondpricer.QuotingConvention = QuotingConvention.FlatPrice;
      amortbondpricer.MarketQuote = 1.0;
      amortbondpricer.EnableZSpreadAdjustment = false;

      Dt fwdSettle = new Dt(10, 12, 2009);
     

      //Get the Initial Set of calcs to tie out against
      double flatPrice = nonAmortBondPricer.FlatPrice();
      double fullPrice = nonAmortBondPricer.FullPrice();
      double yield = nonAmortBondPricer.YieldToMaturity();
      double pv01 = nonAmortBondPricer.PV01();
      double macDuration = nonAmortBondPricer.Duration();
      double modDuration = nonAmortBondPricer.ModDuration();
      double irr = nonAmortBondPricer.Irr();
      double modelFullprice = nonAmortBondPricer.FullModelPrice();
      double accruedInterest = nonAmortBondPricer.AccruedInterest();
      double zSpread = nonAmortBondPricer.ImpliedZSpread();
      double discountMargin = nonAmortBondPricer.DiscountMargin();
      double cdsBasis = nonAmortBondPricer.ImpliedCDSSpread();
      double cdsLevel = nonAmortBondPricer.ImpliedCDSLevel();
      double fwdAccrued = nonAmortBondPricer.AccruedInterest(fwdSettle,fwdSettle);
      double fwdFullPrice = nonAmortBondPricer.FwdFullPrice(fwdSettle);
      double spread01 = nonAmortBondPricer.Spread01();
      double spreadDuration = nonAmortBondPricer.SpreadDuration();
      double spreadConvexity = nonAmortBondPricer.SpreadConvexity();
      double zspread01 = nonAmortBondPricer.ZSpread01();
      double zspreadDuration = nonAmortBondPricer.ZSpreadDuration();
      double ir01 = nonAmortBondPricer.Rate01();
      double irDuration = nonAmortBondPricer.RateDuration();
      double irCOnvexity = nonAmortBondPricer.RateConvexity();
      double amortYield = amortbondpricer.YieldToMaturity();


      var dictionary = new Dictionary<QuotingConvention, double>();
      dictionary.Add(QuotingConvention.FlatPrice, flatPrice);
      dictionary.Add(QuotingConvention.FullPrice, fullPrice);
      dictionary.Add(QuotingConvention.Yield, yield);
      dictionary.Add(QuotingConvention.DiscountMargin, discountMargin);
      dictionary.Add(QuotingConvention.ZSpread, zSpread);

      foreach (QuotingConvention qc in conventions)
      {
        double marketQuote;
        dictionary.TryGetValue(qc, out marketQuote);
        amortbondpricer.QuotingConvention = qc;
        amortbondpricer.MarketQuote = (qc == QuotingConvention.Yield) ? amortYield : marketQuote;

        nonAmortBondPricer.QuotingConvention = qc;
        nonAmortBondPricer.MarketQuote = marketQuote;

        //First test if the Actual values are all in synch 
        Assert.AreEqual(amortbondpricer.FullPrice(), nonAmortBondPricer.FullPrice(),
                        "Full price does not match");
        Assert.AreEqual(amortbondpricer.FlatPrice(), nonAmortBondPricer.FlatPrice(),
                        "Flat price does nto match");
        Assert.AreEqual(amortbondpricer.DiscountMargin(),nonAmortBondPricer.DiscountMargin(),
          "The Discount MArgin does not match");
        Assert.AreEqual(amortbondpricer.MarketSpreadDuration(),
                        nonAmortBondPricer.MarketSpreadDuration(), "The Market Spread Duration does not match");

        Assert.AreEqual(amortbondpricer.ImpliedZSpread(), nonAmortBondPricer.ImpliedZSpread(),
                        "Z Spread does not match");
        Assert.AreEqual(amortbondpricer.YieldToMaturity(), nonAmortBondPricer.YieldToMaturity(),
                        amortbondpricer.YieldToMaturity() * 0.05, "yield does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.Irr(), nonAmortBondPricer.Irr(), "Irr does not match");
        Assert.AreEqual(amortbondpricer.AccruedInterest(), nonAmortBondPricer.AccruedInterest(),
                        "AI does not match");

        Assert.AreEqual(amortbondpricer.ModDuration(), nonAmortBondPricer.ModDuration(),
                        TOLERANCE, "Mod Duration does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.Duration(), nonAmortBondPricer.Duration(),
                        TOLERANCE, "Macaulay duration does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.PV01()/amortbondpricer.EffectiveNotional, nonAmortBondPricer.PV01()/nonAmortBondPricer.EffectiveNotional, TOLERANCE,
                        "Pv01 does not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.Convexity(), nonAmortBondPricer.Convexity(),
                        TOLERANCE, "Convexity des not match within the 5% tolerance");
        Assert.AreEqual(amortbondpricer.FullModelPrice(), nonAmortBondPricer.FullModelPrice(),
                        "Full MOdel price does not match");
        Assert.AreEqual(amortbondpricer.ImpliedCDSSpread(), nonAmortBondPricer.ImpliedCDSSpread(),
                        "Implied Csd Spread does not match");
        Assert.AreEqual(amortbondpricer.ImpliedCDSLevel(), nonAmortBondPricer.ImpliedCDSLevel(),
                        "CDS Level does not match");
        Assert.AreEqual(amortbondpricer.AccruedInterest(fwdSettle,fwdSettle),
                        nonAmortBondPricer.AccruedInterest(fwdSettle,fwdSettle), "Fwd Accrued does not match");
        Assert.AreEqual(amortbondpricer.FwdFullPrice(fwdSettle),
                        nonAmortBondPricer.FwdFullPrice(fwdSettle), "Fwd full price does not match");

        Assert.AreEqual(amortbondpricer.Spread01(), nonAmortBondPricer.Spread01(), "Spread01 does not match");
        Assert.AreEqual(amortbondpricer.SpreadDuration(), nonAmortBondPricer.SpreadDuration(),
                        "Spread Duration does not match");
        Assert.AreEqual(amortbondpricer.SpreadConvexity(), nonAmortBondPricer.SpreadConvexity(),
                        "Spread Convexity does not match");
        
        Assert.AreEqual(amortbondpricer.ZSpread01(), nonAmortBondPricer.ZSpread01(),
                        "ZSpread01 does not match");
        Assert.AreEqual(amortbondpricer.ZSpreadDuration(), nonAmortBondPricer.ZSpreadDuration(),
                        "z spread duration does not match");
        Assert.AreEqual(amortbondpricer.ZSpreadScenario(0.1), nonAmortBondPricer.ZSpreadScenario(0.1),
                        "z spread 10% scenario does not match");


        Assert.AreEqual(amortbondpricer.Rate01(), nonAmortBondPricer.Rate01(), "IR01 does not match");
        Assert.AreEqual(amortbondpricer.RateDuration(), nonAmortBondPricer.RateDuration(),
                        "Rate Duration does not match");
        Assert.AreEqual(amortbondpricer.RateConvexity(), nonAmortBondPricer.RateConvexity(),
                        "Rate Convexity does not match");

      }
      
    }

#endregion 
    #endregion 

    /// <summary>
    /// Tests bond calculations against manually calculated values for "StepUpFixed" bond. 
    /// </summary>
    /// <remarks>
    /// Model vs Manual cashflows comparison are stored in spreadsheet "Bond Step Up Test Fixed", Sheet:8.1 Jun24 for this bond.
    /// </remarks>
    [Test, Smoke]
    public void StepUpFloatingNonCallable()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.01; //spread
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double tol = 0.00001;
      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
#if Support_Changing_Flags
      // set discounting accrued to true to tie out to Excel
      bool origDiscountingAccruedFlag = BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued;
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = true;
#endif
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
    
      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      b.Index = "USDLIBOR";
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 0);

      b.Coupon = 0.01;
      Dt[] stepUpDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] stepUpCoupons = { 0.0150, 0.0200, 0.0300 };

      // Add step ups
      if (stepUpDates != null && stepUpDates.Length > 0)
        for (int j = 0; j < stepUpDates.Length; j++)
          if (stepUpCoupons[j] > 0.0)
            b.CouponSchedule.Add(new CouponPeriod(stepUpDates[j], stepUpCoupons[j]));

      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.ReferenceCurve = ircurve;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.8040;
      pricer.CurrentRate = 0.0342125;

      //test against known values 
      Assert.AreEqual(0.7602778, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(6.61164868602409, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.8040, pricer.FlatPrice(),tol, "The flat price is incorrect");
      Assert.AreEqual(113.02365626, pricer.Pv() * 100, pvInaccuracy * tol, "The model pv does not tie out with manual calculation.");

      /*
      //
      // Test expected cashflow structure
      //
      Timer timer = new Timer();
      timer.Start();

      pricer.Notional = 1000000.0;
      DataTable dataTable = BondCashflows(pricer, pricer.Settle);
      
      timer.Stop();
      ResultData rd = ToResultData(dataTable, timer.Elapsed);   
       */

      Timer timer = new Timer();
      timer.Start();

      pricer.Notional = 1000000.0;
      Schedule psched = new Schedule(pricer.Settle, b.Effective, b.FirstCoupon, b.LastCoupon,
                        b.Maturity, b.Freq, b.BDConvention, b.Calendar,
                        b.PeriodAdjustment, false, b.EomRule);
      int rows = psched.Count;
      // Test against hardcoded values
      int[] periodStartDate = new int[rows];
      int[] periodEndDate = new int[rows];
      int[] paymentDate = new int[rows];
      double[] currentCoupon = new double[rows];
      double[] accrualDays = new double[rows];
      double[] periodFractions = new double[rows];
      double[] loss = new double[rows];
      double[] interest = new double[rows];
      double[] principals = new double[rows];
      double[] totalPayments = new double[rows];
      double[] discountFactors = new double[rows];
      double[] survivalProbs = new double[rows];
      double[] discountedCashflows = new double[rows];

      periodStartDate = new int[] { 20080328, 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328 };

      periodEndDate = new int[] { 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628  };

      paymentDate = new int[] { 20080630, 20080929, 20081229,
                          20090330, 20090629, 20090928, 20091228,
                          20100329, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130930, 20131230,
                          20140328, 20140630, 20140929, 20141229,
                          20150330, 20150629, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628 };

      currentCoupon = new double[] {  0.034213, 0.037444, 0.037457, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.042516, 0.042516, 0.042516,
                                      0.042516, 0.042516, 0.042516, 0.042516,
                                      0.047516, 0.047516, 0.047516, 0.047516,
                                      0.047516, 0.047516, 0.047516, 0.047516,
                                      0.047516, 0.057516, 0.057516, 0.057516,
                                      0.057516 };

      accrualDays = new double[] {92, 92, 91, 90, 92, 92, 91, 90, 92,
                          92, 91, 90, 92, 92, 91, 91, 92, 92, 91, 90, 
                          92, 92, 91, 90, 92, 92, 91, 90, 92, 92, 91, 
                          91, 92, 92, 91, 90, 92};

      periodFractions = new double[] {0.2555556 , 0.2555556 , 0.2527778 , 0.2500000, 
                         0.2555556, 0.2555556, 0.2527778, 0.2500000, 0.2555556, 
                         0.2555556 , 0.2527778 , 0.2500000 , 0.2555556 , 0.2555556, 
                         0.2527778 , 0.2527778 , 0.2555556 , 0.2555556 ,0.2527778, 
                         0.2500000, 0.2555556, 0.2555556, 0.2527778, 0.2500000, 
                         0.2555556, 0.2555556 , 0.2527778 , 0.2500000 , 0.2555556,
                         0.2555556, 0.2527778, 0.2527778 , 0.2555556 , 0.2555556, 
                         0.2527778, 0.2500000, 0.2555556};

      loss = new double[] { 0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0 };

      interest = new double[] { 
                        8743.194444, 9568.943898, 9468.518282, 9379.043688,
                        9587.466881, 9587.466881, 9483.255284, 9379.043688,
                        9587.466881, 9587.466881, 9483.255284, 9379.043688,
                        9587.466881, 9587.466881, 9483.255284, 9483.255284,
                        9587.466881, 10865.244659, 10747.144173, 10629.043688,
                        10865.244659, 10865.244659, 10747.144173, 10629.043688,
                        12143.022437, 12143.022437, 12011.033062, 11879.043688,
                        12143.022437, 12143.022437, 12011.033062, 12011.033062,
                        12143.022437, 14698.577992, 14538.810840, 14379.043688,
                        14698.577992 };

      principals = new double[] { 0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0,  
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0, 
                            1000000.0 };

      totalPayments = new double[] { 
                              8743.194444, 9568.943898, 9468.518282, 9379.043688,
                              9587.466881, 9587.466881, 9483.255284, 9379.043688,
                              9587.466881, 9587.466881, 9483.255284, 9379.043688,
                              9587.466881, 9587.466881, 9483.255284, 9483.255284,
                              9587.466881, 10865.244659, 10747.144173, 10629.043688,
                              10865.244659, 10865.244659, 10747.144173, 10629.043688,
                              12143.022437, 12143.022437, 12011.033062, 11879.043688,
                              12143.022437, 12143.022437, 12011.033062, 12011.033062,
                              12143.022437, 14698.577992, 14538.810840, 14379.043688,
                              1014698.577992 };

      discountFactors = new double[] { 0.998558, 0.991678, 0.984842, 0.978039,
                              0.971283, 0.964574, 0.957911, 0.951294,
                              0.944723, 0.938125, 0.931645, 0.925280,
                              0.918818, 0.912402, 0.906099, 0.899840,
                              0.893556, 0.887316, 0.881187, 0.875167,
                              0.869055, 0.862855, 0.856895, 0.851170,
                              0.845097, 0.839259, 0.833462, 0.827705,
                              0.821987, 0.816309, 0.810670, 0.805070,
                              0.799448, 0.793866, 0.788382, 0.782996,
                              0.777528};

      survivalProbs = new double[] {  1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0 };

      discountedCashflows = new double[] { 
                  8730.583001, 9489.310042, 9324.994980, 9173.071114, 
                  9312.144259, 9247.818811, 9084.112293, 8922.226174,
                  9057.496253, 8994.244698, 8835.026994, 8678.240788,
                  8809.140762, 8747.623558, 8592.771585, 8533.415343,
                  8566.942564, 9640.909904, 9470.244819, 9302.186050,
                  9442.497427, 9375.128924, 9209.168740, 9047.121194,
                  10262.032530, 10191.145543, 10010.740090, 9832.340724,
                  9981.409071, 9912.460546, 9736.988423, 9669.728281,
                  9707.719999, 11668.695341, 11462.134040, 11258.727247,
                  788956.179973 };

      TestBondCashflowTable(pricer, periodStartDate, periodEndDate, paymentDate,
        currentCoupon, accrualDays, periodFractions, loss, interest, principals,
        survivalProbs, discountFactors, totalPayments, discountedCashflows);

      timer.Stop();
#if Support_Changing_Flags
      // restore flag
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = origDiscountingAccruedFlag;
      // return rd;
#endif
    }

    /// <summary>
    /// Tests bond calculations against manually calculated values for "AmortizingFloating" bond. 
    /// </summary>
    /// <remarks>
    /// Model vs Manual cashflows comparison are stored in spreadsheet "Bond Step Up Test Floating" for this bond.
    /// </remarks>
    [Test, Smoke]
    public void AmortizingFloatingNonCallable()
    {
      Dt maturity = new Dt(28, 6, 2017);
      Dt issue = new Dt(28, 6, 2007);
      Dt asOf = new Dt(11, 6, 2008);
      Dt settlement = new Dt(16, 6, 2008);
      double coupon = 0.01;
      DayCount dayCount = DayCount.Actual360;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Following;
      Frequency freq = Frequency.Quarterly;
      Currency ccy = Currency.USD;
      BondType type = BondType.USCorp;

      double tol = 0.00001;
      double pvInaccuracy = Settings.CashflowPricer.DiscountingAccrued ? 1 : 500;
#if Support_Changing_Flags
      // set discounting accrued to true to tie out to Excel
      bool origDiscountingAccruedFlag = BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued;
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = true;
#endif
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

      //setup other bond props
      b.Notional = 1.0;
      b.PeriodAdjustment = false;
      b.Index = "USDLIBOR";
      b.Coupon = 0.01;
      b.ReferenceIndex = new InterestRateIndex(b.Index, b.Freq, b.Ccy, b.DayCount, b.Calendar, 0);
    
      Dt[] amortizingDates = { new Dt(28, 6, 2012), new Dt(28, 3, 2014), new Dt(28, 6, 2016) };
      double[] amortizingAmounts = { 0.1, 0.2, 0.3 };

      // Add step ups
      if (amortizingDates != null && amortizingDates.Length > 0)
        for (int j = 0; j < amortizingDates.Length; j++)
          if (amortizingAmounts[j] > 0.0)
            b.AmortizationSchedule.Add(new Amortization(amortizingDates[j], amortizingAmounts[j]));

      //setup pricer
      DiscountCurve ircurve = Create11Jun08DiscountCurve(asOf);
      BondPricer pricer = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      pricer.Notional = 1.0;
      pricer.ReferenceCurve = ircurve;
      pricer.QuotingConvention = QuotingConvention.FlatPrice;
      pricer.MarketQuote = 0.8040;
      pricer.CurrentRate = 0.0342125;

      //test against known values 
      Assert.AreEqual(0.7602778, 100 * pricer.AccruedInterest(), tol, "The accrued interest is incorrect");
      Assert.AreEqual(80, pricer.AccrualDays(), tol, "The accrual days are incorrect.");
      Assert.AreEqual(7.10697901236094, pricer.YieldToMaturity() * 100, tol, "The ytm is incorrect.");
      Assert.AreEqual(0.804, pricer.FlatPrice(), tol, "the flat price is incorrect");
      
      Assert.AreEqual(107.58527543, pricer.Pv() * 100, pvInaccuracy * tol, "The model pv does not tie out with manual calculation.");

      //Test round trip pricing 
      BondPricer pricer2 = new BondPricer(b, asOf, settlement, ircurve, null, 0, TimeUnit.None, -0.4);
      pricer2.Notional = 1.0;
      pricer2.ReferenceCurve = ircurve;
      pricer2.QuotingConvention = QuotingConvention.Yield;
      pricer2.MarketQuote = 0.0710697901236094;
      pricer2.CurrentRate = 0.0342125;

      //Test against known values 
      Assert.AreEqual(0.8040, pricer2.FlatPrice(), tol, "the round trip flat price does not match");
      /*
      //
      // Test expected cashflow structure
      //
      Timer timer = new Timer();
      timer.Start();

      pricer.Notional = 1000000.0;
      DataTable dataTable = BondCashflows(pricer, pricer.Settle);
      
      timer.Stop();
      ResultData rd = ToResultData(dataTable, timer.Elapsed);   
       */

      Timer timer = new Timer();
      timer.Start();

      pricer.Notional = 1000000.0;
      Schedule psched = new Schedule(pricer.Settle, b.Effective, b.FirstCoupon, b.LastCoupon,
                        b.Maturity, b.Freq, b.BDConvention, b.Calendar,
                        b.PeriodAdjustment, false, b.EomRule);
      int rows = psched.Count;
      // Test against hardcoded values
      int[] periodStartDate = new int[rows];
      int[] periodEndDate = new int[rows];
      int[] paymentDate = new int[rows];
      double[] currentCoupon = new double[rows];
      double[] accrualDays = new double[rows];
      double[] periodFractions = new double[rows];
      double[] loss = new double[rows];
      double[] interest = new double[rows];
      double[] principals = new double[rows];
      double[] totalPayments = new double[rows];
      double[] discountFactors = new double[rows];
      double[] survivalProbs = new double[rows];
      double[] discountedCashflows = new double[rows];

      periodStartDate = new int[] { 20080328, 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328 };

      periodEndDate = new int[] { 20080628, 20080928, 20081228,
                          20090328, 20090628, 20090928, 20091228,
                          20100328, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130928, 20131228,
                          20140328, 20140628, 20140928, 20141228,
                          20150328, 20150628, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628  };

      paymentDate = new int[] { 20080630, 20080929, 20081229,
                          20090330, 20090629, 20090928, 20091228,
                          20100329, 20100628, 20100928, 20101228,
                          20110328, 20110628, 20110928, 20111228,
                          20120328, 20120628, 20120928, 20121228,
                          20130328, 20130628, 20130930, 20131230,
                          20140328, 20140630, 20140929, 20141229,
                          20150330, 20150629, 20150928, 20151228,
                          20160328, 20160628, 20160928, 20161228,
                          20170328, 20170628 };

      currentCoupon = new double[] {  0.034213, 0.037444, 0.037457, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516, 0.037516, 0.037516, 0.037516,
                                      0.037516
                                      };



      accrualDays = new double[] {92, 92, 91, 90, 92, 92, 91, 90, 92,
                          92, 91, 90, 92, 92, 91, 91, 92, 92, 91, 90, 
                          92, 92, 91, 90, 92, 92, 91, 90, 92, 92, 91, 
                          91, 92, 92, 91, 90, 92};

      periodFractions = new double[] {0.2555556 , 0.2555556 , 0.2527778 , 0.2500000, 
                         0.2555556, 0.2555556, 0.2527778, 0.2500000, 0.2555556, 
                         0.2555556 , 0.2527778 , 0.2500000 , 0.2555556 , 0.2555556, 
                         0.2527778 , 0.2527778 , 0.2555556 , 0.2555556 ,0.2527778, 
                         0.2500000, 0.2555556, 0.2555556, 0.2527778, 0.2500000, 
                         0.2555556, 0.2555556 , 0.2527778 , 0.2500000 , 0.2555556,
                         0.2555556, 0.2527778, 0.2527778 , 0.2555556 , 0.2555556, 
                         0.2527778, 0.2500000, 0.2555556};

      loss = new double[] { 0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            0.0 };

      interest = new double[] { 
                        8743.194444, 9568.943898, 9468.518282, 9379.043688,
                        9587.466881, 9587.466881, 9483.255284, 9379.043688,
                        9587.466881, 9587.466881, 9483.255284, 9379.043688,
                        9587.466881, 9587.466881, 9483.255284, 9483.255284,
                        9587.466881, 8628.720193, 8534.929756, 8441.139319,
                        8628.720193, 8628.720193, 8534.929756, 8441.139319,
                        6711.226817, 6711.226817, 6638.278699, 6565.330582,
                        6711.226817, 6711.226817, 6638.278699, 6638.278699,
                        6711.226817, 3834.986752, 3793.302114, 3751.617475,
                        3834.986752 };


      principals = new double[] { 0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0,  
                            0.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 0.0, 
                            100000.0, 0.0, 0.0, 0.0, 
                            0.0, 0.0, 0.0, 200000.0,
                            0.0, 0.0, 0.0, 0.0,
                            0.0, 0.0, 0.0, 0.0,
                            300000.0, 0.0, 0.0, 0.0,
                            400000.0 };

      totalPayments = new double[] { 
                              8743.194444, 9568.943898, 9468.518282, 9379.043688,
                              9587.466881, 9587.466881, 9483.255284, 9379.043688,
                              9587.466881, 9587.466881, 9483.255284, 9379.043688,
                              9587.466881, 9587.466881, 9483.255284, 9483.255284,
                              109587.466881, 8628.720193, 8534.929756, 8441.139319,
                              8628.720193, 8628.720193, 8534.929756, 208441.139319,
                              6711.226817, 6711.226817, 6638.278699, 6565.330582,
                              6711.226817, 6711.226817, 6638.278699, 6638.278699,
                              306711.226817, 3834.986752, 3793.302114, 3751.617475,
                              403834.986752 };

      discountFactors = new double[] { 0.998558, 0.991678, 0.984842, 0.978039,
                              0.971283, 0.964574, 0.957911, 0.951294,
                              0.944723, 0.938125, 0.931645, 0.925280,
                              0.918818, 0.912402, 0.906099, 0.899840,
                              0.893556, 0.887316, 0.881187, 0.875167,
                              0.869055, 0.862855, 0.856895, 0.851170,
                              0.845097, 0.839259, 0.833462, 0.827705,
                              0.821987, 0.816309, 0.810670, 0.805070,
                              0.799448, 0.793866, 0.788382, 0.782996,
                              0.777528};

      survivalProbs = new double[] {  1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0, 1.0, 1.0, 1.0,
                                      1.0 };

      discountedCashflows = new double[] { 
                  8730.583001, 9489.310042, 9324.994980, 9173.071114,
                  9312.144259, 9247.818811, 9084.112293, 8922.226174,
                  9057.496253, 8994.244698, 8835.026994, 8678.240788,
                  8809.140762, 8747.623558, 8592.771585, 8533.415343,
                  97922.584361, 7656.405040, 7520.870009, 7387.404806,
                  7498.834198, 7445.332967, 7313.534371, 177418.806863,
                  5671.638035, 5632.460091, 5532.753291, 5434.155218,
                  5516.542572, 5478.435981, 5381.455742, 5344.282290,
                  245199.803108, 3044.464034, 2990.570395, 2937.499795,
                  313992.859947 };

      TestBondCashflowTable(pricer, periodStartDate, periodEndDate, paymentDate,
        currentCoupon, accrualDays, periodFractions, loss, interest, principals,
        survivalProbs, discountFactors, totalPayments, discountedCashflows);

      timer.Stop();
#if Support_Changing_Flags
      // restore flag
      BaseEntity.Toolkit.Models.CashflowModel.DiscountingAccrued = origDiscountingAccruedFlag;
      // return rd;
#endif
    }

    #endregion

    #endregion  
  }
}
