//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestCustomCurveInterpolator : ToolkitTestBase
  {
    #region Parameteric
    /// <summary>
    ///  A simple implementation of a parametric interpolation which uses no data point.
    /// </summary>
    private class ParametricInterpolator : ICurveInterpolator
    {
      private readonly double _a, _b, _c;

      public ParametricInterpolator(double a, double b, double c)
      {
        _a = a;
        _b = b;
        _c = c;
      }

      #region ICurveInterpolator Members

      public void Initialize(BaseEntity.Toolkit.Curves.Native.Curve curve)
      {
        // do nothing;
      }

      public double Evaluate(BaseEntity.Toolkit.Curves.Native.Curve curve, double t, int index)
      {
        double x = t / 365.0;
        return Math.Exp(_a * x * x + _b * x + _c);
      }

      #endregion

      #region ICloneable Members

      public object Clone()
      {
        return new ParametricInterpolator(_a,_b,_c);
      }

      #endregion
    }

    [Test]
    public void TestParametricInterp()
    {
      double a = 0.0001, b = -0.05, c = 0.0;
      var asOf = Dt.Today();
      var discountCurve = new DiscountCurve(asOf);
      discountCurve.Initialize(new ParametricInterpolator(a,b,c));
      for (int i = 0; i < 10; ++i)
      {
        Dt date = Dt.Add(asOf, i + 1, TimeUnit.Years);
        var actual = discountCurve.Interpolate(date);
        // calculate the expect
        var t = (date - asOf) / 365.0;
        var expect = Math.Exp(a * t * t + b * t + c);
        Assert.AreEqual(expect, actual, 1E-15);
      }
    }
    #endregion

    #region Linear

    /// <summary>
    ///  A simple implementation of linear interpolation with constant extrapolation
    /// </summary>
    private class LinearInterpolator : ICurveInterpolator
    {
      #region ICurveInterpolator Members

      public void Initialize(NativeCurve nativeCurve)
      {
        // do nothing;
      }

      public double Evaluate(NativeCurve nativeCurve, double t, int index)
      {
        var curve = (Curve)nativeCurve;
        int last = curve.Count - 1;
        if (last < 0)
        {
          throw new ToolkitException(String.Format("No point found in curve {0}",
            curve.Name));
        }
        if (index >= last)
        {
          // upper extrapolation: use the last value.
          return curve.GetVal(last);
        }
        if (index < 0)
        {
          // lower extrapolation: use the first point
          return curve.GetVal(0);
        }
        Dt asOf = curve.AsOf;
        double t0 = curve.GetDt(index) - asOf, y0 = curve.GetVal(index);
        double t1 = curve.GetDt(index + 1) - asOf, y1 = curve.GetVal(index + 1);
        return y0 + (y1 - y0) / (t1 - t0) * (t - t0);
      }

      #endregion

      #region ICloneable Members

      public object Clone()
      {
        return new LinearInterpolator();
      }

      #endregion
    }

    [Test]
    public void TestLinearInterp()
    {
      var asOf = Dt.Today();
      var curve = new VolatilityCurve(asOf);
      curve.Initialize(new LinearInterpolator());

      Dt date1 = Dt.Add(asOf, 1, TimeUnit.Years);
      double y1 = 0.4;
      curve.Add(date1, y1);

      Dt date2 = Dt.Add(asOf, 2, TimeUnit.Years);
      double y2 = 0.2;
      curve.Add(date2, y2);

      foreach (var tenor in new[] { "6M", "18M", "30M" })
      {
        Dt date = Dt.Add(asOf, tenor);
        var actual = curve.Interpolate(date);
        // calculate the expect
        double expect;
        if (date <= date1)
          expect = y1;
        else if (date >= date2)
          expect = y2;
        else
          expect = y1 + (y2 - y1) / (date2 - date1) * (date - date1);
        Assert.AreEqual(expect, actual, 1E-15);
      }
    }

    #endregion
  }
}
