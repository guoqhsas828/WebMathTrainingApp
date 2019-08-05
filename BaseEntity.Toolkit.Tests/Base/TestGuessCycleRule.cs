//
// Copyright (c) 2004-2018,   . All rights reserved.
//

using System.Collections.Generic;
using BaseEntity.Toolkit.Base;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class GuessCycleRuleTest : ToolkitTestBase
  {
    BDConvention bdc = BDConvention.Modified;
    Calendar cal = Calendar.None;
    CycleRule rule = CycleRule.None;
    CashflowFlag flags = CashflowFlag.None;

    private static IEnumerable<Dt> DateRange(Dt start, Dt end)
    {
      for (Dt dt = start; dt < end; dt = Dt.Add(dt, 1))
        yield return dt;
    }

    private static CycleRule ToAnchorDay(Dt date, Frequency freq)
    {
      return freq == Frequency.Weekly || freq == Frequency.BiWeekly
        ? (CycleRule.Monday + (int)date.DayOfWeek())
        : (date.Day == 31 ? CycleRule.EOM
          : (CycleRule.First + date.Day - 1));
    }

    /// <summary>
    ///   Test guessing cycle rule with Quarterly frequency
    /// </summary>
    [Test, Smoke]
    public void Quarterly()
    {
      Frequency freq = Frequency.Quarterly;
      const int n = 8;

      // Test every day from 1 Aug 2010 to 31 Dec 2012.
      Dt start = new Dt(20100801);
      Dt end = new Dt(20130101);

      foreach (Dt dt in DateRange(start, end))
      {
        CycleRule expect = ToAnchorDay(dt, freq);
        Dt effective = dt;
        Dt maturity = Dt.Add(dt, freq, n, false);
        Dt firstCpn = Dt.Roll(Dt.Add(dt, freq, 1, false), bdc, cal);
        Dt lastCpn = Dt.Roll(Dt.Add(dt, freq, n - 1, false), bdc, cal);
        CycleRule actual = Schedule.GuessCycleRule(effective, firstCpn,
          lastCpn, maturity, freq, bdc, cal, rule, flags);
        Assert.AreEqual(expect, actual, "Regular@" + dt.ToInt());

        Dt shortMaturity = Dt.Add(lastCpn, 35);
        actual = Schedule.GuessCycleRule(effective, firstCpn,
          lastCpn, shortMaturity, freq, bdc, cal, rule, flags);
        Assert.AreEqual(expect, actual, "ShortLast@" + dt.ToInt());

        Dt shortEffective = Dt.Add(effective, 15);
        actual = Schedule.GuessCycleRule(shortEffective, firstCpn,
          lastCpn, maturity, freq, bdc, cal, rule, flags);
        Assert.AreEqual(expect, actual, "ShortFirst@" + dt.ToInt());
      }
      return;
    }

    /// <summary>
    ///   Test guessing cycle rule with Weekly frequency
    /// </summary>
    [Test, Smoke]
    public void Weekly()
    {
      Frequency freq = Frequency.Weekly;
      Dt effective = new Dt(20100105); // Tuesday
      Dt firstCpn = new Dt(20100119); // Tuesday
      Dt lastCpn = new Dt(20100202); // Tuesday
      Dt maturity = new Dt(20100202); // Tuesday
      CycleRule expect = CycleRule.Tuesday;
      CycleRule actual = Schedule.GuessCycleRule(effective, firstCpn,
        lastCpn, maturity, freq, bdc, cal, rule, flags);
      Assert.AreEqual(expect, actual, "Regular");
    }

    /// <summary>
    ///   Test guessing cycle rule with BiWeekly frequency
    /// </summary>
    [Test, Smoke]
    public void BiWeekly()
    {
      Frequency freq = Frequency.BiWeekly;
      Dt effective = new Dt(20100105); // Tuesday
      Dt firstCpn = new Dt(20100119); // Tuesday
      Dt lastCpn = new Dt(20100202); // Tuesday
      Dt maturity = new Dt(20100202); // Tuesday
      CycleRule expect = CycleRule.Tuesday;
      CycleRule actual = Schedule.GuessCycleRule(effective, firstCpn,
        lastCpn, maturity, freq, bdc, cal, rule, flags);
      Assert.AreEqual(expect, actual, "Regular");
    }
  }
}
