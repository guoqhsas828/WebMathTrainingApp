using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.IO;
using System.Xml.Serialization;
using NUnit.Framework;
using BaseEntity.Configuration;

namespace BaseEntity.Toolkit.Tests.Helpers.Legacy
{
  /// <summary>
  ///   A special class to hold XML serilizable result data
  /// </summary>
  [Serializable]
  public class ResultData
  {
    #region Constructors
    /// <summary>
    ///   Need a default constructor
    /// </summary>
    public ResultData()
    {
    }

    /// <summary>
    ///   Construct result data from a single number
    /// </summary>
    /// <param name="value">result number</param>
    /// <param name="label">label for the value</param>
    /// <param name="timing">time used</param>
    public ResultData(double value, string label, double timing)
    {
      TimeUsed = timing;
      Accuracy = 0;
      Results = new ResultSet[1];
      Results[0] = new ResultSet();
      Results[0].Actuals = new double[] { value };
      if (label != null)
        Results[0].Labels = new string[] { label };
    }

    /// <summary>
    ///   Construct result data from an array
    /// </summary>
    /// <param name="values">result numbers</param>
    /// <param name="labels">labels of for each value</param>
    /// <param name="timing">time used</param>
    public ResultData(double[] values, string[] labels, double timing)
    {
      TimeUsed = timing;
      Accuracy = 0;
      Results = new ResultSet[1];
      Results[0] = new ResultSet();
      Results[0].Actuals = values;
      Results[0].Labels = labels;
    }

    /// <summary>
    ///   Construct result data from an array
    /// </summary>
    /// <param name="values">result numbers</param>
    /// <param name="labels">labels of for each value</param>
    /// <param name="passed">indicators of success or failures</param>
    public ResultData(double[] values, string[] labels, bool[] passed)
    {
      TimeUsed = 0;
      Accuracy = 0;
      Results = new ResultSet[1];
      Results[0] = new ResultSet();
      Results[0].Actuals = values;
      Results[0].Labels = labels;
      Results[0].Passed = passed;
    }

    /// <summary>
    ///   Construct result data from a data table
    /// </summary>
    /// <param name="table">Data table</param>
    /// <param name="timing">Time used</param>
    public ResultData(DataTable table, double timing)
    {
      this.TimeUsed = timing;
      this.Accuracy = 0;

      int cols = table.Columns.Count;
      int rows = table.Rows.Count;
      List<ResultSet> list = new List<ResultSet>();
      double[] values = null;
      string[] labels = null;
      for (int j = 0; j < cols; ++j)
      {
        DataColumn column = table.Columns[j];
        if (column.DataType == typeof(string))
        {
          labels = new string[rows];
          for (int i = 0; i < rows; ++i)
            labels[i] = (string)table.Rows[i][column];
        }
        else
        {
          values = new double[rows];
          for (int i = 0; i < rows; ++i)
            values[i] = (double)table.Rows[i][column];

          ResultSet result = new ResultSet();
          result.Name = column.ColumnName;
          result.Actuals = values;
          result.Labels = labels;

          list.Add(result);
        }
      }
      ResultSet[] rs = list.ToArray();
      this.Results = rs;
    }
    #endregion // Constructors

    #region Load expects

