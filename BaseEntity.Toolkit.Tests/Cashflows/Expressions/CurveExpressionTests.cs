//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Numerics;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  [TestFixture]
  public class CurveExpressionTests
  {
    #region Nested type: fake objects used in tests

    /// <summary>Mock object for overlay curve</summary>
    class MyOverlayCurve : Curve
    {
      public MyOverlayCurve(Dt asOf, Curve baseCurve)
        : base(asOf, Frequency.Continuous, baseCurve)
      { }
    }

    /// <summary>Mock object for spot</summary>
    class MySpot : ISpot
    {
      public Currency Ccy { get; set; }
      public string Name { get; set; }
      public Dt Spot { get; set; }
      public double Value { get; set; }
    }

    [Flags]
    public enum VariantSet { None = 0, Base = 1, Overlay = 2, All = 3 }

    #endregion

    [TestCase(VariantSet.Base)]
    [TestCase(VariantSet.Overlay)]
    public static void OverlayCurveInterpolate(VariantSet flag)
    {
      Dt asOf = Dt.Today(), date = Dt.Add(asOf, 365);
      double spreadFactor = 0.95;
      var interp = new Weighted(new Const(), new Const());
      // Create base curve
      var baseCurve = new DiscountCurve(asOf, 0.1)
      {
        Name = "Discount",
        Interp = interp
      };
      // Create an overlay on the top of the base curve
      var curve = new MyOverlayCurve(asOf, baseCurve)
      {
        Name = "Projection",
        Interp = interp
      };
      curve.Add(date, spreadFactor);

      var variables = new List<object>();
      if ((flag & VariantSet.Base) != 0) variables.Add(baseCurve);
      if ((flag & VariantSet.Overlay) != 0) variables.Add(curve);
      using (E.PushVariants(variables))
      {
        // construct the curve interpolation expression
        var expr = E.Interpolate(curve, date);
        ((IResettable)expr).Reset();

        if ((flag & VariantSet.Overlay) == 0)
        {
          // The expression should be
          //    spreadFactor*baseCurve.Interpolate(date)
          var f = expr as IAffine;
          Assert.That(f, Is.Not.Null);
          Assert.That(f.A, Is.EqualTo(spreadFactor));
          Assert.That(f.B, Is.EqualTo(0.0));
          var c = f.X as CurveInterpolateEvaluable;
          Assert.That(c, Is.Not.Null);
          Assert.That(c.Curve, Is.SameAs(baseCurve));
        }

        // The expression evaluate to the right value
        var expect1 = curve.Interpolate(date);
        expr.ShouldEvaluateTo(expect1);

        // Interpolate on the same curve and same date
        // should return the same instance
        Assert.That(E.Interpolate(curve, date), Is.SameAs(expr));

        // Changing the curve without reset the expression
        // has no impact on IEvaluable.
        baseCurve.SetRate(0, 0.2);
        var expect2 = curve.Interpolate(date);
        Assert.That(expect2,Is.Not.EqualTo(expect1));
        Assert.That(expr.Evaluate(), Is.EqualTo(expect1));

        // It should have effect after reset.
        foreach (var v in E.GetAllEvaluables().OfType<IResettable>())
          v.Reset();
        expr.ShouldEvaluateTo(expect2);
      }
    }

    [Test]
    public static void InvariantCurveInterpolate()
    {
      Dt asOf = Dt.Today(), date = Dt.Add(asOf, 365);

      // This curve is treated as invariant, because it is not
      // in the non-empty set of invariant objects below.
      var curve = new DiscountCurve(asOf, 0.2);
      using (E.PushVariants(new[] { new Curve() }))
      {
        // For constant curve, these expressions should be constant
        var datedConst = E.Interpolate(curve, date);
        double expectValue = curve.Interpolate(date);
        datedConst.ShouldBeConstant(expectValue);
        E.Interpolate(curve, datedConst).ShouldBeConstant(expectValue);
      }
    }

    [TestCase(DayCount.None)]
    [TestCase(DayCount.Actual360)]
    [TestCase(DayCount.Actual365Fixed)]
    public static void CurveInterpolateWithoutPool(DayCount dayCount)
    {
      // We need to test the case with non-zero minutes
      Dt asOf = Dt.Today(), date = new Dt(asOf, 364.5 / 365);

      // This curve will be treated as variable curve
      var curve = new DiscountCurve(asOf)
      {
        DayCount = dayCount
      };
      curve.Add(asOf, 1);
      curve.SetRate(0, 0.5);

      // construct the curve interpolation expression
      var expr = E.Interpolate(curve, date);

      // the expression should evaluate the right value
      var expect1 = curve.Interpolate(date);
      expr.ShouldEvaluateTo(expect1);

      // Changing the curve should have immediate impact on IEvaluable.
      curve.SetRate(0, 0.25);
      var expect2 = curve.Interpolate(date);
      expr.ShouldEvaluateTo(expect2);

      Assert.That(E.GetAllEvaluables(), Is.Null);
      Assert.That(E.GetCommonEvaluables(),Is.Null);
    }

    [Test]
    public static void CurveInterpolateVariableDate()
    {
      Dt asOf = Dt.Today(), date = Dt.Add(asOf, 365);
      var curve = new DiscountCurve(asOf, 0.4);
      var dated = new MySpot { Name = "SpotPrice" };
      using (E.PushVariants())
      {
        var datedExpr = E.SpotPrice(dated);

        // construct the curve interpolation expression
        var expr = E.Interpolate(curve, datedExpr);
        expr.ShouldBeSameAs(E.Unique(new CurveInterpolateVariableDate(
          curve, (IVariableDate)datedExpr)));

        // the expression should evaluate the right value
        var expect = curve.Interpolate(date);
        dated.Spot = date;
        expr.ShouldEvaluateTo(expect);
      }
    }

    [TestCase(DayCount.None)]
    [TestCase(DayCount.Actual360)]
    [TestCase(DayCount.Actual365Fixed)]
    public static void CurveInterpolateWithPool(DayCount dayCount)
    {
      // We need to test the case with non-zero minutes
      Dt asOf = Dt.Today(), date = new Dt(asOf, 364.5 / 365);

      // This curve will be treated as variable curve
      var curve = new DiscountCurve(asOf)
      {
        DayCount = dayCount
      };
      curve.Add(asOf, 1);
      curve.SetRate(0, 0.5);

      using (E.PushVariants())
      {
        // construct the curve interpolation expression
        var expr = E.Interpolate(curve, date);
        if (dayCount == DayCount.Actual365Fixed || dayCount == DayCount.None)
        {
          expr.ShouldBeSameAs(E.Unique(new CurveInterpolateConstantTime(
            curve, date)));
        }
        else
        {
          expr.ShouldBeSameAs(E.Unique(new CurveInterpolateConstantDate(
            curve, date)));
        }

        // make sure the evaluable pool is not empty
        Assert.That(E.GetAllEvaluables(),Is.Not.Null);

        // enable reset mechanism
        ((IResettable)expr).Reset();

        // the expression should evaluate the right value
        var expect1 = curve.Interpolate(date);
        expr.ShouldEvaluateTo(expect1);

        // Interpolate on the same curve and same date
        // should return the same instance
        Assert.That(E.Interpolate(curve, date),Is.SameAs(expr));

        // Changing the curve without reset the expression
        // has no impact on IEvaluable.
        curve.SetRate(0, 0.25);
        var expect2 = curve.Interpolate(date);
        Assert.That(expect2,Is.Not.EqualTo(expect1));
        Assert.That(expr.Evaluate(),Is.EqualTo(expect1));

        // It should have effect after reset.
        foreach (var v in E.GetAllEvaluables().Cast<IResettable>())
          v.Reset();
        expr.ShouldEvaluateTo(expect2);
      }
    }

    [Test]
    public void CurveExceptions()
    {
      // This curve will be treated as variable curve
      var curve = new DiscountCurve(Dt.Today(), 0.25);
      using (E.PushVariants())
      {
        var dated = E.Constant(0.9, Dt.Empty);
        Assert.That(() => E.Interpolate(curve, dated),
          Throws.InstanceOf<ArgumentException>()
          .With.Message.Contains("not a dated constant"));

        Variable notDated = 15;
        Assert.That(() => E.Interpolate(curve, notDated),
          Throws.InstanceOf<ArgumentException>()
            .With.Message.Contains("not a dated expression"));
      }
    }

    [Test]
    public void InterpolateOnSameNativeCurve()
    {
      // This curve will be treated as variable curve
      var curve1 = new DiscountCurve(Dt.Today(), 0.5);
      using (E.PushVariants(new[] { curve1 }))
      {
        // curve2 share the same native curve as curve1,
        // but is a different instance in managed world.
        var curve2 = (Curve)((BaseEntity.Toolkit.Curves.Native.Curve)curve1);
        Assert.That(curve2,Is.Not.SameAs(curve1));

        // Interpolate on curve1 and curve2 should return the same instance
        // because they refer to the same native curve.
        var date = new Dt(curve1.AsOf, 0.5 / 365);
        var ex = E.Interpolate(curve1, date);
        E.Interpolate(curve2, date).ShouldBeSameAs(ex);
      }
    }
  }

}
