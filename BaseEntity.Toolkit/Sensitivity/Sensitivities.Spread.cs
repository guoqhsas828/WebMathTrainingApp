/*
 * Sensitivities.Spread.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 * Partial implementation of the spread sensitivity functions
 * 
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util.Collections;
using Timer = BaseEntity.Toolkit.Util.Timer;
using BaseEntity.Toolkit.Util;

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
    ///   Calculate the Credit Spread 01
    /// </summary>
    /// <remarks>
    ///   <para>The Spread 01 is the change in PV (MTM) if the underlying credit curve is shifted in parallel up by
    ///   one basis point.</para>
    ///   <para>Generally, sensitivities are derivatives of the pricing function for a particular product with respect to some market data.
    ///   The first order sensitivity, or delta, is the partial first derivative of the pricing function and in the 
    ///   context of parameterized models it is often possible to explicitly calculate this derivative in closed form.  More 
    ///   commonly, especially for products with complex dependencies, this derivative must be approximated by finite difference methods.</para>
    ///   <para>Let <m>P(s,r,c)</m> designate the price of a 
    ///   particular product as a function of its underlying spreads <m>(s)</m>, recovery rates 
    ///   <m>(r)</m>, and correlations <formula inline="true"> (c).</formula>  A finite difference approximation
    ///   is calculated as either a forward, backward, or central approximation depending on the "up" and "down" bump values.  
    ///   The function qSpread01() computes the forward difference approximation for a unit "up" bump with respect to the value of the pricing function.
    ///   Delta, or the partial derivative of the pricing function with respect to spreads, is calculated as the difference between the re-priced product and the original product. More formally:
    ///   <math>
    ///   \frac{\partial P}{\partial s} \approx \frac{P(s+u,r,c)-P(s,r,c)}{u}
    ///   </math>
    ///   </para>
    /// </remarks>
    /// <param name="pricer">The pricer.</param>
    /// <param name="measure">The measure.</param>
    /// <param name="upBump">Up bump size.</param>
    /// <param name="downBump">Down bump size.</param>
    /// <param name="rescaleStrikes">The rescale strikes.</param>
    /// <returns>Spread 01</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the spread sensitivity for a <see cref="BaseEntity.Toolkit.Products.Bond">Corporate Bond</see>.</para>
    /// <code language="C#">
    ///   BondPricer pricer;
    ///
    ///   // Initialise bond, pricer and curves
    ///   // ...
    ///
    ///   // Calculate the spread sensitivity of the risky duration.
    ///   double spread01 = Sensitivities.Spread01(
    ///     pricer,          // Pricer for bond
    ///     "Pv",            // Calculate change in Pv
    ///     4.0,             // Based on 4bp up shift
    ///     0.0,             // No down shift
    ///     );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Credit Spread 01 = {0}", spread01 );
    /// </code>
    /// </example>
    public static double Spread01(IPricer pricer, string measure, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        0.0, upBump, downBump, false, true,
        BumpType.Uniform, null, false, false, null, null, rescaleStrikes);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <inheritdoc cref="Sensitivities.Spread01(IPricer, string, double, double, bool[])" />
    /// <param name="pricer">IPricer</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    public static double Spread01(IPricer pricer, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer) },
        0.0, upBump, downBump, false, true,
        BumpType.Uniform, null, false, false, null, null, rescaleStrikes);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <inheritdoc cref="Sensitivities.Spread01(IPricer, string, double, double, bool[])" />
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="rescaleStrikes">Boolean array indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Calculated spread 01</returns>
    public static double Spread01(IPricer pricer, string measure,
      QuotingConvention targetQuoteType, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        targetQuoteType, 0.0, upBump, downBump, false, true,
        BumpType.Uniform, null, false, false, null, null, rescaleStrikes);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    ///   Calculate the Spread Gamma
    /// </summary>
    /// <remarks>
    ///   <para>The Spread Gamma is the change in Spread 01
    ///   if the underlying credit curve is shifted in parallel up by
    ///   one basis point.</para>
    ///   <para>The Spread Gamma is calculated by computing a 2nd order finite difference. 
    ///   First, calculate the measure (such as Pv); Second, bump up the underlying 
    ///   credit curve and re-calculate the measure; third, bump down the underlying 
    ///   credit curve and re-calculate the measure; Finally, return finite difference.</para>
    /// </remarks>
    /// <param name="pricer">The pricer.</param>
    /// <param name="measure">The measure.</param>
    /// <param name="upBump">Up bump size.</param>
    /// <param name="downBump">Down bump size.</param>
    /// <param name="rescaleStrikes">The rescale strikes.</param>
    /// <returns>Spread gamma</returns>
    /// <returns>Calculated spread gamma</returns>
    /// <example>
    /// <para>The following sample demonstrates calculating the spread gamma for a <see cref="BaseEntity.Toolkit.Products.Bond">Corporate Bond</see>.</para>
    /// <code language="C#">
    ///   Bond pricer;
    ///
    ///   // Initialise bond, pricer and curves
    ///   // ...
    ///
    ///   // Calculate the spread sensitivity of the risky duration.
    ///   double spreadGamma = Sensitivities.SpreadGamma(
    ///     pricer,          // Pricer for bond
    ///     "RiskyDuration", // Target measure
    ///     2.0,             // Based on 2bp up shift
    ///     2.0,             // and a 2bp down shift
    ///     );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Credit Spread gamma = {0}", spreadGamma );
    /// </code>
    /// </example>
    public static double SpreadGamma(IPricer pricer, string measure, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, true, false, null, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Gamma"];
    }

    /// <inheritdoc cref="Sensitivities.SpreadGamma(IPricer, string, double, double, bool[])" />
    /// <param name="pricer">IPricer</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    public static double SpreadGamma(IPricer pricer, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer) },
        0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, true, false, null, null, rescaleStrikes);
      return (double)(dataTable.Rows[0])["Gamma"];
    }

    /// <inheritdoc cref="Sensitivities.SpreadGamma(IPricer, string, double, double, bool[])" />
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    public static double SpreadGamma(IPricer pricer, string measure,
      QuotingConvention targetQuoteType, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        targetQuoteType, 0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, true, false, null, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Gamma"];
    }



    /// <summary>
    ///   Calculate the Notional of the sum of the CDS for each underlying credit that
    ///   would hedge the spread delta.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes hedge notional.</para>
    ///
    ///   <para>Equivalent to <see cref="SpreadHedge(IPricer,string,string,double,double, bool[])">
    ///   Spread01(pricer, null, hedgeTenor, upBump, downBump, rescaleStrikes)</see></para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="pricer">IPricer</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Calculated spread hedge notional</returns>
    ///
    public static double SpreadHedge( IPricer pricer, string hedgeTenor, double upBump, double downBump, params bool[] rescaleStrikes)
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer) },
        0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, false, true, hedgeTenor, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Hedge Notional"];
    }

    /// <summary>
    ///   Calculate the Notional of the sum of the CDS for each underlying credit that
    ///   would hedge the spread delta.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes hedge notional.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
    /// <param name="hedgeTenor">Name of curve tenor instrument used for hedge calculations</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <returns>Calculated spread hedge notional</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the spread gamma for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDOPricer pricer;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate the spread sensitivity of fee pv.
    ///   double hedgeNotional = Sensitivities.SpreadHedge(
    ///     pricer,          // Pricer for CDO tranche
    ///     "RiskyDuration", // Target measure
    ///     "5 Y",           // Hedge for 5Yr CDS
    ///     2.0,             // Based on 2bp up shift
    ///     2.0,             // and a 2bp down shift
    ///    );
    ///
    ///   // Print out results
    ///   Console.WriteLine( " Credit Hedge notional = {0}", hedgeNotional );
    /// </code>
    /// </example>
    ///
    public static double SpreadHedge(IPricer pricer, string measure, string hedgeTenor,
      QuotingConvention targetQuoteType, double upBump, double downBump, params bool[] rescaleStrikes
      )
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        targetQuoteType, 0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, false, true, hedgeTenor, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Hedge Notional"];
    }

    /// <summary>
    ///   Calculate the Notional of the sum of the CDS for each underlying credit 
    ///   that would hedge the spread delta.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="measure">The measure.</param>
    /// <param name="hedgeTenor">The hedge tenor.</param>
    /// <param name="upBump">Up bump size.</param>
    /// <param name="downBump">Down bump size.</param>
    /// <param name="rescaleStrikes">The rescale strikes.</param>
    /// <returns>Spread hedge.</returns>
    public static double SpreadHedge(
      IPricer pricer, string measure, string hedgeTenor, double upBump, double downBump, params bool[] rescaleStrikes
      )
    {
      DataTable dataTable = Spread(new PricerEvaluator[] { new PricerEvaluator(pricer, measure) },
        0.0, upBump, downBump,
        false, true, BumpType.Uniform, null, false, true, hedgeTenor, null, rescaleStrikes);

      return (double)(dataTable.Rows[0])["Hedge Notional"];
    }

    /// <summary>
    ///   Calculate the deviation of provided measure by bumping the dispersion of CDS portfolio
    /// </summary>
    ///
    /// <remarks>
    /// <para>Calculates the change from a measure of the dispersion of a portfolio of CDS.</para>
    ///
    /// <formula>s_i^* = s_i + (\alpha - 1) (s_i - \bar{s})</formula>
    ///
    /// <para>where <formula inline="true">\bar{s}</formula> is the intrinsic breakeven premium - namely the
    /// duration-weighted mean spread of entire portfolio for the maturity specified.</para>
    ///
    /// <para>The properties of such kind bumping are two: first, the duration-weighted mean spread
    /// after bump is roughly the same as that of before bump, assuming the duration does not change
    /// dramatically; second, the dispersion of bumped portfolio spreads is altered proportionally by:
    /// <formula inline="true">\mathrm{std}(s^*)=\alpha \times \mathrm{std}(s)</formula>.</para>
    ///
    /// <para>This bump technique can change the spread dispersion of the whole portfolio systematically. 
    /// The proportionality <formula inline="true">\alpha</formula> falls between 0 and 1.</para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">The measure such as Pv, FeePv, etc</param>
    /// <param name="alpha">The portfolio relative dispersion</param>
    ///
    /// <returns>Deviation of measure for each pricer</returns>
    ///
    public static double SpreadDispersion(IPricer pricer, string measure, double alpha)
    {
      DataTable dataTable = SpreadDispersion(
        new PricerEvaluator[] {new PricerEvaluator(pricer, measure)}, alpha, null);
      return (double)(dataTable.Rows[0])["Delta"];
    }

    /// <summary>
    ///   Calculate the deviation of provided measure by bumping the std of CDS portfolio
    /// </summary>
    ///
    /// <remarks>
    /// <para>Calculates the change from a measure of the dispersion of a portfolio of CDS.</para>
    ///
    /// <formula>s_i^* = s_i + (\alpha - 1) (s_i - \bar{s})</formula>
    ///
    /// <para>where <formula inline="true">\bar{s}</formula> is the intrinsic breakeven premium - namely the
    /// duration-weighted mean spread of entire portfolio for the maturity specified.</para>
    ///
    /// <para>The properties of such kind bumping are two: first, the duration-weighted mean spread
    /// after bump is roughly the same as that of before bump, assuming the duration does not change
    /// dramatically; second, the dispersion of bumped portfolio spreads is altered proportionally by:
    /// <formula inline="true">\mathrm{std}(s^*)=\alpha \times \mathrm{std}(s)</formula>.</para>
    ///
    /// <para>This bump technique can change the spread dispersion of the whole portfolio systematically. 
    /// The proportionality <formula inline="true">\alpha</formula> falls between 0 and 1.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">The measure such as Pv, FeePv, etc</param>
    /// <param name="alpha">The portfolio relative dispersion</param>
    ///
    /// <returns>Array of deviation of measures for each pricer</returns>
    ///
    public static double[] SpreadDispersion(
      IPricer[] pricers, string measure, double alpha
      )
    {
      // Validation
      if (pricers.Length < 1 || pricers == null)
        throw new ArgumentException("Must specify pricers");

      DataTable dataTable = SpreadDispersion(CreateAdapters(pricers, measure), alpha, null);
      double[] results = new double[pricers.Length];
      int cnt = 0;
      for (int i = 0; i < pricers.Length; i++)
        results[i] = (pricers[i] != null) ? (double)(dataTable.Rows[cnt++]["Delta"]) : 0.0;
      return results;
    }

    #endregion SummaryRiskMethods

    #region Spread Sensitivity

    /// <summary>
    ///   Calculate the spread sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>Equivalent to <see cref="Spread(IPricer[],string,double,double,double,bool,bool,BumpType,string[],bool,bool,string,DataTable, bool[])">
    ///   Spread(new IPricer[] {pricer}, measure, initialBump, upBump, downBump,
    ///   bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes)</see></para>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable Spread(
      IPricer pricer, string measure, double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor, DataTable dataTable, params bool[] rescaleStrikes
      )
    {
      return Spread(CreateAdapters(pricer, measure), initialBump, upBump, downBump,
      bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes);
    }

    
    /// <summary>
    ///   Calculate the spread sensitivity for a pricer.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
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
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the survival curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes </param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the spread sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate CDO spread sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr CDS hedge.
    ///   //
    ///   DataTable dataTable1 = Sensitivities.Spread(
    ///     new IPricer[] { pricer },   // Pricer for CDO tranche
    ///     "Pv",                       // Calculate change in Pv
    ///     4.0,                        // Based on 4bp up shift
    ///     0.0,                        // No down shift
    ///     false,                      // Bumps are absolute bp
    ///     true,                       // Scale delta and gamma
    ///     BumpType.Parallel,          // Bumps are parallel
    ///     null,                       // All tenors are bumped
    ///     false,                      // Dont bother with Gammas
    ///     true,                       // Do hedge calculation
    ///     "5 Year",                   // Hedge to 5yr tenor
    ///     null                        // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Credit Spread Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable Spread(IPricer[] pricers, string measure,
      QuotingConvention targetQuoteType, double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor, DataTable dataTable, bool[] rescaleStrikes
      )
    {
      return Spread(CreateAdapters(pricers, measure),targetQuoteType,
        initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType,
        bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the spread sensitivity for a pricer.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
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
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the survival curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
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
    ///<param name="sensitivityMethod">Methodology used to compute sensitivities. If set to SemiAnalytic the bump size is taken to be the upBump</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes </param>
    /// 
    /// <returns>Datatable of results</returns>
  
    public static DataTable Spread(IPricer[] pricers, string measure,
      QuotingConvention targetQuoteType, double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor, SensitivityMethod sensitivityMethod, DataTable dataTable, bool[] rescaleStrikes
      )
    {
        bool semiAnalytic = (sensitivityMethod == SensitivityMethod.SemiAnalytic);
        if(semiAnalytic && (String.Compare(measure,"")!=0) && (String.Compare(measure.Trim().ToLower(), "pv")!=0))
            throw new ToolkitException("For the time being only semi-analytic sensitivities for the Pv are exposed");
        if (semiAnalytic)
        {
          if (scaledDelta)
            upBump = 1;
          return CreditAnalyticSensitivities.SemiAnalyticSpreadSensitivities(pricers, upBump, bumpType, bumpRelative, calcHedge, hedgeTenor, rescaleStrikes);
        }
        return Spread(CreateAdapters(pricers, measure), targetQuoteType,
          initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType,
          bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes);
    }


    /// <summary>
    ///   Calculate the spread sensitivity for a pricer.
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes </param>
    /// <returns>Datatable of results</returns>
    public static DataTable Spread(
      IPricer[] pricers, string measure, double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor, DataTable dataTable, bool[] rescaleStrikes
      )
    {
      return Spread(CreateAdapters(pricers, measure), initialBump, upBump, downBump,
                bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the spread sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
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
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the survival curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the spread sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate CDO spread sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr CDS hedge.
    ///   //
    ///   PricerEvaluator pricerEval = new PricerEvaluator(pricer, "ExpectedLoss");
    ///   DataTable dataTable1 = Sensitivities.Spread(
    ///     new PricerEEvaluator[] { pricerEval },   // Pricer evaluator CDO tranche
    ///     4.0,                        // Based on 4bp up shift
    ///     0.0,                        // No down shift
    ///     false,                      // Bumps are absolute bp
    ///     true,                       // Scale delta and gamma
    ///     BumpType.Parallel,          // Bumps are parallel
    ///     null,                       // All tenors are bumped
    ///     false,                      // Dont bother with Gammas
    ///     true,                       // Do hedge calculation
    ///     "5 Year",                   // Hedge to 5yr tenor
    ///     null                        // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Credit Spread Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    private static DataTable Spread(PricerEvaluator[] evaluators,
      QuotingConvention targetQuoteType, double initialBump, double upBump, double downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor, DataTable dataTable
    )
    {
      return Spread(evaluators, targetQuoteType, 
        new[] { initialBump }, new[] { upBump }, new[] { downBump },
        bumpRelative, scaledDelta, bumpType, bumpTenors, 
        calcGamma, calcHedge, hedgeTenor, dataTable);
    }

    /// <summary>
    ///   Calculate the spread sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
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
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the survival curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the spread sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate CDO spread sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr CDS hedge.
    ///   //
    ///   PricerEvaluator pricerEval = new PricerEvaluator(pricer, "ExpectedLoss");
    ///   DataTable dataTable1 = Sensitivities.Spread(
    ///     new PricerEEvaluator[] { pricerEval },   // Pricer evaluator CDO tranche
    ///     4.0,                        // Based on 4bp up shift
    ///     0.0,                        // No down shift
    ///     false,                      // Bumps are absolute bp
    ///     true,                       // Scale delta and gamma
    ///     BumpType.Parallel,          // Bumps are parallel
    ///     null,                       // All tenors are bumped
    ///     false,                      // Dont bother with Gammas
    ///     true,                       // Do hedge calculation
    ///     "5 Year",                   // Hedge to 5yr tenor
    ///     null                        // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Credit Spread Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    private static DataTable Spread(PricerEvaluator[] evaluators,
      QuotingConvention targetQuoteType, double[] initialBump, double[] upBump, double[] downBump,
      bool bumpRelative, bool scaledDelta, BumpType bumpType, string[] bumpTenors,
      bool calcGamma, bool calcHedge, string hedgeTenor, DataTable dataTable
      )
    {
      Timer timer = new Timer();
      timer.start();
      logger.DebugFormat("Calculating spread sensitivities type={0}, up={1}, down={2}", bumpType, upBump, downBump);
      // Validation
      if (upBump.Sum() == -downBump.Sum())
        throw new ArgumentOutOfRangeException("downBump", downBump, "Up-bump size and down-bump size can not be equal.");

      // Create DataTable if we need to
      if (dataTable == null)
      {
        dataTable = ResultTable.CreateResultTable(calcGamma, calcHedge);
      }
      if (evaluators == null || evaluators.Length == 0)
      {
        timer.stop();
        logger.InfoFormat("Completed spread sensitivity in {0}s", timer.getElapsed());
        return dataTable;
      }

      var evaluatorsValidList = new List<PricerEvaluator>();
      var evaluatorsInvalidList = new List<PricerEvaluator>();
      foreach (PricerEvaluator peval in evaluators)
      {
        if (IsSpreadSensitivityApplicable(peval))
          evaluatorsValidList.Add(peval);
        else
          evaluatorsInvalidList.Add(peval);
      }

      if (evaluatorsValidList.Count > 0)
      {
        PricerEvaluator[] evaluatorsValid = evaluatorsValidList.ToArray();
        var curves = evaluatorsValid.GetSpreadSensitivityCurves(true);

        // Group by curve
        int numTasks = 1; //until we reintroduce grid (bumpType == BumpType.Uniform) ? 1 : curves.Count;
        int groupSize = (int)Math.Ceiling((double)curves.Count / (double)numTasks);

        int currentGroup = 0;
        CalibratedCurve[][] curveGroups = new CalibratedCurve[numTasks][];
        for (int i = 0; i < curves.Count; i += groupSize)
        {
          int startIdx = i;
          int endIdx = Math.Min(curves.Count, startIdx + groupSize);
          CalibratedCurve[] curveGroup = new CalibratedCurve[endIdx - startIdx];
          for (int j = startIdx; j < endIdx; j++)
          {
            curveGroup[j - startIdx] = curves[j];
          }
          curveGroups[currentGroup] = curveGroup;
          currentGroup++;
        }

        ArrayList tasks = new ArrayList();
        for (int i = 0; i < curveGroups.GetLength(0); i++)
        {
          string taskName = i.ToString();

          CurveTaskState taskState = new CurveTaskState(evaluatorsValid, curveGroups[i], null,
            initialBump, upBump, downBump, bumpRelative,
            scaledDelta, bumpType, bumpTenors, calcGamma,
            calcHedge, hedgeTenor, targetQuoteType);

          tasks.Add(taskState);
        }

        ArrayList results = new ArrayList();

        // Add results to our DataTable (order should be same as if no grid)
        for (int i = 0; i < numTasks; i++)
        {

          object[][] result = (object[][])CurveTask(tasks[i]);
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

      if (evaluatorsInvalidList.Count > 0 && (bumpType == BumpType.Uniform || bumpType == BumpType.Parallel))
      {
        // Add dummy 0 entries in this case - otherwise the XL functions will be confused:
        foreach (PricerEvaluator peval in evaluatorsInvalidList)
        {
          if (bumpType == BumpType.Uniform || (bumpType == BumpType.Parallel && peval.SurvivalCurves != null && peval.SurvivalCurves.Length == 1))
          {
            DataRow dataRow = dataTable.NewRow();
            if (bumpType == BumpType.Uniform)
            {
              dataRow["Category"] = "all";
              dataRow["Element"] = "all";
              dataRow["Curve Tenor"] = "all";
            }
            else
            {
              // Parralel
              dataRow["Element"] = peval.SurvivalCurves[0].Name;
              dataRow["Curve Tenor"] = "all";
            }
            dataRow["Pricer"] = peval.Product.Description;
            dataRow["Delta"] = 0;
            if (calcGamma)
              dataRow["Gamma"] = 0;
            if (calcHedge)
            {
              dataRow["Hedge Tenor"] = 0;
              dataRow["Hedge Delta"] = 0;
              dataRow["Hedge Notional"] = 0;
            }
            dataTable.Rows.Add(dataRow);
          }
        }
      }

      timer.stop();
      logger.InfoFormat("Completed spread sensitivity in {0}s", timer.getElapsed());

      return dataTable;
    }

    private static bool IsSpreadSensitivityApplicable(PricerEvaluator pEval)
    {
      /* Disable this part for now - M.G., 3/3/2014:
      if (pEval.Pricer is BondPricer)
      {
        var bp = pEval.Pricer as BondPricer;
        if (bp.IsDefaulted(pEval.Settle))
        {
          return false;
        }
        return pEval.Settle < pEval.Product.Maturity;
      }
      */
      if (pEval.Pricer is DefaultedAssetPricer)
        return false;
      return true;
    }

    /// <summary>
    ///   Calculate the deviation of provided measure by bumping the std of CDS portfolio
    /// </summary>
    ///
    /// <remarks>
    /// <para>Calculates the change from a measure of the dispersion of a portfolio of CDS.</para>
    ///
    /// <formula>s_i^* = s_i + (\alpha - 1) (s_i - \bar{s})</formula>
    ///
    /// <para>where <formula inline="true">\bar{s}</formula> is the intrinsic breakeven premium - namely the
    /// duration-weighted mean spread of entire portfolio for the maturity specified.</para>
    ///
    /// <para>The properties of such kind bumping are two: first, the duration-weighted mean spread
    /// after bump is roughly the same as that of before bump, assuming the duration does not change
    /// dramatically; second, the dispersion of bumped portfolio spreads is altered proportionally by:
    /// <formula inline="true">\mathrm{std}(s^*)=\alpha \times \mathrm{std}(s)</formula>.</para>
    ///
    /// <para>This bump technique can change the spread dispersion of the whole portfolio systematically. 
    /// The proportionality <formula inline="true">\alpha</formula> falls between 0 and 1.</para>
    /// </remarks>
    ///
    /// <param name="pricers">A set of CDO Pricers</param>
    /// <param name="measure">The measure such as Pv, FeePv, etc</param>
    /// <param name="alpha">The relative portfolio dispersion</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Deviation of measure for each pricer</returns>
    ///
    public static DataTable SpreadDispersion(
      IPricer[] pricers, string measure, double alpha,
      DataTable dataTable)
    {
      return SpreadDispersion(CreateAdapters(pricers, measure), alpha, dataTable);
    }

    /// <summary>
    ///   Calculate the deviation of provided measure by bumping the std of CDS portfolio
    /// </summary>
    ///
    /// <param name="evaluators">A set of Pricer evaluators</param>
    /// <param name="alpha">The relative portfolio dispersion</param>
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    ///
    /// <returns>Deviation of measure for each pricer</returns>
    ///
    private static DataTable SpreadDispersion(
      PricerEvaluator[] evaluators, double alpha, DataTable dataTable
      )
    {
      Timer timer = new Timer();
      timer.start();

      // Get survival curves for these pricers (force some to exist)
      IList<SurvivalCurve> sc = PricerEvaluatorUtil.GetSurvivalCurves(evaluators, true);
      SurvivalCurve[] curves = sc.ToArray();

      // Validate curves have uniform CDS tenors
      foreach (SurvivalCurve curve in sc)
      {
        SurvivalCalibrator calibrator = curve.SurvivalCalibrator;
        if (calibrator == null)
          throw new ArgumentException(String.Format("The curve '{0}' is not a calibrated curve", curve.Name));
        if (curve.Tenors.Count != curves[0].Tenors.Count)
          throw new ArgumentException(String.Format("The curve '{0}' does not have matching tenors", curve.Name));
        foreach( CurveTenor t in curve.Tenors )
        {
          if( !(t.Product is CDS) )
            throw new ArgumentException(String.Format("The curve '{0}' does not CDS tenors", curve.Name));
        }
      }

      // Create bumped curves
      //
      logger.Debug("Generating bumped curve set");
      CalibratedCurve[] bumpedCurves = CloneUtil.Clone(curves);

      // Calculate weighted average spread for each curve tenor if we need to
      double[] meanSpreads = new double[curves[0].Tenors.Count];
      DiscountCurve discountCurve = ((SurvivalCalibrator) curves[0].Calibrator).DiscountCurve;
      Dt asof = curves[0].Calibrator.AsOf;
      Dt settle = curves[0].Calibrator.Settle;
      for (int i = 0; i < meanSpreads.Length; i++)
      {
        CDS cds = (CDS) (curves[0].Tenors[i].Product);
        CDX cdx = new CDX(cds.Effective, cds.Maturity, cds.Ccy, 100.0/10000, cds.Calendar);
        CDXPricer cdxPricer = new CDXPricer(cdx, asof, settle, discountCurve, curves, 90.0/10000);
        meanSpreads[i] = cdxPricer.IntrinsicBreakEvenPremium();
      }

      // Bump the portfolio's std for provided tenors by provided alpha
      for (int j = 0; j < bumpedCurves.Length; j++ )
      {
        for (int i = 0; i < meanSpreads.Length; i++)
        {
          CDS cds = (CDS)(bumpedCurves[j].Tenors[i].Product);
          cds.Premium = Math.Max(0, alpha * cds.Premium + (1 - alpha) * meanSpreads[i]);
        }
        bumpedCurves[j].ReFit(0);
      }

      // Compute sensitivities
      return Curve(evaluators, curves, bumpedCurves, BumpType.Uniform, false, null, dataTable);
    }

    /// <summary>
    ///  This is wrapper method between versions of Spread(IPricers,...) and code Spread(PricerEvaluator,...)
    ///  Its function is to set and restore the rescale strikes flag for CDO pricer.
    ///  <preliminary>For internal use only.</preliminary>
    /// </summary>
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="targetQuoteType">The quoting convention in which the credit curves are bumped.</param>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// <returns>Result data table.</returns>
    /// <exclude/>
    public static DataTable Spread(
      PricerEvaluator[] evaluators, QuotingConvention targetQuoteType,
      double initialBump, double upBump, double downBump, bool bumpRelative,
      bool scaledDelta, BumpType bumpType, string[] bumpTenors, bool calcGamma,
      bool calcHedge, string hedgeTenor, DataTable dataTable, bool[] rescaleStrikes
      )
    {
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikes);
      DataTable dt = null;
      try
      {
        dt = Spread(evaluators, targetQuoteType, initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable);
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }
      return dt;
    }

    private static DataTable Spread(
      PricerEvaluator[] evaluators,
      double initialBump, double upBump, double downBump, bool bumpRelative,
      bool scaledDelta, BumpType bumpType, string[] bumpTenors, bool calcGamma,
      bool calcHedge, string hedgeTenor, DataTable dataTable, bool[] rescaleStrikes)
    {
      return Spread(evaluators, QuotingConvention.None, initialBump, upBump, downBump,
        bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor,
        dataTable, rescaleStrikes);
    }
    #endregion Spread Sensitivity

    #region Backward Compatible

    /// <summary>
    ///   Calculate the spread sensitivity for a series of pricers.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>Equivalent to <see cref="Spread(IPricer[],string,double,double,double,bool,bool,BumpType,string[],bool,bool,string,DataTable, bool[])">
    ///   Spread(new IPricer[] {pricer}, measure, initialBump, upBump, downBump,
    ///   bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes)</see></para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    public static DataTable
    Spread(
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
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return Spread(CreateAdapters(pricer, null), initialBump, upBump, downBump,
              bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes);
    }

    /// <summary>
    ///   Calculate the spread sensitivity for a pricer.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Computes numerical spread sensitivities and hedge equivalents with several options for
    ///   controlling the way the sensitivities are calculated.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on SurvivalCurves.</para>
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
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///
    ///   <para>This is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    ///
    ///   <para>If the survival curves are specified, an assumed dependence exists between the pricers
    ///   and the curves to be bumped that is maintained by the calculation.</para>
    ///
    ///   <para>Care is taken to ensure that the state of the curves are maintained
    ///   when the method is completed, even if an exception is thrown during the calculation.</para>
    ///   <para>
    ///      Note: the parameter hedgeTenor can take several forms, namely it could be [1] blank, [2] tenor name (5 Years etc), [3] tenor date, 
    ///      and string literal [4] "maturity". The blank hedge tenor means matching the bumped tenors, tenor name is the name of tenor such as
    ///      3 Year, tenor date is the date of to-be-bumped tenor, and "maturity" means to match the product's maturity
    ///   </para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
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
    /// <param name="dataTable">Datatable to add results to or null to return new DataTable</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not</param>
    /// 
    /// <returns>Datatable of results</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates calculating the spread sensitivity for a <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche</see>.</para>
    /// <code language="C#">
    ///   SyntheticCDO cdo;
    ///   SyntheticCDOPricer pricer;
    ///   DiscountCurve discountCurve;
    ///
    ///   // Initialise cdo, pricer and discountCurve
    ///   // ...
    ///
    ///   // Calculate CDO spread sensitivity.
    ///   // Here we are using parallel shifts along with 5 Yr CDS hedge.
    ///   //
    ///   DataTable dataTable1 = Sensitivities.Spread(
    ///     new IPricer[] { pricer },   // Pricer for CDO tranche
    ///     "Pv",                       // Calculate change in Pv
    ///     4.0,                        // Based on 4bp up shift
    ///     0.0,                        // No down shift
    ///     false,                      // Bumps are absolute bp
    ///     true,                       // Scale delta and gamma
    ///     BumpType.Parallel,          // Bumps are parallel
    ///     null,                       // All tenors are bumped
    ///     false,                      // Dont bother with Gammas
    ///     true,                       // Do hedge calculation
    ///     "5 Year",                   // Hedge to 5yr tenor
    ///     null                        // Create new table of results
    ///   );
    ///
    ///   // Print out results
    ///   for( int i = 0; i &lt; dataTable1.Rows.Count; i++ )
    ///   {
    ///     DataRow row = dataTable1.Rows[i];
    ///     Console.WriteLine( " Curve {0}, Credit Spread Delta {1}, 5Yr CDS Hedge {2}",
    ///                        (string)row["Element"], (double)row["Delta"], (double)row["Hedge Notional"] );
    ///   }
    /// </code>
    /// </example>
    ///
    public static DataTable
    Spread(
      IPricer[] pricers,
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
      DataTable dataTable,
      params bool[] rescaleStrikes
      )
    {
      return Spread(CreateAdapters(pricers, null), initialBump, upBump, downBump,
                bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge, hedgeTenor, dataTable, rescaleStrikes);
    }
    #endregion Backward Compatible

  } // class Sensitivities
}
