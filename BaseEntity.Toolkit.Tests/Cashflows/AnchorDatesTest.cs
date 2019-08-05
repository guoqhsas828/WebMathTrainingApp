//
// Copyright (c)    2002-2015. All rights reserved.
//

using System.Collections.Generic;
using BaseEntity.Toolkit.Base;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture]
  public class AnchorDatesTest : ToolkitTestBase
  {
    private static CycleRule ToCycleRule(Dt date, Frequency freq)
    {
      return freq == Frequency.Weekly || freq == Frequency.BiWeekly
               ? (CycleRule.Monday + (int) date.DayOfWeek())
               : (date.Day == 31
                    ? CycleRule.EOM
                    : (CycleRule.First + date.Day - 1));
    }

    private void GetResults(Dt accrualStart, Dt maturity, Dt inputFirstCycle, Dt inputNextToLastCycle, Frequency freq,
                            Dt firstCycle, Dt nextToLastCycle, CycleRule rule, CashflowFlag flag)
    {
      BDConvention roll = BDConvention.Following;
      Calendar cal = Calendar.NYB;

      if (rule == CycleRule.None)
        rule = Schedule.GuessCycleRule(accrualStart, inputFirstCycle,
                                       inputNextToLastCycle, maturity, freq, roll, cal, rule, flag);
      var dp = new Schedule(accrualStart, accrualStart, inputFirstCycle,
                            inputNextToLastCycle, maturity, freq, roll, cal, rule, flag);

      var cycleS = new List<Dt>();
      var cycleE = new List<Dt>();
      for (int i = 0; i < dp.Count; i++)
      {
        cycleS.Add(dp.GetCycleStart(i));
        cycleE.Add(dp.GetCycleEnd(i));
      }
      Dt rolledMaturity = Dt.Roll(maturity, roll, cal);
      Dt firstCoupon = Dt.Roll(firstCycle, roll, cal);
      Dt lastCoupon = Dt.Roll(nextToLastCycle, roll, cal);
      Assert.AreEqual(rolledMaturity.ToInt(),
        dp.GetPaymentDate(dp.Count - 1).ToInt(), "rolled maturity");
      Assert.AreEqual(firstCoupon.ToInt(),
        dp.GetPaymentDate(0).ToInt(), "first coupon date");
      if (dp.Count > 1)
        Assert.AreEqual(lastCoupon.ToInt(),
          dp.GetPaymentDate(dp.Count - 2).ToInt(), "last coupon date");
      else
        Assert.AreEqual(lastCoupon.ToInt(),
          dp.GetPaymentDate(dp.Count - 1).ToInt(), "last coupon date");
      if (inputFirstCycle != Dt.Empty)
        Assert.IsTrue(cycleS.Contains(Dt.Roll(inputFirstCycle, roll, cal)),
          "Contains " + inputFirstCycle.ToInt());
      if (inputNextToLastCycle != Dt.Empty)
        Assert.IsTrue(cycleE.Contains(Dt.Roll(inputNextToLastCycle, roll, cal)),
          "Contains " + inputNextToLastCycle.ToInt());
    }

    private void GetResults(Dt accrualStart, Dt maturity, Dt inputFirstCycle,
                            Dt inputNextToLastCycle, Dt firstCycle, Dt nextToLastCycle, CashflowFlag flag)
    {
      flag |= CashflowFlag.RollLastPaymentDate | CashflowFlag.RespectLastCoupon;
      CycleRule rule = CycleRule.None;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, Frequency.SemiAnnual, firstCycle,
                 nextToLastCycle, rule, flag);
    }


    [Test]
    public void TestNormalPeriods()
    {
      var accrualStart = new Dt(15, 3, 2000);
      var maturity = new Dt(15, 9, 2009);
      Dt inputFirstCycle = Dt.Empty;
      Dt inputNextToLastCycle = Dt.Empty;
      var firstCycle = new Dt(15, 9, 2000);
      var nextToLastCycle = new Dt(15, 3, 2009);
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test]
    public void TestShortFirstPeriods()
    {
      var accrualStart = new Dt(25, 3, 2000);
      var maturity = new Dt(15, 9, 2009);
      var firstCycle = new Dt(15, 9, 2000);
      var nextToLastCycle = new Dt(15, 3, 2009);
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = Dt.Empty;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test]
    public void TestLongFirstPeriod()
    {
      var accrualStart = new Dt(05, 3, 2000);
      var maturity = new Dt(15, 9, 2009);
      var firstCycle = new Dt(15, 9, 2000);
      var nextToLastCycle = new Dt(15, 3, 2009);
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = Dt.Empty;
      ;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test]
    public void TestShortLastPeriod()
    {
      var accrualStart = new Dt(15, 3, 2000);
      var maturity = new Dt(10, 9, 2009);
      var firstCycle = new Dt(15, 9, 2000);
      var nextToLastCycle = new Dt(15, 3, 2009);
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = Dt.Empty;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test]
    public void TestShortLastPeriodImpliedbyFirstCoupon()
    {
      var accrualStart = new Dt(15, 3, 2000);
      var maturity = new Dt(10, 9, 2009);
      var firstCycle = new Dt(15, 4, 2009);
      Dt nextToLastCycle = firstCycle;
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = Dt.Empty;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.StubAtEnd);
    }


    [Test]
    public void TestOneCoupon()
    {
      var accrualStart = new Dt(14, 3, 2000);
      var maturity = new Dt(15, 9, 2000);
      var firstCycle = new Dt(15, 3, 2000);
      Dt nextToLastCycle = firstCycle;
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = nextToLastCycle;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test]
    public void TestOddPeriodMissingFirstCoupon()
    {
      var accrualStart = new Dt(15, 3, 2000);
      var maturity = new Dt(15, 9, 2009);
      var firstCycle = new Dt(25, 3, 2000);
      var nextToLastCycle = new Dt(25, 3, 2009);
      Dt inputFirstCycle = Dt.Empty;
      Dt inputNextToLastCycle = nextToLastCycle;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test, Ignore("The expected firstCycle cannot be on cycle")]
    public void TestOddPeriodMissingLastCoupon()
    {
      // This test is ignored because the expected firstCycle cannot be on cycle.
      // Luca is going to fix this test.
      var accrualStart = new Dt(15, 3, 2000);
      var maturity = new Dt(15, 9, 2009);
      var firstCycle = new Dt(25, 9, 2000);
      var nextToLastCycle = new Dt(15, 3, 2009);
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = Dt.Empty;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, firstCycle, nextToLastCycle,
                 CashflowFlag.None);
    }


    [Test]
    public void TestEOMWithNoDetermination()
    {
      var accrualStart = new Dt(15, 1, 2000);
      var maturity = new Dt(30, 11, 2009);
      var firstCycle = new Dt(31, 3, 2000);
      var nextToLastCycle = new Dt(30, 9, 2009);
      Dt inputFirstCycle = firstCycle;
      Dt inputNextToLastCycle = nextToLastCycle;
      GetResults(accrualStart, maturity, inputFirstCycle, inputNextToLastCycle, Frequency.Monthly, firstCycle,
                 nextToLastCycle, CycleRule.EOM, CashflowFlag.RollLastPaymentDate | CashflowFlag.RespectLastCoupon);
    }


    /*
      TODO Complete tests
     [Test]
     public void Testfeb()
     {
       Dt accrualStart = new Dt(29, 1, 2001);
       Dt maturity = new Dt(29, 2, 2004);
       Dt anchor = maturity;
       const bool EOM = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, anchor, EOM, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(28, 2, 2001), dp.FirstCouponDate);
       Assert.AreEqual(new Dt(29, 1, 2004), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }
    
    
     [Test]
     public void TestfebEOM()
     {
       Dt accrualStart = new Dt(31, 1, 2001);
       Dt maturity = new Dt(29, 2, 2004);
       Dt anchor = maturity;
       const bool EOM = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, anchor, EOM, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(28, 2, 2001), dp.FirstCouponDate);
       Assert.AreEqual(new Dt(31, 1, 2004), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }


     [Test]
     public void TestAnchor()
     {
       Dt accrualStart = new Dt(1, 1, 2000);
       Dt firstCoupon = new Dt(30, 1, 2000);
       Dt lastCoupon = new Dt(30, 8, 2009);
       Dt maturity = new Dt(30, 10, 2009);

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, lastCoupon, maturity, Frequency.Monthly, lastCoupon, false, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorWithNoFirstCoupon()
     {
       Dt accrualStart = new Dt(1, 1, 2000);

       Dt lastCoupon = new Dt(30, 8, 2009);
       Dt maturity = new Dt(30, 10, 2009);

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, lastCoupon, maturity, Frequency.Monthly, lastCoupon, false, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(30,1,2000), dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorWithNoLastCoupon()
     {
       Dt accrualStart = new Dt(1, 1, 2000);
       Dt firstCoupon = new Dt(30, 1, 2000);
       Dt maturity = new Dt(30, 10, 2009);

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.Monthly, maturity, false, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(new Dt(30,9,2009), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorWithNoFirstLastCoupon()
     {
       Dt accrualStart = new Dt(30, 12, 1999);

       Dt maturity = new Dt(30, 10, 2009);

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, maturity, false, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(30, 1, 2000), dp.FirstCouponDate);
       Assert.AreEqual(new Dt(30, 9, 2009), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorEOM()
     {
       Dt accrualStart = new Dt(1, 1, 2000);
       Dt firstCoupon = new Dt(29, 2, 2000);
       Dt lastCoupon = new Dt(30, 9, 2009);
       Dt maturity = new Dt(31, 10, 2009);

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, lastCoupon, maturity, Frequency.Monthly, lastCoupon, false, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorWithNoFirstCouponEOM()
     {
       Dt accrualStart = new Dt(31, 1, 2000);

       Dt lastCoupon = new Dt(30, 9, 2009);
       Dt maturity = new Dt(31, 10, 2009);

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, lastCoupon, maturity, Frequency.Monthly, lastCoupon, false, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(29, 2, 2000), dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorWithNoLastCouponEOM()
     {
       Dt accrualStart = new Dt(31, 1, 2000);
       Dt firstCoupon = new Dt(29,2,2000);
       Dt maturity = new Dt(31, 10, 2009);
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.Monthly, maturity, eom, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(new Dt(30, 9, 2009), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnchorWithNoFirstLastCouponEOM()
     {
       Dt accrualStart = new Dt(31, 1, 2000);
       Dt maturity = new Dt(31, 10, 2009);
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, maturity, eom, Direction.None);
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(29, 2, 2000), dp.FirstCouponDate);
       Assert.AreEqual(new Dt(30, 9, 2009), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN()
     {
       Dt accrualStart = new Dt(31, 1, 2009);
       Dt maturity = new Dt(30, 10, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);
      
       //NOTE: the results are rolled, because FRN has to be a rolled cycle.
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(new Dt(27, 2, 2009), dp.FirstCouponDate);
       Assert.AreEqual(new Dt(30, 9, 2009), dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN2()
     {
       Dt accrualStart = new Dt(31, 1, 2009);
       Dt firstCoupon = new Dt(27, 2, 2009);
       Dt lastCoupon = new Dt(30, 9, 2009);
       Dt maturity = new Dt(31, 10, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, lastCoupon, maturity, Frequency.Monthly, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       //NOTE: the results are rolled, because FRN has to be a rolled cycle.
       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN3()
     {
       Dt accrualStart = new Dt(05, 1, 2009);
       Dt firstCoupon = new Dt(15, 2, 2009);
       Dt lastCoupon = new Dt(30, 9, 2010);
       Dt maturity = new Dt(15, 10, 2010);
       Dt eomDeterm = Dt.Empty;
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, lastCoupon, maturity, Frequency.Monthly, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN4()
     {
       Dt accrualStart = new Dt(29, 9, 2000);
       Dt firstCoupon = new Dt(29, 3, 2001);
       Dt lastCoupon = new Dt(31, 3, 2014);
       Dt maturity = new Dt(29, 3, 2015);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.Annual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN5()
     {
       Dt firstCoupon = new Dt(15, 2, 2009);
       Dt lastCoupon = new Dt(15, 9, 2010);
       Dt maturity = new Dt(29, 10, 2010);
       Dt eomDeterm = Dt.Empty;
       bool eom = true;

       try
       {
         ScheduleParams dp = AnchorDateUtil.FindDates(Dt.Empty, firstCoupon, lastCoupon, maturity, Frequency.Monthly,
                                                  eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);
         logger.Debug(dp);
         Assert.Fail();
       }catch(ArgumentException ae)
       {
         logger.Debug(ae.Message);
       }

     }

     [Test]
     public void TestFRN6()
     {
       Dt accrualStart = new Dt(29, 9, 2000);
       Dt firstCoupon = new Dt(29, 3, 2001);
       Dt lastCoupon = new Dt(31, 3, 2014);
       Dt maturity = new Dt(29, 3, 2015);
       Dt eomDeterm = new Dt(29, 3, 2014); 
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.Annual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN7()
     {
       Dt accrualStart = new Dt(29, 9, 2000);
       Dt firstCoupon = new Dt(29, 3, 2001);
       Dt lastCoupon = new Dt(31, 3, 2014);
       Dt maturity = new Dt(29, 3, 2015);
       Dt eomDeterm = Dt.Empty;
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.Annual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN8()
     {
       Dt accrualStart = new Dt(29, 9, 2000);
       Dt firstCoupon = new Dt(28, 9, 2001);
       Dt lastCoupon = new Dt(30, 9, 2014);
       Dt maturity = new Dt(29, 3, 2015);
       Dt eomDeterm = Dt.Empty;
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, lastCoupon, maturity, Frequency.Annual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN9()
     {
       Dt accrualStart = new Dt(29, 3, 2000);
       Dt firstCoupon = new Dt(13, 10, 2000);
       Dt maturity = new Dt(13, 10, 2000);
       Dt eomDeterm = maturity;
       bool eom = true;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.SemiAnnual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(Dt.Empty, dp.FirstCouponDate);
       Assert.AreEqual(Dt.Empty, dp.NextToLastCouponDate);
     }

     [Test]
     public void TestFRN10()
     {
       Dt accrualStart = new Dt(29, 3, 2000);
       Dt firstCoupon = new Dt(30, 9, 2000);
       Dt lastCoupon = new Dt(30, 3, 2005);
       Dt maturity = new Dt(30, 9, 2005);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, Dt.Empty, maturity, Frequency.SemiAnnual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestFRN11()
     {
       Dt accrualStart = new Dt(29, 3, 2000);
       Dt firstCoupon = new Dt(29, 9, 2000);
       Dt lastCoupon = new Dt(31, 3, 2014);
       Dt maturity = new Dt(30, 9, 2014);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, lastCoupon, maturity, Frequency.SemiAnnual, eomDeterm, eom, BDConvention.FRN, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(lastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnniverNearEOMnoAccrualStart()
     {
       Dt accrualStart = new Dt(30, 9, 2008);
       Dt firstCoupon = new Dt(30,10,2008);
       Dt nextToLastCoupon = new Dt(28, 2, 2009);
       Dt maturity = new Dt(15, 3, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(Dt.Empty, firstCoupon, nextToLastCoupon, maturity, Frequency.Monthly, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnniverNearEOM()
     {
       Dt accrualStart = new Dt(30, 9, 2008);
       Dt firstCoupon = new Dt(30, 10, 2008);
       Dt nextToLastCoupon = new Dt(28, 2, 2009);
       Dt maturity = new Dt(15, 3, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, firstCoupon, nextToLastCoupon, maturity, Frequency.Monthly, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnniverNearEOMnoFirstCoupon()
     {
       Dt accrualStart = new Dt(15, 10, 2008);
       Dt firstCoupon = new Dt(28, 10, 2008);
       Dt nextToLastCoupon = new Dt(28, 2, 2009);
       Dt maturity = new Dt(15, 3, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, nextToLastCoupon, maturity, Frequency.Monthly, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnniverNearEOMnoNextToLastCouponAndAccrualMissing()
     {
       Dt accrualStart = new Dt(30, 9, 2008);
       Dt firstCoupon = new Dt(30, 10, 2008);
       Dt nextToLastCoupon = new Dt(28, 2, 2009);
       Dt maturity = new Dt(15, 3, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(Dt.Empty, firstCoupon, Dt.Empty, maturity, Frequency.Monthly, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestAnniverNearEOMnoNextToLastCouponAndAccrualMissing2()
     {
       Dt accrualStart = new Dt(30, 3, 2000);
       Dt firstCoupon = new Dt(30, 9, 2000);
       Dt nextToLastCoupon = new Dt(30, 3, 2014);
       Dt maturity = new Dt(29, 9, 2014);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(Dt.Empty, firstCoupon, Dt.Empty, maturity, Frequency.SemiAnnual, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void Test28th()
     {
       Dt accrualStart = new Dt(29,11, 2006);
       Dt firstCoupon = new Dt(28, 2, 2007);
       Dt nextToLastCoupon = new Dt(29, 8, 2013);
       Dt maturity = new Dt(29, 11, 2013);
       Dt eomDeterm = Dt.Empty;
       bool eom = false;
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, nextToLastCoupon, maturity, Frequency.Quarterly, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestHH1()
     {
       Dt accrualStart = new Dt(30, 1, 2009);
       Dt firstCoupon = new Dt(28, 2, 2009);
       Dt nextToLastCoupon = new Dt(30, 6, 2009);
       Dt maturity = new Dt(30, 7, 2009);
       Dt eomDeterm = Dt.Empty;
       bool eom = false; 
       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, Dt.Empty, maturity, Frequency.Monthly, eomDeterm, eom, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }

     [Test]
     public void TestMissingFirstCouponEOMBug()
     {
       Dt accrualStart = new Dt(31, 3, 2000);
       Dt firstCoupon = new Dt(30, 4, 2000);
       Dt nextToLastCoupon = new Dt(30, 4, 2013);
       Dt maturity = new Dt(31, 3, 2014);

       Dt eomDeterm = new Dt(30, 9, 2006);
       bool eom = true;
       BDConvention roll = BDConvention.Modified;

       ScheduleParams dp = AnchorDateUtil.FindDates(accrualStart, Dt.Empty, nextToLastCoupon, maturity, Frequency.Annual, eomDeterm, eom, roll, Calendar.None, Direction.None);

       Assert.AreEqual(accrualStart, dp.AccrualStartDate);
       Assert.AreEqual(maturity, dp.Maturity);
       Assert.AreEqual(firstCoupon, dp.FirstCouponDate);
       Assert.AreEqual(nextToLastCoupon, dp.NextToLastCouponDate);
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.FirstCouponDate));
       Assert.That(dp.AnniversaryDates.ContainsValue(dp.NextToLastCouponDate));
     }
     */
  }
}