    /// <summary>
    /// Loads the expects for the current running case in the current running fixture.
    /// </summary>
    /// <param name="caseName">Name of the test case.</param>
    /// <param name="generatingExpects">Set to <c>true</c> if it is generating expects.</param>
    /// <param name="list">The list of test result data</param>
    /// <returns>A ResultData object holding all the expects;
    /// or an empty ResultData object when no expects data exists and
    /// the current test case requests generate expects.</returns>
    internal static ResultData LoadExpects(string caseName,
      bool generatingExpects, IList<ResultData> list)
    {
      ResultData rd = null;
      foreach (ResultData r in list)
      {
        if (String.CompareOrdinal(caseName, r.TestName) == 0)
        {
          rd = r;
          break;
        }
      }

      if (rd != null)
      {
        rd._generatingExpects = generatingExpects;
        return rd;
      }

      if (!generatingExpects)
        throw new ApplicationException($"Expects data for test '{caseName}' not found");

      rd = new ResultData();
      rd._generatingExpects = generatingExpects;
      rd.TestName = caseName;
      rd.Accuracy = 1.0E-9;
      rd.Results = new[] {new ResultData.ResultSet()};
      list.Add(rd);
      return rd;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Matches the expects and indictates failure if there is any.
    /// </summary>
    public void MatchExpects()
    {
      MatchExpects(this);
    }

    private static void MatchExpects(ResultData rd)
    {
      // Shall we mark this as error?
      if (rd == null) return;

      // Are we generating expects?
      if (rd._generatingExpects)
      {
        rd.ToExpects();
        return;
      }

      // Check any error.
      var msg = rd.MatchExpectsReportError();
      if (msg == null) return;
      Assert.Fail("Match expects failed.{0}{1}",
        Environment.NewLine, msg);
    }


    internal string MatchExpectsReportError()
    {
      double accuracy = Accuracy;
      if (accuracy < 0) return null;
      if (accuracy.Equals(0.0)) accuracy = 1E-9;
      if(Results==null || Results.Length==0) return null;
      for(int j = 0; j < this.Results.Length; ++j)
      {
        var rs = Results[j];
        double[] expects = rs.Expects;
        double[] results = rs.Actuals;
        if (expects == null)
        {
          return "expects array is null";
        }
        int count = expects.Length;
        for (int i = 0; i < count; ++i)
        {
          if (double.IsPositiveInfinity(expects[i]) && double.IsPositiveInfinity(results[i]) ||
              double.IsNegativeInfinity(expects[i]) && double.IsNegativeInfinity(results[i]))
          {
            continue;
          }
            
          double tol = accuracy * (1 + Math.Abs(expects[i]));
          double abserr = Math.Abs(results[i] - expects[i]);
          if ((Double.IsNaN(abserr) && !Double.IsNaN(expects[i])) || (abserr > tol))
            return String.Format(
              "[Table {0}][Row {1}] Expect {2}, Got {3}, AbsErr {4}",
              String.IsNullOrEmpty(rs.Name)?j.ToString() : rs.Name,
              rs.Labels == null || String.IsNullOrEmpty(rs.Labels[i])
                ? i.ToString() : rs.Labels[i],
              expects[i], results[i], abserr);
        }
      }
      return null;
    }

    /// <summary>
    ///   Make actual values to be the expects
    /// </summary>
    public void ToExpects()
    {
      //- This need revisit: we change the expect timing
      //- only when the expect is uninitialized (equals to 0)
      //- or, when the expect timing exists, it has to be
      //- larger than the actual timing.
      //- This is a way to make sure thatthe expect timing never
      //- downgraded when updating the existing expects due to
      //  algorithm changes.
      if (TimeExpect <= 0 || (TimeExpect > TimeUsed && TimeUsed > 0))
        TimeExpect = TimeUsed;

      //- Generate expects: set all the extects to be actuals
      foreach (ResultSet rs in Results)
        if (rs.Actuals != null) rs.Expects = rs.Actuals;

      //- Clear the actuals so the object can write to disk
      //- as expects only data set.
      TimeUsed = 0.0;
      foreach (ResultSet rs in Results)
        rs.Actuals = null;

      return;
    }

    /// <summary>
    ///   Create a data table representing a result set
    /// </summary>
    /// <param name="timeUsed">Time used</param>
    /// <param name="timeExpect">Time expected</param>
    /// <param name="accuracy">Accuracy</param>
    /// <param name="data">Result set</param>
    /// <returns></returns>
    public static DataTable MakeTable(
      double timeUsed, double timeExpect, double accuracy,
      ResultSet data)
    {
      DataTable table = new DataTable(data.Name);

      // add columns
      AddColumn(table, typeof(int), "No");
      AddColumn(table, typeof(string), "Name");
      AddColumn(table, null, "Result");
      AddColumn(table, null, "Expect");
      AddColumn(table, typeof(double), "Difference (absolute)");
      AddColumn(table, typeof(double), "Difference (relative)");
      AddColumn(table, typeof(bool), "Passed");

      // add rows
      DataRow row;
      int baseIdx = 0;
      if (timeUsed > 0 && timeExpect > 0)
      {
        row = table.NewRow();
        row["No"] = baseIdx + 1;
        row["Name"] = "Time Used";
        row["Result"] = timeUsed;
        row["Expect"] = timeExpect;
        row["Difference (absolute)"] = timeUsed - timeExpect;
        row["Difference (relative)"] =
          (timeUsed - timeExpect) / (1 + timeExpect);
        row["Passed"] = !IsTimingFailed(timeUsed, timeExpect);
        table.Rows.Add(row);
        baseIdx = 1;
      }

      row = table.NewRow(); row["Name"] = "Average";
      row["No"] = baseIdx + 1;
      table.Rows.Add(row);
      row = table.NewRow(); row["Name"] = "Maximum";
      row["No"] = baseIdx + 2;
      table.Rows.Add(row);
      row = table.NewRow(); row["Name"] = "Minimum";
      row["No"] = baseIdx + 3;
      table.Rows.Add(row);


      double avgResult = 0, avgExpect = 0,
        avgDiffAbs = 0, avgDiffRlt = 0,
        maxDiffAbs = Double.NegativeInfinity,
        maxDiffRlt = Double.NegativeInfinity,
        maxResult = Double.NegativeInfinity,
        maxExpect = Double.NegativeInfinity,
        minDiffAbs = Double.PositiveInfinity,
        minDiffRlt = Double.PositiveInfinity,
        minResult = Double.PositiveInfinity,
        minExpect = Double.PositiveInfinity;
      for (int i = 0; i < data.Actuals.Length; ++i)
      {
        string label = (data.Labels == null || data.Labels.Length <= i
          || data.Labels[i] == null) ? "" : data.Labels[i];

        row = table.NewRow();
        row["No"] = baseIdx + 4 + i;

        double actual = data.Actuals[i];
        if (!Double.IsNaN(actual))
        {
          row["Result"] = actual;
          avgResult += (actual - avgResult) / (i + 1);
          maxResult = Math.Max(maxResult, actual);
          minResult = Math.Min(minResult, actual);
        }
        else if (label.Contains("\t"))
        {
          // Compare strings.  No numerical calculations.
          string[] items = label.Split('\t');
          label = items[0];
          if (items.Length > 1) row["Expect"] = items[1];
          if (items.Length > 2) row["Result"] = items[2];
          if (data.Passed == null || data.Passed.Length <= i)
            row["Passed"] = items.Length <= 2 || items[1].Equals(items[2]);
          else
            row["Passed"] = data.Passed[i];
          row["Name"] = label.Length > 0 ? label : ("Item " + (i + 1));
          table.Rows.Add(row);
          continue;
        }
        else
          row["Result"] = actual;

        double expect = data.Expects[i];
        row["Expect"] = expect;
        if (!Double.IsNaN(expect))
        {
          avgExpect += (expect - avgExpect) / (i + 1);
          maxExpect = Math.Max(maxExpect, expect);
          minExpect = Math.Min(minExpect, expect);
        }

        double diff;
        row["Difference (absolute)"] = diff
          = Math.Abs(actual - expect);
        if (!Double.IsNaN(diff))
        {
          avgDiffAbs += (diff - avgDiffAbs) / (i + 1);
          maxDiffAbs = Math.Max(maxDiffAbs, diff);
          minDiffAbs = Math.Min(minDiffAbs, diff);
        }
        row["Difference (relative)"] = diff
          = diff / (1 + Math.Abs(expect));
        if (!Double.IsNaN(diff))
        {
          avgDiffRlt += (diff - avgDiffRlt) / (i + 1);
          maxDiffRlt = Math.Max(maxDiffRlt, diff);
          minDiffRlt = Math.Min(minDiffRlt, diff);
        }
        if (data.Passed == null || data.Passed.Length <= i)
        {
          double abserr = Math.Abs(diff);
          row["Passed"] = !((Double.IsNaN(abserr)
            && !Double.IsNaN(expect)) || abserr > accuracy);
        }
        else
          row["Passed"] = data.Passed[i];

        row["Name"] = label.Length > 0 ? label : ("Item " + (i + 1));
        table.Rows.Add(row);
      }

      // average difference
      row = table.Rows[baseIdx];
      row["Result"] = avgResult;
      row["Expect"] = avgExpect;
      row["Difference (absolute)"] = avgDiffAbs;
      row["Difference (relative)"] = avgDiffRlt;

      // maximum difference
      row = table.Rows[baseIdx + 1];
      row["Result"] = maxResult;
      row["Expect"] = maxExpect;
      row["Difference (absolute)"] = maxDiffAbs;
      row["Difference (relative)"] = maxDiffRlt;

      // minimum difference
      row = table.Rows[baseIdx + 2];
      row["Result"] = minResult;
      row["Expect"] = minExpect;
      row["Difference (absolute)"] = minDiffAbs;
      row["Difference (relative)"] = minDiffRlt;

      return table;
    }

    /// <summary>
    ///   Helper: add a column to table
    /// </summary>
    /// <param name="table">Data table</param>
    /// <param name="type">column data type</param>
    /// <param name="title">column title</param>
    private static void AddColumn(
      DataTable table, Type type, string title)
    {
      DataColumn col = new DataColumn();
      if (type != null)
        col.DataType = type;
      col.ColumnName = title;
      col.AutoIncrement = false;
      col.Caption = title;
      //col.ReadOnly = true;
      col.Unique = false;
      table.Columns.Add(col);
    }
    #endregion // Methods

    #region Test Behaviors
    /// <summary>
    ///   Test if a test is using too much time
    /// </summary>
    /// <param name="timeUsed">Actual time used</param>
    /// <param name="timeExpect">Expected timing</param>
    /// <returns>True if the test is too slow</returns>
    public static bool IsTimingFailed(
      double timeUsed, double timeExpect)
    {
      if (timeUsed > 0 && timeExpect > 0)
        return (timeUsed - timeExpect) / (1 + timeExpect) > TimingTolerance;
      return false;
    }

    /// <summary>
    ///   Tolerance of time used
    /// </summary>
    /// <remarks>
    ///   A test if marked as having error if
    ///   (timeUsed - timeExpect) / (1 + timeExpect) > TimeingTolerance
    /// </remarks>
    public static double TimingTolerance { get; set; } = Double.MaxValue;

    #endregion Test Behaviors

    #region Data_And_Properties
    /// <summary>
    ///   DataSet representing the result data
    /// </summary>
    public DataSet DataSet
    {
      get
      {
        DataSet dataSet = new DataSet("Results of " + TestName);
        foreach (ResultSet data in this.Results)
        {
          DataTable table = MakeTable(
            this.TimeUsed, this.TimeExpect, this.Accuracy, data);
          dataSet.Tables.Add(table);
        }
        return dataSet;
      }
    }

    /// <summary>
    ///   Test name
    /// </summary>
    public string TestName;

    /// <summary>
    ///   Time used
    /// </summary>
    public double TimeExpect;

    /// <summary>
    ///   Expected accuracy 
    /// </summary>
    public double Accuracy;

    /// <summary>
    ///   Array of result sets
    /// </summary>
    public ResultSet[] Results;

    /// <summary>
    ///   Time used
    /// </summary>
    public double TimeUsed;

    /// <summary>
    /// Gets a value indicating whether this instance has expects.
    /// </summary>
    /// <remarks></remarks>
    public bool HasExpects => Results != null && Results.Length > 0 && Results[0].Expects != null;

    [NonSerialized] private bool _generatingExpects;
    #endregion // Data_And_Properties

    #region Types
    /// <summary>
    ///   Result set
    /// </summary>
    /// <remarks>One of the data members Values and Labels can be null.</remarks>
    [Serializable]
    public class ResultSet
    {
      public string Name;
      public string[] Labels;
      public double[] Expects;
      public double[] Actuals;
      public bool[] Passed;
    }
    #endregion // Types

  } // class ResultData

  /// <summary>
  ///   Represent a clollection of result data
  /// </summary>
  [Serializable]
  public class ResultCollection
  {
    public ResultData[] List;
  }

}
