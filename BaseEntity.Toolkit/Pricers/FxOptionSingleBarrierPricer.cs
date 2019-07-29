//
// FxOptionSingleBarrierPricer.cs
//  -2011. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Class used for pricing Barrier option with Time Dependent Barriers
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FxOption" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="FxOptionPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.FxOption"/>
  /// <seealso cref="FxOptionPricerBase"/>
  [Serializable]
  public class FxOptionSingleBarrierPricer: FxOptionPricerBase, IFxOptionPricer
  {
    #region data
    private Barrier adjustedBarrier_;
    private int flags_;

    private bool UseNewModel
    {
      get { return (flags_ & TimeDependentBarrierOption.UseOldModel) == 0; }
    }


    // For Vanna-Volga pricer
    [NonSerialized, NoClone] private VannaVolgaCoefficients atmValues_;

    #endregion 

    #region Constructors
    /// <summary>
    /// Constructor for the time dependent barrier option pricer
    /// </summary>
    /// <param name="barrierOption">The barrier option.</param>
    /// <param name="asOf">As of date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="domesticRateCurve">The domestic rate curve.</param>
    /// <param name="foreignRateCurve">The foreign rate curve.</param>
    /// <param name="fxCurve">The FX curve</param>
    /// <param name="surface">The surface.</param>
    /// <param name="smileAdjustment">Smile adjusment method.</param>
    public FxOptionSingleBarrierPricer(
      FxOption barrierOption,
      Dt asOf,
      Dt settle,
      DiscountCurve domesticRateCurve,
      DiscountCurve foreignRateCurve,
      FxCurve fxCurve,
      CalibratedVolatilitySurface surface,
      int smileAdjustment)
      : base(barrierOption, asOf, settle, domesticRateCurve,
        foreignRateCurve, fxCurve, surface,
        (SmileAdjustmentMethod)smileAdjustment)
    {
      flags_ = 0;

      if (FxOption.IsBarrier && FxOption.Barriers.Count != 0)
      {
        var barrier = (Barrier) FxOption.Barriers[0].Clone();
        barrier.Value = AdjustedBarrierValue(SpotFxRate,
          barrier.MonitoringFrequency);
        barrier.MonitoringFrequency = Frequency.Continuous;
        adjustedBarrier_ = barrier;
      }
      else
      {
        throw new ToolkitException("No barrier specified");
      }
    }

    #endregion

    #region Properties 
    /// <summary>
    /// The adjusted barrier
    /// </summary>
    public Barrier AdjustedBarrier
    {
      get { return adjustedBarrier_; }
    }

    /// <summary>
    /// The adjusted barrier
    /// </summary>
    private Barrier Barrier
    {
      get { return FxOption.Barriers[0]; }
    }

    /// <summary>
    /// Gets a value indicating whether the option has knocked in.
    /// </summary>
    /// <value>
    ///   <c>true</c> if knocked in; otherwise, <c>false</c>.
    /// </value>
    public bool IsKnockedIn
    {
      get
      {
        if(Barrier.BarrierType == OptionBarrierType.UpIn && SpotFxRate > Barrier.Value)
          return true;
        else if(Barrier.BarrierType == OptionBarrierType.DownIn && SpotFxRate < Barrier.Value)
          return true;
        else
          return false;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the option has knocked out.
    /// </summary>
    /// <value>
    ///   <c>true</c> if knocked in; otherwise, <c>false</c>.
    /// </value>
    public bool IsKnockedOut
    {
      get
      {
        if (Barrier.BarrierType == OptionBarrierType.UpOut && SpotFxRate > Barrier.Value)
          return true;
        else if (Barrier.BarrierType == OptionBarrierType.DownOut && SpotFxRate < Barrier.Value)
          return true;
        else if (!WindowEnd.IsEmpty() && WindowEnd < Settle)
          return true;
        else
          return false;
      }
    }

    private Dt WindowEnd
    {
      get
      {
        if (!FxOption.BarrierWindowEnd.IsEmpty()
          && FxOption.BarrierWindowEnd < FxOption.Maturity)
        {
          return FxOption.BarrierWindowEnd;
        }
        return FxOption.Maturity;
      }
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
      if (SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation)
      {
        return surface.GetAtmVolatilityCurve();
      }
      var curve = new VolatilityCurve(surface.AsOf);

      var barrier = FxOption.Barriers[0];
      foreach (var date in surface.GetTenorDates())
      {
        var vol = surface.Interpolate(date, barrier.Value);
        if (!FxOption.IsTouchOption)
        {
          // For touch option, strike is ignored.
          // So this applies to regular options only.
          var volAtStrike = surface.Interpolate(date, FxOption.Strike);
          var volAtBarrier = vol;
          var barrierCrossProb = 1.0 - NoTouchProbability(
            date, volAtBarrier, FxOption.Barriers[0]);
          vol = InterpolatedVolatility(barrierCrossProb,
            volAtBarrier, volAtStrike, barrier.BarrierType);
        }
        curve.Add(date, vol);
      }
      return curve;
    }

    /// <summary>
    /// Interpolates the volatility.
    /// </summary>
    /// <param name="prob">The prob.</param>
    /// <param name="volAtBarrier">The vol at barrier.</param>
    /// <param name="volAtStrike">The vol at strike.</param>
    /// <param name="type">The type.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private static double InterpolatedVolatility(double prob,
      double volAtBarrier, double volAtStrike, OptionBarrierType type)
    {
      if (Math.Abs(volAtStrike - volAtBarrier) < 10*Double.Epsilon)
        return volAtStrike; // no strike skew

      switch (type)
      {
      case OptionBarrierType.DownIn:
        return prob*volAtStrike + (1 - prob)*volAtBarrier;
      case OptionBarrierType.DownOut:
        return prob*volAtBarrier + (1 - prob)*volAtStrike;
      case OptionBarrierType.UpIn:
        return prob*volAtStrike + (1 - prob)*volAtBarrier;
      case OptionBarrierType.UpOut:
        return prob*volAtBarrier + (1 - prob)*volAtStrike;
      case OptionBarrierType.OneTouch:
      case OptionBarrierType.NoTouch:
        return volAtBarrier; // strike is ignored.
      default:
        throw new Exception(String.Format(
          "Barrier type {0} is not yet supported ", type));
      }
    }

    /// <summary>
    /// Net present value on the settle date of the option, excluding the value
    /// of any additional payment.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public override double ProductPv()
    {
      return UnitPice()*EffectiveNotional;
    }

    /// <summary>
    /// Reset pricer
    /// </summary>
    /// <remarks></remarks>
    public override void Reset()
    {
      ResetAtmValues();
      base.Reset();
    }

    /// <summary>
    /// Gets the adjusted barrier value.
    /// </summary>
    /// <param name="startFx">The start fx.</param>
    /// <param name="monitoringFreq">The monitoring freq.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    private double AdjustedBarrierValue(double startFx, Frequency monitoringFreq)
    {
      var barrier = FxOption.Barriers[0];
      if (FxOption.Maturity < AsOf)
        return barrier.Value;
      return CalculateAdjustedBarrier(startFx, barrier.Value,
        CalculateAverageVolatility(AsOf, FxOption.Maturity),
        monitoringFreq);
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
    /// <para>Single barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The spot Fx rate is shifted up and down by 1bp and the derivative is calculated.</para>
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Delta()
    {
      return UnitDelta()*EffectiveNotional*1E-4;
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
    /// <para>Single barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The spot Fx rate is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Gamma()
    {
      return UnitGamma()*EffectiveNotional*1E-8;
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
    /// <para>Single barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The volatility is shifted up and down by 1% and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Vega()
    {
      return UnitVega()*EffectiveNotional/100;
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
    /// <para>Single barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The spot Fx rate is shifted up and down by 1bp and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Vanna()
    {
      return UnitVanna()*EffectiveNotional*1E-6;
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
    /// <para>Single barrier <see cref="FxOption">FxOptions</see> calculate the derivative using a second order, central 
    /// finite-difference method. The volatility is shifted up and down by 1% and the derivative is calculated.</para> 
    /// </remarks>
    /// 
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double Volga()
    {
      return UnitVolga()*EffectiveNotional*1E-4;
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
      // Make sure the ExerciseDate is set.
      EnsureExerciseDateIsSet();
      return Sensitivities.Rolldown(this, false);
    }

    /// <summary>
    /// Calculates the over hedge.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double CalculateOverHedge()
    {
      if (SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Price, null)*EffectiveNotional;
      }
      var expiration = FxOption.Maturity;
      if (expiration < AsOf)
      {
        return 0.0; 
      }
      var input = GetRatesSigmaTime(expiration, null);
      var T = input.T;
      if (T < 1.0 / 365)
      {
        return 0.0;
      }
      var rf = input.Rf;
      var rd = input.Rd;
      var vol = input.Sigma;
      var surface = VolatilitySurface;
      var kAtm = SpotFxRate*Math.Exp((rd - rf)*T);
      var sigAtm = surface.Interpolate(expiration, kAtm);
      
      var kc = VannaVolgaCalibrator.SolveDelta(SpotFxRate, T, rf, rd, sigAtm, OptionType.Call, 0.25);
      var kp = VannaVolgaCalibrator.SolveDelta(SpotFxRate, T, rf, rd, sigAtm, OptionType.Put, -0.25);

      var sigC = surface.Interpolate(expiration, kc);
      var sigP = surface.Interpolate(expiration, kp);

      var p = NoTouchProbability(expiration, vol, Barrier);
      var vannaCost = CostOfVanna(sigAtm, sigC, sigP, kc, kp, T, rf, rd);
      var volgaCost = CostOfVolga(sigAtm, sigC, sigP, kc, kp, T, rf, rd);
      return p*(vannaCost + volgaCost);
    }

    /// <summary>
    /// Calculates the probability of not crossing the barrier.
    /// </summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="vol">The vol.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="symetric">if set to <c>true</c> [symetric].</param>
    /// <returns>No touch probability</returns>
    /// <remarks></remarks>
    private double NoTouchProbability(Dt maturity, double vol, Barrier barrier, bool symetric)
    {
      var t = barrier.BarrierType;
      bool down = (((t == OptionBarrierType.OneTouch ||
        t == OptionBarrierType.NoTouch) && SpotFxRate >= barrier.Value) ||
        (t == OptionBarrierType.DownIn) || (t == OptionBarrierType.DownOut));
      var p = TimeDependentBarrierOption.CalculateTouchProbability(
        SpotFxRate, barrier.Value, down, vol, AsOf, maturity,
        DomesticDiscountCurve, FxCurve, symetric);
      return (1.0 - p);
    }

    private double NoTouchProbability(Dt maturity, double vol, Barrier barrier)
    {
      return NoTouchProbability(maturity, vol, barrier, false);
    }

    private double NoTouchProbability(bool symmetric)
    {
      var vol = CalculateAverageVolatility(AsOf, FxOption.Maturity);
      return NoTouchProbability(WindowEnd, vol, Barrier, symmetric);
    }

    /// <summary>
    /// Calculates the probability of not crossing the barrier.
    /// </summary>
    /// <returns>NoTouchProbability</returns>
    public double NoTouchProbability()
    {
      var vol = CalculateAverageVolatility(AsOf, FxOption.Maturity);
      return NoTouchProbability(WindowEnd, vol, Barrier, false);
    }

    /// <summary>
    /// Calculates the cost of vanna.
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
    private double CostOfVanna(double sigAtm,double sigC,double sigP,double kc,double kp,double T,double rf,double rd)
    {
      //Compute the vega for the 25 delta call option using atm vol 
      double vega = BlackScholes.Vega(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigAtm);
      var num = Vanna()*vega*(sigC - sigP);

      var callVanna = BlackScholes.Vanna(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigC);
      var putVanna = BlackScholes.Vanna(OptionStyle.European, OptionType.Put, T, SpotFxRate, kp, rd, rf, sigP);

      var den = callVanna - putVanna;
      return num/den;
    }

    /// <summary>
    /// Calculates the cost of volga.
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
    private double CostOfVolga(double sigAtm, double sigC, double sigP, double kc, double kp, double T, double rf, double rd)
    {
      //Compute the vega for the 25 delta call option using atm vol 
      double price = BlackScholes.P(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigAtm);
      double vega = BlackScholes.Vega(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigAtm);
      var num = 2 * Volga() * vega * (0.5 * (sigC + sigP) - price);

      var callVolga = BlackScholes.Vomma(OptionStyle.European, OptionType.Call, T, SpotFxRate, kc, rd, rf, sigC);
      var putVolga = BlackScholes.Vomma(OptionStyle.European, OptionType.Put, T, SpotFxRate, kp, rd, rf, sigP);
      var den = callVolga - putVolga;
      return num/den;
    }

    /// <summary>
    /// Calculates the single flat volatility for an Fx Option.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    double IFxOptionPricer.FlatVolatility()
    {
      return CalculateAverageVolatility(AsOf, FxOption.Maturity);
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      // Must be a barrier option
      if(!FxOption.IsBarrier)
        InvalidValue.AddError(errors, this, "This pricer can only be used for options with a barrier.");

      // Only handle single barriers for now
      if (FxOption.Barriers.Count > 1)
        InvalidValue.AddError(errors, this, "This pricer can only be used for single-barrier options.");

      // Base
      base.Validate(errors);
    }

    #endregion

    #region Unit values

    private double UnitPice()
    {
      if (ExerciseDate < AsOf || FxOption.Maturity < AsOf)
      {
        // The option has already exercised.
        return 0.0;
      }
      return UnitPrice(FxOption)/SettleDiscountFactor();
    }

    private double UnitPrice(FxOption option)
    {
      var coefs = (SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation
        && SmileAdjustment != SmileAdjustmentMethod.NoAdjusment)
        ? GetOverhedgeCoefs(VannaVolgaTarget.Price)
        : null;
      var volCurve = UseNewModel
        ? VolatilityCurve
        : Curve.CreateForwardVolatilityCurve(VolatilityCurve, null);

      if (IsKnockedIn)
      {
        if (option.IsTouchOption)
        {
          // Assume it's paid on settle.
          var price = option.IsNoTouch
            ? 0.0
            : (option.IsPayAtBarrierHit
              ? SettleDiscountFactor()
              : DiscountFactor(option.Maturity));
          if (PremiumInBaseCcy) price /= SpotFxRate;
          return price;
        }

        return ConsistentOverHedge
          ? FxOptionVanillaPricer.CalculateUnitPrice(this, option, coefs)
          : FxOptionVanillaPricer.CaculateOptionValue(option, AsOf,
            SpotFxRate, DomesticDiscountCurve, FxCurve, volCurve, coefs);
      }

      if (IsKnockedOut)
      {
        return 0;
      }

      if (ConsistentOverHedge && coefs != null && 
        option.Barriers[0].IsIn && !option.IsTouchOption)
      {
        var barrier = option.Barriers[0];
        option = (FxOption)option.ShallowCopy();
        option.Barriers = new List<Barrier> { barrier.ToOutType() };
        var pricer = (FxOptionSingleBarrierPricer)ShallowCopy();
        pricer.Product = option;
        pricer.adjustedBarrier_ = adjustedBarrier_.ToOutType();
        var vannilaPrice = FxOptionVanillaPricer.CalculateUnitPrice(pricer, option, coefs);
        return vannilaPrice - pricer.UnitPrice(option);
      }
      else
      {
        // We calculate the adjusted barrier here, in order to incorporate
        // any changes (in volatility, for example) which moves the effective barrier.
        var barrier = AdjustedBarrier;
        barrier.Value = AdjustedBarrierValue(SpotFxRate,
          option.Barriers[0].MonitoringFrequency);
        var input = GetFxCurveSet();
        var price = TimeDependentBarrierOption.P(option.IsRegular, option.Type,
          SpotFxRate, option.Strike, barrier.BarrierType, barrier.Value,
          AsOf, option.BarrierWindowEnd, option.Maturity, volCurve,
          input.DomesticDiscountCurve, FxCurve, flags_ | ((int)option.Flags));
        if (coefs != null)
        {
          price += OverHedgeAdjustment(coefs, null);
        }
        if (PremiumInBaseCcy) price /= SpotFxRate;

        return price;
      }
    }

    private double RecalculateUnitPrice()
    {
      ResetAtmValues();
      return UnitPice();
    }

    private void ResetAtmValues()
    {
      atmValues_ = null;
    }

    private double OverHedgeAdjustment(VannaVolgaTarget target,
      Func<FxOptionSingleBarrierPricer, double> bsFn)
    {
      var coefs = GetOverhedgeCoefs(target);
      if (coefs == null) return 0.0;
      return OverHedgeAdjustment(coefs, bsFn);
    }

    private double OverHedgeAdjustment(VannaVolgaCoefficients coefs,
      Func<FxOptionSingleBarrierPricer, double> bsFn)
    {
      Debug.Assert(coefs != null);
      var pricer = (FxOptionSingleBarrierPricer)ShallowCopy();
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
        // adjust for windowing
        Dt windowEnd = pricer.WindowEnd;
        if (windowEnd < pricer.FxOption.Maturity)
        {
          var sigma = CalculateAverageVolatility(AsOf, pricer.FxOption.Maturity);
          var p = pricer.NoTouchProbability(pricer.FxOption.Maturity,
            sigma, Barrier, SymmetricProbability);
          if (p > 0)
          {
            p /= pricer.NoTouchProbability(windowEnd, sigma, Barrier);
            weight = p * p * weight;
          }
        }
      }
#if EnableCache
      if (atmValues_ == null)
      {
#endif
        atmValues_ = new VannaVolgaCoefficients
        {
          Vega = FxOptionPricerFactory.UnitVega(pricer, pricer.RecalculateUnitPrice),
          Vanna = FxOptionPricerFactory.UnitVanna(pricer, pricer.RecalculateUnitPrice),
          Volga = FxOptionPricerFactory.UnitVolga(pricer, pricer.RecalculateUnitPrice)
        };
#if EnableCache
      }
#endif
      var vega = coefs.Vega == 0.0 ? 0.0 : atmValues_.Vega;
      var vanna = atmValues_.Vanna;
//#if EnableXccyAdjustment
      if (DomesticDiscountCurve.Ccy != FxCurve.SpotFxRate.ToCcy)
      {
        vanna += atmValues_.Vega/SpotFxRate;
      }
//#endif
      var volga = atmValues_.Volga;
      double costs;
      if (UseBloombergWeights)
      {
        costs = 0.5 * (1 + weight) * (coefs.Vega * vega + coefs.Volga * volga) + weight * coefs.Vanna * vanna;
        return costs + bs;
      }
      costs = coefs.Vega * vega + coefs.Vanna * vanna + coefs.Volga * volga;
      return weight * costs + bs;
    }

    private double UnitDelta()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Delta, p => p.UnitDelta());
      }
      return FxOptionPricerFactory.UnitDelta(this, RecalculateUnitPrice);
    }

    private double UnitGamma()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Gamma, p => p.UnitGamma());
      }
      return FxOptionPricerFactory.UnitGamma(this, RecalculateUnitPrice);
    }

    private double UnitVega()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Vega, p => p.UnitVega());
      }
      return FxOptionPricerFactory.UnitVega(this, RecalculateUnitPrice);
    }

    private double UnitVanna()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Vanna, p => p.UnitVanna());
      }
      return FxOptionPricerFactory.UnitVanna(this, RecalculateUnitPrice);
    }

    private double UnitVolga()
    {
      if (VannaVolgaGreek)
      {
        return OverHedgeAdjustment(VannaVolgaTarget.Volga, p => p.UnitVolga());
      }
      return FxOptionPricerFactory.UnitVolga(this, RecalculateUnitPrice);
    }
    #endregion
  }
}
