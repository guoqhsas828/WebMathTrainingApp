// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CDSOption">CDS Option</see> using the
  ///   <see cref="BaseEntity.Toolkit.Models.CDSOptionModel">CDS Option Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDSOption" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.CDSOptionModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDSOption">CDS Option Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CDSOptionModel">CDS Option Model</seealso>
  [Serializable]
  public class CDSOptionPricer : PricerBase, IPricer
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDSOptionPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="option">CDS Option to price</param>
    public
      CDSOptionPricer(CDSOption option)
      : base(option)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="option">Contingent Swap Leg to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurve">Survival curve for underlying name</param>
    /// <param name="volatility">Volatility (in percent) of spread</param>
    /// <remarks>
    ///   <para>The recovery rate is taken from the survival curve.</para>
    ///   <para>The default step size is set to 1 Week.</para>
    ///   <para>The default Skew is set to 2.0 (Normal)</para>
    /// </remarks>
    ///
    /// <example>
    /// <code language="C#">
    ///   DiscountCurve discountCurve;
    ///   SurvivalCurve survivalCurve;
    ///   Dt asOf, settle;
    ///   // ...
    ///   // Set up discountCurve, survivalCurve, asOf and settle dates.
    ///
    ///   // Set up the terms of the option on CDS.
    ///   Dt effective = new Dt(20, 9, 2004);        // Original effective date of option
    ///   PayerReceiver type = PayerReceiver.Payer;  // Option type
    ///   double strike = 0.0075;                    // Option strike of 75bp
    ///   Dt expiration = new Dt(20, 11, 2004);      // Option expiration on 10 Nov'04
    ///   Dt maturity = new Dt(20, 9, 2009);         // Maturity date of underlying cd is 20 Oct'09
    ///
    ///   // Create the CDS option
    ///   CDSOption cdsOption =
    ///     new CDSOption( effective,                // Effective date of option
    ///                    maturity,                 // Maturity date of the underlying CDS
    ///                    Currency.USD,             // Currency of the CDS
    ///                    DayCount.Actual360,       // Daycount of the CDS premium
    ///                    Frequency.Quarterly,      // Payment frequency of the CDS
    ///                    BDConvention.Following,   // Business day convention of the CDS
    ///                    Calendar.NYB,             // Calendar for the CDS
    ///                    expiration,               // Option expiration
    ///                    type,                     // Option type
    ///                    OptionStyle.European,     // Option style
    ///                    strike );                 // Option strike
    ///
    ///   // Set up pricing parameters
    ///   double vol = 1.2;   // 120%
    ///
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer( cdsOption, asOf, settle, discountCurve, survivalCurve, vol );
    ///
    /// </code>
    /// </example>
    ///
    public
      CDSOptionPricer(CDSOption option,
                      Dt asOf,
                      Dt settle,
                      DiscountCurve discountCurve,
                      SurvivalCurve survivalCurve,
                      double volatility)
      : base(option, asOf, settle)
    {
      discountCurve_ = discountCurve;
      survivalCurve_ = survivalCurve;
      stepSize_ = 1;
      stepUnit_ = TimeUnit.Weeks;
      volatility_ = volatility;
      skew_ = 2.0;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      CDSOptionPricer obj = (CDSOptionPricer)base.Clone();
      obj.discountCurve_ = (DiscountCurve)discountCurve_.Clone();
      obj.survivalCurve_ = (SurvivalCurve)survivalCurve_.Clone();
      obj.recoveryCurve_ = (recoveryCurve_ != null) ? (RecoveryCurve)recoveryCurve_.Clone() : null;

      return obj;
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

      CDSOption.Validate(errors);

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      if (survivalCurve_ == null)
        InvalidValue.AddError(errors, this, "SurvivalCurve", String.Format("Invalid survival curve. Cannot be null"));
      if (stepSize_ < 0.0)
        InvalidValue.AddError(errors, this, "StepSize", String.Format("Invalid step size. Must be >= 0, not {0}", stepSize_));
      if (skew_ < 0.0 || skew_ > 2.0)
        InvalidValue.AddError(errors, this, "Skew", String.Format("Invalid skew {0}. Must be >= 0 and <= 2", skew_));

      base.Validate(errors);

      return;
    }

    /// <summary>
    ///   Calculates Fair dollar value for CDS Option
    /// </summary>
    ///
    /// <returns>Pv of CDS Option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer(cdsOption, asOf, settle, discountCurve, survivalCurve,
    ///   											1, TimeUnit.Weeks, recoveryRate, vol, skew);
    ///
    ///   // Calculate pv or fair value
    ///   double fairValue = pricer.Pv();
    ///
    /// </code>
    /// </example>
    ///
    public override double
      ProductPv()
    {
      if (Dt.Cmp(CDSOption.Expiration, AsOf) < 0)
        return 0;

      double pv;
      if (SurvivalCurve.DefaultDate.IsValid() && (Dt.Cmp(SurvivalCurve.DefaultDate, CDSOption.Expiration) < 0))
      {
        if (!Knockout && CDSOption.Type == OptionType.Call)
          pv = -(1 - RecoveryRate) * Notional * DiscountCurve.DiscountFactor(AsOf, SurvivalCurve.DefaultDate);
        else if (!Knockout && CDSOption.Type == OptionType.Put)
          pv = (1 - RecoveryRate) * Notional * DiscountCurve.DiscountFactor(AsOf, SurvivalCurve.DefaultDate);
        else
          pv = 0.0;
      }
      else
      {
        pv = CDSOptionModel.Pv(AsOf,
                               CDSOption.Style,
                               CDSOption.Type,
                               CDSOption.Expiration,
                               CDSOption.Strike,
                               CDSOption.CDS.FirstPrem,
                               CDSOption.CDS.Maturity,
                               CDSOption.CDS.DayCount,
                               CDSOption.CDS.Freq,
                               CDSOption.CDS.BDConvention,
                               CDSOption.CDS.Calendar,
                               CDSOption.CDS.AccruedOnDefault,
                               RecoveryRate,
                               DiscountCurve,
                               SurvivalCurve,
                               Volatility,
                               Skew);
        pv *= Notional;

        if (!Knockout)
          pv += calcFrontEndProtection();
      }

      return pv;
    }

    /// <summary>
    ///   Calculate the fair price of the Option
    /// </summary>
    ///
    /// <returns>the fair price (PV in percent) of the option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDXOptionPricer pricer =
    ///     new CDSOptionPricer( cdsOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///
    ///   // Calculate fair value as a percentage of notional
    ///   double fairPrice = pricer.FairPrice();
    ///
    /// </code>
    /// </example>
    ///
    public double
      FairPrice()
    {
      return ProductPv() / Notional;
    }

    /// <summary>
    ///   Calculates dollar value of front end protection for CDS Option
    /// </summary>
    ///
    /// <remarks>
    ///   Front end protection is the protection PV of the CDS from settle date
    ///   to option expiration date.
    /// </remarks>
    ///
    /// <returns>Up front protection</returns>
    ///
    public double
      FrontEndProtection()
    {
      return (!Knockout) ? calcFrontEndProtection() : 0.0;
    }

    /// <summary>
    ///   Calculates up front protection for CDS Option
    /// </summary>
    ///
    /// <remarks>
    ///   Up-front protection is the protection PV of the CDS from settle date
    ///   to option expiration date.
    /// </remarks>
    ///
    /// <returns>Up front protection per dollar of noyional</returns>
    ///
    private double
      calcFrontEndProtection()
    {
      if (CDSOption.Type == OptionType.Call)
        return 0; // only payer option has UpFrontProtection

      CDS cds = CDSOption.CDS;
      cds = new CDS(Settle, CDSOption.Expiration, cds.Ccy, cds.Premium,
                    cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar);
      CDSCashflowPricer pricer =
        new CDSCashflowPricer(cds, AsOf, Settle, DiscountCurve,
                              SurvivalCurve, StepSize, StepUnit);
      pricer.RecoveryCurve = RecoveryCurve;
      pricer.Notional = this.Notional;

      return -pricer.ProtectionPv(); // Make it positive!!
    }

    /// <summary>
    ///   Calculates implied volatility for CDS Option
    /// </summary>
    ///
    /// <param name="fv">Fair value of CDS Option (as a percentage of Notional)</param>
    ///
    /// <returns>Implied volatility of CDS Option</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer(cdsOption, asOf, settle, discountCurve, survivalCurve,
    ///   											1, TimeUnit.Weeks, recoveryRate, 0.0, skew);
    ///
    ///   // Calculate the implied price from the volatility
    ///   double vol = pricer.IVol(fv);
    ///
    /// </code>
    /// </example>
    ///
    public double
      IVol(double fv)
    {
      Dt jumpDate = this.SurvivalCurve.JumpDate;
      if (!jumpDate.IsEmpty() && Dt.Cmp(jumpDate, this.CDSOption.Expiration) < 0)
        return Double.NaN;

      if (Dt.Cmp(CDSOption.Expiration, AsOf) < 0)
        throw new ToolkitException(String.Format("Out of range error: As-of ({0}) must be prior to expiration ({1})", AsOf, CDSOption.Expiration));

      if (!Knockout)
        fv -= calcFrontEndProtection() / this.Notional;

      logger.Debug(String.Format("IVol({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14})",
                                 AsOf, CDSOption.Style, CDSOption.Type, CDSOption.Expiration, CDSOption.Strike,
                                 CDSOption.CDS.FirstPrem,
                                 CDSOption.CDS.Maturity, CDSOption.CDS.DayCount, CDSOption.CDS.Freq,
                                 CDSOption.CDS.BDConvention,
                                 CDSOption.CDS.Calendar, CDSOption.CDS.AccruedOnDefault, RecoveryRate, fv, Skew));

      double vol = CDSOptionModel.IVol(AsOf,
                                       CDSOption.Style,
                                       CDSOption.Type,
                                       CDSOption.Expiration,
                                       CDSOption.Strike,
                                       CDSOption.CDS.FirstPrem,
                                       CDSOption.CDS.Maturity,
                                       CDSOption.CDS.DayCount,
                                       CDSOption.CDS.Freq,
                                       CDSOption.CDS.BDConvention,
                                       CDSOption.CDS.Calendar,
                                       CDSOption.CDS.AccruedOnDefault,
                                       RecoveryRate,
                                       DiscountCurve,
                                       SurvivalCurve,
                                       fv,
                                       Skew);

      logger.Debug(String.Format("IVol returns {0}", vol));

      return vol;
    }

    /// <summary>
    ///   Calculate forward premium for the underlying CDS on the option
    ///   expiration date.
    /// </summary>
    ///
    /// <returns>The forward premium of the underlying CDS</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer(cdsOption, asOf, settle, discountCurve, survivalCurve,
    ///   											1, TimeUnit.Weeks, recoveryRate, 0.0, skew);
    ///
    ///   // Calculate the expected forward price of the CDS
    ///   double forwardSpread = pricer.ForwardPremium();
    ///
    /// </code>
    /// </example>
    ///
    public double
      ForwardPremium()
    {
      double forward = CDSOptionModel.ForwardPremium(AsOf,
                                                     CDSOption.Style,
                                                     CDSOption.Type,
                                                     CDSOption.Expiration,
                                                     CDSOption.Strike,
                                                     CDSOption.CDS.FirstPrem,
                                                     CDSOption.CDS.Maturity,
                                                     CDSOption.CDS.DayCount,
                                                     CDSOption.CDS.Freq,
                                                     CDSOption.CDS.BDConvention,
                                                     CDSOption.CDS.Calendar,
                                                     CDSOption.CDS.AccruedOnDefault,
                                                     RecoveryRate,
                                                     DiscountCurve,
                                                     SurvivalCurve);
      return forward;
    }

    /// <summary>
    ///   Numeraire used in discounting and pricing for CDS Option model
    /// </summary>
    ///
    /// <returns>The Numeraire used in discounting and pricing for CDS Option model</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer(cdsOption, asOf, settle, discountCurve, survivalCurve,
    ///   											1, TimeUnit.Weeks, recoveryRate, 0.0, skew);
    ///
    ///   // Calculate the numeraire
    ///   double numeraire = pricer.Numeraire();
    ///
    /// </code>
    /// </example>
    ///
    public double
      Numeraire()
    {
      double numeraire = CDSOptionModel.Numeraire(AsOf,
                                                  CDSOption.Expiration,
                                                  CDSOption.CDS.FirstPrem,
                                                  CDSOption.CDS.Maturity,
                                                  CDSOption.CDS.DayCount,
                                                  CDSOption.CDS.Freq,
                                                  CDSOption.CDS.BDConvention,
                                                  CDSOption.CDS.Calendar,
                                                  CDSOption.CDS.AccruedOnDefault,
                                                  RecoveryRate,
                                                  DiscountCurve,
                                                  SurvivalCurve);
      return numeraire;
    }

    /// <summary>
    ///   Intrinsic value
    /// </summary>
    ///
    /// <returns>Intrinsic dollar value (pv to pricing as-of date)</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer(cdsOption, asOf, settle, discountCurve, survivalCurve,
    ///   											1, TimeUnit.Weeks, recoveryRate, 0.0, skew);
    ///
    ///   // Calculate the instrinsic value
    ///   double intrinsic = pricer.Intrinsic();
    ///
    /// </code>
    /// </example>
    ///
    public double
      Intrinsic()
    {
      double S = ForwardPremium();
      double K = CDSOption.Strike;
      double price = (CDSOption.Type == OptionType.Put ? S - K : K - S);
      if (price <= 0)
        return 0;
      double pv01 = Numeraire()
                    * DiscountCurve.Interpolate(AsOf, CDSOption.Expiration)
                    * SurvivalCurve.Interpolate(AsOf, CDSOption.Expiration);
      double iv = pv01 * 10000.0 * price * Notional;

      if (!Knockout)
        iv += calcFrontEndProtection();
      return iv;
    }

    /// <summary>
    ///   Calculate the vega for a CDS Option
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The vega is calculated as the difference between the current
    ///   fair value, and the fair value after increasing the volatility by
    ///   the specified bump size.</para>
    /// </remarks>
    ///
    /// <param name="bump">Bump for volatility in percent (0.01 = 1 percent)</param>
    ///
    /// <returns>The CDS Option vega</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDS Option.
    ///   CDSOptionPricer pricer =
    ///   	new CDSOptionPricer(cdsOption, asOf, settle, discountCurve, survivalCurve,
    ///   											1, TimeUnit.Weeks, recoveryRate, vol, skew);
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
      Dt jumpDate = this.SurvivalCurve.JumpDate;
      if (!jumpDate.IsEmpty() && Dt.Cmp(jumpDate, this.CDSOption.CDS.Maturity) < 0)
        return Double.NaN;

      if (Dt.Cmp(CDSOption.Expiration, AsOf) < 0)
        throw new ToolkitException(String.Format("Out of range error: As-of ({0}) must be prior to expiration ({1})", AsOf, CDSOption.Expiration));
      double orig = CDSOptionModel.Pv(AsOf,
                                      CDSOption.Style,
                                      CDSOption.Type,
                                      CDSOption.Expiration,
                                      CDSOption.Strike,
                                      CDSOption.CDS.FirstPrem,
                                      CDSOption.CDS.Maturity,
                                      CDSOption.CDS.DayCount,
                                      CDSOption.CDS.Freq,
                                      CDSOption.CDS.BDConvention,
                                      CDSOption.CDS.Calendar,
                                      CDSOption.CDS.AccruedOnDefault,
                                      RecoveryRate,
                                      DiscountCurve,
                                      SurvivalCurve,
                                      Volatility,
                                      Skew);

      double up = CDSOptionModel.Pv(AsOf,
                                    CDSOption.Style,
                                    CDSOption.Type,
                                    CDSOption.Expiration,
                                    CDSOption.Strike,
                                    CDSOption.CDS.FirstPrem,
                                    CDSOption.CDS.Maturity,
                                    CDSOption.CDS.DayCount,
                                    CDSOption.CDS.Freq,
                                    CDSOption.CDS.BDConvention,
                                    CDSOption.CDS.Calendar,
                                    CDSOption.CDS.AccruedOnDefault,
                                    RecoveryRate,
                                    DiscountCurve,
                                    SurvivalCurve,
                                    Volatility + bump,
                                    Skew);

      return (up - orig) * Notional;
    }

    /// <summary>
    ///   Daily basis point volatility
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The daily basis point volatility assuming a 252 business day
    ///   year.</para>
    ///
    ///   <formula>
    ///     DailyVol(bp) = \frac{ForwardSpread \times AnnualVol}{\sqrt{252}}
    ///   </formula>
    ///
    ///   <para><i>where:</i></para>
    ///   <para><i>ForwardSpread = Adjusted Forward Spread in basis points</i></para>
    ///   <para><i>AnnualVol = Annualised percentage volatility</i></para>
    ///
    ///   <para>This can be shown to be roughly the daily breakeven volatility. If our
    ///   daily spread move is greater than this amount then buying volatility is
    ///   profitable.</para>
    ///
    /// </remarks>
    ///
    /// <returns>the daily basis point volatility</returns>
    ///
    public double
      BpVolatility()
    {
      return Volatility * ForwardPremium() / Math.Sqrt(250.0);
    }

    /// <summary>
    ///   Calculate Market Delta
    /// </summary>
    ///
    /// <remarks>Market delta is defined as the ratio of the change in option price
    ///  to the change in the CDS upfront for a 1bp widening of CDS spread.
    ///  It describes how the option price changes with the underlying CDS price.</remarks>
    ///
    /// <param name="bumpSize">Bump size in basis points</param>
    ///
    /// <returns>Delta in raw number</returns>
    public double MarketDelta(double bumpSize)
    {
      Dt jumpDate = this.SurvivalCurve.JumpDate;
      if (!jumpDate.IsEmpty() && Dt.Cmp(jumpDate, this.CDSOption.Expiration) < 0)
        return Double.NaN;
      if (Math.Abs(bumpSize) < 1.0E-8)
        throw new ArgumentException("bumpSize size too small");
      double[] pvs0 = bumpedPvs(0.0);
      double[] pvs1 = bumpedPvs(bumpSize);
      return (pvs1[0] - pvs0[0]) / (pvs1[1] - pvs0[1]);
    }

    /// <summary>
    ///   Calculate Market Gamma
    /// </summary>
    ///
    /// <remarks>Market gamma is defined as the change in market delta
    ///  for a 1bp bump of CDS spread.</remarks>
    ///
    /// <param name="bumpSize">Bump size basis points</param>
    /// <param name="scale">If true, scale gamma by bump size</param>
    ///
    /// <returns>Gamma in raw number</returns>
    ///
    public double MarketGamma(double bumpSize, bool scale)
    {
      Dt jumpDate = this.SurvivalCurve.JumpDate;
      if (!jumpDate.IsEmpty() && Dt.Cmp(jumpDate, this.CDSOption.Expiration) < 0)
        return Double.NaN;
      if (bumpSize < 0)
        bumpSize = -bumpSize;
      if (bumpSize < 1.0E-8)
        throw new ArgumentException("bumpSize size too small");
      double[] basePvs = bumpedPvs(0.0);
      double[] upPvs = bumpedPvs(bumpSize);
      double[] downPvs = bumpedPvs(-bumpSize);
      double upDelta = (upPvs[0] - basePvs[0]) / (upPvs[1] - basePvs[1]);
      double downDelta = (downPvs[0] - basePvs[0]) / (downPvs[1] - basePvs[1]);
      double gamma = upDelta - downDelta;
      if (scale) gamma /= (10000 * bumpSize);
      return gamma;
    }

    /// <summary>
    ///   Calculate option price and underlying index upfront after bumping the market spread
    /// </summary>
    /// <param name="bumpSize">bump size</param>
    /// <returns>option price and underlying index upfront</returns>
    private double[] bumpedPvs(double bumpSize)
    {
      double[] pvs = new double[2];
      SurvivalCurve survivalCurve = SurvivalCurve;
      CDSCashflowPricer cdsPricer = new CDSCashflowPricer(
        CDSOption.CDS, AsOf, Settle, DiscountCurve,
        survivalCurve, null, 0.0, StepSize, StepUnit);
      SurvivalCurve savedCurve = null;
      if (Math.Abs(bumpSize) > 1E-8)
      {
        savedCurve = (SurvivalCurve)survivalCurve.Clone();
        bool up = true;
        if (bumpSize < 0)
        {
          up = false;
          bumpSize = -bumpSize;
        }
        CurveUtil.CurveBump(SurvivalCurve, null, bumpSize, up, false, true);
        Reset();
        cdsPricer.Reset();
      }
      pvs[0] = ProductPv();
      pvs[1] = -cdsPricer.FlatPrice() * Notional;
      if (savedCurve != null)
      {
        CurveUtil.CopyQuotes(survivalCurve, savedCurve);
        survivalCurve.Set(savedCurve);
        Reset();
        cdsPricer.Reset();
      }
      return pvs;
    }

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
    ///   Product
    /// </summary>
    public CDSOption CDSOption
    {
      get { return (CDSOption)Product; }
      set { Product = value; }
    }

    /// <summary>
    ///   Discount curve for pricing.
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set { discountCurve_ = value; }
    }

    /// <summary>
    ///   Survival curve for underlying name.
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survivalCurve_; }
      set { survivalCurve_ = value; }
    }

    /// <summary>
    ///   Recovery curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If a separate recovery curve has not been specified, the recovery from the survival
    ///   curve is used. In this case the survival curve must have a Calibrator which provides a
    ///   recovery curve otherwise an exception will be thrown.</para>
    /// </remarks>
    ///
    public RecoveryCurve RecoveryCurve
    {
      get
      {
        if (recoveryCurve_ != null)
          return recoveryCurve_;
        else if (survivalCurve_ != null && survivalCurve_.SurvivalCalibrator != null)
          return survivalCurve_.SurvivalCalibrator.RecoveryCurve;
        else
          return null;
      }
      set { recoveryCurve_ = value; }
    }

    /// <summary>
    ///   Return the recovery rate matching the maturity of this product.
    /// </summary>
    public double RecoveryRate
    {
      get { return (RecoveryCurve != null) ? RecoveryCurve.RecoveryRate(CDSOption.CDS.Maturity) : 0.0; }
    }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public int StepSize
    {
      get { return stepSize_; }
      set { stepSize_ = value; }
    }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set { stepUnit_ = value; }
    }

    /// <summary>
    ///   Volatility of the forward spread (in %).
    /// </summary>
    public double Volatility
    {
      get { return volatility_; }
      set { volatility_ = value; }
    }

    /// <summary>
    ///   Skewness parameter for CEV model.
    /// </summary>
    public double Skew
    {
      get { return skew_; }
      set { skew_ = value; }
    }

    /// <summary>
    ///   Knockout or not
    /// </summary>
    public bool Knockout
    {
      get { return CDSOption.Knockout; }
    }

    #endregion Properties

    #region Data

    private DiscountCurve discountCurve_;
    private SurvivalCurve survivalCurve_;
    private RecoveryCurve recoveryCurve_;
    private int stepSize_;
    private TimeUnit stepUnit_;
    private double volatility_;
    private double skew_;

    #endregion // Data
  }

  // class CDSOptionPricer
}