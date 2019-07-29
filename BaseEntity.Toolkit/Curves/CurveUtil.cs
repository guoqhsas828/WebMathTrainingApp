/*
 * CurveUtil.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util.Collections;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Curves
{

  /// <summary>
  ///   Curve utility methods.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Collection of utility classes for curves. This is separated from the
  ///   Curve class for simplification as these are specialized functions for more
  ///   advanced and less common use.</para>
  /// </remarks>
  ///
  public static partial class CurveUtil
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CurveUtil));

    #region Enumerate Prerequisite Curves

    public static IEnumerable<CalibratedCurve> EnumeratePrerequisiteCurves(
      this CalibratedCurve curve)
    {
      return curve?.EnumerateComponentCurves()
        ?? Enumerable.Empty<CalibratedCurve>();
    }

    #endregion

    #region Build flat curve, relative time based

    /// <summary>
    /// Flatten the curve with the specified rate based on relative time.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="curve">The curve.</param>
    /// <param name="rate">The rate.</param>
    /// <returns>``0.</returns>
    public static T SetRelativeTimeRate<T>(this T curve, double rate)
      where T : Curve
    {
      if (curve != null)
      {
        curve.Clear();
        curve.Flags |= CurveFlags.SmoothTime;
        Dt asOf = curve.AsOf;
        Dt date = asOf + (RelativeTime)1.0;
        curve.Add(date, RateCalc.PriceFromRate(rate, asOf, date));
      }
      return curve;
    }

    #endregion

    #region Access methods
    /// <summary>
    ///  View the curve as a list of curves dates.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>A list curve dates.</returns>
    public static IList<Dt> AsDtList(this Curve curve)
    {
      return ListUtil.CreateList(curve.Count, (i) => curve.GetDt(i));
    }
    /// <summary>
    ///  View the curve as a list of values by curve points.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>A list curve values.</returns>
    public static IList<double> AsValueList(this Curve curve)
    {
      return ListUtil.CreateList(curve.Count, (i) => curve.GetVal(i));
    }
    /// <summary>
    ///  View the curve tenors as a list of tenor names.
    /// </summary>
    /// <param name="tenors">The tenors.</param>
    /// <returns>A list of tenor names</returns>
    public static IList<string> AsNameList(this CurveTenorCollection tenors)
    {
      return ListUtil.CreateList(tenors.Count, (i) => tenors[i].Product.Description);
    }
    #endregion Access methods

    #region Other methods

    // Configuration
    private const int ParallelStart = 4;

    /// <summary>
    ///   Bumps specified tenor of a curve and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnit"/></description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="curve">Curve to bump</param>
    /// <param name="tenor">Tenor to bump or null for all tenors</param>
    /// <param name="bumpUnit">Bump unit for uniform shift (Eg. CDS = 1bp, Bonds = $1)</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    /// <param name="refit">True if refit of curve required</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <exception cref="Exception">If specified tenor not in Curve</exception>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping an individual tenor of a <see cref="CalibratedCurve"/>.</para>
    /// <code language="C#">
    ///   // Have calibrated survival curve with tenor points including a "5 Year" point.
    ///   SurvivalCurve survivalCurve;
    ///
    ///   // ...
    ///
    ///   // Bump the 5Yr tenor point up 10bp and refit the curve.
    ///   Curveutil.CurveBump( survivalCurve, "5 Year", 10, true, false, true );
    ///
    ///   // Bump the 5Yr tenor point back down by 10bp without refitting
    ///   Curveutil.CurveBump( survivalCurve, "5 Year", 10, false, false, false );
    /// </code>
    /// </example>
    ///
    public static double[]
    CurveBump(CalibratedCurve curve, string tenor, double bumpUnit, bool up, bool bumpRelative, bool refit)
    {
      return CurveBump(new[] { curve }, (tenor != null) ? new[] { tenor } : null, new[] { bumpUnit }, up, bumpRelative, refit, null);
    }

    /// <summary>
    /// Compute the Discount forward swap rate
    /// </summary>
    /// <param name="discountCurve"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="dayCount"></param>
    /// <param name="freq"></param>
    /// <param name="roll"></param>
    /// <param name="cal"></param>
    /// <returns></returns>
    public static double DiscountForwardSwapRate(DiscountCurve discountCurve, Dt effective, Dt maturity, DayCount dayCount, Frequency freq,
      BDConvention roll, Calendar cal)
    {
      var sched = new Schedule(effective, effective, Dt.Empty, maturity, freq, roll, cal);
      double den = 0;
      if (sched.Count == 0)
        return 0.0;
      for (int i = 0; i < sched.Count; i++)
        den += discountCurve.Interpolate(sched.GetPaymentDate(i))*sched.Fraction(i, dayCount);
      if (den.ApproximatelyEqualsTo(0.0))
        return 0.0;
      var swapRate = (discountCurve.Interpolate(effective) - discountCurve.Interpolate(maturity))/den;
      return swapRate;
    }

    /// <summary>
    ///   Bumps all tenors of a list of curves together and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnit"/></description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="curves">Array of curves to bump</param>
    /// <param name="bumpUnit">Bump unit for uniform shift (Eg. CDS = 1bp, Bonds = $1)</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    /// <param name="refit">True if refit of curve required</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping all tenors of an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Bump all curves up by a parallel shift of 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, 10, true, false, true );
    ///
    ///   // Bump all curves down by a parallel shift of 10bp without refitting.
    ///   Curveutil.CurveBump( survivalCurves, 10, false, false, false );
    /// </code>
    /// </example>
    ///
    public static double[]
    CurveBump(CalibratedCurve[] curves, double bumpUnit, bool up, bool bumpRelative, bool refit)
    {
      return CurveBump(curves, null, new double[] { bumpUnit }, up, bumpRelative, refit, null);
    }

    /// <summary>
    ///   Bumps specified tenors of a list of curves together and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnit"/></description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="curves">Array of curves to bump</param>
    /// <param name="tenor">Tenor to bump or null for all</param>
    /// <param name="bumpUnit">Bump unit for uniform shift (Eg. CDS = 1bp, Bonds = $1)</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    /// <param name="refit">True if refit of curve required</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <exception cref="Exception">If specified tenor not in Curve</exception>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping an individual tenor of an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves with tenor points including a "5 Year" point.
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Bump the 5 Year tenors for all curves up by 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, "5 Year", 10, true, false, true );
    ///
    ///   // Bump the 5 Year tenors for all curves down by 10bp without refitting.
    ///   Curveutil.CurveBump( survivalCurves, "5 Year", 10, false, false, false );
    /// </code>
    /// </example>
    ///
    public static double[]
    CurveBump(CalibratedCurve[] curves, string tenor, double bumpUnit, bool up, bool bumpRelative, bool refit)
    {
      return CurveBump(curves, (tenor != null) ? new string[] { tenor } : null, new double[] { bumpUnit }, up, bumpRelative, refit, null);
    }

    /// <summary>
    ///   Bumps specified tenors of a list of curves together and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnit"/></description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="curves">Array of curves to bump</param>
    /// <param name="tenors">Array of tenors to bump or null for all</param>
    /// <param name="bumpUnit">Bump unit for uniform shift (Eg. CDS = 1bp, Bonds = $1)</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    /// <param name="refit">True if refit of curve required</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <exception cref="Exception">If specified tenor not in Curve</exception>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping an array of tenors of an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves with tenor points including "1 Year" and "5 Year" points.
    ///   SurvivalCurve [] survivalCurves;
    ///   string [] tenors = new string [] { "1 Year", "5 Year" };
    ///
    ///   // ...
    ///
    ///   // Bump the 1 and 5 Year tenors for all curves up by 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, tenors, 10, true, false, true );
    ///
    ///   // Bump the 1 and 5 Year tenors for all curves down by 10bp without refitting.
    ///   Curveutil.CurveBump( survivalCurves, tenors, 10, false, false, false );
    /// </code>
    /// </example>
    ///
    public static double[]
    CurveBump(CalibratedCurve[] curves, string[] tenors, double bumpUnit, bool up, bool bumpRelative, bool refit)
    {
      return CurveBump(curves, tenors, new double[] { bumpUnit }, up, bumpRelative, refit, null);
    }

    /// <summary>
    ///   Bumps specified tenors of a list of curves together and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnits"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnits"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnits"/></description></item>
    ///   </list>
    ///
    ///   <para>If weights are specified, each curve is bumped by the bumpUnit times the weight for that
    ///   curve. If Weights is null, a weight of 1 is used.</para>
    /// </remarks>
    ///
    /// <param name="curves">Array of curves to bump</param>
    /// <param name="tenors">Array of tenors to bump or null for all</param>
    /// <param name="bumpUnits">Array of bump units (Eg. CDS = 1bp, Bonds = $1) for each tenor or one single bump unit for all</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    /// <param name="refit">True if refit of curve required</param>
    /// <param name="weights">Array of weights for each curve</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <exception cref="Exception">If specified tenor not in Curve</exception>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping an array of tenors by an array of bumps of an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves with tenor points including "1 Year" and "5 Year" points.
    ///   SurvivalCurve [] survivalCurves;
    ///   string [] tenors = new string [] { "1 Year", "5 Year" };
    ///   double [] bumps = new double[] { 5, 10 };
    ///
    ///   // ...
    ///
    ///   // Bump the 1 and 5 Year tenors for all curves up by 5bp and 10bp respectively and refit.
    ///   Curveutil.CurveBump( survivalCurves, tenors, bumps, true, false, true, null );
    ///
    ///   // Bump the 1 and 5 Year tenors for all curves up by 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, tenors, new double [] {10}, true, false, true, null );
    ///
    ///   // Bump all tenors for all curves up by 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, null, new double [] {10}, true, false, true, null );
    /// </code>
    /// </example>
    ///
    public static double[]
    CurveBump(CalibratedCurve[] curves, string[] tenors, double[] bumpUnits, bool up, bool bumpRelative,
               bool refit, double[] weights)
    {
      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      if (!up) flags |= BumpFlags.BumpDown;

      return CurveBump(curves, tenors, bumpUnits, flags, refit, weights);
    }

    /// <summary>
    /// Bumps specified tenors of a list of curves together and refits.
    /// </summary>
    /// <param name="curves">Array of curves to bump</param>
    /// <param name="tenors">Array of tenors to bump or null for all</param>
    /// <param name="bumpUnits">Array of bump units (Eg. CDS = 1bp, Bonds = $1) for each tenor or one single bump unit for all</param>
    /// <param name="bumpFlags">Bump flags, such as BumpRelative, BumpDown, AllowDownCrossingZero and so on</param>
    /// <param name="refit">True if refit of curve required</param>
    /// <param name="weights">Array of weights for each curve</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <exception cref="Exception">If specified tenor not in Curve</exception>
    public static double[]
      CurveBump(CalibratedCurve[] curves, string[] tenors, double[] bumpUnits, BumpFlags bumpFlags,
        bool refit, double[] weights)
    {
      // Validate
      if (curves == null || curves.Length == 0)
        throw new ArgumentException("No curves specified to bump");
      if (bumpUnits == null || bumpUnits.Length == 0)
        throw new ArgumentException("No bump units specified");
      if ((tenors == null || tenors.Length == 0) && bumpUnits.Length != 1)
        throw new ArgumentException("Multiple bumps have " +
                                    "been specified for curve bumping " +
                                    "when all tenors are to be bumped");
      if (tenors != null && bumpUnits.Length != 1 && tenors.Length != bumpUnits.Length)
        throw new ArgumentException("If specific tenors are to be bumped," +
                                    " one bump must be specified or the number " +
                                    "of tenors must match the number of bump");
      if (weights != null && weights.Length > 0 && weights.Length != curves.Length)
        throw new ArgumentException("Number of weights must match number of curves");

      Timer timer = new Timer();
      timer.start();

      var bumpRelative = (bumpFlags & BumpFlags.BumpRelative) != 0;

      logger.DebugFormat("Bumping set of tenors ({0}...) for curves {1}... by {2} {3}",
        (tenors != null && tenors.Length > 0) ? tenors[0] : "all",
        curves[0].Name, bumpRelative ? "factor" : "spread", bumpUnits[0]);

      double[] avgBumps = new double[curves.Length];
      if (refit && curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.For(0, curves.Length, delegate(int j)
        {
          avgBumps[j] = BumpSingleCurve(curves[j], tenors, bumpUnits, bumpFlags,
            refit, (weights == null || weights.Length == 0) ? 1.0 : weights[j], j);
        });
      }
      else
      {
        for (int j = 0; j < curves.Length; j++)
          avgBumps[j] = BumpSingleCurve(curves[j], tenors, bumpUnits, bumpFlags,
            refit, (weights == null || weights.Length == 0) ? 1.0 : weights[j], j);
      }

      timer.stop();
      logger.InfoFormat("Completed bump in {0}s", timer.getElapsed());

      return avgBumps;
    }



    private static double BumpSingleCurve(CalibratedCurve curve,
      string[] tenors, double[] bumpUnits, BumpFlags flags,
      bool refit, double weight, int j)
    {
      if (!curve.JumpDate.IsEmpty())
        return Double.NaN;

      double avgBump = 0;
      int minTenor = 100;   // First tenor bumped

      if (tenors == null || tenors.Length == 0)
      {
        // Bump all tenor point(s)
        minTenor = 0;
        int count = 0;
        foreach (CurveTenor t in curve.Tenors)
        {
          double a = t.BumpQuote(bumpUnits[0] * weight, flags);
          avgBump += (a - avgBump) / (++count);
        }
        if (count == 0)
          avgBump = double.NaN;
      }
      else
      {
        int count = 0;
        for (int i = 0; i < tenors.Length; i++)
        {
          int idx = curve.Tenors.Index(tenors[i]);
          if (idx >= 0)
          {
            if (idx < minTenor)
              minTenor = idx;
            double a = curve.Tenors[idx].BumpQuote(
              ((bumpUnits.Length > 1) ? bumpUnits[i] : bumpUnits[0]) * weight,
              flags);
            avgBump += (a - avgBump) / (++count);
          }
        }
        if (count == 0)
          avgBump = double.NaN;
      }
      
      // Refit curve
      if (refit)
      {
        try
        {
          curve.ReFit(minTenor);
        }
        catch (SurvivalFitException ex)
        {
          throw new CurveBumpException(String.Format(
            "Failed to fit tenor {0} bumping curve index {1}: {2}",
            ex.Tenor.Name, j, ex.Message), j, ex.CurveName, ex.Tenor, ex);
        }
      }
      return avgBump;
    }

    



    /// <summary>
    ///   Bumps specified tenors of a list of curves together and refits.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnit"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnit"/></description></item>
    ///   </list>
    ///
    ///   <para>If weights are specified, each curve is bumped by the bumpUnit times the weight for that
    ///   curve. If Weights is null, a weight of 1 is used.</para>
    /// </remarks>
    ///
    /// <param name="curves">Array of curves to bump</param>
    /// <param name="tenorIndex">Index of the tenor to bump</param>
    /// <param name="bumpUnit">Array of bump units (Eg. CDS = 1bp, Bonds = $1) for each tenor or one single bump unit for all</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    /// <param name="refit">True if refit of curve required</param>
    /// <param name="weights">Array of weights for each curve</param>
    ///
    /// <returns>An array of the average bump size for each curve in bump units</returns>
    ///
    /// <exception cref="Exception">If specified tenor not in Curve</exception>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping an array of tenors by an array of bumps of an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves with tenor points including "1 Year" and "5 Year" points.
    ///   SurvivalCurve [] survivalCurves;
    ///   string [] tenors = new string [] { "1 Year", "5 Year" };
    ///   double [] bumps = new double[] { 5, 10 };
    ///
    ///   // ...
    ///
    ///   // Bump the 1 and 5 Year tenors for all curves up by 5bp and 10bp respectively and refit.
    ///   Curveutil.CurveBump( survivalCurves, tenors, bumps, true, false, true, null );
    ///
    ///   // Bump the 1 and 5 Year tenors for all curves up by 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, tenors, new double [] {10}, true, false, true, null );
    ///
    ///   // Bump all tenors for all curves up by 10bp and refit.
    ///   Curveutil.CurveBump( survivalCurves, null, new double [] {10}, true, false, true, null );
    /// </code>
    /// </example>
    ///
    public static double[]
		CurveBump( CalibratedCurve [] curves, int tenorIndex, double bumpUnit, bool up, bool bumpRelative,
            bool refit, double [] weights )
		{
			// Validate
			if( curves == null || curves.Length == 0 )
				throw new ArgumentException( "No curves specified to bump" );
			if( tenorIndex < 0 )
				throw new ArgumentException( "Negative tenor index is not allowed" );
			if( weights != null && weights.Length > 0 && weights.Length != curves.Length )
				throw new ArgumentException( "Number of weights must match number of curves" );

			Timer timer = new Timer();
			timer.start();

			logger.DebugFormat( "Bumping set of tenors ({0}...) for curves {1}... by {2} {3}",
                tenorIndex, curves[0].Name, bumpRelative ? "factor" : "spread", bumpUnit );

      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      if (!up) flags |= BumpFlags.BumpDown;
      double[] avgBumps = new double[curves.Length];
      if (refit && curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.For(0, curves.Length, delegate(int j)
        {
          double bumpSize = (weights == null || weights.Length == 0)
            ? bumpUnit : weights[j] * bumpUnit;
          avgBumps[j] = BumpSingleCurve(curves[j], tenorIndex, bumpSize,
            flags, refit, j);
        });
      }
      else
      {
        for (int j = 0; j < curves.Length; j++)
        {
          double bumpSize = (weights == null || weights.Length == 0)
            ? bumpUnit : weights[j] * bumpUnit;
          avgBumps[j] = BumpSingleCurve(curves[j], tenorIndex, bumpSize,
            flags, refit, j);
        }
      }
			timer.stop();
			logger.InfoFormat( "Completed bump in {0}s", timer.getElapsed() );

			return avgBumps;
		}

    private static double BumpSingleCurve(CalibratedCurve curve, int tenorIndex,
      double bumpUnit, BumpFlags flags, bool refit, int j)
    {
      double avgBump = 0;
      if (tenorIndex < curve.Tenors.Count)
      {
        avgBump = curve.Tenors[tenorIndex].BumpQuote(bumpUnit, flags);
      }

      // Refit curve
      if (refit)
      {
        try
        {
          curve.ReFit(tenorIndex);
        }
        catch (SurvivalFitException ex)
        {
          throw new CurveBumpException(String.Format(
            "Failed to fit tenor {0} bumping curve at index {1}: {2}",
            ex.Tenor.Name, j, ex.Message), j, ex.CurveName, ex.Tenor, ex);
        }
      }
      return avgBump;
    }

    /// <summary>
    ///   Bump an individual tenor point of a curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The bump units depends on the type of products in the curves being bumped.
    ///   For CDS or Swaps, the bump units are basis points. For Bonds the bump units are dollars.</para>
    ///
    ///   <para>Note that bumps are designed to be symmetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>Bumping of the tenors is performed based on the following alternatives:</para>
    ///   <list type="bullet">
    ///     <item><description>If bump is relative and +ve, tenor quote is
    ///       multiplied by (1+<paramref name="bumpUnits"/>)</description></item>
    ///     <item><description>else if bump is relative and -ve, tenor quote
    ///       is divided by (1+<paramref name="bumpUnits"/>)</description></item>
    ///     <item><description>else bumps tenor quote by <paramref name="bumpUnits"/></description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="tenor">Curve tenor to bump</param>
    /// <param name="bumpUnits">Number of bump units (Eg. CDS = 1bp)</param>
    /// <param name="up">True if bumping up, otherwise bumping down</param>
    /// <param name="bumpRelative">Bump sizes are relative rather than absolute</param>
    ///
    /// <returns>The actual bump in bump units</returns>
    ///
    /// <exception cref="Exception">If specified index not in Calibration</exception>
    ///
    /// <example>
    /// <para>The following sample demonstrates bumping an individual <see cref="CurveTenor"/>.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Bump all tenors up by 10bp.
    ///   foreach( CurveTenor t in survivalCurves.Tenors )
    ///   {
    ///     CurveUtil.BumpTenor( t, 10, true, false );
    ///   }
    ///
    ///   // Refit curve
    ///   survivalCurve.Fit();
    /// </code>
    /// </example>
    ///
    public static double
    BumpTenor(CurveTenor tenor, double bumpUnits, bool up, bool bumpRelative)
    {
      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      if (!up) flags |= BumpFlags.BumpDown;
      return tenor.BumpQuote(bumpUnits, flags);
    }
    
    /// <summary>
    ///   Calculate the hedge sensitivity of an array of curves
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the hedge sensitivities for an array of curves relative to
    ///   a particular tenor of each curve.</para>
    ///
    ///   <para>Computes the difference in pricing the specified hedge tenor instrument
    ///   using the original curve set <paramref name="curves"/> and the bumped curve set
    ///   <paramref name="bumpedCurves"/></para>
    ///
    ///   <para>Results are returned as a percentage of face.</para>
    /// </remarks>
    ///
    /// <param name="curves">Original curves</param>
    /// <param name="bumpedCurves">Bumped curves</param>
    /// <param name="hedgeName">Name of tenor for hedge</param>
    ///
    /// <returns>Array of hedge deltas</returns>
    ///
    public static double[]
    CurveHedge(
               CalibratedCurve[] curves,
               CalibratedCurve[] bumpedCurves,
               string hedgeName
               )
    {
      Timer timer = new Timer();
      timer.start();

      // Calculate hedge sensitivity
      double[] hedge = new double[curves.Length];
      if (curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.For(0, curves.Length, delegate(int i)
        {
          hedge[i] = SingleCurveHedge(curves[i], bumpedCurves[i], hedgeName);
        });
      }
      else
      {
        for (int i = 0; i < curves.Length; i++)
        {
          if (CurveHasTenor(curves[i] , hedgeName ))
          {
            hedge[i] = SingleCurveHedge(curves[i], bumpedCurves[i], hedgeName);
          }
        }
      }

      timer.stop();
      logger.DebugFormat("Calculated Hedge in {0}s", timer.getElapsed());

      return hedge;
    }

    private static double SingleCurveHedge(CalibratedCurve curve,
      CalibratedCurve bumpedCurve, string hedgeName)
    {
      int hedgeIndex = (hedgeName != null) ? curve.Tenors.Index(hedgeName) : -1;
      if (hedgeIndex >= 0)
      {
        // Reprice tenor of curve i using data from bumped curve
        CurveTenor tenor = curve.Tenors[hedgeIndex];
#if USE_ORIG_PV
					double origPv = tenor.ModelPv;
#else
        double origPv = curve.Pv(tenor.Product);
#endif
        double bumpedPv = bumpedCurve.PvAt(hedgeIndex, hedgeName, tenor.Product);
        double hedge = bumpedPv - origPv;
        logger.DebugFormat("Curve {0} tenor {1} hedge delta bump={2} - base={3} = {4}",
          curve.Name, tenor.Name, hedge + origPv, origPv, hedge);
        return hedge;
      }
      else if (curve.Tenors.Count != 0) // throw exception only when tenors is not empty
        throw new ToolkitException(String.Format("Hedge tenor {0} missing form curve {1}",
          hedgeName, bumpedCurve.Name));
      return Double.NaN;
    }

    ///<summary>
    /// Construct a zero-rate based instruments
    ///</summary>
    ///<param name="sourceCurve">Original calibrated curve</param>
    ///<param name="tenorNames">Bump tenor names</param>
    ///<param name="maturities">List of maturities for deriving zero rates</param>
    ///<param name="dc">Zero-rate daycount</param>
    ///<param name="roll">Zero-rate roll</param>
    ///<param name="cal">Zero-rate calendar</param>
    ///<param name="freq">Zero-rate compounding frequency</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    ///<returns>Curve constructed with zero-rate based instruments</returns>
    public static CalibratedCurve ConstructZeroCurve(CalibratedCurve sourceCurve, string[] tenorNames, Dt[] maturities, 
      DayCount dc, BDConvention roll, Calendar cal, Frequency freq, Interp interp)
    {
      var overlay = new DiscountCurve(sourceCurve.GetCurveDate(sourceCurve.AsOf))
                      {
                        Category = sourceCurve.Category,
                        Ccy = sourceCurve.Ccy,
                        Name = sourceCurve.Name + "_Zero",
                        DayCount = DayCount.Actual365Fixed,
                        Frequency = Frequency.Continuous,
                        Interp = interp ?? InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const)
                      };

      var inverse = OverlayCalibrator.IsInverse(sourceCurve);

      // Always lag as of date
      Dt curveDateAsOf = sourceCurve.GetCurveDate(overlay.AsOf);
      for (int i = 0; i < maturities.Length; ++i)
      {
        Dt curveDate = sourceCurve.GetCurveDate(maturities[i]);
        var df = sourceCurve.Interpolate(curveDate);
        var zeroRate = RateCalc.RateFromPrice(inverse ? (1/df) : df, curveDateAsOf, curveDate, dc, freq);

        // Add safety for inflation curves
        overlay.AddMoneyMarket(tenorNames[i], 1.0, curveDate != curveDateAsOf ? curveDateAsOf : curveDateAsOf - 1, curveDate, zeroRate, dc,
              freq, roll, cal);
      }
      overlay.Calibrator = new OverlayCalibrator(sourceCurve.AsOf, sourceCurve, overlay);
      overlay.Fit();
      return overlay;
    }

    #region A Hacker for Swap with multi curves

    // With multiple curves, the current implementation of tenor Pv function
    // depends on the reference eqality of the reference index inside the product
    // and the one on some dependent curves.  This works fine before the cashflow
    // integration through our special implementaion of the object clone method,
    // which makes sure that the reference index is always copied by references.
    // It no longer works after the integration as the bumped curves cloned by
    // in process serialization, bypassing the class specific cloning mechanism.
    // Here is a temporary quick hacker to restore the required reference equality.
    //TODO: this is TEMPORARY.  We need a better solution.

    private static double PvAt(this CalibratedCurve curve,
      int index, string name, IProduct product)
    {
      var bond = product as Bond;
      if (bond !=null)
      {
        var bondPricer = (BondPricer)(curve.Calibrator.GetPricer(curve, product));
        var qh = curve.Tenors[index].QuoteHandler.GetCurrentQuote(curve.Tenors[index]);
        bondPricer.MarketQuote = qh.Value;
        bondPricer.QuotingConvention = qh.Type;
        using (new BondPricer.ZSpreadAdjustment(bondPricer))
        {
          return bondPricer.Pv();
        }
      }
      var swp = product as Swap;
      if (swp == null || swp.NotMulti()) return curve.Pv(product);
      var swp1 = curve.TenorProductAt(index, name) as Swap;
      if (swp1 == null || swp1.NotMulti()) return curve.Pv(product);
      double rlegCpn = swp1.ReceiverLeg.Coupon, plegCpn = swp1.PayerLeg.Coupon;
      try
      {
        swp1.ReceiverLeg.Coupon = swp.ReceiverLeg.Coupon;
        swp1.PayerLeg.Coupon = swp.PayerLeg.Coupon;
        return curve.Pv(swp1);
      }
      finally
      {
        swp1.ReceiverLeg.Coupon = rlegCpn;
        swp1.PayerLeg.Coupon = plegCpn;
      }
    }

    private static bool NotMulti(this Swap swp)
    {
      return swp.PayerLeg.ReferenceIndex == null
        || swp.ReceiverLeg.ReferenceIndex == null
        || swp.PayerLeg.ReferenceIndex.IndexName == swp.ReceiverLeg.ReferenceIndex.IndexName;
    }

    private static IProduct TenorProductAt(this CalibratedCurve curve, int index, string name)
    {
      var tenors = curve.Tenors;
      if (tenors != null && tenors.Count > index && tenors[index].Name == name)
        return tenors[index].Product;
      return null;
    }

    #endregion

    /// <summary>
    ///   Calculate hedge at maturity for each curve
    /// </summary>
    /// <param name="curves">An array of calibrated curve</param>
    /// <param name="bumpedCurves">An array of bumped calibrated curve</param>
    /// <param name="maturity">Single maturity date</param>
    /// <returns>hedge at maturity for each curve</returns>
    public static double[]
    CurveHedge(
               CalibratedCurve[] curves,
               CalibratedCurve[] bumpedCurves,
               Dt maturity
               )
    {
      if (curves == null || curves.Length == 0)
        throw new ToolkitException("Must have at least one calibrated curve");
      if (bumpedCurves == null || bumpedCurves.Length == 0)
        throw new ToolkitException("Must have at least one bumped calibrated curve");
      if (curves != null && bumpedCurves != null && curves.Length != bumpedCurves.Length)
        throw new ToolkitException("Number of calibrated curves must equal number of bumped calibrated curves");
      if (maturity.IsEmpty() || !maturity.IsValid())
        throw new ToolkitException("The maturity is not valid.");
      
      Dt asOf = curves[0].AsOf;
      double[] hedge = ArrayUtil.NewArray(curves.Length, 0.0);
      if (maturity <= asOf)
        return hedge;

      Timer timer = new Timer();
      timer.start();

      if (curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.For(0, curves.Length, delegate(int i)
        {
          hedge[i] = SingleCurveHedge(curves[i], bumpedCurves[i], maturity);
        });
      }
      else
      {
        for (int i = 0; i < curves.Length; ++i)
        {
          hedge[i] = SingleCurveHedge(curves[i], bumpedCurves[i], maturity);
        }
      }
      timer.stop();
      logger.DebugFormat("Calculated Hedge in {0}s", timer.getElapsed());

      return hedge;
    }

    private static double SingleCurveHedge(
      CalibratedCurve curve, CalibratedCurve bumpedCurve, Dt hedgeMaturity)
    {
      // Get the curve tenor closest to the maturity
      CurveTenor curveTenor = FindClosestTenor(new CalibratedCurve[] { curve }, hedgeMaturity);

      // Some curves might be defaulted such that 
      // curveTenor = null, so return Double.NaN
      double hedge = 0;
      if (curveTenor == null)
      {
        logger.DebugFormat("Curve {0} is not valid around maturity date {1}",
          curve.Name, hedgeMaturity);
      }
      else if (curveTenor.Product is CDS)
      {
        // We only support CDS at this moment.
        CDS product = (CDS)curveTenor.Product.Clone();
        if (product.Maturity != hedgeMaturity)
        {
          product.Maturity = hedgeMaturity;
          if (product.FirstPrem > hedgeMaturity)
            product.FirstPrem = hedgeMaturity;
          // if it is a std contract use the fixed coupon from the nearest tenor
          // if not, use the implied par spread at maturity date. 
          if (!product.IsStandardCDSContract && curve is SurvivalCurve)
          {
            product.Premium = ImpliedSpread((SurvivalCurve) curve, hedgeMaturity);
            product.Fee = 0.0;
          }
        }
        double origPv = curve.Pv(product);
        double bumpedPv = bumpedCurve.Pv(product);
        hedge = bumpedPv - origPv;
        logger.DebugFormat("Curve {0} at maturity {1} hedge delta bump={2} - base={3} = {4}",
          curve.Name, product.Maturity, bumpedPv, origPv, hedge);
      }
      else
      {
        logger.DebugFormat("Curve {0}, product type {1} not support arbitrary hedge maturity",
          curve.Name, curveTenor.Product.GetType().Name);
      }
      return hedge;
    }

    /// <summary>
    /// Bumps the recovery and optionally recalibrate the curves.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="recalibrate">if set to <c>true</c>, recalibrate the curves.</param>
    public static void BumpRecovery(this Curve[] curves, double bumpSize, bool recalibrate)
    {
      if (recalibrate && curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.For(0, curves.Length, (i) => Bump(curves[i], bumpSize, recalibrate));
      }
      else
      {
        for (int i = 0; i < curves.Length; ++i)
          Bump(curves[i], bumpSize, recalibrate);
      }
    }

    /// <summary>
    /// Bumps the recovery and optionally recalibrate the curves.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <param name="bumpSizes">Sizes of the bump.</param>
    /// <param name="recalibrate">if set to <c>true</c>, recalibrate the curves.</param>
    public static void BumpRecovery(this Curve[] curves, IList<double> bumpSizes, bool recalibrate)
    {
      if (bumpSizes.Count == 1)
      {
        curves.BumpRecovery(bumpSizes[0], recalibrate);
        return;
      }
      else
      {
        if (curves.Length != bumpSizes.Count)
          throw new ArgumentException("Curves and bumpSizes need to match in lengh for non-uniform bump sizes");
      }

      if (recalibrate && curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.For(0, curves.Length, (i) => Bump(curves[i], bumpSizes[i], recalibrate));
      }
      else
      {
        for (int i = 0; i < curves.Length; ++i)
          Bump(curves[i], bumpSizes[i], recalibrate);
      }
    }

    private static void Bump(Curve curve, double bump, bool recalibrate)
    {
      // Bump recovery up, leave defaulted and settled curves untouched.
      // If a curve does not need to bump, it does not need to fit.
      RecoveryCurve rc = curve as RecoveryCurve;
      if (rc != null)
      {
        if (rc.Recovered != Recovered.WillRecover
          && !rc.DefaultSettlementDate.IsEmpty())
        {
          return;
        }
        rc.Spread += bump;
        return;
      }

      // If we are here, the curve is not a recovery curve.
      // Try survival curve.
      SurvivalCurve sc = curve as SurvivalCurve;
      if (sc == null)
      {
        throw new RecoveryBumpException(String.Format(
          "Curve {0} is not a SurvivalCurve nor RecoveryCurve", curve.Name));
      }
      BaseEntity.Toolkit.Base.Dt dfltDate = sc.DefaultDate;
      BaseEntity.Toolkit.Base.Dt dfltSettleDate = sc.SurvivalCalibrator.RecoveryCurve.JumpDate;

      // If either default date is null or default settlement date is null
      // we should bump the recovery rate; If not, the settlement is given
      // we cannot bump the recovery any more because it's fixed by market
      if (dfltDate.IsEmpty() || dfltSettleDate.IsEmpty() ||
        sc.SurvivalCalibrator.RecoveryCurve.Recovered == Recovered.WillRecover)
      {
        sc.SurvivalCalibrator.RecoveryCurve.Spread += bump;
        if (recalibrate)
          sc.Fit();
      }
      return;
    }

    /// <summary>
    ///   Exception thrown when recovery bump fails.
    /// </summary>
    [Serializable]
    public class RecoveryBumpException : ToolkitException
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="RecoveryBumpException"/> class.
      /// </summary>
      /// <param name="message">Exception message.</param>
      public RecoveryBumpException(string message) : base(message) { }
    }


    /// <summary>
    ///   Clone an array of curves
    /// </summary>
    ///
    /// <param name="curves">Array of curves to clone (some may be null)</param>
    ///
    /// <returns>Array of cloned curves</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates cloning an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Create a set of cloned survival curves
    ///   SurvivalCurve [] savedCurves = (SurvivalCurve [])CurveUtil.CurveCloneWithRecovery(survivalCurves);
    /// </code>
    /// </example>
    ///
    internal static CalibratedCurve[] CurveCloneWithRecovery(CalibratedCurve[] curves)
    {
      Timer timer = new Timer();
      timer.start();

      CalibratedCurve[] clonedCurves = new CalibratedCurve[curves.Length];
      for (int i = 0; i < curves.Length; i++)
        if (curves[i] != null)
        {
          clonedCurves[i] = (CalibratedCurve)curves[i].Clone();
          if (curves[i].Calibrator != null)
          {
            clonedCurves[i].Calibrator = (Calibrator)curves[i].Calibrator.ShallowCopy();
            SurvivalCalibrator cal = clonedCurves[i].Calibrator as SurvivalCalibrator;
            if (cal != null)
              cal.RecoveryCurve = (RecoveryCurve)cal.RecoveryCurve.Clone();

          }
        }
        else
          clonedCurves[i] = null;

      timer.stop();
      logger.DebugFormat("Cloned curves in {0}s", timer.getElapsed());

      return clonedCurves;
    }

    internal static Curve[] CurveCloneWithRecovery(Curve[] curves)
    {
      Timer timer = new Timer();
      timer.start();

      Curve[] clonedCurves = new Curve[curves.Length];
      for (int i = 0; i < curves.Length; i++)
        if (curves[i] != null)
        {
          clonedCurves[i] = (Curve)curves[i].Clone();
          var cc = clonedCurves[i] as CalibratedCurve;
          if (cc == null)
            continue;
          cc.Calibrator = (Calibrator)(curves[i] as CalibratedCurve).Calibrator.ShallowCopy();
          SurvivalCalibrator cal = cc.Calibrator as SurvivalCalibrator;
          if (cal != null)
            cal.RecoveryCurve = (RecoveryCurve)cal.RecoveryCurve.Clone();
        }
        else
          clonedCurves[i] = null;

      timer.stop();
      logger.DebugFormat("Cloned curves in {0}s", timer.getElapsed());

      return clonedCurves;
    }

    /// <summary>
    ///   Restore an array of curves with recovery
    /// </summary>
    ///
    /// <param name="curves">Array of curves to set (some may be null)</param>
    /// <param name="srcCurves">Array of curves saved by CurveCloneWithRecovery</param>
    /// 
    /// <remarks>
    ///   The behavior is different than CurveSet in that this function keeps
    ///   the reference to recovery curves unchanged.
    /// </remarks>
    /// 
    /// <returns>Array of restored curves</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates cloning an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Create a set of cloned survival curves
    ///   SurvivalCurve [] savedCurves = (SurvivalCurve [])CurveUtil.CurveCloneWithRecovery(survivalCurves);
    ///
    ///   // ...
    ///
    ///   // Restore the original curves
    ///   CurveUtil.CurveRestoreWithRecovery(survivalCurves, savedCurves);
    /// </code>
    /// </example>
    ///
    internal static void
    CurveRestoreWithRecovery(CalibratedCurve[] curves, CalibratedCurve[] srcCurves)
    {
      for (int i = 0; i < curves.Length; i++)
      {
        if (srcCurves[i] == null)
          curves[i] = null;
        else if (curves[i] != null)
        {
          SurvivalCalibrator cal = curves[i].Calibrator as SurvivalCalibrator;
          curves[i].Copy(srcCurves[i]); // this also copies the source calibrator
          SurvivalCalibrator srcCal = srcCurves[i].Calibrator as SurvivalCalibrator;
          if (srcCal != cal && cal != null && srcCal != null)
          {
            RecoveryCurve rc = cal.RecoveryCurve;
            rc.Copy(srcCal.RecoveryCurve);
            srcCal.RecoveryCurve = rc;
          }
          else
            curves[i].Calibrator = srcCurves[i].Calibrator;
        }
        else
          curves[i] = (CalibratedCurve)srcCurves[i].Clone();
      }
      return;
    }

    internal static void CurveRestoreWithRecovery(
      Curve[] curves, Curve[] srcCurves)
    {
      for (int i = 0; i < curves.Length; i++)
      {
        if (srcCurves[i] == null)
          curves[i] = null;
        else if (curves[i] != null)
        {
          var rc = curves[i] as RecoveryCurve;
          if (rc != null)
          {
            rc.Copy((RecoveryCurve)srcCurves[i]);
            return;
          }

          var cc = curves[i] as CalibratedCurve;
          if (cc == null)
          {
            curves[i].Set(srcCurves[i]);
            return;
          }

          SurvivalCalibrator cal = cc.Calibrator as SurvivalCalibrator;
          cc.Copy(srcCurves[i]); // this also copies the source calibrator
          SurvivalCalibrator srcCal = ((CalibratedCurve)srcCurves[i])
            .Calibrator as SurvivalCalibrator;
          if (srcCal != cal && cal != null && srcCal != null)
          {
            rc = cal.RecoveryCurve;
            rc.Copy(srcCal.RecoveryCurve);
            srcCal.RecoveryCurve = rc;
          }
          else
            cc.Calibrator = ((CalibratedCurve)srcCurves[i]).Calibrator;
        }
        else
          curves[i] = (Curve)srcCurves[i].Clone();
      }
      return;
    }
    
    /// <summary>
    ///   Set array of curves from a matching array of curves
    /// </summary>
    ///
    /// <param name="curves">Array of curves to set</param>
    /// <param name="srcCurves">Array of source curves (some may be null)</param>
    ///
    /// <example>
    /// <para>The following sample demonstrates setting an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Create a set of cloned survival curves
    ///   SurvivalCurve [] savedCurves = (SurvivalCurve [])CurveUtil.CurveClone(survivalCurves);
    ///
    ///   // ...
    ///
    ///   // Restore copied curve back into survivalCurves.
    ///   CurveUtil.CurveSet( survivalCurves, savedCurves );
    /// </code>
    /// </example>
    ///
    public static void CurveSet(this Curve[] curves, Curve[] srcCurves)
    {
      Timer timer = new Timer();
      timer.start();

      for (int i = 0; i < curves.Length; i++)
      {
        if (srcCurves[i] == null)
          curves[i] = null;
        else if (curves[i] == null)
          curves[i] = new Curve(srcCurves[i]);
        else if (curves[i] is CalibratedCurve)
          ((CalibratedCurve)curves[i]).Copy(srcCurves[i]);
        else
          curves[i].Set(srcCurves[i]);
      }

      timer.stop();
      logger.DebugFormat("Set curves in {0}s", timer.getElapsed());

      return;
    }

    /// <summary>
    ///   Set array of curves from a matching array of curves
    /// </summary>
    ///
    /// <param name="curves">Array of curves to set</param>
    /// <param name="srcCurves">Array of source curves (some may be null)</param>
    ///
    /// <example>
    /// <para>The following sample demonstrates setting an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Create a set of cloned survival curves
    ///   SurvivalCurve [] savedCurves = (SurvivalCurve [])CurveUtil.CurveClone(survivalCurves);
    ///
    ///   // ...
    ///
    ///   // Restore the probabilities and quotes from the saved curve
    ///   CurveUtil.CurveSet( survivalCurves, savedCurves );
    /// </code>
    /// </example>
    ///
    /// <remarks>
    ///   <para>This function assumes curves and source curves have the same tenor structures.
    /// It copies by value the probabilities and market quotes, while the
    /// function <c>CurveSet(curves, srcCurves)</c> copies tenors and calibrators by reference.
    /// </para>
    ///
    /// <para>At this moment, this function is for internal use only.</para>
    /// </remarks>
    ///
    /// <exclude />
    public static void
    CurveRestoreQuotes(CalibratedCurve[] curves, CalibratedCurve[] srcCurves)
    {
      Timer timer = null;
      if (logger.IsDebugEnabled)
      {
        timer = new Timer();
        timer.start();
      }

      for (int i = 0; i < curves.Length; i++)
        if (srcCurves[i] != null)
          CopyQuotes(curves[i], srcCurves[i]);

      if (timer != null)
      {
        timer.stop();
        logger.DebugFormat("Set curves in {0}s", timer.getElapsed());
      }
      return;
    }

    /// <summary>
    ///   Copy probabilities and tenor quotes
    /// </summary>
    /// <param name="curve">Curve to copy to</param>
    /// <param name="srcCurve">Curve to copy from</param>
    ///
    /// <exclude />
    public static void CopyQuotes(CalibratedCurve curve, CalibratedCurve srcCurve)
    {
      curve.Set(srcCurve);
      CurveTenorCollection tenors = curve.Tenors;
      CurveTenorCollection srcTenors = srcCurve.Tenors;
      int count = curve.Tenors.Count;
      for (int i = 0; i < count; ++i)
      {
        IMarketQuote quote = srcTenors[i].CurrentQuote;
        tenors[i].SetQuote(quote.Type, quote.Value);
      }
      return;
    }

    /// <summary>
    ///   Fit array of curves
    /// </summary>
    ///
    /// <param name="curves">Array of curves to fit (some may be null)</param>
    ///
    /// <example>
    /// <para>The following sample demonstrates fitting an array
    /// of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Fit all curves
    ///   Curveutil.CurveFit( survivalCurves );
    /// </code>
    /// </example>
    ///
    public static void CurveFit(Curve[] curves)
    {
      Timer timer = new Timer();
      timer.start();

      if (curves.Length > ParallelStart && Parallel.Enabled)
      {
        Parallel.ForEach(curves, delegate(Curve theCurve)
        {
          var curve = theCurve as CalibratedCurve;
          if (curve != null && curve.JumpDate.IsEmpty())
          {
            //- If JumpDate is set, we should not fit.
            curve.Fit();
          }
        });
      }
      else
      {
        foreach (Curve theCurve in curves)
        {
          var curve = theCurve as CalibratedCurve;
          if (curve != null && curve.JumpDate.IsEmpty())
          {
            //- If JumpDate is set, we should not fit.
            curve.Fit();
          }
        }
      }

      timer.stop();
      logger.DebugFormat("Fitted curves in {0}s", timer.getElapsed());

      return;
    }

    internal static Curve[] SetPoints(this Curve[] curves, Curve[] srcCurves)
    {
      if (ListUtil.IsNullOrEmpty(curves) || ListUtil.IsNullOrEmpty(srcCurves))
        return curves;

      if (curves.Length != srcCurves.Length)
      {
        throw new ToolkitException(String.Format(
          "Destination curves ({0}) and source curves ({1}) not match",
          curves.Length, srcCurves.Length));
      }

      Timer timer = new Timer();
      timer.start();

      for (int i = 0; i < curves.Length; i++)
      {
        if (srcCurves[i] == null)
          curves[i] = null;
        else if (curves[i] == null)
          curves[i] = new Curve(srcCurves[i]);
        else
          curves[i].Set(srcCurves[i]);
      }

      timer.stop();
      logger.DebugFormat("Set curves in {0}s", timer.getElapsed());

      return curves;
    }

    internal static Curve[] SetQuotes(
      this Curve[] curves, IList<IMarketQuote>[] quotes)
    {
      if (ListUtil.IsNullOrEmpty(curves) || ListUtil.IsNullOrEmpty(quotes))
        return curves;

      if (curves.Length != quotes.Length)
      {
        throw new ToolkitException(String.Format(
          "Curves ({0}) and quote arrays ({1}) not match",
          curves.Length, quotes.Length));
      }

      Timer timer = null;
      if (logger.IsDebugEnabled)
      {
        timer = new Timer();
        timer.start();
      }

      for (int i = 0; i < curves.Length; i++)
      {
        var curve = curves[i] as CalibratedCurve;
        if (curve == null || curve.Tenors == null) continue;
        var qs = quotes[i];
        if (qs == null) continue;
        var n = qs.Count;
        if (n != curve.Tenors.Count)
        {
          throw new ToolkitException(String.Format(
            "tenors ({0}) and quotes ({1}) not match in curve {2}",
            curve.Tenors.Count, n, curve.Name));
        }
        for (int t = 0; t < n; ++t)
          curve.Tenors[t].SetQuote(qs[t].Type, qs[t].Value);
      }

      if (timer != null)
      {
        timer.stop();
        logger.DebugFormat("Set curves in {0}s", timer.getElapsed());
      }

      return curves;
    }

    /// <summary>
    ///   Get set of unique tenors for an array of curves
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the unique superset of tenor names for
    ///   an array of curves in the order found in the curves.</para>
    /// </remarks>
    ///
    /// <param name="curves">Array of calibrated curves (some may be null)</param>
    ///
    /// <returns>Array of tenor names</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates getting the unique superset of
    /// curve tenor names from an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Get unique list of the superset of curve tenors from a set of curves
    ///   string [] tenors = CurveUtil.CurveTenors( survivalCurve );
    ///
    ///   // Bump all these curve tenors by 10bp and refit
    ///   Curveutil.CurveBump( survivalCurves, tenors, 10, true, false, true );
    /// </code>
    /// </example>
    ///
    public static string[]
    CurveTenors(CalibratedCurve[] curves)
    {
      // Need to put together a superset of all individual tenors
      ArrayList tenorList = new ArrayList();

      foreach (CalibratedCurve curve in curves)
      {
        if (curve != null)
        {
          for (int j = 0; j < curve.Tenors.Count; j++)
            if (!tenorList.Contains(curve.Tenors[j].Name))
              tenorList.Add(curve.Tenors[j].Name);
        }
      }

      string[] tenors = new string[tenorList.Count];
      for (int i = 0; i < tenorList.Count; i++)
        tenors[i] = (string)tenorList[i];

      return tenors;
    }

    /// <summary>
    /// Get set of unique CurveTenor objects for an array of curves sorted by termination
    /// </summary>
    /// <param name="curves">A set of calibrated curves</param>
    /// <returns>A set of tenors</returns>
    public static CurveTenor[]
    CurveTenorsObj(CalibratedCurve[] curves)
    {
      ArrayList tenorList = new ArrayList();
      ArrayList tenorNameList = new ArrayList();
      foreach (CalibratedCurve curve in curves)
      {
        if (curve != null)
        {
          for (int j = 0; j < curve.Tenors.Count; j++)
            if (!tenorNameList.Contains(curve.Tenors[j].Name))
            {
              tenorNameList.Add(curve.Tenors[j].Name);
              tenorList.Add(curve.Tenors[j]);
            }
        }
      }      
      CurveTenor[] tenors = (CurveTenor[])tenorList.ToArray(typeof(CurveTenor));

      //sort the curve tenors
      for(int i = 0; i < tenors.Length-1; i++)
        for(int j = i+1; j < tenors.Length; j++)
          if(Dt.Cmp(tenors[i].Maturity, tenors[j].Maturity) > 0)
          {
            CurveTenor ct = (CurveTenor)tenors[i].Clone();
            tenors[i] = (CurveTenor)tenors[j].Clone();
            tenors[j] = (CurveTenor)ct.Clone();
          }
      return tenors;
    }

    /// <summary>
    ///   Get set of unique tenors for an array of curves that cover up to a specified maturity date.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the unique superset of tenor names for
    ///   an array of curves in the order found in the curves that are at least past the specified
    ///   maturity date.</para>
    /// </remarks>
    ///
    /// <param name="curves">Array of calibrated curves (some may be null)</param>
    /// <param name="maturity">Maturity date to cover</param>
    ///
    /// <returns>Array of tenor names</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates getting the unique superset of
    /// curve tenor names from an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///   CDS cds;
    ///
    ///   // ...
    ///
    ///   // Get unique list of the superset of curve tenors from a set of curves up to
    ///   // and including the maturity of the CDS.
    ///   string [] tenors = CurveUtil.CurveTenors( survivalCurve, cds.Maturity );
    ///
    ///   // Bump all these curve tenors by 10bp and refit
    ///   Curveutil.CurveBump( survivalCurves, tenors, 10, true, false, true );
    /// </code>
    /// </example>
    ///
    public static string[]
    CurveTenors(CalibratedCurve[] curves, Dt maturity)
    {
      // Need to put together a superset of all individual tenors
      CurveTenorCollection tenorList = new CurveTenorCollection();
      if (curves == null)
        return BaseEntity.Shared.EmptyArray<string>.Instance;

      foreach (CalibratedCurve curve in curves)
      {
        if (curve != null && curve.Tenors.Count > 0)
        {
          if (curve.Tenors.Count == 0)
            continue;

          // Find tenor after maturity
          int lastTenor = 0;
          if (maturity.IsEmpty())
          {
            lastTenor = curve.Tenors.Count - 1;
          }
          else
          {
            while ((lastTenor < (curve.Tenors.Count - 1)) && (curve.Tenors[lastTenor].Maturity <= maturity))
              lastTenor++;
            while ((lastTenor < (curve.Tenors.Count - 2)) && (curve.Tenors[lastTenor + 1].Maturity == curve.Tenors[lastTenor].Maturity))
              lastTenor++;
          }
        
          // Add unique tenors
          for (int j = 0; j <= lastTenor; j++)
          {
            string name = String.IsNullOrEmpty(curve.Tenors[j].Name)
                            ? curve.Tenors[j].Maturity.ToString()
                            : curve.Tenors[j].Name;
            curve.Tenors[j].Name = name;
            if (!tenorList.ContainsTenor(name))
              tenorList.Add(curve.Tenors[j]);
          }
        }
      }
      tenorList.Sort();
      string[] tenors = new string[tenorList.Count];
      int i = 0;
      foreach (CurveTenor ten in tenorList)
      {
        tenors[i++] = ten.Name;
      }

      return tenors;
    }

    /// <summary>
    /// Find the closest tenor for a given date
    /// </summary>
    /// <param name="curves">An array of calibrated curves such as IR curves or survival curves</param>
    /// <param name="date">Date for which closest tenor is needed from the curves</param>
    /// <returns>Tenor name</returns>
    public static CurveTenor FindClosestTenor(CalibratedCurve[] curves, Dt date)
    {
      // We don't need superset curve tenors here, just need to know 
      // if the given date is within curve tenors of any curve
      if (curves == null || curves.Length == 0)
        throw new ToolkitException("FindClosestTenor: Must have at least one calibrated curve");

      // The curve might be defaulted, tenors count maybe 0.
      if (curves[0].Tenors.Count == 0)
        return null;
      CurveTenor tenor = curves[0].Tenors[curves[0].Tenors.Count-1];
      for (int j = 0; j < curves.Length; ++j)
      {
        int lastIndex = curves[j].Tenors.Count - 1;
        if (Dt.Cmp(date, curves[j].Tenors[lastIndex].Maturity) > 0)
        {
          // Get the last tenor for i'th curve, if later  
          // than i-1 last tenor, set the new last tenor 
          CurveTenor tenorAfterLastCurveMaturity = curves[j].Tenors[lastIndex];
          if (Dt.Cmp(tenor.Maturity, tenorAfterLastCurveMaturity.Maturity) < 0)
            tenor = tenorAfterLastCurveMaturity;
        }
        else
        {
          Dt[] curveTenorDates = new Dt[lastIndex + 1];
          int current = 0;
          for (int k = 0; k < lastIndex + 1; ++k)
          {
            curveTenorDates[k] = curves[j].Tenors[k].Maturity;
            if (Dt.Cmp(date, curveTenorDates[k]) <= 0)
            {
              current = k;
              break;
            }
          }
          // Get a closer curve tenor date from the two that enclose the date
          // Note current = k could be 0
          tenor = 
            (current == 0) ? curves[j].Tenors[current] :
              (Dt.Diff(curves[j].Tenors[current - 1].Maturity, date) < Dt.Diff(date, curves[j].Tenors[current].Maturity) ?
               curves[j].Tenors[current - 1] : curves[j].Tenors[current]);
          break;
        }
      }
      return tenor;
    }
    /// <summary>
    ///   Find the closest tenors for given dates
    /// </summary>
    /// <param name="curves">An array of calibrated curves such as IR curves or survival curves </param>
    /// <param name="dates">An array of dates for which closest tenors are needed from the curves</param>
    /// <returns>An array of tenor names</returns>
    public static CurveTenor[] FindClosestTenors(CalibratedCurve[] curves, Dt[] dates)
    {
      // We don't need superset curve tenors here, just need to know 
      // if each of dates is within curve tenors of any curve
      if (dates == null || dates.Length == 0)
        return null;
      CurveTenor[] tenors = new CurveTenor[dates.Length];
      for (int i = 0; i < dates.Length; ++i)
      {
        tenors[i] = FindClosestTenor(curves, dates[i]);
      }
      return tenors;
    }

    /// <summary>
    ///   Verify each of a set of tenors is in at least one of a set of curves
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Checks if tenors specified are found in any curves.</para>
    /// </remarks>
    ///
    /// <param name="curves">Array of calibrated curves</param>
    /// <param name="tenors">Tenor names</param>
    ///
    /// <returns>true if tenors found in at least one of the specified curves</returns>
    ///
    /// <example>
    /// <para>The following sample demonstrates testing if a set of curve names match
    ///  an array of <see cref="CalibratedCurve"/>s.</para>
    /// <code language="C#">
    ///   // Have a set of calibrated survival curves
    ///   SurvivalCurve [] survivalCurves;
    ///
    ///   // ...
    ///
    ///   // Get a list of tenors to test
    ///   string [] tenors = { "3 Year", "5 Year" };
    ///
    ///   // Throw exception if tenors do not exist in any of the curves.
    ///   if( !Curveutil.CurveHasTenors( survivalCurves, tenors ) )
    ///  	  throw new ToolkitException( "Some tenors missing from curves" );
    /// </code>
    /// </example>
    ///
    public static bool
    CurveHasTenors(CalibratedCurve[] curves, string[] tenors)
    {
      foreach (string tenor in tenors)
      {
        bool found = false;
        foreach (CalibratedCurve curve in curves)
        {
          found = CurveHasTenor(curve, tenor);
        }
        if (!found)
          return false;
      }

      return true;
    }

    /// <summary>
    ///   Verify a curve contains a tenor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Checks if tenor specified is found in the curve.</para>
    /// </remarks>
    ///
    /// <param name="curve">Calibrated curve</param>
    /// <param name="tenor">Tenor name</param>
    ///
    /// <returns>true if tenor found in the curve</returns>
    ///
    public static bool
    CurveHasTenor(CalibratedCurve curve, string tenor)
    {
      return (curve != null && curve.Tenors.Index(tenor) >= 0);
    }

    /// <summary>
    /// ****** This method is NOT supposed to be used in general.
    /// The method is to be used in old scaling method. In CDXDurationFactorCalc.cs, if there's
    /// a possible curve tenor name typo (say 5 Y in stead of 5Y if 5Y is a curve tenor name), 
    /// the scaling might fail and falls back to 8.1 dealing. At the beginning of the catch block
    /// we use this method to check if the tenor name is a possible typo that causes the scaling
    /// failure, if so, throws an exception from there.
    /// </summary>
    /// <param name="survCurves"> Array of survival curves </param>
    /// <param name="tenors"> Array of tenor names </param>
    /// <param name="typos"> Array of typos</param>
    /// <returns>Boolean value of having typos or not</returns>
    public static bool TenorTypos(SurvivalCurve[] survCurves, string[] tenors, out string[] typos)
    {
      bool hasTypos = false;
      typos = null;
      if (tenors == null || tenors.Length == 0)
        return hasTypos;

      string[] commonTenors = CurveUtil.CurveTenors(survCurves);
      string[] trimmedCommonTenors = Array.ConvertAll<string, string>(commonTenors, delegate(string str)
      { return System.Text.RegularExpressions.Regex.Replace(str.ToUpper(), @"\s", ""); });
      System.Collections.Generic.List<string> theTypos = new System.Collections.Generic.List<string>();
      for (int j = 0; j < tenors.Length; ++j)
      {
        string trimmedTenor = System.Text.RegularExpressions.Regex.Replace(tenors[j].ToUpper(), @"\s", "");
        for (int i = 0; i < trimmedCommonTenors.Length; ++i)
        {
          if ((trimmedCommonTenors[i].StartsWith(trimmedTenor) ||
               trimmedTenor.StartsWith(trimmedCommonTenors[i])) &&
               string.Compare(commonTenors[i], tenors[j], true) != 0)
            theTypos.Add(tenors[j]);
        }
      }
      if (theTypos.Count > 0)
      {
        typos = (string[])theTypos.ToArray();
        hasTypos = true;
      }
      return hasTypos;
    }

    /// <summary>
    ///   Get the date for tenor name from array of curves
    /// </summary>
    /// <param name="curves">An array of calibrated curves</param>
    /// <param name="tenor">Tenor name</param>
    /// <returns>Date for tenor</returns>
    public static Dt CurveTenorToDt(CalibratedCurve[] curves, string tenor)
    {
      Dt date = new Dt();
      foreach (CalibratedCurve curve in curves)
      {
        int index = -1;
        if (curve != null)
        {
          index = curve.Tenors.Index(tenor);
          if (index >= 0)
          {
            date = curve.Tenors[index].Product.Maturity;
            return date;
          }
        }
      }      
      return date;
    }

    /// <summary>
    ///  Get the dates for tenor names from array of curves
    /// </summary>
    /// <param name="curves">An array of calibrated curves</param>
    /// <param name="tenors">Dates for tenors</param>
    /// <returns></returns>
    public static Dt[] CurveTenorToDt(CalibratedCurve[] curves, string[] tenors)
    {
      if (tenors == null || tenors.Length == 0)
        return null;
      Dt[] dates = new Dt[tenors.Length];
      for (int i = 0; i < tenors.Length; ++i)
      {
        dates[i] = new Dt();
        dates[i] = CurveTenorToDt(curves, tenors[i]);
      }
      return dates;
    }

    /// <summary>
    ///   Get the market quote for an individual tenor point of a curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the natural quote for a specified tenor. For CDS this is the par CDS
    ///   spread. For Bonds this is the price.</para>
    /// </remarks>
    ///
    /// <param name="tenor">Curve tenor to get quote</param>
    ///
    /// <returns>The market quote</returns>
    ///
    public static double
    MarketQuote(CurveTenor tenor)
    {
      return tenor.CurrentQuote.Value;
    }

    /// <summary>
    ///   Set the market quote to an individual tenor point of a curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function assumes the natural quote for a specified tenor
    ///   as returned by the function <c>CurveUtil.MarketQuote()</c>.
    ///   For CDS this is the par CDS spread. For Bonds this is the price.</para>
    /// </remarks>
    ///
    /// <param name="tenor">Curve tenor to set quote</param>
    /// <param name="quote">Market quote to set</param>
    ///
    public static void SetMarketQuote(CurveTenor tenor, double quote)
    {
      IProduct product = tenor.Product;
      if (product is CDS)
        tenor.SetQuote(QuotingConvention.CreditSpread, quote);
      else if ((product is SwapLeg && !((SwapLeg)product).Floating) || product is Note || product is FRA)
        tenor.SetQuote(QuotingConvention.Yield, quote);
      else if ((product is SwapLeg && ((SwapLeg)product).Floating))
        tenor.SetQuote(QuotingConvention.YieldSpread, quote);
      else if (product is StirFuture || product is Bond)
        tenor.SetQuote(QuotingConvention.FlatPrice, quote);
      else if (product is Swap)
      {
        var swap = product as Swap;
        tenor.SetQuote(swap.IsBasisSwap ? QuotingConvention.YieldSpread : QuotingConvention.Yield, quote);
      }
      else
        throw new ArgumentException(String.Format("{0} not supported for curve bumping at the moment", product.GetType()));
      return;
    }


    /// <summary>
    ///   Get par CDS rate given a maturity date
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of CDS</param>
    /// <returns>Par cds rate for maturity in percent</returns>
    public static double ImpliedSpread(this SurvivalCurve survivalCurve, Dt maturity)
    {
       return ImpliedSpread(survivalCurve, maturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY, DEFAULT_ROLL, DEFAULT_CALENDAR);
    }
    
    /// <summary>
    ///   Get par CDS rate given a maturity date and roll convention
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of CDS</param>
    /// <param name="roll">Business day roll convention</param>
    /// <returns>Par cds rate for maturity in percent</returns>
    public static double ImpliedSpread(this SurvivalCurve survivalCurve, Dt maturity, BDConvention roll)
    {
      return ImpliedSpread(survivalCurve, maturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY, roll, DEFAULT_CALENDAR);
    }

    /// <summary>
    ///   Get par CDS rate given a maturity date
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of CDS</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    ///
    /// <returns>Par cds rate for maturity in percent</returns>
    ///
    public static double ImpliedSpread(this SurvivalCurve survivalCurve,
      Dt maturity, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      CDSCashflowPricer pricer = ImpliedPricer(survivalCurve, maturity, dayCount, frequency, roll, calendar);
      CDS cds = pricer.CDS;
      Dt lastPrem = GetPrevCdsDate(maturity);
      if (lastPrem > cds.FirstPrem)
        cds.LastPrem = lastPrem;
      pricer.Reset();
      return pricer.BreakEvenPremium();
    }

    internal static CDS ImpliedCDS(this SurvivalCurve survivalCurve,Dt maturity)
    {
      return ImpliedCDS(survivalCurve, maturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY, DEFAULT_ROLL, DEFAULT_CALENDAR);
    }

    internal static CDS ImpliedCDS(this SurvivalCurve survivalCurve,
      Dt maturity, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      CDSCashflowPricer pricer = ImpliedPricer(survivalCurve, maturity, dayCount, frequency, roll, calendar);
      CDS cds = pricer.CDS;
      Dt lastPrem = GetPrevCdsDate(maturity);
      if (lastPrem > cds.FirstPrem)
        cds.LastPrem = lastPrem;
      pricer.Reset();
      cds.Premium = pricer.BreakEvenPremium();
      return cds;
    }

    private static Dt GetPrevCdsDate(Dt maturity)
    {
      Dt date = Dt.CDSRoll(maturity);
      while (date >= maturity)
        date = Dt.Add(date, Frequency.Quarterly, -1, CycleRule.Twentieth);
      return date;
    }

    /// <summary>
    ///   Get CDS risky duration given a maturity date
    /// </summary>
    /// <remarks>
    /// <para>Uses default parameters 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of CDS</param>
    ///
    /// <returns>CDS risky duration for maturity</returns>
    ///
    public static double
    ImpliedDuration(SurvivalCurve survivalCurve, Dt maturity)
    {
      return ImpliedDuration(survivalCurve, maturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY, DEFAULT_ROLL,DEFAULT_CALENDAR);
    }

    /// <summary>
    ///   Get CDS risky duration given a maturity date
    /// </summary>
    ///
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of CDS</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    ///
    /// <returns>CDS risky duration for maturity</returns>
    ///
    public static double
    ImpliedDuration(SurvivalCurve survivalCurve, Dt maturity, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      CDSCashflowPricer pricer = ImpliedPricer(survivalCurve, maturity, dayCount, frequency, roll, calendar);
      return pricer.RiskyDuration();
    }


    /// <summary>
    ///   Get implied forward CDS risky duration given a forward start date and a maturity date
    /// </summary>
    /// <remarks>
    /// <para>Uses default parameters 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="forwardStart">Forward start date</param>
    /// <param name="maturity">Maturity of CDS</param>
    ///
    /// <returns>CDS risky duration for maturity</returns>
    ///
    public static double
    ImpliedDuration(SurvivalCurve survivalCurve, Dt forwardStart, Dt maturity)
    {
      return ImpliedDuration(survivalCurve, forwardStart, maturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY, DEFAULT_ROLL, DEFAULT_CALENDAR);
    }

    /// <summary>
    ///   Get implied forward CDS risky duration given a forward start date and a maturity date
    /// </summary>
    ///
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="forwardStart">Forward start date</param>
    /// <param name="maturity">Maturity of CDS</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    ///
    /// <returns>CDS risky duration for maturity</returns>
    ///
    public static double
    ImpliedDuration(SurvivalCurve survivalCurve, Dt forwardStart, Dt maturity, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      CDSCashflowPricer pricer = ImpliedPricer(survivalCurve, maturity, dayCount, frequency, roll, calendar);
      return pricer.RiskyDuration(forwardStart);
    }


    /// <summary>
    ///   Get CDS forward spread given a forward start and maturity date
    /// </summary>
    /// <remarks>
    /// <para>Uses default parameters 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="forwardStart">Forward start date for CDS</param>
    /// <param name="forwardMaturity">Maturity of forward starting CDS</param>
    /// <returns>Par cds rate for maturity in percent</returns>
    public static double
    ImpliedForwardSpread(SurvivalCurve survivalCurve, Dt forwardStart, Dt forwardMaturity)
    {
      return ImpliedForwardSpread(survivalCurve, forwardStart, forwardMaturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY,DEFAULT_ROLL, DEFAULT_CALENDAR);
    }

    
    /// <summary>
    ///   Get CDS forward spread given a forward start and maturity date
    /// </summary>
    ///
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="forwardStart">Forward start date for CDS</param>
    /// <param name="forwardMaturity">Maturity of forward starting CDS</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    ///
    /// <returns>Par cds rate for maturity in percent</returns>
    ///
    public static double
    ImpliedForwardSpread(SurvivalCurve survivalCurve, Dt forwardStart, Dt forwardMaturity, DayCount dayCount,
                         Frequency frequency, BDConvention roll, Calendar calendar)
    {
      CDSCashflowPricer pricer = ImpliedPricer(survivalCurve, forwardMaturity, dayCount, frequency, roll, calendar);
      return pricer.FwdPremium(forwardStart);
    }

    /// <summary>
    /// Calculate the implied forward swap rate
    /// </summary>
    /// <param name="discountCurve"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="simpleProjection"></param>
    /// <param name="rateIndex"></param>
    /// <returns></returns>
    internal static double ImpliedForwardSwapRate(DiscountCurve discountCurve,
      Dt effective, Dt maturity, bool simpleProjection, ReferenceIndex rateIndex)
    {
      var daycount = rateIndex.DayCount;
      var frequency = rateIndex.IndexTenor.ToFrequency();
      var roll = rateIndex.Roll;
      var calendar = rateIndex.Calendar;
      // discount curve AsOf date has to be on or before (forward) swap effective date
      if (discountCurve.AsOf > effective)
        throw new ArgumentException(
          "Discount Curve AsOf date can not be after forward swap effective date");

      // construct fixedLeg product and pricer
      SwapLeg fixedSwapLeg = new SwapLeg(effective, maturity,
        Currency.None, 0.0, daycount, frequency, roll, calendar, false);

      if (simpleProjection)
        fixedSwapLeg.CashflowFlag |= CashflowFlag.SimpleProjection;

      SwapLegPricer fixedSwapLegPricer = new SwapLegPricer(fixedSwapLeg,
        effective, effective, 1.0, discountCurve, null, null, null, null, null);

      // construct floatLeg product and pricer
      SwapLeg floatSwapLeg = new SwapLeg(effective, maturity,
        Currency.None, 0.0, daycount, frequency, roll, calendar, false);

      Dt firstCpn = floatSwapLeg.FirstCoupon.IsEmpty()
        ? maturity
        : floatSwapLeg.FirstCoupon;
      double df = discountCurve.DiscountFactor(effective, firstCpn);
      double fraction = Dt.Fraction(effective, firstCpn, daycount);
      var currentRate = fraction > 0 ? ((1/df - 1)/fraction) : 0.0;
      floatSwapLeg.Index = "LIBOR";

      SwapLegPricer floatSwapLegPricer = new SwapLegPricer(floatSwapLeg,
        effective, effective, 1.0, discountCurve, rateIndex,
        discountCurve, new RateResets(effective, currentRate), null, null);

      double swapRate = new SwapPricer(fixedSwapLegPricer, floatSwapLegPricer)
        .ParCoupon();

      return swapRate;
    }


    /// <summary>
    ///   Construct a pricer matching a CDS consistent with the survival term structure.
    /// </summary>
    /// <remarks>
    /// <para>Currently only supports survival curves calibrated using the SurvivalFitCalibrator.</para>
    /// <para>Uses default parameters 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of cds tenor</param>
    /// <returns>CDS Pricer matching maturity</returns>
    public static CDSCashflowPricer
    ImpliedPricer(this SurvivalCurve survivalCurve, Dt maturity)
    {
      return ImpliedPricer(survivalCurve, maturity, DEFAULT_DAYCOUNT, DEFAULT_FREQUENCY, DEFAULT_ROLL, DEFAULT_CALENDAR);
    }


    /// <summary>
    ///   Construct a pricer matching a CDS consistent with the survival term structure.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Currently only supports survival curves calibrated using the SurvivalFitCalibrator</para>
    /// </remarks>
    ///
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="maturity">Maturity of CDS tenor</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    ///
    /// <returns>CDS Pricer matching maturity</returns>
    ///
    public static CDSCashflowPricer
    ImpliedPricer(this SurvivalCurve survivalCurve, Dt maturity, DayCount dayCount,
                  Frequency frequency, BDConvention roll, Calendar calendar)
    {
      if (survivalCurve == null)
        throw new ArgumentException("Null survival curve");
      if (survivalCurve.SurvivalCalibrator == null)
        throw new ArgumentException(String.Format("Survival Curve {0} is not a calibrated curve", survivalCurve.Name));

      var calibrator = (SurvivalCalibrator)survivalCurve.SurvivalCalibrator;
      CDS cds = survivalCurve.CreateCDS(null, maturity, Dt.Empty, 0.0, dayCount, frequency, roll, calendar);
      CDSCashflowPricer pricer = (CDSCashflowPricer)calibrator.GetPricer(survivalCurve, cds);
      return pricer;
    }

    /// <summary>
    ///   Construct a Recovery curve from a single recovery rate or a vector of recovery rates.
    /// </summary>
    /// <exclude />
    static public RecoveryCurve
    GetRecoveryCurve(Dt asOf, Dt[] dates, double[] recoveries, double recoveryDispersion)
    {
      // normalize
      if (dates == null || dates.Length == 0)
        dates = null;

      // Validate
      if (recoveries == null || recoveries.Length <= 0)
        throw new ArgumentException("Must specify at least one recovery rate");
      if (recoveries.Length != 1 && (dates == null || recoveries.Length != dates.Length))
        throw new ArgumentException("Must have single recovery rate or recovery rate for each tenor");

      RecoveryCurve curve = new RecoveryCurve(asOf, RecoveryType.Face, recoveryDispersion);
      if (dates == null)
      {
        curve.Add(asOf, recoveries[0]);
      }
      else
      {
        for (int i = 0; i < recoveries.Length; i++)
          if(dates[i] != null && Dt.Cmp(dates[i], Dt.Empty) != 0)
            curve.Add(dates[i], recoveries.Length==1?recoveries[0]:recoveries[i]);
      }
      return curve;
    }

    /// <summary>
    ///   Get Recovery curve from a variant argument.
    /// </summary>
    /// <exclude />
    static public RecoveryCurve
    GetRecoveryCurve(Dt asOf, IProduct[] products, double[] recoveries, double recoveryDispersion)
    {
      // normalize
      if (products == null || products.Length == 0)
        products = null;

      // Validate
      if (recoveries == null || recoveries.Length <= 0)
        throw new ArgumentException("Must specify at least one recovery rate");
      if (recoveries.Length != 1 && (products == null || recoveries.Length != products.Length))
        throw new ArgumentException("Must have single recovery rate or recovery rate for each tenor");

      RecoveryCurve curve = new RecoveryCurve(asOf, RecoveryType.Face, recoveryDispersion);
      if (products == null)
      {
        curve.Add(asOf, recoveries[0]);
      }
      else
      {
        for (int i = 0; i < recoveries.Length; i++)
          curve.Add(products[i].Maturity, recoveries[i]);
      }
      return curve;
    }

    /// <summary>
    ///   Get an array of curve names
    /// </summary>
    /// <param name="curves">Curves to retries the name</param>
    /// <returns>An array of names</returns>
    public static string[] CurveNames(CalibratedCurve[] curves)
    {
      if (curves == null) return null;
      string[] names = new string[curves.Length];
      for (int i = 0; i < curves.Length; ++i)
        names[i] = curves[i].Name;
      return names;
    }

    ///  <summary>
    ///    Reset Effective/Maturities of Survival Curve Tenors based on configuration flags and refit.
    ///  </summary>
    /// 
    ///  <remarks>
    ///    <para>This is useful when moving a curve forward in time when crossing a roll date</para>
    ///  </remarks>
    /// 
    ///  <param name="curve">curve to reset maturity</param>
    /// <param name="newAsOf">New asOf date</param>
    /// <param name="newProductEffectiveDate">New Effective Date to Calc tenor maturities with.</param>
    /// <param name="resetEffective">Flag, if true, reset the effective date of curve tenor</param>
    /// <param name="resetMaturity">Flag, if true, reset the maturity date of curve tenor</param>
    public static void ResetCdsTenorDatesRecalibrate(SurvivalCurve curve, Dt newAsOf, Dt newProductEffectiveDate, bool resetEffective, bool resetMaturity)
    {
      // Validate
      if (curve == null)
        throw new ArgumentException("No curves specified to reset maturities");

        CurveTenorCollection tenors = curve.Tenors;
        if (tenors != null && tenors.Count > 0)
        {
          foreach (CurveTenor ct in tenors)
          {
            Tenor t;
            var cds = ct.Product as CDS;
            if (cds ==  null)
              continue;
            if (Tenor.TryParse(ct.Name, out t))
            {
              if (resetEffective && (newProductEffectiveDate < cds.Maturity || resetMaturity))
              {
                cds.Effective = newProductEffectiveDate;
              }

              if (resetMaturity)
              {
                var newMaturity = Dt.CDSMaturity(newAsOf, t);
                if (newMaturity > cds.Effective)
                {
                  cds.Maturity = newMaturity;
                }
              }

              if (resetEffective || resetMaturity)
              {
                cds.FirstPrem = Dt.Empty;
                cds.LastPrem = Dt.Empty;
              }
            }
          }

          try
          {
            if (curve.Tenors.Any())
            {
              curve.Clear();
              curve.ReFit(0);
            }
          }
          catch (SurvivalFitException ex)
          {
            throw new CurveBumpException(String.Format("Failed to fit tenor {0} resetting maturity: {1}",
              ex.Tenor.Name, ex.Message), 0, ex.CurveName, ex.Tenor, ex);
          }
        }
    }

    ///<summary>
    /// Check if the tenor is named with fixed maturity date, if yes return the date otherwise return empty date
    ///</summary>
    ///<param name="input">Input name to go though fixed maturity check </param>
    ///<param name="outputDt">The output date converted into, will be empty if the string is not in the acceptable date formats</param>
    ///<returns></returns>
    public static bool TryGetFixedMaturityFromString(string input, out Dt outputDt)
    {
      outputDt = Dt.Empty;

      if (string.IsNullOrEmpty(input))
        return false;

      if (IsInteger(input))
        try
        {
          outputDt = Dt.FromExcelDate(Convert.ToDouble(input));
          if (outputDt != Dt.Empty)
            return true;
        }
        catch
        {
          outputDt = Dt.Empty;
        }

      if (Dt.TryFromStr(input, Dt.FormatDefault, out outputDt) ||
        Dt.TryFromStr(input, "%D", out outputDt) ||
        Dt.TryFromStr(input, "%F", out outputDt) ||
        (IsInteger(input) && Dt.TryFromStr(input, "%Y%m%d", out outputDt)))
        return true;

      return false;
    }

    private static Regex _isNumber = new Regex(@"^\d+$");
    ///<summary>
    /// Test if a string contains only digit characters
    ///</summary>
    ///<param name="theValue">The string to be tested on</param>
    ///<returns></returns>
    public static bool IsInteger(string theValue)
    {
      Match m = _isNumber.Match(theValue);
      return m.Success;
    }

    #endregion Other methods

    #region Validation
    [Conditional("DEBUG")]
    internal static void VerifyFlatCurveLeftContinous(this Curve curve)
    {
      if (curve == null || curve.InterpMethod != InterpMethod.Flat)
        return;
      int count = curve.Count;
      Dt lastDate = curve.AsOf;
      for (int i = -1; ++i < count; )
      {
        Dt date = curve.GetDt(i);
        Dt middle = Dt.Add(date, -1);
        if (middle <= lastDate) continue;
        lastDate = date;
        double actual = curve.Interpolate(middle);
        double expect = curve.GetVal(i);
        if (actual != expect)
          throw new ToolkitException("Flat curve not left continous.");
      }
    }
    [Conditional("DEBUG")]
    internal static void VerifyFlatCurveRightContinous(this Curve curve)
    {
      if (curve == null || curve.InterpMethod != InterpMethod.Flat)
        return;
      int count = curve.Count;
      Dt lastDate = curve.AsOf;
      for (int i = -1; ++i < count; )
      {
        Dt date = curve.GetDt(i);
        Dt middle = Dt.Add(lastDate, 1);
        if (middle >= date) continue;
        lastDate = date;
        double actual = curve.Interpolate(middle);
        double expect = curve.GetVal(i);
        if (actual != expect)
          throw new ToolkitException("Flat curve not right continous.");
      }
    }
    #endregion

    #region Constants
    /// <summary>
    ///  BDConvention
    /// </summary>
    public const BDConvention DEFAULT_ROLL = BDConvention.Modified;
    /// <summary>
    ///  Frequency
    /// </summary>
    public const Frequency DEFAULT_FREQUENCY = Frequency.Quarterly;
    /// <summary>
    ///  Calendar
    /// </summary>
    public static Calendar DEFAULT_CALENDAR = Calendar.None;
    /// <summary>
    ///  Day count
    /// </summary>
    public const DayCount DEFAULT_DAYCOUNT = DayCount.Actual360;
    #endregion

  } // class CurveUtil
}
