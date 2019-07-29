//
//  -2012. All rights reserved.
//
// Design Notes:
// + This design pattern can be used for multiple pricers that use the same underlying model.
// + The class mirrors the model parameters and calculated results and avoids any aditional properties or features.
// + Currently this base class depends on the product deriving from SingleAssetOptionBase. We can break this assumption by
//   adding virtual properties for the product terms required for pricing that the derived classes need to provide in a similar
//   way to the model parameters. RTD Dec'12

using System;
using System.Collections;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Abstract convenient base class for pricing options derived from <see cref="SingleAssetOption"/>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <para>Prices an option based on the Black-Scholes framework.</para>
  ///   <para>Depending on the type of option, different underlying models are called.</para>
  ///   <table border="1" cellpadding="5">
  ///     <colgroup><col align="center"/><col align="center"/></colgroup>
  ///     <tr><th>Type</th><th>Model</th></tr>
  ///     <tr><td>Vanilla</td><td>European options are priced using the <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>,
  ///       American options are priced using the <see cref="BinomialTree">Binomial Model</see></td></tr>
  ///     <tr><td>Digital</td><td>Digital options are priced using the
  ///       <see cref="BaseEntity.Toolkit.Models.DigitalOption">Digital option model</see></td></tr>
  ///     <tr><td>Barrier</td><td>Barrier options are priced using the
  ///       <see cref="BaseEntity.Toolkit.Models.BarrierOption">Barrier option model</see></td></tr>
  ///     <tr><td>One Touch</td><td>One touch (digital barrier) options are priced using the
  ///       <see cref="BaseEntity.Toolkit.Models.DigitalBarrierOption">Digital barrier option model</see></td></tr>
  ///     <tr><td>Lookback</td><td>Fixed strike lookback options are priced using the
  ///       <see cref="BaseEntity.Toolkit.Models.LookbackFixedStrikeOption">Rubinstein (1991) model</see>,
  ///       Floating strike lookback options are priced using the
  ///       <see cref="BaseEntity.Toolkit.Models.LookbackFloatingStrikeOption">Goldman, Sosin &amp;
  ///       Satto (1979)</see></td></tr>
  ///   </table>
  /// 
  ///   <para><h2>Black-Scholes</h2></para>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Models.BlackScholes" />
  /// </remarks>
  // Docs note: remarks are inherited so only include docs suitable for derived classes. RD Mar'14
  [Serializable]
  public abstract class BlackScholesPricerBase : PricerBase, IPricer
  {
    /// <summary>Empty schedule</summary>
    protected static readonly DividendSchedule EmptySchedule = new DividendSchedule(Dt.Empty);

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    protected BlackScholesPricerBase(SingleAssetOptionBase option, Dt asOf, Dt settle)
      : base(option, asOf, settle)
    {}

    #endregion Constructors

    #region Utility Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if (SingleAssetOption.StrikeDetermination != OptionStrikeDeterminationMethod.Fixed)
        InvalidValue.AddError(errors, this, "SingleAssetOption.StrikeDetermination", "Only fixed strikes are supported by this model");
      if (SingleAssetOption.UnderlyingDetermination != OptionUnderlyingDeterminationMethod.Regular)
        InvalidValue.AddError(errors, this, "SingleAssetOption.UnderlyingDetermination", "Only regular price determination options are supported by this model");
      if (SingleAssetOption.IsDigital && BlackScholesDivs != null && BlackScholesDivs.Any())
        InvalidValue.AddError(errors, this, "BlackScholesDivs", "Digital options do not currently support discrete dividents");
      if (SingleAssetOption.IsBarrier)
      {
        if (!SingleAssetOption.BarrierStart.IsEmpty() && Dt.Cmp(SingleAssetOption.BarrierStart, AsOf) > 0)
          InvalidValue.AddError(errors, this, "SingleAssetOption.BarrierStart", "This model does not support non-standard barrier start dates");
        if (!SingleAssetOption.BarrierEnd.IsEmpty() && Dt.Cmp(SingleAssetOption.BarrierStart, SingleAssetOption.Expiration) != 0)
          InvalidValue.AddError(errors, this, "SingleAssetOption.BarrierWindowEnd", "This model does not support non-standard barrier end dates");
        if (BlackScholesDivs != null && BlackScholesDivs.Any())
          InvalidValue.AddError(errors, this, "BlackScholesDivs", "Barrier options do not currently support discrete dividents");
      }
      if (SingleAssetOption.IsDoubleBarrier)
        InvalidValue.AddError(errors, this, "SingleAssetOption.Barriers", "Double barrier options are not currently supported by this model");
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _time = _underlyingQuotedPrice = _underlyingModelPrice = _rfr = _dividend = _volatility = null;
      _fv = _pv = _delta = _gamma = _theta = _vega = _rho = null;
      _lambda = _gearing = _strikeGearing = _vanna = _charm = _speed = _zomma = _color = _vomma = _dualDelta = _dualGamma = null;
    }

    #endregion Utility Methods

    #region Properties

    # region Properties for Black-Scholes Model provided by derivated classes

    /// <summary>
    /// Time
    /// </summary>
    protected abstract double BlackScholesTime { get; }

    /// <summary>
    /// Underlying asset quoted price
    /// </summary>
    /// <remarks>
    /// The fair value and model sensitivities are based on the quoted price of the underlying asset.
    /// </remarks>
    protected abstract double BlackScholesUnderlyingQuotedPrice { get; }

    /// <summary>
    /// Underlying asset model price
    /// </summary>
    /// <remarks>
    /// <para>The pv is based on the model price of the underling asset. This defaults to the
    /// quoted price of the underlying asset if not specified.</para>
    /// <para>This allows standard sensitivities to price based on an underlying price implied
    /// from market rates. An example is an option on a FX forward where the fx forward underlying
    /// price is implied from the fx spot and relative interest rates.</para>
    /// </remarks>
    protected virtual double BlackScholesUnderlyingModelPrice
    {
      get { return BlackScholesUnderlyingQuotedPrice; }
    }

    /// <summary>
    /// Continuously compounded risk free rate. Defaults to 0
    /// </summary>
    protected virtual double BlackScholesRfr
    {
      get { return 0.0; }
    }

    /// <summary>
    /// Continuously compounded dividend rate. Defaults to 0
    /// </summary>
    protected virtual double BlackScholesDividend
    {
      get { return 0.0; }
    }

    /// <summary>
    /// Discrete dividends. If this is set <see cref="BlackScholesDividend"/> is ignored. Defaults to null
    /// </summary>
    protected virtual DividendSchedule BlackScholesDivs
    {
      get { return EmptySchedule; }
    }

    /// <summary>
    /// Black-Scholes flat volatility
    /// </summary>
    protected abstract double BlackScholesVolatility { get; }

    # endregion Properties for Black-Scholes Model provided by derivated classes

    # region Cached model parameters

    // Cached model parameters so derived classes don't have to worry re performance
    private double Time
    {
      get
      {
        if (!_time.HasValue) _time = BlackScholesTime;
        return _time.Value;
      }
    }

    private double UnderlyingQuotedPrice
    {
      get
      {
        if (!_underlyingQuotedPrice.HasValue) _underlyingQuotedPrice = BlackScholesUnderlyingQuotedPrice;
        return _underlyingQuotedPrice.Value;
      }
    }

    private double UnderlyingModelPrice
    {
      get
      {
        if (!_underlyingModelPrice.HasValue) _underlyingModelPrice = BlackScholesUnderlyingModelPrice;
        return _underlyingModelPrice.Value;
      }
    }

    private double Rfr
    {
      get
      {
        if (!_rfr.HasValue) _rfr = BlackScholesRfr;
        return _rfr.Value;
      }
    }

    private double Dividend
    {
      get
      {
        if (!_dividend.HasValue) _dividend = BlackScholesDividend;
        return _dividend.Value;
      }
    }

    private double Volatility
    {
      get
      {
        if (!_volatility.HasValue) _volatility = BlackScholesVolatility;
        return _volatility.Value;
      }
    }

    # endregion Cached model parameters

    /// <summary>
    /// Underlying option
    /// </summary>
    private SingleAssetOptionBase SingleAssetOption
    {
      get { return (SingleAssetOptionBase)Product; }
    }

    public double ImpliedVolatilityAccuracy
    {
      get { return _ivolAccuracy; }
      set { if (value >= 0) _ivolAccuracy = value; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Calculates fair value as a percentage of notional of option based on quoted underlying price
    /// </summary>
    /// <remarks>
    ///   <para>Calculate fair price of the option as a percentag of notional</para>
    /// </remarks>
    /// <returns>Fair price of option</returns>
    public double FairPrice()
    {
      if (!_fv.HasValue)
      {
        if (Dt.Cmp(SingleAssetOption.Expiration, AsOf) < 0)
        {
          _fv = 0.0;
          return _fv.Value;
        }
        // Calculate fair value and model risks
        _fv = FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility, Rfr, true);
      }
      return _fv.Value;
    }

    /// <summary>
    /// Calculates fair value as a percentage of notional of option based on quoted underlying price
    /// </summary>
    /// <param name="underlyingQuotedPrice">Underlying quoted price</param>
    /// <returns>Fair price of option</returns>
    public double FairPrice(double underlyingQuotedPrice)
    {
        if (Dt.Cmp(SingleAssetOption.Expiration, AsOf) < 0)
          return 0.0;
        return FairPrice(underlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility, Rfr, false);
    }

    /// <summary>
    /// Calculate fair value of option as a percentage of the quoted underlying price
    /// </summary>
    /// <returns>Fair value of the option as a percentage of the underlying price</returns>
    public double FairPricePercent()
    {
      return FairPrice() / UnderlyingQuotedPrice;
    }

    /// <summary>
    /// Calculates fair value of option based on quoted underlying price
    /// </summary>
    /// <returns>Fair value of option</returns>
    public double FairValue()
    {
      return FairPrice() * Notional;
    }

    /// <summary>
    /// Calculates present value of option based on model implied underlying price
    /// </summary>
    /// <returns>Pv of option</returns>
    public override double ProductPv()
    {
      if (!_pv.HasValue)
      {
        if (Dt.Cmp(SingleAssetOption.Expiration, Settle) < 0 || IsTerminated)
        {
          _pv = 0.0;
          return _pv.Value;
        }
        // Calculate fair value and model risks
        _pv = FairPrice(UnderlyingModelPrice, SingleAssetOption.Strike, Time, Volatility, Rfr, false) * Notional;
      }
      return _pv.Value;
    }


    /// <summary>
    /// Delta of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of pv to a change in the underlying price
    /// <formula>\Delta = \frac{\partial V}{\partial S}</formula>
    /// </remarks>
    public double Delta()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_delta.HasValue)
        _delta = (FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false) - fv) * Notional;
      return _delta.Value;
    }

    /// <summary>
    /// Gamma of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of Delta to change in underlying price
    /// <formula>\Gamma = \frac{\partial \Delta}{\partial S} = \frac{\partial^2 V}{\partial S^2}</formula>
    /// </remarks>
    public double Gamma()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_gamma.HasValue)
        // gamma based on increase in underlying price so we don't need to worry re underlying price being too small.
      {
        if (UnderlyingQuotedPrice <= 1.0)
        {
          _gamma = (FairPrice(UnderlyingQuotedPrice + 2.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false) + fv
                    - 2.0 * FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false)) * Notional;
        }
        else
        {
          _gamma = (FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false) - 2.0 * fv
                    + FairPrice(UnderlyingQuotedPrice - 1.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false)) * Notional;

        }
      }
      return _gamma.Value;
    }

    /// <summary>
    /// Theta of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of pv to change in time to expiration
    /// <formula>\Theta = \frac{\partial V}{\partial \tau}</formula>
    /// </remarks>
    public double Theta()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_theta.HasValue)
      {
        _theta = (FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike,
          Time + RelativeTime.OneDay, Volatility, Rfr, false) - fv) * Notional;
      }
      return _theta.Value;
    }

    /// <summary>
    /// Vega of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of pv to change in volatility
    /// <formula>\nu = \frac{\partial V}{\partial \sigma}</formula>
    /// </remarks>
    public double Vega()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_vega.HasValue)
        _vega = (FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false) - fv) * Notional;
      return _vega.Value;
    }

    /// <summary>
    /// Rho of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of pv to change in interest rates
    /// <formula>\rho = \frac{\partial V}{\partial r}</formula>
    /// </remarks>
    public double Rho()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_rho.HasValue)
        _rho = (FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility, Rfr + 0.01, false) - fv) * Notional;
      return _rho.Value;
    }

    /// <summary>
    /// Lambda of option
    /// </summary>
    /// <remarks>
    /// Also called emega or elacticity. It is the percentage change in pv per percentage change in the underlying price.
    /// <formula>\lambda = \frac{\partial V}{\partial S}\times\frac{S}{V}</formula>
    /// </remarks>
    public double Lambda()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_lambda.HasValue)
        _lambda = Delta() / Notional * UnderlyingQuotedPrice / fv;
      return _lambda.Value;
    }

    /// <summary>
    /// Gearing of option
    /// </summary>
    /// <remarks>
    /// It is the underlying price divided by the option fair value.
    /// <formula>\text{Gearing} = \frac{S}{V}</formula>
    /// </remarks>
    public double Gearing()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_gearing.HasValue)
        _gearing = UnderlyingQuotedPrice / fv;
      return _gearing.Value;
    }

    /// <summary>
    /// Strike Gearing of option
    /// </summary>
    /// <remarks>
    /// It is the strike price divided by the option fair value.
    /// <formula>\text{Gearing} = \frac{K}{V}</formula>
    /// </remarks>
    public double StrikeGearing()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_strikeGearing.HasValue)
        _strikeGearing = SingleAssetOption.Strike / fv;
      return _strikeGearing.Value;
    }

    /// <summary>
    /// Vanna of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of delta with reference to volatility
    /// <formula>\text{Vanna} = \frac{\partial \Delta}{\partial \sigma} = \frac{\partial \nu}{\partial S} = \frac{\partial^2 V}{\partial S \partial \sigma}</formula>
    /// </remarks>
    public double Vanna()
    {
      double delta = Delta(); // Delta will also generate sensitivities if model provides
      if (!_vanna.HasValue)
        _vanna = (FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false)
                  - FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false)) * Notional
                 - delta;
      return _vanna.Value;
    }

    /// <summary>
    /// Charm of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of delta with reference to time to maturity
    /// <formula>\text{Charm} =- \frac{\partial \Delta}{\partial \tau} = \frac{\partial \Theta}{\partial S} = -\frac{\partial^2 V}{\partial S \, \partial \tau}</formula>
    /// </remarks>
    public double Charm()
    {
      double delta = Delta(); // Delta will also generate sensitivities if model provides
      if (!_charm.HasValue)
        _charm = (FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time + RelativeTime.OneDay, Volatility, Rfr, false)
                  - FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time + RelativeTime.OneDay, Volatility, Rfr, false)) * Notional
                 - delta;
      return _charm.Value;
    }

    /// <summary>
    /// Speed of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of gamma with reference to stock price
    /// <formula>\text{Speed} = \frac{\partial\Gamma}{\partial S} = \frac{\partial^3 V}{\partial S^3}</formula>
    /// </remarks>
    public double Speed()
    {
      double fv = FairPrice(); // Fair value will also generate sensitivities if model provides
      if (!_speed.HasValue)
        // Gamma based on increase in underlying price so we don't need to worry re underlying price being too small.
        _speed = (FairPrice(UnderlyingQuotedPrice + 3.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false)
                  + 3.0 * FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false)
                  - 2.0 * FairPrice(UnderlyingQuotedPrice + 2.0, SingleAssetOption.Strike, Time, Volatility, Rfr, false)
                  - fv) * Notional;
      return _speed.Value;
    }

    /// <summary>
    /// Zomma of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of gamma with reference to volatility
    /// <formula>\text{Zomma} = \frac{\partial \Gamma}{\partial \sigma} = \frac{\partial \text{Vanna}}{\partial S} = \frac{\partial^3 V}{\partial S^2 \, \partial \sigma}</formula>
    /// </remarks>
    public double Zomma()
    {
      double gamma = Gamma(); // Gamma will also generate sensitivities if model provides
      if (!_zomma.HasValue)
        // Gamma based on increase in underlying price so we don't need to worry re underlying price being too small.
        _zomma = (FairPrice(UnderlyingQuotedPrice + 2.0, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false)
                  + FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false)
                  - 2.0 * FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false)) * Notional
                 - gamma;
      return _zomma.Value;
    }

    /// <summary>
    /// Color of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of gamma with reference to time to maturity
    /// <formula>\text{Color} = \frac{\partial \Gamma}{\partial \tau} = \frac{\partial^3 V}{\partial S^2 \, \partial \tau}</formula>
    /// </remarks>
    public double Color()
    {
      double gamma = Gamma(); // Gamma will also generate sensitivities if model provides
      if (!_color.HasValue)
        // Gamma based on increase in underlying price so we don't need to worry re underlying price being too small.
        _color = (FairPrice(UnderlyingQuotedPrice + 2.0, SingleAssetOption.Strike, Time + RelativeTime.OneDay, Volatility, Rfr, false)
                  + FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time + RelativeTime.OneDay, Volatility, Rfr, false)
                  - 2.0 * FairPrice(UnderlyingQuotedPrice + 1.0, SingleAssetOption.Strike, Time + RelativeTime.OneDay, Volatility, Rfr, false)) * Notional
                 - gamma;
      return _color.Value;
    }

    /// <summary>
    /// Vomma of option
    /// </summary>
    /// <remarks>
    /// Second order sensitivity of option with reference to volatility
    /// <formula>\text{Vomma} = \frac{\partial \nu}{\partial \sigma} = \frac{\partial^2 V}{\partial \sigma^2}</formula>
    /// </remarks>
    public double Vomma()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairPrice();
      if (!_vomma.HasValue)
        _vomma = (FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility + 0.01, Rfr, false) +
                  FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike, Time, Volatility - 0.01, Rfr, false) - 2.0 * fv) * Notional;
      return _vomma.Value;
    }

    /// <summary>
    /// Dual Delta of option
    /// </summary>
    /// <remarks>
    /// Sensitivity of option with reference to strike. This is also the probability that the option will finish in the money.
    /// <formula>\text{Dual Delta} = \frac{\partial V}{\partial K}</formula>
    /// </remarks>
    public double DualDelta()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairPrice();
      if (!_dualDelta.HasValue)
        _dualDelta = (FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike + 1, Time, Volatility, Rfr, false) - fv) * Notional;
      return _dualDelta.Value;
    }

    /// <summary>
    /// Dual Gamma of option
    /// </summary>
    /// <remarks>
    /// Second order sensitivity of option with reference to strike.
    /// <formula>\text{Dual Gamma} = \frac{\partial \text{Dual Delta}}{\partial K} = \frac{\partial^2 V}{\partial K^2}</formula>
    /// </remarks>
    public double DualGamma()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairPrice();
      if (!_dualGamma.HasValue)
        _dualGamma = (FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike + 1.0, Time, Volatility, Rfr, false) +
                      FairPrice(UnderlyingQuotedPrice, SingleAssetOption.Strike - 1.0, Time, Volatility, Rfr, false) - 2.0 * fv) * Notional;
      return _dualGamma.Value;
    }

    /// <summary>
    /// Calculates implied volatility from fair value
    /// </summary>
    /// <param name="price">Option price in percent</param>
    /// <returns>Implied vol of option</returns>
    public double IVol(double price)
    {
      // Calculate fair value and model risks
      if (SingleAssetOption.IsBarrier)
      {
        var rebate = SingleAssetOption.Rebate;
        if (rebate > 0 || rebate < 0 || SingleAssetOption.SettlementType == SettlementType.Physical)
        {
          return CalculateImpliedVolatility(price);
        }

        // Barrier option
        if (SingleAssetOption.IsTouchOption)
        {
          var btype = SingleAssetOption.Barriers[0].BarrierType;
          var flags = btype == OptionBarrierType.OneTouch ? OptionBarrierFlag.OneTouch : OptionBarrierFlag.NoTouch;
          if (SingleAssetOption.BarrierPayoffTime == BarrierOptionPayoffTime.AtBarrierHit)
          {
            flags |= OptionBarrierFlag.PayAtBarrierHit;
          }
          return TimeDependentBarrierOption.ImpliedVolatility(SingleAssetOption.Type, btype, Time, UnderlyingQuotedPrice, SingleAssetOption.Strike,
                                                              SingleAssetOption.Barriers[0].Value, 0.0, Rfr, Dividend, (int)flags, price);
        }
        else if (SingleAssetOption.IsDigital)
        {
          return DigitalBarrierOption.ImpliedVolatility(SingleAssetOption.Style, SingleAssetOption.Barriers[0].BarrierType, Time, UnderlyingQuotedPrice,
                                                        SingleAssetOption.Strike, SingleAssetOption.Barriers[0].Value, Rfr, Dividend, price);
        }
        else
        {
          int flags = 0;
          if ((rebate > 0 || rebate < 0) && SingleAssetOption.BarrierPayoffTime == BarrierOptionPayoffTime.Default)
          {
            // Rebate is not zero nor NaN, and no payoff time is specified,
            // We set payoff time to be at barrier hit for out barrier, and
            // at expiry for in barrier.
            if (SingleAssetOption.Barriers[0].IsOut) flags |= (int)OptionBarrierFlag.PayAtBarrierHit;
          }
          else if (SingleAssetOption.BarrierPayoffTime == BarrierOptionPayoffTime.AtBarrierHit)
          {
            if (SingleAssetOption.Barriers[0].IsOut) flags |= (int)OptionBarrierFlag.PayAtBarrierHit;
          }
          return TimeDependentBarrierOption.ImpliedVolatility(SingleAssetOption.Type, SingleAssetOption.Barriers[0].BarrierType, Time, UnderlyingQuotedPrice,
                                                              SingleAssetOption.Strike, SingleAssetOption.Barriers[0].Value, rebate, Rfr, Dividend, flags, price);
        }
      }
      else if (SingleAssetOption.IsDigital)
      {
        // Regular digital option
        OptionDigitalType digitalType = (SingleAssetOption.SettlementType == SettlementType.Cash) ? OptionDigitalType.Cash : OptionDigitalType.Asset;
        return DigitalOption.ImpliedVolatility(SingleAssetOption.Style, SingleAssetOption.Type, digitalType, Time, UnderlyingQuotedPrice,
                                               SingleAssetOption.Strike, Rfr, Dividend, price);
      }
      else if (SingleAssetOption.Style == OptionStyle.European)
      {
        // European vanilla option
        var v = BlackScholes.TryImplyVolatility(SingleAssetOption.Style, SingleAssetOption.Type, Time, UnderlyingQuotedPrice, SingleAssetOption.Strike, Rfr,
                                              Dividend, BlackScholesDivs, price, _ivolAccuracy);
        if (Double.IsNaN(v) && price.AlmostEquals(0.0))
          v = 0.0;
        else if (Double.IsNaN(v))
          throw new SolverException("Failed to solve implied volatility");
        return v;
      }
      else if (SingleAssetOption.Style == OptionStyle.American)
      {
        // American vanilla option
        return BinomialTree.ImpliedVolatility(SingleAssetOption.Style, SingleAssetOption.Type, Time, 0, UnderlyingQuotedPrice, SingleAssetOption.Strike, Rfr,
                                              Dividend, BlackScholesDivs, 0, price);
      }
      throw new ToolkitException("Unsupported option type");
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair price of option</returns>
    private double FairPrice(double ulPrice, double strike, double time, double volatility, double rfr, bool saveSensitivities)
    {
      double fv = 0.0;
      if (saveSensitivities)
      {
        _delta = _gamma = _theta = _vega = _rho = _lambda = _gearing = _strikeGearing = _vanna =
          _charm = _speed = _zomma = _color = _vomma = _dualDelta = _dualGamma = null;
      }
      // Calculate fair value and model risks
      var div = Dividend;
      if (SingleAssetOption.IsBarrier)
      {
        var btype = SingleAssetOption.Barriers[0].BarrierType;
        var barrier = SingleAssetOption.Barriers[0].Value;

        // Barrier option
        if (SingleAssetOption.IsTouchOption)
        {
          fv = TouchOptionPrice(btype, SingleAssetOption.SettlementType,
            time, ulPrice, barrier, rfr, div, volatility);
          var rebate = SingleAssetOption.Rebate;
          if (rebate > 0 || rebate < 0)
          {
            var noLuckType = (btype == OptionBarrierType.OneTouch
              ? OptionBarrierType.NoTouch
              : OptionBarrierType.OneTouch);
            var p = TouchOptionPrice(noLuckType, SettlementType.Cash,
              time, ulPrice, strike, rfr, div, volatility);
            fv += p*rebate;
          }
        }
        else if (SingleAssetOption.IsDigital)
        {
          const double cashAmt = 1.0;
          var flags = OptionBarrierFlag.Regular;
          if (SingleAssetOption.SettlementType == SettlementType.Physical)
            flags |= OptionBarrierFlag.PayAsset;
          fv = DigitalBarrierOption.Price(SingleAssetOption.Type,
            btype, time, ulPrice, strike, barrier, cashAmt, rfr, div,
            volatility, flags);
          var rebate = SingleAssetOption.Rebate;
          if (IsKnocked(btype, ulPrice, barrier))
          {
            if (SingleAssetOption.Barriers[0].IsOut && SingleAssetOption
              .BarrierPayoffTime == BarrierOptionPayoffTime.AtExpiry &&
              (rebate > 0 || rebate < 0))
            {
              fv += rebate * Math.Exp(-rfr * time);
            }
          }
          else if (rebate > 0 || rebate < 0)
          {
            var noLuckType = (SingleAssetOption.Barriers[0].IsIn
              ? OptionBarrierType.NoTouch
              : OptionBarrierType.OneTouch);
            var p = TouchOptionPrice(noLuckType, SettlementType.Cash,
              time, ulPrice, strike, rfr, div, volatility);
            fv += p * rebate;
          }
        }
        else
        {
          int flags = 0;
          var rebate = SingleAssetOption.Rebate;
          if (IsKnocked(btype, ulPrice, barrier) && (SingleAssetOption.Barriers[0].IsIn ||
            SingleAssetOption.BarrierPayoffTime != BarrierOptionPayoffTime.AtExpiry))
          {
            // Either (1) no need to pay rebate when the option is knocked in;
            // Or (2) rebate is paid in the past if knocked out and pay-at-barrier-hit.
            rebate = 0;
          }
          else if ((rebate > 0 || rebate < 0) && SingleAssetOption
            .BarrierPayoffTime == BarrierOptionPayoffTime.Default)
          {
            // Rebate is not zero nor NaN, and no payoff time is specified,
            // We set payoff time to be at barrier hit for out barrier, and
            // at expiry for in barrier.
            if (SingleAssetOption.Barriers[0].IsOut) flags |= (int)OptionBarrierFlag.PayAtBarrierHit;
          }
          else if (SingleAssetOption.BarrierPayoffTime == BarrierOptionPayoffTime.AtBarrierHit)
          {
            if (SingleAssetOption.Barriers[0].IsOut) flags |= (int)OptionBarrierFlag.PayAtBarrierHit;
          }
          fv = TimeDependentBarrierOption.Price(SingleAssetOption.Type, btype, time, ulPrice,
            strike, barrier, rebate, rfr, div, volatility, flags);
        }
      }
      else if (SingleAssetOption.IsDigital)
      {
        // Regular digital option
        OptionDigitalType digitalType = (SingleAssetOption.SettlementType == SettlementType.Cash) ? OptionDigitalType.Cash : OptionDigitalType.Asset;
        fv = DigitalOption.P(SingleAssetOption.Style, SingleAssetOption.Type,
          digitalType, time, ulPrice, SingleAssetOption.Strike, rfr, div,
          volatility, 1.0);
      }
      else if (SingleAssetOption.Style == OptionStyle.European)
      {
        // European vanilla option
        if (saveSensitivities)
        {
          double d = 0.0,
                 g = 0.0,
                 t = 0.0,
                 v = 0.0,
                 r = 0.0,
                 la = 0.0,
                 ge = 0.0,
                 kge = 0.0,
                 va = 0.0,
                 ch = 0.0,
                 sp = 0.0,
                 zo = 0.0,
                 co = 0.0,
                 vo = 0.0,
                 dd = 0.0,
                 dg = 0.0;
          fv = BlackScholes.P(SingleAssetOption.Style, SingleAssetOption.Type, time, ulPrice, strike, rfr, Dividend, BlackScholesDivs, volatility,
                              ref d, ref g, ref t, ref v, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo,
                              ref co, ref vo, ref dd, ref dg);
          _delta = d * Notional;
          _gamma = g * Notional;
          _theta = t *RelativeTime.OneDay * Notional; // Convert from years to 1 day
          _vega = v / 100.0 * Notional; // Convert to 1pc
          _rho = r / 100.0 * Notional; // Convert to 1pc
          _lambda = la;
          _gearing = ge;
          _strikeGearing = kge;
          _vanna = va / 100.0 * Notional; // Convert to 1pc
          _charm = ch * RelativeTime.OneDay * Notional; // Convert from years to 1 day
          _speed = sp * Notional;
          _zomma = zo / 100.0 * Notional; // Convert to 1pc
          _color = co * RelativeTime.OneDay * Notional; // Convert from years to 1 day
          _vomma = vo / 100.0 * Notional; // Convert to 1pc
          _dualDelta = dd * Notional;
          _dualGamma = dg * Notional;
        }
        else
        {
          fv = BlackScholes.P(SingleAssetOption.Style, SingleAssetOption.Type, time, ulPrice, strike, rfr, Dividend, BlackScholesDivs, volatility);
        }
      }
      else if (SingleAssetOption.Style == OptionStyle.American)
      {
        // American vanilla option
        double d = 0.0, g = 0.0, t = 0.0;
        fv = BinomialTree.P(SingleAssetOption.Style, SingleAssetOption.Type, time, 0, ulPrice, strike, rfr, Dividend, BlackScholesDivs, volatility, 0, ref d,
                            ref g, ref t);
        if (saveSensitivities)
        {
          _delta = d * Notional;
          _gamma = g * Notional;
          _theta = t * Notional;
        }
      }
      else
        throw new ToolkitException("Unsupported option type");
      return fv;
    }

    private static bool IsKnocked(OptionBarrierType btype, double spot, double barrier)
    {
      switch (btype)
      {
      case OptionBarrierType.DownOut:
      case OptionBarrierType.DownIn:
        return spot <= barrier;
      case OptionBarrierType.UpIn:
      case OptionBarrierType.UpOut:
        return spot >= barrier;
      }
      return false;
    }

    private double TouchOptionPrice(OptionBarrierType btype, SettlementType stype,
      double time, double spot, double strike, double r, double d, double volatility)
    {
      var flags = btype == OptionBarrierType.OneTouch ? OptionBarrierFlag.OneTouch : OptionBarrierFlag.NoTouch;
      if (SingleAssetOption.BarrierPayoffTime == BarrierOptionPayoffTime.AtBarrierHit)
      {
        flags |= OptionBarrierFlag.PayAtBarrierHit;
      }
      if (stype == SettlementType.Physical)
        flags |= OptionBarrierFlag.PayAsset;
      var p = TimeDependentBarrierOption.Price(SingleAssetOption.Type, btype, time, spot, strike,
        SingleAssetOption.Barriers[0].Value, 0.0, r, d, volatility, (int)flags);
      // TimeDependentBarrierOption is the model for FX options.  To use it for equity options
      // and commodity options, it is necessary to make adjustments.
      return (flags & OptionBarrierFlag.PayAsset) == 0
        ? p
        : p * ((flags & OptionBarrierFlag.PayAtBarrierHit) != 0
          ? spot
          : (spot * Math.Exp((r - d) * time)));
    }

    private double CalculateImpliedVolatility(double fairPrice)
    {
      var solver = new Brent2();
      solver.setLowerBounds(1E-15);
      solver.setToleranceX(1E-6);
      solver.setToleranceF(Math.Max(1E-14, 1E-12 * Math.Abs(fairPrice)));
      double ulPrice = UnderlyingQuotedPrice,
        strike = SingleAssetOption.Strike,
        time = Time,
        iniVol = Volatility,
        rfr = Rfr;
      var res = solver.solve(v => FairPrice(ulPrice, strike, time, v, rfr, false),
        null, fairPrice, iniVol * 0.9, iniVol * 1.1);
      return res;
    }


    /// <summary>
    /// Calculates the intrinsic value of option based on quoted underlying price
    /// </summary>
    /// <returns>Intrinsic value of option</returns>
    public double IntrinsicValue(double ulPrice)
    {
      return (SingleAssetOption.Type == OptionType.Call
        ? Math.Max(ulPrice - SingleAssetOption.Strike, 0.0)
        : Math.Max(SingleAssetOption.Strike - ulPrice, 0.0)) * Notional;
    }

    #endregion Methods

    #region Data

    private double? _time;
    private double? _underlyingQuotedPrice;
    private double? _underlyingModelPrice;
    private double? _rfr;
    private double? _dividend;
    private double? _volatility;

    private double? _fv;
    private double? _pv;
    private double? _delta;
    private double? _gamma;
    private double? _theta;
    private double? _vega;
    private double? _rho;
    private double? _lambda;
    private double? _gearing;
    private double? _strikeGearing;
    private double? _vanna;
    private double? _charm;
    private double? _speed;
    private double? _zomma;
    private double? _color;
    private double? _vomma;
    private double? _dualDelta;
    private double? _dualGamma;

    private double _ivolAccuracy = 1E-15;

    #endregion Data
  }
}
