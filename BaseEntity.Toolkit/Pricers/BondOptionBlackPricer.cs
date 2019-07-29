// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.Diagnostics;
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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.BondOption">Bond Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.BondOption" />
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.BondOption">Bond Option</seealso>
  [Serializable]
  public class BondOptionBlackPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="bondQuote">Bond quote</param>
    /// <param name="bondQuotingConvention">Bond quoting convention</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="volatility">Volatility</param>
    /// <param name="notional">Total notional</param>
    public BondOptionBlackPricer(
      BondOption option, Dt asOf, Dt settle, double bondQuote, QuotingConvention bondQuotingConvention,
      double rfr, double volatility, double notional)
      : base(option, asOf, settle)
    {
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility);
      BondQuote = bondQuote;
      BondQuotingConvention = bondQuotingConvention;
      Notional = notional;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="bondQuote">Underlying bond quote</param>
    /// <param name="bondQuotingConvention">Underlying bond quoting convention</param>
    /// <param name="discountCurve">Discount/term repo curve pricing</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="notional">Total notional</param>
    public BondOptionBlackPricer(
      BondOption option, Dt asOf, Dt settle, double bondQuote, QuotingConvention bondQuotingConvention,
      DiscountCurve discountCurve, IVolatilitySurface volSurface, double notional)
      : base(option, asOf, settle)
    {
      DiscountCurve = discountCurve;
      VolatilitySurface = volSurface;
      BondQuote = bondQuote;
      BondQuotingConvention = bondQuotingConvention;
      Notional = notional;
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
      if (DiscountCurve == null && Rfr < 0.0)
        InvalidValue.AddError(errors, this, "Rfr", String.Format("Invalid discount rate {0}, Must be >= 0", Rfr));
      if (VolatilitySurface == null && Volatility < 0.0)
        InvalidValue.AddError(errors, this, "Volatility", String.Format("Invalid volatility {0}, Must be >= 0", Volatility));
      if (BondQuote <= 0.0)
        InvalidValue.AddError(errors, this, "BondQuote", String.Format("Invalid Bond market quote {0}, must be >= 0", BondQuote));
      if (BondQuotingConvention == QuotingConvention.None)
        InvalidValue.AddError(errors, this, "BondQuotingConvention", String.Format("Invalid Bond quoting convention {0}", BondQuotingConvention));
    }

    /// <summary>
    /// calculate product pv
    /// </summary>
    /// <returns></returns>
      public override double ProductPv()
      {
       double survivalAtExpiry = (SurvivalCurve != null) ? SurvivalCurve.SurvivalProb(BondOption.Expiration) : 1.0;
       return base.ProductPv()* survivalAtExpiry;
      }

      /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _bondPricer = null;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration date in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, BondOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, BondOption.Expiration).Days; }
    }

    /// <summary>
    /// Market quote of underlying bond
    /// </summary>
    public double BondQuote { get; set; }

    /// <summary>
    /// Quoting convention of underlying bond
    /// </summary>
    public QuotingConvention BondQuotingConvention { get; set; }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, BondOption.Expiration); }
    }

    /// <summary>
    /// Discount (repo) Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Projection Curve
    /// </summary>
    public DiscountCurve ReferenceCurve { get; set; }

    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    ///   Flat volatility to option expiration
    /// </summary>
    /// <remarks>
    ///   <para>Flat volatility to option expiration date.</para>
    /// </remarks>
    public double Volatility
    {
      get { return VolatilitySurface.Interpolate(BondOption.Expiration, BondForwardFlatPrice(), BondOption.Strike); }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }

    /// <summary>
    /// Number of contracts
    /// </summary>
    public double Contracts
    {
      get { return Notional / Product.Notional; }
      set { Notional = BondOption.Bond.Notional * value; }
    }

    /// <summary>
    /// Bond option product
    /// </summary>
    public BondOption BondOption
    {
      get { return (BondOption)Product; }
    }

    /// <summary>
    /// Pricer for underlying bond
    /// </summary>
    public BondPricer BondPricer
    {
      get
      {
        if (_bondPricer == null)
        {
          _bondPricer = new BondPricer(BondOption.Bond, AsOf, Settle, DiscountCurve, SurvivalCurve, 0, TimeUnit.None, -1)
          {
            MarketQuote = BondQuote,
            QuotingConvention = BondQuotingConvention
          };
          if (ReferenceCurve != null)
            _bondPricer.ReferenceCurve = ReferenceCurve;
        }
        return _bondPricer;
      }
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
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying quoted price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return BondForwardFlatPrice(); } }
    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice { get { return BondForwardModelValue(); } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr { get { return Rfr; } }
    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend { get { return Rfr; } }
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

    #endregion Properties

    #region Methods

    #region UL Bond Calculations

    /// <summary>
    /// Full price of underlying bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Pricers.BondPricer.FullPrice()" />
    /// </remarks>
    /// <returns>Full price of underlying bond</returns>
    public double BondFullPrice()
    {
      return BondPricer.CurrentFullPrice();
    }

    /// <summary>
    /// Clean price of underlying bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Pricers.BondPricer.FlatPrice()" />
    /// </remarks>
    /// <returns>Clean price of underlying bond</returns>
    public double BondFlatPrice()
    {
      // Hack as temp workaround to bondpricer bug. RTD Oct'11
      return (BondQuotingConvention == QuotingConvention.FlatPrice) ? BondQuote : BondPricer.FlatPrice();
    }

    /// <summary>
    /// Accrued of underlying bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Pricers.BondPricer.Accrued()" />
    /// </remarks>
    /// <returns>Accrued of underlying bond on delivery date</returns>
    public double BondAccrued()
    {
      return BondPricer.Accrued();
    }

    /// <summary>
    /// Yield to maturity of underlying bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Pricers.BondPricer.YieldToMaturity()" />
    /// </remarks>
    /// <returns>Yield of underlying bond</returns>
    public double BondYieldToMaturity()
    {
      return BondPricer.YieldToMaturity();
    }

    /// <summary>
    /// Pv01 of underlying bond on settlement date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Pricers.BondPricer.PV01()" />
    /// </remarks>
    /// <returns>Yield of underlying bond</returns>
    public double BondPv01()
    {
      return BondPricer.PV01();
    }

    /// <summary>
    /// Forward full price of underlying bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Forward full price as a percentage of notional implied from the current underlying price
    ///   and the discount (funding) curve.</para>
    /// </remarks>
    /// <returns>Forward full price of underlying bond</returns>
    public double BondForwardFullPrice()
    {
      return BondForwardFullPrice(BondFullPrice());
    }

    /// <summary>
    /// Forward full price of underlying bond on expiration date
    /// </summary>
    /// <remarks>
    ///   <para>Forward full price as a percentage of notional implied from the current underlying price
    ///   and the discount (funding) curve.</para>
    /// </remarks>
    /// <param name="fullPrice">Bond full price</param>
    /// <returns>Forward full price of underlying bond</returns>
    public double BondForwardFullPrice(double fullPrice)
    {
      return BondPricer.ForwardFullPrice(BondOption.Expiration, fullPrice);
    }

    /// <summary>
    /// Forward clean price of underlying bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Forward clean price as a percentage of notional implied from the current underlying price
    ///   and the discount (funding) curve.</para>
    /// </remarks>
    /// <returns>Forward clean price of underlying bond</returns>
    public double BondForwardFlatPrice()
    {
      return BondForwardFullPrice() - BondForwardAccrued();
    }

    /// <summary>
    /// Forward clean pv of underlying bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Forward clean price as a percentage of notional implied from the current underlying price
    ///   and the discount (funding) curve.</para>
    /// </remarks>
    /// <returns>Forward clean pv of underlying bond</returns>
    public double BondForwardModelValue()
    {
      return BondForwardFullPrice(BondPricer.Pv()) - BondForwardAccrued();
    }

    /// <summary>
    /// Forwards accrued of underlying bond on delivery date
    /// </summary>
    /// <remarks>
    ///   <para>Accrued of the underlying on the futures last delivery date as a percentage of notional.</para>
    /// </remarks>
    /// <returns>Forward accrued of underlying bond</returns>
    public double BondForwardAccrued()
    {
      return BondPricer.AccruedInterest(BondOption.Expiration, BondOption.Expiration);
    }

    #endregion UL Bond Calculations

    #region Sensitivity Calculations

    /// <summary>
    ///   Model implied change in option value for a 1 dollar increase (0.01) in the underlying bond price
    /// </summary>
    /// <remarks>
    ///   <para>The price value of an 01 is the change in the value of the option implied by
    ///   a one dollar change in the underlying bond.</para>
    ///   <para>The futures price 01 is:</para>
    ///   <formula>
    ///     Price01 = V[F_t(P+0.01/2)] - V[F_{t}(P-0.01/2)]
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Price01</formula> is the price 01</description></item>
    ///     <item><description><formula inline="true">V(f)</formula> is the futures contract value given a futures model price <m>f</m></description></item>
    ///     <item><description><formula inline="true">F(p)</formula> is the futures model price given a underlying bond price p</description></item>
    ///     <item><description><formula inline="true">P</formula> is the underlying bond price</description></item>
    ///   </list>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   CTD forward bond price divided by the conversion factor times the tick value.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   underlying nominal bond forward yield given a 1 dollar increase in the current price.</para>
    /// </remarks>
    /// <returns>Price value of the underlying bond price 01 as a percentage of notional</returns>
    public double Price01()
    {
      var fullPrice = BondPricer.FullPrice();
      const double bump = 0.01;
      var fwdPriceUp = BondForwardFullPrice(fullPrice + bump / 2.0);
      var fwdPriceDn = BondForwardFullPrice(fullPrice - bump / 2.0);
      // Change in option value
      return (FairPrice(fwdPriceUp) - FairPrice(fwdPriceDn));
    }

    /// <summary>
    ///   Model implied change in futures value for a 1 dollar increase in the underlying bond price
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="Price01()" />
    /// </remarks>
    /// <returns>Dollar price value of the underying bond price 01</returns>
    public double Price01Value()
    {
      return Price01() * Notional;
    }

    /// <summary>
    ///   Model implied change in option value for a 1bp drop in underlying bond yield
    /// </summary>
    /// <remarks>
    ///   <para>The price value of an 01 is the change in the value of the option implied by
    ///   a one bp change in the underlying bond yield.</para>
    ///   <para>The futures price 01 is:</para>
    ///   <formula>
    ///     Price01 = V[F_t(P + \frac{\textup{pv01}^{ul}}{2})] - V[F_{t}(P-\frac{\textup{pv01}^{ul}}{2})]
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Price01</formula> is the price 01</description></item>
    ///     <item><description><formula inline="true">V[f]</formula> is the futures contract value given a futures model price <m>f</m></description></item>
    ///     <item><description><formula inline="true">F(p)</formula> is the futures model price given a underlying bond price p</description></item>
    ///     <item><description><formula inline="true">P</formula> is the underlying bond price</description></item>
    ///     <item><description><formula inline="true">pv01^{ul}</formula> is the underlying bond pv01</description></item>
    ///   </list>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   CTD forward bond price from a 1bp drop in the CTD yield divided by the conversion
    ///   factor times the tick value.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   underlying nominal bond forward yield given a 1bp drop in the bond yield.</para>
    ///   <note>The Pv01 is expressed as a percentage of notional.</note>
    /// </remarks>
    /// <returns>Price value of the underlying bond yield 01 as a percentage of notional</returns>
    public double Pv01()
    {
      if (DiscountCurve == null)
        throw new ArgumentException("Discount/Funding curve required");
      var fullPrice = BondPricer.FullPrice();
      var bump = BondPricer.PV01();
      var fwdPriceUp = BondForwardFullPrice(fullPrice + bump / 2.0);
      var fwdPriceDn = BondForwardFullPrice(fullPrice - bump / 2.0);
      // Change in futures value
      return (FairPrice(fwdPriceUp) - FairPrice(fwdPriceDn));
    }

    /// <summary>
    ///   Model implied change in option value for a 1bp drop in the underlying bond yield
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="Pv01()" />
    /// </remarks>
    /// <returns>Dollar price value of the underlying yield 01</returns>
    public double Pv01Value()
    {
      return Pv01() * Notional;
    }

    /// <summary>
    ///   Model implied change in option value for a 1bp drop in underlying bond forward yield
    /// </summary>
    /// <remarks>
    ///   <para>The forward value of an 01 is the change in the value of the option implied by
    ///   a one bp change in the underlying bond forward yield.</para>
    ///   <para>The futures price 01 is:</para>
    ///   <formula>
    ///     Price01 = V[F_t(P_T + \frac{\textup{pv01}^{ul}_T}{2})] - V[F_t(P_T - \frac{pv01^{ul}_T}{2}]
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Price01</formula> is the price 01</description></item>
    ///     <item><description><formula inline="true">V(f)</formula> is the futures contract value given a futures model price <m>f</m></description></item>
    ///     <item><description><formula inline="true">F(p_T)</formula> is the futures model price given a underlying bond forward price p_T</description></item>
    ///     <item><description><formula inline="true">P_T</formula> is the underlying bond forward price</description></item>
    ///     <item><description><formula inline="true">\textup{pv01}^{ul}_T</formula> is the underlying bond forward pv01</description></item>
    ///   </list>
    ///   <para><b>Price Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from the change in the
    ///   CTD forward bond price from a 1bp drop in the CTD forward yield divided by the conversion
    ///   factor times the tick value.</para>
    ///   <para><b>Indexed Yield Quoted Futures</b></para>
    ///   <para>The theoretical change in the futures value is calculated from a 1bp drop in the
    ///   underlying nominal bond forward yield.</para>
    /// </remarks>
    /// <returns>Price value of the underlying forward yield 01</returns>
    public double Fv01()
    {
      var fwdSettle = BondOption.Expiration;
      var fwdFullPrice = BondForwardFullPrice();
      var fwdAccrued = BondForwardAccrued();
      var fwdYield = BondPricer.FwdYield(fwdFullPrice, fwdSettle, 0.0, YieldCAMethod.None);
      var ctdFwd01 = BondPricer.FwdPv01(fwdSettle, fwdFullPrice, fwdYield);
      var fwdPriceUp = (fwdFullPrice - fwdAccrued + ctdFwd01 / 2.0);
      var fwdPriceDn = (fwdFullPrice - fwdAccrued - ctdFwd01 / 2.0);
      // Change in futures value
      return (FairPrice(fwdPriceUp) - FairPrice(fwdPriceDn));
    }

    /// <summary>
    ///   Model implied change in option value for a 1bp drop in underlying bond forward yield
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="Pv01()" />
    /// </remarks>
    /// <returns>Dollar price value of the underlying forward yield 01</returns>
    public double Fv01Value()
    {
      return Fv01() * Notional;
    }

    /// <summary>
    /// Modified duration
    /// </summary>
    /// <remarks>
    ///   <para>This is the price sensitivity to a 1bp yield change divided by the price.</para>
    ///   <para>The modified duration is calculated as:</para>
    ///   <formula>
    ///     DUR_mod = \frac{Pv01}{P}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">DUR_mod</formula> is the modified duration</description></item>
    ///			<item><description><formula inline="true">Pv01</formula> is the price change for a 1bp drop in the CTD yield</description></item>
    ///     <item><description><formula inline="true">P</formula> is the price</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Modified duration</returns>
    public double ModDuration()
    {
      return 10000.0 * Pv01() / BondFullPrice();
    }

    /// <summary>
    /// Convexity
    /// </summary>
    /// <remarks>
    ///   <para>This is the Pv01 sensitivity to a 1bp yield change divided by the price.</para>
    ///   <para>The convexity is calculated as:</para>
    ///   <formula>
    ///     C = \frac{C^{ul}}{Cf}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">c</formula> is the futures convexity</description></item>
    ///			<item><description><formula inline="true">C^{ul}</formula> is the convexity of the nomial deliverable bond</description></item>
    ///     <item><description><formula inline="true">Cf</formula> is the conversion factor</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Convexity</returns>
    public double Convexity()
    {
      // Note: Should really be using bond forward convexity
      // Note: Bond convexity scaled 100 more than Bloomberg. Adjust spreadsheet now but maybe change function later.
      return BondPricer.Convexity();
    }

    #endregion Sensitivity Calculations

    #endregion Methods

    #region IVolatilitySurfaceProvider Methods

    System.Collections.Generic.IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      if (VolatilitySurface != null) yield return VolatilitySurface;
    }

    #endregion IVolatilitySurfaceProvider Methods

    #region Data

    private BondPricer _bondPricer;

    #endregion Data
  }

  internal static class ForwardBondPriceCalculator
  {
    internal static double CurrentFullPrice(this BondPricer pricer)
    {
      Dt settle = pricer.Settle, effective = pricer.Bond.Effective;
      var sc = pricer.SurvivalCurve;
      if (sc == null || pricer.MarketQuote > 0 || settle >= effective)
      {
        return pricer.FullPrice();
      }
      return Calculate(pricer.FullModelPrice, effective)
        *sc.SurvivalProb(settle, effective);
    }

    internal static double ForwardFullPrice(this BondPricer pricer,
      Dt expiry, double fullPrice)
    {
      if (!pricer.IsProductActive(expiry) || pricer.IsOnLastDate(expiry))
        return 0.0;

      if (pricer.HasDefaulted)
        return pricer.FwdFullPrice(expiry, fullPrice);

      return pricer.BondCashflowAdapter.PsForwardValue(pricer,
        pricer.ProtectionStart, fullPrice, expiry);
    }

    internal static double PsForwardValue(this CashflowAdapter cf,
      BondPricer pricer, Dt settle, double fullPrice, Dt expiry)
    {
      var dc = pricer.DiscountCurve;
      var sc = pricer.SurvivalCurve;

      var effective = cf.Effective;
      if (effective > expiry)
      {
        var pv = cf.Pv(effective, effective, dc, sc, null,
          0.0, pricer.StepSize, pricer.StepUnit, pricer.CashflowModelFlags);

        // Bond will be issued after the expiry
        return pv*dc.DiscountFactor(expiry, effective)*(
          sc == null ? 1.0 : sc.SurvivalProb(expiry, effective));
      }
      if (effective > settle)
      {
        // Bond will be issued after the settle but before the expiry
        return cf.Pv(expiry, expiry, dc, sc, null, 0.0,
          pricer.StepSize, pricer.StepUnit, pricer.CashflowModelFlags);
      }

      // Bond issued on or before the settle
      var df = dc.DiscountFactor(settle, expiry);
      var sp = sc == null ? 1.0 : sc.SurvivalProb(settle, expiry);

      int idx = 0;
      for (int n = cf.Count; idx < n; ++idx)
      {
        if (cf.GetEndDt(idx) > expiry) break;
      }

      var cfpv = PaymentScheduleUtils.PvTo(cf, settle,
        settle, dc, sc, null, 0.0, pricer.CashflowModelFlags,
        pricer.StepSize, pricer.StepUnit, expiry);

      Dt begin;
      if (sc != null && (begin = cf.GetStartDt(idx)) < expiry)
      {
        Dt end = cf.GetEndDt(idx);
        double df0 = 1.0, sp0 = 1.0;
        if (begin > settle)
        {
          df0 = dc.DiscountFactor(settle, begin);
          sp0 = sc.SurvivalProb(settle, begin);
        }
        double df1 = dc.DiscountFactor(settle, end),
          sp1 = sc.SurvivalProb(settle, end);
        // we should add protection till expiry
        cfpv += 0.5*((df0 + df1)*(sp0 - sp1) - (df + df1)*(sp - sp1))
          *cf.GetDefaultAmount(idx);
      }
      return (fullPrice - cfpv)/df/sp;
    }

    private static double Calculate(Func<double> fn, Dt protectionStart)
    {
      var pricer = (BondPricer)fn.Target;
      var savedSettle = pricer.Settle;
      try
      {
        pricer.Settle = protectionStart;
        return fn();
      }
      finally
      {
        pricer.Settle = savedSettle;
      }
    }
  }
}
