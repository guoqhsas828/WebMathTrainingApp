//
// Copyright (c) 2004-2018,   . All rights reserved.
//
using System;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Tests
{
  using NUnit.Framework;

  [TestFixture]
  public class TestDtValidation
  {
    [TestCase(1, 1, 2150, 0, 0)]
    [TestCase(1, 1, 2255, 0, 0)]
    [TestCase(31, 12, 2155, 23, 59)]
    [TestCase(31, 12, 2155, 0, 59)]
    [TestCase(31, 12, 2155, 23, 0)]
    [TestCase(1, 8, 1814, 0, 0)]
    [TestCase(31, 12, 1899, 23, 59)]
    [TestCase(1, 1, 1, 0, 0)]
    [TestCase(31, 5, 2015, 13, 30)]
    [TestCase(20, 4, 2015, 2, 13)]
    [TestCase(31, 12, 2149, 23, 50)]
    [TestCase(1, 1, 1900, 0, 0)]
    public void TestDtDateTime(int day, int month, int year, int hour, int minute)
    {
      var date = new DateTime(year, month, day, hour, minute, 0);
      var dt = new Dt(date);
      if (date < Dt.MinValue.ToDateTime())
      {
        Assert.AreEqual(Dt.Empty, dt);
        TestRoundTrip(dt);
      }
      else if (date > Dt.MaxValue.ToDateTime())
      {
        Assert.AreEqual(Dt.MaxValue, dt);
        TestRoundTrip(dt);
      }
      else
      {
        Assert.AreEqual(true, IsEqual(dt, date));
        TestRoundTrip(dt);
      }
    }


    private static void TestRoundTrip(Dt dt)
    {
      var dtToDateTime = dt.ToDateTime();
      var dtRound = new Dt(dtToDateTime);
      Assert.AreEqual(dt, dtRound);
    }


    private static bool IsEqual(Dt dt, DateTime dTime)
    {
      return (dt.Day == dTime.Day && dt.Month == dTime.Month && dt.Year == dTime.Year &&
              dt.Hour == dTime.Hour && dt.Minute == 10*(dTime.Minute/10));
    }


    [TestCase(33, 1, 2014)]
    [TestCase(1, 13, 2014)]
    [TestCase(0, 0, 0)]
    public void TestDtDateTime(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () => new Dt(new DateTime(year, month, day)));
    }

    public void TestDtDateTimeExpectException(int day, int month, int year)
    {
    }

    [TestCase(32, 1, 2014)]
    [TestCase(1, 14, 2014)]
    [TestCase(1, 1, 2512)]
    public void TestDt01(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () => new Dt(day, (Month)month, year));
    }

    [TestCase(32, 1, 2014)]
    [TestCase(1, 14, 2014)]
    [TestCase(1, 1, 2152)]
    public void TestDt02(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () => new Dt(day, month, year));
    }

    [TestCase(21520101)]
    [TestCase(20141301)]
    [TestCase(20140133)]
    public void TestDt03(int date)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () => new Dt(date));
    }

    [TestCase(32, 1, 2014, 1, 1, 1)]
    [TestCase(1, 13, 2014, 1, 1, 1)]
    [TestCase(1, 1, 2159, 1, 1, 1)]
    [TestCase(1, 1, 2014, 25, 1, 1)]
    [TestCase(1, 1, 2014, 1, 1440, 1)]
    [TestCase(1, 1, 2014, 23, 70, 1)]
    public void TestDt04(int day, int month, int year, int hour, int minute, int second)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () => new Dt(day, month, year, hour, minute, second));
    }


    [TestCase(255)] //Test n*365 days from 1/1/1900
    public void TestDt05(double time)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () => new Dt(time));
    }

    [TestCase(1, 1, 2014, 250)]
    [TestCase(1, 1, 2152, 0)]
    [TestCase(33, 1, 2014, 0)]
    [TestCase(1, 14, 2014, 0)]
    public void TestDt06(int day, int month, int year, double time)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = CreateDt(year, month, day);
          var dt2 = new Dt(dt, time);
        });
    }

    [TestCase(32, 1, 2014)]
    [TestCase(1, 14, 2014)]
    public void TestStartOfDay(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.StartOfDay(CreateDt(year, month, day));
        });
    }

    [TestCase(32, 1, 2014)]
    [TestCase(1, 14, 2014)]
    public void TestEndOfDay(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.EndOfDay(CreateDt(year, month, day));
        });
    }

    [TestCase(32, 1, 2014)]
    [TestCase(29, 2, 2001)]
    [TestCase(27, 2, 2152)]
    public void TestToJulian(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = CreateDt(year, month, day);
          dt.ToJulian();
        });
    }
    
    [TestCase(1, 1, 2014, 136)]
    [TestCase(1, 1, 2152, 0)]
    [TestCase(33, 1, 2014, 0)]
    [TestCase(1, 14, 2014, 0)]
    public void TestDtAddRelativeTimeInYear(int day, int month, int year, double years)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var date = CreateDt(year, month, day);
          var relativeTime = (RelativeTime) years;
          var lastDate = Dt.Add(date, relativeTime);
        });
    }

    [TestCase(1, 1, 2014, 60000)]
    [TestCase(33, 1, 2014, 0)]
    [TestCase(1, 14, 2014, 0)]
    [TestCase(1, 2, 2152, 0)]
    public void TestDtAddDay(int day, int month, int year, int n)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.Add(CreateDt(year, month, day), n);
        });
    }

    [TestCase(1, 13, 2014, 1, 1, 2014)]
    [TestCase(1, 1, 2152, 1, 1, 2014)]
    [TestCase(33, 1, 2014, 1, 1, 2014)]
    [TestCase(1, 1, 2014, 1, 13, 2014)]
    [TestCase(1, 1, 2014, 1, 1, 2152)]
    [TestCase(1, 1, 2014, 33, 1, 2014)]
    public void TestTimeInYears(int day1, int month1, int year1, int day2, int month2, int year2)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var diff = Dt.TimeInYears(CreateDt(year1, month1, day1), CreateDt(year2, month2, day2));
        });
    }

    [TestCase(1, 1, 2152)]
    [TestCase(33, 1, 2014)]
    [TestCase(1, 14, 2014)]
    public void TestToExcelDate(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.ToExcelDate(CreateDt(year, month, day));
        });
    }

    [TestCase(91500)] //test add days from 1/1/1900
    public void TestFromExcelDate(int n)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.FromExcelDate(n);
        });
    }

    [TestCase(1, 1, 2152)]
    [TestCase(33, 1, 2014)]
    [TestCase(1, 14, 2014)]
    public void TestIsValidSettlement(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = new Dt(day, month, year);
          var lastDate = CalendarCalc.IsValidSettlement(Calendar.NYB, dt.Day, dt.Month, dt.Year);
        });
    }

    [TestCase(23, 12, 2152)]
    [TestCase(23, 13, 20143)]
    public void TestIsLastDayOfMonth(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var test1 = CreateDt(year, month, day).IsLastDayOfMonth();
        });
    }

    [TestCase(32, 1, 2014, "NYB")]
    [TestCase(32, 1, 2014, "TGT")]
    [TestCase(32, 1, 2014, "LNB")]
    [TestCase(32, 1, 2014, "PSB")]
    [TestCase(32, 1, 2014, "SFS")]
    [TestCase(1, 13, 2014, "NYB")]
    [TestCase(1, 13, 2014, "TGT")]
    [TestCase(1, 13, 2014, "LNB")]
    [TestCase(1, 13, 2014, "PSB")]
    [TestCase(1, 13, 2014, "SFS")]
    [TestCase(1, 1, 2152, "NYB")]
    [TestCase(1, 1, 2152, "TGT")]
    [TestCase(1, 1, 2152, "LNB")]
    [TestCase(1, 1, 2152, "PSB")]
    [TestCase(1, 1, 2152, "SFS")]
    public void TestIsLastBusinessDayOfMonth(int day, int month, int year, string cal)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var calendar = CalendarCalc.GetCalendar(cal);
          var res = Dt.IsLastBusinessDayOfMonth(CreateDt(year, month, day), calendar);
        });
    }

    [TestCase(1, 1, 2151, "0 Y")]
    [TestCase(33, 1, 2014, "0 Y")]
    [TestCase(1, 14, 2014, "0 Y")]
    [TestCase(1, 1, 2149, "10 Y")]
    [TestCase(1, 1, 2151, "0 Month")]
    [TestCase(33, 1, 2014, "0 Month")]
    [TestCase(1, 14, 2014, "0 Month")]
    [TestCase(1, 1, 2149, "13 Month")]
    public void TestDtAddTenor(int day, int month, int year, string str)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var tenor = Tenor.Parse(str);
          var lastDate = Dt.Add(CreateDt(year, month, day), tenor);
        });
    }

    [TestCase(33, 1, 2014, 0, CycleRule.None)]
    [TestCase(1, 14, 2014, 0, CycleRule.None)]
    [TestCase(1, 2, 2152, 0, CycleRule.None)]
    [TestCase(1, 1, 2014, 8000, CycleRule.Monday)]
    [TestCase(1, 1, 2014, 8000, CycleRule.Sunday)]
    [TestCase(32, 1, 2014, 10, CycleRule.IMM)]
    public void TestDtAddWeeks(int day, int month, int year, int n, CycleRule cycleRule)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.AddWeeks(CreateDt(year, month, day), n, cycleRule);
        });
    }

    [TestCase(18, 9, 2014, 2400, CycleRule.IMM)]
    [TestCase(18, 9, 2014, 2400, CycleRule.IMMNZD)]
    [TestCase(18, 9, 2014, 2400, CycleRule.First)]
    [TestCase(18, 9, 2014, 2400, CycleRule.Thirtieth)]
    [TestCase(18, 9, 2014, 2400, CycleRule.EOM)]
    [TestCase(18, 9, 2014, 2400, CycleRule.None)]
    public void TestDtAddMonths(int day, int month, int year, int n, CycleRule cycleRule)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.AddMonths(CreateDt(year, month, day), n, cycleRule);
        });
    }

    [TestCase(1, 1, 2151, Frequency.None, 0, CycleRule.None)]
    [TestCase(33, 1, 2149, Frequency.None, 0, CycleRule.None)]
    [TestCase(1, 14, 2151, Frequency.None, 0, CycleRule.None)]
    [TestCase(1, 1, 2014, Frequency.TwentyEightDays, 60000, CycleRule.None)]
    [TestCase(1, 1, 2014, Frequency.BiWeekly, 60000, CycleRule.None)]
    [TestCase(1, 1, 2014, Frequency.Weekly, 60000, CycleRule.None)]
    [TestCase(1, 1, 2014, Frequency.Daily, 60000, CycleRule.None)]
    public void TestDtAddFromFrequency01(int day, int month, int year, Frequency freq, int n, CycleRule crule)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.Add(CreateDt(year, month, day), freq, n, crule);
        });
    }

    [TestCase(1, 1, 2014, Frequency.Daily, 50000)]
    [TestCase(1, 1, 2014, Frequency.TwentyEightDays, 2000)]
    [TestCase(1, 1, 2014, Frequency.Quarterly, 560)]
    [TestCase(1, 1, 2014, Frequency.Weekly, 8000)]
    [TestCase(1, 1, 2014, Frequency.BiWeekly, 4000)]
    public void TestDtAddFromFrequency02(int day, int month, int year, Frequency freq, int n)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var anchorDay = new Dt(20140320);
          var eomDeterminationDay = new Dt(20140330);
          var dt = Dt.Add(CreateDt(year, month, day), freq, n, anchorDay, eomDeterminationDay, true);
          var dt2 = Dt.Add(CreateDt(year, month, day), freq, n, anchorDay, eomDeterminationDay, false);
        });
    }

    [TestCase(1, 1, 2014, "140 Y")]
    [TestCase(1, 1, 2114, "1000 M")]
    [TestCase(1, 1, 2149, "1000 D")]
    public void TestDtAddFromString(int day, int month, int year, string str)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.Add(CreateDt(year, month, day), str);
        });
    }

    [TestCase(18, 9, 2014, 2400, false)]
    [TestCase(18, 9, 2014, 2400, true)]
    [TestCase(18, 9, 2152, 12, false)]
    [TestCase(18, 9, 2152, 12, true)]
    public void TestDtAddMonth01(int day, int month, int year, int n, bool eomRule)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.AddMonth(CreateDt(year, month, day), n, eomRule);
        });
    }

    [TestCase(2100, 7, 20, 600)]
    public void TestDtAddMonth02(int year, int month, int day, int n)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var date = CreateDt(year, month, day);
          var anchorDate = CreateDt(2014, 1, 1);
          var eomDeterminationDate = CreateDt(2014, 1, 1);
          var lastDate = Dt.Add(date, Frequency.Monthly, n, anchorDate, eomDeterminationDate, false);
        });
    }

    [TestCase(1, 1, 2014, 150, TimeUnit.Years)]
    [TestCase(33, 1, 2014, 10, TimeUnit.Years)]
    [TestCase(1, 13, 2014, 10, TimeUnit.Years)]
    [TestCase(1, 1, 2151, 10, TimeUnit.Years)]
    [TestCase(1, 1, 2014, 10, TimeUnit.None)]
    [TestCase(1, 1, 2149, 150, TimeUnit.Weeks)]
    [TestCase(1, 1, 2149, 15, TimeUnit.Months)]
    public void TestDtAddTimeUnit(int day, int month, int year, int n, TimeUnit timeUnit)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.Add(CreateDt(year, month, day), n, timeUnit);
        });
    }

    [TestCase(1, 1, 2152, 0)]
    [TestCase(1, 13, 2014, 0)]
    [TestCase(1, 33, 2014, 0)]
    [TestCase(1, 1, 2014, 51100)]
    public void TestDtAddDays(int year, int month, int day, int days)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.AddDays(CreateDt(year, month, day), 0, Calendar.None);
        });
    }

    [TestCase(1, 1, 2149, "10 Y")]
    [TestCase(1, 1, 2149, "13 Month")]
    [TestCase(1, 1, 2151, "0 Y")]
    [TestCase(33, 1, 2014, "0 Y")]
    [TestCase(1, 14, 2014, "0 Y")]
    [TestCase(21, 1, 2149, "10 Y")]
    [TestCase(1, 1, 2151, "0 Month")]
    [TestCase(33, 1, 2014, "0 Month")]
    [TestCase(1, 14, 2014, "0 Month")]
    [TestCase(21, 1, 2149, "13 Month")]
    public void TestCDSMaturity(int day, int month, int year, string str)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var tenor = Tenor.Parse(str);
          var lastDate = Dt.CDSMaturity(CreateDt(year, month, day), tenor);
        });
    }

    [TestCase(21, 1, 2149, 10, TimeUnit.Years)]
    [TestCase(21, 1, 2149, 14, TimeUnit.Months)]
    public void TestCDSMaturity02(int day, int month, int year, int n, TimeUnit timeUnit)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var lastDate = Dt.CDSMaturity(CreateDt(year, month, day), n, timeUnit);
        });
    }

    [TestCase(1, 1, 2151)]
    [TestCase(33, 1, 2014)]
    [TestCase(1, 14, 2014)]
    public void TestSNACFirstAccrualStart(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.SNACFirstAccrualStart(CreateDt(year, month, day), Calendar.None), "None");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.SNACFirstAccrualStart(CreateDt(year, month, day), Calendar.TGT), "TGT");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.SNACFirstAccrualStart(CreateDt(year, month, day), Calendar.NYB), "NYB");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.SNACFirstAccrualStart(CreateDt(year, month, day), Calendar.LNB), "LNB");
    }

    [TestCase(32, 1, 2014, 0, TimeUnit.Months)]
    [TestCase(1, 13, 2014, 0, TimeUnit.Months)]
    [TestCase(1, 1, 2152, 0, TimeUnit.Months)]
    [TestCase(32, 1, 2014, 0, TimeUnit.Years)]
    public void TestLiborMaturity(int day, int month, int year, int n, TimeUnit timeUnit)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var tenor = new Tenor(n, timeUnit);
          var dt = Dt.LiborMaturity(CreateDt(year, month, day), tenor, Calendar.NYB, BDConvention.Modified);
        });
    }

    [TestCase(1, 1, 2114, 1000, TimeUnit.Months)]
    public void TestLiborMaturity2(int day, int month, int year, int n, TimeUnit timeUnit)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var tenor = new Tenor(n, timeUnit);
          var dt = Dt.LiborMaturity(CreateDt(year, month, day), tenor, Calendar.NYB, BDConvention.Modified);
        });
    }

    [TestCase(32, 1, 2014)]
    [TestCase(1, 13, 2014)]
    [TestCase(1, 1, 2152)]
    public void TestDtCDSRoll01(int day, int month, int year)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.CDSRoll(CreateDt(year, month, day));
        });
    }

    [TestCase(32, 1, 2014, false)]
    [TestCase(1, 13, 2014, false)]
    [TestCase(1, 1, 2152, false)]
    [TestCase(32, 1, 2014, true)]
    [TestCase(1, 13, 2014, true)]
    [TestCase(1, 1, 2152, true)]
    public void TestDtCDSRoll02(int day, int month, int year, bool isStandard)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.CDSRoll(CreateDt(year, month, day), isStandard);
        });
    }

    [TestCase(20140132)]
    [TestCase(20141301)]
    [TestCase(21520101)]
    public void TestDtImmNext(int date)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.ImmNext(CreateDt(date));
        });
    }

    [TestCase(1, 13, 2014, "EDZ8")]
    [TestCase(1, 1, 2152, "EDZ8")]
    public void TestImmDate01(int day, int month, int year, string code)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.ImmDate(CreateDt(year, month, day), code);
        });
    }

    [TestCase(1, 13, 2014, "EDZ8", "None")]
    [TestCase(1, 13, 2014, "EDZ8", "IMMNZD")]
    [TestCase(1, 13, 2014, "EDZ8", "IMM")]
    [TestCase(1, 1, 2152, "EDZ8", "None")]
    [TestCase(1, 1, 2152, "EDZ8", "IMMNZD")]
    [TestCase(1, 1, 2152, "EDZ8", "IMM")]
    public void TestImmDate02(int day, int month, int year, string code, string cycleRule)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var cr = (CycleRule) Enum.Parse(typeof(CycleRule), cycleRule);
          var res = Dt.ImmDate(CreateDt(year, month, day), code, cr);
        });
    }

    [TestCase(DayCount.ActualActualBond)]
    [TestCase(DayCount.ActualActualEuro)]
    [TestCase(DayCount.ActualActual)]
    [TestCase(DayCount.Actual365L)]
    [TestCase(DayCount.Actual365Fixed)]
    [TestCase(DayCount.Actual360)]
    [TestCase(DayCount.Thirty360Isma)]
    [TestCase(DayCount.ThirtyE360)]
    [TestCase(DayCount.Thirty360)]
    [TestCase(DayCount.Actual366)]
    [TestCase(DayCount.OneOne)]
    [TestCase(DayCount.Months)]
    [TestCase(DayCount.None)]
    public void TestFraction01(DayCount dc)
    {
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(), new Dt(20140930), dc), "start day");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), CreateDt(2014, 9, 31), dc), "end day");
    }

    [TestCase(DayCount.Actual365Fixed, Frequency.Monthly)]
    [TestCase(DayCount.Actual360, Frequency.Monthly)]
    [TestCase(DayCount.Thirty360Isma, Frequency.Monthly)]
    [TestCase(DayCount.ThirtyE360, Frequency.Monthly)]
    [TestCase(DayCount.Thirty360, Frequency.Monthly)]
    [TestCase(DayCount.Actual366, Frequency.Monthly)]
    [TestCase(DayCount.OneOne, Frequency.Monthly)]
    [TestCase(DayCount.Months, Frequency.Monthly)]
    [TestCase(DayCount.None, Frequency.Monthly)]
    public void TestFraction02(DayCount dc, Frequency freq)
    {
      Dt start = new Dt(20140925), end = new Dt(20141010);
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(), new Dt(20140930), start, end, dc, freq), "pstart");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), CreateDt(2014, 9, 31), start, end, dc, freq), "pend");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), new Dt(20141005), CreateDt(2014, 9, 31), end, dc, freq), "start");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), new Dt(20140930), start, CreateDt(2014, 9, 31), dc, freq), "end");
    }

    [TestCase(DayCount.ActualActualBond, Frequency.Monthly)]
    [TestCase(DayCount.ActualActual, Frequency.Monthly)]
    [TestCase(DayCount.ActualActualEuro, Frequency.Monthly)]
    [TestCase(DayCount.Actual365L, Frequency.Monthly)]
    public void TestFraction03(DayCount dc, Frequency freq)
    {
      Dt start = new Dt(20140925), end = new Dt(20141010);
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(), new Dt(20140930), start, end, dc, freq), "pstart");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), CreateDt(2014, 9, 31), start, end, dc, freq), "pend");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), new Dt(20141005), CreateDt(2014, 9, 31), end, dc, freq), "start");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.Fraction(new Dt(20140831), new Dt(20140930), start, CreateDt(2014, 9, 31), dc, freq), "end");
    }

    [TestCase(DayCount.ActualActualBond)]
    [TestCase(DayCount.ActualActualEuro)]
    [TestCase(DayCount.ActualActual)]
    [TestCase(DayCount.Actual365L)]
    [TestCase(DayCount.Actual365Fixed)]
    [TestCase(DayCount.Actual360)]
    [TestCase(DayCount.Thirty360Isma)]
    [TestCase(DayCount.ThirtyE360)]
    [TestCase(DayCount.Thirty360)]
    [TestCase(DayCount.Actual366)]
    [TestCase(DayCount.OneOne)]
    [TestCase(DayCount.Months)]
    [TestCase(DayCount.None)]
    public void TestFractionDays01(DayCount dc)
    {
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.FractionDays(new Dt(), new Dt(20140930), dc), "start day");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.FractionDays(new Dt(20140831), CreateDt(2014, 9, 31), dc), "end day");
    }

    [TestCase(DayCount.ActualActualBond)]
    [TestCase(DayCount.ActualActualEuro)]
    [TestCase(DayCount.ActualActual)]
    [TestCase(DayCount.Actual365L)]
    [TestCase(DayCount.Actual365Fixed)]
    [TestCase(DayCount.Actual360)]
    [TestCase(DayCount.Thirty360Isma)]
    [TestCase(DayCount.ThirtyE360)]
    [TestCase(DayCount.Thirty360)]
    [TestCase(DayCount.Actual366)]
    [TestCase(DayCount.OneOne)]
    [TestCase(DayCount.Months)]
    [TestCase(DayCount.None)]
    public void TestFractionDays02(DayCount dc)
    {
      Dt start = new Dt(20140925), end = new Dt(20141010);
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.FractionDays(new Dt(), new Dt(20140930), start, end, dc), "pstart");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.FractionDays(new Dt(20140831), CreateDt(2014, 9, 31), start, end, dc), "pend");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.FractionDays(new Dt(20140831), new Dt(20141005), CreateDt(2014, 9, 31), end, dc), "start");
      Assert.Throws<ArgumentOutOfRangeException>(() => Dt.FractionDays(new Dt(20140831), new Dt(20140930), start, CreateDt(2014, 9, 31), dc), "end");
    }

    [TestCase("01-Jan-2151", null)]
    [TestCase("32-Jan-2014", null)]
    [TestCase("01-Jan-2151", "%d-%b-%Y")]
    [TestCase("32-Jan-2014", "%d-%b-%Y")]
    [TestCase("01-Jan-2151", "%d-%b-%Y")]
    [TestCase("21510101", "%Y%m%d")]
    [TestCase("20141301", "%Y%m%d")]
    [TestCase("20140132", "%Y%m%d")]
    [TestCase("01/32/04", "%D")]
    [TestCase("13/01/04", "%D")]
    [TestCase("01/01/2151", "%F")]
    [TestCase("01/32/2014", "%F")]
    [TestCase("13/01/2014", "%F")]
    [TestCase("01/01/2014 25:01:01", "%D %T")]
    public void TestDtFromStr(string date, string fmt)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = (fmt == null) ? Dt.FromStr(date) : Dt.FromStr(date, fmt);
        });
    }

    [TestCase("01-Jan-2151")]
    [TestCase("32-Jan-2014")]
    [TestCase("01-Jan-2151")]
    [TestCase("32-Jan-2014")]
    [TestCase("01-Jan-2151")]
    [TestCase("21510101")]
    [TestCase("20141301")]
    [TestCase("20140132")]
    [TestCase("01/32/04")]
    [TestCase("01/01/2151")]
    [TestCase("01/32/2014")]
    [TestCase("01/01/2014 25:01:01")]
    public void TestDtParse(string date)
    {
      Assert.Throws<FormatException>(
        () =>
        {
          var res = Dt.Parse(date);
        });
    }

    [TestCase(20140132)]
    [TestCase(20141301)]
    [TestCase(21520101)]
    [TestCase(0)]
    public void TestDtRoll(int date)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = Dt.Roll(CreateDt(date), BDConvention.Modified, Calendar.NYB);
        });
    }

    [TestCase(1, 13, 2014, 1, 1, 2014)]
    [TestCase(1, 1, 2152, 1, 1, 2014)]
    [TestCase(1, 1, 2014, 1, 13, 2014)]
    [TestCase(1, 1, 2014, 1, 1, 2152)]
    public void DtDiff01(int day1, int month1, int year1, int day2, int month2, int year2)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var diff = Dt.Diff(CreateDt(year1, month1, day1), CreateDt(year2, month2, day2));
        });
    }

    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.Thirty360Isma)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.Thirty360)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.ThirtyE360)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.Actual360)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.Actual365Fixed)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.Actual365L)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.ActualActualBond)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.ActualActual)]
    [TestCase(1, 1, 2152, 1, 1, 2014, DayCount.ActualActualEuro)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.Thirty360Isma)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.Thirty360)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.ThirtyE360)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.Actual360)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.Actual365Fixed)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.Actual365L)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.ActualActualBond)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.ActualActual)]
    [TestCase(1, 1, 2014, 1, 1, 2152, DayCount.ActualActualEuro)]
    public void DtDiff02(int day1, int month1, int year1, int day2, int month2, int year2, DayCount dc)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var diff = Dt.Diff(CreateDt(year1, month1, day1), CreateDt(year2, month2, day2), dc);
        });
    }

    [TestCase(0)]
    [TestCase(20140230)]
    [TestCase(21540830)]
    [TestCase(21041301)]
    public void TestAddMonths(int date)
    {
      Assert.Throws<ArgumentOutOfRangeException>(
        () =>
        {
          var dt = (date == 0) ? Dt.Empty : CreateDt(date/10000, (date%10000)/100, date%100);
          Dt.AddMonths(dt, 1, CycleRule.None);
        });
    }

    [Test]
    public void IsLeapYear()
    {
      Assert.IsTrue(Dt.IsLeapYear(2000), "2000 is leap year");
      Assert.IsFalse(Dt.IsLeapYear(2100), "2100 is leap year");
      Assert.IsFalse(Dt.IsLeapYear(1900), "1900 is leap year");
      Assert.IsFalse(Dt.IsLeapYear(2010), "2010 is leap year");
      Assert.IsTrue(Dt.IsLeapYear(2012), "2012 is leap year");
      Assert.IsFalse(Dt.IsLeapYear(2013), "2013 is leap year");
      Assert.IsFalse(Dt.IsLeapYear(2014), "2014 is leap year");
    }

    [TestCase(1, 1, 1900, 0, 0, 0)]
    public void TestDtMinValue(int day, int month, int year, int hour, int minute, int second)
    {
      var dt = new Dt(day, month, year, hour, minute, second);
      Assert.AreEqual(dt, Dt.MinValue);
      Assert.AreNotEqual(Dt.Empty, Dt.MinValue);
    }

    [TestCase(31, 12, 2149, 23, 50, 0)]
    public void TestDtMaxValue(int day, int month, int year, int hour, int minute, int second)
    {
      var dt = new Dt(day, month, year, hour, minute, second);
      Assert.AreEqual(dt, Dt.MaxValue);
    }

    private static Dt CreateDt(int date)
    {
      return date == 0
        ? Dt.Empty
        : CreateDt(date / 10000, date % 10000 / 100, date % 100);
    }

    private static Dt CreateDt(int year, int month, int day)
    {
      var a = new Dt[1];
      var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(a, 0);
      Marshal.StructureToPtr(new DtData
      {
        Year = (byte)(year-1900),
        Month = (byte)month,
        Day = (byte)day
      }, ptr, false);
      return a[0];
    }

    private struct DtData
    {
      public byte Year, Month, Day, Minute;
    }
  }
}
