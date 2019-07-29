/*
 * HeterogeneousBasketPricer.cs
 *
 *
 */

#define USE_OWN_BumpedPvs
// #define INCLUDE_EXTRA_DEBUG // Define to include exra debug output

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
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{

  ///
  /// <summary>
  ///   Pricing helper class for Heterogeneous basket pricer
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.</para>
  ///
  ///   <para>BasketPricer classes are typically used internally by Pricer classes and are not used
  ///   directly by the user.</para>
  /// </remarks>
  ///
  [Serializable]
  public class HeterogeneousBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(HeterogeneousBasketPricer));

    #region Constructors

    /// <exclude />
    protected HeterogeneousBasketPricer()
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Factor correlations for the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    public HeterogeneousBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      Copula copula,
      Correlation correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels
      )
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating Heterogeneous Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);

      // Validate
      //
      // The correlation object MUST be a term structure!
      if( !(correlation is FactorCorrelation || correlation is CorrelationTermStruct) )
        throw new ArgumentException(String.Format("The correlation must be either FactorCorrelation or CorrelationTermStruct, not {0}", correlation.GetType()));

      this.LossDistribution = new Curve2D();
      this.AmorDistribution = new Curve2D();
      this.distributionComputed_ = false;

      #if USE_OWN_BumpedPvs
      this.wantSensitivity_ = false;
      this.bumpedCurveIndex_ = 0;
      #endif

      logger.Debug("Basket created");
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      HeterogeneousBasketPricer obj = (HeterogeneousBasketPricer)base.Clone();
      obj.lossDistribution_ = lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_.clone();
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
      HeterogeneousBasketPricer obj = (HeterogeneousBasketPricer)base.Duplicate();

      // Make clone of computation devices
      obj.lossDistribution_ = lossDistribution_ == null ? null : lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_ == null ? null : amorDistribution_.clone();

      return obj;
    }
    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Compute the whole distribution, save the result for later use
    /// </summary>
    private void
    ComputeAndSaveDistribution()
    {
      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing distribution for Heterogeneous basket");

      double[] recoveryRates = RecoveryRates;
      double[] recoveryDispersions = RecoveryDispersions;

      CorrelationTermStruct corr = this.CorrelationTermStruct;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      HeterogeneousBasketModel.ComputeDistributions(
          false,
          start,
          Maturity,
          StepSize,
          StepUnit,
          this.CopulaType,
          this.DfCommon,
          this.DfIdiosyncratic,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst,
          this.IntegrationPointsSecond,
          SurvivalCurves,
          Principals,
          recoveryRates,
          recoveryDispersions,
          CookedLossLevels.ToArray(),
          GridSize,
          LossDistribution,
          AmorDistribution);
      distributionComputed_ = true;
      #if USE_OWN_BumpedPvs
      wantSensitivity_ = false;
      #endif

      timer.stop();
      logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());

      return;
    }

    ///
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
    public override double[,]
    CalcLossDistribution(bool wantProbability,
      Dt date, double[] lossLevels)
    {
      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing loss distribution for Heterogeneous basket");

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      if (Dt.Cmp(start, date) > 0)
        throw new ArgumentOutOfRangeException("date", "date is before portfolio start");
      if (Dt.Cmp(Maturity, date) < 0)
        throw new ArgumentOutOfRangeException("date", "date is after maturity");

      lossLevels = SetLossLevels(lossLevels, false).ToArray();
      for (int i = 0; i < lossLevels.Length; ++i)
      {
        // By its nature the distribution is disrete. To avoid unexpected
        // results, we round numbers to nearest effective decimal points,
        // to make sure, for example,  2.0 does not become somthing like
        // 1.999999999999954
        decimal x = (decimal)lossLevels[i];
        lossLevels[i] = (double)Math.Round(x, EffectiveDigits);
        if (lossLevels[i] > 1.0)
          lossLevels[i] = 1.0;
      }

      double[] recoveryRates = RecoveryRates;
      double[] recoveryDispersions = RecoveryDispersions;

      CorrelationTermStruct corr = this.CorrelationTermStruct;

      Curve2D lossDistribution = new Curve2D();
      Curve2D amorDistribution = new Curve2D();

      Dt portfolioStart = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      HeterogeneousBasketModel.ComputeDistributions(
          wantProbability,
          portfolioStart,
          date,
          50,
          TimeUnit.Years,
          // force to compute one period from portfolio start to date only
          this.CopulaType,
          this.DfCommon,
          this.DfIdiosyncratic,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst,
          this.IntegrationPointsSecond,
          SurvivalCurves,
          Principals,
          recoveryRates,
          recoveryDispersions,
          lossLevels,
          GridSize,
          lossDistribution,
          amorDistribution);
      double totalPrincipal = TotalPrincipal;
      double initialBalance = InitialBalance;
      double prevLoss = this.PreviousLoss;
      int N = lossDistribution.NumLevels();
      double[,] results = new double[N, 2];
      for (int i = 0; i < N; ++i)
      {
        double level = lossDistribution.GetLevel(i);
        results[i, 0] = level * initialBalance + prevLoss;
        level = lossDistribution.Interpolate(date, level);
        results[i, 1] = wantProbability ? level : (level / totalPrincipal + prevLoss);
      }

      timer.stop();
      logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());

      return results;
    }

    #if USE_OWN_BumpedPvs

    /// <summary>
    ///   Compute the whole set of distributions for sensitivity analysis,
    ///   save the result for later use
    /// </summary>
    ///
    /// <param name="bumpedSurvivalCurves">A set of bumped survival curves</param>
    ///
    private void
    ComputeAndSaveSensitivities(SurvivalCurve[] bumpedSurvivalCurves)
    {
      if (!wantSensitivity_) return;

      Curve[] bcurves = new Curve[bumpedSurvivalCurves.Length];
      for (int i = 0; i < bcurves.Length; i++)
        bcurves[i] = bumpedSurvivalCurves[i];

      double[] recoveryRates = RecoveryRates;
      double[] recoveryDispersions = RecoveryDispersions;

      CorrelationTermStruct corr = this.CorrelationTermStruct;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      HeterogeneousBasketModel.ComputeDistributions(
          false,
          start,
          Maturity,
          StepSize,
          StepUnit,
          this.CopulaType,
          this.DfCommon,
          this.DfIdiosyncratic,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst,
          this.IntegrationPointsSecond,
          SurvivalCurves,
          bcurves,
          Principals,
          recoveryRates,
          recoveryDispersions,
          CookedLossLevels.ToArray(),
          GridSize,
          LossDistribution,
          AmorDistribution);
      distributionComputed_ = true;
    }

    #endif

    ///
    /// <summary>
    ///   Compute the accumulated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    /// <returns>The expected accumulative loss on a tranche</returns>
    public override double
    AccumulatedLoss(
        Dt date,
        double trancheBegin,
        double trancheEnd)
    {
      #if USE_OWN_BumpedPvs
      int groupIdx = bumpedCurveIndex_;
      if (!wantSensitivity_)
      {
        groupIdx = 0;
        if (!distributionComputed_)
          ComputeAndSaveDistribution();
      }
      else if (!distributionComputed_)
        throw new ArgumentException("You must call ComputeAndSaveSensitivities() first.");
      #else
      int groupIdx = 0;
      if (!distributionComputed_)
        ComputeAndSaveDistribution();
      #endif

      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);

      loss += LossDistribution.Interpolate(groupIdx, date, trancheBegin, trancheEnd) / TotalPrincipal;

      //logger.DebugFormat("Computed Loss for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, loss );

      return loss;
    }

    ///
    /// <summary>
    ///   Compute the amortized amount on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    /// <returns>The expected accumulative amortization on a tranche.</returns>
    public override double
    AmortizedAmount(
        Dt date,
        double trancheBegin,
        double trancheEnd)
    {
      #if USE_OWN_BumpedPvs
      int groupIdx = bumpedCurveIndex_;
      if (!wantSensitivity_)
      {
        groupIdx = 0;
        if (!distributionComputed_)
          ComputeAndSaveDistribution();
      }
      else if (!distributionComputed_)
        throw new ArgumentException("You must call ComputeAndSaveSensitivities() first.");
      #else
      int groupIdx = 0;
      if (!distributionComputed_)
        ComputeAndSaveDistribution();
      #endif

      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;
      AdjustTrancheLevels(true, ref tBegin, ref tEnd, ref amortized);
      amortized += AmorDistribution.Interpolate(groupIdx, date, tBegin, tEnd) / TotalPrincipal;

      //logger.DebugFormat("Computed Amortization for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, amort );

      return amortized;
    }


    ///
    /// <summary>
    ///   Reset the pricer such that in the next request for AccumulatedLoss()
    ///   or AmortizedAmount(), it recompute everything.
    /// </summary>
    ///
    public override void Reset()
    {
      distributionComputed_ = false;
    }

    /// <summary>
    ///   Experimental reset function
    ///   <preliminary/>
    /// </summary>
    /// <param name="what">Pricer attributes changed</param>
    /// <exclude/>
    public override void Reset(SyntheticCDOPricer.ResetFlag what)
    {
      base.Reset(what);
      if ((what & SyntheticCDOPricer.ResetFlag.Settle)
        == SyntheticCDOPricer.ResetFlag.Settle && lossDistribution_ != null)
      {
        Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
        if (start != lossDistribution_.GetAsOf())
        {
          lossDistribution_.SetAsOf(start);
          if (amorDistribution_ != null)
            amorDistribution_.SetAsOf(start);
        }
      }
      return;
    }

