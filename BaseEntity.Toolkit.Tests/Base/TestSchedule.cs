//
// TestSchedule.cs
// Test schedule routines
// Copyright (c) 2004-2008,   . All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class ScheduleTest : ToolkitTestBase
  {
    /// <summary>
    ///   Test of simple schedule without long or short first or last coupon periods
    /// </summary>
    [Test, Smoke]
    public void ShortLastCouponFRN()
    {
      Dt asOf = new Dt(20081009);
      Dt effective = new Dt(20070420);
      Dt firstCpnDate = new Dt(20070701); // No specified first coupon
      Dt lastCpnDate = new Dt(20140401); // No specified last coupon
      Dt maturity = new Dt(20140420);
      Frequency freq = Frequency.Quarterly;
      Calendar cal = Calendar.NYB;
      BDConvention bdc = BDConvention.Modified;
      bool adjustNext = true;
      bool eomRule = false;

      // Expected schedule dates and period fractions
      int[] startDate = { 20081002, 20090102, 20090402, 20090702, 20091002, 20100104, 20100405, 20100706, 20101006, 20110106, 20110406, 20110706, 20111006, 20120106, 20120409, 20120709, 20121009, 20130109, 20130409, 20130709, 20131009, 20140109 };
      int[] endDate = { 20090102, 20090402, 20090702, 20091002, 20100104, 20100405, 20100706, 20101006, 20110106, 20110406, 20110706, 20111006, 20120106, 20120409, 20120709, 20121009, 20130109, 20130409, 20130709, 20131009, 20140109, 20140421 };
      int[] cycleStart = { 20081002, 20090102, 20090402, 20090702, 20091002, 20100104, 20100405, 20100706, 20101006, 20110106, 20110406, 20110706, 20111006, 20120106, 20120409, 20120709, 20121009, 20130109, 20130409, 20130709, 20131009, 20140109 };
      int[] cycleEnd = { 20090102, 20090402, 20090702, 20091002, 20100104, 20100405, 20100706, 20101006, 20110106, 20110406, 20110706, 20111006, 20120106, 20120409, 20120709, 20121009, 20130109, 20130409, 20130709, 20131009, 20140109, 20140701 };
      int[] payment = { 20090102, 20090402, 20090702, 20091002, 20100104, 20100405, 20100706, 20101006, 20110106, 20110406, 20110706, 20111006, 20120106, 20120409, 20120709, 20121009, 20130109, 20130409, 20130709, 20131009, 20140109, 20140421 };

      Schedule sched = new Schedule(asOf, effective, firstCpnDate, lastCpnDate,
        maturity, freq, bdc, cal, adjustNext, eomRule);
      //ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, bdc, cal, adjustNext, eomRule,
      //             startDate, endDate, cycleStart, cycleEnd, payment, null, null, null);
      int count = payment.Length;
      int numPeriods = sched.Count;
      AssertEqual("PeriodCount", count, numPeriods);

      // Compare generated schedule
      for (int i = 0; i < count && i < sched.Count; i++)
      {
        AssertEqual("AccrualStart[" + i + ']', startDate[i], sched.GetPeriodStart(i).ToInt());
        AssertEqual("AccrualEnd[" + i + ']', endDate[i], sched.GetPeriodEnd(i).ToInt());
        AssertEqual("CycleStart[" + i + ']', cycleStart[i], sched.GetCycleStart(i).ToInt());
        AssertEqual("CycleEnd[" + i + ']', cycleEnd[i], sched.GetCycleEnd(i).ToInt());
        AssertEqual("Payment[" + i + ']', payment[i], sched.GetPaymentDate(i).ToInt());
      }
      return;
    }

    /// <summary>
    ///   Test of simple schedule without long or short first or last coupon periods
    /// </summary>
    [Test, Smoke]
    public void Simple()
    {
      Dt asOf = new Dt(20000705);
      Dt effective = new Dt(20000105);
      Dt firstCpnDate = new Dt(); // No specified first coupon
      Dt lastCpnDate = new Dt(); // No specified last coupon
      Dt maturity = new Dt(20040105);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = false;
      bool eomRule = false;

      // Expected schedule dates and period fractions
      int[] startDate =    {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] endDate =      {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040105};
      int[] cycleStart =   {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] cycleEnd =     {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040105};
      int[] payment =      {20000705, 20010105, 20010705, 20020107, 20020705, 20030106, 20030707, 20040105};
      double[] a360fract = {0.505556, 0.511111, 0.502778, 0.511111, 0.502778, 0.511111, 0.502778, 0.511111};
      double[] t360fract = {0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000};
      double[] aafract =   {0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000};

      ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, roll, cal, adjustNext, eomRule,
                   startDate, endDate, cycleStart, cycleEnd, payment, a360fract, t360fract, aafract);

      return;
    }

    /// <summary>
    ///   Test of schedule with long first coupon period
    /// </summary>
    [Test, Smoke]
    public void LongFirstCoupon()
    {
      Dt asOf = new Dt(20000705);
      Dt effective = new Dt(19991205);
      Dt firstCpnDate = new Dt(20000705); // Long first coupon period
      Dt lastCpnDate = new Dt(); // No specified last coupon
      Dt maturity = new Dt(20040105);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = false;
      bool eomRule = false;

      // Expected schedule dates and period fractions
      int[] startDate =    {19991205, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] endDate =      {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040105};
      int[] cycleStart =   {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] cycleEnd =     {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040105};
      int[] payment =      {20000705, 20010105, 20010705, 20020107, 20020705, 20030106, 20030707, 20040105};
      double[] a360fract = {0.591667, 0.511111, 0.502778, 0.511111, 0.502778, 0.511111, 0.502778, 0.511111};
      double[] t360fract = {0.583333, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000};
      double[] aafract =   {0.584239, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000};

      ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, roll, cal, adjustNext, eomRule,
                   startDate, endDate, cycleStart, cycleEnd, payment, a360fract, t360fract, aafract);

      return;
    }

    /// <summary>
    ///   Test of schedule with long last coupon period
    /// </summary>
    [Test, Smoke]
    public void LongLastCoupon()
    {
      Dt asOf = new Dt(20000704);
      Dt effective = new Dt(20000105);
      Dt firstCpnDate = new Dt(); // No specified first coupon
      Dt lastCpnDate = new Dt(20030705); // Long last coupon period
      Dt maturity = new Dt(20040305);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = false;
      bool eomRule = false;

      // Expected schedule dates and period fractions
      int[] startDate =    {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] endDate =      {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040305};
      int[] cycleStart =   {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] cycleEnd =     {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040105};
      int[] payment =      {20000705, 20010105, 20010705, 20020107, 20020705, 20030106, 20030707, 20040305};
      double[] a360fract = {0.505556, 0.511111, 0.502778, 0.511111, 0.502778, 0.511111, 0.502778, 0.677778};
      double[] t360fract = {0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.666667};
      double[] aafract =   {0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.664835};

      ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, roll, cal, adjustNext, eomRule,
                   startDate, endDate, cycleStart, cycleEnd, payment, a360fract, t360fract, aafract);

      return;
    }

    /// <summary>
    ///   Test of schedule with short last coupon period
    /// </summary>
    [Test, Smoke]
    public void ShortLastCoupon()
    {
      Dt asOf = new Dt(20000705);
      Dt effective = new Dt(20000105);
      Dt firstCpnDate = new Dt(20000705);
      Dt lastCpnDate = new Dt(); // Implied short last coupon
      Dt maturity = new Dt(20040101);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = false;
      bool eomRule = false;

      // Expected schedule dates and period fractions
      int[] startDate =    {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] endDate =      {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040101};
      int[] cycleStart =   {20000105, 20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705};
      int[] cycleEnd =     {20000705, 20010105, 20010705, 20020105, 20020705, 20030105, 20030705, 20040105};
      int[] payment =      {20000705, 20010105, 20010705, 20020107, 20020705, 20030106, 20030707, 20040101};
      double[] a360fract = {0.505556, 0.511111, 0.502778, 0.511111, 0.502778, 0.511111, 0.502778, 0.500000};
      double[] t360fract = {0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.488889};
      double[] aafract =   {0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.500000, 0.489130};

      ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, roll, cal, adjustNext, eomRule,
                   startDate, endDate, cycleStart, cycleEnd, payment, a360fract, t360fract, aafract);

      return;
    }

    [Test, Smoke]
    public void EOM()
    {
      // Test of EOM Rule
      Dt asOf = new Dt(2, 1, 2003);
      Dt effective = new Dt(31, 10, 2002);
      Dt firstCpnDate = new Dt(28, 2, 2003);  // Short first period
      Dt lastCpnDate = new Dt();
      Dt maturity = new Dt(31, 10, 2004); // Short last period
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Modified;
      bool adjustNext = false;
      bool eomRule = true;                // Use end of month

      // Expected results
      int[] startDate =    {20021031, 20030228, 20030831, 20040229, 20040831};
      int[] endDate =      {20030228, 20030831, 20040229, 20040831, 20041031};
      int[] cycleStart =   {20020831, 20030228, 20030831, 20040229, 20040831};
      int[] cycleEnd =     {20030228, 20030831, 20040229, 20040831, 20050228};
      int[] payment =      {20030228, 20030829, 20040227, 20040831, 20041029};
      double[] a360fract = {0.333333, 0.511111, 0.505556, 0.511111, 0.169444};
      double[] t360fract = {0.327778, 0.508333, 0.497222, 0.505556, 0.166667};
      double[] aafract =   {0.331492, 0.500000, 0.500000, 0.500000, 0.168508};

      ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, roll, cal, adjustNext, eomRule,
                   startDate, endDate, cycleStart, cycleEnd, payment, a360fract, t360fract, aafract);

      return;
    }

    [Test, Smoke]
    public void AdjustNext()
    {
      // Regular schedule with adjustNext and weekend roll
      Dt asOf = new Dt(6, 1, 2002);
      Dt effective = new Dt(4, 1, 2002);
      Dt firstCpnDate = new Dt(4, 7, 2002);
      Dt lastCpnDate = new Dt();
      Dt maturity = new Dt(7, 1, 2004);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = true;
      bool eomRule = false;

      // Expected results
      int[] startDate =    {20020104, 20020704, 20030106, 20030707};
      int[] endDate =      {20020704, 20030106, 20030707, 20040107};
      int[] cycleStart =   {20020104, 20020704, 20030106, 20030707};
      int[] cycleEnd =     {20020704, 20030106, 20030707, 20040107};
      int[] payment =      {20020704, 20030106, 20030707, 20040107};
      double[] a360fract = {0.502778, 0.516667, 0.505556, 0.511111};
      double[] t360fract = {0.500000, 0.505556, 0.502778, 0.500000};
      double[] aafract =   {0.500000, 0.500000, 0.500000, 0.500000};

      ScheduleTester(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, roll, cal, adjustNext, eomRule,
                   startDate, endDate, cycleStart, cycleEnd, payment, a360fract, t360fract, aafract);

      return;
    }

    /// <summary>
    ///   Test Coupon date functions
    /// </summary>
    [Test, Smoke]
    public void CouponDates()
    {
      Dt asOf = new Dt(20020105);
      Dt firstAccrual = new Dt(20000105);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt lastCpn = new Dt(); // No specified last coupon
      Dt maturity = new Dt(20040105);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = false;
      bool eomRule = false;

      // Test coupon date functions
      Dt bdt;      // Base date
      Dt dt;       // Result
      Dt tdt;      // Target date
      int numCpns; // Number of remaining coupons

      Schedule schedule = new Schedule(firstAccrual, firstAccrual, firstCpn,
        lastCpn, maturity, freq, roll, cal, adjustNext, eomRule);

      // In between coupons
      bdt = new Dt(20030301);
      dt = schedule.GetNextCouponDate(bdt);
      tdt = new Dt(20030705);
      Assert.AreEqual(tdt, dt, String.Format("Expected NextCouponDate after {0} to be {1}, got {2}", bdt, tdt, dt));
      dt = schedule.GetPrevCouponDate(bdt);
      tdt = new Dt(20030105);
      Assert.AreEqual(tdt, dt, String.Format("Expected PreviousCouponDate after {0} to be {1}, got {2}", bdt, tdt, dt));
      numCpns = schedule.NumberOfCouponsRemaining(bdt);
      Assert.AreEqual(2, numCpns, String.Format("Expected NumberOfCouponsRemaining after {0} to be {1}, got {2}", bdt, 2, numCpns));

      // On coupon cycle date
      bdt = new Dt(20030105);
      dt = schedule.GetNextCouponDate(bdt);
      tdt = new Dt(20030705);
      Assert.AreEqual(tdt, dt, String.Format("Expected NextCouponDate after {0} to be {1}, got {2}", bdt, tdt, dt));
      dt = schedule.GetPrevCouponDate(bdt);
      tdt = new Dt(20030105);
      Assert.AreEqual(tdt, dt, String.Format("Expected PreviousCouponDate after {0} to be {1}, got {2}", bdt, tdt, dt));
      numCpns = schedule.NumberOfCouponsRemaining(bdt);
      Assert.AreEqual(2, numCpns, String.Format("Expected NumberOfCouponsRemaining after {0} to be {1}, got {2}", bdt, 2, numCpns));

      // Before first coupon date
      bdt = new Dt(20000101);
      dt = schedule.GetNextCouponDate(bdt);
      tdt = new Dt(20000705);
      Assert.AreEqual(tdt, dt, String.Format("Expected NextCouponDate after {0} to be {1}, got {2}", bdt, tdt, dt));
      dt = schedule.GetPrevCouponDate(bdt);
      Assert.IsTrue(dt.IsEmpty(), String.Format("Expected no PreviousCouponDate after {0}, got {1}", bdt, dt));
      numCpns = schedule.NumberOfCouponsRemaining(bdt);
      Assert.AreEqual(8, numCpns, String.Format("Expected NumberOfCouponsRemaining after {0} to be {1}, got {2}", bdt, 8, numCpns));

      // After last coupon date
      bdt = new Dt(20040115);
      dt = schedule.GetNextCouponDate(bdt);
      Assert.IsTrue(dt.IsEmpty(), String.Format("Expected no NextCouponDate after {0}, got {1}", bdt, dt));
      dt = schedule.GetPrevCouponDate(bdt);
      tdt = new Dt(20040105);
      Assert.AreEqual(tdt, dt, String.Format("Expected PreviousCouponDate after {0} to be {1}, got {2}", bdt, tdt, dt));
      numCpns = schedule.NumberOfCouponsRemaining(bdt);
      Assert.AreEqual(0, numCpns, String.Format("Expected NumberOfCouponsRemaining after {0} to be {1}, got {2}", bdt, 2, numCpns));

      return;
    }

    [Test, Smoke]
    public void WeekendRoll()
    {
      // Regular schedule with weekend roll
      Dt asOf = new Dt(10, 1, 2002);
      Dt firstAccrual = new Dt(4, 1, 2000);
      Dt firstCpn = new Dt(); // No specified first coupon
      Dt maturity = new Dt(4, 1, 2004);
      Frequency freq = Frequency.SemiAnnual;
      Calendar cal = Calendar.None;
      BDConvention roll = BDConvention.Following;
      bool adjustNext = false;
      bool eomRule = false;   

      Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity, freq, roll, cal, adjustNext, eomRule);

      //Console.Write("***\nWeekendRoll\n***\n\n");
      //dumpSched(sched);
      //Console.Write("\n***\n");

      // Expected results
      int[] dates = { 20020704, 20030104, 20030704, 20040104 };
      int[] payments = { 20020704, 20030106, 20030704, 20040105 };
      int count = dates.Length;

      int numPeriods = sched.Count;
      AssertEqual("PeriodCount", count, numPeriods);
      
      for( int i = 0; i < count && i < sched.Count; i++ )
      {
        if( i > 0 )
        {
          AssertEqual("PeriodStart[" + i + ']', dates[i-1], sched.GetPeriodStart(i).ToInt());
        }
        AssertEqual("PeriodEnd[" + i + ']', dates[i], sched.GetPeriodEnd(i).ToInt());
        AssertEqual("Payment[" + i + ']', payments[i], sched.GetPaymentDate(i).ToInt());
      }

      return;
    }

    [Test, Smoke]
    public void ScheduleRollsToMaturity1()
    {
      // Regular schedule with roll
      Dt asOf = new Dt(15, 9, 2004);
      Dt firstAccrual = new Dt(15, 9, 2004);
      Dt firstCpn = new Dt(15, 12, 2004);
      Dt maturity = new Dt(17, 9, 2007);
      Frequency freq = Frequency.Quarterly;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Modified;
      bool adjustNext = false;
      bool eomRule = false;

      Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity, freq, roll, cal, adjustNext, eomRule);	

      //Console.Write("\n\n***\nScheduleRollsToMaturity\n***\n\n");
      //dumpSched(sched);
      //Console.Write("\n***\n");

      // Expected results
      int[] dates = { 20041215,
                      20050315, 20050615, 20050915, 20051215,
                      20060315, 20060615, 20060915, 20061215,
                      20070315, 20070615, 20070917
      };
      int[] payments = { 20041215,
                         20050315, 20050615, 20050915, 20051215,
                         20060315, 20060615, 20060915, 20061215,
                         20070315, 20070615, 20070917
      };
      int count = dates.Length;

      int numPeriods = sched.Count;
      Assert.AreEqual( count, numPeriods, String.Format( "Expected {0} periods, got {1}", count, numPeriods));

      for( int i = 0; i < count && i < sched.Count; i++ )
      {
        if( i > 0 )
        {
          Assert.AreEqual( dates[i-1], sched.GetPeriodStart(i).ToInt(),
                           String.Format( "{0}: Expected period start date {1}, got {2}", i, dates[i-1], sched.GetPeriodStart(i)));
        }
        Assert.AreEqual( dates[i], sched.GetPeriodEnd(i).ToInt(),
                         String.Format( "{0}: Expected period end date {1}, got {2}", i, dates[i], sched.GetPeriodEnd(i)));
        Assert.AreEqual( payments[i], sched.GetPaymentDate(i).ToInt(),
                         String.Format( "{0}: Expected payment date {1}, got {2}", i, payments[i], sched.GetPaymentDate(i)));
      }

      // Now compare the last two payment dates -- they should be different!
      Assert.IsFalse( Dt.Cmp(sched.GetPaymentDate(numPeriods-1), sched.GetPaymentDate(numPeriods-2)) == 0,
                      String.Format( "Expected differing payment dates {0}, {1}",
                                     sched.GetPaymentDate(numPeriods-2), sched.GetPaymentDate(numPeriods-1)));

      return;
    }
    
    [Test, Smoke]
    public void ScheduleRollsToMaturity2()
    {
      // Construct Schedule (found to be bad in C++ as of 15 September 2004)
      // Subtle point with this example. Note the maturity date one day after
      // the natural cycle end date. This one day on a holiday rolls to the next day.
      // and the period end date is adjusted along with the period payment date.
      //
      Dt asOf = new Dt(14, 9, 2004);
      Dt firstAccrual = new Dt(14, 9, 2004);
      Dt firstCpn = new Dt(14, 12, 2004);
      Dt maturity = new Dt(15, 9, 2014);
      Frequency freq = Frequency.Quarterly;
      Calendar cal = Calendar.NYB;
      BDConvention roll = BDConvention.Modified;
      bool adjustNext = false;
      bool eomRule = false;

      Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity, freq, roll, cal, adjustNext, eomRule);

      //Console.Write("\n\n***\nScheduleRollsToMaturity2\n***\n\n");
      //dumpSched(sched);
      //Console.Write("\n***\n");

      // Expected results
      int[] dates = { 20041214,
                      20050314, 20050614, 20050914, 20051214,
                      20060314, 20060614, 20060914, 20061214,
                      20070314, 20070614, 20070914, 20071214,
                      20080314, 20080614, 20080914, 20081214,
                      20090314, 20090614, 20090914, 20091214,
                      20100314, 20100614, 20100914, 20101214,
                      20110314, 20110614, 20110914, 20111214,
                      20120314, 20120614, 20120914, 20121214,
                      20130314, 20130614, 20130914, 20131214,
                      20140314, 20140614, 20140915
      };
      int[] payments = { 20041214,
                         20050314, 20050614, 20050914, 20051214,
                         20060314, 20060614, 20060914, 20061214,
                         20070314, 20070614, 20070914, 20071214,
                         20080314, 20080616, 20080915, 20081215,
                         20090316, 20090615, 20090914, 20091214,
                         20100315, 20100614, 20100914, 20101214,
                         20110314, 20110614, 20110914, 20111214,
                         20120314, 20120614, 20120914, 20121214,
                         20130314, 20130614, 20130916, 20131216,
                         20140314, 20140616, 20140915
      };
      int count = dates.Length;

      int numPeriods = sched.Count;
      Assert.AreEqual( count, numPeriods, String.Format( "Expected {0} periods, got {1}", count, numPeriods));

      for( int i = 0; i < count && i < sched.Count; i++ )
      {
        if( i > 0 )
        {
          Assert.AreEqual( dates[i-1], sched.GetPeriodStart(i).ToInt(),
                           String.Format( "{0}: Expected period start date {1}, got {2}", i, dates[i-1], sched.GetPeriodStart(i)));
        }
        Assert.AreEqual( dates[i], sched.GetPeriodEnd(i).ToInt(),
                         String.Format( "{0}: Expected period end date {1}, got {2}", i, dates[i], sched.GetPeriodEnd(i)));
        Assert.AreEqual( payments[i], sched.GetPaymentDate(i).ToInt(),
                         String.Format( "{0}: Expected payment date {1}, got {2}", i, payments[i], sched.GetPaymentDate(i)));
      }

      // Now compare the last two payment dates -- they should be different!
      Assert.IsFalse( Dt.Cmp(sched.GetPaymentDate(numPeriods-1), sched.GetPaymentDate(numPeriods-2)) == 0,
                      String.Format( "Expected differing payment dates {0}, {1}",
                                     sched.GetPaymentDate(numPeriods-2), sched.GetPaymentDate(numPeriods-1)));

      return;
    }

    [Test, Smoke]
    public void InvalidAsOf()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt();
        Dt firstAccrual = new Dt(14, 9, 2004);
        Dt firstCpn = new Dt(14, 12, 2004);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void InvalidFirstAccrual()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt();
        Dt firstCpn = new Dt(14, 12, 2004);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void InvalidMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt(14, 9, 2004);
        Dt firstCpn = new Dt(14, 12, 2004);
        Dt maturity = new Dt();

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void FirstAccrualAfterMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt(14, 9, 2024);
        Dt firstCpn = new Dt(14, 12, 2004);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void FirstCpnBeforeFirstAccrual()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt(14, 9, 2004);
        Dt firstCpn = new Dt(14, 12, 2000);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void FirstCpnAfterMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt(14, 9, 2004);
        Dt firstCpn = new Dt(14, 12, 2020);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, maturity, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void LastCpnAfterMaturity()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt(14, 9, 2004);
        Dt firstCpn = new Dt(14, 12, 2004);
        Dt lastCpn = new Dt(15, 9, 2024);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, lastCpn, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    [Test, Smoke]
    public void LastCpnBeforeFirstCpn()
    {
      Assert.Throws<ApplicationException>(() =>
      {
        Dt asOf = new Dt(14, 9, 2004);
        Dt firstAccrual = new Dt(14, 9, 2004);
        Dt firstCpn = new Dt(14, 12, 2004);
        Dt lastCpn = new Dt(15, 9, 2004);
        Dt maturity = new Dt(15, 9, 2014);

        Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, lastCpn, maturity,
          Frequency.None, BDConvention.None, Calendar.None, false, false);
      });
    }

    // Utility to test results for a schedule
    private static void ScheduleTester(
      Dt asOf, Dt effective, Dt firstCpnDate, Dt lastCpnDate, Dt maturity, Frequency freq,
      BDConvention roll, Calendar cal, bool adjustNext, bool eomRule,
      int[] startDate, int[] endDate, int[] cycleStart, int[] cycleEnd, int[] payment,
      double[] a360fract, double[] t360fract, double[] aafract
      )
    {
      // Generate schedule
      Schedule sched = new Schedule(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq,
        roll, cal, adjustNext, eomRule);

      // Test number of elements
      int count = payment.Length;
      int numPeriods = sched.Count;
      AssertEqual("PeriodCount", count, numPeriods);

      // Compare generated schedule
      for (int i = 0; i < count && i < sched.Count; i++)
      {
        AssertEqual("AccrualStart[" + i + ']', startDate[i], sched.GetPeriodStart(i).ToInt());
        AssertEqual("AccrualEnd[" + i + ']', endDate[i], sched.GetPeriodEnd(i).ToInt());
        AssertEqual("CycleStart[" + i + ']', cycleStart[i], sched.GetCycleStart(i).ToInt());
        AssertEqual("CycleEnd[" + i + ']', cycleEnd[i], sched.GetCycleEnd(i).ToInt());
        AssertEqual("Payment[" + i + ']', payment[i], sched.GetPaymentDate(i).ToInt());
        if (a360fract != null)
          AssertEqual("Actual360[" + i + ']', a360fract[i], sched.Fraction(i, DayCount.Actual360, !adjustNext), 0.000001);
        if (t360fract != null)
          AssertEqual("Thirty360[" + i + ']', t360fract[i], sched.Fraction(i, DayCount.Thirty360, !adjustNext), 0.000001);
        if (aafract != null)
          AssertEqual("ActualActualBond[" + i + ']', aafract[i], sched.Fraction(i, DayCount.ActualActualBond, !adjustNext), 0.000001);
      }

      // Test separate date functions
      sched = new Schedule(effective, effective, firstCpnDate, lastCpnDate, maturity, freq,
        roll, cal, adjustNext, eomRule);
      for (int i = 0; i < count && i < sched.Count; i++)
      {
        // Test from prior coupon payment date
        Dt bdt = new Dt(cycleStart[i]);
        Dt dt = sched.GetNextCouponDate(bdt);
        Dt tdt = new Dt(endDate[i]);
        AssertEqual("NextCouponDate[" + i + ']', tdt.ToInt(), dt.ToInt());
        dt = sched.GetPrevCouponDate(bdt);
        tdt = (i > 0) ? new Dt(endDate[i - 1]) : new Dt();
        AssertEqual("PrevCouponDate[" + i + ']', tdt.ToInt(), dt.ToInt());
        int numCpns = sched.NumberOfCouponsRemaining(bdt);
        AssertEqual("#CouponsRemaining[" + i + ']', sched.Count - i, numCpns);
        // Test from day after prior coupon payment date
        bdt = Dt.Add(bdt, 1);
        tdt = new Dt(endDate[i]);
        dt = sched.GetNextCouponDate(bdt);
        AssertEqual("NextCouponDate[" + i + ']', tdt.ToInt(), dt.ToInt());
        dt = sched.GetPrevCouponDate(bdt);
        tdt = (i > 0) ? new Dt(endDate[i - 1]) : new Dt();
        AssertEqual("PrevCouponDate[" + i + ']', tdt.ToInt(), dt.ToInt());
        numCpns = sched.NumberOfCouponsRemaining(bdt);
        AssertEqual("#CouponsRemaining[" + i + ']', sched.Count - i, numCpns);
      }
      return;
    }

    // Utility to dump schedule if we need it
    static void
    dumpSched(Schedule sched)
    {
      for (int i=0; i < sched.Count; i++)
      {
        Console.Write(String.Format("Index: {0} Previous: {1} Next: {2} Payment: {3}\n",
                                    i,
                                    sched.GetPeriodStart(i),
                                    sched.GetPeriodEnd(i),
                                    sched.GetPaymentDate(i)));
      }
    }
  }
}
