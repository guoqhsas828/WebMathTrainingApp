//
// TestRelativeTime.cs
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class RelativeTimeTest : ToolkitTestBase
  {

    #region Date and time roundtrip

    /// <summary>
    ///   Round tripping the one hour difference
    /// </summary>
    [TestCaseSource(nameof(TimeData))]
    public void OneHourDiff(double timeValue)
    {
      const double onehour = 1.0 / 24 / RelativeTime.DaysPerYear;
      var today = Dt.Today();
      var timeExpect = (RelativeTime)timeValue;
      var date0 = Dt.Add(today, timeExpect);

      // Add one hour.
      var date1 = date0 + (RelativeTime)onehour;
      var diff = Dt.RelativeTime(date0, date1);
      Assert.AreEqual(onehour, diff, 1E-15);

      // Subtract one hour.
      date1 = date0 - (RelativeTime)onehour;
      diff = Dt.RelativeTime(date0, date1);
      Assert.AreEqual(-onehour, diff, 1E-15);
    }

    /// <summary>
    ///  Test roundtrip between the time to expiry and the expiry date.
    /// </summary>
    [TestCaseSource(nameof(TimeData))]
    public void TimeToExpiry(double timeValue)
    {
      var today = Dt.Today();

      // Non-negative time.
      var timeExpect = (RelativeTime)timeValue;
      var expiry = Dt.Add(today, timeExpect);
      var time = Dt.RelativeTime(today, expiry);
      Assert.AreEqual(timeExpect, time, TimeTolerance);
      var date = today + time;
      Assert.AreEqual(expiry, date);

      // Negative time also works
      timeExpect = (RelativeTime)(-timeValue);
      expiry = Dt.Add(today, timeExpect);
      time = new RelativeTime(today, expiry);
      Assert.AreEqual(timeExpect, time, TimeTolerance);
      date = today + time;
      Assert.AreEqual(expiry, date);
    }

    // Time tolerance is approximately half hour.
    private const double TimeTolerance = 0.5 / 24 / 365;

    public static double[] TimeData { get; set; } = new[]
    {
      0.0, 1.0 / 365, 7.0 / 365, 15.0 / 365, 1.0 / 12, 0.25, 0.5, 0.75, 1.0, 1.5, 5.0, 10
    };

    #endregion

    #region Rates and discount factors

    /// <summary>
    ///  Test roundtrip between the rate and the discount factor.
    /// </summary>
    /// <param name="rate">The rate.</param>
    [TestCaseSource(nameof(RateData))]
    public void Rate(double rate)
    {
      var today = Dt.Today();
      foreach (var te in TimeData)
      {
        var expiry = today + (RelativeTime)te;

        // Is the discount factor calculation correct?
        var df = RateCalc.PriceFromRate(rate, today, expiry);
        var time = Dt.RelativeTime(today, expiry);
        Assert.AreEqual(Math.Exp(-rate * time), df, 1E-15);

        // Is the rate calculation correct?
        var resultRate = RateCalc.RateFromPrice(df, today, expiry);
        if (time.Equals(0.0)) Assert.AreEqual(0.0, resultRate);
        else Assert.AreEqual(rate, resultRate, 5E-14);

        // Roundtripping the rate calculation, curve based.
        var discountCurve = new DiscountCurve(today).SetRelativeTimeRate(rate);
        resultRate = RateCalc.Rate(discountCurve, today, expiry);
        if (time.Equals(0.0)) Assert.AreEqual(0.0, resultRate);
        else Assert.AreEqual(rate, resultRate, 5E-14);

        // Roundtripping the rate with an arbitrary date after as-of.
        Dt date = today + (RelativeTime)2.0;
        resultRate = RateCalc.Rate(discountCurve, today, date);
        Assert.AreEqual(rate, resultRate, 5E-14);
      }
    }

    public static double[] RateData { get; set; } = new[]
    {
      0,
      // positive rates
      0.001, 0.005, 0.01, 0.025, 0.03, 0.1, 0.5,
      // Negative rates should also work
      -0.001, -0.005, -0.01, -0.03, -0.05, -0.1, -0.5,
    };

    #endregion
  }
}
