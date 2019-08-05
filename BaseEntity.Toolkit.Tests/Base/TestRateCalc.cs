//
// TestRateCalc.cs
// Test rate calculations
//

using BaseEntity.Toolkit.Base;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class RateCalcTest
  {
    private const double epsilon = 1e-6;

    /// <summary>
    ///   Test conversion of rate to price
    /// </summary>
    [Test, Smoke]
    public void TestPriceFromRate()
    {
      Dt start = new Dt(1, Month.January, 2007);
      Dt end = new Dt(1, Month.September, 2007);
      Assert.AreEqual(0.960842087, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual365Fixed, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.961406707, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual365Fixed, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.961950077, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual365Fixed, Frequency.Annual), epsilon);
      Assert.AreEqual(0.961589125, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual365Fixed, Frequency.None), epsilon);
      Assert.AreEqual(0.960309165, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual360, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.960881311, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual360, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.961431929, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual360, Frequency.Annual), epsilon);
      Assert.AreEqual(0.961076406, RateCalc.PriceFromRate(0.06, start, end, DayCount.Actual360, Frequency.None), epsilon);

      Assert.AreEqual(0.960842087, RateCalc.PriceFromRate(0.06, 0.665753425, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.961406707, RateCalc.PriceFromRate(0.06, 0.665753425, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.961950077, RateCalc.PriceFromRate(0.06, 0.665753425, Frequency.Annual), epsilon);
      Assert.AreEqual(0.961589125, RateCalc.PriceFromRate(0.06, 0.665753425, Frequency.None), epsilon);
      Assert.AreEqual(0.960309165, RateCalc.PriceFromRate(0.06, 0.675, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.960881311, RateCalc.PriceFromRate(0.06, 0.675, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.961431929, RateCalc.PriceFromRate(0.06, 0.675, Frequency.Annual), epsilon);
      Assert.AreEqual(0.961076406, RateCalc.PriceFromRate(0.06, 0.675, Frequency.None), epsilon);

      return;
    }

    /// <summary>
    ///   Test conversion of price to rate
    /// </summary>
    [Test, Smoke]
    public void TestRateFromPrice()
    {
      Dt start = new Dt(1, Month.January, 2007);
      Dt end = new Dt(1, Month.September, 2007);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.960842087, start, end, DayCount.Actual365Fixed, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961406707, start, end, DayCount.Actual365Fixed, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961950077, start, end, DayCount.Actual365Fixed, Frequency.Annual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961589125, start, end, DayCount.Actual365Fixed, Frequency.None), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.960309165, start, end, DayCount.Actual360, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.960881311, start, end, DayCount.Actual360, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961431929, start, end, DayCount.Actual360, Frequency.Annual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961076406, start, end, DayCount.Actual360, Frequency.None), epsilon);

      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.960842087, 0.665753425, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961406707, 0.665753425, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961950077, 0.665753425, Frequency.Annual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961589125, 0.665753425, Frequency.None), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.960309165, 0.675, Frequency.Continuous), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.960881311, 0.675, Frequency.SemiAnnual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961431929, 0.675, Frequency.Annual), epsilon);
      Assert.AreEqual(0.06, RateCalc.RateFromPrice(0.961076406, 0.675, Frequency.None), epsilon);

      return;
    }

  } // class TestRateCalc
} 
