/*
 * SimulatedPath.PartialProxy.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using static BaseEntity.Toolkit.Models.Simulations.MarketEnvironment;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// The extension methods for the simulated paths
  /// </summary>
  public static class SimulationPathExtensions
  {
    #region Methods

    /// <summary>
    /// Evolve discount curve or interpolate by L-2 projection on neighboring simulation dates
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">Curve id</param>
    /// <param name="dt">Future date</param>
    /// <param name="dtIdx">Simulation date index or binary complement of simulation date immediately following dt</param>
    /// <param name="discountCurve">Curve to evolve</param>
    /// <param name="fx">Martingale part of FxRate process</param>
    /// <param name="numeraire">Discounted numeraire process</param>
    public static void EvolveDiscount(
      this SimulatedPath path,
      int curveId, double dt, int dtIdx, DiscountCurve discountCurve,
      out double fx, out double numeraire)
    {
      fx = numeraire = 1.0;
      if (dtIdx >= 0)
        path.EvolveDiscountCurve(curveId, dtIdx, GetNative(discountCurve), ref fx, ref numeraire);
      else
      {
        dtIdx = ~dtIdx;
        path.EvolveDiscountCurve(curveId, dt, dtIdx, GetNative(discountCurve), ref fx, ref numeraire);
      }
    }

    /// <summary>
    /// Evolve spot price or interpolate by L-2 projection on neighboring simulation dates
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="id">Price id</param>
    /// <param name="dt">Future date</param>
    /// <param name="dtIdx">Simulation date index or binary complement of simulation date immediately following dt</param>
    public static double EvolveSpot(
      this SimulatedPath path,
      int id, double dt, int dtIdx)
    {
      if (dtIdx >= 0)
        return path.EvolveSpotPrice(id, dtIdx);
      {
        dtIdx = ~dtIdx;
        return path.EvolveSpotPrice(id, dt, dtIdx);
      }
    }

    /// <summary>
    /// Evolve credit curve or interpolate by L-2 projection on neighboring simulation dates
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">Curve id</param>
    /// <param name="dt">Future date</param>
    /// <param name="dtIdx">Simulation date index or binary complement of simulation date immediately following dt</param>
    /// <param name="creditCurve">Curve to evolve</param>
    public static void EvolveCredit(
      this SimulatedPath path,
      int curveId, double dt, int dtIdx, SurvivalCurve creditCurve)
    {
      if (dtIdx >= 0)
        path.EvolveCreditCurve(curveId, dtIdx, GetNative(creditCurve));
      else
      {
        dtIdx = ~dtIdx;
        path.EvolveCreditCurve(curveId, dt, dtIdx, GetNative(creditCurve));
      }
    }

    /// <summary>
    /// Evolve forward curve or interpolate by L-2 projection on neighboring simulation dates
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">Curve id</param>
    /// <param name="dt">Future date</param>
    /// <param name="dtIdx">Simulation date index or binary complement of simulation date immediately following dt</param>
    /// <param name="forwardCurve">Curve to evolve</param>
    public static void EvolveForward(
      this SimulatedPath path,
      int curveId, double dt, int dtIdx, CalibratedCurve forwardCurve)
    {
      if (dtIdx >= 0)
        path.EvolveForwardCurve(curveId, dtIdx, GetNative(forwardCurve));
      else
      {
        dtIdx = ~dtIdx;
        path.EvolveForwardCurve(curveId, dt, dtIdx, GetNative(forwardCurve));
      }
    }

    #endregion

    #region Wrapper methods

    /// <summary>
    ///    Evolve a discount curve to a future date (not belonging to the simulation dates) conditional on the state at neighboring simulation dates 
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">The id of the curve to get</param>
    /// <param name="dt">Date.</param>
    /// <param name="dateIndex">Index of the forward date immediately after dt.</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="fxRate">Spot Fx rate at the forward date between denomination currency of DiscountCurve[0] and DiscountCurve[curveId]</param>
    /// <param name="numeraire">Realization of discounted numeraire asset at dateIdx</param>
    /// <remarks>This function assumes that discountCurve tenors are those inputed to the simulator</remarks>
    public static void EvolveDiscountCurve(
      this SimulatedPath path,
      int curveId, double dt, int dateIndex,
      DiscountCurve discountCurve, ref double fxRate, ref double numeraire)
    {
      path.EvolveDiscountCurve(curveId, dt, dateIndex, GetNative(discountCurve),
        ref fxRate, ref numeraire);
    }

    /// <summary>
    ///   Evolve a discount curve to a future date.
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">The id of the curve to get</param>
    /// <param name="dateIndex">Index of the forward date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="fxRate">Spot Fx rate at the forward date between denomination currency of DiscountCurve[0] and DiscountCurve[curveId]</param>
    /// <param name="numeraire">Realization of discounted numeraire asset at dateIdx</param>
    /// <remarks>This function assumes that discountCurve tenors are those inputed to the simulator</remarks>
    public static void EvolveDiscountCurve(
      this SimulatedPath path,
      int curveId, int dateIndex,
      DiscountCurve discountCurve, ref double fxRate, ref double numeraire)
    {
      path.EvolveDiscountCurve(curveId, dateIndex, GetNative(discountCurve),
        ref fxRate, ref numeraire);
    }

    /// <summary>
    ///   Evolve a survival curve to a future date (not belonging to the simulation dates) conditional on the state at neighboring simulation dates 
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">The id of the curve to get.</param>
    /// <param name="dateIndex">Index of the forward date immediately after dt.</param>
    /// <param name="creditCurve">Survival curve</param>
    /// <remarks>This function assumes that creditCurve tenors are those inputed to the simulator</remarks>
    public static void EvolveCreditCurve(
      this SimulatedPath path,
      int curveId, int dateIndex, SurvivalCurve creditCurve)
    {
      path.EvolveCreditCurve(curveId, dateIndex, GetNative(creditCurve));
    }

    /// <summary>
    ///   Evolve a forward curve to a future date. 
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">The id of the curve to get.</param>
    /// <param name="dateIndex">Index of the forward date.</param>
    /// <param name="forwardCurve">Forward price term structure</param>
    /// <remarks>This function assumes that forward curve tenors are those inputed to the simulator</remarks>
    public static void EvolveForwardCurve(
      this SimulatedPath path,
      int curveId, int dateIndex, CalibratedCurve forwardCurve)
    {
      path.EvolveForwardCurve(curveId, dateIndex, GetNative(forwardCurve));
    }

    /// <summary>
    ///    Evolve a discount curve to a set of future dates (not belonging to the simulation dates) conditional on the state at neighboring simulation dates 
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="curveId">The id of the curve to get</param>
    /// <param name="dts">Dates.</param>
    /// <param name="fractions">Dates in double form (Dt.FractDiff).</param>
    /// <param name="dateIndexes">Index of the forward date immediately after dt.</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="values">the array to fill with results</param>
    /// <remarks>This function assumes that discountCurve tenors are those inputed to the simulator</remarks>
    public static void EvolveInterpolatedDiscounts(
      this SimulatedPath path,
      int curveId, Dt[] dts, double[] fractions,
      int[] dateIndexes, DiscountCurve discountCurve, IntPtr values)
    {
      path.EvolveInterpolatedDiscounts(curveId, dts, fractions,
        dateIndexes, GetNative(discountCurve), values);
    }

    #endregion
  }
}