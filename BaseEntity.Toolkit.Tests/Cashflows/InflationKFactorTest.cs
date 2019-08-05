//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture]
  public class InflationKFactorTest : ToolkitTestBase
  {
    #region Data 

    private Dt _issue, _asOf;
    private InflationIndex _rebasedCPI;
    private InflationIndex _oldCPI;
    private InflationIndex _testCPI;
    private Schedule _sched;
    private string _resetDataFile = "data/aud_cpi_resets.csv";
    #endregion

    public InflationKFactorTest()
    {
      _issue = new Dt(21, 2, 2012);
      _asOf = new Dt(21, 11, 2014);
      _rebasedCPI = new InflationIndex("CPI_Rebased", Tenor.ThreeMonths, Currency.AUD, DayCount.Actual360, Calendar.SYB, BDConvention.Following,
        Frequency.Quarterly, Tenor.Empty) { HistoricalObservations = new RateResets() };
      _oldCPI = new InflationIndex("CPI_Old", Tenor.ThreeMonths, Currency.AUD, DayCount.Actual360, Calendar.SYB, BDConvention.Following,
        Frequency.Quarterly, Tenor.Empty) { HistoricalObservations = new RateResets() };
      _sched = new Schedule(_issue, _issue, Dt.Empty, _asOf, Frequency.Quarterly, BDConvention.Modified, Calendar.SYB);

      using (var reader = new CsvReader(GetTestFilePath(_resetDataFile)))
      {
        string[] line;

        // Read the header
        var header = new Dictionary<string, int>();
        while ((line = reader.GetCsvLine()) != null)
        {
          if (line.Length == 0 || String.IsNullOrEmpty(line[0])) continue;
          for (int i = 0; i < line.Length; ++i)
          {
            var name = line[i];
            if (name == null) continue;
            name = name.Replace(" ", "");
            if (name.Length == 0) continue;
            header.Add(name, i);
          }
          break;
        }

        // Read the data and add resets
        while ((line = reader.GetCsvLine()) != null)
        {
          if (line.Length == 0 || String.IsNullOrEmpty(line[0])) continue;
          var f = new CsvFields(header, line);
          var resetDt = Dt.FromStr(f.GetString("IndexationDt"), "%d-%b-%Y");
          var rebasedCpi = f.GetDouble("RebasedCPI");
          _rebasedCPI.HistoricalObservations.Add(new RateReset(resetDt, rebasedCpi));
          var oldCpi = f.GetDouble("OldCPI");
          _oldCPI.HistoricalObservations.Add(new RateReset(resetDt, oldCpi));
        }
      }
    }

    [Test]
    public void TestRebasedKFactors()
    {
      var expectedKFactors = new[]{100.300000, 100.350150 ,100.651200 ,101.607387 ,102.420246 ,102.717265 ,103.117862 ,103.922181 ,104.930226 ,105.633259 ,106.182552 };
      var kFactorCalculator = new InflationKFactorCalculator(_asOf, _rebasedCPI, Tenor.Parse("2Q"), Tenor.Parse("4Q"), null, _sched);
      for (int i = 0; i < _sched.Count; i++)
      {
        var nextCoupon = _sched.GetPaymentDate(i);
        var fixingSchedule = kFactorCalculator.GetFixingSchedule(i==0 ? _sched.AsOf :_sched.GetPaymentDate(i - 1), _sched.GetPeriodStart(i), _sched.GetPeriodEnd(i), nextCoupon);
        var fixing = kFactorCalculator.Fixing(fixingSchedule);
        Assert.AreEqual(expectedKFactors[i]/100.0, fixing.Forward, 1.0E6); 
      }
    }

    [Test]
    public void TestKFactorAtTime0()
    {
      var kFactorCalculator = new InflationKFactorCalculator(_asOf, _rebasedCPI, Tenor.Parse("2Q"), Tenor.Parse("4Q"), null, _sched);
      var fixingSchedule = kFactorCalculator.GetFixingSchedule(_sched.AsOf, _sched.AsOf, _sched.AsOf, _sched.AsOf);
      var fixing = kFactorCalculator.Fixing(fixingSchedule);
      Assert.AreEqual(1.00, fixing.Forward, 1.0E6);
    }

    [Test]
    public void TestConstant1PercentCPI()
    {
      var testCPI = new InflationIndex("CPI_Test", Tenor.ThreeMonths, Currency.AUD, DayCount.Actual360, Calendar.SYB, BDConvention.Following,
        Frequency.Quarterly, Tenor.Empty) { HistoricalObservations = new RateResets() };
      var dt = new Dt(31 ,3,2011);
      double cpi = 100.0; 
      while (dt < _asOf)
      {
        var increment = cpi * .01;
        cpi += increment; 
        testCPI.HistoricalObservations.Add(new RateReset(dt, cpi));
        dt = Dt.Add(dt, Frequency.Quarterly, 1, CycleRule.EOM);
        cpi += increment;
        testCPI.HistoricalObservations.Add(new RateReset(dt, cpi));
        dt = Dt.Add(dt, Frequency.Quarterly, 1, CycleRule.EOM);
      }

      var kFactorCalculator = new InflationKFactorCalculator(_asOf, testCPI, Tenor.Parse("1Q"), Tenor.Parse("2Q"), null, _sched);
      for (int i = 1; i < _sched.Count; i++)
      {
        var nextCoupon = _sched.GetPaymentDate(i);
        var fixingSchedule = kFactorCalculator.GetFixingSchedule(_sched.GetPaymentDate(i - 1), _sched.GetPeriodStart(i), _sched.GetPeriodEnd(i), nextCoupon);
        var fixing = kFactorCalculator.Fixing(fixingSchedule);
        Assert.AreEqual(1.0 + (i+1)*.01, fixing.Forward, 1.0E4);
      }
    }

    [Test]
    public void Test2DayScheduleDiff()
    {
      //demonstrate that moving schedules 2 days does not change K factors (unless this causes move from 1 quarter to next). 
      var kFactorCalculator = new InflationKFactorCalculator(_asOf, _rebasedCPI, Tenor.Parse("2Q"), Tenor.Parse("4Q"), null, _sched);
      var issuePlus2 = Dt.AddDays(_issue, 5, Calendar.None);
      var asOfPlus2 = Dt.AddDays(_asOf, 2, Calendar.None);
      var sched2 = new Schedule(issuePlus2, issuePlus2, Dt.Empty, asOfPlus2, Frequency.Quarterly, BDConvention.Modified, Calendar.SYB);
      var kFactorCalculator2 = new InflationKFactorCalculator(asOfPlus2, _rebasedCPI, Tenor.Parse("2Q"), Tenor.Parse("4Q"), null, sched2);
      for (int i = 1; i < _sched.Count; i++)
      {
        var nextCoupon = _sched.GetPaymentDate(i);
        var nextCoupon2 = sched2.GetPaymentDate(i);
        var fixingSchedule = kFactorCalculator.GetFixingSchedule(_sched.GetPaymentDate(i - 1), _sched.GetPeriodStart(i), _sched.GetPeriodEnd(i), nextCoupon);
        var fixingSchedule2 = kFactorCalculator2.GetFixingSchedule(sched2.GetPaymentDate(i -1 ), sched2.GetPeriodStart(i), sched2.GetPeriodEnd(i), nextCoupon2);
        var fixing = kFactorCalculator.Fixing(fixingSchedule);
        var fixing2 = kFactorCalculator2.Fixing(fixingSchedule2);
        Assert.AreEqual(fixing.Forward, fixing2.Forward);
      }
    }


  }
}
