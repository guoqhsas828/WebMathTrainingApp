using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using log4net;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   Sensitivity functions version 2.
  /// </summary>
  /// <remarks></remarks>
  public static partial class Sensitivities2
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(Sensitivities2));

    /// <summary>
    /// Gets the curve separator char.
    /// </summary>
    /// <remarks></remarks>
    public static char CurveSeparatorChar { get { return '\n'; } }

    #region Interface usage example

    /// <summary>
    /// Calculate curve and surface sensitivities to market quotes for a set of pricers.
    /// </summary>
    /// <remarks>
    ///   <para>Computes sensitivities to market quotes and hedge equivalents with options for controlling the way the sensitivities are calculated.</para>
    ///   <para>Sensitivities to quotes used to calibrate the rate curve can be calculated for any result supported by the pricing models.</para>
    ///   <para>Various options exist for how the calculations are performed including the size of the bump as well as the bump method used.</para>
    ///   <para>The size of bumps is in bump units. The bump unit size depends on the type of products in the curves being bumped.
    ///   For CDS the bump units are basis points. For Bonds the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///   <para>Gamma and hedge results are calculated if <paramref name="calcGamma"/> and
    ///   <paramref name="calcHedge"/> are true respectively.</para>
    ///   <para><b>Calculation Options</b></para>
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Uniform</term><description>Select all the relevant tenors from all the curves and bump them simultaneously,
    ///       yielding 1 row of output for each pricer.</description></item>
    ///     <item><term>Parallel</term><description>Group the relevant tenors by the containing curves and bump each group separately,
    ///       yielding <n>n</n> rows of output for each pricer affected by <m>n</m> curves</description></item>
    ///     <item><term>By Tenor</term><description>Select all the relevant tenors from all curves and bump them individually,
    ///       yielding <m>k</m> rows of output for each pricer where <m>k</m> is the total number of tenors</description></item>
    ///   </list>
    ///   <para>In the multiple curves settings, a bump of an individual tenor may impact several curves.
    ///     This function re-calibrates all the curves affected after the bumping.</para>
    ///   <para><b>Hedge Options</b></para>
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///   <para>The parameter <paramref name="hedgeTenor"/> can take several forms:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>empty</term><description>Hedges are calculated for all tenors bumped</description></item>
    ///     <item><term>Tenor Name</term><description>Hedges are calculated for the specified tenor (eg 3Yr)</description></item>
    ///     <item><term>Date</term><description>Hedges are calculated for the specified tenor date</description></item>
    ///     <item><term>"maturity"</term><description>Hedges are calculated with the tenor matching the products maturity</description></item>
    ///     <item><term>"matching"</term><description>Hedges are calculated with the tenor being bumped</description></item>
    ///   </list>
    ///   <para>Filtering can specify a subset of curves to bump <paramref name="curveNames"/> as well
    ///   as a subset of tenors to bump (<paramref name="bumpTenors"/>).</para>
    ///   <para><b>Calculation Details</b></para>
    ///   <para>Let <m>P(i)</m> designate the price of a particular product as a function of the instruments quoted in the market (<m>i</m>).
    ///   A finite difference approximation is calculated as either a forward, backward, or central approximation depending on the "up" and "down" bump values. 
    ///   The function qRateDelta() computes the forward difference approximation for a unit "up" bump with respect to the value of the pricing function.
    ///   Delta, or the partial derivative of the pricing function with respect to interest rates, is calculated as the difference between the re-priced
    ///   product and the original product. More formally:</para>
    ///   <formula>
    ///   \frac{\partial P}{\partial i} \approx \frac{P(i+u)-P(i-d)}{u+d}
    ///   </formula>
    ///   <para>Gamma is the second derivative of the pricing function and is the finite difference approximation of the first derivative
    ///   of delta. Define:</para>
    ///   <para>Forward term:</para>
    ///   <formula>
    ///   \frac{\partial P}{\partial i}^u \approx \frac{P(i+u)-P(i)}{u}
    ///   </formula>
    ///   <para>Backward term:</para>
    ///   <formula>
    ///   \frac{\partial P}{\partial i}^d \approx \frac{P(i)-P(i-d)}{d}
    ///   </formula>
    ///   <para>Then gamma is:</para>
    ///   <formula>
    ///   \Gamma \approx \frac{\displaystyle\frac{\partial P}{\partial i}^u - \frac{\partial P}{\partial i}^d}{\displaystyle\frac{u+d}{2}}
    ///   </formula>
    ///   <para><b>Outputs</b></para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve or surface</description></item>
    ///     <item><term>Element</term><description>Name of curve or surface</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve or surface tenor (or all for parallel shift)</description></item>
    ///     <item><term>Pricer</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///   <para>This calculation is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    /// </remarks>
    /// <param name="pricers">The list of pricers</param>
    /// <param name="measure">The measure to calculate sensitivities to</param>
    /// <param name="curveNames">The names of the curves to calculate sensitivities for</param>
    /// <param name="quoteTarget">The type of quote to bump. This determines the type of sensitivities generated</param>
    /// <param name="upBump">Up bump size in bump units</param>
    /// <param name="downBump">Down bump size in bump units</param>
    /// <param name="bumpType">Type of the bump</param>
    /// <param name="bumpRelative">If set to <c>true</c>, bump sizes are specified relatively instead of absolutely</param>
    /// <param name="bumpTenors">The names of the tenors to calculate sensitivities for</param>
    /// <param name="scaleDelta">If set to <c>true</c>, scale deltas by the actual bump sizes</param>
    /// <param name="calcGamma">If set to <c>true</c>, calculate gammas</param>
    /// <param name="hedgeTenor">The name of the tenor to calculate hedges against</param>
    /// <param name="calcHedge">if set to <c>true</c>, calculate hedge ratios and notional</param>
    /// <param name="cache">If set to <c>true</c>, try to cache intermediate results as much as possible</param>
    /// <param name="dataTable">The data table to fill with results or null to create a new DataTable</param>
    /// <returns>A DataTable containing the sensitivity results</returns>
    public static DataTable Calculate(
      IPricer[] pricers, object measure, string[] curveNames, BumpTarget quoteTarget,
      double upBump, double downBump, BumpType bumpType, bool bumpRelative,
      string[] bumpTenors, bool scaleDelta, bool calcGamma,
      string hedgeTenor, bool calcHedge, bool cache, DataTable dataTable)
    {
      return Calculate(pricers, measure, curveNames, quoteTarget,
        upBump, downBump, bumpType, bumpRelative?BumpFlags.BumpRelative:BumpFlags.None,
        bumpTenors, scaleDelta, calcGamma, hedgeTenor, calcHedge, cache, dataTable);
    }

    /// <summary>
    /// Calculate curve and surface sensitivities to market quotes for a set of pricers.
    /// </summary>
    /// <remarks>
    ///   <para>Computes sensitivities to market quotes and hedge equivalents with options for controlling the way the sensitivities are calculated.</para>
    ///   <para>Sensitivities to quotes used to calibrate the rate curve can be calculated for any result supported by the pricing models.</para>
    ///   <para>Various options exist for how the calculations are performed including the size of the bump as well as the bump method used.</para>
    ///   <para>The size of bumps is in bump units. The bump unit size depends on the type of products in the curves being bumped.
    ///   For CDS the bump units are basis points. For Bonds the bump units are dollars. If absolute bumps are specified, the results are scaled
    ///   to be per bump unit.</para>
    ///   <para>Gamma and hedge results are calculated if <paramref name="calcGamma"/> and
    ///   <paramref name="calcHedge"/> are true respectively.</para>
    ///   <para><b>Calculation Options</b></para>
    ///   <para><paramref name="bumpType"/> determines how the bumping and recalculation is
    ///   performed. Options exist for tenor, parallel, category or uniform bumping.</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Uniform</term><description>Select all the relevant tenors from all the curves and bump them simultaneously,
    ///       yielding 1 row of output for each pricer.</description></item>
    ///     <item><term>Parallel</term><description>Group the relevant tenors by the containing curves and bump each group separately,
    ///       yielding <n>n</n> rows of output for each pricer affected by <m>n</m> curves</description></item>
    ///     <item><term>By Tenor</term><description>Select all the relevant tenors from all curves and bump them individually,
    ///       yielding <m>k</m> rows of output for each pricer where <m>k</m> is the total number of tenors</description></item>
    ///   </list>
    ///   <para>In the multiple curves settings, a bump of an individual tenor may impact several curves.
    ///     This function re-calibrates all the curves affected after the bumping.</para>
    ///   <para><b>Hedge Options</b></para>
    ///   <para>If <paramref name="calcHedge"/> is true, hedge equivalents are calculated based on
    ///   the specified hedge tenor <paramref name="hedgeTenor"/>.  A value of "matching" for
    ///   <paramref name="hedgeTenor"/> will calculate by-tenor hedges when <paramref name="bumpType"/>
    ///   is ByTenor.</para>
    ///   <para>The parameter <paramref name="hedgeTenor"/> can take several forms:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>empty</term><description>Hedges are calculated for all tenors bumped</description></item>
    ///     <item><term>Tenor Name</term><description>Hedges are calculated for the specified tenor (eg 3Yr)</description></item>
    ///     <item><term>Date</term><description>Hedges are calculated for the specified tenor date</description></item>
    ///     <item><term>"maturity"</term><description>Hedges are calculated with the tenor matching the products maturity</description></item>
    ///     <item><term>"matching"</term><description>Hedges are calculated with the tenor being bumped</description></item>
    ///   </list>
    ///   <para>Filtering can specify a subset of curves to bump <paramref name="curveNames"/> as well
    ///   as a subset of tenors to bump (<paramref name="bumpTenors"/>).</para>
    ///   <para><b>Calculation Details</b></para>
    ///   <para>Let <m>P(i)</m> designate the price of a particular product as a function of the instruments quoted in the market (<m>i</m>).
    ///   A finite difference approximation is calculated as either a forward, backward, or central approximation depending on the "up" and "down" bump values. 
    ///   The function qRateDelta() computes the forward difference approximation for a unit "up" bump with respect to the value of the pricing function.
    ///   Delta, or the partial derivative of the pricing function with respect to interest rates, is calculated as the difference between the re-priced
    ///   product and the original product. More formally:</para>
    ///   <formula>
    ///   \frac{\partial P}{\partial i} \approx \frac{P(i+u)-P(i-d)}{u+d}
    ///   </formula>
    ///   <para>Gamma is the second derivative of the pricing function and is the finite difference approximation of the first derivative
    ///   of delta. Define:</para>
    ///   <para>Forward term:</para>
    ///   <formula>
    ///   \frac{\partial P}{\partial i}^u \approx \frac{P(i+u)-P(i)}{u}
    ///   </formula>
    ///   <para>Backward term:</para>
    ///   <formula>
    ///   \frac{\partial P}{\partial i}^d \approx \frac{P(i)-P(i-d)}{d}
    ///   </formula>
    ///   <para>Then gamma is:</para>
    ///   <formula>
    ///   \Gamma \approx \frac{\displaystyle\frac{\partial P}{\partial i}^u - \frac{\partial P}{\partial i}^d}{\displaystyle\frac{u+d}{2}}
    ///   </formula>
    ///   <para><b>Outputs</b></para>
    ///   <para>The output table consists the following columns:</para>
    ///   <list type="table">
    ///     <listheader><term>Name</term><description>Description</description></listheader>
    ///     <item><term>Category</term><description>Category of curve or surface</description></item>
    ///     <item><term>Element</term><description>Name of curve or surface</description></item>
    ///     <item><term>Curve Tenor</term><description>Name of curve or surface tenor (or all for parallel shift)</description></item>
    ///     <item><term>Pricer</term><description>Name of instrument priced</description></item>
    ///     <item><term>Delta</term><description>Delta</description></item>
    ///     <item><term>Gamma</term><description>Gamma (if <paramref name="calcGamma"/> is true)</description></item>
    ///     <item><term>Hedge Tenor</term><description>Tenor of hedge instrument (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Delta</term><description>HedgeDelta (if <paramref name="calcHedge"/> is true)</description></item>
    ///     <item><term>Hedge Notional</term><description>HedgeNotional (if <paramref name="calcHedge"/> is true)</description></item>
    ///   </list>
    ///   <para>This calculation is optimized for some pricing models such as basket pricing models and
    ///   can result in significant speed improvement over separately bumping and recalculating.</para>
    /// </remarks>
    /// <param name="pricers">The list of pricers</param>
    /// <param name="measure">The measure to calculate sensitivities to</param>
    /// <param name="curveNames">The names of the curves to calculate sensitivities for</param>
    /// <param name="quoteTarget">The type of quote to bump. This determines the type of sensitivities generated</param>
    /// <param name="upBump">Up bump size in bump units</param>
    /// <param name="downBump">Down bump size in bump units</param>
    /// <param name="bumpType">Type of the bump</param>
    /// <param name="bumpFlags">The flags to specify how to perform the bumps</param>
    /// <param name="bumpTenors">The names of the tenors to calculate sensitivities for</param>
    /// <param name="scaleDelta">If set to <c>true</c>, scale deltas by the actual bump sizes</param>
    /// <param name="calcGamma">If set to <c>true</c>, calculate gammas</param>
    /// <param name="hedgeTenor">The name of the tenor to calculate hedges against</param>
    /// <param name="calcHedge">if set to <c>true</c>, calculate hedge ratios and notional</param>
    /// <param name="cache">If set to <c>true</c>, try to cache intermediate results as much as possible</param>
    /// <param name="dataTable">The data table to fill with results or null to create a new DataTable</param>
    /// <returns>A DataTable containing the sensitivity results</returns>
    public static DataTable Calculate(
      IPricer[] pricers, object measure, string[] curveNames, BumpTarget quoteTarget,
      double upBump, double downBump, BumpType bumpType, BumpFlags bumpFlags,
      string[] bumpTenors, bool scaleDelta, bool calcGamma,
      string hedgeTenor, bool calcHedge, bool cache, DataTable dataTable)
    {
      if (quoteTarget == BumpTarget.Volatilities)
      {
        if (calcHedge)
        {
          logger.Warn("Volatility sensitivity does not support hedging calculation");
        }
        return Calculate(pricers, measure, upBump, downBump,
          bumpType, bumpFlags, scaleDelta, calcGamma,
          CreateVolatilitySurfacesGetter(bumpFlags),
          CreateVolatilityTenorFilter(bumpTenors, curveNames),
          cache, dataTable);
      }
      var savedIncludeSpot = CurveTenorSelectors.IncludeSpotPrice;
      CurveTenorSelectors.IncludeSpotPrice = ((quoteTarget & BumpTarget.IncludeSpot) != 0);
      try
      {
        var getCurves = CreateCurvesGetter(quoteTarget, bumpFlags);
        var selector = CreateTenorFilter(bumpType, quoteTarget, bumpTenors, curveNames);
        var getHedgePricer = CreateHedgePricerGetter(quoteTarget, bumpType, bumpFlags, calcHedge, hedgeTenor);
        return Calculate(pricers, measure, upBump, downBump, bumpType,
          bumpFlags, scaleDelta, calcGamma, getCurves, selector,
          getHedgePricer, cache, dataTable);
      }
      finally
      {
        CurveTenorSelectors.IncludeSpotPrice = savedIncludeSpot;
      }
    }

    #endregion

    #region General interface

    /// <summary>
    /// Calculates the specified pricers.
    /// </summary>
    /// <remarks></remarks>
    /// <param name="pricers">The pricers.</param>
    /// <param name="measure">The measure.</param>
    /// <param name="upBump">Up bump.</param>
    /// <param name="downBump">Down bump.</param>
    /// <param name="bumpType">Type of the bump.</param>
    /// <param name="bumpFlags">Flags control how quotes are bumped and curves refited.</param>
    /// <param name="scaleDelta">If set to <c>true</c>, scale deltas by the actual bump sizes.</param>
    /// <param name="calculateGamma">If set to <c>true</c>, calculate gammas.</param>
    /// <param name="getCurves">The delegate to get curves.</param>
    /// <param name="tenorFilter">The delegate to filter tenors.</param>
    /// <param name="getHedgeEvaluator">The delegate to get hedge pricers based on tenor selections.</param>
    /// <param name="cache">If set to <c>true</c>, try to cache intermediate results as much as possible.</param>
    /// <param name="dataTable">The data table.</param>
    /// <returns></returns>
    public static DataTable Calculate(
      IPricer[] pricers, object measure, double upBump, double downBump,
      BumpType bumpType, BumpFlags bumpFlags, bool scaleDelta, bool calculateGamma,
      Func<PricerEvaluator[], IList<CalibratedCurve>> getCurves,
      Func<CalibratedCurve, CurveTenor, bool> tenorFilter,
      Func<ISensitivitySelection, IReEvaluator> getHedgeEvaluator,
      bool cache, DataTable dataTable)
    {
      var evaluators = pricers.CreateAdapters(measure);
      switch (bumpType)
      {
      case BumpType.Uniform:
        return Calculate(evaluators, upBump, downBump,
          bumpFlags, scaleDelta, calculateGamma, getCurves,
          curves => new[] {curves.SelectTenors(tenorFilter, "Uniform")},
          getHedgeEvaluator,
          cache, dataTable).Table;
      case BumpType.Parallel:
        return Calculate(evaluators, upBump, downBump,
          bumpFlags, scaleDelta, calculateGamma, getCurves,
          curves => curves.GetParallelSelections(tenorFilter),
          getHedgeEvaluator,
          cache, dataTable).Table;
      case BumpType.ByTenor:
        return Calculate(evaluators, upBump, downBump,
          bumpFlags, scaleDelta, calculateGamma, getCurves,
          curves => GetByTenorsSelections(curves, tenorFilter, getHedgeEvaluator),
          getHedgeEvaluator == null
            ? null as Func<ISensitivitySelection, IReEvaluator>
            : sel => ((ByTenorSelection)sel).HedgePricer,
          cache, dataTable).Table;
      default:
        throw new ToolkitException(String.Format("Bump type {0} not supported yet",
          bumpType));
      }
    }

    /// <summary>
    /// Creates the curves getter based on target tenor types.
    /// </summary>
    /// <remarks></remarks>
    /// <param name="bumpTenorTarget">The bump tenor target.</param>
    /// <param name="flags">BumpFlags</param>
    /// <returns></returns>
    public static Func<PricerEvaluator[], IList<CalibratedCurve>> 
      CreateCurvesGetter(BumpTarget bumpTenorTarget, BumpFlags flags)
    {
      //TODO: Shall we implement a generic function to get all curves
      //TODO: inside pricers, as a full general function should do?
      if ((bumpTenorTarget & AllCurveQuotes) == 0)
      {
        throw new ToolkitException(String.Format("Unable to handle target tenor types {0}", bumpTenorTarget));
      }
      var target = bumpTenorTarget;
      if ((flags & BumpFlags.RecalibrateSurvival) != 0)
        target |= BumpTarget.CreditQuotes;
      return p => GetCurves(p, target);
    }

    /// <summary>
    /// Creates the filter.
    /// </summary>
    /// <param name="bumpType">Type of the bump.</param>
    /// <param name="bumpTenorTarget">The bump tenor target.</param>
    /// <param name="bumpTenors">The bump tenors.</param>
    /// <param name="curveNames">The names of the curves to extract tenors.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static Func<CalibratedCurve, CurveTenor, bool> CreateTenorFilter(
      BumpType bumpType, BumpTarget bumpTenorTarget,
      string[] bumpTenors, string[] curveNames)
    {
      if ((bumpTenorTarget & AllCurveQuotes) == 0)
      {
        throw new ToolkitException(String.Format(
          "Unable to handle target tenor types {0}",
          bumpTenorTarget));
      }
      if (bumpTenors == null || bumpTenors.Length == 0)
      {
        if (bumpType == BumpType.Uniform)
        {
          switch (bumpTenorTarget)
          {
            case BumpTarget.InterestRates:
              return CurveTenorSelectors.UniformRate;
            case BumpTarget.InflationRates:
            case BumpTarget.CommodityPrice:
            case BumpTarget.StockPrice:
              return CurveTenorSelectors.UniformForwardPrice;
            case BumpTarget.InterestRateBasis:
              return CurveTenorSelectors.UniformRateBasis;
            case BumpTarget.FxRates:
              return CurveTenorSelectors.UniformFxRate;
            case BumpTarget.CreditQuotes:
              return CurveTenorSelectors.UniformCredit;
            default:
              break;
          }
        }
        if (curveNames == null || curveNames.Length == 0)
        {
          return (c, t) => CurveSelect(c, bumpTenorTarget)
                           && IsSelected(t, bumpTenorTarget);
        }
        return (c, t) => CurveSelect(c, bumpTenorTarget)
                         && curveNames.Contains(c.Name)
                         && IsSelected(t, bumpTenorTarget);
      }
      if (curveNames == null || curveNames.Length == 0)
      {
        return (c, t) => CurveSelect(c, bumpTenorTarget)
                         && IsSelected(t, bumpTenorTarget)
                         && bumpTenors.Contains(t.Name);
      }
      return (c, t) => CurveSelect(c, bumpTenorTarget)
                       && curveNames.Contains(c.Name)
                       && IsSelected(t, bumpTenorTarget)
                       && bumpTenors.Contains(t.Name);
    }

    private const BumpTarget AllCurveQuotes =
      BumpTarget.InterestRates | BumpTarget.InterestRateBasis |
      BumpTarget.FxRates | BumpTarget.CreditQuotes | BumpTarget.InflationRates
      | BumpTarget.CommodityPrice | BumpTarget.StockPrice;

    #endregion General interface

    #region Retrieve dependent curves

    /// <summary>
    ///   Get all the curves in the pricer evaluators which
    ///   depend on the specified curves.
    /// </summary>
    /// <param name="evaluators">The pricer evaluators</param>
    /// <param name="curves">The parent curves</param>
    /// <returns>The parent curves and all the curves depedent on them,
    ///  in the dependency order.</returns>
    public static IList<CalibratedCurve> GetDependentCurves(
      this PricerEvaluator[] evaluators,
      IEnumerable<CalibratedCurve> curves)
    {
      if (curves == null) return null;
      return GetDependentCurves(curves, GetCurves(evaluators, AllCurveQuotes));
    }

    /// <summary>
    ///   Get all the curves in the reference curve set which
    ///   depend on the specified curves.
    /// </summary>
    /// <param name="curves">The parent curves</param>
    /// <param name="referenceCurveSet">The reference curve set.</param>
    /// <returns>The parent curves and all the curves depedent on them,
    ///  in the dependency order.</returns>
    private static IList<CalibratedCurve> GetDependentCurves(
      IEnumerable<CalibratedCurve> curves,
      IEnumerable<CalibratedCurve> referenceCurveSet)
    {
      if (curves == null)
        return null;
      curves = curves.Where(c => c.Calibrator != null);
      if (referenceCurveSet != null)
        referenceCurveSet = referenceCurveSet.Where(c => c.Calibrator != null);
      return curves.GetDescendants(referenceCurveSet,
        c => c.EnumeratePrerequisiteCurves().Where(pc => pc.Calibrator != null));
    }

    #endregion Retrieve dependent curves

    #region Utils


    private static bool CurveSelect(CalibratedCurve curve, BumpTarget bumpTarget)
    {
      var fpTarget = BumpTarget.InflationRates 
        | BumpTarget.CommodityPrice | BumpTarget.StockPrice;

      if ((bumpTarget & fpTarget) == 0) return true;

      return ((bumpTarget & BumpTarget.CommodityPrice) != 0 && (curve is CommodityCurve))
             || ((bumpTarget & BumpTarget.StockPrice) != 0 && (curve is StockCurve))
             || ((bumpTarget & BumpTarget.InflationRates) != 0
                 && (curve is DiscountCurve || curve is InflationFactorCurve));
    }

    /// <summary>
    ///   Get all the curves the pricer evaluators depend on.
    /// </summary>
    /// <param name="evaluators">The pricer evaluators</param>
    /// <returns>The parent curves and all the curves depedent on them,
    ///  in the dependency order.</returns>
    internal static IList<CalibratedCurve> GetPrerequisiteCurves(
      this PricerEvaluator[] evaluators)
    {
      return GetCurves(evaluators, AllCurveQuotes)
        .ToDependencyGraph(c => c.EnumeratePrerequisiteCurves());
    }

    internal static IEnumerable<CalibratedCurve> GetCurves(
      this PricerEvaluator pricer, BumpTarget bumpTenorTarget)
    {
      const BumpTarget irQuotes = BumpTarget.InterestRates
                                  | BumpTarget.InterestRateBasis;
      if ((bumpTenorTarget & irQuotes) != 0)
      {
        foreach (var curve in pricer.RateCurves)
        {
          if (curve is DiscountCurve) yield return curve;
        }
      }
      if ((bumpTenorTarget & (BumpTarget.FxRates|irQuotes)) != 0
        && pricer.FxCurve != null)
      {
          foreach (var fx in pricer.FxCurve)
          {
            yield return fx;
          }
      }
      if ((bumpTenorTarget & BumpTarget.CreditQuotes) != 0)
      {
        var g = pricer.Pricer as ISpreadSensitivityCurvesGetter;
        IEnumerable<SurvivalCurve> sc = g != null
          ? g.GetCurves() : pricer.SurvivalCurves;
        if (sc != null)
        {
          foreach (var s in sc.Where(s => s != null))
            yield return s;
        }
      } // end of if
    }

    private static IList<CalibratedCurve> GetCurves(
      PricerEvaluator[] p, BumpTarget bumpTenorTarget)
    {
      IEnumerable<CalibratedCurve> result = null;
      if ((bumpTenorTarget & (BumpTarget.InterestRates
        | BumpTarget.InterestRateBasis)) != 0)
      {
        result= GetRateCurves(p);
      }
      if ((bumpTenorTarget & BumpTarget.CommodityPrice) != 0)
      {
        result = result?.Concat(p.GetCommodityCurves()) 
          ?? p.GetCommodityCurves();
      }
      if ((bumpTenorTarget & BumpTarget.StockPrice) != 0)
      {
        result = result?.Concat(p.GetStockCurves())
          ?? p.GetStockCurves();
      }
      if ((bumpTenorTarget & BumpTarget.InflationRates) != 0)
      {
        result = result?.Concat(p.GetInflationCurves()) 
          ?? p.GetInflationCurves();
      }
      if ((bumpTenorTarget & BumpTarget.FxRates) != 0)
      {
        result = result?.Concat(p.GetAllFxCurves()) 
          ?? p.GetAllFxCurves();
      }
      if ((bumpTenorTarget & BumpTarget.CreditQuotes) != 0)
      {
        result = result?.Concat(p.GetNonDefaultedCreditCurves()) 
          ?? p.GetNonDefaultedCreditCurves();
      }
      return result == null
        ? EmptyArray<CalibratedCurve>.Instance
        : ((result as IList<CalibratedCurve>) ?? result.ToList());
    }


    private static IList<CalibratedCurve> GetInflationCurves(
      this PricerEvaluator[] pricers)
    {
      var list = new List<CalibratedCurve>();

      foreach (var pricer in pricers)
      {
        var ic = pricer.InflationCurves;
        if (ic != null)
        {
          foreach (var r in ic)
          {
            list.AddCurve(r);
          }
        }
        var rc = pricer.ReferenceCurves;
        if (rc == null) continue;
        foreach (var r in rc.OfType<InflationCurve>())
        {
          list.AddCurve(r);
        }
      }
      return list;
    }

    private static IList<CalibratedCurve> GetCommodityCurves(
      this PricerEvaluator[] pricers)
    {
      var list = new List<CalibratedCurve>();
      foreach (var pricer in pricers)
      {
        var cCurves = pricer.CommodityCurves;
        if (cCurves != null)
        {
          foreach (var r in cCurves)
          {
            list.AddCurve(r);
          }
        }
        var rc = pricer.ReferenceCurves;
        if (rc == null) continue;
        foreach (var r in rc.OfType<CommodityCurve>())
        {
          list.AddCurve(r);
        }
      }
      return list;
    }

    private static IList<CalibratedCurve> GetStockCurves(
      this PricerEvaluator[] pricers)
    {
      var list = new List<CalibratedCurve>();

      foreach (var pricer in pricers)
      {
        var sCurves = pricer.StockCurves;
        if (sCurves != null)
        {
          foreach (var r in sCurves)
          {
            list.AddCurve(r);
          }
        }
        var rc = pricer.ReferenceCurves;
        if (rc == null) continue;
        foreach (var r in rc.OfType<StockCurve>())
        {
          list.AddCurve(r);
        }
      }
      return list;
    }

    private static void AddCurve(this IList<CalibratedCurve> list,
      ForwardPriceCurve fpc)
    {
      if (fpc == null || list.Contains(fpc)) return;
      //for inflation curve, we need to add the target curve
      var iCurve = fpc as InflationCurve;
      if (iCurve != null)
      {
        var target = fpc.TargetCurve;
        if (!list.Contains(target)) list.Add(target);
        return;
      }
      if(!list.Contains(fpc)) list.Add(fpc);
      return;
    }


    private static IEnumerable<SurvivalCurve> GetNonDefaultedCreditCurves(
      this PricerEvaluator[] p)
    {
      return p.GetSpreadSensitivityCurves(false)
        .Where(s => s.Defaulted == Defaulted.NotDefaulted);
    }


    private static IList<CalibratedCurve> GetRateCurves(
      this IEnumerable<PricerEvaluator> pricers)
    {
      var list = new List<CalibratedCurve>();
      var checkedFxCurves = new List<FxCurve>();
      foreach (var pricer in pricers)
      {
        foreach (var curve in pricer.RateCurves.Where(
          curve => curve is DiscountCurve && !list.Contains(curve)))
        {
          list.Add(curve);
        }
        var fx = pricer.FxCurve;
        if (fx == null) continue;
        foreach (var fxCurve in fx.OfType<FxCurve>().Where(
          fxCurve => !checkedFxCurves.Contains(fxCurve)))
        {
          checkedFxCurves.Add(fxCurve);
          fxCurve.GetComponentCurves<DiscountCurve>(true, list);
        }
      }
      return list;
    }

    private static IEnumerable<CalibratedCurve> GetAllFxCurves(
      this IEnumerable<PricerEvaluator> pricers)
    {
      var list = new List<CalibratedCurve>();
      var checkedFxCurves = new List<FxCurve>();
      foreach (var pricer in pricers)
      {
        var fx = pricer.FxCurve;
        if (fx == null) continue;
        foreach (var curve in fx)
        {
          var fxCurve = curve as FxCurve;
          // Not an FxCurve derived class
          if (fxCurve == null)
          {
            if (curve != null && !list.Contains(curve))
            {
              list.Add(curve);
            }
            continue;
          }
          if (checkedFxCurves.Contains(fxCurve)) continue;
          checkedFxCurves.Add(fxCurve);
          fxCurve.GetComponentCurves(IsFxComponentCurve, true, list);
        }
      }
      return list;
    }

    private static bool IsFxComponentCurve(this CalibratedCurve curve)
    {
      return curve.Calibrator is FxBasisFitCalibrator
        || curve.Tenors.Any(t => t.Product is FxForward);
    }

    private static bool IsSelected(CurveTenor tenor, BumpTarget target)
    {
      return ((target & BumpTarget.InterestRates) != 0 && tenor.IsRateTenor()) ||
             ((target & BumpTarget.InterestRateBasis) != 0 && tenor.IsRateBasisTenor()) ||
             ((target & BumpTarget.FxRates) != 0 && tenor.IsFxTenor()) ||
             ((target & BumpTarget.CreditQuotes) != 0 && tenor.IsCreditTenor()) ||
             ((target & BumpTarget.InflationRates) != 0 && tenor.IsInflationRateTenor()) ||
             ((target & BumpTarget.StockPrice) != 0 && tenor.IsStockPriceTenor()) ||
             ((target & BumpTarget.CommodityPrice) != 0 && tenor.IsCommodityPriceTenor());
    }

    /// <summary>
    /// Creates the hedge pricer getter.
    /// </summary>
    /// <param name="target">The bump target</param>
    /// <param name="bumpType">Type of the bump.</param>
    /// <param name="flags">Flags</param>
    /// <param name="calcHedge">Calculate hedge</param>
    /// <param name="hedgeTenor">The hedge tenor.</param>
    /// <returns></returns>
    private static Func<ISensitivitySelection, IReEvaluator> CreateHedgePricerGetter(
      BumpTarget target, BumpType bumpType, BumpFlags flags,
      bool calcHedge, string hedgeTenor)
    {
      if (!calcHedge) return null;

      if (bumpType == BumpType.ByTenor && (hedgeTenor == "matching"
        || (flags & BumpFlags.NoHedgeOnTenorMismatch) != 0))
      {
        return ByTenorHedgePricerMaker(hedgeTenor);
      }
      if (bumpType == BumpType.Parallel)
        return s => GetHedgePricerForParallelBump(s, hedgeTenor);

      //TODO: better handling hedge
      Func<ISensitivitySelection, IReEvaluator> toPricer;
      if ((target & BumpTarget.CreditQuotes) != 0)
      {
        toPricer = s => s == null
          ? null
          : (s is ICurveTenorSelection
            ? ((ICurveTenorSelection)s).Curves
            : s.AllCurves).GetHedgePricerByTenorName(hedgeTenor);
        return toPricer.PreserveEqualityBy(s => GetKey(s, hedgeTenor));
      }
      toPricer = s => s == null
        ? null
        : s.AllCurves.GetHedgePricerByTenorName(hedgeTenor);
      return toPricer.PreserveEqualityBy(s => hedgeTenor);
    }

    private static string GetKey(ISensitivitySelection ss, string hedgeTenor)
    {
      var bs = ss as ICurveTenorSelection;
      if (bs != null && bs.Curves != null && bs.Curves.Count == 1
        && bs.Curves[0] is SurvivalCurve)
      {
        return String.Format("{0}.{1}", bs.Curves[0].Id, hedgeTenor);
      }
      return hedgeTenor;
    }

    #endregion Utils

    #region Core functions

    internal static ResultTable Calculate(
      PricerEvaluator[] evaluators,
      double upBump,
      double downBump,
      BumpFlags bumpFlags,
      bool scaledDelta,
      bool calcGamma,
      Func<PricerEvaluator[],IList<CalibratedCurve>> getCurves,
      Func<DependencyGraph<CalibratedCurve>, IEnumerable<ICurveTenorSelection>> getTenorSelections,
      Func<ISensitivitySelection, IReEvaluator> getHedgePricer,
      bool cache,
      DataTable dataTable)
    {
      var results = new ResultTable(dataTable, calcGamma,getHedgePricer!=null);
      if (evaluators == null || evaluators.Length == 0) return results;

      var curves = getCurves(evaluators);
      if(curves==null||curves.Count==0) return results;
      var graph = curves.Where(c => c.Calibrator != null)
        .ToDependencyGraph(c => c.EnumeratePrerequisiteCurves());
      var selections = getTenorSelections(graph).ToArray();
      int count = selections.Length;

      var hedgePricers = new IReEvaluator[count];
      if (getHedgePricer != null)
      {
        for (int i = 0; i < count; ++i)
          hedgePricers[i] = getHedgePricer(selections[i].SetAllCurves(graph));

        // Include in dependency graph the curves affecting hedge pricers
        var originalCurveSet = graph;
        graph = originalCurveSet.Concat(getCurves(hedgePricers
          .OfType<ReEvaluator>().Select(p => p != null ? p.Evaluator : null)
          .Where(p => p != null).Distinct().ToArray()))
          .ToDependencyGraph(c => c.EnumeratePrerequisiteCurves());

        // Update tenor selections when new curves are pulled in by hedge pricers.
        if (graph.Count != originalCurveSet.Count)
          selections.Update(getTenorSelections(graph));
      }

      using (var evals = evaluators.ToReEvaluatorList(bumpFlags))
      using (SaveCurveCacheStates(graph, cache))
      {
        graph.InitializeCurveBump(bumpFlags);
        var workspace = new List<CalibratedCurve>(graph.Count);
        for (int i = 0; i < count; ++i)
        {
          CalculateSensitivities(evals, graph, selections[i],
            upBump, downBump, bumpFlags, scaledDelta, calcGamma,
            hedgePricers[i], workspace, results);
        }
      }
      return results;
    }

    private static void Update(this ICurveTenorSelection[] selections,
      IEnumerable<ICurveTenorSelection> possiblyExpandedSelections)
    {
      foreach (var possiblyExpanded in possiblyExpandedSelections)
      {
        for (int i = 0; i < selections.Length; ++i)
        {
          if (selections[i].Name == possiblyExpanded.Name)
          {
            selections[i] = possiblyExpanded;
            break;
          }
        }
      }
    }

    private static ReEvaluatorList ToReEvaluatorList(
      this IEnumerable<PricerEvaluator> pricers, BumpFlags flags)
    {
      var list = new ReEvaluatorList();
      list.AddRange(pricers.Where(p => p != null).Select(ReEvaluator.Create));
      var rescaleStrike = (flags & BumpFlags.RemapCorrelations) != 0;
      for (int i = 0, n = list.Count; i < n; ++i)
        list[i].SetRescaleStrike(rescaleStrike);
      return list;
    }

    private static ISensitivitySelection SetAllCurves(
      this ISensitivitySelection selection,
      DependencyGraph<CalibratedCurve> allCurves)
    {
      return selection.AllCurves != null
        ? selection
        : new SensitivitySelection(selection, allCurves);
    }
    private class SensitivitySelection : ISensitivitySelection
    {
      public SensitivitySelection(ISensitivitySelection sel,
        DependencyGraph<CalibratedCurve> allCurves)
      {
        sel_ = sel;
        allCurves_ = allCurves;
      }
      public string Name { get { return sel_.Name; } }
      public IList<CurveTenor> Tenors { get { return sel_.Tenors; }}
      public IEnumerable<CalibratedCurve> AllCurves
      {
        get { return allCurves_.ReverseOrdered(); }
      }
      private ISensitivitySelection sel_;
      private DependencyGraph<CalibratedCurve> allCurves_;
    }

    private static CurveCacheStateSaver<T> SaveCurveCacheStates<T>(
      this IList<T> curves, bool useCache) where T: Curve
    {
      return new CurveCacheStateSaver<T>(useCache,curves);
    }

    private class CurveCacheStateSaver<T> : IDisposable where T : Curve
    {
      private Curve[] curves_;

      public CurveCacheStateSaver(bool useCache, IList<T> curves)
      {
        if (!useCache || curves == null || curves.Count == 0) return;
        int count = curves.Count;
        curves_ = new Curve[count];
        for (int i = 0; i < count; ++i)
        {
          if (curves[i].CacheEnabled) continue;
          curves_[i] = curves[i];
          var fpc = curves[i] as ForwardPriceCurve;
          if (fpc?.TargetCurve == null)
            curves[i].EnableCache();
        }
      }

      public void Dispose()
      {
        if (curves_ == null) return;
        for (int i = 0; i < curves_.Length; ++i)
          if (curves_[i] != null) curves_[i].DisableCache();
      }
    }

    private static void CalculateSensitivities(
      IEnumerable<ReEvaluator> pricerEvaluators,
      DependencyGraph<CalibratedCurve> graph, ICurveTenorSelection selection,
      double upBump, double downBump, BumpFlags flags,
      bool scaledDelta, bool calcGamma, IReEvaluator hedging,
      List<CalibratedCurve> affectedCurves, ResultTable results)
    {
      //TODO: remove this condition, for it should be the same for both in-place and overlay.
      var evaluators = (flags & BumpFlags.BumpInPlace) == 0
        ? pricerEvaluators.ToArray()
        : pricerEvaluators.Where(p => p.DependsOn(selection)).ToArray();
      if (evaluators.IsNullOrEmpty() || selection == null) return;

      try
      {
        double[] upTable = null, downTable = null;
        double upBumped = 0, downBumped = 0, upHedge = 0, downHedge = 0;

        // Bump up
        if (!upBump.ApproximatelyEqualsTo(0.0))
        {
          logger.DebugFormat("Selector {0} - bumping up", selection.Name);

          upBumped = graph.BumpTenors(selection, upBump, flags, affectedCurves);
          if (!upBumped.ApproximatelyEqualsTo(0.0))
          {
            upTable = evaluators.Select(p => p.ReEvaluate()).ToArray();
            if (hedging != null)
            {
              upHedge = hedging.ReEvaluate() - hedging.BaseValue;
            }
          }
        }

        // Bump down
        if (!downBump.ApproximatelyEqualsTo(0.0))
        {
          logger.DebugFormat("Selector {0} - bumped down", selection.Name);

          downBumped = graph.BumpTenors(selection, downBump,
            flags | BumpFlags.BumpDown, affectedCurves);
          if (!downBumped.ApproximatelyEqualsTo(0.0))
          {
            downTable = evaluators.Select(p => p.ReEvaluate()).ToArray();
            if (hedging != null)
            {
              downHedge = hedging.ReEvaluate() - hedging.BaseValue;
            }
          }
        }

        // Save results
        logger.DebugFormat("Selector {0} - saving results", selection.Name);

        Fill(results, evaluators,
          GetCategory(selection), GetCurveNames(selection), selection.Name,
          scaledDelta, calcGamma, upTable, upBumped, downTable, downBumped,
          hedging, upHedge, downHedge);
      }
      finally
      {
        affectedCurves.RestoreBaseCurves();
        affectedCurves.Clear();
      }
    }

    private static void Fill(ResultTable results, IReEvaluator[] evaluators,
      string category, string elemLabel, string tenorName, bool scaledDelta, bool calcGamma,
      double[] upTable, double upBumped, double[] downTable, double downBumped,
      IReEvaluator hedging, double upHedge, double downHedge)
    {
      double hedgeDelta = hedging != null
        ? CalcHedge(upHedge, downHedge, scaledDelta, upBumped, downBumped)
        : 0;

      // Fill in the table
      var hedgeName = hedging == null ? null : hedging.Name;
      for (int j = 0; j < evaluators.Length; j++)
      {
        var row = results.NewRow();
        row.Category = category;
        row.Element = elemLabel;
        row.Pricer = evaluators[j].Name;
        row.CurveTenor = tenorName;
        var basePv = evaluators[j].BaseValue;

        // The curve is bumped by non-zero units....
        logger.DebugFormat("Tenor {0}, trade {1}, up = {2}, mid = {3}, down = {4}",
          tenorName, evaluators[j].Name,
          (upTable != null) ? upTable[j] : 0.0, basePv,
          (downTable != null) ? downTable[j] : 0.0);

        double delta = Sensitivities.CalcDelta(j, basePv, upTable, downTable,
          scaledDelta, upBumped, downBumped);
        row.Delta = delta;

        if (calcGamma)
        {
          double gamma = Sensitivities.CalcGamma(j, basePv, upTable, downTable,
            scaledDelta, upBumped, downBumped);
          row.Gamma = gamma;
        }

        if (results.CalcHedge)
        {
          row.HedgeTenor = hedgeName;
          row.HedgeDelta = 1000000 * hedgeDelta;
          row.HedgeNotional = !hedgeDelta.AlmostEquals(0.0) ? delta / hedgeDelta : 0.0;
        }

        results.AddRow(row);
      } // tranches...
    }

    private static double CalcHedge(double upHedge, double downHedge,
      bool scaled, double upBump, double downBump)
    {
      double hedge = upHedge - downHedge;
      if (scaled)
      {
        //ensure denominator is non-zero; 
        //upBump on downBump may be zeroed out during the sensitivity calc if the curve is defaulted
        if ((upBump + downBump).ApproximatelyEqualsTo(0))
          return 0.0;
        hedge /= (upBump + downBump);
      }
      return hedge;
    }

    private static string GetCurveNames(ICurveTenorSelection selection)
    {
      var curves = selection.Curves;
      if (curves == null || curves.Count == 0)
        return "all";
      string label = curves[0].Name;
      for (int i = 1, n = curves.Count; i < n; ++i)
      {
        // First make sure that no name appear twice.
        bool hasit = false;
        var name = curves[i].Name;
        for (int j = 0; j < i; ++j)
        {
          if (name != curves[j].Name) continue;
          hasit = true;
          break;
        }
        if (hasit) continue;
        // Only the new names are added to the label.
        label += CurveSeparatorChar;
        label += name;
      }
      return label;
    }

    private static string GetCategory(ICurveTenorSelection selection)
    {
      if (selection.Curves.Count == 1)
      {
        var curve = selection.Curves.First() as SurvivalCurve;
        if(curve != null) return curve.Category;
      }
      return "all";
    }

    #endregion

    #region By-Tenor selection

    private static IEnumerable<ByTenorSelection> GetByTenorsSelections(
      DependencyGraph<CalibratedCurve> curves,
      Func<CalibratedCurve,CurveTenor,bool> tenorFilter,
      Func<ISensitivitySelection, IReEvaluator> getHedgePricer)
    {
      var dict = new Dictionary<CurveTenor, Tuple<IReEvaluator,
        List<CurveTenor>, List<CalibratedCurve>>>(CurveTenorComparer.Default);
      // We find hedge pricers starting from the most dependent curves
      // for the reason that they have more complete infomation about
      // the curves the use.
      foreach (var curve in curves.ReverseOrdered())
      {
        for (int i = 0, n = curve.Tenors.Count; i < n; ++i)
        {
          var tenor = curve.Tenors[i];
          Tuple<IReEvaluator, List<CurveTenor>, List<CalibratedCurve>> value;
          if (dict.TryGetValue(tenor, out value))
          {
            var tenors = value.Item2;
            if (!tenors.Contains(tenor)) tenors.Add(tenor);
            //TODO: The following are removed, for we only include one curve
            //TODO: the tenor naturally belongs to. Currently this is picked
            //TODO: as the most dependent curve containing the tenor, done below.
            //var crvs = value.Item3;
            //if (!crvs.Contains(curve)) crvs.Add(curve);
          }
          else if (tenorFilter == null || tenorFilter(curve, tenor))
          {
            IReEvaluator pricer = null;
            if (getHedgePricer != null)
            {
              var sel = new ByTenorSelection(curves, new[] {curve}, new[] {tenor}, null);
              pricer = getHedgePricer(sel);
            }
            dict.Add(tenor, MakeTuple(pricer, tenor, curve));
          }
        }
      }
      return dict.Select(pair => new ByTenorSelection(
        curves,
        pair.Value.Item3.ToArray(),
        pair.Value.Item2.ToArray(),
        pair.Value.Item1));
    }

    static Func<ISensitivitySelection, IReEvaluator> ByTenorHedgePricerMaker(
      string hedgeTenor)
    {
      bool notMatching = hedgeTenor != "matching";
      return selection =>
        {
          var curve = ((ByTenorSelection)selection).Curves.FirstOrDefault();
          var tenor = selection.Tenors[0];
          if (curve == null || curve.Calibrator == null ||
              (notMatching && hedgeTenor != tenor.Name))
          {
            return null;
          }
          var pricer = curve.Calibrator.GetPricer(
            curve, (IProduct) tenor.Product.Clone());
          pricer.Product.Description = tenor.Name;
          return ReEvaluator.Create(pricer);
        };
    }

    static Tuple<IReEvaluator, List<CurveTenor>, List<CalibratedCurve>>
      MakeTuple(IReEvaluator p, CurveTenor t, CalibratedCurve c)
    {
      return new Tuple<IReEvaluator, List<CurveTenor>, List<CalibratedCurve>>(
        p, new List<CurveTenor> {t}, new List<CalibratedCurve> {c});
    }

    #endregion

    #region Retrieve hedge pricers

    internal static IReEvaluator GetHedgePricerByTenorName(
      this PricerEvaluator[] pricers,
      string hedgeTenor,
      Func<PricerEvaluator[], IList<CalibratedCurve>> getCurves)
    {
      if (pricers == null || getCurves == null || String.IsNullOrEmpty(hedgeTenor))
        return null;
      var curves = getCurves(pricers);
      if (curves == null || curves.Count == 0) return null;
      return curves.GetHedgePricerByTenorName(hedgeTenor);
    }

    internal static IReEvaluator GetHedgePricerByTenorName(
      this IEnumerable<CalibratedCurve> curves, string hedgeTenor)
    {
      if (String.IsNullOrEmpty(hedgeTenor)) return null;
      return curves.Select(curve => curve.GetHedgePricerByTenorName(hedgeTenor))
        .ToEvaluator(hedgeTenor);
    }

    private static IReEvaluator GetHedgePricerForParallelBump(
      ISensitivitySelection selection, string hedgeTenor)
    {
      if (selection == null) return null;
      var sel = selection as ICurveTenorSelection;
      if (sel != null && sel.Curves != null)
      {
        var pricer = sel.Curves.GetHedgePricerByTenorName(hedgeTenor);
        if (pricer != null) return pricer;
      }
      return selection.AllCurves.GetHedgePricerByTenorName(hedgeTenor);
    }

    private static IPricer GetHedgePricerByTenorName(
      this CalibratedCurve curve, string hedgeTenor)
    {
      if (curve == null || curve.Calibrator == null) return null;
      var tenor = curve.Tenors.FirstOrDefault(t => t.Name == hedgeTenor);
      return tenor == null ? null : curve.GetPricer(tenor.Product, hedgeTenor);
    }

    private static IPricer GetPricer(
      this CalibratedCurve curve, IProduct product, string name)
    {
      if (curve == null || curve.Calibrator == null || product == null)
        return null;
      product = (IProduct) product.Clone();
      var p= curve.Calibrator.GetPricer(curve, product);
      if (p!=null && !String.IsNullOrEmpty(name))
        p.Product.Description = name;
      return p;
    }

    private static IReEvaluator ToEvaluator(
      this IEnumerable<IPricer> pricers, string name)
    {
      if (pricers == null) return null;
      PricerEvaluator evaluator = null;
      List<PricerEvaluator> list = null;
      foreach (var pricer in pricers)
      {
        if (pricer == null) continue;
        if (evaluator == null)
        {
          evaluator = new PricerEvaluator(pricer);
          continue;
        }
        if (list == null)
        {
          list = new List<PricerEvaluator>
          {
            evaluator,
            new PricerEvaluator(pricer)
          };
          continue;
        }
        list.Add(new PricerEvaluator(pricer));
      }
      if (list == null) return ReEvaluator.Create(evaluator);
      return new AggregateEvaluator(list, name);
    }

    #endregion Retrieve hedge pricers
  }
}
