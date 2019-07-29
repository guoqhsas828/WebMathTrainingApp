using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Store raw path results and compute relevant CCR measures
  /// </summary>
  [Serializable]
  internal class CCRMeasureAccumulator : ICCRMeasureAccumulator
  {
    #region Data

    private readonly Dt asOf_;
    private readonly SurvivalCurve cptyCurve_;
    private readonly double[] cptyRecovery_;
    private readonly Dictionary<CCRMeasure, EmpiricalDistribution[]> exposureDistributions_;
    private readonly Dictionary<CCRMeasure, EmpiricalDistribution[]> collateralDistributions_;
    private readonly Dt[] exposureDates_;
    private readonly Dictionary<CCRMeasure, double[]> fundamentalMeasures_;
    private Tuple<Dt[], double[]>[] integrationKernels_;

    private readonly Dictionary<CCRMeasure, double> minConfidenceIntervals_;
    private readonly Dictionary<CCRMeasure, Accumulator[]> negativeAccumulators_;
    private readonly int pathCount_;
    private readonly Dictionary<CCRMeasure, Accumulator[]> positiveAccumulators_;
    private int pathsRun_;

    private const int CPTY_DFLT = 0;
    private const int OWN_DFLT = 1;
    private const int SURVIVAL = 2;
    private const int IGNORE_DFLT = 3; 

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    internal CCRMeasureAccumulator(Dt asOf, Dt[] exposureDates, Tuple<Dt[], double[]>[] integrationKernels,
                                        double[] cptyRecovery, SurvivalCurve cptyCurve, int pathCount)
    {
      asOf_ = asOf;
      exposureDates_ = exposureDates;
      cptyRecovery_ = cptyRecovery;
      integrationKernels_ = integrationKernels;
      pathCount_ = pathCount;
      positiveAccumulators_ = new Dictionary<CCRMeasure, Accumulator[]>();
      negativeAccumulators_ = new Dictionary<CCRMeasure, Accumulator[]>();
      fundamentalMeasures_ = new Dictionary<CCRMeasure, double[]>();
      exposureDistributions_ = new Dictionary<CCRMeasure, EmpiricalDistribution[]>();
      collateralDistributions_ = new Dictionary<CCRMeasure, EmpiricalDistribution[]>();
      minConfidenceIntervals_ = new Dictionary<CCRMeasure, double>();
      cptyCurve_ = cptyCurve;
    }

    #endregion

    /// <summary>
    /// Get or Set integration kernels for default and survival over time
    /// </summary>
    public Tuple<Dt[], double[]>[] IntegrationKernels
    {
      get { return integrationKernels_; }
      set { integrationKernels_ = value; }
    }


    #region incremental data structures

    #region Nested type: AccumulateDelegate

    internal delegate void AccumulateDelegate<T>(T accumulator, SimulatedPathValues p, double e);

    #endregion

    #region Nested type: Accumulator

    [Serializable]
    private abstract class Accumulator
    {
      public int d { get; set; }
      public double pVal { get; set; }
      public RadonNikodymDerivative radonNikodym { get; set; }
      public bool discounted { get; set; }
      public abstract void Merge(Accumulator other);
    }

    #endregion

    #region Nested type: PfeAccumulator

    [Serializable]
    private class PfeAccumulator : Accumulator
    {
      public PfeAccumulator()
      {
        MTMs = new List<double>();
        PDFs = new List<double>();
        Collaterals = new List<double>();
        Mass = Norm = 0.0;
      }

      public int SampleSize { get; set; }
      public double Mass { get; set; }
      public double Norm { get; set; }
      public List<double> MTMs { get; private set; }
      public List<double> Collaterals { get; private set; }
      public List<double> PDFs { get; private set; }

      public void Add(double mtm, double collateral, double pdf)
      {
        MTMs.Add(mtm);
        PDFs.Add(pdf);
        Collaterals.Add(collateral);
      }

      public void Sort()
      {
        var joined = MTMs.Select(((mtm, i) => new {MTM = mtm, Collateral = Collaterals[i], PDF = PDFs[i]})).OrderBy(j => j.MTM).ToList();
        MTMs.Clear();
        Collaterals.Clear();
        PDFs.Clear();
        for (int fromIdx = 0; fromIdx < joined.Count; fromIdx++)
        {
          MTMs.Add(joined[fromIdx].MTM);
          Collaterals.Add(joined[fromIdx].Collateral);
          PDFs.Add(joined[fromIdx].PDF);
          int toIdx = MTMs.Count - 1; 
          // exposures of the same value will be next to each other
          // if any adjacent MTMs are equal, just add pdf
          while (fromIdx < joined.Count - 1 && MTMs[toIdx].AlmostEquals(joined[fromIdx + 1].MTM))
          {
            PDFs[toIdx] += joined[++fromIdx].PDF;
          }
        }
      }

      public override void Merge(Accumulator other)
      {
        var pfeOther = other as PfeAccumulator;
        if (pfeOther == null)
          return;
        if(pfeOther == this)
          throw new ArgumentException("Cannot merge accumulator with self.");
        if (SampleSize == 0)
          SampleSize = pfeOther.SampleSize;
        for (int i = 0; i < pfeOther.MTMs.Count; i++)
        {
          Add(pfeOther.MTMs[i], pfeOther.Collaterals[i], pfeOther.PDFs[i]);
        }
        Mass += pfeOther.Mass;
        Norm += pfeOther.Norm;
      }
    }

    [Serializable]
    private class CollateralAccumulator : PfeAccumulator
    {
    }

    #endregion

    #region Nested type: PvAccumulator

    [Serializable]
    private class PvAccumulator : Accumulator
    {
      public double WeightedExposure { get; set; }
      public double Norm { get; set; }

      public override void Merge(Accumulator other)
      {
        var pvOther = other as PvAccumulator;
        if (pvOther == null)
          return;
        WeightedExposure += pvOther.WeightedExposure;
        Norm += pvOther.Norm;
      }
    }



    [Serializable]
    private class FundingSpreadAccumulator : PvAccumulator
    {
      public double FundingSpread { get; set; }
      
      public override void Merge(Accumulator other)
      {
        var fOther = other as FundingSpreadAccumulator;
        if (fOther == null)
          return;
        WeightedExposure += fOther.WeightedExposure;
        Norm += fOther.Norm;
        FundingSpread += fOther.FundingSpread;
      }
    }
    #endregion

    #region Nested type: ReduceDelegate

    internal delegate double ReduceDelegate<T>(T accumulator);

    #endregion

    #region Nested type: SigmaAccumulator

    [Serializable]
    private class SigmaAccumulator : Accumulator
    {
      public double WeightedExposure { get; set; }
      public double WeightedExposureSquared { get; set; }
      public double Norm { get; set; }

      public override void Merge(Accumulator other)
      {
        var sigmaOther = other as SigmaAccumulator;
        if (sigmaOther == null)
          return;
        WeightedExposure += sigmaOther.WeightedExposure;
        WeightedExposureSquared += sigmaOther.WeightedExposureSquared;
        Norm += sigmaOther.Norm;
      }
    }

    #endregion

    #endregion

    #region Calculations

    private static void AccumulatePv(PvAccumulator accumulator, SimulatedPathValues p, double e)
    {
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt*rnd;
      double df = p.GetDiscountFactor(d);
      accumulator.WeightedExposure += w*df*e;
      accumulator.Norm += accumulator.discounted ? w : w*df;
    }

    private static void AccumulateFundingCost(PvAccumulator accumulator, SimulatedPathValues p, double e)
    {
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt*rnd;
      double df = p.GetDiscountFactor(d);
      accumulator.WeightedExposure += w * df * e * p.GetBorrowSpread(d);
      accumulator.Norm += accumulator.discounted ? w : w * df;
    }


    private static void AccumulateFundingBenefit(PvAccumulator accumulator, SimulatedPathValues p, double e)
    {
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt * rnd;
      double df = p.GetDiscountFactor(d);
      accumulator.WeightedExposure += w * df * e * p.GetLendSpread(d);
      accumulator.Norm += accumulator.discounted ? w : w * df;
    }

   
    private static void AccumulateBorrowSpread(FundingSpreadAccumulator accumulator, SimulatedPathValues p, double e)
    {
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt * rnd;
      double df = p.GetDiscountFactor(d);
      accumulator.WeightedExposure += w * df * e;
      accumulator.FundingSpread += w * p.GetBorrowSpread(d);
      accumulator.Norm += w;
    }

    private static void AccumulateLendSpread(FundingSpreadAccumulator accumulator, SimulatedPathValues p, double e)
    {
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt * rnd;
      double df = p.GetDiscountFactor(d);
      accumulator.WeightedExposure += w * df * e;
      accumulator.FundingSpread += w * p.GetLendSpread(d);
      accumulator.Norm += w;
    }


    private static double ReducePv(PvAccumulator accumulator)
    {
      return (accumulator.Norm <= 0.0) ? 0.0 : accumulator.WeightedExposure/accumulator.Norm;
    }

    private static double ReduceFundingSpread(FundingSpreadAccumulator accumulator)
    {
      var e = (accumulator.Norm <= 0.0) ? 0.0 : accumulator.WeightedExposure / accumulator.Norm;
      var spread = (accumulator.Norm <= 0.0) ? 0.0 : accumulator.FundingSpread / accumulator.Norm;
      return e * spread; 
    }


    private static void AccumulateSigma(SigmaAccumulator accumulator, SimulatedPathValues p, double e)
    {
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt*rnd;
      double df = p.GetDiscountFactor(d);
      accumulator.WeightedExposure += w*df*e;
      accumulator.WeightedExposureSquared += accumulator.discounted ? w*df*df*e*e : w*df*e*e;
      accumulator.Norm += accumulator.discounted ? w : w*df;
    }

    private static double ReduceSigma(SigmaAccumulator accumulator)
    {
      return (accumulator.Norm <= 0.0)
               ? 0.0
               : FloorIfAlmostZero(Math.Sqrt(
                 Math.Max(accumulator.WeightedExposureSquared / accumulator.Norm -
                          (accumulator.WeightedExposure / accumulator.Norm * accumulator.WeightedExposure /
                           accumulator.Norm), 0.0)), 1e-5);
    }

    private static double FloorIfAlmostZero(double value, double threshold)
    {
      return Math.Abs(value) < threshold ? 0 : value;
    }

    private void AccumulatePfe(PfeAccumulator accumulator, SimulatedPathValues p, double e, double collateral)
    {
      accumulator.SampleSize = pathCount_;
      int d = accumulator.d;
      double wt = p.Weight;
      double rnd = accumulator.radonNikodym(p, d);
      double w = wt*rnd;
      if (w <= 0)
        return;
      double df = p.GetDiscountFactor(d);
      if (accumulator.discounted)
        e *= df;
      else
        w *= df;
      accumulator.Norm += w;
      if (e > 0)
      {
        accumulator.Add(e, collateral, w);
        accumulator.Mass += w;
      }
    }

    private static Tuple<EmpiricalDistribution,EmpiricalDistribution> BuildDistribution(PfeAccumulator accumulator)
    {
      if (accumulator.Norm <= 0.0)
        return new Tuple<EmpiricalDistribution, EmpiricalDistribution>(new EmpiricalDistribution(new[] { 0.0 }, new[] { 1.0 }), new EmpiricalDistribution(new[] { 0.0 }, new[] { 1.0 }));
      accumulator.Sort();
      if (accumulator.Mass < accumulator.Norm)
      {
        accumulator.MTMs.Insert(0, 0.0);
        accumulator.Collaterals.Insert(0, 0.0);
        accumulator.PDFs.Insert(0, accumulator.Norm - accumulator.Mass);
      }
      var x = accumulator.MTMs.ToArray();
      var c = accumulator.Collaterals.ToArray();
      double[] cdf = accumulator.PDFs.ToArray();
      cdf[0] /= accumulator.Norm;
      for (int i = 1; i < x.Length; i++)
        cdf[i] = cdf[i - 1] + cdf[i]/accumulator.Norm;
      var exposureDist = new EmpiricalDistribution(x, cdf);
      var collateralDist = new EmpiricalDistribution(c, cdf);
      return new Tuple<EmpiricalDistribution, EmpiricalDistribution>(exposureDist, collateralDist);
    }

    #endregion

    #region ICounterpartyCreditRiskCalculations Methods

    private void AddFundamentalMeasure<T>(CCRMeasure measure, double ci, RadonNikodymDerivative radonNikodym,
                                          bool discount, bool negative) where T : Accumulator, new()
    {
      var accumulators = ArrayUtil.Generate(exposureDates_.Length,
                                            i =>
                                            new T
                                              {
                                                d = i,
                                                pVal = ci,
                                                radonNikodym = radonNikodym,
                                                discounted = discount
                                              });
      if (measure == CCRMeasure.PFE ||
          measure == CCRMeasure.PFE0 ||
          measure == CCRMeasure.DiscountedPFE ||
          measure == CCRMeasure.DiscountedPFE0 ||
          measure == CCRMeasure.EC ||
          measure == CCRMeasure.EC0 ||
          measure == CCRMeasure.PFCSA ||
          measure == CCRMeasure.PFNCSA ||
          measure == CCRMeasure.PFNE ||
          measure == CCRMeasure.DiscountedPFNE 
        )
      {
        if (!minConfidenceIntervals_.ContainsKey(measure) || minConfidenceIntervals_[measure] > ci)
        {
          minConfidenceIntervals_[measure] = ci;
          if (negative)
            negativeAccumulators_[measure] = accumulators;
          else
            positiveAccumulators_[measure] = accumulators;
        }
      }
      else
      {
        if (negative)
          negativeAccumulators_[measure] = accumulators;
        else
          positiveAccumulators_[measure] = accumulators;
      }
      
    }

    /// <summary>
    /// Add a CCR measure to be accumulated during simulation
    /// </summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="ci">Confidence level (required only for tail measures)</param>
    public void AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      switch (measure)
      {
        case CCRMeasure.EE:
        case CCRMeasure.EEE:
        case CCRMeasure.EPE:
        case CCRMeasure.EEPE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.EE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.EE, ci, CptyRn, false, false);
          break;
        case CCRMeasure.EPV:
          if (!positiveAccumulators_.ContainsKey(measure))
            AddFundamentalMeasure<PvAccumulator>(measure, ci, ZeroRn, false, false);
          break;
        case CCRMeasure.EE0:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.EE0))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.EE0, ci, ZeroRn, false, false);
          break;
        case CCRMeasure.CVA:
        case CCRMeasure.CVATheta:
        case CCRMeasure.DiscountedEE:
        case CCRMeasure.EffectiveMaturity:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE, ci, CptyRn, true, false);
          break;
        case CCRMeasure.RWA:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE, ci, CptyRn, true, false);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.EE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.EE, ci, CptyRn, false, false);
          break;
        case CCRMeasure.RWA0:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE0))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE0, ci, ZeroRn, true, false);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.EE0))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.EE0, ci, ZeroRn, false, false);
          break;
        case CCRMeasure.DiscountedEPV:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEPV))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEPV, ci, ZeroRn, true, false);
          break;
        case CCRMeasure.CVA0:
        case CCRMeasure.DiscountedEE0:
        case CCRMeasure.EffectiveMaturity0:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE0))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE0, ci, ZeroRn, true, false);
          break;
        case CCRMeasure.NEE:
        case CCRMeasure.ENE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.NEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.NEE, ci, OwnRn, false, true);
          break;
        case CCRMeasure.DVA:
        case CCRMeasure.DVATheta:
        case CCRMeasure.DiscountedNEE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedNEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedNEE, ci, OwnRn, true, true);
          break;
        case CCRMeasure.NEE0:
          if (!positiveAccumulators_.ContainsKey(measure))
            AddFundamentalMeasure<PvAccumulator>(measure, ci, ZeroRn, false, true);
          break;
        case CCRMeasure.DVA0:
        case CCRMeasure.DiscountedNEE0:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedNEE0))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedNEE0, ci, ZeroRn, true, true);
          break;
        case CCRMeasure.FCA:
        case CCRMeasure.FCATheta:
          if (!positiveAccumulators_.ContainsKey(measure))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.FCA, ci, FundingRn, true, false);
          break;
        case CCRMeasure.FCA0:
        case CCRMeasure.FCANoDefault:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.FCA0))
            AddFundamentalMeasure<FundingSpreadAccumulator>(CCRMeasure.FCA0, ci, ZeroRn, true, false);
          break;
        case CCRMeasure.FBA:
        case CCRMeasure.FBATheta:
          if (!negativeAccumulators_.ContainsKey(measure))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.FBA, ci, FundingRn, true, true);
          break;
        case CCRMeasure.FBA0:
        case CCRMeasure.FBANoDefault:
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.FBA0))
            AddFundamentalMeasure<FundingSpreadAccumulator>(CCRMeasure.FBA0, ci, ZeroRn, true, true);
          break;
        case CCRMeasure.FVA:
        case CCRMeasure.FVATheta:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.FCA))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.FCA, ci, FundingRn, true, false);
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.FBA))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.FBA, ci, FundingRn, true, true);
          break;
        case CCRMeasure.FVA0:
        case CCRMeasure.FVANoDefault:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.FCA0))
            AddFundamentalMeasure<FundingSpreadAccumulator>(CCRMeasure.FCA0, ci, ZeroRn, true, false);
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.FBA0))
            AddFundamentalMeasure<FundingSpreadAccumulator>(CCRMeasure.FBA0, ci, ZeroRn, true, true);
          break;
        case CCRMeasure.PFE:
        case CCRMeasure.MPFE:
        case CCRMeasure.PFCSA:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.PFE) || minConfidenceIntervals_[CCRMeasure.PFE] > ci)
            AddFundamentalMeasure<PfeAccumulator>(CCRMeasure.PFE, ci, CptyRn, false, false);
          break;
        case CCRMeasure.DiscountedPFE:
          if (!positiveAccumulators_.ContainsKey(measure) || minConfidenceIntervals_[CCRMeasure.DiscountedPFE] > ci)
            AddFundamentalMeasure<PfeAccumulator>(measure, ci, CptyRn, true, false);
          break;
        case CCRMeasure.PFE0:
          if (!positiveAccumulators_.ContainsKey(measure) || minConfidenceIntervals_[CCRMeasure.PFE0] > ci)
            AddFundamentalMeasure<PfeAccumulator>(measure, ci, ZeroRn, false, false);
          break;
        case CCRMeasure.DiscountedPFE0:
          if (!positiveAccumulators_.ContainsKey(measure) || minConfidenceIntervals_[CCRMeasure.DiscountedPFE0] > ci)
            AddFundamentalMeasure<PfeAccumulator>(measure, ci, ZeroRn, true, false);
          break;
        case CCRMeasure.PFNE:
        case CCRMeasure.MPFNE:
        case CCRMeasure.PFNCSA:
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.PFNE) || minConfidenceIntervals_[CCRMeasure.PFNE] > ci)
            AddFundamentalMeasure<PfeAccumulator>(CCRMeasure.PFNE, ci, OwnRn, false, true);
          break;
        case CCRMeasure.DiscountedPFNE:
          if (!negativeAccumulators_.ContainsKey(measure) || minConfidenceIntervals_[CCRMeasure.DiscountedPFNE] > ci)
            AddFundamentalMeasure<PfeAccumulator>(measure, ci, OwnRn, true, true);
          break;
        case CCRMeasure.Sigma:
        case CCRMeasure.SigmaDiscountedEE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.SigmaDiscountedEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaDiscountedEE, ci, CptyRn, true, false);
          break;
        case CCRMeasure.StdErrDiscountedEE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.SigmaDiscountedEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaDiscountedEE, ci, CptyRn, true, false);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE, ci, CptyRn, true, false);
          break;
        case CCRMeasure.SigmaEE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.SigmaEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaEE, ci, CptyRn, false, false);
          break;
        case CCRMeasure.StdErrEE:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.SigmaEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaEE, ci, CptyRn, false, false);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.EE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.EE, ci, CptyRn, false, false);
          break;
        case CCRMeasure.SigmaNEE:
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.SigmaNEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaNEE, ci, OwnRn, false, true);
          break;
        case CCRMeasure.StdErrNEE:
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.SigmaNEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaNEE, ci, OwnRn, false, true);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.NEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.NEE, ci, OwnRn, false, true);
          break;
        case CCRMeasure.SigmaDiscountedNEE:
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.SigmaDiscountedNEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaDiscountedNEE, ci, OwnRn, true, true);
          break;
        case CCRMeasure.StdErrDiscountedNEE:
          if (!negativeAccumulators_.ContainsKey(CCRMeasure.SigmaDiscountedNEE))
            AddFundamentalMeasure<SigmaAccumulator>(CCRMeasure.SigmaDiscountedNEE, ci, OwnRn, true, true);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedNEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedNEE, ci, OwnRn, true, true);
          
          break;
        case CCRMeasure.EC:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE, ci, CptyRn, true, false);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedPFE) ||
              minConfidenceIntervals_[CCRMeasure.DiscountedPFE] > ci)
            AddFundamentalMeasure<PfeAccumulator>(CCRMeasure.DiscountedPFE, ci, CptyRn, true, false);
          break;
        case CCRMeasure.EC0:
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedEE0))
            AddFundamentalMeasure<PvAccumulator>(CCRMeasure.DiscountedEE0, ci, ZeroRn, true, false);
          if (!positiveAccumulators_.ContainsKey(CCRMeasure.DiscountedPFE0) ||
              minConfidenceIntervals_[CCRMeasure.DiscountedPFE0] > ci)
            AddFundamentalMeasure<PfeAccumulator>(CCRMeasure.DiscountedPFE0, ci, ZeroRn, true, false);
          break;
        default:
          throw new NotSupportedException(String.Format("Measure {0} not implemented", measure));
      }
    }

    /// <summary>
    /// Check if a CCR measure has been accumulated during simulation
    /// </summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="ci">Confidence level (required only for tail measures)</param>
    public bool HasMeasureAccumulator(CCRMeasure measure, double ci)
    {
      if (!fundamentalMeasures_.Any())
        ReduceCumulativeValues();

      switch (measure)
      {
        case CCRMeasure.EE:
        case CCRMeasure.EEE:
        case CCRMeasure.EPE:
        case CCRMeasure.EEPE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.EE);
        case CCRMeasure.NEE:
        case CCRMeasure.ENE: 
          return fundamentalMeasures_.ContainsKey(CCRMeasure.NEE);
        case CCRMeasure.NEE0:
        case CCRMeasure.EE0:
        case CCRMeasure.DiscountedEPV:
        case CCRMeasure.EPV:
          return fundamentalMeasures_.ContainsKey(measure);
        case CCRMeasure.CVA:
        case CCRMeasure.CVATheta:
        case CCRMeasure.DiscountedEE:
        case CCRMeasure.EffectiveMaturity:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE);
        case CCRMeasure.RWA:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE) && fundamentalMeasures_.ContainsKey(CCRMeasure.EE);
        case CCRMeasure.RWA0:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE0) && fundamentalMeasures_.ContainsKey(CCRMeasure.EE0);
        case CCRMeasure.CVA0:
        case CCRMeasure.DiscountedEE0:
        case CCRMeasure.EffectiveMaturity0:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE0);
        case CCRMeasure.DVA:
        case CCRMeasure.DVATheta:
        case CCRMeasure.DiscountedNEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedNEE);
        case CCRMeasure.DVA0:
        case CCRMeasure.DiscountedNEE0:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedNEE0);
        case CCRMeasure.FCA0:
        case CCRMeasure.FCANoDefault:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.FCA0);
        case CCRMeasure.FCA:
        case CCRMeasure.FCATheta:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.FCA);
        case CCRMeasure.FBA:
        case CCRMeasure.FBATheta:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.FBA);
        case CCRMeasure.FBA0:
        case CCRMeasure.FBANoDefault:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.FBA0);
        case CCRMeasure.FVA:
        case CCRMeasure.FVATheta:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.FCA) && fundamentalMeasures_.ContainsKey(CCRMeasure.FBA);
        case CCRMeasure.FVA0:
        case CCRMeasure.FVANoDefault:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.FCA0) &&
                 fundamentalMeasures_.ContainsKey(CCRMeasure.FBA0);
        case CCRMeasure.PFE:
        case CCRMeasure.MPFE:
        case CCRMeasure.PFCSA:
          return (exposureDistributions_.ContainsKey(CCRMeasure.PFE) && minConfidenceIntervals_[CCRMeasure.PFE] <= ci);
        case CCRMeasure.DiscountedPFE:
          return (exposureDistributions_.ContainsKey(CCRMeasure.DiscountedPFE) && minConfidenceIntervals_[CCRMeasure.DiscountedPFE] <= ci);
        case CCRMeasure.PFE0:
          return (exposureDistributions_.ContainsKey(CCRMeasure.PFE0) && minConfidenceIntervals_[CCRMeasure.PFE0] <= ci);
        case CCRMeasure.DiscountedPFE0:
          return (exposureDistributions_.ContainsKey(CCRMeasure.DiscountedPFE0) && minConfidenceIntervals_[CCRMeasure.DiscountedPFE0] <= ci);
        case CCRMeasure.PFNE:
        case CCRMeasure.MPFNE:
        case CCRMeasure.PFNCSA:
          return exposureDistributions_.ContainsKey(CCRMeasure.PFNE) && minConfidenceIntervals_[CCRMeasure.PFNE] <= ci;
        case CCRMeasure.DiscountedPFNE:
          return exposureDistributions_.ContainsKey(CCRMeasure.DiscountedPFNE) && minConfidenceIntervals_[CCRMeasure.DiscountedPFNE] <= ci;
        case CCRMeasure.EC:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE) &&
                 exposureDistributions_.ContainsKey(CCRMeasure.DiscountedPFE) &&
                 minConfidenceIntervals_[CCRMeasure.DiscountedPFE] <= ci;
        case CCRMeasure.EC0:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE0) && 
                 exposureDistributions_.ContainsKey(CCRMeasure.DiscountedPFE0) &&
                 minConfidenceIntervals_[CCRMeasure.DiscountedPFE0] <= ci;
        case CCRMeasure.Sigma:
        case CCRMeasure.SigmaDiscountedEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaDiscountedEE);
        case CCRMeasure.StdErrDiscountedEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaDiscountedEE) && fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedEE);
        case CCRMeasure.SigmaEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaEE);
        case CCRMeasure.StdErrEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaEE) && fundamentalMeasures_.ContainsKey(CCRMeasure.EE);
        case CCRMeasure.SigmaNEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaNEE);
        case CCRMeasure.StdErrNEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaNEE) && fundamentalMeasures_.ContainsKey(CCRMeasure.NEE);
        case CCRMeasure.SigmaDiscountedNEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaDiscountedNEE); 
        case CCRMeasure.StdErrDiscountedNEE:
          return fundamentalMeasures_.ContainsKey(CCRMeasure.SigmaDiscountedNEE) && fundamentalMeasures_.ContainsKey(CCRMeasure.DiscountedNEE);
        default:
          throw new NotSupportedException(String.Format("Measure {0} not implemented", measure));
      }
    }

    public void AccumulateExposures(SimulatedPathValues path, int dateIdx, double positiveExposure, double positiveCollateral, double negativeExposure,
      double negativeCollateral)
    {
      var posExposurePoint = new PathWiseExposure.ExposurePoint() {Path = path, PathIdx = path.Id, DateIdx = dateIdx, Exposure = positiveExposure, Collateral = positiveCollateral, FundingExposure = positiveExposure};
      var negExposurePoint = new PathWiseExposure.ExposurePoint() { Path = path, PathIdx = path.Id, DateIdx = dateIdx, Exposure = negativeExposure, Collateral = negativeCollateral, FundingExposure = negativeExposure};
      AccumulateExposures(posExposurePoint, negExposurePoint); 
    }

    public void AccumulateExposures(PathWiseExposure.ExposurePoint positiveExposurePoint, PathWiseExposure.ExposurePoint negativeExposurePoint)
    {
      var dateIdx = positiveExposurePoint.DateIdx;
      var path = positiveExposurePoint.Path;
      var positiveExposure = positiveExposurePoint.Exposure;
      var positiveCollateral = positiveExposurePoint.Collateral;
      var negativeExposure = negativeExposurePoint.Exposure;
      var negativeCollateral = negativeExposurePoint.Collateral;

      foreach (var key in positiveAccumulators_.Keys)
      {
        var accumulators = positiveAccumulators_[key];
        if (accumulators is PvAccumulator[])
        {
          if (key == CCRMeasure.FCA)
          {
            AccumulateFundingCost((PvAccumulator)accumulators[dateIdx], path, positiveExposurePoint.FundingExposure);
          }
          else if (key == CCRMeasure.FCA0)
          {
            AccumulateBorrowSpread((FundingSpreadAccumulator)accumulators[dateIdx], path, positiveExposurePoint.FundingExposure);  
          }
          else if (key == CCRMeasure.EPV || key == CCRMeasure.DiscountedEPV) 
          {
            AccumulatePv((PvAccumulator)accumulators[dateIdx], path, positiveExposure + positiveCollateral - negativeExposure - negativeCollateral);
          }
          else
          {
            AccumulatePv((PvAccumulator) accumulators[dateIdx], path, positiveExposure);
          }
        }
        else if (accumulators is PfeAccumulator[])
        {
          AccumulatePfe((PfeAccumulator) accumulators[dateIdx], path, positiveExposure, positiveCollateral);
        }
        else if (accumulators is SigmaAccumulator[])
        {
          AccumulateSigma((SigmaAccumulator) accumulators[dateIdx], path, positiveExposure);
        }
      }

      foreach (var key in negativeAccumulators_.Keys)
      {
        var accumulators = negativeAccumulators_[key];
        if (key == CCRMeasure.FBA)
        {
          AccumulateFundingBenefit((PvAccumulator)accumulators[dateIdx], path, negativeExposurePoint.FundingExposure);
        }
        else if (key == CCRMeasure.FBA0)
        {
          AccumulateLendSpread((FundingSpreadAccumulator)accumulators[dateIdx], path, negativeExposurePoint.FundingExposure); 
        }
        else if (accumulators is PvAccumulator[])
        {
          AccumulatePv((PvAccumulator) accumulators[dateIdx], path, negativeExposure);
        }
        else if (accumulators is PfeAccumulator[])
        {
          AccumulatePfe((PfeAccumulator) accumulators[dateIdx], path, negativeExposure, negativeCollateral);
        }
        else if (accumulators is SigmaAccumulator[])
        {
          AccumulateSigma((SigmaAccumulator) accumulators[dateIdx], path, negativeExposure);
        }
      }
      pathsRun_++;
    }

    public void MergeCumulativeValues(CCRMeasureAccumulator other)
    {
      foreach (var key in other.positiveAccumulators_.Keys)
      {
        if (!positiveAccumulators_.ContainsKey(key))
          throw new ToolkitException("key {0} is not present in merge target IncrementalCCRCalculations", key);
        for (int i = 0; i < other.positiveAccumulators_[key].Length; i++)
        {
          var otherAccumulator = other.positiveAccumulators_[key][i];
          positiveAccumulators_[key][i].Merge(otherAccumulator);
        }
      }

      foreach (var key in other.negativeAccumulators_.Keys)
      {
        if (!negativeAccumulators_.ContainsKey(key))
          throw new ToolkitException("key {0} is not present in merge target IncrementalCCRCalculations", key);
        for (int i = 0; i < other.negativeAccumulators_[key].Length; i++)
        {
          var otherAccumulator = other.negativeAccumulators_[key][i];
          negativeAccumulators_[key][i].Merge(otherAccumulator);
        }
      }
      pathsRun_ += other.pathsRun_;
    }


    public double GetMeasure(CCRMeasure measure, Dt date, double ci)
    {
      return GetMeasure(measure, date, ci, null);
    }

    public double GetMeasure(CCRMeasure measure, Dt date, double ci, double[] multipliers)
    {
      //if (pathsRun_ != pathCount_)
      //  throw new ToolkitException("IncrementalCCRCalculations in invalid state. GetMeasure called before all paths have been run.");
      if (!fundamentalMeasures_.Any())
        ReduceCumulativeValues();

      double retVal = 0.0;
      switch (measure)
      {
        case CCRMeasure.CVA:
          if (integrationKernels_.Length >= 1)
          {
            retVal =
              -Integrate(CCRMeasure.DiscountedEE, ci, integrationKernels_[CPTY_DFLT], cptyRecovery_[0], multipliers);
          }
          break;
        case CCRMeasure.CVATheta:
          if (integrationKernels_.Length >= 1)
          {
            retVal =
              IntegrateTheta(CCRMeasure.DiscountedEE, ci, integrationKernels_[CPTY_DFLT], date, cptyRecovery_[0], multipliers);
          }
          break;
        case CCRMeasure.DVA:
          if (integrationKernels_.Length >= 2)
          {
            retVal = Integrate(CCRMeasure.DiscountedNEE, ci, integrationKernels_[OWN_DFLT], cptyRecovery_[1], multipliers);
          }
          break;
        case CCRMeasure.DVATheta:
          if (integrationKernels_.Length >= 2)
          {
            retVal = -IntegrateTheta(CCRMeasure.DiscountedNEE, ci, integrationKernels_[OWN_DFLT], date, cptyRecovery_[1], multipliers);
          }
          break;
        case CCRMeasure.FCA:
          if (integrationKernels_.Length >= 3)
          {
            retVal = -Integrate(CCRMeasure.FCA, ci, integrationKernels_[SURVIVAL], 0.0, multipliers);
          }
          break;
        case CCRMeasure.FCATheta:
          if (integrationKernels_.Length >= 3)
          {
            retVal = IntegrateTheta(CCRMeasure.FCA, ci, integrationKernels_[SURVIVAL], date, 0.0, multipliers);
          }
          break;
        case CCRMeasure.FBA:
          if (integrationKernels_.Length >= 3) 
          {
            retVal = Integrate(CCRMeasure.FBA, ci, integrationKernels_[SURVIVAL], 0.0, multipliers);
          }
          break;
        case CCRMeasure.FVA:
          retVal = this.GetMeasure(CCRMeasure.FBA, date, ci, multipliers) + this.GetMeasure(CCRMeasure.FCA, date, ci, multipliers);
          break;
        case CCRMeasure.FBATheta:
          if (integrationKernels_.Length >= 3)
          {
            retVal = -IntegrateTheta(CCRMeasure.FBA, ci, integrationKernels_[SURVIVAL], date, 0.0, multipliers);
          }
          break;
        case CCRMeasure.CVA0:
          if (integrationKernels_.Length >= 1)
          {
            retVal =
              -Integrate(CCRMeasure.DiscountedEE0, ci, integrationKernels_[CPTY_DFLT], cptyRecovery_[0], multipliers);
          }
          break;
        case CCRMeasure.DVA0:
          if (integrationKernels_.Length >= 2)
          {
            retVal = Integrate(CCRMeasure.DiscountedNEE0, ci, integrationKernels_[OWN_DFLT], cptyRecovery_[1], multipliers);
          }
          break;
        case CCRMeasure.FCA0:
          if (integrationKernels_.Length >= 3)
          {
            retVal = -Integrate(CCRMeasure.FCA0, ci, integrationKernels_[SURVIVAL], 0.0, multipliers);
          }
          break;
        case CCRMeasure.FBA0:
          if (integrationKernels_.Length >= 3)
          {
            retVal = Integrate(CCRMeasure.FBA0, ci, integrationKernels_[SURVIVAL], 0.0, multipliers);
          }
          break;
        case CCRMeasure.FVA0:
          retVal = this.GetMeasure(CCRMeasure.FBA0, date, ci, multipliers) + this.GetMeasure(CCRMeasure.FCA0, date, ci, multipliers);
          break;
        case CCRMeasure.FCANoDefault:
          if (integrationKernels_.Length >= 4)
          {
            retVal = -Integrate(CCRMeasure.FCA0, ci, integrationKernels_[IGNORE_DFLT], 0.0, multipliers);
          }
          break;
        case CCRMeasure.FBANoDefault:
          if (integrationKernels_.Length >= 4)
          {
            retVal = Integrate(CCRMeasure.FBA0, ci, integrationKernels_[IGNORE_DFLT], 0.0, multipliers);
          }
          break;
        
        case CCRMeasure.EC:
          if (integrationKernels_.Length >= 1)
          {
            retVal =
              -Integrate(measure, ci, integrationKernels_[CPTY_DFLT], cptyRecovery_[0], multipliers);
          }
          break;
        case CCRMeasure.EC0:
          if (integrationKernels_.Length >= 1)
          {
            retVal =
              -Integrate(measure, ci, integrationKernels_[CPTY_DFLT], cptyRecovery_[0], multipliers);
          }
          break;
        case CCRMeasure.DiscountedEPV:
        case CCRMeasure.EPV:
        case CCRMeasure.EE:
        case CCRMeasure.EE0:
        case CCRMeasure.DiscountedEE:
        case CCRMeasure.DiscountedEE0:
        case CCRMeasure.NEE:
        case CCRMeasure.DiscountedNEE:
        case CCRMeasure.NEE0:
        case CCRMeasure.DiscountedNEE0:
        case CCRMeasure.PFE:
        case CCRMeasure.DiscountedPFE:
        case CCRMeasure.PFE0:
        case CCRMeasure.DiscountedPFE0:
        case CCRMeasure.PFCSA:
        case CCRMeasure.PFNCSA:
        case CCRMeasure.PFNE:
        case CCRMeasure.DiscountedPFNE:
        case CCRMeasure.SigmaEE:
        case CCRMeasure.SigmaNEE:
        case CCRMeasure.SigmaDiscountedNEE:
          retVal = Interpolate(measure, date, ci);
          break;
        case CCRMeasure.Sigma:
        case CCRMeasure.SigmaDiscountedEE:
          retVal = Interpolate(CCRMeasure.SigmaDiscountedEE, date, ci);
          break;
        case CCRMeasure.StdErrDiscountedEE:
          retVal = Interpolate(CCRMeasure.SigmaDiscountedEE, date, ci) / (Math.Sqrt(pathCount_) * Interpolate(CCRMeasure.DiscountedEE, date, ci));
          break;
        case CCRMeasure.StdErrEE:
          retVal = Interpolate(CCRMeasure.SigmaEE, date, ci) / (Math.Sqrt(pathCount_) * Interpolate(CCRMeasure.EE, date, ci));
          break;
        case CCRMeasure.StdErrDiscountedNEE:
          retVal = Interpolate(CCRMeasure.SigmaDiscountedNEE, date, ci) / (Math.Sqrt(pathCount_) * Interpolate(CCRMeasure.DiscountedNEE, date, ci));
          break;
        case CCRMeasure.StdErrNEE:
          retVal = Interpolate(CCRMeasure.SigmaNEE, date, ci) / (Math.Sqrt(pathCount_) * Interpolate(CCRMeasure.NEE, date, ci));
          break;
        case CCRMeasure.EEE:
          retVal = RunningMax(CCRMeasure.EE, date, ci);
          break;
        case CCRMeasure.EPE:
          retVal = TimeAverage(CCRMeasure.EE, date, ci);
          break;
        case CCRMeasure.ENE:
          retVal = TimeAverage(CCRMeasure.NEE, date, ci);
          break;
        case CCRMeasure.EEPE:
          retVal = TimeAverage((t) => RunningMax(CCRMeasure.EE, exposureDates_[t], ci), date);
          break;
        case CCRMeasure.MPFE:
          retVal = RunningMax(CCRMeasure.PFE, date, ci);
          break;
        case CCRMeasure.MPFNE:
          retVal = RunningMax(CCRMeasure.PFNE, date, ci);
          break;
        case CCRMeasure.RWA:
        case CCRMeasure.RWA0:
          retVal = RiskWeightedAssets(measure);
          break;
        case CCRMeasure.EffectiveMaturity:
          retVal = EffectiveMaturity(MeasureFunction(CCRMeasure.DiscountedEE, 0.0));
          break;
        case CCRMeasure.EffectiveMaturity0:
          retVal = EffectiveMaturity(MeasureFunction(CCRMeasure.DiscountedEE0, 0.0));
          break;
        default:
          throw new NotSupportedException(String.Format("Measure {0} not implemented", measure));
      }
      return retVal;
    }

    #endregion

    #region Utils

    public void ReduceCumulativeValues()
    {
      foreach (CCRMeasure key in positiveAccumulators_.Keys)
      {
        Accumulator[] accumulators = positiveAccumulators_[key];
        if (accumulators is FundingSpreadAccumulator[])
        {
          var results = new double[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as FundingSpreadAccumulator;
            results[i] = ReduceFundingSpread(accumulator);
          }
          fundamentalMeasures_[key] = results;
        }
        else if (accumulators is PvAccumulator[])
        {
          var results = new double[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as PvAccumulator;
            results[i] = ReducePv(accumulator);
          }
          fundamentalMeasures_[key] = results;
        }
        else if (accumulators is PfeAccumulator[])
        {
          var exposureDists = new EmpiricalDistribution[accumulators.Length];
          var collateralDists = new EmpiricalDistribution[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as PfeAccumulator;
            var results = BuildDistribution(accumulator);
            exposureDists[i] = results.Item1;
            collateralDists[i] = results.Item2;
          }
          exposureDistributions_[key] = exposureDists;
          if(key == CCRMeasure.PFE)
            collateralDistributions_[CCRMeasure.PFCSA] = collateralDists;
          
        }
        else if (accumulators is SigmaAccumulator[])
        {
          var results = new double[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as SigmaAccumulator;
            results[i] = ReduceSigma(accumulator);
          }
          fundamentalMeasures_[key] = results;
        }
      }

      foreach (CCRMeasure key in negativeAccumulators_.Keys)
      {
        Accumulator[] accumulators = negativeAccumulators_[key];
        if (accumulators is FundingSpreadAccumulator[])
        {
          var results = new double[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as FundingSpreadAccumulator;
            results[i] = ReduceFundingSpread(accumulator);
          }
          fundamentalMeasures_[key] = results;
        }
        else if (accumulators is PvAccumulator[])
        {
          var results = new double[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as PvAccumulator;
            results[i] = ReducePv(accumulator);
          }
          fundamentalMeasures_[key] = results;
        }
        else if (accumulators is PfeAccumulator[])
        {
          var exposureDists = new EmpiricalDistribution[accumulators.Length];
          var collateralDists = new EmpiricalDistribution[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as PfeAccumulator;
            var results = BuildDistribution(accumulator);
            exposureDists[i] = results.Item1;
            collateralDists[i] = results.Item2;
          }
          exposureDistributions_[key] = exposureDists;
          if (key == CCRMeasure.PFNE)
            collateralDistributions_[CCRMeasure.PFNCSA] = collateralDists;
        }
        else if (accumulators is SigmaAccumulator[])
        {
          var results = new double[accumulators.Length];
          for (int i = 0; i < accumulators.Length; i++)
          {
            var accumulator = accumulators[i] as SigmaAccumulator;
            results[i] = ReduceSigma(accumulator);
          }
          fundamentalMeasures_[key] = results;
        }
      }
      // we can clear these now to save space
      positiveAccumulators_.Clear();
      negativeAccumulators_.Clear();
    }

    private static double CptyRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d)*p.GetRadonNikodymCpty(d);
    }

    private static double OwnRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d)*p.GetRadonNikodymOwn(d);
    }

    private static double ZeroRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d);
    }

    private double FundingRn(SimulatedPathValues p, int d)
    {
      return p.GetRadonNikodym(d)*p.GetRadonNikodymSurvival(d);
    }


    private Func<int, double> MeasureFunction(CCRMeasure measure, double pVal)
    {
      Func<int, double> func = null;
      if (fundamentalMeasures_.ContainsKey(measure))
        func = (t) => fundamentalMeasures_[measure][t];
      else if (exposureDistributions_.ContainsKey(measure))
        func = (t) => exposureDistributions_[measure][t].Quantile(pVal);
      else if (collateralDistributions_.ContainsKey(measure))
        func = (t) => collateralDistributions_[measure][t].Quantile(pVal);
      else if (measure == CCRMeasure.EC)
        func = (t) => exposureDistributions_[CCRMeasure.DiscountedPFE][t].Quantile(pVal) - fundamentalMeasures_[CCRMeasure.DiscountedEE][t];
      else if (measure == CCRMeasure.EC0)
        func = (t) => exposureDistributions_[CCRMeasure.DiscountedPFE0][t].Quantile(pVal) - fundamentalMeasures_[CCRMeasure.DiscountedEE0][t];
      if (func == null)
        throw new ArgumentException(String.Format("No results found for measure {0}", measure), "measure");
      return func;
    }

    private Func<int, double> MeasureFunction(CCRMeasure measure, double pVal, double[] multipliers)
    {
      if (multipliers == null)
        return MeasureFunction(measure, pVal);
      Func<int, double> func = null;

      if (fundamentalMeasures_.ContainsKey(measure))
        func = (t) => fundamentalMeasures_[measure][t]*multipliers[t];
      else if (exposureDistributions_.ContainsKey(measure))
        func = (t) => exposureDistributions_[measure][t].Quantile(pVal) * multipliers[t];
      else if (collateralDistributions_.ContainsKey(measure))
        func = (t) => collateralDistributions_[measure][t].Quantile(pVal) * multipliers[t];
      else if (measure == CCRMeasure.EC)
        func = (t) => (exposureDistributions_[CCRMeasure.DiscountedPFE][t].Quantile(pVal) - fundamentalMeasures_[CCRMeasure.DiscountedEE][t]) * multipliers[t];
      else if (measure == CCRMeasure.EC0)
        func = (t) => (exposureDistributions_[CCRMeasure.DiscountedPFE0][t].Quantile(pVal) - fundamentalMeasures_[CCRMeasure.DiscountedEE0][t])*multipliers[t];
      if (func == null)
        throw new ArgumentException(String.Format("No results found for measure {0}", measure), "measure");
      return func;
    }


    private double Interpolate(Func<int, double> func, Dt date)
    {
      int days;
      var dtDouble = date.ToDouble();
      if (Dt.Cmp(date, exposureDates_[0]) <= 0)
        return func(0);
      if ((days = Dt.Cmp(date, exposureDates_[exposureDates_.Length - 1])) >= 0)
        return (days == 0) ? func(exposureDates_.Length - 1) : 0.0;
      int offset = Array.BinarySearch(exposureDates_, date);
      if (offset > 0)
        return func(offset);
      offset = ~offset;
      double t = Dt.Diff(exposureDates_[offset - 1], date);
      double dt = Dt.Diff(exposureDates_[offset - 1], exposureDates_[offset]);
      double p0 = func(offset - 1);
      double p1 = func(offset);
      double p = p0 + (p1 - p0)*t/dt;
      return p;
    }

    private double Interpolate(CCRMeasure measure, Dt date, double pVal)
    {
      return Interpolate(MeasureFunction(measure, pVal), date);
    }

    private double Integrate(CCRMeasure measure, double pVal, Tuple<Dt[], double[]> kernel, double recovery,
                             double[] multipliers)
    {
      return Integrate(MeasureFunction(measure, pVal, multipliers), kernel, recovery);
    }

    private double Integrate(Func<int, double> func, Tuple<Dt[], double[]> kernel, double recovery)
    {
      double retVal = 0;
      double fNext = Interpolate(func, asOf_);
      for (int i = 0; i < kernel.Item1.Length; ++i)
      {
        double fPrev = fNext;
        var dt = kernel.Item1[i];
        fNext = Interpolate(func, dt);
        retVal += 0.5*(1 - recovery)*(fNext + fPrev)*kernel.Item2[i];
      }
      return retVal;
    }

    private double IntegrateTheta(CCRMeasure measure, double pVal, Tuple<Dt[], double[]> kernel, Dt date, double recovery,
                             double[] multipliers)
    {
      return IntegrateTheta(MeasureFunction(measure, pVal, multipliers), kernel, date, recovery);
    }

    private double IntegrateTheta(Func<int, double> func, Tuple<Dt[], double[]> kernel, Dt date, double recovery)
    {
      if (asOf_ >= kernel.Item1.Last())
        return 0.0;
      if (date <= asOf_)
        return 0.0;
      int from = Array.BinarySearch(kernel.Item1, asOf_);
      int to = (date >= kernel.Item1.Last()) ? kernel.Item1.Length - 1 : Array.BinarySearch(kernel.Item1, date);
      from = (from < 0) ? ~from : from + 1;
      to = (to < 0) ? ~to : to;
      double retVal = 0;
      double fNext = Interpolate(func, asOf_);
      for (int i = from; i < to; ++i)
      {
        double fPrev = fNext;
        var dt = kernel.Item1[i];
        fNext = Interpolate(func, dt);
        retVal += 0.5 * (1 - recovery) * (fNext + fPrev) * kernel.Item2[i];
      }
      double lastKernelValue;
      if (to >= 1)
      {
        double w1 = Dt.Diff(kernel.Item1[to - 1], date);
        double w2 = Dt.Diff(date, kernel.Item1[to]);
        lastKernelValue = w2 / (w1 + w2) * kernel.Item2[to - 1] + w1 / (w1 + w2) * kernel.Item2[to]; // Interpolate the kernel value at time date.
      }
      else // for the case that to = 0.
      {
        lastKernelValue = kernel.Item2[to];
      }     
      retVal += 0.5 * (1 - recovery) * (fNext + Interpolate(func, date)) * lastKernelValue;
      return retVal;
    }

    private double RunningMax(CCRMeasure measure, Dt date, double pVal)
    {
      return RunningMax(MeasureFunction(measure, pVal), date);
    }

    private double RunningMax(Func<int, double> func, Dt date)
    {
      double runningMax = 0;
      for (int i = 0; i < exposureDates_.Length; ++i)
      {
        if (Dt.Cmp(exposureDates_[i], date) < 0)
        {
          double nextVal = func(i);
          runningMax = Math.Max(runningMax, nextVal);
        }
        else
        {
          double nextVal = Interpolate(func, date);
          runningMax = Math.Max(runningMax, nextVal);
          break;
        }
      }
      return runningMax;
    }

    private double TimeAverage(CCRMeasure measure, Dt date, double pVal)
    {
      return TimeAverage(MeasureFunction(measure, pVal), date);
    }

    private double TimeAverage(Func<int, double> func, Dt date)
    {
      double f = func(0);
      double dt, T = Dt.FractDiff(asOf_, exposureDates_[0]);
      double retVal = f*T;
      for (int i = 1; i < exposureDates_.Length; ++i)
      {
        if (Dt.Cmp(exposureDates_[i], date) < 0)
        {
          dt = Dt.FractDiff(exposureDates_[i - 1], exposureDates_[i]);
          f = func(i);
          retVal += f*dt;
          T += dt;
        }
        else
        {
          dt = Dt.FractDiff(exposureDates_[i - 1], date);
          f = Interpolate(func, date);
          retVal += f*dt;
          T += dt;
          break;
        }
      }
      return (T <= 1e-12) ? 0.0 : retVal/T;
    }

    private double EffectiveMaturity(Func<int, double> func)
    {
      var lastExposureDate = exposureDates_[exposureDates_.Length - 1];
      var oneYearOut = Dt.Add(asOf_, new Tenor(1, TimeUnit.Years));
      double numerator = TimeAverage(func, lastExposureDate)*
                         Dt.Years(asOf_, lastExposureDate, DayCount.Actual365Fixed) -
                         TimeAverage(func, oneYearOut);
      double denominator = TimeAverage((t) => RunningMax(func, exposureDates_[t]), oneYearOut);

      if (numerator == 0.0 && denominator == 0.0)
        return 1.0;
      if (denominator <= 0.0)
        return 5.0;
      return 1.0 + numerator/denominator;
    }

    private double RiskWeightedAssets(CCRMeasure measure)
    {
      if (measure == CCRMeasure.RWA)
        return RiskWeightedAssets(MeasureFunction(CCRMeasure.DiscountedEE, 0.0), MeasureFunction(CCRMeasure.EE, 0.0));
      if (measure == CCRMeasure.RWA0)
        return RiskWeightedAssets(MeasureFunction(CCRMeasure.DiscountedEE0, 0.0), MeasureFunction(CCRMeasure.EE0, 0.0));
      throw new ArgumentException("Measure not supported", "measure");
    }

    private double RiskWeightedAssets(Func<int, double> discountFunc, Func<int, double> noDiscountFunc)
    {
      var oneYearOut = Dt.Add(asOf_, new Tenor(1, TimeUnit.Years));
      var defaultProb = (cptyCurve_ != null) ? 1.0 - cptyCurve_.Interpolate(oneYearOut) : 0.0;
      var r = 0.12 + 0.12*Math.Exp(-50*defaultProb);
      var b = (0.11852 - 0.05478*Math.Log(defaultProb));
      b *= b;
      var lgd = (integrationKernels_.Length >= 1) ? cptyRecovery_[0] : 1.0;
      var effectiveMaturity = EffectiveMaturity(discountFunc);
      effectiveMaturity = Math.Min(effectiveMaturity, 5.0);
      var k = lgd*(
                    Normal.cumulative(
                      (Normal.inverseCumulative(defaultProb, 0.0, 1.0) +
                       Math.Sqrt(r)*Normal.inverseCumulative(0.999, 0.0, 1.0))/Math.Sqrt(1 - r), 0.0, 1.0) - defaultProb)*
              (1 + (effectiveMaturity - 2.5)*b)/(1.0 - 1.5*b);
      var eepe = TimeAverage((t) => RunningMax(noDiscountFunc, exposureDates_[t]), oneYearOut);
      var alpha = 1.4;
      var ead = alpha*eepe;
      return ead*12.5*k;
    }

    private delegate double RadonNikodymDerivative(SimulatedPathValues path, int date);

    #endregion
  }
}