//
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.StockCliquetOption">Stock Ratchet Option</see>
  ///   using the <see cref="BaseEntity.Toolkit.Models.RatchetOption"/> model.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.StockRatchetOption" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BlackScholesPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StockRatchetOption"/>
  /// <seealso cref="BlackScholesPricerBase"/>
  [Serializable]
  public partial class StockRatchetOptionPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Construct pricer using a single vol
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrice">Underlying stock price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dividend">%Dividend rate (continuous comp, eg. 0.05)</param>
    /// <param name="divs">Schedule of discrete dividends</param>
    /// <param name="vol">Volatility (eg. 0.2)</param>
    public StockRatchetOptionPricer(
      StockRatchetOption option, Dt asOf, Dt settle, double stockPrice,
      DiscountCurve discountCurve, double dividend, DividendSchedule divs, double vol
      )
      : base(option, asOf, settle)
    {
      // Set data, using properties to include validation
      StockPrice = stockPrice;
      DiscountCurve = discountCurve;
      Dividend = dividend;
      Divs = divs ?? new DividendSchedule(asOf);
      Volatility = vol;
      StrikeResets = new RateResets();
    }

    /// <summary>
    /// Construct pricer using a vol surface
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrice">Underlying stock price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dividend">%Dividend rate (continuous comp, eg. 0.05)</param>
    /// <param name="divs">Schedule of discrete dividends</param>
    /// <param name="volSurface">Volatility Surface</param>
    public StockRatchetOptionPricer(
      StockRatchetOption option, Dt asOf, Dt settle, double stockPrice,
      DiscountCurve discountCurve, double dividend, DividendSchedule divs, CalibratedVolatilitySurface volSurface
      )
      : base(option, asOf, settle)
    {
      // Set data, using properties to include validation
      StockPrice = stockPrice;
      DiscountCurve = discountCurve;
      Dividend = dividend;
      Divs = divs ?? new DividendSchedule(asOf);
      VolatilitySurface = volSurface;
      StrikeResets = new RateResets();
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
      if (StockPrice <= 0.0)
        InvalidValue.AddError(errors, this, "StockPrice", String.Format("Invalid price. Must be non negative, Not {0}", StockPrice));
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing discount curve");
      if (Dividend < 0.0 || Dividend > 2.0)
        InvalidValue.AddError(errors, this, "Dividend", String.Format("Invalid dividend {0}. Must be >= 0 and <= 2", Dividend));
      if (VolatilitySurface == null)
        InvalidValue.AddError(errors, this, "VolatilitySurface", "Invalid volatility surface. Cannot be null");
      else
        VolatilitySurface.Validate(errors);
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _pv = _delta = _gamma = _theta = _vega = _rho = null;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Current Price price of underlying asset
    /// </summary>
    public double StockPrice { get; private set; }

    /// <summary>
    /// Risk free rate (5 percent = 0.05)
    /// </summary>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, AsOf, StockRatchetOption.Expiration); }
    }

    /// <summary>
    /// Dividend rate (5 percent = 0.05)
    /// </summary>
    public double Dividend { get; private set; }

    /// <summary>
    /// Dividend rate (5 percent = 0.05)
    /// </summary>
    public DividendSchedule Divs { get; private set; }

    /// <summary>
    /// Volatility (5 percent = 0.05)
    /// </summary>
    public double Volatility
    {
      get { return VolatilitySurface.Interpolate(StockRatchetOption.Expiration, StockRatchetOption.Strike); }
      private set { VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(AsOf, value); }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public CalibratedVolatilitySurface VolatilitySurface { get; private set; }

    /// <summary>
    /// Time to expiration in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, StockRatchetOption.Expiration).Value; }
    }

    private IEnumerable<double> Resets
    {
      get
      {
        return StockRatchetOption.ResetDates
          .Select(d => (d - AsOf) / 365.25).Where(t => t > 0);
      }
    }

    /// <summary>
    /// Time to expiration in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, StockRatchetOption.Expiration).Days; }
    }

    /// <summary>
    ///   Ratchet Stock Option product
    /// </summary>
    public StockRatchetOption StockRatchetOption
    {
      get { return (StockRatchetOption)Product; }
    }

    /// <summary>
    ///   Discount Curve used for Payment
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Historical Strike Resets
    /// </summary>
    public RateResets StrikeResets { get; set; }

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
    /// Include the strike-resetting related payout on maturity to option pv calculation
    /// </summary>
    /// <returns></returns>
    public override double Pv()
    {
      if (Settle >= Product.Maturity)
        return 0.0;

      var pv = base.Pv();

      var ps = GetStrikeResetPayoutPaymentSchedule(Dt.Empty, Settle);
      pv += CurrentNotional * ps.CalculatePv(AsOf, Settle, DiscountCurve, null, null, 0.0, 0,
               TimeUnit.None, AdapterUtil.CreateFlags(false, false, false));

      return pv;
    }

    /// <summary>
    /// Produce the payment schedule
    /// </summary>
    /// <param name="from">from date</param>
    /// <param name="resetCutoff">resetCutoff date</param>
    /// <returns></returns>
    public PaymentSchedule GetStrikeResetPayoutPaymentSchedule(Dt from, Dt resetCutoff)
    {
      var strikeFixingDates = StrikeResets.OrderBy(sf => sf.Date)
        .Select(sf => sf.Date).ToArray();
      var strikeFixingStrikes = StrikeResets.OrderBy(sf => sf.Date)
        .Select(sf => sf.Rate).ToArray();
      var stockRachetOption = StockRatchetOption;
      var sign = stockRachetOption.Type == OptionType.Call ? 1.0 : -1.0;
      var ccy = stockRachetOption.Ccy;

      var ps = new PaymentSchedule();
      for (int idx = 1; idx < strikeFixingDates.Length; idx++)
      {
        var resetDate = strikeFixingDates[idx];
        if (!resetCutoff.IsEmpty() && resetDate > resetCutoff)
          continue;
        var payDate = stockRachetOption.PayoutOnResetDate
          ? resetDate
          : stockRachetOption.Maturity;
        if (!from.IsEmpty() && payDate <= from)
          continue;
        var amount = Math.Max(sign * (strikeFixingStrikes[idx]
                                      - strikeFixingStrikes[idx - 1]), 0.0);
        ps.AddPayment(new FixedInterestPayment(resetDate, payDate, ccy,
          Dt.Empty, Dt.Empty, resetDate, payDate, Dt.Empty, 1.0, 0.0,
          DayCount.Actual365Fixed, Frequency.None));
        ps.AddPayment(new PrincipalExchange(payDate, amount, ccy));
      }
      return ps;
    }


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
        _pv = FairValue(StockRatchetOption, StockPrice, Resets, Time, Volatility, Rfr,
          Dividend, Divs, ref _delta, ref _gamma, ref _vega, ref _theta, ref _rho);
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
        _delta = FairValue(StockRatchetOption, StockPrice + 1.0,
                   Resets, Time, Volatility, Rfr, Dividend, Divs) - fv;
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
        _gamma = FairValue(StockRatchetOption, StockPrice + 1.0,
                   Resets, Time, Volatility, Rfr, Dividend, Divs) +
                 FairValue(StockRatchetOption, StockPrice - 1.0,
                   Resets, Time, Volatility, Rfr, Dividend, Divs) - 2.0 * fv;
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
      {
        // The expiry and All the reset dates are added one day.
        const double oneDay = (1.0 / 365.0);
        _theta = FairValue(StockRatchetOption, StockPrice,
          Resets.Select(t => t + oneDay), Time + oneDay,
          Volatility, Rfr, Dividend, Divs) - fv;
      }
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
        _vega = FairValue(StockRatchetOption, StockPrice, Resets,
          Time, Volatility + 0.01, Rfr, Dividend, Divs) - fv;
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
        _rho = FairValue(StockRatchetOption, StockPrice, Resets,
          Time, Volatility, Rfr + 0.01, Dividend, Divs) - fv;
      return _rho.Value * Notional;
    }

    /// <summary>
    /// Calculates implied volatility from fair value
    /// </summary>
    /// <param name="price">Option price in percent</param>
    /// <returns>Implied vol of option</returns>
    public double IVol(double price)
    {
      return BinomialTree.ImpliedVolatility(OptionStyle.European, StockRatchetOption.Type, Time, 0, StockPrice,
        StockRatchetOption.Strike, Rfr, Dividend, Divs, 0, price);
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    private static double FairValue(StockRatchetOption option, double ulPrice,
      IEnumerable<double> resets, double time, double volatility, double rfr,
      double dividend, DividendSchedule divs)
    {
      double? delta = null, gamma = null, vega = null, theta = null, rho = null;
      return FairValue(option, ulPrice, resets, time, volatility, rfr, dividend, divs,
        ref delta, ref gamma, ref vega, ref theta, ref rho);
    }

    /// <summary>
    /// Calculates fair value (as a percentage of notional) of option
    /// </summary>
    /// <returns>Fair value of option</returns>
    private static double FairValue(StockRatchetOption option, double ulPrice,
      IEnumerable<double> resets, double time, double volatility, double rfr,
      double dividend, DividendSchedule divs, ref double? delta,
      ref double? gamma, ref double? vega, ref double? theta, ref double? rho)
    {
      double d = 0.0, g = 0.0, t = 0.0, v = 0.0, r = 0.0;
      double fv = BlackScholes.RatchetOptionValue(option.Type, resets,
        time, ulPrice, option.Strike, rfr, dividend, volatility, divs,
        ref d, ref g, ref t, ref v, ref r);
      delta = d;
      gamma = g;
      theta = t / 365.0; // Convert theta from years to 1 day
      vega = v / 100.0;
      rho = r / 100.0;
      return fv;
    }

    #endregion Methods

    #region Data

    private double? _pv;
    private double? _delta;
    private double? _gamma;
    private double? _theta;
    private double? _vega;
    private double? _rho;

    #endregion Data
  }
}