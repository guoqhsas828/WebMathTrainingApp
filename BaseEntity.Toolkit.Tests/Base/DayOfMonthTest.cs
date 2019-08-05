//
// Copyright (c)  2011. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class DayOfMonthTest
  {
    /// <summary>
    /// Test Dt.DayOfMonth
    /// </summary>
    // ASX 30D Cash Futures
    [TestCase(1, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140131)]
    [TestCase(2, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140228)]
    [TestCase(3, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140331)]
    [TestCase(4, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140430)]
    [TestCase(5, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140530)]
    [TestCase(6, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140630)]
    [TestCase(7, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140731)]
    [TestCase(8, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140829)]
    [TestCase(9, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20140930)]
    [TestCase(10, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20141031)]
    [TestCase(11, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20141128)]
    [TestCase(12, 2014, Toolkit.Base.DayOfMonth.Last, BDConvention.Preceding, "SYB", ExpectedResult = 20141231)]
    // ASX 90 Day Bill Futures
    [TestCase(3, 2014, Toolkit.Base.DayOfMonth.SecondThursday, BDConvention.Following, "SYB", ExpectedResult = 20140313)]
    [TestCase(6, 2014, Toolkit.Base.DayOfMonth.SecondThursday, BDConvention.Following, "SYB", ExpectedResult = 20140612)]
    [TestCase(9, 2014, Toolkit.Base.DayOfMonth.SecondThursday, BDConvention.Following, "SYB", ExpectedResult = 20140911)]
    [TestCase(12, 2014, Toolkit.Base.DayOfMonth.SecondThursday, BDConvention.Following, "SYB", ExpectedResult = 20141211)]
    // ASX TBond Futures
    [TestCase(3, 2014, Toolkit.Base.DayOfMonth.ThirdMonday, BDConvention.Following, "SYB", ExpectedResult = 20140317)]
    [TestCase(6, 2014, Toolkit.Base.DayOfMonth.ThirdMonday, BDConvention.Following, "SYB", ExpectedResult = 20140616)]
    [TestCase(9, 2014, Toolkit.Base.DayOfMonth.ThirdMonday, BDConvention.Following, "SYB", ExpectedResult = 20140915)]
    [TestCase(12, 2014, Toolkit.Base.DayOfMonth.ThirdMonday, BDConvention.Following, "SYB", ExpectedResult = 20141215)]
    // ASX 90 NZD Day Bill Futures
    [TestCase(3, 2014, Toolkit.Base.DayOfMonth.SecondWednesday, BDConvention.Following, "SYB", ExpectedResult = 20140312)]
    [TestCase(6, 2014, Toolkit.Base.DayOfMonth.SecondWednesday, BDConvention.Following, "SYB", ExpectedResult = 20140611)]
    [TestCase(9, 2014, Toolkit.Base.DayOfMonth.SecondWednesday, BDConvention.Following, "SYB", ExpectedResult = 20140910)]
    [TestCase(12, 2014, Toolkit.Base.DayOfMonth.SecondWednesday, BDConvention.Following, "SYB", ExpectedResult = 20141210)]
    // CME 3M ED Futures
    [TestCase(8, 2014, Toolkit.Base.DayOfMonth.ThirdWednesday, BDConvention.Following, "LNB", ExpectedResult = 20140820)]
    [TestCase(9, 2014, Toolkit.Base.DayOfMonth.ThirdWednesday, BDConvention.Following, "LNB", ExpectedResult = 20140917)]
    [TestCase(10, 2014, Toolkit.Base.DayOfMonth.ThirdWednesday, BDConvention.Following, "LNB", ExpectedResult = 20141015)]
    [TestCase(11, 2014, Toolkit.Base.DayOfMonth.ThirdWednesday, BDConvention.Following, "LNB", ExpectedResult = 20141119)]
    [TestCase(12, 2014, Toolkit.Base.DayOfMonth.ThirdWednesday, BDConvention.Following, "LNB", ExpectedResult = 20141217)]
    public int DayOfMonth(int month, int year, DayOfMonth dom, BDConvention roll, string cal)
    {
      var calendar = Calendar.Parse(cal);
      return Dt.DayOfMonth(month, year, dom, roll, calendar).ToInt();
    }
  }
}
