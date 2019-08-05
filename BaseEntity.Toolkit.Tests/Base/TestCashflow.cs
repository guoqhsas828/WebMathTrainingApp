//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Native;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Native;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.Native;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;
using CashflowFactory = BaseEntity.Toolkit.Cashflows.CashflowFactory;
using CashflowModel = BaseEntity.Toolkit.Models.CashflowModel;

namespace BaseEntity.Toolkit.Tests
{

  [TestFixture, Smoke]
  public class TestCashflow : ToolkitTestBase
  {
    public TestCashflow()
    {
      ExpectsFileName = "data/expects/TestCashflow.expects";
    }

    [Test, Smoke]
    public void Simple()
    {
      // Simple case of regular cashflow schedule
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt maturity = new Dt(1, 1, 2004);
      Currency ccy = Currency.USD;
      double fee = 0.0;
      Dt feeSettle = new Dt();
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.None;
      Calendar cal = Calendar.None;
      double principal = 1.0;
      bool accruedPaid = true;
      bool includeDfltDate = false;
      double lossAmount = -0.40;
      Currency dfltCcy = Currency.USD;

      const CycleRule cycleRule = CycleRule.None;
      const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault;
      var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);

      Cashflow cf = new Cashflow();
      CashflowFactory.FillFixed(cf, asOf, schedParams, ccy, daycount,
                                coupon, null,
                                principal, null,
                                lossAmount, dfltCcy, Dt.Empty,
                                fee, feeSettle);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, ccy, fee, feeSettle,
                                coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                includeDfltDate, lossAmount, dfltCcy, false);
#endif

      //dumpCashflow(cf);

      //
      // Test expected cashflow structure
      //
      int[] date = { 20020101, 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02555555, 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      int numPeriods = cf.Count;
      Assert.AreEqual(count, numPeriods, String.Format("Expected {0} payments, got {1}", count, numPeriods));

      for (int i = 0; i < count; i++)
      {
        Assert.AreEqual(date[i],
                         cf.GetDt(i).ToInt(),
                         String.Format("{0}: Expected payment date {1}, got {2}", i, date[i], cf.GetDt(i)));
        Assert.AreEqual(amount[i],
                         cf.GetAmount(i),
                         0.0000001,
                         String.Format("{0}: Expected amount {1}, got {2}", i, amount[i], cf.GetAmount(i)));
        Assert.AreEqual(accrued[i],
                         cf.GetAccrued(i),
                         0.0000001,
                         String.Format("{0}: Expected accrued {1}, got {2}", i, accrued[i], cf.GetAccrued(i)));
        Assert.AreEqual(dAmount[i],
                         cf.GetDefaultAmount(i),
                         0.0000001,
                         String.Format("{0}: Expected default payment {1}, got {2}", i, dAmount[i], cf.GetDefaultAmount(i)));
      }

      return;
    }


    [Test, Smoke]
    public void Regular()
    {
      // Test regular pv calculation matching sample spreadsheet results
      Dt asOf = new Dt(1, 2, 2003);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt maturity = new Dt(1, 1, 2004);
      Currency ccy = Currency.USD;
      double fee = 0.0;
      Dt feeSettle = new Dt();
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.None;
      Calendar cal = Calendar.None;
      double principal = 0.0;
      bool accruedPaid = true;
      bool includeDfltDate = false;
      double lossAmount = -0.40;
      Currency dfltCcy = Currency.USD;
      bool includeMaturityAccrual = settings_.CDSCashflowPricer.IncludeMaturityAccrual;
      bool includeMaturityProtection = settings_.CDSCashflowPricer.IncludeMaturityProtection;
      bool discountingAccrued = settings_.CashflowPricer.DiscountingAccrued;

      const CycleRule cycleRule = CycleRule.None;

      CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault;
      if (includeMaturityAccrual)
        flags |= CashflowFlag.IncludeMaturityAccrual;

      var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);

      Cashflow cf = new Cashflow();
      CashflowFactory.FillFixed(cf, asOf, schedParams, ccy, daycount,
                                coupon, null,
                                principal, null,
                                lossAmount, dfltCcy, Dt.Empty,
                                fee, feeSettle);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, ccy, fee, feeSettle,
        coupon, daycount, freq, roll, cal, principal, accruedPaid,
        includeDfltDate, lossAmount, dfltCcy, includeMaturityAccrual);
