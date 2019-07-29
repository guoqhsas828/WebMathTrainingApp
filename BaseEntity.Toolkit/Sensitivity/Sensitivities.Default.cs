/*
 * Sensitivities.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 *  Partial implementation of the default sensitivity functions
 * 
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Sensitivity
{
  ///
  /// <summary>
  ///   
  /// </summary>
  //Methods for calculating generalized sensitivity measures
  public static partial class Sensitivities
  {
    #region SummaryRiskMethods
    
    /// <summary>
    ///   Compute the default (event) sensitivity.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivity to the largest average 5Yr spread
    ///   credit curve.</para>
    ///
    ///   <para>Equivalent to <see cref="VOD(IPricer,string, bool[])">VOD(pricer, null, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">IPricer</param>
    /// <param name="rescaleStrikes">Boolean indicating resacle strikes flag or not for CDO pricer</param>
    /// <returns>Default sensitivity</returns>
    /// 
    public static double
    VOD(IPricer pricer, params bool[] rescaleStrikes)
    {
      DataTable dataTable = VOD(new PricerEvaluator[] { new PricerEvaluator(pricer, "Pv") }, null, rescaleStrikes);
      return dataTable==null ? 0 : (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    ///   Compute the default (event) sensitivity.
    /// </summary>
    /// <remarks>
    ///   <para>The Default or VOD sensitivity is the change in MTM given an instintaneous
    ///   default of the underlying credit curve with the lowest 5Yr survival probability
    ///   (highest spread).</para>
    /// </remarks>
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="rescaleStrikes">Boolean indicating resacle strike or not for CDO pricer</param>
    /// <returns>Default sensitivity</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the VOD for
    /// a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate Default event sensitivity
    ///   double VOD = Sensitivities.VOD(
    ///     pricer,             // Pricer for CDO tranche
    ///     "Pv",               // Calculate the change in Pv
    ///    );
    ///
    ///   // Print out results
    ///   Console.WriteLine( "VOD = {0}, VOD );
    /// </code>
    /// </example>
    ///
    public static double VOD(IPricer pricer, string measure, params bool[] rescaleStrikes)
    {
      DataTable dataTable = VOD(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) }, null, rescaleStrikes);
      return dataTable==null ? 0 : (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    ///   Compute the default (event) sensitivity to the largest spread name
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivity to the largest average 5Yr spread
    ///   credit curve.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Array of results</returns>
    ///
    public static double[] VOD(IPricer[] pricers, string measure, bool[] rescaleStrikes)
    {
      // Validation
      if (pricers.Length < 1 || pricers == null)
        throw new System.ArgumentException("Must specify pricers");

      DataTable dataTable = VOD(pricers, measure, null, rescaleStrikes);
      double[] results = new double[pricers.Length];
      int cnt = 0;
      for (int i = 0; i < pricers.Length; i++)
        results[i] = (pricers[i] != null) ? (dataTable==null?0:(double)(dataTable.Rows[cnt++]["Delta"])) : 0.0;
      return results;
    }

    #endregion SummaryRiskMethods

    #region Default Sensitivity

    /// <summary>
    ///   Compute the default (event) sensitivity for a pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    /// 
    ///   <para>Equivalent to <see cref="Default(IPricer[],string,bool,string,DataTable, bool[])">
    ///   Default(new IPricer[] {pricer}, measure, calcHedge, hedgeTenor, dataTable, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Default(IPricer pricer, string measure, bool calcHedge, string hedgeTenor, DataTable dataTable, params bool[] rescaleStrikes)
    {
      return Default(CreateAdapters(pricer, measure), calcHedge, hedgeTenor, dataTable, null, rescaleStrikes);
    }

   
    /// <summary>
    ///   Compute the default (event) sensitivity for a pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    /// 
    ///   <para>Equivalent to <see cref="Default(IPricer[],string,bool,string,DataTable, bool[])">
    ///   Default(new IPricer[] {pricer}, measure, calcHedge, hedgeTenor, dataTable, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="defaultRecoveries">Recovery rates upon default</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Default(IPricer pricer, string measure, bool calcHedge, string hedgeTenor, DataTable dataTable,
      double[] defaultRecoveries, params bool[] rescaleStrikes)
    {
      return Default(CreateAdapters(pricer, measure), calcHedge, hedgeTenor, dataTable, defaultRecoveries, rescaleStrikes);
    }

    /// <summary>
    ///   Compute the default (event) sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The survival curves are individually marked as defaulted and the products
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>An assumed dependence exists between the pricers and the curves to be bumped
    ///   that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the default event sensitivity
    /// for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO[] cdos;
    ///   SyntheticCDOHeterogeneousPricer[] pricers;
    ///
    ///   // Initialise cdos and pricers
    ///   // ...
    ///
    ///   // Calculate Default event sensitivities along with 5Yr CDS hedges
    ///   //
    ///   DataTable dataTable = Sensitivities.Default( pricers,                     // Pricers for CDO tranches
    ///                                                "Pv",                        // Calculate change in Pv
    ///                                                true,                        // Do hedge calculation
    ///                                                "5 Year",                    // Hedge to 5yr tenor
    ///                                                null                         // Create new table of results
    ///                                               );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Curve {0}, {1} default event sensitivity is {2}, 5Yr CDS Hedge is {3}",
    ///       (string)row["Element"], row["Pricer"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable Default(
      IPricer[] pricers, string measure, bool calcHedge, string hedgeTenor, DataTable dataTable, bool[] rescaleStrikes
      )
    {
      return Default(CreateAdapters(pricers, measure), calcHedge, hedgeTenor, dataTable, null, rescaleStrikes);
    }

    /// <summary>
    ///   Compute the default (event) sensitivity for a pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    /// 
    ///   <para>Equivalent to <see cref="Default(IPricer[],string,bool,string,DataTable, bool[])">
    ///   Default(new IPricer[] {pricer}, measure, calcHedge, hedgeTenor, dataTable, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricers">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="defaultRecoveries">Recovery rates upon default</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable Default(
      IPricer[] pricers, string measure, bool calcHedge, string hedgeTenor,
      DataTable dataTable, double[] defaultRecoveries, bool[] rescaleStrikes
      )
    {
      return Default(CreateAdapters(pricers, measure), calcHedge, hedgeTenor, dataTable, defaultRecoveries, rescaleStrikes);
    }
    /// <summary>
    ///   Compute the default (event) sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The survival curves are individually marked as defaulted and the products
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>An assumed dependence exists between the pricers and the curves to be bumped
    ///   that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///<param name="sensitivityMethod">Methodology used to compute sensitivities</param> 
    ///<param name="defaultRecoveries">Array of user specified default recoveries</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
  
    public static DataTable Default(
      IPricer[] pricers, string measure, bool calcHedge, string hedgeTenor, DataTable dataTable, SensitivityMethod sensitivityMethod, double[] defaultRecoveries, bool[] rescaleStrikes)
    {
        bool semiAnalytic = (sensitivityMethod == SensitivityMethod.SemiAnalytic);
        if (semiAnalytic && (String.Compare(measure, "") != 0) && (String.Compare(measure.Trim().ToLower(), "pv") != 0))
            throw new ToolkitException("For the time being only semi-analytic sensitivities for the Pv are exposed");
        if (semiAnalytic)
            return CreditAnalyticSensitivities.SemiAnalyticVOD(pricers, calcHedge, hedgeTenor, rescaleStrikes);
        return Default(CreateAdapters(pricers, measure), calcHedge, hedgeTenor, dataTable, defaultRecoveries, rescaleStrikes);
    }
    
     
   
    /// <summary>
    ///   Compute the default (event) sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The survival curves are individually marked as defaulted and the products
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>An assumed dependence exists between the pricers and the curves to be bumped
    ///   that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricer evaluators</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="defaultRecoveries">Recovery rates upon default</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the default event sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate Default event sensitivities along with 5Yr CDS hedges
    ///   //
    ///   DataTable dataTable = Sensitivities.Default( CreateAdapters(pricer,null}, // Pricer for CDO tranche
    ///                                                true,                        // Do hedge calculation
    ///                                                "5 Year",                    // Hedge to 5yr tenor
    ///                                                null,                        // Create new table of results
    ///                                                null                         // If no recovery rates specified upon default
    ///                                               );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Default event sensitivity is {1}, 5Yr CDS Hedge is {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    private static DataTable Default(PricerEvaluator[] pricers, bool calcHedge,
      string hedgeTenor, DataTable dataTable, double[] defaultRecoveries)
    {
      // Find the latest settlement date
      // Note: there are two approaches:
      //   (1) Require the settle date to be the same for all pricers and return the common settle date;
      //   (2) Not require a common settle date and return the latest settle date;
      // Here we adopt approach 2, in order not to break any old sheets.
      Dt settle = LastSettle(pricers);

      bool[] ignorePricers = null;
      if (pricers != null && pricers.Length > 0)
      {
        ignorePricers = new bool[pricers.Length]; // default all elements to false
        for (int j = 0; j < pricers.Length; j++)
          ignorePricers[j] = !IsDefaultSensitivityApplicable(pricers[j]);
      }

      // Get survival curves for these pricers
      var sc = ignorePricers == null
        ? (IList<SurvivalCurve>) EmptyArray<SurvivalCurve>.Instance
        : pricers.Where((p,i)=>!ignorePricers[i]).ToArray().GetDefaultSensitivityCurves(true);

      // Assume all pricers use same basket
      if (defaultRecoveries != null && defaultRecoveries.Length > 1 && defaultRecoveries.Length != sc.Count)
        throw new ArgumentException("Length of recoveries on default must match that of survival curves");

      if (defaultRecoveries != null && defaultRecoveries.Length == 1)
        defaultRecoveries = ArrayUtil.NewArray<double>(sc.Count, defaultRecoveries[0]);

      // Find all the survival curves not defaulted on the settle
      if (sc.Count > 0)
      {
        List<SurvivalCurve> tmp = new List<SurvivalCurve>();
        List<double> dfltRecov = new List<double>();
        for (int i = 0; i < sc.Count; ++i)
        {
          Dt dflt = sc[i].DefaultDate;
          if (dflt.IsValid() && Dt.Cmp(dflt, settle) <= 0)
            continue; // do not include curves defaulted on or before settle
          tmp.Add(sc[i]);
          if (defaultRecoveries != null)
            dfltRecov.Add(defaultRecoveries[i]);
        }
        sc = tmp;
        if (defaultRecoveries != null)
          defaultRecoveries = dfltRecov.ToArray();
      }
      SurvivalCurve[] survivalCurves = sc.ToArray();

      // Save the survival curves with recovery, later will be restored with recovery
      CalibratedCurve[] savedSurvivalCurves = CurveUtil.CurveCloneWithRecovery(survivalCurves);

      // Create defaulted curves
      logger.Debug("Generating default curve set");
      CalibratedCurve[] bumpedCurves = CurveUtil.CurveCloneWithRecovery(survivalCurves);
      for (int i = 0; i < bumpedCurves.Length; i++)
      {
        // bumpedCurves could be null due to null sc
        // for safty, get asOf inside the loop
        Dt asOf = bumpedCurves[i].AsOf;
        // create new recovery curve at new default recovery rate
        if (defaultRecoveries != null)
        {
          // NOTE: new a recovery curve and assign to bumped survival curve's recovery curve
          //       will break the reference. Hence we need only to modify the fundamental data
          RecoveryCurve r = ((SurvivalCurve)bumpedCurves[i]).SurvivalCalibrator.RecoveryCurve;
          double spread = r.Interpolate(settle);
          ((SurvivalCurve)bumpedCurves[i]).SurvivalCalibrator.RecoveryCurve.Spread += (defaultRecoveries[i] - spread);
        }
        SurvivalCurve scv = (SurvivalCurve) bumpedCurves[i];
        if (scv.Defaulted != Defaulted.HasDefaulted)
        {
          scv.DefaultDate = settle;
          scv.Defaulted = Defaulted.WillDefault;
        }
      }

      if (defaultRecoveries != null)
        foreach (PricerEvaluator pe in pricers)
          pe.IncludeRecoverySensitivity = true;

      // Compute sensitivities
      DataTable dt;
      var savedFlags = SetDefaultChangedFlags(pricers);
      try
      {
        dt = Curve(pricers, survivalCurves,
          bumpedCurves, BumpType.Parallel, calcHedge, hedgeTenor, dataTable, ignorePricers);
      }
      finally
      {
        ResetDefaultChangedFlags(pricers, savedFlags);
      }

      //  Restore survival curves with recovery
      CurveUtil.CurveRestoreWithRecovery(survivalCurves, savedSurvivalCurves);

      return dt;
    }

    private static bool IsDefaultSensitivityApplicable(PricerEvaluator pEval)
    {
      if (pEval.Pricer is BondPricer)
      {
        var bp = pEval.Pricer as BondPricer;
        if (bp.IsDefaulted(pEval.Settle))
        {
          return false;
        }
        return pEval.Settle < pEval.Product.Maturity;
      }
      if (pEval.Pricer is DefaultedAssetPricer)
        return false;
      return true;
    }

    private static bool IsRecoverySensitivityApplicable(PricerEvaluator pEval)
    {
      if (pEval.Pricer is DefaultedAssetPricer)
        return ((DefaultedAssetPricer)pEval.Pricer).RecoveryCurve != null;
      return true;
    }

    /// <summary>
    ///  Wrapper method between Default(IPricer,...) and Default(PricerEvaluator,...)
    ///  Its function is to set and restore rescale strikes falg for CDO prcier.
    ///  <preliminary>For internal use only.</preliminary>
    /// </summary>
    /// <param name="pricers">Array of pricer evaluators</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="defaultRecoveries">Recovery rates upon default</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricers</param>
    /// <returns>Data table.</returns>
    /// <exclude/>
    public static DataTable Default(PricerEvaluator[] pricers, bool calcHedge,
      string hedgeTenor, DataTable dataTable, double[] defaultRecoveries, bool[] rescaleStrikes)
    {
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(pricers, rescaleStrikes);
      DataTable dt = null;
      try
      {
        dt = Default(pricers, calcHedge, hedgeTenor, dataTable, defaultRecoveries);
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(pricers, rescaleStrikesSaved);
      }
      return dt;
    }

    /// <summary>
    ///   Compute the default (event) sensitivity to the largest spread name
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivity to the largest average 5Yr spread
    ///   credit curve.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable VOD(IPricer[] pricers, string measure, DataTable dataTable, bool[] rescaleStrikes)
    {
      return VOD(CreateAdapters(pricers, measure), dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Compute the default (event) sensitivity to the largest spread name
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivity to the largest average 5Yr spread
    ///   credit curve.</para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    private static DataTable VOD(PricerEvaluator[] evaluators, DataTable dataTable )
    {
      // Find the latest settlement date
      // Note: there are two approaches:
      //   (1) Require the settle date to be the same for all pricers and return the common settle date;
      //   (2) Not require a common settle date and return the latest settle date;
      // Here we adopt approach 2, in order not to break any old sheets.
      Dt settle = LastSettle(evaluators);

      bool[] ignorePricers = null;
      if (evaluators != null && evaluators.Length > 0)
      {
        ignorePricers = new bool[evaluators.Length]; // default all elements to false
        for (int j = 0; j < evaluators.Length; j++)
          ignorePricers[j] = !IsDefaultSensitivityApplicable(evaluators[j]);  // Do not compute default sensitivity for matured items ...
      }

      // Get survival curves for these pricers
      var sc = ignorePricers == null
        ? (IList<SurvivalCurve>)EmptyArray<SurvivalCurve>.Instance
        : evaluators.Where((p, i) => !ignorePricers[i]).ToArray().GetDefaultSensitivityCurves(true);

      // Should be OK to just use the first product's maturity as a proxy.
      Dt date = evaluators[0].Product.Maturity;
      SurvivalCurve largestSpreadCurve = null;
      double maxDP = 0.0;
      for (int i = 0; i < sc.Count; i++)
      {
        // We ignore all the curves which defaulted on or before settle
        Dt dfltDate = sc[i].DefaultDate;
        if (dfltDate.IsValid() && Dt.Cmp(dfltDate, settle) <= 0)
          continue;

        // Find the curve with the largest default probability at maturity
        double dp = sc[i].DefaultProb(date);
        if (dp >= maxDP)
        {
          maxDP = dp;
          largestSpreadCurve = sc[i];
        }
      }
      // If the largest spread curve is not found, all curves are defaulted.
      // We return 0.0 for this case
      if (largestSpreadCurve == null)
        return null;

      // Create defaulted curve
      SurvivalCurve defaultedCurve = (SurvivalCurve)largestSpreadCurve.Clone();
      defaultedCurve.DefaultDate = settle;
      defaultedCurve.Defaulted = Defaulted.WillDefault;

      // Compute sensitivities
      var savedFlags = SetDefaultChangedFlags(evaluators);
      try
      {
        DataTable dt = Curve(evaluators, new SurvivalCurve[] { largestSpreadCurve },
          new SurvivalCurve[] { defaultedCurve }, BumpType.Uniform, false, null, null, ignorePricers);
        return dt;
      }
      finally
      {
        ResetDefaultChangedFlags(evaluators, savedFlags);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
    private static DataTable VOD(PricerEvaluator[] evaluators, DataTable dataTable, bool[] rescaleStrikes)
    {
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikes);
      DataTable dt = null;
      try
      {
        dt = VOD(evaluators, dataTable);
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }
      return dt;
    }

    private static int[] SetDefaultChangedFlags(PricerEvaluator[] pricers)
    {
      if(pricers==null) return null;
      int[] flags = new int[pricers.Length];
      for(int i = 0; i < pricers.Length;++i)
      {
        flags[i] = pricers[i].SensitivityFlags;
        pricers[i].SensitivityFlags |= PricerEvaluator.DefaultChangedFlag;
      }
      return flags;
    }
    private static void ResetDefaultChangedFlags(PricerEvaluator[] pricers, int[] flags)
    {
      if (pricers == null || flags == null) return;
      for (int i = 0; i < pricers.Length; ++i)
        pricers[i].SensitivityFlags = flags[i];
    }
    #endregion Default Sensitivity

    #region Backward Compatible
    /// <summary>
    ///   Compute the default (event) sensitivity for a pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    /// 
    ///   <para>Equivalent to <see cref="Default(IPricer[],string,bool,string,DataTable, bool[])">
    ///   Default(new IPricer[] {pricer}, measure, calcHedge, hedgeTenor, dataTable, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Default(IPricer pricer, bool calcHedge, string hedgeTenor, DataTable dataTable)
    {
      return Default(CreateAdapters(pricer, null), calcHedge, hedgeTenor, dataTable, null);
    }

    /// <summary>
    ///   Compute the default (event) sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical default event sensitivities and hedge equivalents with several
    ///   options for controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The survival curves are individually marked as defaulted and the products
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>An assumed dependence exists between the pricers and the curves to be bumped
    ///   that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricer evaluators</param>
    /// <param name="calcHedge">Calculate hedge equivalent</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the default event sensitivity
    /// for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO[] cdos;
    ///   SyntheticCDOHeterogeneousPricer[] pricers;
    ///
    ///   // Initialise cdos and pricers
    ///   // ...
    ///
    ///   // Calculate Default event sensitivities along with 5Yr CDS hedges
    ///   //
    ///   DataTable dataTable = Sensitivities.Default( pricers,                     // Pricers for CDO tranches
    ///                                                "Pv",                        // Calculate change in Pv
    ///                                                true,                        // Do hedge calculation
    ///                                                "5 Year",                    // Hedge to 5yr tenor
    ///                                                null                         // Create new table of results
    ///                                               );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Curve {0}, {1} default event sensitivity is {2}, 5Yr CDS Hedge is {3}",
    ///       (string)row["Element"], row["Pricer"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Default(
      IPricer[] pricers,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable
      )
    {
      return Default(CreateAdapters(pricers, null), calcHedge, hedgeTenor, dataTable, null);
    }
    #endregion Backward Compatible

  } // class Sensitivities
}
