//
// Copyright (c)    2002-2015. All rights reserved.
//

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows
{

  [TestFixture]
  public class CompoundingPrincipalTests
  {
    public enum CouponType
    {
      Constant,
      StepUp,
      StepDown,
    }

    [TestCase(CouponType.Constant)]
    [TestCase(CouponType.StepDown)]
    [TestCase(CouponType.StepUp)]
    public void FixedCouponCompounding(CouponType cpnType)
    {
      var ccy = Currency.EUR;
      var dayCount = DayCount.ActualActualBond;
      var compoundingConv = CompoundingConvention.ISDA;

      var payments = BuildSchedule(cpnType, ccy, dayCount).ToList();
      var expect = CalculateCompoundedCoupon(payments);

      var swapleg = CreateSwapLeg(payments, compoundingConv);

      var asOf = new Dt(2, 8, 2016);
      var pricer = new SwapLegPricer(swapleg, asOf, asOf, 1.0,
        new DiscountCurve(asOf, 0.0), null, null, null, null, null);

      var pv = pricer.Pv();
      Assert.AreEqual(expect, pv, 1E-15);
    }

    private static SwapLeg CreateSwapLeg(
      IList<InterestPayment> payments,
      CompoundingConvention compoundingConv)
    {
      var count = payments.Count;
      Debug.Assert(count > 0);

      var ps = new PaymentSchedule();
      ps.AddPayments(payments);

      var ccy = payments[0].Ccy;
      var dayCount = payments[0].DayCount;
      Dt effective = payments[0].AccrualStart,
        maturity = payments[count - 1].AccrualEnd;
      return new SwapLeg(effective, maturity,
        ccy, 0, dayCount, Frequency.None,
        BDConvention.None, Calendar.None, false)
      {
        CompoundingConvention = compoundingConv,
        CustomPaymentSchedule = ps,
      };

    }

    private static double CalculateCompoundedCoupon(
      IList<InterestPayment> payments)
    {
      double sum = 0, principal = payments[0].Notional;
      for (int i = 0, n = payments.Count; i < n; ++i)
      {
        var p = payments[i];
        Assert.IsFalse(p.AccrualStart.IsEmpty(), "AccrualStart is empty");
        Assert.IsFalse(p.AccrualEnd.IsEmpty(), "AccrualEnd is empty");
        var amount = p.EffectiveRate * p.AccrualFactor * principal;
        principal += amount;
        sum += amount;
      }
      return sum;
    }

    private static IEnumerable<InterestPayment> BuildSchedule(
      CouponType cpnType, Currency ccy, DayCount dc)
    {
      double notional = 1.0;
      var data = _cpnDates;
      Dt payDt = new Dt(20, 4, 2018),
        accrualBegin = new Dt(data[0, 1], data[0, 0], data[0, 2]);
      for (int i = 1, n = data.GetLength(0); i < n; ++i)
      {
        Dt accrualEnd = new Dt(data[i, 1], data[i, 0], data[i, 2]);
        yield return new FixedInterestPayment(
          Dt.Empty, payDt, ccy, accrualBegin, accrualEnd,
          accrualBegin, accrualEnd, Dt.Empty, notional,
          Coupon(i, cpnType), dc, Frequency.None)
        {
          AccrueOnCycle = true
        };
        accrualBegin = accrualEnd;
      }
    }

    private static double Coupon(int i, CouponType cpnType)
    {
      const double coupon = 0.02329, delta = 0.002;
      switch (cpnType)
      {
        case CouponType.StepUp:
          return coupon + i * delta;
        case CouponType.StepDown:
          return coupon + i * delta;
        default:
          break;
      }
      return coupon;
    }

    private static int[,] _cpnDates =
    {
      {4, 20, 2007},
      {10, 20, 2007},
      {4, 20, 2008},
      {10, 20, 2008},
      {4, 20, 2009},
      {10, 20, 2009},
      {4, 20, 2010},
      {10, 20, 2010},
      {4, 20, 2011},
      {10, 20, 2011},
      {4, 20, 2012},
      {10, 20, 2012},
      {4, 20, 2013},
      {10, 20, 2013},
      {4, 20, 2014},
      {10, 20, 2014},
      {4, 20, 2015},
      {10, 20, 2015},
      {4, 20, 2016},
      {10, 20, 2016},
      {4, 20, 2017},
      {10, 20, 2017},
      {4, 20, 2018},
    };
  }
}
