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
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Pricers.BasketForNtdPricers
{

  ///
  /// <summary>
  ///   Compute Ntd loss and survival by numerical integration
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public class SemiAnalyticBasketForNtdPricer : BasketForNtdPricer
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(SemiAnalyticBasketForNtdPricer));

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
    public SemiAnalyticBasketForNtdPricer(
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
      IntegrationPointsFirst = 25;
      IntegrationPointsSecond = 9;
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
      // Invalid Integration Points Second
      if (IntegrationPointsSecond <= 0)
        InvalidValue.AddError(errors, this, "IntegrationPointsSecond",
          String.Format("IntegrationPointsSecond is not positive"));
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SemiAnalyticBasketForNtdPricer)base.Clone();
      obj.IntegrationPointsFirst = IntegrationPointsFirst;
      obj.IntegrationPointsSecond = IntegrationPointsSecond;
      return obj;
    }

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
      // init probability
      if (probDistribution_ == null)
        ComputeAndSaveDistribution();
      // make curve
      var curve = new SurvivalCurve(AsOf);
      var distribution = probDistribution_;
      int nDates = distribution.NumDates();
      int level = nth - PrevDefaults - 1;
      for (int i = 0; i < nDates; ++i)
        curve.Add(distribution.GetDate(i), GetProbability(distribution, i, level));
      return curve;
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
      if (probDistribution_ == null)
        ComputeAndSaveDistribution();
      var curve = new Curve(AsOf);
      Curve lossGivenNthDefaultCurve;
      int level = nth - PrevDefaults - 1;
      // Check if the basket is homogeneous. 
      // If false, use new SemiAnalyticNtdModel 
      double[] lossGivenDefault;
      if (!CheckHomogeneous(out lossGivenDefault))
      {
        lossGivenNthDefaultCurve = new Curve(AsOf,
          InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const,
            0.0, Double.MaxValue), DayCount.None, Frequency.Continuous);
        var dts = CreateDateArray(AsOf); //is this right??
        foreach (var dt in dts)
          lossGivenNthDefaultCurve.Add(dt, 0.0);
        FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation(Correlation);
        SemiAnalyticNtdModel.LossGivenNthDefault(nth - PrevDefaults,
          CopulaType, DfCommon, DfIdiosyncratic, Copula.Data,
          corr.Correlations, new int[1], IntegrationPointsFirst,
          0.0, survivalCurves_, lossGivenDefault, lossGivenNthDefaultCurve);
        for (int i = 0; i < probDistribution_.NumDates(); ++i)
        {
          Dt date = probDistribution_.GetDate(i);
          double defaultProb = 1.0 - GetProbability(probDistribution_, i, level);
          curve.Add(date, lossGivenNthDefaultCurve.Interpolate(date) * defaultProb);
        }
        return curve;
      }
      for (int i = 0; i < probDistribution_.NumDates(); ++i)
      {
        double defaultProb = 1 - GetProbability(probDistribution_, i, level);
        Dt date = probDistribution_.GetDate(i);
        double lossRate = DefaultLossRate(Settle, date);
        curve.Add(date, lossRate * defaultProb);
      }
      return curve;
    }

    internal Func<Dt, double> GetLossGivenNthDefaultFn(int nth)
    {
      UpdateCurves();
      if (PrevDefaults >= nth)
        return DefaultedLossCurve(nth).Interpolate;
      if (probDistribution_ == null)
        ComputeAndSaveDistribution();
      Curve lossGivenNthDefaultCurve;
      // Check if the basket is homogeneous. 
      // If false, use new SemiAnalyticNtdModel 
      double[] lossGivenDefault;
      if (!CheckHomogeneous(out lossGivenDefault))
      {
        lossGivenNthDefaultCurve = new Curve(AsOf,
          InterpFactory.FromMethod(InterpMethod.Linear, ExtrapMethod.Const,
            0.0, Double.MaxValue), DayCount.None, Frequency.Continuous);
        var dts = CreateDateArray(AsOf); //is this right??
        foreach (var dt in dts)
          lossGivenNthDefaultCurve.Add(dt, 0.0);
        FactorCorrelation corr = CorrelationFactory.CreateFactorCorrelation(Correlation);
        SemiAnalyticNtdModel.LossGivenNthDefault(nth - PrevDefaults,
          CopulaType, DfCommon, DfIdiosyncratic, Copula.Data,
          corr.Correlations, new int[1], IntegrationPointsFirst,
          0.0, survivalCurves_, lossGivenDefault, lossGivenNthDefaultCurve);
        return lossGivenNthDefaultCurve.Interpolate;
      }
      var settle = Settle;
      return dt => DefaultLossRate(settle, dt);
    }

    /// <summary>
    ///   Compute the loss rate on default
    /// </summary>
    ///
    /// <param name="start">Period start date</param>
    /// <param name="end">Period end date</param>
    private double DefaultLossRate(Dt start, Dt end)
    {
      if (Dt.Cmp(start, end) >= 0)
        return 0.0;
      UpdateCurves();
      if (survivalCurves_.Length == 0)
        return 0.0;
      var sc = survivalCurves_;
      var notionals = principals_;
      var recoveries = RecoveryRates;
      double totalLoss = 0;
      double totalDefault = 0;
      int basketSize = sc.Length;
      for (int i = 0; i < basketSize; ++i)
      {
        // default probability
        double p = 1 - sc[i].Interpolate(start, end);
        if (p < 0)
          p = 0;
        else if (p > 1)
          p = 1;
        // loss
        double dflt = p * notionals[i];
        totalDefault += dflt;
        double loss = dflt * (1 - recoveries[i]);
        totalLoss += loss;
      }
      if (totalLoss < 1.0E-14)
        return 0.0; // in case totalDefault = 0
      return totalLoss / totalDefault;
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

    /// <summary>
    ///   Compute the whole distribution, save the result for later use
    /// </summary>
    private void ComputeAndSaveDistribution()
    {
      int basketSize = survivalCurves_.Length;
      if (basketSize == 0)
      {
        // All names defaulted.
        probDistribution_ = AllDefaultedDistribution(Settle);
        return;
      }
      var lossLevels = new double[basketSize + 1];
      for (int i = 0; i <= basketSize; ++i)
        lossLevels[i] = ((double)i) / basketSize;
      var corr = CorrelationFactory.CreateFactorCorrelation(Correlation);
      probDistribution_ = new Curve2D();
      HomogeneousBasketModel.ComputeDistributions(true,
        Settle, Maturity, StepSize, StepUnit,
        CopulaType, DfCommon, DfIdiosyncratic,
        corr.Correlations, new int[1], IntegrationPointsFirst,
        IntegrationPointsSecond, survivalCurves_, lossLevels,
        probDistribution_);
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
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Number of integration points (read only)
    /// </summary>
    public int IntegrationPointsFirst { get; set; }

    /// <summary>
    ///   Number of integration points (read only)
    /// </summary>
    public int IntegrationPointsSecond { get; set; }

    #endregion

    #region Data

    private Curve2D probDistribution_;

    #endregion
  } // class BasketForNtdPricer
}