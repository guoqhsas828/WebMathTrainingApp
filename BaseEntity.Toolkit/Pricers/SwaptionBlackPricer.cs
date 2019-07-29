/*
 * SwaptionBlackPricer.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using Cashflow = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// <para>Price a <see cref="BaseEntity.Toolkit.Products.Swaption">Swaption</see> using the
  /// <see cref="BaseEntity.Toolkit.Models.Black">Black Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Swaption" />
  /// <para><h2>Black Model</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.Black" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Swaption">Swaption Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.Black">Black model</seealso>
  [Serializable]
  public class SwaptionBlackPricer : PricerBase, IPricer, ILockedRatesPricerProvider
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SwaptionBlackPricer));

    #region Constructors

    /// <summary>
    ///   Constructor for a Swaption pricer
    /// </summary>
    ///
    /// <param name="product">Swaption to price</param>
    ///
    protected SwaptionBlackPricer(Swaption product)
      : base(product)
    { }

    /// <summary>
    /// Construct a Swaption pricer
    /// </summary>
    /// <param name="swaption">Swaption to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="referenceCurve">(Floating Leg) Reference curve (if required)</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="volatilityObject">The volatility object.</param>
    public SwaptionBlackPricer(
      Swaption swaption,
      Dt asOf,
      Dt settle,
      DiscountCurve referenceCurve,
      DiscountCurve discountCurve,
      IVolatilityObject volatilityObject)
      : base(swaption, asOf, settle)
    {
      ReferenceCurve = referenceCurve;
      DiscountCurve = discountCurve;
      volatilityObject_ = volatilityObject;
      SetUpVolatilityCalculator();
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="swaption"></param>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="discountCurve"></param>
    /// <param name="referenceCurve"></param>
    /// <param name="rateResets"></param>
    /// <param name="volatilityObject"></param>
    public SwaptionBlackPricer(
      Swaption swaption,
      Dt asOf,
      Dt settle,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      RateResets rateResets,
      IVolatilityObject volatilityObject)
      : base(swaption, asOf, settle)
    {
      rateResets_ = rateResets;
      ReferenceCurve = referenceCurve;
      DiscountCurve = discountCurve;
      volatilityObject_ = volatilityObject;
      SetUpVolatilityCalculator();
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Clone
    /// </summary>
    ///
    public override object Clone()
    {
      var obj = (SwaptionBlackPricer)base.Clone();
      obj.discountCurve_ = (discountCurve_ != null)
        ? (DiscountCurve)discountCurve_.Clone() : null;
      // Careful with clone of reference curve as this may be shared with discount curve.
      if (referenceCurve_ == null)
        obj.referenceCurve_ = null;
      else if (referenceCurve_ == discountCurve_)
        obj.referenceCurve_ = obj.discountCurve_;
      else
        obj.referenceCurve_ = (DiscountCurve)referenceCurve_.Clone();
      obj.rateResets_ = CloneUtil.Clone(rateResets_);
      obj.volatilityObject_ = (IVolatilityObject)volatilityObject_.Clone();
      obj.SetUpVolatilityCalculator();

      return obj;
    }

    /// <summary>
    /// Sets up volatility calculator.
    /// </summary>
    /// <param name="context">The context.</param>
    [OnDeserialized, AfterFieldsCloned]
    private void SetUpVolatilityCalculator(StreamingContext context)
    {
      SetUpVolatilityCalculator();
    }

    /// <summary>
    /// Sets up volatility calculator.
    /// </summary>
    internal void SetUpVolatilityCalculator()
    {
      if (volatilityObject_ == null) return;
      var fn = SwaptionVolatilityFactory.GetCalculator(
        volatilityObject_, Swaption, this);
      volatilityCalculator_ = () => fn(AsOf);
      SetUpTimeCalculator();
    }

    private void SetUpTimeCalculator()
    {
      var c = volatilityObject_ as SabrSwaptionVolatilityCube;
      timeCalculator_ = c == null
        ? (begin, end) => Dt.Fraction(begin, end, DayCount.Actual365Fixed)
        : c.TimeCalculator;
    }

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

      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve","Invalid discount curve. Cannot be null");

      if (Swaption.UnderlyingFloatLeg.Ccy != Swaption.UnderlyingFixedLeg.Ccy)
        InvalidValue.AddError(errors, this, "Invalid Swap Legs. The 2 legs must use same currency");

      if (ReferenceIndex == null)
        InvalidValue.AddError(errors, this, "Missing reference index on the floating leg.");

      if (ModelType == SwaptionModelType.LinearTerminalSwapRate
        && Swaption.SettlementType != SettlementType.Cash)
      {
        throw new ArgumentException("LinearTerminalSwapRate " +
          "model is only for cash-settled swaption");
      }

      return;
    }


    /// <summary>
    ///   Calculate the MTM of the swaption
    /// </summary>
    ///
    /// <returns>MTM of Swaption </returns>
    ///
    public override double ProductPv()
    {
      var swpn = this.Swaption;
      switch (ModelType)
      {
        case SwaptionModelType.PhysicalStandard:
          return PhysicalStandardPv();
        case SwaptionModelType.CashStandard:
          return CashStandardPv();
        case SwaptionModelType.LinearTerminalSwapRate:
          return SwaptionTerminalSwapRateModel.LinearTsrPv(swpn,
            AsOf, Settle, DiscountCurve, ReferenceCurve,
            VolatilityObject, RateResets)*Notional;
        default:
          throw new ArgumentException("The model type is not supported");
      }
    }

    private double PhysicalStandardPv()
    {
      // swaption expiration should be the first reset date of floating leg !!!
      var swaption = this.Swaption;
      double T = Time; // fixed leg daycount  ???

      if (T <= 0)  //Option has passed its expiration
        return 0.0;

      // Interpolate swaption volatility for this maturity
      double swaptionVol = volatilityCalculator_();

      //calculate swap rate
      double effectiveStrike = swaption.EffectiveSwaptionStrike(Settle, Settle,
        DiscountCurve, ReferenceCurve, RateResets, false,
        out var rate, out var level);
      level *= Notional;

      return CalcPv(level, rate, effectiveStrike, swaptionVol, T,
        swaption.OptionType, volatilityObject_.DistributionType);
    }

    private double CashStandardPv()
    {
      var swaption = this.Swaption;
      double T = Time; 

      if (T <= 0)  
        return 0.0;

      double swaptionVol = volatilityCalculator_();

      var settle = Settle;
      var discountCurve = DiscountCurve;
      double effectiveStrike = swaption.EffectiveSwaptionStrike(settle, settle,
        discountCurve, ReferenceCurve, RateResets, false, 
        out var rate, out var level);

      level = CalcCashLevel(swaption, settle, settle, discountCurve, false, rate);
      level *= Notional;

      return CalcPv(level, rate, effectiveStrike, swaptionVol, T, 
        swaption.OptionType, volatilityObject_.DistributionType);
    }


    private double CalcPv(double annunity, double rate, double strike,
      double volatility, double time, OptionType optionType, DistributionType volType)
    {
      double swaptionValue;
      if (volType == DistributionType.LogNormal)
      {
        if (strike <= 0 || rate <= 0)
        {
          swaptionValue = annunity * Math.Max((optionType 
            == OptionType.Call ? 1.0 : -1.0) * (rate - strike), 0.0);
        }
        else
        {
          swaptionValue = annunity * Black.P(optionType, time, rate, strike, volatility);
        }
      }
      else if (volType == DistributionType.Normal)
      {
        swaptionValue = annunity * BlackNormal.P(optionType, time, 0, rate, strike, volatility);
      }
      else
      {
        throw new ToolkitException("DistributionType {0} not supported!", volType);
      }

      // Done
      return swaptionValue;
    }

    private double CalcCashLevel(Swaption swpn, Dt asOf, Dt settle, 
      DiscountCurve discountCurve,  bool excludeNotionalPay, double pRate)
    {
      var fixedLeg = swpn.UnderlyingFixedLeg;

      var unitFixedPs = new SwapLegPricer(fixedLeg, asOf, settle, 1.0, discountCurve,
        null, null, null, null, null).GetPaymentSchedule(null, settle);

      foreach (var payment in unitFixedPs.OfType<FixedInterestPayment>())
      {
        payment.FixedCoupon = 1.0;
      }

      var unitFixedCf = RateVolatilityUtil.GetPsCashflowAdapter(unitFixedPs,
        excludeNotionalPay, fixedLeg.Notional);

      return SwaptionTerminalSwapRateModel.CashSettledAnnunity(
        unitFixedCf, (int) fixedLeg.Freq, pRate);
    }

    #region Private Calculations

    private class BlackGreeks
    {
      internal double Delta, Gamma, Theta, Vega, Level;
    }

    /// <summary>
    ///   Calculate Swaption Sensitivities
    /// </summary>
    ///
    /// <returns>Swaption Sensitivities</returns>
    ///
    private BlackGreeks SwaptionSensitivities()
    {
      if(ModelType == SwaptionModelType.LinearTerminalSwapRate)
        throw new ArgumentException("Cannot use linear terminal swap" +
          "rate model to calculate black greeks");

      // Interpolate swaption volatility for this maturity
      double swaptionVol = volatilityCalculator_();

      // swaption expiration should be the first reset date of floating leg !!!
      var swaption = this.Swaption;
      double T = Time; // fixed leg daycount  ???

      //calculate swap rate
      double level, rate;
      double effectiveStrike = swaption.EffectiveSwaptionStrike(Settle, Settle,
        DiscountCurve, ReferenceCurve, RateResets, false, out rate, out level);

      if (ModelType == SwaptionModelType.CashStandard)
      {
        level = CalcCashLevel(swaption, Settle, Settle, DiscountCurve, false, rate);
      }

      level *= Notional;

      // Sensitivities
      double delta = 0, gamma = 0, vega = 0, theta = 0;
      if (volatilityObject_.DistributionType == DistributionType.LogNormal)
      {
        Black.P(swaption.OptionType, T, rate, effectiveStrike, swaptionVol,
          ref delta, ref gamma, ref theta, ref vega);
        vega *= 0.01;
      }
      else if (volatilityObject_.DistributionType == DistributionType.Normal)
      {
        BlackNormal.P(swaption.OptionType, T, 0, rate, effectiveStrike,
          swaptionVol, ref delta, ref gamma, ref theta, ref vega);
        vega *= 0.0001;
      }
      else
      {
        throw new ToolkitException("DistributionType {0} not supported!",
          volatilityObject_.DistributionType);
      }

      return new BlackGreeks
      {
        Delta = delta,
        Gamma = gamma,
        Theta = theta,
        Vega = vega,
        Level = level
      };
    }

    private double[] Rate01(double upBump, double downBump)
    {
      var origCalculator = volatilityCalculator_;
      try
      {
        //For backward compatibility, we keep the volatility constant.
        var sigma = origCalculator();
        volatilityCalculator_ = () => sigma;

        // Calculate
        DataTable dataTable = Sensitivities.Rate(new[] {this}, null, 0.0,
          upBump, downBump, false, true, BumpType.Uniform, null, true,
          false, null, false, null, null, "Keep", null);

        double delta = (double)(dataTable.Rows[0])["Delta"];
        double gamma = (double)(dataTable.Rows[0])["Gamma"];

        if (Swaption.Type == PayerReceiver.Payer)
          return new[] { delta, gamma };
        else
          return new[] { -delta, gamma };
      }
      finally
      {
        volatilityCalculator_ = origCalculator;
      }
    }

    #endregion

    /// <summary>
    ///   Calculate the delta(hedge) of the swaption
    /// </summary>
    ///
    /// <returns>Delta(Hedge) of the swaption</returns>
    ///
    public double DeltaHedge()
    {
      return Math.Abs(SwaptionSensitivities().Delta);
    }

    /// <summary>
    ///   Calculate the gamma of the Swaption
    ///   (Change in DV01 due to 1bp bump of discount and reference curves)
    /// </summary>
    ///
    /// <returns>Gamma value of the swaption</returns>
    ///
    public double Gamma()
    {
      return Rate01(4, 4)[1];
    }

    /// <summary>
    ///   Calculate Vega by Black formula.
    /// </summary>
    /// <returns>Vega</returns>
    public double Vega()
    {
      var g = SwaptionSensitivities();
      return g.Vega*g.Level;
    }

    ///<summary>
    /// Calculate the swaption pv change caused by volatility shift
    ///</summary>
    ///
    /// <remarks>
    ///   <para>The Vega is the change in PV (MTM)
    ///   if the underlying volatility is shifted in parallel up by
    ///   1%.</para>
    ///
    ///   <para>The Vega is calculated by calculating the PV (MTM)
    ///   then bumping up the underlying volatility curve by 1% (100bps)
    ///   and re-calculating the PV and returning the difference in value .</para>
    /// </remarks>
    ///
    /// <returns>Vega</returns>
    ///
    ///<param name="bumpSize">Bump size on volatiity</param>
    ///<returns>Vega</returns>
    public double Vega(double bumpSize)
    {
      var origCalculator = volatilityCalculator_;
      try
      {
        // Calculate
        double origPv = ProductPv();
        // Bump the volatility up by bumpSize and reprice;
        volatilityCalculator_ = () => (origCalculator() + bumpSize);
        double bumpedPv = ProductPv();

        return (bumpedPv - origPv);
      }
      finally
      {
        volatilityCalculator_ = origCalculator;
      }
    }

    /// <summary>
    ///   Calculate theta by Black formula.
    /// </summary>
    /// <returns>Theta</returns>
    public double Theta()
    {
      var g = SwaptionSensitivities();
      return g.Theta*g.Level/365;
    }

    /// <summary>
    ///   Calculate theta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   full price, and the full price at the specified future pricing date
    ///   <paramref name="toAsOf"/> and future settlement date
    ///   <paramref name="toSettle"/>.</para>
    ///
    ///   <para>All term structures are held constant while moving the
    ///   the pricing and settlement dates (ie the 30 day discount factor remain
    ///   unchanged relative to the pricing dates.</para>
    /// </remarks>
    ///
    /// <param name="toAsOf">Forward pricing date</param>
    /// <param name="toSettle">Forward settlement date</param>
    ///
    /// <returns>MTM impact of moving pricing and settlement dates forward</returns>
    ///
    public double Theta(Dt toAsOf, Dt toSettle)
    {
      // Remember all the as-of dates we have.
      Dt asOf = AsOf;
      Dt settle = Settle;
      Dt dcAsOf = (DiscountCurve != null) ? DiscountCurve.AsOf : asOf;
      Dt rfAsOf = (ReferenceCurve != null) ? ReferenceCurve.AsOf : asOf;

      // Compute base case
      double theta = -ProductPv();

      try
      {
        // Set all dates forward
        AsOf = toAsOf;
        Settle = toSettle;
        if (DiscountCurve != null) DiscountCurve.AsOf = toAsOf;
        if (ReferenceCurve != null) ReferenceCurve.AsOf = toAsOf;

        // Reprice
        theta += ProductPv();
      }
      finally
      {
        // Restore all modified dates
        AsOf = asOf;
        Settle = settle;
        if (DiscountCurve != null) DiscountCurve.AsOf = dcAsOf;
        if (ReferenceCurve != null) ReferenceCurve.AsOf = rfAsOf;
      }
      return theta;
    }

    /// <summary>
    ///   Calculate the DV01
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The DV01 is the change in PV (MTM)
    ///   if the underlying rate curves are shifted in parallel up by
    ///   one basis point(for both swap legs).</para>
    ///
    ///   <para>The DV01 is calculated by calculating the PV (MTM)
    ///   then bumping up the underlying discount curve by 1 bps
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    ///
    /// <returns>DV01</returns>
    ///
    public double DV01()
    {
      return Rate01(4, 4)[0];
    }

    /// <summary>
    /// Calculates implied volatility for IR Swaption
    /// </summary>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double IVol()
    {
      // There is no need to go through all the redundant calculations
      // and introduce uneccessary solver errors.
      //return IVol(ProductPv(), volatilityObject_.DistributionType);
      return volatilityCalculator_();
    }

    /// <summary>
    /// Calculates implied volatility for IR Swaption
    /// </summary>
    /// <param name="pv">The pv.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double IVol(double pv)
    {
      return IVol(pv, volatilityObject_.DistributionType);
    }

    /// <summary>
    /// Calculates implied volatility for IR Swaption
    /// </summary>
    /// <param name="outputType">Type of the output.</param>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double IVol(DistributionType outputType)
    {
      return IVol(ProductPv(), outputType);
    }

    /// <summary>
    /// Calculates implied volatility for IR Swaption
    /// </summary>
    /// <param name="fv">MTM of IR swaption in dollars</param>
    /// <param name="outputType">Type of the volatility output.</param>
    /// <returns>Implied volatility of IR Swaption</returns>
    public double IVol(double fv, DistributionType outputType)
    {
      logger.Debug("Calculating implied volatility for IR Swaption...");

      // Set up root finder
      var rf = new Brent2();
      rf.setToleranceX(1e-6);
      rf.setToleranceF(1e-8);
      rf.setLowerBounds(1E-10);

      // Setup solver function.
      // We do a memberwise clone and leave the original pricer intact.
      var pricer = (SwaptionBlackPricer) ShallowCopy();
      Func<double, double> fn = (sigma) =>
      {
        pricer.volatilityCalculator_ = () => sigma;
        return pricer.ProductPv();
      };

      double v = fn(0.1);
      double res;
      try
      {
        res = (v >= fv)
        ? rf.solve(fn, null, fv, 0.01, 0.10)
        : rf.solve(fn, null, fv, 0.1, 0.8);
      }
      catch (SolverException)
      {
        res = v >= fv ? 0.01 : 0.8;
      }


      // Ensure vol is reported in the correct terms
      return LogNormalToNormalConverter.ConvertSwaption(AsOf, Settle,
        DiscountCurve, ReferenceCurve, Swaption, res, RateResets,
        volatilityObject_.DistributionType, outputType);
    }

    /// <summary>
    /// Calculates the forward swap rate.
    /// </summary>
    /// <returns>
    ///   <see cref="Double"/>
    /// </returns>
    public double ForwardSwapRate()
    {
      var floatLegPricer = new SwapLegPricer(Swaption.UnderlyingFloatLeg,
        AsOf, Settle, Notional, DiscountCurve, Swaption.UnderlyingFloatLeg.ReferenceIndex, ReferenceCurve, RateResets,
        null, null);

      var fixedLegPricer = new SwapLegPricer(Swaption.UnderlyingFixedLeg,
        AsOf, Settle, Notional, DiscountCurve, null, null, null, null, null);

      //calculate swap rate
      double level;
      return SwapPricer.ParCoupon(fixedLegPricer, floatLegPricer, out level);
    }
    #endregion // Methods

    #region Utility Methods
    ///<summary>
    /// This method calculates the vega sensitivity based on the volatility interpolated by new inputs
    ///</summary>
    ///<returns>Vega sensitivity</returns>
    public static double SwaptionBumpVega(SwaptionBlackPricer pricer, Dt expiry, Tenor fwdTenor)
    {
      var bumpPricer = (SwaptionBlackPricer) pricer.MemberwiseClone();
      var swapEffective = RateVolatilityUtil.SwaptionStandardForwardSwapEffective(expiry,
                                                                                  bumpPricer.Swaption.NotificationDays,
                                                                                  bumpPricer.Swaption.
                                                                                    NotificationCalendar);
      SwapLeg fixedLeg = (SwapLeg) bumpPricer.Swaption.UnderlyingFixedLeg.Clone();
      fixedLeg.Effective = swapEffective;
      fixedLeg.Maturity = Dt.Add(swapEffective, fwdTenor);
      fixedLeg.AmortizationSchedule.Clear();
      fixedLeg.CouponSchedule.Clear();
      fixedLeg.Coupon = CurveUtil.DiscountForwardSwapRate( bumpPricer.ReferenceCurve, swapEffective, fixedLeg.Maturity,
                                                         fixedLeg.DayCount,
                                                         fixedLeg.Freq,
                                                         fixedLeg.BDConvention,
                                                         fixedLeg.Calendar);

      SwapLeg floatLeg = (SwapLeg) bumpPricer.Swaption.UnderlyingFloatLeg.Clone();
      floatLeg.Effective = swapEffective;
      floatLeg.Maturity = Dt.Add(swapEffective, fwdTenor);
      floatLeg.AmortizationSchedule.Clear();
      floatLeg.CouponSchedule.Clear();


      var swaption = new Swaption(bumpPricer.Swaption.Effective, swapEffective,
                                  bumpPricer.Swaption.Ccy, fixedLeg, floatLeg, bumpPricer.Swaption.NotificationDays,
                                  bumpPricer.Swaption.Type, bumpPricer.Swaption.Style, fixedLeg.Coupon)
                                  {
                                    OptionRight = bumpPricer.Swaption.OptionRight,
                                    NotificationCalendar = bumpPricer.Swaption.NotificationCalendar
                                  };

      bumpPricer.Product = swaption;

      var fn = SwaptionVolatilityFactory.GetCalculator(bumpPricer.volatilityObject_,
                                                       swaption, bumpPricer);
      bumpPricer.volatilityCalculator_ = () => fn(bumpPricer.AsOf);
      var origPv = bumpPricer.ProductPv();

      // Bump the volatility up by 1% and reprice;
      bumpPricer.volatilityCalculator_ = () => (fn(bumpPricer.AsOf) + 0.01);
      double bumpedPv = bumpPricer.ProductPv();

      return (bumpedPv - origPv);

    }


    #endregion

    #region Properties

    /// <summary>
    ///   Cap Product
    /// </summary>
    public Swaption Swaption
    {
      get { return (Swaption)Product; }
    }

    /// <summary>
    /// Gets the time to expiry.
    /// </summary>
    /// <remarks></remarks>
    public double Time
    {
      get
      {
        return timeCalculator_ == null
          ? (Swaption.Expiration - AsOf) / 365.0
          : timeCalculator_(AsOf, Swaption.Expiration);
      }
    }

    /// <summary>
    ///   Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set { referenceCurve_ = value; }
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
    /// Gets or sets the reference of the floating leg.
    /// </summary>
    /// <value>The index of the reference.</value>
    public ReferenceIndex ReferenceIndex
    {
      get { return Swaption.UnderlyingFloatLeg.ReferenceIndex; }
    }

    /// <summary>
    ///   Volatility Curve
    /// </summary>
    public IVolatilityObject VolatilityObject
    {
      get { return volatilityObject_; }
      set
      {
        volatilityObject_ = value;
        SetUpVolatilityCalculator();
      }
    }

    /// <summary>
    ///   The Black volatility.
    /// </summary>
    public double Volatility
    {
      get
      {
        return volatilityCalculator_ == null
          ? Double.NaN : (volatilityCalculator_());
      }
    }

    /// <summary>
    /// Effective strike. The 
    /// </summary>
    public double EffectiveStrike
    {
      get
      {
        double rate, level;
        return RateVolatilityUtil.EffectiveSwaptionStrike(Swaption, AsOf, Settle, DiscountCurve, ReferenceCurve,
                                                          RateResets, false, out rate, out level);
      }
    }

    /// <summary>
    ///   Historical rate fixings
    /// </summary>
    public RateResets RateResets
    {
      get
      {
        if (rateResets_ == null)
          rateResets_ = new RateResets();
        return rateResets_;
      }
      internal set { rateResets_ = value; }
    }

    /// <summary>
    ///   Current floating rate
    /// </summary>
    public double CurrentRate
    {
      get
      {
        bool found;
        return rateResets_.GetRate(AsOf, out found);
      }
      set
      {
        // Set the RateResets to support returning the current coupon
        rateResets_ = new RateResets();
        rateResets_.AllResets.Add(Swaption.UnderlyingFloatLeg.Effective, value);
        Reset();
      }
    }

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

    /// <summary>
    /// Swaption model type
    /// </summary>
    public SwaptionModelType ModelType { get; set; }
    #endregion Properties

    #region Data

    private DiscountCurve referenceCurve_;
    private DiscountCurve discountCurve_;
    private IVolatilityObject volatilityObject_;
    [NonSerialized, NoClone, Mutable]
    private Func<double> volatilityCalculator_;
    [NonSerialized, NoClone, Mutable]
    private Func<Dt,Dt,double> timeCalculator_;

    private RateResets rateResets_;
    #endregion // Data

    #region ILockedRatesPricerProvider Members

    /// <summary>
    ///   Get a pricer in which all the rate fixings with the reset dates on
    ///   or before the anchor date are fixed at the current projected values.
    /// </summary>
    /// <param name="anchorDate">The anchor date.</param>
    /// <returns>The original pricer instance if no rate locked;
    ///   Otherwise, the cloned pricer with the rates locked.</returns>
    /// <remarks>This method never modifies the original pricer,
    ///  whose states and behaviors remain exactly the same before
    ///  and after calling this method.</remarks>
    IPricer ILockedRatesPricerProvider.LockRatesAt(Dt anchorDate)
    {
      // We lock the rates using a dumy pricer for the underlying floating leg
      // as the locked rates provider.
      var swaplegPricer = new SwapLegPricer(Swaption.UnderlyingFloatLeg,
        Settle, Settle, 1.0, DiscountCurve,
        Swaption.UnderlyingFloatLeg.ReferenceIndex,
        ReferenceCurve, RateResets, null, null);
      var lockedRatesPricer = (SwapLegPricer)
        ((ILockedRatesPricerProvider)swaplegPricer).LockRatesAt(anchorDate);

      // If nothing locked, return this instance.
      if (ReferenceEquals(lockedRatesPricer, swaplegPricer)) return this;
      // Otherwise, returns a clone with the rates locked.
      return this.FastClone(new FastCloningContext
      {
        {swaplegPricer.RateResets, lockedRatesPricer.RateResets},
        {swaplegPricer.ReferenceIndex, lockedRatesPricer.ReferenceIndex}
      });
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      return this;
    }

    #endregion
  }
}
