/*
 * BasketForNtdPricer.cs
 *
 */

using System;
using System.Collections;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;

namespace BaseEntity.Toolkit.Pricers.BasketForNtdPricers
{
  /// <summary>
  ///   Compute Ntd expected loss and survival on the event of counterparty survival 
  /// </summary>
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  [Serializable]
  public class SemiAnalyticBasketForNtdPricerDynamic : BasketForNtdPricer, IDynamicDistribution  
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(SemiAnalyticBasketForNtdPricerDynamic));

    #region Constructors
    /// <summary>
    ///   constructor
    /// </summary>
    ///
    /// <param name="nth">Nth</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival curves of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    ///
    public SemiAnalyticBasketForNtdPricerDynamic(
                              int nth,
                              Dt asOf,
                              Dt settle,
                              Dt maturity,
                              SurvivalCurve[] survivalCurves,
                              RecoveryCurve[] recoveryCurves,
                              double[] principals,
                              Correlation correlation, 
                              int stepSize,
                              TimeUnit stepUnit)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals, new Copula(), correlation, stepSize, stepUnit)
    {
      DistributionComputed = false;
      N = nth;
      IntegrationPointsFirst = 25;
    }

    #endregion

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
      // Invalid Integration Points First
      if (IntegrationPointsFirst <= 0)
        InvalidValue.AddError(errors, this, "IntegrationPointsFirst",
                              String.Format("IntegrationPointsFirst is not positive"));
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Compute the survival curve for the nth default on the event of counterparty survival
    /// </summary>
    /// <remarks>
    ///  The survival probability is defined as one minus the probability
    ///  that the nth default occurs.
    /// </remarks>
    /// <param name="nth">The index of default</param>
    public override SurvivalCurve NthSurvivalCurve(int nth)
    {
      return nthSurvival_;
    }

    /// <summary>
    ///   Compute the expected loss curve for the nth default on the event of counterparty survival
    /// </summary>
    /// <remarks>
    ///   This curve represents the expected cumulative losses over time.
    /// </remarks>
    /// <param name="nth">The index of default</param>
    public override Curve NthLossCurve(int nth)
    {
      return nthLoss_;
    }

    /// <summary>
    /// Reset
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      DistributionComputed = false;
      conditionalNthLoss_ = null;
      conditionalNthSurvival_ = null;
      nthSurvival_ = null;
      nthLoss_ = null;
      quadPoints_ = null;
      quadWeights_ = null;
    }
    
    /// <summary>
    ///   For internal use only
    ///   <preliminary/>
    /// </summary>
    /// <remarks>
    /// Compute the conditional distribution
    /// </remarks>
    private void ComputeAndSaveDistribution()
   {
     var start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
     var dates = CreateDateArray(start);
     conditionalNthLoss_ = new double[dates.Length,IntegrationPointsFirst];
     conditionalNthSurvival_ = new double[dates.Length,IntegrationPointsFirst];
     nthLoss_ = new Curve(start);
     nthSurvival_ = new SurvivalCurve(start);
     foreach (var dt in dates)
     {
       nthLoss_.Add(dt, 0.0);
       nthSurvival_.Add(dt, 0.0);
     }
     quadPoints_ = new double[IntegrationPointsFirst];
     quadWeights_ = new double[IntegrationPointsFirst];
     var corr = CorrelationFactory.CreateFactorCorrelation(Correlation);
     LossProcess.InitializeNthLoss(AsOf, dates, new int[1], corr.Correlations,
                                   N - PrevDefaults, survivalCurves_, principals_, RecoveryRates, RecoveryDispersions,
                                   quadPoints_, quadWeights_, conditionalNthLoss_,
                                   conditionalNthSurvival_);
     DistributionComputed = true;
   }

    /// <summary>
    /// Condition on <m>z_i</m>
    /// </summary>
    /// <param name="i">Quadrature point index</param>
    void IDynamicDistribution.ConditionOn(int i)
    {
      if (!DistributionComputed)
        ComputeAndSaveDistribution();
      for (int t = 0; t < nthLoss_.Count; ++t)
      {
        nthLoss_.SetVal(t, conditionalNthLoss_[t, i]);
        nthSurvival_.SetVal(t, conditionalNthSurvival_[t, i]);
      }
    }

    /// <summary>
    /// <m>P(L_T > K)</m> where K is detach 
    /// </summary>
    /// <param name="date">Date</param>
    double IDynamicDistribution.ExhaustionProbability(Dt date)
    {
      return 1.0 - nthSurvival_.Interpolate(date);
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SemiAnalyticBasketForNtdPricerDynamic) base.Clone();
      obj.IntegrationPointsFirst = IntegrationPointsFirst;
      if (conditionalNthSurvival_ != null)
        obj.conditionalNthSurvival_ = (double[,]) conditionalNthSurvival_.Clone();
      if (conditionalNthLoss_ != null)
        obj.conditionalNthLoss_ = (double[,]) conditionalNthLoss_.Clone();
      if (nthSurvival_ != null)
        obj.nthSurvival_ = (SurvivalCurve) nthSurvival_.Clone();
      if (nthLoss_ != null)
        obj.nthLoss_ = (Curve) nthLoss_.Clone();
      obj.DistributionComputed = DistributionComputed;
      return obj;
    }

    #endregion

    #region Properties
    /// <summary>
    ///   Number of integration points (read only)
    /// </summary>
    public int IntegrationPointsFirst
    {
      get;
      set;
    }

    /// <summary>
    /// Quadrature weights
    /// </summary>
    double[] IDynamicDistribution.QuadratureWeights { get { return quadWeights_; } }
    /// <summary>
    /// Quadrature points
    /// </summary>
    double[] IDynamicDistribution.QuadraturePoints { get { return quadPoints_; } }
    /// <summary>
    /// Exhaustion level
    /// </summary>
    public int N
    {
      get { return n_; }
      set { n_ = value; Reset(); }
    }

    /// <summary>
    /// Check wheter distribution is computed
    /// </summary>
    public bool DistributionComputed { get; private set; } 
    #endregion

    #region Data
    private int n_;
    private double[] quadPoints_;
    private double[] quadWeights_;
    private double[,] conditionalNthLoss_;
    private double[,] conditionalNthSurvival_;
    private SurvivalCurve nthSurvival_;
    private Curve nthLoss_;
    #endregion

  } // class BasketForNtdPricer
}