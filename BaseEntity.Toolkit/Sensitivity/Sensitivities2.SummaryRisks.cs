using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Sensitivity
{
  public static partial class Sensitivities2
  {
    /// <summary>
    /// calculate spread01
    /// </summary>
    /// <param name="pricer">pricer</param>
    /// <param name="measure">measure. such as Pv</param>
    /// <param name="upBump">up bump size in bump unit</param>
    /// <param name="downBump">down bump size in bump unit</param>
    /// <param name="bumpFlags">bump flags.</param>
    /// <returns></returns>
    public static double Spread01(this IPricer pricer, string measure,
      double upBump, double downBump, BumpFlags bumpFlags)
    {
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, null, null,
        BumpTarget.CreditQuotes, upBump, downBump, BumpType.Uniform,
        bumpFlags, null, true, false, null, false, _useCache, null);
      Debug.Assert(dataTable.IsEmpty() || dataTable.Rows.Count == 1);
      return dataTable.IsEmpty() ? double.NaN : (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    /// Calculate spread gamma
    /// </summary>
    /// <param name="pricer">pricer</param>
    /// <param name="measure">measure</param>
    /// <param name="upBump">up bump size in bump unit</param>
    /// <param name="downBump">down bump size in bump unit</param>
    /// <param name="bumpFlags">bump flags</param>
    /// <returns></returns>
    public static double SpreadGamma(this IPricer pricer, string measure,
      double upBump, double downBump, BumpFlags bumpFlags)
    {
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, measure, null,
        BumpTarget.CreditQuotes, upBump, downBump, BumpType.Uniform,
        bumpFlags, null, true, true, null, false, _useCache, null);
      Debug.Assert(dataTable.IsEmpty() || dataTable.Rows.Count == 1);
      return dataTable.IsEmpty() ? double.NaN : (double)(dataTable.Rows[0])["Gamma"];
    }

    /// <summary>
    /// calculate spread hedge
    /// </summary>
    /// <param name="pricer">pricer</param>
    /// <param name="measure">measure</param>
    /// <param name="hedgeTenor">which tenor to hedge</param>
    /// <param name="upBump">up bump size in bump unit</param>
    /// <param name="downBump">down bump size in bump unit</param>
    /// <param name="bumpFlags">bump flags</param>
    /// <returns></returns>
    public static double SpreadHedge(this IPricer pricer, string measure,
      string hedgeTenor, double upBump, double downBump, BumpFlags bumpFlags)
    {
      var dataTable = Sensitivities2.Calculate(new[] { pricer }, measure, null,
        BumpTarget.CreditQuotes, upBump, downBump, BumpType.Uniform,
        bumpFlags, null, true, false, hedgeTenor, true, _useCache, null);
      Debug.Assert(dataTable.IsEmpty() || dataTable.Rows.Count == 1);
      return dataTable.IsEmpty() ? double.NaN : (double)(dataTable.Rows[0])["Hedge Notional"];
    }

    private static bool IsEmpty(this DataTable table)
    {
      return table == null || table.Rows.Count == 0;
    }

    #region Data

    private const bool _useCache = true;

    #endregion
  }
}