#if USE_OWN_BumpedPvs

    /// <summary>
    ///   Fast calculation of the MTM values for a series of Synthetic CDO tranches,
    ///   with each of the survival curves replaced by its alternative.
    /// </summary>
    ///
    /// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
    /// <param name="altSurvivalCurves">Array alternative survival curves</param>
    ///
    /// <remarks>
    ///   <para>Recalculation is avoided if the basket and altSurvivalCurves are the same.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two-dimensional array.
    ///    Each column identifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
    public override double[,]
    BumpedPvs(
        SyntheticCDOPricer[] pricers,
        SurvivalCurve[] altSurvivalCurves
        )
    {
      // Sanity check
      int basketSize = Count;
      if (altSurvivalCurves.Length != basketSize)
        throw new ArgumentOutOfRangeException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new ArgumentOutOfRangeException(String.Format("Pricer #{0} is not using this basket pricer!", j));

      // compute the whole distributions
      wantSensitivity_ = true;
      ComputeAndSaveSensitivities(altSurvivalCurves);

      #if INCLUDE_EXTRA_DEBUG

      if (logger.IsDebugEnabled)
      {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(String.Format("{0}\n", LossDistribution.GetAsOf().ToString()));
        for (int groupIdx=0; groupIdx<LossDistribution.NumGroups(); groupIdx++)
        {
          sb.Append(String.Format("groupIdx={0}\n", groupIdx));
          for (int dateIdx=0; dateIdx<LossDistribution.NumDates(); dateIdx++)
          {
            for (int levelIdx=0; levelIdx<LossDistribution.NumLevels(); levelIdx++)
            {
              int valueIdx = 
                dateIdx * LossDistribution.DateStride() + 
                levelIdx * LossDistribution.LevelStride() + 
                groupIdx * LossDistribution.GroupStride();
              sb.Append(String.Format("[{0},{1}] = {2}\n",
                                      LossDistribution.GetDate(dateIdx),
                                      LossDistribution.GetLevel(levelIdx),
                                      LossDistribution.GetValue(valueIdx)));

            }
          }
        }
        logger.Debug(sb.ToString());
      }

      #endif

      // now create and fill the table of values
      double[,] table = new double[basketSize + 1, pricers.Length];
      for (int i = 0; i <= basketSize; i++)
      {
        // we want the results with the ith curve bumped
        bumpedCurveIndex_ = i;
        // compute the prices
        for (int j = 0; j < pricers.Length; ++j)
          table[i, j] = pricers[j].FullPrice();
      }

      // restore states
      wantSensitivity_ = false;
      bumpedCurveIndex_ = 0;

      // done
      return table;
    }

    /// <summary>
    ///   Fast calculation of the price values for a series of Synthetic CDO tranches,
    ///   with each of the survival curves replaced by its alternative.
    /// </summary>
    ///
    /// <param name="pricers">
    ///   An array of CDO pricer evaluators sharing this basket
    /// </param>
    /// <param name="altSurvivalCurves">
    ///   Array alternative survival curves.
    /// </param>
    /// <param name="includeRecoverySensitivity">
    ///   If true, use the recovery curves in the alternative survival curves
    ///   to calculate sensitivities.
    /// </param>
    ///
    /// <remarks>
    ///   <para>Recalculation is avoided if the basket and altSurvivalCurves are the same.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of price values represented by a two dimensional array.
    ///    Each column identifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
    internal protected override double[,] BumpedPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity
      )
    {
      if (includeRecoverySensitivity || NeedExactJtD(pricers))
        return base.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);

      // Sanity check
      int basketSize = Count;
      if (altSurvivalCurves.Length != basketSize)
        throw new ArgumentOutOfRangeException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new ArgumentOutOfRangeException(String.Format("Pricer #{0} is not using this basket pricer!", j));

      // compute the whole distributions
      wantSensitivity_ = true;
      ComputeAndSaveSensitivities(altSurvivalCurves);

