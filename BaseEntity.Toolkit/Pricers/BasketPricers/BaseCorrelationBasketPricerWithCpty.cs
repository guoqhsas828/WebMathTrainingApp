using System;
using System.Collections.Generic;
using BaseEntity.Shared;
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
  ///  Compute loss/amortization on the event of survival of the counterparty <m>E(L_T I_{\tau > T})</m>
  /// </summary>
  ///
  /// <remarks>
  ///   <para>This helper class sets up a basket and pre-calculates loss/amort on the event of survival of the counterparty.</para>
  ///
  /// </remarks>
  ///
  [Serializable]
  public class BaseCorrelationBasketPricerWithCpty : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelationBasketPricerWithCpty));

    #region Constructors

    private BaseCorrelationBasketPricerWithCpty(
      Dt asOf,
      Dt settle,
      Dt maturity,
      DiscountCurve discountCurve,
      SurvivalCurve cptyCurve,
      double cptyCorrelation,
      CreditPool basket,
      int stepSize,
      TimeUnit stepUnit,
      CorrelationObject correlationObject,
      double attach,
      double detach
      )
      : base(asOf, settle, maturity, basket, new Copula(), correlationObject, stepSize, stepUnit, new[] {attach, detach})
    {
      logger.DebugFormat("Creating semi-analytic Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);
      DiscountCurve = discountCurve;
      CounterpartyCurve = cptyCurve;
      CounterpartyCorrelation = cptyCorrelation;
      cCurves_ = rCurves_ = null;
      lossDistribution_ = null;
      amorDistribution_ = null;
      distributionComputed_ = false;
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
    /// <param name="cptyCurve">Survival curve of the counterparty</param>
    /// <param name="cptyCorrelation">Correlation of counterparty (in Gaussian copula sense)</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="correlationObject">Base correlation object</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="attach">Attachment</param>
    /// <param name="detach">Detachment</param>
    /// <param name="checkRefinance">If true, check refinance infomation from survival curves</param>
    ///
    public BaseCorrelationBasketPricerWithCpty(
      Dt asOf,
      Dt settle,
      Dt maturity,
      DiscountCurve discountCurve,
      SurvivalCurve cptyCurve,
      double cptyCorrelation,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      CorrelationObject correlationObject,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      double attach,
      double detach,
      bool checkRefinance
      )
      : this(asOf, settle, maturity, discountCurve, cptyCurve, cptyCorrelation, new CreditPool(principals, survivalCurves,
        recoveryCurves, null, null, checkRefinance, null), stepSize, stepUnit, correlationObject, attach, detach)
    { }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (BaseCorrelationBasketPricerWithCpty)base.Clone();
      obj.lossDistribution_ = lossDistribution_ == null ? null : Array.ConvertAll(lossDistribution_, c => c.clone());
      obj.amorDistribution_ = amorDistribution_ == null ? null : Array.ConvertAll(amorDistribution_, c => c.clone());
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
    /// <param name="levels">loss levels</param>
    /// <param name="lossDistributions">distribution of losses</param>
    /// <param name="amorDistributions">distribution of amortizations</param>
    /// <param name="noAmortization">Do not amortize</param>
    private static void InitializeDistributions(
      Dt start,
      Dt stop,
      int stepSize,
      TimeUnit stepUnit,
      IList<double> levels,
      ref Curve[] lossDistributions,
      ref Curve[] amorDistributions,
      bool noAmortization)
    {
      IList<Dt> dates = GenerateGridDates(start, stop, stepSize, stepUnit, null);
      InitializeDistributions(start, dates, levels, ref lossDistributions, ref amorDistributions, noAmortization);
    }

    private static void InitializeDistributions(Dt start, IEnumerable<Dt> dates, ICollection<double> levels, ref Curve[] lossDistributions, ref Curve[] amorDistributions, bool noAmortization)
    {
      lossDistributions = ArrayUtil.Generate(levels.Count, i =>
                                                             {
                                                               var retVal = new Curve(start);
                                                               foreach (var dt in dates) retVal.Add(dt, 0.0);
                                                               return retVal;
                                                             });
      amorDistributions = ArrayUtil.Generate(levels.Count, i =>
                                                             {
                                                               var retVal = new Curve(start);
                                                               if (noAmortization)
                                                                 return retVal;
                                                               foreach (var dt in dates)
                                                                 retVal.Add(dt, 0.0);
                                                               return retVal;
                                                             });
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
      SyntheticCDO cdo = CreateCdo();
      cdo.Attachment = 0.0;
      cdo.Detachment = detachment;
      basket.Correlation = sco;
      basket.RawLossLevels = new UniqueSequence<double>(0.0, detachment);
      CorrelationObject codp = baseCorrelation.GetCorrelations(cdo, names, null, basket, DiscountCurve, 0, 0);
      if (detachment > 0.99999999 && codp is ICorrelationSetFactor)
        ((ICorrelationSetFactor) codp).SetFactor(0.0);
      basket.Correlation = codp;
      return basket.CorrelationTermStruct;
    }

    /// <summary>
    ///   For internal use only
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    ///   Compute the whole distribution, save the result for later use
    /// </remarks>
    private void ComputeAndSaveDistribution(params double[] levels)
    {
      var start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      TransformRefinanceCurves(start, Maturity);
      InitializeDistributions(start, Maturity, StepSize, StepUnit, levels, ref lossDistribution_, ref amorDistribution_,
                              NoAmortization);
      int model = GetModelChoice();
      for (int i = 0; i < levels.Length; ++i)
      {
        double detach = levels[i];
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
        var ld = AdjustTrancheLevel(false, detach);
        var ad = AdjustTrancheLevel(true, 1.0 - detach);
        SemiAnalyticBasketModelGreeks.Compute(ld, ad, 0, LossDistribution[i].Count, corrData, corrDates,
                                              IntegrationPointsFirst, new double[0], cCurves_, Principals, RecoveryRates,
                                              RecoveryDispersions, rCurves_, CounterpartyCurve,
                                              Array.ConvertAll(corrDates, d => Math.Sqrt(CounterpartyCorrelation)),
                                              model, LossDistribution[i], AmorDistribution[i]);
      }
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
      if (trancheBegin > trancheEnd)
        throw new ArgumentException(String.Format("Attachment cannot be greater than Detachment: {0} > {1}",
                                                  trancheBegin, trancheEnd));
      if (!distributionComputed_)
      {
        if (trancheBegin > 0)
          ComputeAndSaveDistribution(trancheBegin, trancheEnd);
        else
          ComputeAndSaveDistribution(trancheEnd);
      }
      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);
      double remainingNotional = (TotalPrincipal - DefaultedPrincipal)/TotalPrincipal;
      loss += (trancheBegin > 0)
                ? (LossDistribution[1].Interpolate(date) - LossDistribution[0].Interpolate(date))*remainingNotional
                : LossDistribution[0].Interpolate(date)*remainingNotional;
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
      double remainingNotional = (TotalPrincipal - DefaultedPrincipal)/TotalPrincipal;
      amortized += (trancheBegin > 0)
                     ? (AmorDistribution[1].Interpolate(date) - AmorDistribution[0].Interpolate(date))*remainingNotional
                     : AmorDistribution[0].Interpolate(date)*remainingNotional;
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
        if (LossDistribution != null)
        {
          foreach (var curve in LossDistribution)
            curve.AsOf = start;
        }
        if (AmorDistribution != null)
        {
          foreach (var curve in AmorDistribution)
            curve.AsOf = start;
        }
        return;
      }
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
    ///   Computed distribution for basket
    /// </summary>
    public Curve[] LossDistribution
    {
      get { return lossDistribution_; }
      set { lossDistribution_ = value; }
    }

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve[] AmorDistribution
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

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get; private set;
    }

    
    #endregion // Properties

    #region Data
    // Transformed curves
    private SurvivalCurve[] cCurves_;
    private SurvivalCurve[] rCurves_;
    // Calculated distribution data
    
    private Curve[] lossDistribution_;
    private Curve[] amorDistribution_;
    private bool distributionComputed_;
    //-----------------------------------------------------------------------
   #endregion Data
  }// class SemiAnalyticBasketPricer
}
