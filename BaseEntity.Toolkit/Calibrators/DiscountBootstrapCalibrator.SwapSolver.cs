/*
 * DiscountRateCalibrator.SwapGeneric.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using Parameter = BaseEntity.Toolkit.Models.RateModelParameters.Param;
using Process = BaseEntity.Toolkit.Models.RateModelParameters.Process;

namespace BaseEntity.Toolkit.Calibrators
{
  public partial class DiscountBootstrapCalibrator
  {
    #region Data and Properties

    private SwapCalibrationMethod swapCalibrationMethod_;

    /// <summary>
    ///   Gets or sets the swap calibration method (Extrap or Solve).
    /// </summary>
    /// <value>The swap calibration method.</value>
    public SwapCalibrationMethod SwapCalibrationMethod
    {
      get { return swapCalibrationMethod_; }
      set { swapCalibrationMethod_ = value; }
    }

    #endregion Data and Properties

    #region Calibration

    /// <summary>
    ///   Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    /// <param name = "curve">Discount curve to calibrate</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      if (SwapCalibrationMethod == SwapCalibrationMethod.Extrap)
      {
        // Old method
        FitFromExtrap(curve, fromIdx);
        return;
      }

      var discountCurve = new OverlayWrapper(curve);

      // Start from scratch each time as this is fast
      discountCurve.Clear();

      // Set up swap calibrator
      var swapCalibrator = new SwapSolverCalibrator();

      foreach (CurveTenor tenor in discountCurve.Tenors)
        if (tenor.Product is SwapLeg)
        {
          var swap = (SwapLeg) tenor.Product;
          swapCalibrator.Add(swap);
        }
      Dt firstSwapDate = (swapCalibrator.Count == 0 ? Dt.Empty : swapCalibrator.GetSwap(0).Maturity);

      // Fit initial money market rates up to first swap date
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if ((!firstSwapDate.IsEmpty()) && Dt.Cmp(tenor.Maturity, firstSwapDate) >= 0) break;
        if (tenor.Product is Note)
        {
          var note = (Note) tenor.Product;
          // ISDA zero curve compatible: it always roll the MM matuirty to a business day.
          // NOTE: if the BDConvention is None, no roll happens.
          Dt maturity = Dt.Roll(note.Maturity, note.BDConvention, note.Calendar);
          double df = RateCalc.PriceFromRate(note.Coupon, AsOf, maturity, note.DayCount, note.Freq);
          logger.Debug(String.Format("Tenor {0}, MM rate={1}, df={2}", note.Maturity, note.Coupon, df));
          discountCurve.Add(tenor.Maturity, df);
        }
      } // for each

      // Overlay Eurodollar Futures up to first swap date
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if (tenor.Product is StirFuture)
        {
          var fut = (StirFuture) tenor.Product;

          // Test if we are after first swap date
          if ((!firstSwapDate.IsEmpty()) && Dt.Cmp(fut.DepositMaturity, firstSwapDate) >= 0) break;

          // Interpolate or extrapolate discount factor to start of this ED Future
          if (discountCurve.Count < 1) throw new ToolkitException("Must specify some MM rates before first ED Future");
          double prevDf = discountCurve.DiscountFactor(fut.Maturity);

          // Set up curve for start of ED futures deposit period
          int nextIdx = discountCurve.After(fut.Maturity);
          // If futures date before last date in curve, need to clear remaining points
          if (nextIdx < discountCurve.Count) discountCurve.Shrink(nextIdx);
          // If futures date does not match exising point in curve then we need to add a point
          if ((discountCurve.Count <= 0) || (Dt.Cmp(fut.Maturity, discountCurve.GetDt(discountCurve.Count - 1)) != 0))
            discountCurve.Add(fut.Maturity, prevDf);

          // Add discount factor for maturity of this ED futures.
          double rate = 1.0 - tenor.MarketPv;
          // Implement convexity adjustment
          double caAdjustment = 0.0;
          if (caMethod_ == FuturesCAMethod.Manual)
          {
            IModelParameter caCurve;
            if(ModelParameters.TryGetValue(Process.Projection, RateModelParameters.Param.Custom, out caCurve))
              caAdjustment = ModelParameters.Interpolate(fut.Maturity, rate, RateModelParameters.Param.Custom,
                                                         RateModelParameters.Process.Projection);
            else
              caAdjustment = 0.0;
          }
          else if (caMethod_ == FuturesCAMethod.Hull)
          {
            if (VolatilityCurve == null)
              throw new ToolkitException("Attempting to do convexity adjustment for ED Futures without vols");
            double vol = VolatilityCurve.Volatility(fut.Maturity);
            double years = Dt.TimeInYears(AsOf, fut.Maturity);
            double term = Dt.TimeInYears(fut.Maturity, fut.DepositMaturity);
            caAdjustment = ConvexityAdjustments.EDFutures(rate, years, term, vol, caMethod_);
            logger.Debug(String.Format("Tenor {0}, CA vol {1}, years {2}, term {3}, adj {4}", fut.DepositMaturity, vol,
                                       years, term, caAdjustment));
          }
          double df = prevDf*
                      RateCalc.PriceFromRate(rate + caAdjustment, fut.Maturity, fut.DepositMaturity, fut.ReferenceIndex.DayCount,
                                           Frequency.None);
          logger.Debug(String.Format("Tenor {0}, EDFut rate={1}+{2}, df={3}", fut.DepositMaturity, rate, caAdjustment,
                                     df));
          discountCurve.Add(fut.DepositMaturity, df);
        }
      }

      // Bootstrap swap rates
      if (firstSwapDate.IsValid()) swapCalibrator.Fit(discountCurve);

      return;
    }

    // Fit()

    #endregion Calibration

    #region SwapSolverCalibrator

    // class SwapGenericCalibrator

    #region Nested type: CurvePointSetter

    private class CurvePointSetter
    {
      private readonly Curve curve_;
      private readonly Dt[] dates_;
      private readonly double[] vals_;
      private int start_;

      internal CurvePointSetter(OverlayWrapper curve)
      {
        curve_ = curve.CurveToFit.clone();
        int count = curve.Count;
        dates_ = new Dt[count];
        vals_ = new double[count];
        for (int i = 0; i < count; ++i)
        {
          dates_[i] = curve.GetDt(i);
          vals_[i] = curve.GetVal(i);
        }
        start_ = 0;
        curve_.Clear();
      }

      internal void Add(Dt date, double val)
      {
        for (int i = start_; i < dates_.Length; ++i)
        {
          int cmp = Dt.Cmp(dates_[i], date);
          if (cmp > 0)
          {
            curve_.Add(date, val);
            start_ = i;
            return;
          }
          curve_.Add(dates_[i], vals_[i]);
          if (cmp == 0)
          {
            start_ = i + 1;
            return;
          }
        }
        start_ = dates_.Length;
        curve_.Add(date, val);
      }

      internal Curve GetCurve()
      {
        for (int i = start_; i < dates_.Length; ++i) curve_.Add(dates_[i], vals_[i]);
        return curve_;
      }
    }

    #endregion

    #region Nested type: SwapDiscountFn

    private class SwapDiscountFn : SolverFn
    {
      private double baseSumDf_;
      private Dt[] dates_;
      private OverlayWrapper discountCurve_;
      private double[] fractions_;
      private Dt maturity_;
      private double rate_;
      private int start_;
      private int stop_;
      private OverlayWrapper workCurve_;

      public static void FitSwap(OverlayWrapper discountCurve, CouponSchedule swapSchedule, Dt swapMaturity,
                                 double swapRate, DayCount swapDC, Frequency swapFreq)
      {
        var zc = discountCurve;
        int last = zc.Count - 1;
        Dt lastCurveDate = last < 0 ? Dt.Empty : zc.GetDt(last);
        var cps = new CurvePointSetter(zc);

        // We add curve points only when swap maturity
        // is after the last curve date.
        if (lastCurveDate >= swapMaturity) return;

        // Calculate the unchanged portion of sum dfs.
        double baseSumDf = 0;
        Dt[] dates = swapSchedule.Dates;
        double[] fractions = swapSchedule.Fractions;
        int schedCount = swapSchedule.Dates.Length;
        int start = schedCount;
        for (int i = 0; i < schedCount; ++i)
        {
          Dt date = dates[i];
          if (date > lastCurveDate)
          {
            start = i;
            break;
          }
          double df = discountCurve.DiscountFactor(date);
          cps.Add(date, df);
          baseSumDf += df*fractions[i];
        }
        discountCurve.Set(cps.GetCurve());

        {
          int stop;
          double startDf;
          fractions = GetStartDfAndStopIndex(last <= 0 ? new OverlayWrapper(new DiscountCurve(zc.AsOf, swapRate)) : discountCurve,
                                             swapSchedule.Effective, swapMaturity, swapRate, baseSumDf, dates, fractions,
                                             schedCount, start, out stop, out startDf);
          if (stop == start)
          {
            // special case: no need for a solver.
            discountCurve.Add(swapMaturity, startDf);
            return;
          }

          // Now set up a solver fn.
          // We need a solver since the discount factors between start and stop
          // depends on the end discount factor which is what we want to find.
          var fn = new SwapDiscountFn();
          fn.fractions_ = fractions;
          fn.baseSumDf_ = baseSumDf;
          fn.rate_ = swapRate;
          fn.maturity_ = swapMaturity;
          fn.dates_ = dates;
          fn.start_ = start;
          fn.stop_ = stop;
          fn.discountCurve_ = discountCurve;
          fn.workCurve_ = discountCurve.Clone();

          // Now set up a solver
          Solver rf = new Brent2();
          double df = rf.solve(fn, 0.0, startDf - 0.001, startDf + 0.001);
          fn.FillCurve(discountCurve, df);
        }
        return;
      }

      private static double[] GetStartDfAndStopIndex(OverlayWrapper discountCurve, Dt effective, Dt swapMaturity,
                                                     double swapRate, double baseSumDf, Dt[] dates, double[] fractions,
                                                     int schedCount, int start, out int stop, out double startDf)
      {
        double sumDf = baseSumDf;
        stop = start;
        for (int i = start; i < schedCount; ++i)
        {
          Dt date = dates[i];
          int cmp = Dt.Cmp(date, swapMaturity);
          if (cmp == 0)
          {
            stop = i;
            break;
          }
          else if (cmp > 0)
          {
            // special case: swap maturity is not on schedule.
            // We do approximate to the fraction.
            Dt begin = i == 0 ? effective : dates[i - 1];
            double frac = Dt.Diff(begin, swapMaturity)/((double) Dt.Diff(begin, date));
            fractions = (double[]) fractions.Clone();
            fractions[i] *= frac;
            stop = i;
            break;
          }

          sumDf += discountCurve.DiscountFactor(date)*fractions[i];
        }

        startDf = (1 - swapRate*sumDf)/(1 + swapRate*fractions[stop]);
        return fractions;
      }

      public override double evaluate(double x)
      {
        var curve = workCurve_;
        curve.Clear();
        curve.CurveToFit.Set(discountCurve_.CurveToFit);
        double y = FillCurve(curve, x);
        return x - y;
      }

      private double FillCurve(OverlayWrapper curve, double discount)
      {
        int curveIdx = curve.Count;
        curve.Add(maturity_, discount);

        double sumDf = baseSumDf_;
        for (int i = start_; i < stop_; ++i)
        {
          Dt date = dates_[i];
          double df = workCurve_.DiscountFactor(date);
          sumDf += df*fractions_[i];
          curve.Set(curveIdx, date, df);
          curve.Add(maturity_, discount);
          ++curveIdx;
        }
        return (1 - rate_*sumDf)/(1 + rate_*fractions_[stop_]);
      }
    }

    #endregion

    #region Nested type: SwapSolverCalibrator

    /// <summary>
    ///   A generic swap calibrator.
    /// </summary>
    private class SwapSolverCalibrator
    {
      private readonly List<CouponSchedule> schedules_ = new List<CouponSchedule>();
      private readonly List<SwapLeg> swaps_ = new List<SwapLeg>();

      /// <summary>
      ///   Gets the number of swaps.
      /// </summary>
      /// <value>The count.</value>
      public int Count
      {
        get { return swaps_.Count; }
      }

      /// <summary>
      ///   Adds the specified swap.
      /// </summary>
      /// <param name = "swap">The swap.</param>
      public void Add(SwapLeg swap)
      {
        swaps_.Add(swap);
        schedules_.Add(new CouponSchedule(swap));
      }

      /// <summary>
      ///   Fits the discount curve points.
      /// </summary>
      /// <param name = "curve">Discount curve.</param>
      public void Fit(OverlayWrapper curve)
      {
        int count = Count;
        for (int i = 0; i < count; ++i)
        {
          SwapLeg swap = swaps_[i];
          CouponSchedule sched = schedules_[i];
          Dt maturity = sched.LastDate;
          SwapDiscountFn.FitSwap(curve, sched, maturity, swap.Coupon, swap.DayCount, swap.Freq);
        }
        return;
      }

      /// <summary>
      ///   Gets the swap at position idx.
      /// </summary>
      /// <param name = "idx">Index.</param>
      /// <returns>SwapLeg</returns>
      public SwapLeg GetSwap(int idx)
      {
        return swaps_[idx];
      }
    }

    #endregion

    // class SwapSolverFn

    #endregion SwapSolverCalibrator

    #region Schedule Workaround

    /// <summary>
    ///   This class is used to work around the bugs
    ///   in current implementation of schedule.
    /// </summary>
    private class CouponSchedule
    {
      // Assume end of month rule is false.
      private const bool eom = false;
      internal readonly Dt[] Dates;
      internal readonly Dt Effective;
      internal readonly double[] Fractions;

      /// <summary>
      ///   Initializes a new instance of the <see cref = "CouponSchedule" /> class.
      /// </summary>
      /// <param name = "swap">The swap.</param>
      public CouponSchedule(SwapLeg swap)
      {
        // Conventions
        DayCount dc = swap.DayCount;
        Frequency freq = swap.Freq;
        BDConvention roll = swap.BDConvention;
        Calendar cal = swap.Calendar;
        Dt effective = Effective = swap.Effective;
        Dt stop = swap.Maturity;

        // Start with the first coupon date
        Dt[] dates = GetCouponDates(effective, stop, freq, roll, cal);
        var fractions = new double[dates.Length];

        // Add all the intermediate dates
        Dt start = effective;
        for (int i = 0; i < dates.Length; ++i)
        {
          Dt date = dates[i];
          double frac = dc == DayCount.None ? (1.0/(int) freq) : Dt.Fraction(start, date, start, date, dc, freq);
          fractions[i] = frac;
          start = date;
        }

        // We are done.
        Dates = dates;
        Fractions = fractions;
      }

      /// <summary>
      ///   Gets the last date in the dates array..
      /// </summary>
      /// <value>The last date.</value>
      internal Dt LastDate
      {
        get { return Dates == null ? Dt.Empty : Dates[Dates.Length - 1]; }
      }

      /// <summary>
      ///   Get an array of all the coupon dates.
      /// </summary>
      /// <rematks>
      ///   <para>We first step forward from effective by compound frequencies.
      ///     If this meet the maturity date exactly, then we find all the coupon
      ///     dates by stepping forward from the effective.</para>
      /// 
      ///   <para>Otherwise, we step backward from the maturity
      ///     to find all the coupon dates.</para>
      /// 
      ///   <para>The date array excludes the start date, but includes the 
      ///     end date which is adjusted to a business day.</para>
      /// </rematks>
      /// <param name = "begin">Effective date.</param>
      /// <param name = "end">Maturity date.</param>
      /// <param name = "freq">Frequency.</param>
      /// <param name = "roll">Roll convention.</param>
      /// <param name = "cal">Calendar.</param>
      /// <returns>An array of coupon dates.</returns>
      private static Dt[] GetCouponDates(Dt begin, Dt end, Frequency freq, BDConvention roll, Calendar cal)
      {
        // Adjust maturity to a business day, as ISDA CDS model
        // does this.  This is returned as the last date.
        // However, all the work in this function should use the
        // original unadjusted dates.  Otherwise, the output
        // coupon dates are likely to be incorrect.
        Dt adjMaturity = Dt.Roll(end, roll, cal);

        // Find the approximate number of periods.
        int count = Dt.Diff(begin, end)*(int) freq/365;

        // Find the exact number of periods.
        int cmp = GetPeriodCount(begin, end, freq, eom, ref count);

        // Output array
        Dt[] dates;

        // If at most one coupon period....
        if (count <= 1)
        {
          if (cmp >= 0 || count == 0)
          {
            dates = new[] {adjMaturity};
          }
          else
          {
            Dt date = Roll(end, freq, -count, roll, cal, eom);
            if (date < adjMaturity && date > begin) dates = new[] {date, adjMaturity};
            else dates = new[] {adjMaturity};
          }
          return dates;
        }

        // Do we meet the original unadjusted maturity date?
        if (cmp == 0)
        {
          // Yes, we step forward from the start;
          dates = new Dt[count];
          for (int i = 1; i < count; ++i) dates[i - 1] = Roll(begin, freq, i, roll, cal, eom);
          dates[count - 1] = adjMaturity;
          return dates;
        }

        // Now, we need to work backward to find all the coupon
        // dates.  Please note that we have to step back from
        // the unadjusted maturity date.
        if (Roll(end, freq, -count, roll, cal, eom) <= begin)
        {
          // make sure the first date is after effective.
          --count;
        }
        dates = new Dt[count + 1];
        for (int i = 0; i < count; ++i) dates[i] = Roll(end, freq, i - count, roll, cal, eom);
        dates[count] = adjMaturity;
        return dates;
      }

      /// <summary>
      ///   Finds the exact number of periods and returns an
      ///   integer indicating the comparison of the last roll date
      ///   with the maturity date.
      /// </summary>
      /// <param name = "effective">Effective date.</param>
      /// <param name = "maturity">Maturity date.</param>
      /// <param name = "freq">Frequency.</param>
      /// <param name = "eom">if set to <c>true</c>, dates are at the end of month.</param>
      /// <param name = "count">The period count.</param>
      /// <returns></returns>
      private static int GetPeriodCount(Dt effective, Dt maturity, Frequency freq, bool eom, ref int count)
      {
        // Add the periods to effective
        // and compare it with maturity.
        Dt date = Dt.Add(effective, freq, count, eom);
        int cmp = Dt.Cmp(date, maturity);

        // Adjust the periods to be within the
        // range of [effective, maturity].
        while (cmp < 0)
        {
          date = Dt.Add(effective, freq, ++count, eom);
          cmp = Dt.Cmp(date, maturity);
        }
        while (count > 1 && cmp > 0)
        {
          date = Dt.Add(effective, freq, --count, eom);
          cmp = Dt.Cmp(date, maturity);
        }
        return cmp;
      }

      /// <summary>
      ///   Steps from the start date by <c>n</c> compound frequencies,
      ///   and rolls the result date to a business day.
      /// </summary>
      /// <param name = "start">Start Date.</param>
      /// <param name = "freq">Frequency.</param>
      /// <param name = "n">Number of compound frequencies.</param>
      /// <param name = "roll">Business day convention.</param>
      /// <param name = "cal">Calendar.</param>
      /// <param name = "eomRule">if set to <c>true</c> then the result should be
      ///   at the end of the month.</param>
      /// <returns>A date.</returns>
      private static Dt Roll(Dt start, Frequency freq, int n, BDConvention roll, Calendar cal, bool eomRule)
      {
        start = Dt.Add(start, freq, n, eomRule);
        return Dt.Roll(start, roll, cal);
      }
    }

    #endregion Schedule Workaround
  }


  /// <exclude />
  [Serializable]
  public class DiscountBootstrapCalibratorConfig
  {
    /// <exclude />
    [ToolkitConfig("Method to determine swap discount factor (Extrap only).")] public readonly SwapCalibrationMethod
      SwapCalibrationMethod = SwapCalibrationMethod.Extrap;
  }


  /// <exclude />
  public enum SwapCalibrationMethod
  {
    /// <exclude />
    Extrap,
    /// <exclude />
    Solve
  } ;
}

// namespace BaseEntity.Toolkit.Calibrators
