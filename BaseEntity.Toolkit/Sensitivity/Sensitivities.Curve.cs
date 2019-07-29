/*
 * Sensitivities.Curve.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 *  Partial implementation of the curve sensitivity functions
 * 
 */
using System;
using System.Data;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Sensitivity
{
  ///
  /// <summary>
  ///   
  /// </summary>
  //Methods for calculating generalized sensitivity measures
  public static partial class Sensitivities
  {

    #region Curve_Sensitivity
    
    /// <summary>
    ///   Calculate the sensitivity to a set of specified bumped curves for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes the delta between pricing using the base (original) curves and each of
    ///   specified perturbed curves. This is similar to the CurveSensitivity function excepting
    ///   that the bumped curves are specified rather than generated.</para>
    ///
    ///   <para>Equivalent to <see cref="Curve(IPricer[],string,CalibratedCurve[],CalibratedCurve[],BumpType,bool,string,DataTable)">
    ///   Curve(new IPricer[] {pricer}, measure, curves, bumpedCurves, bumpType, calcHedge, hedgeTenor, dataTable)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="curves">Array of curves for the base case or null for all curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Curve(
      IPricer pricer,
      string measure,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      BumpType bumpType,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable
      )
    {
      return Curve(CreateAdapters(pricer, measure), curves, bumpedCurves, bumpType,
        calcHedge, hedgeTenor, dataTable);
    }

    /// <summary>
    ///   Calculate the sensitivity to a set of specified bumped curves for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes the delta between pricing using the base (original) curves and each of
    ///   specified perturbed curves. This is similar to the CurveSensitivity function excepting
    ///   that the bumped curves are specified rather than generated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Valid options include parallel, category or uniform bumping.</para>
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
    ///   <para>If the original curves are specified, a dependence is assumed to exist between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="curves">Array of curves for the base case or null for all curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the curve sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate default event risk by name.
    ///   // To do this we first create a set of maching defaulted curves then call this method
    ///   // with the original curves and the defaulted curve sets.
    ///   //
    ///   // Create a copied set of defaulted curves
    ///   SurvivalCurve [] origSurvivalCurves = pricer.SurvivalCurves;
    ///   SurvivalCurve [] defaultedSurvivalCurves = new SurvivalCurve[origSurvivalCurves.Length];
    ///   for( int i = 0; i &lt; origSurvivalCurves.Length; i++ )
    ///   {
    ///     defaultedSurvivalCurves[i] = origSurvivalCurves[i].Clone();
    ///     defaultedSurvivalCurves[i].Defaulted = Defaulted.WillDefault;
    ///   }
    ///
    ///   // Compute sensitivities
    ///   DataTable dataTable = Sensitivities.Curve(
    ///    new IPricer[] { pricer },    // Pricer for CDO
    ///    origSurvivalCurves,          // Original survival curves to change
    ///    defaultedSurvivalCurves,     // Defaulted survival curves
    ///    BumpType.Parallel,           // Bumps are parallel
    ///    true,                        // Do hedge calculation
    ///    "5 Year",                    // Hedge to 5yr tenor
    ///    null                         // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Default Event Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Curve(
      IPricer[] pricers,
      string measure,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      BumpType bumpType,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable
      )
    {
      return Curve(CreateAdapters(pricers, measure), curves, bumpedCurves, bumpType,
        calcHedge, hedgeTenor, dataTable );
    }

    private static DataTable
      Curve(
      PricerEvaluator[] evaluators,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      BumpType bumpType,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable
      )
    {
      bool[] ignorePricers = null;
      if (evaluators != null && evaluators.Length > 0)
        ignorePricers = new bool[evaluators.Length];  // default all elements to false
      DataTable dt = Curve(evaluators, curves, bumpedCurves, bumpType, calcHedge, hedgeTenor, dataTable, ignorePricers); // delegate to the version below.
      return dt;
    }

    /// <summary>
    ///   Calculate the sensitivity to a set of specified bumped curves for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes the delta between pricing using the base (original) curves and each of
    ///   specified perturbed curves. This is similar to the CurveSensitivity function excepting
    ///   that the bumped curves are specified rather than generated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Valid options include parallel, category or uniform bumping.</para>
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
    ///   <para>If the original curves are specified, a dependence is assumed to exist between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="curves">Array of curves for the base case or null for all curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="ignorePricers">The elements of this array correspond to the element of evaluators array. 
    /// Will set bumped pv and sensitivity to 0 for those elements of this array which are set to true.</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the curve sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate default event risk by name.
    ///   // To do this we first create a set of maching defaulted curves then call this method
    ///   // with the original curves and the defaulted curve sets.
    ///   //
    ///   // Create a copied set of defaulted curves
    ///   SurvivalCurve [] origSurvivalCurves = pricer.SurvivalCurves;
    ///   SurvivalCurve [] defaultedSurvivalCurves = new SurvivalCurve[origSurvivalCurves.Length];
    ///   for( int i = 0; i &lt; origSurvivalCurves.Length; i++ )
    ///   {
    ///     defaultedSurvivalCurves[i] = origSurvivalCurves[i].Clone();
    ///     defaultedSurvivalCurves[i].Defaulted = Defaulted.WillDefault;
    ///   }
    ///
    ///   // Compute sensitivities
    ///   PricerEvaluator pricerEval = new PricerEvaluator(pricer, "ExpectedLoss");
    ///   DataTable dataTable = Sensitivities.Curve(
    ///    new PricerEvaluator[] { pricerEval },    // Pricer evaluator for CDO
    ///    origSurvivalCurves,          // Original survival curves to change
    ///    defaultedSurvivalCurves,     // Defaulted survival curves
    ///    BumpType.Parallel,           // Bumps are parallel
    ///    true,                        // Do hedge calculation
    ///    "5 Year",                    // Hedge to 5yr tenor
    ///    null                         // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Default Event Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    private static DataTable
    Curve(
      PricerEvaluator[] evaluators,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      BumpType bumpType,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable,
      bool[] ignorePricers
      )
    {
      Timer timer = new Timer();
      timer.start();

      logger.Debug("Calculating curve spread sensitivities");

      // Validation
      if (curves == null)
        throw new ArgumentException("Must specify base curves");
      if (bumpedCurves == null || bumpedCurves.Length != curves.Length)
        throw new ArgumentException("Number of bumped curves must match number of base curves");

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = ResultTable.CreateResultTable(false, calcHedge, false);
      }

      if (evaluators == null || evaluators.Length == 0 || bumpedCurves.Length == 0)
      {
        timer.stop();
        logger.InfoFormat("Completed curve sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }

      if (ignorePricers == null || ignorePricers.Length != evaluators.Length)
        throw new ArgumentException("The size of ignorePricers array must match the size of evaluators array");

      // Save a copy of the initial curves
      logger.DebugFormat("Saving copy of curves before bumping (time {0}s)", timer.getElapsed());

      CalibratedCurve[] savedCurves = CloneUtil.Clone(curves);

      // Find the maturity date for each pricer evaluator
      Dt[] hedgeMaturities = null;
      bool[] hedgeMaturitiesOrNot = null;
      if (calcHedge && (String.Compare(hedgeTenor != null ? hedgeTenor.ToLower() : hedgeTenor, "maturity") == 0))
      {
        hedgeMaturities = new Dt[evaluators.Length];
        hedgeMaturitiesOrNot = new bool[evaluators.Length];
        for (int i = 0; i < evaluators.Length; ++i)
        {
          hedgeMaturities[i] = evaluators[i].Product.Maturity;
          hedgeMaturitiesOrNot[i] = false;
        }
      }
      // Convert the string to Dt if hedgeTenor passed in is an excel date
      Dt hedgeDate = new Dt();
      if (calcHedge && String.Compare(hedgeTenor, "matching") != 0)
      {
        if (String.Compare(hedgeTenor != null ? hedgeTenor.ToLower() : hedgeTenor, "maturity") != 0
          && (hedgeTenor != null ? hedgeTenor.Length : 0) >= 5 && StringIsNum(hedgeTenor))
          hedgeDate = Dt.FromExcelDate(Double.Parse(hedgeTenor));
      }
      // Compute sensitivities
      // Any errors are trapped and the curves restored.
      try
      {
        double[,] upTable = null;
        double[] upHedge = null;
        double[][] upHedge2 = null;
        // Compute base case
        logger.Debug("Computing base case");
        double[] mtm = new double[evaluators.Length];
        PricerReset(evaluators);
        for (int j = 0; j < evaluators.Length; j++)
        {
          mtm[j] = ignorePricers[j] ? 0.0 : evaluators[j].Evaluate();
        }

        if (hedgeMaturities != null && hedgeMaturities.Length > 0)
        {
          upHedge2 = new double[hedgeMaturities.Length][];
        }
        bool calcTenorHedge = false;
        switch (bumpType)
        {
          case BumpType.Parallel:
            // Make sure we use the right hedge. "matching" doesn't really make sense here.
            if (calcHedge && String.Compare(hedgeTenor, "matching") == 0)
              throw new ArgumentException("Hedge calculation for a parallel bump requires specification of the hedge security, 'matching' not allows");
            
            calcTenorHedge = calcHedge;
            if (Dt.Cmp(hedgeDate, Dt.Empty) != 0)
              calcTenorHedge = calcTenorHedge && (CurveUtil.FindClosestTenor(curves, hedgeDate) != null);

            // Compute using bumped curves
            logger.Debug("Computing using bumped curves");
            upTable = CalcIndividualBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, null, null, mtm, ignorePricers);
            if (calcTenorHedge)
            {
              if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeTenor);
              else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
              else
                for (int l = 0; l < hedgeMaturities.Length; ++l)
                  upHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
            }
            // Save results
            logger.Debug("Saving results");
            for (int i = 0; i < bumpedCurves.Length; i++)
            {

              for (int j = 0; j < evaluators.Length; j++)
              {
                if (evaluators[j].DependsOn(curves[i]))
                {
                  logger.DebugFormat(" Curve {0}, trade {1}, up = {2}, base = {3}",
                    bumpedCurves[i].Name, evaluators[j].Product.Description,
                    (upTable != null) ? upTable[i + 1, j] : 0.0,
                    (upTable != null) ? upTable[0, j] : 0.0);

                  DataRow row = dataTable.NewRow();
                  row["Category"] = bumpedCurves[i].Category;
                  row["Element"] = bumpedCurves[i].Name;
                  row["Pricer"] = evaluators[j].Product.Description;

                  double delta = CalcDelta(i, j, upTable, null, false, 0.0, 0.0);
                  row["Delta"] = delta;
                  
                  if (calcTenorHedge)
                  {
                    double hedgeDelta;
                    if (hedgeMaturities != null && hedgeMaturities.Length > 0)
                    {
                      hedgeDelta = CalcHedge(i, upHedge2[j], null, false, 0, 0);
                      row["Hedge Tenor"] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                    }
                    else
                    {
                      if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                      {
                        hedgeDelta = CalcHedge(i, upHedge, null, false, 0, 0);
                        row["Hedge Tenor"] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeDate)))).ToString(DefaultDateFormat);
                      }
                      else
                      {
                        hedgeDelta = CalcHedge(i, upHedge, null, false, 0, 0);
                        row["Hedge Tenor"] = hedgeTenor;
                      }
                    }
                    row["Hedge Delta"] = 1000000 * hedgeDelta;
                    row["Hedge Notional"] = (hedgeDelta != 0.0) ? delta / hedgeDelta : 0.0;
                  }
                  else
                  {
                    if (calcHedge)
                    {
                      //row[6] = System.DBNull.Value;
                      row["Hedge Delta"] = row["Hedge Notional"] = 0;
                    }
                  }
                  dataTable.Rows.Add(row);
                }
              } // pricers...

            } // curves...
            break;
          case BumpType.Uniform:
            // Make sure we use the right hedge. "matching" doesn't really make sense here.
            if (calcHedge && String.Compare(hedgeTenor, "matching") == 0)
              throw new ArgumentException("Hedge calculation for a parallel bump requires specification of the hedge security, 'matching' not allows");

            // Compute using bumped curves
            logger.Debug("Computing using bumped curves");
            upTable = CalcBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, null, null, mtm, ignorePricers);

            // Save results
            logger.Debug("Saving results");
            for (int j = 0; j < evaluators.Length; j++)
            {
              logger.DebugFormat(" Curve all, trade {0}, base = {1}, up = {2}",
                                  evaluators[j].Product.Description,
                                  (upTable != null) ? upTable[0, j] : 0.0,
                                  (upTable != null) ? upTable[1, j] : 0.0);

              DataRow row = dataTable.NewRow();
              row["Category"] = "all";
              row["Element"] = "all";
              row["Pricer"] = evaluators[j].Product.Description;

              double delta = CalcDelta(0, j, upTable, null, false, 0.0, 0.0);
              row["Delta"] = delta;
              dataTable.Rows.Add(row);
            } // pricers...
            break;
          default:
            throw new ArgumentOutOfRangeException("bumpType", bumpType, "This type of bump is not yet supported");
        } // switch...
      } // try...
      finally
      {
        // Restore what we may have changed
        CurveUtil.CurveSet(curves, savedCurves);

        //--- Mark pricers as needing recalculation
        PricerReset(evaluators);
      }

      timer.stop();
      logger.InfoFormat("Completed curve sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    #endregion Curve Sensitivity

    #region Backward Compatible
    /// <summary>
    ///   Calculate the sensitivity to a set of specified bumped curves for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes the delta between pricing using the base (original) curves and each of
    ///   specified perturbed curves. This is similar to the CurveSensitivity function excepting
    ///   that the bumped curves are specified rather than generated.</para>
    ///
    ///   <para>Equivalent to <see cref="Curve(IPricer[],string,CalibratedCurve[],CalibratedCurve[],BumpType,bool,string,DataTable)">
    ///   Curve(new IPricer[] {pricer}, measure, curves, bumpedCurves, bumpType, calcHedge, hedgeTenor, dataTable)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="curves">Array of curves for the base case or null for all curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    [Obsolete]
    public static DataTable
    Curve(
      IPricer pricer,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      BumpType bumpType,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable
      )
    {
      return Curve(CreateAdapters(pricer, null), curves, bumpedCurves, bumpType,
        calcHedge, hedgeTenor, dataTable);
    }

    /// <summary>
    ///   Calculate the sensitivity to a set of specified bumped curves for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes the delta between pricing using the base (original) curves and each of
    ///   specified perturbed curves. This is similar to the CurveSensitivity function excepting
    ///   that the bumped curves are specified rather than generated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Valid options include parallel, category or uniform bumping.</para>
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
    ///   <para>If the original curves are specified, a dependence is assumed to exist between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curves">Array of curves for the base case or null for all curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the curve sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate default event risk by name.
    ///   // To do this we first create a set of maching defaulted curves then call this method
    ///   // with the original curves and the defaulted curve sets.
    ///   //
    ///   // Create a copied set of defaulted curves
    ///   SurvivalCurve [] origSurvivalCurves = pricer.SurvivalCurves;
    ///   SurvivalCurve [] defaultedSurvivalCurves = new SurvivalCurve[origSurvivalCurves.Length];
    ///   for( int i = 0; i &lt; origSurvivalCurves.Length; i++ )
    ///   {
    ///     defaultedSurvivalCurves[i] = origSurvivalCurves[i].Clone();
    ///     defaultedSurvivalCurves[i].Defaulted = Defaulted.WillDefault;
    ///   }
    ///
    ///   // Compute sensitivities
    ///   DataTable dataTable = Sensitivities.Curve(
    ///    new IPricer[] { pricer },    // Pricer for CDO
    ///    origSurvivalCurves,          // Original survival curves to change
    ///    defaultedSurvivalCurves,     // Defaulted survival curves
    ///    BumpType.Parallel,           // Bumps are parallel
    ///    true,                        // Do hedge calculation
    ///    "5 Year",                    // Hedge to 5yr tenor
    ///    null                         // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Default Event Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    [Obsolete]
    public static DataTable
    Curve(
      IPricer[] pricers,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      BumpType bumpType,
      bool calcHedge,
      string hedgeTenor,
      DataTable dataTable
      )
    {
      return Curve(CreateAdapters(pricers, null), curves, bumpedCurves, bumpType,
        calcHedge, hedgeTenor, dataTable);
    }
    #endregion Backward Compatible

  } // class Sensitivities
}
