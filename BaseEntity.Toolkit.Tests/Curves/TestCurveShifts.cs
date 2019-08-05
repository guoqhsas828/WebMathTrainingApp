//
// Copyright (c)    2002-2016. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestCurveShifts : ToolkitTestBase
  {
    private const int N = 1;
    const string File1 = @"toolkit\test\data\USDLIBOR_3M_vs_6M_SwapPricer.xml";
    const string File2 = @"toolkit\test\data\FB46535-3Pricers.xml";

    private IPricer LoadPricer()
    {
      string filename = File2;
      if (filename == File1)
      {
        return (IPricer) XmlSerialization.ReadXmlFile(
          File1.GetFullPath());
      }

      return ((object[])XmlSerialization.ReadXmlFile(File2.GetFullPath()))
        .Select(a => ((IPricer[])((object[])a)[0])[0]).First();
    }


    #region Helpers
    // Test consistency between two consecutive calls
    private void TestRateSensitivities(
      IPricer pricer,
      BumpTarget quoteType, BumpType bumpType, string[] bumpTenors,
      string hedgeTenor, bool calcHedge)
    {
      const bool cache = true;
      DataTable table1 = null, table2 = null;

      double timing1 = 0, timing2 = 0;
      for (int i = 0; i < N; ++i)
      {
        double t1, t2;

        // The first call
        var pricers = new[] {pricer};
        using (new CheckStates(true, pricers))
        {
          var timer = new Timer();
          timer.Start();
          table1 = Sensitivities2.Calculate(pricers, null, null, quoteType,
            4, 4, bumpType, false, bumpTenors, true, true, hedgeTenor, calcHedge,
            cache, null);
          timer.Stop();
          t1 = timer.Elapsed;
        }

        // The second call
        using (new CheckStates(true, pricers))
        {
          var timer = new Timer();
          timer.Start();
          table2 = Sensitivities2.Calculate(pricers, null, null, quoteType,
            4, 4, bumpType, false, bumpTenors, true, true, hedgeTenor, calcHedge,
            cache, null);
          timer.Stop();
          t2 = timer.Elapsed;
        }
        timing1 += t1;
        timing2 += t2;
      }

      AssertAreResultsEqual(table1, timing1 / N, table2, timing2 / N);
    }

    // Test consistency between the calls with/without cache
    private void TestRateSensitivityCache(
      BumpTarget quoteType, BumpType bumpType, string[] bumpTenors,
      string hedgeTenor, bool calcHedge, bool timing = false)
    {
      DataTable table1 = null, table2 = null;

      double timing1 = 0, timing2 = 0;
      for (int i = 0; i < N; ++i)
      {
        double t1, t2;

        // The first call, without cache
        var pricers = new[] { LoadPricer() };
        using (new CheckStates(true, pricers))
        {
          var timer = new Timer();
          timer.Start();
          table1 = Sensitivities2.Calculate(pricers, null, null, quoteType,
            4, 4, bumpType, false, bumpTenors, true, true, hedgeTenor, calcHedge,
            false, null);
          timer.Stop();
          t1 = timer.Elapsed;
        }

        // The second call
        pricers[0] = LoadPricer();
        using (new CheckStates(true, pricers))
        {
          var timer = new Timer();
          timer.Start();
          table2 = Sensitivities2.Calculate(pricers, null, null, quoteType,
            4, 4, bumpType, false, bumpTenors, true, true, hedgeTenor, calcHedge,
            true, null);
          timer.Stop();
          t2 = timer.Elapsed;
        }
        if (timing) Assert.Less(t2, t1, "Timing");
        timing1 += t1;
        timing2 += t2;
      }

      AssertAreResultsEqual(table1, timing1 / N, table2, timing2 / N);
    }
    #endregion

    #region Test Methods

   


    [Test]
    public void CurveShiftConsistency()
    {

      var pricers =  ((object[])XmlSerialization.ReadXmlFile(File2.GetFullPath()))
        .Select(a => ((IPricer[])((object[])a)[0])[0]).Distinct().ToArray();

      var curves = pricers
        .SelectMany(p => new PricerEvaluator(p).RateCurves)
        .Where(c => c.Calibrator != null)
        .ToDependencyGraph(c => c.Calibrator.EnumerateParentCurves())
        .ReverseOrdered()
        .ClearCurveShifts()
        .ToArray();

      var curves0 = new PricerEvaluator(pricers[0]).RateCurves
        .Where(c => c.Calibrator != null)
        .ToDependencyGraph(c => c.Calibrator.EnumerateParentCurves())
        .ReverseOrdered().ToArray();
      var curves1 = new PricerEvaluator(pricers[1]).RateCurves
        .Where(c => c.Calibrator != null)
        .ToDependencyGraph(c => c.Calibrator.EnumerateParentCurves())
        .ReverseOrdered().ToArray();

      var bummpTenors = new[] {"USDLIBOR.3 Months.Swap_2Y"};

      var table1a = Sensitivities2.Calculate(new []{pricers[1]},
        null, null, BumpTarget.InterestRates, 1, 0, BumpType.ByTenor,
        false, bummpTenors, true, false, "matching", true, true, null);
      var shifts1a = curves.Select(c=>c.CurveShifts).ToArray();
      curves.ClearCurveShifts().Count();

      var table0 = Sensitivities2.Calculate(new[]{pricers[0]},
        null, null, BumpTarget.InterestRates, 1, 0, BumpType.ByTenor,
        false, bummpTenors, true, false, "matching", true, true, null);
      var shifts0 = curves.Select(c => c.CurveShifts).ToArray().CloneObjectGraph();

      var table1b = Sensitivities2.Calculate(new[] { pricers[1] },
        null, null, BumpTarget.InterestRates, 1, 0, BumpType.ByTenor,
        false, bummpTenors, true, false, "matching", true, true, null);
      var shifts1b = curves.Select(c => c.CurveShifts).ToArray();

      var diffs = shifts1a.Select((s, i) => s == null ? null :
        ObjectStatesChecker.Compare(s, shifts1b[i])).ToArray();
      AssertAreResultsEqual(table1a, 0, table1b, 0);
      return;
    }

    /// <exclude/>
    [Test]
    public void ByTenorConsistency()
    {
      var bumpTarget = BumpTarget.InterestRates | BumpTarget.InterestRateBasis;
      var bumpFlags = BumpFlags.None;
      string[] bumpTenors = null;// new[] { "USDLIBOR_3M.Swap_2 Yr" };// new[] { "USDLIBOR_3M.Swap_2 Yr" };
      DataTable table1 = null, table2 = null;
      var pricers = new[] {LoadPricer()}
        .SelectMany(p => new PricerEvaluator(p).RateCurves)
        .GetTenorPricers(t => t.IsRateTenor());

      double t1, t2;

      // The first call
      using (new CheckStates(true, pricers))
      {
        var timer = new Timer();
        timer.Start();
        table1 = Sensitivities2.Calculate(pricers, null, null, bumpTarget,
          4, 4, BumpType.ByTenor, bumpFlags, bumpTenors,
          true, true, "matching", true, true, null);
        timer.Stop();
        t1 = timer.Elapsed;
        table1.AssertTenorConsistency(1E-4, s => s.EndsWith("2 W"));
      }

      // The second call
      using (new CheckStates(true, pricers))
      {
        var timer = new Timer();
        timer.Start();
        table2 = Sensitivities2.Calculate(pricers, null, null, bumpTarget,
          4, 4, BumpType.ByTenor, bumpFlags, bumpTenors,
          true, true, "matching", true, true, null);
        timer.Stop();
        t2 = timer.Elapsed;
        table2.AssertTenorConsistency(1E-4, s => s.EndsWith("2 W"));
      }

      Assert.Less(t2, t1, "Timing");
      //AssertAreResultsEqual(table1, t1, table2, t2);
    }

    /// <exclude/>
    [Test]
    public void ByTenorRateMatching()
    {
      TestRateSensitivities(LoadPricer(),
        BumpTarget.InterestRates | BumpTarget.InterestRateBasis,
        BumpType.ByTenor, null, "matching", true);
    }

    /// <exclude/>
    [Test]
    public void ByTenorRateMatchingCache()
    {
      TestRateSensitivityCache(
        BumpTarget.InterestRates | BumpTarget.InterestRateBasis,
        BumpType.ByTenor, null, "matching", true);
    }

    /// <exclude/>
    [Test, Category("Timing"), Explicit]
    public void ByTenorRateMatchingCacheTiming()
    {
      TestRateSensitivityCache(
        BumpTarget.InterestRates | BumpTarget.InterestRateBasis,
        BumpType.ByTenor, null, "matching", true, true);
    }

    /// <exclude/>
    [Test]
    public void ByTenorRateNone()
    {
      TestRateSensitivities(LoadPricer(),
        BumpTarget.InterestRates | BumpTarget.InterestRateBasis,
        BumpType.ByTenor, null, "matching", false);
    }

    /// <exclude/>
    [Test]
    public void ByTenorRateNoneCache()
    {
      TestRateSensitivityCache(
        BumpTarget.InterestRates | BumpTarget.InterestRateBasis,
        BumpType.ByTenor, null, "matching", false);
    }

    /// <exclude/>
    [Test, Category("Timing"), Explicit]
    public void ByTenorRateNoneCacheTiming()
    {
      TestRateSensitivityCache(
        BumpTarget.InterestRates | BumpTarget.InterestRateBasis,
        BumpType.ByTenor, null, "matching", false, true);
    }

    /// <exclude/>
    [Test]
    public void ParallelRateSwap5Y()
    {
      TestRateSensitivities(LoadPricer(), BumpTarget.InterestRates,
        BumpType.Parallel, null, "USDLIBOR_3M.Swap_5 Yr", true);
    }

    /// <exclude/>
    [Test]
    public void ParallelRateSwap5YCache()
    {
      TestRateSensitivityCache(BumpTarget.InterestRates,
        BumpType.Parallel, null, "USDLIBOR_3M.Swap_5 Yr", true);
    }

    /// <exclude/>
    [Test, Category("Timing"), Explicit]
    public void ParallelRateSwap5YCacheTiming()
    {
      TestRateSensitivityCache(BumpTarget.InterestRates,
        BumpType.Parallel, null, "USDLIBOR_3M.Swap_5 Yr", true, true);
    }

    /// <exclude/>
    [Test]
    public void ParallelRateNone()
    {
      TestRateSensitivities(LoadPricer(), BumpTarget.InterestRates,
        BumpType.Parallel, null, "USDLIBOR_3M.Swap_5 Yr", false);
    }

    /// <exclude/>
    [Test]
    public void ParallelRateNoneCache()
    {
      TestRateSensitivityCache(BumpTarget.InterestRates,
        BumpType.Parallel, null, "USDLIBOR_3M.Swap_5 Yr", false);
    }

    /// <exclude/>
    [Test, Category("Timing"), Explicit]
    public void ParallelRateNoneCacheTiming()
    {
      TestRateSensitivityCache(BumpTarget.InterestRates,
        BumpType.Parallel, null, "USDLIBOR_3M.Swap_5 Yr", false, true);
    }

    /// <exclude/>
    [Test]
    public void Uniform()
    {
      var pricers = new[] {LoadPricer()};

      string[] bumpTenors = null;// new[] { "FEDFUNDS_1D.USDLIBOR_3M.BasisSwap_2Yr" };
      double timing1, timing2;
      DataTable table1, table2;
      var selector = CurveTenorSelectors.UniformRate;

      // The first call
      using (new CheckStates(true, pricers))
      {
        var timer = new Timer();
        timer.Start();
        table1 = Sensitivities2.Calculate(pricers, null, null, BumpTarget.InterestRates,
          4, 4, BumpType.Uniform, false, bumpTenors, true, true, "", false,
          false, null);
        timer.Stop();
        timing1 = timer.Elapsed;
      }

      // The second call
      using (new CheckStates(true, pricers))
      {
        var timer = new Timer();
        timer.Start();
        table2 = Sensitivities2.Calculate(pricers, null, null, BumpTarget.InterestRates,
          4, 4, BumpType.Uniform, false, bumpTenors, true, true, "", false,
          false, null);
        timer.Stop();
        timing2 = timer.Elapsed;
      }

      Assert.Less(timing2, timing1, "Timing");
      AssertAreResultsEqual(table1,timing1,table2,timing2);
    }

    #endregion

    #region Result table comparison helpers
    private void AssertAreResultsEqual(DataTable expects, double expectTiming,
      DataTable actuals, double actualTiming)
    {
      var rd = MakeResultData(expects, "Curve Tenor",
        new[]
        {
          "Delta", "Gamma",
          "Hedge Delta", "Hedge Notional"
        }, expectTiming);
      rd.ToExpects();
      FillActuals(rd, actuals, actualTiming);
      MatchExpects(rd);
    }

    private static void FillActuals(ResultData rd,
      DataTable table, double timeUsed)
    {
      int cols = rd.Results.Length;
      for (int j = 0; j < cols; ++j)
      {
        var rs = rd.Results[j];
        int rows = rs.Expects.Length;
        if (rows > table.Rows.Count) rows = table.Rows.Count;
        var name = rs.Name;
        DataColumn column = table.Columns[name];
        if (column == null)
        {
          rs.Actuals = EmptyArray<double>.Instance;
          continue;
        }
        if (column.DataType == typeof(string))
        {
          var labels = rs.Labels;
          for (int i = 0; i < rows; ++i)
          {
            labels[i] += '\t';
            labels[i] += (string)table.Rows[i][column];
          }
        }
        else
        {
          rs.Actuals = GetColumnValues<double>(table, column);
        }
      }
      rd.TimeUsed = timeUsed;
    }

    private static ResultData MakeResultData(
      DataTable table, string labelColName, string[] dataColNames, double timeUsed)
    {
      var rd = new ResultData();
      rd.Accuracy = 0;
      int cols = dataColNames.Length;
      int rows = table.Rows.Count;
      string[] leftLabels = GetColumnValues<string>(
        table, table.Columns[labelColName]);
      if (leftLabels.Length == 0)
        leftLabels = Enumerable.Range(0, rows).Select(i => "Row " + i).ToArray();
      var list = new List<ResultData.ResultSet>();
      for (int j = 0; j < cols; ++j)
      {
        DataColumn column = table.Columns[dataColNames[j]];
        if(column==null) continue;
        string[] labels = leftLabels;
        double[] values = null;
        if (column.DataType == typeof(string))
        {
          labels = new string[rows];
          for (int i = 0; i < rows; ++i)
            labels[i] = leftLabels[i] + '\t' + (string) table.Rows[i][column];
        }
        else
        {
          values = GetColumnValues<double>(table, column);
        }
        var result = new ResultData.ResultSet();
        result.Name = column.ColumnName;
        result.Actuals = values;
        result.Labels = labels;
        list.Add(result);
      }
      rd.Results = list.ToArray();
      rd.TimeUsed = timeUsed;
      return rd;
    }

    private static T[] GetColumnValues<T>(DataTable table, DataColumn column)
    {
      bool hasValue = false;
      int rows = table.Rows.Count;
      var values = new T[rows];
      for (int i = 0; i < rows; ++i)
      {
        var d = table.Rows[i][column];
        if (d == null || d is DBNull) continue;
        hasValue = true;
        values[i] = (T)d;
      }
      return hasValue ? values : EmptyArray<T>.Instance;
    }
    #endregion
  }
}
