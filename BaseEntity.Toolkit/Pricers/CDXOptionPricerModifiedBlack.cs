//
// CDXOptionPricer.cs
//  -2008. All rights reserved.
//
#define DIRECT_BLACK

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Integrals;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.CDXOption">Credit Index Option (Option on Credit Index Note)</see>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDXOption" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// <para>This pricer is based on modifed Black formula.</para>
  /// <para>A factory class <see cref="CreditIndexOptionPricerFactory"/> provides a simple interface
  /// to the alternate credit index option models.</para>
  /// <para>The price of an index swap contract (CDX) with deal premium <m>F</m> at market spread <m>S</m>
  /// is given by</para>
  /// <math>P(S) = \gamma(S) (S - F)</math>
  /// <para>where <m>\gamma(S)</m> is the risky <c>PV01</c> calculated from a flat credit curve calibrated
  /// in such a way that its spread equals to the market spread <m>S</m>, using 40% recovery rate and
  /// current Libor curve.  Note that the risky <c>PV01</c> depends on market spread.  This differentiates
  /// the pricing model of an index contract from a CDS contract, since in the later case <c>PV01</c> does
  /// not depend market spread.</para>
  /// <para>To price an European option on an index swap contract, people usually assume that the forward
  /// market spread at the expiration date <m>T</m>, denoted as <m>S_T</m>, follows a log-normal
  /// distribution with standard deviation <m>\sigma</m>, which is called <em>volatility</em>.</para>
  /// <para>The <em>modified Black model</em> recognizes that the forward <c>PV01</c> is given by
  /// <m>\gamma(S_T)</m>, which depends on forward market spread <m>S_T</m>. Therefore it calculates the
  /// values of option by computing the expectations:</para>
  /// <math>\mathrm{Payer} = E[ (P(S_T) - P(K))^+ ]</math>
  /// <math>\mathrm{Receiver} = E[ (P(K) - P(S_T))^+ ]</math>
  /// <para>The negative correlation between the forward <c>PV01</c> and forward spread <m>S_T</m> prevents
  /// us from directly applying Black formula to the above expectations and a more sophisticated numerical
  /// evaluation procedure, which is accurate up to cubic order, is adopted instead.
  /// By taking into account the negative correlation between forward <c>PV01</c> and forward spread,
  /// the modified Black model often yields a smaller payer option value and a larger receiver option
  /// value than those calculated using the standard Black model.
  /// It also leads to different implied volatilities for a given option price in the two models.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDXOption">Option on CDX Note</seealso>
  /// <seealso cref="CDXOptionPricer">Pricer for Option on CDX Note</seealso>
  /// <seealso cref="CDXOptionPricerBlack">Standard Black model</seealso>
  /// <seealso cref="CreditIndexOptionPricerFactory">CDS Index Option Pricer factory</seealso>
  /// <example>
  /// <code language="C#">
  ///   DiscountCurve discountCurve;
  ///   SurvivalCurve [] survivalCurves;
  ///   Dt asOf, settle;
  ///
  ///   // Set up discountCurve, survivalCurves, asOf and settle dates.
  ///   // ...
  /// 
  ///   // Create underlying credit index
  ///   CDX note = new CDX(
  ///     new Dt(20, 6, 2004),                 // Effective date of the CDS index
  ///     new Dt(20, 6, 2009),                 // Maturity date of the CDS index
  ///     Currency.USD,                        // Currency of the CDS index
  ///     40/10000,                            // CDS index premium (40bp)
  ///     DayCount.Actual360,                  // Daycount of the CDS index premium
  ///     Frequency.Quarterly,                 // Payment frequency of the CDS index premium
  ///     BDConvention.Following,              // Business day convention of the CDS index
  ///     Calendar.NYB                         // Calendar for the CDS Index
  ///   );
  /// 
  ///   // Create the credit index option
  ///   CDXOption cdxOption = new CDXOption(
  ///     new Dt(20, 9, 2004),                 // Effective date of option
  ///     Currency.USD,                        // Currency of the CDS
  ///     note,                                // Underlying index note
  ///     new Dt(20, 11, 2004),                // Option expiration
  ///     PayerReceiver.Payer,                 // Option type
  ///     OptionStyle.European,                // Option style
  ///     0.0075,                              // Option strike of 75bp
  ///     false                                // Strike spreads instead of values
  ///   );
  /// 
  ///   // Create a pricing for the CDX Option.
  ///   CDXOptionPricer pricer = new CDXOptionPricerModifiedBlack(
  ///     cdxOption,                           // Index Option
  ///     asOf,                                // Pricing date
  ///     settle,                              // Settlement date
  ///     discountCurve,                       // Discount curve
  ///     survivalCurves,                      // Survival curves
  ///     60/10000.0,                          // current market spread (60bps)
  ///     1.2                                  // volatility 120%
  ///   );
  ///   pricer.ModelType = OptionModelType.ModifedBlack; // modified Black model
  ///   pricer.AdjustSpread = true;            // adjust spread by front end protection
  /// </code>
  /// </example>
  [Serializable]
  public class CDXOptionPricerModifiedBlack : CDXOptionPricer
  {
    // Logger
    private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CDXOptionPricerModifiedBlack));

    private const double tinyVolatility = 1.0E-4;

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>This constructor takes a reference to underlying credit curves.
    /// When the array of survival curves is not null, the <c>Pv()</c> method
    /// behaves differently than in a pricer constructed on market data only
    /// (without underlying credit curves).  In this case, the <em>implied
    /// market spread</em> calculated from the credit curves is used
    /// to compute <c>Pv</c> and therefore the changes in the credit curves
    /// causes changes in option values.</para>
    /// </remarks>
    /// <param name="option">CDX Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of CDS Index</param>
    /// <param name="marketQuote">Market quote of the underlying cdx</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    /// <example>
    /// <code language="C#">
    ///   DiscountCurve discountCurve;
    ///   SurvivalCurve [] survivalCurves;
    ///   Dt asOf, settle;
    ///
    ///   // Set up discountCurve, survivalCurves, asOf and settle dates.
    ///   // ...
    ///   // Create underlying credit index
    ///   CDX note = new CDX(
    ///     new Dt(20, 6, 2004),                 // Effective date of the CDS index
    ///     new Dt(20, 6, 2009),                 // Maturity date of the CDS index
    ///     Currency.USD,                        // Currency of the CDS index
    ///     40/10000,                            // CDS index premium (40bp)
    ///     DayCount.Actual360,                  // Daycount of the CDS index premium
    ///     Frequency.Quarterly,                 // Payment frequency of the CDS index premium
    ///     BDConvention.Following,              // Business day convention of the CDS index
    ///     Calendar.NYB                         // Calendar for the CDS Index
    ///   );
    ///   // Create the credit index option
    ///   CDXOption cdxOption = new CDXOption(
    ///     new Dt(20, 9, 2004),                 // Effective date of option
    ///     Currency.USD,                        // Currency of the CDS
    ///     note,                                // Underlying index note
    ///     new Dt(20, 11, 2004),                // Option expiration
    ///     PayerReceiver.Payer,                 // Option type
    ///     OptionStyle.European,                // Option style
    ///     0.0075,                              // Option strike of 75bp
    ///     false );                             // Strike spreads instead of values
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer = new CDXOptionPricerModifiedBlack(
    ///     cdxOption,                           // Index Option
    ///     asOf,                                // Pricing date
    ///     settle,                              // Settlement date
    ///     discountCurve,                       // Discount curve
    ///     survivalCurves,                      // Survival curves
    ///     60/10000.0,                          // current market spread (60bps)
    ///     1.2                                  // volatility 120%
    ///   );
    ///   pricer.ModelType = OptionModelType.Black; // standard Black model
    ///   pricer.AdjustSpread = true;            // adjust spread by front end protection
    /// </code>
    /// </example>
    public CDXOptionPricerModifiedBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double marketQuote,
      VolatilitySurface volatility)
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketQuote, volatility)
    {
      // Set data, using properties to include validation
      this.Center = Double.NaN;
    }

    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerModifiedBlack( 
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double marketQuote,
      double volatility )
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketQuote, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility))
    {
      // Set data, using properties to include validation
      this.Center = Double.NaN;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>This constructor takes a reference to underlying credit curves.
    ///   When the array of survival curves is not null, the <c>Pv()</c> method
    ///   behaves differently than in a pricer constructed on market data only
    ///   (without underlying credit curves).  In this case, the <em>implied
    ///   market spread</em> calculated from the credit curves is used
    ///   to compute <c>Pv</c> and therefore the changes in the credit curves
    ///   causes changes in option values.
    ///   </para>
    /// </remarks>
    /// <param name="option">CDX Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of CDS Index</param>
    /// <param name="marketCurve">Market curve of the underlying index</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    public CDXOptionPricerModifiedBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      SurvivalCurve marketCurve,
      CalibratedVolatilitySurface volatility)
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketCurve, volatility)
    {
      // Set data, using properties to include validation
      this.Center = Double.NaN;
    }

    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerModifiedBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      SurvivalCurve marketCurve,
      double volatility )
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketCurve, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility))
    {
      // Set data, using properties to include validation
      this.Center = Double.NaN;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDXOptionPricerModifiedBlack"/> class.
    /// </summary>
    /// <param name="option">CDX Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="basketSize">Size of the basket</param>
    /// <param name="marketQuote">Market quote of the underlying cdx</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    public CDXOptionPricerModifiedBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      int basketSize,
      double marketQuote,
      VolatilitySurface volatility)
      : base(option, asOf, settle, discountCurve, null, basketSize,
        marketQuote, volatility)
    {
      // Set data, using properties to include validation
      this.Center = Double.NaN;
    }

    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerModifiedBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      int basketSize,
      double marketQuote,
      double volatility)
      : base(option, asOf, settle, discountCurve, null, basketSize,
        marketQuote, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility))
    {
      // Set data, using properties to include validation
      this.Center = Double.NaN;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      CDXOptionPricerModifiedBlack obj = (CDXOptionPricerModifiedBlack)base.Clone();

      return obj;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Calculate the market value of the Option at the specified volatility.
    /// </summary>
    /// <param name="vol">Volatility.</param>
    /// <returns>the market value of the option in dollars</returns>
    /// <example>
    /// <code language="C#">
    ///   // ...
    ///   double vol    = 1.2;                           // volatility 120%
    ///
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve, spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate market value using volatility 60%
    ///   double marketValue = pricer.MarketValue( 0.6 );
    /// </code>
    /// </example>
    public override double
    MarketValue(double vol)
    {
      // On expiry date value is just the cash settled if exercised
      if (Dt.Cmp(CDXOption.Expiration, this.Settle) == 0)
        return Intrinsic();
      // after that option is done
      if (Dt.Cmp(CDXOption.Expiration, this.Settle) < 0)
        return 0;

      // The normal case
      double savedVol = this.Volatility;
      double value = 0;
      try
      {
        this.Volatility = vol;

        double mu = this.Center;
        if( Double.IsNaN(mu) )
          mu = ImpliedCenter();

        double v = ( this.Type == OptionType.Put ?
          ExpectedPayerValue( mu ) : ExpectedReceiverValue( mu ) );

        v *= SurvivalProbability(this.Spread) *
          DiscountCurve.DiscountFactor(CDXOption.Expiration)
          / DiscountCurve.DiscountFactor( AsOf );

        value = v * Notional;
      }
      finally
      {
        this.Volatility = savedVol;
      }

      return value;
    }


    /// <summary>
    ///   Calculate the implied center of forward distribution
    /// </summary>
    /// <remarks>
    ///   <para>This functions calculates the center value (mean) of the distribution
    ///   of forward spread,
    ///   using the modified Black model.</para>
    ///   <para>The center, denoted as <m>\mu</m>,
    ///   is the value that equating the expectation of the stochastic forward exercise
    ///   price to the exercise price evaluated at current market spread, i.e.,
    ///   <math>
    ///     \mathrm{P}(S_0) = E[\mathrm{P}(S_T) ],
    ///   </math>
    ///   where <m>P(S)</m> denotes the exercise price
    ///   evaluated at spread <m>S</m>,
    ///   <m>S_0</m> is current market spread,
    ///   <m>S_T</m> is stochastic forward spread,
    ///   which following a log-normal distribution
    ///   with mean <m>e^\mu</m> and the volatility
    ///   given by the user.</para>
    ///   <para>This method only applies to the modified Black model.  For the standard
    ///   Black model, the center is always 0.</para>
    /// </remarks>
    /// <returns>Center of the forward distribution.</returns>
    /// <example>
    /// <code language="C#">
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDSOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves, quotedSpread, volatility);
    ///   pricer.ModelType = OptionModelType.ModifiedBlack;
    ///   pricer.AdjustSpread = true;
    ///
    ///   // Calculate the implied center of forward spread
    ///   double mu = pricer.ImpliedCenter();
    /// </code>
    /// </example>
    public double
    ImpliedCenter()
    {
      double x0 = this.Spread;
      if( this.AdjustSpread )
        x0 = AdjustedForwardSpread( x0 );
      double target = ExerciseMarketValue( x0 );

      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(1e-2);
      rf.setToleranceF(1e-6);

      ExpectedMarketValueEvaluator fn =
        new ExpectedMarketValueEvaluator(this);

      // Solve
      double mu = rf.solve(fn, target, x0 - 0.05, x0 + 0.05 );

      return mu;
    }

    class ExpectedMarketValueEvaluator : SolverFn
    {
      private CDXOptionPricerModifiedBlack pricer_;

      public ExpectedMarketValueEvaluator(CDXOptionPricerModifiedBlack pricer)
      {
        pricer_ = pricer;
      }

      public override double evaluate(double x)
      {
        double v = pricer_.ExpectedMarketValue(x);
        return v;
      }
    }


    /// <summary>
    ///   Calculates implied volatility for index option
    /// </summary>
    /// <param name="fv">Fair value of CDX Option in dollars</param>
    /// <returns>Implied volatility of CDX Option</returns>
    /// <example>
    /// <code language="C#">
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate implied volatility
    ///   double vol = pricer.IVol(fv);
    /// </code>
    /// </example>
    public override double
    IVol(double fv)
    {
      logger.Debug( "Calculating implied volatility for CDS Index Option..." );

      double savedVol = this.Volatility;

      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(1e-3);
      rf.setToleranceF(1e-6);
      rf.setLowerBounds(1E-10);
      rf.setUpperBounds(9.9999);

      FairPriceEvaluator fn = new FairPriceEvaluator(this);

      // Solve
      double res;
      try
      {
        double v = fn.evaluate(0.1);
        if (v >= fv)
        {
          res = rf.solve(fn, fv, 0.01, 0.10);
          return res;
        }
        v = fn.evaluate(1.0);
        if (v >= fv)
        {
          res = rf.solve(fn, fv, 0.1, 1.0);
          return res;
        }
        v = fn.evaluate(2.0);
        if (v >= fv)
        {
          res = rf.solve(fn, fv, 1.0, 2.0);
          return res;
        }
        v = fn.evaluate(4.0);
        if (v >= fv)
        {
          res = rf.solve(fn, fv, 2.0, 4.0);
          return res;
        }
        
        res = rf.solve(fn, fv, 4.0, 8.0);
      }
      finally
      {
        // Tidy up transient data
        this.Volatility = savedVol;
      }

      return res;
    }

    class FairPriceEvaluator : SolverFn
    {
      private CDXOptionPricerModifiedBlack pricer_;

      public FairPriceEvaluator(CDXOptionPricerModifiedBlack pricer)
      {
        pricer_ = pricer;
      }

      public override double evaluate(double x)
      {
        pricer_.Volatility = x;
        double v = pricer_. FairPrice();
        return v;
      }
    }

    /// <summary>
    /// Index upfront value at a given market spread
    /// </summary>
    /// <remarks>
    /// <para>This is the present value of the expected forward value
    /// plus the front end protection.  The expectation is taken
    /// based on the particular distribution of spread or price
    /// with the given spread as the current market spread quote.
    /// The value is per unit notional and
    /// is discounted by both the survival probability from settle to expiry
    /// and the discount factor from as-of to expiry.</para>
    /// </remarks>
    /// <param name="spread">Market spread in raw number (1bp = 0.0001)</param>
    /// <param name="premium">The premium in raw numbers.</param>
    /// <returns>Index upfront value</returns>
    protected override double IndexUpfrontValue(double spread, double premium)
    {
      double savedSpread = this.Spread;
      this.Spread = spread;
      double mu = this.Center;
      if (Double.IsNaN(mu))
        mu = ImpliedCenter();
      double pv = this.ExpectedMarketValue(mu);
      this.Spread = savedSpread;
      pv *= SurvivalProbability(this.Spread) * 
        DiscountCurve.DiscountFactor(CDXOption.Expiration)
        / DiscountCurve.DiscountFactor(AsOf);
      return pv;
    }

    /// <summary>
    /// Calculate expected strike value at expiry.
    /// </summary>
    /// <remarks>
    /// <para>The value is discounted by both the interest rate and the survival probability.</para>
    /// </remarks>
    /// <returns>The expected strike value</returns>
    public override double StrikeValue()
    {
      double pv = StrikeIsPrice ?
        PriceToValue(Strike) : ExerciseMarketValue(Strike);
      pv *= SurvivalProbability(this.Spread);
      pv *= DiscountCurve.DiscountFactor(CDXOption.Expiration)
        / DiscountCurve.DiscountFactor(AsOf);
      return pv;
    }

    /// <summary>
    /// Calculate the probability that the option is in the money
    /// on the expiry.
    /// </summary>
    /// <param name="spread">The forward spread (unadjusted).</param>
    /// <returns>Probability.</returns>
    protected override double CalculateProbabilityInTheMoney(double spread)
    {
      double T = this.Time;
      double mu = this.Center;
      if (Double.IsNaN(mu))
        mu = ImpliedCenter();
      mu *= T;

      double pv01 = ForwardPV01(spread);
      if (this.AdjustSpread)
        spread += FrontEndProtection(spread) / pv01;
      double k = StrikeIsPrice ? PriceToSpread(Strike) : Strike;

      double sigma = this.Volatility * Math.Sqrt(T);
      k = (this.Type == OptionType.Put
             ? (Math.Log(spread/k) + mu - sigma*sigma/2)
             : (Math.Log(k/spread) - mu + sigma*sigma/2));

      return Normal.cumulative(k, 0.0, sigma);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///  Center value of forward distribution
    /// </summary>
    public double Center
    {
      get { return mu_; }
      set { mu_ = value; }
    }
    #endregion

    #region Data
    private double mu_;
    #endregion

    #region Integrals

    private double PV01(double spread)
    {
      CDXPricer pricer = ForwardCDXPricer(spread);
      double pv01 = pricer.RiskyDuration();
      return pv01;
    }

    class MarketPriceEvaluator : IUnivariateFunction
    {
      public MarketPriceEvaluator(
        CDXOptionPricerModifiedBlack pricer, double m, double p)
      {
        pricer_ = pricer;
        s_ = Math.Exp(m);
        if (double.IsNaN(s_) || double.IsInfinity(s_))
        {
          throw new SolverException(String.Format("The CDX Market Price could not be bracketed: strike price, volatility, or time to expiry may be wrong."));
        }
        p_ = p;
      }

      public double evaluate(double x)
      {
        double spread = s_ * x;
        if (spread > 1)
          spread = 1; // max spread
        return pricer_.ExerciseMarketValue(spread) - p_;
      }

      CDXOptionPricerModifiedBlack pricer_;
      private double s_, p_;
    }

    // compute the expectation of market values
    private double
    ExpectedMarketValue(double mu)
    {
      double x0 = this.Spread;
      if (this.AdjustSpread)
        x0 = AdjustedForwardSpread(x0);
      double T = this.Time;
      double std = this.Volatility * Math.Sqrt(T);
      if (std < 1E-7)
        return ExerciseMarketValue(x0);

      mu *= T;
      double m = Math.Log(x0) + mu - std * std / 2;

      if (DefaultQuadraturePoints <= 0)
      {
        double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        double d = (Math.Log(K / x0) - mu) / std;
        LogNormal quad = new LogNormal(d, std);
        MarketPriceEvaluator fn = new MarketPriceEvaluator(this, m, 0);
        return quad.Integral(fn);
      }

      double[] weights = gauss_quadrature_w_;
      double[] points = gauss_quadrature_x_;

      double sum = 0;
      for (int i = 0; i < points.Length; ++i)
      {
        double spread = Math.Exp(m + std * points[i]);
        double c = ExerciseMarketValue(spread);
        sum += c * weights[i];
      }

      return sum;
    }

    // compute the expectation of market values
    private double
    ExpectedPayerValue(double mu)
    {
      double x0 = this.Spread;
      if (this.AdjustSpread)
        x0 = AdjustedForwardSpread(x0);

      double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
      double gamma_K = PV01(K);

      double T = this.Time;
      double std = this.Volatility * Math.Sqrt(T);
      if (std < 1E-7)
      {
        // zero volatility
        if (x0 > K)
          return (PV01(x0) - gamma_K) * (x0 - CDX.Premium);
        else
          return 0.0;
      }

      mu *= T;
      double m = Math.Log(x0) + mu - std * std / 2;

      if (DefaultQuadraturePoints <= 0)
      {
        double d = (Math.Log(K / x0) - mu) / std;
        LogNormal quad = new LogNormal(d, std);
        MarketPriceEvaluator fn = new MarketPriceEvaluator(this, m,
          StrikeIsPrice ? PriceToValue(Strike) : ExerciseMarketValue(K));
        return quad.RightIntegral(fn);
      }

      double black = Toolkit.Models.Black.B(x0 * Math.Exp(mu) / K, std);
      black *= K * gamma_K;

      double corr = PayerCorrelationTerm(K, gamma_K, std, m);

      return black + corr;
    }


    // compute the expectation of market values
    private double
    ExpectedReceiverValue(double mu)
    {
      double x0 = this.Spread;
      if (this.AdjustSpread)
        x0 = AdjustedForwardSpread(x0);

      double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
      double gamma_K = PV01(K);

      double T = this.Time;
      double std = this.Volatility * Math.Sqrt(T);
      if (std < 1E-7)
      {
        // zero volatility
        if (x0 < K)
          return (gamma_K - PV01(x0)) * (x0 - CDX.Premium);
        else
          return 0.0;
      }

      mu *= T;
      double m = Math.Log(x0) + mu - std * std / 2;

      if (DefaultQuadraturePoints <= 0)
      {
        double d = (Math.Log(K / x0) - mu) / std;
        LogNormal quad = new LogNormal(d, std);
        MarketPriceEvaluator fn = new MarketPriceEvaluator(this, m,
          StrikeIsPrice ? PriceToValue(Strike) : ExerciseMarketValue(K));
        return -quad.LeftIntegral(fn);
      }

      double spread = x0 * Math.Exp(mu);
      double black = Toolkit.Models.Black.B(K / spread, std);
      black *= spread * gamma_K;

      double corr = ReceiverCorrelationTerm(K, gamma_K, std, m);

      return black + corr;
    }

    /// <summary>
    ///   Calculate correlation effect of a payer option
    /// </summary>
    /// <param name="K">Strike</param>
    /// <param name="gamma_K">Forward PV01 at strike</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="m">
    ///   Log of multiplier such that <c>exp(m + sigma*z)</c>
    ///   is the forward spread.
    /// </param>
    /// <returns>Correlation effect</returns>
    internal double PayerCorrelationTerm(
      double K, double gamma_K,
      double sigma, double m)
    {
      double C = this.CDX.Premium;

      double[] weights = gauss_quadrature_w_;
      double[] points = gauss_quadrature_x_;

      int start = -1;
      if (K <= 1E-12 || sigma <= 1E-12)
        start = 0;
      else
      {
        double lower_bound = (Math.Log(K) - m) / sigma;
        for (int i = 0; i < points.Length; ++i)
          if (points[i] > lower_bound)
          {
            start = i;
            break;
          }
        if (start < 0)
          return 0.0;
      }

      double sum = 0;
      for (int i = start; i < points.Length; ++i)
      {
        double spread = Math.Exp(m + sigma * points[i]);
        double gamma = PV01(spread);
        double t = (gamma - gamma_K) * (spread - C);
        sum += t * weights[i];
      }
      return sum;
    }

    /// <summary>
    ///   Calculate correlation effect of a receiver option
    /// </summary>
    /// <param name="K">Strike</param>
    /// <param name="gamma_K">Forward PV01 at strike</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="m">
    ///   Log of multiplier such that <c>exp(m + sigma*z)</c>
    ///   is the forward spread.
    /// </param>
    /// <returns>Correlation effect</returns>
    internal double ReceiverCorrelationTerm(
      double K, double gamma_K, double sigma, double m)
    {
      double C = this.CDX.Premium;

      double[] weights = gauss_quadrature_w_;
      double[] points = gauss_quadrature_x_;

      int start = -1;
      if (K <= 1E-12 || sigma <= 1E-12)
        start = points.Length - 1;
      else
      {
        double upper_bound = (Math.Log(K) - m) / sigma;
        for (int i = points.Length - 1; i >= 0; --i)
          if (points[i] < upper_bound)
          {
            start = i;
            break;
          }
        if (start < 0)
          return 0.0;
      }

      double sum = 0;
      for (int i = start; i >= 0; --i)
      {
        double spread = Math.Exp(m + sigma * points[i]);
        double gamma = PV01(spread);
        double t = (gamma_K - gamma) * (spread - C);
        sum += t * weights[i];
      }
      return sum;
    }

    /// <summary>
    ///   Default quadrature points
    /// </summary>
    /// <exclude />
    private static int DefaultQuadraturePoints
    {
      get { return defaultQuadraturePoints_; }
    }

    private static readonly double[] gauss_quadrature_x_ = null;
    private static readonly double[] gauss_quadrature_w_ = null;
    private static readonly int defaultQuadraturePoints_ = 0;

    #endregion // Integrals

  }
}
