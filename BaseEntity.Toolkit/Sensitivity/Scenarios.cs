/*
 *   2014. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using log4net;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// Interface for Scenario Shift definitions
  /// </summary>
  /// <remarks>
  /// <para>This interface defines the contract between the scenario functions and 
  /// defined shifts. Shifts can change
  /// anything impacting the pricing including curves, model parameters and trade terms.</para>
  /// <para>Scenario shifts implement this interface.</para>
  /// <para>Examples of shifts include:</para>
  /// <list type="table">
  ///   <listheader><term>Scenario</term><description>Description</description></listheader>
  ///   <item><term><see cref="ScenarioShiftPricerTerms"/></term><description>Change 
  /// Pricer or Product properties</description></item>
  ///   <item><term><see cref="ScenarioShiftCurves"/></term><description>Parallel 
  /// shift of a Curve</description></item>
  ///   <item><term><see cref="ScenarioShiftCreditCurves"/></term><description>Parallel 
  /// shift of CreditCurve spreads and recovery rates</description></item>
  ///   <item><term><see cref="ScenarioShiftDefaults"/></term><description>CreditCurve 
  /// defaults</description></item>
  ///   <item><term><see cref="ScenarioShiftFxCurves"/></term><description>Parallel 
  /// shift of FX curve rates and basis spreads</description></item>
  ///   <item><term><see cref="ScenarioShiftCorrelation"/></term><description>Parallel 
  /// shift of correlations</description></item>
  ///   <item><term><see cref="ScenarioShiftVolatilities"/></term><description>Parallel 
  /// shift of volatilities</description></item>
  /// </list>
  /// <para>Additional shifts can be defined by implementing <see cref="IScenarioShift">Scenario 
  /// Shift Interface</see>.</para>
  /// <para>See <see cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],
  /// bool,bool,DataTable)"/> for how the scenario shifts
  /// are leveraged by the scenario code.</para>
  /// <para>Notes:</para>
  /// <list type="bullet">
  ///   <item><description>IScenarioShift.RestoreState() is always called so should handle 
  /// case were SaveState() has not been called first</description></item>
  /// </list>
  /// </remarks>
  /// <seealso cref="Scenarios"/>
  /// <seealso cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],bool,bool,DataTable)"/>
  public interface IScenarioShift
  {
    /// <summary>
    /// Validate scenario shift
    /// </summary>
    void Validate();

    /// <summary>
    /// Save state before shift
    /// </summary>
    /// <remarks>
    /// <para>Saves all state information affected by the shift. This method is called by the
    /// scenario functions before the scenario is applied to save state. <see cref="RestoreState"/>
    /// is called after the scenario shift is calculated to restore the state.</para>
    /// </remarks>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    void SaveState(PricerEvaluator[] evaluators);

    /// <summary>
    /// Restore state after shift then clear saved state
    /// </summary>
    /// <remarks>
    /// <para>Restores any state information affected by the shift. This method is called by the
    /// scenario functions afer the scenario is applied to restore state. Note this method
    /// is always called so should handle case where <see cref="SaveState"/> has not been
    /// called.</para>
    /// <para>After the state is restored, the save state is cleared.</para>
    /// </remarks>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    void RestoreState(PricerEvaluator[] evaluators);

    /// <summary>
    /// Perform scenario shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    void PerformShift(PricerEvaluator[] evaluators);
  
    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <remarks>
    /// <para>This is called after all shifts have been performed.</para>
    /// </remarks>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    void PerformRefit(PricerEvaluator[] evaluators);
  }

  /// <summary>
  /// <para>Generalised scenario functions</para>
  /// </summary>
  /// <remarks>
  /// <para>The generalised scenario functions provide an extendable framework 
  /// for efficiently calculating the impact of any
  /// kind of shift on pricing results for all <see cref="IPricer">Pricers</see> 
  /// supported by the Toolkit.</para>
  /// <para>The scenario functions take one or more <see cref="IPricer">Pricers</see>, 
  /// a pricer method (measure) to calculate, and
  /// a set of <see cref="IScenarioShift">Scenario Shifts</see> to apply.</para>
  /// <para><see cref="IScenarioShift">Scenario Shifts</see> can change anything 
  /// the <see cref="IPricer">Pricers</see> depend on
  /// including curves, pricing date, model parameters, and product terms.</para>
  /// <para>See <see cref="IScenarioShift"/> for examples of common scenario shifts.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],bool,bool,DataTable)"/>
  public static class Scenarios
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(Scenarios));

    /// <summary>
    /// Scenario calculator that returns the change in result for a single pricer 
    /// given a set of scenario shifts
    /// </summary>
    /// <remarks>
    /// <para>The scenario functions provide a general framework for calculating 
    /// the impact of any kind of shift on a pricing result.</para>
    /// <para>This function takes a <paramref name="pricer"/> along with the 
    /// <paramref name="measure">pricer method (measure)</paramref>
    /// to calculate and returns the change in the specified measure. It is 
    /// a convenient wrapper for
    /// <see cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],bool,bool,DataTable)"/>.</para>
    /// </remarks>
    /// <seealso cref="IScenarioShift"/>
    /// <seealso cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],bool,bool,DataTable)"/>
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Method of pricer to calculate (result or measure)</param>
    /// <param name="scenarioShifts">List of scenario shifts</param>
    /// <param name="rescaleStrikes">Rescale strikes when repricing correlation 
    /// products</param>
    /// <param name="allowMissing">Allow missing method. If true pricers without 
    /// this method are ignored, otherwise an exception is thrown</param>
    /// <returns>Change in calculated measure</returns>
    public static double CalcScenario(
      IPricer pricer, string measure, IScenarioShift[] scenarioShifts, 
      bool rescaleStrikes, bool allowMissing
      )
    {
      var dataTable = CalcScenario(new[] { pricer }, new[] {measure}, scenarioShifts, 
        rescaleStrikes, allowMissing, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    /// Scenario calculator that returns the change in results for a 
    /// list of pricers given a set of scenario shifts
    /// </summary>
    /// <remarks>
    /// <para>The scenario functions provide a general framework for calculating 
    /// the impact of any kind of shift on a pricing result.</para>
    /// <para>This function takes a list of <paramref name="pricers"/> along with the
    /// <paramref name="measure">pricer method (measure)</paramref> to 
    /// calculate and returns an array of the changes in
    /// the specified measure. It is a convenient wrapper for
    /// <see cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],bool,bool,DataTable)"/>.</para>
    /// </remarks>
    /// <seealso cref="IScenarioShift"/>
    /// <seealso cref="Scenarios.CalcScenario(IPricer[],string[],IScenarioShift[],bool,bool,DataTable)"/>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Method of pricer to calculate (result or measure)</param>
    /// <param name="scenarioShifts">List of scenario shifts</param>
    /// <param name="rescaleStrikes">Rescale strikes when repricing correlation products</param>
    /// <param name="allowMissing">Allow missing method. If true pricers without 
    /// this method are ignored, otherwise an exception is thrown</param>
    /// <returns>Array of changes in calculated measure</returns>
    public static double[] CalcScenario(
      IPricer[] pricers, string measure, IScenarioShift[] scenarioShifts, 
      bool rescaleStrikes, bool allowMissing
      )
    {
      var dataTable = CalcScenario(pricers, new[]{measure}, scenarioShifts, 
        rescaleStrikes, true, null);
      var results = new double[pricers.Length];
      for (int i = 0; i < results.Length; i++)
        results[i] = (double)(dataTable.Rows[i])["Delta"];
      return results;
    }

    /// <summary>
    /// General Scenario calculator for a list of pricers given a set of scenario shifts
    /// </summary>
    /// <remarks>
    /// <para>The scenario functions provide a general framework for calculating the impact of any kind of shift on a pricing result.</para>
    /// <para>The scenario functions take a list of <paramref name="pricers"/> along with a list of pricer
    /// <paramref name="measures"/> to calculate.
    /// If any of the <paramref name="pricers"/> does not support any of the specified <paramref name="measures"/> then
    /// they are ignored for those pricers.</para>
    /// <para>The scenario functions also take a list of <paramref name="scenarioShifts"/> that specify the shifts to apply.
    /// Each shift is applied in sequence and it's effect is based on the result of the prior shifts.</para>
    /// <para><paramref name="scenarioShifts">Shifts</paramref> implement the <see cref="IScenarioShift"/> interface and can
    /// change anything the <paramref name="pricers"/> depend on including curves, time, model parameters, and product terms.</para>
    /// <para>The usage pattern for <see cref="IScenarioShift"/>s is:</para>
    /// <list type="number">
    ///   <item><description>Application calls constructor of IScenarioShift passing 
    /// in all data the shift needs</description></item>
    ///   <item><description>Application calls IScenarioShift.Validate() to validate 
    /// the shift</description></item>
    ///   <item><description>Application calls Sensitivities.Scenario with list of 
    /// IScenarioShifts</description></item>
    ///   <item><description>Sensitivities.Scenario calls the pricer to calculate the base 
    /// result</description></item>
    ///   <item><description>Sensitivities.Scenario calls IScenarioShift.SaveState(evaluators) 
    /// to save the state of the pricing environment</description></item>
    ///   <item><description>Sensitivities.Scenario calls IScenarioShift.PerformShift(evaluators) 
    /// to perform the shift</description></item>
    ///   <item><description>Sensitivities.Scenario calls IScenarioShift.PerformRefit(evaluators) 
    /// to calibrate any dependent curves once all shifts has been performed</description></item>
    ///   <item><description>Sensitivities.Scenario calls the pricer to calculate 
    /// the shifted result</description></item>
    ///   <item><description>Sensitivities.Scenario calls IScenarioShift.RestoreState() 
    /// to restore the state of the pricing environment</description></item>
    /// </list>
    /// <para>The returned <see cref="DataTable"/> has the following columns:</para>
    /// <list type="table">
    ///   <item><term>Pricer</term><description>Pricer description</description></item>
    ///   <item><term>Calculation</term><description>Name of calculation</description></item>
    ///   <item><term>Base</term><description>Base result (ie before shift)</description></item>
    ///   <item><term>Scenario</term><description>Scenario result (ie after shift)</description></item>
    ///   <item><term>Delta</term><description>Difference between scenario and base results</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para>The following sample demonstrates use of the scenario functions.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated discount curves
    ///   DiscountCurve[] discountCurves;
    ///   // Have a set of parallel IR bumps for each discount curve
    ///   double[] bumps;
    ///   // Have a portfolio of Swaps
    ///   SwapPricer[] pricers;
    ///
    ///   // Create pricer inputs and pricer
    ///   // ...
    ///
    ///   // Create scenario shift to bump discount curves
    ///   var shift = new ScenarioShiftCurves(discountCurves, bumps, ScenarioShiftType.Absolute, null);
    ///   // Calculate scenario results
    ///   var delta = Scenarios.CalcScenario(pricers, new[]{"Pv"}, new[] { shift }, false, null);
    ///   // Print out results
    ///   Console.Writeline("Scenario Results...");
    ///   Console.Writeline("\tBase\tScenario\tDelta");
    ///   foreach( var row in dataTable.Rows )
    ///   {
    ///     Console.WriteLine(String.Format("\t{0}\t{1}\t{2}", row["Base"], row["Scenario"], row["Delta"]);
    ///   }
    /// </code>
    /// </example>
    /// <seealso cref="IScenarioShift"/>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measures">List of pricer methods calculate</param>
    /// <param name="scenarioShifts">List of scenario shifts</param>
    /// <param name="rescaleStrikes">Rescale strikes when repricing correlation products</param>
    /// <param name="allowMissing">Allow missing method. If true pricers without 
    /// this method are ignored, otherwise an exception is thrown</param>
    /// <param name="dataTable">Datatable for results or null to create a new results table</param>
    /// <returns>Array of scenario results</returns>
    public static DataTable CalcScenario(
      IPricer[] pricers, string[] measures, IScenarioShift[] scenarioShifts, bool rescaleStrikes,
      bool allowMissing, DataTable dataTable
      )
    {
      // Array of evaluators and related info for each measure to calculate
      var evals = new List<PricerEvaluator>();
      foreach (var m in measures)
        evals.AddRange(pricers.CreateAdapters(m, allowMissing, true));
      var evaluators = evals.ToArray();
      var fixers = PricerLockStrikes(evaluators);
      var rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(evaluators, new[] { rescaleStrikes });
      // Tests for specific shifts
      var defaultChanged = scenarioShifts.OfType<ScenarioShiftDefaults>().Any();
      var recoveryBumped = scenarioShifts.OfType<ScenarioShiftDefaults>().Any(c => c.RecoveriesBumped) ||
        scenarioShifts.OfType<ScenarioShiftCreditCurves>().Any(c => c.RecoveriesBumped);
      var correlationBumped = scenarioShifts.OfType<ScenarioShiftCorrelation>().Any();

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = new DataTable("Scenario Report");
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Calculation", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Base", typeof(double)));
        dataTable.Columns.Add(new DataColumn("Scenario", typeof(double)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
      }

      try
      {
        // Calculate the base values
        logger.DebugFormat("CalcScenario: Calculating base values...");
        var baseResult = new double[evaluators.Length];
        for (var i = 0; i < evaluators.Length; ++i)
        {
          baseResult[i] = evaluators[i].Evaluate();
          logger.DebugFormat("{0} {1} = {2}", evaluators[i].Pricer.Product.Description, evaluators[i].MethodName, baseResult[i]);
        }
        // Save state
        logger.DebugFormat("Saving state...");
        foreach (var s in scenarioShifts)
          s.SaveState(evaluators);
        // Perform shifts
        logger.DebugFormat("Performing shifts...");
        foreach (var s in scenarioShifts)
        {
          logger.DebugFormat("Shift {0}...", s);
          s.PerformShift(evaluators);
        }

        // Refitting calibrated objects
        logger.DebugFormat("Performing refits...");
        foreach (var s in scenarioShifts)
        {
          logger.DebugFormat("Refit {0}...", s);
          s.PerformRefit(evaluators);
        }

        if (correlationBumped)
        {
          // Act as if rescaleStrikes = true
          PricerUnlockStrikes(fixers);
          fixers = null;
        }

        // Reset price 
        PricerResetOriginalBasket(evaluators, defaultChanged, recoveryBumped, correlationBumped);

        // Calculate results
        logger.DebugFormat("Calculating results...");
        for (var i = 0; i < evaluators.Length; ++i)
        {
          var scenarioResult = evaluators[i].Evaluate();
          logger.DebugFormat("{0} {1} = {2}", evaluators[i].Pricer.Product.Description, evaluators[i].MethodName, scenarioResult);
          var row = dataTable.NewRow();
          row["Pricer"] = evaluators[i].Pricer.Product.Description;
          row["Calculation"] = evaluators[i].MethodName;
          row["Base"] = baseResult[i];
          row["Scenario"] = scenarioResult;
          row["Delta"] = scenarioResult - baseResult[i];
          dataTable.Rows.Add(row);
        }
      }
      finally
      {
        // Restore state (in reverse order so that any incremental effects will be correctly undone).
        logger.DebugFormat("Restoring states...");
        foreach (var s in scenarioShifts.Reverse())
        {
          logger.DebugFormat("Restoring {0}...", s);
          s.RestoreState(evaluators);
        }

        // Reset price
        PricerResetOriginalBasket(evaluators, defaultChanged, recoveryBumped, correlationBumped);
        PricerUnlockStrikes(fixers);
        Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }

      // return the results
      logger.DebugFormat("CalcScenario: Done.");
      return dataTable;
    }

    #region Utilities

    private static void PricerResetOriginalBasket(
      PricerEvaluator[] evaluators,
      bool defaultChanged,
      bool recoveryModified,
      bool correlationModified)
    {
      if (evaluators == null)
        return;
      foreach (var t in evaluators)
      {
        if (t == null) continue;
        var saved = t.SensitivityFlags;
        if (defaultChanged)
          t.SensitivityFlags |= PricerEvaluator.DefaultChangedFlag;
        t.Reset(recoveryModified, correlationModified);
        t.SensitivityFlags = saved;
      }
      return;
    }

    private static IDisposable[] PricerLockStrikes(PricerEvaluator[] pricers)
    {
      var fixers = new IDisposable[pricers.Length];
      for (int i = 0; i < pricers.Length; i++)
      {
        var basket = pricers[i].Basket as BaseCorrelationBasketPricer;
        if (basket == null || basket.RescaleStrike) continue;
        fixers[i] = basket.LockCorrection();
      }
      return fixers;
    }

    private static void PricerUnlockStrikes(IDisposable[] fixers)
    {
      if (fixers == null) return;
      for (int i = 0; i < fixers.Length; i++)
      {
        if (fixers[i] != null) fixers[i].Dispose();
      }
    }

    /// <summary>
    /// Bump a double value
    /// </summary>
    /// <param name="originalValue">original value</param>
    /// <param name="type">Scenario bump type, such as Absolute, Relative and Specified</param>
    /// <param name="bumpSize">bump size</param>
    /// <returns></returns>
    public static double Bump(double originalValue,
     ScenarioShiftType type, double bumpSize)
    {
      var value = originalValue;
      switch (type)
      {
        case ScenarioShiftType.Absolute:
          value += bumpSize;
          break;
        case ScenarioShiftType.Relative:
          value += originalValue * bumpSize;
          break;
        case ScenarioShiftType.Specified:
          value = bumpSize;
          break;
      }
      return value;
    }

    #endregion Utilities

  }
}
