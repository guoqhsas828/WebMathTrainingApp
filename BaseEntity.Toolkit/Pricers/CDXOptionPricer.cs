//
// CDXOptionPricer.cs
//  -2008. All rights reserved.
//

//#define USE_PROTECTION_AS_FRONT_END
#define DIRECT_BLACK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Defines the base class Index Option Pricers
  /// </summary>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDXOption">Option on CDX Note</seealso>
  /// <seealso cref="CDXOptionPricerBlack">Option on CDX Note Pricer based on the standard Black model</seealso>
  /// <seealso cref="CDXOptionPricerModifiedBlack">Option on CDX Note Pricer based on the modified Black model</seealso>
  [Serializable]
  public abstract class CDXOptionPricer : PricerBase, ICreditIndexOptionPricer, IDefaultSensitivityCurvesGetter
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDXOptionPricer));

    #region Config

    /// <summary>
    ///   Force the forward value be clean
    /// </summary>
    /// <exclude />
    public bool ForceCleanValue
    {
      get { return (choices_ & CDXOptionModelParam.ForceCleanValue) == CDXOptionModelParam.ForceCleanValue; }
    }

    /// <summary>
    ///   Force to ignore the term structure of the user market curve
    /// </summary>
    /// <exclude />
    public bool ForceFlatMarketCurve
    {
      get { return (choices_ & CDXOptionModelParam.ForceFlatMarketCurve) == CDXOptionModelParam.ForceFlatMarketCurve; }
    }

    /// <summary>
    ///   Include maturity date in accrual calculation for CDS/CDO pricers
    /// </summary>
    /// <exclude />
    public bool UseProtectionPvForFrontEnd
    {
      get { return (choices_ & CDXOptionModelParam.UseProtectionPvForFrontEnd) == CDXOptionModelParam.UseProtectionPvForFrontEnd; }
    }

    /// <summary>
    ///   Always adjust spread by market method
    /// </summary>
    /// <exclude />
    public bool AlwaysAdjustSpreadByMarketMethod
    {
      get { return (choices_ & CDXOptionModelParam.AdjustSpreadByMarketMethod) == CDXOptionModelParam.AdjustSpreadByMarketMethod; }
    }

    /// <summary>
    ///   Force instrinsic survival probability in spread adjustment
    /// </summary>
    public bool ForceIntrinsicSurvival
    {
      get { return (choices_ & CDXOptionModelParam.ForceIntrinsicSurvival) == CDXOptionModelParam.ForceIntrinsicSurvival; }
    }

    /// <summary>
    ///   Force instrinsic survival probability in spread adjustment
    /// </summary>
    public bool MarketPayoff
    {
      get { return (choices_ & CDXOptionModelParam.MarketPayoff) == CDXOptionModelParam.MarketPayoff; }
    }

    #endregion Config

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
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
    ///
    /// <param name="option">CDX Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of CDS Index</param>
    /// <param name="basketSize">Basket size</param>
    /// <param name="marketQuote">Market quote of the underlying index</param>
    /// <param name="volatility">Volatility surface</param>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   DiscountCurve discountCurve;
    ///   SurvivalCurve [] survivalCurves;
    ///   Dt asOf, settle;
    ///   // ...
    ///   // Set up discountCurve, survivalCurves, asOf and settle dates.
    ///
    ///   // Set up the terms of the underlying index
    ///   Dt cdxEffective = new Dt(20, 6, 2004);     // effective date of index note
    ///   Dt cdxMaturity = new Dt(20, 6, 2009);      // maturity date of index note
    ///   Dt dealPremium = 40;                       // deal premium of index note
    ///
    ///   // Create underlying index
    ///   CDX note = new CDX( cdxEffective,               // Effective date of the note
    ///                       cdxMaturity,            // Maturity date of the note
    ///                       Currency.USD,           // Currency of the CDX
    ///                       dealPremium/10000,      // Deal premium in raw number
    ///                       DayCount.Actual360,     // Daycount of the CDS premium
    ///                       Frequency.Quarterly,    // Payment frequency of the CDS
    ///                       BDConvention.Following, // Business day convention of the CDS
    ///                       Calendar.NYB            // Calendar for the CDS
    ///                      );
    ///
    ///   // Set up the terms of the option on CDS.
    ///   Dt effective = new Dt(20, 9, 2004);            // Original effective date of option
    ///   PayerReceiver type = PayerReceiver.Payer;        // Option type
    ///   double strike = 0.0075;                    // Option strike of 75bp
    ///   Dt expiration = new Dt(20, 11, 2004);      // Option expiration on 10 Nov'04
    ///
    ///   // Create the CDX option
    ///   CDXOption cdxOption =
    ///     new CDXOption( effective,                    // Effective date of option
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
    ///   OptionModelType model = OptionModelType.Black; // standard Black model
    ///   bool adjustSpread = true;                      // adjust spread by front end protection
    ///
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    /// </code>
    /// </example>
    ///
    protected CDXOptionPricer(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      int basketSize,
      double marketQuote,
      VolatilitySurface volatility)
      : base(option, asOf, settle)
    {
      // Set data, using properties to include validation
      discountCurve_ = discountCurve;
      if (survivalCurves != null && survivalCurves.Length > 0)
      {
        survivalCurves_ = survivalCurves;
        basketSize_ = survivalCurves.Length;
      }
      else
      {
        basketSize_ = basketSize;
      }
      MarketQuote = marketQuote;
      volatility_ = Double.NaN;
      volatilitySurface_ = volatility;
      return;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
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
    ///
    /// <param name="option">CDX Option</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of CDX Index</param>
    /// <param name="basketSize">Basket size</param>
    /// <param name="marketCurve">Market CDX curve</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    ///
    protected CDXOptionPricer(
      CDXOption option,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      int basketSize,
      SurvivalCurve marketCurve,
      CalibratedVolatilitySurface volatility)
      : base(option, asOf, settle)
    {
      // Set data, using properties to include validation
      discountCurve_ = discountCurve;
      if (survivalCurves != null && survivalCurves.Length > 0)
      {
        survivalCurves_ = survivalCurves;
        basketSize_ = survivalCurves.Length;
      }
      else
      {
        basketSize_ = basketSize;
      }
      SetMarketSurvivalCurve(marketCurve);
      volatility_ = Double.NaN;
      volatilitySurface_ = volatility;
      return;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CDXOptionPricer obj = (CDXOptionPricer)base.Clone();

      obj.survivalCurves_ = null;
      if (survivalCurves_ != null)
      {
        obj.survivalCurves_ = new SurvivalCurve[survivalCurves_.Length];
        obj.survivalCurves_ = CloneUtil.Clone(survivalCurves_);
      }
      obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();

      return obj;
    }

    /// <summary>
    /// Create a new copy of this pricer with the specified quote
    /// while everything else are copied memberwise to the new pricer.
    /// </summary>
    /// <param name="quote">The market quote.</param>
    /// <returns>ICreditIndexOptionPricer.</returns>
    public ICreditIndexOptionPricer Update(MarketQuote quote)
    {
      var pricer = (CDXOptionPricer)MemberwiseClone();
      pricer._marketQuote = quote;
      pricer.Reset();
      return pricer;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (volatilitySurface_ == null)
      {
        InvalidValue.AddError(errors, this, "VolatilitySurface",
          String.Format("Invalid volatility surface. Cannot be null"));
      }
      else
      {
        volatilitySurface_.Validate(errors);
      }

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve",
          String.Format("Invalid discount curve. Cannot be null"));


      if (
        !(QuotingConvention == QuotingConvention.CreditSpread ||
          QuotingConvention == QuotingConvention.FlatPrice))
        InvalidValue.AddError(errors, this, "QuotingConvention",
          String.Format(
            "Invalid quoting convention {0}. Must be either creditspread or flatprice",
            QuotingConvention));

      return;
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
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///   pricer.ModelType = model;
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
      // option expired
      if (Dt.Cmp(CDXOption.Expiration, this.Settle) < 0)
        return 0;

      double res;
      if (survivalCurves_ != null)
      {
        SurvivalCurve savedMarketCurve = marketSurvivalCurve_;
        try
        {
          if (savedMarketCurve != null)
          {
            marketSurvivalCurve_ = (SurvivalCurve)savedMarketCurve.Clone();
            marketSurvivalCurve_.Calibrator = (Calibrator)marketSurvivalCurve_.Calibrator.Clone();
          }

          // Get index fair forward spread and this links PV to underlying names
          CDXPricer cdxPricer = ForwardCDXPricer(this.Spread);
          cdxPricer.SurvivalCurves = survivalCurves_;
          double fairSpread = cdxPricer.ImpliedQuotedSpread(cdxPricer.IntrinsicValue(false));

          // Price of the fair spread
          Spread = fairSpread;
          res = MarketValue();
        }
        finally
        {
          marketSurvivalCurve_ = savedMarketCurve;
        }
      }
      else
        res = MarketValue();

      return res;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks>
    /// 	<para>There are some pricers which need to remember some internal state
    /// in order to skip redundant calculation steps. This method is provided
    /// to indicate that all internal states should be cleared or updated.</para>
    /// 	<para>Derived Pricers may implement this and should call base.Reset()</para>
    /// </remarks>
    public override void Reset()
    {
      volatility_ = Double.NaN;
      base.Reset();
    }

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
      if (what == ResetVolatility || what == ResetAll)
      {
        volatility_ = Double.NaN;
      }
      base.Reset(what);
    }

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
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve, spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate market value
    ///   double marketValue = pricer.MarketValue();
    ///
    /// </code>
    /// </example>
    ///
    public double MarketValue()
    {
      return MarketValue(Volatility);
    }

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
    ///   // Calculate market value using volatility 60%
    ///   double marketValue = pricer.MarketValue( 0.6 );
    ///
    /// </code>
    /// </example>
    ///
    public abstract double MarketValue(double vol);

    /// <summary>
    ///   Intrinsic value of option
    /// </summary>
    ///
    /// <returns>Intrinsic dollar value (pv to pricing as-of date)</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer(cdxOption, asOf, settle, discountCurve, spread);
    ///
    ///   // Calculate the intrinsic value
    ///   double intrinsic = pricer.Intrinsic();
    ///
    /// </code>
    /// </example>
    ///
    public double Intrinsic()
    {
      double spread = Spread;
      if (AdjustSpread)
        spread = AdjustedForwardSpread(spread);
      double S = StrikeIsPrice ? (SpreadToPrice(spread) - 1.0) : ExerciseMarketValue(spread);
      double K = StrikeIsPrice ? (Strike - 1) : ExerciseMarketValue(CDXOption.Strike);
      double price = (CDXOption.Type == OptionType.Put ? S - K : K - S);
      if (price <= 0)
        return 0;

      // Discounting the price back to as-of date
      price *= DiscountCurve.Interpolate(AsOf, CDXOption.Expiration)
          * SurvivalProbability(Spread);

      return price * Notional;
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
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate pv or fair value
    ///   double fairValue = pricer.FairPrice();
    ///
    /// </code>
    /// </example>
    ///
    public double FairPrice()
    {
      return FairPrice(Volatility);
    }

    /// <summary>
    ///   Calculate the fair price of the Option with a different volatility.
    /// </summary>
    ///
    /// <param name="vol">Override volatility.</param>
    ///
    /// <returns>the fair price (PV in percent) of the option</returns>
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
    ///   // Calculate pv or fair value with volatility 210%
    ///   double fairValue = pricer.FairPrice(2.10);
    ///
    /// </code>
    /// </example>
    ///
    public double FairPrice(double vol)
    {
      return MarketValue(vol) / Notional;
    }

    /// <summary>
    ///   Calculate the exercise price at expiration date
    /// </summary>
    ///
    /// <returns>The exercise price (1-based and not discounted)</returns>
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
    ///   // Calculate exercise price
    ///   double exercisePrice = pricer.ExercisePrice();
    ///
    /// </code>
    /// </example>
    ///
    public double ExercisePrice()
    {
      double v = StrikeIsPrice ? Strike
        : ValueToPrice(ExerciseMarketValue(Strike));
      return v;
    }

    internal double ValueToPrice(double value)
    {
      return (1 - value) - ForwardAccrued();
    }

    internal double PriceToValue(double price)
    {
      return (1 - price) - ForwardAccrued();
    }

    /// <summary>
    ///   Calculate the discounted strike value on expiration date
    /// </summary>
    ///
    /// <remarks>
    ///   The value is discounted by both the interest rate and the suvival probability.
    /// </remarks>
    ///
    /// <returns>The discounted exercise value</returns>
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
    ///   // Calculate exercise price
    ///   double strikeValue = pricer.StrikeValue();
    ///
    /// </code>
    /// </example>
    ///
    public virtual double StrikeValue()
    {
      double v = ExerciseStrikeValue(Spread);
      return v;
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
    public abstract double IVol(double fv);

    /// <summary>
    ///   Calculates expected forward value of the underlying CDX at expiration date
    /// </summary>
    ///
    /// <returns>Forward value at expiration date</returns>
    ///
    /// <remarks>
    ///   The value of the index at expiry evaluated at the current market spread
    ///   or as implied by the current market price.  The value is not discounted. 
    /// </remarks>
    /// 
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve, spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate forward value
    ///   double forwardValue = pricer.ForwardValue();
    ///
    /// </code>
    /// </example>
    ///
    public double ForwardValue()
    {
      double v = ExerciseMarketValue(Spread);
      v *= Notional;
      return v;
    }

    /// <summary>
    ///   Calculate the forward start pv01
    /// </summary>
    public double ForwardPV01()
    {
      return ForwardPV01(this.Spread);
    }

    /// <summary>
    ///   Calculate the forward spread
    /// </summary>
    /// <exclude />
    public double ForwardSpread()
    {
      return ForwardSpread(Spread);
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
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve, spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate forward spread
    ///   double adjustedSpread = pricer.AdjustedForwardSpread();
    ///
    /// </code>
    /// </example>
    ///
    public double AdjustedForwardSpread()
    {
      return AdjustedForwardSpread(Spread);
    }

    /// <summary>
    ///  Calculates the Spread01 with the specified bump sizes.
    /// </summary>
    /// <param name="upBump">Up bump size in basis points (1bp = 0.0001).</param>
    /// <param name="downBump">Down bump size in basis points (1bp = 0.0001).</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException"></exception>
    public double Spread01(double upBump, double downBump, BumpFlags bumpFlags)
    {
      if (!(upBump + downBump > 1E-12))
      {
        throw new ToolkitException(String.Format(
          "Invalid bump sizes: up {0}, down {1}", upBump, downBump));
      }
      if (survivalCurves_ != null)
      {
        double unitDeltaValue = Sensitivities2.Spread01(this, null, upBump, downBump, bumpFlags);
        return unitDeltaValue / Notional * 100.0;
      }
      var originalSpread = Spread;
      try
      {
        Spread += upBump / 10000.0;
        Reset();
        var pvUp = ProductPv();
        Spread -= (upBump + downBump)/10000.0;
        Reset();
        var pvDown = ProductPv();
        return (pvUp - pvDown) / (upBump + downBump) / Notional * 100.0;
      }
      finally
      {
        Spread = originalSpread;
        Reset();
      }
    }



    /// <summary>
    /// Strike for the option viewed as option on CDX spread
    /// </summary>
    public double EffectiveStrike
    {
      get { return StrikeIsPrice ? PriceToSpread(Strike) : Strike; }
    }

    private double[][] ScaleBumpSurvivalCurves(double bumpSize)
    {
      if (!FullReplicatingMethod || survivalCurves_ == null || Math.Abs(bumpSize) < 1e-10)
        return null;

      double[][] savedQuotes = new double[survivalCurves_.Length][];
      for (int i = 0; i < survivalCurves_.Length;++i )
      {
        savedQuotes[i] = ArrayUtil.Generate<double>(survivalCurves_[i].Tenors.Count,
                                                   (k)=> CurveUtil.MarketQuote(survivalCurves_[i].Tenors[k]));
      }
     
      object bs = bumpSize<0.01? bumpSize*10000 : bumpSize;
      object[] survBumps = Enumerable.Repeat(bs, survivalCurves_.Length).ToArray();

      Sensitivities.BumpSurvivalCurves(Settle, null, survivalCurves_, survBumps, false, false, null, true);

      return savedQuotes;
    }

    private bool RestoreSurvivalCurves(double[][] marketQuotes)
    {
      if (marketQuotes == null)
        return false;

      for (int i = 0; i < survivalCurves_.Length; ++i)
      {
        for (int j = 0; j < survivalCurves_[i].Tenors.Count; ++j)
          CurveUtil.SetMarketQuote(survivalCurves_[i].Tenors[j], marketQuotes[i][j]);
      }
      CurveUtil.CurveFit(survivalCurves_);
      return true;
    }
    
    /// <summary>
    /// Calculate option price and underlying index upfront after bumping the market spread
    /// </summary>
    /// <param name="bumpSize">bump size</param>
    /// <param name="useDealPremium">if set to <c>true</c>, use deal premium for index value calculation; otherwise, the strike spread.</param>
    /// <returns>
    /// option price and underlying index upfront
    /// </returns>
    private double[] bumpedPvs(double bumpSize, bool useDealPremium)
    {
      Spread += bumpSize;
      double[][] savedQuotes = ScaleBumpSurvivalCurves(bumpSize);

      try
      {
        double[] pvs = new double[2];
        pvs[0] = FairPrice();
        if (useDealPremium)
        {
          pvs[1] = IndexUpfrontValue();
        }
        else
        {
          double premium = StrikeIsPrice ? PriceToSpread(Strike) : Strike;
          pvs[1] = IndexUpfrontValue(Spread, premium);
        }
        RestoreSurvivalCurves(savedQuotes);
        return pvs;
      }
      finally
      {
        Spread -= bumpSize;
      }
    }
    internal static Func<ICreditIndexOptionPricer, double, bool, double[]>
      BumpedPvFn = (p, b, d) => ((CDXOptionPricer)p).bumpedPvs(b, d);

    /// <summary>
    ///   Index upfront value per unit notional
    /// </summary>
    ///
    /// <remarks>
    ///   This is the present value of the expected forward value
    ///   plus the front end protection.  The expectation is taken
    ///   based on the particular distribution of spread or price.
    ///   The value is per unit notional and 
    ///   is discounted by both the survival probability from settle to expiry
    ///   and the discount factor from as-of to expiry.
    /// </remarks>
    ///
    /// <returns>Index upfront value</returns>
    public double IndexUpfrontValue()
    {
      return IndexUpfrontValue(Spread);
    }

    private double IndexUpfrontValue(double spread)
    {
      return IndexUpfrontValue(spread, this.CDX.Premium);
    }

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
    protected abstract double IndexUpfrontValue(double spread, double premium);

    /// <summary>
    ///   Expected survival of the index at expiry
    /// </summary>
    /// <returns>Survival probability from the settle to expiry</returns>
    public double ExpectedSurvival()
    {
      return SurvivalProbability(Spread);
    }

    /// <summary>
    ///   Scale survival curves such that forward intrinsic value
    ///   equals to the forward marke value on the expiration date
    /// </summary>
    public void SynchronizeSurvivalCurves()
    {
      if (survivalCurves_ == null)
        return; // nothing to do;

      // Create a forward pricer
      CDXPricer cdxPricer = ForwardCDXPricer(this.Spread);
      cdxPricer.SurvivalCurves = survivalCurves_;

      //- calculate values
      double intrisicValue = cdxPricer.IntrinsicValue(false);
      double marketValue = cdxPricer.MarketValue();
      if (Math.Abs(intrisicValue - marketValue) < 1E-9)
        return; // already synchronized, nothing to do;

      double factor = cdxPricer.BasisFactor(null, null, null, marketValue, null);
      SurvivalCurve[] survivalCurves = CloneUtil.Clone(survivalCurves_);
      CurveUtil.CurveBump(survivalCurves, null, new double[] { factor }, true, true, true, null);
      this.SurvivalCurves = survivalCurves;
      return;
    }

    /// <summary>
    ///  Calculate the probability that the option is in the money
    ///  on the expiry.
    /// </summary>
    /// <returns>Probability.</returns>
    public double ProbabilityInTheMoney()
    {
      return CalculateProbabilityInTheMoney(this.Spread);
    }

    /// <summary>
    ///  Calculate the probability that the option is in the money
    ///  on the expiry.
    /// </summary>
    /// <param name="spread">The forward spread (unadjusted).</param>
    /// <returns>Probability.</returns>
    protected abstract double CalculateProbabilityInTheMoney(double spread);
    #endregion Methods

    #region Properties

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
            paymentPricer_ = BuildPaymentPricer(Payment, discountCurve_);
        }
        return paymentPricer_;
      }
    }

    /// <summary>
    ///   CDX Option product
    /// </summary>
    public CDXOption CDXOption
    {
      get { return (CDXOption)Product; }
    }

    /// <summary>
    ///   Option style
    /// </summary>
    public OptionStyle Style
    {
      get { return CDXOption.Style; }
      set { CDXOption.Style = value; }
    }

    /// <summary>
    ///   Option type
    /// </summary>
    public OptionType Type
    {
      get { return CDXOption.Type; }
      set { CDXOption.Type = value; }
    }

    /// <summary>
    ///   Option strike in either spread (1bp = 0.0001) or price(1 = par)
    /// </summary>
    public double Strike
    {
      get { return CDXOption.Strike; }
      set { CDXOption.Strike = value; }
    }

    /// <summary>
    ///   Option strike is value rather than spread
    /// </summary>
    public bool StrikeIsPrice
    {
      get { return CDXOption.StrikeIsPrice; }
      set { CDXOption.StrikeIsPrice = value; }
    }

    /// <summary>
    ///   Current spread of underlying asset
    /// </summary>
    public double Spread
    {
      get { return GetMarketSpread(); }
      set { SetMarketCurve(value, MarketSurvivalCurve); }
    }

    /// <summary>
    ///   Annualised Volatility
    /// </summary>
    public double Volatility
    {
      get
      {
        if(Double.IsNaN(volatility_))
        {
          if(volatilitySurface_ == null){ return 0.0; }
          volatility_ = volatilitySurface_.Interpolate(
            CDXOption.Expiration, CDXOption.Strike);
        }
        return volatility_;
      }
      internal set
      {
        volatility_ = value;
      }
    }

    /// <summary>
    ///   Annualized Volatility
    /// </summary>
    public VolatilitySurface VolatilitySurface
    {
      get { return volatilitySurface_; }
    }

    /// <summary>
    ///   Time to expiration
    /// </summary>
    public double Time
    {
      get
      {
        return (CDXOption.Expiration - AsOf) / 365.25;
      }
    }

    /// <summary>
    ///   Survival curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      // In order for sensitivity codes to work
      // we need to return different sets of curves
      // based on the availability of market curves
      get
      {
        if (survivalCurves_ != null)
          return survivalCurves_;
        else
        {
          CheckForMarketSurvivalCurve();
          return new SurvivalCurve[] { MarketSurvivalCurve };
        }
          
      }
      set
      {
        if (value == null || value.Length == 0)
          survivalCurves_ = null;
        else if (value.Length == 1)
          SetMarketSurvivalCurve(value[0]);
        else
          survivalCurves_ = value;
      } // allow null value
    }

    /// <summary>
    ///   Portfolio survival curves
    /// </summary>
    public SurvivalCurve[] PortfolioSurvivalCurves
    {
      get { return survivalCurves_; }
      set
      {
        survivalCurves_ = (value != null && value.Length == 0 ? null : value);
      }
    }

    /// <summary>
    ///   Survival curve based on market spread
    /// </summary>
    public SurvivalCurve MarketSurvivalCurve
    {
      get
      {
        if (marketSurvivalCurve_ == null)
        {
          return marketSurvivalCurve_ = CreateMarketSurvivalCurve();
        }
        else
          return marketSurvivalCurve_;
      }

      set { SetMarketSurvivalCurve(value); }
    }

    /// <summary>
    /// The purpose of this method is to create a Market Survival Curve on the fly but to not hold it in the data member as the MarketSurvivalCurve property does.
    /// It is a minimal change for a bug fix, and the design of how the caching of the market survival curve is done may need to be looked at.
    /// </summary>
    /// <returns></returns>
    private SurvivalCurve CreateMarketSurvivalCurve()
    {
      if (QuotingConvention == QuotingConvention.FlatPrice)
      // convert to spread
      {
        CDX cdx = CDXOption.CDX;
        // The normal case
        cdx = new CDX(cdx.Effective, CDX.Maturity, cdx.Ccy, cdx.Premium,
          cdx.DayCount, cdx.Freq, cdx.BDConvention, cdx.Calendar);
        cdx.CopyScheduleParams(CDXOption.CDX);
        CDXPricer pricer = new CDXPricer(cdx, AsOf, Settle, DiscountCurve, 0.01);
        pricer.MarketRecoveryRate = marketRecoveryRate_;
        double spread = pricer.PriceToSpread(MarketQuote);
        return SetMarketCurve(spread, null);
      }
      else
        return SetMarketCurve(MarketQuote, null);
    }

    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get
      {
        if (survivalCurves_ == null)
        {
          CheckForMarketSurvivalCurve();
          if (MarketSurvivalCurve.SurvivalCalibrator != null
            && MarketSurvivalCurve.SurvivalCalibrator.RecoveryCurve != null)
          {
            return new RecoveryCurve[] { MarketSurvivalCurve.SurvivalCalibrator.RecoveryCurve };
          }
          return null;
        }
        RecoveryCurve[] recoveryCurves = new RecoveryCurve[survivalCurves_.Length];
        for (int i = 0; i < survivalCurves_.Length; i++)
        {
          if (survivalCurves_[i].Calibrator != null)
            recoveryCurves[i] = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
          else
            throw new ArgumentException(String.Format(
              "Must specify recoveries as curve {0} does not have recoveries from calibration",
              survivalCurves_[i].Name));
        }
        return recoveryCurves;
      }
    }

    private void CheckForMarketSurvivalCurve()
    {
      if(MarketSurvivalCurve == null)
      {
        throw new ArgumentException("If survival curves are set to null, you must set a Market Survival Curve for the whole index.");
      }
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
    ///   Underlying CDS index
    /// </summary>
    public CDX CDX
    {
      get { return CDXOption.CDX; }
    }

    /// <summary>
    ///   Recovery rate for market standard calculations
    /// </summary>
    public double MarketRecoveryRate
    {
      get { return marketRecoveryRate_; }
      set { marketRecoveryRate_ = value; }
    }

    /// <summary>
    ///   If true, adjust forward spread by front end protection
    /// </summary>
    public bool AdjustSpread
    {
      get
      {
        return (choices_ & CDXOptionModelParam.AdjustSpread)
          == CDXOptionModelParam.AdjustSpread;
      }
      private set
      {
        if (value)
        {
          choices_ |= CDXOptionModelParam.AdjustSpread;
        }
        else
        {
          choices_ &= ~CDXOptionModelParam.AdjustSpread;
        }
      }
    }

    /// <summary>
    ///   If true, ignore the underly, name by name credit curves
    /// </summary>
    public bool MarketAdjustOnly
    {
      get
      {
        return (choices_ & CDXOptionModelParam.AdjustSpreadByMarketMethod)
          == CDXOptionModelParam.AdjustSpreadByMarketMethod;
      }
    }

    /// <summary>
    ///   If true, ignore the underly, name by name credit curves
    /// </summary>
    public bool FullReplicatingMethod
    {
      get
      {
        return (choices_ & CDXOptionModelParam.FullReplicatingMethod)
          == CDXOptionModelParam.FullReplicatingMethod;
      }
    }
    
    /// <summary>
    ///   Current market quote of underlying index
    /// </summary>
    /// 
    /// <details>
    ///   <para>CreditSpread and FlatPrice  quoting types are supported
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is CreditSpread.</para>
    /// </details>
    public double MarketQuote
    {
      get { return _marketQuote.Value; }
      set { _marketQuote.Value = value; }
    }

    /// <summary>
    ///   Quoting convention for CDXOption
    /// </summary>
    public QuotingConvention QuotingConvention
    {
      get
      {
        if (_marketQuote.Type != QuotingConvention.FlatPrice)
          _marketQuote.Type = QuotingConvention.CreditSpread;
        return _marketQuote.Type;
      }
      set { _marketQuote.Type = value; }
    }

    /// <summary>
    ///   Get and set the option model parameters.
    /// </summary>
    public CDXOptionModelParam ModelParam
    {
      get { return choices_; }
      set { choices_ = value; }
    }

    /// <summary>
    /// Model basis.
    /// </summary>
    public double ModelBasis
    {
      get { return 0.0; }
    }

    /// <summary>
    /// Gets or sets the size of the basket.
    /// For internal use only.
    /// </summary>
    /// <value>The size of the basket.</value>
    internal protected int BasketSize
    {
      get { return basketSize_; }
      set { basketSize_ = value; }
    }
    #endregion

    #region Data

    private SurvivalCurve[] survivalCurves_;
    private DiscountCurve discountCurve_;
    private MarketQuote _marketQuote = new MarketQuote(
      0.0, QuotingConvention.CreditSpread);
    private VolatilitySurface volatilitySurface_;

    private SurvivalCurve marketSurvivalCurve_;
    private double marketRecoveryRate_ = 0.4;
    private int basketSize_;
    private CDXOptionModelParam choices_ =
      CDXOptionModelParam.AdjustSpread |
      CDXOptionModelParam.ForceFlatMarketCurve |
      CDXOptionModelParam.AdjustSpreadByMarketMethod;

    [Mutable] private double volatility_;
    [Mutable] private SurvivalCurve defaultSensitivityCurve_;

    private const bool _useCache = true;
    #endregion

    #region Helpers

    /// <summary>
    ///   Get market spread
    /// </summary>
    /// 
    /// <remarks>In order for the spread sensitivity functions to work,
    /// we save market spread in a survival curve called markeSurvivalCurve.
    /// Every time when we need the spread value, we retrieve it from the curve.</remarks>
    /// 
    /// <returns>market spread</returns>
    /// 
    private double GetMarketSpread()
    {
      CDX cdx = CDXOption.CDX;
      CurveTenor tenor = MarketSurvivalCurve.TenorAfter(cdx.Maturity);
      if (!(tenor.Product is CDS))
        throw new ToolkitException("Invalid CDX market spread curve!");
      CDS cds = (CDS)tenor.Product;
      return cds.Premium;
    }

    /// <summary>
    ///   Set market spread to a market suvival curve
    /// </summary>
    /// <param name="spread">Market spread</param>
    /// <param name="curve">Market survival curve</param>
    /// <returns>Survival curve fit to the spread</returns>
    private SurvivalCurve SetMarketCurve(double spread, SurvivalCurve curve)
    {
      if (spread < 0.0)
        throw new ArgumentOutOfRangeException("spread", String.Format("Spread must be non-negative. Not {0}", spread));

      // create a cdx pricer
      CDX cdx = CDXOption.CDX;
      if (curve == null)
      {
        CDXPricer pricer = new CDXPricer(cdx, AsOf, Settle, DiscountCurve, spread);
        pricer.MarketRecoveryRate = marketRecoveryRate_;
        curve = pricer.EquivalentCDSPricer.SurvivalCurve;
        curve.Flags |= CurveFlags.Internal;
      }
      else
      {
        CurveTenor tenor = curve.TenorAfter(cdx.Maturity);
        if (!(tenor.Product is CDS))
          throw new ToolkitException("CDX market spread curve tenor is not CDS!");
        CDS cds = (CDS)tenor.Product;
        cds.Premium = spread;
        curve.Calibrator.AsOf = AsOf;
        curve.Calibrator.Settle = Settle;
        curve.Fit();
      }
      return curve;
    }

    /// <summary>
    ///   Create a copy of market curve and set the given spread at the tenor corresponding to CDX maturity
    /// </summary>
    /// <param name="spread">Spread</param>
    /// <returns>SurvivalCurve</returns>
    private SurvivalCurve CloneMarketCurve(double spread)
    {
      SurvivalCurve curve = marketSurvivalCurve_;
      if (curve != null)
      {
        SurvivalFitCalibrator calibrator = (SurvivalFitCalibrator)curve.SurvivalCalibrator.Clone();
        calibrator.ForceFit = true;
        curve = (SurvivalCurve)curve.Clone();
        curve.Calibrator = calibrator;
      }
      return SetMarketCurve(spread, curve);
    }

    /// <summary>
    ///   Set market spread curve
    /// </summary>
    /// <remarks>In order for the spread sensitivity functions to work,
    /// we save market spread in a survival curve called markeSurvivalCurve.
    /// The curve can be set directly, for example, after someone bump it.</remarks>
    /// <returns>market spread</returns>
    private void SetMarketSurvivalCurve(SurvivalCurve curve)
    {
      if (curve != null)
      {
        CDX cdx = CDXOption.CDX;
        CurveTenor tenor = curve.TenorAfter(cdx.Maturity);
        if (!(tenor.Product is CDS))
          throw new ToolkitException("CDX market spread curve tenor is not CDS!");
        // clone since we ant to own this curve
        marketSurvivalCurve_ = (SurvivalCurve)curve.Clone();
        marketSurvivalCurve_.Calibrator = (Calibrator)curve.Calibrator.Clone();
      }
      else
        marketSurvivalCurve_ = CreateMarketSurvivalCurve();
      return;
    }

    /// <summary>
    ///   Calculate market value at exercise date
    /// </summary>
    ///
    /// <remarks>
    /// Given a spread, compute market value of the index at the exercise date
    /// The value is from the protection buyer's perspective.
    /// </remarks>
    ///
    /// <returns>Exercise market value</returns>
    ///
    /// <exclude />
    public double
    ExerciseMarketValue(double spread)
    {
      // construct a CDS index pricer to compute the market value
      // of the underlying index at the Expiration date

      CDXPricer pricer = ForwardCDXPricer(spread);
      double v = -pricer.MarketValue();
      return v;
    }

    /// <summary>
    ///   Calculate market value at exercise date
    /// </summary>
    ///
    /// <remarks>
    /// Given a spread, compute market value of the index at the exercise date
    /// The value is from the protection buyer's perspective.
    /// </remarks>
    ///
    /// <returns>Exercise market value</returns>
    ///
    /// <exclude />
    private double
    ExerciseStrikeValue(double spread)
    {
      double pv = ForwardPV01(spread) * (CDXOption.Strike - CDX.Premium);
      return pv;
    }

    /// <summary>
    ///   Calculate the adjusted forward spread
    /// </summary>
    /// <exclude />
    public double AdjustedForwardSpread(double spread)
    {
      double adjust = FrontEndProtection(spread) / ForwardPV01(spread);
      return ForwardSpread(spread) + adjust;
    }

    /// <summary>
    ///   Calculate the forward spread
    /// </summary>
    /// <exclude />
    protected double ForwardSpread(double spread)
    {
      if (survivalCurves_ != null && FullReplicatingMethod)
      {
        spread = 0.0;
        double durationSum = 0.0;
        for (int i = 0; i < survivalCurves_.Length; i++)
        {
          CDSCashflowPricer cdsPricer = CurveUtil.ImpliedPricer(
                      survivalCurves_[i], CDXOption.CDX.Maturity, CDXOption.CDX.DayCount,
                      CDXOption.CDX.Freq, CDXOption.CDX.BDConvention, CDXOption.CDX.Calendar);
          double duration = cdsPricer.RiskyDuration(CDXOption.Expiration);
          double cdsSpread = cdsPricer.FwdPremium(CDXOption.Expiration);
          spread += duration * cdsSpread;
          durationSum += duration;
        }
        spread /= durationSum;
      }
      return spread;
    }

    /// <summary>
    ///   Calculate the forward pv01
    /// </summary>
    /// <exclude />
    protected double ForwardPV01(double spread)
    {
      double pv01 = 0;
      if (survivalCurves_ != null && FullReplicatingMethod)
      {
        for (int i = 0; i < survivalCurves_.Length; i++)
        {
          CDSCashflowPricer cdsPricer = CurveUtil.ImpliedPricer(
                      survivalCurves_[i], CDXOption.CDX.Maturity, CDXOption.CDX.DayCount,
                      CDXOption.CDX.Freq, CDXOption.CDX.BDConvention, CDXOption.CDX.Calendar);
          double weight = CDXOption.CDX.Weights == null ? 1.0 / survivalCurves_.Length : CDXOption.CDX.Weights[i];
          pv01 += cdsPricer.RiskyDuration(CDXOption.Expiration) * weight;
        }
        pv01 *= DiscountCurve.DiscountFactor(AsOf, CDXOption.Expiration) * SurvivalProbability(spread);
      }
      else
      {
        CDXPricer pricer = ForwardCDXPricer(spread);
        pv01 = pricer.RiskyDuration();
        pv01 *= DiscountCurve.DiscountFactor(AsOf, pricer.AsOf) * SurvivalProbability(spread);
      }
      return pv01;
    }

    private static CDX CreateCompatibleCDX(CDX cdx, Dt effective, Dt maturity)
    {
      Dt firsCpn = cdx.Schedule.GetNextCouponDate(effective);
      Dt lastCpn = cdx.Schedule.GetPrevCouponDate(maturity);
      if (firsCpn > lastCpn) firsCpn = Dt.Empty;
      CDX note = new CDX(effective, maturity, cdx.Ccy, cdx.Premium,
        cdx.DayCount, cdx.Freq, cdx.BDConvention, cdx.Calendar, cdx.Weights);
      note.FirstPrem = firsCpn;
      if (lastCpn > effective) note.LastCoupon = lastCpn;
      note.CycleRule = cdx.Schedule.CycleRule;
      note.CashflowFlag = cdx.CashflowFlag;
      note.Weights = cdx.Weights;
      return note;
    }

    /// <summary>
    ///   Calculate front end protection
    ///  </summary>
    ///
    /// <remarks>Front end protection is the present value of the expected
    /// cumulative loss from settlement to option expiration date.</remarks>
    /// <exclude />
    protected double FrontEndProtection(double spread)
    {
      Dt settle = Settle;
      Dt expiry = CDXOption.Expiration;
      CDX cdx = CDXOption.CDX;
      Dt effective = cdx.Effective;

      // For forward CDX starting after the expiration, or the option expired before settle
      // upfront protection is zero
      if ( (effective >= expiry)|| (expiry <= settle) )
        return 0.0;

      // The normal case
      if (UseProtectionPvForFrontEnd && !FullReplicatingMethod)
      {
        //- Losses from the names defaulted between the pricer settle and the expiry
        double lossPv, remain;
        CheckDefaultedNames(true, out lossPv, out remain);

        cdx = CreateCompatibleCDX(cdx, effective, expiry);
        CDXPricer pricer = new CDXPricer(cdx, AsOf, settle, DiscountCurve, spread);
        pricer.MarketRecoveryRate = marketRecoveryRate_;
        if (!ForceFlatMarketCurve)
          pricer.UserSpecifiedMarketSurvivalCurve = CloneMarketCurve(spread);
        pricer.Notional = 1.0;

        //return -pricer.EquivalentCDSPricer.ProtectionPv(); // Make it positive!!
        return lossPv + remain * spread * pricer.EquivalentCDSPricer.RiskyDuration();
      }
      else
      {
        // cumulative loss from settle to option expiry
        double cumuLoss = 0.0;
        if (Dt.Cmp(settle, effective) < 0)
          settle = effective; // For forward start CDX

        if (survivalCurves_ != null && (!MarketAdjustOnly || FullReplicatingMethod))
        {
          // If we have underlying portfolio, calculate from the survival curves
          double[] weights = cdx.Weights;
          for (int i = 0; i < survivalCurves_.Length; i++)
          {
            SurvivalCurve sc = survivalCurves_[i];
            double recoveryRate = sc.SurvivalCalibrator.RecoveryCurve.RecoveryRate(expiry);
            double loss = sc.DefaultProb(settle, expiry) * (1 - recoveryRate);
            cumuLoss += loss * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
          }
        }
        else
        {
          //- Losses from the names defaulted between the pricer settle and the expiry
          double loss, remain;
          CheckDefaultedNames(false, out loss, out remain);

          //- If we do not have underlying portfolio, calculate from the market data
          cdx = CreateCompatibleCDX(cdx, effective, expiry);
          CDXPricer cdxPricer = new CDXPricer(cdx, AsOf, Settle, DiscountCurve, spread);
          cdxPricer.MarketRecoveryRate = marketRecoveryRate_;
          if (!ForceFlatMarketCurve)
            cdxPricer.UserSpecifiedMarketSurvivalCurve = CloneMarketCurve(spread);
          CDSCashflowPricer pricer = cdxPricer.EquivalentCDSPricer;
          double recoveryRate = pricer.RecoveryRate;
          SurvivalCurve sc = pricer.SurvivalCurve;
          cumuLoss = loss + remain * sc.DefaultProb(settle, expiry) * (1 - recoveryRate);
        }

        cumuLoss *= DiscountCurve.DiscountFactor(AsOf, expiry);
        return cumuLoss;
      }
    }

    /// <summary>
    ///   Calculate the remaining notional at expiry and the loss pv
    ///   from the names defaulted between settle and expiry
    /// </summary>
    /// <param name="discount">Whether to discount the default losses</param>
    /// <param name="loss">Default loss pv</param>
    /// <param name="remain">Remaining notional at expiry</param>
    private void CheckDefaultedNames(bool discount, out double loss, out double remain)
    {
      var sc = survivalCurves_;
      var rc = RecoveryCurves;
      if (sc == null || sc.Length == 0)
      {
        sc = new[] {defaultSensitivityCurve_};
        if (rc == null || rc.Length != 1)
          rc = new[] {new RecoveryCurve(AsOf, MarketRecoveryRate)};
      }
      sc.CheckDefaultedNames(rc, CDX.Weights, AsOf, Settle, CDXOption.Expiration,
        discount ? DiscountCurve : null, BasketSize, false, out loss, out remain);
    }

    /// <summary>
    ///   Calculate average survival probability
    /// </summary>
    /// <exclude />
    protected double SurvivalProbability(double spread)
    {
      Dt settle = Settle;
      Dt expiry = CDXOption.Expiration;
      CDX cdx = CDXOption.CDX;
      Dt effective = cdx.Effective;

      // For forward CDX starting after the expiration, survival is one
      if ( (effective >= expiry) || (expiry <= settle) )
        return 1.0;

      // The normal case
      cdx = CreateCompatibleCDX(cdx, effective, expiry);
      double sp;
      if (survivalCurves_ == null || (MarketAdjustOnly && !FullReplicatingMethod && !ForceIntrinsicSurvival))
      {
        CDXPricer pricer = new CDXPricer(cdx, settle, settle, DiscountCurve, spread);
        pricer.MarketRecoveryRate = marketRecoveryRate_;
        if (!ForceFlatMarketCurve)
          pricer.UserSpecifiedMarketSurvivalCurve = CloneMarketCurve(spread);
        sp = pricer.EquivalentCDSPricer.SurvivalProbability();
      }
      else
      {
        CDXPricer pricer = new CDXPricer(cdx, settle, settle, DiscountCurve, SurvivalCurves, spread);
        pricer.MarketRecoveryRate = marketRecoveryRate_;
        sp = pricer.IntrinsicSurvivalProbability();
      }
      return sp;
    }

    /// <summary>
    ///   Create an index pricer starting at the expiration date.
    /// </summary>
    /// <exclude />
    protected CDXPricer ForwardCDXPricer(double spread)
    {
      CDXOption cdxo = CDXOption;
      Dt fwdDate = cdxo.Expiration;
      CDX cdx = cdxo.CDX;
      if (ForceCleanValue && Dt.Cmp(cdx.Effective, fwdDate) < 0)
      {
        cdx = CreateCompatibleCDX(cdx, fwdDate, cdxo.CDX.Maturity);
      }
      CDXPricer pricer = new CDXPricer(cdx, fwdDate, fwdDate, DiscountCurve, spread);
      pricer.MarketRecoveryRate = marketRecoveryRate_;
      if (!ForceFlatMarketCurve)
        pricer.UserSpecifiedMarketSurvivalCurve = CloneMarketCurve(spread);
      return pricer;
    }

    /// <summary>
    ///   Convert forward price to forward spread
    /// </summary>
    /// <param name="price">Forward price</param>
    /// <returns>Forward spread</returns>
    protected internal double PriceToSpread(double price)
    {
      CDXPricer pricer = ForwardCDXPricer(this.Spread);
      double spread = pricer.PriceToSpread(price);
      return spread;
    }

    /// <summary>
    ///   Convert forward spread to forward price
    /// </summary>
    /// <param name="spread">Forward spread</param>
    /// <returns>Forward price</returns>
    protected internal double SpreadToPrice(double spread)
    {
      CDXPricer pricer = ForwardCDXPricer(spread);
      return pricer.SpreadToPrice(spread);
    }

    /// <summary>
    ///   Accrued at the expiration date
    /// </summary>
    private double ForwardAccrued()
    {
      CDXPricer pricer = ForwardCDXPricer(this.Spread);
      return pricer.Accrued();
    }
    #endregion Helpers

    #region IDefaultSensitivityCurvesGetter Members

    IList<SurvivalCurve> IDefaultSensitivityCurvesGetter.GetCurves()
    {
      if (survivalCurves_ != null || basketSize_ <= 0)
      {
        defaultSensitivityCurve_ = null;
        return this.SurvivalCurves;
      }
      else
      {
        defaultSensitivityCurve_ = CreateMarketSurvivalCurve();
        if ((defaultSensitivityCurve_.Flags & CurveFlags.Internal) == 0)
        {
          defaultSensitivityCurve_ = (SurvivalCurve)defaultSensitivityCurve_.Clone();
          defaultSensitivityCurve_.Flags |= CurveFlags.Internal;
        }
        defaultSensitivityCurve_.Name = "MarketCurve";
        return new SurvivalCurve[] { defaultSensitivityCurve_ };
      }
    }

    #endregion

    #region ICreditIndexOptionPricer Members

    VolatilitySurface ICreditIndexOptionPricer.VolatilitySurface
    {
      get { return VolatilitySurface; }
    }

    double ICreditIndexOptionPricer.AtTheMoneyForwardValue
    {
      get { return IndexUpfrontValue() / DiscountFactor; }
    }

    double ICreditIndexOptionPricer.ForwardStrikeValue
    {
      get { return StrikeValue() / DiscountFactor; }
    }

    double ICreditIndexOptionPricer.ForwardUpfrontValue
    {
      get { return ExerciseMarketValue(Spread) / Notional / DiscountFactor; }
    }

    double ICreditIndexOptionPricer.FrontEndProtection
    {
      get { return FrontEndProtection(Spread) / DiscountFactor; }
    }
    double ICreditIndexOptionPricer.ExistingLoss
    {
      get { return 0; }
    }

    double ICreditIndexOptionPricer.ExpectedSurvival
    {
      get { return ExpectedSurvival(); }
    }

    double ICreditIndexOptionPricer.ForwardPv01
    {
      get { return ForwardPV01(); }
    }

    double ICreditIndexOptionPricer.OptionIntrinsicValue
    {
      get { return Intrinsic() / Notional; }
    }

    CDXPricer ICreditIndexOptionPricer.GetPricerForUnderlying()
    {
      return new CDXPricer(CDXOption.CDX, AsOf,
        Settle, DiscountCurve, SurvivalCurves)
      {
        MarketQuote = MarketQuote,
        QuotingConvention = QuotingConvention,
        MarketRecoveryRate = MarketRecoveryRate,
      };
    }

    double ICreditIndexOptionPricer.CalculateFairPrice(double volatility)
    {
      return Double.IsNaN(volatility)
        ? MarketValue() / Notional
        : MarketValue(volatility) / Notional;
    }

    double ICreditIndexOptionPricer.CalculateExerciseProbability(
      double volatility)
    {
      if (Double.IsNaN(volatility)) return ProbabilityInTheMoney();
      var pricer = (CDXOptionPricer)MemberwiseClone();
      pricer.Reset();
      pricer.Volatility=volatility;
      return pricer.ProbabilityInTheMoney();
    }

    double ICreditIndexOptionPricer.ImplyVolatility(double fairValue)
    {
      return IVol(fairValue);
    }

    private double DiscountFactor
    {
      get { return DiscountCurve.DiscountFactor(AsOf, CDXOption.Expiration); }
    }
    #endregion

    #region IPricer<CDXOption> Members

    CDXOption IPricer<CDXOption>.Product
    {
      get { return CDXOption; }
    }

    #endregion
  } // class CDXOptionPricer
}  
