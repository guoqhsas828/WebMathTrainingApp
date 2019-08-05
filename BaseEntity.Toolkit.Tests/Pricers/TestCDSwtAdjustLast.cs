//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestCDSwtAdjustLast : ToolkitTestBase
  {
    const double tolerance = 1.0E-6;

    #region Tests

    /// <summary>
    ///   Test regular CDS schedule
    /// </summary>
    [Test, Smoke]
    public void Cashflows()
    {
      Dt asOf = new Dt(20090820) ;
      Dt effective = asOf;
      double premium = 100/10000.0;
      Dt firstPremDate = new Dt(); // No specified first coupon
      Dt lastPremDate = new Dt(); // No specified last coupon
      Dt maturity = new Dt(20100620);
      Frequency freq = Frequency.Quarterly;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      double notional = 1000000.0;

      int lastPaymentDate = (ToolkitConfigurator.Settings.CashflowPricer.RollLastPaymentDate) ? 20100621 : 20100620;

      // Expected schedule dates and period fractions
      // Note that we add a day to the last period to include the accrued for maturity date
      int[] payment = {20091221, 20100322, lastPaymentDate};
      double[] accrued = {
                           premium*Dt.Fraction(asOf, new Dt(20091221), DayCount.Actual360),
                           premium*Dt.Fraction(new Dt(20091221), new Dt(20100322), DayCount.Actual360),
                           premium*Dt.Fraction(new Dt(20100322), new Dt(20100621), DayCount.Actual360)
                         };
     

      CDSTest(asOf, effective, premium, firstPremDate, lastPremDate, maturity, freq, roll, cal, notional, payment, accrued);

      return;
    }

    #endregion Tests

    #region Utilities

    // Utility to test results for a CDS
    private static void CDSTest(
      Dt asOf, Dt effective, double prem, Dt firstPremDate, Dt lastPremDate, Dt maturity, Frequency freq,
      BDConvention roll, Calendar cal, double notional, int[] payment, double[] accrued
      )
    {
      // Create CDS pricer
      CDS cds = new CDS(effective, maturity, Currency.USD, prem, DayCount.Actual360, freq, roll, cal);
      cds.FirstPrem = firstPremDate;
      cds.LastPrem = lastPremDate;
      DiscountCurve discountCurve = new DiscountCurve(asOf, 0.0);
      SurvivalCurve survivalCurve = new SurvivalCurve(asOf, 0.0);
      CDSCashflowPricer pricer = new CDSCashflowPricer(cds, asOf, asOf, discountCurve, survivalCurve, 0, TimeUnit.None);
      pricer.Notional = notional;
      Cashflow cf = pricer.Cashflow;

      // Test number of elements
      int count = payment.Length;
      int numPeriods = cf.Count;
      Assert.AreEqual(count, numPeriods, String.Format("Expected {0} premiums, got {1}", count, numPeriods));

      // Compare generated schedule
      for (int i = 0; i < count && i < cf.Count; i++)
      {
        Assert.AreEqual(payment[i], cf.GetDt(i).ToInt(),
                        String.Format("{0}: Expected payment date {1}, got {2}", i, payment[i], cf.GetDt(i)));
        Assert.AreEqual(accrued[i], cf.GetAccrued(i), 0.000001,
                        String.Format("{0}: Expected payment amount {1}, got {2}", i, accrued[i], cf.GetAmount(i)));
      }

      return;
    }

    #endregion Utilities

  } // class TestCDSCashflows
}