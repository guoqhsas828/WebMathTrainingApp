/*
 * PvAveragingBasketPricer.cs
 *
 *  -2008. All rights reserved.     
 *
 * $Id $
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
  /// <summary>
  ///   Simple wrapper class for PV averaging calculation
  /// </summary>
  ///
  /// <exclude />
  [Serializable]
  class PvAveragingBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(PvAveragingBasketPricer));

    #region Constructors

    /// <exclude />
    internal PvAveragingBasketPricer()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="basket">Base basket used to calculate</param>
    /// <param name="correlationMixed">Mixed correlations</param>
    public PvAveragingBasketPricer(
      BasketPricer basket,
      CorrelationMixed correlationMixed
      )
    {
      basket_ = basket.Duplicate();
      basket_.CopyTo(this);
      this.Correlation = correlationMixed;
      this.dumyCorrelation_ = new PvAveragingCorrelation(this);
      computationInitialized_ = false;
      calculators_ = null;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned basket</returns>
    public override object Clone()
    {
      PvAveragingBasketPricer obj = (PvAveragingBasketPricer)base.Clone();
      obj.basket_ = (BasketPricer)basket_.Clone();
      obj.CopyTo(obj.basket_);
      obj.dumyCorrelation_ = new PvAveragingCorrelation(obj);
      return obj;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned basket</returns>
    /// <exclude />
    public override BasketPricer  Duplicate()
    {
      PvAveragingBasketPricer obj = (PvAveragingBasketPricer)base.Duplicate();
      obj.basket_ = basket_.Duplicate();
      obj.dumyCorrelation_ = new PvAveragingCorrelation(obj);
      obj.computationInitialized_ = false;
      obj.calculators_ = null;
      return obj;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Reset the calculations
    /// </summary>
    public override void Reset()
    {
      computationInitialized_ = false;
    }

    /// <summary>
    ///   Set up basket calculators
    /// </summary>
    private void InitializeComputation()
    {
      this.CopyTo(basket_);
      if (this.Correlation is CorrelationMixed)
      {
        CorrelationMixed correlationMixed = (CorrelationMixed)this.Correlation;
        double[] weights = correlationMixed.Weights;
        CorrelationObject[] correlations = correlationMixed.CorrelationObjects;
        calculators_ = new BasketPricer[correlations.Length];
        for (int i = 0; i < correlations.Length; ++i)
          if (Math.Abs(weights[i]) > 1E-15)
          {
            BasketPricer bp = basket_.Duplicate();
            bp.Correlation = correlations[i];
            bp.Reset();
            calculators_[i] = bp;
          }
      }
      else
      {
        basket_.Reset();
        calculators_ = null;
      }
      computationInitialized_ = true;
    }


    /// <summary>
    ///   Compute the accumlated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// <returns>accumulated losses</returns>
    public override double AccumulatedLoss(Dt date, double trancheBegin, double trancheEnd)
    {
      if (!computationInitialized_)
        InitializeComputation();

      if(calculators_ == null)
        return basket_.AccumulatedLoss(date, trancheBegin, trancheEnd);

      double sumLoss = 0;
      double sumWeights = 0;
      double[] weights = ((CorrelationMixed)this.Correlation).Weights;
      for (int i = 0; i < calculators_.Length; ++i)
        if (calculators_[i] != null)
        {
          sumLoss += calculators_[i].AccumulatedLoss(date, trancheBegin, trancheEnd) * weights[i];
          sumWeights += weights[i];
        }
      return sumLoss / sumWeights;
    }

    /// <summary>
    ///   Compute the amortized amount on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    /// <returns>Accumulated ammortizations</returns>
    public override double AmortizedAmount(Dt date, double trancheBegin, double trancheEnd)
    {
      if (!computationInitialized_)
        InitializeComputation();

      if (calculators_ == null)
        return basket_.AmortizedAmount(date, trancheBegin, trancheEnd);

      double sumLoss = 0;
      double sumWeights = 0;
      double[] weights = ((CorrelationMixed)this.Correlation).Weights;
      for (int i = 0; i < calculators_.Length; ++i)
        if (calculators_[i] != null)
        {
          sumLoss += calculators_[i].AmortizedAmount(date, trancheBegin, trancheEnd) * weights[i];
          sumWeights += weights[i];
        }
      return sumLoss / sumWeights;
    }


    /// <summary>
    ///   Fast calculation of the MTM values for a series of Synthetic CDO tranches,
    ///   with each of the survival curves replaced by its alternative.
    /// </summary>
    ///
    /// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
    /// <param name="altSurvivalCurves">Array alternative survival curves</param>
    ///
    /// <remarks>
    ///   <para>Recalculation is avoided if the basket's survival curves and altSurvivalCurves
    ///   are the same.</para>
    ///
    ///   <para>Note: <paramref name="altSurvivalCurves"/> must contain bumped curves matching
    ///   all SurvivalCurves for the basket. If original curves are passed in as part of the
    ///   bumped curve set (ie unbumped), no bump will be performed.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column indentifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    public override double[,] BumpedPvs(SyntheticCDOPricer[] pricers, SurvivalCurve[] altSurvivalCurves)
    {
      if (!computationInitialized_)
        InitializeComputation();

      if (calculators_ == null)
        return basket_.BumpedPvs(pricers, altSurvivalCurves);

      double[] weights = ((CorrelationMixed)this.Correlation).Weights;
      double sumWeights = Sum(weights);
      if (sumWeights < 1E-9)
        throw new System.ArgumentException("Sum of weights is close to zero");

      SyntheticCDOPricer[] dummyPricers = new SyntheticCDOPricer[pricers.Length];
      double[,] sumPvs = null;
      for (int i = 0; i < calculators_.Length; ++i)
        if (calculators_[i] != null)
        {
          BasketPricer basket = calculators_[i];
          for (int j = 0; j < pricers.Length; ++j)
          {
            dummyPricers[j] = new SyntheticCDOPricer(pricers[j].CDO,
              basket, pricers[j].DiscountCurve, pricers[j].Notional);
          }
          double[,] pvs = basket.BumpedPvs(dummyPricers, altSurvivalCurves);
          int M = pvs.GetLength(0);
          int N = pvs.GetLength(1);
          if (sumPvs == null)
            sumPvs = new double[M, N];
          double weight = weights[i];
          for (int j = 0; j < M; ++j)
            for (int k = 0; k < N; ++k)
              sumPvs[j, k] += weight * pvs[j, k] / sumWeights;
        }
      return sumPvs;
    }

    /// <summary>
    ///   Compute the cumulative loss distribution
    /// </summary>
    ///
    /// <remarks>
    ///   The returned array has two columns, the first of which contains the 
    ///   loss levels and the second column contains the corresponding cumulative
    ///   probabilities or expected base losses.
    /// </remarks>
    ///
    /// <param name="wantProbability">If true, return probabilities; else, return expected base losses</param>
    /// <param name="date">The date at which to calculate the distribution</param>
    /// <param name="lossLevels">Array of lossLevels (should be between 0 and 1)</param>
    /// 
    public override double[,] CalcLossDistribution(bool wantProbability, Dt date, double[] lossLevels)
    {
      if (!computationInitialized_)
        InitializeComputation();

      if (calculators_ == null)
        return basket_.CalcLossDistribution(wantProbability, date, lossLevels);

      double[] weights = ((CorrelationMixed)this.Correlation).Weights;
      double sumWeights = Sum(weights);
      if (sumWeights < 1E-9)
        throw new System.ArgumentException("Sum of weights is close to zero");

      double[,] sumResults = null;
      for (int i = 0; i < calculators_.Length; ++i)
        if (calculators_[i] != null)
        {
          double[,] results = calculators_[i].CalcLossDistribution(wantProbability, date, lossLevels);
          int M = results.GetLength(0);
          int N = results.GetLength(1);
          if (sumResults == null)
            sumResults = new double[M, N];
          double weight = weights[i];
          for (int j = 0; j < M; ++j)
            for (int k = 0; k < N; ++k)
              sumResults[j, k] += weight * results[j, k] / sumWeights;
        }
      return sumResults;
    }

    /// <summary>
    ///   Calculate ths summation of an array
    /// </summary>
    /// <param name="a">Array</param>
    /// <returns>summation</returns>
    private double Sum(double[] a)
    {
      double sum = 0;
      for (int i = 0; i < a.Length; ++i)
        sum += a[i];
      return sum;
    }
    #endregion // Methods

    #region Properties
    /// <summary>
    ///   The underlying calculation engine
    /// </summary>
    public BasketPricer BasketCalculator
    {
      get { return basket_; }
    }
    #endregion // Properties

    #region Data
    BasketPricer basket_;
    private bool computationInitialized_;

    BasketPricer[] calculators_;
    #endregion // Data

    #region Types
    /// <summary>
    ///   For internal use only
    /// <preliminary/>
    /// </summary>
    /// <exclude/>
    [Serializable]
    public class PvAveragingCorrelation : CorrelationTermStruct
    {
      internal PvAveragingCorrelation(BasketPricer basket)
        : base(Utils.GetCreditNames(basket.SurvivalCurves), new double[1], new Dt[1])
      {}
    };
    private PvAveragingCorrelation dumyCorrelation_;
    internal PvAveragingCorrelation DumyCorrelation
    {
      get { return dumyCorrelation_; }
    }
    #endregion Types
  }// BootstrapPvAveragingBasketPricer
}
