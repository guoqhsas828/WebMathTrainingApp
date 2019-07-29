// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Calibrators.Volatilities.Bump;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Calibrators.Volatilities
  .VolatilitySurfaceFactory;

namespace BaseEntity.Toolkit.Sensitivity
{
  public static partial class Sensitivities2
  {
    /// <summary>
    /// Calculates the volatility sensitivities of the specified pricers.
    /// </summary>
    /// <param name="pricers">The pricers.</param>
    /// <param name="measure">The measure.</param>
    /// <param name="upBump">Up bump.</param>
    /// <param name="downBump">Down bump.</param>
    /// <param name="bumpType">Type of the bump.</param>
    /// <param name="bumpFlags">Flags control how quotes are bumped and curves refitted.</param>
    /// <param name="scaleDelta">if set to <c>true</c> [scale delta].</param>
    /// <param name="calculateGamma">if set to <c>true</c> [calculate gamma].</param>
    /// <param name="vsGetter">The vs getter.</param>
    /// <param name="tenorFilter">The tenor filter.</param>
    /// <param name="cache">if set to <c>true</c> [cache].</param>
    /// <param name="dataTable">The data table.</param>
    /// <returns>DataTable.</returns>
    /// <exception cref="ToolkitException"></exception>
    internal static DataTable Calculate(
      IPricer[] pricers,
      object measure,
      double upBump,
      double downBump,
      BumpType bumpType,
      BumpFlags bumpFlags,
      bool scaleDelta,
      bool calculateGamma,
      Func<PricerEvaluator[], IList<CalibratedVolatilitySurface>> vsGetter,
      Func<CalibratedVolatilitySurface, IVolatilityTenor, bool> tenorFilter,
      bool cache,
      DataTable dataTable)
    {
      var evaluators = pricers.CreateAdapters(measure);
      switch (bumpType)
      {
        case BumpType.Uniform:
          return Calculate(evaluators, upBump, downBump,
                           bumpFlags, scaleDelta, calculateGamma, vsGetter,
                           surfaces => new[] {surfaces.SelectTenors(tenorFilter, "Uniform")},
                           cache, dataTable).Table;
        case BumpType.ByTenor:
          return Calculate(evaluators, upBump, downBump,
                           bumpFlags, scaleDelta, calculateGamma, vsGetter,
                           surfaces => surfaces.SelectByTenor(tenorFilter),
                           cache, dataTable).Table;
        case BumpType.Parallel:
          return Calculate(evaluators, upBump, downBump,
                           bumpFlags, scaleDelta, calculateGamma, vsGetter,
                           surfaces => surfaces.SelectParallel(tenorFilter),
                           cache, dataTable).Table;
        default:
          throw new ToolkitException(String.Format(
            "Bump type {0} for volatilities not supported yet",
            bumpType));
      }
    }

    #region Core Implementation of volatility bumping and recalculation

    internal static ResultTable Calculate(
      PricerEvaluator[] evaluators,
      double upBump,
      double downBump,
      BumpFlags bumpFlags,
      bool scaledDelta,
      bool calcGamma,
      Func<PricerEvaluator[], IList<CalibratedVolatilitySurface>> getSurfaces,
      Func<IList<CalibratedVolatilitySurface>,
        IEnumerable<IVolatilityTenorSelection>> getTenorSelections,
      bool cache,
      DataTable dataTable)
    {
      var results = new ResultTable(dataTable, calcGamma, false);
      if (evaluators == null || evaluators.Length == 0) return results;

      var surfaces = getSurfaces(evaluators);
      if (surfaces == null || surfaces.Count == 0) return results;
      var selections = getTenorSelections(surfaces).ToArray();
      int count = selections.Length;

      double[] basePv = evaluators.Select(p => p.Reset().Evaluate()).ToArray();

      for (int i = 0; i < count; ++i)
      {
        CalculateSensitivities(evaluators, selections[i], basePv,
          upBump, downBump, bumpFlags,
          scaledDelta, calcGamma, results);
      }
      return results;
    }

    private static void CalculateSensitivities(
      PricerEvaluator[] evaluators,
      IVolatilityTenorSelection selection,
      double[] basePv, double upBump, double downBump, BumpFlags flags,
      bool scaledDelta, bool calcGamma, ResultTable results)
    {
      try
      {
        double[] upTable = null, downTable = null;
        BumpResult upBumped = 0, downBumped = 0;

        // Bump up
        if (!upBump.AlmostEquals(0.0))
        {
          logger.DebugFormat("Selector {0} - bumping up", selection.Name);

          upBumped = selection.Bump(upBump, flags);
          if (!upBumped.IsEmpty)
          {
            upTable = evaluators.Select(p => ResetVolatility(p).Evaluate())
              .ToArray();
          }
        }

        // Bump down
        if (!downBump.AlmostEquals(0.0))
        {
          logger.DebugFormat("Selector {0} - bumped down", selection.Name);

          downBumped = selection.Bump(downBump, flags | BumpFlags.BumpDown);
          if (!downBumped.IsEmpty)
          {
            downTable = evaluators.Select(p => ResetVolatility(p).Evaluate())
              .ToArray();
          }
        }

        // Save results
        logger.DebugFormat("Selector {0} - saving results", selection.Name);

        var evals = evaluators.Select((p,i) => new ReEvaluator(p, basePv[i]))
          .ToArray();
        Fill(results, evals, "all", GetSurfaceName(selection.Surfaces),
             selection.Name, scaledDelta, calcGamma, upTable, upBumped.Amount,
             downTable, downBumped.Amount, null, 0.0, 0.0);
      }
      finally
      {
        selection.Restore(flags);
      }
    }

