/*
 * SemiAnalyticBasketPricer.cs
 *
 */

#define USE_OWN_BumpedPvs
// #define INCLUDE_EXTRA_DEBUG // Define to include exra debug output

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Concurrency;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  #region Config
  /// <exclude />
  [Serializable]
  public class SemiAnalyticBasketPricerConfig
  {
    // /// <exclude />
    // [ToolkitConfig("Interpolate loss distribution for correlation factor larger than this value.")]
    // public readonly double FactorInterpStart = 0.99999;

    /// <exclude />
    [ToolkitConfig("Whether to enable multicore support in the semi-analytic model.")]
    public readonly bool MulticoreSupport = true; // added 9.2

    /// <exclude />
    [ToolkitConfig("Whether to use the old, proportional probability model.")]
    public readonly bool UseOldLcdxTrancheModel = false; // added 9.2

    /// <exclude />
    [ToolkitConfig("Whether to make the fixed recoveries stochastic in correlated recovery model.")]
    public readonly bool StochasticFixedRecovery = false; // added 9.3
  }
  #endregion // Config

  /// <summary>
  ///   Pricing helper class for Heterogeneous basket pricer
  /// </summary>
  /// <remarks>
  ///   <para>This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.</para>
  ///   <para>BasketPricer classes are typically used internally by Pricer classes and are not used
  ///   directly by the user.</para>
  /// </remarks>
  [Serializable]
  public class SemiAnalyticBasketPricer : BasketPricer, IAnalyticDerivativesProvider
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SemiAnalyticBasketPricer));

    #region Config
    private bool MulticoreSupport
    {
      get { return ParallelSupport.Enabled; }
    }
    #endregion Config

    #region Constructors

    /// <exclude />
    internal protected SemiAnalyticBasketPricer()
    {
        
    }

    internal SemiAnalyticBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      CreditPool basket,
      Copula copula,
      Correlation correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels
      )
      : base(asOf, settle, maturity, basket, copula, correlation,
        stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating semi-analytic Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);
       
      // Validate
      //
      // The correlation object MUST be a term structure!
      if (!(correlation is FactorCorrelation || correlation is CorrelationTermStruct))
        throw new System.ArgumentException(String.Format(
          "The correlation must be either FactorCorrelation or CorrelationTermStruct, not {0}",
          correlation.GetType()));

      cCurves_ = rCurves_ = null;
      this.lossDistribution_ = null;
      this.amorDistribution_ = null;
      this.distributionComputed_ = false;

#if USE_OWN_BumpedPvs
      this.wantSensitivity_ = false;
      this.bumpedCurveIndex_ = 0;
