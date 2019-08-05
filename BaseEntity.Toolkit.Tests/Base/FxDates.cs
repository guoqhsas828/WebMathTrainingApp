//
// FxDates.cs
// Date tests related to fx rates
// Copyright (c) 2011,   . All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class FxDatesTest : ToolkitTestBase
  {
    private readonly Dt arbitraryDate_ = Dt.FromStr("1-JAN-2010");
    private FxRate NYB_TGT;
    private FxRate NYB_TKB;

    [OneTimeSetUp]
    public void Init()
    {
      NYB_TGT = new FxRate(arbitraryDate_, 2, Currency.USD, Currency.EUR, 0.0, Calendar.NYB, Calendar.TGT);
      NYB_TKB = new FxRate(arbitraryDate_, 2, Currency.USD, Currency.JPY, 0.0, Calendar.NYB, Calendar.TKB);
    }

    [Test, Smoke]
    public void RegularWeekday()
    {
      var monday = Dt.FromStr("12-SEP-2011");
      var wednesday = Dt.Add(monday, 2);
      var spotDate = NYB_TGT.FxSpotDate(monday);
      Assert.AreEqual(spotDate, wednesday);
      return;
    }

    [Test, Smoke]
    public void RegularThursday()
    {
      var thursday = Dt.FromStr("15-SEP-2011");
      var nextMonday = Dt.FromStr("19-SEP-2011");
      var spotDate = NYB_TGT.FxSpotDate(thursday);
      Assert.AreEqual(spotDate, nextMonday);
      return;
    }

    [Test, Smoke]
    public void RegularFriday()
    {
      var friday = Dt.FromStr("16-SEP-2011");
      var nextTuesday = Dt.FromStr("20-SEP-2011");
      var spotDate = NYB_TGT.FxSpotDate(friday);
      Assert.AreEqual(spotDate, nextTuesday);
      return;
    }

    [Test, Smoke]
    public void IndependenceDay1()
    {
      // regular T+2 falls on US holiday, rolls to T+3
      var thursday = Dt.FromStr("30-JUN-2011");
      var nextTuesday = Dt.FromStr("5-JUL-2011");
      var spotDate = NYB_TGT.FxSpotDate(thursday);
      Assert.AreEqual(spotDate, nextTuesday);
      return;
    }

    [Test, Smoke]
    public void IndependenceDay2()
    {
      // T+2 skips US holiday, T+1 being a US holiday doesn't affect result
      var friday = Dt.FromStr("1-JUL-2011");
      var nextTuesday = Dt.FromStr("5-JUL-2011");
      var spotDate = NYB_TGT.FxSpotDate(friday);
      Assert.AreEqual(spotDate, nextTuesday);
      return;
    }

    [Test, Smoke]
    public void SuccessiveHolsInEachCalendar1()
    {
      // mon 24th is hol in Tokyo, tue 25th is hol in US, therefore expect wed 26th
      var dec20th = Dt.FromStr("20-DEC-2012"); // thursday
      var spotDate = NYB_TKB.FxSpotDate(dec20th);
      var dec26th = Dt.FromStr("26-DEC-2012");
      Assert.AreEqual(spotDate, dec26th);
    }

    [Test, Smoke]
    public void SuccessiveHolsInEachCalendar2()
    {
      // mon 24th is hol in Tokyo, tue 25th is hol in US
      var dec21st = Dt.FromStr("21-DEC-2012"); // friday
      var spotDate = NYB_TKB.FxSpotDate(dec21st);
      var dec26th = Dt.FromStr("26-DEC-2012");
      Assert.AreEqual(spotDate, dec26th);
    }

    [Test, Smoke]
    public void SuccessiveHolsInEachCalendar3()
    {
      // mon 24th is hol in Tokyo, tue 25th is hol in US
      // 26th is good bus day in both
      // 1 clear bus day on NYB
      // 2 clear bus day on TKB
      var dec24th = Dt.FromStr("24-DEC-2012"); // monday
      var spotDate = NYB_TKB.FxSpotDate(dec24th);
      var dec26th = Dt.FromStr("26-DEC-2012"); // wednesday
      Assert.AreEqual(spotDate, dec26th);
    }

    [Test, Smoke]
    public void SuccessiveHolsInEachCalendarRoll()
    {
      // 2012: mon 24th is hol in Tokyo, tue 25th is hol in US
      var dec20th2011 = Dt.FromStr("20-DEC-2011"); // monday
      var dec22nd2011 = NYB_TKB.FxSpotDate(dec20th2011);
      var spotPlus1Y = Dt.Add(dec22nd2011, Tenor.Parse("1Y"));
      var rolled = NYB_TKB.Roll(spotPlus1Y);
      var dec26th2012 = Dt.FromStr("26-DEC-2012");
      Assert.AreEqual(rolled,dec26th2012);
    }

  }
}