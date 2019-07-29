//
// FxOptionSingleBarrierPricer.cs
//  -2011. All rights reserved.
//

using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer class for pricing Double barrier options 
  /// </summary>
  /// <remarks>
  /// <para><b>FX Options</b></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FxOption" />
  /// <para><b>Pricing</b></para>
  /// <inheritdoc cref="FxOptionPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.FxOption"/>
  /// <seealso cref="FxOptionPricerBase"/>
  [Serializable]
  public class FxOptionDoubleBarrierPricer : FxOptionPricerBase, IFxOptionPricer
  {
    #region Data

    private Barrier[] adjustedBarriers_;

    private int flags_;

    private bool UseNewModel =>
      (flags_ & TimeDependentBarrierOption.UseOldModel) == 0;

    #endregion

    #region constructors

    /// <summary>
    /// Create a FxOptionDoubleBarrier pricer
    /// </summary>
    /// <param name="barrierOption">The barrier option.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="domesticDiscountCurve">The domestic discount curve.</param>
    /// <param name="foreignDiscountCurve">The foreign discount curve.</param>
    /// <param name="fxCurve">The FX curve.</param>
    /// <param name="surface">The surface.</param>
    /// <param name="smileAdjustment">Smile adjusment method.</param>
    public FxOptionDoubleBarrierPricer(
      FxOption barrierOption,
      Dt asOf,
      Dt settle,
      DiscountCurve domesticDiscountCurve,
      DiscountCurve foreignDiscountCurve,
      FxCurve fxCurve,
      CalibratedVolatilitySurface surface,
      int smileAdjustment)
      : base(barrierOption, asOf, settle, domesticDiscountCurve,
        foreignDiscountCurve, fxCurve, surface,
        (SmileAdjustmentMethod) smileAdjustment)
    {
      //created the adjusted barrier 
      int count = FxOption.Barriers.Count;
      var adjustedBarriers = new Barrier[count];
      double spotFxRate = SpotFxRate;
      for (int i = 0; i < count; i++)
      {
        var barrier = adjustedBarriers[i] =
          (Barrier) FxOption.Barriers[i].Clone();
        barrier.Value = AdjustedBarrier(spotFxRate,
          barrier.Value, barrier.MonitoringFrequency);
        barrier.MonitoringFrequency = Frequency.Continuous;
      }
      adjustedBarriers_ = adjustedBarriers;
      flags_ = 0;
    }


    #endregion

    #region Volatilities

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
      if (SmileAdjustment == SmileAdjustmentMethod.NoAdjusment || (
        FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions &&
        SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation))
      {
        return VolatilitySurface.GetAtmVolatilityCurve();
      }
      return CreateBarrierVolatilityCurve(VolatilitySurface);
    }

    /// <summary>
    /// Creates the barrier volatility curve.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>
    ///   <see cref="VolatilityCurve"/>
    /// </returns>
    private VolatilityCurve CreateBarrierVolatilityCurve(VolatilitySurface surface)
    {
      var isTouch = FxOption.IsTouchOption;
      var curve = new VolatilityCurve(surface.AsOf);

      foreach (var date in surface.GetTenorDates())
      {
        var sigL = surface.Interpolate(date, LowerBarrier.Value);
        var sigU = surface.Interpolate(date, UpperBarrier.Value);
        var sigK = isTouch ? 0 : surface.Interpolate(date, FxOption.Strike);
        var p = DownInProbability(date, sigL, LowerBarrier);
        var q = UpInProbability(date, sigU, UpperBarrier);
        var r = isTouch ? 0 : NoTouchProbability(date, sigK);
        //Debug.Assert(Math.Abs(p + q + r - 1) < 1E-8);

        double vol = InterpolatedVolatility(p, q, r, sigL, sigU, sigK);
        //Debug.Assert(!Double.IsNaN(vol));
        curve.Add(date, vol);
      }
      return curve;
    }

    /// <summary>
    /// Interpolates the volatility.
    /// </summary>
    /// <param name="p">The p.</param>
    /// <param name="q">The q.</param>
    /// <param name="r">The r.</param>
    /// <param name="sigL">The sig L.</param>
    /// <param name="sigU">The sig U.</param>
    /// <param name="sigK">The sig K.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private double InterpolatedVolatility(double p, double q, double r, double sigL, double sigU, double sigK)
    {
      var pq = p + q;
      if (LowerBarrier.BarrierType == OptionBarrierType.DownIn && UpperBarrier.BarrierType == OptionBarrierType.UpIn)
      {
        return pq > 0
          ? (1 - r)*sigK + r*((p/pq)*sigL + (q/pq)*sigU)
          : (1 - r)*sigK;
      }
      else
      {
        return pq > 0
          ? r*sigK + (1 - r)*((p/pq)*sigL + (q/pq)*sigU)
          : r*sigK;
      }
    }

    /// <summary>
    /// Probability of not crossing either barrier.
    /// </summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="volatility">The vol.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private double NoTouchProbability(Dt maturity, double volatility)
    {
      if (IsKnockedIn() || IsKnockedOut())
        return 0.0;

      if (FxOption.Settings.ExactDoubleBarrierProbability)
      {
        var fcs = GetFxCurveSet();
        var input = fcs.GetRatesSigmaTime(AsOf, FxOption.Maturity, null);
        return DoubleBarrierOptionPricer.NoTouchProbability(
          input.T, SpotFxRate, input.Rd, input.Rf,
          LowerBarrier.Value, UpperBarrier.Value, volatility);
      }

      return DoubleBarrierOptionPricer.NoTouchProbability(
        AsOf, maturity, SpotFxRate, LowerBarrier.Value, UpperBarrier.Value,
        new Curve(AsOf, volatility), GetFxCurveSet(), flags_);
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      if (!FxOption.IsDoubleBarrier)
        InvalidValue.AddError(errors, this, "Barrier",
          String.Format("This pricer can price only double barrier options"));
      base.Validate(errors);
    }

    /// <summary>
    /// Adjusts the barrier.
    /// </summary>
    /// <param name="startFx">The start fx.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="freq">The freq.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private double AdjustedBarrier(double startFx, double barrier, Frequency freq)
    {
      return CalculateAdjustedBarrier(startFx, barrier,
        CalculateAverageVolatility(AsOf, FxOption.Maturity), freq);
    }

    /// <summary>
    /// Calculate the no touch probability.
    /// </summary>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double NoTouchProbability()
    {
      if (IsKnockedIn() || IsKnockedOut())
        return 0.0;
      else
      {
        return DoubleBarrierOptionPricer.NoTouchProbability(Settle,
          FxOption.Maturity, SpotFxRate, LowerBarrier.Value, UpperBarrier.Value,
          VolatilityCurve, GetFxCurveSet(), flags_);
      }
    }

    /// <summary>
    /// Calculates the over hedge amount.
    /// </summary>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double CalculateOverHedge()
    {
      if (SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation
        && FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Price, null) * EffectiveNotional;
      }

      // This needs to check that if the settle discount adjustment is required.
      var expiration = FxOption.Maturity;
      var input = GetRatesSigmaTime(expiration, null);
      var T = input.T;
      var rf = input.Rf;
      var rd = input.Rd;
      var vol = input.Sigma;

      var kAtm = SpotFxRate*Math.Exp((rd - rf)*T);
      var sigAtm = VolatilitySurface.Interpolate(FxOption.Maturity, kAtm);

      var kc = VannaVolgaCalibrator.SolveDelta(SpotFxRate, T, rf, rd, sigAtm, OptionType.Call, 0.25);
      var kp = VannaVolgaCalibrator.SolveDelta(SpotFxRate, T, rf, rd, sigAtm, OptionType.Put, -0.25);

      var sigC = VolatilitySurface.Interpolate(FxOption.Maturity, kc);
      var sigP = VolatilitySurface.Interpolate(FxOption.Maturity, kp);

      var p = NoTouchProbability(FxOption.Maturity, vol);
      var vannaCost = CostOfVanna(sigAtm, sigC, sigP, kc, kp, T, rf, rd);
      var volgaCost = CostOfVolga(sigAtm, sigC, sigP, kc, kp, T, rf, rd);
      return p*(vannaCost + volgaCost);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Net present value on the settle date of the option, excluding the value
    /// of any additional payment.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public override double ProductPv()
    {
      if (FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
        return UnitPrice()*EffectiveNotional;
      
      if (FxOption.Maturity < AsOf)
      {
        // The option has already exercised.
        return 0.0;
      }

      var fcs = GetFxCurveSet();
      double price;
      if (IsKnockedIn())
      {
        var input = GetRatesSigmaTime(FxOption.Maturity, fcs);
        if (FxOption.IsDigital)
        {
          price = DigitalOption.P(OptionStyle.European, FxOption.Type, OptionDigitalType.Cash,
            input.T, SpotFxRate, FxOption.Strike, input.Rd, input.Rf, input.Sigma, 1.0);
        }
        else
        {
          price = BlackScholes.P(OptionStyle.European, FxOption.Type, input.T, SpotFxRate, FxOption.Strike, input.Rd,
            input.Rf, input.Sigma);
        }
      }
      else
      {
        if (IsKnockedOut())
        {
          price = 0.0;
        }
        else
        {
          // We calculate the adjusted barrier here, in order to incorporate
          // any changes (in volatility, for example) which moves the effective barrier.
          var fxo = FxOption;
          var lowerBarrier = adjustedBarriers_[0];
          lowerBarrier.Value = AdjustedBarrier(SpotFxRate,
            fxo.Barriers[0].Value, fxo.Barriers[0].MonitoringFrequency);
          var upperBarrier = adjustedBarriers_[1];
          upperBarrier.Value = AdjustedBarrier(SpotFxRate,
            fxo.Barriers[1].Value, fxo.Barriers[1].MonitoringFrequency);
          price = DoubleBarrierOptionPricer.P(
            fxo, lowerBarrier, upperBarrier, SpotFxRate, AsOf, FxOption.Maturity,
            UseNewModel ? VolatilityCurve : Curve.CreateForwardVolatilityCurve(VolatilityCurve, null),
            fcs, flags_);
        }
      }
      //if (PremiumInBaseCcy) price /= SpotFxRate;
      return price*EffectiveNotional/SettleDiscountFactor();
    }

    /// <summary>
    /// Calculates the single flat volatility for an FX Option.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    double IFxOptionPricer.FlatVolatility()
    {
      return CalculateAverageVolatility(AsOf, FxOption.Maturity);
    }

    /// <summary>
    /// Calculates the Delta.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Delta is the change in <see cref="ProductPv"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point of change. Thus, the Delta function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="ProductPv"/>.</para>
    /// <para>Double barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The spot Fx rate is shifted up and down by 1bp and the derivative is calculated.</para>
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Delta()
    {
      if (FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
        return UnitDelta()*EffectiveNotional;

      var savedSpot = SpotFxRate;
      double delta;
      try
      {
        var bumpSize = 0.0001;

        // Up
        SpotFxRate = savedSpot + bumpSize;
        var pu = ProductPv();

        // Done
        SpotFxRate = savedSpot - bumpSize;
        var pd = ProductPv();

        // Finite Difference
        delta = (pu - pd)/(2*bumpSize);
      }
      finally
      {
        //restore
        SpotFxRate = savedSpot;
      }

      // Done
      return (delta*0.0001);
    }

    /// <summary>
    /// Calculates the Gamma.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Gamma is the change in <see cref="Delta"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point of change. Thus, the Gamma function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Delta"/>.</para>
    /// <para>Double barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The spot Fx rate is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Gamma()
    {
      if (FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
        return UnitGamma()*EffectiveNotional;

      var savedSpot = SpotFxRate;
      double gamma;
      try
      {
        var bumpSize = 0.0001;

        // Up
        SpotFxRate = savedSpot + bumpSize;
        var pu = Delta()/(0.0001*EffectiveNotional);

        // Down
        SpotFxRate = savedSpot - bumpSize;
        var pd = Delta()/(0.0001*EffectiveNotional);

        // Finite difference
        gamma = (pu - pd)/(2*bumpSize);
      }
      finally
      {
        SpotFxRate = savedSpot;
      }

      // Done
      return gamma*EffectiveNotional*0.0001*0.0001;
    }

    /// <summary>
    /// Calculates the Vega.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Vega is the change in <see cref="ProductPv"/> of the <see cref="FxOption"/> given a change in the volatility. 
    /// This derivative is first calculated and then scaled to be per volatility (1%). Thus, the Vega function's 
    /// value can be multiplied by a projected change in the volatility (in percentage terms) to estimate the change in 
    /// <see cref="ProductPv"/>.</para>
    /// <para>Double barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The volatility is shifted up and down by 1% and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Vega()
    {
      if (FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
        return UnitVega()*EffectiveNotional;

      double savedSpread = VolatilityCurve.Spread;
      try
      {
        // Up
        VolatilityCurve.Spread += 100.0/10000;
        var uSig = CalculateAverageVolatility(AsOf, FxOption.Maturity);
        var upPv = ProductPv();

        // Down
        VolatilityCurve.Spread -= 200.0/10000;
        var dSig = CalculateAverageVolatility(AsOf, FxOption.Maturity);
        var downPv = ProductPv();

        // Finite difference
        double vol01 = (upPv - downPv)/(uSig - dSig)/100;

        // Done
        return vol01;
      }
      finally
      {
        VolatilityCurve.Spread = savedSpread;
      }
    }

    /// <summary>
    /// Calculates the Vanna.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Vanna is the change in <see cref="Vega"/> of the <see cref="FxOption"/> given a change in the spot Fx rate. 
    /// This derivative is first calculated and then scaled to be per basis point. Thus, the Vanna function's 
    /// value can be multiplied by a projected change in the spot Fx rate (in bps) to estimate the change in 
    /// <see cref="Vega"/>.</para>
    /// <para>Double barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The spot Fx rate is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Vanna()
    {
      if (FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
        return UnitVanna()*EffectiveNotional;

      var savedSpot = SpotFxRate;
      double vanna;
      try
      {
        var bumpSize = 0.01;

        // Up
        SpotFxRate = savedSpot + bumpSize;
        var pu = Vega()/(0.01*EffectiveNotional);

        // Down
        SpotFxRate = savedSpot - bumpSize;
        var pd = Vega()/(0.01*EffectiveNotional);

        // Finite difference
        vanna = (pu - pd)/(2*bumpSize);
      }
      finally
      {
        SpotFxRate = savedSpot;
      }

      // Done
      return vanna*EffectiveNotional*0.01*0.0001;
    }

    /// <summary>
    /// Calculates the Volga.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Volga is the change in <see cref="Vega"/> of the <see cref="FxOption"/> given a change in the volatility. 
    /// This derivative is first calculated and then scaled to be per volatility (1%). Thus, the Volga function's 
    /// value can be multiplied by a projected change in the volatility (in percentage terms) to estimate the change in 
    /// <see cref="Vega"/>.</para>
    /// <para>Double barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The volatility is shifted up and down by 1% and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Volga()
    {
      if (FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions)
        return UnitVolga()*EffectiveNotional;

      var savedSpread = VolatilityCurve.Spread;
      try
      {
        // Base
        var origSig = CalculateAverageVolatility(AsOf, FxOption.Maturity);
        var p = ProductPv();

        // Up
        VolatilityCurve.Spread += 100.0/10000;
        var uSig = CalculateAverageVolatility(AsOf, FxOption.Maturity);
        var uv = (ProductPv() - p)/(uSig - origSig)/100;

        // Down
        VolatilityCurve.Spread -= 200.0/10000;
        var dSig = CalculateAverageVolatility(AsOf, FxOption.Maturity);
        var dv = (ProductPv() - p)/(dSig - origSig)/100;

        // Finite Difference
        double volga = (uv - dv)/100;

        // Done
        return volga;
      }
      finally
      {
        VolatilityCurve.Spread = savedSpread;
      }
    }

    /// <summary>
    /// Calculates the theta.
    /// </summary>
    /// 
    /// <seealso cref="Sensitivities.Rolldown(IPricer,bool)"/>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Theta()
    {
      return Sensitivities.Rolldown(this, false);
    }

    /// <summary>
    /// Checks if the option is already knocked in 
    /// </summary>
    /// <returns></returns>
    private bool IsKnockedIn()
    {
      if (FxOption.Barriers[0].BarrierType == OptionBarrierType.DownIn
        && FxOption.Barriers[1].BarrierType == OptionBarrierType.UpIn)
      {
        return (SpotFxRate < LowerBarrier.Value || SpotFxRate > UpperBarrier.Value);
      }
      else
      {
        return false;
      }
    }

    /// <summary>
    /// Checks if the option is already knocked out
    /// </summary>
    /// <returns>
    ///   <see cref="bool"/>
    /// </returns>
    private bool IsKnockedOut()
    {
      if (FxOption.Barriers[0].BarrierType == OptionBarrierType.DownOut
        && FxOption.Barriers[1].BarrierType == OptionBarrierType.UpOut)
      {
        return (SpotFxRate < LowerBarrier.Value || SpotFxRate > UpperBarrier.Value);
      }
      else
      {
        return false;
      }
    }

    /// <summary>
    /// The cost of the vanna.
    /// </summary>
    /// <param name="sigAtm">The sig atm.</param>
    /// <param name="sigC">The sig C.</param>
    /// <param name="sigP">The sig P.</param>
    /// <param name="kc">The kc.</param>
    /// <param name="kp">The kp.</param>
    /// <param name="T">The T.</param>
    /// <param name="rf">The rf.</param>
    /// <param name="rd">The rd.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private double CostOfVanna(double sigAtm, double sigC, double sigP, double kc, double kp, double T, double rf,
      double rd)
    {
      //Compute the vega for the 25 delta call option using atm vol 
      var vega = BlackScholes.Vega(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigAtm);
      var num = Vanna()*vega*(sigC - sigP);
      var callVanna = BlackScholes.Vanna(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigC);
      var putVanna = BlackScholes.Vanna(OptionStyle.European, OptionType.Put, T, SpotFxRate, kp, rd, rf, sigP);
      var den = callVanna - putVanna;
      return num/den;
    }

    /// <summary>
    /// Cost of the volga.
    /// </summary>
    /// <param name="sigAtm">The sig atm.</param>
    /// <param name="sigC">The sig C.</param>
    /// <param name="sigP">The sig P.</param>
    /// <param name="kc">The kc.</param>
    /// <param name="kp">The kp.</param>
    /// <param name="T">The T.</param>
    /// <param name="rf">The rf.</param>
    /// <param name="rd">The rd.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private double CostOfVolga(double sigAtm, double sigC, double sigP, double kc, double kp, double T, double rf,
      double rd)
    {
      //Compute the vega for the 25 delta call option using atm vol 
      double price = BlackScholes.P(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigAtm);
      double vega = BlackScholes.Vega(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigAtm);
      var num = 2*Volga()*vega*(0.5*(sigC + sigP) - price);

      var callVolga = BlackScholes.Vomma(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigC);
      var putVolga = BlackScholes.Vomma(OptionStyle.European, OptionType.Put, T, SpotFxRate, kp, rd, rf, sigP);
      var den = callVolga - putVolga;
      return num/den;
    }

    #endregion Methods

    #region properties

    /// <summary>
    /// Gets the adjusted upper barrier in the double barrier option
    /// </summary>
    public Barrier AdjustedUpperBarrier
    {
      get { return adjustedBarriers_[1]; }
    }

    /// <summary>
    /// Gets the adjusted lower barrier in the double barrier option
    /// </summary>
    public Barrier AdjustedLowerBarrier
    {
      get { return adjustedBarriers_[0]; }
    }

    /// <summary>
    /// Gets the upper barrier in the double barrier option
    /// </summary>
    public Barrier UpperBarrier
    {
      get { return FxOption.Barriers[1]; }
    }

    /// <summary>
    /// The lower barrier in the double barrier option
    /// </summary>
    public Barrier LowerBarrier
    {
      get { return FxOption.Barriers[0]; }
    }

    #endregion

    #region Unit price calculations

    private double UnitPrice()
    {
      if (ExerciseDate < AsOf)
      {
        // The option has already exercised.
        return 0.0;
      }

      FxOption option = FxOption;
      double price;
      var coefs = (SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation
        && SmileAdjustment != SmileAdjustmentMethod.NoAdjusment)
        ? GetOverhedgeCoefs(VannaVolgaTarget.Price)
        : null;
      if (IsKnockedIn())
      {
        if (option.IsTouchOption)
        {
          // Assume it's paid on settle.
          price = option.IsNoTouch
            ? 0.0
            : (option.IsPayAtBarrierHit
              ? SettleDiscountFactor()
              : DiscountFactor(option.Maturity));
          if (PremiumInBaseCcy) price /= SpotFxRate;
        }
        else
        {
          price = FxOptionVanillaPricer.CalculateUnitPrice(this, option, coefs);
        }
      }
      else if (IsKnockedOut())
      {
        price = 0;
      }
      else
      {
        // We calculate the adjusted barrier here, in order to incorporate
        // any changes (in volatility, for example) which moves the effective barrier.
        var fxo = FxOption;
        var lowerBarrier = adjustedBarriers_[0];
        lowerBarrier.Value = AdjustedBarrier(SpotFxRate,
          fxo.Barriers[0].Value, fxo.Barriers[0].MonitoringFrequency);
        var upperBarrier = adjustedBarriers_[1];
        upperBarrier.Value = AdjustedBarrier(SpotFxRate,
          fxo.Barriers[1].Value, fxo.Barriers[1].MonitoringFrequency);
        var fcs = GetFxCurveSet();
        if (fxo.IsTouchOption)
        {
          var input = GetRatesSigmaTime(FxOption.Maturity, fcs);
          price = DoubleBarrierOptionPricer.TouchOptionPrice(
            fxo.IsNoTouch, fxo.IsPayAtBarrierHit,
            input.T, SpotFxRate, input.Rd, input.Rf,
            lowerBarrier.Value, upperBarrier.Value, input.Sigma);
        }
        else
        {
          price = DoubleBarrierOptionPricer.P(fxo,
            lowerBarrier, upperBarrier, SpotFxRate, AsOf, FxOption.Maturity,
            UseNewModel ? VolatilityCurve : Curve.CreateForwardVolatilityCurve(
              VolatilityCurve, null),
            fcs, flags_);
        }
        if (coefs != null)
        {
          price += OverHedgeAdjustment(coefs, null);
        }
        if (PremiumInBaseCcy) price /= SpotFxRate;
      }

      // Done
      return price/SettleDiscountFactor();
    }

    private double OverHedgeAdjustment(VannaVolgaTarget target,
      Func<FxOptionDoubleBarrierPricer, double> bsFn)
    {
      var coefs = GetOverhedgeCoefs(target);
      if (coefs == null) return 0.0;
      return OverHedgeAdjustment(coefs, bsFn);
    }

    private double OverHedgeAdjustment(VannaVolgaCoefficients coefs,
      Func<FxOptionDoubleBarrierPricer, double> bsFn)
    {
      Debug.Assert(coefs != null);
      var pricer = (FxOptionDoubleBarrierPricer) ShallowCopy();
      pricer.Notional = 1.0;
      pricer.SmileAdjustment = SmileAdjustmentMethod.NoAdjusment;
      var bs = bsFn == null ? 0.0 : bsFn(pricer);

      var weight = 1.0;
      if (!NoExtraProbability)
      {
        weight = pricer.NoTouchProbability(SymmetricProbability);
        if (SymmetricOverHedge)
        {
          if (pricer.FxOption.IsTouchOption)
            weight = 2*weight*(1 - weight);
          else if (weight < 0.5)
            weight = 1.0 - weight;
        }
      }

      var vega = coefs.Vega.AlmostEquals(0.0)
        ? 0.0 : FxOptionPricerFactory.UnitVega(pricer, pricer.UnitPrice);
      var vanna = FxOptionPricerFactory.UnitVanna(pricer, pricer.UnitPrice);

      if (DomesticDiscountCurve.Ccy != FxCurve.SpotFxRate.ToCcy)
      {
        vanna += vega/SpotFxRate;
      }

      var volga = FxOptionPricerFactory.UnitVolga(pricer, pricer.UnitPrice);
      double costs;
      if (UseBloombergWeights)
      {
        costs = 0.5*(1 + weight)*(coefs.Vega*vega + coefs.Volga*volga) + weight*coefs.Vanna*vanna;
        return costs + bs;
      }
      costs = coefs.Vega*vega + coefs.Vanna*vanna + coefs.Volga*volga;
      return weight*costs + bs;
    }


    /// <summary>
    /// Calculate the no-touch probability.
    /// </summary>
    private double NoTouchProbability(bool symmetric)
    {
      if (IsKnockedIn() || IsKnockedOut())
        return 0.0;

      var fcs = GetFxCurveSet();
      var input = GetRatesSigmaTime(FxOption.Maturity, fcs);
      Dt maturity = FxOption.Maturity, settle = Settle;
      double S = SpotFxRate, L = LowerBarrier.Value, U = UpperBarrier.Value;
      var domesticCurve = DomesticDiscountCurve;
      var fxCurve = FxCurve;
      var domesticDf = domesticCurve.DiscountFactor(settle, maturity);
      var fxFactor = fxCurve.Interpolate(settle, maturity);
      double foreignDf;
      if (domesticCurve.Ccy != fxCurve.SpotFxRate.ToCcy)
      {
        foreignDf = domesticDf/fxFactor;
        S = 1/S;
        var tmp = 1/L;
        L = 1/U;
        U = tmp;
      }
      else
      {
        foreignDf = domesticDf*fxFactor;
      }
      var rf = -Math.Log(foreignDf)/input.T;
      var rd = -Math.Log(domesticDf)/input.T;

      // Probability in the domestic measure
      var p0 = DoubleBarrierOptionPricer.NoTouchProbability(
        input.T, S, rd, rf, L, U, input.Sigma);
      if (!symmetric) return p0;

      // Probability in the foreign measure
      var p1 = DoubleBarrierOptionPricer.NoTouchProbability(
        input.T, 1/S, rf, rd, 1/U, 1/L, input.Sigma);

      // THe symmetric probability
      return (p0 + p1)/2;
    }

    private double UnitDelta()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Delta, p => p.UnitDelta());
      }
      return FxOptionPricerFactory.UnitDelta(this, UnitPrice);
    }

    private double UnitGamma()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Gamma, p => p.UnitGamma());
      }
      return FxOptionPricerFactory.UnitGamma(this, UnitPrice);
    }

    private double UnitVega()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Vega, p => p.UnitVega());
      }
      return FxOptionPricerFactory.UnitVega(this, UnitPrice);
    }

    private double UnitVanna()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Vanna, p => p.UnitVanna());
      }
      return FxOptionPricerFactory.UnitVanna(this, UnitPrice);
    }

    private double UnitVolga()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Volga, p => p.UnitVolga());
      }
      return FxOptionPricerFactory.UnitVolga(this, UnitPrice);
    }

    #endregion
  }
}
