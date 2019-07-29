/*
 * BasketPricer.cs
 *
 */
#define Include_Obsolete
//#define Handle_Defaulted_By_8_7

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Concurrency;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using PricerEvaluator = BaseEntity.Toolkit.Sensitivity.PricerEvaluator;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;
using BasketDefaultInfoBuilder = BaseEntity.Toolkit.Pricers.Baskets.BasketDefaultInfo.Builder;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  #region Config
  /// <exclude/>
  [Serializable]
  public class BasketPricerConfig
  {
    /// <exclude/>
    [ToolkitConfig("Use natural settlement in basket pricer")]
    public readonly bool UseNaturalSettlement = true;

    /// <exclude/>
    [ToolkitConfig("Whether to subtract the principals of shorted names from total principal")]
    public readonly bool SubstractShortedFromPrincipal = false;

    /// <exclude/>
    [ToolkitConfig("Whether to use curve recovery to interpolate base correlation.")]
    public readonly bool UseCurveRecoveryForBaseCorrelation = true;

    /// <exclude/>
    [ToolkitConfig("Whether to do deep cloning in parallel sensitivity.")]
    public readonly bool DeepCloningInParallelSensitivity = false;

    /// <exclude/>
    [ToolkitConfig("Whether to compute the Jump to default exactly or proximately.")]
    public readonly bool ExactJumpToDefault = false; // need to change to true in 10.1

    /// <exclude/>
    [ToolkitConfig("Whether to compute the sensitivity consistently with rescaling strikes turned off.")]
    public readonly bool ConsistentSensitivity = true;
  }
  #endregion Config

  /// <summary>
  ///   Base class for all basket pricers
  /// </summary>
  /// <remarks>
  ///   <para>This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.</para>
  ///   <para>BasketPricer classes are typically used internally by Pricer classes and are not used
  ///   directly by the user.</para>
  ///   <para>Examples of basket pricers include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="Toolkit.Pricers.BasketPricers.SemiAnalyticBasketPricer">Semi-analytic basket pricer</see></description></item>
  ///     <item><description><see cref="Toolkit.Pricers.BasketPricers.LargePoolBasketPricer">Large pool basket pricer</see></description></item>
  ///     <item><description><see cref="Toolkit.Pricers.BasketPricers.MonteCarloBasketPricer">Monte Carlo basket pricer</see></description></item>
  ///   </list>
  /// </remarks>
  [Serializable]
  public abstract class BasketPricer : BaseEntityObject
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BasketPricer));

    #region Config
    /// <summary>
    ///   Whether to support concurrent calculation of sensitivities.
    /// </summary>
    protected static readonly bool supportConcurentSensitivity_ = true;
    /// <summary>
    ///   Use 8.7 RecoverySensitivityRoutine. Hard-coded.
    ///   <preliminary />
    /// </summary>
    protected static readonly bool Use8dot7RecoverySensitivityRoutine = true;

    /// <summary>
    ///   Retrieve the current config settings
    /// </summary>
    protected ToolkitConfigSettings settings_ => ToolkitConfigurator.Settings;

    /// <summary>
    ///   Whther to use natural settlement date
    /// </summary>
    public bool UseNaturalSettlement
    {
      get { return settings_.BasketPricer.UseNaturalSettlement; }
    }

    /// <summary>
    ///   Whther to use survival curve recovery for base correlation interpolation
    /// </summary>
    public bool UseCurveRecoveryForBaseCorrelation
    {
      get { return settings_.BasketPricer.UseCurveRecoveryForBaseCorrelation; }
    }

    /// <summary>
    ///   Whther to subtract short names from total principals
    /// </summary>
    public bool SubstractShortedFromPrincipal
    {
      get { return settings_.BasketPricer.SubstractShortedFromPrincipal; }
    }

    internal bool ExactJumpToDefault
    {
      get { return settings_.BasketPricer.ExactJumpToDefault; }
    }
    #endregion // Config

    #region Constructors

    /// <summary>
    ///   constructor
    /// </summary>
    protected BasketPricer()
    {
      this.UseQuasiRng = false;
      this.GridSize = 0;
      this.addComplement_ = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BasketPricer"/> class.
    /// </summary>
    /// <param name="asOf">Pricing date.</param>
    /// <param name="settle">Settle date.</param>
    /// <param name="maturity">Maturity date.</param>
    /// <param name="basket">Basket.</param>
    /// <param name="copula">Copula.</param>
    /// <param name="correlation">Correlation.</param>
    /// <param name="stepSize">Size of the step.</param>
    /// <param name="stepUnit">Step unit.</param>
    /// <param name="lossLevels">Loss levels.</param>
    protected internal BasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      CreditPool basket,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels)
    {
      // Set the dates
      this.AsOf = asOf;
      this.PortfolioStart = Dt.Empty;
      this.Settle = settle;
      this.Maturity = maturity;

      // early maturities, refinace and counterparty infos
      this.maturities_ = null;
      this.refinanceCurves_ = null;
      this.refinanceCorrelations_ = null;
      this.counterpartyCurve_ = null;
      this.counterpartyCorrelation_ = Double.NaN;
      if (settings_.SemiAnalyticBasketPricer.UseOldLcdxTrancheModel)
        ModelType = BasketModelType.LCDOProportional;

      // Set underlying curves and participations
      addComplement_ = true;
      Set(basket, correlation, lossLevels);

      // Set copula
      this.Copula = copula;

      // Set numerical options
      this.StepSize = stepSize;
      this.StepUnit = stepUnit;
      this.UseQuasiRng = false;

      int BasketSize = this.Count;
      int defaultPoints =
          (BasketSize < 40 ? 7 + (BasketSize - 5) * 10 / 35
              : 17 + (BasketSize - 40) / 10);
      if (defaultPoints > 120) defaultPoints = 120;
      this.integrationPointsFirst_ = defaultPoints;
      this.integrationPointsSecond_ = 5;
      this.gridSize_ = 0;

      // set the default sample size
      this.sampleSize_ = 10000;

      return;
    }

    /// <summary>
    ///   constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names (or null to use survivalCurve recoveries</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="copula">Copula for the correlation structure</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    ///
    protected BasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      Copula copula,
      CorrelationObject correlation,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels)
      : this(asOf, settle, maturity, new CreditPool(principals,
        survivalCurves, recoveryCurves, null, null, false, null),
        copula, correlation, stepSize, stepUnit, lossLevels)
    {
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      BasketPricer obj = (BasketPricer)base.Clone();

      if (rawLossLevels_ != null)
        obj.rawLossLevels_ = (UniqueSequence<double>)rawLossLevels_.Clone();
      if (cookedLossLevels_ != null)
        obj.cookedLossLevels_ = (UniqueSequence<double>)cookedLossLevels_.Clone();
      obj.ResetDerivatives();
      obj.copula_ = CloneUtil.Clone(copula_);
      obj.Correlation = CloneUtil.Clone(correlation_);

      // This setup loss levels and credit portfolio data, including
      // original basket, survival curves, recovery curves, refinance
      // curves, refinance correlations, principals, as well as the basket
      // default info.
      obj.Reset(originalBasket_.DeepClone());

      obj.counterpartyCurve_ = CloneUtil.Clone(counterpartyCurve_);

      if (additionalGridDates_ != null)
        obj.additionalGridDates_ = (UniqueSequence<Dt>)additionalGridDates_.Clone();
      if (timeGrid_ != null)
        obj.timeGrid_ = (UniqueSequence<Dt>)timeGrid_.Clone();

      obj.tsCorrelation_ = tsCorrelation_ == null ? null : (CorrelationTermStruct)tsCorrelation_.Clone();

      return obj;
    }


    /// <summary>
    ///   Duplicate a basket pricer
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
    /// <returns>Duplicated basket pricer</returns>
    public virtual BasketPricer Duplicate()
    {
      BasketPricer obj = (BasketPricer)ShallowCopy();
      return obj;
    }

    /// <summary>
    ///   Create a copy of basket with different underlying curves and participations
    /// </summary>
    /// 
    /// <param name="basket">Basket to substitute with.</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    public virtual BasketPricer Substitute(
      CreditPool basket,
      CorrelationObject correlation,
      Array lossLevels)
    {
      BasketPricer obj = (BasketPricer)ShallowCopy();
      obj.Set(basket, correlation, lossLevels);
      return obj;
    }

    /// <summary>
    ///   Create a copy of basket with different underlying curves and participations
    /// </summary>
    /// 
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names (or null to use survivalCurve recoveries</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
    public BasketPricer Substitute(
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      CorrelationObject correlation,
      Array lossLevels)
    {
      BasketPricer obj = Substitute(new CreditPool(principals,
        survivalCurves, recoveryCurves, null, null,
        this.refinanceCurves_ != null, null),
        correlation, lossLevels);
      return obj;
    }

    #endregion Constructors

    #region Methods

    #region Validate

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Invalid AsOf date
      if (!asOf_.IsEmpty() && !asOf_.IsValid())
        InvalidValue.AddError(errors, this, "AsOf", String.Format("Invalid AsOf. Must be empty or valid date, not {0}", asOf_));

      // Invalid Settle date
      if (!settle_.IsEmpty() && !settle_.IsValid())
        InvalidValue.AddError(errors, this, "Settle", String.Format("Invalid Settle. Must be empty or valid date, not {0}", settle_));

      // Invalid Maturity date
      if (!maturity_.IsEmpty() && !maturity_.IsValid())
        InvalidValue.AddError(errors, this, "AsOf", String.Format("Invalid Maturity. Must be empty or valid date, not {0}", maturity_));

      // Invalid correlation
      if (correlation_ == null)
        InvalidValue.AddError(errors, this, "Correlation", String.Format("Correlation cannot be null"));

      // Invalid copula
      if (copula_ == null)
        InvalidValue.AddError(errors, this, "Copula", String.Format("Copula cannot be null"));

      // Invalid Survival Curves
      if (survivalCurves_ == null)
        InvalidValue.AddError(errors, this, "SurvivalCurves", String.Format("Survival Curves cannot be null"));

      // Invalid Survival Curves
      if (recoveryCurves_ == null)
        InvalidValue.AddError(errors, this, "RecoveryCurves", String.Format("Survival Curves cannot be null"));

      // Invalid Principals
      if (principals_ == null)
        InvalidValue.AddError(errors, this, "Principals", String.Format("Principals cannot be null"));

      // Invalid StepSize
      if (stepSize_ <= 0.0)
        InvalidValue.AddError(errors, this, "StepSize", String.Format("Invalid stepsize. Must be +Ve, Not {0}", stepSize_));

      // Invalid Integration Points Second
      if (effectiveDigits_ < 0.0)
        InvalidValue.AddError(errors, this, "EffectiveDigits", String.Format("Effective Digits is not positive"));

      // Invalid Sample Size
      if (sampleSize_ < 0.0)
        InvalidValue.AddError(errors, this, "SampleSize", String.Format("Sample Size is not positive"));

      // Invalid Grid Size
      if (gridSize_ < 0.0)
        InvalidValue.AddError(errors, this, "GridSize", String.Format("Grid size cannot be negative ({0})", gridSize_));
      if (gridSize_ > 0.5)
        InvalidValue.AddError(errors, this, "GridSize", String.Format("Grid size must less than 0.5, not {0}", gridSize_));

      // Invalid Integration Points First
      if (integrationPointsFirst_ <= 0.0)
        InvalidValue.AddError(errors, this, "IntegrationPointsFirst", String.Format("IntegrationPointsFirst is not positive"));

      // Invalid Integration Points Second
      if (integrationPointsSecond_ <= 0.0)
        InvalidValue.AddError(errors, this, "IntegrationPointsSecond", String.Format("IntegrationPointsSecond is not positive"));

      // Invalid Correlation
      if (this.Correlation is ExternalFactorCorrelation)
      {
#if DEBUG
        if (copula_.CopulaType == CopulaType.Gauss)
          copula_.CopulaType = CopulaType.ExternalGauss;
#endif
        InvalidValue.AddError(errors, this, "CopulaType", String.Format("CopulaType cannot be ExternalFactorCorrelation"));
      }

      return;
    }

    #endregion

    #region Distribution_Calculation


    ///
    /// <summary>
    ///   Compute the accumlated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    public abstract double AccumulatedLoss(Dt date, double trancheBegin, double trancheEnd);

    ///
    /// <summary>
    ///   Compute the amortized amount on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    public abstract double AmortizedAmount(Dt date, double trancheBegin, double trancheEnd);



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
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the raw survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the raw survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param> 
    public virtual void AccumulatedLossDerivatives(Dt date, double trancheBegin, double trancheEnd, double[] retVal)
    {
      throw new ToolkitException("This pricer does not support semi-analytic sensitivity computations.");
    }

    ///
    /// <summary>
    ///   Compute the derivatives of the amortized amount on a dollar on the index
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    ///<param name="retVal">retVal is an array of size N *(K+K*(K+1)/2 +2), where K is the number of tenors of each survival curve, 
    /// and N is the size of the basket. Let L = K+K*(K+1)/2 +2
    /// retVal[i*L + 0..i*L + K-1] is the gradient w.r.t the raw survival curve ordinates of the ith name,
    /// retVal[i*L +K..i*L +K + K*(K+1)/2-1] is the hessian w.r.t the raw survival curve ordinates of the ith name, 
    /// retVal[i*L +K + K*(K+1)/2] is the value of default of the ith name
    /// retVal[i*L +K + K*(K+1)/2+1] is the derivative with respect to the ith obligor's mean recovery rate
    /// </param> 
    public virtual void AmortizedAmountDerivatives(Dt date, double trancheBegin, double trancheEnd, double[] retVal)
    {
      throw new ToolkitException("This pricer does not support semi-analytic sensitivity computations.");

    }
    /// <summary>
    ///   Reset the BasketPricer
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Reset any internal calculated values.</para>
    /// </remarks>
    ///
    public virtual void Reset()
    {
      if (this.Correlation.Modified)
      {
        ResetCorrelation();
      }
      recomputeDerivatives_ = true;
    }

    /// <summary>
    ///   Experimental reset function
    ///   <preliminary/>
    /// </summary>
    /// <param name="what">Pricer attributes changed</param>
    /// <exclude/>
    public virtual void Reset(SyntheticCDOPricer.ResetFlag what)
    {
      if ((what & SyntheticCDOPricer.ResetFlag.Correlation) == SyntheticCDOPricer.ResetFlag.Correlation)
        ResetCorrelation();
      if ((what & SyntheticCDOPricer.ResetFlag.Recovery) == SyntheticCDOPricer.ResetFlag.Recovery)
        ResetRecoveryRates();
      // always reset distribution
      ResetDistribution();
    }


    /// <summary>
    ///   Reset distribution
    /// </summary>
    protected internal void ResetDistribution()
    {
      // TODO: Once Reset() become reset all, this should be modified
      Reset();
    }

    /// <summary>
    ///   Set a flag to re-calculate correlation term strcuture
    /// </summary>
    /// <exclude />
    protected internal void ResetCorrelation()
    {
      tsCorrelation_ = null;
      correlation_.SetReadyState(false);
    }

    /// <summary>
    ///   Reset recovery rates and recovery dispersion to null
    /// </summary>
    /// <exclude />
    protected internal void ResetRecoveryRates()
    {
      recoveryRates_ = null;
      recoveryDispersions_ = null;
    }

    /// <summary>
    ///   Set recovery curves and recovery rates to null
    /// </summary>
    /// <exclude />
    protected internal void ResetRecoveryCurves()
    {
      recoveryCurves_ = null;
      recoveryRates_ = null;
    }

    /// <summary>
    ///   Reset the original basket.
    /// </summary>
    /// <exclude />
    protected internal virtual void Reset(CreditPool originalBasket)
    {
      Set(originalBasket, null, null);
    }

    /// <summary>
    /// Reset derivatives computations 
    /// </summary>
    protected internal void ResetDerivatives()
    {
      rawDetachments_ = null;
      trancheLossDer_ = null;
      trancheAmortDer_ = null;
      recomputeDerivatives_ = true;
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
    public abstract double[,] CalcLossDistribution(bool wantProbability, Dt date, double[] lossLevels);

    /// <summary>
    ///   Set the re-calculation start date
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>The derived class can override this function to set an internal state
    ///   indicating that only the the loss distributions after the
    ///   <paramref name="date">re-calculation start date</paramref> need to recalculate.
    ///   Therefore, the loss distributions for the 
    ///   for the dates before the start date will not be reset.
    ///   This behaviour persists until another call to this function
    ///   with a different date or an empty date, in the later case the
    ///   recalculation always begins with the protection start date. </para>
    /// 
    ///  <para>The default implementation of this function does nothing.</para>
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
    protected internal virtual void SetRecalculationStartDate(Dt date, bool keepPrevResult)
    { }

    /// <summary>
    ///   Calculation of the price values for a series of Synthetic CDO tranches,
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
    /// 
    ///   <para>Note: The default implementation in the <see ref="BasketPricer"/> is general
    ///   purpose routine.  The derived class can implement a more efficient version when
    ///   it sees fit.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
    protected internal virtual double[,] BumpedPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity)
    {
      return GenericBumpedPvs(pricers, altSurvivalCurves, includeRecoverySensitivity);
    }

    /// <summary>
    ///   Calculation of the MTM values for a series of Synthetic CDO tranches,
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
    /// 
    ///   <para>Note: The default implementation in the <see ref="BasketPricer"/> is general
    ///   purpose routine.  The derived class can implement a more efficient version when
    ///   it sees fit.</para>
    /// </remarks>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column indentifies a CDO tranche, while row 0 contains the base values
    ///    and row i (i &gt; 0) contains the values when the curve i is replaced
    ///    by its alternative
    /// </returns>
    ///
    public virtual double[,] BumpedPvs(
      SyntheticCDOPricer[] pricers, SurvivalCurve[] altSurvivalCurves)
    {
      return GenericBumpedPvs(pricers, altSurvivalCurves);
    }



    /// <summary>
    ///   Generic calculation of the MTM values for a series of Synthetic CDO tranches,
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
    ///
    protected internal double[,] GenericBumpedPvs(
      SyntheticCDOPricer[] pricers, SurvivalCurve[] altSurvivalCurves)
    {

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

      logger.Debug("Computing spread sensitivity deltas for basket");

      SurvivalCurve[] survivalCurves = SurvivalCurves;

      // now create and fill the table of values
      double[,] table = new double[basketSize + 1, pricers.Length];

      // compute the base case
      Reset();
      for (int j = 0; j < pricers.Length; ++j)
        table[0, j] = pricers[j].FullPrice();

      for (int i = 0; i < basketSize; i++)
      {
        if (survivalCurves[i] == altSurvivalCurves[i])
        {
          // Don't bother recalculating if the curves are unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[i + 1, j] = table[0, j];
        }
        else
        {
          // we want the results with the ith curve bumped
          SurvivalCurve savedSurvivalCurve = survivalCurves[i];
          survivalCurves[i] = altSurvivalCurves[i];

          try
          {
            // need to recompute the distribution
            Reset();

            // compute the prices
            for (int j = 0; j < pricers.Length; ++j)
              table[i + 1, j] = pricers[j].FullPrice();
          }
          finally
          {
            // restore the old survival curve
            survivalCurves[i] = savedSurvivalCurve;
          }
        }
      }

      Reset();

      // done
      return table;
    }

    /// <summary>
    ///   Bumped pv input pricers and curves.
    /// </summary>
    private class BumpedPvParams
    {
      public PricerEvaluator[] Evaluators;
      public SurvivalCurve[] AltSurvivalCurves;
      public RecoveryCurve[] AltRecoveryCurves;
    }

    /// <summary>
    ///  Clones all the input pricers and curves.
    /// </summary>
    /// <remarks>
    ///   This method preseves the references and works with any object graphs.
    ///   It does not depend on any particular object structure, only requiring that
    ///   they are serializable.
    /// </remarks>
    /// <param name="evaluators">The pricer evaluators.</param>
    /// <param name="altSurvivalCurves">The alt survival curves.</param>
    /// <param name="includeRecoverySensitivity">if set to <c>true</c>, include recovery sensitivity.</param>
    /// <returns>An independent copy of input pricers and curves.</returns>
    private BumpedPvParams CloneBumpedPvParams(
      PricerEvaluator[] evaluators,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity)
    {
      if(settings_.BasketPricer.DeepCloningInParallelSensitivity)
      {
        var cloned = CloneUtil.CloneObjectGraph(
          Array.ConvertAll(evaluators, (e) => (SyntheticCDOPricer)e.Pricer),
          altSurvivalCurves);
        var pricers = cloned.Item1;
        var altSc = cloned.Item2;
        var altRc = hasFixedRecovery_ || !includeRecoverySensitivity
          ? pricers[0].Basket.RecoveryCurves : GetRecoveryCurves(altSc);
        var par = new BumpedPvParams
        {
          Evaluators = ArrayUtil.Generate(pricers.Length,
            (i) => evaluators[i].Substitute(pricers[i])),
          AltSurvivalCurves = altSc,
          AltRecoveryCurves = altRc
        };
        return par;
      }
      else
      {
        BasketPricer basket = this.Duplicate();
        basket.SurvivalCurves = (SurvivalCurve[])SurvivalCurves.Clone();
        basket.RecoveryCurves = (RecoveryCurve[])RecoveryCurves.Clone();
        RecoveryCurve[] altRecoveryCurves =
          hasFixedRecovery_ || !includeRecoverySensitivity ?
          basket.RecoveryCurves : GetRecoveryCurves(altSurvivalCurves);

        PricerEvaluator[] evals = new PricerEvaluator[evaluators.Length];
        for (int i = 0; i < evaluators.Length; ++i)
        {
          SyntheticCDOPricer p = (SyntheticCDOPricer)evaluators[i].Pricer;
          p = p.Substitute(p.CDO, basket, 0, false); p.Basket.Reset();
          evals[i] = evaluators[i].Substitute(p);
        }
        return new BumpedPvParams
        {
          Evaluators = evals,
          AltSurvivalCurves = altSurvivalCurves,
          AltRecoveryCurves = altRecoveryCurves
        };
      }
    }

    /// <summary>
    ///   Parallel-safe evaluation of the bumped pv for a curve.
    /// </summary>
    /// <param name="i">The index of the bumped curve.</param>
    /// <param name="par">The parameter object.</param>
    /// <param name="table">The result table.</param>
    private static void ParallelEvaluateBumpedPv(
      int i, BumpedPvParams par, double[,] table)
    {
      PricerEvaluator[] evals = par.Evaluators;
      BasketPricer basket = evals[0].Basket;
      SurvivalCurve[] sc = basket.SurvivalCurves;
      RecoveryCurve[] rc = basket.RecoveryCurves;
      SurvivalCurve[] altSc = par.AltSurvivalCurves;
      RecoveryCurve[] altRc = par.AltRecoveryCurves;
      if (sc[i] == altSc[i] && rc[i] == altRc[i])
      {
        // Don't bother recalculating if the curves are unchanged.
        for (int j = 0; j < evals.Length; ++j)
          table[i + 1, j] = table[0, j];
        return;
      }

      // we want the results with the ith curve bumped
      SurvivalCurve savedSurvivalCurve = sc[i];
      sc[i] = altSc[i];
      RecoveryCurve savedRecoveryCurve = null;
      double savedRate = 0;
      if (rc[i] != altRc[i])
      {
        double[] rates = basket.RecoveryRates;
        savedRate = rates[i];
        savedRecoveryCurve = rc[i];
        rc[i] = altRc[i];
        rates[i] = rc[i].Interpolate(basket.Maturity);
      }

      try
      {
        // need to recompute the distribution
        basket.Reset();

        // compute the prices
        for (int j = 0; j < evals.Length; ++j)
        {
          table[i + 1, j] = evals[j].Evaluate();
        }
      }
      finally
      {
        // restore the old survival curve
        sc[i] = savedSurvivalCurve;
        if (savedRecoveryCurve != null)
        {
          rc[i] = savedRecoveryCurve;
          basket.RecoveryRates[i] = savedRate;
        }
      }
      return;
    }


    /// <summary>
    ///   Generic calculation of the price values for a series of Synthetic CDO tranches,
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
    protected internal double[,] GenericBumpedPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity)
    {
      if (NeedExactJtD(pricers))
      {
        return GenericDefaultPvs(pricers, altSurvivalCurves,
          includeRecoverySensitivity);
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

      logger.Debug("Computing spread sensitivity deltas for basket");

      Dt maturity = Maturity;
      RecoveryCurve[] recoveryCurves = RecoveryCurves;
      double[] recoveryRates = RecoveryRates;
      SurvivalCurve[] survivalCurves = SurvivalCurves;
      double[,] table = new double[bumpCount + 1, pricers.Length];

      // compute the base case
      Reset();
      for (int j = 0; j < pricers.Length; ++j)
        table[0, j] = pricers[j].Evaluate();

      // Compute the sensitivity from the unsettled defaults
      if (bumpCount > basketSize)
      {
        CalculateDefaultPvTable(table, pricers, altSurvivalCurves);
        altSurvivalCurves = altSurvivalCurves.Take(basketSize).ToArray();
      }

      if (basketSize > 4 && supportConcurentSensitivity_)
      {
        Parallel.For(0, basketSize,
          () => CloneBumpedPvParams(pricers, altSurvivalCurves, includeRecoverySensitivity),
          (i, par) => ParallelEvaluateBumpedPv(i, par, table));
        // done
        return table;
      }

      // The old non-parallel algorithm
      RecoveryCurve[] altRecoveryCurves =
        hasFixedRecovery_ || !includeRecoverySensitivity ?
        recoveryCurves : GetRecoveryCurves(altSurvivalCurves);

      // now create and fill the table of values
      for (int i = 0; i < basketSize; i++)
      {
        if (survivalCurves[i] == altSurvivalCurves[i]
          && recoveryCurves[i] == altRecoveryCurves[i])
        {
          // Don't bother recalculating if the curves are unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[i + 1, j] = table[0, j];
        }
        else
        {
          // we want the results with the ith curve bumped
          SurvivalCurve savedSurvivalCurve = survivalCurves[i];
          survivalCurves[i] = altSurvivalCurves[i];
          RecoveryCurve savedRecoveryCurve = null;
              if (recoveryCurves[i] != altRecoveryCurves[i])
              {
                savedRecoveryCurve = recoveryCurves[i];
                recoveryCurves[i] = altRecoveryCurves[i];
                recoveryRates[i] = altRecoveryCurves[i].Interpolate(maturity);
              }

          try
          {
            // need to recompute the distribution
              Reset();

            // compute the prices
            for (int j = 0; j < pricers.Length; ++j)
              table[i + 1, j] = pricers[j].Evaluate();
          }
          finally
          {
            // restore the old survival curve
              survivalCurves[i] = savedSurvivalCurve;
              if (savedRecoveryCurve != null)
              {
                recoveryCurves[i] = savedRecoveryCurve;
                recoveryRates[i] = savedRecoveryCurve.Interpolate(maturity);
              }
            }
          }
        }

      Reset();

      // done
      return table;
    }

    /// <summary>
    /// Computes the PVs of the unsettled defaults with alternative recoveries.
    /// </summary>
    /// <param name="pricers">The pricers</param>
    /// <param name="bumpedSurvivalCurves">The bumped survival curves</param>
    /// <param name="table">The result table</param>
    /// <exception cref="ToolkitException"></exception>
    internal void CalculateDefaultPvTable(
      double[,] table,
      PricerEvaluator[] pricers,
      SurvivalCurve[] bumpedSurvivalCurves)
    {
      var indices = OriginalBasket.SurvivalCurves
        .Select((sc, i) => sc.HasUnsettledRecovery() ? i : -1)
        .Where(i => i >= 0).ToArray();
      int start = Count, bumpCount = bumpedSurvivalCurves.Length;
      if (start + indices.Length != bumpCount)
      {
        throw new ToolkitException($"Unsettled recovery count must be {indices.Length}, not {bumpCount - start}");
      }

      SurvivalCurve originalSurvival = null;
      RecoveryCurve originalRecovery = null;
      int idx = -1;
      try
      {
        for (int i = start; i < bumpCount; ++i)
        {
          idx = indices[i - start];
          originalSurvival = OriginalBasket.SurvivalCurves[idx];
          originalRecovery = OriginalBasket.RecoveryCurves?[idx];

          // The bumped curves
          var sc = bumpedSurvivalCurves[i];
          var rc = sc.SurvivalCalibrator.RecoveryCurve;

          // Don't bother recalculating if the curves are unchanged.
          if (originalSurvival == sc && (
            originalRecovery == null || originalRecovery == rc))
          {
            for (int j = 0; j < pricers.Length; ++j)
              table[i + 1, j] = table[0, j];
            continue;
          }

          // Use the bumped curves for calculation.
          SetCurves(idx, sc, rc);

          // Reset the basket and pricers
          ResetDefaults(pricers);

          // Now calculate the PVs.
          for (int j = 0; j < pricers.Length; ++j)
            table[i + 1, j] = pricers[j].Evaluate();

          // Restore the original curves.
          SetCurves(idx, originalSurvival, originalRecovery);
        }
      }
      finally
      {
        if (originalSurvival != null)
        {
          // Make sure we restore everything before return
          SetCurves(idx, originalSurvival, originalRecovery);
          ResetDefaults(pricers);
        }
      }
    }

    private void SetCurves(int idx, SurvivalCurve sc, RecoveryCurve rc)
    {
      var basket = OriginalBasket;
      basket.SurvivalCurves[idx] = sc;
      if (rc != null && basket.RecoveryCurves != null)
        basket.RecoveryCurves[idx] = rc;
    }

    private void ResetDefaults(PricerEvaluator[] pricers)
    {
      // Reset basket to update the default settlement.
      // Also reset loss levels to adapt to the new values
      // of the effective attachment/detachment.
      Set(OriginalBasket, null, RawLossLevels);

      // Reset other intermediate results
      Reset();

      // Reset the CDO pricers to pick up the changes
      for (int j = 0; j < pricers.Length; ++j)
      {
        (pricers[j].Pricer as SyntheticCDOPricer)?.UpdateEffectiveNotional();
      }
    }

    internal bool NeedExactJtD(PricerEvaluator[] pricers)
    {
      if (!ExactJumpToDefault) return false;
      if (pricers != null)
      {
        for (int i = 0; i < pricers.Length; ++i)
          if (pricers[i].DefaultChanged) return true;
      }
      return false;
    }

    /// <summary>
    ///   Default pv input pricers and curves.
    /// </summary>
    private class DefaultPvParams
    {
      public PricerEvaluator[] Evaluators;
      public SurvivalCurve[] SurvivalCurves;
      public RecoveryCurve[] RecoveryCurves;
      public int[] CurveSlots;
      public SurvivalCurve[] AltSurvivalCurves;
      public RecoveryCurve[] AltRecoveryCurves;
    }

    private DefaultPvParams CloneDefaultPvParams(
      PricerEvaluator[] evaluators,
      int[] curveSlots,
      SurvivalCurve[] altSurvivalCurves,
      bool includeRecoverySensitivity)
    {
      var cloned = CloneUtil.CloneObjectGraph(
        Array.ConvertAll(evaluators, (e) => (SyntheticCDOPricer) e.Pricer),
        altSurvivalCurves);
      var pricers = cloned.Item1;
      var basket = pricers[0].Basket;
      var survivalCurves = basket.OriginalBasket.SurvivalCurves;
      var recoveryCurves = basket.OriginalBasket.RecoveryCurves
        ?? GetRecoveryCurves(survivalCurves);

      var altSc = cloned.Item2;
      var altRc = hasFixedRecovery_ || !includeRecoverySensitivity
        ? recoveryCurves : GetRecoveryCurves(altSc);
      var par = new DefaultPvParams
      {
        Evaluators = ArrayUtil.Generate(pricers.Length,
          (i) => evaluators[i].Substitute(pricers[i])),
        SurvivalCurves = survivalCurves,
        RecoveryCurves = recoveryCurves,
        CurveSlots = curveSlots,
        AltSurvivalCurves = altSc,
        AltRecoveryCurves = altRc
      };
      return par;
    }

    private static void ParallelEvaluateDefaultPv(
      int row, DefaultPvParams par, double[,] table)
    {
      int i = par.CurveSlots[row] - 1;
      Debug.Assert(i >= 0);
      var evals = par.Evaluators;
      var basket = evals[0].Basket;
      SurvivalCurve[] sc = par.SurvivalCurves;
      RecoveryCurve[] rc = par.RecoveryCurves;
      SurvivalCurve[] altSc = par.AltSurvivalCurves;
      RecoveryCurve[] altRc = par.AltRecoveryCurves;
      if (sc[i] == altSc[i] && rc[i] == altRc[i])
      {
        // Don't bother recalculating if the curves are unchanged.
        for (int j = 0; j < evals.Length; ++j)
          table[row + 1, j] = table[0, j];
        return;
      }

      // we want the results with the ith curve bumped
      SurvivalCurve savedSurvivalCurve = sc[i];
      sc[i] = altSc[i];
      if(altSc[i].Defaulted==Defaulted.WillDefault
        && altSc[i].DefaultDate.IsEmpty())
      {
        altSc[i].DefaultDate = basket.Settle;
        altSc[i].Defaulted = Defaulted.WillDefault;
      }
      RecoveryCurve savedRecoveryCurve = rc[i];
      rc[i] = altRc[i];

      try
      {
        // need to recompute the distribution
        for (int j = 0; j < evals.Length; ++j)
          evals[j].Reset();

        // compute the prices
        for (int j = 0; j < evals.Length; ++j)
        {
          table[row + 1, j] = evals[j].Evaluate();
        }
      }
      finally
      {
        // restore the old survival curve
        sc[i] = savedSurvivalCurve;
        rc[i] = savedRecoveryCurve;
      }
      return;
    }

    /// <summary>
    /// Default PVs
    /// </summary>
    /// <param name="pricers"></param>
    /// <param name="defaultSurvivalCurves"></param>
    /// <param name="includeRecoverySensitivity"></param>
    /// <returns></returns>
    protected internal double[,] GenericDefaultPvs(
      PricerEvaluator[] pricers,
      SurvivalCurve[] defaultSurvivalCurves,
      bool includeRecoverySensitivity)
    {

      // Sanity check
      int basketSize = Count, bumpCount = defaultSurvivalCurves.Length;
      if (bumpCount != basketSize && bumpCount != this.GetSurvivalBumpCount(includeRecoverySensitivity))
        throw new System.ArgumentException(String.Format(
          "Invalid number of survival curves. Must be {0}, not {1}",
          basketSize, defaultSurvivalCurves.Length));
      for (int j = 0; j < pricers.Length; ++j)
        if (pricers[j].Basket != this)
          throw new System.ArgumentException(String.Format(
            "Pricer #{0} is not using this basket pricer!", j));

      logger.Debug("Computing spread sensitivity deltas for basket");

      var table = new double[bumpCount + 1, pricers.Length];

      // compute the base case
      Reset();
      for (int j = 0; j < pricers.Length; ++j)
        table[0, j] = pricers[j].Evaluate();

      // Compute the possible recovery sensitivity from the unsettled defaults
      if (bumpCount > basketSize)
      {
        CalculateDefaultPvTable(table, pricers, defaultSurvivalCurves);
        defaultSurvivalCurves = defaultSurvivalCurves.Take(basketSize).ToArray();
      }

      // Compute the sensitivity for the remaining entities
      var basket = pricers[0].Basket;
      var curveSlots = new int[bumpCount];
      var altSurvivalCurves = SurvivalDeltaCalculator.FindMatchedCurves(
        basket.OriginalBasket.SurvivalCurves,
        basket.SurvivalCurves,
        defaultSurvivalCurves, curveSlots);

      if (basketSize > 4 && supportConcurentSensitivity_)
      {
        Parallel.For(0, basketSize,
          () => CloneDefaultPvParams(pricers, curveSlots,
            altSurvivalCurves, includeRecoverySensitivity),
          (i, par) => ParallelEvaluateDefaultPv(i, par, table));
        // done
        return table;
      }

      // The old non-parallel algorithm
      var survivalCurves = basket.OriginalBasket.SurvivalCurves;
      var recoveryCurves = basket.OriginalBasket.RecoveryCurves
        ?? GetRecoveryCurves(survivalCurves);
      var altRecoveryCurves = hasFixedRecovery_ || !includeRecoverySensitivity
        ? recoveryCurves : GetRecoveryCurves(altSurvivalCurves);

      // now create and fill the table of values
      for (int row = 0; row < basketSize; row++)
      {
        int i = curveSlots[row] - 1;
        if (survivalCurves[i] == altSurvivalCurves[i]
          && recoveryCurves[i] == altRecoveryCurves[i])
        {
          // Don't bother recalculating if the curves are unchanged.
          for (int j = 0; j < pricers.Length; ++j)
            table[row + 1, j] = table[0, j];
        }
        else
        {
          // we want the results with the ith curve bumped
          SurvivalCurve savedSurvivalCurve = survivalCurves[i];
          survivalCurves[i] = altSurvivalCurves[i];
          RecoveryCurve savedRecoveryCurve = recoveryCurves[i];
          recoveryCurves[i] = altRecoveryCurves[i];

          try
          {
            // need to recompute the distribution
            for (int j = 0; j < pricers.Length; ++j)
              pricers[j].Reset();

            // compute the prices
            for (int j = 0; j < pricers.Length; ++j)
              table[row + 1, j] = pricers[j].Evaluate();
          }
          finally
          {
            // restore the old survival curve
            survivalCurves[i] = savedSurvivalCurve;
            recoveryCurves[i] = savedRecoveryCurve;
          }
        }
      }

      Reset(null);
      Reset();

      // done
      return table;
    }

    /// <summary>
    /// Computes the semi-analytic greeks of the tranche loss and amortization for given detachments 
    /// </summary>
    /// <param name="rawDetachments">Array of raw tranche attachment-detachment points</param>
    internal virtual void ComputeAndSaveSemiAnalyticSensitivities(UniqueSequence<double> rawDetachments)
    {
      throw new NotImplementedException("SemiAnalytic greeks are not supported by this pricer");
    }

    /// <summary>
    /// Initialize the CurveArrays that hold single name derivatives information 
    /// </summary>
    /// <param name="dates">dates on which to compute the loss</param>
    /// <param name="lossDerivatives">CurveArray of single name loss derivatives</param>
    /// <param name="amorDerivatives">CurveArray of single name amortization derivatives</param>
    /// <param name="noAmort">True if basket does not amortizes</param>
    private static void InitializeSemiAnalyticSensitivities(IList<Dt> dates, CurveArray lossDerivatives, CurveArray amorDerivatives, bool noAmort)
    {
      lossDerivatives.Initialize(dates.Count);
      for (int i = 0; i < dates.Count; ++i)
        lossDerivatives.SetDate(i, dates[i]);
      if (noAmort)
        return;
      amorDerivatives.Initialize(dates.Count);
      for (int i = 0; i < dates.Count; ++i)
        amorDerivatives.SetDate(i, dates[i]);
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
      if (maturities == null)
        return new double[0];
      var result = new double[maturities.Length];
      for (int i = 0; i < maturities.Length; ++i)
        if (!maturities[i].IsEmpty())
          result[i] = maturities[i].ToDouble();
      return result;
    }


    /// <summary>
    /// Denote by <m>\tau</m> default time and by <m>\eta</m> refinancing times. 
    /// This function transforms survival probability curves <m>P(\tau > t)</m>
    /// and refinancing probability curves <m>P(\eta > t)</m> into <m>P(\tau > t, \tau \leq \eta)</m>
    /// and <m>P(\eta > t, \eta \leq tau)</m>
    /// </summary>
    /// <param name="start">Start date</param>
    /// <param name="stop">End date</param>
    /// <param name="rawSurvCurves">Un-adjusted survival curves</param>
    /// <param name="rawRefinCurves">Un-adjusted refinancing curves</param>
    /// <param name="adjSurvCurves">Overwritten by the adjusted survival curves</param>
    /// <param name="adjRefinCurves">Overwritten by the adjusted refinancing curves</param>
    /// <param name="correlations">Defaul-refinancing correlation</param>
    /// <param name="stepSize">Step size for transformation</param>
    /// <param name="stepUnit">Step time unit for transformation</param>
    protected void TransformRefinanceCurves(Dt start, Dt stop, Curve[] rawSurvCurves, Curve[] rawRefinCurves, out Curve[] adjSurvCurves, out Curve[] adjRefinCurves,
        double[] correlations, int stepSize, TimeUnit stepUnit)
    {
      int N = rawSurvCurves.Length;
      if (rawRefinCurves == null || rawRefinCurves.Length == 0)
      {
        adjSurvCurves = rawSurvCurves;
        adjRefinCurves = new Curve[N];
        return;
      }
      if (N != rawRefinCurves.Length)
        throw new ArgumentException(String.Format(
          "Lengths of SurvivalCurves ({0}) and RefinanceCurves ({1}) not match",
          rawSurvCurves.Length, rawRefinCurves.Length));
      if (N != correlations.Length)
        throw new ArgumentException(String.Format(
          "Lengths of RefinanceCurves ({0}) and RefinanceCorrelations ({1}) not match",
          N, correlations.Length));
      adjSurvCurves = new Curve[N];
      adjRefinCurves = new Curve[N];
      for (int i = 0; i < N; ++i)
      {
        if (rawRefinCurves[i] == null || rawRefinCurves[i].Count == 0)
        {
          adjSurvCurves[i] = rawSurvCurves[i];
          adjRefinCurves[i] = null;
        }
        else
        {
          var cCurve = new AdjustedSurvivalCurve(start, DayCount.Actual365Fixed, Frequency.Continuous);
          var rCurve = new AdjustedSurvivalCurve(start, DayCount.Actual365Fixed, Frequency.Continuous);
          CounterpartyAdjusted.MakeCurves(start, stop, rawSurvCurves[i], rawRefinCurves[i], correlations[i], cCurve, rCurve, stepSize, stepUnit);
          adjSurvCurves[i] = new Curve(cCurve);
          adjRefinCurves[i] = new Curve(rCurve);
        }

      }
      return;
    }

    

    /// <summary>
    ///For BasketPricer objects that implement IAnalyticDerivativesProvider interface
    ///<preliminary/>
    /// </summary>
    ///<param name="survivalCurves">Array of survival curves</param>
    /// <param name="refinancingCurves">Array of refinancing curves</param>
    ///<param name="crModel">Integer representing model choice</param>
    ///<param name="rawDetachments">Detachment of the loss level for which to compute tranche loss derivatives</param>
    ///<param name="correlations">Array of correlation term structure per detachment point</param>
    /// <remarks>
    /// Compute the semi-analytic derivative of expected tranche loss(tranche amortization) at TimeGrid,  w.r.t individual name curve ordinates 
    /// and stores them for later use.
    /// </remarks>
    internal void ComputeAndSaveSemiAnalyticSensitivities(
      Curve[] survivalCurves, Curve[] refinancingCurves,
      SemiAnalyticBasketModel.RecoveryCorrelationModel crModel,
      UniqueSequence<double> rawDetachments, CorrelationTermStruct[] correlations)
    {
      var model = crModel.ModelChoice;
      var recoveryRates = RecoveryRates;
      var recoveryDispersions = RecoveryDispersions;
      if (rawDetachments_ == null)
      {
        rawDetachments_ = new UniqueSequence<double>();
        trancheLossDer_ = new UniqueSequence<CurveArray>();
        trancheAmortDer_ = new UniqueSequence<CurveArray>();
      }
      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      bool multiThreaded = ParallelSupport.Enabled;
      for (int i = 0; i < rawDetachments.Count; ++i)
      {
        double detach = rawDetachments[i];
        if (RawDetachments.Contains(detach))
          continue;
        double ld = AdjustTrancheLevel(false, detach);
        double ad = AdjustTrancheLevel(true, 1.0 - detach);
        var trancheLossDer = new CurveArray(start);
        var trancheAmortDer = new CurveArray(start);
        InitializeSemiAnalyticSensitivities(TimeGrid, trancheLossDer, trancheAmortDer, NoAmortization);
        SemiAnalyticBasketModelGreeks.Compute(ld, ad, 0, trancheLossDer.NumDates(), CopulaType,
                                              DfCommon, DfIdiosyncratic, Copula.Data,
                                              correlations[i].Correlations,
                                              correlations[i].GetDatesAsInt(UseNaturalSettlement ? start : AsOf),
                                              IntegrationPointsFirst,
                                              TransformMaturities(EarlyMaturities),
                                              survivalCurves, Principals,
                                              recoveryRates, recoveryDispersions, refinancingCurves,
                                              model, multiThreaded, trancheLossDer, trancheAmortDer);
        trancheLossDer.Detach = trancheAmortDer.Detach = detach;
        rawDetachments_.Add(detach);
        trancheLossDer_.Add(trancheLossDer);
        trancheAmortDer_.Add(trancheAmortDer);
      }
    }


    /// <summary>
    ///   Compute the expected losses occurred the whole basket for a period
    /// </summary>
    ///
    /// <param name="start">Period start date</param>
    /// <param name="end">Period end date</param>
    public virtual double
    BasketLoss(Dt start, Dt end)
    {
      SurvivalCurve[] sc = this.SurvivalCurves;
      double[] prin = this.Principals;

      // sanity check
      if (prin.Length < sc.Length)
        throw new ToolkitException(String.Format("Length of principals {0} less than basket size {1}", prin.Length, sc.Length));

      // in case this is a pool like CDO squared
      int numSubBasket = prin.Length / sc.Length;
      if (numSubBasket > 1)
      {
        int basketSize = sc.Length;
        prin = new double[basketSize];
        for (int i = 0; i < basketSize; ++i)
        {
          // notional
          double n = 0;
          for (int j = 0, idx = i; j < numSubBasket; idx += basketSize, ++j)
            n += this.Principals[idx];
          prin[i] = n;
        }
      }

      double result = BasketLoss(start, end, prin, sc, this.RecoveryRates);
      return result / TotalPrincipal;
    }

    /// <summary>
    ///   Compute the expected amortization occurred the whole basket for a period
    /// </summary>
    ///
    /// <param name="start">Period start date</param>
    /// <param name="end">Period end date</param>
    public virtual double
    BasketAmortize(Dt start, Dt end)
    {
      SurvivalCurve[] sc = this.SurvivalCurves;
      double[] prin = this.Principals;

      // sanity check
      if (prin.Length < sc.Length)
        throw new ToolkitException(String.Format("Length of principals {0} less than basket size {1}", prin.Length, sc.Length));

      // in case this is a pool like CDO squared
      int numSubBasket = prin.Length / sc.Length;
      if (numSubBasket > 1)
      {
        int basketSize = sc.Length;
        prin = new double[basketSize];
        for (int i = 0; i < basketSize; ++i)
        {
          // notional
          double n = 0;
          for (int j = 0, idx = i; j < numSubBasket; idx += basketSize, ++j)
            n += this.Principals[idx];
          prin[i] = n;
        }
      }

      double result = BasketAmortize(start, end, prin, sc, this.RecoveryRates);
      return result / TotalPrincipal;
    }

    /// <summary>
    ///   Compute the protection pv on the whole basket for a period
    /// </summary>
    ///
    /// <param name="start">starting date</param>
    /// <param name="end">ending date</param>
    /// <param name="discountCurve">discount curve</param>
    public virtual double
    BasketLossPv(Dt start, Dt end, DiscountCurve discountCurve)
    {
      SurvivalCurve[] sc = this.SurvivalCurves;
      double[] rc = this.RecoveryRates;
      double[] prin = this.Principals;

      // sanity check
      if (prin.Length < sc.Length)
        throw new ToolkitException(String.Format("Length of principals {0} less than basket size {1}", prin.Length, sc.Length));

      // In case this is a pool like CDO squared.
      // Also handle the case where sc.Length = 0, i.e., all names defaulted.
      int numSubBasket = prin.Length / (sc.Length < 1 ? 1 : sc.Length);
      if (numSubBasket > 1)
      {
        int basketSize = sc.Length;
        prin = new double[basketSize];
        for (int i = 0; i < basketSize; ++i)
        {
          // notional
          double n = 0;
          for (int j = 0, idx = i; j < numSubBasket; idx += basketSize, ++j)
            n += this.Principals[idx];
          prin[i] = n;
        }
      }

      // Add up the pv of covered defaults
      double pv = 0.0;
      Dt current = start;
      double dfCurrent = discountCurve.DiscountFactor(current);
      double accumulatedLoss = 0.0;

      // Loop through the contract until maturity
      while (Dt.Cmp(current, end) < 0)
      {
        double accumulatedLossPrev = accumulatedLoss;
        double dfPrevious = dfCurrent;
        Dt previous = current;
        current = Dt.Add(previous, this.StepSize, this.StepUnit);
        if (Dt.Cmp(current, end) > 0)
        {
          current = end;
        }
        accumulatedLoss = BasketLoss(start, current, prin, sc, rc);

        // Now discount them and accumulate
        double lossInThisPeriod = accumulatedLoss - accumulatedLossPrev;
        dfCurrent = discountCurve.DiscountFactor(current);
        // Note: Here we use the middle of period discount factor, which is different
        // than in Cashflow model.  We need to make the two consistent
        pv += 0.5 * (dfPrevious + dfCurrent) * lossInThisPeriod;
      }

      // return the result
      return pv / TotalPrincipal;
    }

    /// <summary>
    ///   Calculate the maximum level of possible amortizations.
    /// </summary>
    /// <remarks>
    ///   <para>This function calculates the sum of maximum possible
    ///   amortization by names.</para>
    ///   
    ///   <para>Normally, the maximum possible amortization level of
    ///   an individual name equals its recovery rate times its notional.
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
    protected internal virtual double MaximumAmortizationLevel()
    {
      if (WithCorrelatedRecovery)
        return 1.0;
      double[] dispersions = RecoveryDispersions;
      double[] rates = RecoveryRates;
      double[] principals = Principals;
      double amor = 0;
      for (int i = 0; i < rates.Length; ++i)
        amor += principals[i] * (dispersions[i] > 0 ? 1 : rates[i]);
      return PreviousAmortized + amor / TotalPrincipal;
    }

    /// <summary>
    ///   Calculate the maximum level of possible losses.
    /// </summary>
    /// <remarks>
    ///   <para>This function calculates the sum of maximum possible
    ///   losses by names.</para>
    /// 
    ///   <para>Normally, the maximum possible loss level of
    ///   an individual name equals one minus the recovery rate and
    ///   then times ots notional.
    ///   However, if one name has positive recovery dispersion,
    ///   then the maximum possible loss rate is taken as 1
    ///   and the maximum loss level equals to its notional.</para>
    /// 
    ///  <para>The derived classes with their own prepayment assumption
    ///   and recovery treatments may override this
    ///   method to calculate the correct values.</para>
    /// </remarks>
    /// 
    /// <returns>
    ///   The maximum loss level, expressed as a share in
    ///   the basket total principal (0.01 means 1%).
    /// </returns>
    protected internal virtual double MaximumLossLevel()
    {
      if (WithCorrelatedRecovery)
        return 1.0;
      double[] dispersions = RecoveryDispersions;
      double[] rates = RecoveryRates;
      double[] principals = Principals;
      double loss = 0;
      for (int i = 0; i < rates.Length; ++i)
        loss += principals[i] * (dispersions[i] > 0 ? 1 : (1 - rates[i]));
      return PreviousLoss + loss / TotalPrincipal;
    }

    // compute basket loss
    private static double
    BasketLoss(Dt start, Dt end, double[] notionals, SurvivalCurve[] sc, double[] recoveries)
    {
      double result = 0;
      int basketSize = sc.Length;
      for (int i = 0; i < basketSize; ++i)
      {
        // default probability
        double p = sc[i].Interpolate(start);

        // default probability: include effects of the defaulted curves
        p = (p <= 1E-8 ? 1.0 : (1 - sc[i].Interpolate(end) / p));
        if (p < 0)
          p = 0;
        else if (p > 1)
          p = 1;

        // loss
        double loss = p * notionals[i] * (1 - recoveries[i]);
        result += loss;
      }

      return result;
    }

    // compute basket loss
    private static double
    BasketAmortize(Dt start, Dt end, double[] notionals, SurvivalCurve[] sc, double[] recoveries)
    {
      double result = 0;
      int basketSize = sc.Length;
      for (int i = 0; i < basketSize; ++i)
      {
        // survival probability
        double p = sc[i].Interpolate(start);

        // default probability: include effects of the defaulted curves
        p = (p <= 1E-8 ? 1.0 : (1 - sc[i].Interpolate(end) / p));
        if (p < 0)
          p = 0;
        else if (p > 1)
          p = 1;

        // loss
        double amor = p * notionals[i] * recoveries[i];
        result += amor;
      }

      return result;
    }
    #endregion // Distribution_Calculation

    #region Set_Up_Loss_Levels
    // Rework the loss loevels machanism

    /// <summary>
    ///   Check if all the values are already contained
    ///   in the basket loss levels list
    /// </summary>
    /// <param name="values">Values to check</param>
    /// <returns>
    ///   True if all the values are in the basket loss levels list;
    ///   False if any one of them is not in the list.
    /// </returns>
    internal bool LossLevelsContain(params double[] values)
    {
      if (rawLossLevels_ == null)
      {
        if (cookedLossLevels_ == null)
          return false;
        //rawLossLevels_ = new UniqueSequence<double>(values);
        throw new System.NullReferenceException("Raw loss levels cannot be null");
      }
      return rawLossLevels_.ContainsAll(values);
    }

    /// <summary>
    ///   Check if all the values are already contained
    ///   in the basket loss levels list
    /// </summary>
    /// <param name="values">Values to check</param>
    /// <returns>
    ///   True if any value is not in the basket loss levels list;
    ///   False if all the values arein the list and nothing added.
    /// </returns>
    internal bool AddLossLevels(params double[] values)
    {
      if (rawLossLevels_ == null)
      {
        if (cookedLossLevels_ == null)
          rawLossLevels_ = new UniqueSequence<double>();
        else
          //rawLossLevels_ = new UniqueSequence<double>(cookedLossLevels_);
          throw new System.NullReferenceException("Raw loss levels cannot be null");
      }
#if Handle_Defaulted_By_8_7
      return rawLossLevels_.Add(values);
#else
      if (rawLossLevels_.Add(values))
      {
        cookedLossLevels_ = null;
        return true;
      }
      return false;
#endif
    }

    ///
    /// <summary>
    ///   Set an array of loss levels
    /// </summary>
    /// 
    internal void SetLossLevels()
    {
      cookedLossLevels_ = SetLossLevels(rawLossLevels_, addComplement_);
    }

    /// <summary>
    ///   Set up an array of loss levels and adjust the subordinations
    ///   for defaulted names.
    /// </summary>
    /// 
    /// <param name="lossLevels">
    ///   Loss levels as the shares of the basket total principal.
    /// </param>
    /// <param name="addComplement">
    ///   True if add complements (one minus the levels) to the
    ///   cooked loss levels.
    /// </param>
    /// <returns>An <see cref="UniqueSequence{T}"/> of the cooked loss levels.</returns>
    protected internal UniqueSequence<double> SetLossLevels(
      IEnumerable<double> lossLevels, bool addComplement)
    {
      if (lossLevels == null)
        throw new System.NullReferenceException("Input loss levels cannot be null");

      double prevLoss = prevLoss_;
      double remainingBasket = this.InitialBalance;

      // Create a list
      UniqueSequence<double> list = new UniqueSequence<double>();

      // always start with level 0
      list.Add(0.0);
      if (remainingBasket >= 1E-15)
        foreach (double xi in lossLevels)
        {
          double dp = (xi - prevLoss) / remainingBasket;
          if (dp < 0) dp = 0;
          else
          {
            if (dp > 1) dp = 1;
            list.Add(dp);
          }

          if (addComplement)
          {
            //if (dp < 1) list.Add(1 - dp);
            dp = (1 - xi - prevAmor_) / remainingBasket;
            if (dp < 0) dp = 0;
            else
            {
              if (dp > 1) dp = 1;
              list.Add(dp);
            }
          }
        }
      return list;
    }

    #endregion // Set_Up_Loss_Levels

    #region Principal_And_Tranche_Levels

    /// <summary>
    ///   Callback method before the principal array is set
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method is called just before a new array of principals is set.
    ///   The parameter <c>principals</c> contains the new array to be set and
    ///   the property <c>Principals</c> is the old array to be replaced.</para>
    ///
    ///   <para>The returned value should be the appropriate total principal of the basket.
    ///   The default method also scales the principals proportionally to make sure
    ///   that the calculation of the distribution is numerically stable.</para>
    ///
    ///   <para>This method assumes that the defaulted names are not included in the
    ///   array <c>principals</c>.  An assumption need to revisit later.</para>
    /// </remarks>
    ///
    /// <param name="principals">principals to set</param>
    /// <returns>total principal</returns>
    ///
    /// <exclude />
    protected virtual double OnSetPrincipals(double[] principals)
    {
      double totalPrincipal = defaultedPrincipal_
        + ScalePrincipals(principals, ref shorted_);
      return totalPrincipal;
    }

    /// <summary>
    ///   Scale principals and return the total principal
    /// </summary>
    private double ScalePrincipals(double[] principals, ref double shorted)
    {
      double shortedPrincipal = 0.0;
      double totalPrincipal = 0.0;
      if (null != principals)
      {
        int N = principals.Length;
        for (int j = 0; j < N; j++)
        {
          if (principals[j] > 0)
            totalPrincipal += principals[j];
          else
            shortedPrincipal += principals[j];
        }

        // scale the principals to proper magnitude
        double principal = totalPrincipal / N;
        if (principal < 1.0)
        {
          double scale = 1.0 / principal;
          for (int j = 0; j < N; j++)
            principals[j] *= scale;
          totalPrincipal *= scale;
        }
      }
      if (SubstractShortedFromPrincipal)
      {
        totalPrincipal += shortedPrincipal;
        shorted = 0.0;
      }
      else
      {
        shorted = shortedPrincipal / totalPrincipal;
      }
      return totalPrincipal;
    }

    /// <summary>
    ///   Adjust tranche attachment/detachment levels according to previous basket losses
    /// </summary>
    /// <param name="forAmotize">True if the level is for amortization</param>
    /// <param name="level">tranche level on the original basket</param>
    /// <returns>Tranche level on the remaining basket</returns>
    /// <exclude />
    protected internal double AdjustTrancheLevel(
      bool forAmotize,
      double level
      )
    {
      double prevBasketLoss = forAmotize ? prevAmor_ : prevLoss_;
      level -= prevBasketLoss;
      if (level <= 0)
        return 0.0;

      // Will not enter the following block in the mode compatible with release before 8.7
      if (!settings_.SyntheticCDOPricer.UseOriginalNotionalForFee)
      {
        double remainingBasket = 1 - prevAmor_ - prevLoss_;
        if (remainingBasket < 1E-15)
          return 0.0;
        level /= remainingBasket;
        if (level > 1) level = 1;
      }

      // adjust for short names
      if (!SubstractShortedFromPrincipal)
      {
        double remainingBasket = 1 + shorted_;
        if (remainingBasket < 1E-15)
          throw new ToolkitException("Too many short names");
        level /= remainingBasket;
        if (level > 1) level = 1;
      }

      return level;
    }

    /// <summary>
    ///   Adjust tranche attachment/detachment levels according to previous basket losses
    /// </summary>
    /// <param name="forAmotize">True if the level is for amortization</param>
    /// <param name="level">tranche level on the remaining basket</param>
    /// <returns>Tranche level on the original basket</returns>
    /// <exclude />
    protected internal double RestoreTrancheLevel(
      bool forAmotize,
      double level
      )
    {
      // Will not enter the following block in the mode compatible with release before 8.7
      if (!settings_.SyntheticCDOPricer.UseOriginalNotionalForFee)
      {
        double remainingBasket = 1 - prevAmor_ - prevLoss_;
        if (remainingBasket < 1E-15)
          remainingBasket = 0;
        level *= remainingBasket;
      }

      // adjust for short names
      if (!SubstractShortedFromPrincipal)
      {
        double remainingBasket = 1 + shorted_;
        if (remainingBasket < 1E-15)
          throw new ToolkitException("Too many short names");
        level *= remainingBasket;
      }

      double prevBasketLoss = forAmotize ? prevAmor_ : prevLoss_;
      level += prevBasketLoss;
      if (level > 1)
        return 1.0;

      return level;
    }

    /// <summary>
    ///   Adjust tranche levels according to previous basket losses
    /// </summary>
    /// <exclude />
    protected internal void
    AdjustTrancheLevels(
      bool forAmotize,
      ref double trancheBegin,
      ref double trancheEnd,
      ref double trancheLoss
      )
    {
      trancheLoss = 0;

      double prevBasketLoss = forAmotize ? prevAmor_ : prevLoss_;
      if (trancheBegin >= prevBasketLoss)
      {
        trancheBegin -= prevBasketLoss;
        trancheEnd -= prevBasketLoss;
      }
      else if (trancheEnd >= prevBasketLoss)
      {
        trancheLoss = prevBasketLoss - trancheBegin;
#if Handle_Defaulted_By_8_7
        trancheEnd -= trancheLoss; // 8.7 WRONG!!!!
#else
        trancheEnd -= prevBasketLoss; // 9.0 RIGHT!!!!
#endif
        trancheBegin = 0;
      }
      else
      {
        trancheLoss = trancheEnd - trancheBegin;
        trancheEnd = trancheBegin = 0;
      }

      // Will not enter the following block in the mode compatible with release before 8.7
      if (!settings_.SyntheticCDOPricer.UseOriginalNotionalForFee)
      {
        double remainingBasket = 1 - prevAmor_ - prevLoss_;
        if (remainingBasket < 1E-15)
        {
          trancheBegin = trancheEnd = 0;
          return;
        }
        trancheBegin /= remainingBasket;
        if (trancheBegin > 1) trancheBegin = 1;
        trancheEnd /= remainingBasket;
        if (trancheEnd > 1) trancheEnd = 1;
      }

      // adjust for short names
      if (!SubstractShortedFromPrincipal)
      {
        double remainingBasket = 1 + shorted_;
        if (remainingBasket < 1E-15)
          throw new ToolkitException("Too many short names");
        trancheBegin /= remainingBasket;
        if (trancheBegin > 1) trancheBegin = 1;
        trancheEnd /= remainingBasket;
        if (trancheEnd > 1) trancheEnd = 1;
      }

      return;
    }

    /// <summary>
    ///   Adjust tranche levels according to previous basket losses
    ///   and amortizations
    /// </summary>
    /// <exclude />
    public static double TrancheSurvival(
      double preLoss, double preAmor,
      double trancheBegin, double trancheEnd)
    {
      // save the original tranche size
      double originalTrancheWidth = trancheEnd - trancheBegin;
      if (originalTrancheWidth <= 1E-15)
        return 0;

      // deduct from the tranche the basket loss
      if (trancheBegin >= preLoss)
      {
        trancheBegin -= preLoss;
        trancheEnd -= preLoss;
      }
      else if (trancheEnd >= preLoss)
      {
        trancheEnd -= preLoss;
        trancheBegin = 0;
      }
      else
      {
        // nothing left for the tranche
        return 0;
      }

      // deduct from the tranche the basket amortization
      double remainingBasket = 1 - preAmor - preLoss;
      if (trancheBegin >= remainingBasket)
        return 0; // nothing left 
      else if (trancheEnd > remainingBasket)
        trancheEnd = remainingBasket;

      return (trancheEnd - trancheBegin) / originalTrancheWidth;
    }

    /// <summary>
    ///   Calculate accrual fraction with the relevant defaulted names included
    /// </summary>
    /// <param name="begin">Accrual begin date</param>
    /// <param name="end">Accrual end date</param>
    /// <param name="dayCount">Day count</param>
    /// <param name="ap">Tranche attachment point</param>
    /// <param name="dp">Tranche detachment point</param>
    /// <returns>Accrual fraction</returns>
    public double AccrualFraction(Dt begin, Dt end, DayCount dayCount,
      double ap, double dp)
    {
      if (defaultInfo_ == null)
        return Dt.Fraction(begin, end, dayCount);
      return defaultInfo_.AccrualFraction(begin, end, dayCount, ap, dp);
    }

    /// <summary>
    ///   Calculate the pv of the values from the names
    ///   which have defaulted but need to be settled after the pricer settle date.
    /// </summary>
    /// <param name="settle">Pricer settle date</param>
    /// <param name="maturity">Pricer maturity date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="ap">Tranche attachment</param>
    /// <param name="dp">Tranche detachment</param>
    /// <param name="includeLoss">True if include the (negative) loss values</param>
    /// <param name="includeRecovery">True if include the (positive) recovery values</param>
    /// <returns>The settlement values discounted to the settle date</returns>
    public double DefaultSettlementPv(
      Dt settle, Dt maturity,
      DiscountCurve discountCurve,
      double ap, double dp, bool includeLoss, bool includeRecovery)
    {
      if (defaultInfo_ == null) return 0;
      return defaultInfo_.DefaultSettlementPv(settle, settle, maturity,
        discountCurve, ap, dp, includeLoss, includeRecovery);
    }

    internal double UnsettledDefaultAccrualAdjustment(
      Dt payDt, Dt begin, Dt end, DayCount dayCount,
      double ap, double dp,
      DiscountCurve discountCurve)
    {
      if (defaultInfo_ == null) return 0;
      return defaultInfo_.UnsettledAccrualAdjustment(
        payDt, begin, end, dayCount, ap, dp, discountCurve);
    }
    #endregion // Principal_And_Tranche_Levels

    #region Correlation_Related

    ///
    /// <summary>
    ///   Set the correlation between any pair of credits to be the same number
    /// </summary>
    ///
    /// <remarks>
    ///  <para>
    ///   The correlation between pairs are set to the square of the factor.
    ///  </para>
    /// 
    ///  <para>
    ///   This function also takes care of setting appropriate flags
    ///   informing the pricer that the distributions need to recalculate.
    ///  </para>
    /// </remarks>
    ///
    /// <param name="factor">factor to set</param>
    public virtual void SetFactor(double factor)
    {
      ICorrelationSetFactor corr = (ICorrelationSetFactor)this.Correlation;
      corr.SetFactor(Maturity, factor);
      ResetCorrelation();
      Reset();
    }
    #endregion // Correlation_Related

    #region Copy_And_Initialization

    private void Set(CreditPool basket, CorrelationObject correlation, IEnumerable lossLevels)
    {
      if (basket == null)
      {
        basket = originalBasket_;
        if (basket == null)
          throw new ArgumentException("Basket cannot be null.");
      }

      double[] picks = SetBasket(basket);

      if (correlation != null)
      {
        this.originalCorrelation_ = correlation;
        this.correlation_ = CreateCorrelation(correlation, picks);
      }

      // Set loss levels
      if (lossLevels != null)
      {
        if (!ReferenceEquals(lossLevels, rawLossLevels_))
          this.rawLossLevels_ = UniqueSequence<double>.From(lossLevels);
        this.cookedLossLevels_ = null; // 9.0 RIGHT
      }

      return;
    }

    /// <summary>
    ///   Set the underlying curves and participations
    /// </summary>
    /// <exclude />
    private double[] SetBasket(CreditPool basket)
    {
      originalBasket_ = basket;

      // Validation is done when construct the basket.
      SurvivalCurve[] survivalCurves = CreditPool.ShallowClone(basket.SurvivalCurves);
      RecoveryCurve[] recoveryCurves = CreditPool.ShallowClone(basket.RecoveryCurves);
      double[] principals = CreditPool.ShallowClone(basket.Participations);

      // If recoveryCurves is null, we get them from the survival curves
      if (recoveryCurves == null)
        recoveryCurves = GetRecoveryCurves(survivalCurves);

      // The following functions set many properties:
      //   SurvivalCurves, RecoveryCurves,
      //   Principals (which also set totalPrincipal_),
      //   shortedPrincipal_, defaultedPrincipal_, prevLoss_, prevAmor_,
      //   and defaultInfo.
      Utils.ScaleUp(principals, 10.0);
      double[] picks = RemoveDefaultedCredits(survivalCurves, recoveryCurves, principals);

      if (basket.EarlyMaturities != null)
      {
        maturities_ = ArrayUtil.PickElements(basket.EarlyMaturities, picks);
      }
      if (basket.AsPoolOfLCDS)
      {
        if (basket.RefinanceCurves != null)
        {
          refinanceCurves_ = ArrayUtil.PickElements(
            basket.RefinanceCurves, picks);
          refinanceCorrelations_ = ArrayUtil.PickElements(
            basket.RefinanceCorrelations, picks);
        }
        else
        {
          GetRefinanceInfosFromSurvivalCurves(survivalCurves_,
            out refinanceCurves_, out refinanceCorrelations_);
        }
      }

      return picks;
    }

    /// <summary>
    ///   Copy all the underlying data to another basket pricer
    /// </summary>
    /// <exclude />
    public void CopyTo(BasketPricer basket)
    {
      // copy public properties
      basket.originalBasket_ = this.originalBasket_;
      basket.originalCorrelation_ = this.originalCorrelation_;

      basket.accuracyLevels_ = this.accuracyLevels_;
      basket.additionalGridDates_ = this.additionalGridDates_;
      basket.asOf_ = this.asOf_;
      basket.averageRecoveryRate_ = this.averageRecoveryRate_;
      basket.copula_ = this.copula_;
      basket.correlation_ = this.correlation_;
      basket.counterpartyCurve_ = this.counterpartyCurve_;
      basket.counterpartyCorrelation_ = this.counterpartyCorrelation_;
      basket.defaultedPrincipal_ = this.defaultedPrincipal_;
      basket.effectiveDigits_ = this.effectiveDigits_;
      basket.gridSize_ = this.gridSize_;
      basket.hasFixedRecovery_ = this.hasFixedRecovery_;
      basket.integrationPointsFirst_ = this.integrationPointsFirst_;
      basket.integrationPointsSecond_ = this.integrationPointsSecond_;
      basket.maturities_ = this.maturities_;
      basket.maturity_ = this.maturity_;
      basket.rcmodel_ = this.rcmodel_;
      basket.names_ = this.names_;
      basket.noAmortize_ = this.noAmortize_;
      basket.portfolioStart_ = this.portfolioStart_;
      basket.prevAmor_ = this.prevAmor_;
      basket.prevLoss_ = this.prevLoss_;
      basket.principals_ = this.principals_;
      basket.recoveryCurves_ = this.recoveryCurves_;
      basket.recoveryRates_ = this.recoveryRates_;
      basket.recoveryDispersions_ = this.recoveryDispersions_;
      basket.refinanceCurves_ = this.refinanceCurves_;
      basket.refinanceCorrelations_ = this.refinanceCorrelations_;
      basket.sampleSize_ = this.sampleSize_;
      basket.settle_ = this.settle_;
      basket.shorted_ = this.shorted_;
      basket.stepSize_ = this.stepSize_;
      basket.stepUnit_ = this.stepUnit_;
      basket.survivalCurves_ = this.survivalCurves_;
      basket.totalPrincipal_ = this.totalPrincipal_;
      basket.tsCorrelation_ = this.tsCorrelation_;
      basket.usedQuasiRng_ = this.usedQuasiRng_;
      basket.timeGrid_ = this.timeGrid_;

      basket.rawLossLevels_ = this.rawLossLevels_;
      basket.cookedLossLevels_ = this.cookedLossLevels_;
      basket.defaultInfo_ = this.defaultInfo_;
    }

    //-
    // This method remove all the defaulted credit from the BASKET
    // and returns the values of the default loss and the corresponding
    // amortization amount.
    //-
    private double[] RemoveDefaultedCredits(
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals)
    {
      defaultedPrincipal_ = 0;
      prevLoss_ = 0;
      prevAmor_ = 0;

      // Principals must be properly scaled before calculating default losses
      principals_ = principals;
      ScalePrincipals(principals_, ref shorted_);

      // Initialize local variables
      double[] prin = principals = principals_;
      SurvivalCurve[] sc = survivalCurves;
      RecoveryCurve[] rc = recoveryCurves;

      // Count the defaulted credits
      int count = 0;
      // If sc.Length != prin.Length, this is the CDO^2 basket.
      // Don't remove the defaulted names since we handle it
      // in the derived class.
      bool exactJtD = ExactJumpToDefault;
      if (sc != null && sc.Length == prin.Length)
      {
        if (exactJtD)
        {
          foreach (SurvivalCurve s in sc)
            if (s.Defaulted != Defaulted.NotDefaulted) ++count;
        }
        else
        {
          foreach (SurvivalCurve s in sc)
            if (s.Defaulted == Defaulted.HasDefaulted) ++count;
        }
      }

      // Removed defaulted credits
      BasketDefaultInfo.Builder defaultInfoBuilder
        = new BasketDefaultInfo.Builder();
      double[] picks = ArrayUtil.NewArray(survivalCurves.Length, 1.0);
      if (count > 0)
      {
        // Convert count to be the number of survivals
        count = survivalCurves.Length - count;

        // Create arrays of active credits and calculate prev losses
        prin = new double[count];
        sc = new SurvivalCurve[count];
        rc = new RecoveryCurve[count];
        for (int i = 0, idx = 0; i < survivalCurves.Length; ++i)
        {
          Defaulted defaultStatus = survivalCurves[i].Defaulted;
          if (defaultStatus == Defaulted.HasDefaulted
            || (exactJtD && defaultStatus == Defaulted.WillDefault))
          {
            picks[i] = 0;
            double recoveryRate = recoveryCurves[i].RecoveryRate(this.Maturity);
            defaultedPrincipal_ += principals[i];
            double loss, amor;
            prevAmor_ += (amor = principals[i] * recoveryRate);
            prevLoss_ += (loss = principals[i] * (1 - recoveryRate));
            Dt defaultDate = survivalCurves[i].DefaultDate;
            defaultInfoBuilder.Add(defaultDate, loss, amor);

            // Handle the case WillDefault.
            if (defaultStatus == Defaulted.WillDefault)
            {
              Dt dftSettle = defaultDate < Settle ? Settle : defaultDate;
              defaultInfoBuilder.AddSettle(dftSettle, defaultDate, amor, loss, true);
              continue;
            }
            // Handle the case HasDefaulted.
            if (survivalCurves[i].SurvivalCalibrator == null)
              continue;
            RecoveryCurve rcurve = survivalCurves[i].SurvivalCalibrator.RecoveryCurve;
            if (rcurve == null || rcurve.JumpDate.IsEmpty())
              continue;
            defaultInfoBuilder.AddSettle(rcurve.JumpDate, defaultDate, amor, loss);
          }
          else
          {
            prin[idx] = principals[i];
            sc[idx] = survivalCurves[i];
            rc[idx] = recoveryCurves[i];
            ++idx;
          }
        }
      }

      // Set the basket properties
      this.SurvivalCurves = sc;
      this.RecoveryCurves = rc;
      this.Principals = prin;

      // Loss and amortization must be in percentage
      prevLoss_ /= this.TotalPrincipal;
      prevAmor_ /= this.TotalPrincipal;
      defaultInfo_ = defaultInfoBuilder.ToDefaultInfo(this.TotalPrincipal);

      // done
      return picks;
    }


    /// <summary>
    /// Create a correlation object with a subset of constituents.
    /// </summary>
    /// <param name="correlationObj">The correlation.</param>
    /// <param name="picks">The array indicating what names are included.</param>
    private static CorrelationObject CreateCorrelation(
      CorrelationObject correlationObj, double[] picks)
    {
      // Validate
      if (correlationObj == null)
        throw new ArgumentException("correlation cannot be null.");
      Correlation correlation = correlationObj as Correlation;
      if (correlation == null)
      {
        throw new System.ArgumentException(String.Format(
          "correlation must be either Correlation or CorrelationTermStruct, not {0}",
          correlationObj.GetType()));
      }
      if (picks == null)
        return correlation;

      if (picks.Length > correlation.Names.Length && correlation.Names.Length > 1)
      {
        throw new ArgumentException(String.Format(
          "Consituents of correlation (len={0}) and notionals (len={1}) not match.",
          correlation.Names.Length, picks.Length));
      }

      // Create a new correlation object
      if (correlation is SingleFactorCorrelation)
        correlation = CorrelationFactory.CreateSingleFactorCorrelation((SingleFactorCorrelation)correlation, picks);
      else if (correlation is FactorCorrelation)
        correlation = CorrelationFactory.CreateFactorCorrelation((FactorCorrelation)correlation, picks);
      else if (correlation is GeneralCorrelation)
        correlation = CorrelationFactory.CreateGeneralCorrelation((GeneralCorrelation)correlation, picks);
      else if (correlation is CorrelationTermStruct)
        correlation = CorrelationFactory.CreateCorrelationTermStruct((CorrelationTermStruct)correlation, picks);
      else
        throw new ToolkitException("Unknown correlation type");

      return correlation;
    }

    #endregion // Copy_And_Initialization

    #region Recovery_Related

    // get recovery curves from survival curves
    internal static RecoveryCurve[]
    GetRecoveryCurves(SurvivalCurve[] sc)
    {
      RecoveryCurve[] rc = new RecoveryCurve[sc.Length];
      for (int i = 0; i < sc.Length; ++i)
      {
        SurvivalCalibrator cal = sc[i].SurvivalCalibrator;
        if (cal == null)
          throw new System.ArgumentException(String.Format("null calibrator in survival curve {0}", i));
        rc[i] = cal.RecoveryCurve;
        if (rc[i] == null)
          throw new System.ArgumentException(String.Format("null recovery curve in survival curve {0}", i));
      }
      return rc;
    }

    internal static double[] GetRecoveryRates(
      RecoveryCurve[] recoveryCurves, Dt date)
    {
      double[] recoveryRates = new double[recoveryCurves.Length];
      for (int i = 0; i < recoveryCurves.Length; i++)
      {
        recoveryRates[i] = recoveryCurves[i].RecoveryRate(date);
      }
      return recoveryRates;
    }

    // get recovery curves from survival curves
    internal double[] GetRecoveryRates(SurvivalCurve[] sc)
    {
      double[] recoveryRates = new double[sc.Length];
      for (int i = 0; i < sc.Length; ++i)
      {
        SurvivalCalibrator cal = sc[i].SurvivalCalibrator;
        if (cal == null)
          throw new System.ArgumentException(String.Format(
            "null calibrator in survival curve {0}", i));
        if (cal.RecoveryCurve == null)
          throw new ArgumentException(String.Format("null recovery curve in survival curve {0}", i));
        recoveryRates[i] = cal.RecoveryCurve.RecoveryRate(Maturity);
      }
      return recoveryRates;
    }

    /// <summary>
    ///   Update average recovery rate
    ///   <preliminary/>
    /// </summary>
    /// <exclude />
    protected internal void UpdateRecoveries()
    {
      if (recoveryRates_ == null)
      {
        recoveryRates_ = GetRecoveryRates(RecoveryCurves, Maturity);

      }
      averageRecoveryRate_
        = UpdateAverageRecoveryRate(recoveryRates_);
    }

    /// <summary>
    ///   Update average recovery rate
    /// </summary>
    /// <exclude />
    internal static double UpdateAverageRecoveryRate(double[] recoveryRates)
    {
      // use the average recovery rate
      if (recoveryRates != null)
      {
        double sum = 0;
        for (int i = 0; i < recoveryRates.Length; ++i)
          sum += recoveryRates[i];
        return sum / recoveryRates.Length;
      }
      else
        return 0;
    }
    #endregion Recovery_Related

    #region Set_Up_Date_Grids
    /// <summary>
    ///   Generate time grid dates
    ///   <preliminary/>
    /// </summary>
    /// <remarks>The grid dates are superset of the additional dates
    ///  and the dates generated by step size and step unit.</remarks>
    /// <returns>Grid dates</returns>
    /// <exclude/>
    protected internal static UniqueSequence<Dt> GenerateGridDates(
      Dt start, Dt stop,
      int stepSize, TimeUnit stepUnit,
      UniqueSequence<Dt> additionalDates)
    {
      UniqueSequence<Dt> timeGrid =
        additionalDates == null ? new UniqueSequence<Dt>()
        : (UniqueSequence<Dt>)additionalDates.Clone();
      for (Dt date = start; Dt.Cmp(date, stop) < 0;
        date = Dt.Add(date, stepSize, stepUnit))
      {
        timeGrid.Add(date);
      }

      timeGrid.Add(Dt.Later(start, stop));
      return timeGrid;
    }

    /// <summary>
    ///   Add an additional grid date
    /// <preliminary/>
    /// </summary>
    /// <param name="dates">a list of additional grid dates to add</param>
    public void AddGridDates(params Dt[] dates)
    {
      if (additionalGridDates_ == null)
        additionalGridDates_ = new UniqueSequence<Dt>();
      if (additionalGridDates_.Add(dates))
        timeGrid_ = null;
    }

    /// <summary>
    ///   Generate time grid dates
    /// </summary>
    internal void UpdateGridDates()
    {
      timeGrid_ = GenerateGridDates(
        portfolioStart_.IsEmpty() ? settle_ : portfolioStart_,
        maturity_,
        stepSize_ <= 0 ? 3 : stepSize_,
        stepUnit_ == TimeUnit.None ? TimeUnit.Months : stepUnit_,
        additionalGridDates_);
    }

    /// <summary>
    ///  Set the default info to null before theta calc
    /// </summary>
    public void SetNullDefaultInfo()
    {
      savedDefaultInfo_ = defaultInfo_;
      defaultInfo_ = null;
    }

    /// <summary>
    ///  Set back origional default info after theta
    /// </summary>
    public void SetBackDefaultInfo()
    {
      defaultInfo_ = savedDefaultInfo_;
      savedDefaultInfo_ = null;
    }

    #endregion // Set_Up_Date_Grids

    #region Refinance Infos
    /// <summary>
    ///   Check if the survival curves contain any refinance info
    /// </summary>
    /// <param name="curves">survival curves</param>
    /// <returns>True if contain refianace info</returns>
    public static bool HasRefinanceCurves(SurvivalCurve[] curves)
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
    public static void GetRefinanceInfosFromSurvivalCurves(
      SurvivalCurve[] survivalCurves,
      out SurvivalCurve[] refinanceCurves,
      out double[] refinanceCorrelations)
    {
      refinanceCurves = null;
      refinanceCorrelations = null;
      if (HasRefinanceCurves(survivalCurves))
      {
        int N = survivalCurves.Length;
        SurvivalCurve[] rCurves = new SurvivalCurve[N];
        double[] rCorrelations = new double[N];
        for (int i = 0; i < N; ++i)
        {
          SurvivalCurve curve = survivalCurves[i];
          if (curve.SurvivalCalibrator != null)
          {
            SurvivalCalibrator calibrator = curve.SurvivalCalibrator;
            rCurves[i] = calibrator.CounterpartyCurve;
            rCorrelations[i] = calibrator.CounterpartyCorrelation;
          }
        }
        refinanceCurves = rCurves;
        refinanceCorrelations = rCorrelations;
      }
      return;
    }
    #endregion Refince infos

    #endregion // Methods

    #region Properties

    #region Enumerators

    /// <summary>
    ///   Number of names (read only)
    /// </summary>
    public int Count
    {
      get { return survivalCurves_.Length; }
    }

    /// <summary>
    ///   Return IEnumerator for basket survival curves
    /// </summary>
    public IEnumerator
    GetEnumerator()
    {
      return survivalCurves_.GetEnumerator();
    }

    #endregion //Enumerators

    #region TimeGrids

    /// <summary>
    ///   Portfolio start date
    /// </summary>
    /// <exclude />
    public Dt PortfolioStart
    {
      get { return portfolioStart_; }
      set
      {
        if (Dt.Cmp(portfolioStart_, value) != 0)
          timeGrid_ = null;
        portfolioStart_ = value;
      }
    }

    /// <summary>
    ///   As of date
    /// </summary>
    public Dt AsOf
    {
      get { return asOf_; }
      set
      {
        asOf_ = value;
      }
    }

    /// <summary>
    ///   Settlement date
    /// </summary>
    public Dt Settle
    {
      get { return settle_; }
      set
      {
        if (Dt.Cmp(settle_, value) != 0)
          timeGrid_ = null;
        settle_ = value;
      }
    }

    /// <summary>
    ///   Maturity date
    /// </summary>
    public Dt Maturity
    {
      get { return maturity_; }
      set
      {
        if (Dt.Cmp(maturity_, value) != 0)
        {
          timeGrid_ = null;
          ResetRecoveryRates();
        }
        maturity_ = value;
      }
    }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public int StepSize
    {
      get { return stepSize_; }
      set
      {
        if (stepSize_ != value)
          timeGrid_ = null;
        stepSize_ = value;
      }
    }

    /// <summary>
    ///   Additional time grid dates
    /// </summary>
    /// <exclude />
    public UniqueSequence<Dt> AdditionalGridDates
    {
      get { return additionalGridDates_; }
      set
      {
        additionalGridDates_ = value;
        timeGrid_ = null;
      }
    }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set
      {
        if (stepUnit_ != value)
          timeGrid_ = null;
        stepUnit_ = value;
      }
    }

    /// <summary>
    ///   True if the state of time grid changed
    ///   <preliminary/>
    /// </summary>
    /// <exclude />
    protected internal bool TimeGridChanged
    {
      get { return timeGrid_ == null; }
      private set
      {
        if (value)
          timeGrid_ = null;
      }
    }

    /// <summary>
    ///   A sorted sequence of time grid dates
    /// </summary>
    protected internal UniqueSequence<Dt> TimeGrid
    {
      get
      {
        if (timeGrid_ == null)
          UpdateGridDates();
        return timeGrid_;
      }
    }

    #endregion // TimeGrids

    #region LossLevels

    /// <summary>
    ///   The array of loss levels in percentage used to construct loss curve
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    internal double[] LossLevels // NEED BE PUBLIC FOR BACKWARDD COMPATIBILITY
    {
      get
      {
        if (cookedLossLevels_ == null)
          SetLossLevels();
        return cookedLossLevels_.ToArray();
      }
    }

    /// <summary>
    ///   The array of loss levels in percentage used to construct loss curve
    ///  <preliminary/>
    /// </summary>
    public UniqueSequence<double> RawLossLevels
    {
      get { return rawLossLevels_; }
      set
      {
        rawLossLevels_ = value;
#if Handle_Defaulted_By_8_7
#else
        cookedLossLevels_ = null; // need update
#endif
      }
    }

    /// <summary>
    ///   The array of loss levels in percentage used to construct loss curve
    /// </summary>
    protected internal UniqueSequence<double> CookedLossLevels
    {
      get
      {
        if (cookedLossLevels_ == null)
          SetLossLevels();
        return cookedLossLevels_;
      }
    }

    /// <summary>
    ///   Add complements to loss levels
    /// </summary>
    internal bool LossLevelAddComplement
    {
      get { return addComplement_; }
      set { addComplement_ = value; }
    }

    #endregion // LossLevels

    #region Correlations

    /// <summary>
    ///   Correlation structure of the basket
    /// </summary>
    public CorrelationObject Correlation
    {
      get { return correlation_; }
      set
      {
        if (value == null)
          throw new System.ArgumentException(String.Format(
            "Invalid correlation. Cannot be null"));
        correlation_ = value;
        tsCorrelation_ = null;
      }
    }

    /// <summary>
    ///   Copula structure
    /// </summary>
    public Copula Copula
    {
      get { return copula_; }
      set
      {
        if (value == null)
          throw new System.ArgumentException(String.Format(
            "Invalid copula. Cannot be null"));
        copula_ = value;
      }
    }

    /// <summary>
    ///   Copula type used for pricing
    /// </summary>
    public CopulaType CopulaType
    {
      get
      {
        if (this.Correlation is ExternalFactorCorrelation)
        {
#if DEBUG
          if (copula_.CopulaType == CopulaType.Gauss)
            return CopulaType.ExternalGauss;
#endif
          throw new System.ArgumentException(String.Format(
            "Copula type \'{0}\' cannot use ExternalFactorCorrelation",
            copula_.CopulaType));
        }
        return copula_.CopulaType;
      }
    }

    /// <summary>
    ///   Degrees of freedom for common factor
    /// </summary>
    public int DfCommon
    {
      get { return copula_.DfCommon; }
    }

    /// <summary>
    ///   Degrees of freedom for idiosyncratic factor
    /// </summary>
    public int DfIdiosyncratic
    {
      get { return copula_.DfIdiosyncratic; }
    }

    /// <summary>
    ///   Need to update correlation?
    /// </summary>
    protected internal bool IsCorrelationChanged
    {
      get { return tsCorrelation_ == null || correlation_.Modified; }
    }

    /// <summary>
    ///   Get term structure correlation
    /// </summary>
    /// <exclude />
    public CorrelationTermStruct CorrelationTermStruct
    {
      get
      {
        if (tsCorrelation_ == null || correlation_.Modified)
        {
          if (this.Correlation is CorrelationTermStruct)
          {
            tsCorrelation_ = (CorrelationTermStruct)this.Correlation;
            if (copula_.CopulaType == CopulaType.Poisson)
              tsCorrelation_ = HWDynamicBasketPricer.CorrelationFromParameters(
                tsCorrelation_.Names, tsCorrelation_.Correlations,
                PortfolioStart.IsEmpty() ? Settle : PortfolioStart, Maturity);
          }
          else if (this.Correlation is Correlation)
            tsCorrelation_ = CorrelationTermStruct.FromCorrelations(
              ((Correlation)this.Correlation).Names, new Dt[1] { this.Maturity },
              new Correlation[1] { (Correlation)this.Correlation });
          else if (this.Correlation is BaseCorrelationObject)
          {
            BaseCorrelationObject correlation = (BaseCorrelationObject)this.Correlation;
            if (correlation is BaseCorrelationTermStruct &&
              ((BaseCorrelationTermStruct)correlation).CalibrationMethod == BaseCorrelationCalibrationMethod.TermStructure)
            {
              tsCorrelation_ = CorrelationFactory.CreateCorrelationTermStruct(correlation, this.SurvivalCurves,
                UseNaturalSettlement ? (portfolioStart_.IsEmpty() ? settle_ : portfolioStart_) : asOf_);
            }
            else
              tsCorrelation_ = CorrelationFactory.CreateCorrelationTermStruct(null, this.SurvivalCurves,
                UseNaturalSettlement ? (portfolioStart_.IsEmpty() ? settle_ : portfolioStart_) : asOf_);
          }
          else if (this is PvAveragingBasketPricer)
          {
            tsCorrelation_ = ((PvAveragingBasketPricer)this).DumyCorrelation;
            return tsCorrelation_;
          }
          else
            throw new ArgumentException(String.Format("The Argument correlation must be either FactorCorrelation or CorrelationTermStruct, not {0}",
              this.Correlation.GetType()));
        }
        return tsCorrelation_;
      }
      internal set
      {
        tsCorrelation_ = value;
      }
    }
    #endregion // Correlations

    #region SurvivalCurves

    /// <summary>
    ///  Survival curves from curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return survivalCurves_; }
      set
      {
        survivalCurves_ = value;
        names_ = null;
      }
    }

    /// <summary>
    ///   Get an Array of credit names
    /// </summary>
    ///  <exclude />
    [Browsable(false)]
    public string[] EntityNames
    {
      get
      {
        if (names_ == null)
          names_ = Utils.GetCreditNames(survivalCurves_);
        return names_;
      }
    }
    #endregion // SurvivalCurves

    #region Recoveries

    /// <summary>
    ///   Whether to use fixed recovery curves
    /// </summary>
    protected internal bool HasFixedRecovery
    {
      get { return hasFixedRecovery_; }
      set { hasFixedRecovery_ = value; }
    }

    /// <summary>
    ///   Whether to use correlated recovery model
    ///   (currently only works with semi-analytic model)
    ///   <preliminary/>
    /// </summary>
    public bool WithCorrelatedRecovery
    {
      get { return rcmodel_.ModelChoice.WithCorrelatedRecovery; }
      set { rcmodel_.ModelChoice.WithCorrelatedRecovery = value; }
    }

    /// <summary>
    ///   Correlated recovery model to use.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    public RecoveryCorrelationType QCRModel
    {
      get { return rcmodel_.ModelChoice.QCRModel; }
      set { rcmodel_.ModelChoice.QCRModel = value; }
    }

    /// <summary>
    ///   Basket model type.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    public BasketModelType ModelType
    {
      get { return rcmodel_.ModelChoice.BasketModel; }
      set { rcmodel_.ModelChoice.BasketModel = value; }
    }

    /// <exclude/>
    internal RecoveryCorrelationModel RecoveryCorrelationModel
    {
      get { return rcmodel_; }
      set { rcmodel_ = value; }
    }

    /// <exclude/>
    internal BasketModelChoice ModelChoice
    {
      get { return rcmodel_.ModelChoice; }
    }

    /// <exclude/>
    internal double RecoveryUpperBound
    {
      get { return rcmodel_.MaxRecovery; }
    }

    /// <exclude/>
    internal double RecoveryLowerBound
    {
      get { return rcmodel_.MinRecovery; }
    }

    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get
      {
        if (recoveryCurves_ == null)
          recoveryCurves_ = GetRecoveryCurves(survivalCurves_);
        return recoveryCurves_;
      }
      set
      {
        recoveryCurves_ = value;
        ResetRecoveryRates();
      }
    }

    /// <summary>
    ///   Recovery rates from curves
    /// </summary>
    public double[] RecoveryRates
    {
      get
      {
        if (recoveryRates_ == null)
          UpdateRecoveries();
        return recoveryRates_;
      }
    }

    /// <summary>
    ///   Average recovery rate
    /// </summary>
    public double AverageRecoveryRate
    {
      get
      {
        if (recoveryRates_ == null)
          UpdateRecoveries();
        return averageRecoveryRate_;
      }
    }

    /// <summary>
    ///   Recovery dispersions from curves
    /// </summary>
    public double[] RecoveryDispersions
    {
      get
      {
        if (recoveryDispersions_ == null)
        {
          RecoveryCurve[] recoveryCurves = RecoveryCurves;
          recoveryDispersions_ = new double[recoveryCurves_.Length];
          for (int i = 0; i < recoveryCurves.Length; i++)
          {
            recoveryDispersions_[i] = recoveryCurves[i].RecoveryDispersion;
          }
        }
        return recoveryDispersions_;
      }
    }

    #endregion // Recoveries

    #region Principals

    /// <summary>
    ///   Principal or face values for each name in the basket
    /// </summary>
    public double[] Principals
    {
      // TBD mef 22Apr2004 Improve data validation
      get { return principals_; }
      set
      {
        totalPrincipal_ = OnSetPrincipals(value);
        principals_ = value;
      }
    }

    /// <summary>
    ///    The total original principal/notional in the basket
    /// </summary>
    public double TotalPrincipal
    {
      get { return totalPrincipal_; }
    }

    /// <summary>
    ///    Effective decimal points
    /// </summary>
    public int EffectiveDigits
    {
      get { return effectiveDigits_; }
      set
      {
        effectiveDigits_ = value;
      }
    }

    #endregion // Principals

    #region Refinance, counterparty and Early maturities
    /// <summary>
    ///   Refinance curves.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    public SurvivalCurve[] RefinanceCurves
    {
      get { return refinanceCurves_; }
      set { refinanceCurves_ = value; }
    }

    /// <summary>
    ///   Correlations between default and refinance.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    public double[] RefinanceCorrelations
    {
      get { return refinanceCorrelations_; }
      set { refinanceCorrelations_ = value; }
    }

    /// <summary>
    ///   Early maturities.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    public Dt[] EarlyMaturities
    {
      get { return maturities_; }
      set { maturities_ = value; }
    }

    /// <summary>
    ///   Correlated counterparty curve.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    public SurvivalCurve CounterpartyCurve
    {
      get { return counterpartyCurve_; }
      set { counterpartyCurve_ = value; }
    }

    /// <summary>
    ///   Counterparty correlation.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    public double CounterpartyCorrelation
    {
      get { return counterpartyCorrelation_; }
      set { counterpartyCorrelation_ = value; }
    }
    #endregion Refinance, counterparty and Early maturities

    #region NumericalOptions

    /// <summary>
    ///   Number of integration points (read only)
    /// </summary>
    public double AccuracyLevel
    {
      get { return accuracyLevels_; }
      set { accuracyLevels_ = value; }
    }

    /// <summary>
    ///   Number of integration points (read only)
    /// </summary>
    public int IntegrationPointsFirst
    {
      get { return integrationPointsFirst_; }
      set
      {
        integrationPointsFirst_ = value;
      }
    }

    /// <summary>
    ///   Number of integration points (read only)
    /// </summary>
    public int IntegrationPointsSecond
    {
      get { return integrationPointsSecond_; }
      set
      {
        integrationPointsSecond_ = value;
      }
    }

    /// <summary>
    ///   Sample size in simulation
    /// </summary>
    public int SampleSize
    {
      get { return sampleSize_; }
      set
      {
        sampleSize_ = value;
      }
    }

    /// <summary>
    ///   Use quasi random numbers if possible
    /// </summary>
    public bool UseQuasiRng
    {
      get { return usedQuasiRng_; }
      set { usedQuasiRng_ = value; }
    }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public double GridSize
    {
      get { return gridSize_; }
      set
      {
        gridSize_ = value;
      }
    }

    #endregion // NumericalOptions

    #region Others

    /// <summary>
    ///   Basket loss caused by the defaults happened before the settlement date,
    ///   as the share in total principal (0.01 means 1%)
    /// </summary>
    /// <exclude />
    protected internal double PreviousLoss
    {
      get { return prevLoss_; }
    }

    /// <summary>
    ///   Basket amortization caused by the defaults happened before
    ///   the settlement date, the share in total principal (0.01 means 1%)
    /// </summary>
    /// <exclude />
    protected internal double PreviousAmortized
    {
      get { return prevAmor_; }
    }

    /// <summary>
    ///   Total defaulted principals
    /// </summary>
    /// <exclude />
    protected internal double DefaultedPrincipal
    {
      get { return defaultedPrincipal_; }
    }

    /// <summary>
    ///   Initial outstanding notional as share of the 
    ///   original total notional of basket
    /// </summary>
    protected internal double InitialBalance
    {
      get
      {
        double remainingBasket =
          settings_.SyntheticCDOPricer.UseOriginalNotionalForFee ? 1.0 // 8.6 compatible
          : 1 - prevAmor_ - prevLoss_; // the correct way
        double shortAdjustment = 1 + shorted_;
        if (shortAdjustment < 1E-15)
          throw new ToolkitException("To much short names");
        remainingBasket *= shortAdjustment;
        return remainingBasket;
      }
    }

    /// <summary>
    ///   This basket is owned by a single CDO pricer
    /// </summary>
    internal bool IsUnique
    {
      get { return isUnique_; }
      set { isUnique_ = value; }
    }

    /// <summary>
    ///   If true, the distribution of amortizations will not
    ///   be calculated.
    /// </summary>
    /// <remarks>
    ///   <para>When it is known that no amortization will hit the tranches
    ///   to be priced with this basket, this property can set to true in
    ///   order to speed up the computation.</para>
    /// 
    ///   <para>The default value of this property is <c>true</c>.</para>
    /// </remarks>
    protected internal bool NoAmortization
    {
      get { return noAmortize_; }
      set { noAmortize_ = value; }
    }

    /// <summary>
    ///   The index of the date to start calculating distributions
    /// </summary>
    protected internal int RecalcStartDateIndex
    {
      get { return recalcStartDateIndex_; }
      set { recalcStartDateIndex_ = value; }
    }

    #endregion // Others

    #region Original Basket
    /// <summary>
    ///   The original user input of basket (defaulted names not removed).
    /// </summary>
    public CreditPool OriginalBasket
    {
      get { return originalBasket_; }
      set { originalBasket_ = value; }
    }

    /// <summary>
    ///   The original user input of correlation (defaulted names not removed).
    /// </summary>
    protected internal CorrelationObject OriginalCorrelation
    {
      get { return originalCorrelation_; }
      set { originalCorrelation_ = value; }
    }

    /// <summary>
    /// List of raw detachment points. The index of a given detachment 
    /// </summary>
    public UniqueSequence<double> RawDetachments
    {
      get { return rawDetachments_; }
    }

    /// <summary>
    /// Property set to true each time BasketPricer is reset
    /// </summary>
    public bool RecomputeDerivatives
    {
      get { return recomputeDerivatives_; }
      set { recomputeDerivatives_ = value; }
    }

    /// <summary>
    /// List of CurveArray objects to store expected tranche tranche loss derivatives
    /// </summary>
    public UniqueSequence<CurveArray> TrancheLossDer
    {
      get { return trancheLossDer_; }
    }

    /// <summary>
    /// List of CurveArray objects to store expected tranche amortization derivatives
    /// </summary>
    public UniqueSequence<CurveArray> TrancheAmortDer
    {
      get { return trancheAmortDer_; }
    }

    #endregion Original Basket

    #endregion // Properties

    #region Data

    // basic dates
    private Dt portfolioStart_;
    private Dt asOf_;
    private Dt settle_;
    private Dt maturity_;

    // original basket data
    private CreditPool originalBasket_;
    private CorrelationObject originalCorrelation_;

    //--------------------------------------------------------------------
    //-
    // Survival curves and principals
    private double[] principals_;
    private SurvivalCurve[] survivalCurves_;

    //--------------------------------------------------------------------
    //-
    // Early maturities, refinance and counterpaty data
    private Dt[] maturities_; // early maturity dates by names
    private SurvivalCurve[] refinanceCurves_; // refinance curves
    private double[] refinanceCorrelations_;  // refinance correlations
    private SurvivalCurve counterpartyCurve_;
    private double counterpartyCorrelation_;

    //--------------------------------------------------------------------
    //-
    // Recoveries
    //  (1) If hasFixedRecovery_ == true, recoveryCurves_ are user inputs
    //  (2) Otherwise, it is calculated from the survival curves
    //    (2a) If recoveryCurves_ == null, they are outdated
    private bool hasFixedRecovery_;
    private RecoveryCorrelationModel rcmodel_ = RecoveryCorrelationModel.Default;
    private RecoveryCurve[] recoveryCurves_;

    //--------------------------------------------------------------------
    //
    // correlation data
    private Copula copula_;
    private CorrelationObject correlation_;

    // internally cached data
    private CorrelationTermStruct tsCorrelation_ = null;

    //--------------------------------------------------------------------
    //-
    // time grid
    private int stepSize_;
    private TimeUnit stepUnit_;
    private UniqueSequence<Dt> additionalGridDates_ = null;
    //private bool timeGridChanged_ = true;
    private UniqueSequence<Dt> timeGrid_;  // Internally computed

    //---------------------------------------------------------------------
    //-
    // loss and amortization grid
    private UniqueSequence<double> rawLossLevels_; // User input
    private bool addComplement_;                   // User input
    //private double[] lossLevels_;       // Internally computed
    internal UniqueSequence<double> cookedLossLevels_; // Internally computed

    //--------------------------------------------------------------------------
    //Repositories for semianalytic sensitivities
    [Mutable]
    private UniqueSequence<double> rawDetachments_;
    [Mutable]
    private UniqueSequence<CurveArray> trancheLossDer_;
    [Mutable]
    private UniqueSequence<CurveArray> trancheAmortDer_;
    [Mutable]
    private bool recomputeDerivatives_ = true;

    //-------------------------------------------------------------------------




    // numerical options
    private int integrationPointsFirst_;
    private int integrationPointsSecond_;
    private double accuracyLevels_;
    private double gridSize_;
    private int sampleSize_;
    private bool usedQuasiRng_;
    private int effectiveDigits_ = 15; // to avoid rounding error in discrete loss distribution

    // internally computed values
    private double[] recoveryRates_ = null;
    private double[] recoveryDispersions_ = null;

    private string[] names_ = null; // credit names

    private double averageRecoveryRate_;

    private double totalPrincipal_;
    private double defaultedPrincipal_; // total principal of defaulted credits
    private double shorted_; // principal of short names as share of total principal
    private double prevLoss_; // losses from the defaults before settle date
    private double prevAmor_; // losses from the defaults before settle date
    private bool noAmortize_ = false;
    private Baskets.BasketDefaultInfo defaultInfo_;
    private Baskets.BasketDefaultInfo savedDefaultInfo_;

    private int recalcStartDateIndex_ = -1;

    // internal data
    private bool isUnique_ = false; // this basket is owned by a single CDO pricer
    #endregion Data
  } // class BasketPricer

}