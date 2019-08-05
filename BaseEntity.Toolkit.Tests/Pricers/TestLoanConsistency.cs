//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Class to test consistency in the Loan pricing.
  /// </summary>
  [TestFixture]
  public class TestLoanConsistency : ToolkitTestBase
  {
    #region Tests
    /// <summary>
    /// Tests a fixed coupon loan vs. a floating loan with a flat forward index curve.
    /// </summary>
    [Test]
    public void FlatFloatingVsFixedCoupon()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);

      // Products
      Loan fixedLoan = this.NewLoan("", 0.08, new string[] { "I" }, new double[] { 0.08 });
      Loan floatingLoan = this.NewLoan("USDLIBOR", 0.03, new string[] { "I" }, new double[] { 0.03 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, floatingLoan.DayCount, floatingLoan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, 0.40, indexCurve);
      double recoveryRate = 0.4;
      double lastReset = 0.08;
      LoanPricer fixedPricer = LoanPricerFactory.New(fixedLoan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, recoveryRate, null, 0, 1, CalibrationType.None, 0);
      LoanPricer floatingPricer = LoanPricerFactory.New(floatingLoan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, recoveryRate, null, 0, 1, CalibrationType.None, lastReset);

      // Test
      // We use a tolerance (in %) to handle day counting/calendar problems existing between the coupon schedule 
      // and the discount factor interpolation
      double tolerance = 0.005;
      AssertEqual("Pv", fixedPricer.Pv(), floatingPricer.Pv(), tolerance * fixedPricer.Pv());
    }

    /// <summary>
    /// Tests a loan with a single (no) performance level vs. a loan with several performance levels (where both have the same 100% usage).
    /// </summary>
    [Test]
    public void PricingGridVsNoPricingGrid()
    {
      Dt asOf = new Dt(28, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);

      // Products
      Loan gridLoan = this.NewLoan("USDLIBOR", 300, new string[] { "AAA", "AA", "A", "B", "BB", "CCC" }, new double[] { 0.03, 0.03, 0.03, 0.03, 0.03, 0.03 });
      Loan noGridLoan = this.NewLoan("USDLIBOR", 300, new string[] { "I" }, new double[] { 0.03 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, gridLoan.DayCount, gridLoan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, 0.40, indexCurve);
      RecoveryCurve recovCurve = new RecoveryCurve(asOf, 0.4);
      LoanPricer gridPricer = LoanPricerFactory.New(
        gridLoan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, 0.4, null, 0, 1.0,
        CalibrationType.None, "A",
        new double[] {1.0, 1.0, 1.0, 1.0, 1.0, 1.0},
        new double[] {0.16, 0.17, 0.17, 0.17, 0.17, 0.16}, 
        0.08);
      LoanPricer noGridPricer = LoanPricerFactory.New(noGridLoan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, 0.4, null, 0, 1, CalibrationType.None, 0.08);

      //test
      double tolerance = 0.005;
      AssertEqual("Pv", noGridPricer.Pv(), gridPricer.Pv(), tolerance * noGridPricer.Pv());
    }

    /// <summary>
    /// Tests a simple floating rate instrument setup as the equivalent Bond and Loan. The calculations should be 
    /// the same within a tolerance.
    /// </summary>
    [Test]
    public void LoanPricerVsBondPricerFloating()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 0.03, new string[] { "I" }, new double[] { 0.03 });
      Bond bond = this.NewBond("USDLIBOR", 0.03);
      bond.ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bond.Index, bond.Freq, bond.Ccy, bond.DayCount,
                                                                   bond.Calendar, 0);
      // Setup objects
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, 0.40, indexCurve);
      double recoveryRate = 0.4;
      LoanPricer loanPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, recoveryRate, null, 0, 1, CalibrationType.None, 0.08);
      BondPricer bondPricer = new BondPricer(bond, asOf, settle, indexCurve, survCurve, 0, TimeUnit.None, recoveryRate, 0, 0, true);

      //setup reset for floaters
      bondPricer.RateResets.Add(new RateReset(bond.Effective, 0.08));
      bondPricer.CurrentRate = 0.08;
      bondPricer.ReferenceCurve = indexCurve;
      bondPricer.Notional = 1000000;

      // Test
      double tolerance = 0.0050;
      Assert.AreEqual(bondPricer.Pv(), loanPricer.Pv(false), tolerance * bondPricer.Pv(), "Pv");
      Assert.AreEqual(bondPricer.Accrued(), loanPricer.Accrued(), tolerance * bondPricer.Accrued(), "Accrued");
    }

    /// <summary>
    /// Tests a callable, floating rate instrument setup as the equivalent Bond and Loan. The calculations should be 
    /// the same within a tolerance.
    /// </summary>
    [Test]
    public void LoanPricerVsBondPricerFloatingCallable()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 0.08, new string[] { "I" }, new double[] { 0.08 });
      Bond bond = this.NewBond("USDLIBOR", 0.08);
      bond.ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bond.Index, bond.Freq, bond.Ccy, bond.DayCount,
                                                                   bond.Calendar, 0);
      // Setup objects
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, 0.40, indexCurve);
      double recoveryRate = 0.4;
      LoanPricer loanPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, recoveryRate, null, 0, 1, CalibrationType.None, 0.13);
      BondPricer bondPricer = new BondPricer(bond, asOf, settle, indexCurve, survCurve, 0, TimeUnit.None, recoveryRate, 0.0, 0.5, false);

      // Setup bond callability
      bond.CallSchedule.Add(new CallPeriod(bond.Effective, bond.Maturity, 1.0, 0.0, OptionStyle.American, 0));

      //setup reset for floaters
      bondPricer.RateResets.Add(new RateReset(bond.Effective, 0.13));
      bondPricer.CurrentRate = 0.13;
      bondPricer.ReferenceCurve = indexCurve;
      bondPricer.Notional = 1000000;
      bondPricer.MarketQuote = 1;
      bondPricer.QuotingConvention = QuotingConvention.FlatPrice;
      bondPricer.EnableZSpreadAdjustment = false;

      // Test
      double tolerance = 0.005;
      Assert.AreEqual( bondPricer.Pv(), loanPricer.Pv(), tolerance * bondPricer.Pv(), "Pv");
      Assert.AreEqual(bondPricer.Accrued(), loanPricer.Accrued(), tolerance * bondPricer.Accrued(), "Accrued");
    }

    /// <summary>
    /// Tests a Loan and a Bond that are trading at deep discounts below their stated recovery. 
    /// </summary>
    /// 
    /// <remarks>
    /// This test was implemented as part of a fix to the Loan model where the recovery was not being taken 
    /// into account in the first coupon period. This was causing the model price on these very risky (high spread) loans 
    /// to be incorrect and far off the same structure priced as a bond. 
    /// </remarks>
    /// 
    [Test]
    public void LoanPricerVsBondPricerFloatingDistressed()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recoveryRate = 0.7;
      double price = 0.58;
      double spread = 0.7500;

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 0.03, new string[] { "I" }, new double[] { 0.03 });
      Bond bond = this.NewBond("USDLIBOR", 0.03);
      bond.ReferenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex(bond.Index, bond.Freq, bond.Ccy, bond.DayCount,
                                                                   bond.Calendar, 0);
      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, spread, 5, recoveryRate, indexCurve);
      LoanPricer loanPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve, recoveryRate, null, 0, price, CalibrationType.None, 0.08);
      BondPricer bondPricer = new BondPricer(bond, asOf, settle, indexCurve, survCurve, 0, TimeUnit.None, recoveryRate, 0, 0, true);

      //setup reset for floater
      bondPricer.RateResets.Add(new RateReset(bond.Effective, 0.08));
      bondPricer.CurrentRate = 0.08;
      bondPricer.ReferenceCurve = indexCurve;
      bondPricer.Notional = 1000000;
      bondPricer.MarketQuote = price;
      bondPricer.QuotingConvention = QuotingConvention.FlatPrice;

      // Test
      double tolerance = 0.005;
      Assert.AreEqual(bondPricer.Pv(), loanPricer.Pv(false), tolerance * bondPricer.Pv(), "Pv");
      Assert.AreEqual(bondPricer.Accrued(), loanPricer.Accrued(), tolerance * bondPricer.Accrued(), "Accrued");
    }

    /// <summary>
    /// Tests a simple fixed rate instrument setup as the equivalent Bond and Loan. The calculations should be 
    /// the same within a tolerance.
    /// </summary>
    [Test]
    public void LoanPricerVsBondPricerFixed()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      // CashflowSchedule lc, bc;

      // Products
      Loan loan = this.NewLoan("", 0.08, new string[] { "I" }, new double[] { 0.08 });
      Bond bond = this.NewBond("", 0.08);

      // Flat forward curve
      //DiscountCurve indexCurve = new DiscountCurve(asOf, 0.05);
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, 0.40, indexCurve);
      LoanPricer loanPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, null, survCurve, 0.4, null, 0, 1.0, CalibrationType.None, 0.0);
      BondPricer bondPricer = new BondPricer(bond, asOf, settle, indexCurve, survCurve, 0, TimeUnit.None, 0.4, 0, 0, true);

      //setup pricers 
      bondPricer.Notional = 1000000;

      // Test
      double tolerance = 0.005;
      Assert.AreEqual(bondPricer.Pv(), loanPricer.Pv(false), tolerance * bondPricer.Pv(), "The PV is incorrect");
      Assert.AreEqual(bondPricer.Accrued(), loanPricer.Accrued(), tolerance * bondPricer.Accrued(), "The Accrued is incorrect");
    }

    [Test]
    public void TestLoanCallScheduleWithCallStrike()
    {
      Dt asOf = new Dt(20160414);
      Dt settle = asOf;
      Dt maturity = Dt.Add(asOf, Tenor.Parse("1Y"));

      const double callStrike = 1.01;
      double couponRate = 0.0;

      var loan = GetLoan(asOf, maturity, Dt.Empty, maturity, couponRate) as Loan;
      var frequency = loan.Frequency;
      var callSchedule = new List<CallPeriod>();
      loan.CallSchedule = callSchedule;
      var pricer = GetLoanPricer(asOf, settle, loan) as LoanPricer;
      var ytw1 = pricer.YieldToWorst();
      loan.CallSchedule = null;
      pricer.Reset();
      var ytw2 = pricer.YieldToWorst();
      Assert.AreEqual(ytw1, ytw2, 1E-14);

      //Test yield to maturity(without call schedule)
      var ytm = pricer.YieldToMaturity();
      var ytm1 = pricer.YieldToMaturity(asOf, settle, frequency);
      var ytc1 = pricer.YieldToCall(maturity);
      var ytc11 = pricer.YieldToCall(asOf, settle, frequency, maturity, Double.NaN);
      Assert.AreEqual(ytm, ytc1, 1E-14);
      Assert.AreEqual(ytm, ytm1, 1E-14);
      Assert.AreEqual(ytm, ytc11, 1E-14);

      // Test expect yield
      var ytc2 = pricer.YieldToCall(maturity, callStrike);
      var ytc3 = pricer.YieldToCall(asOf, settle, frequency, maturity, callStrike);
      var expect = callStrike - 1.0;
      Assert.AreEqual(expect, ytc2, 1E-14);
      Assert.AreEqual(expect, ytc3, 1E-14);

      //Test expect yield
      callSchedule.Add(new CallPeriod(maturity, maturity,
        callStrike, 0.0, OptionStyle.European, 0));
      loan.CallSchedule = callSchedule;
      pricer.Reset();
      var ytw = pricer.YieldToWorst();
      Assert.AreEqual(expect, ytw, 1E-14);

      //Test yield to maturity vs yield to call with schedule
      var ytm2 = pricer.YieldToMaturity();
      var ytc4 = pricer.YieldToCall(maturity, callStrike);
      var ytc5 = pricer.YieldToCall(asOf, settle, frequency, maturity, callStrike);
      Assert.AreEqual(ytm2, ytc4, 1E-14);
      Assert.AreEqual(ytm2, ytc5, 1E-14);
    }

    [Test]
    public void TestLoanCallScheduleYieldToWorst()
    {
      Dt asOf = new Dt(20160414);
      Dt settle = asOf;
      Dt maturity = Dt.Add(asOf, Tenor.Parse("2Y"));
      var callDate = Dt.Add(asOf, Tenor.Parse("6M"));
      var callDate1 = Dt.Add(asOf, Tenor.Parse("3Y"));
     
      const double callStrike = 1.01;
      double couponRate = 0.0;

      var loan = GetLoan(asOf, maturity, Dt.Empty, maturity, couponRate) as Loan;
      loan.Frequency=Frequency.Quarterly;
      var callSchedule = new List<CallPeriod>();
      callSchedule.Add(new CallPeriod(callDate, callDate, callStrike,
        0.0, OptionStyle.European, 0));
      callSchedule.Add(new CallPeriod(maturity, maturity,
        callStrike, 0.0, OptionStyle.European, 0));
      callSchedule.Add(new CallPeriod(callDate1, callDate1,
        callStrike, 0.0, OptionStyle.European, 0));
      loan.CallSchedule = callSchedule;
      var pricer = GetLoanPricer(asOf, settle, loan) as LoanPricer;
      //Calculate the yield to maturity
      var ytm = pricer.YieldToMaturity();

      //Yield to maturity will get the worst yield here
      var ytw = pricer.YieldToWorst();

      //Since the callDate1 is after the maturity, so will return yield to maturity
      var ytc = pricer.YieldToCall(callDate1, callStrike);

      //Three years is after the maturity, so will return yield to maturity
      var yt3Y = pricer.YieldToThreeYearCall();

      //The callDate1 is after the maturity, so will return yield to maturity
      var ytc1 = pricer.YieldToCall(callDate1);
      Assert.AreEqual(ytm, ytw, 1E-14);
      Assert.AreEqual(ytm, ytc, 1E-14);
      Assert.AreEqual(ytm, yt3Y, 1E-14);
      Assert.AreEqual(ytm, ytc1, 1E-14);
    }


    [Test]
    public void TestLoanFlatPriceFromYtW()
    {
      Dt asOf = new Dt(20070920);
      Dt settle = asOf;
      Dt effecitve = new Dt(20070420);
      Dt maturity =new Dt(20140420);
      Dt firstCoupon = new Dt(20070701);
      Dt lastCoupon = new Dt(20140401);
      var callDate0 = Dt.Add(asOf, Tenor.Parse("6M"));
      var callDate1 = Dt.Add(asOf, Tenor.Parse("3Y"));

      var calls = new []
      {
        new CallInfo {CallDate = callDate0, CallStrike = 1.10},
        new CallInfo {CallDate = callDate1, CallStrike = 1.01},
        new CallInfo {CallDate = maturity, CallStrike = 1.00},
      };
      const double couponRate = 0.8;
      const double yield = 0.05;

      Loan loan = GetLoan(effecitve, maturity, firstCoupon, lastCoupon, couponRate);
      loan.Frequency = Frequency.Quarterly;
      loan.CallSchedule = CreateCallSchedule(calls, loan.Ccy);
      TestPriceFromYield(asOf, settle, calls, loan, yield);

      //Test call schedule is null
      loan.CallSchedule = null;
      TestPriceFromYield(asOf, settle, null, loan, yield);
    }

    private void TestPriceFromYield(Dt asOf, Dt settle, CallInfo[] calls,
      Loan loan, double yield)
    {
      LoanPricer pricer = GetLoanPricer(asOf, settle, loan);

      if (calls != null && calls.Length > 0)
      {
        var minfp = CalcFlatPrice(pricer, calls[0]);
        for (int i = 1; i < calls.Length; i++)
        {
          var fp = CalcFlatPrice(pricer, calls[i]);
          if (fp < minfp) minfp = fp;
        }
        var zeroflatPrice = pricer.PriceFromYieldToWorst(0.0);
        Assert.AreEqual(minfp, zeroflatPrice, 1E-7);
        var pricer3 = GetLoanPricer(asOf, settle, loan, minfp);
        var zeroYtW = pricer3.YieldToWorst();
        Assert.AreEqual(0.0, zeroYtW, 1E-10);
      }

      var flatPrice2 = pricer.PriceFromYieldToWorst(yield);
      var pricer2 = GetLoanPricer(asOf, settle, loan, flatPrice2);
      var yield2 = pricer2.YieldToWorst();
      Assert.AreEqual(yield, yield2, 1E-7);
    }


    private double CalcFlatPrice(LoanPricer pricer, CallInfo call)
    {
      var asOf = pricer.AsOf;
      var settle = pricer.Settle;
      var cf = pricer.GenerateCashflowToCall(asOf, settle, settle, call.CallDate);
      var last = cf.Count - 1;
      cf.Set(last, cf.GetAmount(last) * call.CallStrike, cf.GetAccrued(last),
        cf.GetDefaultAmount(last));

      var fullPrice = 0.0;
      for (int i = 0; i < cf.Count; i++)
      {
        if (cf.GetDt(i) > settle)
          fullPrice += cf.GetAccrued(i) + cf.GetAmount(i) + cf.GetDefaultAmount(i);
      }

      return fullPrice - pricer.AccruedInterest();
    }

    private struct CallInfo
    {
      public Dt CallDate;
      public double CallStrike;
    }

    private List<CallPeriod> CreateCallSchedule(CallInfo[] calls,
      Currency ccy = Currency.USD)
    {
      var retVal = new List<CallPeriod>();
      for (int i = 0; i < calls.Length; i++)
      {
        retVal.Add(new CallPeriod(calls[i].CallDate, calls[i].CallDate,
          calls[i].CallStrike, 0.0, OptionStyle.European, 0));
      }
      return retVal;
    }

   
    private Loan GetLoan(Dt effective, Dt maturity, Dt firstCoupon,
      Dt lastCoupon, double couponRate)
    {
      var l = new Loan();
      l.BDConvention = BDConvention.Following;
      l.Calendar = Calendar.NYB;
      l.Ccy = Currency.USD;
      l.CommitmentFee = 0.0;
      l.DayCount = DayCount.Actual365Fixed;
      l.Description = "Loan";
      l.Effective = effective;
      l.CycleRule = CycleRule.None;
      l.FirstCoupon = firstCoupon;
      l.Frequency = Frequency.Annual;
      l.LastCoupon = lastCoupon;
      l.LoanType = LoanType.Term;
      l.Maturity = maturity;
      l.Notional = 1.0;
      l.PeriodAdjustment = false;
      l.PerformanceLevels = new string[] { Loan.DefaultPerformanceLevel };
      l.PricingGrid.Add(Loan.DefaultPerformanceLevel, couponRate);

      return l;
    }



    [Test]
    public void TestLoanExerciseScheduleWithoutCallStrike()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = asOf + 1;
      var tenors = new [] {"3M", "6M", "9M", "12M", "15M"};

      //Loan with default call schedule.
      Loan loan = NewLoan("", 0.08, new string[] { "I" }, new double[] { 0.08 });

      var schedule = new List<CallPeriod>();
      var schedule1 = new List<CallPeriod>();
      for (int i = 1; i < tenors.Length; i++)
      {
        var callDate = Dt.Add(settle, Tenor.Parse(tenors[i]));
        var exerciseSchedule = new CallPeriod(callDate,
          callDate, Double.NaN, 0.0, OptionStyle.European, 0);
        schedule.Add(exerciseSchedule);
        schedule1.Add(exerciseSchedule);
      } 

      //Test with call schedule
      Loan loan2 = loan.CloneObjectGraph();
      loan2.CallSchedule = schedule;

      //Test filtering the dates before the settle date and loan maturity date
      Loan loan3 = loan.CloneObjectGraph();
      schedule1.Add(new CallPeriod(Dt.Add(settle, -5), Dt.Add(settle, -5),
        Double.NaN, 0.0, OptionStyle.European, 0));
      schedule1.Add(new CallPeriod(Dt.Add(loan.Maturity, 20),
        Dt.Add(loan.Maturity, 20), Double.NaN, 0.0, OptionStyle.European, 0));
      loan3.CallSchedule = schedule1;

      //Test empty customized schedule. Will return yield to maturity

      var pricer2 = GetLoanPricer(asOf, settle, loan2);
      var ytw2 = pricer2.YieldToWorst();
      var pricer3 = GetLoanPricer(asOf, settle, loan3);
      var ytw3 = pricer3.YieldToWorst();
      Assert.AreEqual(ytw2, ytw3, 1E-15);
    }


    [Test]
    public void TestLoanYieldToWorst0()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = asOf + 1;

      //Loan with default call schedule.
      Loan loan0 = NewLoan("", 0.08, new string[] { "I" }, new double[] { 0.08 });
      var pricer0 = GetLoanPricer(asOf, settle, loan0);

      var yieldToWorst = pricer0.YieldToWorst();

      var defaultCallSchedule = new List<CallPeriod>
      {
        new CallPeriod(Dt.Add(settle, 30, TimeUnit.Days), Dt.Add(settle, 30, TimeUnit.Days),
          Double.NaN, 0.0, OptionStyle.European, 0),
        new CallPeriod(Dt.Add(settle, 1, TimeUnit.Years), Dt.Add(settle, 1, TimeUnit.Years),
          Double.NaN, 0.0, OptionStyle.European, 0),
        new CallPeriod(Dt.Add(settle, 3, TimeUnit.Years), Dt.Add(settle, 3, TimeUnit.Years),
          Double.NaN, 0.0, OptionStyle.European, 0)
      };

      loan0.CallSchedule = defaultCallSchedule;
      var pricer1 = GetLoanPricer(asOf, settle, loan0);
      var yieldToWorst1 = pricer1.YieldToWorst();
      Assert.AreEqual(yieldToWorst, yieldToWorst1, 1E-15);

      //only one call period and it is before the settle date, so we expect it uses
      //the default call schedule.
      var callSchedule2 = new List<CallPeriod>
      {
        new CallPeriod(Dt.Add(settle, -30, TimeUnit.Days),
          Dt.Add(settle, -30, TimeUnit.Days), 1.0, 0.0, OptionStyle.European, 0),
      };

      loan0.CallSchedule = callSchedule2;

      var pricer2 = GetLoanPricer(asOf, settle, loan0);

      var yieldToWorst2 = pricer2.YieldToWorst();
      Assert.AreEqual(yieldToWorst1, yieldToWorst2, 1E-15);
    }



    [Test]
    public void TestLoanYieldToWorst1()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = asOf + 1;
      Loan loan0 = NewLoan("", 0.08, new string[] { "I" }, new double[] { 0.08 });
      //only one call period and it is before the settle date, so we expect it uses
      //the default call schedule.
      //only one call schedule(2Y) and it is after the settle date before the 3 year.
      //so here we expect the call schedule is 2Y and 3Y.
      var callSchedule3 = new List<CallPeriod>
      {
        new CallPeriod(Dt.Add(settle, 2, TimeUnit.Years), 
          Dt.Add(settle, 2, TimeUnit.Years), 1.0, 0.0, OptionStyle.European, 0)
      };

      loan0.CallSchedule = callSchedule3;
      var pricer3 = GetLoanPricer(asOf, settle, loan0);
      var yieldToWorst3 = pricer3.YieldToWorst();
      var yieldToWorst33 = Math.Min(pricer3.YieldToCall(
          Dt.Add(settle, 2, TimeUnit.Years), 1.0),
        pricer3.YieldToThreeYearCall());

      Assert.AreEqual(yieldToWorst3, yieldToWorst33, 1E-15);
    }

    [Test]
    public void TestLoanYieldToWorst2()
    {
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = asOf + 1;
      Loan loan0 = NewLoan("", 0.08, new [] { "I" }, new [] { 0.08 });

      const double callPrice = 95.0;
      var callSchedule4 = new List<CallPeriod>
      {
        new CallPeriod(Dt.Add(settle, 4, TimeUnit.Years),
          Dt.Add(settle, 4, TimeUnit.Years),
          callPrice, 0.0, OptionStyle.European, 0),
      };

      loan0.CallSchedule = callSchedule4;
      var pricer4 = GetLoanPricer(asOf, settle, loan0);
      var yieldToWorst4 = pricer4.YieldToWorst();
      var yieldToWorst44 = Math.Min(pricer4.YieldToCall(
          Dt.Add(settle, 4, TimeUnit.Years), callPrice),
        pricer4.YieldToMaturity());

      Assert.AreEqual(yieldToWorst4, yieldToWorst44, 1E-15);
    }


    [Test]
    public void TestLoanYieldToWorst3()
    {
      //it will filter out all the input schedule, and uses T+30, 1Y, 3Y.
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = asOf + 1;
      Loan loan0 = NewLoan("", 0.08, new string[] {"I"}, new double[] {0.08});
      var callSchedule5 = new List<CallPeriod>
      {
        new CallPeriod(Dt.Add(settle, -2, TimeUnit.Days),
          Dt.Add(settle, -2, TimeUnit.Days),
          Double.NaN, 0.0, OptionStyle.European, 0),
        new CallPeriod(Dt.Add(settle, 10, TimeUnit.Days),
          Dt.Add(settle, 10, TimeUnit.Days),
          Double.NaN, 0.0, OptionStyle.European, 0),
        new CallPeriod(Dt.Add(settle, 40, TimeUnit.Days),
          Dt.Add(settle, 40, TimeUnit.Days),
          Double.NaN, 0.0, OptionStyle.European, 0),
      };

      loan0.CallSchedule = callSchedule5;
      var pricer5 = GetLoanPricer(asOf, settle, loan0);

      var yieldToWorst5 = pricer5.YieldToWorst();
      var yieldToWorst55 = Math.Min(pricer5.YieldToTwoYearCall(),
        pricer5.YieldToThreeYearCall());
      yieldToWorst55 = Math.Min(yieldToWorst55,
        pricer5.YieldToCall(Dt.Add(settle, 30, TimeUnit.Days), double.NaN));

      Assert.AreEqual(yieldToWorst5, yieldToWorst55, 1E-15);
    }

    [Test]
    public void TestLoanYieldToWorst4()
    {
      //it will filter out all the input schedule, and uses T+30, 1Y, 3Y.
      Dt asOf = new Dt(20, 9, 2007);
      Dt settle = asOf + 1;
      Loan loan0 = NewLoan("", 0.08, new string[] {"I"}, new double[] {0.08});
      var callDate1 = Dt.Add(settle, -3, TimeUnit.Years);
      var callDate2 = Dt.Add(settle, -2, TimeUnit.Years);
      var callDate3 = Dt.Add(settle, -1, TimeUnit.Years);
      var callSchedule6 = new List<CallPeriod>
      {
        new CallPeriod(callDate1, callDate1, 1.02, 0.0, OptionStyle.European, 0),
        new CallPeriod(callDate2, callDate2, 1.01, 0.0, OptionStyle.European, 0),
        new CallPeriod(callDate3, callDate3, 1.0, 0.0, OptionStyle.European, 0),
      };

      loan0.CallSchedule = callSchedule6;
      var pricer6 = GetLoanPricer(asOf, settle, loan0);

      var yieldToWorst6 = pricer6.YieldToWorst();
      var yieldToWorst66 = pricer6.YieldToCall(Dt.Add(settle, 30, TimeUnit.Days), 1.0);

      Assert.AreEqual(yieldToWorst6, yieldToWorst66, 1E-15);
    }


    private LoanPricer GetLoanPricer(Dt asOf, Dt settle, Loan loan, double price = 1.0)
    {
      DiscountCurve indexCurve = new DiscountCurve(asOf, 0.03);
      return LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve,
        null, null, 0.4, null, 0, price, CalibrationType.SurvivalCurve, 0.0);
    }



    #region Survival Curve Calibration
    /// <summary>
    /// Tests a loan pricing using a SurvivalCurve calibrated off a Loan Price against a spread calibrated SurvivalCurve.
    /// </summary>
    [Test]
    public void FlatSurvivalCurveCalibration()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recoveryRate = 0.4;
      double price = 1.0;

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 300, new string[] { "CCC", "BBB", "BB", "A", "AA", "AAA" }, new double[] { 0.06, 0.05, 0.04, 0.03, 0.02, 0.01 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);

      // Setup calibrated curve pricer
      LoanPricer calibratedPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve,
                                                          null, recoveryRate, null, 0, price, CalibrationType.SurvivalCurve, 
                                                          "A", null, null, 0.08);

      // Setup fit pricer
      CDS cds = (CDS)calibratedPricer.SurvivalCurve.Tenors[0].Product;
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, cds.Premium, 5, recoveryRate, indexCurve);
      LoanPricer curvePricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve,
                                                     recoveryRate, null, 0, price, CalibrationType.None, "A", null, null, 0.08);

      // Test
      Assert.AreEqual(curvePricer.Pv(), calibratedPricer.Pv(), "Pv");
    }

    /// <summary>
    /// Tests a partially drawn loan pricing using a SurvivalCurve calibrated off a Loan Price 
    /// against a spread calibrated SurvivalCurve, for a Revolver loan
    /// </summary>
    [Test]
    public void FlatSurvivalCurveCalibrationPartialDraw()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recoveryRate = 0.4;
      double price = 1.0;

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 300, new string[] { "CCC", "BBB", "BB", "A", "AA", "AAA" }, new double[] { 0.06, 0.05, 0.04, 0.03, 0.02, 0.01 });
      loan.LoanType = LoanType.Revolver;

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);

      // Setup calibrated curve pricer
      LoanPricer calibratedPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 700000, indexCurve, indexCurve,
                                                          null,recoveryRate, null, 0, price, CalibrationType.SurvivalCurve,
                                                          "A", null, null, 0.08);

      // Setup fit pricer
      CDS cds = (CDS)calibratedPricer.SurvivalCurve.Tenors[0].Product;
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, cds.Premium, 5, recoveryRate, indexCurve);
      LoanPricer curvePricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 700000, indexCurve, indexCurve, survCurve,
                                                     recoveryRate, null, 0, price, CalibrationType.None, "A", null, null, 0.08);

      // Test
      Assert.AreEqual(curvePricer.Pv(), calibratedPricer.Pv(), "Pv");
    }

    /// <summary>
    /// Tests a partially drawn loan pricing using a SurvivalCurve calibrated off a Loan Price 
    /// against a spread calibrated SurvivalCurve, for a Term Loan
    /// </summary>
    [Test]
    public void FlatSurvivalCurveCalibrationPartialDrawTermLoan()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recoveryRate = 0.4;
      double price = 1.0;

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 300, new string[] { "CCC", "BBB", "BB", "A", "AA", "AAA" }, new double[] { 0.06, 0.05, 0.04, 0.03, 0.02, 0.01 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);

      // Setup calibrated curve pricer
      LoanPricer calibratedPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve,
                                                          null, recoveryRate, null, 0, price, CalibrationType.SurvivalCurve,
                                                          "A", null, null, 0.08);

      // Setup fit pricer
      CDS cds = (CDS)calibratedPricer.SurvivalCurve.Tenors[0].Product;
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, cds.Premium, 5, recoveryRate, indexCurve);
      LoanPricer curvePricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve,
                                                     recoveryRate, null, 0, price, CalibrationType.None, "A", null, null, 0.08);

      // Test
      Assert.AreEqual(curvePricer.Pv(), calibratedPricer.Pv(), "Pv");
    }

    #endregion

    /// <summary>
    /// Tests the event sensitivity for Loans - ie JTD.
    /// </summary>
    [Test]
    public void JTD()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recovRate = 0.4;
      double notional = 1000000;

      // Products
      Loan floatingLoan = this.NewLoan("USDLIBOR", 0.03, new string[] { "I" }, new double[] { 0.03 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, floatingLoan.DayCount, floatingLoan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, recovRate, indexCurve);
      LoanPricer floatingPricer = LoanPricerFactory.New(floatingLoan, asOf, settle, notional, notional, indexCurve, indexCurve, survCurve, recovRate, null, 0, 1, CalibrationType.None, 0.08);

      // Calculate approximate value
      double dRecov = indexCurve.DiscountFactor(asOf, settle) * (recovRate * notional) - floatingPricer.Pv();

      // Test
      Assert.AreEqual(dRecov, Toolkit.Sensitivity.Sensitivities.VOD(floatingPricer), 
        (1+Math.Abs(dRecov))*1E-15, "JTD");
    }
    /// <summary>
    /// Test for solving Implied CDS Spreads against distressed loan prices. 
    /// </summary>
    /// 
    [Test, Smoke]
    public void DistressedLoanImpliedCDSSpreadTest()
    {
      Dt asOf = new Dt(23, 6, 2008);
      Dt settle = new Dt(24, 6, 2008);
      DiscountCurve dc = new DiscountCurve(asOf, 0.03);
      Loan bond = new Loan();

      bond.BDConvention = BDConvention.None;
      bond.Ccy = Currency.USD;
      bond.Calendar = Calendar.NYB;
      bond.DayCount = DayCount.Actual360;
      bond.Effective = new Dt(15, 2, 2007);
      bond.CycleRule = CycleRule.None;
      bond.FirstCoupon = new Dt(16, 5, 2007);
      bond.Frequency = Frequency.Quarterly;
      bond.Index = "LIBOR";
      bond.LoanType = LoanType.Term;
      bond.Maturity = new Dt(26, 5, 2014);
      bond.PerformanceLevels = new []{Loan.DefaultPerformanceLevel};
      bond.PeriodAdjustment = false;
      bond.PricingGrid.Add(Loan.DefaultPerformanceLevel, 0.0235);

      // Create Pricer.
      LoanPricer pricer = LoanPricerFactory.New(bond, asOf, settle, 1000000, 1000000, dc, dc, 
        null, 0.7, null, 0, 0.705, CalibrationType.SurvivalCurve, 0.0535);

      // Test
      Assert.IsNotNull(pricer.SurvivalCurve, "Implied Curve was NULL!");
    }
    #endregion Tests

    #region Recovery Tests
    /// <summary>
    /// Tests the recovery sensitivity of a Loan Pricer that is dependent upon 2 different recovery curves.
    /// </summary>
    [Test]
    public void Recovery01SeperateRecoveryCurvesTest()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recovRate = 0.4;
      double notional = 1000000;

      // Products
      Loan floatingLoan = this.NewLoan("USDLIBOR", 0.03, new string[] { "I" }, new double[] { 0.03 });

      // Setup pricer for sensitivities routine
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, floatingLoan.DayCount, floatingLoan.Frequency, 0.05);
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, recovRate, indexCurve);
      LoanPricer floatingPricer = LoanPricerFactory.New(floatingLoan, asOf, settle, notional, notional, indexCurve, indexCurve, survCurve, recovRate, null, 0, 1, CalibrationType.None, 0.08);
      double basePv = floatingPricer.Pv();

      // Setup manually bumped pricer
      recovRate = 0.41;
      SurvivalCurve bumpedSurvCurve = this.NewSurvivalCurve(asOf, settle, 0.03, 5, recovRate, indexCurve);
      LoanPricer bumpedPricer = LoanPricerFactory.New(floatingLoan, asOf, settle, notional, notional, indexCurve, indexCurve, bumpedSurvCurve, recovRate, null, 0, 1.0, CalibrationType.None, 0.08);
      double bumpedPv = bumpedPricer.Pv();

      // Generic sensitivity
      double dR = Toolkit.Sensitivity.Sensitivities.Recovery01(floatingPricer, 0.01, 0, true);

      // Test
      Assert.AreEqual(dR, bumpedPv - basePv, 0.000001,
                      "The manually calculated delta does not match the Sensitivities' routine!");
    }

    /// <summary>
    /// Tests the recovery sensitivity of a Loan Pricer that is dependent upon a single recovery curve.
    /// </summary>
    [Test]
    public void Recovery01SingleRecoveryCurveTest()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recovRate = 0.4;
      double notional = 1000000;

      // Products
      Loan floatingLoan = this.NewLoan("USDLIBOR", 0.03, new string[] { "I" }, new double[] { 0.03 });

      // Setup pricer for sensitivities routine
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, floatingLoan.DayCount, floatingLoan.Frequency, 0.05);
      LoanPricer floatingPricer = LoanPricerFactory.New(floatingLoan, asOf, settle, notional, notional, indexCurve, indexCurve, null, recovRate, null, 0, 1, CalibrationType.SurvivalCurve, 0.08);
      floatingPricer.InterestPeriods.Add(InterestPeriodUtil.DefaultInterestPeriod(asOf, floatingLoan, 0.08, 1.0));
      double basePv = floatingPricer.Pv();

      // Setup manually bumped pricer
      LoanPricer bumpedPricer = LoanPricerFactory.New(floatingLoan, asOf, settle, notional, notional, indexCurve, indexCurve, null, recovRate, null, 0, 1.0, CalibrationType.SurvivalCurve, 0.08);
      bumpedPricer.InterestPeriods.Add(InterestPeriodUtil.DefaultInterestPeriod(asOf, floatingLoan, 0.08, 1.0));
      bumpedPricer.RecoveryCurve.SetVal(0, 0.41);
      bumpedPricer.SurvivalCurve.Fit();

      // Generic sensitivity
      double dR = Toolkit.Sensitivity.Sensitivities.Recovery01(floatingPricer, 0.01, 0, true);

      // Calculate delta manually
      double bumpedPv = bumpedPricer.Pv();

      // Test
      Assert.AreEqual(dR, bumpedPv - basePv, 0.000001,
                      "The manually calculated delta does not match the Sensitivities' routine!");
    }

    #endregion

    #region Model Calibration Tests
    /// <summary>
    /// Tests a Loan using the Implied CDS Curve against a market CDS Curve calibrated to the market price.
    /// </summary>
    [Test]
    public void HSpreadCalibratedModelVsImpliedCDSCurve()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recoveryRate = 0.4;
      double price = 1.0;

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 300, new string[] { "CCC", "BBB", "BB", "A", "AA", "AAA" }, new double[] { 0.06, 0.05, 0.04, 0.03, 0.02, 0.01 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);

      // Setup calibrated curve pricer
      LoanPricer impliedCDSPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve,
                                                          null, recoveryRate, null, 0, price, CalibrationType.SurvivalCurve,
                                                          "A", null, null, 0.08);

      // Setup fit pricer
      CDS cds = (CDS)impliedCDSPricer.SurvivalCurve.Tenors[0].Product;
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, cds.Premium, 5, recoveryRate, indexCurve);
      LoanPricer calibratedPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve,
                                                     recoveryRate, null, 0, price, CalibrationType.SurvivalCurve, "A", null, null, 0.08);

      // Test
      Assert.Less(Math.Abs(calibratedPricer.HSpread()), 1e-6, "HSpread");
      Assert.AreEqual(calibratedPricer.Pv(), impliedCDSPricer.Pv(), calibratedPricer.Pv()*1e-6, "Pv");
    }

    /// <summary>
    /// Tests a Loan using the Implied CDS Curve against a market CDS Curve calibrated to the market price.
    /// </summary>
    [Test]
    public void RSpreadCalibratedModelVsImpliedCDSCurve()
    {
      Dt asOf = new Dt(21, 9, 2007);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      double recoveryRate = 0.4;
      double price = 0.9;

      // Products
      Loan loan = this.NewLoan("USDLIBOR", 300, new string[] { "CCC", "BBB", "BB", "A", "AA", "AAA" }, new double[] { 0.06, 0.05, 0.04, 0.03, 0.02, 0.01 });

      // Flat forward curve
      DiscountCurve indexCurve = NewDiscountCurve(asOf, settle, loan.DayCount, loan.Frequency, 0.05);

      // Setup calibrated curve pricer
      LoanPricer impliedCDSPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve,
                                                          null, recoveryRate, null, 0, price, CalibrationType.SurvivalCurve,
                                                          "A", null, null, 0.08);

      // Setup fit pricer
      CDS cds = (CDS)impliedCDSPricer.SurvivalCurve.Tenors[0].Product;
      SurvivalCurve survCurve = this.NewSurvivalCurve(asOf, settle, cds.Premium, 5, recoveryRate, indexCurve);
      LoanPricer calibratedPricer = LoanPricerFactory.New(loan, asOf, settle, 1000000, 1000000, indexCurve, indexCurve, survCurve,
                                                     recoveryRate, null, 0, price, CalibrationType.DiscountCurve, "A", null, null, 0.08);

      // Test
      Assert.Less(Math.Abs(calibratedPricer.RSpread()), 1e-4, "RSpread");
      Assert.AreEqual(calibratedPricer.Pv(), impliedCDSPricer.Pv(), calibratedPricer.Pv()*1.5e-4, "Pv");
    }
    #endregion

    #region Private Methods

    /// <summary>
    /// Create a flat forward rate discount curve.
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="dc"></param>
    /// <param name="freq"></param>
    /// <param name="fwdRate"></param>
    /// <returns></returns>
    private DiscountCurve NewDiscountCurve(Dt asOf, Dt settle, DayCount dc, Frequency freq, double fwdRate)
    {
      DiscountBootstrapCalibrator cal = new DiscountBootstrapCalibrator(asOf, settle);
      Interp interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const);
      DiscountCurve discCurve = new DiscountCurve(cal, interp, dc, freq);

      // Add rate
      //discCurve.AddMoneyMarket("1 Years", Dt.Add(asOf, 1, TimeUnit.Years), fwdRate, dc);
      discCurve.AddSwap("30 Years", Dt.Add(asOf, 2, TimeUnit.Years), fwdRate, dc, freq, BDConvention.None, Calendar.None);

      // Fit
      discCurve.Fit();

      // Done
      return discCurve;
    }

    /// <summary>
    /// Calibrate a flat hazard rate survival curve from a single spread quote.
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="spread"></param>
    /// <returns></returns>
    private SurvivalCurve NewSurvivalCurve(Dt asOf, Dt settle, double spread, int yr, double recovery, DiscountCurve dc)
    {
      SurvivalFitCalibrator cal = new SurvivalFitCalibrator(asOf, settle, recovery, dc);
      SurvivalCurve sc = new SurvivalCurve(cal);
      Dt maturity = Dt.CDSMaturity(asOf, yr, TimeUnit.Years);
      sc.AddCDS(maturity, spread, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.None);
      sc.Fit();
      return sc;
    }

    /// <summary>
    /// Create a new Loan.
    /// </summary>
    /// <returns></returns>
    private Loan NewLoan(string index, double spread, string[] lvls, double[] cpns)
    {
      Loan l = new Loan();
      l.BDConvention = BDConvention.Following;
      l.Calendar = Calendar.NYB;
      l.Ccy = Toolkit.Base.Currency.USD;
      l.CommitmentFee = 0.0;
      l.DayCount = DayCount.Actual365Fixed;
      l.Description = (StringUtil.HasValue(index) ? "Floating" : "Fixed") + " Loan";
      l.Effective = new Dt(20, 4, 2007);
      l.CycleRule = CycleRule.None;
      l.FirstCoupon = new Dt(1, 7, 2007);
      l.Frequency = Frequency.Quarterly;
      l.Index = index;
      l.LastCoupon = new Dt(1, 4, 2014);
      l.LastDraw = new Dt(20, 4, 2008);
      l.LoanType = LoanType.Term;
      l.Maturity = new Dt(20, 4, 2014);
      l.Notional = 1.0;
      l.PeriodAdjustment = false;

      //setup performance pricing
      if (!ArrayUtil.HasValue(lvls))
      {
        l.PerformanceLevels = new string[] { Loan.DefaultPerformanceLevel };
        l.PricingGrid.Add(Loan.DefaultPerformanceLevel, spread);
      }
      else
      {
        l.PerformanceLevels = lvls;
        for (int i = 0; i < lvls.Length; i++)
          l.PricingGrid.Add(lvls[i], cpns[i]);
      }

      return l;
    }

    /// <summary>
    /// Create a new Bond.
    /// </summary>
    /// <returns></returns>
    private Bond NewBond(string index, double spread)
    {
      Bond l = new Bond(
        new Dt(20, 4, 2007),
        new Dt(20, 4, 2014),
        Currency.USD,
        BondType.USCorp,
        spread,
        DayCount.Actual365Fixed,
        CycleRule.None,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);

      l.Description = (StringUtil.HasValue(index) ? "Floating" : "Fixed") + " Loan";
      l.FirstCoupon = new Dt(1, 7, 2007);
      l.Index = index;
      l.LastCoupon = new Dt(1, 4, 2014);
      l.Notional = 1.0;
      l.PeriodAdjustment = false;

      return l;
    }
    #endregion Private Methods
  }
}
