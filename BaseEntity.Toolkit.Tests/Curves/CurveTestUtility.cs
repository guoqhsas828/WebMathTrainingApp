using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Curves
{
  internal static class CurveTestUtility
  {

    public static void AssertTenorConsistency(this DataTable table,
      double tolerance, Func<string, bool> exclude)
    {
      double tol = tolerance;
      var rows = table.Rows;
      var rowCount = rows.Count;
      var pricers = table.Columns["Pricer"];
      var tenors = table.Columns["Curve Tenor"];
      var hedges = table.Columns["Hedge Notional"];
      for (int i = 0; i < rowCount; ++i)
      {
        var row = rows[i];
        var tenor = (string)row[tenors];
        if (exclude != null && exclude(tenor)) continue;
        var pricer = (string)row[pricers];
        var hedge = (double)row[hedges];
        if (tenor != pricer)
        {
          // Hedge notional should be zero.
          if (Math.Abs(hedge) > tol && !Regex.IsMatch(tenor, @"_1\s*D(?:days?)?$"))
            Assert.AreEqual(0, hedge, tol, "\n  Tenor : " + tenor + "\n  Pricer: " + pricer);
        }
        else
        {
          // Hedge notional should be one.
          if (Math.Abs(hedge - 1) > tol)
            Assert.AreEqual(1, hedge, tol, "\n  Tenor : " + tenor + "\n  Pricer: " + pricer);
        }
      }
      Assert.AreEqual(rowCount, rowCount, "Total Rows Asserted");
    }

    public static IPricer[] GetTenorPricers(
      this IEnumerable<CalibratedCurve> curves,
      Func<CurveTenor, bool> predicator)
    {
      return curves.Where(c => c.Calibrator != null)
        .ToDependencyGraph(c => c.Calibrator.EnumerateParentCurves())
        .ReverseOrdered()
        .ClearCurveShifts()
        .SelectMany(c => c.Tenors.Where(predicator)
          .Select(t => new { Curve = c, Tenor = t }))
        .DistinctBy(o => o.Tenor.Name)
        .OrderBy(o => o.Tenor.CurveDate)
        .Select(o => GetPricer(o.Tenor, o.Curve))
        .ToArray();
    }

    internal static IEnumerable<CalibratedCurve> ClearCurveShifts(
      this IEnumerable<CalibratedCurve> curves)
    {
      foreach (var curve in curves)
      {
        curve.CurveShifts = null;
        yield return curve;
      }
    }

    private static IPricer GetPricer(CurveTenor tenor, CalibratedCurve curve)
    {
      var pricer = curve.Calibrator.GetPricer(
        curve, (IProduct)tenor.Product.Clone());
      pricer.Product.Description = tenor.Name;
      return pricer;
    }

  }

}