//
// Copyright (c)    2018. All rights reserved.
//
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  using NUnit.Framework;

  [TestFixture]
  public class BondNotificationDaysTests
  {
    [Test]
    public void ActivePeriods()
    {
      var notificationDays = new BusinessDays(15, Calendar.NYB);
      Dt asOf = new Dt(20170221),
        activeCallStart = (asOf + 1) + notificationDays;
      var calls = CallPeriods();
      var expectCount = calls.Count(c => c.StartDate >= activeCallStart);
      var actives = calls.GetActivePeriods(asOf, notificationDays).ToArray();
      Assert.AreEqual(expectCount, actives.Length, "Active count");
      for (int i = 0; i < expectCount; ++i)
      {
        Assert.That(actives[i].StartDate,
          Is.GreaterThanOrEqualTo(activeCallStart), "At " + i);
      }
    }

    private static CallPeriod[] CallPeriods()
    {
      return new[]
      {
        Call(new Dt(20160301), 1.03),
        Call(new Dt(20170301), 1.025),
        Call(new Dt(20180301), 1.02),
        Call(new Dt(20190301), 1.01),
        Call(new Dt(20200301), 1.0),
        Call(new Dt(20210301), 1.0),
        Call(new Dt(20220301), 1.0),
        Call(new Dt(20230301), 1.0),
        Call(new Dt(20240301), 1.0),
        Call(new Dt(20250301), 1.0),
      };
    }

    private static CallPeriod Call(Dt date, double strikePrice)
    {
      return new CallPeriod(date, date, strikePrice,
        0, OptionStyle.European, 0);
    }
  }
}
