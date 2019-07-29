/*
 * Sensitivities.Recovery.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 *  Partial implementation of the generalized sensitivity functions
 * 
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;

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
    ///   Compute the recovery sensitivity.
    /// </summary>
    /// <remarks>
    ///   <para>The Recovery 01 is the change in PV (MTM) if the recovery rate assumptions
    ///   of the underlying credit curves are bumped up by one percent.</para>
    ///   <para>Generally, sensitivities are derivatives of the pricing function for a particular product with respect to some market data.
    ///   The first order sensitivity, or delta, is the partial first derivative of the pricing function and in the 
    ///   context of parameterized models it is often possible to explicitly calculate this derivative in closed form.  More 
    ///   commonly, especially for products with complex dependencies, this derivative must be approximated by finite difference methods.</para>
    ///   <para>Let <formula inline="true"> P(s,r,c) </formula> designate the price of a 
    ///   particular product as a function of its underlying spreads <formula inline="true"> (s)</formula>, recovery rates 
    ///   <formula inline="true"> (r)</formula>, and correlations <formula inline="true"> (c).</formula>  A finite difference approximation
    ///   is calculated as either a forward, backward, or central approximation depending on the "up" and "down" bump values.  
    ///   The function qRecovery01() computes the forward difference approximation for a unit "up" bump with respect to the value of the pricing function.
    ///   Delta, or the partial derivative of the pricing function with respect to recovery rates, is calculated as the difference between the re-priced product and the original product. More formally:
    ///   <formula>
    ///   \frac{\partial P}{\partial r} \approx \frac{P(s,r+u,c)-P(s,r,c)}{u}
    ///   </formula>
    ///   </para>
    /// </remarks>
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="rescaleStrikes">Boolean indicating resacle strikes or not for CDO Pricer</param>
    /// <returns>Recovery 01</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the recovery sensitivity for a <see cref="BaseEntity.Toolkit.Products.Bond">Corporate Bond</see>.</para>
    /// <code language="C#">
    ///   Bond pricer;
    ///
    ///   // Initialise bond, pricer and curves
    ///   // ...
    ///
    ///   // Calculate the recovery rate sensitivity of the fair spread
    ///   double recovery01 = Sensitivities.Recovery01(
    ///     pricer,             // Pricer for Bond
    ///     "Pv",               // Target measure
    ///     4.0,                // Based on 4bp up shift
    ///     0.0,                // No down shift
    ///     true,               // Recalibrate survival curves after bumping recovery rate
    ///    );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Recovery01 = {0}", recovery01 );
    /// </code>
    /// </example>
    public static double Recovery01(
      IPricer pricer,
      string measure,
      double upBump,
      double downBump,
      bool recalibrate,
      params bool[] rescaleStrikes
      )
    {
      return Recovery01(new PricerEvaluator(pricer, measure), upBump, downBump, recalibrate, rescaleStrikes);
    }

    /// <inheritdoc cref="Sensitivities.Recovery01(IPricer, string, double, double, bool, bool[])" />
    /// <param name="pricer">Pricer</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="rescaleStrikes">Boolean indicating resacle strikes or not for CDO pricer</param>
    public static double Recovery01(
      IPricer pricer,
      double upBump,
      double downBump,
      bool recalibrate,
      params bool[] rescaleStrikes
      )
    {
      return Recovery01(new PricerEvaluator(pricer), upBump, downBump, recalibrate, rescaleStrikes);
    }

    /// <inheritdoc cref="Sensitivities.Recovery01(IPricer, string, double, double, bool, bool[])" />
    /// <param name="evaluator">Pricer evaluator</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    private static double Recovery01(
      PricerEvaluator evaluator,
      double upBump,
      double downBump,
      bool recalibrate,
      params bool[] rescaleStrikes
      )
    {
      DataTable dataTable = Recovery(new PricerEvaluator[] { evaluator }, recalibrate,
        upBump, downBump, BumpType.Uniform, false, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Delta"];
    }

    #endregion SummaryRiskMethods

    #region Recovery Sensitivity

    /// <summary>
    ///   Compute the recovery rate sensitivity for a pricer.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>Equivalent to <see cref="Recovery(IPricer[],string,bool,double,double,BumpType,bool,DataTable, bool[])">
    ///   Recovery(new ISpread[] {pricer}, measure, recalibrate, upBump, downBump,
    ///   bumpType, calcGamma, dataTable, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Recovery(IPricer pricer, string measure, bool recalibrate, double upBump, double downBump,
      BumpType bumpType, bool calcGamma, DataTable dataTable, params bool[] rescaleStrikes)
    {
      return Recovery(CreateAdapters(pricer, measure), recalibrate,
        upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
    }

    
    /// <summary>
    ///   Compute the recovery rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The recovery curves for each survival curve are are bumped per
    ///   the parameters. If <paramref name="recalibrate"/> is true, the survival curves are refitted
    ///   and the pricers are recalculated.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the recovery curves are specified, an assumed dependence exists between
    ///   the pricers and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves and recovery curves
    ///   are maintained when the method is completed, even if an exception is thrown during
    ///   the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the recovery rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate recovery rate sensitivity.
    ///   // Here we are using parallel shifts of the recovery curve
    ///   //
    ///   DataTable dataTable = Sensitivities.Recovery( new IPricer[] { pricer },    // Pricer for CDO tranche
    ///                                                 "Pv",                        // Calculate change in PV
    ///                                                 true,                        // Recalibrate survival curves after bumping recovery rate
    ///                                                 0.01,                        // Based on 1pc up shift
    ///                                                 0.0,                         // No down shift
    ///                                                 BumpType.Parallel,           // Bumps are parallel
    ///                                                 false,                       // Dont bother with Gammas
    ///                                                 null                         // Create new table of results
    ///                                                );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Recovery sensitivity is {1}",
    ///                        (string)row["Element"], (double)row["Delta"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Recovery(
      IPricer[] pricers,
      string measure,
      bool recalibrate,
      double upBump,
      double downBump,
      BumpType bumpType,
      bool calcGamma,
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return Recovery(CreateAdapters(pricers, measure), recalibrate,
        upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Compute the recovery rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The recovery curves for each survival curve are are bumped per
    ///   the parameters. If <paramref name="recalibrate"/> is true, the survival curves are refitted
    ///   and the pricers are recalculated.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the recovery curves are specified, an assumed dependence exists between
    ///   the pricers and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves and recovery curves
    ///   are maintained when the method is completed, even if an exception is thrown during
    ///   the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    ///<param name="sensitivityMethod">Methodology for computation of sensitivities. If SensitivityMethod.SemiAnalytic is chosen, 
    /// recalibration of underlying survival probabilities is not yet supported</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Datatable of results</returns>

    public static DataTable Recovery(
      IPricer[] pricers,
      string measure,
      bool recalibrate,
      double upBump,
      double downBump,
      BumpType bumpType,
      bool calcGamma,
      SensitivityMethod sensitivityMethod,
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      bool semiAnalytic = (sensitivityMethod == SensitivityMethod.SemiAnalytic);
      if (semiAnalytic)
      {
        measure = (StringUtil.HasValue(measure)) ? measure.Trim() : "Pv";
        if (String.Compare(measure, "Pv", true) != 0)
          throw new ArgumentException("For the time being only semi-analytic sensitivities for the Pv are exposed");
        if (recalibrate)
          throw new ArgumentException("SemiAnalytic recovery deltas do not support recalibration of dependent curves");
        if (calcGamma)
          throw new ArgumentException("SemiAnalytic recovery gammas not supported");
        return CreditAnalyticSensitivities.SemiAnalyticRecoveryDelta(pricers, upBump, rescaleStrikes);
      }
      return Recovery(CreateAdapters(pricers, measure), recalibrate,
                      upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Compute the recovery rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The recovery curves for each survival curve are are bumped per
    ///   the parameters. If <paramref name="recalibrate"/> is true, the survival curves are refitted
    ///   and the pricers are recalculated.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the recovery curves are specified, an assumed dependence exists between
    ///   the pricers and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves and recovery curves
    ///   are maintained when the method is completed, even if an exception is thrown during
    ///   the calculation.</para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricers</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Recovery(
      PricerEvaluator[] evaluators,
      bool recalibrate,
      double upBump,
      double downBump,
      BumpType bumpType,
      bool calcGamma,
      DataTable dataTable
      )
    {
      double[,] upTable = null;
      double[,] downTable = null;

      Timer timer = new Timer();
      timer.start();

      logger.DebugFormat("Calculating recovery sensitivities up={0}, down={1}", upBump, downBump);

      // Validations
      if (upBump == -downBump)
        throw new ArgumentException("Up-bump size and down-bump size can not be equal.");

      bool[] ignorePricers = null;
      if (evaluators != null && evaluators.Length > 0)
      {
        ignorePricers = new bool[evaluators.Length]; // default all elements to false
        for (int j = 0; j < evaluators.Length; j++)
          ignorePricers[j] = !IsRecoverySensitivityApplicable(evaluators[j]);
      }

      // Get survival/recovery curves for these pricers
      List<Curve> sc = ignorePricers == null
        ? evaluators.GetRecoverySensitivityCurves(true) :
        evaluators.Where((p, i) => !ignorePricers[i]).ToArray().GetRecoverySensitivityCurves(true);
      sc.AddRange(evaluators.GetBasketUnsettledDefaults());

      Curve[] curves = sc.ToArray();

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = new DataTable("Recovery Rate Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));

      }

      if (evaluators == null || evaluators.Length == 0 || sc == null || sc.Count ==0)
      {
        timer.stop();
        logger.InfoFormat("Completed recovery sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }

      // Copy everything we are going to bump
      logger.Debug("Saving copy of curves we are going to bump");
      Curve[] savedCurves = CurveUtil.CurveCloneWithRecovery(curves);
      Curve[] bumpedCurves = null;

      // Compute the base case
      logger.Debug("Computing base cases");
      double[] mtm = null;

      // Compute sensitivities
      // Any errors are trapped and the curves restored.
      try
      {
        // DOTO: remove this quick hack
        for (int j = 0; j < evaluators.Length; ++j)
          evaluators[j].IncludeRecoverySensitivity = true;

        // Do bumps
        switch (bumpType)
        {
          #region Parallel
          case BumpType.Parallel:
            upTable = null;
            if (upBump != 0.0)
            {
              logger.Debug("Bumping all curves up");
              bumpedCurves = CurveUtil.CurveCloneWithRecovery(curves);
              bumpedCurves.BumpRecovery(upBump, recalibrate);
              PricerResetRecoveryRates(evaluators);
              upTable = CalcIndividualBumpedPvs(evaluators,
                curves, bumpedCurves, savedCurves, null, null, mtm);
              // restore recovery
              bumpedCurves.BumpRecovery(-upBump, false);
            }

            downTable = null;
            if (downBump != 0.0)
            {
              logger.Debug("Bumping all curves down");
              if (bumpedCurves == null)
                bumpedCurves = CurveUtil.CurveCloneWithRecovery(curves);
              if (upTable != null)
              {
                // In this case, base values have already been calculated
                mtm = new double[evaluators.Length];
                for (int j = 0; j < mtm.Length; ++j)
                  mtm[j] = upTable[0, j];
              }
              bumpedCurves.BumpRecovery(-downBump, recalibrate);
              PricerResetRecoveryRates(evaluators);
              downTable = CalcIndividualBumpedPvs(evaluators,
                curves, bumpedCurves, savedCurves, null, null, mtm);
              // restore recovery
              bumpedCurves.BumpRecovery(downBump, false);
            }

            // Save results
            for (int i = 0; i < curves.Length; ++i)
            {
              logger.DebugFormat("Saving results for curve {0}", curves[i].Name);
              for (int j = 0; j < evaluators.Length; j++)
              {
                if (evaluators[j].DependsOn(curves[i]))
                {
                  logger.DebugFormat(" Curve {0}, trade {1}, up = {2}, mid = {3}, down = {4}",
                    curves[i].Name, evaluators[j].Product.Description,
                    (upTable != null) ? upTable[i + 1, j] : 0.0,
                    (upTable != null) ? upTable[0, j] : downTable[0, j],
                    (downTable != null) ? downTable[i + 1, j] : 0.0);

                  DataRow row = dataTable.NewRow();
                  row["Category"] = curves[i].Category;
                  row["Element"] = curves[i].Name;
                  row["Pricer"] = evaluators[j].Product.Description;

                  double delta;
                  delta = CalcDelta(i, j, upTable, downTable, true, upBump * 100, downBump * 100);
                  row["Delta"] = delta;

                  if (calcGamma)
                    row["Gamma"] = CalcGamma(i, j, upTable, downTable, true, upBump * 100, downBump * 100);

                  dataTable.Rows.Add(row);
                } // tranches...
              } // curves
            }
            break;
          #endregion Parallel
          #region Uniform
          case BumpType.Uniform:
            logger.DebugFormat("Computing uniform recovery sensitivity");

            // Base value
            PricerResetRecoveryRates(evaluators);
            mtm = new double[evaluators.Length];
            for (int j = 0; j < evaluators.Length; j++)
              mtm[j] = evaluators[j].Evaluate();

            // Bump up
            if (upBump != 0.0)
            {
              // Bump up recovery
              logger.DebugFormat("Bumping up recovery by {0}", upBump);
              upTable = new double[2, evaluators.Length];
              curves.BumpRecovery(upBump, recalibrate);

              // Mark pricers as needing recalculation
              PricerResetRecoveryRates(evaluators);
              // Reprice
              for (int j = 0; j < evaluators.Length; j++)
              {
                upTable[0, j] = mtm[j];
                upTable[1, j] = evaluators[j].Evaluate();
                logger.DebugFormat("up value for {0} is {1}", evaluators[j].Product.Description, upTable[1, j]);
              }
              // Restore spread
              curves.BumpRecovery(-upBump, false);
            }

            // Bump down
            if (downBump != 0.0)
            {
              // Bump down recovery
              logger.DebugFormat("Bumping down recovery by {0}", downBump);
              downTable = new double[2, evaluators.Length];
              curves.BumpRecovery(-downBump, recalibrate);

              // Mark pricers as needing recalculation
              PricerResetRecoveryRates(evaluators);
              // Reprice
              for (int j = 0; j < evaluators.Length; j++)
              {
                downTable[0, j] = mtm[j];
                downTable[1, j] = evaluators[j].Evaluate();
                logger.DebugFormat("up value for {0} is {1}", evaluators[j].Product.Description, downTable[1, j]);
              }
              // Restore spread
              curves.BumpRecovery(downBump, false);
            }

            // Fit to restore curve if necessary
            if (recalibrate)
              curves.BumpRecovery(0.0, true);

            // Save results
            logger.DebugFormat("Saving results");
            for (int j = 0; j < evaluators.Length; j++)
            {
              logger.DebugFormat(" Curve all, trade {0}, up = {1}, mid = {2}, down = {3}",
                                  evaluators[j].Product.Description,
                                  (upTable != null) ? upTable[1, j] : 0.0,
                                  mtm[j],
                                  (downTable != null) ? downTable[1, j] : 0.0);

              DataRow row = dataTable.NewRow();
              row["Category"] = "all";
              row["Element"] = "all";
              row["Pricer"] = evaluators[j].Product.Description;

              double delta = CalcDelta(0, j, upTable, downTable, true, upBump * 100, downBump * 100);
              row["Delta"] = delta;

              if (calcGamma)
                row["Gamma"] = CalcGamma(0, j, upTable, downTable, true, upBump * 100, downBump * 100);

              dataTable.Rows.Add(row);
            } // tranches...

            break;
          #endregion Uniform
          default:
            throw new ArgumentException(String.Format("This type of bump ({0}) is not yet supported ", bumpType));
        }
      }
      finally
      {
        // Restore what we may have changed
        CurveUtil.CurveRestoreWithRecovery(curves, savedCurves);
        if (recalibrate)
          CurveUtil.CurveFit(curves);
        // Mark pricers as needing recalculation
        PricerResetRecoveryRates(evaluators);
      }

      timer.stop();
      logger.InfoFormat("Completed recovery sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    /// <summary>
    ///   Compute the recovery rate sensitivity for a series of pricers based on actual recovery rate changes.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The recovery curves for each survival curve are are bumped per
    ///   the parameters. If <paramref name="recalibrate"/> is true, the survival curves are refitted
    ///   and the pricers are recalculated.</para>
    ///
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the recovery curves are specified, an assumed dependence exists between
    ///   the pricers and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves and recovery curves
    ///   are maintained when the method is completed, even if an exception is thrown during
    ///   the calculation.</para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricers</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="recoveryBumps">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="scale">Scale the result</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Recovery(PricerEvaluator[] evaluators,
      bool recalibrate,
      IDictionary<string, double> recoveryBumps,
      bool scale,
      bool calcGamma,
      DataTable dataTable
      )
    {
      double[,] upTable = null;
      double[,] downTable = null;

      Timer timer = new Timer();
      timer.start();

      // Get survival/recovery curves for these pricers
      List<Curve> sc = evaluators.GetRecoverySensitivityCurves(true);
      Curve[] curves = sc.ToArray();
      double[] recoveryChanges = sc.Select(curve => recoveryBumps.ContainsKey(curve.Name) ? recoveryBumps[curve.Name] : 0.0).ToArray();
      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = new DataTable("Recovery Rate Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Category", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));

      }

      if (evaluators == null || evaluators.Length == 0)
      {
        timer.stop();
        logger.InfoFormat("Completed recovery sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }

      // Copy everything we are going to bump
      Curve[] savedCurves = CurveUtil.CurveCloneWithRecovery(curves);

      // Compute the base case
      logger.Debug("Computing base cases");
      double[] mtm = null;

      // Compute sensitivities
      // Any errors are trapped and the curves restored.
      try
      {
        // DOTO: remove this quick hack
        for (int j = 0; j < evaluators.Length; ++j)
          evaluators[j].IncludeRecoverySensitivity = true;

        // Do bumps
        {
             // Base value
            PricerResetRecoveryRates(evaluators);
            mtm = new double[evaluators.Length];
            for (int j = 0; j < evaluators.Length; j++)
              mtm[j] = evaluators[j].Evaluate();

            // Bump up
            if (recoveryChanges.Any(bumpAmt => !bumpAmt.AlmostEquals(0.0)))
            {
              // Bump up recovery
              upTable = new double[2, evaluators.Length];
              curves.BumpRecovery(recoveryChanges, recalibrate);

              // Mark pricers as needing recalculation
              PricerResetRecoveryRates(evaluators);
              // Reprice
              for (int j = 0; j < evaluators.Length; j++)
              {
                upTable[0, j] = mtm[j];
                upTable[1, j] = evaluators[j].Evaluate();
                logger.DebugFormat("up value for {0} is {1}", evaluators[j].Product.Description, upTable[1, j]);
              }
              // Restore spread
              curves.BumpRecovery(ArrayUtil.Convert(recoveryChanges, upBump => -upBump), false);
            }

            // Fit to restore curve if necessary
            if (recalibrate)
              curves.BumpRecovery(0.0, true);

            // Save results
            logger.DebugFormat("Saving results");
            for (int j = 0; j < evaluators.Length; j++)
            {
              logger.DebugFormat(" Curve all, trade {0}, up = {1}, mid = {2}, down = {3}",
                                  evaluators[j].Product.Description,
                                  (upTable != null) ? upTable[1, j] : 0.0,
                                  mtm[j],
                                  (downTable != null) ? downTable[1, j] : 0.0);

              DataRow row = dataTable.NewRow();
              row["Category"] = "all";
              row["Element"] = "all";
              row["Pricer"] = evaluators[j].Product.Description;

              double delta = CalcDelta(0, j, upTable, null, scale, recoveryChanges.Average() * 100, 0);
              row["Delta"] = delta;

              if (calcGamma)
                row["Gamma"] = CalcGamma(0, j, upTable, null, scale, recoveryChanges.Average() * 100, 0);

              dataTable.Rows.Add(row);
            }  


        }
      }
      finally
      {
        // Restore what we may have changed
        CurveUtil.CurveRestoreWithRecovery(curves, savedCurves);
        if (recalibrate)
          CurveUtil.CurveFit(curves);
        // Mark pricers as needing recalculation
        PricerResetRecoveryRates(evaluators);
      }

      timer.stop();
      logger.InfoFormat("Completed recovery sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    #endregion // Recovery_Sensitivity

    #region Backward Compatible
    /// <summary>
    ///   Compute the recovery rate sensitivity for a pricer.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>Equivalent to <see cref="Recovery(IPricer[],string,bool,double,double,BumpType,bool,DataTable, bool[])">
    ///   Recovery(new ISpread[] {pricer}, measure, recalibrate, upBump, downBump,
    ///   bumpType, calcGamma, dataTable, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Recovery(IPricer pricer, bool recalibrate, double upBump, double downBump,
      BumpType bumpType, bool calcGamma, DataTable dataTable, params bool[] rescaleStrikes)
    {
      return Recovery(CreateAdapters(pricer, null), recalibrate,
        upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Compute the recovery rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical recovery sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    ///
    ///   <para>The recovery curves for each survival curve are are bumped per
    ///   the parameters. If <paramref name="recalibrate"/> is true, the survival curves are refitted
    ///   and the pricers are recalculated.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the recovery curves are specified, an assumed dependence exists between
    ///   the pricers and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the survival curves and recovery curves
    ///   are maintained when the method is completed, even if an exception is thrown during
    ///   the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the recovery rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate recovery rate sensitivity.
    ///   // Here we are using parallel shifts of the recovery curve
    ///   //
    ///   DataTable dataTable = Sensitivities.Recovery( new IPricer[] { pricer },    // Pricer for CDO tranche
    ///                                                 "Pv",                        // Calculate change in PV
    ///                                                 true,                        // Recalibrate survival curves after bumping recovery rate
    ///                                                 0.01,                        // Based on 1pc up shift
    ///                                                 0.0,                         // No down shift
    ///                                                 BumpType.Parallel,           // Bumps are parallel
    ///                                                 false,                       // Dont bother with Gammas
    ///                                                 null                         // Create new table of results
    ///                                                );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Recovery sensitivity is {1}",
    ///                        (string)row["Element"], (double)row["Delta"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Recovery(
      IPricer[] pricers,
      bool recalibrate,
      double upBump,
      double downBump,
      BumpType bumpType,
      bool calcGamma,
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return Recovery(CreateAdapters(pricers, null), recalibrate,
              upBump, downBump, bumpType, calcGamma, dataTable, rescaleStrikes);
    }
    
    /// <summary>
    ///  Wrapper method between Recovery(IPricer,...) and Recovery(PricerEvalator, ...) 
    ///  Its function is to set and restore rescale strikes for CDO pricers
    /// </summary>
    /// <param name="evaluators">Array of pricers</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns></returns>
    public static DataTable Recovery(PricerEvaluator[] evaluators, bool recalibrate, double upBump, 
      double downBump, BumpType bumpType, bool calcGamma, DataTable dataTable, bool[] rescaleStrikes)
    {
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikes);
      DataTable dt = null;
      try
      {
        dt = Recovery(evaluators, recalibrate, upBump, downBump, bumpType, calcGamma, dataTable);
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }
      return dt;
    }

    /// <summary>
    ///  Wrapper method between Recovery(IPricer,...) and Recovery(PricerEvalator, ...) 
    ///  Its function is to set and restore rescale strikes for CDO pricers
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="riskMeasure">Risk measure </param>
    /// <param name="recalibrate">Recalibrate survival curves after changing recovery rates</param>
    /// <param name="recoveryChanges">Bump sizes in percent (ie .05 is 5 percent)</param>
    /// <param name="scale">Scale the result</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns></returns>
    public static double[] Recovery(IPricer[] pricers, string riskMeasure, bool recalibrate, IDictionary<string, double> recoveryChanges,
      bool scale, bool calcGamma, DataTable dataTable, bool[] rescaleStrikes)
    {
      PricerEvaluator[] evaluators = CreateAdapters(pricers, riskMeasure);
      bool[] rescaleStrikesSaved = ResetRescaleStrikes(evaluators, rescaleStrikes);
      DataTable dt = null;
      try
      {
        dt = Recovery(evaluators, recalibrate, recoveryChanges, scale, calcGamma, dataTable);
      }
      finally
      {
        ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }

      var retVal = new double[dt.Rows.Count];
      int rowIdx = 0;
      foreach (DataRow row in dt.Rows)
      {
        retVal[rowIdx++] = (double)row["Delta"];
      }
      return retVal;
    }

    #endregion Backward Compatible

  } // class Sensitivities

}
