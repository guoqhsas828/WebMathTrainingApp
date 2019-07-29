/*
 * BaseCorrelationBasketPricer.cs
 *
 */
//#define Old_Approach
#define SUPPORT_PRECOMPUTED_CORRELATION
//#define Handle_Defaulted_By_8_7

using System;
using System.Collections;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util.Configuration;
using CreditPortfolio = BaseEntity.Toolkit.Pricers.Baskets.CreditPool;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{

  /// <summary>
  ///   Basket pricer based on heterogeneous basket model and base correlations.
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public class BaseCorrelationBasketPricer : BasketPricer, IAnalyticDerivativesProvider
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelationBasketPricer));

    #region Constructors

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
    /// <param name="correlation">Base correlations</param>
    /// <param name="attachment">Attachment point</param>
    /// <param name="detachment">Detachment point</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    ///
    public BaseCorrelationBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      BaseCorrelationObject correlation,
      double attachment,
      double detachment,
      int stepSize,
      TimeUnit stepUnit)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
        new Copula(), new SingleFactorCorrelation(new string[survivalCurves.Length], 0.0),
        stepSize, stepUnit, new double[] { attachment, detachment })
    {
      logger.DebugFormat("Creating Base correlation Basket asof={0}, settle={1}, maturity={2}", asOf, settle, maturity);
      this.Correlation = correlation;
      if (correlation.EntityNames == null)
        ((BaseCorrelationObject)this.Correlation).EntityNames = Utils.GetCreditNames(this.SurvivalCurves);

      this.apBasketPricer_ = this.dpBasketPricer_ =
        new BootstrapHeterogeneousBasketPricer(
              asOf, settle, maturity, survivalCurves, recoveryCurves,
              principals, new Copula(CopulaType.Gauss, 0, 0),
              this.CorrelationTermStruct, stepSize, stepUnit,
              new double[] { attachment, detachment });
      this.distributionComputed_ = false;

      this.RescaleStrike = false;
      this.discountCurve_ = discountCurve;
      this.attachment_ = attachment;
      this.detachment_ = detachment;
      this.correlationReady_ = false;
      this.useSurvivalRecovery_ = UseCurveRecoveryForBaseCorrelation;
      this.ConsistentCreditSensitivity = ToolkitConfigurator.Settings.BasketPricer.ConsistentSensitivity;

      logger.Debug("Basket created");
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="basket">Underlying basket pricer</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="correlation">Base correlations</param>
    /// <param name="rescaleStrikes">Scale strikes each time we price, otherwise fixed initially</param>
    /// <param name="attachment">Attachment point</param>
    /// <param name="detachment">Detachment point</param>
    ///
    public BaseCorrelationBasketPricer(
      BasketPricer basket,
      DiscountCurve discountCurve,
      BaseCorrelationObject correlation,
      bool rescaleStrikes,
      double attachment,
      double detachment)
    {
      logger.DebugFormat("Creating Base correlation Basket asof={0}, settle={1}, maturity={2}", basket.AsOf, basket.Settle, basket.Maturity);

      basket.CopyTo(this);

#if SUPPORT_PRECOMPUTED_CORRELATION
      // Set the right correlation
      this.precomputedCorrelation_ = (correlation == null);
      if (precomputedCorrelation_)
        correlation = new BaseCorrelation(
          BaseCorrelationMethod.ArbitrageFree,
          BaseCorrelationStrikeMethod.Unscaled, null,
          new double[] { attachment, detachment },
          new double[2] { Double.NaN, Double.NaN }); // This dummy correlation should never be used!!!
#endif // SUPPORT_PRECOMPUTED_CORRELATION

      // Set the right correlation and basket calculator
      Set(basket, correlation);

      // need the clone to make sure the basket is unique
      this.distributionComputed_ = false;

      this.RescaleStrike = rescaleStrikes;
      this.discountCurve_ = discountCurve;
      this.attachment_ = attachment;
      this.detachment_ = detachment;
      this.useSurvivalRecovery_ = UseCurveRecoveryForBaseCorrelation;
      this.ConsistentCreditSensitivity = ToolkitConfigurator.Settings.BasketPricer.ConsistentSensitivity;

      logger.Debug("Basket created");
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      BaseCorrelationBasketPricer obj = (BaseCorrelationBasketPricer)base.Clone();

      obj.dpBasketPricer_ = (BasketPricer)dpBasketPricer_.Clone();
      obj.apBasketPricer_ =
          (dpBasketPricer_ == apBasketPricer_ ? obj.dpBasketPricer_
           : (BasketPricer)apBasketPricer_.Clone());

      obj.discountCurve_ = (discountCurve_ != null) ? (DiscountCurve)discountCurve_.Clone() : null;

      return obj;
    }

    /// <summary>
    ///   Create a duplicated pricer
    ///   <preliminary />
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Duplicate() differs from Clone() in that it copies by references all the 
    ///   basic data and numerical options defined in the BasketPricer class.  Both
    ///   the original basket and the duplicated basket share the same underlying curves,
    ///   pricinpals and correlation object.  Unlike the MemberwiseClone() function, however,
    ///   it does not copy by reference any intermediate computational data such as
    ///   loss distributions and computed detachment/attachment correlations.  These data
    ///   are held separately in the duplicated and original baskets.
    ///   </para>
    /// 
    ///   <para>In this way, it provides an easy way to construct objects performing
    ///   independent calculations on the same set of input data.</para>
    /// </remarks>
    /// 
    /// <returns>A duplicated pricer</returns>
    public override BasketPricer Duplicate()
    {
      BaseCorrelationBasketPricer bp =
        (BaseCorrelationBasketPricer)base.Duplicate();
      bp.apBasketPricer_ = bp.dpBasketPricer_
        = dpBasketPricer_.Duplicate();
      if (apBasketPricer_ != dpBasketPricer_)
        bp.apBasketPricer_ = apBasketPricer_.Duplicate();
      return bp;
    }

    /// <summary>
    ///   Create a copy of basket with different underlying curves and participations
    ///   <preliminary />
    /// </summary>
    /// 
    /// <param name="basket">Basket to substitute with.</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    public override BasketPricer Substitute(
      CreditPool basket,
      CorrelationObject correlation,
      Array lossLevels)
    {
      if (!(correlation is BaseCorrelationObject))
        throw new System.ArgumentException("Correlation must be a base correlation object");

      BaseCorrelationBasketPricer obj = (BaseCorrelationBasketPricer)
        base.Substitute(basket,
          new SingleFactorCorrelation(new string[basket.CreditCount], 0.0),
          lossLevels);
      obj.Set(this.GetBasketCalculator().Duplicate(), (BaseCorrelationObject)correlation);

      return obj;
    }


    /// <summary>
    ///    Set basket calculator and base correlation
    ///   <preliminary />
    /// </summary>
    /// <param name="basket">Basket calculator</param>
    /// <param name="correlation">Base correlatiob</param>
    internal void Set(BasketPricer basket, BaseCorrelationObject correlation)
    {
      // Check basket
      if (basket == null)
      {
        basket = GetBasketCalculator();
        if (basket == null)
          throw new System.ArgumentException("basket cannot be null");
      }

      // Update correlation if needed.
      if (correlation == null)
      {
        correlation = (BaseCorrelationObject)this.OriginalCorrelation;
        if (correlation == null)
          throw new ArgumentException("correlation cannot be null.");
      }
      else
      {
        // Save the original correlation
        this.OriginalCorrelation = correlation;
      }
      // Create a copy of the original correlation with the selected names
      correlation = correlation.Create(Utils.GetCreditNames(this.SurvivalCurves));
      this.Correlation = correlation;

      if (correlation is BaseCorrelationMixWeighted)
      {
        // Create a simple correlation mixed object
        CorrelationMixed correlationMixed = new CorrelationMixed(
          new CorrelationObject[] { new SingleFactorCorrelation(correlation.EntityNames, 0.0) },
          new double[] { 1.0 });

        // Setup Pv averaging pricer
        PvAveragingBasketPricer bp = new PvAveragingBasketPricer(basket, correlationMixed);
        this.apBasketPricer_ = this.dpBasketPricer_ = bp;
      }
      else if ((correlation is BaseCorrelationTermStruct) &&
        (((BaseCorrelationTermStruct)correlation).CalibrationMethod == BaseCorrelationCalibrationMethod.TermStructure) &&
        (basket is HeterogeneousBasketPricer))
      {
        // setup bootstraping pricer
        BootstrapHeterogeneousBasketPricer bp = new BootstrapHeterogeneousBasketPricer();
        basket.CopyTo(bp);
        bp.AmorDistribution = ((HeterogeneousBasketPricer)basket).AmorDistribution.clone();
        bp.LossDistribution = ((HeterogeneousBasketPricer)basket).LossDistribution.clone();
        bp.DistributionComputed = false;
        this.apBasketPricer_ = this.dpBasketPricer_ = bp;
      }
      else
        this.apBasketPricer_ = this.dpBasketPricer_ = (BasketPricer)basket.Duplicate();

      distributionComputed_ = false;
      correlationReady_ = false;
      return;
    }

    /// <summary>
    ///  Reset the original basket.
    /// </summary>
    /// <param name="originalBasket">original Basket.</param>
    /// <exclude/>
    protected internal override void Reset(CreditPool originalBasket)
    {
      base.Reset(originalBasket);
      Set(dpBasketPricer_, null);
    }

    /// <summary>
    ///   Get underlying basket calculator
    ///   <preliminary/>
    /// </summary>
    /// <remarks>For round trip test only.</remarks>
    /// <returns>Basket calculator</returns>
    /// <exclude />
    public BasketPricer GetBasketCalculator()
    {
      if (dpBasketPricer_ is PvAveragingBasketPricer)
        return ((PvAveragingBasketPricer)dpBasketPricer_).BasketCalculator;
      return dpBasketPricer_;
    }
    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {

      base.Validate(errors);

      if ((BaseCorrelationObject)this.Correlation == null)
        InvalidValue.AddError(errors, this, "Correlation", String.Format("base correlation cannot be null"));

      // Attachment and detachment point consistent
      if (attachment_ >= detachment_)
        InvalidValue.AddError(errors, this, "Attachment", String.Format("Attachment point {0} must be before detachment point {1}", attachment_, detachment_));
      if ((attachment_ < 0.0) || (attachment_ > 1.0))
        InvalidValue.AddError(errors, this, "Attachment", String.Format("Attachment point must be between 0 and 100%, not {0}", attachment_));
      if ((detachment_ < 0.0) || (detachment_ > 1.0))
        InvalidValue.AddError(errors, this, "Detachment", String.Format("Detachment point must be between 0 and 100%, not {0}", detachment_));

      return;
    }


    /// <summary>
    ///   Recompute the detachment/attachment correlation
    /// </summary>
    private void UpdateCorrelations()
    {
      // Update recoveries first
      UpdateRecoveries();

#if SUPPORT_PRECOMPUTED_CORRELATION
      if (precomputedCorrelation_)
      {
        // Reset detachment point pricer
        CopyTo(dpBasketPricer_);
        dpBasketPricer_.Correlation = this.CorrelationTermStruct;

        // Set correlation and loss levels
        double dpFactor = Math.Sqrt(dpCorrelation_);
        dpBasketPricer_.SetFactor(dpFactor);
#if Handle_Defaulted_By_8_7
        dpBasketPricer_.LossLevels = this.LossLevels; // 8.7 WRONG!!!!
#else
        dpBasketPricer_.RawLossLevels = this.RawLossLevels; // 9.0 RIGHT!!!!
#endif
        dpBasketPricer_.Reset();

        if (attachment_ <= 0 || apCorrelation_ == dpCorrelation_)
        {
          // Only compute and use the detachment distributions if we need them (ie the correlations differ)
          apBasketPricer_ = dpBasketPricer_;
        }
        else
        {
          // Reset attachment point pricer
          apBasketPricer_ = ConsistentCreditSensitivity
            ? dpBasketPricer_.Duplicate() : (BasketPricer)dpBasketPricer_.Clone(); // we need a separate pricer
#if Handle_Defaulted_By_8_7
          apBasketPricer_.LossLevels = this.LossLevels; // 8.7 WRONG!!!!
#else
          apBasketPricer_.RawLossLevels = this.RawLossLevels; // 9.0 RIGHT!!!!
#endif
          double apFactor = Math.Sqrt(apCorrelation_);
          apBasketPricer_.SetFactor(apFactor);
          apBasketPricer_.Reset();
        }

        return;
      }
#endif // SUPPORT_PRECOMPUTED_CORRELATION

      // Call new function to update correlations
      UpdateCorrelations(this.BaseCorrelation);

      // Set a flag indicating correlations are ready
      this.CorrelationTermStruct = dpBasketPricer_.CorrelationTermStruct;
      this.Correlation.SetReadyState(true);
      this.correlationReady_ = true;

      return;
    }

    /// <summary>
    ///   Compute the whole distribution, save the result for later use
    /// </summary>
    private void
    ComputeAndSaveDistribution()
    {
      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing distribution for Base Correlation basket");

      // Do we need to update the correlation?
      UpdateCorrelations();

      apBasketPricer_.Reset();
      dpBasketPricer_.Reset();
      distributionComputed_ = true;

      timer.stop();
      logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());

      return;
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
    public override double[,]
    CalcLossDistribution(
      bool wantProbability,
      Dt date, double[] lossLevels)
    {
      double[] levels = lossLevels;
      int nLevels = levels.Length;
      double[,] result = new double[nLevels, 2];

      BasketPricer bp = (BasketPricer)dpBasketPricer_.Clone();
      for (int i = 0; i < nLevels; ++i)
        if (levels[i] > 0)
        {
          // Remember that for each detachment point, we may have
          // a different value of correlation
          ComputeCorrelations(bp, levels[i]);
          double[,] tmp = bp.CalcLossDistribution(
            wantProbability, date, new double[] { 0.0, levels[i] });
          result[i, 0] = levels[i];
          result[i, 1] = tmp[1, 1];
        }
        else if (i + 1 < nLevels)
        {
          // We combine 0.0 and the next level in one call
          ComputeCorrelations(bp, levels[++i]);
          double[,] tmp = bp.CalcLossDistribution(
            wantProbability, date, new double[] { 0.0, levels[i] });
          result[i, 0] = levels[i];
          result[i, 1] = tmp[1, 1];
          result[i - 1, 0] = 0.0;
          result[i - 1, 1] = tmp[0, 1];
        }
        else
        {
          ComputeCorrelations(bp, 0.0);
          double[,] tmp = bp.CalcLossDistribution(
            wantProbability, date, new double[] { 0.0, 0.01 });
          result[i, 0] = 0.0;
          result[i, 1] = tmp[0, 1];
        }

      return result;
    }

    /// <summary>
    /// Create a CorrelationTermStruct object from BaseCorrelation object and given detachment
    /// </summary>
    /// <param name="baseCorrelation">BaseCorrelation object</param>
    /// <param name="detachment">Detachment point</param>
    /// <returns>Correlation object</returns>
    private CorrelationTermStruct CalculateCorrelation(BaseCorrelationObject baseCorrelation, double detachment)
    {
      BasketPricer basket = dpBasketPricer_.Duplicate();
      basket.Correlation = CloneUtil.Clone(basket.Correlation);
      basket.RecoveryCurves = CloneUtil.Clone(basket.RecoveryCurves);
      string[] names = baseCorrelation.EntityNames;
      SingleFactorCorrelation sco = new SingleFactorCorrelation(names, 0.0);
      bool resetRecoveries = (HasFixedRecovery && SurvivalRecoveryForStrikes);
      SyntheticCDO cdo = CreateCDO();
      cdo.Attachment = 0.0;
      cdo.Detachment = detachment;
      basket.Correlation = sco;
      basket.RawLossLevels = new UniqueSequence<double>(0.0, detachment);
      if (resetRecoveries)
      {
        basket.RecoveryCurves = GetRecoveryCurves(SurvivalCurves);
        basket.HasFixedRecovery = false;
      }
      CorrelationObject codp = baseCorrelation.GetCorrelations(cdo, names, null, basket, discountCurve_, 0, 0);
      if (detachment > 0.99999999 && codp is ICorrelationSetFactor)
        ((ICorrelationSetFactor)codp).SetFactor(0.0);
      basket.Correlation = codp;
      return basket.CorrelationTermStruct;
    }

    ///
    /// <summary>
    ///   Compute the derivatives of the accumulated loss on a  dollar on the index
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///<param name="retVal">retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param> 
    public override void AccumulatedLossDerivatives(Dt date, double trancheBegin, double trancheEnd, double[] retVal)
    {
      if (this.dpBasketPricer_ is SemiAnalyticBasketPricer)
      {
        if (dpBasketPricer_.RawDetachments == null ||
            !(dpBasketPricer_.RawDetachments.Contains(trancheBegin) && dpBasketPricer_.RawDetachments.Contains(trancheEnd)))
          ComputeAndSaveSemiAnalyticSensitivities(new UniqueSequence<double>(trancheBegin, trancheEnd));
        this.dpBasketPricer_.AccumulatedLossDerivatives(date, trancheBegin, trancheEnd, retVal);
      }
      else
        throw new NotImplementedException("This pricer does not support semi-analytic sensitivity computations");
    }

    ///
    /// <summary>
    ///   Compute the derivatives of the amortized amount on a dollar on the index
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///<param name="retVal">Array of size N*(2*(K+K*(K+1)/2 ) +2), where K is the number of tenors of each survival and refinancing curve,
    /// and N is the size of the basket. Let L = 2*(K+K*(K+1)/2 ) +2
    /// retVal[i*L +0..i*L +K-1] is the gradient w.r.t the survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the survival curve ordinates of the ith name,
    /// retVal[i*L +K + K*(K+1)/2..i*L +2K + K*(K+1)/2-1] is the gradient w.r.t the refinancing curve ordinates of the ith name, 
    /// retVal[i*L +2K + K*(K+1)/2..i*L +2*(K + K*(K+1)/2)-1] is the hessian w.r.t the refinancing curve ordinates of the ith name,
    /// retVal[i*L +2*(K + K*(K+1)/2)] is the value of default of the ith name,
    /// retVal[i*L +2*(K + K*(K+1)/2)+1] is the derivative with respect to the ith obligor's mean recovery rate of the ith name</param> 
    public override void AmortizedAmountDerivatives(Dt date, double trancheBegin, double trancheEnd, double[] retVal)
    {
      if (this.NoAmortization)
      {
        for (int i = 0; i < retVal.Length; i++)
          retVal[i] = 0.0;
        return;
      }
      if (this.dpBasketPricer_ is SemiAnalyticBasketPricer)
      {
        if (dpBasketPricer_.RawDetachments == null ||
              !(dpBasketPricer_.RawDetachments.Contains(trancheBegin) && dpBasketPricer_.RawDetachments.Contains(trancheEnd)))
          ComputeAndSaveSemiAnalyticSensitivities(new UniqueSequence<double>(trancheBegin, trancheEnd));
        this.dpBasketPricer_.AmortizedAmountDerivatives(date, trancheBegin, trancheEnd, retVal);
      }
      else
        throw new NotImplementedException("This pricer does not support semi-analytic sensitivity computations");
    }


    /// <summary>
    ///For internal use only
    ///<preliminary/>
    /// </summary>
    ///<param name="rawDetachments">Detachment of the loss level for which to compute tranche loss derivatives</param>
    /// <remarks>
    /// Compute the semi-analytic derivative of expected tranche loss at TimeGrid (tranche amortization) w.r.t individual name curve ordinates 
    /// and stores them for later use.
    /// </remarks>
    internal override void ComputeAndSaveSemiAnalyticSensitivities(UniqueSequence<double> rawDetachments)
    {
      if (dpBasketPricer_ == null)
      {
        throw new ToolkitException("Underlying semi-analytic basket pricers is null");
      }
      SemiAnalyticBasketPricer pricer;
      if (dpBasketPricer_ is SemiAnalyticBasketPricer)
      {
        //CopyTo(dpBasketPricer_);
        UpdateCorrelations();
        pricer = (SemiAnalyticBasketPricer)dpBasketPricer_;
      }
      else
      {
        throw new NotImplementedException("This pricer does not implement semi-analytic greeks computation.");
      }
      var corrs = new CorrelationTermStruct[rawDetachments.Count];
      for (int i = 0; i < corrs.Length; i++)
      {
        if (rawDetachments[i] <= 1e-6 || rawDetachments[i] >= 1 - 1e-6)
        {
          string[] names = BaseCorrelation.EntityNames;
          Dt[] dates = BaseCorrelation.GetTermStructDates();
          if (dates == null || dates.Length == 0)
          {
            dates = new[] { Maturity };
          }
          var data = new double[dates.Length];
          corrs[i] = new CorrelationTermStruct(names, data, dates, BaseCorrelation.MinCorrelation,
                                                BaseCorrelation.MaxCorrelation);
          corrs[i].SetFactor(0.0); //does not matter anyways

        }
        else
          corrs[i] = CalculateCorrelation(BaseCorrelation, rawDetachments[i]);
      }
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      Curve[] cCurves;
      Curve[] rCurves;
      TransformRefinanceCurves(start, Maturity, SurvivalCurves, RefinanceCurves, out cCurves, out rCurves,
                               RefinanceCorrelations, StepSize, StepUnit);
      var model = pricer.GetModelChoice();
      Reset();
      pricer.ComputeAndSaveSemiAnalyticSensitivities(cCurves, rCurves, model, rawDetachments, corrs);
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
      if (!distributionComputed_)
        ComputeAndSaveDistribution();
      double apLoss = apBasketPricer_.AccumulatedLoss(date, 0.0, trancheBegin);
      double dpLoss = dpBasketPricer_.AccumulatedLoss(date, 0.0, trancheEnd);
      logger.DebugFormat("Computed loss for {0}-{1} @{2} as {3} = {4} - {5}", trancheBegin, trancheEnd, date, dpLoss, apLoss, dpLoss - apLoss);

      return (dpLoss - apLoss);
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
      if (!distributionComputed_)
        ComputeAndSaveDistribution();

      double apAmort = apBasketPricer_.AmortizedAmount(date, 0.0, trancheBegin);
      double dpAmort = dpBasketPricer_.AmortizedAmount(date, 0.0, trancheEnd);

      logger.DebugFormat("Computed Amortization for {0}-{1} @{2} as {3} = {4} - {5}", trancheBegin, trancheEnd, date, dpAmort, apAmort, dpAmort - apAmort);

      return (dpAmort - apAmort);
    }


    /// <summary>
    ///   Reset the pricer such that in the next request for AccumulatedLoss()
    ///   or AmortizedAmount(), it recompute everything.
    /// </summary>
    public override void
    Reset()
    {
      distributionComputed_ = false;
      ResetDerivatives();
      if (rescaleStrike_)
        ResetCorrelation();
      if (correlationReady_)
        correlationReady_ = !this.IsCorrelationChanged;

    }


    /// <summary>
    ///   Set the correlation between any pair of credits to be the same number
    /// </summary>
    ///
    /// <param name="factor">factor to set</param>
    ///
    /// <remarks>
    ///   The correlation between pairs are set to the square of the factor.
    /// </remarks>
    public override void
    SetFactor(double factor)
    {
      this.Correlation = null;
      double corr = factor * factor;
      apCorrelation_ = dpCorrelation_ = corr;
    }

    /// <summary>
    ///   Calculate base correlation delta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Base correlation level delta is the change in the upfront fee
    ///   (for tranches with non-zero upfront)
    ///   or in the spread (for tranches with zero upfront), due to a 1% up bump
    ///   in both attachment correlation and detachment correlation.</para>
    ///
    ///   <para>Base correlation skew delta is the change in the upfront fee
    ///   (for tranches with non-zero upfront)
    ///   or in the spread (for tranches with zero upfront), due to a 1% up bump
    ///   in the detachment correlation while holding the attachment correlation unchanged.</para>
    ///
    ///   <para>If the underlying cdo has nonzero fee, the delta is the change in upfront fee.
    ///   Otherwise, it is the change in spread in raw values.  In both case, the delta is scaled by
    ///   the changes in correlations, i.e., it is the percentage change in value caused by 1% changes
    ///   in correlations.</para>
    /// </remarks>
    ///
    /// <param name="pricer">CDO pricer based on base correlation</param>
    /// <param name="bumpSize">Bump size</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="parallel">Parallel bump both attachment or detachment correlation
    ///                        or bump only the detachment correlation</param>
    ///
    /// <returns>BaseCorrelation level or skew delta</returns>
    ///
    /// <exclude />
    public static double
    BaseCorrelationDelta(
      SyntheticCDOPricer pricer,
      double bumpSize,
      bool relative,
      bool parallel)
    {
      bool doUpfront = (Math.Abs(pricer.CDO.Fee) >= 1.0E-7);
      if (parallel)
        return BaseCorrelationDelta(pricer, bumpSize, bumpSize, relative, doUpfront, true);
      else
        return BaseCorrelationDelta(pricer, 0.0, bumpSize, relative, doUpfront, true);
    }

    /// <summary>
    ///   Calculate base correlation delta
    /// </summary>
    /// <remarks>
    ///   <para>Base correlation level delta is the change in the MTM value
    ///   with unit notional (i.e., upfront value), or the change in the spread,
    ///   due to a 1% up bump in both attachment correlation and detachment
    ///   correlation.</para>
    ///   <para>Base correlation skew delta is the change in the MTM value
    ///   with unit notional (i.e., upfront value), or the change in the spread,
    ///   due to a 1% up bump in the detachment correlation while holding the 
    ///   attachment correlation unchanged.</para>
    /// </remarks>
    /// <param name="pricer">CDO pricer based on base correlation</param>
    /// <param name="apBumpSize">Bump size to attachment correlation</param>
    /// <param name="dpBumpSize">Bump size to detachment correlation</param>
    /// <param name="bumpRelative">Bump is relative</param>
    /// <param name="measure">Price measure to calculate (Pv, Protection Pv, Fee Pv, etc.)</param>
    /// <param name="scale">Delta is scaled by the change in correlation</param>
    /// <returns>BaseCorrelation delta</returns>
    public static double
    BaseCorrelationDelta(
      SyntheticCDOPricer pricer,
      double apBumpSize,
      double dpBumpSize,
      bool bumpRelative,
      string measure,
      bool scale)
    {
      return BaseCorrelationDelta(
        new PricerEvaluator(pricer, measure, false, false),
        apBumpSize, dpBumpSize, bumpRelative, scale);
    }

    /// <summary>
    ///   Calculate base correlation delta
    /// </summary>
    /// <remarks>
    ///   <para>Base correlation level delta is the change in the MTM value
    ///   with unit notional (i.e., upfront value), or the change in the spread,
    ///   due to a 1% up bump in both attachment correlation and detachment
    ///   correlation.</para>
    ///   <para>Base correlation skew delta is the change in the MTM value
    ///   with unit notional (i.e., upfront value), or the change in the spread,
    ///   due to a 1% up bump in the detachment correlation while holding the 
    ///   attachment correlation unchanged.</para>
    /// </remarks>
    /// <param name="pricer">CDO pricer based on base correlation</param>
    /// <param name="apBumpSize">Bump size to attachment correlation</param>
    /// <param name="dpBumpSize">Bump size to detachment correlation</param>
    /// <param name="bumpRelative">Bump is relative</param>
    /// <param name="feeDelta">If true, calculate the changes in break-even fee; otherwise, the changes in break-even spread</param>
    /// <param name="scale">Delta is scaled by the change in correlation</param>
    /// <returns>BaseCorrelation delta</returns>
    [Obsolete("Please use explicit price measure")]
    public static double
      BaseCorrelationDelta(
      SyntheticCDOPricer pricer,
      double apBumpSize,
      double dpBumpSize,
      bool bumpRelative,
      bool feeDelta,
      bool scale)
    {
      var evaluator = feeDelta
        ? new PricerEvaluator(pricer, p =>
          ((SyntheticCDOPricer) p).BreakEvenFee(false))
        : new PricerEvaluator(pricer, p =>
          ((SyntheticCDOPricer) p).BreakEvenPremium(false));
      return BaseCorrelationDelta(evaluator,
        apBumpSize, dpBumpSize, bumpRelative, scale);
    }

    private static double BaseCorrelationDelta(
      PricerEvaluator evaluator,
      double apBumpSize,
      double dpBumpSize,
      bool bumpRelative,
      bool scale)
    {
      var pricer = (SyntheticCDOPricer) evaluator.Pricer;
      if (!(pricer.Basket is BaseCorrelationBasketPricer))
        throw new ArgumentException("Pricer is not base correlation pricer");

      // parallel
      bool parallel = Math.Abs(apBumpSize - dpBumpSize) <= 1E-8;

      // calculate the base value and this also update correlations
      // The false means not to change CDO type when calculate BEF or BEP
      double baseValue = evaluator.Evaluate();

      // Now prepare to bump detachment/attachment correlations
      //-
      BaseCorrelationBasketPricer basket = (BaseCorrelationBasketPricer)pricer.Basket;
      BasketPricer dpBasket = basket.dpBasketPricer_; // detachment pricer
      BasketPricer apBasket = basket.apBasketPricer_; // attachment pricer
      if (dpBasket == apBasket && basket.Attachment >= 1E-7 && !parallel)
      {
        // we need a separate attachment pricer
        basket.apBasketPricer_ = apBasket = (BasketPricer)dpBasket.Duplicate();
        apBasket.Reset();
        apBasket.RawLossLevels = new UniqueSequence<double>(0.0, basket.Attachment); // 9.0 RIGHT!!!!
        dpBasket.RawLossLevels = new UniqueSequence<double>(0.0, basket.Detachment);
      }

      // bump the detachment correlations
      dpBasket.Correlation.Modified = true;
      double change = dpBasket.Correlation.BumpCorrelations(dpBumpSize, bumpRelative, false);
      dpBasket.Reset();

      // bump the attachment correlations if necessary
      if (dpBasket != apBasket && Math.Abs(apBumpSize) > 1E-8)
      {
        apBasket.Correlation.Modified = true;
        double da = apBasket.Correlation.BumpCorrelations(apBumpSize, bumpRelative, false);
        apBasket.Reset();
        change = (change + da) / 2;
      }

      // With these two set, correlation will not be updated
      basket.distributionComputed_ = true;
      basket.correlationReady_ = true;

      double delta = Double.NaN;
      try
      {
        // The false means not to change CDO type when calculate BEF or BEP
        double value = evaluator.Evaluate();
        delta = value - baseValue;
        if (Math.Abs(change) > 1E-8 && scale)
          delta /= change * 100;
      }
      finally
      {
        // Set a flag telling the pricer that both the dpBasketPricer_
        // and apBasketPricer_ are invalid and should be re-constructed.
        basket.correlationReady_ = false;
        basket.Reset();
      }

      return delta;
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
    ///   <para>Recalculation is avoided if the basket's survival curves and altSurvivalCurves
    ///   are the same.</para>
    ///
    ///   <para>Note: <paramref name="altSurvivalCurves"/> must contain bumped curves matching
    ///   all SurvivalCurves for the basket. If original curves are passed in as part of the
    ///   bumped curve set (ie unbumped), no bump will be performed.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
    internal protected override double[,] BumpedPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity)
    {
      // If we need rescale the strike, use a generic but slow routine
      if (this.RescaleStrike && null != this.Correlation)
        return base.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);

      // For exact Jump to default, we handle rescale strikes separately.
      if (NeedExactJtD(pricers))
      {
        using (new CorrelationFixer(this))
          return base.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);
      }

      // For safety, if any measure is not additive, we use the generic routine
      foreach (PricerEvaluator p in pricers)
        if (!p.IsAdditive)
          return base.BumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);

      // Sanity check
      int basketSize = Count, bumpCount = altSurvivalCurves.Length;
      if (bumpCount != basketSize && bumpCount != this.GetSurvivalBumpCount(includeRecoverySensitivity))
      {
        throw new System.ArgumentException(String.Format("Invalid number of survival curves. Must be {0}, not {1}", basketSize, bumpCount));
      }
      Timer timer = new Timer();
      timer.start();
      logger.Debug("Computing spread sensitivity deltas for Homogeneous basket");

      // now create and fill the table of values
      double[,] result = new double[bumpCount + 1, pricers.Length];

      CalculateRegularPvTable(result, pricers, includeRecoverySensitivity,
        bumpCount > basketSize
          ? altSurvivalCurves.Take(basketSize).ToArray()
          : altSurvivalCurves);

      // Compute the sensitivity from the unsettled defaults
      if (bumpCount > basketSize)
      {
        CalculateDefaultPvTable(result, pricers, altSurvivalCurves);
      }

      timer.stop();
      logger.DebugFormat("Completed basket spread sensitivity deltas in {0} seconds", timer.getElapsed());

      return result;
    }

    private void CalculateRegularPvTable(
      double[,] result,
      PricerEvaluator[] pricers,
      bool includeRecoverySensitivity,
      SurvivalCurve[] altSurvivalCurves)
    {
      // Since we only calculate the correlations once,
      // we do it here and save a copy of both correlations.
      this.UpdateCorrelations();
      CorrelationObject dpCorrelation = (CorrelationObject)dpBasketPricer_.Correlation;
      CorrelationObject apCorrelation = (CorrelationObject)apBasketPricer_.Correlation;

      // now create and fill the table of values
      int basketSize= Count, nPricers = pricers.Length;

      // a dummy pricer array
      PricerEvaluator[] dummyPricers = new PricerEvaluator[1];

      // a copy of basket pricer
      BasketPricer basket = (BasketPricer)dpBasketPricer_.Duplicate();

      // Set correlation and loss levels

      // now fill the table
      for (int j = 0; j < nPricers; ++j)
      {
        SyntheticCDOPricer pricerj = (SyntheticCDOPricer)pricers[j].Pricer;
        SyntheticCDO pricerCDO = pricerj.CDO;
        double ap = pricerCDO.Attachment;
        double dp = pricerCDO.Detachment;

        // set the detachment point correlation
        basket.Correlation = dpCorrelation;

        if (apCorrelation == dpCorrelation)
        {
          // Need only one pricer if detachment correlation and attachment correlation are the same
#if Handle_Defaulted_By_8_7
          basket.SetLossLevels(new double[] { ap, dp }); // 8.7 WRONG!!!!
#else
          basket.RawLossLevels = new UniqueSequence<double>(ap, dp); // 9.0 RIGHT!!!!
#endif
          basket.Reset();
          dummyPricers[0] = pricers[j].Substitute(
            pricerj.Substitute(pricerCDO, basket, 0, false));
          double[,] tmp = basket.BumpedPvs(dummyPricers,
            altSurvivalCurves, includeRecoverySensitivity);
          for (int i = 0; i <= basketSize; ++i)
            result[i, j] = tmp[i, 0];
        }
        else
        {
          // Here we calculate detachment prices and attachment prices separately
#if Handle_Defaulted_By_8_7
          basket.SetLossLevels(new double[] { 0.0, dp }); // 8.7 WRONG!!!!
#else
          basket.RawLossLevels = new UniqueSequence<double>(0.0, dp); // 9.0 RIGHT!!!
#endif
          basket.Reset();

          double totalPrincipal = pricerj.TotalPrincipal;
          SyntheticCDO cdo = (SyntheticCDO)pricerCDO.Clone();
          cdo.Attachment = 0.0;
          dummyPricers[0] = pricers[j].Substitute(
            pricerj.Substitute(cdo, basket, totalPrincipal * cdo.TrancheWidth, false));
          double[,] tmp1 = basket.BumpedPvs(dummyPricers, altSurvivalCurves, includeRecoverySensitivity);

          // set attachment point correlation
#if Handle_Defaulted_By_8_7
          basket.SetLossLevels(new double[] { 0.0, ap }); // 8.7 WRONG!!!!
#else
          basket.RawLossLevels = new UniqueSequence<double>(0.0, ap); // 9.0 RIGHT!!!!
#endif
          basket.Correlation = apCorrelation;
          basket.Reset();

          cdo.Detachment = ap;
          dummyPricers[0] = pricers[j].Substitute(
            pricerj.Substitute(cdo, basket, totalPrincipal * cdo.TrancheWidth, false));
          double[,] tmp0 = basket.BumpedPvs(dummyPricers, altSurvivalCurves, includeRecoverySensitivity);

          for (int i = 0; i <= basketSize; ++i)
            result[i, j] = tmp1[i, 0] - tmp0[i, 0];
        }
      }

    }

    /// <summary>
    ///   Create a copy of detachment basket pricer
    ///   <preliminary/>
    /// </summary>
    ///
    /// <remarks>For internal use only.  Currently it is only used by implied correlation codes.</remarks>
    ///
    /// <param name="keepTermStruct">Keep term struct, or flat it (as required by implied correlation)</param>
    ///
    /// <returns>Basket pricer</returns>
    /// <exclude />
    public BasketPricer CreateDetachmentBasketPricer(bool keepTermStruct)
    {
      BasketPricer basket = GetBasketCalculator();
      if (keepTermStruct)
      {
        if (!distributionComputed_)
          UpdateCorrelations();
        basket = (BasketPricer)basket.Clone();
      }
      else
      {
        basket = (BasketPricer)basket.Clone();
        CopyTo(basket);
        basket.Correlation = CorrelationFactory.CreateCorrelationTermStruct(
          null, this.EntityNames, this.SurvivalCurves,
          UseNaturalSettlement ? (PortfolioStart.IsEmpty() ? Settle : PortfolioStart)
            : this.AsOf);
        // need to reset tenor index
        //if (basket is BootstrapHeterogeneousBasketPricer)
        //  ((BootstrapHeterogeneousBasketPricer)basket).SetMaturity(basket.Maturity);
        basket.Reset();
      }
      return basket;
    }
    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Distribution computed
    /// </summary>
    public bool DistributionComputed
    {
      get { return distributionComputed_; }
      set { distributionComputed_ = value; }
    }


    /// <summary>
    ///   Attachment point base correlation
    /// </summary>
    public double APCorrelation
    {
      get
      {
        SetDpApCorrelations();
        return apCorrelation_;
      }
      set { apCorrelation_ = value; }
    }


    /// <summary>
    ///   Detachment point base correlation
    /// </summary>
    public double DPCorrelation
    {
      get
      {
        SetDpApCorrelations();
        return dpCorrelation_;
      }
      set { dpCorrelation_ = value; }
    }


    /// <summary>
    ///   Attachment base correlation strike
    /// </summary>
    public double APStrike
    {
      get
      {
        if (!this.correlationReady_)
          UpdateCorrelations();
        return CalculateStrike(true);
      }
    }


    /// <summary>
    ///   Detachment base correlation strike
    /// </summary>
    public double DPStrike
    {
      get
      {
        if (!this.correlationReady_)
          UpdateCorrelations();
        return CalculateStrike(false);
      }
    }

    /// <summary>
    ///   Attachment point
    /// </summary>
    public double Attachment
    {
      get { return attachment_; }
      set
      {
        attachment_ = value;
        correlationReady_ = false;
      }
    }


    /// <summary>
    ///   Detachment point
    /// </summary>
    public double Detachment
    {
      get { return detachment_; }
      set
      {
        detachment_ = value;
        correlationReady_ = false;
      }
    }

    /// <summary>
    ///   Base correlation
    /// </summary>
    public BaseCorrelationObject BaseCorrelation
    {
      get { return (BaseCorrelationObject)this.Correlation; }
      set
      {
        Set(null, value);
      }
    }

    /// <summary>
    ///   Re-scale strike points every time we price.
    /// </summary>
    public bool RescaleStrike
    {
      get { return rescaleStrike_; }
      set { rescaleStrike_ = value; }
    }

    /// <summary>
    ///   Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set { discountCurve_ = value; }
    }

    /// <summary>
    ///   Use survival recovery for strike calculation
    ///   <preliminary/>
    /// </summary>
    /// <exclude />
    public bool SurvivalRecoveryForStrikes
    {
      get { return useSurvivalRecovery_; }
      set { useSurvivalRecovery_ = value; }
    }

    internal bool ConsistentCreditSensitivity { get; set; }
    #endregion // Properties

    #region Data

    private BasketPricer dpBasketPricer_;
    private BasketPricer apBasketPricer_;
    private bool distributionComputed_;

    private double attachment_;
    private double detachment_;
    private double apCorrelation_;  // Attachment point base correlation
    private double dpCorrelation_;  // Detachment point base correlation
    private bool rescaleStrike_;
    private bool correlationReady_; // for internal use only
    private bool useSurvivalRecovery_;

    private DiscountCurve discountCurve_;

