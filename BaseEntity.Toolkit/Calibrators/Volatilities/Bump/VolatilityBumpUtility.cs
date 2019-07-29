/*
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using log4net;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Interface for the special volatility smile tenors
  /// with their own mechanism to determine the level
  /// of the smile.  Currently only the risk-reversal
  /// and butterfly type of quotes implement this.
  /// </summary>
  public interface IVolatilityLevelHolder
  {
    /// <summary>
    /// Gets or sets the volatility level.
    /// </summary>
    /// <value>The level.</value>
    double Level { get; set; }
  }
}

namespace BaseEntity.Toolkit.Calibrators.Volatilities.Bump
{
  /// <summary>
  /// Utility methods for Volatility Bumps
  /// </summary>
  public static class VolatilityBumpUtility
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(VolatilityBumpUtility));

    public static void Restore(
      this IVolatilityTenorSelection selection,
      BumpFlags flags)
    {
      if ((flags & BumpFlags.BumpInterpolated) != 0)
      {
        foreach (var surface in selection.Surfaces)
          RestoreInterpolated(surface);
        return;
      }

      foreach (var tenor in selection.Tenors)
        tenor.Restore();
      foreach (var surface in selection.Surfaces)
        surface.Fit();
    }

    public static BumpResult Bump(
      this IVolatilityTenorSelection selection,
      double bumpSize, BumpFlags flags)
    {
      if ((flags & BumpFlags.BumpInterpolated) != 0)
      {
        var result = new BumpAccumulator();
        foreach (var surface in selection.Surfaces)
        {
          BumpInterpolated(surface, bumpSize, flags, result);
        }
        return result;
      }

      var bumped = selection.Tenors.Average(t => t.Bump(bumpSize, flags));
      foreach (var surface in selection.Surfaces)
      {
        try
        {
          surface.Fit();
        }
        catch (SolverException ex)
        {
          logger.Error(String.Format("Failed bump {0}", selection.Name), ex);
          return 0.0;
        }
      }
      return bumped;
    }

    /// <summary>
    /// Bumps the interpolated volatility by the specified size.
    /// </summary>
    /// <param name="surface">The surface to bump</param>
    /// <param name="bumpSize">Size of the bump</param>
    /// <param name="flags">The flags</param>
    public static void BumpInterpolated(
      this CalibratedVolatilitySurface surface,
      double bumpSize, BumpFlags flags)
    {
      BumpInterpolated(surface, bumpSize, flags, new BumpAccumulator());
    }

    /// <summary>
    /// Bumps the interpolated volatility by the specified size.
    /// </summary>
    /// <param name="surface">The surface to bump</param>
    /// <param name="bumpSize">Size of the bump</param>
    /// <param name="flags">The flags</param>
    /// <param name="result">The result accumulator</param>
    /// <exception cref="System.NotSupportedException"></exception>
    private static void BumpInterpolated(
      CalibratedVolatilitySurface surface,
      double bumpSize, BumpFlags flags,
      BumpAccumulator result)
    {
      var rateSurface = surface as RateVolatilitySurface;
      if (rateSurface != null)
      {
        var rateInterp = rateSurface.RateVolatilityInterpolator;
        var swapInterp = rateInterp as ISwapVolatilitySurfaceInterpolator;
        if (swapInterp != null)
        {
          rateSurface.RateVolatilityInterpolator =
            new SwapVolatilityBumpInterpolator(swapInterp,
              bumpSize, flags, result);
          return;
        }
        if (rateInterp != null && surface.Interpolator == null)
        {
          throw new NotSupportedException(
            $"Bump interpolated value of {rateSurface.GetType().Name} not supported yet");
        }
      }
      surface.Interpolator = VolatilityBumpInterpolator.Create(
        surface.Interpolator, bumpSize, flags, result);
    }

    /// <summary>
    /// Restores the interpolated bump to the original states.
    /// </summary>
    /// <param name="surface">The surface.</param>
    public static void RestoreInterpolated(
      this CalibratedVolatilitySurface surface)
    {
      VolatilityBumpInterpolator bumped;
      var rateSurface = surface as RateVolatilitySurface;
      if (rateSurface != null)
      {
        bumped = rateSurface.RateVolatilityInterpolator as VolatilityBumpInterpolator;
        if (bumped != null)
        {
          rateSurface.RateVolatilityInterpolator = bumped.BaseInterpolator;
          return;
        }
      }
      bumped = surface.Interpolator as VolatilityBumpInterpolator;
      if (bumped != null)
        surface.Interpolator = bumped.BaseInterpolator;
    }

    /// <summary>
    /// Util method to perform a bump scenario
    /// </summary>
    /// <param name="scenarioName"></param>
    /// <param name="surfaces"></param>
    /// <param name="tenors"></param>
    /// <param name="bumps"></param>
    /// <param name="flags"></param>
    /// <returns></returns>
    public static double BumpVolatilities(string scenarioName,
      IList<CalibratedVolatilitySurface> surfaces,
      IList<string> tenors,
      IList<double> bumps,
      BumpFlags flags)
    {
      Func<CalibratedVolatilitySurface, IVolatilityTenor, double> bumpFilter
        = (surface, tenor) => surfaces.Contains(surface) && tenors.Contains(tenor.Name)
          ? bumps[tenors.IndexOf(tenor.Name)] : 0.0;
      var scenario = surfaces.SelectScenario(scenarioName, bumpFilter);
      return scenario.Bump(flags);
    }

    /// <summary>
    /// Util method to perform a bump scenario
    /// </summary>
    /// <param name="scenarioName"></param>
    /// <param name="surface"> </param>
    /// <param name="bumps"></param>
    /// <param name="relative"> </param>
    /// <param name="refit"> </param>
    /// <returns></returns>
    public static double BumpVolatilities(string scenarioName,
      CalibratedVolatilitySurface surface,
      Func<IVolatilityTenor, double> bumps,
      bool relative, bool refit)
    {
      Func<CalibratedVolatilitySurface, IVolatilityTenor, double> bumpFilter = (s, t) => s == surface ? bumps(t) : 0.0;
      var surfaces = new[] {surface};
      var scenario = surfaces.SelectScenario(scenarioName, bumpFilter);
      var flags = relative ? BumpFlags.BumpRelative : BumpFlags.None;
      flags |= refit ? BumpFlags.RefitCurve : BumpFlags.None; 
      return scenario.Bump(flags);
    }

    public static double Bump(
      this IVolatilityScenarioSelection selection, BumpFlags flags)
    {
      var bumped = selection.Tenors
        .Zip(selection.Bumps, (t, b)=> new {Tenor = t, Bump = b})
        .Average(t => t.Tenor.Bump(t.Bump, flags));
      foreach (var surface in selection.Surfaces)
      {
        try
        {
          surface.Fit();
        }
        catch (SolverException ex)
        {
          logger.Error(String.Format("Failed bump {0}", selection.Name), ex);
          return 0.0;
        }
      }
      return bumped;
    }


    public static double Bump(
      this IVolatilityTenor tenor,
      IList<double> baseValues, 
      double bumpSize, bool bumpRelative, bool up)
    {
      var levelHolder = tenor as IVolatilityLevelHolder;
      if (levelHolder != null)
      {
        var level = levelHolder.Level;
        var bumpAmt = CalculateBumpAmount(
          tenor.Name, level, bumpSize, bumpRelative, up);
        levelHolder.Level += bumpAmt;
        return ((up) ? bumpAmt : -bumpAmt) * VolatilityScaler;
      }

      var vols = tenor.QuoteValues;
      if (vols.Count == 0) return 0.0;

      var baseVols = baseValues ?? vols;
      if (baseVols.Count != vols.Count)
      {
        throw new ToolkitException("Base and target volatilities not match");
      }

      double avgBum = 0;
      for (int i = vols.Count; --i >= 0; )
      {
        var bumpAmt = CalculateBumpAmount(
          tenor.Name, baseVols[i], bumpSize, bumpRelative, up);
        vols[i] += bumpAmt;
        avgBum += ((up) ? bumpAmt : -bumpAmt);
      }
      return avgBum / vols.Count * VolatilityScaler;
    }

    public static double CalculateBumpAmount(
      string tenorName, double volatility,
      double bumpSize, bool bumpRelative, bool up,
      bool ignoreZero = true)
    {
      if (ignoreZero && volatility.ApproximatelyEqualsTo(0.0)) return 0.0; //skip bumping when vol is zero

      int sign = 1;
      if (volatility < 0)
      {
        volatility = -volatility;
        sign = -1;
      }

      if (!(volatility >= 0.0)) // this includes double.NaN
      {
        throw new ArgumentException("Volatility to bump should be a normal positive value");
      }

      double bumpAmt = (up) ? bumpSize : -bumpSize;

      if (bumpRelative)
      {
        if (bumpAmt > 0)
        {
          bumpAmt *= volatility;
        }
        else
        {
          bumpAmt /= (1 - bumpAmt);
          bumpAmt *= volatility;
        }
      }
      else
      {
        bumpAmt /= VolatilityScaler;

        if (volatility + bumpAmt*sign < 0.0)
        {
          logger.DebugFormat(
            "Unable to bump '{0}' with volatility {1} by {2}, bump by {3} instead",
            tenorName, volatility, bumpAmt, -volatility / 2);
          bumpAmt = -volatility / 2;
        }
      }
      return bumpAmt;
    }

    private const double VolatilityScaler = 10000;
  }
}
