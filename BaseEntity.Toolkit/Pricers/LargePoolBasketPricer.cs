/*
 * LargePoolBasketPricer.cs
 *
 *
 */

using System;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
  /// <summary>
  ///   Pricing helper class for Uniform basket pricer
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  [Serializable]
  public class LargePoolBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(LargePoolBasketPricer));

    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// 
    /// <remarks>This function never checks that the notionals are uniform.</remarks>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Single factor correlation for all the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    /// <exclude />
    public
    LargePoolBasketPricer(
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
      Array lossLevels)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating Uniform Basket asof={0}, settle={1}, maturity={2}, principal={3}", asOf, settle, maturity, Principal);

      this.survivalCurveAlt_ = null;

      logger.Debug("Uniform Basket created");
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      LargePoolBasketPricer obj = (LargePoolBasketPricer)base.Clone();

      if (survivalCurveAlt_ != null)
        obj.survivalCurveAlt_ = (SurvivalCurve)survivalCurveAlt_.Clone();

      return obj;
    }

    #endregion // Constructors

    #region Methods
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
      logger.Debug("Computing loss distribution for Uniform basket");

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      if (Dt.Cmp(start, date) > 0)
        throw new ArgumentOutOfRangeException("date", "date is before portfolio start");
      if (Dt.Cmp(Maturity, date) < 0)
        throw new ArgumentOutOfRangeException("date", "date is after maturity");

      lossLevels = SetLossLevels(lossLevels, false).ToArray();
      for (int i = 0; i < lossLevels.Length; ++i)
        lossLevels[i] = Math.Round(lossLevels[i], 6);

      CorrelationTermStruct corr = this.CorrelationTermStruct;
      Copula copula = this.Copula;

      double initialBalance = this.InitialBalance;
      double prevLoss = this.PreviousLoss;
      double[,] results = new double[lossLevels.Length, 2];
      for (int i = 0; i < lossLevels.Length; ++i)
      {
        double level = lossLevels[i];
        results[i, 0] = level * initialBalance + prevLoss;
        double newLoss = LargePoolBasketModel.ComputeDistributions(
            wantProbability, start, date,
            copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
            corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
            this.IntegrationPointsFirst, this.IntegrationPointsSecond,
            SurvivalCurves, Principals, RecoveryRates, 0.0, level);
        results[i, 1] = wantProbability ? newLoss : (newLoss * initialBalance + prevLoss);
      }

      timer.stop();
      logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());

      return results;
    }


    ///
    /// <summary>
    ///   Compute the accumlated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    public override double
    AccumulatedLoss(Dt date,
                    double trancheBegin,
                    double trancheEnd)
    {
      double loss = 0.0;

      // Adjust for any defaulted credit
      AdjustTrancheLevels( false,
                           ref trancheBegin,
                           ref trancheEnd,
                           ref loss);

      CorrelationTermStruct corr = this.CorrelationTermStruct;
      Copula copula = this.Copula;
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      double newLoss = LargePoolBasketModel.ComputeDistributions(
          false, start, date,
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
          corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst, this.IntegrationPointsSecond,
          SurvivalCurves, Principals, RecoveryRates, trancheBegin, trancheEnd);
      loss += newLoss * InitialBalance;

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
    public override double
    AmortizedAmount(Dt date,
                    double trancheBegin,
                    double trancheEnd)
    {
      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;

      // Adjust for any defaulted credits
      AdjustTrancheLevels(true, ref tBegin, ref tEnd, ref amortized);

      double[] recoveryRates = new double[RecoveryRates.Length];
      for (int i = 0; i < recoveryRates.Length; ++i)
        recoveryRates[i] = 1 - RecoveryRates[i];

      CorrelationTermStruct corr = this.CorrelationTermStruct;
      Copula copula = this.Copula;
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      double loss = LargePoolBasketModel.ComputeDistributions(
          false, start, date,
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
          corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst, this.IntegrationPointsSecond,
          SurvivalCurves, Principals, recoveryRates, tBegin, tEnd);
      amortized += loss * InitialBalance;

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
      survivalCurveAlt_ = null;
    }


    /// <summary>
    ///   Fast calculation of the MTM values for a series of Synthetic CDO tranches,
    ///   with each of the survival curves replaced by its alternative.
    /// </summary>
    ///
    /// <param name="pricers">An array of CDO pricers to price sharing this basket pricer</param>
    /// <param name="altSurvivalCurves">Array alternative survival curves</param>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column indentifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    public override double[,]
    BumpedPvs(
              SyntheticCDOPricer[] pricers,
              SurvivalCurve[] altSurvivalCurves
              )
    {
      // Sanity check
      int basketSize = Count;
      if (altSurvivalCurves.Length != basketSize)
        throw new ArgumentException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new ArgumentException(String.Format("Pricer #{0} is not using this basket pricer!", j));

      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing spread sensitivity deltas for Uniform basket");

      // need SurvivalCurve
      SurvivalCurve survivalCurve = SurvivalCurves[0];

      // compute the whole distributions
      logger.DebugFormat("Computing distributions for curves (time {0}s)", timer.getElapsed());

      // now create and fill the table of values
      logger.DebugFormat("Filling results table (time {0}s)", timer.getElapsed());
      double[,] table = new double[altSurvivalCurves.Length + 1, pricers.Length];

      // compute the base case
      survivalCurveAlt_ = null;
      for (int j = 0; j < pricers.Length; ++j)
        table[0, j] = pricers[j].FullPrice();

      // for each curve
      int lastIdx = 0;
      for (int i = 1; i <= altSurvivalCurves.Length; i++)
      {
        SurvivalCurve altSurvivalCurve = altSurvivalCurves[i - 1];
        if (survivalCurve == altSurvivalCurve)
        {
          // Don't bother recalculating if the curve is unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[i, j] = table[0, j];
        }
        else if (survivalCurveAlt_ == altSurvivalCurve)
        {
          // Don't bother recalculating if the curve is unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[i, j] = table[lastIdx, j];
        }
        else
        {
          // we want the results with the ith curve bumped
          survivalCurveAlt_ = altSurvivalCurve;
          // compute the prices
          for (int j = 0; j < pricers.Length; ++j)
            table[i, j] = pricers[j].FullPrice();
          lastIdx = i;
        }
      }

      // restore states
      survivalCurveAlt_ = null;

      timer.stop();
      logger.DebugFormat("Completed basket spread sensitivity deltas in {0} seconds", timer.getElapsed());

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
        throw new ArgumentException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, altSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new ArgumentException(String.Format("Pricer #{0} is not using this basket pricer!", j));

      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing spread sensitivity deltas for Uniform basket");

      // need SurvivalCurve
      SurvivalCurve survivalCurve = SurvivalCurves[0];

      // compute the whole distributions
      logger.DebugFormat("Computing distributions for curves (time {0}s)", timer.getElapsed());

      // now create and fill the table of values
      logger.DebugFormat("Filling results table (time {0}s)", timer.getElapsed());
      double[,] table = new double[altSurvivalCurves.Length + 1, pricers.Length];

      // compute the base case
      survivalCurveAlt_ = null;
      for (int j = 0; j < pricers.Length; ++j)
        table[0, j] = pricers[j].Evaluate();

      // for each curve
      int lastIdx = 0;
      for (int i = 1; i <= altSurvivalCurves.Length; i++)
      {
        SurvivalCurve altSurvivalCurve = altSurvivalCurves[i - 1];
        if (survivalCurve == altSurvivalCurve)
        {
          // Don't bother recalculating if the curve is unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[i, j] = table[0, j];
        }
        else if (survivalCurveAlt_ == altSurvivalCurve)
        {
          // Don't bother recalculating if the curve is unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[i, j] = table[lastIdx, j];
        }
        else
        {
          // we want the results with the ith curve bumped
          survivalCurveAlt_ = altSurvivalCurve;
          // compute the prices
          for (int j = 0; j < pricers.Length; ++j)
            table[i, j] = pricers[j].Evaluate();
          lastIdx = i;
        }
      }

      // restore states
      survivalCurveAlt_ = null;

      timer.stop();
      logger.DebugFormat("Completed basket spread sensitivity deltas in {0} seconds", timer.getElapsed());

      // done
      return table;
    }
    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Recovery rate
    /// </summary>
    public double RecoveryRate
    {
      get
      {
        // use the average recovery rate
        double sum = 0;
        double[] recoveryRates = RecoveryRates;
        for (int i = 0; i < recoveryRates.Length; ++i)
          sum += recoveryRates[i];
        return (sum / recoveryRates.Length);
      }
    }


    /// <summary>
    ///   Recovery rate dispersion
    /// </summary>
    public double RecoveryDispersion
    {
      get
      {
        double sum = 0;
        double[] recoveryDispersions = RecoveryDispersions;
        for (int i = 0; i < recoveryDispersions.Length; ++i)
          sum += recoveryDispersions[i];
        return (sum / recoveryDispersions.Length);
      }
    }


    /// <summary>
    ///   Principal for each name
    /// </summary>
    public double Principal
    {
      get { return TotalPrincipal / Count; }
    }

    #endregion // Properties

    #region Data

    private SurvivalCurve survivalCurveAlt_;

    #endregion // Data

  } // class LargePoolBasketPricer

}
