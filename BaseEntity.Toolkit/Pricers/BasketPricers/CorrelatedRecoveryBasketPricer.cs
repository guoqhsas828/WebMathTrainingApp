/*
 * CorrelatedRecoveryBasketPricer.cs
 *
 *
 */
using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
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
  /// <summary>
  ///    Corelated recovery basket pricer
  /// </summary>
  [Serializable]
  public class CorrelatedRecoveryBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CorrelatedRecoveryBasketPricer));

    #region Config
    /// <exclude />
    public static readonly double FactorInterpStart = 0.99999;

    #endregion // Config

    #region Constructors

    /// <exclude />
    internal protected CorrelatedRecoveryBasketPricer()
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
    /// <param name="recoveryCorrelations">Array of recovery correlations</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Factor correlations for the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    public CorrelatedRecoveryBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] recoveryCorrelations,
      double[] principals,
      Copula copula,
      Correlation correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels )
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
        copula, correlation, stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating semi-analytic Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);

      // Validate
      //
      // The correlation object MUST be a term structure!
      if (!(correlation is FactorCorrelation || correlation is CorrelationTermStruct))
        throw new System.ArgumentException(String.Format(
          "The correlation must be either FactorCorrelation or CorrelationTermStruct, not {0}",
          correlation.GetType()));

      this.recoveryCorrelations_ = recoveryCorrelations;
      this.lossDistribution_ = null;
      this.amorDistribution_ = null;
      this.distributionComputed_ = false;

      logger.Debug("Basket created");
    }


    /// <summary>
    ///   Duplicate a basket pricer
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Duplicate() differs from Clone() in that it copies by references all the 
    ///   basic data and numerical options defined in the BasketPricer class.  But it is
    ///   not the same as the MemberwiseClone() function, since it does not copy by reference
    ///   the computational data such as LossDistributions in CorrelatedRecoveryBasketPricer class.
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
      CorrelatedRecoveryBasketPricer obj = (CorrelatedRecoveryBasketPricer)base.Duplicate();

      // Don't clone the user input data: recoveryCorrelations_;
      // Make clone of computation devices
      obj.lossDistribution_ = lossDistribution_ == null ? null : lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_ == null ? null : amorDistribution_.clone();

      return obj;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      CorrelatedRecoveryBasketPricer obj = (CorrelatedRecoveryBasketPricer)base.Clone();

      obj.lossDistribution_ = lossDistribution_ == null ? null : lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_ == null ? null : amorDistribution_.clone();
      obj.recoveryCorrelations_ = CloneUtil.Clone(recoveryCorrelations_);
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
    ///   Calculate the maximum level of possible amortizations.
    /// </summary>
    /// <remarks>
    ///   <para>This function calculates the sum of maximum possible
    ///   amortization by names.</para>
    ///   
    ///   <para>Normally, the maximum possible amortization level of
    ///   an individual name equals its recovery rate times notional.
    ///   However, if one name has positive recovery dispersion, or
    ///   it has a maturity earlier than basket maturity, or it is
    ///   a LCDS, then the maximum possible amortization rate
    ///   is taken as 100% and the amortization level equals to
    ///   its notional.</para>
    /// 
    ///  <para>The derived classes with their own prepayment assumption
    ///   and recovery treatments may override this
    ///   method to calculate the correct values.</para>
    /// </remarks>
    /// <returns>
    ///   The maximum amortization level, expressed as a share in
    ///   the basket total principal (0.01 means 1%).
    /// </returns>
    internal protected override double MaximumAmortizationLevel()
    {
      SurvivalCurve[] refinanceCurves = this.RefinanceCurves;
      Dt[] maturities = this.EarlyMaturities;
      if (maturities == null && refinanceCurves == null)
        return base.MaximumAmortizationLevel();

      double[] dispersions = RecoveryDispersions;
      double[] rates = RecoveryRates;
      double[] principals = Principals;
      double amor = 0;
      for (int i = 0; i < rates.Length; ++i)
        amor += principals[i] * (
          dispersions[i] > 0
          || refinanceCurves[i] != null
          || !maturities[i].IsEmpty() ? 1 : rates[i]);
      return PreviousAmortized + amor / TotalPrincipal;
    }

    /// <summary>
    ///   For internal use only
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    ///   Compute the whole distribution, save the result for later use
    /// </remarks>
    internal void ComputeAndSaveDistribution()
    {
      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing distribution for semy-analytic basket");

      double[] recoveryRates = RecoveryRates;
      double[] recoveryDispersions = RecoveryDispersions;
      double[] recoveryCorrelations = recoveryCorrelations_;

      CorrelationTermStruct corr = this.CorrelationTermStruct;
            
      
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

      // Initialize distributions
      int startDateIndex = this.RecalcStartDateIndex;
      if (lossDistribution_ == null || startDateIndex < 0)
      {
        if (lossDistribution_ == null)
          lossDistribution_ = new Curve2D();
        if (this.NoAmortization)
          amorDistribution_ = null;
        else if (amorDistribution_ == null)
          amorDistribution_ = new Curve2D();
        InitializeDistributions(start, TimeGrid, CookedLossLevels, 1,
          lossDistribution_, amorDistribution_);
        startDateIndex = 0;
      }
      lossDistribution_.Accuracy = AccuracyLevel;

      // Calculate distributions
      CorrelatedRecoveryBasketModel.ComputeDistributions(
          false, startDateIndex, lossDistribution_.NumDates(),
          CopulaType, DfCommon, DfIdiosyncratic, this.Copula.Data,
          corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          IntegrationPointsFirst, IntegrationPointsSecond,
          new double[0], SurvivalCurves, 
          Principals, 
          recoveryRates, 
          recoveryDispersions,
          recoveryCorrelations,
          GridSize,
          lossDistribution_,
          amorDistribution_ == null ? emptyDistribution : amorDistribution_);

      distributionComputed_ = true;

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

      // initialize distributions
      Curve2D lossDistribution = new Curve2D();
      InitializeDistributions(start, date,
        100, TimeUnit.Years, 1, lossLevels,
        lossDistribution, null);

      // Calculate distributions
      Copula copula = this.Copula;
      CorrelatedRecoveryBasketModel.ComputeDistributions(
        wantProbability, 0, lossDistribution.NumDates(),
        copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
        corr.Correlations,
        corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
        this.IntegrationPointsFirst, this.IntegrationPointsSecond,
        new double[]{}, SurvivalCurves, Principals,
        recoveryRates, recoveryDispersions, recoveryCorrelations_,
        GridSize, lossDistribution, emptyDistribution);
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

    ///
    /// <summary>
    ///   Compute the accumulated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    public override double
    AccumulatedLoss(
        Dt date,
        double trancheBegin,
        double trancheEnd)
    {
      int groupIdx = 0;
      if (!distributionComputed_)
        ComputeAndSaveDistribution();
      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);

      loss += lossDistribution_.Interpolate(groupIdx, date, trancheBegin, trancheEnd) / TotalPrincipal;

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
    public override double
    AmortizedAmount(
        Dt date,
        double trancheBegin,
        double trancheEnd)
    {
      int groupIdx = 0;
      if (!distributionComputed_)
        ComputeAndSaveDistribution();
      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;
      AdjustTrancheLevels(true, ref tBegin, ref tEnd, ref amortized);
      if (amorDistribution_ != null)
        amortized += amorDistribution_.Interpolate(groupIdx, date, tBegin, tEnd) / TotalPrincipal;

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
      if (index < 0)
        index = ~index;
      this.RecalcStartDateIndex = startDateIndex = index;

      // Case 1: we want to re calculate everything
      if (!keepPrevResult)
      {
        lossDistribution_ = null;
        distributionComputed_ = false;
        return;
      }

      // Case 2: keep previous result and nothing changed
      if (lossDistribution_ == null || index <= 0 || !timeGridChanged)
        return;

      // Case 3: we want to keep previous results and the time
      //   grid changed.

      // First check if we need to recalculate everything.
      if (lossDistribution_.NumDates() < index
        || Dt.Cmp(lossDistribution_.GetDate(index - 1), timeGrid[index - 1]) != 0)
      {
        lossDistribution_ = null; // recalculate the whole distribution
        distributionComputed_ = false;
        return;
      }

      // Now we can keep the previous results, we resize the distribution
      //   objects to accomodate the time grids after the given date.
      int count = timeGrid.Count;
      lossDistribution_.ResizeByDates(count, index);
      for (int i = index; i < count; ++i)
        lossDistribution_.SetDate(i, timeGrid[i]);

      if (amorDistribution_ != null)
      {
        amorDistribution_.ResizeByDates(count, index);
        for (int i = index; i < count; ++i)
          amorDistribution_.SetDate(i, timeGrid[i]);
      }

      distributionComputed_ = false;
      return;
    }

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

    internal static Curve2D EmptyDistribution
    {
      get { return emptyDistribution; }
    }
    #endregion // Properties

    #region Data
    // recovery correlation
    private double[] recoveryCorrelations_;

    // Calculated distribution data
    private Curve2D lossDistribution_;
    private Curve2D amorDistribution_;
    private bool distributionComputed_;

    private static readonly Curve2D emptyDistribution = new Curve2D();
    #endregion Data

    #region Factory Methods
    /// <summary>
    ///  Create a basket pricer
    /// </summary>
    /// <param name="portfolioStart">Portfolio start date</param>
    /// <param name="asOfDate">Asof date</param>
    /// <param name="settleDate">Settlement date</param>
    /// <param name="maturityDate">Maturity date</param>
    /// <param name="survivalCurves">Array of survival curvers</param>
    /// <param name="recoveryCorrelations">Array of recovery correlations</param>
    /// <param name="principals">Array of principals</param>
    /// <param name="copula">Copula</param>
    /// <param name="correlation">Correlation</param>
    /// <param name="stepSize">Step size (such as 3)</param>
    /// <param name="stepUnit">Step unit (such as Months)</param>
    /// <param name="lossLevels">Array of loss levels</param>
    /// <param name="quadraturePoints">NUmber of quadrature points</param>
    /// <param name="gridSize">Grid size</param>
    /// <returns>Basket pricer</returns>
    public static CorrelatedRecoveryBasketPricer CreateBasket(
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      Dt maturityDate,
      SurvivalCurve[] survivalCurves,
      double[] recoveryCorrelations,
      double[] principals,
      Copula copula,
      object correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels,
      int quadraturePoints,
      double gridSize
    )
    {
      // Set up basket arguments
      SurvivalCurve[] sc;
      RecoveryCurve[] rc;
      double[] prins;
      double[] picks;
      BasketPricerFactory.SetupArgs(survivalCurves, principals, out sc, out rc, out prins, out picks);
      recoveryCorrelations = ArrayUtil.PickElements(recoveryCorrelations, picks);

      CorrelatedRecoveryBasketPricer basket;
      if (correlation is BaseCorrelationObject)
      {
        basket = new CorrelatedRecoveryBasketPricer(asOfDate, settleDate, maturityDate,
          sc, rc, recoveryCorrelations, prins, copula,
          BasketPricerFactory.DefaultSingleFactorCorrelation(sc), stepSize, stepUnit, lossLevels);
      }
      else
      {
        if (correlation is double)
          correlation = new SingleFactorCorrelation(new string[sc.Length], (double)correlation);
        else if (correlation is double[])
          correlation = new FactorCorrelation(new string[sc.Length],
            ((double[])correlation).Length / sc.Length, (double[])correlation);

        if (correlation is CorrelationTermStruct && copula.CopulaType == CopulaType.Poisson)
        {
          basket = new CorrelatedRecoveryBasketPricer(asOfDate, settleDate, maturityDate,
          sc, rc, recoveryCorrelations, prins, copula,
          (CorrelationTermStruct)correlation, stepSize, stepUnit, lossLevels);
        }
        else if (correlation is CorrelationObject)
        {
          FactorCorrelation corr = correlation is SingleFactorCorrelation ?
            (SingleFactorCorrelation)correlation :
            CorrelationFactory.CreateFactorCorrelation((Correlation)correlation, picks);
          basket = new CorrelatedRecoveryBasketPricer(asOfDate, settleDate, maturityDate,
          sc, rc, recoveryCorrelations, prins, copula, corr, stepSize, stepUnit, lossLevels);
        }
        else
          throw new ArgumentException("Invalid correlation parameter");
      }
      if (quadraturePoints <= 0)
        quadraturePoints = BasketPricerFactory.DefaultQuadraturePoints(copula, sc.Length);
      basket.IntegrationPointsFirst = quadraturePoints;
      if (gridSize > 0.0)
        basket.GridSize = gridSize;
      if (portfolioStart.IsValid())
        basket.PortfolioStart = portfolioStart;

      return basket;
    }

    /// <summary>
    ///   Creates a pricer for Synthetic CDO using the generalized semi-analytic basket
    ///   model.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Convenience wrapper for <see href="BaseEntity.Toolkit.Pricers.SyntheticCDOHeterogeneousPricer"/>.</para>
    /// </remarks>
    ///
    /// <param name="cdo">Synthetic CDO product or array of CDOs sharing same asset pool</param>
    /// <param name="portfolioStart">Date to start loss distribution calculation</param>
    /// <param name="asOfDate">Pricing as-of date</param>
    /// <param name="settleDate">Settlement date for pricing</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurves">Array of Survival Curves for each basket name</param>
    /// <param name="recoveryCorrelations">Array of recovery rates correlations</param>
    /// <param name="principals">Array of original face amounts for each basket name (may be null, empty or contain zeros)</param>
    /// <param name="copula">Copula object</param>
    /// <param name="correlation">Any type of correlation object. Converted to appropriate correlation as required</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time (or 0 for default)</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years (or None for default)</param>
    /// <param name="quadraturePoints">Number of quadrature points used in numerical integration (or 0 for default)</param>
    /// <param name="gridSize">The grid used to update probabilities</param>
    /// <param name="notional">Notional amount(s) for product(s). May be null (defaults to tranch size),
    ///   a single notional for all tranches, or an array of notionals matching each tranche</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="rateResets">Rate resets for funded floating CDOs, or null</param>
    ///
    /// <returns>Constructed generalized semi-analytic Synthetic CDO pricer</returns>
    ///
    public static SyntheticCDOPricer[] CreateCDOPricers(
      SyntheticCDO[] cdo,
      Dt portfolioStart,
      Dt asOfDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] recoveryCorrelations,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      int quadraturePoints,
      double gridSize,
      double[] notional,
      bool rescaleStrikes,
      params List<RateReset>[] rateResets
      )
    {
      // Validation
      BasketPricerFactory.Validate(cdo, notional, rateResets);

      // Find the latest maturity from CDO array
      Dt maturityDate = ProductUtil.LastMaturity(cdo);

      // Find the loss levels from CDO tranches
      double[,] lossLevels = BasketPricerFactory.LossLevelsFromTranches(cdo);

      // Create a basket pricer
      CorrelatedRecoveryBasketPricer basket = CreateBasket(
        portfolioStart, asOfDate, settleDate, maturityDate,
        survivalCurves, recoveryCorrelations, principals, copula,
        correlation, stepSize, stepUnit, lossLevels, quadraturePoints, gridSize);
      if (quadraturePoints <= 0)
        basket.IntegrationPointsFirst +=
          BasketPricerFactory.DefaultQuadraturePointsAdjust(copula.CopulaType, cdo);
      BasketPricerFactory.AddGridDates(basket, cdo);
      double maxBasketAmortLevel = basket.MaximumAmortizationLevel();
      double minCdoAmortLevel = BasketPricerFactory.MinimumAmortizationLevel(cdo);
      if (maxBasketAmortLevel <= minCdoAmortLevel)
        basket.NoAmortization = true;

      // Create Synthetic CDO Pricers
      CorrelationObject sharedCorrelation = null;
      SyntheticCDOPricer[] pricer = new SyntheticCDOPricer[cdo.Length];
      for (int i = 0; i < cdo.Length; i++)
      {
        if ((cdo[i] != null) &&
            ((notional == null) || (notional.Length == 0) ||
             (notional[i < notional.Length ? i : 0] != 0.0)))
        {
          BasketPricer basketPricer;
          if (correlation is BaseCorrelationObject)
          {
            if (maxBasketAmortLevel <= BasketPricerFactory.MinimumAmortizationLevel(cdo[i]))
              basket.NoAmortization = true;
            basketPricer = new BaseCorrelationBasketPricer(
                basket, discountCurve, (BaseCorrelationObject)correlation,
                rescaleStrikes, cdo[i].Attachment, cdo[i].Detachment);
            basketPricer.Maturity = cdo[i].Maturity;
            basket.NoAmortization = (maxBasketAmortLevel <= minCdoAmortLevel);
          }
          else
            basketPricer = basket;

          // make sure these pricers share the same correlation object
          if (sharedCorrelation == null)
            sharedCorrelation = basketPricer.Correlation;
          else
            basketPricer.Correlation = sharedCorrelation;

          pricer[i] = new SyntheticCDOPricer(cdo[i], basketPricer, discountCurve, 1.0,
            rateResets == null || rateResets.Length == 0 ? null
              : rateResets[i < rateResets.Length ? i : 0]);
          pricer[i].Notional = (notional == null || notional.Length == 0)
                                   ? basketPricer.TotalPrincipal * cdo[i].TrancheWidth
                                   : notional[i < notional.Length ? i : 0];
          pricer[i].Basket.Reset();
        }
      }
      // done
      return pricer;
    }

    #endregion Factory Methods
  } // class CorrelatedRecoveryBasketPricer

}