#endif

      logger.Debug("Basket created");
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
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    ///
    public SemiAnalyticBasketPricer(
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
      bool checkRefinance
      )
      : this(asOf, settle, maturity, new CreditPool(principals, survivalCurves,
        recoveryCurves, null, null, checkRefinance, null), copula, correlation,
        stepSize, stepUnit, lossLevels)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="maturities">Maturity dates of individual names</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Factor correlations for the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    ///
    public SemiAnalyticBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      Dt[] maturities,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      Copula copula,
      Correlation correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels,
      bool checkRefinance
      )
      : this(asOf, settle, maturity, new CreditPool(principals, survivalCurves,
        recoveryCurves, null, null, checkRefinance, maturities),
        copula, correlation, stepSize, stepUnit, lossLevels)
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
    /// <param name="refinanceCurves">Refinance curves</param>
    /// <param name="refinanceCorrelations">Correlations between refinance and default</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="copula">Copula structure</param>
    /// <param name="correlation">Factor correlations for the names in the basket</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    public SemiAnalyticBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      SurvivalCurve[] refinanceCurves,
      double[] refinanceCorrelations,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      Copula copula,
      Correlation correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels
      )
      : this(asOf, settle, maturity, new CreditPool(principals, survivalCurves,
        recoveryCurves, refinanceCurves, refinanceCorrelations,
        refinanceCurves != null, null), copula, correlation,
        stepSize, stepUnit, lossLevels)
    {
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      SemiAnalyticBasketPricer obj = (SemiAnalyticBasketPricer)base.Clone();

      obj.lossDistribution_ = lossDistribution_ == null ? null : lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_ == null ? null : amorDistribution_.clone();
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
      SemiAnalyticBasketPricer obj = (SemiAnalyticBasketPricer)base.Duplicate();

      // Make clone of computation devices
      obj.lossDistribution_ = lossDistribution_ == null ? null : lossDistribution_.clone();
      obj.amorDistribution_ = amorDistribution_ == null ? null : amorDistribution_.clone();

      return obj;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Check if the survival curves contain any refinance info
    /// </summary>
    /// <param name="curves">survival curves</param>
    /// <returns>True if contain refianace info</returns>
    public new static bool HasRefinanceCurves(SurvivalCurve[] curves)
    {
      foreach (SurvivalCurve curve in curves)
      {
        if (curve.SurvivalCalibrator != null && curve.SurvivalCalibrator.CounterpartyCurve != null)
          return true;
      }
      return false;
    }

    /// <summary>
    ///   Get refinance curves and correlations from survivalCurves
    /// </summary>
    public void GetRefinanceInfosFromSurvivalCurves()
    {
      SurvivalCurve[] refinanceCurves = null;
      double[] refinanceCorrelations = null;
      SurvivalCurve[] survivalCurves = this.SurvivalCurves;
      if (HasRefinanceCurves(survivalCurves))
      {
        int N = survivalCurves.Length;
        refinanceCurves = new SurvivalCurve[N];
        refinanceCorrelations = new double[N];
        for (int i = 0; i < N; ++i)
        {
          SurvivalCurve curve = survivalCurves[i];
          if (curve.SurvivalCalibrator != null)
          {
            SurvivalCalibrator calibrator = curve.SurvivalCalibrator;
            refinanceCurves[i] = calibrator.CounterpartyCurve;
            refinanceCorrelations[i] = calibrator.CounterpartyCorrelation;
          }
        }
      }
      RefinanceCurves = refinanceCurves;
      RefinanceCorrelations = refinanceCorrelations;
      return;
    }

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
      lossDistributions.Accuracy = AccuracyLevel;

      if (amorDistributions == null)
        return;

      amorDistributions.Initialize(nDates, nLevels, nGroups);
      amorDistributions.SetAsOf(start);
      for (int i = 0; i < nDates; ++i)
        amorDistributions.SetDate(i, dates[i]);
      for (int i = 0; i < nLevels; ++i)
        amorDistributions.SetLevel(i, levels[i]);
      amorDistributions.Accuracy = AccuracyLevel;

      return;
    }
    
    
    /// <summary>
    ///   Transform survival curves and refinance curves
    /// </summary>
    /// <param name="start">start date</param>
    /// <param name="stop">stop date</param>
    private void TransformRefinanceCurves(Dt start, Dt stop)
    {
      if (cCurves_ != null)
        return;

      SurvivalCurve[] survivalCurves = this.SurvivalCurves;
      double[] refinanceCorrelations = this.RefinanceCorrelations;
      SurvivalCurve[] refinanceCurves = this.RefinanceCurves;
      if (refinanceCurves == null)
      {
        cCurves_ = survivalCurves;
        rCurves_ = new SurvivalCurve[0];
        return;
      }

      int N = survivalCurves.Length;
      if (N != refinanceCurves.Length)
        throw new System.ArgumentException(String.Format(
          "Lengths of SurvivalCurves ({0}) and RefinanceCurves ({1}) not match",
          survivalCurves.Length, refinanceCurves.Length));
      if (N != refinanceCorrelations.Length)
        throw new System.ArgumentException(String.Format(
          "Lengths of RefinanceCurves ({0}) and RefinanceCorrelations ({1}) not match",
          N, refinanceCorrelations.Length));

      cCurves_ = new SurvivalCurve[N];
      rCurves_ = new SurvivalCurve[N];
      for (int i = 0; i < N; ++i)
      {
        SurvivalCurve cCurve, rCurve;
        if (refinanceCurves[i] == null)
        {
          cCurve = survivalCurves[i];
          rCurve = null;
        }
        else
        {
          cCurve = new SurvivalCurve(start);
          rCurve = new SurvivalCurve(start);
          CounterpartyRisk.TransformSurvivalCurves(start, stop,
            survivalCurves[i], refinanceCurves[i], refinanceCorrelations[i],
            cCurve, rCurve, StepSize, StepUnit);
        }
        cCurves_[i] = cCurve;
        rCurves_[i] = rCurve;
      }

      return;
    }

    /// <summary>
    ///   Convert maturities to double array
    /// </summary>
    /// <remarks>It seems ArrayOfDtMarshaler does not work. To pass Dt[] to C++, we
    /// first convert it to double[].</remarks>
    /// <param name="maturities">maturities</param>
    /// <returns></returns>
    private static double[] TransformMaturities(Dt[] maturities)
    {
      if (maturities==null)
        return new double[0];

      double[] result = new double[maturities.Length];
      for (int i = 0; i < maturities.Length; ++i)
        if (!maturities[i].IsEmpty())
          result[i] = maturities[i].ToDouble();
      return result;
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
      if (WithCorrelatedRecovery)
        return 1.0;
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
          || (refinanceCurves!=null && refinanceCurves[i] != null)
          || (maturities!=null && !maturities[i].IsEmpty())
          ? 1 : rates[i]);
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
      logger.Debug("Computing distribution for semi-analytic basket");

      double[] recoveryRates = RecoveryRates;
      double[] recoveryDispersions = RecoveryDispersions;

      CorrelationTermStruct corr = this.CorrelationTermStruct;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

      // Initialize distributions
      int startDateIndex = this.RecalcStartDateIndex;
      if (lossDistribution_ == null || startDateIndex < 0)
      {
        if (lossDistribution_==null)
          lossDistribution_ = new Curve2D();
        if (this.NoAmortization)
          amorDistribution_ = null;
        else if (amorDistribution_ == null)
          amorDistribution_ = new Curve2D();
        InitializeDistributions(start, TimeGrid, CookedLossLevels, 1,
          lossDistribution_, amorDistribution_);
        startDateIndex = 0;
      }

      // If all names defaulted, then nothing to do.
      if (SurvivalCurves.Length == 0)
        return;

      // Initialize refinance curves
      TransformRefinanceCurves(start, Maturity);

      // Calculate distributions
      if (MulticoreSupport)
      {
        SemiAnalyticBasketModel2.ComputeDistributions(
          false, startDateIndex, lossDistribution_.NumDates(),
          CopulaType, DfCommon, DfIdiosyncratic, this.Copula.Data,
          corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          IntegrationPointsFirst,
          TransformMaturities(this.EarlyMaturities),
          cCurves_, Principals, recoveryRates, recoveryDispersions,
          rCurves_, GetModelChoice(), GridSize,
          lossDistribution_,
          amorDistribution_ == null ? emptyDistribution : amorDistribution_);
      }
      else
      {
        SemiAnalyticBasketModel.ComputeDistributions(
            false, startDateIndex, lossDistribution_.NumDates(),
            CopulaType, DfCommon, DfIdiosyncratic, this.Copula.Data,
            corr.Correlations, corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
            IntegrationPointsFirst, IntegrationPointsSecond,
            TransformMaturities(this.EarlyMaturities),
            cCurves_, Principals, recoveryRates, recoveryDispersions,
            rCurves_, GetModelChoice(), GridSize, lossDistribution_,
            amorDistribution_ == null ? emptyDistribution : amorDistribution_);
      }
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
      int[] mapping = new int[lossLevels.Length];
      double[] preLosses = new double[lossLevels.Length];
      List<double> levels = new List<double>();
      levels.Add(0.0);
      for (int i = 0; i < lossLevels.Length; ++i)
      {
        double ap = 0, dp = lossLevels[i], loss = 0;
        AdjustTrancheLevels(false, ref ap, ref dp, ref loss);
        preLosses[i] = loss;
        if (dp > 0.0)
          levels.Add(dp);
        mapping[i] = levels.Count - 1;
      }
      lossLevels = levels.ToArray();
      
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

      // Initialize refinance curves
      TransformRefinanceCurves(start, Maturity);

      // Calculate distributions
      Copula copula = this.Copula;
      if (MulticoreSupport)
      {
        SemiAnalyticBasketModel2.ComputeDistributions(
          wantProbability, 0, lossDistribution.NumDates(),
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst,
          TransformMaturities(this.EarlyMaturities),
          cCurves_, Principals, recoveryRates, recoveryDispersions,
          rCurves_, GetModelChoice(), GridSize, lossDistribution,
          emptyDistribution);
      }
      else
      {
        SemiAnalyticBasketModel.ComputeDistributions(
          wantProbability, 0, lossDistribution.NumDates(),
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst, this.IntegrationPointsSecond,
          TransformMaturities(this.EarlyMaturities),
          cCurves_, Principals, recoveryRates, recoveryDispersions,
          rCurves_, GetModelChoice(), GridSize, lossDistribution,
          emptyDistribution);
      }
      double totalPrincipal = TotalPrincipal;
      double initialBalance = InitialBalance;      
      double[,] results = new double[preLosses.Length, 2];
      for (int k = 0; k < preLosses.Length; ++k)
      {
        double prevLoss = preLosses[k];
        int i = mapping[k];
        double level = lossDistribution.GetLevel(i);
        results[k, 0] = level * initialBalance + prevLoss;
        level = lossDistribution.Interpolate(date, level);
        results[k, 1] = wantProbability ? level : (level / totalPrincipal + prevLoss);
      }
      timer.stop();
      logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());

      return results;
    }

