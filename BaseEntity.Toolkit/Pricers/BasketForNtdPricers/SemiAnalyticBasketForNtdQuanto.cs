/*
 * BasketForNtdPricer.cs
 *
 *
 */

using System;
using System.Collections;
using System.Linq;
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
  ///   Compute Ntd expected loss and survival on the event of counterparty survival 
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public class SemiAnalyticBasketForNtdPricerQuanto : SemiAnalyticBasketForNtdPricer
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(SemiAnalyticBasketForNtdPricerQuanto));

    #region Constructors
    /// <summary>
    ///   constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="discountCurve">The dicount curve for the currency of numeraire process</param>
    /// <param name="survivalCurves">Survival Curve of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="fxCurve">Forward FX curve between baseCcy and numeraireCcy</param>
    /// <param name="fxCorrelation">Correlation between the forward <m>FX_t(T)</m> (from numeraire currency to quote currency) and the latent factor driving defaults under quote currency measure</param>
    /// <param name="fxDevaluation">Jump of forward FX (from numeraire currency to quote currency)<m>\theta FX_{\tau-} = FX_{\tau} - FX_{\tau-}</m> at default times</param>
    /// <param name="atmFxVolatility">At the money forward FX volatility</param>
    /// <param name="fxAtInception">FX rate (from numeraire currency to quote currency) at contract inception</param>
    /// <param name="recoveryCcy">Denomination currency of loss payment</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    /// <remarks>
    /// Let <m>W_t</m> be the Brownian motion driving FX rates,
    /// Default times are driven by the closing random variable of a "systemic" martingale, 
    /// i.e. <m>\tau_i = F^{-1}(\Phi(\rho_i M_\infty + \sqrt{1 - \rho^2 Z_i}))</m> 
    /// where <m>Z_i</m> are independent standard Gaussians
    /// and <m>M_\infty = \int_0^\infty \frac{\sqrt{\theta}}{1 + \theta t} dZ_t</m> with
    /// <m>\langle W,Z\rangle_t = \rho t</m> 
    /// </remarks>
    public SemiAnalyticBasketForNtdPricerQuanto(
                              Dt asOf,
                              Dt settle,
                              Dt maturity,
                              DiscountCurve discountCurve,
                              SurvivalCurve[] survivalCurves,
                              RecoveryCurve[] recoveryCurves,
                              double[] principals,
                              Correlation correlation,
                              FxCurve fxCurve,
                              VolatilityCurve atmFxVolatility,
                              double fxCorrelation,
                              double fxDevaluation,
                              Currency recoveryCcy,
                              double fxAtInception,
                              int stepSize,
                              TimeUnit stepUnit)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals, new Copula(), correlation, stepSize, stepUnit)
    {
      FxCurve = fxCurve;
      FxAtInception = fxAtInception;
      AtmFxVolatility = atmFxVolatility;
      FxCorrelation = fxCorrelation;
      FxDevaluation = fxDevaluation;
      IntegrationPointsFirst = 25;
      DiscountCurve = discountCurve;
      RecoveryCcy = recoveryCcy;
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
      if (NumeraireCurrency == Currency.None)
        InvalidValue.AddError(errors, this, "NumeraireCurrency", "NumeraireCurrency cannot be None");
      else if (NumeraireCurrency != FxCurve.Ccy1 && NumeraireCurrency != FxCurve.Ccy2)
        InvalidValue.AddError(errors, this, "FxCurve",
                              String.Format("FxCurve to/from currency {0} expected", NumeraireCurrency));
      if (IntegrationPointsFirst <= 0)
        InvalidValue.AddError(errors, this, "IntegrationPointsFirst",
                              String.Format("IntegrationPointsFirst is not positive"));
      //Make sure that all underlying survival curves are expressed under the same currency
      var baseCcy = Currency.None;
      foreach (var sc in survivalCurves_)
      {
        if (baseCcy == Currency.None)
          baseCcy = sc.Ccy;
        else if (baseCcy != sc.Ccy)
        {
          InvalidValue.AddError(errors, this, "SurvivalCurves",
                                "All underlying SurvivalCurves should be denominated in the same currency.");
          break;
        }
      }
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
      var sc = GetForeignSurvivalCurve(nth);
      return GetDomesticSurvivalCurve(sc);
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
      var fsc = GetForeignSurvivalCurve(nth);
      var curve = new Curve(AsOf);
      if (fsc == null || fsc.Count == 0)
      {
        curve.Add(Maturity, 0.0);
        return curve;
      }

      var dsc = GetDomesticSurvivalCurve(fsc);
      var lgd = GetLossGivenNthDefaultFn(nth);
      if (RecoveryCcy == NumeraireCurrency)
      {
        double settleSp = dsc.Interpolate(Settle);
        for (int i = 0, n = dsc.Count; i < n; ++i)
        {
          Dt date = dsc.GetDt(i);
          double sp = dsc.GetVal(i) / settleSp;
          double defaultProb = 1.0 - sp;
          double lossRate = lgd(date);
          curve.Add(date, lossRate * defaultProb);
        }
        return curve;
      }
      double? notionalCap = null;
      var contractFx = FxAtInception > 0 ? FxAtInception : (double?)null;
      var cumulative = 0.0;
      var inrementals = fsc.GetIncrementalProtections(dsc, OptionType.Call,
        AsOf, Settle, fsc.Select(p=>p.Date).ToList(), true,
        FxCurve, lgd, notionalCap, contractFx, AtmFxVolatility,
        FxCorrelation, FxDevaluation);
      foreach (var p in inrementals)
      {
        var date = p.Date;
        if (date < AsOf) continue;
        cumulative += (p.DefaultValue - p.OptionValue) / p.DiscountFactor;
        curve.Add(date, cumulative);
      }
      return curve;
    }

    private SurvivalCurve GetForeignSurvivalCurve(int nth)
    {
      var sc = base.NthSurvivalCurve(nth);
      sc.Ccy = RecoveryCcy;
      return sc;
    }

    private SurvivalCurve GetDomesticSurvivalCurve(SurvivalCurve fsc)
    {
      return fsc == null || fsc.Count == 0 ? fsc : fsc.ToDomesticForwardMeasure(
        Maturity, DiscountCurve, AtmFxVolatility, FxCorrelation, FxDevaluation,
        StepSize, StepUnit);
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SemiAnalyticBasketForNtdPricerQuanto)base.Clone();
      obj.DiscountCurve = DiscountCurve;
      obj.FxCurve = FxCurve;
      obj.FxCorrelation = FxCorrelation;
      obj.AtmFxVolatility = AtmFxVolatility;
      obj.RecoveryCcy = RecoveryCcy;
      obj.FxAtInception = FxAtInception;
      return obj;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Numeraire currency
    /// </summary>
    public Currency NumeraireCurrency
    {
      get { return DiscountCurve.Ccy; }
    }

    /// <summary>
    ///   Discount curve.
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Fx curve
    /// </summary>
    public FxCurve FxCurve { get; private set; }

    /// <summary>
    /// Black volatility of ATM Fx forwards
    /// </summary>
    public VolatilityCurve AtmFxVolatility { get; private set; }

    /// <summary>
    /// Fx correlation
    /// </summary>
    public double FxCorrelation { get; set; }

    /// <summary>
    /// Jump of forward FX (from numeraire currency to quote currency)<m>\theta FX_{\tau-} = FX_{\tau} - FX_{\tau-}</m>  at default times
    /// </summary>
    public double FxDevaluation { get; set; }

    /// <summary>
    /// Denomination currency of loss payment 
    /// </summary>
    public Currency RecoveryCcy { get; private set; }

    /// <summary>
    /// FxRate (from quote currency to numeraire currency) at inception
    /// </summary>
    public double FxAtInception { get; private set; }

    #endregion

  } // class BasketForNtdPricer
}
