/*
 * CDXOptionPricer.cs
 *
 *
 */
#define DIRECT_BLACK
#define CALL_MODEL

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.CDXOption">CDS Index Option (Option on CDX Note)</see>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDXOption" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// <para>This pricer is based on standard Black formula.</para>
  /// <para>A factory class <see cref="CreditIndexOptionPricerFactory"/> provides a simple interface
  /// to the alternate credit index option models.</para>
  /// <para>The price of an index swap contract (CDX) with deal premium <formula inline="true">F</formula>
  /// at market spread <formula inline="true">S</formula> is given by</para>
  /// <formula>P(S) = \gamma(S) (S - F)</formula>
  /// <para>where <formula inline="true">\gamma(S)</formula> is the risky <c>PV01</c> calculated from
  /// a flat credit curve calibrated in such a way that its spread equals to the market spread
  /// <formula inline="true">S</formula>, using 40% recovery rate and current Libor curve.
  /// Note that the risky <c>PV01</c> depends on market spread.  This differentiates the pricing
  /// model of an index contract from a CDS contract, since in the later case <c>PV01</c> does not depend
  /// market spread.</para>
  /// <para>There are two approches to price an European option on an index swap contract
  /// based on the standard Black formula: the spread volatility approach and the price
  /// volatility approach.</para>
  /// 
  /// <para><b>The Spread Volatility Approach</b></para>
  /// <para>This approach assumes that the forward
  /// market spread at the expiration date <formula inline="true">T</formula>,
  /// denoted as <formula inline="true">S_T</formula>,
  /// follows a log-normal distribution with standard deviation
  /// <formula inline="true">\sigma</formula>, which is called <em>volatility</em>.</para>
  /// <para>The <em>standard Black model</em> ignores the dependency of forward <c>PV01</c> on
  /// forward market spread and treats the index option in the same way as a CDS option.
  /// In other words, in the model the prices of options given by:</para>
  /// <formula>\mathrm{Payer} = \mathrm{PV01} \cdot E[ (S_T - K)^+ ]</formula>
  /// <formula>\mathrm{Receiver} = \mathrm{PV01} \cdot E[ (K - S_T)^+ ]</formula>
  /// <para>where <formula inline="true">K</formula> is strike on spread,
  /// <formula inline="true">\mathrm{PV01} \equiv \gamma(S_0)</formula>
  /// is evaluated at current market spread <formula inline="true">S_0</formula>.
  /// Standard black formula is directly applied to calculate the expeactation part
  /// in the above formula.</para>
  /// <para>Although it has deficiency, this simple standard Black model seems being
  /// widely used in the industry.</para>
  /// 
  /// <para><b>The Price Volatility Approach</b></para>
  /// <para>Some indices, especially the high yield indices, are normally quoted in
  /// 100-based prices in stead of spreads.  The standard Black model of price volatility
  /// assumes that the market price itself follows log-normal distribution with constant
  /// volatility .  Let <formula inline="true">P_T</formula> be the market price of the
  /// underlying index on the expiry time <formula inline="true">T</formula>,
  /// this approach assumes that
  /// <formula>
  ///   P_T = P_0 X_T
  /// </formula>
  /// where <formula inline="true">P_0</formula> is the forward price on the expiry
  /// as implied by current market quotes, <formula inline="true">X_T</formula>
  /// a log-normal variable with mean 1 and standard deviation
  /// <formula inline="true">\sigma \sqrt{T}</formula>.
  /// The prices of options are given by:
  /// <formula>\mathrm{Payer} = E[ (P_T - P_K)^+ ]</formula>
  /// <formula>\mathrm{Receiver} = E[ (P_K - P_T)^+ ]</formula>
  /// where <formula inline="true">P_K</formula> is the strike on price.
  /// Again, the standard Black formula is directly applied
  /// to calculate the expectation part in the above formula.
  /// Generally speaking, the distribution of price as implied from the spread volatility
  /// approach is more skewed than the log-normal distribution of the price itself
  /// and the price volatility is much smaller in magnitude than the spread volatility.</para>
  /// 
  /// <p><b>Front End Protection</b></p>
  /// <para>The contract may include <i>front end protection</i> (FEP) provisions which function
  /// in much the same way as knock-ins do in the single name CDS option
  /// world. Essentially the ultimate seller of protection will compensate the buyer of
  /// protection for defaults that occur during the life of the option. In a modeling sense, this
  /// translates into adjusting the expected forward spread, which is used as the center (mean)
  /// of the distribution used in either Black analysis above. The calculation can be performed
  /// two ways, depending upon the inputs available. This is a recurring theme and will be
  /// discussed in more detail in a later section. If only the market spread is available, the
  /// expected losses over the life of the option are calculated using the <c>flat curve approach</c>
  /// which consists of pricing the index as a single name CDS using a credit curve that is flat
  /// at the market spread with an assumed 40% recovery rate. If all the underlying credit
  /// curves are available, the expected losses are calculated by aggregating the expected
  /// losses on a name by name basis using the individual credit curves to perform this
  /// calculation. It is important that the underlying curves be scaled to remove the index basis
  /// (as in base correlation calibration) so this replicating portfolio approach captures the
  /// current index quote properly.</para>
  ///
  /// <p><b>Adjusting the forward spread</b></p>
  /// <para>To account for front end protection, the forward spread is modified in calculating the
  /// value of the option. The front end protection is always in favor of the payer (the ultimate
  /// protection buyer should the option be exercised) so the adjustment is always a positive
  /// change to the forward spread which makes the option more likely to be in the money for
  /// a payer option and less likely to be in the money for a receiver option. The adjustment is
  /// quite literally the spread adjustment equivalent to the front end protection expected
  /// payment. More formally, let <formula inline="true">\epsilon</formula> be the forward spread
  /// adjustment, which is calculated via
  /// <formula inline="true">\epsilon = DF_T E[ L_T ] / PV01_T</formula>
  /// where the numerator is the discounted expected cumulative losses
  /// on the index from 0 to <i>T</i>, the expiration date of the option, and the denominator is the
  /// forward annuity of the index for one basis point. Calculation of the numerator is, as
  /// noted above, dependent on whether the <c>flat curve approach</c> is used or the replicating
  /// portfolio approach. The flat curve approach generally yields larger spread adjustments –
  /// sometimes quite significantly so – as the replicating portfolio approach uses the
  /// individual curves which are generally upward sloping, with a smaller spread than the
  /// market quote flat curve level at the short end of the term structure. Currently in the
  /// Toolkit the denominator is always calculated using the forward spread as the
  /// basis for a flat curve calculation of the annuity.</para>
  ///
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDXOption">Option on CDX Note</seealso>
  /// <seealso cref="CDXOptionPricer">Pricer for Option on CDX Note</seealso>
  /// <seealso cref="CDXOptionPricerModifiedBlack">Modified Black model</seealso>
  /// <seealso cref="CreditIndexOptionPricerFactory">CDS Index Option Pricer factory</seealso>
  /// <example>
  /// <code language="C#">
  ///   DiscountCurve discountCurve;
  ///   SurvivalCurve [] survivalCurves;
  ///   CalibratedVolatilitySurface volSurface;
  ///   Dt asOf, settle;
  /// 
  ///   // Set up discountCurve, survivalCurves, volSurface, asOf and settle dates.
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
  ///   CDXOptionPricer pricer = new CDXOptionPricerBlack(
  ///     cdxOption,                           // Index Option
  ///     asOf,                                // Pricing date
  ///     settle,                              // Settlement date
  ///     discountCurve,                       // Discount curve
  ///     survivalCurves,                      // Survival curves
  ///     60/10000.0,                          // current market spread (60bps)
  ///     volSurface                           // volatility surface
  ///   );
  ///   pricer.ModelType = OptionModelType.Black; // standard Black model
  ///   pricer.AdjustSpread = true;            // adjust spread by front end protection
  /// </code>
  /// </example>
  [Serializable]
  public class CDXOptionPricerBlack : CDXOptionPricer
  {
    // Logger
    private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CDXOptionPricerBlack));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>This constructor takes a reference to underlying creadit curves.
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
    /// <param name="marketQuote">Market quote of the underlying index</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    /// <example>
    /// <code language="C#">
    ///   DiscountCurve discountCurve;
    ///   SurvivalCurve [] survivalCurves;
    ///   CalibratedVolatilitySurface volSurface;
    ///   Dt asOf, settle;
    /// 
    ///   // Set up discountCurve, survivalCurves, volSurface, asOf and settle dates.
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
    ///     false );                             // Strike spreads instead of values
    /// 
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer = new CDXOptionPricerBlack(
    ///     cdxOption,                           // Index Option
    ///     asOf,                                // Pricing date
    ///     settle,                              // Settlement date
    ///     discountCurve,                       // Discount curve
    ///     survivalCurves,                      // Survival curves
    ///     60/10000.0,                          // current market spread (60bps)
    ///     volSurface                           // volatility surface
    ///   );
    ///   pricer.ModelType = OptionModelType.Black; // standard Black model
    ///   pricer.AdjustSpread = true;            // adjust spread by front end protection
    /// </code>
    /// </example>
    public CDXOptionPricerBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve [] survivalCurves,
      double marketQuote,
      VolatilitySurface volatility)
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketQuote, volatility)
    {}

    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double marketQuote,
      double volatility)
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketQuote, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility))
    { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <remarks>
    ///   <para>This constructor takes a reference to underlying creadit curves.
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
    public CDXOptionPricerBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      SurvivalCurve marketCurve,
      CalibratedVolatilitySurface volatility)
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketCurve, volatility)
    { }


    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      SurvivalCurve marketCurve,
      double volatility)
      : base(option, asOf, settle, discountCurve, survivalCurves, 0,
        marketCurve, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility))
    {}

    /// <summary>
    /// Initializes a new instance of the <see cref="CDXOptionPricerBlack"/> class.
    /// </summary>
    /// <param name="option">CDX Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="basketSize">Size of the basket</param>
    /// <param name="marketQuote">Market quote of the underlying index</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    public CDXOptionPricerBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      int basketSize,
      double marketQuote,
      VolatilitySurface volatility)
      : base(option, asOf, settle, discountCurve, null, basketSize,
        marketQuote, volatility)
    { }


    /// <exclude/>
    [Obsolete("Replaced by a Version with VolatilityTenor surface")]
    public CDXOptionPricerBlack(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      int basketSize,
      double marketQuote,
      double volatility)
      : base(option, asOf, settle, discountCurve, null, basketSize,
        marketQuote, CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility))
    { }

    #endregion Constructors

    #region Methods
    /// <summary>
    ///   Calculate the market value of the Option at the specified volatility.
    /// </summary>
    ///
    /// <param name="vol">Volatility.</param>
    ///
    /// <returns>the market value of the option in dollars</returns>
    ///
    /// <summary>
    ///   Calculate the market value of the Option
    /// </summary>
    ///
    /// <returns>the market value of the option in dollars</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   double vol    = 1.2;                           // volatility 120%
    ///
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve, spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate maket value using volatility 60%
    ///   double marketValue = pricer.MarketValue( 0.6 );
    ///
    /// </code>
    /// </example>
    ///
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
        double spread = ForwardSpread(this.Spread);  
        value = ( this.Type == OptionType.Put ?
          BlackValuePayer( spread ) : BlackValueReceiver( spread ) );
        value *= this.Notional;
      }
      finally
      {
        this.Volatility = savedVol;
      }

      return value;
    }

    /// <summary>
    ///   Calculates implied volatility for index option
    /// </summary>
    ///
    /// <param name="fv">Fair value of CDX Option in dollars</param>
    ///
    /// <returns>Implied volatility of CDX Option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
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
    ///
    /// </code>
    /// </example>
    ///
    public override double
    IVol(double fv)
    {
      logger.Debug( "Calculating implied volatility for CDS Index Option..." );

      double spread = this.Spread;
      return (this.Type == OptionType.Put ?
        BlackIVolPayer(spread, fv) : BlackIVolReceiver(spread, fv));
    }

    /// <summary>
    /// Calculate the probability that the option is in the money
    /// on the expiry.
    /// </summary>
    /// <param name="spread">The forward spread (unadjusted).</param>
    /// <returns>Probability.</returns>
    protected override double CalculateProbabilityInTheMoney(double spread)
    {
      double pv01 = ForwardPV01(spread);
      if (this.AdjustSpread)
        spread += FrontEndProtection(spread) / pv01;

      double sigma = this.Volatility * Math.Sqrt(this.Time);
      double k;
      if (PriceVolatilityApproach)
      {
        double Pk = (StrikeIsPrice ? Strike : SpreadToPrice(Strike));
        double Px = 1 + pv01 * (CDX.Premium - spread);
        k = (this.Type == OptionType.Put
               ? (Math.Log(Pk/Px) + sigma*sigma/2)
               : (Math.Log(Px/Pk) - sigma*sigma/2));
      }
      else
      {
        k = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        k = (this.Type == OptionType.Put
               ? (Math.Log(spread/k) - sigma*sigma/2)
               : (Math.Log(k/spread) + sigma*sigma/2));
      }
      return Numerics.Normal.cumulative(k, 0.0, sigma);
    }
    #endregion // Methods

    #region Properties
    private bool priceVolatilityApproach_ = false;

    /// <summary>
    ///   True to price the option based on the price volatility approach;
    ///   False to price the option based the spread volatility approach.
    /// </summary>
    public bool PriceVolatilityApproach
    {
      get { return priceVolatilityApproach_; }
      set { priceVolatilityApproach_ = value; }
    }
    #endregion // Properties

    #region Helpers

    /// <summary>
    /// Index upfront value at a given market spread
    /// </summary>
    /// <param name="spread">Market spread in raw number (1bp = 0.0001)</param>
    /// <param name="premium">The premium in raw numbers.</param>
    /// <returns>Index upfront value</returns>
    /// <remarks>
    /// This is the present value of the expected forward value
    /// plus the front end protection.  The expectation is taken
    /// based on the particular distribution of spread or price
    /// with the given spread as the current market spread quote.
    /// The value is per unit notional and
    /// is discounted by both the survival probability from settle to expiry
    /// and the discount factor from as-of to expiry.
    /// </remarks>
    protected override double IndexUpfrontValue(double spread, double premium)
    {
      double pv = ForwardPV01(spread) * (spread - premium);
      if (this.AdjustSpread)
        pv += FrontEndProtection(spread);
      return pv;
    }

    /// <summary>
    ///   Calculate put option value based on Black formula
    /// </summary>
    /// <exclude />
    private
    double BlackValuePayer( double spread )
    {
      double pv01 = ForwardPV01(spread);
      if( this.AdjustSpread )
        spread += FrontEndProtection(spread) / pv01;
      double expect;
      if (PriceVolatilityApproach)
      {
        double Pk = (StrikeIsPrice ? Strike : SpreadToPrice(Strike));
        expect = Pk;
        double P0 = 1 + pv01 * (CDX.Premium - spread);
        if (P0 > 1e-9)
        {
          double T = this.Time;
          expect = Models.Black.B(expect / P0,
            this.Volatility * Math.Sqrt(T));
          expect *= P0;
        }
      }
      else
      {
        expect = spread;
        double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        if (MarketPayoff)
          K += (CDX.Premium - Strike) * (pv01 - ForwardPV01(Strike)) / pv01;
        if (K > 1e-10)
        {
          double T = this.Time;
          expect =  Models.Black.B(spread / K,
            this.Volatility * Math.Sqrt(T));
          expect *= K;
        }
        else
        {
          expect = spread-K;
        }
        expect *= pv01;
      }
      return expect;
    }

    /// <summary>
    ///   Calculate call option value based on Black formula
    /// </summary>
    /// <exclude />
    private
    double BlackValueReceiver( double spread )
    {
      double pv01 = ForwardPV01(spread);
      if( this.AdjustSpread )
        spread += FrontEndProtection(spread) / pv01;
      double expect;
      if (PriceVolatilityApproach)
      {
        double P0 = 1 + pv01 * (CDX.Premium - spread);
        expect = P0;
        double Pk = (StrikeIsPrice ? Strike : SpreadToPrice(Strike));
        if (Pk > 1e-7)
        {
          double T = this.Time;
          expect =  Models.Black.B(P0 / Pk,
            this.Volatility * Math.Sqrt(T));
          expect *= Pk;
        }
      }
      else
      {
        double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        if (MarketPayoff)
          K += (CDX.Premium - Strike) * (pv01 - ForwardPV01(Strike)) / pv01;
        expect = K;
        if (spread > 1e-10)
        {
          if (K > 1e-10)
          {
            double T = this.Time;
            expect = Models.Black.B(K / spread,
              this.Volatility * Math.Sqrt(T));
            expect *= spread;
          }
          else
          {
            expect = 0;
          }
        }
        expect *= pv01;
      }
      return expect;
    }

    /// <summary>
    ///   Calculate put option implied volatility
    /// </summary>
    /// <exclude />
    private
    double BlackIVolPayer( double spread, double fv )
    {
      double pv01 = ForwardPV01(spread);
      if (this.AdjustSpread)
        spread += FrontEndProtection(spread) / pv01;

      double vol = 0;
      if (PriceVolatilityApproach)
      {
        double Pk = (StrikeIsPrice ? Strike : SpreadToPrice(Strike));
        if (Pk > 1e-7)
        {
          double P0 = 1 + pv01 * (CDX.Premium - spread);
          vol = Models.Black.BI(
            Pk / P0, fv / P0, 1e-8);
          vol /= Math.Sqrt(this.Time);
        }
      }
      else
      {
        double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
        if (K > 1e-10)
        {
          fv /= pv01;
          vol =  Models.Black.BI(spread / K, fv / K, 1e-8);
          vol /= Math.Sqrt(this.Time);
        }
      }
      return vol;
    }

    /// <summary>
    ///   Calculate call option implied volatility
    /// </summary>
    /// <exclude />
    private
    double BlackIVolReceiver(double spread, double fv)
    {
      double pv01 = ForwardPV01(spread);
      if (this.AdjustSpread)
        spread += FrontEndProtection(spread) / pv01;

      double vol = 0;
      if (PriceVolatilityApproach)
      {
        double P0 = 1 + pv01 * (CDX.Premium - spread);
        if (P0 > 1e-9)
        {
          double Pk = (StrikeIsPrice ? Strike : SpreadToPrice(Strike));
          vol = Models.Black.BI(
          P0 / Pk, fv / Pk, 1e-8);
          vol /= Math.Sqrt(this.Time);
        }
      }
      else
      {
        if (spread > 1e-10)
        {
          double K = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
          fv /= pv01;
          vol =  Models.Black.BI(
            K / spread, fv / spread, 1e-8);
          vol /= Math.Sqrt(this.Time);
        }
      }
      return vol;
    }

    #endregion // Helpers

  } // class CDXOptionPricer

} 
