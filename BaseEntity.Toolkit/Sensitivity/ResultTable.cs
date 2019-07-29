using System.Data;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   This class encapsulate the standard result table in the sensitivity calculation
  /// </summary>
  internal struct ResultTable
  {
    #region Data

    private const int CalcGammaFlag = 1;
    private const int CalcHedgeFlag = 2;
    private readonly int _flags;
    private readonly DataTable _table;

    #endregion

    #region Properties

    public bool CalcGamma
    {
      get { return (_flags & CalcGammaFlag) != 0; }
    }

    public bool CalcHedge
    {
      get { return (_flags & CalcHedgeFlag) != 0; }
    }

    #endregion

    #region Methods and Properties

    public ResultTable(DataTable dataTable, bool calcGamma, bool calcHedge)
    {
      _table = dataTable;
      _flags = 0;
      if (dataTable != null) return;
      _table = CreateResultTable(calcGamma, calcHedge);
      if (calcGamma)
        _flags |= CalcGammaFlag;
      if (calcHedge)
        _flags |= CalcHedgeFlag;
    }

    /// <summary>
    ///   Create results table
    /// </summary>
    /// <param name="calcGamma"></param>
    /// <param name="calcHedge"></param>
    /// <param name="curveTenors"></param>
    /// <returns></returns>
    public static DataTable CreateResultTable(bool calcGamma, bool calcHedge, bool curveTenors = true)
    {
      var table = new DataTable("Curve Sensitivity Report");
      table.Columns.Add(new DataColumn("Category", typeof(string)));
      table.Columns.Add(new DataColumn("Element", typeof(string)));
      if (curveTenors)
        table.Columns.Add(new DataColumn("Curve Tenor", typeof(string)));
      table.Columns.Add(new DataColumn("Pricer", typeof(string)));
      table.Columns.Add(new DataColumn("Delta", typeof(double)));
      if (calcGamma)
      {
        table.Columns.Add(new DataColumn("Gamma", typeof(double)));
      }
      if (calcHedge)
      {
        table.Columns.Add(new DataColumn("Hedge Tenor", typeof(string)));
        table.Columns.Add(new DataColumn("Hedge Delta", typeof(double)));
        table.Columns.Add(new DataColumn("Hedge Notional", typeof(double)));
      }
      return table;
    }

    public DataTable Table
    {
      get { return _table; }
    }

    public Row NewRow()
    {
      return new Row(_table, _flags);
    }

    public void AddRow(Row row)
    {
      _table.Rows.Add(row.DataRow);
    }

    #endregion

    #region Nested type: Row

    public struct Row
    {
      private readonly int flags_;
      private readonly DataRow row_;

      public Row(DataTable table, int flags)
      {
        row_ = table.NewRow();
        flags_ = flags;
      }

      public DataRow DataRow
      {
        get { return row_; }
      }

      public string Category
      {
        get { return (string)row_["Category"]; }
        set { row_["Category"] = value; }
      }

      public string Element
      {
        get { return (string)row_["Element"]; }
        set { row_["Element"] = value; }
      }

      public string Pricer
      {
        get { return (string)row_["Pricer"]; }
        set { row_["Pricer"] = value; }
      }

      public string CurveTenor
      {
        get { return (string)row_["Curve Tenor"]; }
        set { row_["Curve Tenor"] = value; }
      }

      public double Delta
      {
        get { return (double)row_["Delta"]; }
        set { row_["Delta"] = value; }
      }

      public double Gamma
      {
        get { return (flags_ & CalcGammaFlag) == 0 ? 0 : (double)row_["Gamma"]; }
        set { row_["Gamma"] = value; }
      }

      public string HedgeTenor
      {
        get { return (flags_ & CalcHedgeFlag) == 0 ? "" : (string)row_["Hedge Tenor"]; }
        set { row_["Hedge Tenor"] = value; }
      }

      public double HedgeNotional
      {
        get { return (flags_ & CalcHedgeFlag) == 0 ? 0 : (double)row_["Hedge Notional"]; }
        set { row_["Hedge Notional"] = value; }
      }

      public double HedgeDelta
      {
        get { return (flags_ & CalcHedgeFlag) == 0 ? 0 : (double)row_["Hedge Delta"]; }
        set { row_["Hedge Delta"] = value; }
      }
    }

    #endregion
  }
}