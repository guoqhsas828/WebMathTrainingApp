//
//  -2012. All rights reserved.
//

using System;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Vanilla FX option pricer using Black-Scholes.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FxOption" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="FxOptionPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.FxOption"/>
  /// <seealso cref="FxOptionPricerBase"/>
  [Serializable]
  public class FxOptionVanillaPricer : FxOptionPricerBase, IFxOptionPricer
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FxOptionVanillaPricer"/> class.
    /// </summary>
    /// <param name="fxOption">The fx option.</param>
    /// <param name="asOf">The pricing date.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="foreignRateCurve">The foreign rate curve.</param>
    /// <param name="fxCurve">The FX Curve</param>
    /// <param name="volatilitySurface">The volatility surface.</param>
    public FxOptionVanillaPricer(
      FxOption fxOption, 
      Dt asOf, 
      Dt settle, 
      DiscountCurve discountCurve, 
      DiscountCurve foreignRateCurve, 
      FxCurve fxCurve,
      CalibratedVolatilitySurface volatilitySurface) 
      : base(fxOption, asOf, settle, discountCurve, foreignRateCurve,
        fxCurve, volatilitySurface, SmileAdjustmentMethod.OverHedgeCosts)
    {
      // Init calcs
      isCalculated_ = false;
      pv_ = delta_ = vega_ = vanna_ = volga_ = 0;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Constructs the Black volatility curve.
    /// </summary>
    /// <returns>
    ///   <see cref="VolatilityCurve"/>
    /// </returns>
    /// <remarks>
    /// This function is called in the property getter to construct
    /// a volatility curve if the current one is null.
    /// The derived class should implement their own version of
    /// this function to construct a proper volatility curve.
    /// </remarks>
    protected override VolatilityCurve ConstructVolatilityCurve()
    {
      var surface = VolatilitySurface;
      if (SmileAdjustment == SmileAdjustmentMethod.NoAdjusment || (
        FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions &&
        SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation))
      {
        return surface.GetAtmVolatilityCurve();
      }

      var curve = new VolatilityCurve(surface.AsOf);
      foreach(var date in surface.GetTenorDates())
      {
        var volAtStrike = surface.Interpolate(date, FxOption.Strike);
        curve.Add(date, volAtStrike);
      }
      return curve;
    }

    /// <summary>
    /// Performs the calculations.
    /// </summary>
    private void Calculate()
    {
      if (isCalculated_)
        return;

      Dt expiration = FxOption.Maturity;

      if (expiration < AsOf)
      {
        // The option has already exercised.
        pv_ = delta_ = gamma_ = vega_ = vanna_ = volga_ = 0;
      } 
      
      var input = GetRatesSigmaTime(expiration, null);
      vol_ = input.Sigma;

      // Call model
      if (FxOption.IsRegular)
      {
        double theta = 0, rho = 0, lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, charm = 0.0, speed = 0.0, zomma = 0.0,
          color = 0.0, dualDelta = 0.0, dualGamma = 0.0;
        pv_ = BlackScholes.P(FxOption.Style, FxOption.Type, input.T,
          SpotFxRate, FxOption.Strike, input.Rd, input.Rf, input.Sigma,
          ref delta_, ref gamma_, ref theta, ref vega_, ref rho,
          ref lambda, ref gearing, ref strikeGearing, ref vanna_, ref charm, ref speed, ref zomma, ref color, ref volga_, ref dualDelta, ref dualGamma);
      }
      else
      {
        pv_ = DigitalOption.P(FxOption.Style, FxOption.Type, OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
          input.Rd, input.Rf, input.Sigma, 1.0);
        delta_ = DigitalOption.Delta(FxOption.Style, FxOption.Type, OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
          input.Rd, input.Rf, input.Sigma, 1.0);
        gamma_ = DigitalOption.Gamma(FxOption.Style, FxOption.Type, OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
          input.Rd, input.Rf, input.Sigma, 1.0);
        vega_ = DigitalOption.Vega(FxOption.Style, FxOption.Type, OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
          input.Rd, input.Rf, input.Sigma, 1.0);
        vanna_ = DigitalOption.Vanna(FxOption.Style, FxOption.Type, OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
          input.Rd, input.Rf, input.Sigma, 1.0);
        volga_ = DigitalOption.Volga(FxOption.Style, FxOption.Type, OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
          input.Rd, input.Rf, input.Sigma, 1.0);
      }

      if (PremiumInBaseCcy)
      {
        var spot = SpotFxRate;
        pv_ /= spot;
        gamma_ /= spot;
        vega_ /= spot;
        volga_ /= spot;
        delta_ = (delta_ - pv_) / spot;
        vanna_ = (vanna_ - vega_) / spot;
      }

      // Adjust to the settle date value
      double settleDf = SettleDiscountFactor();
      pv_ /= settleDf;
      delta_ /= settleDf;
      gamma_ /= settleDf;
      vega_ /= settleDf;
      vanna_ /= settleDf;
      volga_ /= settleDf;

      // Done
      isCalculated_ = true;
    }

    /// <summary>
    /// Net present value on the settle date of the option, excluding the value
    /// of any additional payment.
    /// </summary>
    /// <returns>Pv</returns>
    public override double ProductPv()
    {
      if (ExplicitOverHedge)
        return UnitPrice()*CurrentNotional;

      if (!isCalculated_)
        Calculate();
      return pv_*CurrentNotional;
    }

    /// <summary>
    /// Calculates the black-scholes delta.
    /// </summary>
    /// <remarks>
    /// <para>Delta is the change in <see cref="ProductPv"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point of change. Thus, the Delta function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="ProductPv"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model.</para>
    /// </remarks>
    /// <returns>Option delta</returns>
    public double Delta()
    {
      if (ExplicitOverHedge)
        return UnitDelta()*CurrentNotional;
      if (!isCalculated_)
        Calculate();
      return delta_*EffectiveNotional*0.0001;
    }

    /// <summary>
    /// Calculates the black-scholes gamma.
    /// </summary>
    /// <remarks>
    /// <para>Gamma is the change in <see cref="Delta"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point of change. Thus, the Gamma function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Delta"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model.</para> 
    /// </remarks>
    /// <returns>Option gamma</returns>
    public double Gamma()
    {
      if (ExplicitOverHedge)
        return UnitGamma()*CurrentNotional;
      if (!isCalculated_)
        Calculate();
      return gamma_*EffectiveNotional*0.0001*0.0001;
    }

    /// <summary>
    /// Calculates the black-scholes vega.
    /// </summary>
    /// <remarks>
    /// <para>Vega is the change in <see cref="ProductPv"/> of the <see cref="FxOption"/> given a change in the volatility. 
    /// This derivative is first calculated and then scaled to be per volatility (1%). Thus, the Vega function's 
    /// value can be multiplied by a projected change in the volatility (in percentage terms) to estimate the change in 
    /// <see cref="ProductPv"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model.</para> 
    /// </remarks>
    /// <returns>Option vega</returns>
    public double Vega()
    {
      if (ExplicitOverHedge)
        return UnitVega()*CurrentNotional;
      if (!isCalculated_)
        Calculate();
      return vega_ * EffectiveNotional * 0.01;
    }

    /// <summary>
    /// Calculates the black-scholes vanna.
    /// </summary>
    /// <remarks>
    /// <para>Vanna is the change in <see cref="Vega"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point. Thus, the Vanna function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Vega"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes"/> model.</para> 
    /// </remarks>
    /// <returns>Option vanna</returns>
    public double Vanna()
    {
      if (ExplicitOverHedge)
        return UnitVanna()*CurrentNotional;
      if (!isCalculated_)
        Calculate();
      return vanna_*EffectiveNotional*0.0001*0.01;
    }

    /// <summary>
    /// Calculates the black-scholes volga.
    /// </summary>
    /// <remarks>
    /// <para>Volga is the change in <see cref="Vega"/> of the <see cref="FxOption"/> given a change in the volatility. 
    /// This derivative is first calculated and then scaled to be per volatility (1%). Thus, the Volga function's 
    /// value can be multiplied by a projected change in the volatility (in percentage terms) to estimate the change in 
    /// <see cref="Vega"/>.</para>
    /// <para>Vanilla <see cref="FxOption">FxOptions</see> calculate the derivative using the analytic formula derived in 
    /// the <see cref="BlackScholes" /> model.</para> 
    /// </remarks>
    /// <returns>Option volga</returns>
    public double Volga()
    {
      if (ExplicitOverHedge)
        return UnitVolga()*CurrentNotional;
      if (!isCalculated_)
        Calculate();
      return volga_*EffectiveNotional*0.01*0.01;
    }

    /// <summary>
    /// Calculates the Theta.
    /// </summary>
    /// <remarks>
    /// <para>Theta is the change in <see cref="PricerBase.Pv"/> of the <see cref="FxOption"/> given a change in time.</para>
    /// </remarks>
    /// <seealso cref="Sensitivities.Rolldown(IPricer, bool)"/>
    /// <returns>Option theta</returns>
    public double Theta()
    {
      if (!theta_.HasValue)
        theta_ = Sensitivities.Rolldown(this, false);
      return theta_.Value;
    }

    /// <summary>
    /// Calculates volatility implied by a given price.
    /// </summary>
    /// <returns>Implied volatility</returns>
    public double ImpliedVolatility(double price)
    {
      price *= SettleDiscountFactor();
      if (PremiumInBaseCcy)
      {
        price *= SpotFxRate;
      }
      Dt expiration = FxOption.Maturity;
      var input = GetRatesSigmaTime(expiration, null);
      if (FxOption.IsRegular)
      {
        return BlackScholes.ImpliedVolatility(FxOption.Style, FxOption.Type,
          input.T, SpotFxRate, FxOption.Strike, input.Rd, input.Rf, price);
      }
      return DigitalOption.ImpliedVolatility(FxOption.Style, FxOption.Type,
        OptionDigitalType.Cash, input.T, SpotFxRate, FxOption.Strike,
        input.Rd, input.Rf, price);
    }

    /// <summary>
    /// Calculates the single flat volatility for an Fx Option.
    /// </summary>
    /// <returns>Calculated flat volatility</returns>
    double IFxOptionPricer.FlatVolatility()
    {
      if (!isCalculated_)
        Calculate();
      return vol_;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    public override void Reset()
    {
      // Reset others
      isCalculated_ = false;
      pv_ = delta_ = vega_ = vanna_ = volga_ = vol_ = 0;
      theta_ = null;

      // base
      base.Reset();
    }

    /// <summary>
    /// Reset the pricer
    /// <preliminary/>
    /// </summary>
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <exclude/>
    public override void Reset(ResetAction what)
    {
      // Reset others
      isCalculated_ = false;
      pv_ = delta_ = vega_ = vanna_ = volga_ = vol_ = 0;
      theta_ = null;

      // Base
      base.Reset(what);
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      var option = FxOption;

      // No barrier pricing
      if(option.IsBarrier)
        InvalidValue.AddError(errors, this, "FxOption", "Barrier options cannot be priced with the FxOptionVanillaPricer");

      // Base
      base.Validate(errors);
    }

    [Obsolete("Replaced by CalculateUnitPrice")]
    internal static double CaculateOptionValue(
      FxOption option,
      Dt settle, double spotFx,
      DiscountCurve domesticCurve, FxCurve fxCurve,
      Curve volCurve, VannaVolgaCoefficients coefs)
    {
      Debug.Assert(option != null);
      Debug.Assert(option.Style == OptionStyle.European);
      Debug.Assert(option.Type != OptionType.None);
      Debug.Assert(option.IsRegular || option.IsDigital);

      var regular = option.IsRegular;
      var type = option.Type;
      var style = option.Style;
      var strike = option.Strike;
      var maturity = option.Maturity;
      var T = (maturity - settle)/365.0;
      var domesticDf = domesticCurve.DiscountFactor(settle, maturity);
      var fxFactor = FxCurveSet.FxFactor(fxCurve, settle, maturity);
      double factor;
      if (domesticCurve.Ccy != fxCurve.SpotFxRate.ToCcy)
      {
        var dc = fxCurve.Ccy2DiscountCurve;
        if (dc != null) domesticDf = dc.DiscountFactor(settle, maturity);
        factor = 1/spotFx;
      }
      else
      {
        factor = 1;
      }
      var foreignDf = domesticDf*fxFactor;
      var rf = -Math.Log(foreignDf) / T;
      var rd = -Math.Log(domesticDf)/T;
      var sigma = volCurve.CalculateAverageVolatility(settle, maturity);

      double price = 0, vega = 0, vanna = 0, volga = 0;
      if (regular)
      {
        price = BlackScholes.P(style, type, T, spotFx, strike, rd, rf, sigma);
        if (coefs == null) return price*factor;

        // Option value with vanna-volga adjustment.
        vega = BlackScholes.Vega(style, type, T, spotFx, strike, rd, rf, sigma);
        vanna = BlackScholes.Vanna(style, type, T, spotFx, strike, rd, rf, sigma);
        volga = BlackScholes.Vomma(style, type, T, spotFx, strike, rd, rf, sigma);
      }
      else
      {
        price = DigitalOption.P(style, type, OptionDigitalType.Cash,
          T, spotFx, strike, rd, rf, sigma, 1.0);
        if (coefs == null) return price*factor;

        // Option value with vanna-volga adjustment.
        if (coefs.Vega != 0)
        {
          vega = DigitalOption.Vega(style, type, OptionDigitalType.Cash, T, spotFx, strike, rd, rf, sigma, 1.0);
        }
        vanna = DigitalOption.Vanna(style, type, OptionDigitalType.Cash, T, spotFx, strike, rd, rf, sigma, 1.0);
        volga = DigitalOption.Volga(style, type, OptionDigitalType.Cash, T, spotFx, strike, rd, rf, sigma, 1.0);
      }
      price += vega*coefs.Vega + vanna*coefs.Vanna + volga*coefs.Volga;
      return price*factor;
    }

    #endregion

    #region Data

    // Calculated values
    private bool isCalculated_;
    private double pv_, delta_, gamma_, vega_, vanna_, volga_, vol_;
    private double? theta_;

    #endregion

    #region Vanna-volga calculation

    internal static double CalculateUnitPrice(
      FxOptionPricerBase pricer, FxOption option, VannaVolgaCoefficients coefs)
    {
      var regular = option.IsRegular;
      var type = option.Type;
      var style = option.Style;
      var strike = option.Strike;
      var expiration = option.Maturity;
      var spotFx = pricer.SpotFxRate;
      var input = pricer.GetRatesSigmaTime(expiration, null);
      double T = input.T, rd = input.Rd, rf = input.Rf, sigma = input.Sigma;
      double price = 0;
      if (regular)
      {
        price = BlackScholes.P(style, type, T, spotFx, strike, rd, rf, sigma);
        if (coefs != null)
        {
          // Option value with vanna-volga adjustment.
          var vega = BlackScholes.Vega(style, type, T, spotFx, strike, rd, rf, sigma);
          var vanna = BlackScholes.Vanna(style, type, T, spotFx, strike, rd, rf, sigma);
          var volga = BlackScholes.Vomma(style, type, T, spotFx, strike, rd, rf, sigma);
          price += vega*coefs.Vega + vanna*coefs.Vanna + volga*coefs.Volga;
        }
      }
      else
      {
        price = DigitalOption.P(style, type, OptionDigitalType.Cash,
          T, spotFx, strike, rd, rf, sigma, 1.0);
        if (coefs != null)
        {
          const OptionDigitalType cash = OptionDigitalType.Cash;
          // Option value with vanna-volga adjustment.
          var vega = coefs.Vega.AlmostEquals(0.0) ? 0.0
            : DigitalOption.Vega(style, type, cash, T, spotFx, strike, rd, rf, sigma, 1.0);
          var vanna = DigitalOption.Vanna(style, type, cash, T, spotFx, strike, rd, rf, sigma, 1.0);
          var volga = DigitalOption.Volga(style, type, cash, T, spotFx, strike, rd, rf, sigma, 1.0);
          price += vega*coefs.Vega + vanna*coefs.Vanna + volga*coefs.Volga;
        }
      }

      if (pricer.PremiumInBaseCcy)
      {
        price /= spotFx;
      }

      return price;
    }

    private double UnitPrice()
    {
      if (FxOption.Maturity < AsOf)
      {
        // The option has already exercised.
        return 0;
      }

      return CalculateUnitPrice(this, FxOption,
        GetOverhedgeCoefs(VannaVolgaTarget.Price))/SettleDiscountFactor();
    }

    private double UnitDelta()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Delta, p => p.Delta());
      }
      return FxOptionPricerFactory.UnitDelta(this, UnitPrice);
    }

    private double UnitGamma()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Gamma, p => p.Gamma());
      }
      return FxOptionPricerFactory.UnitGamma(this, UnitPrice);
    }

    private double UnitVega()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Vega, p => p.Vega());
      }
      return FxOptionPricerFactory.UnitVega(this, UnitPrice);
    }

    private double UnitVanna()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Vanna, p => p.Vanna());
      }
      return FxOptionPricerFactory.UnitVanna(this, UnitPrice);
    }

    private double UnitVolga()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Volga, p => p.Volga());
      }
      return FxOptionPricerFactory.UnitVolga(this, UnitPrice);
    }

    private double OverHedgeAdjustment(VannaVolgaTarget target,
      Func<FxOptionVanillaPricer, double> bsFn)
    {
      var coefs = GetOverhedgeCoefs(target);
      if (coefs == null) return 0.0;
      return OverHedgeAdjustment(coefs, bsFn);
    }

    private double OverHedgeAdjustment(VannaVolgaCoefficients coefs,
      Func<FxOptionVanillaPricer, double> bsFn)
    {
      Debug.Assert(coefs != null);
      var pricer = (FxOptionVanillaPricer) ShallowCopy();
      pricer.Notional = 1.0;
      pricer.SmileAdjustment = SmileAdjustmentMethod.NoAdjusment;
      var bs = bsFn?.Invoke(pricer) ?? 0.0;

      var vega = coefs.Vega.AlmostEquals(0.0) ? 0.0 : pricer.Vega();
      var vanna = pricer.Vanna();

      if (DomesticDiscountCurve.Ccy != FxCurve.SpotFxRate.ToCcy)
      {
        vanna += vega/SpotFxRate;
      }

      var volga = pricer.Volga();
      var costs = coefs.Vega*vega + coefs.Vanna*vanna + coefs.Volga*volga;
      return costs + bs;
    }

    private bool ExplicitOverHedge =>
      SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation
      && SmileAdjustment != SmileAdjustmentMethod.NoAdjusment
      && FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions;

    #endregion
  }
}