#endif

      //
      // Test cashflow model
      //
      DiscountCurve dc = new DiscountCurve(asOf, 0.01);
      SurvivalCurve sc = new SurvivalCurve(asOf, 0.01);

      double[] pvs = new double[4];
      string[] label = new string[4];

      // Test Pv with 3 month pricing grid
      label[0] = "3M Grid";
      pvs[0] = CashflowModel.Pv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection,discountingAccrued, 3, TimeUnit.Months);

      // Test Pv with 1 month pricing grid
      label[1] = "1M Grid";
      pvs[1] = CashflowModel.Pv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection,discountingAccrued, 1, TimeUnit.Months);

      // Test Pv with default pricing grid
      label[2] = "Default Grid";
      pvs[2] = CashflowModel.Pv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection,discountingAccrued, 0, TimeUnit.None);

      // Test Pv with daily pricing grid
      label[3] = "1D Grid";
      pvs[3] = CashflowModel.Pv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection,discountingAccrued, 1, TimeUnit.Days);

      MatchExpects(pvs, label, 0);
    }


    [Test, Smoke]
    public void SettlementBetweenCoupons()
    {
      // Case of settlement between coupons
      Dt asOf = new Dt(2, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt maturity = new Dt(1, 1, 2004);
      Currency ccy = Currency.USD;
      double fee = 0.0;
      Dt feeSettle = new Dt();
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.None;
      Calendar cal = Calendar.None;
      double principal = 1.0;
      bool accruedPaid = true;
      bool includeDfltDate = false;
      double lossAmount = -0.40;
      Currency dfltCcy = Currency.USD;

      const CycleRule cycleRule = CycleRule.None;
      const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault;
      var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);

      Cashflow cf = new Cashflow();
      CashflowFactory.FillFixed(cf, asOf, schedParams, ccy, daycount,
                                coupon, null,
                                principal, null,
                                lossAmount, dfltCcy, Dt.Empty,
                                fee, feeSettle);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, ccy, fee, feeSettle,
                                coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                includeDfltDate, lossAmount, dfltCcy, false);
#endif

      //dumpCashflow(cf);

      //
      // Test expected cashflow structure
      //
      int[] date = { 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      int numPeriods = cf.Count;
      Assert.AreEqual(count, numPeriods, String.Format("Expected {0} payments, got {1}", count, numPeriods));

      for (int i = 0; i < count; i++)
      {
        Assert.AreEqual(date[i],
                         cf.GetDt(i).ToInt(),
                         String.Format("{0}: Expected payment date {1}, got {2}", i, date[i], cf.GetDt(i)));
        Assert.AreEqual(amount[i],
                         cf.GetAmount(i),
                         0.0000001,
                         String.Format("{0}: Expected amount {1}, got {2}", i, amount[i], cf.GetAmount(i)));
        Assert.AreEqual(accrued[i],
                         cf.GetAccrued(i),
                         0.0000001,
                         String.Format("{0}: Expected accrued {1}, got {2}", i, accrued[i], cf.GetAccrued(i)));
        Assert.AreEqual(dAmount[i],
                         cf.GetDefaultAmount(i),
                         0.0000001,
                         String.Format("{0}: Expected default payment {1}, got {2}", i, dAmount[i], cf.GetDefaultAmount(i)));
      }

      return;
    }


    [Test, Smoke]
    public void SettlementDatePaymentIncluded()
    {
      // Case of pv where settlement date payment included
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt maturity = new Dt(1, 1, 2004);
      Currency ccy = Currency.USD;
      double fee = 0.0;
      Dt feeSettle = new Dt();
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.None;
      Calendar cal = Calendar.None;
      double principal = 1.0;
      bool accruedPaid = true;
      bool includeDfltDate = false;
      double lossAmount = -0.40;
      Currency dfltCcy = Currency.USD;

      const CycleRule cycleRule = CycleRule.None;
      const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault;
      var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);

      Cashflow cf = new Cashflow();
      CashflowFactory.FillFixed(cf, asOf, schedParams, ccy, daycount,
                                coupon, null,
                                principal, null,
                                lossAmount, dfltCcy, Dt.Empty,
                                fee, feeSettle);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, ccy, fee, feeSettle,
                                coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                includeDfltDate, lossAmount, dfltCcy, false);
