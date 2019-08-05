// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestSurvivalCurveMixedSpreadCalibrator
  {
    [TestCase("Both null")]
    [TestCase("Tenor names only")]
    [TestCase("Dates only")]
    [TestCase("Dates only with spread")]
    public void SurvivalProbabilities(string key)
    {
      var data = _inputs[key];
      var asOf = data.AsOf;
      var settle = data.Settle;
      var lastDay = data.Maturity;
      var tenorNames = data.Tenors;
      var tenorDates = data.Dates;
      var scalingFactors = new[] {0.3, 0.4, 0.3};
      var discountCurve = new DiscountCurve(asOf, 0.04);
      var survivalCurves = new[]
      {
        SurvivalCurve.FromHazardRate(asOf, discountCurve, "6M", 0.03, 0.4, true),
        SurvivalCurve.FromHazardRate(asOf, discountCurve, "2Y", 0.05, 0.4, true),
        SurvivalCurve.FromHazardRate(asOf, discountCurve, "7Y", 0.06, 0.4, true)
      };
      var curveOldMix = SurvivalCurve.Mixed(asOf,
        settle, Currency.USD,"None", discountCurve, tenorNames, tenorDates,
        survivalCurves, scalingFactors, data.Spread, 0.4);

      var curveNewMix1 = SurvivalMixedSpreadCalibrator.Mixed(asOf,
        settle, Currency.USD, "None", discountCurve, tenorNames, tenorDates,
        survivalCurves, scalingFactors, data.Spread, 0.4, true);

      var curveNewMix2 = SurvivalMixedSpreadCalibrator.Mixed(asOf,
        settle, Currency.USD, "None", discountCurve, tenorNames, tenorDates,
        survivalCurves, scalingFactors, data.Spread, 0.4, false);

      for (Dt dt = asOf, lastDate = lastDay; dt <= lastDate;)
      {
        dt = Dt.Add(dt, 1);
        var probOldMix = curveOldMix.SurvivalProb(dt);
        var probNewMix1 = curveNewMix1.SurvivalProb(dt);
        var probNewMix2 = curveNewMix2.SurvivalProb(dt);
        NUnit.Framework.Assert.AreEqual(probOldMix, probNewMix1, 5E-16);
        NUnit.Framework.Assert.AreEqual(probOldMix, probNewMix2, 5E-16);
        NUnit.Framework.Assert.AreEqual(probNewMix1, probNewMix2, 5E-16);
      }
    }


    [TestCase("Both null")]
    [TestCase("Tenor names only")]
    public void BumpComponentCurve(string key)
    {
      using (new ConfigItems
      {
        {"SurvivalCalibrator.ToleranceX", 1E-15},
        {"SurvivalCalibrator.ToleranceF", 1E-15},
      }.Update())
      {
        DoBumpComponentCurve(_inputs[key]);
      }
    }

    private static void DoBumpComponentCurve(Data data)
    {
      var asOf = data.AsOf;
      var settle = data.Settle;
      var tenorNames = data.Tenors;
      var tenorDates = data.Dates;
      var scalingFactors = new[] {1.0};
      var discountCurve = new DiscountCurve(asOf, 0.04);

      var baseTenors = new[] {"6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y"};
      var baseQuotes = baseTenors.Select((t, i) => 200.0 + i*10).ToArray();
      var parameters = SurvivalCurveParameters.GetDefaultParameters();


      var survivalCurve = SurvivalCurve.FitCDSQuotes(
        String.Empty, asOf, settle, Currency.None, null, false,
        CDSQuoteType.ParSpread, Double.NaN, parameters, discountCurve,
        baseTenors, null, baseQuotes, new[] {0.4},
        0, null, null, 0, Double.NaN, null, false);
      survivalCurve.Name = "Base";

      var survivalCurves = new[] {survivalCurve};

      var curveMixed = SurvivalMixedSpreadCalibrator.Mixed(asOf,
        settle, Currency.USD, "None", discountCurve,
        tenorNames, tenorDates, survivalCurves,
        scalingFactors, 0.0, 0.4, false);
      curveMixed.Name = "Mixed";


      // Assert base and mixed quotes are the same
      for (var i = 0; i < baseTenors.Length; i++)
      {
        NUnit.Framework.Assert.AreEqual(1.0,
          ((SurvivalMixedSpreadCalibrator) curveMixed.Calibrator).MixedQuotes[i] / baseQuotes[i], 1E-12);
      }

      //Test only one component curve
      var tableExpect = CalculateSensitivity(survivalCurve.Tenors,
        discountCurve, survivalCurve);

      var tableActual = CalculateSensitivity(survivalCurve.Tenors,
        discountCurve, curveMixed);
      AssertTableEqual(tableExpect, tableActual, 5E-9);
    }

    

    [TestCase("Both null")]
    [TestCase("Tenor names only")]
    [TestCase("Dates only")]
    public void TestSpreadSensitivity(string key)
    {
      var data = _inputs[key];
      var asOf = data.AsOf;
      var settle = data.Settle;
      var tenorNames = data.Tenors;
      var tenorDates = data.Dates;

      bool calcGamma = true;
      bool calcHedge = true;
      bool scaleDelta = true;
      string hedgeTenor = "matching";

      var scalingFactors = new[] {0.3, 0.4, 0.3};
      var discountCurve = new DiscountCurve(asOf, 0.04);
      var survivalCurves = new[]
      {
        SurvivalCurve.FromHazardRate(asOf, discountCurve, "10Y", 0.03, 0.4, true),
        SurvivalCurve.FromHazardRate(asOf, discountCurve, "10Y", 0.05, 0.4, true),
        SurvivalCurve.FromHazardRate(asOf, discountCurve, "10Y", 0.06, 0.4, true)
      };

      var curveOldMix = SurvivalCurve.Mixed(asOf,
        settle, Currency.USD, "None", discountCurve, tenorNames,
        tenorDates, survivalCurves, scalingFactors, 0.0, 0.4);

      var curveNewMix = SurvivalMixedSpreadCalibrator.Mixed(asOf,
        settle, Currency.USD, "None", discountCurve, tenorNames,
        tenorDates, survivalCurves, scalingFactors, 0.0, 0.4, true);
     
      var tableOldMix = CalculateSensitivity(curveOldMix.Tenors,
        discountCurve, curveOldMix);

      var tableNewMix1 = CalculateSensitivity(curveOldMix.Tenors,
        discountCurve, curveNewMix);


      AssertTableEqual(tableOldMix, tableNewMix1, 1E-11);
    }



    #region Methods

    private static void AssertTableEqual(DataTable expect, DataTable actual,
      double tolerance)
    {
      if (expect == null || actual == null)
      {
        Assert.IsTrue(ReferenceEquals(expect, actual));
        return;
      }

      int ncol = expect.Columns.Count, nrow = expect.Rows.Count;
      Assert.AreEqual(ncol, actual.Columns.Count, "Columns");
      Assert.AreEqual(nrow, actual.Rows.Count, "Rows");
      for (int r = 0; r < nrow; ++r)
      {
        var rowExpect = expect.Rows[r];
        var rowActual = actual.Rows[r];
        for (int c = 0; c < ncol; ++c)
        {
          var valueExpect = rowExpect[c];
          var valueActual = rowActual[c];
          var mismatch = ObjectStatesChecker.Compare(
            valueExpect, valueActual, tolerance);
          if (mismatch == null) continue;
          throw new AssertionException(String.Format(
            "At[{0},{1}]: expected {2}, but got {3}",
            r, c, mismatch.FirstValue, mismatch.SecondValue));
        }
      }
    }


    private static Dt _GetDt(string s)
    {
      return Dt.FromStr(s);
    }


    private static DataTable CalculateSensitivity(
      IEnumerable<CurveTenor> tenors, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve)
    {
      var pricers = tenors.Select(t =>
        new CDSCashflowPricer((CDS)t.Product, discountCurve.AsOf,
          discountCurve, survivalCurve)).OfType<IPricer>().ToArray();

      bool calcGamma = true;
      bool calcHedge = true;
      bool scaleDelta = true;
      string hedgeTenor = "matching";

      return Sensitivities2.Calculate(
        pricers, null, null, BumpTarget.CreditQuotes, 10.0, 10.0,
        BumpType.ByTenor, BumpFlags.BumpInPlace,
        null, scaleDelta, calcGamma,
        hedgeTenor, calcHedge,
        true, null);
    }



    private static Dictionary<string, Data> _inputs = new Dictionary<string, Data>
    {
      {
        "Both null", new Data
        {
          AsOf = new Dt(20100726),
          Settle = new Dt(20100728),
          Maturity = new Dt(20200720),
          Tenors = null,
          Dates = null,
        }
      },
      {
        "Tenor names only", new Data
        {
          AsOf = new Dt(20100726),
          Settle = new Dt(20100728),
          Maturity = new Dt(20200720),
          Tenors = new[] {"6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y"},
          Dates = null,
        }
      },
      {
        "Dates only", new Data
        {
          AsOf = new Dt(20100726),
          Settle = new Dt(20100728),
          Maturity = new Dt(20200720),
          Tenors = null,
          Dates = new[]
          {
            _GetDt("20-Mar-2011"),
            _GetDt("20-Sep-2011"),
            _GetDt("20-Sep-2012"),
            _GetDt("20-Sep-2013"),
            _GetDt("20-Sep-2014"),
            _GetDt("20-Sep-2015"),
            _GetDt("20-Sep-2017"),
            _GetDt("20-Sep-2020"),
          },
        }
      },
      {
        "Dates only with spread", new Data
        {
          AsOf = new Dt(20100726),
          Settle = new Dt(20100728),
          Maturity = new Dt(20200720),
          Tenors = null,
          Dates = new[]
          {
            _GetDt("20-Mar-2011"),
            _GetDt("20-Sep-2011"),
            _GetDt("20-Sep-2012"),
            _GetDt("20-Sep-2013"),
            _GetDt("20-Sep-2014"),
            _GetDt("20-Sep-2015"),
            _GetDt("20-Sep-2017"),
            _GetDt("20-Sep-2020"),
          },
          Spread = 10,
        }
      },
    };

    #endregion Methods

    #region Helper Class

    private class Data
    {
      public Dt AsOf, Settle, Maturity;
      public string[] Tenors;
      public Dt[] Dates;
      public double Spread;
    }

    #endregion Helper Class
  }
}

