//
//  -2012. All rights reserved.
//

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.StockBasketOption">Stock Basket Option</see>
  ///   using a Black-Scholes framework. Depending on the type of option, a variety of different underlying
  ///   models will be called. These include the <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes Model</see>, and the
  ///   <see cref="BaseEntity.Toolkit.Models.BinomialTree">Binomial Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.StockBasketOption" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BlackScholesPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StockCliquetOption"/>
  /// <seealso cref="BlackScholesPricerBase"/>
  [Serializable]
  public class StockBasketOptionPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrices">Stock price for each underlying</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="dividends">Dividend yield for each underlying (continuous comp, eg. 0.05)</param>
    /// <param name="divs">Schedule of discrete dividends for each underlying</param>
    /// <param name="volatilities">Volatilities for each underlying</param>
    /// <param name="correlations">Correlations between underlying assets</param>
    public StockBasketOptionPricer(
      StockBasketOption option, Dt asOf, Dt settle, double[] stockPrices,
      double rfr, double[] dividends, DividendSchedule[] divs, double[] volatilities,
      double[,] correlations
      )
      : base( option, asOf, settle)
    {
      StockPrices = stockPrices;
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      Dividends = dividends;
      Divs = divs;
      Volatilities = new CalibratedVolatilitySurface[volatilities.Length];
      for( var i = 0; i < volatilities.Length; ++i )
        Volatilities[i] = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatilities[i]);
      Correlations = correlations;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrices">Stock price for each underlying</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dividends">Dividend yield for each underlying (continuous comp, eg. 0.05)</param>
    /// <param name="divs">Schedule of discrete dividends for each underlying</param>
    /// <param name="volatilities">Volatility Surface for each underlying</param>
    /// <param name="correlations">Correlations between underlying assets</param>
    public StockBasketOptionPricer(
      StockBasketOption option, Dt asOf, Dt settle, double[] stockPrices,
      DiscountCurve discountCurve, double[] dividends, DividendSchedule[] divs, CalibratedVolatilitySurface[] volatilities,
      double[,] correlations
      )
      : base(option, asOf, settle)
    {
      StockPrices = stockPrices;
      DiscountCurve = discountCurve;
      Dividends = dividends;
      Divs = divs;
      Volatilities = volatilities;
      Correlations = correlations;
    }

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
      if( BasketSize != StockPrices.Length )
        InvalidValue.AddError(errors, this, "StockPrices", String.Format("Number of underlying prices {0} does not match number of stocks in basket {1}", StockPrices.Length, BasketSize));
      foreach( var p in StockPrices )
        if (p <= 0.0)
          InvalidValue.AddError(errors, this, "StockPrices", String.Format("Invalid price. Must be non negative, Not {0}", p));
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Missing discount curve"));
      else
      {
        if (BasketSize != Dividends.Length)
          InvalidValue.AddError(errors, this, "Dividends", String.Format("Number of dividends {0} does not match number of stocks in basket {1}", Dividends.Length, BasketSize));
        foreach (var d in Dividends)
          if (d < 0.0 || d > 2.0)
            InvalidValue.AddError(errors, this, "Dividends", String.Format("Invalid dividend {0}. Must be >= 0 and <= 2", d));
      }
      if (Volatilities == null)
        InvalidValue.AddError(errors, this, "Volatilities", String.Format("Invalid volatility surface. Cannot be null"));
      else if (BasketSize != Volatilities.Length)
        InvalidValue.AddError(errors, this, "Volatilities", String.Format("Number of volatilities {0} does not match number of stocks in basket {1}", Volatilities.Length, BasketSize));
      if (Correlations == null)
        InvalidValue.AddError(errors, this, "Correlations", String.Format("Invalid correlation matrix. Cannot be null"));
      else if (Correlations.Rank != 2 || Correlations.GetLength(0) != BasketSize || Correlations.GetLength(1) != BasketSize)
          InvalidValue.AddError(errors, this, "Correlations", String.Format("Correlations must be {0}x{0} matrix matching number of stocks in basket", BasketSize));
      if (StockBasketOption.StrikeDetermination != OptionStrikeDeterminationMethod.Fixed)
        InvalidValue.AddError(errors, this, "StockBasketOption", "Only fixed strikes are supported by this model");
      if (StockBasketOption.UnderlyingDetermination != OptionUnderlyingDeterminationMethod.Regular)
        InvalidValue.AddError(errors, this, "StockBasketOption", "Only regular price determination options are supported by this model");
      if (StockBasketOption.IsBarrier)
      {
        if (!StockBasketOption.BarrierWindowBegin.IsEmpty() && Dt.Cmp(StockBasketOption.BarrierWindowBegin, AsOf) > 0)
          InvalidValue.AddError(errors, this, "BarrierStart", "This model does not support non-standard barrier start dates");
        if (!StockBasketOption.BarrierWindowEnd.IsEmpty() && Dt.Cmp(StockBasketOption.BarrierWindowBegin, StockBasketOption.Expiration) != 0)
          InvalidValue.AddError(errors, this, "BarrierWindowEnd", "This model does not support non-standard barrier end dates");
      }
      if( StockBasketOption.IsDoubleBarrier )
        InvalidValue.AddError(errors, this, "StockBasketOption", "Double barrier options are not currently supported by this model");
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _pv = _delta = _gamma = _theta = _vega = _rho = _vol = _price = _dividend = null;
    }

    #endregion

    #region Properties

    #region Input Data

    /// <summary>
    /// Current Price price of each underlying asset
    /// </summary>
    public double[] StockPrices { get; set; }

    /// <summary>
    /// Dividend rates for each underlying stock (5 percent = 0.05)
    /// </summary>
    public double[] Dividends { get; set; }

    /// <summary>
    /// Dividend schedule for each underlying stock
    /// </summary>
    public DividendSchedule[] Divs { get; set; }

    /// <summary>
    /// Volatility Surfaces for each underlying stock
    /// </summary>
    public CalibratedVolatilitySurface[] Volatilities { get; set; }

    /// <summary>
    /// Correlations between underlying assets
    /// </summary>
    public double[,] Correlations { get; set; }

    #endregion Input Data

    /// <summary>
    /// Number of underlying stock in basket
    /// </summary>
    public int BasketSize
    {
      get { return (StockBasketOption.Amounts != null) ? StockBasketOption.Amounts.Length : StockPrices.Length; }
    }

    /// <summary>
    /// Risk free rate (5 percent = 0.05)
    /// </summary>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, AsOf, StockBasketOption.Expiration); }
    }

    /// <summary>
    /// Basket volatility
    /// </summary>
    public double Volatility
    {
      get
      {
        if (!_vol.HasValue)
          _vol = CalcBasketVolatility();
        return _vol.Value;
      }
      set { _vol = value; }
    }

    /// <summary>
    /// Basket price
    /// </summary>
    public double BasketPrice
    {
      get
      {
        if( !_price.HasValue )
          _price = CalcBasketPrice();
        return _price.Value;
      }
      set { _price = value; }
    }

    /// <summary>
    /// Basket dividend yield
    /// </summary>
    public double Dividend
    {
      get
      {
        if( !_dividend.HasValue )
          _dividend = CalcBasketDividend();
        return _dividend.Value;
      }
      set { _dividend = value; }
    }

    /// <summary>
    /// Time to expiration in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, StockBasketOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, StockBasketOption.Expiration).Days; }
    }

    /// <summary>
    ///   Stock basket Option product
    /// </summary>
    public StockBasketOption StockBasketOption
    {
      get { return (StockBasketOption)Product; }
    }

    /// <summary>
    ///   Discount Curve used for Payment
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    #region IPricer

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    #endregion IPricer

    #endregion Properties

    #region Methods

    /// <summary>
    /// Calculates present value of option
    /// </summary>
    /// <returns>Pv of option</returns>
    public override double ProductPv()
    {
      return FairValue() * Notional;
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    public double FairValue()
    {
      if (!_pv.HasValue)
      {
        // Calculate fair value and model risks
        _delta = _gamma = _theta = _vega = _rho = null;
        _pv = FairValue(StockBasketOption, BasketPrice, Time, Volatility, Rfr,
          Dividend, ref _delta, ref _gamma, ref _vega, ref _theta, ref _rho);
      }
      return _pv.Value;
    }

    /// <summary>
    /// Delta
    /// </summary>
    public double Delta()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairValue();
      if (!_delta.HasValue)
        _delta = FairValue(StockBasketOption, BasketPrice+1.0, Time, Volatility, Rfr, Dividend) - fv;
      return _delta.Value * Notional;
    }

    /// <summary>
    /// Gamma
    /// </summary>
    public double Gamma()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairValue();
      if (!_gamma.HasValue)
        _gamma = FairValue(StockBasketOption, BasketPrice + 1.0, Time, Volatility, Rfr, Dividend) +
                 FairValue(StockBasketOption, BasketPrice - 1.0, Time, Volatility, Rfr, Dividend) - 2.0 * fv;
      return _gamma.Value * Notional;
    }

    /// <summary>
    /// Theta
    /// </summary>
    public double Theta()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairValue();
      if (!_theta.HasValue)
        _theta = FairValue(StockBasketOption, BasketPrice, Time + (1.0 / 365.0), Volatility, Rfr, Dividend) - fv;
      return _theta.Value * Notional;
    }

    /// <summary>
    /// Vega
    /// </summary>
    public double Vega()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairValue();
      if (!_vega.HasValue)
        _vega = FairValue(StockBasketOption, BasketPrice, Time, Volatility + 0.01, Rfr, Dividend) - fv;
      return _vega.Value * Notional;
    }

    /// <summary>
    /// Rho
    /// </summary>
    public double Rho()
    {
      // Get fair value to force calculation of model sensitivities if not done
      double fv = FairValue();
      if (!_rho.HasValue)
        _rho = FairValue(StockBasketOption, BasketPrice, Time, Volatility, Rfr + 0.01, Dividend) - fv;
      return _rho.Value * Notional;
    }

    /// <summary>
    /// Calculates implied volatility from fair value
    /// </summary>
    /// <param name="price">Option price in percent</param>
    /// <returns>Implied vol of option</returns>
    public double IVol(double price)
    {
      if (StockBasketOption.IsBarrier)
        return 0.0;
      else if (StockBasketOption.IsDigital)
        return 0.0;
      else if (StockBasketOption.Style == OptionStyle.European)
        return BlackScholes.ImpliedVolatility(StockBasketOption.Style, StockBasketOption.Type, Time, BasketPrice,
          StockBasketOption.Strike, Rfr, Dividend, price);
      else if (StockBasketOption.Style == OptionStyle.American)
        return BinomialTree.ImpliedVolatility(StockBasketOption.Style, StockBasketOption.Type, Time, 0, BasketPrice,
                                              StockBasketOption.Strike, Rfr, Dividend, new DividendSchedule(AsOf), 0, price);
      else
        throw new ToolkitException("Unsupported option type");
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    private static double FairValue(StockBasketOption option, double ulPrice, double time, double volatility, double rfr,
      double dividend)
    {
      double? delta = null, gamma = null, vega = null, theta = null, rho = null;
      return FairValue(option, ulPrice, time, volatility, rfr, dividend, ref delta, ref gamma, ref vega, ref theta, ref rho);
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    private static double FairValue(StockBasketOption option, double ulPrice, double time, double volatility, double rfr,
      double dividend, ref double? delta, ref double? gamma, ref double? vega, ref double? theta,
      ref double? rho )
    {
      double fv = 0.0;
      // Calculate fair value and model risks
      if (option.IsBarrier)
      {
        // Barrier option
        if (option.IsDigital)
        {
          double cashAmt = 1.0;
          var flags = OptionBarrierFlag.Regular;
          if (option.SettlementType == SettlementType.Physical)
            flags |= OptionBarrierFlag.PayAsset;
          fv = Models.DigitalBarrierOption.Price(option.Type,
            option.Barriers[0].BarrierType, time, ulPrice, option.Strike,
            option.Barriers[0].Value, cashAmt, rfr, dividend, volatility, flags);
        }
        else
        {
          fv = option.Style == OptionStyle.European
            ? BarrierOption.P(option.Type, option.Barriers[0].BarrierType, time, ulPrice,
              option.Strike, option.Barriers[0].Value, option.Rebate, rfr, dividend, volatility)
            : BarrierOption.P2(option.Style, option.Type, option.Barriers[0].BarrierType,
              time, ulPrice, option.Strike, option.Barriers[0].Value, option.Rebate,
              rfr, dividend, volatility);
        }
      }
      else if (option.IsDigital)
      {
        // Regular digital option
        OptionDigitalType digitalType = (option.SettlementType == SettlementType.Cash) ? OptionDigitalType.Cash : OptionDigitalType.Asset;
        fv = DigitalOption.P(option.Style, option.Type, digitalType, time, ulPrice, option.Strike,
                                    rfr, dividend, volatility, 1.0);
      }
      else if (option.Style == OptionStyle.European)
      {
        // European vanilla option
        double d=0.0, g=0.0, t=0.0, v=0.0, r=0.0, la=0.0, ge=0.0, kge=0.0, va=0.0, ch=0.0, sp=0.0, zo=0.0, co=0.0, vo=0.0, dd=0.0, dg=0.0;
        fv = BlackScholes.P(option.Style, option.Type, time, ulPrice, option.Strike, rfr, dividend, volatility,
          ref d, ref g, ref t, ref v, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo, ref co, ref vo, ref dd, ref dg);
        delta = d;
        gamma = g;
        theta = t/365.0; // Convert theta from years to 1 day
        vega = v/100.0;
        rho = r/100.0;
      }
      else if (option.Style == OptionStyle.American)
      {
        // American vanilla option
        double d = 0.0, g = 0.0, t = 0.0;
        fv = BinomialTree.P(option.Style, option.Type, time, 0, ulPrice, option.Strike, rfr,
                            dividend, new DividendSchedule(Dt.Empty), volatility, 0, ref d, ref g, ref t);
        delta = d;
        gamma = g;
        theta = t;
      }
      else
        throw new ToolkitException("Unsupported option type");
      return fv;
    }

    /// <summary>
    /// Effective basket volatility
    /// </summary>
    private double CalcBasketVolatility()
    {
      var a = new double[BasketSize];
      var sigma = new double[BasketSize];

      // Computes the individual volatilities and weights.
      {
        double suma = 0, time = Time, rfr = Rfr;
        for (int i = 0; i < BasketSize; i++)
        {
          a[i] = (StockBasketOption.Amounts != null) ? StockBasketOption.Amounts[i] : 1.0;
          suma += a[i];
          sigma[i] = Volatilities[i].Interpolate(StockBasketOption.Expiration,
            CalculateForwardPrice(i, rfr, time), StockBasketOption.Strike);
        }
        for (int i = 0; i < BasketSize; i++)
        {
          a[i] /= suma;
        }
      }

      // Simple moment matching.
      double v = 0.0;
      for (int i = 0; i < BasketSize; i++)
      {
        double ai = a[i], si = sigma[i];
        for (int j = 0; j < i; j++)
          v += 2 * ai * a[j] * si * sigma[j] * Correlations[i, j];
        v += ai * ai * si * si;
      }
      return Math.Sqrt(v);
    }

    /// <summary>
    /// Basket price
    /// </summary>
    private double CalcBasketPrice()
    {
      double s = 0.0;
      for (int i = 0; i < BasketSize; i++)
      {
        var a = (StockBasketOption.Amounts != null) ? StockBasketOption.Amounts[i] : 1.0/BasketSize;
        var S = StockPrices[i];
        s += a * S;
      }
      return s;
    }

    /// <summary>
    /// Basket diviend price
    /// </summary>
    public double CalcBasketDividend()
    {
      double f = 0.0, s = 0.0;
      double rfr = Rfr;
      for (int i = 0; i < BasketSize; i++)
      {
        var a = (StockBasketOption.Amounts != null) ? StockBasketOption.Amounts[i] : 1.0/BasketSize;
        var S = StockPrices[i];
        s += a * S;
        if( Divs != null )
          for( int j = 0; j < Divs[i].Size(); j++ )
            S -= Divs[i].GetAmount(j) * Math.Exp(-(rfr) * Divs[i].GetTime(j));
        f += a * S * Math.Exp((rfr - Dividends[i]) * Time);
      }
      return rfr - Math.Log(f / s) / Time;
    }

    private double CalculateForwardPrice(int i, double rfr, double time)
    {
      double spot = StockPrices[i], div = Dividends[i];
      return spot * Math.Exp(time * (rfr - div));
    }
    #endregion Methods

    #region Data

    private double? _pv;
    private double? _delta;
    private double? _gamma;
    private double? _theta;
    private double? _vega;
    private double? _rho;
    private double? _vol;
    private double? _price;
    private double? _dividend;

    #endregion Data
  }
}
