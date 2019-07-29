using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  ///
  /// <summary>
  ///  Compute loss/amortization conditional on <m>F_\infty</m> 
  /// </summary>
  ///
  [Serializable]
  internal class BaseCorrelationBasketPricerDynamic : BasketPricer, IDynamicDistribution
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelationBasketPricerDynamic));

    #region Constructors
    private BaseCorrelationBasketPricerDynamic(
      Dt asOf,
      Dt settle,
      Dt maturity,
      DiscountCurve discountCurve,
      CreditPool basket,
      CorrelationObject correlationObject,
      int stepSize,
      TimeUnit stepUnit,
      double attach,
      double detach,
      bool wantProbability
      )
      : base(asOf, settle, maturity, basket, new Copula(), correlationObject, stepSize, stepUnit, new[] { attach, detach })
    {
      logger.DebugFormat("Creating semi-analytic Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);
      attach_ = attach;
      detach_ = detach;
      DiscountCurve = discountCurve;
      cCurves_ = rCurves_ = null;
      lossWorkspace_ = null;
      amortWorkspace_ = null;
      DistributionComputed = false;
      wantExhaustionProbability_ = wantProbability;
      logger.Debug("Basket created");
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="correlationObject">correlation</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    /// <param name="attach">Attachment point</param>
    /// <param name="detach">Detachment point</param>
    /// <param name="wantExaustionProb">True if want to compute exaustion probability</param>

    public BaseCorrelationBasketPricerDynamic(
      Dt asOf,
      Dt settle,
      Dt maturity,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      CorrelationObject correlationObject,
      int stepSize,
      TimeUnit stepUnit,
      bool checkRefinance,
      double attach,
      double detach,
      bool wantExaustionProb
      )
      : this(asOf, settle, maturity, discountCurve, new CreditPool(principals, survivalCurves,
        recoveryCurves, null, null, checkRefinance, null), correlationObject, stepSize,
        stepUnit, attach, detach, wantExaustionProb)
    {}

    private static object SafeClone<T>(T obj) where T : class, ICloneable
    {
      return (obj == null) ? null : obj.Clone();
    }

    private static object SafeCloneArray<T>(T[] obj) where T : class, ICloneable
    {
      return (obj == null) ? null : Array.ConvertAll(obj, o => (T) SafeClone(o));
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (BaseCorrelationBasketPricerDynamic) base.Clone();
      obj.DiscountCurve = (DiscountCurve) SafeClone(DiscountCurve);
      obj.conditionalLossDistribution_ = (double[][,]) SafeCloneArray(conditionalLossDistribution_);
      obj.conditionalAmorDistribution_ = (double[][,]) SafeCloneArray(conditionalAmorDistribution_);
      obj.conditionalExhaustionProb_ = (double[,]) SafeClone(conditionalExhaustionProb_);
      obj.quadPoints_ = (double[]) SafeClone(quadPoints_);
      obj.quadWeights_ = (double[]) SafeClone(quadWeights_);
      obj.wantExhaustionProbability_ = wantExhaustionProbability_;
      obj.lossWorkspace_ = (Curve[]) SafeCloneArray(lossWorkspace_);
      obj.amortWorkspace_ = (Curve[]) SafeCloneArray(amortWorkspace_);
      obj.probWorkspace_ = (Curve) SafeClone(probWorkspace_);
      return obj;
    }

    #endregion // Constructors

    #region Methods
    private static Dt[] GenerateGridDates(
     Dt start, Dt stop,
     int stepSize, TimeUnit stepUnit)
    {
      if (stepSize == 0 || stepUnit == TimeUnit.None)
      {
        stepSize = 3;
        stepUnit = TimeUnit.Months;
      }
      var timeGrid = new List<Dt>();
      for (Dt date = start;
           Dt.Cmp(date, stop) < 0;
           date = Dt.Add(date, stepSize, stepUnit))
      {
        timeGrid.Add(date);
      }
      timeGrid.Add(stop);
      return timeGrid.ToArray();
    }

    private void InitializeDistributions(Dt start, Dt[] dates, params double[] lossLevels)
    {
      var nDates = dates.Length;
      quadPoints_ = new double[IntegrationPointsFirst];
      quadWeights_ = new double[IntegrationPointsFirst];
      lossWorkspace_ = ArrayUtil.Generate(lossLevels.Length, i =>
                                                               {
                                                                 var retVal = new Curve(start);
                                                                 foreach (var dt in dates)
                                                                   retVal.Add(dt, 0.0);
                                                                 return retVal;
                                                               });
      conditionalLossDistribution_ = ArrayUtil.Generate(lossLevels.Length,
                                                        i => new double[nDates, IntegrationPointsFirst]);
      if (NoAmortization)
      {
        conditionalAmorDistribution_ = ArrayUtil.Generate(lossLevels.Length, i => new double[0,0]);
        amortWorkspace_ = ArrayUtil.Generate(lossLevels.Length, i => new Curve(start));
      }
      else
      {
        amortWorkspace_ = ArrayUtil.Generate(lossLevels.Length, i =>
                                                                  {
                                                                    var retVal = new Curve(start);
                                                                    foreach (var dt in dates)
                                                                      retVal.Add(dt, 0.0);
                                                                    return retVal;
                                                                  });
        conditionalAmorDistribution_ = ArrayUtil.Generate(lossLevels.Length,
                                                          i => new double[nDates,IntegrationPointsFirst]);
      }
      if (wantExhaustionProbability_)
      {
        probWorkspace_ = new Curve(start);
        for (int i = 0; i < nDates; ++i)
          probWorkspace_.Add(dates[i], 0.0);
        conditionalExhaustionProb_ = new double[nDates,IntegrationPointsFirst];
      }
      else
        conditionalExhaustionProb_ = new double[0,0];
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
      var survivalCurves = SurvivalCurves;
      var refinanceCorrelations = RefinanceCorrelations;
      var refinanceCurves = RefinanceCurves;
      if (refinanceCurves == null)
      {
        cCurves_ = survivalCurves;
        rCurves_ = new SurvivalCurve[cCurves_.Length];
        return;
      }
      int n = survivalCurves.Length;
      if (n != refinanceCurves.Length)
        throw new ArgumentException(String.Format(
                                      "Lengths of SurvivalCurves ({0}) and RefinanceCurves ({1}) not match",
                                      survivalCurves.Length, refinanceCurves.Length));
      if (n != refinanceCorrelations.Length)
        throw new ArgumentException(String.Format(
                                      "Lengths of RefinanceCurves ({0}) and RefinanceCorrelations ({1}) not match",
                                      n, refinanceCorrelations.Length));
      cCurves_ = new SurvivalCurve[n];
      rCurves_ = new SurvivalCurve[n];
      for (int i = 0; i < n; ++i)
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
          CounterpartyRisk.TransformSurvivalCurves(start, stop, survivalCurves[i], refinanceCurves[i],
                                                   refinanceCorrelations[i], cCurve, rCurve, StepSize, StepUnit);
        }
        cCurves_[i] = cCurve;
        rCurves_[i] = rCurve;
      }
      return;
    }

    private SyntheticCDO CreateCdo()
    {
      // Create cdo
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      var cdo = new SyntheticCDO(start, Maturity, Currency.None, 0.0, // premium, not used
                                 DayCount.Actual360, Frequency.Quarterly,
                                 BDConvention.Following, Calendar.NYB);
      return cdo;
    }

    /// <summary>
    /// Create a CorrelationTermStruct object from BaseCorrelation object and given detachment
    /// </summary>
    /// <param name="baseCorrelation">BaseCorrelation object</param>
    /// <param name="detachment">Detachment point</param>
    /// <returns>Correlation object</returns>
    private CorrelationTermStruct CalculateCorrelation(BaseCorrelationObject baseCorrelation, double detachment)
    {
      var basket = Duplicate();
      basket.Correlation = CloneUtil.Clone(basket.Correlation);
      basket.RecoveryCurves = CloneUtil.Clone(basket.RecoveryCurves);
      var names = baseCorrelation.EntityNames;
      var sco = new SingleFactorCorrelation(names, 0.0);
      var cdo = CreateCdo();
      cdo.Attachment = 0.0;
      cdo.Detachment = detachment;
      basket.Correlation = sco;
      basket.RawLossLevels = new UniqueSequence<double>(0.0, detachment);
      var codp = baseCorrelation.GetCorrelations(cdo, names, null, basket, DiscountCurve, 0, 0);
      if (detachment > 0.99999999 && codp is ICorrelationSetFactor)
        ((ICorrelationSetFactor) codp).SetFactor(0.0);
      basket.Correlation = codp;
      return basket.CorrelationTermStruct;
    }

    private void InitializeEquityTrancheLoss(int index, Dt[] lossDates, double detach, int model, bool wantProbability)
    {
      var ld = AdjustTrancheLevel(false, detach);
      var ad = AdjustTrancheLevel(true, 1.0 - detach);
      double[] corrData;
      int[] corrDates;
      if (detach > 0.999 || Correlation == null)
      {
        corrData = new[] {0.0};
        corrDates = new[] {0};
      }
      else if (Correlation is BaseCorrelationObject)
      {
        var co = CalculateCorrelation(Correlation as BaseCorrelationObject, detach);
        corrData = co.Correlations;
        corrDates = co.GetDatesAsInt(AsOf);
      }
      else if (Correlation is Correlation)
      {
        corrData = CorrelationTermStruct.Correlations;
        corrDates = CorrelationTermStruct.GetDatesAsInt(AsOf);
      }
      else
        throw new Exception("Correlation type not supported");
      LossProcess.InitializeTrancheLoss(AsOf, lossDates, corrDates, corrData, ld, ad, model, cCurves_,
                                        Principals, RecoveryRates, RecoveryDispersions, rCurves_, quadPoints_,
                                        quadWeights_, conditionalLossDistribution_[index],
                                        conditionalAmorDistribution_[index],
                                        wantProbability ? conditionalExhaustionProb_ : new double[0,0]);


    }


    /// <summary>
    ///   For internal use only
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    /// Compute the conditional distribution and add the loss process to the simulator
    /// </remarks>
    private void ComputeAndSaveDistribution(params double[] lossLevels)
    {
      var start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      TransformRefinanceCurves(start, Maturity);
      var lossDates = GenerateGridDates(start, Maturity, StepSize, StepUnit);
      InitializeDistributions(start, lossDates, lossLevels);
      var model = GetModelChoice();
      for (int i = 0; i < lossLevels.Length; ++i)
        InitializeEquityTrancheLoss(i, lossDates, lossLevels[i], model,
                                    (i == lossLevels.Length - 1) && wantExhaustionProbability_);
      DistributionComputed = true;
    }

    /// <summary>
    ///   Compute the cumulative loss distribution
    /// </summary>
    ///
    /// <remarks>
    ///   The returned array may have three columns, the first of which contains the
    ///   loss levels, the second and the third columns contain the cumulative
    ///   probabilities or expected base losses corresponding to attachment or
    ///   detachment correlations.
    /// </remarks>
    ///
    /// <param name="wantProbability">If true, return probabilities; else, return expected base losses</param>
    /// <param name="date">The date at which to calculate the distribution</param>
    /// <param name="lossLevels">Array of lossLevels (should be between 0 and 1)</param>
    public override double[,] CalcLossDistribution(bool wantProbability, Dt date, double[] lossLevels)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Evolve distribuition 
    /// </summary>
    /// <param name="i">Quadrature point index</param>
    void IDynamicDistribution.ConditionOn(int i)
    {
      if (!DistributionComputed)
      {
        if (attach_ > 0.0)
          ComputeAndSaveDistribution(attach_, detach_);
        else
          ComputeAndSaveDistribution(detach_);
      }
      int count = (attach_ > 0.0) ? 2 : 1;
      for (int j = 0; j < count; ++j)
      {
        for (int t = 0; t < lossWorkspace_[j].Count; ++t)
          lossWorkspace_[j].SetVal(t, conditionalLossDistribution_[j][t, i]);
        for (int t = 0; t < amortWorkspace_[j].Count; ++t)
          amortWorkspace_[j].SetVal(t, conditionalAmorDistribution_[j][t, i]);
      }
      for (int t = 0; t < probWorkspace_.Count; ++t)
        probWorkspace_.SetVal(t, conditionalExhaustionProb_[t, i]);
    }

    /// <summary>
    /// <m>P(L_T > K)</m> where K is detach 
    /// </summary>
    /// <param name="date">Date</param>
    double IDynamicDistribution.ExhaustionProbability(Dt date)
    {
      if (probWorkspace_ == null)
        return 0.0;
      double exProb = probWorkspace_.Interpolate(date);
      return Math.Max(Math.Min(exProb, 1.0), 0.0);
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
      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);
      double remainingNotional = (TotalPrincipal - DefaultedPrincipal)/
                                 TotalPrincipal;
      loss += (trancheBegin > 0)
                ? (lossWorkspace_[1].Interpolate(date) - lossWorkspace_[0].Interpolate(date))*remainingNotional
                : lossWorkspace_[0].Interpolate(date)*remainingNotional;
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
      if (NoAmortization)
        return 0.0;
      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;
      AdjustTrancheLevels(true, ref tBegin, ref tEnd, ref amortized);
      double remainingNotional = (TotalPrincipal - DefaultedPrincipal)/
                                 TotalPrincipal;
      amortized += (trancheBegin > 0)
                     ? (amortWorkspace_[1].Interpolate(date) - amortWorkspace_[0].Interpolate(date))*remainingNotional
                     : amortWorkspace_[0].Interpolate(date)*remainingNotional;
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
      DistributionComputed = false;
      cCurves_ = rCurves_ = null;
      conditionalLossDistribution_ = null;
      conditionalAmorDistribution_ = null;
      conditionalExhaustionProb_ = null;
    }

    /// <summary>
    ///  Get an integer representing QCR/LCDO model choice
    ///  to be passed to C++ model.
    /// </summary>
    /// <returns>An integer representing the model choice</returns>
    internal int GetModelChoice()
    {
      const int extendedBit = 0x4000;
      const int propLcdoBit = 0x8000;
      int m = (int)QCRModel;
      if (HasFixedRecovery && !settings_.SemiAnalyticBasketPricer.StochasticFixedRecovery)
        m = (int)RecoveryCorrelationType.None;
      if (ModelChoice.ExtendedCorreltion || Correlation.MaxCorrelation > 1)
        m |= extendedBit;
      if (ModelType == BasketModelType.LCDOCommonSignal)
        return m; // Don't set flag if the new model is requested explicitly.
      if (ModelType == BasketModelType.LCDOProportional || settings_.SemiAnalyticBasketPricer.UseOldLcdxTrancheModel)
        m |= propLcdoBit; // The old model is requested explicitly, or through configuration.
      return m;
    }

    #endregion Methods

    #region Properties
    /// <summary>
    /// Quadrature weights
    /// </summary>
    double[] IDynamicDistribution.QuadratureWeights { get { return quadWeights_; } }
    /// <summary>
    /// Quadrature points
    /// </summary>
    double[] IDynamicDistribution.QuadraturePoints { get { return quadPoints_; } }
    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }
    /// <summary>
    ///   Distribution computed
    /// </summary>
    public bool DistributionComputed
    {
      get;
      private set;
    }
    #endregion // Properties

    #region Data
    // Transformed curves
    private SurvivalCurve[] cCurves_;
    private SurvivalCurve[] rCurves_;
    // Calculated distribution data
    private Curve[] lossWorkspace_;
    private Curve[] amortWorkspace_;
    private Curve probWorkspace_;
    private bool wantExhaustionProbability_;
    private readonly double attach_;
    private readonly double detach_;
    private double[] quadPoints_;
    private double[] quadWeights_;
    private double[][,] conditionalLossDistribution_;
    private double[][,] conditionalAmorDistribution_;
    private double[,] conditionalExhaustionProb_;
    //-----------------------------------------------------------------------
    #endregion Data
  }// class BaseCorrelationBasketPricerDynamic
}
