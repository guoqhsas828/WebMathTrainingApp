//
// Copyright (c)    2002-2016. All rights reserved.
//
using System;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Curves
{
  using NUnit.Framework;

  [TestFixture]
  public class TestCorrectiveOverlay
  {
    #region Set up and tear down

    private IDisposable _tempConfig;

    [OneTimeSetUp]
    public void SetUp()
    {
      _tempConfig = new ConfigItems
      {
        {"Simulations.EnableCorrectionForCurveTenorChange", true}
      }.Update();
    }

    [OneTimeTearDown]
    public void DearDown()
    {
      if (_tempConfig == null) return;
      _tempConfig.Dispose();
      _tempConfig = null;
    }

    #endregion

    #region Tests

    [Test]
    public static void OverlayOnOverlay()
    {
      Dt asOf = new Dt(20160620);

      // The original curve dates vs simulation tenor dates
      var originalCurveDates = new[] {new Dt(20180620), new Dt(20200622)};
      var tenorDates = new[] {new Dt(20170620), new Dt(20190620), new Dt(20210620), };
      var testDates = Enumerable.Range(1, 12)
        .Select(i => Dt.AddMonth(asOf, i*6, false)).ToArray();

      // Create a discount curve
      var discounts = new[] {1/(1 + 2*0.01), 1/(1 + 4*0.02)};
      var projects = new[] {1/(1 + 2*0.02), 1/(1 + 4*0.04)};
      var discountCurve = new DiscountCurve(asOf)
      {
        Ccy = Currency.USD,
        Name = "Discount",
      };
      discountCurve.Add(originalCurveDates, discounts);

      // Create a projection curve as overlay
      var projectCurve = new DiscountCurve(new ProjectionCurveFitCalibrator(
        asOf, discountCurve, StandardReferenceIndices.Create("USDLIBOR_3M"),
        null, new CalibratorSettings()), discountCurve)
      {
        Ccy = Currency.USD,
        Name = "Project"
      };
      projectCurve.Add(originalCurveDates[0], projects[0]/discounts[0]);
      projectCurve.Add(originalCurveDates[1], projects[1]/discounts[1]);

      // Calculate the factors on difference tenors
      var values = Array.ConvertAll(tenorDates, discountCurve.Interpolate);
      var factors = Array.ConvertAll(tenorDates,
        d => projectCurve.Interpolate(d) / discountCurve.Interpolate(d));
      var testFactors = Array.ConvertAll(testDates,
        d => projectCurve.Interpolate(d) / discountCurve.Interpolate(d));

      // Call market environment Conform()
      new MarketEnvironment(asOf, tenorDates, new[] {discountCurve},
        null, null, null, null).Conform();

      // Check consistency
      for (int i = 0, n = originalCurveDates.Length; i < n; ++i)
      {
        Assert.AreEqual(discounts[i],
          discountCurve.Interpolate(originalCurveDates[i]), 1E-15, "Discount");
        Assert.AreEqual(projects[i],
          projectCurve.Interpolate(originalCurveDates[i]), 1E-15, "Project");
      }
      for (int i = 0, n = tenorDates.Length; i < n; ++i)
      {
        Assert.AreEqual(values[i],
          discountCurve.Interpolate(tenorDates[i]), 1E-15, "Discount");
        Assert.AreEqual(values[i]*factors[i],
          projectCurve.Interpolate(tenorDates[i]), 1E-15, "Project");
      }

      // Fake some simulation
      var baseCurve = GetBaseCurve(discountCurve);
      for (int i = 0, n = baseCurve.Count; i < n; ++i)
      {
        foreach (var perturb in new[] {0.5, 0.8, 1.2, 2.0})
        {
          // Perturb the curve point on the discount curve
          var df = values[i]*perturb;
          baseCurve.SetVal(i, df);
          Assert.AreEqual(df, discountCurve.Interpolate(tenorDates[i]), 1E-15);

          // Check expected change on the projection curve
          var expect = df*factors[i];
          var actual = projectCurve.Interpolate(tenorDates[i]);
          Assert.AreEqual(expect, actual, 1E-15);

          // Interpolate on the dates other than tenor dates
          for (int t = 0; t < testDates.Length; ++t)
          {
            var d = testDates[t];
            expect = discountCurve.Interpolate(d)*testFactors[t];
            actual = projectCurve.Interpolate(d);
            Assert.AreEqual(expect, actual, 1E-15);
          }

          // Restore the original point
          baseCurve.SetVal(i, values[i]);
        }
      }

    }

    [Test]
    public static void ZeroValuePoints()
    {
      Dt asOf = new Dt(20160620);

      // The original curve dates vs simulation tenor dates
      var originalCurveDates = new[] { new Dt(20180620), new Dt(20200622) };
      var tenorDates = new[] { new Dt(20170620), new Dt(20190620), new Dt(20210620), };
      var testDates = Enumerable.Range(1, 12)
        .Select(i => Dt.AddMonth(asOf, i * 6, false)).ToArray();

      // Create a discount curve
      var hazardRates = new[] { 1.0, 200 };
      var survivalCurve = new SurvivalCurve(asOf)
      {
        Ccy = Currency.USD,
        Name = "Survival",
      };
      for (int i = 0; i < originalCurveDates.Length; i++)
      {
        survivalCurve.Add(originalCurveDates[i], 1.0);
        survivalCurve.SetRate(i, hazardRates[i]);
      }

      // The values before conform
      var expects = Array.ConvertAll(testDates, survivalCurve.Interpolate);

      // Call market environment Conform()
      new MarketEnvironment(asOf, tenorDates, null,
        null, new[] {survivalCurve}, null, null).Conform();

      // The values after conform
      var actuals = Array.ConvertAll(testDates, survivalCurve.Interpolate);

      Assert.That(actuals, Is.EqualTo(expects).Within(1E-14));
      return;

    }


    private static Curve GetBaseCurve(Curve curve)
    {
      var overlay = curve.CustomInterpolator as CorrectiveOverlay;
      Debug.Assert(overlay != null);
      return (Curve) overlay.BaseCurve;
    }

    #endregion
  }
}
