//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;

using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Curves
{
  using NUnit.Framework;

  [TestFixture]
  public class VolatilityCurveTests
  {
    [Test]
    public void FastClone()
    {
      var asOf = _asOf;
      var dates = _dates;

      // Piecewise constant forward volatilities
      var sigmas = CreatePoints(asOf, dates);

      // Create a Black volatility curve.
      var blackCurve = CreateBlackVolatilityCurve(asOf, dates, sigmas);
      Assert.That(blackCurve.Interp, Is.InstanceOf<SquareLinearVolatilityInterp>());
      Assert.That(blackCurve.Interp.UpperExtrap, Is.Null);
      Assert.That(blackCurve.Interp.LowerExtrap, Is.Null);

      // Cloning should work fine
      var cloned = blackCurve.FastClone(null);
      Assert.That(cloned.ToArray(), Is.EqualTo(blackCurve.ToArray()));
    }

    [Test]
    public void PiecewiseFlatCurve()
    {
      var asOf = _asOf;

      var sigmas = CreatePoints(asOf, _dates);
      var curve = new VolatilityCurve(asOf) { { _dates, sigmas } };

      var n = sigmas.Length;
      var actuals = new double[n];

      // Piecewise constant curve is specified by Flat interpolation.
      // The default constructor creates a right continuous interpolator.
      curve.Interp = new Flat();
      for (int i = 0; i < n; ++i)
      {
        actuals[i] = curve.Interpolate(curve.GetDt(i) + 1);
      }
      Assert.That(actuals, Is.EqualTo(sigmas), "Right continuity");


      // To specify the left continuous interpolation,
      // construct the interpolator with a tiny number.
      curve.Interp = new Flat(1E-15);
      for (int i = 0; i < n; ++i)
      {
        actuals[i] = curve.Interpolate(curve.GetDt(i) - 1);
      }
      Assert.That(actuals, Is.EqualTo(sigmas), "Left continuity");
    }


    [Test]
    public void BlackVolatilityCurve()
    {
      var asOf = _asOf;
      var dates = _dates;

      // Piecewise constant forward volatilities
      var sigmas = CreatePoints(asOf, dates);

      // Create a Black volatility curve.
      var blackCurve = CreateBlackVolatilityCurve(asOf, dates, sigmas);

      // Check that the implied forward volatilities match the inputs.
      CheckForwardVolatilities(blackCurve, sigmas);
    }

    [Test]
    public void FowardToBlackVolatilityCurve()
    {
      var asOf = _asOf;

      var dates = _dates;
      var sigmas = CreatePoints(asOf, dates);

      // Create a curve with piecewise constant forward volatilities
      var forwardVolatilityCurve = new VolatilityCurve(asOf)
      {
        Interp = new Flat(1E-15), // left continuous!
      };
      forwardVolatilityCurve.Add(dates, sigmas);

      // Convert to Black volatility curve
      var blackCurve = VolatilityCurve.
        FromForwardVolatilityCurve(forwardVolatilityCurve);

      // Check that the implied forward volatilities match the inputs.
      CheckForwardVolatilities(blackCurve, sigmas);
    }

    [Test]
    public void BlackToForwardVolatilityCurve()
    {
      var asOf = _asOf;
      var dates = _dates;

      // Piecewise constant forward volatilities
      var sigmas = CreatePoints(asOf, dates);

      // Create a Black volatility curve.
      var blackCurve = CreateBlackVolatilityCurve(asOf, dates, sigmas);

      // Convert to piecewise constant forward volatility curve
      var curve = VolatilityCurve.ToForwardVolatilityCurve(blackCurve);

      // Check that the implied forward volatilities match the inputs.
      var points = curve.Select(pt => pt.Value).ToArray();
      Assert.That(points, Is.EqualTo(sigmas).Within(1E-15),
        "Forward volatility curve");

      // Check that the forward curve is left continuous.
      for (int i = 0; i < points.Length; ++i)
      {
        points[i] = curve.Interpolate(curve.GetDt(i) - 1);
      }
      Assert.That(points, Is.EqualTo(sigmas).Within(1E-15), "Left continuity");

      // Can we get back the original Black curve?
      var blackPoints = blackCurve.Select(pt => pt.Value).ToArray();
      var blackCurve1 = VolatilityCurve.FromForwardVolatilityCurve(curve);
      points = blackCurve1.Select(pt => pt.Value).ToArray();
      Assert.That(points, Is.EqualTo(blackPoints).Within(1E-15),
        "Round-trip Black volatility curve");
    }


    private static void CheckForwardVolatilities(
      Curve blackCurve, double[] expects)
    {
      Debug.Assert(blackCurve.Count == expects.Length);

      var n = expects.Length;
      var sigmasOnCurvePpoints = new double[n];
      var sigmasFromInterpolated = new double[n];

      var asOf = blackCurve.AsOf;
      Dt lastTenor = asOf;
      double t0 = 0, v0 = 0;
      for (int i = 0; i < n; ++i)
      {
        var tenor = blackCurve.GetDt(i);
        var t1 = tenor - asOf;
        var v1 = blackCurve.Interpolate(tenor);
        sigmasOnCurvePpoints[i] = Math.Sqrt((v1 * v1 * t1 - v0 * v0 * t0) / (t1 - t0));

        var date = lastTenor + (Dt.Diff(lastTenor, tenor) + 1) / 2;
        var tm = date - asOf;
        var vm = blackCurve.Interpolate(date);
        sigmasFromInterpolated[i] = Math.Sqrt((vm * vm * tm - v0 * v0 * t0) / (tm - t0));

        t0 = t1;
        lastTenor = tenor;
        v0 = v1;
      }
      Assert.That(sigmasOnCurvePpoints, Is.EqualTo(expects).Within(5E-15),
        "Forward volatilities on the curve points");
      Assert.That(sigmasFromInterpolated, Is.EqualTo(expects).Within(5E-15),
        "Forward volatilities from interpolation");

      // extrapolation
      {
        Dt date = blackCurve.GetDt(n - 1) + 30;
        var t1 = date - asOf;
        var v1 = blackCurve.Interpolate(date);
        var s1 = Math.Sqrt((v1 * v1 * t1 - v0 * v0 * t0) / (t1 - t0));
        Assert.AreEqual(expects[n - 1], s1, 5E-15,
          "Forward volatility from extrapolation");
      }
    }

    private static VolatilityCurve CreateBlackVolatilityCurve(
      Dt asOf, Dt[] dates, double[] piecewiseFlatSigmas)
    {
      var blackCurve = new VolatilityCurve(asOf)
      {
        Interp = new SquareLinearVolatilityInterp()
      };

      // Create a Black volatility curve.
      var n = dates.Length;
      double v = 0, t0 = 0;
      for (int i = 0; i < n; ++i)
      {
        var time = (dates[i] - asOf)/365.0;
        var sig = piecewiseFlatSigmas[i];
        v += sig*sig*(time - t0);
        blackCurve.Add(dates[i], Math.Sqrt(v/time));
        t0 = time;
      }
      return blackCurve;
    }

    private static double[] CreatePoints(Dt asOf, Dt[] dates)
    {
      var n = dates.Length;
      var sigmas = new double[n];
      double c = 0.1, a = 0.4;
      for (int i = 0; i < n; ++i)
      {
        sigmas[i] = c + a*(dates[i] - asOf)/365.0;
      }
      return sigmas;
    }

    enum PointSpec { Increasing, Decreasing, Random }

    private Dt _asOf = new Dt(25, 10, 2016);

    private Dt[] _dates =
    {
      // _D("26-Oct-2016"),
      _D("01-Nov-2016"),
      _D("08-Nov-2016"),
      _D("15-Nov-2016"),
      _D("25-Nov-2016"),
      _D("25-Dec-2016"),
      _D("25-Jan-2017"),
      _D("25-Feb-2017"),
      _D("25-Mar-2017"),
      _D("25-Apr-2017"),
      _D("25-May-2017"),
      _D("25-Jun-2017"),
      _D("25-Jul-2017"),
      _D("25-Aug-2017"),
      _D("25-Sep-2017"),
      _D("25-Oct-2017"),
      _D("25-Jan-2018"),
      _D("25-Apr-2018"),
    };

    private double[] _values =
    {
      // 0.0064069086315924157,
      0.0064069086315924157,
      0.0064069086315924157,
      0.0064069086315924157,
      0.0064069086315924157,
      0.0064492704560868150,
      0.0065632023756143614,
      0.0065139482815953749,
      0.0064866837391425545,
      0.0064662082955156226,
      0.0064063958935621206,
      0.0063597187283302289,
      0.0063244138096296837,
      0.0062834533072725200,
      0.0062498750370428282,
      0.0062226787490277987,
      0.0061476202835188155,
      0.0060981260227814388,
    };

    private static Dt _D(string date)
    {
      return date.ParseDt();
    }
  }
}

