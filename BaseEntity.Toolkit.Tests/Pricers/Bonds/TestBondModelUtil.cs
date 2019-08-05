//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  /// <summary>
  /// Test bond utility functions.
  /// </summary>
  [TestFixture]
  public class TestBondModelUtil : ToolkitTestBase
  {
    struct Data
    {
      public BondType BondType;
      public Dt Effective, FirstCoupon, LastCoupon, Maturity;
      public double CouponRate;
      public DayCount DayCount;
      public Frequency Freq;
      public CycleRule CycleRule;
      public BDConvention BdConvention;
      public Calendar Calendar;

      public Bond Bond
      {
        get
        {
          var bond = new Bond(Effective, Maturity, Currency.USD, BondType,
            CouponRate, DayCount, CycleRule, Freq, BdConvention, Calendar);
          bond.FirstCoupon = FirstCoupon;
          bond.LastCoupon = LastCoupon;
          return bond;
        }
      }
    }

    private static IEnumerable<Dt> BusinessDays(Data data)
    {
      Dt start = data.Effective, end = data.Maturity;
      var bdc = data.BdConvention;
      var cal = data.Calendar;
      Dt dt = start;
      while(dt <= end)
      {
        Dt current = Dt.Roll(dt, data.BdConvention, data.Calendar);
        while (current < dt)
        {
          current = Dt.Add(current, 1);
          current = Dt.Roll(current, bdc, cal);
        }
        dt = Dt.Add(current, 1);
        yield return current;
      }
    }

    private void Compare(bool ignoreExDiv,Data data)
    {

      var bond = data.Bond;
      foreach (var settle in BusinessDays(data))
      {
        Schedule schedule = bond.Schedule;
        int idx = schedule.GetNextCouponIndex(settle);
        if (idx < 0)
          idx = schedule.Count - 1;
        if (settle <= schedule.GetPeriodStart(idx) && idx > 0)
          --idx;
        Dt prevCycle = schedule.GetCycleStart(idx),
          nextCycle = schedule.GetCycleEnd(idx);

        var sp = schedule as IScheduleParams;
        int oldDays = BondModelUtil.AccrualDays(settle, bond.BondType,
          sp.AccrualStartDate, sp.FirstCouponDate,
          sp.NextToLastCouponDate, prevCycle, nextCycle,
          sp.Maturity, bond.AccrualDayCount, bond.PeriodAdjustment,
          bond.BDConvention, bond.Calendar, bond.EomRule,
          false, ignoreExDiv);
        int newDays = BondModelUtil.AccrualDays(settle, bond, false, ignoreExDiv);
        if (newDays != oldDays)
          Assert.AreEqual(oldDays, newDays, "AccrualDays@" + settle.ToInt());

        double oldAi = BondModelUtil.AccruedInterest(settle, bond.BondType,
          sp.AccrualStartDate, sp.FirstCouponDate,
          sp.NextToLastCouponDate, prevCycle, nextCycle,
          sp.Maturity, data.CouponRate, bond.AccrualDayCount, bond.Freq,
          bond.EomRule, bond.PeriodAdjustment,
          bond.BDConvention, bond.Calendar, ignoreExDiv);
        double newAi = BondModelUtil.AccruedInterest(
          settle, bond, bond.AccrualDayCount, data.CouponRate, false, ignoreExDiv);
        if (newAi != oldAi)
          Assert.AreEqual(oldAi, newAi, 1E-5, "AccruedInterest@" + settle.ToInt());
      }
      
    }

    [Test]
    public void WithExDiv()
    {
      Data data = new Data
      {
        BondType = BondType.USCorp,
        Effective = new Dt(20100125),
        Maturity = new Dt(20130325),
        FirstCoupon = new Dt(20100625),
        CouponRate = 0.01,
        Freq = Frequency.Quarterly,
        Calendar = Calendar.TGT,
        DayCount = DayCount.Actual360
      };
      Compare(false,data);
    }

    [Test]
    public void ShortFirstCoupon()
    {
      Data data = new Data
      {
        BondType = BondType.USCorp,
        Effective = new Dt(20100520),
        Maturity = new Dt(20130325),
        FirstCoupon = new Dt(20100625),
        CouponRate = 0.01,
        Freq = Frequency.Quarterly,
        Calendar = Calendar.TGT,
        DayCount = DayCount.Actual360
      };
     Compare(false,data);
    }

    [Test]
    public void LongFirstCoupon()
    {
      Data data = new Data
      {
        BondType = BondType.USCorp,
        Effective = new Dt(20100120),
        Maturity = new Dt(20130325),
        FirstCoupon = new Dt(20100625),
        CouponRate = 0.01,
        Freq = Frequency.Quarterly,
        Calendar = Calendar.TGT,
        DayCount = DayCount.Actual360
      };
      Compare(false, data);
    }

    [Test, Smoke]
    public void YieldOnCouponDate()
    {
      const double TOLERANCE = 0.001;
      Dt issue = new Dt(15, 3, 2006);
      Dt maturity = new Dt(15, 3, 2011);

      const double coupon = 0.03141593;
      Calendar cal = Calendar.NYB;
      const BDConvention roll = BDConvention.Following;
      const Currency ccy = Currency.USD;
      foreach (var dc in new[] { DayCount.Thirty360, DayCount.Thirty360Isma, DayCount.ThirtyE360, DayCount.Actual360, DayCount.Actual365Fixed, Toolkit.Base.DayCount.Actual365L, DayCount.Actual366, DayCount.ActualActual, DayCount.ActualActualBond, DayCount.ActualActualEuro })
      {
        foreach (var freq in new[] { Frequency.SemiAnnual, Frequency.Quarterly, Frequency.Annual })
        {
          foreach (BondType type in Enum.GetValues(typeof(BondType)))
          {
            // skip some special cases where the general test doesn't apply

            if (type == BondType.ITLGovt || type == BondType.USTBill)
              continue;

            Bond b = new Bond(
              issue,
              maturity,
              ccy,
              type,
              coupon,
              dc,
              CycleRule.None,
              freq,
              roll,
              cal)
            {
              Notional = 1,
              PeriodAdjustment = false
            };

            Dt settle = issue;
            do
            {
              BondPricer pricer = new BondPricer(b, settle)
              {
                MarketQuote = 1.0,
                QuotingConvention = QuotingConvention.FlatPrice
              };
              Assert.AreEqual(coupon, pricer.YieldToMaturity(), TOLERANCE,
                dc + "/" + freq + "/" + type + "/" + settle);
            } while ((settle = b.Schedule.GetNextCouponDate(settle)) < maturity);
          }
        }
      }
    }

    [Test, Smoke]
    public void DiscountMarginBeforeBondEffective()
    {
      const double TOLERANCE = 0.00025;
      var issue = new Dt(14, 11, 2014);
      var effective = new Dt(21, 11, 2014);
      var maturity = new Dt(21, 11, 2025);

      const double coupon = 0.04596;
      var cal = Calendar.NYB;
      const BDConvention roll = BDConvention.Following;
      const Currency ccy = Currency.USD;
      var rateIndex = new InterestRateIndex("USDLIBOR", Frequency.Quarterly, ccy, DayCount.Actual360, cal, 0);
      for (Dt settle = issue; settle < effective; settle = Dt.AddDays(settle, 1, cal))
      {
        var discountCurve = new DiscountCurve(settle, 0.01);
        var referenceCurve = discountCurve;

        foreach (var dc in new[]
            {
              DayCount.Thirty360, DayCount.Thirty360Isma, DayCount.ThirtyE360, DayCount.Actual360, DayCount.Actual365Fixed, DayCount.Actual365L,
              DayCount.Actual366, DayCount.ActualActual, DayCount.ActualActualBond, DayCount.ActualActualEuro
            })
        {
          foreach (var freq in new[] {Frequency.SemiAnnual, Frequency.Quarterly, Frequency.Annual})
          {
            foreach (BondType type in Enum.GetValues(typeof(BondType)))
            {
              var b = new Bond(
                effective,
                maturity,
                ccy,
                type,
                coupon,
                dc,
                CycleRule.None,
                freq,
                roll,
                cal)
              {
                Notional = 1,
                PeriodAdjustment = false,
                Index = rateIndex.IndexName,
                ReferenceIndex = rateIndex,
                Tenor = rateIndex.IndexTenor
              };

                var pricer = new BondPricer(b, settle, effective)
                { 
                  ProductSettle = settle,
                  MarketQuote = b.Coupon,
                  QuotingConvention = QuotingConvention.DiscountMargin,
                  DiscountCurve = discountCurve,
                  ReferenceCurve = referenceCurve
                };
                Assert.AreEqual(1.0, pricer.FullPrice(), TOLERANCE,
                  string.Format("The full price [{0}] does not match par on a floating bond quoted with DiscountMargin equal to spread on/before effective date [{1}]", pricer.FullPrice(), effective));
              
            }
          }
        }
      }
    }

  }
}