#endif

      //
      // Expected cashflow structure
      //
      int[] date = { 20020101, 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02555555, 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      //
      // Test cashflow model
      //
      DiscountCurve dc = new DiscountCurve(asOf, 0.0);
      SurvivalCurve sc = new SurvivalCurve(asOf, 0.0);

      // Test Pv
      bool includeMaturityProtection = settings_.CDSCashflowPricer.IncludeMaturityProtection;
      bool discountingAccrued = settings_.CashflowPricer.DiscountingAccrued;
      double pv = CashflowModel.Pv(cf, asOf, asOf, dc, sc, null,
        0.0, true, includeMaturityProtection,discountingAccrued, 0, TimeUnit.None);
      double expectedPv = 0.0;
      for (int i = 0; i < count; i++)
        expectedPv += (amount[i] + accrued[i]);
      Assert.AreEqual(expectedPv,
                       pv,
                       0.0000001,
                       String.Format("Expected pv {0}, got {1}", expectedPv, pv));

      // Test sum of fee and protection pv
      double pv2 = CashflowModel.FeePv(cf, asOf, asOf, dc, sc, null,
        0.0, true, includeMaturityProtection, discountingAccrued, 0, TimeUnit.None) +
        CashflowModel.ProtectionPv(cf, asOf, asOf, dc, sc, null,
        0.0, true, includeMaturityProtection, discountingAccrued, 0, TimeUnit.None);
      Assert.AreEqual(expectedPv,
                       pv2,
                       0.0000001,
                       String.Format("Expected pv accrued + pv protection {0}, got {1}", expectedPv, pv2));

      return;
    }


    [Test, Smoke]
    public void SettlementDatePaymentExcluded()
    {
      // Case of pv where settlement date payment included
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt maturity = new Dt(1, 1, 2004);
      Currency ccy = Currency.USD;
      double fee = 0.0;
      Dt feeSettle = new Dt();
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      Frequency freq = Frequency.SemiAnnual;
      BDConvention roll = BDConvention.None;
      Calendar cal = Calendar.None;
      double principal = 1.0;
      bool accruedPaid = true;
      bool includeDfltDate = false;
      double lossAmount = -0.40;
      Currency dfltCcy = Currency.USD;

      const CycleRule cycleRule = CycleRule.None;
      const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault;
      var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);

      Cashflow cf = new Cashflow();
      CashflowFactory.FillFixed(cf, asOf, schedParams, ccy, daycount,
                                coupon, null,
                                principal, null,
                                lossAmount, dfltCcy, Dt.Empty,
                                fee, feeSettle);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, ccy, fee, feeSettle,
                                coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                includeDfltDate, lossAmount, dfltCcy, false);