#if USE_OWN_BumpedPvs

    /// <summary>
    ///   For internal use only
    ///   <preliminary/>
    /// </summary>
    ///
    /// <remarks>
    ///   Compute the whole set of distributions for sensitivity analysis,
    ///   save the result for later use
    /// </remarks>
    /// 
    /// <param name="bumpedSurvivalCurves">A set of bumped survival curves</param>
    /// <param name="bumpedRecoveryRates">An array of bumped recovery rates</param>
    ///
    internal void ComputeAndSaveSensitivities(
      SurvivalCurve[] bumpedSurvivalCurves,
      double[] bumpedRecoveryRates)
    {
      if (!wantSensitivity_) return;

      Curve[] bcurves = new Curve[bumpedSurvivalCurves.Length];
      for (int i = 0; i < bcurves.Length; i++)
        bcurves[i] = bumpedSurvivalCurves[i];

      double[] recoveryRates = RecoveryRates;
      if (bumpedRecoveryRates == null)
        bumpedRecoveryRates = new double[0];
      double[] recoveryDispersions = RecoveryDispersions;

      CorrelationTermStruct corr = this.CorrelationTermStruct;

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

      // Initialize distributions
      lossDistribution_ = new Curve2D();
      amorDistribution_ = this.NoAmortization ? null : new Curve2D();
      InitializeDistributions(start,
        TimeGrid, CookedLossLevels, (1 + bcurves.Length), 
        lossDistribution_, amorDistribution_);

      // Initialize refinance curves
      //TransformRefinanceCurves(start, Maturity);

      // Calculate distributions
      Copula copula = this.Copula;
      if (MulticoreSupport)
      {
        SemiAnalyticBasketModel2.ComputeDistributions(
          false, 0, lossDistribution_.NumDates(),
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst,
          TransformMaturities(this.EarlyMaturities),
          SurvivalCurves, bcurves,
          this.Principals,
          recoveryRates, bumpedRecoveryRates, recoveryDispersions,
          GetModelChoice(), this.GridSize, lossDistribution_,
          amorDistribution_ == null ? emptyDistribution : amorDistribution_);
      }
      else
      {
        SemiAnalyticBasketModel.ComputeDistributions(
          false, 0, lossDistribution_.NumDates(),
          copula.CopulaType, copula.DfCommon, copula.DfIdiosyncratic, copula.Data,
          corr.Correlations,
          corr.GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
          this.IntegrationPointsFirst, this.IntegrationPointsSecond,
          TransformMaturities(this.EarlyMaturities),
          SurvivalCurves, bcurves,
          this.Principals,
          recoveryRates, bumpedRecoveryRates, recoveryDispersions,
          GetModelChoice(), this.GridSize, lossDistribution_,
          amorDistribution_ == null ? emptyDistribution : amorDistribution_);
      }
      distributionComputed_ = true;
    }

