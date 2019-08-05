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
  /// <summary>
  /// Test CDS cashflow generation
  /// </summary>
  [TestFixture]
  public class TestCDSCashflows : ToolkitTestBase
  {
    const double tolerance = 1.0E-6;

    #region Tests

    /// <summary>
    ///   Test regular CDS schedule
    /// </summary>
    [Test, Smoke]
    public void Regular()
    {
      Dt asOf = new Dt(20000705);
      Dt effective = new Dt(20070328);
      double premium = 100 / 10000.0;
      Dt firstPremDate = new Dt(); // No specified first coupon
      Dt lastPremDate = new Dt(); // No specified last coupon
      Dt maturity = new Dt(20120620);
      Frequency freq = Frequency.Quarterly;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      double notional = 1000000.0;

      // Expected schedule dates and period fractions
      int[] payment = {
                        20070620, 20070920, 20071220, 20080320, 20080620, 20080922, 20081222, 20090320, 20090622,
                        20090921, 20091221, 20100322, 20100621, 20100920, 20101220, 20110321, 20110620, 20110920,
                        20111220, 20120320, 20120620
                      };
      double[] amt = {
                       2333.33, 2555.56, 2527.78, 2527.78, 2555.56, 2611.11, 2527.78, 2444.44, 2611.11,
                       2527.78, 2527.78, 2527.78, 2527.78, 2527.78, 2527.78, 2527.78, 2527.78, 2555.56,
                       2527.78, 2527.78, 2583.33
                     };

      CDSTest(asOf, effective, premium, firstPremDate, lastPremDate, maturity, freq, roll, cal, notional, payment, amt);
    }

    /// <summary>
    /// 
    /// </summary>
    [Test, Smoke]
    public void CycleOn30th()
    {
      Dt asOf = new Dt(20070530);
      Dt effective = new Dt(20070530);
      Dt maturity = new Dt(20100530);
      const double premium = 130 / 10000.0;
      Dt firstPremDate = new Dt(20070830); // No specified first coupon
      Dt lastPremDate = new Dt(); // No specified last coupon
      Calendar cal = Calendar.NYB;
      const Frequency freq = Frequency.Quarterly;
      const BDConvention roll = BDConvention.Modified;
      const double notional = 25000000.0;

      // Expected schedule dates and period fractions
      int[] payment = 
      {
        20070830, 20071130, 20080229, 20080530, 
        20080829, 20081128, 20090227, 20090529,
        20090831, 20091130, 20100226,
        ToolkitConfigurator.Settings.CashflowPricer.RollLastPaymentDate
         ? 20100528 : 20100530
      };
      double[] amt = {
                       83055.56, 83055.56, 82152.78, 82152.78,
                       82152.78, 82152.78, 82152.78, 82152.78,
                       84861.11, 82152.78, 79444.44, 84861.11 
                     };

      CDSTest(asOf, effective, premium, firstPremDate, lastPremDate, maturity, freq, roll, cal, notional, payment, amt);
    }
    
    #endregion

    #region Utilities

    // Utility to test results for a CDS
    private static void CDSTest(
      Dt asOf, Dt effective, double prem, Dt firstPremDate, Dt lastPremDate, Dt maturity, Frequency freq,
      BDConvention roll, Calendar cal, double notional, int[] payment, double[] amt
      )
    {
      // Create CDS pricer
      CDS cds = new CDS(effective, maturity, Currency.USD, prem, DayCount.Actual360, freq, roll, cal);
      cds.FirstPrem = firstPremDate;
      cds.LastPrem = lastPremDate;
      DiscountCurve discountCurve = new DiscountCurve(asOf, 0.0);
      SurvivalCurve survivalCurve = new SurvivalCurve(asOf, 1.0);
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
        int expectDate = payment[i];
        Dt actualDate = cf.GetDt(i);
        Assert.AreEqual(expectDate, actualDate.ToInt(),
                        String.Format("{0}: Expected payment date {1}, got {2}", i, expectDate, actualDate));

        double expectCoupon = amt[i];
        double actualCoupon = Math.Round(cf.GetAccrued(i)*notional, 2);
        Assert.AreEqual(expectCoupon, actualCoupon, 0.000001,
                        String.Format("{0}: Expected payment amount {1}, got {2}", i, expectCoupon, actualCoupon));
      }
    }

    #endregion

  } // class TestCDSCashflows
}
