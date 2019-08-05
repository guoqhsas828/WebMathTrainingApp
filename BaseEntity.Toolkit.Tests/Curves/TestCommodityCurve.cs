//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestCommodityCurve : ToolkitTestBase
  {
    /// <summary>
    ///   Round trip tests of the spot and lease rate.
    /// </summary>
    [TestCase(0.05, 0.125)]
    [TestCase(0.0, 0.125)]
    [TestCase(0.0, 0.0)]
    [TestCase(0.0, -0.125)]
    [TestCase(0.05, -0.125)]
    public void LeaseRate(double interestRate, double leaseRate)
    {
      const double spot = 100;
      var asOf = Dt.Today();
      var dc = new DiscountCurve(asOf).SetRelativeTimeRate(interestRate);
      var cc = new CommodityCurve(asOf, spot, dc, leaseRate, null);
      AssertEqual("SpotValue", spot, cc.Spot.Value);
      AssertEqual("SpotPrice", spot, cc.SpotPrice);
      var maturity = asOf + (RelativeTime)1.0;
      var equivRate = cc.EquivalentLeaseRate(maturity);
      AssertEqual("EquivLeaseRate", leaseRate, equivRate, 1E-12);
      var impliedRate = cc.ImpliedLeaseRate(asOf,maturity);
      AssertEqual("ImpliedLeaseRate", leaseRate, impliedRate, 1E-12);
    }

    /// <summary>
    ///   Verify the forward interpolation with discrete lease payments (positive and negative)
    /// </summary>
    [TestCase(0.05, 1)]
    [TestCase(0.05, -1)]
    public void LeasePayments(double interestRate, int sign)
    {
      const double spot = 100;
      const double lease = 2.0;
      Dt asOf = Dt.Today();
      Dt date1 = asOf + (RelativeTime)0.25;
      Dt date2 = asOf + (RelativeTime)0.5;
      var ds = new DividendSchedule(asOf, new[]
        {
          Tuple.Create(date1, DividendSchedule.DividendType.Fixed, sign*lease),
          Tuple.Create(date2, DividendSchedule.DividendType.Fixed, sign*lease),
        });
      var dc = new DiscountCurve(asOf).SetRelativeTimeRate(interestRate);
      var cc = new CommodityCurve(asOf, spot, dc, 0.0, ds);

      double expectFwd, actualFwd;

      // The forward just before the first lease payment
      var onehour = (RelativeTime)(1.0 / 24 / RelativeTime.DaysPerYear);
      expectFwd = spot * Math.Exp(interestRate * (0.25 - onehour));
      actualFwd = cc.Interpolate(date1 - onehour);
      AssertEqual("Forward 0", expectFwd, actualFwd);

      // The forward on the first payment date
      expectFwd = spot * Math.Exp(interestRate * 0.25) - sign*lease;
      actualFwd = cc.Interpolate(date1);
      AssertEqual("Forward 1", expectFwd, actualFwd, 1E-12);

      // The forward on the second payment date
      expectFwd = spot * Math.Exp(interestRate * 0.5)
        - sign*lease*(Math.Exp(interestRate*0.25) + 1);
      actualFwd = cc.Interpolate(date2);
      AssertEqual("Forward 2", expectFwd, actualFwd);
    }
  }
}
