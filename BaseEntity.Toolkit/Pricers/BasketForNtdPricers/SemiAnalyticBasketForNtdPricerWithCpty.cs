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
using BaseEntity.Toolkit.Models;
namespace BaseEntity.Toolkit.Pricers.BasketForNtdPricers
{

  ///
  /// <summary>
  ///   Compute Ntd expected loss and survival on the event of counterparty survival 
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public class SemiAnalyticBasketForNtdPricerWithCpty : BasketForNtdPricer
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(SemiAnalyticBasketForNtdPricerWithCpty));

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
    /// <param name="correlation">Correlation data</param>
    /// <param name="cptyCurve">Survival curve of the counterparty</param>
    /// <param name="cptyCorrelation">Factor correlation of the counterparty</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    ///
    public SemiAnalyticBasketForNtdPricerWithCpty(
                              Dt asOf,
                              Dt settle,
                              Dt maturity,
                              SurvivalCurve[] survivalCurves,
                              RecoveryCurve[] recoveryCurves,
                              double[] principals,
                              Correlation correlation,
                              SurvivalCurve cptyCurve,
                              double cptyCorrelation,
                              int stepSize,
                              TimeUnit stepUnit)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals, new Copula(), correlation, stepSize, stepUnit)
    {
      CptyCurve = cptyCurve;
      CptyCorrelation = cptyCorrelation;
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
    ///
    /// <summary>
    ///   Compute the survival curve for the nth default on the event of counterparty survival
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
      // make curve
      var curve = new SurvivalCurve(AsOf);
      var dts = CreateDateArray(Settle);
      foreach (Dt dt in dts)
        curve.Add(dt, 0.0);
      FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation(Correlation);
      double[] lgd;
      CheckHomogeneous(out lgd);
      SemiAnalyticNtdModel.LossGivenNthDefault(nth - PrevDefaults, corr.Correlations, new int[1], IntegrationPointsFirst,
                                               survivalCurves_, lgd, new[] {Math.Sqrt(CptyCorrelation)},
                                               CptyCurve, true, new Curve(AsOf), curve);
      return curve;
    }

    ///
    /// <summary>
    ///   Compute the expected loss curve for the nth default on the event of counterparty survival
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
      var dts = CreateDateArray(Settle);
      var corr = CorrelationFactory.CreateFactorCorrelation(Correlation);
      double[] lgd;
      CheckHomogeneous(out lgd);
      var sc = new Curve(AsOf);
      var c = new Curve(AsOf);
      foreach (Dt dt in dts)
      {
        sc.Add(dt, 0.0);
        c.Add(dt, 0.0);
      }
      SemiAnalyticNtdModel.LossGivenNthDefault(nth - PrevDefaults, corr.Correlations, new int[1], IntegrationPointsFirst,
                                               survivalCurves_, lgd, new[] {Math.Sqrt(CptyCorrelation)}, CptyCurve,
                                               false, c, sc);
      return c;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SemiAnalyticBasketForNtdPricerWithCpty)base.Clone();
      obj.IntegrationPointsFirst = IntegrationPointsFirst;
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
    ///   Counterparty survival curve
    /// </summary>
    public SurvivalCurve CptyCurve
    {
      get; private set;
    }

    /// <summary>
    /// Counterparty correlation
    /// </summary>
    public double CptyCorrelation
    {
      get; private set;
    }
    #endregion
    
  } // class BasketForNtdPricer
}