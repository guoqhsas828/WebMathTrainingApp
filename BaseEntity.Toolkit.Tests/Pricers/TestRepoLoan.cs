//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestRepoLoan
  {
    #region Fixed Rate Repo Tests

    private RepoLoan GetRepoLoan(Dt effective, Dt maturity, 
      double repoRate, Frequency freq, bool isOpen)
    {
      maturity = isOpen ? Dt.Empty : maturity;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective,
        maturity, repoRate, 0.05, ccy, freq, dc, bdConv, cal);
      repoLoan.IsOpen = isOpen;
      return repoLoan;
    }

    [Test]
    public void TestOpenRepoAccrued()
    {
      var effective = new Dt(20180124);
      var settle = Dt.Add(effective, 7);
      var discountCurve = new DiscountCurve(effective, 0.005);
      var openRepo = GetRepoLoan(effective, Dt.Empty, 0.03, Frequency.None, true);
      var pricer = new RepoLoanPricer(openRepo, null, settle, 
        settle, -1.0, discountCurve, null);
      var accrued = pricer.Accrued();
      var settle2 = Dt.Add(settle, 7);
      var pricer2 = new RepoLoanPricer(openRepo, null, settle2, 
        settle2, -1.0, discountCurve, null);
      var accrued2 = pricer2.Accrued();

      NUnit.Framework.Assert.AreEqual(accrued2, accrued*2,  1E-14);
    }


    [Test]
    public void TestOpenRepoAccrued1()
    {
      var effective = new Dt(20180125);
      var disCurve = new DiscountCurve(effective, 0.0);
      var repo = GetRepoLoan(effective, Dt.Empty, 0.05,
        Frequency.None, false);
      var pricer = new RepoLoanPricer(repo, null, effective,
        effective, 1.0, disCurve, null);
      var accrued = pricer.Accrued();
      Assert.AreEqual(0.0, accrued);
    }

    //for the open reop, the pv includes the accrued upto the 
    //settle date and the notional payments.
    [Test]
    public void TestOpenRepoPv()
    {
      var effective = new Dt(20180125);
      var settle = Dt.Add(effective, 7);
      var disCurve = new DiscountCurve(effective, 0.0);

      var openRepo = GetRepoLoan(effective, Dt.Empty,
        0.05, Frequency.None, true);
      var pricer = new RepoLoanPricer(openRepo, null, settle, settle,
        1.0, disCurve, null);

      var accrued = pricer.Accrued();
      var pv = pricer.Pv();

      NUnit.Framework.Assert.AreEqual(pv, accrued + pricer.Notional, 1e-14);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestOpenRepoAccrued2(bool isOpen)
    {
      var effective = new Dt(20180125);
      var maturity = isOpen ? Dt.Empty : Dt.Add(effective, 7);
      var settle = isOpen ? Dt.Add(effective, 6) : Dt.Add(maturity, -1);
      var disCurve = new DiscountCurve(effective, 0.0);

      var openRepo = GetRepoLoan(effective, maturity, 
        0.05, Frequency.None, isOpen);
      var pricer1 = new RepoLoanPricer(openRepo, null, settle, settle,
        1.0, disCurve, null);

      var accrued1 = pricer1.Accrued();
      var dtFraction = Dt.Fraction(effective, settle, DayCount.Actual360);
      var expect = openRepo.RepoRate * dtFraction;
      Assert.AreEqual(expect, accrued1, 1e-14);
    }

    //for the term and open repo, we test if all the coupon 
    //schedules are before the repo effective date.
    [TestCase(false)]
    [TestCase(true)]
    public void TestRepoStepUpandDownBefore(bool isOpen)
    {
      var effective = new Dt(1, 6, 2015);
      var date = Dt.Add(effective, Frequency.Weekly, 2, CycleRule.None);
      var maturity = isOpen ? Dt.Empty : date;
      var settle = isOpen ? date : effective;
      var discountCurve = new DiscountCurve(effective, 0.005);
      var repoWithSched = GetRepoLoan(effective, maturity, 0.0278, Frequency.Weekly,isOpen);
      repoWithSched.CouponSchedule.Add(new CouponPeriod(effective - 15, 0.02));
      repoWithSched.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, -5), 0.04));
      var repoPricer1 = new RepoLoanPricer(repoWithSched, null, settle, settle, 
        -1.0, discountCurve, null);
      var pv1 = repoPricer1.Pv();

      var rate = 0.0278;
      var repoNoSched = GetRepoLoan(effective, maturity, rate, Frequency.Weekly, isOpen);
      var repoPricer2 = new RepoLoanPricer(repoNoSched, null, settle,
        settle, -1.0, discountCurve, null);
      var pv2 = repoPricer2.Pv();

      Assert.AreEqual(pv1, pv2, 1E-14*repoWithSched.Notional);
    }


    //for the term and open repo, test when the repo effective date is the 
    // middle of coupon schedules.
    [TestCase(false)]
    [TestCase(true)]
    public void TestRepoStepUpandDownMiddle(bool isOpen)
    {
      var effective = new Dt(1, 6, 2015);
      var date = Dt.Add(effective, Frequency.Weekly, 1, CycleRule.None);
      var maturity = isOpen ? Dt.Empty : date;
      var settle = isOpen ? date : effective;
      var discountCurve = new DiscountCurve(effective, 0.005);
      var repo1 = GetRepoLoan(effective, maturity, 0.03, Frequency.None, isOpen);
      repo1.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, -1), 0.04));
      repo1.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, 3), 0.05));
      var repoPricer1 = new RepoLoanPricer(repo1, null, settle,
        settle, -1.0, discountCurve, null);
      
      var pv1 = repoPricer1.Pv();

      var rate = (0.04*3 + 0.05*4)/7;
      var repo2 = GetRepoLoan(effective, maturity, rate, Frequency.None, isOpen);
      var repoPricer2 = new RepoLoanPricer(repo2, null, settle,
        settle, -1.0, discountCurve, null);
      var pv2 = repoPricer2.Pv();

      Assert.AreEqual(pv1, pv2, 1E-8 * repo1.Notional);
    }


    //for the term and open repo, test all the coupon schedules
    // are after the the repo effective date.
    [TestCase(false)]
    [TestCase(true)]
    public void TestRepoStepUpandDownAfter(bool isOpen)
    {
      var effective = new Dt(1, 6, 2015);
      var date = Dt.Add(effective, 14);
      var maturity = isOpen ? Dt.Empty : date;
      var settle = isOpen ? date : effective;
      var discountCurve = new DiscountCurve(effective, 0.0);
      var repo1 = GetRepoLoan(effective, maturity, 0.03, Frequency.None, isOpen);
      repo1.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, 10), 0.0475));
      repo1.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, 5), 0.065));
      var repoPricer1 = new RepoLoanPricer(repo1, null, settle,
        settle, -1.0, discountCurve, null);
      var pv1 = repoPricer1.Pv();

      var rate = (0.03*5 + 0.065*5 + 0.0475*4)/Dt.Diff(effective, date);
      var repo2 = GetRepoLoan(effective, maturity, rate, Frequency.None, isOpen);
      var repoPricer2 = new RepoLoanPricer(repo2, null, settle,
        settle, -1.0, discountCurve, null);
      var pv2 = repoPricer2.Pv();
      Assert.AreEqual(pv1, pv2, 2E-8 * repo1.Notional);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestRepoStepUpandDownAfter1(bool isOpen)
    {
      var effective = new Dt(1, 6, 2015);
      var date = Dt.Add(effective, 1);
      var maturity = isOpen ? Dt.Empty : date;
      var settle = isOpen ? date : effective;
      var discountCurve = new DiscountCurve(effective, 0.005);
      var repo1 = GetRepoLoan(effective, maturity, 0.03, Frequency.None, isOpen);
      repo1.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, 10), 0.0475));
      repo1.CouponSchedule.Add(new CouponPeriod(Dt.Add(effective, 5), 0.065));
      var repoPricer1 = new RepoLoanPricer(repo1, null, settle,
        settle, -1.0, discountCurve, null);
      var pv1 = repoPricer1.Pv();

      var rate = 0.03 / Dt.Diff(effective, date);
      var repo2 = GetRepoLoan(effective, maturity, rate, Frequency.None, isOpen);
      var repoPricer2 = new RepoLoanPricer(repo2, null, settle,
        settle, -1.0, discountCurve, null);
      var pv2 = repoPricer2.Pv();
      Assert.AreEqual(pv1, pv2, 2E-8 * repo1.Notional);
    }



    [Test]
    public void FixedRateRepoLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Months);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, ccy, dc, bdConv, cal);
      var swapLeg = new SwapLeg(asOf, maturity, ccy, repoRate, dc, Frequency.None, bdConv, cal, false);
      swapLeg.Notional = 1.0;
      swapLeg.IsZeroCoupon = true;
      swapLeg.FinalExchange = true;

      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);

      var swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, notional, discountCurve, null, null, null, null, null);
      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, null);
      repoLoanPricer.Validate();
      var swapLegPv = swapPricer.Pv();
      var repoPv = repoLoanPricer.Pv();
      AssertEqual("Repo Pv", swapLegPv, repoPv, 1e-8);
    }

    [Test]
    public void FixedRateRepoReverseRepoLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Months);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;
      
      var repoLoan = new RepoLoan(RepoType.Repo, effective, maturity, repoRate, 0.05, ccy, dc, bdConv, cal);
      var revRepoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, ccy, dc, bdConv, cal);
      
      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);

      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, -notional, discountCurve, null);
      var revRepoLoanPricer = new RepoLoanPricer(revRepoLoan, null, asOf, asOf, notional, discountCurve, null);
      repoLoanPricer.Validate();
      revRepoLoanPricer.Validate();
      AssertEqual("Repo Pv", revRepoLoanPricer.Pv(), -repoLoanPricer.Pv(), 1e-8);
    }


    [Test]
    public void FixedRateRiskyRepoLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Months);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;
      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, ccy, dc, bdConv, cal);
      
      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);
      var survivalCurve = new SurvivalCurve(asOf, 0.07);

      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, null);
      repoLoanPricer.Validate();
      var riskyRepoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, survivalCurve);
      riskyRepoLoanPricer.Validate();
      Assert.AreNotEqual(repoLoanPricer.Pv(), riskyRepoLoanPricer.Pv(), "Repo Pv");
    }

    #endregion

    #region Floating (OIS) Rate Repo Tests

    [Test]
    public void FloatingOvernightIndexSwapRateRepoLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Months);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var rateIndex = SwapLegTestUtils.GetLiborIndex("1D") as ReferenceIndex;
      var freq = Frequency.Daily;

      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, rateIndex, Tenor.Empty, ccy, dc, bdConv, cal);
      var swapLeg = new SwapLeg(effective, maturity, Frequency.None, repoRate, rateIndex, ccy, dc, bdConv, cal)
      {
        CompoundingFrequency = freq,
        CompoundingConvention = CompoundingConvention.None,
        ProjectionType = ProjectionType.ArithmeticAverageRate
      };
      swapLeg.Notional = 1.0;
      swapLeg.IsZeroCoupon = true;
      swapLeg.FinalExchange = true;

      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);
      var rateCurve = new DiscountCurve(asOf, 0.006);

      var swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, null, null);
      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, null);
      repoLoanPricer.Validate();
      AssertEqual("Repo Pv", swapPricer.Pv(), repoLoanPricer.Pv(), 1e-8);
    }

    [Test]
    public void FloatingOvernightIndexSwapRateMultiResetsLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = new Dt(1, 5, 2015);
      var maturity = Dt.Add(effective, 1, TimeUnit.Years);
      var cal = Calendar.TGT;
      var repoRate = 0.0;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;

      var rateIndex = new InterestRateIndex("EONIA", Frequency.Daily, ccy, dc, cal, 2);//SwapLegTestUtils.GetLiborIndex("1D") as ReferenceIndex;
      var resetLag = new Tenor(rateIndex.SettlementDays, TimeUnit.Days);
      var resets = new SortedDictionary<Dt, double>();
      var d = Dt.AddDays(effective, -rateIndex.SettlementDays, cal);
      for (;;)
      {
        resets.Add(d, 0.006);
        if (d > asOf)
          break;
        d = Dt.AddDays(d, 1, cal);
      }
      var rateResets = new RateResets(resets);
      rateIndex.HistoricalObservations = rateResets;
      var freq = Frequency.Daily;

      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, rateIndex, resetLag, ccy, dc, bdConv, cal);
      var swapLeg = new SwapLeg(effective, maturity, Frequency.None, repoRate, rateIndex, ccy, dc, bdConv, cal)
      {
        CompoundingFrequency = freq,
        CompoundingConvention = CompoundingConvention.None,
        ProjectionType = ProjectionType.ArithmeticAverageRate
      };
      swapLeg.Notional = 1.0;
      swapLeg.IsZeroCoupon = true;
      swapLeg.FinalExchange = true;

      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005) { Name = "DiscountCurve" };
      var rateCurve = new DiscountCurve(asOf, 0.006) { Name = "RateCurve" };
      rateCurve.ReferenceIndex = rateIndex;

      var swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, rateResets, null, null);
      swapPricer.ApproximateForFastCalculation = false;
      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, rateResets, null);
      repoLoanPricer.Validate();
      AssertEqual("Repo Accrued", swapPricer.Accrued(), repoLoanPricer.Accrued(), 1e-8);
      AssertEqual("Repo Pv", swapPricer.Pv(), repoLoanPricer.Pv(), 1e-8);
    }

    #endregion

    #region Floating (LIBOR) Rate Repo Tests

    [Test]
    public void FloatingLiborRateRepoLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Months);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var rateIndex = SwapLegTestUtils.GetLiborIndex("3M") as ReferenceIndex;
      var freq = rateIndex.IndexTenor.ToFrequency();

      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, rateIndex, Tenor.Empty, ccy, dc, bdConv, cal);
      var swapLeg = new SwapLeg(effective, maturity, freq, repoRate, rateIndex, ccy, dc, bdConv, cal)
      {
        CompoundingFrequency = freq,
        CompoundingConvention = CompoundingConvention.None,
        ProjectionType = ProjectionType.SimpleProjection
      };
      swapLeg.Notional = 1.0;
      //swapLeg.IsZeroCoupon = true;
      swapLeg.FinalExchange = true;

      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);
      var rateCurve = new DiscountCurve(asOf, 0.006);

      var swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, null, null);
      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, null);
      repoLoanPricer.Validate();
      AssertEqual("Repo Pv", swapPricer.Pv(), repoLoanPricer.Pv(), 1e-8);
    }

    [Test]
    public void FloatingLiborRateRepoMultiplePaymentsLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Years);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var rateIndex = SwapLegTestUtils.GetLiborIndex("3M") as ReferenceIndex;
      var freq = rateIndex.IndexTenor.ToFrequency();

      var repoLoan = new RepoLoan(RepoType.ReverseRepo, effective, maturity, repoRate, 0.05, rateIndex, Tenor.Empty, ccy, dc, bdConv, cal);
      var swapLeg = new SwapLeg(effective, maturity, freq, repoRate, rateIndex, ccy, dc, bdConv, cal)
      {
        CompoundingFrequency = Frequency.None,
        CompoundingConvention = CompoundingConvention.None,
        ProjectionType = ProjectionType.SimpleProjection
      };
      swapLeg.Notional = 1.0;
      swapLeg.FinalExchange = true;

      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);
      var rateCurve = new DiscountCurve(asOf, 0.006);

      var swapPricer = new SwapLegPricer(swapLeg, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, null, null);
      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, null);
      repoLoanPricer.Validate();
      AssertEqual("Repo Pv", swapPricer.Pv(), repoLoanPricer.Pv(), 1e-8);
    }

    #endregion

    #region Sell/Buy-Back / Buy/Sell-Back Tests

    [Test]
    public void SellBuyBackLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Weeks);
      var repoRate = 0.04;
      var haircut = 0.025;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var repoLoan = new RepoLoan(RepoType.BuySellBack, effective, maturity, repoRate, haircut, ccy, dc, bdConv, cal);

      var swapLegInterest = new SwapLeg(asOf, maturity, ccy, repoRate, dc, Frequency.None, bdConv, cal, false);
      swapLegInterest.IsZeroCoupon = true;

      var swapLegFull = new SwapLeg(asOf, maturity, ccy, repoRate, dc, Frequency.None, bdConv, cal, false);
      swapLegFull.IsZeroCoupon = true;
      swapLegFull.Notional = 1.0;
      swapLegFull.FinalExchange = true;

      var notional = 1017.412500;
      var discountCurve = new DiscountCurve(asOf, 0.005);

      // Collateral
      var bondPrice = 104.35; // Full price
      var bondNotional = -(notional / ((1 - haircut))) / (bondPrice / 100.0);
      var bondCoupon = 0.06;
      var bondEffective = Dt.Add(asOf, -1, TimeUnit.Years);
      var bondMaturity = Dt.Add(asOf, 9, TimeUnit.Years);
      var bond = new Bond(bondEffective, bondMaturity, ccy, BondType.EURGovt, bondCoupon, dc, CycleRule.First, Frequency.SemiAnnual, bdConv, cal);
      var bondPricer = new BondPricer(bond, asOf, asOf, discountCurve, null, 0, TimeUnit.Days, 0.4)
      {
        MarketQuote = bondPrice,
        QuotingConvention = QuotingConvention.FullPrice,
        Notional = bondNotional
      };

      var repoLoanPricer = new RepoLoanPricer(repoLoan, bondPricer, asOf, asOf, notional, discountCurve, null);
      repoLoanPricer.Validate();
      var swapLegInterestPricer = new SwapLegPricer(swapLegInterest, asOf, asOf, notional, discountCurve, null, null, null, null, null);
      var swapLegPv = swapLegInterestPricer.Pv();
      var repoPv = repoLoanPricer.RepoInterest()*discountCurve.DiscountFactor(maturity);
      AssertEqual("Repo Interest", swapLegPv, repoPv, 1e-8);

      // Asset Income
      var ps = bondPricer.GetPaymentSchedule(null, bondEffective);
      var coupons = ps.GetPaymentsByType<InterestPayment>().Where(o => o.PayDt > effective && o.PayDt <= maturity);
      var interestPayments = coupons as InterestPayment[] ?? coupons.ToArray();
      var couponSum = interestPayments.Select(o => o.Amount).Sum() * bondNotional;
      AssertEqual("Repo Asset Coupons", couponSum, repoLoanPricer.AssetIncome(), 1e-8);

      // handle coupons
      var swapInterestPaymentPricers = new List<SwapLegPricer>();
      foreach (var c in interestPayments)
      {
        var swapLegCoupon = new SwapLeg(c.PayDt, maturity, ccy, repoRate, dc, Frequency.None, bdConv, cal, false);
        swapLegCoupon.Notional = 1.0;
        swapLegCoupon.IsZeroCoupon = true;
        swapLegCoupon.FinalExchange = true;
        var swapLegCouponPricer = new SwapLegPricer(swapLegCoupon, asOf, asOf, c.Amount * bondNotional, discountCurve, null, null, null, null, null);
        swapInterestPaymentPricers.Add(swapLegCouponPricer);
      }

      var swapLegPricer = new SwapLegPricer(swapLegFull, asOf, asOf, notional, discountCurve, null, null, null, null, null);
      var swapPv1 = swapLegPricer.Pv() + swapInterestPaymentPricers.Select(o => o.Pv()).Sum();
      var repoPv1 = repoLoanPricer.Pv();
      AssertEqual("Repo Pv", swapPv1, repoPv1, 1e-8);
    }

    [Test]
    public void SellBuyBackLoanVsBuySellBackPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Years);
      var repoRate = 0.04;
      var haircut = 0.025;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var repoLoan1 = new RepoLoan(RepoType.BuySellBack, effective, maturity, repoRate, haircut, ccy, dc, bdConv, cal);
      var repoLoan2 = new RepoLoan(RepoType.SellBuyBack, effective, maturity, repoRate, haircut, ccy, dc, bdConv, cal);
      var notional = 1017.412500;
      var discountCurve = new DiscountCurve(asOf, 0.005);

      // Collateral
      var bondPrice = 104.35; // Full price
      var bondNotional = -(notional / ((1 - haircut))) / (bondPrice / 100.0);
      var bondCoupon = 0.06;
      var bondEffective = Dt.Add(asOf, -1, TimeUnit.Years);
      var bondMaturity = Dt.Add(asOf, 9, TimeUnit.Years);
      var bond = new Bond(bondEffective, bondMaturity, ccy, BondType.EURGovt, bondCoupon, dc, CycleRule.First, Frequency.SemiAnnual, bdConv, cal);
      var bondPricer1 = new BondPricer(bond, asOf, asOf, discountCurve, null, 0, TimeUnit.Days, 0.4)
      {
        MarketQuote = bondPrice,
        QuotingConvention = QuotingConvention.FullPrice,
        Notional = bondNotional
      };
      var bondPricer2 = new BondPricer(bond, asOf, asOf, discountCurve, null, 0, TimeUnit.Days, 0.4)
      {
        MarketQuote = bondPrice,
        QuotingConvention = QuotingConvention.FullPrice,
        Notional = -bondNotional
      };

      var repoLoanPricer1 = new RepoLoanPricer(repoLoan1, bondPricer1, asOf, asOf, notional, discountCurve, null);
      repoLoanPricer1.Validate();
      var repoLoanPricer2 = new RepoLoanPricer(repoLoan2, bondPricer2, asOf, asOf, -notional, discountCurve, null);
      repoLoanPricer2.Validate();
      AssertEqual("Repo Pv", repoLoanPricer1.Pv(), -repoLoanPricer2.Pv(), 1e-8);
    }

    #endregion

    #region Clone test

    [Test]
    public void CloneRepoLoanPv()
    {
      var asOf = new Dt(1, 6, 2015);
      var effective = asOf;
      var maturity = Dt.Add(effective, 1, TimeUnit.Months);
      var repoRate = 0.04;
      var ccy = Currency.EUR;
      var dc = DayCount.Actual360;
      var bdConv = BDConvention.Following;
      var cal = Calendar.TGT;

      var rateIndex = SwapLegTestUtils.GetLiborIndex("3M") as ReferenceIndex;
      var freq = Frequency.Quarterly;

      var repoLoan = new RepoLoan(RepoType.Repo, effective, maturity, repoRate, 0.05, rateIndex, Tenor.Empty, ccy, dc, bdConv, cal);
      var swapLeg = new SwapLeg(asOf, maturity, freq, repoRate, rateIndex, ccy, dc, bdConv, cal);
      swapLeg.Notional = 1.0;
      //swapLeg.IsZeroCoupon = true;
      swapLeg.FinalExchange = true;

      var notional = 1000.0;
      var discountCurve = new DiscountCurve(asOf, 0.005);
      var rateCurve = new DiscountCurve(asOf, 0.006);
      var survivalCurve = new SurvivalCurve(asOf, 0.07);

      var repoLoanPricer = new RepoLoanPricer(repoLoan, null, asOf, asOf, notional, discountCurve, rateIndex, rateCurve, null, survivalCurve);

      try
      {
        AssertEqual("Repo Pv", (repoLoanPricer.Clone() as RepoLoanPricer).Pv(), repoLoanPricer.Pv(), 1e-8);
      }
      catch (Exception)
      {
        NUnit.Framework.Assert.Fail();
      }
    }

    #endregion
  }
}