#if SUPPORT_PRECOMPUTED_CORRELATION
    private bool precomputedCorrelation_ = false;
#endif //SUPPORT_PRECOMPUTED_CORRELATION

    [Mutable] private CorrelationFixer correlationFixer_;
    #endregion Data

    #region Correlation_Calculation
    /// <summary>
    /// Locks the interpolated correlation from the other basket.
    /// </summary>
    /// <param name="basket">The basket.</param>
    public void LockCorrections(BaseCorrelationBasketPricer basket)
    {
      var fixer = new CorrelationFixer(basket);
      basket.correlationFixer_ = fixer;
      apBasketPricer_ = fixer.ApBasket;
      dpBasketPricer_ = fixer.DpBasket;
      correlationReady_ = true;
      RescaleStrike = false;
    }

    internal IDisposable LockCorrection()
    {
      return new CorrelationFixer(this);
    }
    [Serializable]
    class CorrelationFixer : IDisposable
    {
      private BaseCorrelationBasketPricer basket_;
      private CorrelationObject savedCorr_;
      internal BasketPricer DpBasket{ get; set;}
      internal BasketPricer ApBasket{ get; set;}
      internal CorrelationFixer(BaseCorrelationBasketPricer basket)
      {
        if (basket.RescaleStrike || basket.correlationFixer_ != null) return;
        basket_ = basket;
        savedCorr_ = basket.Correlation;
        basket.UpdateCorrelations();
        basket.correlationFixer_ = this;
        DpBasket = basket.dpBasketPricer_;
        ApBasket = basket.apBasketPricer_;
      }

      #region IDisposable Members

      public void Dispose()
      {
        if (basket_ != null && savedCorr_ != null)
        {
          basket_.correlationFixer_ = null;
          basket_.Correlation = savedCorr_;
          basket_.ResetCorrelation();
        }
      }

      #endregion
    }

    /// <summary>
    ///   Recompute the detachment/attachment correlation
    /// </summary>
    private void UpdateCorrelations(BaseCorrelationObject baseCorrelation)
    {
      if (correlationFixer_ != null)
      {
        dpBasketPricer_ = correlationFixer_.DpBasket;
        apBasketPricer_ = correlationFixer_.ApBasket;
        correlationReady_ = true;
      }

      if (this.correlationReady_ && !this.RescaleStrike)
      {
        CorrelationObject co = (CorrelationObject)dpBasketPricer_.Correlation;
        CopyTo(dpBasketPricer_);
        dpBasketPricer_.Correlation = co;
        dpBasketPricer_.Reset();
        if (dpBasketPricer_ != apBasketPricer_)
        {
          dpBasketPricer_.RawLossLevels = new UniqueSequence<double>(0.0, detachment_); // 9.0 RIGHT!!!!
          co = (CorrelationObject)apBasketPricer_.Correlation;
          apBasketPricer_ = ConsistentCreditSensitivity
            ? dpBasketPricer_.Duplicate() : (BasketPricer)dpBasketPricer_.Clone();
          apBasketPricer_.Correlation = co;
          apBasketPricer_.RawLossLevels = new UniqueSequence<double>(0.0, attachment_); // 9.0 RIGHT!!!!
          apBasketPricer_.Reset();
        }
        else
        {
          apBasketPricer_ = dpBasketPricer_;
          apBasketPricer_.RawLossLevels = new UniqueSequence<double>(attachment_, detachment_); // 9.0 RIGHT!!!!
        }
        return;
      }

      // Base correlation object
      string[] names = this.BaseCorrelation.EntityNames;
      SingleFactorCorrelation sco = new SingleFactorCorrelation(names, 0.0);
      bool resetRecoveries = (HasFixedRecovery && SurvivalRecoveryForStrikes);

      // Create cdo
      SyntheticCDO cdo = CreateCDO();

      // Reset detachment point pricer
      cdo.Attachment = 0.0;
      cdo.Detachment = detachment_;
      CopyTo(dpBasketPricer_);
      dpBasketPricer_.Correlation = sco;

      dpBasketPricer_.RawLossLevels = new UniqueSequence<double>(0.0, detachment_); // 9.0 RIGHT
      if (resetRecoveries)
      {
        dpBasketPricer_.RecoveryCurves = GetRecoveryCurves(this.SurvivalCurves);
        dpBasketPricer_.HasFixedRecovery = false;
      }
      CorrelationObject codp = baseCorrelation.GetCorrelations(
        cdo, names, null, dpBasketPricer_, discountCurve_, 0, 0);
      if (detachment_ > 0.99999999 && codp is ICorrelationSetFactor)
        ((ICorrelationSetFactor)codp).SetFactor(0.0);
      dpBasketPricer_.Correlation = codp;
      if (resetRecoveries)
      {
        dpBasketPricer_.RecoveryCurves = this.RecoveryCurves;
        dpBasketPricer_.HasFixedRecovery = this.HasFixedRecovery;
      }

      // Reset attachment pricer
      bool diff = false;
      if (attachment_ > 1.0E-7)
      {
        diff = true;
        cdo.Attachment = 0.0;
        cdo.Detachment = attachment_;
        // we need a separate attachment pricer and correlation object
        //-apBasketPricer_ = (BasketPricer)BaseEntityObject.MemberwiseClone(dpBasketPricer_);
        //-apBasketPricer_ = (BasketPricer)dpBasketPricer_.Clone();
        apBasketPricer_ = dpBasketPricer_.Duplicate();
        apBasketPricer_.Correlation = new SingleFactorCorrelation(names, 0.0);
        apBasketPricer_.RawLossLevels = new UniqueSequence<double>(0.0, attachment_); // 9.0 RIGHT!!!!
        if (resetRecoveries)
        {
          apBasketPricer_.RecoveryCurves = GetRecoveryCurves(this.SurvivalCurves);
          apBasketPricer_.HasFixedRecovery = false;
        }
        CorrelationObject coap = baseCorrelation.GetCorrelations(
          cdo, names, null, apBasketPricer_, discountCurve_, 0, 0);
        apBasketPricer_.Correlation = coap;
        if (resetRecoveries)
        {
          apBasketPricer_.RecoveryCurves = this.RecoveryCurves;
          apBasketPricer_.HasFixedRecovery = this.HasFixedRecovery;
        }
        apBasketPricer_.Reset();
      }

      // reset the underlying baskets
      dpBasketPricer_.Maturity = this.Maturity;
      dpBasketPricer_.Reset();
      if (!diff)
      {
        apBasketPricer_ = dpBasketPricer_;
        apBasketPricer_.RawLossLevels = new UniqueSequence<double>(attachment_, detachment_); // 9.0 RIGHT!!!!
      }
      else
      {
        apBasketPricer_.Maturity = this.Maturity;
        apBasketPricer_.Reset();
      }

      return;
    }

    /// <summary>
    ///   Compute correlations at a detachment point
    /// </summary>
    private void ComputeCorrelations(BasketPricer basket, double detachment)
    {
      ComputeCorrelations(basket, this.BaseCorrelation, detachment);
    }

    /// <summary>
    ///   Compute correlations at a detachment point
    /// </summary>
    private void ComputeCorrelations(
      BasketPricer basket,
      BaseCorrelationObject baseCorrelation,
      double detachment)
    {
#if SUPPORT_PRECOMPUTED_CORRELATION
      if (precomputedCorrelation_)
      {
        throw new System.NotSupportedException("Old constructor does not support this function!  Please construct the pricer using a non-null base correlation object.");
      }
#endif // SUPPORT_PRECOMPUTED_CORRELATION

      // Base correlation object
      string[] names = this.BaseCorrelation.EntityNames;
      SingleFactorCorrelation sco = new SingleFactorCorrelation(names, 0.0);

      // Create cdo
      SyntheticCDO cdo = CreateCDO();

      // Reset detachment point pricer
      cdo.Attachment = 0.0;
      cdo.Detachment = detachment;
      CopyTo(basket);

      // Reset the loss levels
#if Handle_Defaulted_By_8_7
      basket.LossLevels = CheckLossLevels(new double[] { 0.0, detachment <= 0 ? 0.03 : detachment }, // 8.7 WRONG!!!!
        this.PreviousLoss, this.PreviousAmortized);
#else
      basket.RawLossLevels = new UniqueSequence<double>(0.0, detachment <= 0 ? 0.03 : detachment); // 9.0 RIGHT
#endif

      // Find the detachment correlation object
      basket.Correlation = sco;
      bool resetRecoveries = HasFixedRecovery && SurvivalRecoveryForStrikes;
      if (resetRecoveries)
      {
        basket.RecoveryCurves = GetRecoveryCurves(this.SurvivalCurves);
        basket.HasFixedRecovery = false;
      }
      CorrelationObject codp = baseCorrelation.GetCorrelations(
        cdo, names, null, basket, discountCurve_, 0, 0);
      if (detachment > 0.99999999 && codp is ICorrelationSetFactor)
        ((ICorrelationSetFactor)codp).SetFactor(0.0);
      basket.Correlation = codp;
      if (resetRecoveries)
      {
        basket.RecoveryCurves = this.RecoveryCurves;
        basket.HasFixedRecovery = this.HasFixedRecovery;
      }

      // reset the underlying baskets
      basket.Maturity = this.Maturity;
      basket.Reset();

      return;
    }
    #endregion // Correlation_Calculation

    #region Backward_Compatible

    /// <summary>
    ///   Set old style ApCorrelation and DpCorrelation
    /// </summary>
    private void SetDpApCorrelations()
    {
      if (dpBasketPricer_ == null || !correlationReady_)
        UpdateCorrelations();
      dpCorrelation_ = apCorrelation_ = FindLastCorrelationValue(dpBasketPricer_.Correlation, this.Maturity);
      if (dpBasketPricer_ != apBasketPricer_)
        apCorrelation_ = FindLastCorrelationValue(apBasketPricer_.Correlation, this.Maturity);
    }

    /// <summary>
    ///   Find the last correlation number by maturity date
    /// </summary>
    /// <param name="correlation">correlationObject</param>
    /// <param name="maturity">maturity date</param>
    /// <returns>correlation value</returns>
    private static double FindLastCorrelationValue(
      CorrelationObject correlation, Dt maturity)
    {
      if (correlation is CorrelationTermStruct)
      {
        return GetCorrelation((CorrelationTermStruct)correlation, maturity);
      }
      else if (correlation is FactorCorrelation)
      {
        return GetCorrelation((FactorCorrelation)correlation);
      }
      else if (correlation is CorrelationMixed)
      {
        double result = 0;
        CorrelationMixed com = (CorrelationMixed)correlation;
        double[] weights = com.Weights;
        double sumWeight = 0;
        CorrelationObject[] corrs = com.CorrelationObjects;
        for (int i = 0; i < corrs.Length; ++i)
          if (Math.Abs(weights[i]) > 1E-15)
          {
            sumWeight += weights[i];
            result += weights[i] * FindLastCorrelationValue(corrs[i], maturity);
          }
        if (Math.Abs(sumWeight) > 1E-12)
          return result / sumWeight;
        return result;
      }
      else
        throw new System.NotSupportedException("Only correlation consists of FactorCorrelation and CorrelationTermStruct are support");
    }

    /// <summary>
    ///   Calculate average correlation from a factor correlation object
    /// </summary>
    /// <param name="co">Factor correlation object</param>
    /// <returns>Correlation value</returns>
    private static double GetCorrelation(FactorCorrelation co)
    {
      double[] data = co.Correlations;
      if (data.Length == 1)
        return data[0] * data[0];

      return GetCorrelation(data, 0, data.Length, co.BasketSize);
    }

    /// <summary>
    ///   Find average correlation of a date from correlation term structure object
    /// </summary>
    /// <param name="co">correlation term structure object</param>
    /// <param name="maurity">maturity date</param>
    /// <returns>correlation value</returns>
    private static double GetCorrelation(
      CorrelationTermStruct co, Dt maurity)
    {
      double[] data = co.Correlations;
      Dt[] dates = co.Dates;
      int tenor = FindLastDateIndex(dates, maurity);
      int stride = data.Length / dates.Length;

      // the case of single factor
      if (stride <= 1)
      {
        double factor = data[tenor];
        return factor * factor;
      }

      // the case of factor array
      return GetCorrelation(data, stride * tenor, stride, co.BasketSize);
    }

    /// <summary>
    ///   Calculate average correlation from a factor corraltion array
    /// </summary>
    /// <param name="data">factor array</param>
    /// <param name="baseIdx">base index</param>
    /// <param name="stride">total size of the factor array segment</param>
    /// <param name="basketSize">number of names</param>
    /// <returns>average correlation</returns>
    private static double GetCorrelation(
      double[] data, int baseIdx, int stride, int basketSize)
    {
      double result = 0;
      int numFactors = stride / basketSize;
      if (numFactors <= 1)
      {
        for (int i = 0; i < stride; ++i)
        {
          double factor = data[baseIdx + i];
          result += (factor * factor - result) / (i + 1);
        }
      }
      else
      {
        for (int i = 0; i < basketSize; ++i)
        {
          double corr = 0;
          for (int f = 0; f < numFactors; ++f)
          {
            double factor = data[baseIdx + basketSize * f];
            corr += factor * factor;
          }
          result += (corr - result) / (i + 1);
        }
      }
      return result;
    }

    /// <summary>
    ///   Find the last index corresponding to a date
    /// </summary>
    /// <param name="dates">Tenor dates</param>
    /// <param name="maturity">maturity</param>
    /// <returns>tenor index index</returns>
    private static int FindLastDateIndex(Dt[] dates, Dt maturity)
    {
      if (dates == null || dates.Length <= 1)
        return 0;
      for (int i = 0; i < dates.Length; ++i)
        if (Dt.Cmp(maturity, dates[i]) <= 0)
          return i;
      return dates.Length - 1;
    }

    /// <summary>
    ///   Calculate attachment or detachment strike
    /// </summary>
    /// <remarks>This function is fast once correlation is ready.</remarks>
    /// <param name="apStrike">True mean attachment strike, false means detachment strike</param>
    /// <returns>strike</returns>
    private double CalculateStrike(bool apStrike)
    {
      // find base correlation strikeMethod
      BaseCorrelationObject bco = this.BaseCorrelation;
      BaseCorrelationStrikeMethod strikeMethod = BaseCorrelationStrikeMethod.Unscaled;
      BaseEntity.Toolkit.Base.BaseCorrelation.IStrikeEvaluator strikeEvaluator = null;
      GetStrikeMethod(bco, ref strikeMethod, ref strikeEvaluator);

      // Get dpBasket and apBasket up to date
      if (!distributionComputed_)
        UpdateCorrelations();

      // Get the correct basket
      BasketPricer basket = apStrike ? apBasketPricer_ : dpBasketPricer_;

      // Create cdo for calculation
      SyntheticCDO cdo = CreateCDO();
      cdo.Attachment = 0.0;
      cdo.Detachment = apStrike ? attachment_ : detachment_;

      //- Create CDO Pricer
      SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basket, discountCurve_, 1.0, null);

      // Calculate strike.
      double[] tmp = BaseEntity.Toolkit.Base.BaseCorrelation.Strike(
        new SyntheticCDOPricer[] { pricer }, strikeMethod, strikeEvaluator, null);

      return tmp[0];
    }

    /// <summary>
    ///   Find strike method and evaluator in base correlation object
    /// </summary>
    /// <param name="bco">Base Correlation Object</param>
    /// <param name="strikeMethod">Strike method (output)</param>
    /// <param name="strikeEvaluator">Strike evaluator (output)</param>
    private static void GetStrikeMethod(
      BaseCorrelationObject bco,
      ref  BaseCorrelationStrikeMethod strikeMethod,
      ref BaseEntity.Toolkit.Base.BaseCorrelation.IStrikeEvaluator strikeEvaluator)
    {
      if (bco == null)
        throw new System.NotSupportedException("Base correlation object is null and strike calculation is not applicable");

      if (bco is BaseCorrelationMixed)
      {
        // recursive
        BaseCorrelationObject[] bcs = ((BaseCorrelationMixed)bco).BaseCorrelations;
        if (bcs != null)
        {
          foreach (BaseCorrelationObject b in bcs)
            if (b != null)
            {
              GetStrikeMethod(b, ref strikeMethod, ref strikeEvaluator);
              return;
            }
        }
        throw new System.NotSupportedException("Base correlation mix object contains no base correlations");
      }
      else if (bco is BaseCorrelationTermStruct)
      {
        strikeMethod = ((BaseCorrelationTermStruct)bco).BaseCorrelations[0].StrikeMethod;
        strikeEvaluator = ((BaseCorrelationTermStruct)bco).BaseCorrelations[0].StrikeEvaluator;
      }
      else
      {
        strikeMethod = ((BaseCorrelation)bco).StrikeMethod;
        strikeEvaluator = ((BaseCorrelation)bco).StrikeEvaluator;
      }

      return;
    }

    private SyntheticCDO CreateCDO()
    {
      // Create cdo
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      SyntheticCDO cdo = new SyntheticCDO(
        start, this.Maturity,
        Currency.None, 0.0, // premium, not used
        DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB);
      return cdo;
    }
    #endregion // Backward_Compatible

    #region IAnalyticDerivativesProvider Members

    /// <summary>
    /// True if this pricer supports semi-analytic sensitivies 
    /// </summary>
    bool IAnalyticDerivativesProvider.HasAnalyticDerivatives
    {
      get
      {
        var p = this.dpBasketPricer_ as IAnalyticDerivativesProvider;
        if(p == null)
          return false;
        return p.HasAnalyticDerivatives;
      }
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

  } // class BaseCorrelationBasketPricer
}