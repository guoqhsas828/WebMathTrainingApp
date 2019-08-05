//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestCallableBondModel : ToolkitTestBase
  {
    private double meanReversion_ = -0.5;
    private double sigma_ = 0.2;
    private double interestRate_ = 0.05;
    private double hazardRate_ = 0.5;
    private double recoveryRate_ = 0.4;

    [Flags]
    enum ScheduleFeature
    {
      None = 0, Amortizing = 1, StepUpCoupon = 2, StepDownCoupon = 4
    }

    private void PvConsistencyTest(double coupon, string tenor,
      Frequency freq, double hazardRate, ScheduleFeature feature,
      int notificationDays)
    {
      const int daysToSettle = 1;

      // Nonempty s means with survival curve.
      string s = hazardRate != 0 ? "S-" : "";

      // Create curves
      Dt asOf = new Dt(20180206);
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
        for (int i = 1; i <= 12; ++i)
        {
          Dt date = Dt.Add(asOf, freq, i, CycleRule.None);
          if(date > maturity) break;
          bond.AmortizationSchedule.Add(new Amortization(date,
            AmortizationType.PercentOfInitialNotional, 1.0/12));
        }
      }
      if ((feature & ScheduleFeature.StepUpCoupon) != 0)
      {
        for (int i = 1; i <= 12; ++i)
        {
          Dt date = Dt.Add(asOf, freq, i, CycleRule.None);
          if (date > maturity) break;
          bond.CouponSchedule.Add(new CouponPeriod(date,
            coupon*(1 + i/12.0)));
        }
      }
      else if ((feature & ScheduleFeature.StepDownCoupon) != 0)
      {
        for (int i = 1; i <= 12; ++i)
        {
          Dt date = Dt.Add(asOf, freq, i, CycleRule.None);
          if (date > maturity) break;
          bond.CouponSchedule.Add(new CouponPeriod(date,
            coupon*(1 - i/12.0)));
        }
      }

      // Use the regular bond pricer to price it.
      var pricer = new BondPricer(bond, asOf, settle, discountCurve,
        survivalCurve, 0, TimeUnit.None, recoveryRate_);
      var expect = pricer.ProductPv();

      // Calculate the pv of the same bond with a tree.
      // Case 1: no call option.
      var callable = (Bond) bond.ShallowCopy();
      {
        // With Hull-White process
        double hwPv = HullWhiteTreeCashflowModel.Pv(pricer.BondCashflowAdapter,
          asOf, settle, discountCurve, survivalCurve, recoveryRate_,
          DiffusionProcessKind.HullWhite, meanReversion_, sigma_,
          callable, 0.0);
        Assert.AreEqual(expect, hwPv, 1E-15, "HW-N-" + s + tenor);
        // With Black-Karasinsky process
        double bkPv = HullWhiteTreeCashflowModel.Pv(pricer.BondCashflowAdapter,
          asOf, settle, discountCurve, survivalCurve, recoveryRate_,
          DiffusionProcessKind.BlackKarasinski, meanReversion_, sigma_,
          callable, 0.0);
        Assert.AreEqual(expect, bkPv, 5E-10, "BK-N-" + s + tenor);
      }

      // Calculate the pv of the same bond with a tree.
      // Case 2: call options enabled, but strike at ridiculously high level.
      callable.CallSchedule.Add(new CallPeriod(asOf, maturity,
        100, 1000.0, OptionStyle.American, 0));
      callable.NotificationDays = notificationDays;
      {
        // With Hull-White process
        double hwPv = HullWhiteTreeCashflowModel.Pv(pricer.BondCashflowAdapter,
          asOf, settle, discountCurve, survivalCurve, recoveryRate_,
          DiffusionProcessKind.HullWhite, meanReversion_, sigma_,
          callable, 0.0);
        Assert.AreEqual(expect, hwPv, 1E-15, "HW-C-" + s + tenor);
        // With Black-Karasinsky process
        double bkPv = HullWhiteTreeCashflowModel.Pv(pricer.BondCashflowAdapter,
          asOf, settle, discountCurve, survivalCurve, recoveryRate_,
          DiffusionProcessKind.BlackKarasinski, meanReversion_, sigma_,
          callable, 0.0);
        Assert.AreEqual(expect, bkPv, 5E-10, "BK-C-" + s + tenor);
      }
    }

    private void PvConsistencyTest(double coupon, string tenor,
      Frequency freq, double hazardRate, int notificationDays)
    {
      PvConsistencyTest(coupon, tenor, freq, hazardRate,
        ScheduleFeature.None, notificationDays);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void ZeroCouponBond(int notificationDays)
    {
      PvConsistencyTest(0.0, "1M", Frequency.None, 0, notificationDays);
      PvConsistencyTest(0.0, "1M", Frequency.None, hazardRate_, notificationDays);
      PvConsistencyTest(0.0, "3M", Frequency.None, 0, notificationDays);
      PvConsistencyTest(0.0, "3M", Frequency.None, hazardRate_, notificationDays);
      PvConsistencyTest(0.0, "6M", Frequency.None, 0, notificationDays);
      PvConsistencyTest(0.0, "6M", Frequency.None, hazardRate_, notificationDays);
      PvConsistencyTest(0.0, "9M", Frequency.None, 0, notificationDays);
      PvConsistencyTest(0.0, "9M", Frequency.None, hazardRate_, notificationDays);
      PvConsistencyTest(0.0, "1Y", Frequency.None, 0, notificationDays);
      PvConsistencyTest(0.0, "1Y", Frequency.None, hazardRate_, notificationDays);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void TwoPeriodCouponBond(int notificationDays)
    {
      PvConsistencyTest(0.1, "1Y", Frequency.SemiAnnual, 0, notificationDays);
      PvConsistencyTest(0.1, "1Y", Frequency.SemiAnnual, hazardRate_, notificationDays);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void FourPeriodCouponBond(int notificationDays)
    {
      PvConsistencyTest(0.1, "1Y", Frequency.Quarterly, 0, notificationDays);
      PvConsistencyTest(0.1, "1Y", Frequency.Quarterly, hazardRate_, notificationDays);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void TwelvePeriodCouponBond(int notificationDays)
    {
      PvConsistencyTest(0.1, "1Y", Frequency.Monthly, 0, notificationDays);
      PvConsistencyTest(0.1, "1Y", Frequency.Monthly, hazardRate_, notificationDays);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void AmortizingCouponBond(int notificationDays)
    {
      PvConsistencyTest(0.1, "1Y", Frequency.Monthly,
        0, ScheduleFeature.Amortizing, notificationDays);
      PvConsistencyTest(0.1, "1Y", Frequency.Monthly,
        hazardRate_, ScheduleFeature.Amortizing, notificationDays);
    }
  }
}
