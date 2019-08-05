//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  /// Test Loan model.
  /// </summary>
  [TestFixture]
  public class TestLoanModel : ToolkitTestBase
  {
    #region Test Distributions

    [Test]
    public void HighSpread()
    {
      const double hazardRate = 0.7;
      double[] endDistributions = new double[] { 0.1, 0.15, 0.55, 0.20 };
      Dt start = Dt.Today();
      for (int d = 0; d < 24; ++d)
      {
        Dt today = Dt.Add(start, d*30);
        SurvivalCurve curve = new SurvivalCurve(today, hazardRate);

        // Without prepay curve
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, null, endDistributions, startRating);

        // With prepay curve
        SurvivalCurve prepay = new SurvivalCurve(today, 0.0);
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, prepay, endDistributions, startRating);
      }
      return;
    }

    [Test]
    public void LowSpread()
    {
      const double hazardRate = 0.01;
      double[] endDistributions = new double[] { 0.1, 0.15, 0.55, 0.20 };
      Dt start = Dt.Today();
      for (int d = 0; d < 24; ++d)
      {
        Dt today = Dt.Add(start, d*30);
        SurvivalCurve curve = new SurvivalCurve(today, hazardRate);

        // Without prepay curve
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, null, endDistributions, startRating);

        // With prepay curve
        SurvivalCurve prepay = new SurvivalCurve(today, 0.05);
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, prepay, endDistributions, startRating);
      }
      return;
    }

    [Test]
    public void ZeroSpread()
    {
      const double hazardRate = 0.0;
      double[] endDistributions = new double[] { 0.1, 0.15, 0.55, 0.20 };
      Dt start = Dt.Today();
      for (int d = 0; d < 24; ++d)
      {
        Dt today = Dt.Add(start, d*30);
        SurvivalCurve curve = new SurvivalCurve(today, hazardRate);

        // Without prepay curve
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, null, endDistributions, startRating);

        // With prepay curve
        SurvivalCurve prepay = new SurvivalCurve(today, 0.1);
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, prepay, endDistributions, startRating);
      }
      return;
    }

    [Test]
    public void MiddleSpread()
    {
      const double hazardRate = 0.2;
      double[] endDistributions = new double[] { 0.1, 0.15, 0.55, 0.20 };
      Dt start = Dt.Today();
      for (int d = 0; d < 24; ++d)
      {
        Dt today = Dt.Add(start, d*30);
        SurvivalCurve curve = new SurvivalCurve(today, hazardRate);

        // Without prepay curve
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, null, endDistributions, startRating);

        // With prepay curve
        SurvivalCurve prepay = new SurvivalCurve(today, 0.01);
        for (int startRating = 0; startRating < endDistributions.Length;
          ++startRating)
          TestDistributions(today, curve, prepay, endDistributions, startRating);
      }
      return;
    }

    [Test]
    public void HighSurvivalAndPrepayment()
    {
      const double hazardRate = 0.041531861457047257;
      double[] endDistributions = new double[] {0.16, 0.17, 0.17, 0.17, 0.16};

      Dt start = Dt.Today();
      for (int d = 0; d < 24; ++d)
      {
        Dt today = Dt.Add(start, d*30);
        SurvivalCurve curve = new SurvivalCurve(today, hazardRate);
        SurvivalCurve prepay = new SurvivalCurve(today, 0.266376921469414);

        // Test Distribution
        TestDistributions(today, curve, prepay, endDistributions, 2);
      }
      return;
    }

    [Test]
    public void InvalidTransition()
    {
      double[] hazardRates = {0.0, 0.01};
      double[] endDistributions = new double[] {0, 0, 0, 0};
      Dt start = Dt.Today();
      for (int d = 0; d < 12; ++d)
      {
        Dt today = Dt.Add(start, d*30);
        SurvivalCurve prepay = null;
        foreach (double h in hazardRates)
        {
          SurvivalCurve curve = new SurvivalCurve(today, h);
          for (int endRating = 0; endRating < endDistributions.Length;
            ++endRating)
          {
            endDistributions[endRating] = 1.0;
            for (int startRating = 0; startRating < endDistributions.Length;
              ++startRating)
            {
              bool expectException = (h != 0.0 || startRating != endRating);
              string msg = "Exception";
              bool gotException = false;
              try
              {
                TestDistributions(today, curve, prepay, endDistributions,
                  startRating);
              }
              catch (ToolkitException e)
              {
                msg = e.Message;
                gotException = true;
              }
              if (!expectException)
              {
                // It is possible that the starting rating ends with a probability
                // close to zero.  Hence we only test the cases where no exception
                // is expected.
                Assert.AreEqual(expectException, gotException, msg);
              }
            }
            endDistributions[endRating] = 0.0;
          }
        }
      }
      return;
    }

    public static void TestDistributions(
      Dt today,
      SurvivalCurve survivalCurve,
      SurvivalCurve prepayCurve,
      double[] endDistributions,
      int startRating)
    {
      Dt maturity = Dt.Add(today, 5, TimeUnit.Years);
      Dt[] dates = CreateDateArray(today, maturity, 3, TimeUnit.Months);

      var distribution = LoanModel.StateDistribution.Calculate(
        today, survivalCurve, prepayCurve, null, 
        dates, endDistributions, startRating);

      double[] p0 = StateProbabilities(0, distribution);
      CheckDistribution("Prob@" + 0, p0);

      int lastdate = dates.Length - 1;
      for (int t = 0; t < lastdate; ++t)
      {
        double[,] A = TransitionMatrix(t, distribution);
        CheckTransition("Tran@" + t, A);
        double[] x = NextDistribution(p0, A);
        double[] p1 = StateProbabilities(t + 1, distribution);
        CheckDistribution("Prob@" + t, p1);
        CheckEquality("Next@" + t, p1, x);
        p0 = p1;
      }

      return;
    }

    #endregion Test Distributions

    #region Test pricing

    [Test]
    public void PvWithPricingGrid()
    {
      double recoveryRate = 0.4;
      double coupon = 0.0125;
      Dt today = Dt.Today();
      DiscountCurve discountCurve = new DiscountCurve(today, 0.05);
      SurvivalCurve survivalCurve = new SurvivalCurve(today, 0.1);
      RecoveryCurve recoveryCurve = new RecoveryCurve(today, recoveryRate);
      double[] endDistributions = new double[] { 0.16, 0.17, 0.17, 0.17, 0.17, 0.16 };
      int startRating = 3;

      Dt maturity = Dt.Add(today, 5, TimeUnit.Years);
      Dt[] dates = CreateDateArray(today, maturity, 3, TimeUnit.Months);

      var distribution = LoanModel.StateDistribution.Calculate(
        today, survivalCurve, null, null, dates, endDistributions, startRating);
      double[][,] A = new double[dates.Length][,];
      for (int t = 0; t < dates.Length - 1; ++t)
        A[t] = TransitionMatrix(t, distribution);

      var prices = LoanModel.Recursive(
        (int t, int i) => coupon, null, (int t, int i) => 1.0,
        discountCurve, recoveryCurve, 1.0, distribution, false, true);

      // Check price at starting index
      Assert.IsFalse(double.IsNaN(prices[0, startRating]));
    }

    [Test]
    public void SimplePv()
    {
      double recoveryRate = 0.4;
      double coupon = 0.0125;
      Dt today = Dt.Today();
      DiscountCurve discountCurve = new DiscountCurve(today, 0.05);
      SurvivalCurve survivalCurve = new SurvivalCurve(today, 0.1);
      RecoveryCurve recoveryCurve = new RecoveryCurve(today, recoveryRate);
      double[] endDistributions = new double[] { 1.0 };
      int startRating = 0;

      Dt maturity = Dt.Add(today, 1, TimeUnit.Years);
      Dt[] dates = CreateDateArray(today, maturity, 3, TimeUnit.Months);

      double pv = 0;
      for (int t = 1; t < dates.Length; ++t)
      {
        double df = discountCurve.DiscountFactor(today, dates[t]);
        double sp = survivalCurve.Interpolate(today, dates[t]);
        pv += sp * df * coupon;
        double dp = survivalCurve.Interpolate(today, dates[t - 1]) - sp;
        Dt dfltDate = new Dt((dates[t - 1].ToDouble()
          + dates[t].ToDouble()) / 2);
        pv += dp * recoveryRate * discountCurve.DiscountFactor(today, dfltDate);
        if (t == dates.Length - 1)
          pv += sp * df;
      }

      var distribution = LoanModel.StateDistribution.Calculate(
        today, survivalCurve, null, null, dates, endDistributions, startRating);
      var prices = LoanModel.Recursive(
        (int t, int i) => coupon, null, (int t, int i) => 1.0,
        discountCurve, recoveryCurve, 1.0, distribution, false, true);

      Assert.AreEqual(pv, prices[0, distribution.StartState], 1E-15, "Price");
      return;
    }

    /// <summary>
    /// Prices a Loan that should be valued at par. 
    /// </summary>
    [Test]
    public void ParPv()
    {
      var coupon = 0.05;
      var recoveryRate = 0.4;
      var today = new Dt(21, 3, 2009);
      var discountCurve = new DiscountCurve(today, 0.01);
      var endDistributions = new double[] { 1.0 };
      var maturity = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var usage = new double[] { 1.0 };
      var levels = new string[] { "I" };
      var effective = Dt.Subtract(today, Frequency.Annual, false);
      var terms = new ScheduleParams(
        effective, Dt.Empty, Dt.Empty, maturity, Frequency.Quarterly, BDConvention.None, Calendar.NYB, CycleRule.None,
        CashflowFlag.None);
      var intper = InterestPeriodUtil.DefaultInterestPeriod(today, terms, DayCount.Actual360, 0.06, 1.0);
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>() { intper };

      // Setup survival curve
      var calibrator = new SurvivalFitCalibrator(today, today, recoveryRate, discountCurve);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(maturity, coupon, DayCount.Actual360, Frequency.Quarterly, BDConvention.None, Calendar.NYB);
      survivalCurve.Fit();
      var recoveryCurve = calibrator.RecoveryCurve;
      coupon = CurveUtil.ImpliedSpread(survivalCurve, maturity);
      var pricingGrid = new Dictionary<string, double>() { { "I", coupon } };

      // Calculate PV
      var pv = LoanModel.Pv(today, today, terms, DayCount.Actual360, true, "I", 0,
                                        levels, pricingGrid, 0, usage, endDistributions, discountCurve,
                                        discountCurve, survivalCurve,
                                        recoveryCurve, null, false, false, null, 0, null, interestPeriods, CalibrationType.None, 0, null);
      var accrued = LoanModel.Accrued(today, terms, DayCount.Actual360, true, 0.05, 0, interestPeriods);

      // Test
      Assert.AreEqual(1.0, pv - accrued, 0.01, "The Loan should be priced around par!");
    }
    #endregion Test pricing

    #region Test Solvers
    /// <summary>
    /// Calculates the implied discount spread then calculates the price using the spread vs. the expected price.
    /// </summary>
    [Test]
    public void ImpliedDiscountSpread()
    {
      var recoveryRate = 0.4;
      var coupon = 0.05;
      var today = new Dt(21, 3, 2009);
      var discountCurve = new DiscountCurve(today, 0.01);
      var referenceCurve = new DiscountCurve(today, 0.01);
      var endDistributions = new double[] { 1.0 };
      var maturity = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var usage = new double[] { 1.0 };
      var levels = new string[] { "I" };
      var effective = Dt.Subtract(today, Frequency.Annual, false);
      var pricingGrid = new Dictionary<string, double>() { { "I", coupon } };
      ScheduleParams terms = new ScheduleParams(effective, Dt.Empty, Dt.Empty, maturity, Frequency.Quarterly,
                                                BDConvention.None,
                                                Calendar.NYB, CycleRule.None, CashflowFlag.None);
      var intper = InterestPeriodUtil.DefaultInterestPeriod(today, terms, DayCount.Actual360, 0.06, 1.0);
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>() { intper };

      // Setup survival curve
      var calibrator = new SurvivalFitCalibrator(today, today, recoveryRate, discountCurve);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(maturity, coupon, DayCount.Actual360, Frequency.Quarterly, BDConvention.None, Calendar.NYB);
      survivalCurve.Fit();
      var recoveryCurve = calibrator.RecoveryCurve;

      // Calculate Implied Discount Spread
      var spread = LoanModel.ImpliedDiscountSpread(today, today, terms, DayCount.Actual360, discountCurve, true,
                                        referenceCurve, survivalCurve, recoveryCurve, false, false, null, null, 0, "I", 0, levels,
                                        pricingGrid, 0, usage, endDistributions, null, interestPeriods, 0.9, true, -1, (double[])null, null);

      // Set spread on discount curve
      discountCurve.Spread = spread;

      // Calc pv
      var pv = LoanModel.Pv(today, today, terms, DayCount.Actual360, true, "I", 0,
                                  levels, pricingGrid, 0, usage, endDistributions, discountCurve,
                                  referenceCurve, survivalCurve,
                                  recoveryCurve, null, false, false, null, 0, null, interestPeriods, CalibrationType.None, 0, true, -1, null, null);

      // Test
      Assert.AreEqual(0.9, pv, 0.0001, "The price with the implied spread does not match the expected!");
    }

    /// <summary>
    /// Calculates the implied survival spread then calculates the price using the spread vs. the expected price.
    /// </summary>
    [Test]
    public void ImpliedSurvivalSpread()
    {
      var recoveryRate = 0.4;
      var coupon = 0.05;
      var today = new Dt(21, 3, 2009);
      var discountCurve = new DiscountCurve(today, 0.01);
      var referenceCurve = new DiscountCurve(today, 0.01);
      var endDistributions = new double[] { 1.0 };
      var maturity = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var usage = new double[] { 1.0 };
      var levels = new string[] { "I" };
      var effective = Dt.Subtract(today, Frequency.Annual, false);
      var pricingGrid = new Dictionary<string, double>() { { "I", coupon } };
      ScheduleParams terms = new ScheduleParams(effective, Dt.Empty, Dt.Empty, maturity, Frequency.Quarterly,
                                                BDConvention.None,
                                                Calendar.NYB, CycleRule.None, CashflowFlag.None);
      var intper = InterestPeriodUtil.DefaultInterestPeriod(today, terms, DayCount.Actual360, 0.06, 1.0);
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>() { intper };

      // Setup survival curve
      var calibrator = new SurvivalFitCalibrator(today, today, recoveryRate, discountCurve);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(maturity, coupon, DayCount.Actual360, Frequency.Quarterly, BDConvention.None, Calendar.NYB);
      survivalCurve.Fit();
      var recoveryCurve = calibrator.RecoveryCurve;

      // Calculate Implied Discount Spread
      var spread = LoanModel.ImpliedCreditSpread(today, today, terms, DayCount.Actual360, discountCurve, true,
                                        referenceCurve, survivalCurve, recoveryCurve, false, false, null, null, 0, "I", 0, levels,
                                        pricingGrid, 0, usage, endDistributions, null, interestPeriods, 0.9, true, -1, null, null);

      // Set spread on discount curve
      survivalCurve.Spread = spread;

      // Calc pv
      var pv = LoanModel.Pv(today, today, terms, DayCount.Actual360, true, "I", 0,
                                  levels, pricingGrid, 0, usage, endDistributions, discountCurve,
                                  referenceCurve, survivalCurve,
                                  recoveryCurve, null, false, false, null, 0, null, interestPeriods, CalibrationType.None, 0, null);

      // Test
      Assert.AreEqual(0.9, pv, 0.0001, "The price with the implied spread does not match the expected!");
    }

    /// <summary>
    /// Calculates the flat survival curve implied by a price, then calculates the 
    /// model price using the implied curve vs. the expected price.
    /// </summary>
    [Test]
    public void ImpliedCDSCurve()
    {
      var recoveryRate = 0.4;
      var coupon = 0.05;
      var today = new Dt(21, 3, 2009);
      var discountCurve = new DiscountCurve(today, 0.01);
      var referenceCurve = new DiscountCurve(today, 0.01);
      var endDistributions = new double[] { 1.0 };
      var maturity = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var usage = new double[] { 1.0 };
      var levels = new string[] { "I" };
      var effective = Dt.Subtract(today, Frequency.Annual, false);
      var pricingGrid = new Dictionary<string, double>() { { "I", coupon } };
      ScheduleParams terms = new ScheduleParams(effective, Dt.Empty, Dt.Empty, maturity, Frequency.Quarterly,
                                                BDConvention.None,
                                                Calendar.NYB, CycleRule.None, CashflowFlag.None);
      var intper = InterestPeriodUtil.DefaultInterestPeriod(today, terms, DayCount.Actual360, 0.06, 1.0);
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>() { intper };

      // Setup survival curve
      var calibrator = new SurvivalFitCalibrator(today, today, recoveryRate, discountCurve);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(maturity, coupon, DayCount.Actual360, Frequency.Quarterly, BDConvention.None, Calendar.NYB);
      survivalCurve.Fit();
      var recoveryCurve = calibrator.RecoveryCurve;

      // Calculate Implied Discount Spread
      var sc = LoanModel.ImpliedCDSCurve(today, today, terms, DayCount.Actual360, discountCurve, true,
                                        referenceCurve, recoveryCurve, false, null, null, 0, "I", 0, levels,
                                        pricingGrid, 0, usage, endDistributions, null, interestPeriods, 0.9, null, null);

      // Calc pv
      var pv = LoanModel.Pv(today, today, terms, DayCount.Actual360, true, "I", 0,
                                  levels, pricingGrid, 0, usage, endDistributions, discountCurve,
                                  referenceCurve, sc,
                                  recoveryCurve, null, false, true, null, 0, null, interestPeriods, CalibrationType.None, 0, null);

      // Test
      Assert.AreEqual(0.9, pv, 0.0001, "The price with the implied curve does not match the expected!");
    }

    [Test]
    public void ImpliedCDSSpreadForDistressedLoanNearMaturity()
    {
      var recoveryRate = 0.7;
      var coupon = 0.02;
      var today = new Dt(1, 3, 2009);
      var discountCurve = new DiscountCurve(today, 0.05);
      var referenceCurve = new DiscountCurve(today, 0.05);
      var endDistributions = new double[] { 1.0 };
      var maturity = new Dt(30, 3, 2009);
      var roll = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var usage = new double[] { 1.0 };
      var levels = new string[] { "I" };
      var effective = new Dt(15, 2, 2007);
      var firstCpn = new Dt(16, 5, 2007);
      var pricingGrid = new Dictionary<string, double>() { { "I", coupon } };
      ScheduleParams terms = new ScheduleParams(effective, firstCpn, Dt.Empty, maturity, Frequency.Quarterly,
                                                BDConvention.None,
                                                Calendar.NYB, CycleRule.None, CashflowFlag.None);
      var intper = InterestPeriodUtil.DefaultInterestPeriod(today, terms, DayCount.Actual360, 0.0765, 1.0);
      IList<InterestPeriod> interestPeriods = new List<InterestPeriod>() { intper };

      // Setup survival curve
      var calibrator = new SurvivalFitCalibrator(today, today, recoveryRate, discountCurve);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(roll, 0, DayCount.Actual360, Frequency.Quarterly, BDConvention.None, Calendar.NYB);
      survivalCurve.Fit();
      var recoveryCurve = calibrator.RecoveryCurve;

      // Calculate Implied Discount Spread
      var spread = LoanModel.ImpliedCreditSpread(today, today, terms, DayCount.Actual360, discountCurve, true,
                                        referenceCurve, survivalCurve, recoveryCurve, false, false, null, null, 0, "I", 0, levels,
                                        pricingGrid, 0, usage, endDistributions, null, interestPeriods, 0.80, true, -1, null, null);

      // Set spread on discount curve
      survivalCurve.Spread = spread;

      // Calc pv
      var pv = LoanModel.Pv(today, today, terms, DayCount.Actual360, true, "I", 0,
                                  levels, pricingGrid, 0, usage, endDistributions, discountCurve,
                                  referenceCurve, survivalCurve,
                                  recoveryCurve, null, false, false, null, 0, null, interestPeriods, CalibrationType.None, 0, null);

      // Test
      Assert.AreEqual(0.80, pv, 0.0001, "The price with the implied spread does not match the expected!");
    }

    /// <summary>
    /// Calculates the flat prepayment curve implied by an E[WAL], then calculates the 
    /// E[WAL] using the implied curve vs. the target value.
    /// </summary>
    [Test]
    public void ImpliedPrepaymentCurve()
    {
      var today = new Dt(21, 3, 2009);
      var maturity = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var effective = Dt.Subtract(today, Frequency.Annual, false);

      // Schedule
      ScheduleParams terms = new ScheduleParams(effective, Dt.Empty, Dt.Empty, maturity, Frequency.Quarterly,
                                                BDConvention.None,
                                                Calendar.NYB, CycleRule.None, CashflowFlag.None);

      // Calculate Implied Discount Spread
      var pc = LoanModel.ImpliedPrepaymentCurve(today, today, terms, Currency.USD, DayCount.Actual360, null, 1.0, 3.25);
      
      // Calc wal
      var wal = LoanModel.ExpectedWAL(today, terms, Currency.USD, DayCount.Actual360, 1.0, null, pc);

      // Test
      Assert.AreEqual(3.25, wal, 0.0001, "The expected WAL did not match the target WAL!");
    }
    #endregion

    #region Test Expected WAL
    /// <summary>
    /// Calculates the expected weighted average life.
    /// </summary>
    [Test]
    public void ExpectedWAL()
    {
      var today = new Dt(21, 3, 2009);
      var maturity = Dt.CDSMaturity(today, 5, TimeUnit.Years);
      var effective = Dt.Subtract(today, Frequency.Annual, false);

      // Setup prepayment curve
      var prepaymentCurve = new SurvivalCurve(today, 0);

      // Schedule
      ScheduleParams terms = new ScheduleParams(effective, Dt.Empty, Dt.Empty, maturity, Frequency.Quarterly,
                                                BDConvention.None,
                                                Calendar.NYB, CycleRule.None, CashflowFlag.None);

      // Calc wal
      var wal = LoanModel.ExpectedWAL(today, terms, Currency.USD, DayCount.Actual360, 1.0, null, prepaymentCurve);

      // Test
      Assert.AreEqual(Dt.Fraction(today, maturity, DayCount.Actual360), wal, 0.0001, "The expected WAL did not match the target WAL!");
    }

    #endregion

    #region Utilities

    const int DIM = 10;
    private static Dt[] CreateDateArray(Dt start, Dt end, int step, TimeUnit unit)
    {
      List<Dt> list = new List<Dt>();
      list.Add(start);
      if (start < end)
      {
        int nstep = 0;
        bool keepgoing = true;
        do
        {
          nstep += step;
          Dt date = Dt.Add(start, nstep, unit);
          if (date >= end)
          {
            date = end;
            keepgoing = false;
          }
          list.Add(date);
        } while (keepgoing);
      }
      return list.ToArray();
    }

    public static double[] StateProbabilities(int t,
      LoanModel.StateDistribution dist)
    {
      int n = dist.StateCount;
      double[] result = new double[n + 2];
      result[0] = dist.DefaultProbability(t);
      for (int i = 1; i <= n; ++i)
        result[i] = dist.Probability(t, i - 1);
      result[n + 1] = dist.PrepayProbability(t);
      return result;
    }

    public static double[,] TransitionMatrix(int t,
      LoanModel.StateDistribution dist)
    {
      int n = dist.StateCount;
      double[,] result = new double[n + 2, n + 2];

      // Default stay default.
      result[0, 0] = 1.0;

      for (int i = 1; i <= n; ++i)
      {
        // Jump to default
        result[i, 0] = dist.JumpToDefault(t, i - 1);

        // Normal states
        for (int j = 1; j <= n; ++j)
          result[i, j] = dist.JumpToState(t, i - 1, j - 1);

        // Jump to prepaid
        result[i, n + 1] = dist.JumpToPrepay(t, i - 1);
      }

      // Prepaid stays prepaid.
      result[n + 1, n + 1] = 1.0;

      return result;
    }

    public static double[] NextDistribution(
      double[] P, double[,] A)
    {
      int n = P.Length;
      double[] result = new double[n];
      for (int j = 0; j < n; ++j)
      {
        double sum = 0;
        for (int i = 0; i < n; ++i)
          sum += P[i] * A[i, j];
        result[j] = sum;
      }
      return result;
    }

    /// <summary>
    /// Checks validity of probability distribution.
    /// </summary>
    /// <param name="label">Label</param>
    /// <param name="p">Probabilities by states.</param>
    /// <returns>0 if no error.</returns>
    public static void CheckDistribution(string label, double[] p)
    {
      double sum = 0;
      for (int i = 0; i < p.Length; ++i)
      {
        if (p[i] < 0)
        {
          Assert.AreEqual(false, true, label + "[" + i + "] < 0");
          return;
        }
        sum += p[i];
      }
      const double tolerance = 1E-15;
      if (Math.Abs(sum - 1) > tolerance)
      {
        Assert.AreEqual(1.0, sum, tolerance, label + ".Sum");
        return;
      }
      return;
    }

    /// <summary>
    /// Checks validity of transition matrix.
    /// </summary>
    /// <param name="label">Label</param>
    /// <param name="A">Transition matrix.</param>
    /// <returns>0 if no error.</returns>
    public static void CheckTransition(string label, double[,] A)
    {
      int n = A.GetLength(0);
      for (int i = 0; i < n; ++i)
      {
        double sum = 0;
        for (int j = 0; j < n; ++j)
        {
          if (A[i, j] < 0)
          {
            Assert.AreEqual(false, true, label + "[" + i + ", " + "] < 0");
            return;
          }
          sum += A[i, j];
        }
        const double tolerance = 2E-15;
        if (Math.Abs(sum - 1) > tolerance)
        {
          Assert.AreEqual(1.0, sum, tolerance, label + ".SumRow(" + i + ')');
          return;
        }
      }
      return;
    }

    /// <summary>
    /// Checks the equality of two arrays.
    /// </summary>
    /// <param name="label">Label</param>
    /// <param name="a">Array a.</param>
    /// <param name="b">Array b.</param>
    /// <returns>0 if no error.</returns>
    public static void CheckEquality(string label, double[] a, double[] b)
    {
      const double tolerance = 3E-10;
      for (int i = 0; i < a.Length; ++i)
        if (Math.Abs(a[i] - b[i]) > tolerance)
        {
          Assert.AreEqual(a[i], b[i], tolerance, label + "[" + i + ']');
          return;
        }
      return;
    }

    #endregion Utilities
  }
}
