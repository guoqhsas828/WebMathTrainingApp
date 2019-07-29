//
// Sensitivities.Correlation.cs
// Partial implementation of the correlation sensitivity functions
//  -2014. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// </summary>
  //Compute the base correlation sensitivity for a series of CDO pricers
  public static partial class Sensitivities
  {
    #region SummaryRiskMethods

    /// <summary>
    ///   Compute the correlation sensitivity.
    /// </summary>
    /// <remarks>
    ///   <para>The Correlation 01 is the change in PV (MTM) if the correlation assumptions
    ///   of the underlying credit curves are bumped up by one percent.</para>
    ///   <para>Generally, sensitivities are derivatives of the pricing function for a particular product with respect to some market data.
    ///   The first order sensitivity, or delta, is the partial first derivative of the pricing function and in the 
    ///   context of parameterized models it is often possible to explicitly calculate this derivative in closed form.  More 
    ///   commonly, especially for products with complex dependencies, this derivative must be approximated by finite difference methods.</para>
    ///   <para>Let <m> P(s,r,c) </m> designate the price of a 
    ///   particular product as a function of its underlying spreads <m> (s)</m>, recovery rates 
    ///   <m> (r)</m>, and correlations <m> (c).</m>  A finite difference approximation
    ///   is calculated as either a forward, backward, or central approximation depending on the "up" and "down" bump values.  
    ///   The function qCorrelation01() computes the forward difference approximation for a unit "up" bump with respect to the value of the pricing function.
    ///   Delta, or the partial derivative of the pricing function with respect to correlation, is calculated as the difference between the re-priced product and the original product. More formally:
    ///   <formula>
    ///   \frac{\partial P}{\partial c} \approx \frac{P(s,r,c+u)-P(s,r,c)}{u}
    ///   </formula>
    ///   </para>
    ///   <note>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on correlations.</note>
    /// </remarks>
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns>Correlation 01</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the correlation sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate the correlation sensitivity of the fair spread
    ///   double correlation01 = Sensitivities.Correlation01(
    ///     pricer,             // Pricer for CDO tranche
    ///     "BreakEvenPremium", // Target measure
    ///     0.01,               // Based on 1pc up shift
    ///     0.0,                // No down shift
    ///    );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Correlation01 = {0}", correlation01 );
    /// </code>
    /// </example>
    public static double Correlation01(IPricer pricer, string measure, double upBump, double downBump)
    {
      var dataTable = Correlation(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        upBump, downBump, false, true,
        BumpType.Uniform, false, false, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <inheritdoc cref="Toolkit.Sensitivity.Sensitivities.Correlation01(BaseEntity.Toolkit.Pricers.IPricer, string, double, double)" />
    /// <param name="pricer">Pricer</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    public static double Correlation01(IPricer pricer, double upBump, double downBump)
    {
      var dataTable = Correlation(new PricerEvaluator[] { new PricerEvaluator(pricer) },
        upBump, downBump, false, true,
        BumpType.Uniform, false, false, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <inheritdoc cref="Toolkit.Sensitivity.Sensitivities.Correlation01(BaseEntity.Toolkit.Pricers.IPricer, string, double, double)" />
    /// <param name="evaluator">Pricer evaluator</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    private static double Correlation01( PricerEvaluator evaluator, double upBump, double downBump )
    {
      var dataTable = Correlation(new PricerEvaluator[] { evaluator },
        upBump, downBump, false, true,
        BumpType.Uniform, false, false, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    #endregion SummaryRiskMethods

    #region Correlation Sensitivity

    /// <inheritdoc cref="Toolkit.Sensitivity.Sensitivities.Correlation(PricerEvaluator[], double, double, bool, bool, BumpType, bool, bool, DataTable)" />
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="bumpFactors">Bump betas rather than correlations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    public static DataTable Correlation(
      IPricer pricer,
      string measure,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      bool calcGamma,
      bool bumpFactors,
      DataTable dataTable
      )
    {
      return Correlation(CreateAdapters(pricer, measure), upBump, downBump, bumpRelative,
        scaledDelta, bumpType, calcGamma, bumpFactors, dataTable);
    }

    /// <inheritdoc cref="Toolkit.Sensitivity.Sensitivities.Correlation(PricerEvaluator[], double, double, bool, bool, BumpType, bool, bool, DataTable)" />
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="bumpFactors">Bump betas rather than correlations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    public static DataTable Correlation(
      IPricer[] pricers,
      string measure,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      bool calcGamma,
      bool bumpFactors,
      DataTable dataTable
      )
    {
      return Correlation(CreateAdapters(pricers, measure), upBump, downBump, bumpRelative,
        scaledDelta, bumpType, calcGamma, bumpFactors, dataTable);
    }

    /// <summary>
    ///   Compute the correlation sensitivity for a series of pricers
    /// </summary>
    /// <remarks>
    ///   <para>Computes numerical correlation sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on correlations.</para>
    ///   <para>The correlations are bumped per the parameters and the pricers
    ///   are recalculated.</para>
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be absolute.</para>
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed.</para>
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of tenor if applicable</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///   <para>Care is taken to ensure that the state of the correlations are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="bumpFactors">Bump betas rather than correlations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the correlation sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate correlation sensitivity.
    ///   // Here we are using parallel relative shifts of the correlations
    ///   //
    ///   DataTable dataTable = Sensitivities.Correlation( new IPricer[] { pricer },    // Pricer for CDO tranche
    ///                                                    0.10,                        // Based on 10 percent up shift
    ///                                                    0.0,                         // No down shift
    ///                                                    true,                        // Interpret shifts as relative
    ///                                                    BumpType.Parallel,           // Bumps are parallel
    ///                                                    null,                        // Bump all correlations
    ///                                                    false,                       // Dont bother with Gammas
    ///                                                    false,                       // Bump correlations rather than factors
    ///                                                    null                         // Create new table of results
    ///                                                   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Correlation for {0}, sensitivity is {1}",
    ///                        (string)row["Element"], (double)row["Delta"] );
    ///   }
    /// </code>
    /// </example>
    private static DataTable Correlation(
      PricerEvaluator[] pricers,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      bool calcGamma,
      bool bumpFactors,
      DataTable dataTable
      )
    {
      bool[] calculated = new bool[pricers.Length];
      for (int i = 0; i < calculated.Length; ++i)
        if (!calculated[i])
        {
          List<PricerEvaluator> list = new List<PricerEvaluator>();
          list.Add(pricers[i]);
          calculated[i] = true;
          BasketPricer basket = pricers[i].Basket;
          for (int j = i + 1; j < calculated.Length; ++j)
          {
            if (!calculated[j] && pricers[j].Basket == basket)
            {
              list.Add(pricers[j]);
              calculated[j] = true;
            }
          }
          dataTable = DoCorrelation(list.ToArray(), upBump, downBump, bumpRelative,
            scaledDelta, bumpType, calcGamma, bumpFactors, dataTable);
        }
      if (dataTable == null)
      {
        dataTable = new DataTable("Correlation Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Curve Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
      }
      return dataTable;
    }

    private static DataTable DoCorrelation(
      PricerEvaluator[] pricers,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      bool calcGamma,
      bool bumpFactors,
      DataTable dataTable
      )
    {
      // Validations
      if (upBump == -downBump)
        throw new ArgumentException("Up-bump size and down-bump size can not be equal.");

      logger.DebugFormat("Calculating correlation sensitivities up={0}, down={1}", upBump, downBump);

      Timer timer = new Timer();
      timer.start();

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = new DataTable("Correlation Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Element", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Curve Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
      }

      if (pricers == null || pricers.Length == 0)
      {
        timer.stop();
        logger.InfoFormat("Completed correlation sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }

      // Get correlation
      if (bumpType == BumpType.Parallel)
        pricers = TransformSingleFactorCorrelation(pricers);
      CorrelationObject[] corrs = PricerEvaluatorUtil.GetCorrelations(pricers).ToArray();
      CorrelationObject[] savedCorrelations = CloneUtil.Clone(corrs);
      foreach (CorrelationObject correlation in corrs)
      {
        double[,] upTable = null;
        double[,] downTable = null;
        double avgUpBump = 0.0, avgDownBump = 0.0;

        // compute the base case
        logger.Debug("Computing base cases");
        double[] mtm = new double[pricers.Length];
        PricerReset(pricers);
        for (int j = 0; j < pricers.Length; j++)
          mtm[j] = pricers[j].Evaluate();

        // Copy everything we are going to bump
        logger.Debug("Saving copy of correlations we are going to bump");

        // Backward compatibility hacks
        if (bumpType == BumpType.Parallel && (correlation is ICorrelationBumpTermStruct) &&
          (correlation is BaseCorrelationObject))
        {
          bumpType = BumpType.ByTenor;
        }

        // Compute sensitivities
        // Any errors are trapped and the correlations restored
        correlation.Modified = true; // tell pricers to recalculate correlation-related values
        try
        {
          switch (bumpType)
          {
            case BumpType.ByTenor:
              // Bump all correlations simultaneous for each tenor
              if (!(correlation is ICorrelationBumpTermStruct))
                throw new ArgumentException("Correlation must be a term structure to use BumpTenor");
              ICorrelationBumpTermStruct tsCorr = (ICorrelationBumpTermStruct)correlation;
              Dt[] dates = tsCorr.Dates;
              int tenorCount = dates.Length;
              // Bump each tenor
              for (int t = 0; t < tenorCount; ++t)
              {
                upTable = null;
                downTable = null;

                logger.DebugFormat("Computing correlation sensitivity for strike #{0} ({1})", t, dates[t]);

                // Bump up
                avgUpBump = 0.0;
                if (upBump != 0.0)
                {
                  // Bump up correlations
                  logger.DebugFormat("Bumping up tenor #{0} by {1}", t, upBump);
                  upTable = new double[2, pricers.Length];
                  avgUpBump = tsCorr.BumpTenor(t, upBump, bumpRelative, bumpFactors);
                  // Reprice
                  PricerResetCorrelations(pricers);
                  for (int j = 0; j < pricers.Length; j++)
                  {
                    upTable[0, j] = mtm[j];
                    upTable[1, j] = pricers[j].Evaluate();
                    logger.DebugFormat("up value for {0} is {1}", pricers[j].Product.Description, upTable[1, j]);
                  }
                  // Restore correlations
                  tsCorr.BumpTenor(t, -upBump, bumpRelative, bumpFactors);
                }

                // Bump down
                avgDownBump = 0.0;
                if (downBump != 0.0)
                {
                  // Bump down correlations
                  logger.DebugFormat("Bumping down tenor {0} on by {1}", dates[t], downBump);
                  downTable = new double[2, pricers.Length];
                  avgDownBump = -tsCorr.BumpTenor(t, -downBump, bumpRelative, bumpFactors);
                  // Mark pricers as needing recalculation
                  PricerResetCorrelations(pricers);
                  for (int j = 0; j < pricers.Length; j++)
                  {
                    downTable[0, j] = mtm[j];
                    downTable[1, j] = pricers[j].Evaluate();
                    logger.DebugFormat("up value for {0} is {1}", pricers[j].Product.Description, downTable[1, j]);
                  }
                  // Restore correlations
                  tsCorr.BumpTenor(t, downBump, bumpRelative, bumpFactors);
                }

                // Save results
                logger.DebugFormat("Saving results for tenor #{0} ({1})", t, dates[t]);
                for (int j = 0; j < pricers.Length; j++)
                {
                  logger.DebugFormat(" Tenor {0}, trade {1}, up = {2}, mid = {3}, down = {4}",
                                      dates[t], pricers[j].Product.Description,
                                      (upTable != null) ? upTable[1, j] : 0.0,
                                      mtm[j],
                                      (downTable != null) ? downTable[1, j] : 0.0);

                  DataRow row = dataTable.NewRow();
                  row["Element"] = "all";
                  row["Curve Tenor"] = String.Format("{0}", dates[t]);
                  row["Pricer"] = pricers[j].Product.Description;

                  double delta;
                  delta = CalcDelta(0, j, upTable, downTable, scaledDelta, avgUpBump * 100.0, avgDownBump * 100.0);
                  row["Delta"] = delta;

                  if (calcGamma)
                    row["Gamma"] = CalcGamma(0, j, upTable, downTable, scaledDelta, avgUpBump * 100.0, avgDownBump * 100.0);
                  dataTable.Rows.Add(row);
                }
              }
              break;

            case BumpType.Parallel:
              // Bump correlations by name/strike, simultaneous for all tenors
              if (!(correlation is ICorrelationBump))
                throw new ArgumentException("Correlation cannot use Bump Type Parallel");
              ICorrelationBump corr = (ICorrelationBump)correlation;
              int nameCount = corr.NameCount;
              // Bump each name
              for (int i = 0; i < nameCount; ++i)
              {
                string name = corr.GetName(i);

                upTable = null;
                downTable = null;

                logger.DebugFormat("Computing correlation sensitivity for {0}", correlation.Name);

                // Bump up
                avgUpBump = 0.0;
                if (upBump != 0.0)
                {
                  // Bump up correlations
                  logger.DebugFormat("Bumping up all strikes by {0}", upBump);
                  upTable = new double[2, pricers.Length];
                  avgUpBump = corr.BumpCorrelations(i, upBump, bumpRelative, bumpFactors);
                  // Reprice
                  PricerResetCorrelations(pricers);
                  for (int j = 0; j < pricers.Length; j++)
                  {
                    upTable[0, j] = mtm[j];
                    upTable[1, j] = pricers[j].Evaluate();
                    logger.DebugFormat("up value for {0} is {1}", pricers[j].Product.Description, upTable[1, j]);
                  }
                  // Restore correlations
                  corr.BumpCorrelations(i, -upBump, bumpRelative, bumpFactors);
                }

                // Bump down
                avgDownBump = 0.0;
                if (downBump != 0.0)
                {
                  // Bump down correlations
                  logger.DebugFormat("Bumping down all strikes by {0}", downBump);
                  downTable = new double[2, pricers.Length];
                  avgDownBump = -corr.BumpCorrelations(i, -downBump, bumpRelative, bumpFactors);
                  // Mark pricers as needing recalculation
                  PricerResetCorrelations(pricers);
                  for (int j = 0; j < pricers.Length; j++)
                  {
                    downTable[0, j] = mtm[j];
                    downTable[1, j] = pricers[j].Evaluate();
                    logger.DebugFormat("up value for {0} is {1}", pricers[j].Product.Description, downTable[1, j]);
                  }
                  // Restore correlations
                  corr.BumpCorrelations(i, downBump, bumpRelative, bumpFactors);
                }

                // Save results
                logger.DebugFormat("Saving results for {0}", name);
                for (int j = 0; j < pricers.Length; j++)
                {
                  logger.DebugFormat(" Item {0}, Bump all tenors, trade {1}, up = {2}, mid = {3}, down = {4}",
                                      name, pricers[j].Product.Description,
                                      (upTable != null) ? upTable[1, j] : 0.0,
                                      mtm[j],
                                      (downTable != null) ? downTable[1, j] : 0.0);

                  DataRow row = dataTable.NewRow();
                  row["Element"] = name;
                  row["Curve Tenor"] = "all";
                  row["Pricer"] = pricers[j].Product.Description;

                  double delta;
                  delta = CalcDelta(0, j, upTable, downTable, scaledDelta, avgUpBump * 100.0, avgDownBump * 100.0);
                  row["Delta"] = delta;

                  if (calcGamma)
                    row["Gamma"] = CalcGamma(0, j, upTable, downTable, scaledDelta, avgUpBump * 100.0, avgDownBump * 100.0);
                  dataTable.Rows.Add(row);
                }
              }
              break;

            case BumpType.Uniform:
              logger.Debug("Bumping all correlations uniformly");

              // Bump up
              avgUpBump = 0.0;
              if (upBump != 0.0)
              {
                // Bump up correlations
                logger.DebugFormat("Bumping up correlations by {0}", upBump);
                upTable = new double[2, pricers.Length];
                avgUpBump = correlation.BumpCorrelations(upBump, bumpRelative, bumpFactors);
                // Reprice
                PricerResetCorrelations(pricers);
                for (int j = 0; j < pricers.Length; j++)
                {
                  upTable[0, j] = mtm[j];
                  upTable[1, j] = pricers[j].Evaluate();
                  logger.DebugFormat("up value for {0} is {1}", pricers[j].Product.Description, upTable[1, j]);
                }
                // Restore correlations
                correlation.BumpCorrelations(-upBump, bumpRelative, bumpFactors);
              }

              // Bump down
              avgDownBump = 0.0;
              if (downBump != 0.0)
              {
                // Bump down correlations
                logger.DebugFormat("Bumping down correlation by {0}", downBump);
                downTable = new double[2, pricers.Length];
                avgDownBump = -correlation.BumpCorrelations(-downBump, bumpRelative, bumpFactors);
                // Reprice
                PricerResetCorrelations(pricers);
                for (int j = 0; j < pricers.Length; j++)
                {
                  downTable[0, j] = mtm[j];
                  downTable[1, j] = pricers[j].Evaluate();
                  logger.DebugFormat("up value for {0} is {1}", pricers[j].Product.Description, downTable[1, j]);
                }
                // Restore correlations
                correlation.BumpCorrelations(downBump, bumpRelative, bumpFactors);
              }

              // Save results
              logger.Debug("Saving results");
              for (int j = 0; j < pricers.Length; j++)
              {
                logger.DebugFormat(" Strike all, trade {0}, up = {1}, mid = {2}, down = {3}",
                                    pricers[j].Product.Description,
                                    (upTable != null) ? upTable[1, j] : 0.0,
                                    mtm[j],
                                    (downTable != null) ? downTable[1, j] : 0.0);

                DataRow row = dataTable.NewRow();
                row["Element"] = correlation.Name;
                row["Curve Tenor"] = "all";
                row["Pricer"] = pricers[j].Product.Description;

                double delta;
                delta = CalcDelta(0, j, upTable, downTable, scaledDelta, avgUpBump * 100.0, avgDownBump * 100.0);
                row["Delta"] = delta;

                if (calcGamma)
                  row["Gamma"] = CalcGamma(0, j, upTable, downTable, scaledDelta, avgUpBump * 100.0, avgDownBump * 100.0);

                dataTable.Rows.Add(row);
              }

              break;
            default:
              throw new ArgumentOutOfRangeException("bumpType", bumpType, "This type of bump is not yet supported ");
          }
        }
        finally
        {
          // Restore correlations
          RestoreCorrelation(corrs, savedCorrelations);
          // Mark pricers as needing recalculation
          PricerResetCorrelations(pricers);
        }
      }

      timer.stop();
      logger.InfoFormat("Completed correlation sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    /// <summary>
    ///   Tranfordm single factor correlation to an equivalent factor correlation
    ///   such that the paralell bump gives the right deltas
    /// </summary>
    /// <param name="pricers">Input pricer evaluators</param>
    /// <returns>Output pricer evaluators</returns>
    private static PricerEvaluator[] TransformSingleFactorCorrelation(
      PricerEvaluator[] pricers)
    {
      if (pricers==null)
        return null;
      bool hasSingleFactorCorrelation = false;
      for (int i = 0; i < pricers.Length; ++i)
      {
        SyntheticCDOPricer p = pricers[i].Pricer as SyntheticCDOPricer;
        if (p!=null && (p.Basket is SemiAnalyticBasketPricer)
          && p.Basket.Correlation is SingleFactorCorrelation)
        {
          hasSingleFactorCorrelation = true;
          break;
        }
      }
      if (!hasSingleFactorCorrelation)
        return pricers;
      PricerEvaluator[] tmp = new PricerEvaluator[pricers.Length];
      for (int i = 0; i < pricers.Length; ++i)
        if (tmp[i]==null)
        {
          SyntheticCDOPricer p = pricers[i].Pricer as SyntheticCDOPricer;
          if (p == null || !(p.Basket is SemiAnalyticBasketPricer)
            || !(p.Basket.Correlation is SingleFactorCorrelation))
          {
            tmp[i] = pricers[i];
            continue;
          }
          BasketPricer origBasket = p.Basket;
          FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation(
            (Correlation)p.Basket.Correlation);
          BasketPricer basket = origBasket.Substitute(origBasket.SurvivalCurves,
            null,origBasket.Principals,corr,origBasket.LossLevels);
          tmp[i] = pricers[i].Substitute(
            new SyntheticCDOPricer(p.CDO,basket,p.DiscountCurve,p.Notional,p.RateResets));
          for (int j = i + 1; j < pricers.Length;++j)
          {
            if (tmp[j] != null)
              continue;
            p = pricers[j].Pricer as SyntheticCDOPricer;
            if (p == null || p.Basket != origBasket)
              continue;
            tmp[j] = pricers[j].Substitute(
              new SyntheticCDOPricer(p.CDO, basket, p.DiscountCurve, p.Notional,p.RateResets));
          }
        }
      return tmp;
    }
    #endregion Correlation Sensitivity

    #region BaseCorrelation Sensitivity

    /// <summary>
    ///   Calculate deltas due to base correlation changes
    /// </summary>
    /// 
    /// <param name="pricer">CDO tranche pricer</param>
    /// <param name="measures">Price measures to calculate deltas for.
    /// They must be the names of the evaluation methods defined in
    /// SyntheticCDOPricer, such as Pv, FeePv, ExpectedLoss, etc..</param>
    /// <param name="bumpComponents">Array of names of the selected
    /// components to bump.  This parameter applies to mixed base
    /// correlation objects and it is ignored for non-mixed single
    /// object.  A null value means bump all components.</param>
    /// <param name="bumpTenorDates">Array of the selected tenor dates
    /// to bump.  This parameter applies to base correlation term structures
    /// and it is ignored for simple base correlation without term structure.
    /// A null value means bump all tenors.</param>
    /// <param name="bumpDetachments">Array of the selected detachment points
    /// to bump.  This should be an array of detachments points associated
    /// with the strikes.  A null value means to bump the correlations
    /// at all strikes.</param>
    /// <param name="bumpSizes">Array of the bump sizes applied to
    /// the correlations on the selected detachment points. If the array
    /// is null or empty, no bump is performed.  Else if it contains only
    /// a single element, the element is applied to all detachment points.
    /// Otherwise, the array is required to have the same size as the array of
    /// detachments.</param>
    /// <param name="bumpQuotes">Bump on market quotes instead of on correlations</param>
    /// <param name="relative">Boolean value indicating if a relative bump is required.</param>
    /// <param name="scale">Scaling factor (0 for no scling, 100 for bumping on correlation and upfront fee,
    ///   10000 for bumping spreads).</param>
    /// 
    /// <returns>Array of deltas for the price measures</returns>
    /// 
    /// <exception cref="ArgumentException">
    ///   <para>(1) The pricer contains no base correlation object.</para>
    ///   <para>(1) No price measure is specifief.</para>
    /// </exception>
    /// 
    /// <example>
    ///   The following codes calculate impacts on Value values and expect losses
    ///   for 1% bump on the correlation at detachment 3%, and 0.5% bump on the
    ///   correlation at detachment 7%.
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo and pricer
    ///   // ...
    ///
    ///   // Calculate CDO base correlation sensitivity.
    ///   //
    ///   double[] deltas = Sensitivities.BaseCorrelation01(
    ///     pricer,                               // CDO tranche pricer
    ///     new string[]{ "Pv", "ExpectedLoss" }, // Price measures
    ///     null,                                 // Bump all components
    ///     null,                                 // Bump all tenors
    ///     new double[]{ 0.03, 0.07 },           // Bump 3% and 7% points
    ///     new double[]{ 0.01, 0.005 },          // Bump sizes
    ///     false,                                // Bump absolute values
    ///     false                                 // Don't scale the deltas
    ///   );
    ///   double deltaMtM = deltas[0];   // the change in Value value
    ///   double deltaEL = deltas[1];    // the change in expected loss
    /// </code>
    /// </example>
    public static double[] BaseCorrelation01(
      SyntheticCDOPricer pricer,
      string[] measures,
      string[] bumpComponents,
      Dt[] bumpTenorDates,
      double[] bumpDetachments,
      BumpSize[] bumpSizes,
      bool bumpQuotes,
      bool relative,
      double scale)
    {
      // Sanity check
      if (!(pricer.Basket.Correlation is BaseCorrelationObject))
        throw new ArgumentException("Pricer does not contain base correlation object");
      if (measures == null || measures.Length == 0)
        throw new ArgumentOutOfRangeException("measures", "No price measure specified");

      // Storage for results
      double[] result = new double[measures.Length];

      // Get a bumped correlation object
      BaseCorrelationObject altBaseCorrelation = (BaseCorrelationObject)
        CloneUtil.CloneObjectGraph(pricer.Basket.Correlation);
      altBaseCorrelation.EntityNames = null; // do not check by name dictionary
      double avgBump = altBaseCorrelation.BumpCorrelations(
        bumpComponents, bumpTenorDates, bumpDetachments, bumpSizes, null,
        relative, bumpQuotes, null);
      if (Math.Abs(avgBump) < 1E-9)
        return result; // Nothing bumped.

      // Create pricer evaluators and calculate base values
      PricerEvaluator[] evaluators = new PricerEvaluator[measures.Length];
      for (int i = 0; i < measures.Length; ++i)
      {
        evaluators[i] = new PricerEvaluator(pricer, measures[i]);
        result[i] = evaluators[i].Evaluate();
      }

      // Calculate bumped values
      double[] bumpEvals = BumpedEval(evaluators, altBaseCorrelation);
      for (int i = 0; i < measures.Length; ++i)
        result[i] = bumpEvals[i] - result[i];
      if (scale != 0.0)
      {
        for (int i = 0; i < measures.Length; ++i)
          result[i] /= (avgBump * scale);
      }

      // return the result
      return result;
    }

    /// <summary>
    ///   Compute the base correlation sensitivity for a series of CDO pricers
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical base correlation sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on correlations.</para>
    ///
    ///   <para>The correlations are bumped per the parameters and the pricers
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be absolute.</para>
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
    ///     <item><term>Surface</term><description>Name of inner component</description></item>
    ///     <item><term>Tenor Date</term><description>Maturity date of the Base correlation skew</description></item>
    ///     <item><term>Detachment</term><description>Detachment point of the base tranche</description></item>
    ///     <item><term>Measure</term><description>Name of the price measure</description></item>
    ///     <item><term>Pricer</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the correlations are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of CDO pricers</param>
    /// <param name="measures">Price measures to evaluate</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpUnit">Bump unit (Natural, Percentage, BasisPoint, or None).</param>
    /// <param name="bumpComponents">Array of names of the selected components to bump.
    ///   This parameter applies to mixed base correlation objects and it is ignored
    ///   for non-mixed single object. A null value means bump all components.</param>
    /// <param name="bumpTenorDates">Array of the selected tenor dates to bump.
    ///   This parameter applies to base correlation term structures and it is ignored
    ///   for simple base correlation without term structure. A null value means
    ///   bump all tenors.</param>
    /// <param name="bumpDetachments">Array of the selected detachment points to bump.
    ///   This should be an array of detachments points associated with the strikes.
    ///   A null value means to bump the correlations at all strikes.</param>
    /// <param name="bumpTarget">Select Bump target on market quotes or correlations</param>
    /// <param name="bumpRelative">Boolean value indicating if a relative bump is required.</param>
    /// <param name="scale">Scaling factor (0 if no scaling is required; 100 for bumping on correlations or on fee quotes;
    ///   10000 for bumping on spreads).</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="calcHedge">Calculate hedge delta and ratios</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the correlation sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate the correlation sensitivity of the fair spread
    ///   // Here we are using parallel relative shifts of the correlations
    ///   //
    ///   DataTable dataTable = Sensitivities.BaseCorrelation(
    ///     new SyntheticCDOPricer[] { pricer },  // Pricer for CDO tranche
    ///     new string[]{"Pv","ExpectedLoss"},    // Price mesure
    ///     0.10,                                 // Based on 10 percent up shift
    ///     0.0,                                  // No down shift
    ///     null,                                 // Bump all components
    ///     null,                                 // Bump all tenors
    ///     new double[]{ 0.03, 0.07 },           // Bump 3% and 7% points
    ///     false,                                // Interpret shifts as on correltions
    ///     true,                                 // Interpret shifts as relative
    ///     BumpType.Parallel,                    // Bumps are parallel
    ///     null,                                 // Bump all correlations
    ///     false,                                // Dont bother with Gammas
    ///     false,                                // Dont bother with Hedges
    ///     null                                  // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Correlation for {0}, sensitivity is {1}",
    ///                        (string)row["Measure"], (double)row["Delta"] );
    ///   }
    /// </code>
    /// </example>
    ///
    /// 
    public static DataTable BaseCorrelation(
      SyntheticCDOPricer[] pricers,
      string[] measures,
      double upBump,
      double downBump,
      BumpUnit bumpUnit,
      string[] bumpComponents,
      Dt[] bumpTenorDates,
      double[] bumpDetachments,
      BumpTarget bumpTarget,
      bool bumpRelative,
      double scale,
      BaseCorrelationBumpType bumpType,
      bool calcGamma,
      bool calcHedge,
      DataTable dataTable
      )
    {
      // Sanity check
      if (measures == null || measures.Length == 0)
        throw new ArgumentOutOfRangeException("measures", "No price measure specified");
      if (pricers == null || pricers.Length == 0)
        throw new ArgumentOutOfRangeException("pricers", "No pricer specified");
      for (int i = 0; i < pricers.Length; ++i)
      {
        if (!(pricers[i].Basket.Correlation is BaseCorrelationObject))
          throw new ArgumentException("Pricers do not contain base correlation object");
        if (pricers[i].Basket.Correlation != pricers[0].Basket.Correlation)
          throw new ArgumentException("Pricers do not contain the same base correlation object");
      }

      // Base correlation object
      BaseCorrelationObject correlation = (BaseCorrelationObject)pricers[0].Basket.Correlation;

      // Create the pricer evaluators
      PricerEvaluator[] evaluators = new PricerEvaluator[pricers.Length * measures.Length];
      for (int i = 0, idx = 0; i < pricers.Length; ++i)
        for (int j = 0; j < measures.Length; ++j)
          evaluators[idx++] = new PricerEvaluator(pricers[i], measures[j]);

      logger.DebugFormat("Calculating base correlation sensitivities up={0}, down={1}",
        upBump, downBump);
      Timer timer = new Timer();
      timer.start();

      dataTable = DoBaseCorrelationSensitivity(evaluators, null, upBump, downBump, bumpUnit, correlation,
        bumpComponents, bumpTenorDates, bumpDetachments, bumpTarget, bumpRelative,
        scale, bumpType, calcGamma, calcHedge, dataTable);

      timer.stop();
      logger.InfoFormat("Completed base correlation sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    #endregion BaseCorrelation Sensitivity

    #region BaseCorrelation Sensitivity Evaluations


    /// <summary>
    ///   Evaluate price measures with different correlation objects
    ///   <preliminary/>
    /// </summary>
    /// 
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="altCorrelation">Alternative correlation object</param>
    /// 
    /// <returns>
    ///   <para>A two-dimesional array with two columns.  The first column is the
    ///   base value with the original correlation, and the second is calculated
    ///   with the alternative correlation.
    ///   The number of rows equal to the number of evaluators, arranged in the 
    ///   same order, with each row containing the results of one evaluator.</para>
    /// 
    ///   <para>If an evluator is not built on a CDO pricer, the corresponding 
    ///   second column constains <c>Double.NaN</c>.</para>
    /// </returns>
    ///
    public static double[] BumpedEval(IPricer pricer, string measure, CorrelationObject altCorrelation)
    {
      return BumpedEval(CreateAdapters(pricer, measure), altCorrelation);
    }

    /// <summary>
    ///   Evaluate price measures with different correlation objects
    ///   <preliminary/>
    /// </summary>
    /// 
    /// <param name="evaluators">Array of evaluators</param>
    /// <param name="altCorrelation">Alternative correlation object</param>
    /// 
    /// <returns>
    ///   <para>A two-dimesional array with two columns.  The first column is the
    ///   base value with the original correlation, and the second is calculated
    ///   with the alternative correlation.
    ///   The number of rows equal to the number of evaluators, arranged in the 
    ///   same order, with each row containing the results of one evaluator.</para>
    /// 
    ///   <para>If an evluator is not built on a CDO pricer, the corresponding 
    ///   second column constains <c>Double.NaN</c>.</para>
    /// </returns>
    public static double[] BumpedEval( PricerEvaluator[] evaluators, CorrelationObject altCorrelation )
    {
#if DEBUG
      // Sanity check
      if (evaluators == null || evaluators.Length == 0)
        throw new ArgumentException("evaluators cannot be null");
#endif

      // Allocate result array
      double[] result = new double[evaluators.Length];

      // Calculate the bumped values
      bool[] calculated = new bool[evaluators.Length];
      for (int i = 0; i < evaluators.Length; ++i)
        if (!calculated[i])
        {
          IPricer pi = evaluators[i].Pricer;
          if (!(pi is SyntheticCDOPricer))
          {
            logger.Debug(String.Format("Pricer [0] is not a CDO pricer but {1}, return NaN", i + 1, pi.GetType().FullName));
            result[i] = Double.NaN;
            continue;
          }
          // Create a new pricer with the alternative correlation object
          SyntheticCDOPricer pricer = ((SyntheticCDOPricer)pi).Substitute(altCorrelation);
          result[i] = evaluators[i].Evaluate(pricer);
          calculated[i] = true;
          for (int j = i + 1; j < evaluators.Length && !calculated[j]; ++j)
            if (pi == evaluators[j].Pricer)
            {
              // Same pricer, perhaps different price measures
              result[j] = evaluators[j].Evaluate(pricer);
              calculated[j] = true;
            }
          // end of loop
        }

      return result;
    }

    public static DataTable DoBaseCorrelationSensitivity(
      PricerEvaluator[] evaluators,
      double[] baseTable,
      double upBump,
      double downBump,
      BumpUnit bumpUnit,
      BaseCorrelationObject correlation,
      string[] bumpComponents,
      Dt[] bumpTenorDates,
      double[] bumpDetachments,
      BumpTarget bumpTarget,
      bool bumpRelative,
      double scaleMode,
      BaseCorrelationBumpType bumpType,
      bool calcGamma,
      bool calcHedge,
      DataTable dataTable
      )
    {
      bool includeTranche = bumpTarget != BumpTarget.Correlation &&
        bumpTarget != BumpTarget.IndexQuotes;
      bool includeIndex = ((bumpTarget & BumpTarget.IndexQuotes) != 0);

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = new DataTable("Base Correlation Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("Surface", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Detachment", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Measure", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Pricer", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
        if (calcHedge)
        {
          dataTable.Columns.Add(new DataColumn("Underlying Delta", typeof(double)));
          dataTable.Columns.Add(new DataColumn("Delta Ratio", typeof(double)));
        }
      }

      // compute the base case
      double[] mtm = baseTable;
      if (mtm == null)
      {
        logger.Debug("Computing base cases");
        mtm = new double[evaluators.Length];
        PricerReset(evaluators);
        for (int j = 0; j < evaluators.Length; j++)
          mtm[j] = evaluators[j].Evaluate();
      }

      // Compute sensitivities
      switch (bumpType)
      {
        case BaseCorrelationBumpType.Uniform:
          BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
            bumpComponents, bumpTenorDates, bumpDetachments,
            includeTranche, includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
          break;

        case BaseCorrelationBumpType.ByComponent:
          if (bumpComponents == null)
            bumpComponents = BaseCorrelationObject.FindComponentNames(correlation);
          if (bumpComponents == null)
            BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
              bumpComponents, bumpTenorDates, bumpDetachments,
              includeTranche,includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
          else
            foreach (string com in bumpComponents)
            {
              string[] coms = new string[] { com };
              BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                coms, bumpTenorDates, bumpDetachments,
                includeTranche,includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
            }
          break;

        case BaseCorrelationBumpType.ByTenor:
          if (bumpTenorDates == null)
            bumpTenorDates = BaseCorrelationObject.FindTenorDates(correlation);
          if (bumpComponents == null)
            bumpComponents = BaseCorrelationObject.FindComponentNames(correlation);
          if (bumpComponents != null && bumpComponents.Length > 1)
          {
            // Recursively call the ByTenor bump with each selected component
            foreach (string com in bumpComponents)
            {
              string[] coms = new string[] { com };
              DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                coms, bumpTenorDates, bumpDetachments, bumpTarget, bumpRelative,
                scaleMode, bumpType, calcGamma, calcHedge, dataTable);
            }
            break;
          }
          if (bumpTenorDates == null)
            BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
              bumpComponents, bumpTenorDates, bumpDetachments,
              includeTranche,includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
          else
            foreach (Dt date in bumpTenorDates)
            {
              Dt[] tenorDates = new Dt[] { date };
              BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                bumpComponents, tenorDates, bumpDetachments,
                includeTranche,includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
            }
          break;

        case BaseCorrelationBumpType.ByStrike:
          if (includeIndex && (bumpDetachments == null || bumpDetachments.Length != 1 || bumpDetachments[0] != 0.0))
          {
            DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
              bumpComponents, bumpTenorDates, new double[] { 0 }, BumpTarget.IndexQuotes, bumpRelative,
              scaleMode, bumpType, calcGamma, calcHedge, dataTable);
            DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
              bumpComponents, bumpTenorDates, bumpDetachments, BumpTarget.TrancheQuotes, bumpRelative,
              scaleMode, bumpType, calcGamma, calcHedge, dataTable);
            break;
          }
          if (bumpDetachments == null)
            bumpDetachments = BaseCorrelationObject.FindDetachments(correlation);
          if (bumpComponents == null)
            bumpComponents = BaseCorrelationObject.FindComponentNames(correlation);
          if (bumpComponents != null && bumpComponents.Length > 1)
          {
            // Recursively call the ByStrike bump with each selected component
            foreach (string com in bumpComponents)
            {
              string[] coms = new string[] { com };
              DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                coms, bumpTenorDates, bumpDetachments, bumpTarget, bumpRelative,
                scaleMode, bumpType, calcGamma, calcHedge, dataTable);
            }
            break;
          }
          if (bumpDetachments == null)
            BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
              bumpComponents, bumpTenorDates, bumpDetachments,
              includeTranche,includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
          else
            foreach (double dp in bumpDetachments)
            {
              double[] dps = new double[] { dp };
              BaseCorrelation(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                bumpComponents, bumpTenorDates, dps,
                includeTranche,includeIndex, bumpRelative, scaleMode, calcGamma, calcHedge, dataTable);
            }
          break;

        case BaseCorrelationBumpType.ByPoint:
          if (bumpComponents == null)
            bumpComponents = BaseCorrelationObject.FindComponentNames(correlation);
          if (bumpComponents == null)
            bumpComponents = new string[] { "All" };
          if (bumpComponents.Length == 1)
          {
            if (bumpTenorDates == null)
              bumpTenorDates = BaseCorrelationObject.FindTenorDates(correlation);
            if (bumpTenorDates == null)
              bumpTenorDates = new Dt[] { Dt.Empty };
            if (bumpTenorDates.Length == 1)
              // Recursively call the ByStrike bump
              DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                bumpComponents, bumpTenorDates, bumpDetachments, bumpTarget, bumpRelative,
                scaleMode, BaseCorrelationBumpType.ByStrike, calcGamma, calcHedge, dataTable);
            else
              // Recursively call the ByPoint bump with each selected tenor
              foreach (Dt date in bumpTenorDates)
              {
                Dt[] tenors = new Dt[] { date };
                DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                  bumpComponents, tenors, bumpDetachments, bumpTarget, bumpRelative,
                  scaleMode, bumpType, calcGamma, calcHedge, dataTable);
              }
          }
          else
            // Recursively call the ByPoint bump with each selected component
            foreach (string com in bumpComponents)
            {
              string[] coms = new string[] { com };
              DoBaseCorrelationSensitivity(evaluators, mtm, upBump, downBump, bumpUnit, correlation,
                coms, bumpTenorDates, bumpDetachments, bumpTarget, bumpRelative,
                scaleMode, bumpType, calcGamma, calcHedge, dataTable);
            }
          break;

        default:
          throw new ArgumentException(String.Format("This type of bump ({0}) is not yet supported ", bumpType));
      }

      return dataTable;
    }


    public static void BaseCorrelation(
      PricerEvaluator[] evaluators,
      double[] baseTable,
      double upBump,
      double downBump,
      BumpUnit bumpUnit,
      BaseCorrelationObject correlation,
      string[] bumpComponents,
      Dt[] bumpTenorDates,
      double[] bumpDetachments,
      bool includeTranche,
      bool includeIndex,
      bool bumpRelative,
      double scale,
      bool calcGamma,
      bool calcHedge,
      DataTable dataTable
      )
    {
      var trancheOrCorrelation = includeTranche || !includeIndex;
      var bumpQuotes = includeTranche || includeIndex;

      double[] upTable = null;
      double[] downTable = null;

      if (calcHedge)
      {
        if (bumpComponents.Length > 1 || bumpTenorDates.Length > 1 || bumpDetachments.Length > 1)
          throw new ArgumentException(
            "Cannot calculate hedge when bumping more than one point simultaneously");
      }

      // Bump up
      double avgUpBump = 0.0, upHedgeDelta = 0;
      if (upBump != 0.0)
      {
        BaseCorrelationObject altBaseCorrelation =
          CloneUtil.CloneObjectGraph(correlation);
        altBaseCorrelation.EntityNames = null; // do not check by name dictionary
        ArrayList hedgeInfo = calcHedge ? new ArrayList() : null;
        avgUpBump = altBaseCorrelation.BumpCorrelations(
          bumpComponents, bumpTenorDates, bumpDetachments,
          new[] { new BumpSize(trancheOrCorrelation ? upBump : 0.0, bumpUnit) },
          includeIndex ? new BumpSize(upBump, bumpUnit) : null,
          bumpRelative, bumpQuotes, hedgeInfo);
        if (calcHedge) upHedgeDelta = GetHedgeBumpedValue(hedgeInfo);
        if (avgUpBump >= 1E-9)
          upTable = BumpedEval(evaluators, altBaseCorrelation);
      }

      // Bump down
      double avgDownBump = 0.0, downHedgeDelta = 0.0;
      if (downBump != 0.0)
      {
        BaseCorrelationObject altBaseCorrelation =
          CloneUtil.CloneObjectGraph(correlation);
        altBaseCorrelation.EntityNames = null; // do not check by name dictionary
        ArrayList hedgeInfo = calcHedge ? new ArrayList() : null;
        avgDownBump = -altBaseCorrelation.BumpCorrelations(
          bumpComponents, bumpTenorDates, bumpDetachments,
          new[] { new BumpSize(trancheOrCorrelation ? (-downBump) : 0.0, bumpUnit) },
          includeIndex ? new BumpSize(-downBump, bumpUnit) : null,
          bumpRelative, bumpQuotes, hedgeInfo);
        if (calcHedge) downHedgeDelta = GetHedgeBumpedValue(hedgeInfo);
        if (avgDownBump >= 1E-9)
          downTable = BumpedEval(evaluators, altBaseCorrelation);
      }

      if (upTable == null && downTable == null)
        return;

      bool scaledDelta = (scale != 0);
      double scaleUnit = scale != 0 ? scale : 1.0;
      double hedgeDelta = upHedgeDelta - downHedgeDelta;
      double avgTotalBump = scaledDelta ? ((avgUpBump + avgDownBump) * scaleUnit) : 1.0;

      for (int j = 0; j < evaluators.Length; ++j)
      {
        DataRow row = dataTable.NewRow();
        if (bumpComponents != null && bumpComponents.Length == 1)
          row["Surface"] = bumpComponents[0];
        else
          row["Surface"] = "all";

        if (bumpTenorDates != null && bumpTenorDates.Length == 1)
        {
          row["Tenor"] = bumpTenorDates[0].ToStr("%D");
        }
        else
          row["Tenor"] = "";

        if (bumpDetachments != null && bumpDetachments.Length == 1)
          row["Detachment"] = bumpDetachments[0].ToString();
        else
          row["Detachment"] = "0";

        row["Pricer"] = evaluators[j].Pricer.Product.Description;
        row["Measure"] = evaluators[j].MethodName;

        row["Delta"] = CalcDelta(j, baseTable, upTable, downTable,
          scaledDelta, avgUpBump * scaleUnit, avgDownBump * scaleUnit);

        if (calcGamma)
          row["Gamma"] = CalcGamma(j, baseTable, upTable, downTable,
            scaledDelta, avgUpBump * scaleUnit, avgDownBump * scaleUnit);

        if (calcHedge)
        {
          row["Underlying Delta"] = 1000000 * hedgeDelta / avgTotalBump;
          row["Delta Ratio"] = Math.Abs(hedgeDelta) < 1E-12 ? 0.0
            : (CalcDelta(j, baseTable, upTable, downTable,
            false, avgUpBump * scaleUnit, avgDownBump * scaleUnit)
            / ((PricerBase)evaluators[j].Pricer).Notional / hedgeDelta);
        }

        dataTable.Rows.Add(row);
      }
      return;
    }

    private static double GetHedgeBumpedValue(ArrayList hedgeInfo)
    {
      if(hedgeInfo.Count == 0)
        return 0.0;

      double delta = 0.0;
      if (hedgeInfo[0] is double[,])
      {
        double[,] deltas = (double[,])hedgeInfo[0];
        if (deltas.Length != 0) delta = deltas[0, 0];
      }
      else if (hedgeInfo[0] is double[])
      {
        double[] deltas = (double[])hedgeInfo[0];
        if (deltas.Length != 0) delta = deltas[0];
      }
      return delta;
    }

    /// <summary>
    ///   Local utility function to calc Delta for pricer j
    /// </summary>
    /// <param name="j">Pricer index</param>
    /// <param name="baseTable">Base table</param>
    /// <param name="upTable">Up bumped table</param>
    /// <param name="downTable">Down bumped table</param>
    /// <param name="scaled">Whether to scale the result</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns>Delta</returns>
    public static double
    CalcDelta(int j, double[] baseTable, double[] upTable, double[] downTable,
      bool scaled, double upBump, double downBump)
    {
      return upTable == null && downTable == null
        ? 0.0
        : CalcDelta(j, baseTable[j], upTable, downTable,
          scaled, upBump, downBump);
    }

    public static double CalcDelta(int j, double baseValue, double[] upTable, double[] downTable,
      bool scaled, double upBump, double downBump)
    {
      double delta = 0.0;

      if (null != upTable)
        delta += (upTable[j] - baseValue);
      if (null != downTable)
        delta -= (downTable[j] - baseValue);
      if (scaled)
        delta /= (upBump + downBump);

      return delta;
    }

    /// <summary>
    ///   Local utility function to calc Gamma for pricer j
    /// </summary>
    /// <param name="j">Pricer index</param>
    /// <param name="baseTable">Base table</param>
    /// <param name="upTable">Up bumped table</param>
    /// <param name="downTable">Down bumped table</param>
    /// <param name="scaled">Whether to scale the result</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns>Gamma</returns>
    public static double
    CalcGamma(int j, double[] baseTable, double[] upTable, double[] downTable,
      bool scaled, double upBump, double downBump)
    {
      return CalcGamma(j, baseTable[j], upTable, downTable,
        scaled, upBump, downBump);
    }

    public static double
    CalcGamma(int j, double baseValue, double[] upTable, double[] downTable,
      bool scaled, double upBump, double downBump)
    {
      double gamma = 0.0;

      if (null != upTable && null != downTable && scaled)
      {
        // NOTE!!! If upBump != downBump you're really getting a guess at the gamma not
        // at x, but at x + 0.5*(upBump-downBump).  This is unlikely to be a huge deal,
        // but if you have a highly convex function it might be an issue.

        // Find change in deltas. First term approximates delta at (x + 0.5*upBump) and
        // the second term approximates (minus) delta at (x - 0.5*downBump)
        gamma = (upTable[j] - baseValue) / upBump
          + (downTable[j] - baseValue) / downBump;
        // Now scale by the change in x
        gamma /= ((upBump + downBump) / 2);
      }

      return gamma;
    }

    #endregion BaseCorrelation Sensitivity Evaluations

    #region Market Delta

    /// <summary>
    ///   Market tranche delta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Market tranche delta is simply the ratio of CDO spread01 to Index market spread01.</para>
    /// 
    ///   <para>The parameter <c>marketSpread</c> can be <c>NaN</c>, in which case the market
    ///   spread is calculated as the implied quoted spread based on individual credit curves.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of CDO pricers</param>
    /// <param name="cdx">The underlying CDX</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of curves to bump or null for all</param>
    /// <param name="deltaBump">Bump size of for delta calculation</param>
    /// <param name="cdxQuote">Market quote for the index Note</param>
    /// <param name="rescaleStrikes">Whether to recalculate the trsikes/correlations.</param>
    /// <param name="relativeScaling">True if scale spread relatively</param>
    /// 
    /// <returns>Tranche delta</returns>
    ///
    public static double[] MarketTrancheDelta(
      SyntheticCDOPricer[] pricers,
      CDX cdx,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double deltaBump,
      MarketQuote cdxQuote,
      bool rescaleStrikes,
      bool relativeScaling = false)
    {
      double[] result;
      if (!CalculateMarketTrancheDelta(pricers, cdx, discountCurve,
        survivalCurves, deltaBump, cdxQuote, rescaleStrikes,
        relativeScaling, out result))
      {
        logger.Debug("Failed to find scaling factor. Try direct bump instead.");
        CalculateMarketTrancheDelta(pricers, cdx, discountCurve,
          survivalCurves, deltaBump,
          new MarketQuote(0.0, QuotingConvention.None),
          rescaleStrikes, relativeScaling, out result);
      }
      return result;
    }

    /// <summary>
    ///  Help function to calculates the market tranche delta.
    /// </summary>
    /// <param name="pricers">The pricers.</param>
    /// <param name="cdx">The CDX.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="survivalCurves">The survival curves.</param>
    /// <param name="deltaBump">The delta bump.</param>
    /// <param name="cdxQuote">The CDX quote.</param>
    /// <param name="rescaleStrikes">if set to <c>true</c>, rescale strikes.</param>
    /// <param name="relativeScaling">True if scale spreads relatively</param>
    /// <param name="result">The result.</param>
    /// <returns>True if the calculation succeeds, False if it fails to find a scaling factor.</returns>
    private static bool CalculateMarketTrancheDelta(
      SyntheticCDOPricer[] pricers,
      CDX cdx,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double deltaBump,
      MarketQuote cdxQuote,
      bool rescaleStrikes,
      bool relativeScaling,
      out double[] result)
    {
      if (pricers == null || pricers.Length == 0)
      {
        result = null;
        return true;
      }

      // Create clones of pricers and curves,
      // so the originals are intact.
      {
        // Here we must clone all the inputs in one call.
        // This neccessary to preserve all the references across
        // objects, including the curves both inside and outside
        // the pricers.
        var cloned = CloneUtil.CloneObjectGraph(
          new object[] {pricers, cdx, discountCurve, survivalCurves});
        pricers = (SyntheticCDOPricer[]) cloned[0];
        cdx = (CDX) cloned[1];
        discountCurve = (DiscountCurve) cloned[2];
        survivalCurves = (SurvivalCurve[]) cloned[3];
        // Change the rescaling strikes property to the desired value.
        foreach (var p in pricers)
        {
          var b = p.Basket as BaseCorrelationBasketPricer;
          if (b != null) b.RescaleStrike = rescaleStrikes;
        }
      }

      // find the underlying basket and dates
      BasketPricer basket = pricers[0].Basket;
      Dt asOf = basket.AsOf;
      Dt settle = basket.Settle;
      if (survivalCurves == null)
        survivalCurves = basket.OriginalBasket.SurvivalCurves;
      if (discountCurve == null)
        discountCurve = pricers[0].DiscountCurve;

      // create CDX pricer and calculate the implied spread
      ICDXPricer cdxPricer = CDXPricerUtil.CreateCdxPricer(
        cdx, asOf, settle, discountCurve, survivalCurves);
      double indexNotional = cdxPricer.Notional;
      if (Math.Abs(cdxPricer.EffectiveNotional) < 1E-15)
        throw new InvalidOperationException("Effective notional cannot be zero");
      double cdxValue = cdxPricer.IntrinsicValue(false);

      // allocate space for results and calculate the base values
      result = new double[pricers.Length];
      for (int i = 0; i < pricers.Length; ++i)
        result[i] = pricers[i].Pv();

      if (cdxQuote.Type == QuotingConvention.None)
      {
        // uniform bump the survival curves
        CurveUtil.CurveBump(survivalCurves, deltaBump, true, false, true);
      }
      else
      {
        double[] factors;
        cdxQuote.Value += cdxQuote.Type == QuotingConvention.CreditSpread
          ? deltaBump/10000.0 : deltaBump/100.0;
        bool[] scales = Array.ConvertAll<SurvivalCurve, bool>(survivalCurves,
          (c) => c.Defaulted == Defaulted.NotDefaulted);
        SurvivalCurve[] bumpedCurves;
        try
        {
          CDXScaling.Scaling(asOf, settle, new CDX[] {cdx},
            new MarketQuote[] {cdxQuote}, new bool[] {true},
            cdxPricer.MarketRecoveryRate, relativeScaling,
            discountCurve, survivalCurves, false, true,
            CDXScalingMethod.Model, scales,
            out factors, out bumpedCurves);
        }
        catch
        {
          return false;
        }
        for (int i = 0; i < bumpedCurves.Length; ++i)
          survivalCurves[i].Set(bumpedCurves[i]);
      }

      // Calculate the CDX delta
      cdxPricer = CDXPricerUtil.CreateCdxPricer(
        cdx, asOf, settle, discountCurve, survivalCurves);
      double cdxDelta = (cdxPricer.IntrinsicValue(false) - cdxValue)/
        indexNotional;
      for (int i = 0; i < pricers.Length; ++i)
      {
        SyntheticCDOPricer p = pricers[i];
        p.Basket.Reset();
        double notional = p.Notional;
        if (Math.Abs(p.EffectiveNotional) < 1E-15)
        {
          result[i] = 0;
          continue;
        }
        // non-zero notional
        result[i] = (p.Pv() - result[i])/notional/cdxDelta;
      }
      return true;
    }

    /// <summary>
    ///   Tranche delta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Tranche delta is defined as the ratio of the change in the Value value
    ///   of a tranche to the change in the Value of underlying index due to a 0.1bp
    ///   parallel widening in the underlying index/reference swap curve, while
    ///   single names are adjusted proportionally.</para>
    /// 
    ///   <para>The parameter <c>marketSpread</c> can be <c>NaN</c>, in which case the market
    ///   spread is calculated as the implied quoted spread based on individual credit curves.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of CDO pricers</param>
    /// <param name="cdx">The underlying CDX</param>
    /// <param name="scalingMethod">Scaling method for the tenor</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of curves to bump or null for all</param>
    /// <param name="scales">Array of booleans indicating whether to scale a name</param>
    /// <param name="calcGamma">True if need to calculate tranche gamma</param>
    /// <param name="deltaBump">Bump size of for delta calculation</param>
    /// <param name="gammaBump">Bump size of for gamma calculation</param>
    /// <param name="marketQuote">Market spread in raw number(1bp = 0.0001) for the index Note</param>
    /// 
    /// <returns>Tranche delta</returns>
    ///
    public static double[,] TrancheDelta(
      SyntheticCDOPricer[] pricers,
      CDX cdx,
      CDXScalingMethod scalingMethod,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      bool[] scales,
      BumpSize deltaBump,
      double gammaBump,
      bool calcGamma,
      MarketQuote marketQuote)
    {
      if (pricers == null || pricers.Length == 0)
        return null;

      // find the underlying basket
      BasketPricer basket = pricers[0].Basket;

      // Special treatment of FLM tranche
      if (pricers[0].Basket is ForwardLossBasketPricer)
      {
        return calcFlmDelta(
            pricers,
            (ForwardLossBasketPricer)pricers[0].Basket,
            cdx.Description, deltaBump.Size, gammaBump, calcGamma);//in this case, normalization is done already on original notional, no need to fix
      }

      // the original survival curves
      if (survivalCurves == null)
        survivalCurves = basket.OriginalBasket.SurvivalCurves;

      // create CDX pricer and calculate the index value
      Dt asOf = basket.AsOf;
      Dt settle = basket.Settle;
      ICDXPricer cdxPricer = CDXPricerUtil.CreateCdxPricer(
        cdx, asOf, settle, discountCurve, survivalCurves);
      double indexNotional = cdxPricer.Notional;
      if (Math.Abs(cdxPricer.EffectiveNotional) < 1E-15)
        throw new InvalidOperationException("Effective notional cannot be zero");
      double cdxMtm = cdxPricer.IntrinsicValue(false);

      // if no quote supplied, we find the implied quote.
      if (marketQuote.Type == QuotingConvention.None)
      {
        double cleanPrice = 1 + (cdxMtm - cdxPricer.Accrued()) / indexNotional;
        if (deltaBump.Unit == BumpUnit.BasisPoint)
        {
          double spread = cdxPricer.PriceToSpread(cleanPrice);
          marketQuote = new MarketQuote(spread, QuotingConvention.CreditSpread);
        }
        else
          marketQuote = new MarketQuote(cleanPrice, QuotingConvention.FlatPrice);
      }

      // claculate the base values
      double[] cdoMtms = Array.ConvertAll<SyntheticCDOPricer, double>(
        pricers, delegate(SyntheticCDOPricer p) { return p.Pv(); });
      double[] cdoNtnls = Array.ConvertAll<SyntheticCDOPricer, double>(
        pricers, delegate(SyntheticCDOPricer p) { return p.Notional; });

      // storage
      int cols = (calcGamma ? 2 : 1);
      int N = pricers.Length;
      double[,] result = new double[N, cols];

      // find the bumped quote
      MarketQuote bumpedQuote = new MarketQuote(
        (deltaBump.Unit == BumpUnit.BasisPoint ?
          deltaBump.Size / 10000.0 : deltaBump.Size / 100.0)
        + marketQuote.Value, marketQuote.Type);

      // find the bumped curves matching the quote
      double[] factors;
      SurvivalCurve[] bumpedCurves;
      if (scales == null)
      {
        scales = Array.ConvertAll<SurvivalCurve, bool>(survivalCurves,
          (c) => c.Defaulted == Defaulted.NotDefaulted);
      }
      CDXScaling.Scaling(asOf, settle, new CDX[] { cdx },
        new MarketQuote[] { bumpedQuote }, new bool[] { true },
        cdxPricer.MarketRecoveryRate, false, discountCurve,
        survivalCurves, false, true, scalingMethod, scales,
        out factors, out bumpedCurves);

      // calculate the deltas
      cdxPricer = CDXPricerUtil.CreateCdxPricer(
        cdx, asOf, settle, discountCurve, bumpedCurves);
      double cdxDelta = (cdxPricer.IntrinsicValue(false) - cdxMtm) / indexNotional;
      for (int i = 0; i < pricers.Length; ++i)
        if (Math.Abs(cdoNtnls[i]) > 1E-14)
        {
          SyntheticCDOPricer p = Substitute(pricers[i],bumpedCurves);
          // tranche delta
          result[i, 0] = (p.Pv() - cdoMtms[i]) / cdoNtnls[i] / cdxDelta;
        }

      // we have done for tranche deltas
      if (!calcGamma)
        return result;

      // For tranche gamma, we bump the market quotes proportionally
      bumpedQuote = new MarketQuote(
        marketQuote.Value * (1 + gammaBump), marketQuote.Type);

      // find the bumped curves matching the quote
      CDXScaling.Scaling(asOf, settle, new CDX[] { cdx },
        new MarketQuote[] { bumpedQuote }, new bool[] { true },
        cdxPricer.MarketRecoveryRate, false,
        discountCurve, survivalCurves, false, true, scalingMethod, scales,
        out factors, out bumpedCurves);

      // calculate the tranche gammas
      cdxPricer = CDXPricerUtil.CreateCdxPricer(
        cdx, asOf, settle, discountCurve, bumpedCurves);
      cdxDelta = (cdxPricer.IntrinsicValue(false) - cdxMtm) / indexNotional;
      for (int i = 0; i < pricers.Length; ++i)
        if (Math.Abs(pricers[i].EffectiveNotional) > 1E-14)
        {
          SyntheticCDOPricer p = Substitute(pricers[i], bumpedCurves);
          // tranche gamma
          result[i, 1] = (p.Pv() - cdoMtms[i]) / cdoNtnls[i]
            - result[i, 0] * cdxDelta;
        }

      // we're done
      return result;
    }

    // Special treatment of FLM tranche
    private static double[,]
    calcFlmDelta(
        SyntheticCDOPricer[] pricers,
        ForwardLossBasketPricer basket,
        string tenor,
        double deltaBump,
        double gammaBump,
        bool calcGamma)
    {
      // Find the cdx corresponding to tenor
      int cdxIdx = -1;
      CDX[] nodes = basket.IndexNotes;
      double[] spreads = basket.IndexSpreads;
      {
        for (int i = 0; i < nodes.Length; ++i)
          if (String.Compare(nodes[i].Description, tenor, true) == 0)
          {
            cdxIdx = i;
            break;
          }
        if (cdxIdx < 0)
          throw new ArgumentOutOfRangeException("tenor", tenor, "Tenor does not exist");
      }

      // number of tranches
      int N = pricers.Length;

      // create CDX pricer
      CDXPricer cdxPricer = new CDXPricer(nodes[cdxIdx], basket.AsOf, basket.Settle, basket.DiscountCurve, basket.SurvivalCurves);

      // calculate the base values
      double cdxBaseValue = cdxPricer.MarketValue(spreads[cdxIdx]);
      double[] trancheBaseValues = new double[N];
      basket.Reset();
      for (int i = 0; i < N; ++i)
      {
        // Please note that it is important to reconstruct tranche pricers
        // because the contructor will take care of FixedRecovery and make
        // clone of basket when required
        SyntheticCDOPricer pricer = new SyntheticCDOPricer(
            pricers[i].CDO, basket, pricers[i].DiscountCurve, pricers[i].Notional,pricers[i].RateResets);
        trancheBaseValues[i] = pricer.Pv();
      }

      // calculate 10bp bump values
      double bump = (Math.Abs(deltaBump) < 1E-15 ? 0.00001 : deltaBump);
      spreads[cdxIdx] += bump;
      double cdx10bpValue = cdxPricer.MarketValue(spreads[cdxIdx]);
      double[] tranche10bpValues = new double[N];
      basket.Reset();
      try
      {
        for (int i = 0; i < N; ++i)
        {
          // Please note that it is important to reconstruct tranche pricers
          // because the contructor will take care of FixedRecovery and make
          // clone of basket when required
          SyntheticCDOPricer pricer = new SyntheticCDOPricer(
              pricers[i].CDO, basket, pricers[i].DiscountCurve, pricers[i].Notional,pricers[i].RateResets);
          tranche10bpValues[i] = pricer.Pv();
        }
      }
      finally
      {
        spreads[cdxIdx] -= bump;
      }

      // calculate the ratios
      double cdxDelta = (cdx10bpValue - cdxBaseValue) / cdxPricer.Notional;
      int cols = (calcGamma ? 2 : 1);
      double[,] result = new double[N, cols];
      for (int i = 0; i < N; ++i)
      {
        double MtMtranche = (tranche10bpValues[i] - trancheBaseValues[i]) / pricers[i].Notional;
        result[i, 0] = MtMtranche / cdxDelta;
      }

      if (!calcGamma)
        return result;

      // calculate 110% bump values
      bump = (Math.Abs(gammaBump) < 1E-15 ? 0.10 : gammaBump);
      bump = bump < 0 ? 1.0 / (1 - bump) : (1 + bump);
      spreads[cdxIdx] *= bump;
      double cdx110pValue = cdxPricer.MarketValue(spreads[cdxIdx]);
      double[] tranche110pValues = new double[N];
      basket.Reset();
      try
      {
        for (int i = 0; i < N; ++i)
        {
          // Please note that it is important to reconstruct tranche pricers
          // because the contructor will take care of FixedRecovery and make
          // clone of basket when required
          SyntheticCDOPricer pricer = new SyntheticCDOPricer(
              pricers[i].CDO, basket, pricers[i].DiscountCurve, pricers[i].Notional,pricers[i].RateResets);
          tranche110pValues[i] = pricer.Pv();
        }
      }
      finally
      {
        spreads[cdxIdx] /= bump;
      }

      // calculate Gamma
      //    Gamma = Value(tranche,110%) - Value(index,110%) * Delta(tranche)
      cdxDelta = (cdx110pValue - cdxBaseValue) / cdxPricer.Notional;
      bump -= 1;
      for (int i = 0; i < N; ++i)
      {
        double MtMtranche = (tranche110pValues[i] - trancheBaseValues[i]) / pricers[i].Notional;
        // calculate gamma and normalize to 10% widening
        result[i, 1] = (MtMtranche - cdxDelta * result[i, 0]) * 0.1 / bump;
      }

      // we're done
      return result;
    }

    /// <summary>
    ///   Helper to create a new CDO pricer with the credit curves
    ///   substituted by a new set.
    /// </summary>
    /// <remarks>
    ///   The pricing function on the new pricer can be called without
    ///   affecting the orginal pricer.
    /// </remarks>
    /// <param name="pricer">CDO Pricer</param>
    /// <param name="altSurvivalCurves">Credit curves to use</param>
    /// <returns>A new pricer.</returns>
    private static SyntheticCDOPricer Substitute(
      SyntheticCDOPricer pricer,
      SurvivalCurve[] altSurvivalCurves)
    {
      CreditPool.Builder builder = new CreditPool.Builder(pricer.Basket.OriginalBasket);
      builder.SurvivalCurves = altSurvivalCurves;
      SyntheticCDO cdo = pricer.CDO;
      SyntheticCDOPricer p = pricer.Substitute(
        builder.CreditPool,
        pricer.Basket.Correlation,
        cdo.Attachment, cdo.Detachment);
      if (pricer.Basket.HasFixedRecovery)
      {
        p.Basket.RecoveryCurves = pricer.Basket.RecoveryCurves;
        p.Basket.ResetRecoveryRates();
      }
      return p;
    }
    #endregion Market Delta

    #region CorrelationRiskMeasure
    /// <summary>
    ///   BaseCorrelation level delta
    /// </summary>
    ///
    /// <remarks>
    ///   Base correlation skew delta is the change in the upfront fee (for tranches with non-zero upfront)
    ///   or in the spread (for tranches with zero upfront) due to a 1% up bump
    ///   in both attachment correlation and detachment correlation.
    /// </remarks>
    ///
    /// <param name="pricer">CDO pricer based on base correlation</param>
    /// 
    /// <returns>BaseCorrelation level delta</returns>
    ///
    public static double
    BaseCorrelationLevelDelta(SyntheticCDOPricer pricer)
    {
      return BaseCorrelationBasketPricer.BaseCorrelationDelta(pricer, 0.01, false, true);
    }


    /// <summary>
    ///   BaseCorrelation skew delta
    /// </summary>
    ///
    /// <remarks>
    ///   Base correlation skew delta is the change in the upfront fee (for tranches with non-zero upfront)
    ///   or in the spread (for tranches with zero upfront) due to a 1% up bump
    ///   in the detachment correlation while holding the attachment correlation unchanged.
    /// </remarks>
    ///
    /// <param name="pricer">CDO pricer based on base correlation</param>
    /// 
    /// <returns>BaseCorrelation skew delta</returns>
    ///
    public static double
    BaseCorrelationSkewDelta(SyntheticCDOPricer pricer)
    {
      return BaseCorrelationBasketPricer.BaseCorrelationDelta(pricer, 0.01, false, false);
    }

    #endregion CorrelationRiskMeasure

    #region Backward Compatible

    /// <summary>
    ///   Calculate deltas due to base correlation changes
    /// </summary>
    /// 
    /// <param name="pricer">CDO tranche pricer</param>
    /// <param name="measures">Price measures to calculate deltas for.
    /// They must be the names of the evaluation methods defined in
    /// SyntheticCDOPricer, such as Pv, FeePv, ExpectedLoss, etc..</param>
    /// <param name="bumpComponents">Array of names of the selected
    /// components to bump.  This parameter applies to mixed base
    /// correlation objects and it is ignored for non-mixed single
    /// object.  A null value means bump all components.</param>
    /// <param name="bumpTenorDates">Array of the selected tenor dates
    /// to bump.  This parameter applies to base correlation term structures
    /// and it is ignored for simple base correlation without term structure.
    /// A null value means bump all tenors.</param>
    /// <param name="bumpDetachments">Array of the selected detachment points
    /// to bump.  This should be an array of detachments points associated
    /// with the strikes.  A null value means to bump the correlations
    /// at all strikes.</param>
    /// <param name="bumpSizes">Array of the bump sizes applied to
    /// the correlations on the selected detachment points. If the array
    /// is null or empty, no bump is performed.  Else if it contains only
    /// a single number, the number is applied to all detachment points.
    /// Otherwise, the array is required to have the same size as the array of
    /// detachments.</param>
    /// <param name="relative">Boolean value indicating if a relative bump is required.</param>
    /// <param name="scaleDelta">Boolean value indicating if the deltas
    ///  should be scaled by the average bump size.</param>
    /// 
    /// <returns>Array of deltas for the price measures</returns>
    /// 
    /// <exception cref="ArgumentException">
    ///   <para>(1) The pricer contains no base correlation object.</para>
    ///   <para>(1) No price measure is specifief.</para>
    /// </exception>
    /// 
    /// <example>
    ///   The following codes calculate impacts on Value values and expect losses
    ///   for 1% bump on the correlation at detachment 3%, and 0.5% bump on the
    ///   correlation at detachment 7%.
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo and pricer
    ///   // ...
    ///
    ///   // Calculate CDO base correlation sensitivity.
    ///   //
    ///   double[] deltas = Sensitivities.BaseCorrelation01(
    ///     pricer,                               // CDO tranche pricer
    ///     new string[]{ "Pv", "ExpectedLoss" }, // Price measures
    ///     null,                                 // Bump all components
    ///     null,                                 // Bump all tenors
    ///     new double[]{ 0.03, 0.07 },           // Bump 3% and 7% points
    ///     new double[]{ 0.01, 0.005 },          // Bump sizes
    ///     false,                                // Bump absolute values
    ///     false                                 // Don't scale the deltas
    ///   );
    ///   double deltaMtM = deltas[0];   // the change in Value value
    ///   double deltaEL = deltas[1];    // the change in expected loss
    /// </code>
    /// </example>
    public static double[] BaseCorrelation01(
      SyntheticCDOPricer pricer,
      string[] measures,
      string[] bumpComponents,
      Dt[] bumpTenorDates,
      double[] bumpDetachments,
      double[] bumpSizes,
      bool relative,
      bool scaleDelta)
    {
      return BaseCorrelation01(pricer, measures,
        bumpComponents, bumpTenorDates, bumpDetachments,
        BumpSize.CreatArray(bumpSizes, BumpUnit.None,Double.NaN,Double.NaN),
        false, relative, scaleDelta ? 100.0 : 0.0);
    }

    /// <summary>
    ///   Compute the base correlation sensitivity for a series of CDO pricers
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical base correlation sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on correlations.</para>
    ///
    ///   <para>The correlations are bumped per the parameters and the pricers
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be absolute.</para>
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
    ///     <item><term>Surface</term><description>Name of inner component</description></item>
    ///     <item><term>Tenor Date</term><description>Maturity date of the Base correlation skew</description></item>
    ///     <item><term>Detachment</term><description>Detachment point of the base tranche</description></item>
    ///     <item><term>Measure</term><description>Name of the price measure</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the correlations are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of CDO pricers</param>
    /// <param name="measures">Price measures to evaluate</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpComponents">Array of names of the selected components to bump.
    ///   This parameter applies to mixed base correlation objects and it is ignored
    ///   for non-mixed single object. A null value means bump all components.</param>
    /// <param name="bumpTenorDates">Array of the selected tenor dates to bump.
    ///   This parameter applies to base correlation term structures and it is ignored
    ///   for simple base correlation without term structure. A null value means
    ///   bump all tenors.</param>
    /// <param name="bumpDetachments">Array of the selected detachment points to bump.
    ///   This should be an array of detachments points associated with the strikes.
    ///   A null value means to bump the correlations at all strikes.</param>
    /// <param name="bumpRelative">Boolean value indicating if a relative bump is required.</param>
    /// <param name="scaleDelta">Boolean value indicating if the deltas should be scaled by the average bump size.</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the correlation sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate the correlation sensitivity of the fair spread
    ///   // Here we are using parallel relative shifts of the correlations
    ///   //
    ///   DataTable dataTable = Sensitivities.BaseCorrelation(
    ///     new SyntheticCDOPricer[] { pricer },  // Pricer for CDO tranche
    ///     new string[]{"Pv","ExpectedLoss"},    // Price mesure
    ///     0.10,                                 // Based on 10 percent up shift
    ///     0.0,                                  // No down shift
    ///     null,                                 // Bump all components
    ///     null,                                 // Bump all tenors
    ///     new double[]{ 0.03, 0.07 },           // Bump 3% and 7% points
    ///     true,                                 // Interpret shifts as relative
    ///     BumpType.Parallel,                    // Bumps are parallel
    ///     null,                                 // Bump all correlations
    ///     false,                                // Dont bother with Gammas
    ///     null                                  // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Correlation for {0}, sensitivity is {1}",
    ///                        (string)row["Measure"], (double)row["Delta"] );
    ///   }
    /// </code>
    /// </example>
    ///
    /// 
    public static DataTable BaseCorrelation(
      SyntheticCDOPricer[] pricers,
      string[] measures,
      double upBump,
      double downBump,
      string[] bumpComponents,
      Dt[] bumpTenorDates,
      double[] bumpDetachments,
      bool bumpRelative,
      bool scaleDelta,
      BaseCorrelationBumpType bumpType,
      bool calcGamma,
      DataTable dataTable
      )
    {
      return BaseCorrelation(pricers, measures, upBump, downBump, BumpUnit.None,
        bumpComponents, bumpTenorDates, bumpDetachments, BumpTarget.Correlation, bumpRelative,
        scaleDelta ? 100.0 : 0.0, bumpType, calcGamma, false, dataTable);
    }


#if NOT_USED // RTD Apr'08. To be removed
    /// <summary>
    ///   Compute the correlation sensitivity for a series of pricers
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical correlation sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>Equivalent to <see cref="Correlation(IPricer[],string,double,double,bool,bool,BumpType,bool,bool,DataTable)">
    ///    Correlation(new IPricer[] {pricer}, measure, upBump, downBump, bumpRelative,
    ///    scaledDelta, bumpType, calcGamma, bumpFactors, dataTable)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="bumpFactors">Bump betas rather than correlations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Correlation(
      IPricer pricer,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      bool calcGamma,
      bool bumpFactors,
      DataTable dataTable
      )
    {
      return Correlation(CreateAdapters(pricer, null), upBump, downBump, bumpRelative,
        scaledDelta, bumpType, calcGamma, bumpFactors, dataTable);
    }

    /// <summary>
    ///   Compute the correlation sensitivity for a series of pricers
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical correlation sensitivities with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on correlations.</para>
    ///
    ///   <para>The correlations are bumped per the parameters and the pricers
    ///   are recalculated.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be absolute.</para>
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
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of tenor if applicable</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the correlations are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="upBump">Up bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="downBump">Down bump size in percent (ie .05 is 5 percent)</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="calcGamma">Calculate gamma flag</param>
    /// <param name="bumpFactors">Bump betas rather than correlations</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the correlation sensitivity for a
    /// <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate correlation sensitivity.
    ///   // Here we are using parallel relative shifts of the correlations
    ///   //
    ///   DataTable dataTable = Sensitivities.Correlation( new IPricer[] { pricer },    // Pricer for CDO tranche
    ///                                                    "Pv",                        // Calcualte change in Pv
    ///                                                    0.10,                        // Based on 10 percent up shift
    ///                                                    0.0,                         // No down shift
    ///                                                    true,                        // Interpret shifts as relative
    ///                                                    BumpType.Parallel,           // Bumps are parallel
    ///                                                    null,                        // Bump all correlations
    ///                                                    false,                       // Dont bother with Gammas
    ///                                                    false,                       // Bump correlations rather than factors
    ///                                                    null                         // Create new table of results
    ///                                                   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable.Rows[i];
    ///     Console.WriteLine( " Correlation for {0}, sensitivity is {1}",
    ///                        (string)row["Element"], (double)row["Delta"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Correlation(
      IPricer[] pricers,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      bool calcGamma,
      bool bumpFactors,
      DataTable dataTable
      )
    {
      return Correlation(CreateAdapters(pricers, null), upBump, downBump, bumpRelative,
        scaledDelta, bumpType, calcGamma, bumpFactors, dataTable);
    }
#endif

    #endregion Backward Compatible
  } // class Sensitivities.Correlation
}
