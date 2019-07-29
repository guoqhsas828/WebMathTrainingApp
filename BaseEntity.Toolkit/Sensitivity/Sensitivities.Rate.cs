/*
 * Sensitivities.Rate.cs
 *
 *  -2010. All rights reserved.
 *
 *  Partial implementation of the rate sensitivity functions
 * 
 */
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util.Collections;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

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
    /// Calculate the interest rate sensitivity.
    /// </summary>
    /// <remarks>
    ///   <para>The IR 01 is the change in PV (MTM) if the discount curve swap rates
    ///   are increased by one basis point.</para>
    ///   <para>The IR 01 is calculated by bumping up the underlying IR curve
    ///   and calculating the PV and then bumping down the underlying IR
    ///   curve and re-calculating te PV then returning the difference in value
    ///   divided by the bump size.</para>
    /// </remarks>
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>IR 01</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a
    /// <see cref="BaseEntity.Toolkit.Products.Swap">Interest rate swap</see>.</para>
    /// <code language="C#">
    ///   Swap pricer;
    ///
    ///   // Initialise swap, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate interest rate sensitivity.
    ///   double IR01 = Sensitivities.IR01(
    ///     pricer,             // Pricer for CDO tranche
    ///     "Pv",               // Calculcate change in Pv
    ///     10.0,               // Based on 10bp up shift
    ///     0.0,                // No down shift
    ///     true                // Recalibrate credit curves
    ///     );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " IR01 = {0}", IR01 );
    /// </code>
    /// </example>
    public static double IR01(IPricer pricer, string measure, double upBump,
      double downBump, bool recalibrate, params bool[] rescaleStrikes)
    {
      return IR01(new PricerEvaluator(pricer, measure), upBump, downBump, recalibrate, rescaleStrikes);
    }

    /// <inheritdoc cref="Toolkit.Sensitivity.Sensitivities.IR01(BaseEntity.Toolkit.Pricers.IPricer, string, double, double, bool, bool[])" />
    /// <param name="pricer">IPricer</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Boolean indicatinf rescale strikes or not for CDO pricer</param>
    public static double IR01(IPricer pricer, double upBump, double downBump, bool recalibrate, params bool[] rescaleStrikes)
    {
      return IR01(new PricerEvaluator(pricer), upBump, downBump, recalibrate, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the swap spread sensitivity.
    /// </summary>
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="discountInterp">Interpolation to use for calculation of sensitivities</param>
    /// <param name="curveFitMethod">Curve fitting method to use for calculation of sensitivities</param>
    /// <param name="projection"> Used to replace the discount curve with an equivalent overlay curve. This allows to impose a "functional" relationship
    /// between discount and reference, i.e. <m>\tilde{D}_T = \frac{D_T}{P_T}P_T = B_T P_T</m> where <m>D_T</m>is the calibrated discount
    /// curve and <m>P_T</m> is the calibrated projection curve. The curve <m>B_T = \frac{D_T}{P_T}</m> acts as a basis between 
    /// discount and projection. The discount curve then becomes sensitive to both quotes in the discount curve and the reference curve as the basis 
    /// is mantained constant when quotes are perturbed.  </param>
    /// <returns>Swap spread 01</returns>
    ///
    public static double
    SwapSpread01(IPricer pricer, string measure, double upBump, double downBump, bool recalibrate, InterpScheme discountInterp,
      string curveFitMethod, params CalibratedCurve[] projection)
    {
      DataTable dataTable = Discount(new[] { pricer }, measure, 0.0, upBump, downBump, false, true, BumpType.Uniform,
                                     null, false, false, null, recalibrate, SensitivityMethod.FiniteDifference, null, discountInterp, curveFitMethod, 
                                     (projection != null && projection.Length >0) ? projection[0]: null, new[] { true });
      double ss1;
      //Try catch block only used to check that data table is not null (if there are no tenors). Other exceptions allowed to surface 
      try
      {
        ss1 = (double)(dataTable.Rows[0])["Delta"];
      }
      catch (Exception)
      {

        ss1 = 0.0;
      }
      return ss1;
    }

    /// <summary>
    ///   Calculate the swap interest rate sensitivity.
    /// </summary>
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="discountInterp">Interpolation to use for calculation of sensitivities</param>
    /// <param name="curveFitMethod">Curve fitting method to use for calculation of sensitivities</param>
    /// <param name="projection"> Used to replace the discount curve with an equivalent overlay curve. This allows to impose a "functional" relationship
    /// between discount and reference, i.e. <m>\tilde{D}_T = \frac{D_T}{P_T}P_T = B_T P_T</m> where <m>D_T</m>is the calibrated discount
    /// curve and <m>P_T</m> is the calibrated projection curve. The curve <m>B_T = \frac{D_T}{P_T}</m> acts as a basis between 
    /// discount and projection. The discount curve then becomes sensitive to both quotes in the discount curve and the reference curve as the basis 
    /// is mantained constant when quotes are perturbed.  </param>
    /// <returns>Swap IR 01</returns>
    public static double
    SwapIR01(IPricer pricer, string measure, double upBump, double downBump, bool recalibrate, InterpScheme discountInterp,
      string curveFitMethod, params CalibratedCurve[] projection)
    {
      double ir01;
      DataTable dataTable = Discount(new IPricer[] { pricer }, measure, 0.0, upBump, downBump, false, true,
                                       BumpType.Uniform,
                                       null, false, false, null, recalibrate, SensitivityMethod.FiniteDifference, null,
                                       discountInterp, curveFitMethod, (projection != null && projection.Length >0) ? projection[0] : null, new[] { false });
      //Try catch block only used to check that data table is not null (if there are no tenors). Other exceptions allowed to surface 
      try
      {
        ir01 = (double) (dataTable.Rows[0])["Delta"];
      }
      catch (Exception)
      {
        return 0.0;
      }
      return ir01;
    }

    /// <summary>
    ///   Calculate the interest rate sensitivity.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical interest rate sensitivity.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    /// </remarks>
    ///
    /// <param name="evaluator">Pricer evaluator</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>IR 01</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Create evaluator
    ///   PricerEvaluator pricerEval = new PricerEvaluator(pricer, "ExpectedLoss");
    /// 
    ///   // Calculate interest rate sensitivity.
    ///   double IR01 = Sensitivities.IR01(
    ///     pricerEval, // Pricer evaluator
    ///     10.0,       // Based on 10bp up shift
    ///     0.0,        // No down shift
    ///     true        // Recalibrate credit curves
    ///   );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " IR01 = {0}", IR01 );
    /// </code>
    /// </example>
    ///
    private static double
    IR01( PricerEvaluator evaluator, double upBump, double downBump, bool recalibrate, params bool[] rescaleStrikes)
    {
      // Calculate

      string[] tenors = SelectYieldSpreadTenors(null, new PricerEvaluator[] {evaluator}, false);
      DataTable dataTable = Rate(new PricerEvaluator[] { evaluator },
        0.0, upBump, downBump,
        false, true, BumpType.Uniform, tenors, false, false, null, recalibrate, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    ///   Calculate the interest rate gamma.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical interest rate gamma.</para>
    ///
    ///   <para>Equivalent to <see cref="RateGamma(IPricer,string,double,double,bool, bool[])">
    ///   RateGamma(pricer, null, upBump, downBump, recalibrate, rescaleStrikes)</see></para>
    /// </remarks>
    ///
    /// <param name="pricer">IPricer</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Rate Gamma</returns>
    ///
    public static double
    RateGamma(IPricer pricer, double upBump, double downBump, bool recalibrate, params bool[] rescaleStrikes)
    {
      return RateGamma(new PricerEvaluator(pricer), upBump, downBump, recalibrate, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the interest rate gamma.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical interest rate gamma.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    /// </remarks>
    ///
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>IR 01</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the rate gamma for a
    /// <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate interest rate gamma.
    ///   double gamma = Sensitivities.RateGamma(
    ///     pricer,             // Pricer for CDO tranche
    ///     "Pv",               // Calculcate change in Pv
    ///     10.0,               // Based on 10bp up shift
    ///     0.0,                // No down shift
    ///     true                // Recalibrate credit curves
    ///     );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Rate Gamma = {0}", gamma );
    /// </code>
    /// </example>
    ///
    public static double
    RateGamma(IPricer pricer, string measure, double upBump, double downBump, bool recalibrate, params bool[] rescaleStrikes)
    {
      return RateGamma(new PricerEvaluator(pricer, measure), upBump, downBump, recalibrate, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the interest rate gamma.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical interest rate gamma.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    /// </remarks>
    ///
    /// <param name="evaluator">Pricer evaluator</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <example>
    /// <para>The following sample demonstrates calculating the rate gamma for a
    /// <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Create evaluator
    ///   PricerEvaluator pricerEval = new PricerEvaluator(pricer, "ExpectedLoss");
    /// 
    ///   // Calculate interest rate sensitivity.
    ///   double gamma = Sensitivities.RateGamma(
    ///     pricerEval, // Pricer evaluator
    ///     25.0,       // Based on 25bp up shift
    ///     25.0,       // and 25bp down shift
    ///     true        // Recalibrate credit curves
    ///   );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Rate Gamma = {0}", gamma );
    /// </code>
    /// </example>
    ///
    /// <returns>Rate Gamma</returns>
    ///
    private static double
    RateGamma(PricerEvaluator evaluator, double upBump, double downBump, bool recalibrate, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Rate(new PricerEvaluator[] { evaluator }, 0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, true, false, null, recalibrate, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Gamma"];
    }


    /// <summary>
    /// Calculate the Rate01 of the specified pricers.
    /// </summary>
    /// <param name="pricers">The pricers.</param>
    /// <param name="measure">The measure.</param>
    /// <param name="upBump">Up bump.</param>
    /// <param name="downBump">Down bump.</param>
    /// <param name="flags">The sensitivity flags.</param>
    /// <param name="discountInterp">The discount interpolator</param>
    /// <param name="curveFitMethod">The curve fit method.</param>
    /// <param name="projection">The projection.</param>
    /// <returns></returns>
    public static double[] Rate01(this IPricer[] pricers, string measure,
      double upBump, double downBump, SensitivityFlag flags,
      InterpScheme discountInterp, string curveFitMethod,
      CalibratedCurve projection)
    {
      using (var actions = new RestorationActions())
      {
        IPricer[] swapPricers, otherPricers;
        double[] results;
        actions.SetUpRate01(pricers, flags,
          out swapPricers, out otherPricers, out results);
        var recalibrate = (SensitivityFlag.Recalibrate & flags) != 0;
        // for swap pricers
        if (swapPricers != null)
        {
          var table = Discount(swapPricers, measure, 0.0, upBump, downBump,
            false, true, BumpType.Uniform, null, false, false, null,
            recalibrate, SensitivityMethod.FiniteDifference, null,
            discountInterp, curveFitMethod, projection, new[] {false});
          results.AddResults(table);
        }
        if (otherPricers != null)
        {
          var evaluators = CreateAdapters(otherPricers, measure);
          var tenors = SelectYieldSpreadTenors(null, evaluators, false);
          var table = Rate(evaluators, 0.0, upBump, downBump, false, true,
            BumpType.Uniform, tenors, false, false, null, recalibrate, null,
            null /*rescaleStrikes*/);
          results.AddResults(table);
        }
        return results;
      }
    }

    #endregion SummaryRiskMethods

    #region Rate Sensitivity

    /// <summary>
    ///   Calculate the interest rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical rate sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///   <para><see cref="Rate(IPricer[], string, double,double,double,bool,bool,BumpType,string[],bool,bool,string,bool,DataTable,InterpScheme,string,bool[])"/></para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Rate(
      IPricer pricer,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return Rate(new[] {pricer}, measure, initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, recalibrate, dataTable,
                  null, null, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the interest rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical rate sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para>The curves are initially bumped by the <paramref name="initialBump"/> size and recalibrated.
    ///   The curves are then bumped up and down per the specified bump sizes and the products repriced.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be in bump units. The bump unit size depends on the type
    ///   of products in the curves being bumped. For CDS the bump units are basis points. For Bonds
    ///   the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///
    ///   <para><paramref name="bumpTenors"/> can be used to specify the names of the individual tenors to
    ///   bump for each curve. If <paramref name="bumpTenors"/> is null all tenors are bumped.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve tenor (or all for parallel shift)</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the discount curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="curveFitmethod">The CurveFit Method to use </param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <param name="discountInterp"></param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate interest rate sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr Swap hedge.
    ///   //
    ///   DataTable dataTable2 = Sensitivities.Rate(
    ///     new IPricer[] { pricerEval }, // Pricer evaluator
    ///     "Pv",                         // Calculate change in PV
    ///     10.0,                         // Based on 10bp up shift
    ///     0.0,                          // No down shift
    ///     false,                        // Bumps are absolute bp
    ///     BumpType.Parallel,            // Bumps are parallel
    ///     null,                         // All tenors are bumped
    ///     false,                        // Dont bother with Gammas
    ///     true,                         // Do hedge calculation
    ///     "5 Year",                     // Hedge to 5yr tenor
    ///     true,                         // Recalibrate credit curves
    ///     SensitivityMethod.FiniteDifference // bump quotes and perform reprice
    ///     null                          // Create new table of results
    ///    );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable2.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable2.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Swap Spread Delta {1}, 5Yr Swap Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Rate(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      DataTable dataTable,
      InterpScheme discountInterp,
      string curveFitmethod,
      params bool[] rescaleStrikes
      )
    {
      var interpUpdater = new InterpUpdater {InterpScheme = discountInterp};
      CheckInterpScheme(pricers, ref interpUpdater, ref curveFitmethod);
      if ((interpUpdater != null) || (curveFitmethod != null))
      {
        var clonedPricers = pricers.CloneObjectGraph();
        var evaluators = CreateAdapters(clonedPricers, measure);
        using (NewCurveDependencyGraph(evaluators, calcHedge))
        {
          DiscountUpdater.UpdatePricerDiscountCurves(clonedPricers, interpUpdater, curveFitmethod, true);
          ResetRescaleStrikes(evaluators, rescaleStrikes);
          return DoCalculateSensitivityGeneric(evaluators, initialBump, upBump, downBump, bumpRelative,
            scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, recalibrate,
            PricerEvaluatorUtil.GetRateCurves, dataTable);
        }
      }
      return Rate(CreateAdapters(pricers, measure), initialBump, upBump, downBump, bumpRelative,
        scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, recalibrate, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the reference rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical and analytical rate sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para>The curves are initially bumped by the <paramref name="initialBump"/> size and recalibrated.
    ///   The curves are then bumped up and down per the specified bump sizes and the products repriced.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be in bump units. The bump unit size depends on the type
    ///   of products in the curves being bumped. For CDS the bump units are basis points. For Bonds
    ///   the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///
    ///   <para><paramref name="bumpTenors"/> can be used to specify the names of the individual tenors to
    ///   bump for each curve. If <paramref name="bumpTenors"/> is null all tenors are bumped.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve tenor (or all for parallel shift)</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the discount curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="spreadOnly">True to bump only basis spread</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate interest rate sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr Swap hedge.
    ///   //
    ///   DataTable dataTable2 = Sensitivities.Reference(
    ///     new IPricer[] { pricerEval }, // Pricer evaluator
    ///     "Pv",                         // Calculate change in PV
    ///     10.0,                         // Based on 10bp up shift
    ///     0.0,                          // No down shift
    ///     false,                        // Bumps are absolute bp
    ///     BumpType.Parallel,            // Bumps are parallel
    ///     null,                         // All tenors are bumped
    ///     false,                        // Dont bother with Gammas
    ///     true,                         // Do hedge calculation
    ///     "5 Year",                     // Hedge to 5yr tenor
    ///     true,                         // Recalibrate credit curves
    ///     SensitivityMethod.FiniteDifference // bump quotes and perform reprice
    ///     null                          // Create new table of results
    ///    );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable2.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable2.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Swap Spread Delta {1}, 5Yr Swap Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable Reference(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      params bool[] spreadOnly
      )
    {
      var evaluators = CreateAdapters(pricers, measure);
      foreach (var e in evaluators)
      {
        e.RateCurvesGetter = PropertyGetBuilder
          .CreateGetter<CalibratedCurve>(e.Pricer, "ReferenceCurve");
      }
      bool s0 = (spreadOnly != null) && (spreadOnly.Length > 0 && spreadOnly[0]);
      string[] tenors = SelectYieldSpreadTenors(bumpTenors, evaluators, s0);
      return CalculateSensitivityGeneric(evaluators, initialBump, upBump, downBump,
                                         bumpRelative, scaledDelta, bumpType, tenors, calcGamma, calcHedge, hedgeTenor,
                                         recalibrate,
                                         PricerEvaluatorUtil.GetReferenceCurves, dataTable);
    }


    /// <summary>
    ///   Calculate the reference rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical and analytical rate sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para>The curves are initially bumped by the <paramref name="initialBump"/> size and recalibrated.
    ///   The curves are then bumped up and down per the specified bump sizes and the products repriced.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be in bump units. The bump unit size depends on the type
    ///   of products in the curves being bumped. For CDS the bump units are basis points. For Bonds
    ///   the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///
    ///   <para><paramref name="bumpTenors"/> can be used to specify the names of the individual tenors to
    ///   bump for each curve. If <paramref name="bumpTenors"/> is null all tenors are bumped.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve tenor (or all for parallel shift)</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the discount curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate interest rate sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr Swap hedge.
    ///   //
    ///   DataTable dataTable2 = Sensitivities.Reference(
    ///     new IPricer[] { pricerEval }, // Pricer evaluator
    ///     "Pv",                         // Calculate change in PV
    ///     10.0,                         // Based on 10bp up shift
    ///     0.0,                          // No down shift
    ///     false,                        // Bumps are absolute bp
    ///     BumpType.Parallel,            // Bumps are parallel
    ///     null,                         // All tenors are bumped
    ///     false,                        // Dont bother with Gammas
    ///     true,                         // Do hedge calculation
    ///     "5 Year",                     // Hedge to 5yr tenor
    ///     true,                         // Recalibrate credit curves
    ///     SensitivityMethod.FiniteDifference // bump quotes and perform reprice
    ///     null                          // Create new table of results
    ///    );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable2.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable2.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Swap Spread Delta {1}, 5Yr Swap Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable Inflation(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      var evaluators =
        CreateAdapters(pricers, measure).Where(
          e =>
            {
              e.RateCurvesGetter = PropertyGetBuilder.CreateGetter<InflationCurve>(e.Pricer, "ReferenceCurve");
              return !ArrayUtil.IsNullOrEmpty(e.RateCurvesGetter(e.Pricer));
            }).ToArray();
      var tenors = SelectYieldSpreadTenors(bumpTenors, evaluators, false);
      Func<PricerEvaluator[], bool, IList<CalibratedCurve>> getCurves = (pricerEvaluators, mustExist) =>
                                                                          {
                                                                            var retVal = new List<CalibratedCurve>();
                                                                            foreach (
                                                                              var pricerEvaluator in pricerEvaluators)
                                                                            {
                                                                              var icurve =
                                                                                pricerEvaluator.RateCurvesGetter(
                                                                                  pricerEvaluator.Pricer);
                                                                              foreach (InflationCurve ic in icurve)
                                                                              {
                                                                                if (retVal.Contains(ic))
                                                                                  continue;
                                                                                retVal.Add(ic);
                                                                              }
                                                                            }
                                                                            return retVal;
                                                                          };
      return CalculateSensitivityGeneric(evaluators, initialBump, upBump, downBump,
                                         bumpRelative, scaledDelta, bumpType, tenors, calcGamma, calcHedge, hedgeTenor,
                                         recalibrate, getCurves, dataTable);
    }

    /// <summary>
    ///   Calculate the Fx basis curve sensitivity for a series of pricers.
    /// </summary>
    /// <remarks>
    ///   <para>Computes numerical fx curve sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///   <para>The curves are initially bumped by the <paramref name="initialBump"/> size and recalibrated.
    ///   The curves are then bumped up and down per the specified bump sizes and the products repriced.</para>
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be in bump units. The bump unit size depends on the type
    ///   of products in the curves being bumped. For CDS the bump units are basis points. For Bonds
    ///   the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///   <para><paramref name="bumpTenors"/> can be used to specify the names of the individual tenors to
    ///   bump for each curve. If <paramref name="bumpTenors"/> is null all tenors are bumped.</para>
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve tenor (or all for parallel shift)</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///   <para>If the fx curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    /// </remarks>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a <see cref="SwapLeg">swap leg</see>.</para>
    /// <code language="C#">
    ///   SwapLeg swapLeg;
    ///   SwapLegPricer pricer;
    ///   FxCurve discountCurve;
    ///
    ///   // Initialise swap leg, pricer and fx curve
    ///   // ...
    ///
    ///   // Calculate interest rate sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr Swap hedge.
    ///   //
    ///   DataTable dataTable2 = Sensitivities.Fx(
    ///     new IPricer[] { pricerEval }, // Pricer evaluator
    ///     "Pv",                         // Calculate change in PV
    ///     10.0,                         // Based on 10bp up shift
    ///     0.0,                          // No down shift
    ///     false,                        // Bumps are absolute bp
    ///     BumpType.Parallel,            // Bumps are parallel
    ///     null,                         // All tenors are bumped
    ///     false,                        // Dont bother with Gammas
    ///     true,                         // Do hedge calculation
    ///     "5 Year",                     // Hedge to 5yr tenor
    ///     true,                         // Recalibrate credit curves
    ///     SensitivityMethod.FiniteDifference // bump quotes and perform reprice
    ///     null                          // Create new table of results
    ///    );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable2.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable2.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Swap Spread Delta {1}, 5Yr Swap Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable FxCurve(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      var evaluators = CreateAdapters(pricers, measure);
      var curves = evaluators.GetFxForwardCurves(false).ToArray();
      if (bumpTenors != null && bumpTenors.Length != 0)
      {
        curves = curves.Where(c => c.Tenors.Any(t => bumpTenors.Contains(t.Name))).ToArray();
      }
      else
      {
        bumpTenors = curves.GetTenors(bumpType, FindLastMaturity(evaluators),
          t => t.Product is FxForward);
      }
      return CalculateSensitivityGeneric(evaluators, initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors,
        calcGamma, calcHedge, hedgeTenor, recalibrate,
        (e, b) => curves, dataTable);
    }

    /// <summary>
    /// Currency cross currency basis swap 01
    /// </summary>
    /// <remarks>
    ///   <para>The basis 01 is the change in PV (MTM) if the cross currency basis swap spreads
    ///   are increased by one basis point.</para>
    ///   <para>The basis sensitivity is calculated by bumping the underlying basis swap spreads, recalibrating
    ///   the forward fx, and then repricing the fx forward.</para>
    ///   <para>The basis 01 is calculated by bumping up the basis swap spreads and pricing, then bumping down
    ///   the basis swap spreads and pricing and returning the difference in value divided by the bump size.</para>
    ///   <para>The ccy argument allows filtering of results. If specified, sensitivities for
    ///   the specified currency are returned. Note that these sensitivities are in the
    ///   valuation currency.</para>
    /// </remarks>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    public static DataTable BasisAdjustment(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      var evaluators = CreateAdapters(pricers, measure);
      var curves = evaluators.GetBasisAdjustmentCurve(false).ToArray();
      if (bumpTenors != null && bumpTenors.Length != 0)
      {
        curves = curves.Where(c => c.Tenors.Any(t => bumpTenors.Contains(t.Name))).ToArray();
      }
      else
      {
        bumpTenors = curves.GetTenors(bumpType, FindLastMaturity(evaluators),
          t => !(t.Product is FxForward));
      }
      foreach (var e in evaluators)
      {
        e.RateCurvesGetter = PropertyGetBuilder
          .CreateBasisCurveGetter(e.Pricer.GetType()).Get;
      }
      return CalculateSensitivityGeneric(evaluators, initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors,
        calcGamma, calcHedge, hedgeTenor, recalibrate,
        (e, b) => curves, dataTable);
    }

    private static Dt FindLastMaturity(PricerEvaluator[] pricers)
    {
      return EvalAllRateTenors(pricers) ? Dt.Empty : LastMaturity(pricers);
    }

    private static string[] GetTenors(
      this IEnumerable<CalibratedCurve> curves,
      BumpType bumpType, Dt lastMaturity,
      Func<CurveTenor,bool> predicate)
    {
      if (bumpType == BumpType.ByTenor && !lastMaturity.IsEmpty())
      {
        return curves.SelectMany(c => c.GetTenorsUntil(lastMaturity)
          .Where(predicate).Select(t => t.Name)).Distinct().ToArray();
      }
      return curves.SelectMany(c => c.Tenors.Where(predicate).Select(t => t.Name))
        .Distinct().ToArray();
    }

    private static IEnumerable<CurveTenor> GetTenorsUntil(
      this CalibratedCurve curve, Dt lastMaturity)
    {
      var tenors = curve.Tenors;
      int count = tenors.Count;
      for (int i = count - 1; i > 0; --i)
      {
        if (tenors[i - 1].Maturity > lastMaturity) --count;
        else break;
      }
      for (int i = 0; i < count; ++i)
        yield return tenors[i];
    }

    /// <summary>
    ///   Calculate the discount curve sensitivity for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="curveFitMethod">The Curve Fit method to use for the calculation of the sensitivities</param> 
    /// <param name="discountInterp">The Discount Interp Scheme to use </param>
    /// <param name="projection">Projection curve used to obtain sensitivities to tenors on the projection curve</param>
    /// <param name="spreadOnly">True to only include products quoted as YieldSpread in the sensitivity calculation</param>
    /// <returns>Datatable of results</returns>
    /// <remarks>When proj parameter is provided sensitivities will be calculated with respect to tenors in the given projection curve. This is done by assuming that the forward rates <m>F^p(T_i)</m> of
    /// the projection curve are given by <m>F^p(T_i) = F^d(T_i) + b_i</m> where <m>F^d(T_i)</m> are the forwards of the discount curve and <m>b_i</m> is a forward basis. The discount curve then becomes 
    /// sensitive to both quotes in the discount curve and the reference curve. The basis between forwards is mantained constant when quotes are perturbed. </remarks>
    public static DataTable Discount(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      InterpScheme discountInterp,
      string curveFitMethod,
      CalibratedCurve projection,
      params bool[] spreadOnly
    )
    {
      return Discount(pricers, measure, new[] { initialBump }, new[] { upBump }, new[] { downBump },
        bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge,
        hedgeTenor, recalibrate, method, dataTable, discountInterp,
        curveFitMethod, projection, spreadOnly);
    }

    /// <summary>
    ///   Calculate the discount curve sensitivity for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="curveFitMethod">The Curve Fit method to use for the calculation of the sensitivities</param> 
    /// <param name="discountInterp">The Discount Interp Scheme to use </param>
    /// <param name="projection">Projection curve used to obtain sensitivities to tenors on the projection curve</param>
    /// <param name="spreadOnly">True to only include products quoted as YieldSpread in the sensitivity calculation</param>
    /// <returns>Datatable of results</returns>
    /// <remarks>When proj parameter is provided sensitivities will be calculated with respect to tenors in the given projection curve. This is done by assuming that the forward rates <m>F^p(T_i)</m> of
    /// the projection curve are given by <m>F^p(T_i) = F^d(T_i) + b_i</m> where <m>F^d(T_i)</m> are the forwards of the discount curve and <m>b_i</m> is a forward basis. The discount curve then becomes 
    /// sensitive to both quotes in the discount curve and the reference curve. The basis between forwards is mantained constant when quotes are perturbed. </remarks>
    public static DataTable Discount(
      IPricer[] pricers,
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      InterpScheme discountInterp,
      string curveFitMethod,
      CalibratedCurve projection,
      params bool[] spreadOnly
      )
    {
      var interpUpdater = new InterpUpdater {InterpScheme = discountInterp};
      CheckInterpScheme(pricers, ref interpUpdater, ref curveFitMethod);
      var cloned = CloneUtil.CloneObjectGraph(pricers, projection);
      pricers = cloned.Item1;
      projection = cloned.Item2;
      var evaluators = CreateAdapters(pricers, measure);
      using (NewCurveDependencyGraph(evaluators, calcHedge))
      {
        DiscountUpdater.UpdatePricerDiscountCurves(pricers, interpUpdater, curveFitMethod);
        if (projection != null)
          DiscountUpdater.UpdateDiscountCurves(new[] {projection}, interpUpdater, curveFitMethod);
        DiscountUpdater.ReplaceCurves(pricers, projection);
        foreach (DiscountCurve disc in DiscountUpdater.PricerDiscountCurves(pricers))
          disc.ReFit(0);
        foreach (var e in evaluators)
        {
          e.RateCurvesGetter = PropertyGetBuilder
            .CreateGetter<CalibratedCurve>(e.Pricer, "DiscountCurve");
        }
        bool s0 = spreadOnly != null && spreadOnly.Length > 0 && spreadOnly[0];
        string[] tenors = bumpTenors!=null&&bumpTenors.Length!=0 ? bumpTenors : (
        projection != null
                          ? Array.ConvertAll(DiscountUpdater.SelectTenors(new CalibratedCurve[]{projection}, ten => true).ToArray(),
                                             ten => ten.Name)
              : SelectYieldSpreadTenors(bumpTenors, evaluators, s0));
        if (tenors.Length == 0)
        {
          return dataTable ?? (ResultTable.CreateResultTable(calcGamma, calcHedge));
        }
        return DoCalculateSensitivityGeneric(evaluators, initialBump, upBump,
                                             downBump, bumpRelative, scaledDelta, bumpType, tenors,
                                             calcGamma, calcHedge, hedgeTenor, recalibrate,
                                             PricerEvaluatorUtil.GetRateCurves, dataTable);
      }
    }

    /// <summary>
    ///   Calculate the zero rate sensitivity on discount curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateDiscount(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      return ZeroRateDiscount(pricers, null, measure,
        new[] { initialBump }, new[] { upBump }, new[] { downBump }, 
        bumpRelative, scaledDelta, bumpType, bumpTenors, 
        calcGamma, null, compoundFreq, recalibrate, method, dataTable, null);
    }

    /// <summary>
    ///   Calculate the zero rate sensitivity on discount curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="reportTenors">Array of reporting tenors.  If no tenors are specified then the reporting is done on the bump tenors</param>
    /// <param name="reportCal">Reporting tenor grid calendar</param>
    /// <param name="reportRoll">Reporting tenor grid business daycount convention</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateDiscountReporting(
      IPricer[] pricers,
      string[] curveNames,
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      string[] reportTenors,
      Calendar reportCal,
      BDConvention reportRoll,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      Func<DataTable, DataTable> reportingFunc = null;
      if (reportTenors.Any())
      {
        reportingFunc =
          new ZeroRateReporting(pricers[0].AsOf, reportTenors, reportCal, reportRoll, bumpTenors).ReportKeyTenors;
      }
      return ZeroRateDiscount(pricers, curveNames, measure,
        initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors,
        calcGamma, interp, compoundFreq, recalibrate, method, dataTable, reportingFunc);
    }

    /// <summary>
    ///   Calculate the zero rate sensitivity on discount curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="tableFunc">Function to process results table</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateDiscount(
      IPricer[] pricers,
      string[] curveNames, 
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      Func<DataTable, DataTable> tableFunc = null
      )
    {
      DataTable dt = null;
      var evaluators = CreateAdapters(pricers, measure);
      
      using (new CurveDependencyGraph(evaluators.GetPricerCurves()))
      {
        foreach (DiscountCurve disc in DiscountUpdater.PricerDiscountCurves(pricers))
          disc.ReFit(0);

        PricerEvaluatorUtil.InvokeZeroRateSensitivityCalc(pricers, bumpTenors, "DiscountCurve", interp, compoundFreq, f =>
        {

          foreach (var e in evaluators)
          {
            e.RateCurvesGetter = PricerEvaluatorUtil.ZeroCurveGetter("DiscountCurve");
          }

          var tens = DiscountUpdater.SelectTenors(PricerEvaluatorUtil.GetZeroCurves(evaluators, false).ToArray(),
                                                  ten => ten.CurrentQuote.Type != QuotingConvention.YieldSpread);

          string[] tenors = Array.ConvertAll(tens.ToArray(), ten => ten.Name);
          
      
          if (tenors.Length == 0)
          {
            if (dataTable == null)
            {
              dataTable = ResultTable.CreateResultTable(calcGamma, false);
            }
            dt = dataTable;
          }
          else
          {
            dt = DoCalculateSensitivityGeneric(evaluators, initialBump, upBump,
                                                 downBump, bumpRelative, scaledDelta, bumpType, tenors,
                                                 calcGamma, false, "matching", recalibrate,
                                                 PricerEvaluatorUtil.GetRateCurves, dataTable);
          }
        });
      }

      return tableFunc != null ? (tableFunc(dt)) : dt;
    }

    /// <summary>
    ///   Calculate the zero-rate sensitivity on reference curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity calculation method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateReference(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      return ZeroRateReference(pricers, null, measure, 
        new[] { initialBump }, new[] { upBump }, new[] { downBump }, 
        bumpRelative, scaledDelta, bumpType, bumpTenors,
        calcGamma, null, compoundFreq, recalibrate, method, dataTable, null);
    }

    /// <summary>
    ///   Calculate the zero rate sensitivity on reference curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="reportTenors">Array of reporting tenors.  If no tenors are specified then the reporting is done on the bump tenors</param>
    /// <param name="reportCal">Reporting tenor grid calendar</param>
    /// <param name="reportRoll">Reporting tenor grid business daycount convention</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateReferenceReporting(
      IPricer[] pricers,
      string[] curveNames,
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      string[] reportTenors,
      Calendar reportCal,
      BDConvention reportRoll,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable
      )
    {
      Func<DataTable, DataTable> reportingFunc = null;
      if (reportTenors.Any())
      {
        reportingFunc =
          new ZeroRateReporting(pricers[0].AsOf, reportTenors, reportCal, reportRoll, bumpTenors).ReportKeyTenors;
      }
      return ZeroRateReference(pricers, curveNames, measure,
        initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors,
        calcGamma, interp, compoundFreq, recalibrate, method, dataTable, reportingFunc);
    }
    
    /// <summary>
    ///   Calculate the zero-rate sensitivity on reference curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity calculation method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="tableFunc">Function to process results table</param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateReference(
      IPricer[] pricers,
      string[] curveNames, 
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      Func<DataTable, DataTable> tableFunc = null
      )
    {
      DataTable dt = null;
      PricerEvaluatorUtil.InvokeZeroRateSensitivityCalc(pricers, bumpTenors, "ReferenceCurve", interp, compoundFreq, f=>
      {
        var evaluators = CreateAdapters(pricers, measure);
          foreach (var e in evaluators)
          {
            e.RateCurvesGetter = PricerEvaluatorUtil.ZeroCurveGetter("ReferenceCurve");
          }

          var tens = DiscountUpdater.SelectTenors(PricerEvaluatorUtil.GetZeroCurves(evaluators, false).ToArray(),
                                                  ten => ten.CurrentQuote.Type != QuotingConvention.YieldSpread);

          string[] tenors = Array.ConvertAll(tens.ToArray(), ten => ten.Name);

          if (tenors.Length == 0)
          {
            if (dataTable == null)
            {
              dataTable = ResultTable.CreateResultTable(calcGamma, false);
            }
            dt = dataTable;
          }
          else
          {
            dt = DoCalculateSensitivityGeneric(evaluators, initialBump, upBump,
                                                 downBump, bumpRelative, scaledDelta, bumpType, tenors,
                                                 calcGamma, false, "matching", recalibrate,
                                                 PricerEvaluatorUtil.GetReferenceCurves, dataTable);
          }
      });

      return tableFunc != null ? (tableFunc(dt)) : dt;
    }

    /// <summary>
    ///   Calculate the zero-rate sensitivity for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes"></param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRate(
      IPricer[] pricers,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return ZeroRate(pricers, null, measure, 
        new[] { initialBump }, new [] { upBump }, new [] { downBump }, 
        bumpRelative, scaledDelta, bumpType, bumpTenors, 
        calcGamma, null, compoundFreq, recalibrate, method, dataTable, null, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the zero rate sensitivity on discount curve for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="reportTenors">Array of reporting tenors.  If no tenors are specified then the reporting is done on the bump tenors</param>
    /// <param name="reportCal">Reporting tenor grid calendar</param>
    /// <param name="reportRoll">Reporting tenor grid business daycount convention</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method">Sensitivity method</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes"></param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRateReporting(
      IPricer[] pricers,
      string[] curveNames,
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      string[] reportTenors,
      Calendar reportCal,
      BDConvention reportRoll,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      params bool[] rescaleStrikes
    )
    {
      Func<DataTable, DataTable> reportingFunc = null;
      if (reportTenors!=null && reportTenors.Any() && bumpType == BumpType.ByTenor) // Only for by tenor calculations
      {
        reportingFunc =
          new ZeroRateReporting(pricers[0].AsOf, reportTenors, reportCal, reportRoll, bumpTenors).ReportKeyTenors;
      }
      return ZeroRate(pricers, curveNames, measure,
        initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors,
        calcGamma, interp, compoundFreq, recalibrate, method, dataTable, reportingFunc, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the zero-rate sensitivity for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes"></param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRate(
      IPricer[] pricers,
      string[] curveNames,
      string measure,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      params bool[] rescaleStrikes
    )
    {
      return ZeroRate(pricers, curveNames, measure,
        new[] { initialBump }, new[] { upBump }, new[] { downBump },
        bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma,
        interp, compoundFreq, recalibrate, method, dataTable, null, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the zero-rate sensitivity for a series of pricers.
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveNames">Array of curve to bump for the sensitivity calculation.  If no curves are specified then all curves are bumped</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundFreq">Compounding frequency of zero rate</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="method"></param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="tableFunc">Function to process results table</param>
    /// <param name="rescaleStrikes"></param>
    /// <returns>Datatable of results</returns>
    public static DataTable ZeroRate(
      IPricer[] pricers,
      string[] curveNames,
      string measure,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      Interp interp,
      Frequency compoundFreq,
      bool recalibrate,
      SensitivityMethod method,
      DataTable dataTable,
      Func<DataTable, DataTable> tableFunc = null,
      params bool[] rescaleStrikes
      )
    {
      DataTable dt = null;

      pricers.GetRateAndInflationCurves(curveNames).InvokeZeroRateSensitivityCalc(bumpTenors, interp, compoundFreq, f =>
      {
        var evaluators = CreateAdapters(pricers, measure);
        using (new CurveDependencyGraph(evaluators.GetPricerCurves()))
        {
          if (f == null) f = PricerEvaluatorUtil.GetZeroCurves;
          var tens = DiscountUpdater.SelectTenors(f(evaluators, false).ToArray(),
                                                  ten => ten.CurrentQuote.Type != QuotingConvention.YieldSpread);

          string[] tenors = Array.ConvertAll(tens.ToArray(), ten => ten.Name);

          if (tenors.Length == 0)
          {
            if (dataTable == null)
            {
              dataTable = ResultTable.CreateResultTable(calcGamma, false);
            }
            dt = dataTable;
          }
          else
          {
            bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikes);
            try
            {
              dt = DoCalculateSensitivityGeneric(evaluators, initialBump, upBump,
                                     downBump, bumpRelative, scaledDelta, bumpType, tenors,
                                     calcGamma, false, "matching", recalibrate,
                                     f, dataTable);

            }
            catch (Exception)
            {

              Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
            }
          }
        }
      });

      return tableFunc != null ? (tableFunc(dt)) : dt;
    }

    #endregion

    #region Private implementation

    /// <summary>
    ///   Calculate sensitivities for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical rate sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on curves.</para>
    ///
    ///   <para>The curves are initially bumped by the <paramref name="initialBump"/> size and recalibrated.
    ///   The curves are then bumped up and down per the specified bump sizes and the products repriced.</para>
    ///
    ///   <para>If <paramref name="bumpRelative"/> true, the bumps are taken to be relative (in percent),
    ///   otherwise the bumps are taken to be in bump units. The bump unit size depends on the type
    ///   of products in the curves being bumped. For CDS the bump units are basis points. For Bonds
    ///   the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///
    ///   <para><paramref name="bumpTenors"/> can be used to specify the names of the individual tenors to
    ///   bump for each curve. If <paramref name="bumpTenors"/> is null all tenors are bumped.</para>
    ///
    ///   <para>If <paramref name="calcGamma"/> is true, gamma sensitivities are calculated based on
    ///   the specified up and down bumps.</para>
    ///
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve</description></item>
    ///     <item><term>Curve Name</term><description>Name of curve</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve tenor (or all for parallel shift)</description></item>
    ///     <item><term>Value Name</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>Hedge Delta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>Hedge Notional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the discount curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    ///    <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="curveGet"> Curve getter function </param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the rate sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOHeterogeneousPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate interest rate sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr Swap hedge.
    ///   //
    ///   PricerEvaluator pricerEval = new PricerEvaluator(pricer, "ExpectedLOss");
    ///   DataTable dataTable2 = Sensitivities.Rate(
    ///     new PricerEvaluator[] { pricerEval },   // Pricer evaluator
    ///     10.0,                         // Based on 10bp up shift
    ///     0.0,                          // No down shift
    ///     false,                        // Bumps are absolute bp
    ///     BumpType.Parallel,            // Bumps are parallel
    ///     null,                         // All tenors are bumped
    ///     false,                        // Dont bother with Gammas
    ///     true,                         // Do hedge calculation
    ///     "5 Year",                     // Hedge to 5yr tenor
    ///     true,                         // Recalibrate credit curves
    ///     null                          // Create new table of results
    ///    );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable2.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable2.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Swap Spread Delta {1}, 5Yr Swap Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    private static DataTable CalculateSensitivityGeneric(
      PricerEvaluator[] evaluators,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      Func<PricerEvaluator[], bool, IList<CalibratedCurve>> curveGet,
      DataTable dataTable)
    {
      using (log4net.ThreadContext.Stacks["NDC"].Push("InSensitivityGeneric"))
      using (NewCurveDependencyGraph(evaluators, calcHedge))
      {
        return DoCalculateSensitivityGeneric(evaluators, 
          new [] { initialBump }, new[] { upBump }, new[] { downBump }, 
          bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma,
          calcHedge, hedgeTenor, recalibrate, curveGet, dataTable);
      }
    }


    private static CurveDependencyGraph NewCurveDependencyGraph(
      IEnumerable<PricerEvaluator> evaluators, bool calcHedges)
    {
      var curves = evaluators.GetPricerCurves();
      if (calcHedges)
      {
        var ca = curves.Distinct().ToArray();
        var hcurves = ca.Where(crv => crv != null && !(crv is FxCurve) &&
          crv.Calibrator != null && crv.Tenors != null)
          .SelectMany(
            crv => crv.Tenors
              .Select(ten => crv.Calibrator.GetPricer(crv, ten.Product))
              .Where(pricer => pricer != null)
              .Select(pricer => new PricerEvaluator(pricer))
              .GetPricerCurves());
        curves = ca.Concat(hcurves).Distinct().ToArray();
      }
      return new CurveDependencyGraph(curves);
    }

    private static IEnumerable<CalibratedCurve> GetPricerCurves(
      this IEnumerable<PricerEvaluator> evaluators)
    {
      foreach (var pricer in evaluators.Where(p=>p!=null))
      {
        if(logger.IsDebugEnabled)
        {
          logger.DebugFormat("Pricer {0}", pricer.Pricer.Product.Description);
        }
        IEnumerable<CalibratedCurve> curves = pricer.FxCurve;
        if (curves != null)
        {
          foreach (var c in curves) if (c != null) yield return c;
        }
        Func<IPricer, CalibratedCurve[]> rateCurvesGetter = 
          PropertyGetBuilder.CreateDiscountGetter(pricer.Pricer.GetType()).Get;
        curves = rateCurvesGetter(pricer.Pricer);
        if (curves != null)
        {
          foreach (var c in curves) if (c != null) yield return c;
        }
      }
    }

    /// <summary>
    /// Calculate the rate sensitivity
    /// </summary>
    private static DataTable DoCalculateSensitivityGeneric(
      PricerEvaluator[] evaluators,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      Func<PricerEvaluator[], bool, IList<CalibratedCurve>> curveGet,
      DataTable dataTable
    )
    {
      return DoCalculateSensitivityGeneric(evaluators, 
        new[] { initialBump }, new[] { upBump }, new[] { downBump },
        bumpRelative, scaledDelta, bumpType,
        bumpTenors, calcGamma, calcHedge, hedgeTenor, recalibrate, curveGet, dataTable);
    }

    /// <summary>
    ///  Validate bump tenors are the same size as bumps (if bumps is an array of bumps)
    /// </summary>
    /// <param name="bumps"></param>
    /// <param name="bumpTenors"></param>
    public static void ValidateBumps(double[] bumps, string[] bumpTenors)
    {
      if (!bumps.Any(x => Math.Abs(x) > 0.0) || bumps.Length <= 1) return;
      if (bumpTenors == null || bumpTenors.Length != bumps.Length)
        throw new ArgumentException("Bump size array and bump tenor array must be the same size.");
    }

    /// <summary>
    /// Calculate the rate sensitivity
    /// </summary>
    private static DataTable DoCalculateSensitivityGeneric(
      PricerEvaluator[] evaluators,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      Func<PricerEvaluator[], bool, IList<CalibratedCurve>> curveGet,
      DataTable dataTable
      )
    {
      var timer = new Timer();
      timer.start();
      logger.DebugFormat("Calculating rate sensitivities type={0}, up={1}, down={2}", bumpType, upBump, downBump);
      // Validation
      ValidateBumps(initialBump, bumpTenors);
      ValidateBumps(upBump, bumpTenors);
      ValidateBumps(downBump, bumpTenors);

      if (upBump != null && downBump != null && upBump.Sum() == -downBump.Sum())
        throw new ArgumentException("Up-bump size and down-bump size can not be equal.");

      // Get discount curves for this pricer set
      IList<CalibratedCurve> c = curveGet(evaluators, false);
      CalibratedCurve[] curves = c.ToArray();

      // Get dependent credit curves for this pricer set
      SurvivalCurve[] dependentCurves = null;
      if (recalibrate)
      {
        IList<SurvivalCurve> sc = PricerEvaluatorUtil.GetSurvivalCurves(evaluators, false);
        dependentCurves = sc.ToArray();
      }

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = ResultTable.CreateResultTable(calcGamma, calcHedge);
      }

      if ((evaluators == null) || (evaluators.Length == 0) || (curves == null) || (curves.Length == 0))
      {
        timer.stop();
        logger.InfoFormat("Completed rate sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }
      //do fast calculation if possible
      var originalApprox = new List<bool>();
      foreach (var pe in evaluators)
      {
        var p = pe.Pricer as PricerBase;
        if (p == null)
          continue;
        originalApprox.Add(p.ApproximateForFastCalculation);
        p.ApproximateForFastCalculation = true;
      }

      if (bumpType == BumpType.Uniform)
      {
        DoCalculateSensitivityGeneric(evaluators, initialBump, upBump,
          downBump, bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma,
          calcHedge, hedgeTenor, curves, dependentCurves, dataTable);
      }
      else
      {
        for (int i = 0; i < curves.Length; ++i)
        {
          // Do it one curve at a time in case they have common dependent.
          DoCalculateSensitivityGeneric(evaluators, initialBump, upBump,
            downBump, bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma,
            calcHedge, hedgeTenor, new[] {curves[i]}, dependentCurves, dataTable);
        }
      }

      //repristinate 
      int idx = 0;
      foreach (var pe in evaluators)
      {
        var p = pe.Pricer as PricerBase;
        if (p == null)
          continue;
        p.ApproximateForFastCalculation = originalApprox[idx];
        ++idx;
      }

      timer.stop();
      logger.InfoFormat("Completed rate sensitivity in {0}s", timer.getElapsed());
      
      return dataTable;
    }

    /// <summary>
    /// Calculate the rate sensitivity for a single curve
    /// </summary>
    private static void DoCalculateSensitivityGeneric(
      PricerEvaluator[] evaluators,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      CalibratedCurve[] curves,
      SurvivalCurve[] dependentCurves,
      DataTable dataTable
      )
    {
      int numTasks = 1; //(until we reintroduce grid) evaluators.Length;
      int groupSize = (int) Math.Ceiling((double) evaluators.Length/(double) numTasks);

      int currentGroup = 0;
      PricerEvaluator[][] pricerGroups = new PricerEvaluator[numTasks][];
      for (int i = 0; i < evaluators.Length; i += groupSize)
      {
        int startIdx = i;
        int endIdx = Math.Min(evaluators.Length, startIdx + groupSize);
        PricerEvaluator[] pricerGroup = new PricerEvaluator[endIdx - startIdx];
        for (int j = startIdx; j < endIdx; j++)
        {
          pricerGroup[j - startIdx] = evaluators[j];
        }
        pricerGroups[currentGroup] = pricerGroup;
        currentGroup++;
      }

      ArrayList tasks = new ArrayList();
      for (int i = 0; i < pricerGroups.GetLength(0); i++)
      {
        bool evalAllRateTenors = EvalAllRateTenors(pricerGroups[i]);

        CurveTaskState taskState = new CurveTaskState(pricerGroups[i], curves, dependentCurves,
                                                      initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType,
                                                      bumpTenors, calcGamma,
                                                      calcHedge, hedgeTenor, QuotingConvention.None, evalAllRateTenors);

        tasks.Add(taskState);
      }


      // Add results to our DataTable (order should be same as if no grid)
      for (int i = 0; i < numTasks; i++)
      {
        object[][] result = (object[][]) CurveTask(tasks[i]);
        for (int j = 0; j < result.GetLength(0); j++)
        {
          if (result[j] != null)
          {
            DataRow dataRow = dataTable.NewRow();

            dataRow["Category"] = result[j][0];
            dataRow["Element"] = result[j][1];
            dataRow["Curve Tenor"] = result[j][2];
            dataRow["Pricer"] = result[j][3];
            dataRow["Delta"] = result[j][4];
            if (calcGamma)
              dataRow["Gamma"] = result[j][5];
            if (calcHedge)
            {
              dataRow["Hedge Tenor"] = result[j][6];
              dataRow["Hedge Delta"] = result[j][7];
              dataRow["Hedge Notional"] = result[j][8];
            }

            dataTable.Rows.Add(dataRow);
          }
        }
      }
    }

    private static bool EvalAllRateTenors(PricerEvaluator[] evaluators)
    {
      bool result = false;
      foreach (PricerEvaluator evaluator in evaluators)
      {
        result |= ((int)evaluator.PricerFlags & (int)PricerFlags.SensitivityToAllRateTenors) > 0 ;
      }
      return result;
    }

    /// <summary>
    ///  Wrapper method between Rate(IPricer,...) and Rate(PricerEvaluator,...)
    ///  Its function is to set and restore rescake strikes flag for CDO Pricer
    /// </summary>
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns></returns>
    private static DataTable Rate(PricerEvaluator[] evaluators, double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors, bool calcGamma, bool calcHedge,
      string hedgeTenor, bool recalibrate, DataTable dataTable, bool[] rescaleStrikes)
    {
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikes);
      DataTable dt = null;
      try
      {
        dt = CalculateSensitivityGeneric(evaluators, initialBump, upBump, downBump, bumpRelative, scaledDelta,
          bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, recalibrate, PricerEvaluatorUtil.GetRateCurves, dataTable);
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }
      return dt;
    }
    
    private static string[] SelectYieldSpreadTenors(string[] bumpTenors, PricerEvaluator[] evaluators, bool spreadOnly)
    {
      if (bumpTenors != null && bumpTenors.Length > 0)
        return bumpTenors;
      CalibratedCurve[] dc = PricerEvaluatorUtil.GetRateCurves(evaluators, false).ToArray();
      List<CurveTenor> tens;
      if (spreadOnly)
      {
        tens = DiscountUpdater.SelectTenors(dc, ten => ten.CurrentQuote.Type == QuotingConvention.YieldSpread);
      }
      else
      {
        tens = DiscountUpdater.SelectTenors(dc, ten => ten.CurrentQuote.Type != QuotingConvention.YieldSpread);
      }
      return Array.ConvertAll(tens.ToArray(), ten => ten.Name);
    }

    private static bool IsNotSerializable(IEnumerable<IPricer> pricers)
    {
      foreach(var pricer in pricers)
      {
        if(pricer.GetType().FullName.EndsWith("CashflowCDOPricer"))
          return true;
      }
      return false;
    }
    private static void CheckInterpScheme(IPricer[] pricers,
      ref InterpUpdater interpUpdater, ref string curveFitmethod)
    {
      var discountInterp = interpUpdater.InterpScheme;
      if (discountInterp == null)
      {
        // If no interp scheme specified and curveFitmethod is "Keep"....
        if (String.Compare(curveFitmethod, "Keep", true) == 0)
        {
          // We keep old interp schemes and curve fit method.
          interpUpdater = null;
          curveFitmethod = null;
          return;
        }
        // If curveFitmethod method is neither Bootstrap nor IterativeBootstrap,
        // or if the pricers are not serializable, we don't switch to local interp.
        if ((curveFitmethod != null && !curveFitmethod.EndsWith(
          "Bootstrap", true, null)) || IsNotSerializable(pricers))
        {
          interpUpdater = null;
        }
      }
      return;
    }
    #endregion 

    #region Backward Compatible
    /// <summary>
    ///   Calculate the interest rate sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical rate sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///   <para><see cref="Rate(IPricer[], string, double,double,double,bool,bool,BumpType,string[],bool,bool,string,bool,DataTable,InterpScheme,string,bool[])"/></para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="initialBump">Initial bump size</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="scaledDelta">Deltas and gammas are scaled by the actual bump in basis points or not</param>
    /// <param name="bumpType">Type of bump to apply</param>
    /// <param name="bumpTenors">List of individual tenors to bump (null or empty = all tenors)</param>
    /// <param name="calcGamma">Calculate gamma if true</param>
    /// <param name="calcHedge">Calculate hedge equivalents if true</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations (matching = matching bumped tenors)</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Rate(
      IPricer pricer,
      double initialBump,
      double upBump,
      double downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      bool recalibrate,
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return Rate(CreateAdapters(pricer, null), initialBump, upBump, downBump, bumpRelative,
        scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, recalibrate, dataTable, rescaleStrikes);
    }
    #endregion Backward Compatible

  } // class Sensitivities

  #region RestorationActions
  /// <summary>
  ///  A utility class to perform a set of actions at disposal.
  /// </summary>
  class RestorationActions : IDisposable
  {
    private readonly IList<Action> restoreActions_ = new List<Action>();

    internal IList<Action> Actions { get { return restoreActions_; } }

    void IDisposable.Dispose()
    {
      var actions = restoreActions_;
      if (actions == null) return;
      int count = actions.Count;
      for (int i = 0; i < count; ++i)
      {
        var action = actions[i];
        if (action != null)
        {
          action();
          actions[i] = null;
        }
      }
      return;
    }
  }

  #endregion RestorationActions

  #region Rate01 Utilities

  static class Rate01Utility
  {
    internal static void SetUpRate01(
        this RestorationActions restore,
        IPricer[] pricers,
      SensitivityFlag flags,
      out IPricer[] swapLegPricerArray,
      out IPricer[] otherPricerArray,
      out double[] results)
    {
      swapLegPricerArray = otherPricerArray = null;
      if (pricers == null || pricers.Length == 0)
      {
        results = null;
        return;
      }
      results = new double[pricers.Length];
      bool forcePrincipalExcahnge = (SensitivityFlag.ForcePrincipalExcahnge &
        flags) != 0;
      var actions = restore.Actions;
      var swapPricers = new List<IPricer>();
      var otherPricers = new List<IPricer>();
      for (int i = 0; i < pricers.Length; ++i)
      {
        var pricer = pricers[i];
        if (pricer == null) continue;
        var name = i.ToString();
        if (pricer is BondPricer)
        {
          AddBond(actions, (BondPricer)pricer, name);
          otherPricers.Add(pricer);
          continue;
        }
        if (pricer is SwapLegPricer)
        {
          if (forcePrincipalExcahnge)
          {
            AddSwapLeg(actions, (SwapLegPricer) pricer, name);
            swapPricers.Add(pricer);
          }
          else
          {
            AddProduct(actions, pricer.Product, name);
            otherPricers.Add(pricer);
          }
          continue;
        }
        if (pricer is SwapPricer)
        {
          var swapPricer = (SwapPricer)pricer;
          if (forcePrincipalExcahnge)
          {
            actions.AddSwapLeg(swapPricer.PayerSwapPricer, name);
            swapPricers.Add(swapPricer.PayerSwapPricer);
            actions.AddSwapLeg(swapPricer.ReceiverSwapPricer, name);
            swapPricers.Add(swapPricer.ReceiverSwapPricer);
          }
          else
          {
            actions.AddProduct(swapPricer.PayerSwapPricer.Product, name);
            otherPricers.Add(swapPricer.PayerSwapPricer);
            actions.AddProduct(swapPricer.ReceiverSwapPricer.Product, name);
            otherPricers.Add(swapPricer.ReceiverSwapPricer);
          }
          if (swapPricer.SwaptionPricer != null)
          {
            actions.AddProduct(swapPricer.SwaptionPricer.Product, name);
            otherPricers.Add(swapPricer.SwaptionPricer);
          }
          continue;
        }
        if (pricer is CDSOptionPricer)
        {
          var csoPricer = (CDSOptionPricer)pricer;
          Dt jumpDate = csoPricer.SurvivalCurve.JumpDate;
          if (csoPricer.CDSOption.Knockout && !jumpDate.IsEmpty() &&
            jumpDate < csoPricer.CDSOption.Expiration)
          {
            results[i] = Double.NaN;
            continue; // no restore action.
          }
          // Fall to catch all
        }
        // Catch all other cases.
        AddProduct(actions, pricer.Product, name);
        otherPricers.Add(pricer);
      }
      // Done set up.
      if (swapPricers.Count > 0) swapLegPricerArray = swapPricers.ToArray();
      if (otherPricers.Count > 0) otherPricerArray = otherPricers.ToArray();
    }

    private static void AddBond(
       this ICollection<Action> actions,
       BondPricer pricer, string name)
    {
      var desc = pricer.Product.Description;
      if (pricer.EnableZSpreadAdjustment)
      {
        var origSpread = pricer.DiscountCurve.Spread;
        actions.Add(() =>
        {
          pricer.Product.Description = desc;
          pricer.DiscountCurve.Spread = origSpread;
        });
        pricer.DiscountCurve.Spread = origSpread + pricer.CalcRSpread();
        pricer.Product.Description = name;
      }
      actions.Add(() => { pricer.Product.Description = desc; });
    }

    private static void AddSwapLeg(
      this ICollection<Action> actions,
      SwapLegPricer pricer, string name)
    {
      var swapLeg = pricer.SwapLeg;
      var desc = swapLeg.Description;
      var initialExchange = swapLeg.InitialExchange;
      var intermediateExchange = swapLeg.IntermediateExchange;
      var finalExchange = swapLeg.FinalExchange;
      actions.Add(() =>
      {
        swapLeg.Description = desc;
        swapLeg.InitialExchange = initialExchange;
        swapLeg.IntermediateExchange = intermediateExchange;
        swapLeg.FinalExchange = finalExchange;
      });
      swapLeg.InitialExchange = true;
      swapLeg.IntermediateExchange = true;
      swapLeg.FinalExchange = true;
      swapLeg.Description = name;
    }

    private static void AddProduct(
      this ICollection<Action> actions,
      IProduct product, string name)
    {
      var desc = product.Description;
      actions.Add(() => { product.Description = desc; });
      product.Description = name;
    }

    internal static void AddResults(
      this double[] results, DataTable table)
    {
      int count = table.Rows.Count;
      for (int i = 0; i < count; ++i)
      {
        var row = table.Rows[i];
        var pos = Int32.Parse((string)row["Pricer"]);
        results[pos] += (double)row["Delta"];
      }
    }
  }

  #endregion Rate01 Utilities

  #region ZeroRateSensitivity reporting

  /// <summary>
  /// Light weight object to create zero rate reporting
  /// </summary>
  public class ZeroRateReporting
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="tenors"></param>
    /// <param name="cal"></param>
    /// <param name="roll"></param>
    /// <param name="bumpTenors"></param>
    public ZeroRateReporting(Dt asOf, string[] tenors, Calendar cal, BDConvention roll, string[] bumpTenors)
    {
      _asOf = asOf;
      _tenors = tenors;
      _calendar = cal;
      _roll = roll;
      _bumpTenors = bumpTenors;
    }

    /// <summary>
    ///  Function to reaggregate sensitivities linearly with respect to original tenors.
    /// </summary>
    /// <param name="table">Input data table</param>
    /// <returns>Updated data table</returns>
    public DataTable ReportKeyTenors(DataTable table)
    {
      // Create new datatable
      var calcGamma = table.Columns.Contains("Gamma");
      var dataTable = ResultTable.CreateResultTable(calcGamma, false);

      // Get valuation date
      var bucketMaturities = CollectionUtil.ConvertAll(_tenors, t => Dt.Roll(Dt.Add(_asOf, t), _roll, _calendar)).ToArray();
      var bucketT = new double[bucketMaturities.Count()];
      for (int i = 0; i < bucketMaturities.Count(); i++)
      {
        bucketT[i] = (bucketMaturities[i] - _asOf) / 365.0;
      }

      // Bucket tenor dictionary for list of original tenors and associated weights to apply to delta values
      double[] fullT;
      var bucketMaturityIndices = new Dictionary<string, List<TenorWeightData>>();
      var unifiedBumpTenors = false;
      if (_bumpTenors != null && _bumpTenors.Length > 0 && _bumpTenors[0] != "all")
      {
        unifiedBumpTenors = true;

        // Calculate weights once if possible
        var fullMaturities = CollectionUtil.ConvertAll(_bumpTenors, t => Dt.Roll(Dt.Add(_asOf, t), _roll, _calendar)).ToArray();
        fullT = new double[fullMaturities.Count()];
        for (int i = 0; i < fullMaturities.Count(); i++)
          fullT[i] = (fullMaturities[i] - _asOf) / 365.0;

        bucketMaturityIndices = CreateBucketMaturityIndices(bucketT, fullT, _bumpTenors, _tenors);
      }

      var distinctElements = table.DefaultView.ToTable(true, "Category", "Element", "Pricer");

      for (int e = 0; e < distinctElements.Rows.Count; e++)
      {
        var categoryVal = (string)distinctElements.Rows[e]["Category"];
        var elementVal = (string)distinctElements.Rows[e]["Element"];
        var pricerVal = (string)distinctElements.Rows[e]["Pricer"];

        // Get Curve Tenor rows for a category, element, pricer combination
        var selectStatement = "";

        if (categoryVal != "")
          selectStatement += "Category = '" + categoryVal + "' AND ";

        selectStatement += "Element = '" + elementVal + "' AND Pricer = '" + pricerVal + "'";

        var rowTable = table.Select(selectStatement).CopyToDataTable<DataRow>();
        
        if (!unifiedBumpTenors)
        {
          var dateRows = table.Select(selectStatement);

          var resultTenors = dateRows.Select(r => r["Curve Tenor"].ToString()).ToList();
          var fullMaturities = CollectionUtil.ConvertAll(resultTenors, t => Dt.Roll(Dt.Add(_asOf, t), _roll, _calendar)).ToArray();
          fullT = new double[fullMaturities.Count()];
          for (int i = 0; i < fullMaturities.Count(); i++)
            fullT[i] = (fullMaturities[i] - _asOf) / 365.0;

          bucketMaturityIndices = CreateBucketMaturityIndices(bucketT, fullT, resultTenors.ToArray(), _tenors);
        }

        // Loop over new buckets and add new rows to new table
        for (int j = 0; j < bucketT.Count(); j++)
        {
          // Initialise row and known elements
          var row = dataTable.NewRow();
          row["Category"] = categoryVal;
          row["Element"] = elementVal;
          row["Pricer"] = pricerVal;
          row["Curve Tenor"] = _tenors[j];

          // Calculate delta (and possibly gamma too)
          var deltaWeightedSum = 0.0;
          var gammaWeightedSum = 0.0;

          var dataSelect = bucketMaturityIndices[_tenors[j]];
          foreach (var s in dataSelect)
          {
            var rows0 = rowTable.Select("[Curve Tenor] = '" + s.Tenor + "'");

            foreach (var r in rows0)
            {
              deltaWeightedSum += s.Weight * (double)r["Delta"];

              if (calcGamma)
                gammaWeightedSum += s.Weight * (double)r["Gamma"];
            }
          }

          // Store delta and gamma
          row["Delta"] = deltaWeightedSum;
          if (calcGamma)
            row["Gamma"] = gammaWeightedSum;

          dataTable.Rows.Add(row);
        }
      }
      
      return dataTable;
    }

    /// <summary>
    /// Select data curve tenor element and associated weight
    /// </summary>
    private class TenorWeightData
    {
      public TenorWeightData(string tenor, double wi)
      {
        Tenor = tenor;
        Weight = wi;
      }

      public string Tenor { get; private set; }
      public double Weight { get; private set; }
  }


    /// <summary>
    /// Bucket tenor dictionary for list of original tenors and associated weights to apply to delta values
    /// </summary>
    /// <param name="bucketT"></param>
    /// <param name="fullT"></param>
    /// <param name="bumpTenors"></param>
    /// <param name="bucketTenors"></param>
    /// <returns></returns>
    private static Dictionary<string, List<TenorWeightData>> CreateBucketMaturityIndices(double[] bucketT, double[] fullT, string[] bumpTenors, string[] bucketTenors)
    {
      var bucketMaturityIndices = new Dictionary<string, List<TenorWeightData>>();

      for (int j = 0; j < bucketT.Count(); j++)
      {
        var tj = bucketT[j];
        var tjminus1 = bucketT[j - 1 >= 0 ? j - 1 : 0];
        var tjplus1 = bucketT[j + 1 < bucketT.Count() - 1 ? j + 1 : bucketT.Count() - 1];

        List<TenorWeightData> data = new List<TenorWeightData>();
        for (int i = 0; i < fullT.Count(); i++)
        {
          var wi = 0.0;
          var ti = fullT[i];

          if (ti >= tjminus1 && ti <= tj)
          {
            wi = (tjminus1 != tj) ? (ti - tjminus1) / (tj - tjminus1) : 1.0;
          }
          else if (ti > tj && ti <= tjplus1)
          {
            wi = (tjplus1 != tj) ? (tjplus1 - ti) / (tjplus1 - tj) : 1.0;
          }
          else if ((j == 0 && ti <= tjminus1) || (j == (bucketT.Count() - 1) && ti >= tjplus1))
          {
            wi = 1.0;
          }

          if (wi != 0.0)
          {
            data.Add(new TenorWeightData(bumpTenors[i], wi));
          }
        }

        bucketMaturityIndices.Add(bucketTenors[j], data);
      }

      return bucketMaturityIndices;
    }

    private readonly Dt _asOf;
    private readonly string[] _tenors;
    private readonly string[] _bumpTenors;
    private readonly Calendar _calendar;
    private readonly BDConvention _roll;
  }

  #endregion

  /// <summary>
  ///  Flags for sensitivity options
  /// </summary>
  [Flags]
  public enum SensitivityFlag
  {
    /// <summary>
    /// No special operation.
    /// </summary>
    None = 0,
    /// <summary>
    /// Recalibrate after bumping
    /// </summary>
    Recalibrate = 0x0001,
    /// <summary>
    /// Force notioal exchange of swaps.
    /// </summary>
    ForcePrincipalExcahnge = 0x0002
  }
}
