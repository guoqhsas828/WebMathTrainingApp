/*
 * BasketBootstrapCorrelationPricer.cs
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
  ///   Simple wrapper class for bootstraping correlations
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class employs more efficient method to calculate the loss distributions
  ///   in base correlation calibration.  Its algorithm has some inconsistency with
  ///   the standard heterogeneous basket pricers.
  /// 
  ///   (1) After calling the Reset() function, only the loss distributions for the last tenor period
  ///       are recalculated.  In contrast, the standard pricers will
  ///       recalculate the whole distributions for all the tenor periods from the settlement to maturity.
  /// 
  ///   (2) Accordingly, the SetFactor() function only sets the correlation of the last tenor point
  ///       while keep all other tenor points unchanged.  In contrast, the standard pricers will reset
  ///       the correlations of all the tenor points.
  /// 
  ///    At this moment, this class is intented to be used internally.
  /// </remarks>
  ///
  /// <exclude />
  [Serializable]
  class BasketBootstrapCorrelationPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BasketBootstrapCorrelationPricer));

    #region Constructors

    /// <exclude />
    public BasketBootstrapCorrelationPricer()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="basket">The base basket to bootstrap</param>
    /// <param name="correlation">The correlation term struct</param>
    public BasketBootstrapCorrelationPricer(
      BasketPricer basket,
      CorrelationTermStruct correlation
      )
    {
      basket_ = basket.Duplicate();
      basket_.CopyTo(this);
      this.Correlation = correlation;
      factor_ = 0;
      lastIndex_ = -1;
      computationInitialized_ = false;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned basket</returns>
    public override object Clone()
    {
      BasketBootstrapCorrelationPricer obj = (BasketBootstrapCorrelationPricer)base.Clone();
      obj.basket_ = (BasketPricer)basket_.Clone();
      obj.CopyTo(obj.basket_);
      return obj;
    }

    /// <summary>
    ///   Duplicate a basket pricer
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Duplicate() differs from Clone() in that it copies by references all the 
    ///   basic data and numerical options defined in the BasketPricer class.  But it is
    ///   not the same as the MemberwiseClone() function, since it does not copy by reference
    ///   the computational data such as LossDistributions in SemiAnalyticBasketPricer class.
    ///   </para>
    /// 
    ///   <para>This function provides an easy way to construct objects performing
    ///   independent calculations on the same set of input data.  We will get rid of it
    ///   once we have restructured the basket architecture by furthur separating the basket data,
    ///   the numerical options and the computational devices.</para>
    /// </remarks>
    /// 
    /// <returns>Duplicated basket pricer</returns>
    /// <exclude />
    public override BasketPricer Duplicate()
    {
      BasketBootstrapCorrelationPricer obj = (BasketBootstrapCorrelationPricer)base.Duplicate();
      obj.basket_ = basket_.Duplicate();
      return obj;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Set factor for the last period
    /// </summary>
    /// <param name="factor">factor to set</param>
    public override void SetFactor(double factor)
    {
      factor_ = factor;
      if (!computationInitialized_)
      {
        initializeComputation();
        return;
      }
      CorrelationTermStruct corr = (CorrelationTermStruct)Correlation;
      corr.SetFactorAtDate(lastIndex_, factor_);
      basket_.Reset();
    }

    /// <summary>
    ///   Reset the calculations
    /// </summary>
    public override void Reset()
    {
      computationInitialized_ = false;
    }

    /// <summary>
    ///   Find the index of the period which contains the maturity date
    /// </summary>
    /// <param name="dates">Array of end period dates</param>
    /// <param name="maturity">maturity date</param>
    /// <returns></returns>
    private static int FindTenorIndex(Dt[] dates, Dt maturity)
    {
      int N = dates.Length;
      if (N > 1)
      {
        for (int i = 0; i < N; ++i)
          if (Dt.Cmp(maturity, dates[i]) <= 0)
            return i;
      }
      return N - 1;
    }

    /// <summary>
    ///   Initialize correlation before real computation
    /// </summary>
    private void initializeComputation()
    {
      this.UpdateRecoveries();
      CorrelationTermStruct corr = (CorrelationTermStruct)this.Correlation;
      lastIndex_ = FindTenorIndex(corr.Dates, Maturity);
      corr.SetFactorAtDate(lastIndex_, factor_);
      if (lastIndex_ > 0 && basket_ is SemiAnalyticBasketPricer)
      {
        ((SemiAnalyticBasketPricer)basket_).SetRecalculationStartDate(
          corr.Dates[lastIndex_ - 1], true);
        if (this.IntegrationPointsFirst != basket_.IntegrationPointsFirst)
          ((SemiAnalyticBasketPricer)basket_).LossDistribution = null;
      }
      this.CopyTo(basket_);
      basket_.Reset();
      computationInitialized_ = true;
    }

    #endregion // Methods

    #region Overrides
    //-
    // Override all virtual functions to make sure they are passed through the actuall basket.
    //-
    public override double AccumulatedLoss(Dt date, double trancheBegin, double trancheEnd)
    {
      if (!computationInitialized_)
        initializeComputation();
      return basket_.AccumulatedLoss(date, trancheBegin, trancheEnd);
    }

    public override double AmortizedAmount(Dt date, double trancheBegin, double trancheEnd)
    {
      if (!computationInitialized_)
        initializeComputation();
      return basket_.AmortizedAmount(date, trancheBegin, trancheEnd);
    }

    public override double[,] BumpedPvs(SyntheticCDOPricer[] pricers, SurvivalCurve[] altSurvivalCurves)
    {
      if (!computationInitialized_)
        initializeComputation();
      else
        this.CopyTo(basket_);
      return basket_.BumpedPvs(pricers, altSurvivalCurves);
    }

    public override double[,] CalcLossDistribution(bool wantProbability, Dt date, double[] lossLevels)
    {
      if (!computationInitialized_)
        initializeComputation();
      else
        this.CopyTo(basket_);
      return basket_.CalcLossDistribution(wantProbability, date, lossLevels);
    }

    protected override double OnSetPrincipals(double[] principals)
    {
      this.CopyTo(basket_);
      basket_.Principals = principals;
      return basket_.TotalPrincipal;
    }
    #endregion // Overrides

    #region Properties
    internal BasketPricer InnerBasket { get { return basket_; } }
    #endregion // Properties

    #region Data
    BasketPricer basket_;
    private int lastIndex_;
    private double factor_;
    private bool computationInitialized_;
    #endregion // Data

  }// BootstrapBasketBootstrapCorrelationPricer
}