#endif

      //
      // Expected cashflow structure
      //
      int[] date = { 20020101, 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02555555, 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      //
      // Test cashflow model
      //
      DiscountCurve dc = new DiscountCurve(asOf, 0.0);
      SurvivalCurve sc = new SurvivalCurve(asOf, 0.0);

      // Test Pv
      bool includeMaturityProtection = settings_.CDSCashflowPricer.IncludeMaturityProtection;
      bool discountingAccrued = settings_.CashflowPricer.DiscountingAccrued;
      double pv = CashflowModel.Pv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection, discountingAccrued, 0, TimeUnit.None);
      double expectedPv = 0.0;
      // Start at 1 to skip the settlement payment
      for (int i = 1; i < count; i++)
        expectedPv += (amount[i] + accrued[i]);
      Assert.AreEqual(expectedPv,
                       pv,
                       0.0000001,
                       String.Format("Expected pv {0}, got {1}", expectedPv, pv));

      // Test sum of fee and protection pv
      double pv2 = CashflowModel.FeePv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection, discountingAccrued, 0, TimeUnit.None) +
        CashflowModel.ProtectionPv(cf, asOf, asOf, dc, sc, null,
        0.0, false, includeMaturityProtection, discountingAccrued, 0, TimeUnit.None);
      Assert.AreEqual(expectedPv,
                       pv2,
                       0.0000001,
                       String.Format("Expected pv accrued + pv protection {0}, got {1}", expectedPv, pv2));

      return;
    }

    [Test, Smoke]
    public void WithDefaultEvent()
    {
      // Case of pv where settlement date payment included
      Dt asOf = new Dt(20070929);
      Dt firstAccrual = new Dt(20070929);
      Dt firstCpn = new Dt(20071220);
      Dt lastCpn = new Dt(20121220);
      Dt maturity = new Dt(20121220);
      Currency ccy = Currency.USD;
      double fee = 0.0;
      Dt feeSettle = new Dt();
      double coupon = 0.025;
      DayCount daycount = DayCount.Actual360;
      Frequency freq = Frequency.Quarterly;
      BDConvention roll = BDConvention.Modified;
      Calendar cal = Calendar.NYB;
      double principal = 0.0;
      double lossAmount = -0.55;
      Currency dfltCcy = Currency.USD;
      Dt defaultDate = new Dt(20080122);
      CycleRule rule = CycleRule.None;
      Toolkit.Base.Schedule sched = null;

      Dt[] resetDts = {new Dt(20070929), new Dt(20071220)};
      double[] resetRates = {coupon, coupon};
      DiscountCurve refcurve = new DiscountCurve(new Dt(20080212), 0.0);

      Cashflow cf;
      int count;

      int[] dates = {20071220, 20080122};
      int[] startDates = {20070929, 20071220};
      int[] endDates = {20071220, 20080122};

      Action<string,double[],double[],CashflowFlag>
        dotest = (prefix,frac,acc,f) => 
      {
        // FillFixed
        prefix += (f & CashflowFlag.IncludeDefaultDate) == 0
          ? "-ExclDfltDt:" : "-InclDfltDt:";
        cf = new Cashflow();
        if (prefix.StartsWith("Fixed"))
          Toolkit.Cashflows.Native.CashflowFactory.FillFixed(cf, sched, asOf, firstAccrual, firstCpn,
            lastCpn, maturity, ccy, fee, feeSettle, new[] { maturity },
            new[] { coupon }, daycount, freq, roll, cal, principal, new Dt[0],
            new double[0], lossAmount, dfltCcy, defaultDate, rule, f);
        else
          Toolkit.Cashflows.Native.CashflowFactory.FillFloat(cf, sched, asOf, firstAccrual, firstCpn,
            lastCpn, maturity, ccy, fee, feeSettle, new[] {maturity},
            new[] {coupon}, daycount, freq, roll, cal, refcurve, resetDts,
            resetRates, principal, new Dt[0], new double[0], lossAmount,
            dfltCcy, defaultDate, rule, f);
        count = dates.Length;
        AssertEqual(prefix + "Count", count, cf.Count);
        if (count > cf.Count) count = cf.Count;
        for (int i = 0; i < count; ++i)
        {
          AssertEqual(prefix + "Dt[" + i + ']',
            dates[i], cf.GetDt(i).ToInt());
          AssertEqual(prefix + "Start[" + i + ']',
            startDates[i], cf.GetStartDt(i).ToInt());
          AssertEqual(prefix + "End[" + i + ']',
            endDates[i], cf.GetEndDt(i).ToInt());
          AssertEqual(prefix + "Fraction[" + i + ']',
            frac[i], cf.GetPeriodFraction(i), 1E-15);
          AssertEqual(prefix + "Accrued[" + i + ']',
            acc[i], cf.GetAccrued(i), 1E-15);
        }
      };

      CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual
        | CashflowFlag.AccruedPaidOnDefault;
      double[]fractions = new[] { 0.22777777777777777, 0.0916666666666666667 };
      double[]accrueds = new[] { 0.0056944444444444447, 0.0022916666666666667 };
      dotest("Fixed", fractions, accrueds,flags);
      dotest("Float", fractions, accrueds, flags);

      flags |= CashflowFlag.IncludeDefaultDate;
      fractions = new[] { 0.22777777777777777, 0.094444444444444442 };
      accrueds = new[] {0.0056944444444444447, 0.0023611111111111111};
      dotest("Fixed", fractions, accrueds, flags);
      dotest("Float", fractions, accrueds, flags);

      return;
    }


    [Test, Smoke]
    public void InvalidAsOf()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt();
        Dt firstAccrual = new Dt(1, 1, 2000);
        Dt firstCpn = new Dt(1, 6, 2000);
        Dt maturity = new Dt(1, 1, 2004);
        double coupon = 0.05;
        DayCount daycount = DayCount.Actual360;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
        coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
        principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    [Test, Smoke]
    public void InvalidFirstAccrual()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Dt firstAccrual = new Dt();
        Dt firstCpn = new Dt(1, 6, 2000);
        Dt maturity = new Dt(1, 1, 2004);
        double coupon = 0.05;
        DayCount daycount = DayCount.Actual360;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
                                coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    [Test, Smoke]
    public void InvalidMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Dt firstAccrual = new Dt(1, 1, 2000);
        Dt firstCpn = new Dt(1, 6, 2000);
        Dt maturity = new Dt();
        double coupon = 0.05;
        DayCount daycount = DayCount.Actual360;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
                                coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    [Test, Smoke]
    public void FirstAccrualAfterMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Dt firstAccrual = new Dt(1, 1, 2005);
        Dt firstCpn = new Dt(1, 6, 2000);
        Dt maturity = new Dt(1, 1, 2004);
        double coupon = 0.05;
        DayCount daycount = DayCount.Actual360;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
                                coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    [Test, Smoke]
    public void FirstCpnBeforeFirstAccrual()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Dt firstAccrual = new Dt(1, 1, 2000);
        Dt firstCpn = new Dt(1, 6, 1999);
        Dt maturity = new Dt(1, 1, 2004);
        double coupon = 0.05;
        DayCount daycount = DayCount.Actual360;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
                                coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    [Test, Smoke]
    public void FirstCpnAfterMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Dt firstAccrual = new Dt(1, 1, 2000);
        Dt firstCpn = new Dt(1, 6, 2005);
        Dt maturity = new Dt(1, 1, 2004);
        double coupon = 0.05;
        DayCount daycount = DayCount.Actual360;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
                                coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    [Test, Smoke]
    public void NoDaycount()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(1, 1, 2002);
        Dt firstAccrual = new Dt(1, 1, 2000);
        Dt firstCpn = new Dt(1, 6, 2000);
        Dt maturity = new Dt(1, 1, 2004);
        double coupon = 0.05;
        DayCount daycount = DayCount.None;
        double principal = 1.0;
        double lossAmount = -0.40;

        const CycleRule cycleRule = CycleRule.None;
        const CashflowFlag flags = CashflowFlag.AccruedPaidOnDefault | CashflowFlag.IncludeDefaultDate;
        var schedParams = new ScheduleParams(firstAccrual, firstCpn, Dt.Empty, maturity, Frequency.SemiAnnual,
          BDConvention.None, Calendar.None, cycleRule, flags);

        Cashflow cf = new Cashflow();
        CashflowFactory.FillFixed(cf, asOf, schedParams, Currency.USD, daycount,
          coupon, null,
          principal, null,
          lossAmount, Currency.USD, Dt.Empty,
          0.0, Dt.Empty);

#if OBSOLETE
      CashflowFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, Dt.Empty, maturity, Currency.USD, 0.0, new Dt(),
                                coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                principal, true, true, lossAmount, Currency.USD, false);
#endif
      });
    }


    // Utility to dump cashflow if we need it
    static void
    dumpCashflow(Cashflow cf)
    {
      for (int i = 0; i < cf.Count; i++)
      {
        Console.Write(String.Format("Index: {0} Date: {1} Amount: {2} Accrued: {3} Default: {4}\n",
                                    i,
                                    cf.GetDt(i),
                                    cf.GetAmount(i),
                                    cf.GetAccrued(i),
                                    cf.GetDefaultAmount(i)
                                    ));
      }
    }

    private readonly ToolkitConfigSettings settings_ = ToolkitConfigurator.Settings;
  }
}
