//
// TestDayCount.cs
// Daycount test based on ISDA Doc 'EMU and Market Conventions: Recent Developments',1998.
// Copyright (c) 2002-2014,   . All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class DayCountTest : ToolkitTestBase
  {
    private const double epsilon = 1e-6;

    #region Utilities

    /// <summary>
    ///   Test simple daycount fraction
    /// </summary>
    private static void TestFraction(DayCount dayCount, Dt start, Dt end, double expected)
    {
      TestFraction(dayCount, start, end, start, end, expected);
    }

    /// <summary>
    ///   Test daycount fraction
    /// </summary>
    private static void TestFraction(DayCount dayCount, Dt start, Dt end, Dt refStart, Dt refEnd, double expected)
    {
      double result = Dt.Fraction(refStart, refEnd, start, end, dayCount, Frequency.None);
      string description = String.Format("Dt::fraction(pstart={0}, pend={1}, start={2}, end={3}, dayCount={4})", refStart, refEnd, start, end, dayCount);
      Assert.AreEqual(expected, result, epsilon, description);
    }

    /// <summary>
    ///   Test number of days based on four different daycounts
    /// </summary>
    private static void TestDays(Dt start, Dt end, int act360, int t360, int tE360)
    {
      string description = String.Format("Dt::Diff(start={0}, end={1}, dayCount={2})", start, end, DayCount.Actual360);
      Assert.AreEqual(act360, Dt.Diff(start, end, DayCount.Actual360), epsilon, description);
      description = String.Format("Dt::Diff(start={0}, end={1}, dayCount={2})", start, end, DayCount.Thirty360);
      Assert.AreEqual(t360, Dt.Diff(start, end, DayCount.Thirty360), epsilon, description);
      description = String.Format("Dt::Diff(start={0}, end={1}, dayCount={2})", start, end, DayCount.ThirtyE360);
      Assert.AreEqual(tE360, Dt.Diff(start, end, DayCount.ThirtyE360), epsilon, description);
    }

    #endregion Utilities

    [Test, Smoke]
    public void Test001()
    {
      TestFraction(DayCount.ActualActual, new Dt(1, Month.November, 2003), new Dt(1, Month.May, 2004), 0.497724381);
    }

    // ISDA Doc example

    [Test, Smoke]
    public void Test002()
    {
      TestFraction(DayCount.ActualActualBond, new Dt(1, Month.November, 2003), new Dt(1, Month.May, 2004), 0.5);
    }

    [Test, Smoke]
    public void Test003()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(1, Month.November, 2003), new Dt(1, Month.May, 2004), 0.497268);
    }

    // Short first calculation period (first period)

    [Test, Smoke]
    public void Test004()
    {
      TestFraction(DayCount.ActualActual, new Dt(1, Month.February, 1999), new Dt(1, Month.July, 1999), 0.410958904110);
    }

    [Test, Smoke]
    public void Test005()
    {
      TestFraction(DayCount.ActualActualBond, new Dt(1, Month.February, 1999), new Dt(1, Month.July, 1999), new Dt(1, Month.July, 1998), new Dt(1, Month.July, 1999), 0.410958904110);
    }

    [Test, Smoke]
    public void Test006()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(1, Month.February, 1999), new Dt(1, Month.July, 1999), 0.410958904110);
    }

    // Short first calculation period (second period)

    [Test, Smoke]
    public void Test007()
    {
      TestFraction(DayCount.ActualActual, new Dt(1, Month.July, 1999), new Dt(1, Month.July, 2000), 1.001377348600);
    }

    [Test, Smoke]
    public void Test008()
    {
      TestFraction(DayCount.ActualActualBond, new Dt(1, Month.July, 1999), new Dt(1, Month.July, 2000), new Dt(1, Month.July, 1999), new Dt(1, Month.July, 2000), 1.0);
    }

    [Test, Smoke]
    public void Test009()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(1, Month.July, 1999), new Dt(1, Month.July, 2000), 1.0);
    }

    // Long first calculation period (first period)

    [Test, Smoke]
    public void Test010()
    {
      TestFraction(DayCount.ActualActual, new Dt(15, Month.August, 2002), new Dt(15, Month.July, 2003), 0.915068493151);
    }

    [Test, Smoke]
    [Ignore("See also the FractionISMA test that is excluded")]
    public void Test011()
    {
      TestFraction(DayCount.ActualActualBond,
                    new Dt(15, Month.August, 2002),
                    new Dt(15, Month.July, 2003),
                    new Dt(15, Month.January, 2003), // MEF: I think this date is wrong and should be checked against mktc1198.pdf
                    new Dt(15, Month.July, 2003),
                    0.915760869565);
    }

    [Test, Smoke]
    public void Test012()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(15, Month.August, 2002), new Dt(15, Month.July, 2003), 0.915068493151);
    }

    // long first calculation period (second period)
    /* Warning: the ISDA case is in disagreement with mktc1198.pdf */
    /* MEF: mktc1198.pdf has an error in this calculation; below is the correct target value */

    [Test, Smoke]
    public void Test013()
    {
      TestFraction(DayCount.ActualActual, new Dt(15, Month.July, 2003), new Dt(15, Month.January, 2004), 0.504004790778);
    }

    [Test, Smoke]
    public void Test014()
    {
      TestFraction(DayCount.ActualActualBond, new Dt(15, Month.July, 2003), new Dt(15, Month.January, 2004), new Dt(15, Month.July, 2003), new Dt(15, Month.January, 2004), 0.5);
    }

    [Test, Smoke]
    public void Test015()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(15, Month.July, 2003), new Dt(15, Month.January, 2004), 0.504109589041);
    }

    // Short final calculation period (penultimate period)

    [Test, Smoke]
    public void Test016()
    {
      TestFraction(DayCount.ActualActual, new Dt(30, Month.July, 1999), new Dt(30, Month.January, 2000), 0.503892506924);
    }

    [Test, Smoke]
    public void Test017()
    {
      TestFraction(DayCount.ActualActualBond, new Dt(30, Month.July, 1999), new Dt(30, Month.January, 2000), new Dt(30, Month.July, 1999), new Dt(30, Month.January, 2000), 0.5);
    }

    [Test, Smoke]
    public void Test018()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(30, Month.July, 1999), new Dt(30, Month.January, 2000), 0.504109589041);
    }

    // Short final calculation period (final period)

    [Test, Smoke]
    public void Test019()
    {
      TestFraction(DayCount.ActualActual, new Dt(30, Month.January, 2000), new Dt(30, Month.June, 2000), 0.415300546448);
    }

    [Test, Smoke]
    public void Test020()
    {
      TestFraction(DayCount.ActualActualBond, new Dt(30, Month.January, 2000), new Dt(30, Month.June, 2000), new Dt(30, Month.January, 2000), new Dt(30, Month.July, 2000), 0.417582417582);
    }

    [Test, Smoke]
    public void Test021()
    {
      TestFraction(DayCount.ActualActualEuro, new Dt(30, Month.January, 2000), new Dt(30, Month.June, 2000), 0.41530054644);
    }

    /// <summary>
    ///   Tests of daycounts from Stigam & Robinson, Money market & Bond Calculations, p73,74
    /// </summary>
    [Test, Smoke]
    public void StigamTests()
    {
      TestDays(new Dt(1, Month.January, 1986), new Dt(1, Month.February, 1986), 31, 30, 30);
      TestDays(new Dt(1, Month.January, 1986), new Dt(1, Month.January, 1987), 365, 360, 360);
      TestDays(new Dt(15, Month.January, 1986), new Dt(1, Month.February, 1986), 17, 16, 16);
      TestDays(new Dt(1, Month.February, 1986), new Dt(1, Month.March, 1986), 28, 30, 30);
      TestDays(new Dt(15, Month.February, 1986), new Dt(1, Month.April, 1986), 45, 46, 46);
      TestDays(new Dt(15, Month.March, 1986), new Dt(15, Month.June, 1986), 92, 90, 90);
      TestDays(new Dt(15, Month.March, 1986), new Dt(15, Month.June, 1986), 92, 90, 90);

      TestDays(new Dt(30, Month.August, 1991), new Dt(31, Month.August, 1991), 1, 0, 0);
      TestDays(new Dt(30, Month.August, 1991), new Dt(1, Month.September, 1991), 2, 1, 1);
      TestDays(new Dt(30, Month.August, 1991), new Dt(27, Month.February, 1992), 181, 177, 177);
      TestDays(new Dt(30, Month.August, 1991), new Dt(28, Month.February, 1992), 182, 178, 178);
      TestDays(new Dt(30, Month.August, 1991), new Dt(29, Month.February, 1992), 183, 179, 179);
      TestDays(new Dt(30, Month.August, 1991), new Dt(1, Month.March, 1992), 184, 181, 181);
      TestDays(new Dt(30, Month.August, 1991), new Dt(29, Month.August, 1992), 365, 359, 359);
      TestDays(new Dt(30, Month.August, 1991), new Dt(30, Month.August, 1992), 366, 360, 360);
      TestDays(new Dt(30, Month.August, 1991), new Dt(31, Month.August, 1992), 367, 360, 360);
      TestDays(new Dt(30, Month.August, 1991), new Dt(1, Month.September, 1992), 368, 361, 361);
      TestDays(new Dt(31, Month.August, 1991), new Dt(1, Month.September, 1991), 1, 1, 1);
      TestDays(new Dt(31, Month.August, 1991), new Dt(2, Month.September, 1991), 2, 2, 2);
      TestDays(new Dt(31, Month.August, 1991), new Dt(27, Month.February, 1992), 180, 177, 177);
      TestDays(new Dt(31, Month.August, 1991), new Dt(28, Month.February, 1992), 181, 178, 178);
      TestDays(new Dt(31, Month.August, 1991), new Dt(29, Month.February, 1992), 182, 179, 179);
      TestDays(new Dt(31, Month.August, 1991), new Dt(1, Month.March, 1992), 183, 181, 181);
      TestDays(new Dt(31, Month.August, 1991), new Dt(29, Month.August, 1992), 364, 359, 359);
      TestDays(new Dt(31, Month.August, 1991), new Dt(30, Month.August, 1992), 365, 360, 360);
      TestDays(new Dt(31, Month.August, 1991), new Dt(31, Month.August, 1992), 366, 360, 360);
      TestDays(new Dt(31, Month.August, 1991), new Dt(1, Month.September, 1992), 367, 361, 361);

      TestDays(new Dt(15, Month.January, 1992), new Dt(28, Month.February, 1992), 44, 43, 43);
      TestDays(new Dt(15, Month.January, 1992), new Dt(29, Month.February, 1992), 45, 44, 44);
      TestDays(new Dt(15, Month.January, 1992), new Dt(1, Month.March, 1992), 46, 46, 46);

      TestDays(new Dt(15, Month.January, 1993), new Dt(27, Month.February, 1993), 43, 42, 42);
      TestDays(new Dt(15, Month.January, 1993), new Dt(28, Month.February, 1993), 44, 43, 43);
      TestDays(new Dt(15, Month.January, 1993), new Dt(1, Month.March, 1993), 45, 46, 46);

      TestDays(new Dt(28, Month.February, 1992), new Dt(27, Month.February, 1992), -1, -1, -1);
      TestDays(new Dt(28, Month.February, 1992), new Dt(28, Month.February, 1992), 0, 0, 0);
      TestDays(new Dt(28, Month.February, 1992), new Dt(29, Month.February, 1992), 1, 1, 1);
      TestDays(new Dt(28, Month.February, 1992), new Dt(1, Month.March, 1992), 2, 3, 3);
      TestDays(new Dt(28, Month.February, 1992), new Dt(2, Month.March, 1992), 3, 4, 4);
      TestDays(new Dt(28, Month.February, 1992), new Dt(27, Month.August, 1992), 181, 179, 179);
      TestDays(new Dt(28, Month.February, 1992), new Dt(28, Month.August, 1992), 182, 180, 180);
      TestDays(new Dt(28, Month.February, 1992), new Dt(29, Month.August, 1992), 183, 181, 181);
      TestDays(new Dt(28, Month.February, 1992), new Dt(30, Month.August, 1992), 184, 182, 182);
      TestDays(new Dt(28, Month.February, 1992), new Dt(31, Month.August, 1992), 185, 183, 182);
      TestDays(new Dt(28, Month.February, 1992), new Dt(1, Month.September, 1992), 186, 183, 183);
      TestDays(new Dt(28, Month.February, 1992), new Dt(2, Month.September, 1992), 187, 184, 184);
      TestDays(new Dt(28, Month.February, 1992), new Dt(27, Month.February, 1993), 365, 359, 359);
      TestDays(new Dt(28, Month.February, 1992), new Dt(28, Month.February, 1993), 366, 360, 360);
      TestDays(new Dt(28, Month.February, 1992), new Dt(1, Month.March, 1993), 367, 363, 363);
      TestDays(new Dt(28, Month.February, 1992), new Dt(2, Month.March, 1993), 368, 364, 364);
      TestDays(new Dt(28, Month.February, 1992), new Dt(3, Month.March, 1993), 369, 365, 365);

      TestDays(new Dt(29, Month.February, 1992), new Dt(29, Month.February, 1992), 0, 0, 0);
      TestDays(new Dt(29, Month.February, 1992), new Dt(1, Month.March, 1992), 1, 2, 2);
      TestDays(new Dt(29, Month.February, 1992), new Dt(2, Month.March, 1992), 2, 3, 3);
      TestDays(new Dt(29, Month.February, 1992), new Dt(27, Month.August, 1992), 180, 178, 178);
      TestDays(new Dt(29, Month.February, 1992), new Dt(28, Month.August, 1992), 181, 179, 179);
      TestDays(new Dt(29, Month.February, 1992), new Dt(29, Month.August, 1992), 182, 180, 180);
      TestDays(new Dt(29, Month.February, 1992), new Dt(30, Month.August, 1992), 183, 181, 181);
      TestDays(new Dt(29, Month.February, 1992), new Dt(31, Month.August, 1992), 184, 182, 181);
      TestDays(new Dt(29, Month.February, 1992), new Dt(1, Month.September, 1992), 185, 182, 182);
      TestDays(new Dt(29, Month.February, 1992), new Dt(2, Month.September, 1992), 186, 183, 183);
      TestDays(new Dt(29, Month.February, 1992), new Dt(27, Month.February, 1993), 364, 358, 358);
      TestDays(new Dt(29, Month.February, 1992), new Dt(28, Month.February, 1993), 365, 359, 359);
      TestDays(new Dt(29, Month.February, 1992), new Dt(1, Month.March, 1993), 366, 362, 362);
      TestDays(new Dt(29, Month.February, 1992), new Dt(2, Month.March, 1993), 367, 363, 363);
      TestDays(new Dt(29, Month.February, 1992), new Dt(3, Month.March, 1993), 368, 364, 364);

      TestDays(new Dt(28, Month.February, 1993), new Dt(27, Month.February, 1993), -1, -1, -1);
      TestDays(new Dt(28, Month.February, 1993), new Dt(28, Month.February, 1993), 0, 0, 0);
      TestDays(new Dt(28, Month.February, 1993), new Dt(1, Month.March, 1993), 1, 3, 3);
      TestDays(new Dt(28, Month.February, 1993), new Dt(2, Month.March, 1993), 2, 4, 4);
      TestDays(new Dt(28, Month.February, 1993), new Dt(27, Month.August, 1993), 180, 179, 179);
      TestDays(new Dt(28, Month.February, 1993), new Dt(28, Month.August, 1993), 181, 180, 180);
      TestDays(new Dt(28, Month.February, 1993), new Dt(29, Month.August, 1993), 182, 181, 181);
      TestDays(new Dt(28, Month.February, 1993), new Dt(30, Month.August, 1993), 183, 182, 182);
      TestDays(new Dt(28, Month.February, 1993), new Dt(31, Month.August, 1993), 184, 183, 182);
      TestDays(new Dt(28, Month.February, 1993), new Dt(1, Month.September, 1993), 185, 183, 183);
      TestDays(new Dt(28, Month.February, 1993), new Dt(2, Month.September, 1993), 186, 184, 184);
      TestDays(new Dt(28, Month.February, 1993), new Dt(27, Month.February, 1994), 364, 359, 359);
      TestDays(new Dt(28, Month.February, 1993), new Dt(28, Month.February, 1994), 365, 360, 360);

      return;
    }

  } // class TestDayCount
} 
