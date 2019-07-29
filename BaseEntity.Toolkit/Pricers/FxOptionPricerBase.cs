// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  Smile adjusment method.
  /// </summary>
  public enum SmileAdjustmentMethod
  {
    /// <summary>
    ///  Plain vanilla option volatility is based on vanna volga approach.
    ///  For barrier options, adjust the volatility as the weighted average
    ///  of the volatilities at the barriers and strikes, where the weights
    ///  are based on the probability hitting the barriers.
    /// </summary>
    VolatilityInterpolation,

    /// <summary>
    ///  No adjustment (always use the ATM volatility).
    /// </summary>
    NoAdjusment,

    /// <summary>
    ///  Plain vanilla option volatility is based on vanna volga approach.
    ///  For barrier options, adjust the option value at the ATM volatility
    ///  by the over hedge costs of vanilla options scaled by
    ///  the probability hitting barriers.
    /// </summary>
    OverHedgeCosts,

    /// <summary>
    ///  Plain vanilla option volatility is based on vanna volga approach.
    ///  For barrier options, adjust the option value using the market
    ///  approach, which ignores vega risks.
    /// </summary>
    OverHedgeCostsMarket,
  }

  /// <summary>
  /// The base class for all the FX option pricers.
  /// It provides common data and properties.
  /// </summary>
  /// <remarks>
  /// <para>Prices an fx option based on the Black-Scholes framework.</para>
  /// <para><b>Models</b></para>
  /// <para>Depending on the type of option, different underlying models are called.</para>
  /// <table border="1" cellpadding="5">
  ///   <colgroup><col align="center"/><col align="center"/></colgroup>
  ///   <tr><th>Type</th><th>Model</th></tr>
  ///   <tr><td>Vanilla</td><td>European options are priced using the <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>,
  ///     American options are priced using the <see cref="BinomialTree">Binomial Model</see></td></tr>
  ///   <tr><td>Digital</td><td>Digital options are priced using the
  ///     <see cref="BaseEntity.Toolkit.Models.DigitalOption">Digital option model</see></td></tr>
  ///   <tr><td>Barrier</td><td>Barrier options are priced using the
  ///     <see cref="BaseEntity.Toolkit.Models.BarrierOption">Barrier option model</see></td></tr>
  ///   <tr><td>One Touch</td><td>One touch (digital barrier) options are priced using the
  ///     <see cref="BaseEntity.Toolkit.Models.DigitalBarrierOption">Digital barrier option model</see></td></tr>
  ///   <tr><td>Lookback</td><td>Fixed strike lookback options are priced using the
  ///     <see cref="BaseEntity.Toolkit.Models.LookbackFixedStrikeOption">Rubinstein (1991) model</see>,
  ///     Floating strike lookback options are priced using the
  ///     <see cref="BaseEntity.Toolkit.Models.LookbackFloatingStrikeOption">Goldman, Sosin &amp;
  ///     Satto (1979)</see></td></tr>
  /// </table>
  /// <para><b>Black-Scholes</b></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.BlackScholes" />
  /// </remarks>
  // Docs note: remarks are inherited so only include docs suitable for derived classes. RD Mar'14
  [Serializable]
  public abstract class FxOptionPricerBase : PricerBase, IPricer, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FxOptionVanillaPricer"/> class.
    /// </summary>
    /// <param name="fxOption">The fx option.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="foreignRateCurve">The foreign rate curve.</param>
    /// <param name="fxCurve">The forward FX rate curve.</param>
    /// <param name="volatilitySurface">The volatility surface.</param>
    /// <param name="smileAdjustmentMethod">Smile adjustment method.</param>
    protected FxOptionPricerBase(
      FxOption fxOption,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve foreignRateCurve,
      FxCurve fxCurve,
      VolatilitySurface volatilitySurface,
      SmileAdjustmentMethod smileAdjustmentMethod)
      : base(fxOption, asOf, settle)
    {
      volatilitySurface_ = volatilitySurface;
      discountCurve_ = discountCurve;
      foreignRateCurve_ = foreignRateCurve;
      fxCurve_ = fxCurve;
      spotFxRate_ = fxCurve.SpotRate;
      smileAdjustment_ = smileAdjustmentMethod;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Reset the pricer
    /// <preliminary/>
    /// </summary>
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <remarks>
    /// 	<para>Some pricers need to remember certain internal states in order
    /// to skip redundant calculation steps.
    /// This function tells the pricer that what attributes of the products
    /// and other data have changed and therefore give the pricer an opportunity
    /// to selectively clear/update its internal states.  When used with caution,
    /// this method can be much more efficient than the method Reset() without argument,
    /// since the later resets everything.</para>
    /// 	<para>The default behaviour of this method is to ignore the parameter
    /// <paramref name="what"/> and simply call Reset().  The derived pricers
    /// may implement a more efficient version.</para>
    /// </remarks>
    /// <exclude/>
    public override void Reset(ResetAction what)
    {
      if (what == ResetVolatility)
      {
        blackVolatilityCurve_ = null;
      }
      fwdFxRate_ = null;
      spotFxRate_ = FxCurve == null ? 1.0 : FxCurve.FxRate(FxCurve.SpotFxRate.Spot);
      base.Reset(what);
    }

    /// <summary>
    /// Reset pricer
    /// </summary>
    public override void Reset()
    {
      // Don't reset volatility curve, which is reset only upon explicit request.
      fwdFxRate_ = null;
      spotFxRate_ = FxCurve == null ? 1.0 : FxCurve.FxRate(FxCurve.SpotFxRate.Spot);
      base.Reset();
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      // Validate interest rate curves
      if (FxOption.Ccy != DomesticDiscountCurve.Ccy)
        InvalidValue.AddError(errors, this, "FxOption domestic currency does not match interest rate domestic currency");

      // Validate FX curve
      if (FxOption.ReceiveCcy != FxCurve.SpotFxRate.ToCcy && FxOption.ReceiveCcy != FxCurve.SpotFxRate.FromCcy)
        InvalidValue.AddError(errors, this, "FX Curve does not have the option's domestic currency");
      if ((FxOption.ReceiveCcy == FxCurve.SpotFxRate.ToCcy && FxOption.Underlying.Ccy != FxCurve.SpotFxRate.FromCcy)
          || (FxOption.ReceiveCcy == FxCurve.SpotFxRate.FromCcy && FxOption.Underlying.Ccy != FxCurve.SpotFxRate.ToCcy))
      {
        InvalidValue.AddError(errors, this, "FX Curve does not match the option's foreign currency");
      }

      // Validate that the FxCurve and Vols are consistent
      var cs = VolatilitySurface as CalibratedVolatilitySurface;
      var calibrator = cs == null ? null : cs.Calibrator as FxVolatilityCalibrator;
      if (calibrator != null)
      {
        if (FxCurve.SpotFxRate.ToCcy != calibrator.DomesticRateCurve.Ccy)
          InvalidValue.AddError(errors, this, "FxCurve domestic currency does not match volatility domestic currency");
        if (FxCurve.SpotFxRate.FromCcy != calibrator.ForeignRateCurve.Ccy)
          InvalidValue.AddError(errors, this, "FxCurve foreign currency does not match volatility foreign currency");
      }
      // Make sure the volatility curve is built.
      BuildVolatilityCurve();

      // Base
      base.Validate(errors);
    }

    /// <summary>
    /// The discount factor to the settle date.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    internal double SettleDiscountFactor()
    {
      return DomesticDiscountCurve.DiscountFactor(AsOf, Settle);
    }

    internal double DiscountFactor(Dt date)
    {
      return DomesticDiscountCurve.DiscountFactor(AsOf, date);
    }

    /// <summary>
    /// Constructs the Black volatility curve.
    /// </summary>
    /// <remarks>
    /// This function is called in the property getter to construct
    /// a volatility curve if the current one is null.
    /// The derived class should implement their own version of
    /// this function to construct a proper volatility curve.
    /// </remarks>
    /// <returns>
    ///   <see cref="BaseEntity.Toolkit.Curves.VolatilityCurve"/>
    /// </returns>
    protected abstract VolatilityCurve ConstructVolatilityCurve();

    /// <summary>
    /// Calculates the average volatility, assuming the underlying curve
    ///  is a <em>Black</em> volatility curve.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double CalculateAverageVolatility(Dt start, Dt end)
    {
      return VolatilityCurve.CalculateAverageVolatility(start, end);
    }

    /// <summary>
    /// Calculates the adjusted barrier.
    /// </summary>
    /// <param name="startFx">The start fx.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="sigma">The average volatility.</param>
    /// <param name="monitoringFreq">The monitoring frequency.</param>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public static double CalculateAdjustedBarrier(
      double startFx, double barrier,
      double sigma, Frequency monitoringFreq)
    {
      if (monitoringFreq == Frequency.Continuous || startFx == barrier)
        return barrier;

      const double beta = 0.5826;
      double e = beta * sigma * Math.Sqrt(1.0 / (int)monitoringFreq);
      e = Math.Exp(barrier > startFx ? e : -e);
      return barrier * e;
    }

    /// <summary>
    /// Calculates the forward Fx rate at the option expiration.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double ForwardFxRate()
    {
      if (!fwdFxRate_.HasValue)
      {
        fwdFxRate_ = FxCurve.FxRate(FxCurve.GetExpirySpot(FxOption.Maturity));
      }
      return fwdFxRate_.Value;
    }

    /// <summary>
    /// Calculates the forward Fx points at the option expiration.
    /// </summary>
    /// <returns>Forward fx points at expiration</returns>
    public double ForwardFxPoints()
    {
      return (ForwardFxRate() - SpotFxRate)*10000.0;
    }

    /// <summary>
    /// Calculates the value of the option disregarding any barrier under the <see cref="BlackScholes"/> model.
    /// </summary>
    /// <returns>
    ///   <see cref="double"/>
    /// </returns>
    public double BlackScholesPv()
    {
      var option = this.FxOption;
      var input = GetRatesSigmaTime(option.Maturity, null);
      double delta = 0, gamma = 0, vega = 0, rho = 0, theta = 0,
        lambda = 0.0, gearing = 0.0, strikeGearing = 0.0, vanna = 0.0, charm = 0.0, speed = 0.0, zomma = 0.0, color = 0.0, vomma = 0.0, dualDelta = 0.0, dualGamma = 0.0;
      double val = (option.IsDigital
                      ? DigitalOption.P(OptionStyle.European, option.Type,
                                        OptionDigitalType.Cash, input.T, SpotFxRate, option.Strike,
                                        input.Rd, input.Rf, input.Sigma, 1.0)
                      : BlackScholes.P(OptionStyle.European, option.Type,
                                       input.T, SpotFxRate, option.Strike, input.Rd, input.Rf, input.Sigma,
                                       ref delta, ref gamma, ref theta, ref vega, ref rho,
                                       ref lambda, ref gearing, ref strikeGearing, ref vanna, ref charm, ref speed, ref zomma,
                                       ref color, ref vomma, ref dualDelta, ref dualGamma));
      if (PremiumInBaseCcy)
      {
        val /= SpotFxRate;
      }
      return val * EffectiveNotional / SettleDiscountFactor();
    }

    /// <summary>
    /// Calculate the theoretical price.
    /// </summary>
    /// <returns>The theoretical price.</returns>
    /// <remarks></remarks>
    public virtual double TheoreticalValue()
    {
      var pricer = (FxOptionPricerBase)ShallowCopy();
      pricer.Reset(ResetVolatility);
      pricer.Reset();
      pricer.Notional = 1.0;
      pricer.SmileAdjustment = SmileAdjustmentMethod.NoAdjusment;
      return pricer.Pv();
    }

    internal void BuildVolatilityCurve()
    {
      var flat = VolatilitySurface as FlatVolatility;
      blackVolatilityCurve_ = (flat != null
        ? new VolatilityCurve(AsOf, flat.Volatility)
        : ConstructVolatilityCurve());
    }
    #endregion Methods

    #region Properties

    /// <summary>
    /// Gets the FxOption.
    /// </summary>
    public FxOption FxOption
    {
      get { return Product as FxOption; }
    }

    /// <summary>
    /// Discount curve 
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return DomesticDiscountCurve; }
    }

    /// <summary>
    /// Gets the domestic discount curve.
    /// </summary>
    public DiscountCurve DomesticDiscountCurve
    {
      get { return discountCurve_; }
    }

    /// <summary>
    /// The Cross Currency Basis Curve
    /// </summary>
    public DiscountCurve BasisAdjustment
    {
      get { return (fxCurve_ != null && !fxCurve_.IsSupplied) ? fxCurve_.BasisCurve : null; }
    }

    /// <summary>
    /// Gets the foreign discount curve.
    /// </summary>
    public DiscountCurve ForeignDiscountCurve
    {
      get { return foreignRateCurve_; }
    }

    /// <summary>
    /// Gets domestic and foreign discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      get { return new[] {DomesticDiscountCurve, ForeignDiscountCurve}; }
    }

    /// <summary>
    /// Gets the volatility surface.
    /// </summary>
    public VolatilitySurface VolatilitySurface
    {
      get { return volatilitySurface_; }
    }

    /// <summary>
    /// The Cross Currency exchange rate curve
    /// </summary>
    public FxCurve FxCurve
    {
      get { return fxCurve_; }
    }

    /// <summary>
    /// Gets the <em>Black</em> volatility curve.
    /// </summary>
    /// <value>The volatility curve.</value>
    public VolatilityCurve VolatilityCurve
    {
      get
      {
        if (blackVolatilityCurve_ == null)
        {
          BuildVolatilityCurve();
        }
        return blackVolatilityCurve_;
      }
      set { blackVolatilityCurve_ = value; }
    }

    /// <summary>
    ///  The method for smile adjustment.
    /// </summary>
    public SmileAdjustmentMethod SmileAdjustment
    {
      get { return smileAdjustment_; }
      set { smileAdjustment_ = value; }
    }

    /// <summary>
    /// Gets the spot fx rate.
    /// </summary>
    public double SpotFxRate
    {
      get { return spotFxRate_; }
      protected internal set { spotFxRate_ = value; }
    }

    /// <summary>
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DomesticDiscountCurve);
        }
        return paymentPricer_;
      }
    }

    internal bool PremiumInBaseCcy
    {
      get { return DomesticDiscountCurve.Ccy != Currency.None
        && DomesticDiscountCurve.Ccy == FxCurve.Ccy1; }
    }

    #endregion Properties

    #region Data

    /// <summary>Reset Volatility</summary>
    //public static readonly ResetAction ResetVolatility = new ResetAction();
    private readonly VolatilitySurface volatilitySurface_;

    private readonly DiscountCurve discountCurve_;
    private readonly DiscountCurve foreignRateCurve_;
    private readonly FxCurve fxCurve_;
    private SmileAdjustmentMethod smileAdjustment_;
    private double spotFxRate_;
    private double? fwdFxRate_;

    // Calculated values
    private VolatilityCurve blackVolatilityCurve_;

    #endregion

    #region Exercise date

    private Dt exerciseDate_;

    private void SetExerciseDate()
    {
      var fo = FxOption;
      var br = fo.Barriers;
      if (br.Count > 0 && (fo.Maturity - AsOf) > 0.0 &&
        (fo.IsPayAtBarrierHit && (Math.Abs(br[0].Value - SpotFxRate) < 1E-9)
        || br.Count>1 && Math.Abs(br[1].Value - SpotFxRate) < 1E-9))
      {
        exerciseDate_ = AsOf;
      }
      exerciseDate_ = fo.Maturity;
    }

    internal void EnsureExerciseDateIsSet()
    {
      if (exerciseDate_.IsEmpty()) { SetExerciseDate(); }
    }

    internal Dt ExerciseDate
    {
      get
      {
        EnsureExerciseDateIsSet();
        return exerciseDate_;
      }
      set { exerciseDate_ = value; }
    }

    #endregion

    #region Vanna-Volga flags

    // For Vanna-Volga pricer
    private uint vvflags_ = SymmetricProbabilityFlag | FullSensitivityFlag;
    private const uint NoExtraProbabilityFlag = 1;
    private const uint FullSensitivityFlag = 2;
    private const uint SymmetricProbabilityFlag = 4;
    private const uint SymmetricOverHedgeFlag = 8;
    private const uint PlainWeightsFlag = 16;

    /// <summary>
    /// Gets or sets a value indicating whether there is no extra probability to the overhedge costs.
    /// </summary>
    /// <value><c>true</c> if [no extra probability]; otherwise, <c>false</c>.</value>
    /// <exclude>For Qunatifi internal use only.</exclude>
    public bool NoExtraProbability
    {
      get { return (vvflags_ & NoExtraProbabilityFlag) != 0; }
      set { vvflags_ = vvflags_.SetBitIf(value, NoExtraProbabilityFlag); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether full sensitivity is calculated.
    /// </summary>
    /// <value><c>true</c> if [full sensitivity]; otherwise, <c>false</c>.</value>
    /// <exclude>For Qunatifi internal use only.</exclude>
    public bool FullSensitivity
    {
      get { return (vvflags_ & FullSensitivityFlag) != 0; }
      set { vvflags_ = vvflags_.SetBitIf(value, FullSensitivityFlag); }
    }

    /// <exclude>For Qunatifi internal use only.</exclude>
    public bool SymmetricOverHedge
    {
      get { return (vvflags_ & SymmetricOverHedgeFlag) != 0; }
      set { vvflags_ = vvflags_.SetBitIf(value, SymmetricOverHedgeFlag); }
    }

    /// <exclude>For Qunatifi internal use only.</exclude>
    public bool SymmetricProbability
    {
      get { return (vvflags_ & SymmetricProbabilityFlag) != 0; }
      set { vvflags_ = vvflags_.SetBitIf(value, SymmetricProbabilityFlag); }
    }

    /// <exclude>For Qunatifi internal use only.</exclude>
    public bool UseBloombergWeights
    {
      get { return (vvflags_ & PlainWeightsFlag) == 0; }
      set { vvflags_ = vvflags_.SetBitIf(!value, PlainWeightsFlag); }
    }

    internal bool VannaVolgaGreek =>
      SmileAdjustment != SmileAdjustmentMethod.VolatilityInterpolation
      && SmileAdjustment != SmileAdjustmentMethod.NoAdjusment
      && !FullSensitivity;

    internal static bool ConsistentOverHedge =>
      FxOption.Settings.ConsistentOverHedgeAdjustmentAcrossOptions;

    #endregion

    #region internal interface to models

    internal FxCurveSet GetFxCurveSet()
    {
      // The real calculation always starts with the as-Of date,
      // which is the date the volatility starts.
      // All the values are converted to settle date only
      // at the input/output port.

      var ddc = DomesticDiscountCurve;
      var fxc = FxCurve;
      var begin = fxc.SpotDays > 0 && AsOf < fxc.SpotDate
        ? fxc.SpotDate : AsOf;

      if (PremiumInBaseCcy)
      {
        // Premium in non-numeraire ccy
        return new FxCurveSet(begin, fxc.SpotFxRate,
          fxc.WithDiscountCurve ? null : FxCurve,
          ForeignDiscountCurve, ddc, BasisAdjustment,
          //TOTO: backward compatible only, need to revisit
          fxc.SpotDays == 0);
      }
      // The regular case: premium in numeraire ccy.
      return new FxCurveSet(begin, fxc.SpotFxRate,
        fxc.WithDiscountCurve ? null : FxCurve,
        ddc, ForeignDiscountCurve, BasisAdjustment,
        //TOTO: backward compatible only, need to revisit
        fxc.SpotDays == 0);
    }

    internal FxCurveSet.RatesSigmaTime GetRatesSigmaTime(Dt expiration, FxCurveSet fcs)
    {
      if (fcs == null)
      {
        fcs = GetFxCurveSet();
      }
      return fcs.GetRatesSigmaTime(AsOf, expiration, VolatilityCurve);
    }

    internal double DownInProbability(Dt maturity, double vol, Barrier barrier)
    {
      //If the option is already knocked in then the down in probability is 1 
      if (SpotFxRate - 1E-9 < barrier.Value)
        return 1.0;
      var input = GetFxCurveSet().GetRatesSigmaTime(AsOf, maturity, null);
      return TimeDependentBarrierOption.CalculateDownInProbability(
        SpotFxRate, barrier.Value, input.T, vol, input.Rd, input.Rf);
    }

    internal double UpInProbability(Dt maturity, double vol, Barrier barrier)
    {
      if (SpotFxRate + 1E-9 > barrier.Value)
        return 1.0;
      var input = GetFxCurveSet().GetRatesSigmaTime(AsOf, maturity, null);
      return TimeDependentBarrierOption.CalculateUpInProbability(
        SpotFxRate, barrier.Value, input.T, vol, input.Rd, input.Rf);
    }

    internal VannaVolgaCoefficients GetOverhedgeCoefs(
      VannaVolgaTarget target)
    {
      return VolatilitySurface.GetOverhedgeCoefs(
        GetFxCurveSet(), FxOption.Maturity, SmileAdjustment)[target];
    }

    #endregion

    #region IVolatilitySurfaceProvider Members

    IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      yield return VolatilitySurface;
    }

    #endregion
  }

  // FxOptionPricerBase
}