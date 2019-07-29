using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// </summary>
  // Risk methods for QuantoDefaultSwaps
  public static partial class Sensitivities
  {
    /// <summary>
    /// First order sensitivity to change in FX jump at default
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="bumpSize">Bump size</param>
    /// <returns></returns>
    public static double FxDevaluationDelta(IPricer pricer, double bumpSize)
    {
      var p = pricer as IQuantoDefaultSwapPricer;
      if (p == null)
        return 0.0;
      double retVal;
      double original = p.FxDevaluation;
      try
      {
        double pv = pricer.Pv();
        p.FxDevaluation = Math.Max(original + bumpSize, -1.0);
        double pvp = pricer.Pv();
        retVal = pvp - pv;
      }
      finally
      {
        p.FxDevaluation = original;
      }
      return retVal;
    }

    /// <summary>
    /// First order sensitivity to change in FX-Default Time correlation
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="bumpSize">Bump size</param>
    /// <returns></returns>
    public static double FxCorrelationDelta(IPricer pricer, double bumpSize)
    {
      var p = pricer as IQuantoDefaultSwapPricer;
      if (p == null)
        return 0.0;
      double retVal;
      double original = p.FxCorrelation;
      try
      {
        double pv = pricer.Pv();
        p.FxCorrelation = Math.Min(Math.Max(original + bumpSize, -1.0), 1.0);
        double pvp = pricer.Pv();
        retVal = pvp - pv;
      }
      finally
      {
        p.FxCorrelation = original;
      }
      return retVal;
    }

    /// <summary>
    /// Compute Vega and Vomma w.r.t atm vols of forward FX.
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="upBump">Up bump</param>
    /// <param name="downBump">Down bump</param>
    /// <param name="bumpRelative">True if bump is relative</param>
    /// <param name="bumpType">Bump type</param>
    /// <param name="calcGamma">True to calc Vomma</param>
    /// <param name="dataTable">Results table</param>
    /// <returns>DataTable</returns>
    public static DataTable FxVolatilityQuantoDefaultSwapSensitivities(IPricer pricer, double upBump, double downBump, bool bumpRelative, BumpType bumpType, bool calcGamma, DataTable dataTable)
    {
      var p = pricer as IQuantoDefaultSwapPricer;
      if (p == null || p.FxCurve == null || p.FxVolatility == null)
        return dataTable;
      if (dataTable == null)
      {
        dataTable = new DataTable("Volatility Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
        if (bumpType == BumpType.ByTenor)
          dataTable.Columns.Add(new DataColumn("Curve Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
      }
      var volatilityCurve = p.FxVolatility;
      var originalCurve = CloneUtil.Clone(volatilityCurve);
      try
      {
        var tenors = (bumpType == BumpType.ByTenor)
                       ? ArrayUtil.Generate(volatilityCurve.Tenors.Count, i => new[] { volatilityCurve.Tenors[i].Name })
                       : ArrayUtil.Generate(1, i => new string[0]);
        var basePv = pricer.Pv();
        var upBumped = new List<CalibratedCurve>();
        var downBumped = new List<CalibratedCurve>();
        if (upBump != 0.0)
        {
          var flags = bumpRelative ? BumpFlags.BumpRelative : 0;
          for (int i = 0; i < tenors.Length; ++i)
          {
            var uc = CloneUtil.Clone(originalCurve);
            uc.BumpQuotes(tenors[i], QuotingConvention.None, new[] { upBump }, flags | BumpFlags.RefitCurve);
            upBumped.Add(uc);
          }
        }
        if (calcGamma && downBump != 0.0)
        {
          var flags = bumpRelative ? BumpFlags.BumpRelative | BumpFlags.BumpDown : BumpFlags.BumpDown;
          for (int i = 0; i < tenors.Length; ++i)
          {
            var dc = CloneUtil.Clone(originalCurve);
            dc.BumpQuotes(tenors[i], QuotingConvention.None, new[] { downBump }, flags | BumpFlags.RefitCurve);
            downBumped.Add(dc);
          }
        }
        for (int i = 0; i < upBumped.Count; ++i)
        {
          var row = dataTable.NewRow();
          row["Category"] = volatilityCurve.Category;
          row["Element"] = volatilityCurve.Name;
          if (tenors[i].Length > 0)
            row["Curve Tenor"] = tenors[i][0];
          row["Pricer"] = pricer.Product.Description;
          new[] { volatilityCurve }.CurveSet(new[] { upBumped[i] });
          var uPv = pricer.Pv();
          if (downBumped.Count > 0)
          {
            new[] { volatilityCurve }.CurveSet(new[] { downBumped[i] });
            var dPv = pricer.Pv();
            var delta = uPv - dPv;
            var gamma = uPv - 2 * basePv + dPv;
            row["Delta"] = delta;
            row["Gamma"] = gamma;
          }
          else
          {
            var delta = uPv - basePv;
            row["Delta"] = delta;
          }
          dataTable.Rows.Add(row);
        }
      }
      finally
      {
        new[] { volatilityCurve }.CurveSet(new[] { originalCurve });
      }
      return dataTable;
    }


    /// <summary>
    /// Compute Vega and Vomma w.r.t atm vols of forward FX.
    /// </summary>
    /// <param name="pricers">Array of Quanto Default Swaps</param>
    /// <param name="upBump">Up bump</param>
    /// <param name="downBump">Down bump</param>
    /// <param name="bumpRelative">True if bump is relative</param>
    /// <param name="bumpType">Bump type</param>
    /// <param name="calcGamma">True to calc Vomma</param>
    /// <param name="dataTable">Results table</param>
    /// <returns>DataTable</returns>
    public static DataTable FxVolatilityQuantoDefaultSwapSensitivities(IPricer[] pricers, double upBump, double downBump, bool bumpRelative, BumpType bumpType, bool calcGamma, DataTable dataTable)
    {
      foreach (var p in pricers)
        dataTable = FxVolatilityQuantoDefaultSwapSensitivities(p, upBump, downBump, bumpRelative, bumpType, calcGamma,
                                                               dataTable);
      return dataTable;
    }
  }
}
