//
// TestScheduleDates.cs
// Copyright (c) 2004-2008,   . All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class ScheduleDatesTest : ToolkitTestBase
  {
    private Dt start_ = new Dt(20100831);
    private Dt end_ = new Dt(20130101);

    private class Data
    {
      internal int Count;
      internal Dt CycleBegin, CycleEnd, Effective, Maturity,
        FirstCoupon, LastCoupon, Settle;
      internal BDConvention BDConvention = BDConvention.Modified;
      internal Calendar Calendar = Calendar.None;
      internal Frequency Frequency;
      internal CycleRule CycleRule;
      internal CashflowFlag Flags;

      internal static Data Copy(Data o)
      {
        return (Data) o.MemberwiseClone();
      }

      internal Dt EndPaymentDate => (Flags & CashflowFlag.RollLastPaymentDate) != 0
                                    || ToolkitConfigurator.Settings.CashflowPricer.RollLastPaymentDate
        ? Dt.Roll(Maturity, BDConvention, Calendar) : Maturity;

      internal Dt EndCouponDate => (Flags & CashflowFlag.AdjustLast) != 0
        ? Dt.Roll(Maturity, BDConvention, Calendar) : Maturity;

      internal Dt Roll(Dt date)
      {
        return Dt.Roll(date, BDConvention, Calendar);
      }

      internal Dt NoRoll(Dt date)
      {
        return date;
      }
    }

    private static IEnumerable<Dt> DateRange(Dt start, Dt end)
    {
      for (Dt dt = start; dt < end; dt = Dt.Add(dt, 1))
        yield return dt;
    }

    private static CycleRule ToCycleRule(Dt date, Frequency freq)
    {
      return freq == Frequency.Weekly || freq == Frequency.BiWeekly
        ? (CycleRule.Monday + (int)date.DayOfWeek())
        : (date.Day == 31 ? CycleRule.EOM
          : (CycleRule.First + date.Day - 1));
    }

    private static void AssertEqual(string label,
      int expect, int actual, Data data)
    {
      if (actual != expect)
      {
        Assert.AreEqual(expect, actual,
          "At CycleBegin " + data.CycleBegin.ToInt() + '\t' + label
            + '\t' + expect + '\t' + actual);
        //throw new Exception("Assertion exception");
      }
    }

    private static void AssertBracket(string label,
      int lowerBound, int upperBound, int actual, Data data)
    {
      if (actual < lowerBound || actual >= upperBound)
      {
        Assert.IsTrue(lowerBound <= actual && actual < upperBound,
          "At CycleBegin " + data.CycleBegin.ToInt() + '\t' + label
            + "\t[" + lowerBound + ", " + upperBound
              + "]\t" + actual);
        //throw new Exception("Assertion exception");
      }
    }


    private static void CheckSchedule(string label, Data expect, Data input)
    {
      var flags = input.Flags;
      var roll = (flags & CashflowFlag.AccrueOnCycle) == 0
        ? (Func<Dt, Dt>) input.Roll : (Func<Dt, Dt>) input.NoRoll;
      Dt asOf = input.Settle.IsEmpty() 
        ? roll(Dt.Add(input.Effective, 1)) : input.Settle;
      Dt endPay = expect.EndPaymentDate;
      Dt endCpn = expect.EndCouponDate;
      Dt cycleBegin = expect.CycleBegin, cycleEnd = expect.CycleEnd;
      if ((flags & (CashflowFlag.RespectLastCoupon | CashflowFlag.StubAtEnd)) != 0)
      {
        int k;
        for (k = 0, cycleBegin = roll(expect.CycleBegin);
          cycleBegin > expect.Effective;)
        {
          cycleBegin = roll(Dt.Add(expect.CycleBegin,
            expect.Frequency, --k, expect.CycleRule == CycleRule.EOM));
        }
        if (cycleBegin != expect.Effective)
        {
          // To be safe for short frequency
          Dt dt =  Dt.Add(cycleBegin, expect.Frequency, -1, expect.CycleRule);
          for (k = 0; k < 3; ++k)
          {
            if (Dt.Add(dt, expect.Frequency, k, expect.CycleRule) ==
              expect.Effective)
            {
              cycleBegin = expect.Effective;
              break;
            }
          }
        }
        for (k = 0, cycleEnd = roll(expect.CycleEnd);
          cycleEnd < endCpn;)
        {
          cycleEnd = roll(Dt.Add(expect.CycleEnd,
            expect.Frequency, ++k, expect.CycleRule == CycleRule.EOM));
        }
      }
      else if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
      {
        return; // New setting active, so there is no need to test the old mode.
      }
      else
      {
        // don't test old methods for frequency shorter than monthly
        // for they have bugs.
        if ((int)expect.Frequency > 12) return;
      }

      Dt firstCpnExpect = roll(expect.FirstCoupon);
      if (firstCpnExpect > endCpn) firstCpnExpect = endCpn;
      Dt lastCpnExpect = roll(expect.LastCoupon);
      if (lastCpnExpect > endCpn) lastCpnExpect = endCpn;

      Schedule sched = new Schedule(asOf,
        input.Effective, input.FirstCoupon, input.LastCoupon, input.Maturity,
        input.Frequency, input.BDConvention, input.Calendar,
        input.CycleRule, input.Flags);
      var cloned = CloneUtil.CloneObjectGraph(sched,CloneMethod.Serialization); // check serialization
      int last = sched.Count - 1;
      AssertEqual(label + "Count", expect.Count, last + 1, expect);
      AssertEqual(label + "FirstCycleStart",
        cycleBegin.ToInt(), sched.GetCycleStart(0).ToInt(), expect);
      AssertEqual(label + "FirstAccrualStart",
        expect.Effective.ToInt(), sched.GetPeriodStart(0).ToInt(), expect);
      AssertEqual(label + "FirstCoupon",
        firstCpnExpect.ToInt(), sched.GetPeriodEnd(0).ToInt(), expect);
      AssertEqual(label + "NextToLastCoupon",
        lastCpnExpect.ToInt(),
        (last == 0 ? sched.GetPeriodEnd(0) : sched.GetPeriodStart(last)).ToInt(),
        expect);
      AssertEqual(label + "LastCoupon",
        endCpn.ToInt(), sched.GetPeriodEnd(last).ToInt(), expect);
      AssertEqual(label + "LastCycleEnd",
        cycleEnd.ToInt(), sched.GetCycleEnd(last).ToInt(), expect);
      AssertEqual(label + "LastPayment",
        endPay.ToInt(), sched.GetPaymentDate(last).ToInt(), expect);
      if (expect.Frequency != Frequency.None && expect.CycleRule != CycleRule.FRN)
      {
        AssertBracket(label + "AnchorDate", sched.AnchorDate.ToInt(),
          Dt.Add(sched.AnchorDate, sched.Frequency, 1, sched.CycleRule).ToInt(),
          input.Effective.ToInt(), expect);
      }
    }


    private void DoTest(int stubDays, Data input)
    {
      const int n = 8;
      input.Count = n;
      foreach(Dt anchor in DateRange(start_,end_))
      {
        var freq = input.Frequency;
        var cal = input.Calendar;
        var bdc = input.BDConvention;
        CycleRule rule = input.CycleRule = ToCycleRule(anchor, freq);
        Dt cycleBegin = input.CycleBegin = anchor;
        Dt cycleEnd = input.CycleEnd = Dt.Add(anchor, freq, n, false);
        Dt firstCpn = input.FirstCoupon = 
          Dt.Roll(Dt.Add(anchor, freq, 1, false), bdc, cal);
        Dt lastCpn = input.LastCoupon = 
          Dt.Roll(Dt.Add(anchor, freq, n - 1, false), bdc, cal);

        input.Effective = cycleBegin;
        input.Maturity = cycleEnd;
        input.FirstCoupon = firstCpn;
        input.LastCoupon = lastCpn;

        Data expect = Data.Copy(input);

        // Regular schedule
        expect.Flags = input.Flags = CashflowFlag.RollLastPaymentDate;
        CheckSchedule("Regular.Old:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate;
        CheckSchedule("Regular.StubAtBegin:", expect, input);
        if (ToCycleRule(input.Maturity,freq) == rule)
        {
          // Test CycleRule None.
          // Schedule should find the correct cycle rule from the maturity.
          input.CycleRule = CycleRule.None;
          input.LastCoupon = Dt.Empty;
          CheckSchedule("Regular.MissingLast:", expect, input);
          input.CycleRule = rule;
          input.LastCoupon = lastCpn;
        }

        expect.Flags = input.Flags = CashflowFlag.StubAtEnd |
          CashflowFlag.RespectLastCoupon | CashflowFlag.RollLastPaymentDate;
        CheckSchedule("Regular.StubAtEnd:", expect, input);
        if (ToCycleRule(input.Effective, freq) == rule)
        {
          // Test CycleRule None.
          // Schedule should find the correct cycle rule from the effective.
          input.CycleRule = CycleRule.None;
          input.FirstCoupon = Dt.Empty;
          CheckSchedule("Regular.MissingFirst:", expect, input);
          input.CycleRule = rule;
          input.FirstCoupon = firstCpn;
        }

        // No first coupon date.
        input.FirstCoupon = Dt.Empty;
        expect.Flags = input.Flags = CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingFirst.Old:", expect,input);

        expect.Flags = input.Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingFirst.StubAtBegin:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.StubAtEnd |
          CashflowFlag.RespectLastCoupon | CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingFirst.StubAtEnd:", expect, input);

        // No last coupon date.
        input.FirstCoupon = firstCpn;
        input.LastCoupon = Dt.Empty;
        expect.Flags = input.Flags = CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingLast.Old:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingLast.StubAtBegin:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.StubAtEnd |
          CashflowFlag.RespectLastCoupon | CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingLast.StubAtEnd:", expect, input);

        // No first and last coupon dates.
        input.FirstCoupon = Dt.Empty;
        input.LastCoupon = Dt.Empty;

        expect.Flags = input.Flags = CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingBoth.Old:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingBoth.StubAtBegin:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.StubAtEnd |
          CashflowFlag.RespectLastCoupon | CashflowFlag.RollLastPaymentDate;
        CheckSchedule("MissingBoth.StubAtEnd:", expect, input);

        // Short first and last coupon periods
        input.Effective = expect.Effective = Dt.Add(firstCpn, -stubDays);
        input.FirstCoupon = firstCpn;
        input.LastCoupon = lastCpn;
        input.Maturity = expect.Maturity = Dt.Add(lastCpn, stubDays);

        expect.Flags = input.Flags = CashflowFlag.RollLastPaymentDate;
        CheckSchedule("ShortEnds.Old:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate;
        CheckSchedule("ShortEnds.StubAtBegin:", expect, input);

        expect.Flags = input.Flags = CashflowFlag.StubAtEnd |
          CashflowFlag.RespectLastCoupon | CashflowFlag.RollLastPaymentDate;
        CheckSchedule("ShortEnds.StubAtEnd:", expect, input);

#if Not_Yet
        // Long first or last coupon periods
        start = cycleBegin_;
        stop = cycleEnd_;
        Dt savedFirstCpn = firstCpn_, savedLastCpn = lastCpn_;

        first = firstCpn_ = Dt.Add(firstCpn_, stubDays);
        CheckSchedule("LongEnds.StubAtBegin:", n, start, first, Dt.Empty, stop,
          CashflowFlag.RespectLastCoupon | CashflowFlag.RollLastPaymentDate);
        first = firstCpn_ = savedFirstCpn;

        last = lastCpn_ = Dt.Add(lastCpn_, -stubDays);
        CheckSchedule("LongEnds.StubAtEnd:", n, start, Dt.Empty, last, stop,
          CashflowFlag.StubAtEnd | CashflowFlag.RespectLastCoupon
            | CashflowFlag.RollLastPaymentDate);
        last = lastCpn_ = savedLastCpn;

        lastCpn_ = Dt.Add(cycleEnd_, freq_, -2, false);//old method eats last coupon.
        CheckSchedule("LongEnds.Old:", n - 1, start, first, last, stop,
          CashflowFlag.RollLastPaymentDate);
#endif
      }
    }

    [Test, Smoke]
    public void Weekly()
    {
      Data input = new Data()
      {
        Frequency = Frequency.Weekly,
        Calendar = Calendar.None
      };
      DoTest(3,input);
    }

    [Test, Smoke]
    public void Quarterly()
    {
      Data input = new Data()
      {
        Frequency = Frequency.Quarterly,
        Calendar = Calendar.None
      };
      DoTest(3, input);
    }

    [Test, Smoke]
    public void Monthly()
    {
      Data input = new Data()
      {
        Frequency = Frequency.Monthly,
        Calendar = Calendar.None
      };
      DoTest(3, input);
    }

    [Test, Smoke]
    public void SemiAnnual()
    {
      Data input = new Data()
      {
        Frequency = Frequency.SemiAnnual,
        Calendar = Calendar.None
      };
      DoTest(15, input);
    }

    [Test, Smoke]
    public void FreqNone()
    {
      foreach (Dt effective in DateRange(start_, end_))
      {
        Dt maturity = Dt.Add(effective, Frequency.Annual, 5, CycleRule.None);
        Data input = new Data
        {
          Count = 1,
          CycleBegin = effective,
          CycleEnd = maturity,
          Effective = effective,
          Maturity = maturity,
          FirstCoupon = Dt.Empty,
          LastCoupon = Dt.Empty,
          Frequency = Frequency.None,
          BDConvention = BDConvention.Following,
          Calendar = Calendar.None,
          CycleRule = CycleRule.None,
          Flags = CashflowFlag.None
        };
        Data expect = Data.Copy(input);
        expect.FirstCoupon = expect.LastCoupon = maturity;
        CheckSchedule(effective.ToInt().ToString() + ":", expect, input);
      }
      return;
    }

    [Test, Smoke]
    public void FirstCouponOnMaturity()
    {
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20081001);
      Dt maturity = new Dt(20081220);
      Dt cycleEnd = maturity;
      Dt cycleBegin = Dt.Add(cycleEnd, freq, -1, false);

      Data input = new Data()
      {
        Count = 1,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = Dt.Empty,
        LastCoupon = Dt.Empty,
        Frequency = freq,
        BDConvention = BDConvention.Following,
        Calendar = Calendar.None,
        CycleRule = CycleRule.Twentieth,
        Flags = CashflowFlag.RespectLastCoupon
      };
      Data expect = Data.Copy(input);
      expect.LastCoupon = expect.FirstCoupon = maturity;
      expect.CycleEnd = Dt.Roll(cycleEnd, expect.BDConvention, expect.Calendar);
      expect.CycleBegin = Dt.Roll(cycleBegin, expect.BDConvention, expect.Calendar);

      // Missing first and last coupon dates.
      CheckSchedule("MissingBoth:", expect, input);

      // Missing the last coupon only
      input.FirstCoupon = maturity;
      CheckSchedule("MissingLast:", expect, input);

      // Missing the first coupon only
      input.FirstCoupon = Dt.Empty;
      input.LastCoupon = maturity;
      CheckSchedule("MissingFirst:", expect, input);

      // Missing none
      input.FirstCoupon = input.LastCoupon = maturity;
      CheckSchedule("MissingNone:", expect, input);
    }

    [Test, Smoke]
    public void SingleShortPeriod()
    {
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20100714);
      Dt maturity = new Dt(20100728);
      Dt firstCpn = maturity;
      Dt lastCpn = firstCpn;
      Dt cycleEnd = maturity;
      Dt cycleBegin = Dt.Add(cycleEnd, freq, -1, false);

      Data input = new Data()
      {
        Count = 1,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = BDConvention.Following,
        Calendar = Calendar.None,
        CycleRule = CycleRule.None
      };
      Data expect = Data.Copy(input);
      expect.CycleRule = ToCycleRule(maturity, freq);


      // Missing last coupon only
      input.LastCoupon = Dt.Empty;

      expect.Flags = input.Flags = CashflowFlag.AccrueOnCycle;
      CheckSchedule("Has1stCpn.Old:", expect, input);

      expect.Flags = input.Flags = CashflowFlag.AccrueOnCycle
        | CashflowFlag.StubAtEnd;
      CheckSchedule("Has1stCpn.SubAtEnd:", expect,input);

      expect.Flags = input.Flags = CashflowFlag.AccrueOnCycle |
        CashflowFlag.RespectLastCoupon;
      CheckSchedule("Has1stCpn.SubAtBegin:", expect, input);

      // missing both coupon dates.
      input.FirstCoupon = Dt.Empty;

      expect.Flags = input.Flags = CashflowFlag.AccrueOnCycle;
      CheckSchedule("No1stCpn.Old:", expect, input);

      expect.Flags = input.Flags = CashflowFlag.AccrueOnCycle
        | CashflowFlag.RespectLastCoupon;
      CheckSchedule("No1stCpn.SubAtBegin:", expect, input);

      expect.CycleBegin = effective;
      expect.CycleEnd = Dt.Add(expect.CycleBegin, freq, 1, false);
      expect.Flags = input.Flags = CashflowFlag.AccrueOnCycle
        | CashflowFlag.StubAtEnd;
      CheckSchedule("No1stCpn.SubAtEnd:", expect, input);
    }

    [Test, Smoke]
    public void TinyFirstPeriod()
    {
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20080701);
      Dt maturity = new Dt(20120523);
      Dt firstCpn = new Dt(20080723);
      Dt lastCpn = new Dt(20120223);
      Dt cycleEnd = maturity;
      Dt cycleBegin = new Dt(20080523);

      Data input = new Data()
      {
        Count = 17,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = BDConvention.Following,
        Calendar = Calendar.None,
        CycleRule = CycleRule.None,
        Flags = CashflowFlag.RollLastPaymentDate
          | CashflowFlag.RespectLastCoupon
      };
      Data expect = Data.Copy(input);
      expect.CycleRule = ToCycleRule(maturity, freq);

      CheckSchedule("HasLastCpn:", expect, input);

      input.LastCoupon = Dt.Empty;
      CheckSchedule("NoLastCpn:", expect, input);
    }

    [Test, Smoke]
    public void SettleOnHoliday()
    {
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20091212);
      Dt maturity = new Dt(20141212);
      Dt firstCpn = Dt.Empty;
      Dt lastCpn = Dt.Empty;
      Dt cycleEnd = new Dt(20141212);
      Dt cycleBegin = new Dt(20091212);

      Data input = new Data()
      {
        Count = 20,
        Settle = effective,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = BDConvention.Following,
        Calendar = Calendar.None,
        CycleRule = CycleRule.None
      };
      Data expect = Data.Copy(input);
      expect.CycleRule = ToCycleRule(effective, freq);
      expect.FirstCoupon = new Dt(20100312);
      expect.LastCoupon = new Dt(20140912);

      expect.Flags = input.Flags = CashflowFlag.RollLastPaymentDate;
      CheckSchedule("", expect, input);
    }

    [Test, Smoke]
    public void SettleOnFirstCoupon()
    {
      // Note: If settle on a coupon date, then that date should be included
      //       in the generated schedule.

      BDConvention bdc = BDConvention.Modified;
      Calendar cal = Calendar.LNB;
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20080701);
      Dt maturity = new Dt(20120523);
      Dt firstCpn = Dt.Roll(new Dt(20080723), bdc, cal);
      Dt lastCpn = Dt.Empty;
      Dt cycleEnd = new Dt(20120523);
      Dt cycleBegin = new Dt(20080523);
      const int fullCount = 17;

      Data data = new Data()
      {
        Count = fullCount,
        Settle = effective,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = bdc,
        Calendar = cal,
        CycleRule = ToCycleRule(cycleBegin, freq),
        Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate
      };
      Data expect, input;

      // Regular, full schedule.
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.Settle = expect.Settle = firstCpn;
      expect.LastCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      CheckSchedule("Regular:", expect, input);

      // Irregular first coupon, stub at begin, full schedule.
      expect.FirstCoupon = input.FirstCoupon = expect.Settle
        = input.Settle = Dt.Roll(Dt.Add(firstCpn, 7), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      CheckSchedule("StubAtBegin.Irregular:", expect, input);

      // Irregular first coupon, stub at end, full schedule.
      expect.FirstCoupon = input.FirstCoupon = expect.Settle
        = input.Settle = data.FirstCoupon;
      expect.Flags = input.Flags = (data.Flags |= CashflowFlag.StubAtEnd);
      expect.CycleBegin = Dt.Roll(new Dt(20080423), bdc, cal);
      expect.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120423), bdc, cal);
      CheckSchedule("StubAtEnd.Irregular:", expect, input);
    }

    [Test, Smoke]
    public void SettleInMiddle()
    {
      BDConvention bdc = BDConvention.Modified;
      Calendar cal = Calendar.LNB;
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20080701);
      Dt maturity = new Dt(20120523);
      Dt firstCpn = Dt.Roll(new Dt(20080723), bdc, cal);
      Dt lastCpn = Dt.Empty;
      Dt cycleEnd = new Dt(20120523);
      Dt cycleBegin = new Dt(20080523);
      const int fullCount = 17;

      Data data = new Data()
      {
        Count = fullCount,
        Settle = effective,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = bdc,
        Calendar = cal,
        CycleRule = ToCycleRule(cycleBegin, freq),
        Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate
      };
      Data expect, input;

      // Regular, settle in the second period.
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.Settle = expect.Settle = Dt.Roll(Dt.Add(firstCpn, 14), bdc, cal);
      expect.Effective = firstCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20080523), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20080823), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      expect.Count = fullCount - 1;
      CheckSchedule("Regular.Second:", expect, input);

      // Irregular first coupon, stub at begin, settle in the second period.
      expect.Settle = input.Settle = Dt.Roll(Dt.Add(firstCpn, 14), bdc, cal);
      input.FirstCoupon = Dt.Roll(Dt.Add(firstCpn, 7), bdc, cal);
      expect.Effective = input.FirstCoupon;
      CheckSchedule("StubAtBegin.Second:", expect, input);

      // Irregular first coupon, stub at end, settle in the second period.
      expect.Settle = input.Settle = Dt.Roll(Dt.Add(firstCpn, 14), bdc, cal);
      expect.Flags = (input.Flags |= CashflowFlag.StubAtEnd);
      input.FirstCoupon = firstCpn;
      expect.Effective = firstCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20080723), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20081023), bdc, cal);
      expect.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120423), bdc, cal);
      CheckSchedule("StubAtEnd.Second:", expect, input);

      // Regular, settle in the third period.
      Dt secondCpn = Dt.Roll(new Dt(20080823), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.Settle = expect.Settle = Dt.Roll(Dt.Add(secondCpn, 14), bdc, cal);
      expect.Effective = secondCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20080823), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20081123), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      expect.Count = fullCount - 2;
      CheckSchedule("Regular.Third:", expect, input);

      // Irregular first coupon, stub at begin, settle in the third period.
      expect.Settle = input.Settle = Dt.Roll(Dt.Add(secondCpn, 14), bdc, cal);
      input.FirstCoupon = Dt.Roll(Dt.Add(firstCpn, 7), bdc, cal);
      expect.Effective = secondCpn;
      CheckSchedule("StubAtBegin.Third:", expect, input);

      // Irregular first coupon, stub at end, settle in the third period.
      secondCpn = Dt.Roll(new Dt(20081023), bdc, cal);
      expect.Settle = input.Settle = Dt.Roll(Dt.Add(secondCpn, 14), bdc, cal);
      expect.Flags = (input.Flags |= CashflowFlag.StubAtEnd);
      input.FirstCoupon = firstCpn;
      expect.Effective = secondCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20081023), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20090123), bdc, cal);
      expect.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120423), bdc, cal);
      CheckSchedule("StubAtEnd.Third:", expect, input);

      // Regular, settle on the third coupon date.
      secondCpn = Dt.Roll(new Dt(20080823), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.Settle = expect.Settle = Dt.Roll(new Dt(20081123), bdc, cal);
      expect.Effective = secondCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20080823), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20081123), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      expect.Count = fullCount - 2;
      CheckSchedule("Regular.OnThird:", expect, input);

      // Irregular first coupon, stub at begin, settle on the third coupon.
      expect.Settle = input.Settle = Dt.Roll(new Dt(20081123), bdc, cal);
      input.FirstCoupon = Dt.Roll(Dt.Add(firstCpn, 7), bdc, cal);
      expect.Effective = secondCpn;
      CheckSchedule("StubAtBegin.OnThird:", expect, input);

      // Irregular first coupon, stub at end, settle on the third period coupon.
      secondCpn = Dt.Roll(new Dt(20081023), bdc, cal);
      expect.Settle = input.Settle = Dt.Roll(new Dt(20090123), bdc, cal);
      expect.Flags = (input.Flags |= CashflowFlag.StubAtEnd);
      input.FirstCoupon = firstCpn;
      expect.Effective = secondCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20081023), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20090123), bdc, cal);
      expect.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120423), bdc, cal);
      CheckSchedule("StubAtEnd.OnThird:", expect, input);

      // Regular, settle in the next to last period.
      Dt secondToLastCpn = Dt.Roll(new Dt(20111123), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.Settle = expect.Settle = 
        Dt.Roll(Dt.Add(secondToLastCpn, 14), bdc, cal);
      expect.Effective = secondToLastCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20111123), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120223), bdc, cal);
      expect.Count = 2;
      CheckSchedule("Regular.NextToLast:", expect, input);

      // Irregular first coupon, stub at begin, settle in the next to last period.
      expect.Settle = input.Settle = Dt.Roll(Dt.Add(secondToLastCpn, 14), bdc, cal);
      input.FirstCoupon = Dt.Roll(Dt.Add(firstCpn, 7), bdc, cal);
      expect.Effective = secondToLastCpn;
      CheckSchedule("StubAtBegin.NextToLast:", expect, input);

      // Irregular first coupon, stub at end, settle in the next to last period.
      secondToLastCpn = Dt.Roll(new Dt(20120123), bdc, cal);
      expect.Settle = input.Settle = 
        Dt.Roll(Dt.Add(secondToLastCpn, 14), bdc, cal);
      expect.Flags = (input.Flags |= CashflowFlag.StubAtEnd);
      input.FirstCoupon = firstCpn;
      expect.Effective = secondToLastCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20120123), bdc, cal);
      expect.FirstCoupon = Dt.Roll(new Dt(20120423), bdc, cal);
      expect.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect.LastCoupon = Dt.Roll(new Dt(20120423), bdc, cal);
      CheckSchedule("StubAtEnd.NextToLast:", expect, input);
    }

    [Test, Smoke]
    public void SettleOnLastCoupon()
    {
      // Note: If settle on a coupon date, then that date should be included
      //       in the generated schedule.

      BDConvention bdc = BDConvention.Modified;
      Calendar cal = Calendar.LNB;
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20080701);
      Dt maturity = new Dt(20120523);
      Dt firstCpn = Dt.Roll(new Dt(20080723), bdc, cal);
      Dt lastCpn = Dt.Roll(new Dt(20120223), bdc, cal);
      Dt cycleEnd = new Dt(20120523);
      Dt cycleBegin = new Dt(20080523);
      const int fullCount = 17;

      Data data = new Data()
      {
        Count = fullCount,
        Settle = effective,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = bdc,
        Calendar = cal,
        CycleRule = ToCycleRule(cycleBegin, freq),
        Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate
      };
      Data expect, input;

      // Regular, full schedule.
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.LastCoupon = Dt.Empty;
      CheckSchedule("Regular.Full:", expect, input);

      // Regular, settle on last coupon.
      input.LastCoupon = lastCpn;
      input.Settle = expect.Settle = lastCpn;
      expect.CycleBegin = expect.Effective = Dt.Roll(new Dt(20111123), bdc, cal);
      expect.FirstCoupon = expect.LastCoupon = lastCpn;
      expect.Count = 2;
      CheckSchedule("Regular.Short:", expect, input);

      // Irregular last coupon, stub at begin, full schedule.
      lastCpn = Dt.Roll(Dt.Add(lastCpn, -7), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      expect.LastCoupon = input.LastCoupon = lastCpn;
      expect.Count = fullCount;
      CheckSchedule("StubAtBegin.Irregular.Full:", expect, input);

      // Irregular last coupon, stub at begin, settle in last period.
      input.Settle = expect.Settle = lastCpn;
      expect.CycleBegin = expect.Effective = Dt.Roll(new Dt(20111123), bdc, cal);
      expect.FirstCoupon = expect.LastCoupon = lastCpn;
      expect.Count = 2;
      CheckSchedule("StubAtBegin.Irregular.Short:", expect, input);

      // Irregular last coupon, stub at end, full schedule.
      lastCpn = data.LastCoupon;
      data.Flags |= CashflowFlag.StubAtEnd;
      data.CycleBegin = Dt.Roll(new Dt(20080423), bdc, cal);
      data.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      CheckSchedule("StubAtEnd.Irregular.Full:", expect, input);

      // Irregular last coupon, stub at end, settle in last period.
      input.Settle = expect.Settle = lastCpn;
      expect.Effective = expect.CycleBegin = Dt.Roll(new Dt(20120123), bdc, cal);
      expect.FirstCoupon = expect.LastCoupon = lastCpn;
      expect.Count = 2;
      CheckSchedule("StubAtEnd.Irregular.Short:", expect, input);
    }

    [Test, Smoke]
    public void SettleInLastPeriod()
    {
      BDConvention bdc = BDConvention.Modified;
      Calendar cal = Calendar.LNB;
      Frequency freq = Frequency.Quarterly;
      Dt effective = new Dt(20080701);
      Dt maturity = new Dt(20120523);
      Dt firstCpn = Dt.Roll(new Dt(20080723), bdc, cal);
      Dt lastCpn = Dt.Roll(new Dt(20120223), bdc, cal);
      Dt cycleEnd = new Dt(20120523);
      Dt cycleBegin = new Dt(20080523);
      const int fullCount = 17;

      Data data = new Data()
      {
        Count = fullCount,
        Settle = effective,
        CycleBegin = cycleBegin,
        CycleEnd = cycleEnd,
        Effective = effective,
        Maturity = maturity,
        FirstCoupon = firstCpn,
        LastCoupon = lastCpn,
        Frequency = freq,
        BDConvention = bdc,
        Calendar = cal,
        CycleRule = ToCycleRule(cycleBegin, freq),
        Flags = CashflowFlag.RespectLastCoupon |
          CashflowFlag.RollLastPaymentDate
      };
      Data expect, input;

      // Regular, full schedule.
      expect = Data.Copy(data);
      input = Data.Copy(data);
      input.LastCoupon = Dt.Empty;
      CheckSchedule("Regular.Full:", expect, input);

      // Regular, settle in last period.
      input.LastCoupon = lastCpn;
      input.Settle = expect.Settle = Dt.Roll(Dt.Add(lastCpn, 7), bdc, cal);
      expect.CycleBegin = expect.Effective = lastCpn;
      expect.FirstCoupon = expect.LastCoupon = maturity;
      expect.Count = 1;
      CheckSchedule("Regular.Short:", expect, input);

      // Irregular last coupon, stub at begin, full schedule.
      lastCpn = Dt.Roll(Dt.Add(lastCpn, -7), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      expect.LastCoupon = input.LastCoupon = lastCpn;
      expect.Count = fullCount;
      CheckSchedule("StubAtBegin.Irregular.Full:", expect, input);

      // Irregular last coupon, stub at begin, settle in last period.
      input.Settle = expect.Settle = Dt.Roll(Dt.Add(lastCpn, 7), bdc, cal);
      input.LastCoupon = lastCpn;
      expect.Effective = lastCpn;
      expect.CycleBegin = Dt.Roll(Dt.Add(lastCpn, freq, -1, data.CycleRule),
        bdc, cal);
      expect.FirstCoupon = expect.LastCoupon = maturity;
      expect.Count = 1;
      CheckSchedule("StubAtBegin.Irregular.Short:", expect, input);

      // Irregular last coupon, stub at end, full schedule.
      lastCpn = data.LastCoupon;
      data.Flags |= CashflowFlag.StubAtEnd;
      data.CycleBegin = Dt.Roll(new Dt(20080423), bdc, cal);
      data.CycleEnd = Dt.Roll(new Dt(20120723), bdc, cal);
      expect = Data.Copy(data);
      input = Data.Copy(data);
      CheckSchedule("StubAtEnd.Irregular.Full:", expect, input);

      // Irregular last coupon, stub at end, settle in last period.
      input.Settle = expect.Settle = Dt.Roll(Dt.Add(lastCpn, 7), bdc, cal);
      input.LastCoupon = lastCpn;
      expect.Effective = lastCpn;
      expect.CycleBegin = Dt.Roll(new Dt(20120123), bdc, cal);
      expect.FirstCoupon = expect.LastCoupon = maturity;
      expect.Count = 1;
      CheckSchedule("StubAtEnd.Irregular.Short:", expect, input);
    }

  } // class TestScheduleDates
}