#endif

    /// <summary>
    ///Compute semi-analytic sensitivities of loss at TimeGrid
    ///<preliminary/>
    /// </summary>
    ///<param name="rawDetachments">Raw attachments and detachments of the loss levels for which to compute tranche loss(amortization) derivatives</param>
    /// <remarks>
    /// Compute the semi-analytic derivative of expected tranche loss at TimeGrid (tranche amortization) w.r.t individual name curve ordinates 
    /// and stores them for later use.
    /// </remarks>
    internal override void ComputeAndSaveSemiAnalyticSensitivities(UniqueSequence<double> rawDetachments)
    {
        CorrelationTermStruct[] corrs = new CorrelationTermStruct[rawDetachments.Count];
        for (int i = 0; i < corrs.Length; i++)
            corrs[i] = CorrelationTermStruct;
        Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
        Curve[] cCurves;
        Curve[] rCurves;
        TransformRefinanceCurves(start, Maturity, SurvivalCurves, RefinanceCurves, out cCurves, out rCurves,
                                 RefinanceCorrelations, StepSize, StepUnit);
        ComputeAndSaveSemiAnalyticSensitivities(cCurves, rCurves, GetModelChoice(), rawDetachments, corrs);
        RecomputeDerivatives = false;
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
      if (trancheBegin > trancheEnd)
        throw new ArgumentException(String.Format("Attachment cannot be greater than Detachment: {0} > {1}", trancheBegin, trancheEnd));

      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);

      double maxLoss = lossDistribution_.GetLevel(lossDistribution_.LevelCount - 1);
      if (trancheEnd > maxLoss)
        throw new ArgumentException(String.Format("Loss Distribution isn't available past level: {0}",maxLoss));
      loss += lossDistribution_.Interpolate(groupIdx, date, trancheBegin , trancheEnd ) / TotalPrincipal;

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
      if (amorDistribution_ != null)
        amortized += amorDistribution_.Interpolate(groupIdx, date, tBegin, tEnd) / TotalPrincipal;

      //logger.DebugFormat("Computed Amortization for {0}-{1} @{2} as {3}", trancheBegin, trancheEnd, date, amort );

      return amortized;
    }


    ///
    /// <summary>
    ///   Compute the derivatives of the accumulated loss on a  dollar on the index
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///<param name="retVal">RetVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the raw survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the raw survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param> 
    public override void AccumulatedLossDerivatives(Dt date, double trancheBegin, double trancheEnd, double[] retVal)
    {
        if (RawDetachments == null || !(RawDetachments.Contains(trancheBegin) && RawDetachments.Contains(trancheEnd)))
            ComputeAndSaveSemiAnalyticSensitivities(new UniqueSequence<double>(trancheBegin, trancheEnd));
        int dp = RawDetachments.IndexOf(trancheEnd);
        int ap = RawDetachments.IndexOf(trancheBegin);
        double[] tmp = new double[retVal.Length];
        TrancheLossDer[dp].Interpolate(date, retVal);
        TrancheLossDer[ap].Interpolate(date, tmp); 
        double multiplier = (TotalPrincipal - DefaultedPrincipal) / TotalPrincipal;
        for (int i = 0; i < retVal.Length; i++)
            retVal[i] = (retVal[i] - tmp[i]) * multiplier;
    }

    ///
    /// <summary>
    ///   Compute the derivatives of the amortized amount on a dollar on the index
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///<param name="retVal">RetVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the raw survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the raw survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param> 
    public override void AmortizedAmountDerivatives(Dt date, double trancheBegin, double trancheEnd, double[] retVal)
    {
        if(this.NoAmortization)
        {
          for (int i = 0; i < retVal.Length; i++)
            retVal[i] = 0.0;
          return;
        }
        if (RawDetachments == null || !(RawDetachments.Contains(trancheBegin) && RawDetachments.Contains(trancheEnd)))
            ComputeAndSaveSemiAnalyticSensitivities(new UniqueSequence<double>(trancheBegin, trancheEnd));
        int dp = RawDetachments.IndexOf(trancheEnd);
        int ap = RawDetachments.IndexOf(trancheBegin);
        double[] tmp = new double[retVal.Length];
        TrancheAmortDer[dp].Interpolate(date, retVal);
        TrancheAmortDer[ap].Interpolate(date, tmp);
        double multiplier = (TotalPrincipal - DefaultedPrincipal) / TotalPrincipal;
        for (int i = 0; i < retVal.Length; i++)
            retVal[i] = (-retVal[i] + tmp[i]) * multiplier;

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
      cCurves_ = rCurves_ = null;
      lossDistribution_ = null;
      amorDistribution_ = null;
      ResetDerivatives();
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
      // We do not special routine for basket of LCDS yet
      if (this.RefinanceCurves != null)// || WithCorrelatedRecovery)
        return base.BumpedPvs(pricers, altSurvivalCurves);

      // Sanity check
      int basketSize = Count;
      if (altSurvivalCurves.Length != basketSize)
        throw new System.ArgumentException(String.Format(
          "Invalid number of survival curves. Must be {0}, not {1}", 
          basketSize, altSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new System.ArgumentException(String.Format(
            "Pricer #{0} is not using this basket pricer!", j));
      // compute the whole distributions
      wantSensitivity_ = true;
      ComputeAndSaveSensitivities(altSurvivalCurves, null);

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
      // We do not special routine for basket of LCDS yet
      if (this.RefinanceCurves != null || NeedExactJtD(pricers)
        || (includeRecoverySensitivity && Use8dot7RecoverySensitivityRoutine))
      {
        return base.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);
      }

      // Sanity check
      int basketSize = Count, bumpCount = altSurvivalCurves.Length;
      if (bumpCount != basketSize && bumpCount != this.GetSurvivalBumpCount(includeRecoverySensitivity))
        throw new System.ArgumentException(String.Format(
          "Invalid number of survival curves. Must be {0}, not {1}",
          basketSize, altSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new System.ArgumentException(String.Format(
            "Pricer #{0} is not using this basket pricer!", j));

      // Create table with correct size
      double[,] table = new double[bumpCount + 1, pricers.Length];

      CalculateRegularPvTable(table, pricers, includeRecoverySensitivity,
        bumpCount > basketSize
          ? altSurvivalCurves.Take(basketSize).ToArray()
          : altSurvivalCurves);

      // Compute the sensitivity from the unsettled defaults
      if (bumpCount > basketSize)
      {
        CalculateDefaultPvTable(table, pricers, altSurvivalCurves);
      }

      // done
      return table;
    }

    private void CalculateRegularPvTable(
      double[,] table,
      PricerEvaluator[] pricers,
      bool includeRecoverySensitivity,
      SurvivalCurve[] altSurvivalCurves)
    {
      // compute the whole distributions
      wantSensitivity_ = true;
      ComputeAndSaveSensitivities(altSurvivalCurves,
        HasFixedRecovery || !includeRecoverySensitivity ? null
          : GetRecoveryRates(altSurvivalCurves));

      // now create and fill the table of values
      int basketSize = Count;
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
    }
#endif

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
      if (index<0)
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
        || Dt.Cmp(lossDistribution_.GetDate(index-1),timeGrid[index-1]) != 0)
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

    /// <summary>
    ///  Get an integer representing QCR/LCDO model choice
    ///  to be passed to C++ model.
    /// </summary>
    /// <returns>An integer representing the model choice</returns>
    internal SemiAnalyticBasketModel.RecoveryCorrelationModel GetModelChoice()
    {
      const int extendedBit = 0x4000;
      const int propLcdoBit = 0x8000;
      int m = (int)QCRModel;
      if (this.HasFixedRecovery && !settings_.SemiAnalyticBasketPricer.StochasticFixedRecovery)
        m = (int) RecoveryCorrelationType.None;
      if (ModelChoice.ExtendedCorreltion || Correlation.MaxCorrelation > 1)
        m |= extendedBit;
      if (ModelType == BasketModelType.LCDOCommonSignal)
        goto done; // Don't set flag if the new model is requested explicitly.
      if (ModelType == BasketModelType.LCDOProportional || settings_.SemiAnalyticBasketPricer.UseOldLcdxTrancheModel)
        m |= propLcdoBit; // The old model is requested explicitly, or through configuration.
      done:
      return new SemiAnalyticBasketModel.RecoveryCorrelationModel
      {
        ModelChoice = m,
        MaxRecovery = RecoveryUpperBound,
        MinRecovery = RecoveryLowerBound,
      };
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
    // Transformed curves
    private SurvivalCurve[] cCurves_;
    private SurvivalCurve[] rCurves_;

    // Calculated distribution data
    private Curve2D lossDistribution_;
    private Curve2D amorDistribution_;
    private bool distributionComputed_;

   //-----------------------------------------------------------------------
  

#if USE_OWN_BumpedPvs
    private bool wantSensitivity_;
    private int bumpedCurveIndex_;
#endif

    private static readonly Curve2D emptyDistribution = new Curve2D();
    #endregion Data

    #region IAnalyticDerivativesProvider Members

   /// <summary>
   /// True if this pricer supports semi-analytic sensitivies 
   /// </summary>
     bool IAnalyticDerivativesProvider.HasAnalyticDerivatives
    {
        get { return true; }
    }

     
    /// <summary>
    /// Computes PV derivatives wrt the ordinates of each underlying curve 
    /// </summary>
    /// <returns>IDerivativeCollection object</returns>
    /// <remarks>Computation is delegated to CDO pricers</remarks>
    IDerivativeCollection IAnalyticDerivativesProvider.GetDerivativesWrtOrdinates()
    {
        throw new NotImplementedException("Method not implemented");
    }
    #endregion
  } // class SemiAnalyticBasketPricer

}
