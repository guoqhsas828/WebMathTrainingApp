using System;
using System.Data;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// </summary>
  // class that is used to calculate the sensitivity to seasonality
  public static partial class Sensitivities
  {
    #region public methods

    ///<summary>
    /// method used to calculate the seasonality sensitivity 
    ///</summary>
    ///<param name="pricers"></param>
    ///<param name="measure">The measure ( default is Pv) </param>
    ///<param name="upbump">The bump Size </param>
    ///<param name="seasonalityEffect">seasonality index</param>
    ///<param name="dataTable">data table to store results</param>
    ///<returns>A DataTable with the sensitivities to seasonality</returns>
    public static DataTable
      Seasonality(
      IPricer[] pricers,
      string measure,
      double upbump,
      SeasonalityEffect seasonalityEffect,
      DataTable dataTable)
    {
      return Seasonality(CreateAdapters(pricers, measure), upbump, seasonalityEffect, dataTable);

    }

    #endregion

    #region private methods

    /// <summary>
    /// Wrapper method for computing the seasonality sensitivity
    /// </summary>
    /// <param name="evaluators">The array of pricer evaluators</param>
    /// <param name="upbump"></param>
    /// <param name="seasonalityEffect"></param>
    /// <param name="dataTable"></param>
    /// <returns></returns>
    private static DataTable Seasonality(PricerEvaluator[] evaluators,
                                         double upbump,
                                         SeasonalityEffect seasonalityEffect,
                                         DataTable dataTable)
    {
      var timer = new Timer();

      timer.start();
      logger.DebugFormat("Calculating the Seasonality Sensitivity ");

      //Create a Data Table if we need to 
      if (dataTable == null)
      {
        dataTable = new DataTable("Seasonality Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Month", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
      }

      if (evaluators == null || evaluators.Length == 0)
      {
        timer.stop();
        logger.InfoFormat("Completed seasonality sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }

      object[][] results = CalcSeasonalityDelta(evaluators, upbump, seasonalityEffect,
                                                dataTable);
      //Fill the values in the datatable
      int rows = results.GetUpperBound(0) + 1;
      for (int i = 0; i < rows; i++)
      {
        if (results[i] == null)
          continue;

        DataRow row = dataTable.NewRow();
        row["Category"] = results[i][0];
        row["Element"] = results[i][1];
        row["Month"] = results[i][2];
        row["Pricer"] = results[i][3];
        row["Delta"] = results[i][4];
        dataTable.Rows.Add(row);
      }

      timer.stop();
      logger.InfoFormat("Completed Seasonality sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    /// <summary>
    /// The Method that actually computes the seasonality delta and updates the datatable accordingly. The inflation curve is not recalibrated
    /// </summary>
    /// <param name="evaluators">Evaluators</param>
    /// <param name="upbump">Up bump size</param>
    /// <param name="seasonalityEffect">Seasonality index object</param>
    /// <param name="dataTable">Data table</param>
    private static object[][] CalcSeasonalityDelta(PricerEvaluator[] evaluators,
                                                   double upbump,
                                                   SeasonalityEffect seasonalityEffect,
                                                   DataTable dataTable)
    {
      int rowCount = evaluators.Length * 12;
      int colCount = dataTable.Columns.Count;
      var rows = new object[rowCount][];
      int currentRow = 0;
      foreach (PricerEvaluator evaluator in evaluators)
      {
        Curve seasonality = GetSeasonalityForPricer(evaluator.Pricer);
        if (seasonality == null)
          continue;
        //copy of seasonality to restore curve to its original state
        var originalSeasonality = (Curve)seasonality.Clone();
        try
        {
          if (seasonalityEffect != null)
          {
            new[] {seasonality}.CurveSet(new[] {seasonalityEffect.SeasonalityAdjustment()});
            SetSeasonalityForPricer(evaluator.Pricer, seasonality);
          }
          //copy of seasonality before bumping is performed (could have been changed if seasonality index is not null)
          Curve savedSeasonality = (Curve)seasonality.Clone();
          double originalPv = evaluator.Evaluate();
          for (int m = 1; m <= 12; m++)
          {
            var row = new object[colCount];
            Curve bumpedSeasonality = SeasonalityEffect.PerturbSeasonalityAdjustment((Month)m, savedSeasonality, upbump);
            new[] {seasonality}.CurveSet(new[] {bumpedSeasonality});
            double bumpedPv = evaluator.Evaluate();
            double delta = bumpedPv - originalPv;
            row[0] = "";
            row[1] = "Seasonality";
            row[2] = Enum.GetName(typeof(Month), (Month)m);
            row[3] = evaluator.Pricer.Product.Description;
            row[4] = delta;
            rows[currentRow++] = row;
            new[] {seasonality}.CurveSet(new[] {savedSeasonality});
          }
        }
        finally
        {
          new[] {seasonality}.CurveSet(new[] {originalSeasonality});
        }
      }
      return rows;
    }

    /// <summary>
    /// Wrapper method used to get the seasonality adjustment for the pricer
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <returns>The seasonality adjustment curve</returns>
    private static Curve GetSeasonalityForPricer(IPricer pricer)
    {
      if (pricer is InflationBondPricer)
      {
        var inflationBondPricer = (InflationBondPricer)pricer;
        var icurve = inflationBondPricer.ReferenceCurve;
        if (icurve != null && icurve.Overlay != null)
          return icurve.Overlay;
      }
      else if (pricer is SwapLegPricer)
      {
        var swapLegPricer = (SwapLegPricer)pricer;
        var icurve = swapLegPricer.ReferenceCurve as InflationCurve;
        if (icurve != null && icurve.Overlay != null)
          return icurve.Overlay;
      }
      else
      {
        throw new ArgumentException("Pricer has to be either InflationBondPricer /SwapLegPricer");
      }
      return null;
    }


    /// <summary>
    /// wrapper method used to set the seasonality index for the pricer
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="overlay">Seasonality overlay curve</param>
    private static void SetSeasonalityForPricer(IPricer pricer, Curve overlay)
    {
      if (pricer is InflationBondPricer)
      {
        var inflationBondPricer = (InflationBondPricer)pricer;
        if (inflationBondPricer.ReferenceCurve != null)
        {
          var icurve = inflationBondPricer.ReferenceCurve;
          new[] {icurve.Overlay}.CurveSet(new[] {overlay});
        }
      }
      else if (pricer is SwapLegPricer)
      {
        var swapPricer = (SwapLegPricer)pricer;
        if (swapPricer.ReferenceCurve != null)
        {
          var icurve = (InflationCurve)swapPricer.ReferenceCurve;
          new[] {icurve.Overlay}.CurveSet(new[] {overlay});
        }
      }
      else
      {
        throw new ArgumentException("Pricer has to be either InflationBondPricer /SwapLegPricer/InflationFuturePricer");
      }
    }

    #endregion
  }
}

