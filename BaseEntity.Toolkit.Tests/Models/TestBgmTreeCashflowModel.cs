//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Collections;

using NUnit.Framework;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBgmTreeCashflowModel
  {
    private double sigma_ = 0.2;
    private double interestRate_ = 0.05;
    private double hazardRate_ = 0.5;
    private double recoveryRate_ = 0.4;
    private double treeTol = 1E-12;

    [Flags]
    enum ScheduleFeature
    {
      None = 0, Amortizing = 1, StepUpCoupon = 2, StepDownCoupon = 4
    }

    private void PvConsistencyTest(double coupon, string tenor,
      Frequency freq, double hazardRate, ScheduleFeature feature,
      int stepsPerYear)
    {
      const int daysToSettle = 0;

      // Nonempty s means with survival curve.
      string s = hazardRate != 0 ? "S-" : "";

      // Create curves
      Dt asOf = Dt.Today();
      Dt settle = asOf;
      if (daysToSettle > 0)
        settle = Dt.AddDays(settle, daysToSettle, Calendar.None);
      Dt maturity = Dt.Add(asOf, tenor);
      var discountCurve = new DiscountCurve(asOf, interestRate_);
      var survivalCurve = new SurvivalCurve(asOf, hazardRate);

      // Create a plain bond
      Bond bond = new Bond(asOf, maturity, Currency.USD, BondType.None,
        coupon, DayCount.Actual365Fixed, CycleRule.None, freq,
        BDConvention.None, Calendar.None);
      if ((feature & ScheduleFeature.Amortizing) != 0)
      {
        for (int i = 1; i <= 2; ++i)
        {
          Dt date = Dt.Add(asOf, freq, i, CycleRule.None);
          if (date > maturity) break;
          bond.AmortizationSchedule.Add(new Amortization(date,
            AmortizationType.PercentOfInitialNotional, 1.0 / 2));
        }
      }
      if ((feature & ScheduleFeature.StepUpCoupon) != 0)
      {
        for (int i = 1; i <= 12; ++i)
        {
          Dt date = Dt.Add(asOf, freq, i, CycleRule.None);
          if (date > maturity) break;
          bond.CouponSchedule.Add(new CouponPeriod(date,
            coupon * (1 + i / 12.0)));
        }
      }
      else if ((feature & ScheduleFeature.StepDownCoupon) != 0)
      {
        for (int i = 1; i <= 12; ++i)
        {
          Dt date = Dt.Add(asOf, freq, i, CycleRule.None);
          if (date > maturity) break;
          bond.CouponSchedule.Add(new CouponPeriod(date,
            coupon * (1 - i / 12.0)));
        }
      }

      // Use the regular bond pricer to price it.
      var pricer = new BondPricer(bond, asOf, settle, discountCurve,
        survivalCurve, 0, TimeUnit.None, recoveryRate_);
      var expect = pricer.ProductPv();
      var cf = pricer.Cashflow;

      int count = cf.Count;
      var volCurves = new VolatilityCurve[count];
      {
        var vc = new VolatilityCurve(asOf, sigma_);
        for (int i = 1; i < count; ++i)
          volCurves[i] = vc;
      }
      var dates = ListUtil.CreateList(count, (i) => cf.GetDt(i)).ToArray();
      var tree = BuildTree(cf, asOf, settle,
        discountCurve, null, dates, volCurves, stepsPerYear, treeTol);

      // Calculate the pv of the same bond with a tree.
      // Case 1: no call option.
      //var callable = (Bond)bond.ShallowCopy();
      {
        double bgmPv = tree.CalculatePv(dates,
          (i, v, r) => cf.GetAccrued(i) + cf.GetAmount(i) + v);
        Assert.AreEqual(expect, bgmPv, 5E-8, "BGM-N-" + s + tenor);
      }
    }


    private static BgmTreeCashflowModel.CalibratedTree BuildTree(
      Cashflow cf, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve survivalCurve,
#if NotYet
      BgmForwardVolatilitySurface surface,
#endif
      Dt[] tenorDates,
      VolatilityCurve[] volatilityCurves,
      int stepsPerYear, double tolerance)
    {
      RateSystem tree;
      if (stepsPerYear <= 0) stepsPerYear = 12;

#if NotYet // Build a raw tree
      Dt[] tenorDates;
      {
        var cfDates = ListUtil.CreateList<Dt>(cf.Count, cf.GetDt);
        var volObj = surface.CalibratedVolatilities;
        tenorDates = Array.ConvertAll(volObj.TenorDates, (t) => new Dt(asOf, t));
        tree = BgmBinomialTree.CalculateRateSystem(
          1.0/stepsPerYear, tolerance, asOf, discountCurve,
          tenorDates, volObj.BuildBlackVolatilityCurves(), cfDates);
      }
#else
      {
        var cfDates = ListUtil.CreateList<Dt>(cf.Count, cf.GetDt);
        tree = BgmBinomialTree.CalculateRateSystem(
          1.0 / stepsPerYear, tolerance, asOf, discountCurve,
          tenorDates, volatilityCurves, cfDates);
      }
#endif

      Dt[] dates = tree.NodeDates;
      var dist = new RateAnnuity[dates.Length][];

      // Walk through the tree and match the term structure of the discount curve.
      int last = dates.Length - 1;
      for (int iDate = dates.Length; --iDate >= 0;)
      {
        int rateIndex = tree.GetLastResetIndex(iDate);
        Debug.Assert(dates.Length > tenorDates.Length || rateIndex == iDate,
          "Tenor and rates not match");
        Dt tenorEnd = tenorDates[rateIndex];
        Dt begin = dates[iDate];
        Dt end = iDate == last ? tenorEnd : dates[iDate + 1];
        Debug.Assert(tenorEnd >= end, "tenorEnd >= end");
        double fracEnd = (tenorEnd - end) / 365.0;
        double fracBegin = (tenorEnd - begin) / 365.0;
        int nStates = tree.GetStateCount(iDate);
        var ras = new RateAnnuity[nStates];
        double suma1 = 0, suma0 = 0, sump = 0;
        for (int i = nStates; --i >= 0;)
        {
          double p = nStates == 1 ? 1.0 : tree.GetProbability(iDate, i),
            a = tree.GetAnnuity(rateIndex, iDate, i),
            r = tree.GetRate(rateIndex, iDate, i);
          double a1 = ras[i].Annuity = a * (1 + fracEnd * r);
          double a0 = ras[i].Rate = a * (1 + fracBegin * r);
          sump += p;
          suma1 += p * a1;
          suma0 += p * a0;
        }
        suma1 /= sump;
        double f1 = discountCurve.DiscountFactor(asOf, end) / suma1;
        suma0 /= sump;
        double f0 = discountCurve.DiscountFactor(asOf, begin) / suma0;
        double frac = (end - begin) / 365.0;
        for (int i = nStates; --i >= 0;)
        {
          double a1 = ras[i].Annuity *= f1;
          double a0 = ras[i].Rate *= f0;
          ras[i].Rate = (a0 / a1 - 1) / frac;
        }
        dist[iDate] = ras;
      }
      return new BgmTreeCashflowModel.CalibratedTree {Tree = tree, RateAnnuities = dist};
    }


    private void PvConsistencyTest(double coupon, string tenor,
      Frequency freq, double hazardRate)
    {
      PvConsistencyTest(coupon, tenor, freq, hazardRate, ScheduleFeature.None, 12*10);
    }

    [Test]
    public void ZeroCouponBond()
    {
      //PvConsistencyTest(0.0, "1M", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "2M", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "3M", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "6M", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "1Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "2Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "5Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.0, "10Y", Frequency.Monthly, 0);
    }

    [Test]
    public void CouponBond()
    {
      //PvConsistencyTest(0.1, "1M", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "2M", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "3M", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "6M", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "1Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "2Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "5Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "10Y", Frequency.Monthly, 0);
      PvConsistencyTest(0.1, "15Y", Frequency.Monthly, 0,
        ScheduleFeature.None, 40);
      PvConsistencyTest(0.1, "20Y", Frequency.Quarterly, 0,
        ScheduleFeature.None, 50);
    }

    [Test]
    public void AmortizingBond()
    {
      //PvConsistencyTest(0.1, "1M", Frequency.Monthly, 0,
      //  ScheduleFeature.Amortizing);
      PvConsistencyTest(0.1, "2M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "3M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "4M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "5M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "6M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "7M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "8M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "9M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "10M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "11M", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
      PvConsistencyTest(0.1, "1Y", Frequency.Monthly, 0,
        ScheduleFeature.Amortizing, 120);
    }
  }
}