    private static PricerEvaluator ResetVolatility(PricerEvaluator evaluator)
    {
      var pricer = evaluator.Pricer as PricerBase;
      if (pricer != null) pricer.Reset(PricerBase.ResetVolatility);
      return evaluator.Reset();
    }

    private static string GetSurfaceName(IEnumerable<CalibratedVolatilitySurface> surfaces)
    {
      if (surfaces == null) return String.Empty;
      CalibratedVolatilitySurface surface = null;
      foreach (var volatilitySurface in surfaces)
      {
        if (surface != null) return "All";
        surface = volatilitySurface;
      }
      return surface == null ? String.Empty : surface.Name;
    }

    #endregion

    #region Volatility Surfaces Getters

    /// <summary>
    /// Get all calibrated volatility surfaces for a list of pricer evaluators
    /// </summary>
    /// <param name="evaluators">Pricer evaluators</param>
    /// <param name="bumpFlags">The bump flags.</param>
    /// <returns>IEnumerable list of CalibratedVolatilitySurfaces</returns>
    public static IList<CalibratedVolatilitySurface> GetVolatilitySurfaces(
      PricerEvaluator[] evaluators, BumpFlags bumpFlags)
    {
      return CreateVolatilitySurfacesGetter(bumpFlags)(evaluators);
    }

    private static Func<PricerEvaluator[], IList<CalibratedVolatilitySurface>>
      CreateVolatilitySurfacesGetter(BumpFlags flag)
    {
      if ((flag & BumpFlags.BumpInterpolated) != 0)
      {
        return prices => prices.SelectMany(GetVolatilitySurfaces)
          .Where(s => s != null).Distinct().ToList();
      }
      return prices => prices
        .SelectMany(GetVolatilitySurfaces).Where(s => s != null)
        .SelectMany(GetComponenSurfaces).Distinct().ToList();

    }

    /// <summary>
    /// Get all calibrated volatility surfaces for pricer evaluator
    /// </summary>
    /// <param name="evaluator">Evaluator</param>
    /// <returns>IEnumerable list of CalibratedVolatilitySurfaces</returns>
    private static IEnumerable<CalibratedVolatilitySurface> GetVolatilitySurfaces(PricerEvaluator evaluator)
    {
      var vsp = evaluator.Pricer as IVolatilitySurfaceProvider;
      return vsp != null
               ? vsp.GetVolatilitySurfaces().OfType<CalibratedVolatilitySurface>()
               : GetVolatilitySurfaces(evaluator.Pricer);
    }

    private static IEnumerable<CalibratedVolatilitySurface>
      GetVolatilitySurfaces(IPricer pricer)
    {
      var surface = GetVolatilitySurfacesDynamic(pricer);
      var cube = surface as SwaptionVolatilityCube;
      if (cube != null && cube.VolatilitySurface != null)
      {
        // We bump ATM volatilities only.
        surface = cube.VolatilitySurface;
      }
      yield return surface;
    }

    private static CalibratedVolatilitySurface
      GetVolatilitySurfacesDynamic(IPricer pricer)
    {
      dynamic dyn = pricer;
      try
      {
        return dyn.VolatilityObject as CalibratedVolatilitySurface;
      }
      catch (Exception)
      {
        // ignored
      }

      try
      {
        return dyn.VolatilitySurface as CalibratedVolatilitySurface;
      }
      catch (Exception)
      {
        // ignored
      }

      try
      {
        return dyn.VolatilityCube as CalibratedVolatilitySurface;
      }
      catch (Exception)
      {
        // ignored
      }

      return null;
    }

    #endregion

    #region Volatility tenor filters

    internal static Func<CalibratedVolatilitySurface, IVolatilityTenor, bool>
      CreateVolatilityTenorFilter(string[] bumpTenors, string[] surfaceNames)
    {
      if (bumpTenors == null || bumpTenors.Length == 0)
      {
        if (surfaceNames == null || surfaceNames.Length == 0)
        {
          return (c, t) => true;
        }
        return (c, t) => surfaceNames.Contains(c.Name);
      }
      if (surfaceNames == null || surfaceNames.Length == 0)
      {
        return (c, t) => bumpTenors.Contains(t.Name);
      }
      return (c, t) => surfaceNames.Contains(c.Name)
                       && bumpTenors.Contains(t.Name);
    }

    #endregion
  }
}