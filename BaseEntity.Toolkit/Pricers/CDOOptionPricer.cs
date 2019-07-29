/*
 * CDOOptionPricer.cs
 *
 *
 */
#define CALL_MODEL

using System;
using System.Collections;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CDOOption">CDO Tranche Option (Option on CDO Tranche)</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDOOption" />
  /// <para><h2>Pricing</h2></para>
  /// <para>This pricer is base on standard Black model of market spot spread.</para>
  /// <para>Consider a tranche which starts at time <formula inline="true">T</formula> and matures at time
  /// <formula inline="true">T_1</formula>.  Let <formula inline="true">S</formula> be the premium paid on the tranche.
  /// Then from the perspective of a protection seller, the forward value of the tranche is given by:</para>
  /// <formula>
  ///   V(S,F) = \mathrm{Pv01} (S - F),
  ///   \qquad
  ///   F = \frac{\mathrm{ProtectionPv}}{\mathrm{Pv01}}
  /// </formula>
  /// <para>where <formula inline="true">F</formula> is the forward break-even spread
  /// at time <formula inline="true">T</formula>.</para>
  /// <para>A call (payer) option with expiry <formula inline="true">T</formula>
  /// gives the buyer an option to buy the protection on
  /// the forward start CDO tranche with a deal premium <formula inline="true">K</formula> at time
  /// <formula inline="true">T</formula>. The optimal exercise strategy is to exercise the option and buy protection
  /// at cost <formula inline="true">K</formula> if the realized forward spread at <formula inline="true">T</formula>
  /// is greater than <formula inline="true">K</formula>. The gain earned can be monetized by
  /// immediately selling protection at the market spread.
  /// Let <formula inline="true">S_T</formula> be the spot sread at option expiry, the value of the option is</para>
  /// <formula>C(S_T,K) = \mathrm{Pv01} (S_T - K)^{+}</formula>
  /// <para>Similarly, a put (receiver) option gives the buyer an option to sell
  /// protection on the forward start CDO tranche with a deal premium <formula inline="true">K</formula>
  /// at time <formula inline="true">T</formula>.
  /// The value of the option at <formula inline="true">T</formula> is</para>
  /// <formula>P(S_T,K) = \mathrm{Pv01} (K - S_T)^{+}</formula>
  /// <para>The standard Black model assumes that, using forward
  /// <formula inline="true">\mathrm{Pv01}</formula> as the numeraire,
  /// the spot spread <formula inline="true">S_T</formula> at future time
  /// is a martingale and it follows the log-normal distribution with the mean
  /// equals the current forward spread.
  /// In other words, the prices of the options are the present values of
  /// the expectations given by:</para>
  /// <formula>\mathrm{Payer} = \mathrm{PV01} \cdot E[ (S_T - K)^+ ]</formula>
  /// <formula>\mathrm{Receiver} = \mathrm{PV01} \cdot E[ (K - S_T)^+ ]</formula>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDOOption">Option on CDO Tranche</seealso>
  [Serializable]
  public class CDOOptionPricer : PricerBase, IPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDOOptionPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="option">CDO Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="basket">Underlying basket</param>
    /// <param name="spread">Market spread of the underlying tranche</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   DiscountCurve discountCurve;
    ///   Dt asOf, settle;
    ///   // ...
    ///   // Set up discountCurve, asOf and settle dates.
    ///
    ///   // Set up the terms of the underlying index
    ///   Dt cdoEffective = new Dt(20, 6, 2004);     // effective date of index note
    ///   Dt cdoMaturity = new Dt(20, 6, 2009);      // maturity date of index note
    ///   Dt dealPremium = 40;                       // deal premium of index note
    ///
    ///   // Create CDO product
    ///   SyntheticCDO cdo =
    ///     new SyntheticCDO( cdoEffective,          // Effective date
    ///                       cdoMaturity,           // Maturity date
    ///                       Currency.USD,          // Premium and recovery Currency is dollars
    ///                       DayCount.Actual360,    // Premium accrual Daycount is Actual/360
    ///                       Frequency.Quarterly,   // Quarterly premium payments
    ///                       Calendar.NYB,          // Calendar is Target
    ///                       0.02,                  // Premium is 200bp per annum
    ///                       0.0,                   // No up-front fee is paid
    ///                       0.07,                  // Attachment point is 7%
    ///                       0.1                    // Detachment point is 10%
    ///                     );
    ///
    ///   // Set up the terms of the option on CDO.
    ///   Dt effective = new Dt(20, 9, 2004);        // Original effective date of option
    ///   PayerReceiver type = PayerReceiver.Payer;        // Option type
    ///   double strike = 0.0075;                    // Option strike of 75bp
    ///   Dt expiration = new Dt(20, 11, 2004);      // Option expiration on 10 Nov'04
    ///
    ///   // Create the CDX option
    ///   CDOOption CDOOption =
    ///     new CDOOption( effective,                // Effective date of option
    ///                    Currency.USD,             // Currency of the CDS
    ///                    mote,                     // Underlying index note
    ///                    expiration,               // Option expiration
    ///                    type,                     // Option type
    ///                    OptionStyle.European,     // Option style
    ///                    strike,                   // Option strike
    ///                    false );                  // Strike spreads instead of values
    ///
    ///   // Set up pricing parameters
    ///   double spread = 0.0060;                        // current market spread (60bps)
    ///   double vol    = 1.2;                           // volatility 120%
    ///   bool adjustSpread = true;                      // adjust spread by front end protection
    ///
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( CDOOption, asOf, settle, discountCurve, basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    /// </code>
    /// </example>
    ///
    public
    CDOOptionPricer(
      CDOOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      BasketPricer basket,
      double spread,
      double volatility)
      : base(option, asOf, settle)
    {
      // Set data, using properties to include validation
      this.DiscountCurve = discountCurve;
      this.Basket = basket;
      this.Spread = spread;
      if (Double.IsNaN(spread))
        this.ImplySpread = true;
      this.Volatility = volatility;
      this.cdoPricer_ = null;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      CDOOptionPricer obj = (CDOOptionPricer)base.Clone();

      obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
      obj.basket_ = (BasketPricer)basket_.Clone();

      // do not remember the intermediate results
      obj.cdoPricer_ = null;

      return obj;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      if (basket_ == null)
        InvalidValue.AddError(errors, this, "Basket", String.Format("Basket cannot be null"));
  
      if (volatility_ < 0.0 || volatility_ > 10.0)
        InvalidValue.AddError(errors, this, "Volatility", String.Format("Volatility invalid. Must be between 0 and 10.0. Note {0}", volatility_));

      return;
    }

    /// <summary>
    ///   Reset the pricer
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Clear the internal cache of intermediate results.  This method should
    ///   be called when the state of the pricer was modified, for example, through
    ///   setting the properties.</para>
    /// </remarks>
    ///
    public override void Reset()
    {
      cdoPricer_ = null;
    }

    /// <summary>
    ///   Calculate pv of the Option.
    /// </summary>
    ///
    /// <returns>the PV of the option in dollars</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate pv or fair value
    ///   double fairValue = pricer.Pv();
    ///
    /// </code>
    /// </example>
    ///
    public override double ProductPv()
    {
      return OptionValue(GetSpread(), this.Volatility);
    }

    /// <summary>
    ///   Intrinsic value of option
    /// </summary>
    ///
    /// <returns>Intrinsic value (pv at as-of date)</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer(cdoOption, asOf, settle, discountCurve, basket, spread);
    ///
    ///   // Calculate the intrinsic value
    ///   double intrinsic = pricer.Intrinsic();
    ///
    /// </code>
    /// </example>
    ///
    public double
    Intrinsic()
    {
      double spread = GetSpread();
      if (this.AdjustSpread)
        spread = AdjustedForwardSpread(spread);
      double S = ExercisePrice(spread);
      double K = ExercisePrice(CDOOption.Strike);
      double price = (CDOOption.Type == OptionType.Put ? S - K : K - S);
      if (price <= 0)
        return 0;
      // Discounting the price back to as-of date
      price *= DiscountCurve.Interpolate(AsOf, CDOOption.Expiration)
          * ExpectedSurvival();
      return price;
    }


    /// <summary>
    ///   Calculate the vega for a CDO Option
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The vega is calculated as the difference between the current
    ///   fair value, and the fair value after increasing the volatility by
    ///   the specified bump size.</para>
    /// </remarks>
    ///
    /// <param name="bump">Bump for volatilty in percent (0.01 = 1 percent)</param>
    ///
    /// <returns>The CDO Option vega</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDSOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          survivalCurves, quotedSpread, volatility);
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate option vega for a 1pc move up in volatility
    ///   double vega = pricer.Vega(0.01);
    ///
    /// </code>
    /// </example>
    ///
    public double
    Vega(double bump)
    {
      return Vega(bump, this.Volatility);
    }

    /// <summary>
    ///   Calculate the vega for a CDO Option
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The vega is calculated as the difference between the current
    ///   fair value, and the fair value after increasing the volatility by
    ///   the specified bump size.</para>
    /// </remarks>
    ///
    /// <param name="bump">Bump for volatilty in percent (0.01 = 1 percent)</param>
    /// <param name="volatility">Optional override for base volatilty</param>
    ///
    /// <returns>The CDO Option vega</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDSOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          survivalCurves, quotedSpread, volatility);
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate option vega for a 1pc move up in volatility
    ///   double vega = pricer.Vega(0.01, Double.NaN);
    ///
    /// </code>
    /// </example>
    ///
    public double
    Vega(double bump, double volatility)
    {
      if (Dt.Cmp(CDOOption.Expiration, AsOf) < 0)
        throw new ToolkitException(String.Format("Out of range error: As-of ({0}) must be prior to expiration ({1})",
                                                                            AsOf, CDOOption.Expiration));
      if (Math.Abs(bump) <= 1.0E-9)
        throw new ToolkitException(String.Format("Out of range error: bump ({0}) is too close to zero", bump));

      double spread = GetSpread();
      if (this.AdjustSpread)
        spread = AdjustedForwardSpread(spread);
      if (Double.IsNaN(volatility))
        volatility = this.Volatility;
      double y0 = OptionValue(spread, volatility);
      double y1 = OptionValue(spread, volatility + bump);
      return y1 - y0;
    }

    /// <summary>
    ///   Calculate the fair value of the Option per dollar of notional.
    /// </summary>
    ///
    /// <returns>the fair value (PV in percent) of the option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate pv or fair value
    ///   double fairValue = pricer.FairValue();
    ///
    /// </code>
    /// </example>
    ///
    public double
    FairValue()
    {
      return FairValue(this.Volatility);
    }


    /// <summary>
    ///   Calculate the fair value of the Option with a different volatility.
    /// </summary>
    ///
    /// <param name="vol">Optional override volatility.</param>
    ///
    /// <returns>the fair value (PV in percent) of the option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate pv or fair value with volatility 210%
    ///   double fairValue = pricer.FairValue(2.10);
    ///
    /// </code>
    /// </example>
    ///
    public double
    FairValue(double vol)
    {
      double spread = GetSpread();
      if (Double.IsNaN(vol))
        vol = this.Volatility;
      return OptionPrice(spread, vol);
    }


    /// <summary>
    ///   Calculate the discounted exercise price at expiration date
    /// </summary>
    ///
    /// <returns>the discounted exercise price</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate exercise price
    ///   double exercisePrice = pricer.ExercisePrice();
    ///
    /// </code>
    /// </example>
    ///
    public double
    ExercisePrice()
    {
      double spread = GetSpread();
      if (this.AdjustSpread)
        spread = AdjustedForwardSpread(spread);
      double v = ExercisePrice(spread);
      return v;
    }


    /// <summary>
    ///   Calculate the discounted strike price at expiration date
    /// </summary>
    ///
    /// <remarks>The price is discounted by both the interest rate and the suvival probability.</remarks>
    ///
    /// <returns>the discounted exercise price</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate exercise price
    ///   double strikePrice = pricer.StrikePrice();
    ///
    /// </code>
    /// </example>
    ///
    public double
    StrikePrice()
    {
      double v = ExercisePrice(this.Strike);
      return v;
    }


    /// <summary>
    ///   Calculates implied volatility for CDO option
    /// </summary>
    ///
    /// <param name="fv">Fair value of CDO Option in dollars</param>
    ///
    /// <returns>Implied volatility of CDO Option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve,
    ///                          basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate implied volatility
    ///   double vol = pricer.IVol(fv);
    ///
    /// </code>
    /// </example>
    ///
    public double IVol(double fv)
    {
      double spread = GetSpread();
      return (this.Type == OptionType.Put ?
        BlackIVolPayer(spread, fv) : BlackIVolReceiver(spread, fv));
    }

    /// <summary>
    ///   Calculates expected forward value of the underlying CDX at expiration date
    /// </summary>
    ///
    /// <returns>Forward value at expiration date</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve, basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate forward value
    ///   double forwardValue = pricer.ForwardValue();
    ///
    /// </code>
    /// </example>
    ///
    public double
    ForwardValue()
    {
      double spread = GetSpread();
      return ExercisePrice(spread) * this.Notional;
    }

    /// <summary>
    ///   Calculate adjusted forward spread
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This method adjusts the market spread by front end protection
    ///   to get the adjusted forward spread.</para>
    ///
    ///   <formula>
    ///     \mathrm{Adjusted\,Forward\,Spread}
    ///        = \mathrm{Market\,Spread}
    ///        + \frac{\mathrm{Value\,of\,Front\,End\,Protection}}
    ///          {\mathrm{Forward\,PV01\,at\,Market\,Spread}}
    ///   </formula>
    /// </remarks>
    ///
    /// <returns>the adjusted forward spread</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDO Option.
    ///   CDOOptionPricer pricer =
    ///     new CDOOptionPricer( cdoOption, asOf, settle, discountCurve, basket, spread, vol );
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate forward spread
    ///   double adjustedSpread = pricer.AdjustedForwardSpread();
    ///
    /// </code>
    /// </example>
    ///
    public double
    AdjustedForwardSpread()
    {
      double spread = GetSpread();
      return AdjustedForwardSpread(spread);
    }

    /// <summary>
    ///   Expected survival of the tranche at expirary
    /// </summary>
    /// <returns></returns>
    public double ExpectedSurvival()
    {
      if (cdoPricer_ == null)
        cdoPricer_ = new SyntheticCDOPricer(
          this.CDO, this.Basket, this.DiscountCurve, 1.0, null/*ResetRate*/);
      return 1 - cdoPricer_.LossToDate(CDOOption.Expiration);
    }
    #endregion // Methods

    #region Properties

    /// <summary>
    ///   CDO Option product
    /// </summary>
    public CDOOption CDOOption
    {
      get { return (CDOOption)Product; }
    }


    /// <summary>
    ///   Option style
    /// </summary>
    public OptionStyle Style
    {
      get { return CDOOption.Style; }
      set { CDOOption.Style = value; }
    }


    /// <summary>
    ///   Option type
    /// </summary>
    public OptionType Type
    {
      get { return CDOOption.Type; }
      set { CDOOption.Type = value; }
    }


    /// <summary>
    ///   Option strike price
    /// </summary>
    public double Strike
    {
      get { return CDOOption.Strike; }
      set { CDOOption.Strike = value; }
    }


    /// <summary>
    ///   Option strike price
    /// </summary>
    public bool StrikeIsPrice
    {
      get { return CDOOption.StrikeIsPrice; }
    }


    /// <summary>
    ///   Current spread of underlying asset
    /// </summary>
    public double Spread
    {
      get { return spread_; }
      set { spread_ = value; }
    }


    /// <summary>
    ///   Volatility
    /// </summary>
    public double Volatility
    {
      get { return volatility_; }
      set
      {
        volatility_ = value;
      }
    }


    /// <summary>
    ///   Time to expiration
    /// </summary>
    public double Time
    {
      get
      {
        return (CDOOption.Expiration - AsOf) / 365.25;
      }
    }

    /// <summary>
    ///   Underlying CDS tranche
    /// </summary>
    public SyntheticCDO CDO
    {
      get { return this.CDOOption.CDO; }
    }


    /// <summary>
    ///   Underlying portfolio basket
    /// </summary>
    public BasketPricer Basket
    {
      get { return basket_; }
      set
      {
        basket_ = value;
      }
    }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public int StepSize
    {
      get { return Basket.StepSize; }
      set { Basket.StepSize = value; }
    }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    public TimeUnit StepUnit
    {
      get { return Basket.StepUnit; }
      set { Basket.StepUnit = value; }
    }

    /// <summary>
    ///   Correlation structure of the basket
    /// </summary>
    public CorrelationObject Correlation
    {
      get { return (CorrelationObject)Basket.Correlation; }
      set { Basket.Correlation = value; }
    }


    /// <summary>
    ///   Copula structure
    /// </summary>
    public Copula Copula
    {
      get { return Basket.Copula; }
      set { Basket.Copula = value; }
    }


    /// <summary>
    ///  Survival curves from curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return Basket.SurvivalCurves; }
      set { Basket.SurvivalCurves = value; }
    }


    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get { return Basket.RecoveryCurves; }
      set { Basket.RecoveryCurves = value; }
    }


    /// <summary>
    ///   Recovery rates from curves
    /// </summary>
    public double[] RecoveryRates
    {
      get { return Basket.RecoveryRates; }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set
      {
        discountCurve_ = value;
      }
    }

    /// <summary>
    ///   If true, adjust forward spread by front end protection
    /// </summary>
    public bool AdjustSpread
    {
      get { return adjustSpread_; }
      set { adjustSpread_ = value; }
    }

    /// <summary>
    ///   If true, current cdo spread is calculated from the basket
    /// </summary>
    public bool ImplySpread
    {
      get { return implySpread_; }
      set { implySpread_ = value; }
    }
    #endregion // Properties

    #region Data
    private DiscountCurve discountCurve_;
    private BasketPricer basket_;
    private double spread_;
    private double volatility_;

    private bool implySpread_ = false;
    private bool adjustSpread_ = true;

    private SyntheticCDOPricer cdoPricer_;
    private double fwdBreakEvenSpread_;
    private double fwdPV01_;
    private double frontEndProtection_;
    #endregion // Data

    #region Helpers
    /// <summary>
    ///   Calculate option price
    /// </summary>
    /// <returns>price</returns>
    private double OptionPrice(double spread, double vol)
    {
      return (this.Type == OptionType.Put ?
        BlackValuePayer(spread, vol) : BlackValueReceiver(spread, vol));
    }

    /// <summary>
    ///   Calculate option value
    /// </summary>
    /// <returns>value</returns>
    private double OptionValue(double spread, double vol)
    {
      double value = (this.Type == OptionType.Put ?
        BlackValuePayer(spread, vol) : BlackValueReceiver(spread, vol));
      value *= this.Notional;
      return value;
    }

    /// <summary>
    ///   Calculate put option value based on Black formula
    /// </summary>
    /// <exclude />
    private double BlackValuePayer(double spread, double volalitity)
    {
      double pv01 = ForwardPV01();
      if (this.AdjustSpread)
        spread += FrontEndProtection() / pv01;
      double expect = spread;
      if (this.Strike > 1e-10)
      {
        double K = this.Strike;
        double T = this.Time;
#if CALL_MODEL
        expect = Toolkit.Models.Black.B( spread/K,
                                          volalitity * Math.Sqrt(T));
        expect *= K;
#else
        double sd = volalitity * Math.Sqrt(T);
        double d1 = Math.Log(spread / K) / sd + sd / 2;
        double d2 = d1 - sd;
        d1 = BaseEntity.Toolkit.Numerics.Normal.cumulative(d1, 0, 1);
        d2 = BaseEntity.Toolkit.Numerics.Normal.cumulative(d2, 0, 1);
        expect = d1 * spread - d2 * K;
#endif
      }
      expect *= pv01;
      return expect;
    }

    /// <summary>
    ///   Calculate call option value based on Black formula
    /// </summary>
    /// <exclude />
    private double BlackValueReceiver(double spread, double volalitity)
    {
      double pv01 = ForwardPV01();
      if (this.AdjustSpread)
        spread += FrontEndProtection() / pv01;
      double expect = this.Strike;
      if (spread > 1e-10)
      {
        double T = this.Time;
#if CALL_MODEL
        expect = Toolkit.Models.Black.B( this.Strike/spread,
                                          volalitity * Math.Sqrt(T));
        expect *= spread;
#else
        double K = this.Strike;
        double sd = volalitity * Math.Sqrt(T);
        double d1 = Math.Log(spread / K) / sd + sd / 2;
        double d2 = d1 - sd;
        d1 = BaseEntity.Toolkit.Numerics.Normal.cumulative(-d1, 0, 1);
        d2 = BaseEntity.Toolkit.Numerics.Normal.cumulative(-d2, 0, 1);
        expect = -d1 * spread + d2 * K;
#endif
      }
      expect *= pv01;
      return expect;
    }

    /// <summary>
    ///   Calculate put option implied volatility
    /// </summary>
    /// <exclude />
    private
    double BlackIVolPayer(double spread, double fv)
    {
      double vol = 0;
      if (this.Strike > 1e-10)
      {
        double pv01 = ForwardPV01();
        fv /= pv01;
        if (this.AdjustSpread)
          spread += FrontEndProtection() / pv01;
        double T = this.Time;
        double K = this.Strike;
        vol = Toolkit.Models.Black.BI(spread / K, fv / K, 1e-8);
        vol /= Math.Sqrt(T);
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
      double vol = 0;
      if (spread > 1e-10)
      {
        double pv01 = ForwardPV01();
        fv /= pv01;
        if (this.AdjustSpread)
          spread += FrontEndProtection() / pv01;
        vol = Toolkit.Models.Black.BI(this.Strike / spread,
                                        fv / spread, 1e-8);
        double T = this.Time;
        vol /= Math.Sqrt(T);
      }
      return vol;
    }

    /// <summary>
    ///   Calculate forward spread (not adjusted)
    /// </summary>
    /// <returns>spread</returns>
    private double GetSpread()
    {
      double spread;
      if (implySpread_)
      {
        // Calculate the spread
        if (cdoPricer_ == null)
          CreateCDOPricer();
        spread = cdoPricer_.BreakEvenPremium();
      }
      else
        spread = this.Spread;

      return spread;
    }

    /// <summary>
    ///   Calculate CDO price at exercise date
    /// </summary>
    ///
    /// <remarks>
    /// Given a spread, compute market value of the tranche at the exercise date
    /// The value is from the protection buyer's perspective.
    /// </remarks>
    ///
    /// <returns>Exercise market value</returns>
    ///
    /// <exclude />
    protected double
    ExercisePrice(double spread)
    {
      if (cdoPricer_ == null)
        CreateCDOPricer();
      double pv = ForwardPV01() * (spread - fwdBreakEvenSpread_);
      return pv;
    }

    /// <summary>
    ///   Calculate the adjusted forwadr sread
    /// </summary>
    /// <exclude />
    protected double AdjustedForwardSpread(double spread)
    {
      double adjust = FrontEndProtection() / ForwardPV01();
      return spread + adjust;
    }

    /// <summary>
    ///   Calculate the forward pv01
    /// </summary>
    /// <exclude />
    protected double ForwardPV01()
    {
      if (cdoPricer_ == null)
        CreateCDOPricer();
      return fwdPV01_;
    }

    /// <summary>
    ///   Calculate front end protection
    ///  </summary>
    ///
    /// <remarks>Front end protection is the present value of the expected
    /// cumulative loss from settlement to option expiraration date.</remarks>
    /// <exclude />
    protected double FrontEndProtection()
    {
      if (cdoPricer_ == null)
        CreateCDOPricer();
      return frontEndProtection_;
    }

    /// <summary>
    ///   Create a pricer and calculate forward values
    /// </summary>
    private void CreateCDOPricer()
    {
      SyntheticCDO cdo = this.CDO;
      BasketPricer basket = this.Basket;
      SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, basket, discountCurve_, 1.0, null/*ResetRates*/);
      cdoPricer_ = pricer;
      pricer.Basket.Reset();
      Dt expiry = this.CDOOption.Expiration;
      Dt start = cdoPricer_.Basket.PortfolioStart;
      if (!start.IsValid()) start = cdoPricer_.Basket.Settle;
      double df = DiscountCurve.DiscountFactor(this.AsOf, expiry);
      fwdPV01_ = pricer.RiskyDuration(expiry) * df;
      if (Dt.Cmp(start, expiry) < 0)
      {
        double cumuLoss = cdoPricer_.LossToDate(expiry);
        frontEndProtection_ = cumuLoss * df;
      }
      else
        frontEndProtection_ = 0;
      fwdBreakEvenSpread_ = pricer.BreakEvenPremium(expiry);
    }
    #endregion // Helpers

  } // class CDOOptionPricer

}
