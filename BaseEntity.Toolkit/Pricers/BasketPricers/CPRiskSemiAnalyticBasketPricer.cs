/*
 * CPRiskSemiAnalyticBasketPricer.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id$
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  /// <summary>
  ///   Semi-nalytic basket model with correlated counterparty risks.
  ///   For internal use only.
  ///   <prelimninary/>
  /// </summary>
  [Serializable]
  public class CPRiskSemiAnalyticBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CPRiskSemiAnalyticBasketPricer));

    #region Constructors

    /// <exclude />
    internal protected CPRiskSemiAnalyticBasketPricer()
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
    /// <param name="counterpartyCurve">Counterparty survival curve</param>
    /// <param name="counterpartyCorrelation">Counterparty correlation</param>
    ///
    public CPRiskSemiAnalyticBasketPricer(
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
      Array lossLevels,
      SurvivalCurve counterpartyCurve,
      double counterpartyCorrelation
      )
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating CPRisk Semi-analytic Basket asof={0}, settle={1}, maturity={2}",
        asOf, settle, maturity);

      this.CounterpartyCurve = counterpartyCurve;
      this.CounterpartyCorrelation = counterpartyCorrelation;
      correlatedLossDistribution_ = null;
      cpStartSurvivalProb_ = Double.NaN;

      this.distCalc_ = new SemiAnalyticBasketPricer();
      this.CopyTo(distCalc_);

      logger.Debug("Basket created");
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="basket">The basket</param>
    /// <param name="counterpartyCurve">Counterparty survival curve</param>
    /// <param name="counterpartyCorrelation">Counterparty correlation</param>
    ///
    public CPRiskSemiAnalyticBasketPricer(
      BasketPricer basket,
      SurvivalCurve counterpartyCurve,
      double counterpartyCorrelation
      )
      : base()
    {
      basket.CopyTo(this);

      // CP risk specifications
      this.CounterpartyCurve = counterpartyCurve;
      this.CounterpartyCorrelation = counterpartyCorrelation;
      correlatedLossDistribution_ = null;
      cpStartSurvivalProb_ = Double.NaN;
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
      CPRiskSemiAnalyticBasketPricer obj = (CPRiskSemiAnalyticBasketPricer)base.Duplicate();

      // duplicate the calculation device
      obj.distCalc_ = distCalc_.Duplicate();

      // Make clone of computation devices
      obj.correlatedLossDistribution_ =
        correlatedLossDistribution_ == null ? null : correlatedLossDistribution_.clone();

      return obj;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CPRiskSemiAnalyticBasketPricer obj = (CPRiskSemiAnalyticBasketPricer)base.Clone();

      // Clone the calculation device
      obj.distCalc_ = (BasketPricer)distCalc_.Clone();
      obj.CopyTo(obj.distCalc_);
      obj.correlatedLossDistribution_ =
        correlatedLossDistribution_ == null ? null : correlatedLossDistribution_.clone();
      return obj;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    ///    Initialize distribution object
    /// </summary>
    /// <param name="start">start date</param>
    /// <param name="stop">stop date</param>
    /// <param name="stepSize">step size of time grid</param>
    /// <param name="stepUnit">step unit of time grid</param>
    /// <param name="nGroups">number of groups</param>
    /// <param name="levels">loss levels</param>
    /// <param name="lossDistributions">distribution of losses</param>
    /// <param name="amorDistributions">distribution of amortizations</param>
    private void InitializeDistributions(
      Dt start, Dt stop,
      int stepSize, TimeUnit stepUnit,
      int nGroups, double[] levels,
      Curve2D lossDistributions, Curve2D amorDistributions)
    {
      IList<Dt> dates = GenerateGridDates(start, stop, stepSize, stepUnit, null);
      InitializeDistributions(start, dates, levels, nGroups,
        lossDistributions, amorDistributions);
    }

    /// <summary>
    ///    Initialize distribution object
    /// </summary>
    /// <param name="start">start date</param>
    /// <param name="dates">date grid</param>
    /// <param name="nGroups">number of groups</param>
    /// <param name="levels">loss levels</param>
    /// <param name="lossDistributions">distribution of losses</param>
    /// <param name="amorDistributions">distribution of amortizations</param>
    private void InitializeDistributions(
      Dt start,
      IList<Dt> dates, IList<double> levels, int nGroups,
      Curve2D lossDistributions, Curve2D amorDistributions)
    {
      int nDates = dates.Count;
      int nLevels = levels.Count;

      lossDistributions.Initialize(nDates, nLevels, nGroups);
      lossDistributions.SetAsOf(start);
      for (int i = 0; i < nDates; ++i)
        lossDistributions.SetDate(i, dates[i]);
      for (int i = 0; i < nLevels; ++i)
        lossDistributions.SetLevel(i, levels[i]);

      if (amorDistributions == null)
        return;

      amorDistributions.Initialize(nDates, nLevels, nGroups);
      amorDistributions.SetAsOf(start);
      for (int i = 0; i < nDates; ++i)
        amorDistributions.SetDate(i, dates[i]);
      for (int i = 0; i < nLevels; ++i)
        amorDistributions.SetLevel(i, levels[i]);

      return;
    }

    /// <summary>
    ///   For internal use only
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    ///   Compute the whole distributions over time, save the result for later use
    /// </remarks>
    private void ComputeAndSaveDistribution()
    {
      this.CopyTo(distCalc_);
      distCalc_.Reset();

      SurvivalCurve counterpartyCurve = this.CounterpartyCurve;
      if (counterpartyCurve == null)
      {
        correlatedLossDistribution_ = null;
        distributionComputed_ = true;
        return;
      }

      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing distribution for semy-analytic basket with counterparty");

      // start date and survival probability
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      cpStartSurvivalProb_ = counterpartyCurve.Interpolate(start);
      if (cpStartSurvivalProb_ < 1E-7)
        cpStartSurvivalProb_ = 1.0; // to avoid dividing by zero

      // Initialize distributions
      int startDateIndex = this.RecalcStartDateIndex;
      if (correlatedLossDistribution_ == null || startDateIndex < 0)
      {
        if (correlatedLossDistribution_ == null)
          correlatedLossDistribution_ = new Curve2D();
        InitializeDistributions(start, TimeGrid, CookedLossLevels, 1,
          correlatedLossDistribution_, null);
        startDateIndex = 0;
      }

      if (!UseCorrelatedCPRiskModel())
      {
        //SemiAnalyticBasketModel.ComputeDistributions(
        //  startDateIndex, correlatedLossDistribution_.NumDates(),
        //  LossDistribution,
        //  correlatedLossDistribution_);
      }
      else
      {
        double[] recoveryRates = RecoveryRates;
        double[] recoveryDispersions = RecoveryDispersions;

        CorrelationTermStruct corr = this.CorrelationTermStruct;

        // allow negative correlation on the counterparty
        double counterpartyCorrelation = this.CounterpartyCorrelation;
        double cpFactor = counterpartyCorrelation < 0 ?
          (-Math.Sqrt(-counterpartyCorrelation)) : Math.Sqrt(counterpartyCorrelation);

        // calculate the distributions with correlation counterparty risks
        SemiAnalyticBasketModel.ComputeDistributions(
          false,
          startDateIndex, correlatedLossDistribution_.NumDates(),
          CopulaType, DfCommon, DfIdiosyncratic, this.Copula.Data,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          IntegrationPointsFirst, IntegrationPointsSecond,
          SurvivalCurves, Principals, recoveryRates, recoveryDispersions,
          counterpartyCurve, cpFactor,
          GridSize,
          correlatedLossDistribution_,
          SemiAnalyticBasketPricer.EmptyDistribution);
      }
      distributionComputed_ = true;

      timer.stop();
      logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());
      return;
    }

    ///
    /// <summary>
    ///   Compute the accumulated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    public override double AccumulatedLoss(
      Dt date,
      double trancheBegin,
      double trancheEnd)
    {
      if (!distributionComputed_)
        ComputeAndSaveDistribution();

      if (correlatedLossDistribution_ == null)
        return distCalc_.AccumulatedLoss(date, trancheBegin, trancheEnd);

      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);
      loss += correlatedLossDistribution_.Interpolate(date, trancheBegin, trancheEnd) / TotalPrincipal;

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
    public override double AmortizedAmount(
      Dt date,
      double trancheBegin,
      double trancheEnd)
    {
      if (!distributionComputed_)
        ComputeAndSaveDistribution();

      // expected amortizations without counterparty risks
      double amor_ncr = distCalc_.AmortizedAmount(date, trancheBegin, trancheEnd);
      if (correlatedLossDistribution_ == null)
        return amor_ncr;

      // expected losses without counterparty risks
      double loss_ncr = distCalc_.AccumulatedLoss(date, trancheBegin, trancheEnd);

      // expected balance without counterparty risks
      double bal_ncr = 1 - loss_ncr - amor_ncr;

      // expected amortization with counterparty risks
      double amor = 1 - distCalc_.AccumulatedLoss(date, trancheBegin, trancheEnd)
        - CounterpartySurvivalProbability(date) * bal_ncr;
      return amor;
    }

    /// <summary>
    ///   Calculate counterparty survival probability at a date
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Survival probability</returns>
    private double CounterpartySurvivalProbability(Dt date)
    {
      SurvivalCurve counterpartyCurve = this.CounterpartyCurve;
      if (counterpartyCurve == null)
        return 1.0;
      double p = counterpartyCurve.Interpolate(date) / cpStartSurvivalProb_;
      if (p < 0.0)
        return 0.0;
      if (p > 1.0)
        return 1.0;
      return p;
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
      distCalc_.Reset();
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
        == SyntheticCDOPricer.ResetFlag.Settle && correlatedLossDistribution_ != null)
      {
        Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
        if (start != correlatedLossDistribution_.GetAsOf())
          correlatedLossDistribution_.SetAsOf(start);
      }
      this.CopyTo(distCalc_);
      distCalc_.Reset(what);
      return;
    }

    /// <summary>
    ///   Set the re-calculation start date
    /// </summary>
    /// 
    /// <remarks>
    ///   After a successfully call to this function, every call to Reset()
    ///   will only reset the the loss distributions after the
    ///   <paramref name="date">re-calculation start date</paramref> and
    ///   recalculate thems.  The loss distributions for the 
    ///   for the dates before the start date will not be reset.
    ///   This behaviour persists until another call to this function
    ///   with a different date or an empty date, in the later case the
    ///   re=calculation always begins with the protection start date. 
    /// </remarks>
    /// 
    /// <param name="date">
    ///   Date from which to start re-calculation of loss distributions.
    ///   An empty dates means to the use the protection start date.
    /// </param>
    /// <param name="keepPrevResult">
    ///   If true, this function assumes the loss distributions for the dates
    ///   before the start date have already calculated and it will not calculate
    ///   them;  Otherwise, the distributions for previous dates are updated once
    ///   and the results are saved for later use.
    /// </param>
    internal protected override void SetRecalculationStartDate(Dt date, bool keepPrevResult)
    {
      MySetRecalculationStartDate(date, keepPrevResult);
      this.CopyTo(distCalc_);
      distCalc_.SetRecalculationStartDate(date, keepPrevResult);
    } 

    private void MySetRecalculationStartDate(Dt date, bool keepPrevResult)
    {
      int startDateIndex = this.RecalcStartDateIndex;
      if (date.IsEmpty())
      {
        if (startDateIndex != -1)
        {
          this.RecalcStartDateIndex = -1;
          distributionComputed_ = false;
        }
        return;
      }

      bool timeGridChanged = this.TimeGridChanged;
      UniqueSequence<Dt> timeGrid = this.TimeGrid;
      int index = timeGrid.BinarySearch(date);
      if (index<0)
        index = ~index;
      this.RecalcStartDateIndex = startDateIndex = index;

      // Case 1: we want to re calculate everything
      if (!keepPrevResult)
      {
        correlatedLossDistribution_ = null;
        distributionComputed_ = false;
        return;
      }

      // Case 2: keep previous result and nothing changed
      if (correlatedLossDistribution_ == null || index <= 0 || !timeGridChanged)
        return;

      // Case 3: we want to keep previous results and the time
      //   grid changed.

      // First check if we need to recalculate everything.
      if (correlatedLossDistribution_.NumDates() < index
        || Dt.Cmp(correlatedLossDistribution_.GetDate(index - 1), timeGrid[index - 1]) != 0)
      {
        correlatedLossDistribution_ = null; // recalculate the whole distribution
        distributionComputed_ = false;
        return;
      }

      // Now we can keep the previous results, we resize the distribution
      //   objects to accomodate the time grids after the given date.
      int count = timeGrid.Count;
      correlatedLossDistribution_.ResizeByDates(count, index);
      for (int i = index; i < count; ++i)
        correlatedLossDistribution_.SetDate(i, timeGrid[i]);

      distributionComputed_ = false;
      return;
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
    ///   <para>Recalculation is avoided if the basket and altSurvivalCurves are the same.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column identifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
    public override double[,] BumpedPvs(
      SyntheticCDOPricer[] pricers,
      SurvivalCurve[] altSurvivalCurves
      )
    {
      if (UseCorrelatedCPRiskModel())
        return this.GenericBumpedPvs(pricers,altSurvivalCurves);
      else
        return distCalc_.BumpedPvs(pricers,altSurvivalCurves);
    }

    /// <summary>
    ///   Fast calculation of the MTM values for a series of Synthetic CDO tranches,
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
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column identifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    internal protected override double[,] BumpedPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity)
    {
      if (UseCorrelatedCPRiskModel())
        return this.GenericBumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);
      else
        return distCalc_.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);
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
    public override double[,] CalcLossDistribution(
      bool wantProbability, Dt date, double[] lossLevels)
    {
      throw new NotImplementedException(
        "CalcLossDistribution() with counterparty risks is not implemented yet");
    }

    /// <summary>
    ///   Check if we need and actually can apply the correlated CP risk model
    /// </summary>
    /// <returns>True if we need correlated CP model; false otherwise</returns>
    private bool UseCorrelatedCPRiskModel()
    {
      if (this.CounterpartyCurve == null)
        return false;
      double counterpartyCorrelation = this.CounterpartyCorrelation;
      if (this.RefinanceCurves != null || this.EarlyMaturities != null)
      {
        if (counterpartyCorrelation != 0.0 && !Double.IsNaN(counterpartyCorrelation))
        {
          throw new ToolkitException(String.Format(
            "Cannot do correlated counterparty risks for {0}.",
            this.RefinanceCurves != null ? "LCDO" : "early maturities"));
        }
        return false;
      }
      return !Double.IsNaN(counterpartyCorrelation);
    }
    #endregion Methods

    #region Properties
    #endregion Properties

    #region Data
    private BasketPricer distCalc_;

    // intermediate results
    private bool distributionComputed_;
    private Curve2D correlatedLossDistribution_;
    private double cpStartSurvivalProb_;
    #endregion Data
  } // class CPRiskSemiAnalyticBasketPricer

}
