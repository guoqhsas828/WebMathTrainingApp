using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Util;

using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  ///<summary>
  /// Engine for calculation of regulatory CVA VaR
  ///</summary>
  public class CVAVaREngine
  {
    private readonly Dt asOf_;
    private readonly Dt[] exposureDates_;
    private readonly PathWiseExposure pathwiseExposure_;

    ///<summary>
    /// Constructs a calculation engine for computing Basel III CVA and VaR 
    ///</summary>
    ///<param name="asOf">simulation asOf date</param>
    ///<param name="exposureDates">dates when exposures are simulated. Same as passed in when running simulation</param>
    ///<param name="pricerNettingSets">list of netting set names ordered by pricer. Same as passed in when running simulation</param>
    ///<param name="netting">the netting and collateral data</param>
    public CVAVaREngine(Dt asOf, Dt[] exposureDates,
                        string[] pricerNettingSets,
                        Netting netting)
    {
      asOf_ = asOf;
      exposureDates_ = exposureDates;
      var nettingMap = new Dictionary<string, int>();

      for (int i = 0; i < pricerNettingSets.Length; ++i)
      {
        string nettingSet = pricerNettingSets[i];
        if (!nettingMap.ContainsKey(nettingSet))
        {
          int idx = nettingMap.Count;
          nettingMap.Add(nettingSet, idx);
        }
      }
      pathwiseExposure_ = new PathWiseExposure(exposureDates, nettingMap, netting,
                                               PathWiseExposure.RiskyParty.Counterparty);
    }

    ///<summary>
    /// Calculate change in Basel III CVA for each scredit spread shift 
    ///</summary>
    ///<param name="exposures">The expected exposure profile</param>
    ///<param name="counterparyCurve">risky counterparty survival curve</param>
    ///<param name="tenors">cds curve tenors that will be bumped in each VaR scenario</param>
    ///<param name="survivalProbShifts">bumps to survival probs by [scenarioIdx][tenorIdx]</param>
    ///<param name="shiftRelative">relative or absolute bump units</param>
    ///<param name="cdsHedges">pricers for approved CDS Hedges - protection on counterparty</param>
    ///Each pricer is coupled with an int indexing the underlyer that is the hedge for this counterparty.
    ///<returns>Array of CVA deltas in same order as spreadShifts input, used to calculate CVA VaR</returns>
    public double[] CalculateShiftedRegulatoryCVADeltas(double[] exposures,
                                                        SurvivalCurve counterparyCurve,
                                                        string[] tenors,
                                                        double[][] survivalProbShifts,
                                                        bool shiftRelative,
                                                        CDSCashflowPricer[] cdsHedges
                                                        )
    {
      if (tenors.Length != survivalProbShifts[0].Length)
        throw new ToolkitException("shifts provided for {0} tenors, does not match expected {1}", survivalProbShifts[0].Length,
                                   tenors.Length);
      var shiftedCVADeltas = new double[survivalProbShifts.GetLength(0)];
      var originalCVA = CalculateRegulatoryCVA(exposures, counterparyCurve);
      var hedge = 0.0;
      if (cdsHedges != null)
      {
        foreach (var cdsHedge in cdsHedges)
        {
          // survival curve might be a scaled curve, or different seniority
          hedge += CalculateRegulatoryCDSValue(cdsHedge, cdsHedge.SurvivalCurve);
        }
      }
      
      originalCVA -= hedge;

      Parallel.For(0, shiftedCVADeltas.Length,
                   (i) =>
                   {
                     // map from original curve to bumped curve so we don't repeat work
                     var curveDict = new Dictionary<SurvivalCurve, SurvivalCurve>();
                     curveDict[counterparyCurve] = BumpCurve(counterparyCurve, tenors, survivalProbShifts[i], shiftRelative);
                     shiftedCVADeltas[i] = CalculateRegulatoryCVA(exposures, curveDict[counterparyCurve]);

                     var hedgePv = 0.0;

                     if (cdsHedges != null)
                     {
                       foreach (var cdsHedge in cdsHedges)
                       {
                         // survival curve might be a scaled curve, or different seniority
                         if (!curveDict.ContainsKey(cdsHedge.SurvivalCurve))
                         {
                           curveDict[cdsHedge.SurvivalCurve] = BumpCurve(cdsHedge.SurvivalCurve, tenors, survivalProbShifts[i], shiftRelative);
                         }
                         hedgePv += CalculateRegulatoryCDSValue(cdsHedge, curveDict[cdsHedge.SurvivalCurve]);
                       }
                     }

                     shiftedCVADeltas[i] = originalCVA - (shiftedCVADeltas[i] - hedgePv);
                   });


      return shiftedCVADeltas;
    }

    /// <summary>
    /// Calculate change in index hedge value for each index level shift 
    /// </summary>
    /// <param name="indexHedges"> </param>
    /// <param name="indexLevelShifts"> </param>
    /// <param name="indexShiftQuotingConv"></param>
    /// <param name="indexShiftBumpRelative"> </param>
    /// <param name="indexHedgeWeights"> </param>
    /// <returns>Array of deltas in same order as spreadShifts input, used to offset CVA VaR</returns>
    public double[] CalculateShiftedIndexHedgeDeltas(   CDXPricer[] indexHedges,
                                                        double[][] indexLevelShifts,
                                                        QuotingConvention[] indexShiftQuotingConv, 
                                                        bool[] indexShiftBumpRelative,
                                                        double[] indexHedgeWeights
                                                        )
    {
      if(indexLevelShifts.Length == 0)
        return new double[0];
      var hedgeDeltas = new double[indexLevelShifts[0].Length]; 
      var indexCds = new CDSCashflowPricer[indexHedges.Length];
      double origHedgeValue = 0.0;
      for (int h = 0; h < indexHedges.Length; ++h)
      {
        var indexHedge = indexHedges[h];
        indexCds[h] = indexHedge.EquivalentCDSPricer;
        var indexLevelCurve = indexCds[h].SurvivalCurve;
        origHedgeValue += CalculateRegulatoryCDSValue(indexCds[h], indexLevelCurve) * indexHedgeWeights[h];
      }
      
      Parallel.For(0, hedgeDeltas.Length,
                   (i) =>
                   {
                      var shiftedHedgeValue = 0.0;
                      for (int h = 0; h < indexHedges.Length; ++h)
                      {
                        var indexHedge = indexHedges[h];
                        double bumpedSpread = indexHedge.MarketPremium; 
                        if(indexShiftQuotingConv[h] == QuotingConvention.FlatPrice)
                        {
                          var marketPrice = indexHedge.MarketPrice(); 
                          var shiftAmt = indexLevelShifts[h][i]; 
                          if(indexShiftBumpRelative[h])
                            shiftAmt = marketPrice * shiftAmt; 
                          var bumpedPrice = marketPrice + shiftAmt; 
                          bumpedSpread = CDXPricerUtil.CDXPriceToSpread(indexHedge.AsOf, indexHedge.Settle, indexHedge.CDX, bumpedPrice, 
                                                                        indexHedge.DiscountCurve, indexHedge.SurvivalCurves, 
                                                                        indexHedge.MarketRecoveryRate, indexHedge.CurrentRate); 
                        }
                        else
                        {
                          var marketSpread = indexHedge.MarketPremium; 
                          var shiftAmt = indexLevelShifts[h][i]; 
                          if(indexShiftBumpRelative[h])
                            shiftAmt = marketSpread * shiftAmt; 
                          bumpedSpread = marketSpread + shiftAmt; 
                        }
                        var indexLevelCurve = CDXPricerUtil.FitFlatSurvivalCurve(indexHedge.AsOf, indexHedge.Settle, indexHedge.CDX, bumpedSpread,
                                                           indexHedge.MarketRecoveryRate, indexHedge.DiscountCurve, indexHedge.RateResets);

                        shiftedHedgeValue += CalculateRegulatoryCDSValue(indexCds[h], indexLevelCurve) * indexHedgeWeights[h];
                      }
                     
                     hedgeDeltas[i] = shiftedHedgeValue - origHedgeValue;
                   });


      return hedgeDeltas;
    }

    /// <summary>
    /// Get CVA VaR from an array of precalculated CVA deltas
    /// </summary>
    /// <param name="deltas">the array of cva deltas</param>
    ///<param name="confidenceInterval">the % confidence interval to compute VaR at</param>
    /// <returns>CVA VaR</returns>
    public double RegulatoryVaRFromCVADeltas(double[] deltas, double confidenceInterval)
    {
      var p = ArrayUtil.Generate(deltas.Length, (i) => (i + 1)/(double) deltas.Length);
      // flip sign so Sort() will order correctly
      var x = ArrayUtil.Generate(deltas.Length, (i) => -deltas[i]);
      Array.Sort(x);
      var distribution = new EmpiricalDistribution(x, p);
      return -distribution.Quantile(confidenceInterval);
    }

    ///<summary>
    /// Calculate Basel III CVA VaR
    ///</summary>
    ///<param name="exposures">The expected exposure profile</param>
    ///<param name="counterparyCurve">risky counterparty survival curve</param>
    ///<param name="tenors">cds curve tenors that will be bumped in each VaR scenario</param>
    ///<param name="survivalShifts">bumps to survival probs by [scenarioIdx][tenorIdx]</param>
    ///<param name="bumpRelative">relative or absolute bump units</param>
    ///<param name="confidenceInterval">the % confidence interval to compute VaR at</param>
    ///<param name="cdsHedges">pricers for approved CDS Hedges - protection on counterparty</param>
    ///Each pricer is coupled with an int indexing the underlyer that is the hedge for this counterparty.
    ///<returns>CVA VaR</returns>
    public double CalculateRegulatoryVaR(double[] exposures,
                                         SurvivalCurve counterparyCurve,
                                         string[] tenors,
                                         double[][] survivalShifts,
                                         bool bumpRelative,
                                         double confidenceInterval,
                                         CDSCashflowPricer[] cdsHedges)
    {
      var cva = CalculateShiftedRegulatoryCVADeltas(exposures, counterparyCurve, tenors, 
                                                    survivalShifts, bumpRelative, cdsHedges);

      return RegulatoryVaRFromCVADeltas(cva, confidenceInterval);
    }


    ///<summary>
    /// Calculate Basel III CVA for a given set of exposures 
    ///</summary>
    ///<param name="exposure">the exposure profile</param>
    ///<param name="counterparyCurve">the counterparty survival curve</param>
    ///<returns></returns>
    public double CalculateRegulatoryCVA(double[] exposure, SurvivalCurve counterparyCurve)
    {
      var spread = new double[exposureDates_.Length];
      var t = new double[exposureDates_.Length];
      var ps = new double[exposureDates_.Length];
      var lgd = 1.00 - counterparyCurve.SurvivalCalibrator.RecoveryCurve.RecoveryRate(counterparyCurve.AsOf);
      var df = new double[exposureDates_.Length];
      var discountCurve = counterparyCurve.SurvivalCalibrator.DiscountCurve;

      for (int i = 0; i < exposureDates_.Length; ++i)
      {
        spread[i] = counterparyCurve.ImpliedSpread(exposureDates_[i]);
        t[i] = Dt.Years(asOf_, exposureDates_[i], DayCount.Actual365Fixed);
        ps[i] = Math.Exp(-(spread[i]*t[i])/lgd);
        df[i] = discountCurve.DiscountFactor(exposureDates_[i]); 
      }

      double cva = 0.0;
      for (int i = 1; i < exposureDates_.Length; ++i)
      {
        double pd = Math.Max(0.0, ps[i - 1] - ps[i]);
        cva += pd*((exposure[i - 1]*df[i-1] + exposure[i]*df[i])/2);
      }
      cva *= lgd;
      return cva;
    }

    private double CalculateRegulatoryCDSValue(CDSCashflowPricer pricer, SurvivalCurve survivalCurve)
    {
      int numPeriods = Array.BinarySearch(exposureDates_, pricer.CDS.Maturity);
      if (numPeriods < 0)
        numPeriods = ~numPeriods;
      var exposureDts = ArrayUtil.Generate(numPeriods,
                                           (i) => i < numPeriods - 1 ? exposureDates_[i] : pricer.CDS.Maturity);

      double lgd = 1.00 - survivalCurve.SurvivalCalibrator.RecoveryCurve.RecoveryRate(pricer.AsOf);
      double notional = -pricer.Notional; // buy protection is negative, so flip sign here
      var spread = new double[numPeriods];
      var t = new double[numPeriods];
      var ps = new double[numPeriods];
      var df = new double[numPeriods];
      for (int i = 0; i < numPeriods; ++i)
      {
        spread[i] = survivalCurve.ImpliedSpread(exposureDts[i]);
        t[i] = Dt.Years(asOf_, exposureDts[i], DayCount.Actual365Fixed);
        ps[i] = Math.Exp(-(spread[i]*t[i])/lgd);
        df[i] = pricer.DiscountCurve.DiscountFactor(exposureDts[i]);
      }
      double pv = 0.0;
      for (int i = 1; i < numPeriods; ++i)
      {
        double pd = Math.Max(0.0, ps[i - 1] - ps[i]);
        pv += pd*((df[i - 1] + df[i])/2);
      }
      pv *= lgd*notional;
      return pv;
    }

    private SurvivalCurve BumpCurve(SurvivalCurve originalCurve, string[] tenors, double[] survivalProbShifts, bool relative)
    {
      var bumpedCurve = CurveUtil.CurveCloneWithRecovery(new[] { originalCurve })[0];
      Conform(bumpedCurve, tenors);
      var shiftedCurveValues = new double[tenors.Length];
      for (int i = 0; i < tenors.Length; i++)
      {
        double originalValue = bumpedCurve.GetVal(i);
        double shiftAmount = survivalProbShifts[i];
        if (relative)
        {
          shiftAmount = originalValue * shiftAmount;
        }
        shiftedCurveValues[i] = originalValue + shiftAmount;
      }
      
      for (int i = 0; i < tenors.Length; i++)
      {
        bumpedCurve.SetVal(i, shiftedCurveValues[i]);
      }
      
      return (SurvivalCurve)bumpedCurve;
    }

    //Other interpolations are too time consuming
    private static void ResetInterp(Curve curve)
    {
      var method = InterpMethod.Custom;
      try
      {
        method = curve.InterpMethod;
      }
      catch (Exception)
      {
      }
      if (method == InterpMethod.Weighted || method == InterpMethod.Linear)
        return;
      Extrap lower = new Const();
      Extrap upper = new Smooth();
      curve.Interp = new Linear(upper, lower);
    }


    /// <summary>
    /// Conform curve tenors to those explicitely provided
    /// </summary>
    /// <param name="curve">CalibratedCurve object</param>
    /// <param name="tenors">Given tenors</param>
    private void Conform(Curve curve, string[] tenors)
    {
      if (curve == null)
        return;
      var dts = Array.ConvertAll(tenors, t => Dt.Roll(Dt.Add(curve.AsOf, t), BDConvention.Following, Calendar.None));
      double[] y = Array.ConvertAll(dts, curve.Interpolate);
      curve.Clear();
      ResetInterp(curve);
      curve.Add(dts, y);
    }

    
  }
}