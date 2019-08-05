//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class TestCashflowStream : ToolkitTestBase
  {
    // Requirement:
    //   BaseEntity.Toolkit.Models.CashflowStreamModel.DiscountingAccrued == true


    [Test, Smoke]
    public void Simple()
    {
      // Simple case of regular CashflowStream schedule
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
      double origNotional = 1.0;
      bool exchangePrincipal = true;
      bool accruedPaid = true;
      bool includeDfltDate = false;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, ccy, fee, feeSettle,
                                      coupon, daycount, freq, roll, cal, null,
                                      origNotional, null, exchangePrincipal,
                                      accruedPaid, includeDfltDate, false, false, false, false,
                                      false/*CDSCashflowPricer.IncludeMaturityAccrual*/ );

      //dumpCashflowStream(cf);

      //
      // Test expected CashflowStream structure
      //
      int[] date = { 20020101, 20020701, 20030101, 20030701, 20040101 };
      double[] principal = { 0.0, 0.0, 0.0, 0.0, 1.0 };
      double[] interest = { 0.02555555, 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double [] notional = { 1.0, 1.0, 1.0, 1.0, 1.0 };

      int count = date.Length;

      int numPeriods = cf.Count;
      Assert.AreEqual( count, numPeriods, String.Format( "Expected {0} payments, got {1}", count, numPeriods));

      for( int i = 0; i < count; i++ )
      {
        Assert.AreEqual( date[i], cf.GetDate(i).ToInt(), String.Format( "{0}: Expected payment date {1}, got {2}", i, date[i], cf.GetDate(i)));
        Assert.AreEqual( principal[i], cf.GetPrincipal(i), 0.0000001, String.Format( "{0}: Expected amount {1}, got {2}", i, principal[i], cf.GetPrincipal(i)));
        Assert.AreEqual( interest[i], cf.GetInterest(i), 0.0000001, String.Format( "{0}: Expected accrued {1}, got {2}", i, interest[i], cf.GetInterest(i)));
        Assert.AreEqual( notional[i], cf.GetNotional(i), 0.0000001, String.Format( "{0}: Expected notional {1}, got {2}", i, notional[i], cf.GetNotional(i)));
      }

      return;
    }


#if NOTYET

    [Test, Smoke, Ignore]
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
      double origNotional = 1.0;
      bool exchangePrincipal = false;
      bool accruedPaid = true;
      bool includeDfltDate = false;
      double recovery = 0.60;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, ccy, fee, feeSettle,
                                      coupon, daycount, freq, roll, cal, null,
                                      origNotional, null, exchangePrincipal,
                                      accruedPaid, includeDfltDate, false, false, false, false,
                                      false/*CDSCashflowPricer.IncludeMaturityAccrual*/);

      //
      // Test CashflowStream model
      //
      DiscountCurve dc = new DiscountCurve(asOf, 0.01);
      SurvivalCurve sc = new SurvivalCurve(asOf, 0.01);

      // Test Pv
      bool includeMaturityProtection = false;
      double pv = CashflowStreamModel.Pv(
        cf.WrappedCashflow, cf.GetUpfrontFee(), cf.GetFeeSettle(),
        asOf, asOf, dc, sc, null, 0.0, RecoveryType.Face, recovery, 0.0, false,
        includeMaturityProtection, 0.5, 0, TimeUnit.None);
      double expectedPv = 0.04652206;
      Assert.AreEqual( expectedPv, pv, 0.0000001, String.Format( "Expected pv {0}, got {1}", expectedPv, pv));

      return;
    }

    [Test]
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

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, ccy, fee, feeSettle,
                                      coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                      includeDfltDate, lossAmount, dfltCcy,
                                      CDSCashflowPricer.IncludeMaturityAccrual);

      //dumpCashflowStream(cf);

      //
      // Test expected CashflowStream structure
      //
      int[] date = { 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      int numPeriods = cf.Count;
      Assert.AreEqual( count, numPeriods, String.Format( "Expected {0} payments, got {1}", count, numPeriods));

      for( int i = 0; i < count; i++ )
      {
        Assert.AreEqual( date[i],
                         (int)cf.GetDate(i),
                         String.Format( "{0}: Expected payment date {1}, got {2}", i, date[i], cf.GetDate(i)));
        Assert.AreEqual( amount[i],
                         cf.GetAmount(i),
                         0.0000001,
                         String.Format( "{0}: Expected amount {1}, got {2}", i, amount[i], cf.GetAmount(i)));
        Assert.AreEqual( accrued[i],
                         cf.GetAccrued(i),
                         0.0000001,
                         String.Format( "{0}: Expected accrued {1}, got {2}", i, accrued[i], cf.GetAccrued(i)));
        Assert.AreEqual( dAmount[i],
                         cf.GetDefaultAmount(i),
                         0.0000001,
                         String.Format( "{0}: Expected default payment {1}, got {2}", i, dAmount[i], cf.GetDefaultAmount(i)));
      }

      return;
    }


    [Test]
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

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, ccy, fee, feeSettle,
                                      coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                      includeDfltDate, lossAmount, dfltCcy,
                                      CDSCashflowPricer.IncludeMaturityAccrual);

      //
      // Expected CashflowStream structure
      //
      int[] date = { 20020101, 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02555555, 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      //
      // Test CashflowStream model
      //
      DiscountCurve dc = new DiscountCurve(asOf, 0.0);
      SurvivalCurve sc = new SurvivalCurve(asOf, 0.0);

      // Test Pv
      double pv = CashflowStreamModel.Pv(cf, asOf, asOf, dc, sc, null,
                                   0.0, true, 0, TimeUnit.None);
      double expectedPv = 0.0;
      for( int i = 0; i < count; i++ )
        expectedPv += (amount[i] + accrued[i]);
      Assert.AreEqual( expectedPv,
                       pv,
                       0.0000001,
                       String.Format( "Expected pv {0}, got {1}", expectedPv, pv));

      // Test sum of fee and protection pv
      double pv2 = CashflowStreamModel.FeePv(cf, asOf, asOf, dc, sc, null,
                                       0.0, true, 0, TimeUnit.None) +
        CashflowStreamModel.ProtectionPv(cf, asOf, asOf, dc, sc, null,
                                   0.0, true, 0, TimeUnit.None);
      Assert.AreEqual( expectedPv,
                       pv2,
                       0.0000001,
                       String.Format( "Expected pv accrued + pv protection {0}, got {1}", expectedPv, pv2));

      return;
    }


    [Test]
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

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, ccy, fee, feeSettle,
                                      coupon, daycount, freq, roll, cal, principal, accruedPaid,
                                      includeDfltDate, lossAmount, dfltCcy,
                                      CDSCashflowPricer.IncludeMaturityAccrual);

      //
      // Expected CashflowStream structure
      //
      int[] date = { 20020101, 20020701, 20030101, 20030701, 20040101 };
      double[] amount = { 0.0, 0.0, 0.0, 0.0, 1.0 };
      double[] accrued = { 0.02555555, 0.02513888, 0.02555555, 0.02513888, 0.02555555 };
      double[] dAmount = { -0.4, -0.4, -0.4, -0.4, -0.4 };

      int count = date.Length;

      //
      // Test CashflowStream model
      //
      DiscountCurve dc = new DiscountCurve(asOf, 0.0);
      SurvivalCurve sc = new SurvivalCurve(asOf, 0.0);

      // Test Pv
      double pv = CashflowStreamModel.Pv(cf, asOf, asOf, dc, sc, null,
                                   0.0, false, 0, TimeUnit.None);
      double expectedPv = 0.0;
      // Start at 1 to skip the settlement payment
      for( int i = 1; i < count; i++ )
        expectedPv += (amount[i] + accrued[i]);
      Assert.AreEqual( expectedPv,
                       pv,
                       0.0000001,
                       String.Format( "Expected pv {0}, got {1}", expectedPv, pv));

      // Test sum of fee and protection pv
      double pv2 = CashflowStreamModel.FeePv(cf, asOf, asOf, dc, sc, null,
                                       0.0, false, 0, TimeUnit.None) +
        CashflowStreamModel.ProtectionPv(cf, asOf, asOf, dc, sc, null,
                                   0.0, false, 0, TimeUnit.None);
      Assert.AreEqual( expectedPv,
                       pv2,
                       0.0000001,
                       String.Format( "Expected pv accrued + pv protection {0}, got {1}", expectedPv, pv2));

      return;
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void InvalidAsOf()
    {
      Dt asOf = new Dt();
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 1, 2004);
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void InvalidFirstAccrual()
    {
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt();
      Dt firstCpn = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 1, 2004);
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void InvalidMaturity()
    {
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(1, 6, 2000);
      Dt maturity = new Dt();
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void FirstAccrualAfterMaturity()
    {
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2005);
      Dt firstCpn = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 1, 2004);
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void FirstCpnBeforeFirstAccrual()
    {
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(1, 6, 1999);
      Dt maturity = new Dt(1, 1, 2004);
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void FirstCpnAfterMaturity()
    {
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(1, 6, 2005);
      Dt maturity = new Dt(1, 1, 2004);
      double coupon = 0.05;
      DayCount daycount = DayCount.Actual360;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }


    [Test]
    [ExpectedException(typeof(ApplicationException))]
    public void NoDaycount()
    {
      Dt asOf = new Dt(1, 1, 2002);
      Dt firstAccrual = new Dt(1, 1, 2000);
      Dt firstCpn = new Dt(1, 6, 2000);
      Dt maturity = new Dt(1, 1, 2004);
      double coupon = 0.05;
      DayCount daycount = DayCount.None;
      double principal = 1.0;
      double lossAmount = -0.40;

      CashflowStream cf = new CashflowStream();
      CashflowStreamFactory.FillFixed(cf, asOf, firstAccrual, firstCpn, maturity, Currency.USD, 0.0, new Dt(),
                                      coupon, daycount, Frequency.SemiAnnual, BDConvention.None, Calendar.None,
                                      principal, true, true, lossAmount, Currency.USD,
                                      CDSCashflowPricer.IncludeMaturityAccrual);
    }

#endif

    // Utility to dump CashflowStream if we need it
    static void
    dumpCashflowStream(CashflowStream cf)
    {
      for (int i=0; i < cf.Count; i++)
      {
        Console.Write(String.Format("Index: {0} Date: {1} Principal: {2} Interest: {3} Notional: {4}\n",
                                    i,
                                    cf.GetDate(i),
                                    cf.GetPrincipal(i),
                                    cf.GetInterest(i),
                                    cf.GetNotional(i)
                                    ));
      }
    }

  }
}
