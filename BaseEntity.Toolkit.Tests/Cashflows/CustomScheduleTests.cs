//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture]
  public class CustomScheduleTests
  {
    #region Tests

    [Test]
    public void CouponPeriods()
    {
      ISchedule regular, custom;
      GetRegularAndCustomSchedules(out regular, out custom);
      var periods0 = regular.Periods;
      var periods1 = custom.Periods;

      Assert.That(periods1.Count,Is.EqualTo(periods0.Count));
      for (int i = 0, n = periods0.Count; i < n; ++i)
      {
        var p0 = periods0[i];
        var p1 = periods1[i];
        Assert.That(p1.AccrualBegin,Is.EqualTo(p0.AccrualBegin));
        Assert.That(p1.AccrualEnd,Is.EqualTo(p0.AccrualEnd));
        Assert.That(p1.CycleBegin,Is.EqualTo(p0.CycleBegin));
        Assert.That(p1.CycleEnd,Is.EqualTo(p0.CycleEnd));
        Assert.That(p1.Payment,Is.EqualTo(p0.Payment));
      }
    }

    [Test]
    public void NextCouponDate()
    {
      ISchedule regular, custom;
      GetRegularAndCustomSchedules(out regular, out custom);

      Func<ISchedule, Dt, Dt> fn = (sched, dt) => sched.GetNextCouponDate(dt);
      CheckAllDates(regular, custom, 0, fn);
      CheckAllDates(regular, custom, regular.Periods.Count - 1, fn);
    }

    [Test]
    public void PrevCouponDate()
    {
      ISchedule regular, custom;
      GetRegularAndCustomSchedules(out regular, out custom);

      Func<ISchedule, Dt, Dt> fn = (sched, dt) => sched.GetPrevCouponDate(dt);
      CheckAllDates(regular, custom, 0, fn);
      CheckAllDates(regular, custom, regular.Periods.Count - 1, fn);
    }

    [Flags]
    public enum Spec
    {
      NoSpread = 0,
      ConstSpread = 1,
      StepUpCoupon = 2,
      StepDownSpread = 4,
    }

    [TestCase(Spec.NoSpread)]
    [TestCase(Spec.ConstSpread)]
    [TestCase(Spec.StepUpCoupon)]
    [TestCase(Spec.StepDownSpread)]
    [TestCase(Spec.StepUpCoupon | Spec.StepDownSpread)]
    public void SwaptRatesLevelsStrikes(Spec spec)
    {
      // Product terms
      // Market environment
      var asOf = Dt.Roll(Dt.Add(effective, Frequency.Monthly, -3,
        CycleRule.None), bdc, calendar);
      var discountCurve = new DiscountCurve(asOf, 0.03)
      {
        Ccy = ccy,
        ReferenceIndex = index
      };

      // Regular products without custom schedules
      var fixedLeg = new SwapLeg(effective, maturity, ccy,
        spec.HasFlag(Spec.StepUpCoupon) ? 0.0 : 0.05,
        DayCount.Actual365Fixed, Frequency.SemiAnnual,
        bdc, calendar, false);
      if ((spec & Spec.StepUpCoupon) != 0)
      {
        fixedLeg.CouponSchedule.Add(new CouponPeriod(effective, 0.02));
        fixedLeg.CouponSchedule.Add(new CouponPeriod(middle, 0.05));
      }
      var floatLeg = new SwapLeg(effective, maturity, Frequency.Quarterly,
        spec == Spec.ConstSpread ? 0.02 : 0.0, index);
      if ((spec & Spec.StepDownSpread) != 0)
      {
        floatLeg.CouponSchedule.Add(new CouponPeriod(effective, 0.02));
        floatLeg.CouponSchedule.Add(new CouponPeriod(middle, 0.01));
      }

      // Calculate the swap rate, level, and strike.
      SetUsePaymentScheduleForCashflow(false);
      double rate0, level0;
      var strike0 = new Swaption(asOf, effective, ccy, fixedLeg, floatLeg,
        0, PayerReceiver.Payer, OptionStyle.European, 0)
        .EffectiveSwaptionStrike(asOf, asOf, discountCurve, discountCurve,
          null, true, out rate0, out level0);

      // Let's change every thing to custom schedules and clear
      // all the other schedule information.
      SetCustomPaymentSchedule(fixedLeg,
        new SwapLegPricer(fixedLeg, effective, effective,
          1.0, discountCurve, null, null, null, null, null)
          .GetPaymentSchedule(null, effective));
      SetCustomPaymentSchedule(floatLeg,
        new SwapLegPricer(floatLeg, effective, effective,
          1.0, discountCurve, index, discountCurve, null, null, null)
          .GetPaymentSchedule(null, effective));

      // Calculate the swap rate, level, and strike.
      SetUsePaymentScheduleForCashflow(true);
      double rate1, level1;
      var strike1 = new Swaption(asOf, effective, ccy, fixedLeg, floatLeg,
        0, PayerReceiver.Payer, OptionStyle.European, 0)
        .EffectiveSwaptionStrike(asOf, asOf, discountCurve, discountCurve,
          null, true, out rate1, out level1);

      // Do we get the same swap rates, levels and strikes?
      Assert.That(strike1,Is.EqualTo(strike0).Within(1E-14));
      Assert.That(rate1,Is.EqualTo(rate0).Within(1E-14));
      Assert.That(level1,Is.EqualTo(level0).Within(1E-14));

      return;
    }

    #endregion

    #region Utilities and data

    private static void CheckAllDates(
      ISchedule regular, ISchedule custom, int index,
      Func<ISchedule, Dt, Dt> getDateFunction)
    {
      Dt begin = regular.Periods[index].AccrualBegin - 10,
        end = regular.Periods[index].AccrualEnd + 10;
      for (Dt date = begin; date < end; date = date + 1)
      {
        var res0 = getDateFunction(regular, date);
        var res1 = getDateFunction(custom, date);
        Assert.That(res1,Is.EqualTo(res0),date.ToInt().ToString());
      }
    }

    private void GetRegularAndCustomSchedules(
      out ISchedule regularSchdule, out ISchedule customSchdule)
    {
      var swapLeg = new SwapLeg(effective, maturity, ccy,
        0.05, DayCount.Actual365Fixed, Frequency.SemiAnnual,
        bdc, calendar, false);
      regularSchdule = swapLeg.Schedule;

      SetCustomPaymentSchedule(swapLeg, new SwapLegPricer(
        swapLeg, effective, effective, 1.0, null, null, null,
        null, null, null).GetPaymentSchedule(null, effective));
      customSchdule = swapLeg.CustomPaymentSchedule;
    }

    public void SetCustomPaymentSchedule(
      SwapLeg swapLeg, PaymentSchedule ps)
    {
      swapLeg.CustomPaymentSchedule = ps;
      swapLeg.CouponSchedule.Clear();
      swapLeg.Freq = Frequency.None;
      swapLeg.BDConvention = BDConvention.None;
      swapLeg.DayCount = DayCount.None;
    }

    public static void SetUsePaymentScheduleForCashflow(bool enable)
    {
      var field = typeof (RateVolatilityUtil).GetField(
        "_usePaymentScheduleForCashflow",
        System.Reflection.BindingFlags.Static |
          System.Reflection.BindingFlags.NonPublic);
      field.SetValue(null, enable);
    }

    private readonly Dt effective = new Dt(20160322),
      middle = new Dt(20180320),
      maturity = new Dt(20210322);
    private readonly Calendar calendar = Calendar.NYB;
    private readonly BDConvention bdc = BDConvention.Following;
    private readonly DayCount dayCount = DayCount.Actual360;
    private readonly Currency ccy = Currency.USD;
    private readonly ReferenceIndex index = StandardReferenceIndices.Create("USDLIBOR_3M");

    #endregion
  }
}