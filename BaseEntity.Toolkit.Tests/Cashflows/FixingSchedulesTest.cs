//
// Copyright (c)    2002-2015. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture]
  public class FixingSchedulesTest : ToolkitTestBase
  {
    #region Data 

    private Dt asOf_;
    private CalibratedCurve referenceCurve_;
    private DiscountCurve discount_;
    private ReferenceIndex liborRi_;
    private ReferenceIndex cmsRi_;
    private ReferenceIndex cpiRi_;
    private ReferenceIndex ffRi_;
    private Schedule sched_;
    #endregion 
    
    public FixingSchedulesTest()
    {
      asOf_ = new Dt(28, 1, 2010);
      referenceCurve_ = new DiscountCurve(asOf_, 0.04);
      discount_ = new DiscountCurve(asOf_, 0.035);
      liborRi_ = SwapLegTestUtils.GetLiborIndex("3M");
      cmsRi_ = SwapLegTestUtils.GetCMSIndex("5Y");
      cpiRi_ = new InflationIndex("CPI", Currency.USD, DayCount.Actual360, Calendar.NYB, BDConvention.Following,
                                  Frequency.Monthly, Tenor.Empty);
      ffRi_ = new InterestRateIndex("FF", new Tenor(1, TimeUnit.Days), Currency.USD, DayCount.Actual360, Calendar.NYB,
                                    BDConvention.Modified, 2);
      sched_ = new Schedule(asOf_, asOf_, Dt.Empty, Dt.Add(asOf_, 15, TimeUnit.Years), Frequency.Quarterly, BDConvention.Modified, Calendar.NYB);
      
    }

    /// <summary>
    /// Reset date with natural lag (days to settle of the index)
    /// </summary>
    [Test]
    public void ResetDateWtNaturalResetLag()
    {
     for (int i = 0; i < sched_.Count; i++)
      {
        Dt resetDt = RateResetUtil.ResetDate(sched_.GetPeriodStart(i), liborRi_, Tenor.Empty);
        Assert.AreEqual(Dt.AddDays(sched_.GetPeriodStart(i), -liborRi_.SettlementDays, liborRi_.Calendar).ToInt(),
                        resetDt.ToInt());
      }
    }

    /// <summary>
    /// Reset date with arbitrary reset lag
    /// </summary>
    [Test]
    public void ResetDateWtResetLag()
    {
     Tenor resetLag = new Tenor(3, TimeUnit.Months);
      for (int i = 0; i < sched_.Count; i++){
        
        Dt resetDt = RateResetUtil.ResetDate(sched_.GetPeriodStart(i), liborRi_, resetLag);
        Assert.AreEqual(Dt.Roll(Dt.Add(sched_.GetPeriodStart(i),  - resetLag.N, resetLag.Units), liborRi_.Roll, liborRi_.Calendar).ToInt(),resetDt.ToInt());
      }
    }


    /// <summary>
    /// Reset day with cycle rule and weekly sampling frequency
    /// </summary>
    [Test]
    public void ResetDateOnMondayWeeklySamplingFrequency()
    {
      InterestRateIndex ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Weekly, CycleRule.Monday, 0);

      Dt fixDt = sched_.GetPeriodStart(5);
      Dt expect = Dt.Add(fixDt, -3);
      Dt dt = Dt.Add(fixDt, -3);
      Dt resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Monday test");
      dt = Dt.Add(fixDt, -2);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Tuesday test");
      dt = Dt.Add(fixDt, -1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Wednesday test");
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Thursday test");
      dt = Dt.Add(fixDt, 1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Friday test");
    }

    /// <summary>
    /// Reset day with cycle rule and weekly sampling frequency
    /// </summary>
    [Test]
    public void ResetDateOnTuesdayWeeklySamplingFrequency()
    {
      InterestRateIndex ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Weekly, CycleRule.Tuesday, 0);

      Dt fixDt = sched_.GetPeriodStart(5);
      Dt expect = Dt.Add(fixDt, -2);
      Dt dt = Dt.Add(fixDt, -3);
      Dt resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Monday test");
      dt = Dt.Add(fixDt, -2);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Tuesday test");
      dt = Dt.Add(fixDt, -1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Wednesday test");
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Thursday test");
      dt = Dt.Add(fixDt, 1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Friday test");
    }

    /// <summary>
    /// Reset day with cycle rule and weekly sampling frequency
    /// </summary>
    [Test]
    public void ResetDateOnWednesdayWeeklySamplingFrequency()
    {
      InterestRateIndex ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Weekly, CycleRule.Wednesday, 0);

      Dt fixDt = sched_.GetPeriodStart(5);
      Dt expect = Dt.Add(fixDt, -1);
      Dt dt = Dt.Add(fixDt, -3);
      Dt resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Monday test");
      dt = Dt.Add(fixDt, -2);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Tuesday test");
      dt = Dt.Add(fixDt, -1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Wednesday test");
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Thursday test");
      dt = Dt.Add(fixDt, 1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Friday test");
    }

    /// <summary>
    /// Reset day with cycle rule and weekly sampling frequency
    /// </summary>
    [Test]
    public void ResetDateOnThursdayWeeklySamplingFrequency()
    {
      InterestRateIndex ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Weekly, CycleRule.Thursday, 0);

      Dt fixDt = sched_.GetPeriodStart(5);
      Dt expect = fixDt;
      Dt dt = Dt.Add(fixDt, -3);
      Dt resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Monday test");
      dt = Dt.Add(fixDt, -2);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Tuesday test");
      dt = Dt.Add(fixDt, -1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Wednesday test");
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Thursday test");
      dt = Dt.Add(fixDt, 1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Friday test");
    }

    /// <summary>
    /// Reset day with cycle rule and weekly sampling frequency
    /// </summary>
    [Test]
    public void ResetDateOnFridayWeeklySamplingFrequency()
    {
      InterestRateIndex ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Weekly, CycleRule.Friday, 0);

      Dt fixDt = sched_.GetPeriodStart(5);
      Dt expect = Dt.Add(fixDt, 1);
      Dt dt = Dt.Add(fixDt, -3);
      Dt resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Monday test");
      dt = Dt.Add(fixDt, -2);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Tuesday test");
      dt = Dt.Add(fixDt, -1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Wednesday test");
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), Dt.Add(expect, -7).ToInt(), "Thursday test");
      dt = Dt.Add(fixDt, 1);
      resetDt = RateResetUtil.ResetDate(dt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Friday test");
    }

    /// <summary>
    /// Reset day with cycle rule and monthly sampling frequency
    /// </summary>
    [Test]
    public void ResetDateMonthlySamplingFrequency()
    {
      InterestRateIndex ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Monthly, CycleRule.First, 0);

      Dt fixDt = sched_.GetPeriodStart(5);
      Dt expect = new Dt(1, fixDt.Month, fixDt.Year);
      Dt resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Reset prior to fixing");

      ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Monthly, CycleRule.Fifteenth, 0);
      expect = new Dt(15, fixDt.Month, fixDt.Year);
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Reset prior to fixing");
      expect = fixDt;
      ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Monthly, CycleRule.TwentyEighth, 0);
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Reset on fixing");
      expect  = new Dt(31, 3, 2011);
      ri = new InterestRateIndex("ReferenceIndex", new Tenor(Frequency.Quarterly), Currency.USD,
                                                   DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                   Frequency.Monthly, CycleRule.EOM, 0);
      resetDt = RateResetUtil.ResetDate(fixDt, ri, Tenor.Empty);
      Assert.AreEqual(resetDt.ToInt(), expect.ToInt(), "Reset after fixing");
      
    }

    

  }
}
