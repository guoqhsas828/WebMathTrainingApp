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

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test calculations on bonds with 21-business-day payment gap period
  /// </summary>
  [TestFixture]
  public class TestBondPaymentGap
  {

    public Bond Bond { get; set; }
    public Bond RegularBond { get; set; }
    private const int PaymentGapDays = 21;
    private const bool PaymentGapBusinessFlag = true;

    /// Set up a regular bond which accrues $1 a day with 10,000 notional, and another bond having same properties except additional payment gap
    [OneTimeSetUp]
    public void Setup()
    {
      RegularBond = new Bond(new Dt(15, 9, 2010),
                      new Dt(15, 9, 2012),
                      Currency.USD,
                      BondType.USCorp,
                      3.6/100.0,
                      DayCount.Thirty360,
                      CycleRule.Fifteenth,
                      Frequency.SemiAnnual,
                      BDConvention.Modified,
                      Calendar.NYB);

      Bond = (Bond) RegularBond.Clone();
      Bond.PaymentLagRule = new PayLagRule(PaymentGapDays, PaymentGapBusinessFlag);
    }

    [Test]
    public void PaymentGapVsRegularAccrued() //Accrued shall be the same on spot trades, differ on some special dates
    {
      var i = new Dt(15, 9, 2010);
      var e = new Dt(15, 3, 2011);
      var s = Dt.AddDays(e, -PaymentGapDays-5, Calendar.NYB); 
      Dt d = s;
      Dt laggedPaymentDt = Dt.AddDays(e, PaymentGapDays, Calendar.NYB);
      Dt testEnd = Dt.AddDays(laggedPaymentDt, 5, Calendar.NYB);
      Dt nextCoupon = e;
      var nextNextCoupon = new Dt(15, 9, 2011);
      while (d < testEnd)
      {
        int regularAccrued = Dt.Diff(d >=e ? e: i, d, DayCount.Thirty360);
        int delayedAccrued = Dt.Diff(d>=laggedPaymentDt ? e : i, d, DayCount.Thirty360);


        var discountCurve = CreateDiscountCurve(d);

        // create a pricer for a trade settling on spot date
        var bondPricerRegularSpot = new BondPricer(RegularBond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
                           {
                             QuotingConvention = QuotingConvention.FlatPrice,
                             MarketQuote = 1.0,
                             Notional = 10000.0
                           };
        // create a similar pricer on the payment gap bond on spot date
        var laggedBondPricerSpot = new BondPricer(Bond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0
        };
        Assert.AreEqual(regularAccrued, bondPricerRegularSpot.Accrued(), 1E-13,
          String.Format("New trade on regular bond accrual {0}", d));
        Assert.AreEqual(regularAccrued / bondPricerRegularSpot.Notional,
          bondPricerRegularSpot.AccruedInterest(d, s), 1E-13, String.Format("New trade on payment gap bond accrual {0}", d));
        var regularSpotCashflow = bondPricerRegularSpot.Cashflow;
        var laggedSpotCashflow = laggedBondPricerSpot.Cashflow;
        Assert.AreEqual((d >= nextCoupon ? nextNextCoupon : nextCoupon).ToInt(),
          (regularSpotCashflow.Count > 0 ? regularSpotCashflow.GetEndDt(0) : Dt.Empty).ToInt(),
          String.Format("New trade on regular bond next cashflow period end {0}", d));
        Assert.AreEqual((d >= nextCoupon ? nextNextCoupon : nextCoupon).ToInt(),
          (laggedSpotCashflow.Count > 0 ? laggedSpotCashflow.GetEndDt(0) : Dt.Empty).ToInt(),
          String.Format("New trade on payment gap bond next trade cashflow period end {0}", d));
        Assert.AreNotEqual((regularSpotCashflow.Count > 0 ? regularSpotCashflow.GetDt(0) : Dt.Empty).ToInt(),
                        (laggedSpotCashflow.Count > 0 ? laggedSpotCashflow.GetDt(0) : Dt.Empty).ToInt(),
          String.Format("Coupon payment from gap bond is lagged behind the regular bond {0}", d));
        // create a pricer for the regular bond trade settling prior to the previous coupon period end
        var regularBondPricerSettled = new BondPricer(RegularBond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
         {
           QuotingConvention = QuotingConvention.FlatPrice,
           MarketQuote = 1.0,
           Notional = 10000.0,
           TradeSettle = s
         };

        // create a pricer for the payment gap bond trade settling prior to the previous coupon period end
        var laggedBondPricerSettled = new BondPricer(Bond, d, d, discountCurve, null, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = s
        };

        Assert.AreEqual(delayedAccrued,
                        laggedBondPricerSettled.Accrued(), 1E-13,
          String.Format("With trade settle set, Bond with payment gap trade accrual {0}", d));
        Assert.AreEqual(regularAccrued / bondPricerRegularSpot.Notional,
                        regularBondPricerSettled.AccruedInterest(), 1E-13,
          String.Format("With trade settle set, Regular bond trade accrual {0}", d));
        var regularBondTradeCashflow = regularBondPricerSettled.TradeCashflow;
        var laggedBondTradeCashflow = laggedBondPricerSettled.TradeCashflow;
        Assert.AreEqual((d >= nextCoupon ? nextNextCoupon : nextCoupon).ToInt(),
                        (regularBondTradeCashflow.Count > 0 ? regularBondTradeCashflow.GetEndDt(0) : Dt.Empty).ToInt(),
          String.Format("With trade settle set, Regular bond trade next cashflow period end {0}", d));
        Assert.AreEqual((d >= laggedPaymentDt ? nextNextCoupon : nextCoupon).ToInt(),
          (laggedBondTradeCashflow.Count > 0 ? laggedBondTradeCashflow.GetEndDt(0) : Dt.Empty).ToInt(),
          String.Format("With trade settle set, Bond with payment gap trade next cashflow period end {0}", d));
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }


    [Test]
    public void PaymentGapVsRegularBondSpotSettle()
    {
      var e = new Dt(15, 3, 2011);

      var s = Dt.AddDays(e, -PaymentGapDays - 5, Calendar.NYB); 
      Dt d = s;
      Dt laggedPaymentDt = Dt.AddDays(e, PaymentGapDays, Calendar.NYB);
      Dt testEnd = Dt.AddDays(laggedPaymentDt, 5, Calendar.NYB);

      while (d < testEnd)
      {
        var discountCurve = CreateDiscountCurve(d);
        var survivalCurve = CreateSurvivalCurveForAmortBond(d, discountCurve, 0.4);
        // create a pricer for a regular bond trade settling on spot date
        var regBondPricerSpot = new BondPricer(RegularBond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0
        };

        // create a pricer for a payment gap bond trade settling on spot date
        var laggedbondPricerSpot = new BondPricer(Bond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0
        };

        CompareMarketCalcs(regBondPricerSpot, laggedbondPricerSpot, Dt.Empty, laggedPaymentDt);
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }

    [Test]
    public void PaymentGapVsRegularBondInclusiveSettle()
    {
      var e = new Dt(15, 3, 2011);

      var s = Dt.AddDays(e, -PaymentGapDays - 5, Calendar.NYB);
      Dt d = s;
      Dt laggedPaymentDt = Dt.AddDays(e, PaymentGapDays, Calendar.NYB);
      Dt testEnd = Dt.AddDays(laggedPaymentDt, 5, Calendar.NYB);
      while (d < testEnd)
      {

        var discountCurve = CreateDiscountCurve(d);
        var survivalCurve = CreateSurvivalCurveForAmortBond(d, discountCurve, 0.4);
        // create a pricer for a trade settling on date prior to previous coupon period end
        var regBondPricer = new BondPricer(RegularBond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0, 
          TradeSettle = s
        };

        // create a pricer for a trade settling on date prior to previous coupon period end
        var laggedBondPricer = new BondPricer(Bond, d, d, discountCurve, survivalCurve, 0, TimeUnit.None, 0.0, 0.0, 0.0, true)
        {
          QuotingConvention = QuotingConvention.FlatPrice,
          MarketQuote = 1.0,
          Notional = 10000.0,
          TradeSettle = s
        };

        CompareMarketCalcs(regBondPricer, laggedBondPricer, e, laggedPaymentDt);
        d = Dt.AddDays(d, 1, Calendar.NYB);
      }
    }

    private delegate double Calc(BondPricer p);
    private void CompareMarketCalcs(BondPricer pRegular, BondPricer pLaggedPay, Dt nextCpnDate, Dt laggedPaymentDt)
    {
      foreach (var ci in new Dictionary<string, Calc>
                           {
                             {"YieldToMaturity", p => p.YieldToMaturity()},
                             {"ModDuration", p => p.ModDuration()},
                             {"Convexity", p => p.Convexity()},
                             {"Full Price", p => p.FullPrice()},
                             {"Accrued", p => p.Accrued()},
                             {"Accrued Interest", p => p.AccruedInterest()}
                           })
      {
        if (ci.Key == "Accrued" && !nextCpnDate.IsEmpty() && pRegular.AsOf >= nextCpnDate && pRegular.AsOf < laggedPaymentDt)
        {
          Assert.AreNotEqual(ci.Value(pRegular), ci.Value(pLaggedPay),
            String.Format("Compare {0} on {1}, expected to differ", ci.Key, pRegular.AsOf));}
        else
        {
          Assert.AreEqual(ci.Value(pRegular), ci.Value(pLaggedPay),
            String.Format("Compare {0} on {1}, expected to match", ci.Key, pRegular.AsOf));}
      }

      if (pRegular.SurvivalCurve != null && pLaggedPay.SurvivalCurve != null)
      {
        foreach (var ci in new Dictionary<string, Calc>
                             {
                               {"CDS Level", p => p.ImpliedCDSLevel()},
                               {"CDS Spread", p => p.ImpliedCDSSpread()}
                             })
        {
          Assert.AreNotEqual(ci.Value(pRegular), ci.Value(pLaggedPay),
            String.Format("Compare {0} on {1}, expected to differ", ci.Key, pRegular.AsOf));
        }
      }

      foreach (var ci in new Dictionary<string, Calc>
                           {
                             {"Pv", p => p.Pv()},
                             {"ZSpread", p => p.ImpliedZSpread()},
                             {"Asset Swap Spread", p => p.AssetSwapSpread(DayCount.Actual360, Frequency.Quarterly)},
                             {"Model Full Price", p => p.FullModelPrice()},
                             {"Irr", p => p.Irr()}
                           })
      {
        Assert.AreNotEqual(ci.Value(pRegular), ci.Value(pLaggedPay),
          String.Format("Compare {0} on {1}, expected to differ", ci.Key, pRegular.AsOf));
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