#if INCLUDE_EXTRA_DEBUG

      if (logger.IsDebugEnabled)
      {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(String.Format("{0}\n", LossDistribution.GetAsOf().ToString()));
        for (int groupIdx=0; groupIdx<LossDistribution.NumGroups(); groupIdx++)
        {
          sb.Append(String.Format("groupIdx={0}\n", groupIdx));
          for (int dateIdx=0; dateIdx<LossDistribution.NumDates(); dateIdx++)
          {
            for (int levelIdx=0; levelIdx<LossDistribution.NumLevels(); levelIdx++)
            {
              int valueIdx = 
                dateIdx * LossDistribution.DateStride() + 
                levelIdx * LossDistribution.LevelStride() + 
                groupIdx * LossDistribution.GroupStride();
              sb.Append(String.Format("[{0},{1}] = {2}\n",
                                      LossDistribution.GetDate(dateIdx),
                                      LossDistribution.GetLevel(levelIdx),
                                      LossDistribution.GetValue(valueIdx)));

            }
          }
        }
        logger.Debug(sb.ToString());
      }

#endif

      // now create and fill the table of values
      double[,] table = new double[basketSize + 1, pricers.Length];
      for (int i = 0; i <= basketSize; i++)
      {
        // we want the results with the ith curve bumped
        bumpedCurveIndex_ = i;
        // compute the prices
        for (int j = 0; j < pricers.Length; ++j)
          table[i, j] = pricers[j].Evaluate();
      }

      // restore states
      wantSensitivity_ = false;
      bumpedCurveIndex_ = 0;

      // done
      return table;
    }
#endif

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve2D LossDistribution
    {
      get { return lossDistribution_; }
      set { lossDistribution_ = value; }
    }

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve2D AmorDistribution
    {
      get { return amorDistribution_; }
      set { amorDistribution_ = value; }
    }

    /// <summary>
    ///   Distribution computed
    /// </summary>
    public bool DistributionComputed
    {
      get { return distributionComputed_; }
      set { distributionComputed_ = value; }
    }

    #endregion // Properties

    #region Data

    private Curve2D lossDistribution_;
    private Curve2D amorDistribution_;
    private bool distributionComputed_;

    #if USE_OWN_BumpedPvs
    private bool wantSensitivity_;
    private int bumpedCurveIndex_;
    #endif

    #endregion Data

  } // class HeterogeneousBasketPricer

}
