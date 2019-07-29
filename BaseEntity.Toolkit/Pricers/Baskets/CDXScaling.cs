/*
 * CDXScalingHazardRate.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   Parallel scaling the hazard rates survival curves
  ///   to match the index quotes
  /// </summary>
  internal static class CDXScaling
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDXScaling));

    #region Methods
    /// <summary>
    ///   Parallel scaling the hazard rates of survival curves
    ///   to match a given set of index quotes
    /// </summary>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date for pricing</param>
    /// <param name="cdx">List of CDX to base scaling on</param>
    /// <param name="quotes">Market quotes for each CDX</param>
    /// <param name="useTenors">Booleans indicate if a tenor participates the scaling</param>
    /// <param name="marketRecoveryRate">Recovery rate to calculate the market value</param>
    /// <param name="relativeScaling">True to bump relatively (%), false to bump absolutely (bps)</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each name</param>
    /// <param name="scaleOnHazardRates">Bump hazard rate (true) or spread (false)</param>
    /// <param name="failOnInadequateTenors">If true, it fails when neccessary tenors needed on CDS curves to match indeces</param>
    /// <param name="cdxScalingMethod">CDXScalingMethod, Model or Duration</param>
    /// <param name="scales">Bool array to decide which curve needs to be scaled</param>
    /// <param name="scalingFactors">Scaling factors that are out</param>
    /// <param name="scaledSurvivalCurves">Scaled survival curves that are out</param>
    internal static void Scaling(
      Dt asOf,
      Dt settle,
      CDX[] cdx,
      MarketQuote[] quotes,
      bool[] useTenors,
      double marketRecoveryRate,
      bool relativeScaling,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      bool scaleOnHazardRates,
      bool failOnInadequateTenors,
      CDXScalingMethod cdxScalingMethod,
      bool[] scales,
      out double[] scalingFactors,
      out SurvivalCurve[] scaledSurvivalCurves)
    {
      // Find the dates with valid quotes
      Dt[] referenceDates = ArrayUtil.GenerateIf<Dt>(cdx.Length,
        delegate(int i) { return useTenors[i]; },
        delegate(int i) { return cdx[i].Maturity; });

      if (scales == null)
        scales = ArrayUtil.NewArray<bool>(survivalCurves.Length, true);

      // Create an array of SurvivalCurveScaling objects
      SurvivalCurveScaling[] scalings = ArrayUtil.Generate<SurvivalCurveScaling>(
        survivalCurves.Length,
        delegate(int i)
        {
          SurvivalCurve c = (SurvivalCurve)survivalCurves[i].Clone();
          c.Calibrator = (Calibrator)c.Calibrator.Clone();
          if (!scales[i]) return scaleOnHazardRates ? ((SurvivalCurveScaling)new HazardRateScaling(c))
                                                    : ((SurvivalCurveScaling)new SpreadScaling(c));
          ScaledSurvivalCurveBuilder builder =
            new ScaledSurvivalCurveBuilder(c, referenceDates, failOnInadequateTenors);

          // The tenors may include addded tenors
          c.Tenors = builder.Tenors;

          SurvivalCurveScaling scs =
            scaleOnHazardRates
            ? ((SurvivalCurveScaling)new HazardRateScaling(c, builder.CurvePoints, builder.LastStartIndex))
            : ((SurvivalCurveScaling)new SpreadScaling(c, builder.CurvePoints, builder.LastStartIndex));

          // Initialize the scaleUpToIndicator to 0 with length of number of new tenors
          return scs;
        });

      // Get an array of curves referencing the scaled curves
      SurvivalCurve[] scaledCurves = Array.ConvertAll<SurvivalCurveScaling, SurvivalCurve>(
        scalings, delegate(SurvivalCurveScaling b) { return (SurvivalCurve)b.ScaledCurve; });

      // Compute the scaling factors
      double[] factors = ArrayUtil.NewArray(quotes.Length, Double.NaN);
      for (int i = 0, start = 0; i < quotes.Length; ++i)
      {
        if (useTenors[i])
        {
          CDX note = cdx[i];
          if (scaleOnHazardRates)
          {
            if (cdxScalingMethod == CDXScalingMethod.Model)
            {
              double targetValue = MarketValue(note, quotes[i], asOf, settle,
                discountCurve, scaledCurves, marketRecoveryRate);
              ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
                note, asOf, settle, discountCurve, scaledCurves);
              foreach (HazardRateScaling b in scalings) b.SetBumpRange(note.Maturity);
              double x = IndexValueEvaluator.Solve(
                targetValue, pricer, scalings, relativeScaling);
              foreach (HazardRateScaling b in scalings) b.Bump(x, relativeScaling);
              for (; start <= i; ++start) factors[start] = x;
            }
            else if (cdxScalingMethod == CDXScalingMethod.Duration)
            {
              // Simply return the market quotes
              double targetValue = 10000 * MarketSpread(note, quotes[i], asOf, settle,
                discountCurve, scaledCurves, marketRecoveryRate);
              ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
                note, asOf, settle, discountCurve, scaledCurves);
              foreach (HazardRateScaling b in scalings) b.SetBumpRange(note.Maturity);
              double x = IndexProtectionEvaluator.Solve(
                targetValue, pricer, scalings, relativeScaling);
              foreach (HazardRateScaling b in scalings) b.Bump(x, relativeScaling);
              for (; start <= i; ++start) factors[start] = x;
            }
            else if (cdxScalingMethod == CDXScalingMethod.Spread)
            {
              // Simply return the market quotes
              double targetValue = 10000 * MarketSpread(note, quotes[i], asOf, settle,
                discountCurve, scaledCurves, marketRecoveryRate);
              ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
                note, asOf, settle, discountCurve, scaledCurves);
              foreach (HazardRateScaling b in scalings) b.SetBumpRange(note.Maturity);
              double x = IndexSpreadEvaluator.Solve(
                targetValue, pricer, scalings, relativeScaling);
              foreach (HazardRateScaling b in scalings) b.Bump(x, relativeScaling);
              for (; start <= i; ++start) factors[start] = x;
            }

            // Calculate the market quotes implied by the scaled curves
            foreach (SurvivalCurve c in scaledCurves)
              ScaledSurvivalCurveBuilder.SynchronizeCurveQuotes(c);
          }
          else // Scale on the Spread Quotes
          {
            if (cdxScalingMethod == CDXScalingMethod.Model)
            {
              // Compute the target as the equivalent cds pricer's market value for CDX[i]
              double targetValue = MarketValue(note, quotes[i], asOf, settle,
                discountCurve, scaledCurves, marketRecoveryRate);

              // The cdx pricer for the current maturity takes updated scaled curves
              ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
                note, asOf, settle, discountCurve, scaledCurves);

              // Set bump range for each scaling
              foreach (SpreadScaling b in scalings)
                b.SetBumpRange(note.Maturity);
              double x = IndexValueEvaluator.Solve(targetValue, pricer, scalings, relativeScaling);
              for (; start <= i; ++start) factors[start] = x;
            }
            else if (cdxScalingMethod == CDXScalingMethod.Duration)
            {
              double targetValue = 10000 * MarketSpread(note, quotes[i], asOf, settle,
                discountCurve, scaledCurves, marketRecoveryRate);
              ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
                note, asOf, settle, discountCurve, scaledCurves);
              foreach (SpreadScaling b in scalings)
                b.SetBumpRange(note.Maturity);
              double x = IndexProtectionEvaluator.Solve(targetValue, pricer, scalings, relativeScaling);
              for (; start <= i; ++start) factors[start] = x;
            }
            else if (cdxScalingMethod == CDXScalingMethod.Spread)
            {
              double targetValue = 10000 * MarketSpread(note, quotes[i], asOf, settle,
                discountCurve, scaledCurves, marketRecoveryRate);
              ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
                note, asOf, settle, discountCurve, scaledCurves);
              foreach (SpreadScaling b in scalings)
                b.SetBumpRange(note.Maturity);
              double x = IndexSpreadEvaluator.Solve(targetValue, pricer, scalings, relativeScaling);
              for (; start <= i; ++start) factors[start] = x;
            }

            // Since this is bumping on spread quotes, no need to calculate
            // the implied spread.
          }
        }
      } // end for loop

      // Output the results
      scaledSurvivalCurves = scaledCurves;
      scalingFactors = factors;

      return;
    }

    //used to increase accuracy when bumping hazard rates absolutely
    private static double AdjustForAccuracy( SurvivalCurveScaling[] scalings, bool relative, double res)
    {
      if (!relative && scalings.Length > 0 && scalings[0] is HazardRateScaling)
        res /= 100;
      return res;
    }

    // Used for duration method
    private static double MarketSpread(CDX cdx, MarketQuote quote,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves, double recoveryRate)
    {
      if (quote.Type == QuotingConvention.FlatPrice)
      {
        ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
          cdx, asOf, settle, discountCurve, survivalCurves);
        pricer.MarketQuote = quote.Value;
        pricer.MarketRecoveryRate = recoveryRate;
        return pricer.PriceToSpread(quote.Value);
      }
      return quote.Value;
    }

    // Used for Model method
    internal static double MarketValue(CDX cdx, MarketQuote quote,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves, double recoveryRate)
    {
      ICDXPricer pricer = CDXPricerUtil.CreateCdxPricer(
        cdx, asOf, settle, discountCurve, survivalCurves);
      if (quote.Type == QuotingConvention.FlatPrice)
        return CDXPricerUtil.MarketValue(pricer, quote.Value, quote.Type, null);
      // Spread quote: we calculate the market value
      pricer.MarketQuote = quote.Value;
      pricer.MarketRecoveryRate = recoveryRate;
      return pricer.MarketValue();
    }

    #endregion Method

    #region IndexValueEvaluator
    private class IndexValueEvaluator : SolverFn
    {
      private IndexValueEvaluator(
        ICDXPricer pricer, SurvivalCurveScaling[] scalings, bool relative)
      {
        relative_ = relative;
        pricer_ = pricer;
        scalings_ = scalings;
      }
      internal static double Solve(
        double marketValue, ICDXPricer pricer,
        SurvivalCurveScaling[] scalings, bool relative)
      {
        // Set up root finder
        Brent2 rf = new Brent2();
        rf.setToleranceX(ToleranceX);
        rf.setToleranceF(ToleranceF);
        if (MaxIterations > 0)
          rf.setMaxIterations(MaxIterations);
        IndexValueEvaluator fn = new IndexValueEvaluator(pricer, scalings, relative);

        double res = Double.NaN;
        try
        {
          res = rf.solve(fn, marketValue, -0.5, 0.55);
        }
        catch (Exception e)
        {
          logger.DebugFormat("{0}", e.Message);
          throw e;
        }

        res = AdjustForAccuracy(scalings, relative, res);
        return res;
      }

      public override double evaluate(double x)
      {
        x = AdjustForAccuracy(scalings_, relative_, x);
        Parallel.ForEach(scalings_, delegate(SurvivalCurveScaling s)
        {
          s.Bump(x, relative_);
        });
        pricer_.Reset();
        // Reset pricer's survival curves. Some curves are good up to previous tenor
        pricer_.SurvivalCurves = Array.ConvertAll<SurvivalCurveScaling, SurvivalCurve>
          (scalings_, delegate(SurvivalCurveScaling scs) { return scs.ScaledCurve; });

        return pricer_.IntrinsicValue(true);
      }
      private bool relative_;
      private SurvivalCurveScaling[] scalings_;
      private ICDXPricer pricer_;
      const double ToleranceX = 1E-6;
      const double ToleranceF = 1E-8;
      const int MaxIterations = 30;
    }
    #endregion IndexValueEvaluator

    #region IndexProtectionEvaluator
    private class IndexProtectionEvaluator : SolverFn
    {
      private IndexProtectionEvaluator(
        ICDXPricer pricer, SurvivalCurveScaling[] scalings, bool relative)
      {
        relative_ = relative;
        pricer_ = pricer;
        scalings_ = scalings;
      }
      internal static double Solve(
        double marketQuote, ICDXPricer pricer,
        SurvivalCurveScaling[] scalings, bool relative)
      {
        // Set up root finder
        Brent2 rf = new Brent2();
        rf.setToleranceX(ToleranceX);
        rf.setToleranceF(ToleranceF);
        if (MaxIterations > 0)
          rf.setMaxIterations(MaxIterations);
        IndexProtectionEvaluator fn = new IndexProtectionEvaluator(pricer, scalings, relative);

        double res = Double.NaN;
        try
        {
          res = rf.solve(fn, marketQuote, -0.52, 0.52);
        }
        catch (Exception e)
        {
          logger.DebugFormat("{0}", e.Message);
          throw e;
        }
        res = AdjustForAccuracy(scalings, relative, res);
        return res;
      }
      public override double evaluate(double x)
      {
        x = AdjustForAccuracy(scalings_, relative_, x);
        Parallel.ForEach(scalings_, delegate(SurvivalCurveScaling s)
        {
          s.Bump(x, relative_);
        });
        pricer_.Reset();
        pricer_.SurvivalCurves = Array.ConvertAll<SurvivalCurveScaling, SurvivalCurve>
               (scalings_, delegate(SurvivalCurveScaling scs) { return scs.ScaledCurve; });
        
        // Calculate average CDS scaling weights
        double weightedSpread = 0.0;
        double durationSum = 0.0;
        for (int i = 0; i < scalings_.Length; i++)
        {          
          // May change here to use ALL curve. For skipped curve, just use updated saved curve
          CDX cdx = (CDX)pricer_.Product;
          CDSCashflowPricer pricer = CurveUtil.ImpliedPricer(
             scalings_[i].ScaledCurve, cdx.Maturity, cdx.DayCount, 
            cdx.Freq, cdx.BDConvention, cdx.Calendar);

          double weight = cdx.Weights == null ? 1.0 : cdx.Weights[i];
          double duration = pricer.RiskyDuration();
          double spread = pricer.BreakEvenPremium();
          weightedSpread += duration * spread * weight;
          durationSum += duration * weight;
        }
        weightedSpread /= durationSum;
        return weightedSpread*10000;
      }
      private bool relative_;
      private SurvivalCurveScaling[] scalings_;
      private ICDXPricer pricer_;
      const double ToleranceX = 1E-6;
      const double ToleranceF = 1E-8;
      const int MaxIterations = 30;
    }
    #endregion IndexProtectionEvaluator

    #region IndexSpreadEvaluator
    private class IndexSpreadEvaluator : SolverFn
    {
      private IndexSpreadEvaluator(
        ICDXPricer pricer, SurvivalCurveScaling[] scalings, bool relative)
      {
        relative_ = relative;
        pricer_ = pricer;
        scalings_ = scalings;
      }
      internal static double Solve(
        double marketQuote, ICDXPricer pricer,
        SurvivalCurveScaling[] scalings, bool relative)
      {
        // Set up root finder
        Brent2 rf = new Brent2();
        rf.setToleranceX(ToleranceX);
        rf.setToleranceF(ToleranceF);
        if (MaxIterations > 0)
          rf.setMaxIterations(MaxIterations);
        IndexSpreadEvaluator fn = new IndexSpreadEvaluator(pricer, scalings, relative);

        double res = Double.NaN;
        try
        {
          res = rf.solve(fn, marketQuote, -0.2, 0.2);
        }
        catch (Exception e)
        {
          logger.DebugFormat("{0}", e.Message);
          throw;
        }
        res = AdjustForAccuracy(scalings, relative, res);
        return res;
      }
      public override double evaluate(double x)
      {
        x = AdjustForAccuracy(scalings_, relative_, x);
        Parallel.ForEach(scalings_, delegate(SurvivalCurveScaling s)
        {
          s.Bump(x, relative_);
        });
        pricer_.Reset();
        pricer_.SurvivalCurves = Array.ConvertAll<SurvivalCurveScaling, SurvivalCurve>
               (scalings_, delegate(SurvivalCurveScaling scs) { return scs.ScaledCurve; });

        // Calculate average CDS scaling weights
        double weightedSpread = 0.0;
        double weightSum = 0.0;
        for (int i = 0; i < scalings_.Length; i++)
        {
          CDX cdx = (CDX)pricer_.Product;
          // May change here to use ALL curve. For skipped curve, just use updated saved curve
          CDSCashflowPricer pricer = CurveUtil.ImpliedPricer(
            scalings_[i].ScaledCurve, cdx.Maturity, cdx.DayCount,
            cdx.Freq, cdx.BDConvention, cdx.Calendar);

          double weight = cdx.Weights == null ? 1.0 : cdx.Weights[i];
          double spread = pricer.BreakEvenPremium();
          weightedSpread += spread * weight;
          weightSum += weight;
        }
        weightedSpread /= weightSum;
        return weightedSpread * 10000;
      }
      private bool relative_;
      private SurvivalCurveScaling[] scalings_;
      private ICDXPricer pricer_;
      const double ToleranceX = 1E-6;
      const double ToleranceF = 1E-8;
      const int MaxIterations = 30;
    }
    #endregion IndexSpreadEvaluator
  } // class CDXScalingHazardRate

}
