/*
 * MonteCarloBasketForNtdPricer.cs
 *
 *
 */

using System;
using System.Collections;
using BaseEntity.Toolkit.Numerics.Rng;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers.BasketForNtdPricers
{
 
  ///
  /// <summary>
  ///   Compute Ntd loss and survival by Monte Carlo simulation
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public class MonteCarloBasketForNtdPricer : BasketForNtdPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MonteCarloBasketForNtdPricer));

    #region Constructors

    /// <summary>
    ///   constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="copula">Copula for the correlation structure</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    ///
    public MonteCarloBasketForNtdPricer(
                               Dt asOf,
                               Dt settle,
                               Dt maturity,
                               SurvivalCurve[] survivalCurves,
                               RecoveryCurve[] recoveryCurves,
                               double[] principals,
                               Copula copula,
                               Correlation correlation,
                               int stepSize,
                               TimeUnit stepUnit)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals, copula, correlation, stepSize, stepUnit)
    {
      UseQuasiRng = false;
      SampleSize = 10000;
      Seed = 0;
      probDistribution_ = null;
      lossDistribution_ = null;
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (MonteCarloBasketForNtdPricer) base.Clone();
      obj.UseQuasiRng = UseQuasiRng;
      obj.SampleSize = SampleSize;
      obj.Seed = Seed;
      return obj;
    }
    #endregion // Constructors

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
      // Sample Size
      if (SampleSize < 0.0)
        InvalidValue.AddError(errors, this, "SampleSize", String.Format("Sample Size is not positive"));
    }
    #endregion

    #region Methods
    ///
    /// <summary>
    ///   Compute the survival curve for the nth default
    /// </summary>
    ///
    /// <remarks>
    ///  The survival probability is defined as one minus the probability
    ///  that the nth default occurs.
    /// </remarks>
    ///
    /// <param name="nth">The index of default</param>
    ///
    public override SurvivalCurve NthSurvivalCurve(int nth)
    {
      UpdateCurves();
      if (PrevDefaults >= nth)
        return DefaultedSurvivalCurve(nth);
      return NthSurvivalCurveMonteCarlo(nth - PrevDefaults);
    }

    ///
    /// <summary>
    ///   Compute the expected loss curve for the nth default
    /// </summary>
    ///
    /// <remarks>
    ///   This curve represents the expected cumulative losses over time.
    /// </remarks>
    ///
    /// <param name="nth">The index of default</param>
    /// 
    public override Curve NthLossCurve(int nth)
    {
      UpdateCurves();
      if (PrevDefaults >= nth)
        return DefaultedLossCurve(nth);
      return NthLossCurveMonteCarlo(nth - PrevDefaults);
    }

    ///
    /// <summary>
    ///   Reset the pricer
    /// </summary>
    ///
    /// <remarks>
    ///   This method reset the basket pricer  such that in the next request
    ///   for NthSurvivalCurve() or NthLossCurve(), it will recompute everything.
    /// </remarks>
    ///
    public override void Reset()
    {
      base.Reset();
      probDistribution_ = null;
      lossDistribution_ = null;
    }

    /// <summary>
    ///   Construct the nth survival curve
    /// </summary>
    private SurvivalCurve NthSurvivalCurveMonteCarlo(int nth)
    {
      if (null == probDistribution_)
        ComputeDistributionMonteCarlo();
      // make curve
      var curve = new SurvivalCurve(AsOf);
      var distribution = probDistribution_;
      int nDates = distribution.NumDates();
      for (int i = 0; i < nDates; ++i)
        curve.Add(distribution.GetDate(i), 1 - GetProbability(distribution, i, nth));
      return curve;
    }


    /// <summary>
    ///   Construct the nth recovery curve
    /// </summary>
    private Curve NthLossCurveMonteCarlo(int nth)
    {
      if (null == lossDistribution_)
        ComputeDistributionMonteCarlo();
      // make curve
      double principal = TotalPrincipal/Count;
      var curve = new Curve(AsOf);
      var distribution = lossDistribution_;
      int nDates = distribution.NumDates();
      for (int i = 0; i < nDates; ++i)
        curve.Add(distribution.GetDate(i), distribution.GetValue(i, nth)/principal);
      return curve;
    }

    /// <summary>
    ///    Get survival probability
    /// </summary>
    /// <param name="distribution">Curve2D</param>
    /// <param name="dateIdx">date index</param>
    /// <param name="levelIdx">level index</param>
    /// <returns></returns>
    private static double GetProbability(Curve2D distribution, int dateIdx, int levelIdx)
    {
      const double tiny = 1.0E-14;
      double p = distribution.GetValue(dateIdx, levelIdx);
      if (p >= 1)
        p = 1.0 - tiny;
      else if (p <= 0)
        p = tiny;
      return p;
    }

    private static Curve2D AllDefaultedDistribution(Dt settle)
    {
      Curve2D distribution = new Curve2D(settle);
      distribution.Initialize(1, 2);
      distribution.SetDate(0, settle);
      distribution.SetLevel(0, 0.0);
      distribution.SetLevel(1, 1.0);
      distribution.SetValue(0, 0.0);
      distribution.SetValue(1, 1.0);
      return distribution;
    }

    /// <summary>
    ///   Compute the whole distribution, save the result for later use
    /// </summary>
    private void ComputeDistributionMonteCarlo()
    {
      int basketSize = survivalCurves_.Length;
      if (basketSize == 0)
      {
        // All names defaulted.
        lossDistribution_ = AllDefaultedDistribution(Settle);
        probDistribution_ = AllDefaultedDistribution(Settle);
        return;
      }
      InitializeDistributionMonteCarlo();
      var recoveryRates = RecoveryRates;
      var recoveryDispersions = RecoveryDispersions;
      GeneralCorrelation corr = CorrelationFactory.CreateGeneralCorrelation(Correlation);
      // determine the seed
      int seed = Seed;
      if (seed < 0)
      {
        seed = (int) RandomNumberGenerator.RandomSeed;
        if (seed < 0)
          seed = -seed;
      }
      // Run simulation
      MonteCarloBasketModel.ComputeNtdDistributions(
        CopulaType, DfCommon, DfIdiosyncratic,
        corr.Correlations, new int[1], survivalCurves_, principals_, recoveryRates, recoveryDispersions,
        SampleSize, UseQuasiRng, seed, lossDistribution_, probDistribution_);
    }

    /// <summary>
    ///   Initialize the distribution
    /// </summary>
    private void InitializeDistributionMonteCarlo()
    {
      var dates = CreateDateArray(Settle);
      int nDates = dates.Length;
      int nLevels = survivalCurves_.Length + 1;
      lossDistribution_ = new Curve2D();
      probDistribution_ = new Curve2D();
      lossDistribution_.Initialize(nDates, nLevels, 1);
      probDistribution_.Initialize(nDates, nLevels, 1);
      lossDistribution_.SetAsOf(Settle);
      probDistribution_.SetAsOf(Settle);
      for (int i = 0; i < nDates; ++i)
      {
        lossDistribution_.SetDate(i, dates[i]);
        probDistribution_.SetDate(i, dates[i]);
      }
      for (int i = 0; i < nLevels; ++i)
      {
        lossDistribution_.SetLevel(i, i);
        probDistribution_.SetLevel(i, i);
      }
    }

    #endregion

    #region Properties
    /// <summary>
    ///   Sample size in simulation
    /// </summary>
    public int SampleSize
    {
      get; set;
    }

    /// <summary>
    ///   Use quasi random numbers if possible
    /// </summary>
    public bool UseQuasiRng
    {
      get; set;
    }

    /// <summary>
    ///   Seed for random number generator
    /// </summary>
    public int Seed
    {
      get; set;
    }

    #endregion // Properties

    #region Data
    private Curve2D lossDistribution_;
    private Curve2D probDistribution_;
    #endregion

  } // class MonteCarloBasketForNtdPricer

}
