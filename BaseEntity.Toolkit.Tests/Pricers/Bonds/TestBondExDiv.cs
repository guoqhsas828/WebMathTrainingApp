//
// Copyright (c)    2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Numerics;
using System.Collections.Generic;
using BaseEntity.Toolkit.Calibrators;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test calculations on bonds with an ex-dividend period
  /// </summary>
  [TestFixture]
  public class TestBondExDiv : ToolkitTestBase
  {

    public Bond Bond { get; set; }
    private const int ExDivDays = 5;

    ///<summary>
    /// Set up a bond which accrues $1 a day with 10,000 notional
    /// </summary>
    [SetUp]
    public void Setup()
    {
      Bond = new Bond(new Dt(15, 9, 2010),
                      new Dt(15, 9, 2012),
                      Currency.USD,
                      BondType.USCorp,
                      3.6 / 100.0,
                      DayCount.Thirty360,
                      CycleRule.Fifteenth,
                      Frequency.SemiAnnual,
                      BDConvention.Modified,
                      Calendar.NYB)
               {
                 BondExDivRule = new ExDivRule(ExDivDays, true)
               };
    }

    [Test]
    public void ExDivAccrued()
    {
      var i = new Dt(15, 9, 2010);
      var s = new Dt(1, 3, 2011);
      Dt d = s;
      var e = new Dt(15, 3, 2011);
      Dt exdiv = Dt.AddDays(e, -ExDivDays, Calendar.NYB);
      Dt nextCoupon = e;
      var nextNextCoupon = new Dt(15, 9, 2011);
      while (d < e)
      {
        int exdivAccrued = d >= exdiv
                             ? -Dt.Diff(d, e, DayCount.Thirty360)
                             : Dt.Diff(i, d, DayCount.Thirty360);
        int cumdivAccrued = Dt.Diff(i, d, DayCount.Thirty360);


        var discountCurve = CreateDiscountCurve(d);

        // create a pricer for a trade settling on spot date
        var bondPricerSpot = new BondPricer(Bond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
                           {
                             QuotingConvention = QuotingConvention.FlatPrice,
                             MarketQuote = 1.0,
                             Notional = 10000.0
                           };
        Assert.AreEqual(exdivAccrued, bondPricerSpot.Accrued(), 1E-13,
          String.Format("New trade accrual {0}", d));
        Assert.AreEqual(cumdivAccrued / bondPricerSpot.Notional, 1E-13,
          bondPricerSpot.AccruedInterest(d, s),
          String.Format("Cum div trade accrual {0}", d));
        var exDivCashflow = bondPricerSpot.Cashflow;
        var exDivCashflow2 = bondPricerSpot.TradeCashflow;
        Assert.AreEqual((d >= exdiv ? nextNextCoupon : nextCoupon).ToInt(),
          (exDivCashflow.Count > 0 ? exDivCashflow.GetDt(0) : Dt.Empty).ToInt(),
          String.Format("New trade next cashflow {0}", d));
        Assert.AreEqual((d >= exdiv ? nextNextCoupon : nextCoupon).ToInt(),
          (exDivCashflow2.Count > 0 ? exDivCashflow2.GetDt(0) : Dt.Empty).ToInt(),
          String.Format("New trade next trade cashflow {0}", d));
        // create a pricer for a trade settling cum div
        var bondPricerCum = new BondPricer(Bond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
         {
           QuotingConvention = QuotingConvention.FlatPrice,
           MarketQuote = 1.0,
           Notional = 10000.0,
           TradeSettle = s
         };
        Assert.AreEqual(cumdivAccrued, bondPricerCum.Accrued(), 1E-13,
          String.Format("With trade settle set, Cum div trade accrual {0}", d));
        Assert.AreEqual(exdivAccrued / bondPricerSpot.Notional,
          bondPricerCum.AccruedInterest(), 1E-13,
          String.Format("With trade settle set, New trade accrual {0}", d));
        exDivCashflow = bondPricerCum.Cashflow;
        var cumDivCashflow = bondPricerCum.TradeCashflow;
        Assert.AreEqual((d >= exdiv ? nextNextCoupon : nextCoupon).ToInt(),
          (exDivCashflow.Count > 0 ? exDivCashflow.GetDt(0) : Dt.Empty).ToInt(),
          String.Format("With trade settle set, New trade next cashflow {0}", d));
        Assert.AreEqual(nextCoupon.ToInt(),
          (cumDivCashflow.Count > 0 ? cumDivCashflow.GetDt(0) : Dt.Empty).ToInt(),
          String.Format("With trade settle set, Cum div trade next cashflow {0}", d));
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }


    [Test]
    public void ExDivSpotSettleVsCumDiv()
    {
      var i = new Dt(15, 9, 2010);
      var s = new Dt(1, 3, 2011);
      Dt d = s;
      var e = new Dt(15, 3, 2011);
      Dt exdiv = Dt.AddDays(e, -ExDivDays, Calendar.NYB);
      while (d < e)
      {
        int exdivAccrued = d >= exdiv
                             ? -Dt.Diff(d, e, DayCount.Thirty360)
                             : Dt.Diff(i, d, DayCount.Thirty360);
        int cumdivAccrued = Dt.Diff(i, d, DayCount.Thirty360);


        var discountCurve = CreateDiscountCurve(d);

        // create a pricer for a trade settling on spot date
        var bondPricerSpot = new BondPricer(Bond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0
        };
        Assert.AreEqual(exdivAccrued, bondPricerSpot.Accrued(), 1E-13,
          String.Format("New trade accrual {0}", d));
        Assert.AreEqual(cumdivAccrued / bondPricerSpot.Notional,
          bondPricerSpot.AccruedInterest(d, s), 1E-13,
          String.Format("Cum div trade accrual {0}", d));

        // create a pricer for a trade settling cum div
        var bondPricerCum = new BondPricer(Bond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = s
        };

        CompareMarketCalcs(bondPricerSpot, bondPricerCum, exdiv);
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }

    [Test]
    public void ExDivSpotSettleVsCumDiv2()
    {
      var i = new Dt(15, 9, 2010);
      var s = new Dt(1, 3, 2011);
      Dt d = s;
      var e = new Dt(15, 3, 2011);
      Dt exdiv = Dt.AddDays(e, -ExDivDays, Calendar.NYB);
      while (d < e)
      {
        int exdivAccrued = d >= exdiv
                             ? -Dt.Diff(d, e, DayCount.Thirty360)
                             : Dt.Diff(i, d, DayCount.Thirty360);
        int cumdivAccrued = Dt.Diff(i, d, DayCount.Thirty360);


        var discountCurve = CreateDiscountCurve(d);
        var survivalCurve = CreateSurvivalCurveForAmortBond(d, discountCurve, 0.4);
        // create a pricer for a trade settling on spot date
        var bondPricerSpot = new BondPricer(Bond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0
        };
        Assert.AreEqual(exdivAccrued, bondPricerSpot.Accrued(), 1E-13,
          String.Format("New trade accrual {0}", d));
        Assert.AreEqual(cumdivAccrued / bondPricerSpot.Notional,
          bondPricerSpot.AccruedInterest(d, s), 1E-13,
          String.Format("Cum div trade accrual {0}", d));

        // create a pricer for a trade settling cum div
        var bondPricerCum = new BondPricer(Bond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = s
        };

        CompareMarketCalcs(bondPricerSpot, bondPricerCum, exdiv);
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }

    [Test]
    public void CumDivBondRegularVsCustomizedSchedule()
    {
      var i = new Dt(15, 9, 2010);
      var s = new Dt(1, 3, 2011);
      Dt d = s;
      var e = new Dt(15, 3, 2011);
      while (d < e)
      {
        var discountCurve = CreateDiscountCurve(d);
        var survivalCurve = CreateSurvivalCurveForAmortBond(d, discountCurve, 0.4);
        // create a pricer for a trade settling on spot date
        var cumDivBondPricer = new BondPricer(Bond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0,
          true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = s
        };

        var ps = cumDivBondPricer.GetPaymentSchedule(null, s);

        var customBond2 = (Bond)Bond.Clone();
        customBond2.CustomPaymentSchedule = ps;

        var bondPricerCustomSchedule = new BondPricer(customBond2, d, d, discountCurve, survivalCurve, 0, TimeUnit.None,
          0.0, 0.0, 0.0,
          true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = s
        };

        CompareRegularBondCalcVsCustomized(cumDivBondPricer, bondPricerCustomSchedule, 1E-6); 
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }

    [Test]
    public void ExDivBondRegularVsCustomizedSchedule()
    {
      var i = new Dt(15, 9, 2010);
      var s = new Dt(1, 3, 2011);
      Dt d = s;
      var e = new Dt(15, 3, 2011);
      while (d < e)
      {
        var discountCurve = CreateDiscountCurve(d);
        var survivalCurve = CreateSurvivalCurveForAmortBond(d, discountCurve, 0.4);
        // create a pricer for a trade settling on spot date
        var cumDivBondPricer = new BondPricer(Bond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0,
          true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = d
        };

        var ps = cumDivBondPricer.GetPaymentSchedule(null, s);

        var customBond2 = (Bond)Bond.Clone();
        customBond2.CustomPaymentSchedule = ps;

        var bondPricerCustomSchedule = new BondPricer(customBond2, d, d, discountCurve, survivalCurve, 0, TimeUnit.None,
          0.0, 0.0, 0.0,
          true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = d
        };

        CompareRegularBondCalcVsCustomized(cumDivBondPricer, bondPricerCustomSchedule, 1E-6);
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }

    private delegate double Calc(BondPricer p);
    private void CompareMarketCalcs(BondPricer p1, BondPricer p2, Dt exDivDay)
    {
      foreach (var ci in new Dictionary<string, Calc>
                              {
                                {"YieldToMaturity", p => p.YieldToMaturity()},
                                {"ModDuration", p => p.ModDuration()},
                                {"Convexity", p => p.Convexity()},
                                {"Full Price", p=>p.FullPrice()},
                                {"Accrued Interest", p=>p.AccruedInterest()},
                                {"ZSpread", p=>p.ImpliedZSpread()},
                                {"Asset Swap Spread", p=>p.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly)},
                                {"Model Full Price", p=>p.FullModelPrice()},
                                {"Irr", p=>p.Irr()}
                              })
      {
        Assert.AreEqual(ci.Value(p1), ci.Value(p2), String.Format("Compare {0} on {1}, expected to match", ci.Key, p1.AsOf));
      }

      if (p1.SurvivalCurve != null && p2.SurvivalCurve != null)
      {
        foreach (var ci in new Dictionary<string, Calc>
                              {
                                {"CDS Level", p => p.ImpliedCDSLevel()},
                                {"CDS Spread", p => p.ImpliedCDSSpread()}
                              })
        {
          Assert.AreEqual(ci.Value(p1), ci.Value(p2), String.Format("Compare {0} on {1}, expected to match", ci.Key, p1.AsOf));
        }
      }

      foreach (var ci in new Dictionary<string, Calc>
                              {
                                {"Pv", p => p.Pv()},
                                {"Accrued", p => p.Accrued()}
                              })
      {
        if (p1.Settle >= exDivDay)
        Assert.AreNotEqual(ci.Value(p1), ci.Value(p2), String.Format("Compare {0} on {1}, expected to differ", ci.Key, p1.AsOf));
        else
        {
          Assert.AreEqual(ci.Value(p1), ci.Value(p2), String.Format("Compare {0} on {1}, expected to match", ci.Key, p1.AsOf));
        }
      }
    }

    private void CompareRegularBondCalcVsCustomized(BondPricer p1, BondPricer p2, double tolerance)
    {
      foreach (var ci in new Dictionary<string, Calc>
                              {
                                {"YieldToMaturity", p => p.YieldToMaturity()},
                                {"Full Price", p=>p.FullPrice()},
                                {"Accrued Interest", p=>p.AccruedInterest()},
                                {"ZSpread", p=>p.ImpliedZSpread()},
                                {"Model Full Price", p=>p.FullModelPrice()},
                                {"Irr", p=>p.Irr()}
                              })
      {
        Assert.AreEqual(ci.Value(p1), ci.Value(p2), tolerance, String.Format("Compare {0} on {1}, expected to match", ci.Key, p1.AsOf));
      }

      if (p1.SurvivalCurve != null && p2.SurvivalCurve != null)
      {
        foreach (var ci in new Dictionary<string, Calc>
                              {
                                {"CDS Level", p => p.ImpliedCDSLevel()},
                                {"CDS Spread", p => p.ImpliedCDSSpread()}
                              })
        {
          Assert.AreEqual(ci.Value(p1), ci.Value(p2), tolerance, String.Format("Compare {0} on {1}, expected to match", ci.Key, p1.AsOf));
        }
      }

      foreach (var ci in new Dictionary<string, Calc>
                              {
                                {"Pv", p => p.Pv()},
                                {"Accrued", p => p.Accrued()}
                              })
      {
          Assert.AreEqual(ci.Value(p1), ci.Value(p2), tolerance, String.Format("Compare {0} on {1}, expected to match", ci.Key, p1.AsOf));
      }
    }

    private DiscountCurve CreateDiscountCurve(Dt asOf)
    {

      var calibrator = new DiscountBootstrapCalibrator(asOf, asOf)
                       {
                         SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const),
                         SwapCalibrationMethod = SwapCalibrationMethod.Extrap
                       };
      var curve = new DiscountCurve(calibrator)
                    {
                      DayCount = DayCount.Actual365Fixed,
                      Frequency = Frequency.Continuous,
                      Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
                      Ccy = Currency.USD
                    };
      curve.AddSwap("10Y", Dt.Add(asOf, "10Y"), 2.0 / 100.0, DayCount.Thirty360, Frequency.SemiAnnual, BDConvention.None,
                    Calendar.None);
      curve.Fit();
      return curve;

    }

    private SurvivalCurve CreateSurvivalCurveForAmortBond(Dt asof, DiscountCurve ircurve, double recovery)
    {
      Dt settle = Dt.Add(asof, 1);
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

  }
}
