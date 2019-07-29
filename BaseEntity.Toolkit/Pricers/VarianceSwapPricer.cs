// 
//   2017. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Models;
using log4net;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.VarianceSwap">Variance Swap</see> 
  /// using a variance swap curve and interest rate discount curve for MtM.
  /// 
  /// Theoretical values may be computed with a volatility surface
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.VarianceSwap" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.VarianceSwap">Fx Forward Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Curves.VolatilityCurve">Fx Curve</seealso>
  [Serializable]
  public partial class VarianceSwapPricer : PricerBase, IPricer, ILockedRatesPricerProvider
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof(VarianceSwapPricer));

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The stock price and dividend rate are implied from the stock forward curve.</para>
    /// </remarks>
    /// <param name="varianceSwap">Variance swap</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional">Variance swap notional</param>
    /// <param name="underlyingCurve">Term structure of stock forward prices</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="varianceSwapCurve">Variance swap curve</param>
    /// <param name="resets">Realized prices</param>
    /// <param name="useAsOfResets">Use As-Of resets</param>
    public VarianceSwapPricer(
      VarianceSwap varianceSwap, Dt asOf, Dt settle, double notional, StockCurve underlyingCurve,
      DiscountCurve discountCurve, VolatilityCurve varianceSwapCurve, RateResets resets, bool useAsOfResets)
      : this(varianceSwap, asOf, settle, notional, underlyingCurve, discountCurve, varianceSwapCurve, null, resets, useAsOfResets) { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The stock price and dividend rate are implied from the stock forward curve.</para>
    /// </remarks>
    /// <param name="varianceSwap">Variance swap</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional">Variance swap notional</param>
    /// <param name="underlyingCurve">Term structure of stock forward prices</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="varianceSwapCurve">Variance swap curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="resets">Realized prices</param>
    /// <param name="useAsOfResets">Use As-Of resets</param>
    public VarianceSwapPricer(
      VarianceSwap varianceSwap, Dt asOf, Dt settle, double notional, StockCurve underlyingCurve,
      DiscountCurve discountCurve, VolatilityCurve varianceSwapCurve, IVolatilitySurface volSurface, RateResets resets, bool useAsOfResets)
      : base(varianceSwap, asOf, settle)
    {
      Notional = notional;
      StockCurve = underlyingCurve;
      DiscountCurve = (discountCurve ?? underlyingCurve.DiscountCurve);
      VolatilitySurface = volSurface;
      VarianceSwapCurve = varianceSwapCurve;
      VarianceStrike = varianceSwap.StrikePrice;
      PriceObservations = resets;
      UseAsOfResets = useAsOfResets;
    }

    #endregion Constructors

    #region Methods

    #region Initialization 

    /// <summary>
    /// Gets the rate projector.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="swap">The variance swap</param>
    /// <param name="refIndex">Refence Index</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="stockCurve">Stock curve</param>
    /// <param name="varianceCurve">The variance swap curve</param>
    /// <param name="volatilitySurface">The volatility surface</param>
    /// <param name="theoretical">Theoretical approach</param>
    /// <param name="approximate">Approximate price observation schedule</param>
    /// <returns></returns>
    public IRateProjector GetRateProjector(Dt asOf, VarianceSwap swap, ReferenceIndex refIndex, DiscountCurve discountCurve, 
      CalibratedCurve stockCurve, VolatilityCurve varianceCurve, IVolatilitySurface volatilitySurface, bool theoretical, bool approximate)
    {
      if (!theoretical)
        return new VarianceCurveCalculator(asOf, refIndex, stockCurve, varianceCurve, true, approximate, UseAsOfResets);
      return new VarianceOptionReplicationCalculator(asOf, refIndex, DiscountCurve, stockCurve, volatilitySurface, true, approximate, UseAsOfResets);
    }

    #endregion

    /// <summary>
    /// Shallow copy 
    /// </summary>
    /// <returns>A fx forward pricer object.</returns>
    public override object Clone()
    {
      return new VarianceSwapPricer(Product as VarianceSwap, AsOf, Settle, Notional, StockCurve, DiscountCurve, VarianceSwapCurve, VolatilitySurface, PriceObservations, UseAsOfResets);
    }

    /// <summary>
    ///   Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    public override double Accrued()
    {
      return 0.0;
    }

    private PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from, Dt to, bool theoreticalValue)
    {
      if (ps == null)
        ps = new PaymentSchedule();
      if (from > VarianceSwap.EffectiveMaturity)
        return null;
      var index = new EquityPriceIndex("Stock", VarianceSwap.Ccy, DayCount.None, VarianceSwap.Calendar, BDConvention.Following, SpotDays)
      {
        HistoricalObservations = PriceObservations
      };
      var rateProjector = GetRateProjector(AsOf, VarianceSwap, index, DiscountCurve, StockCurve, 
        VarianceSwapCurve, VolatilitySurface, theoreticalValue, ApproximateForFastCalculation);
      var swap = VarianceSwap;
      var payment = new VariancePayment(swap.EffectiveMaturity, swap.Ccy, swap.Effective, swap.ValuationDate, 1.0, 0.0, rateProjector, null);
      ps.AddPayment(payment);
      return ps;
    }

    /// <summary>
    /// Get the payment schedule: a detailed representation of payments
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">Include payments starting from from date</param>
    /// <returns>payments associated to the swap</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(paymentSchedule, from, Dt.Empty, false);
    }

    /// <summary>
    /// Get the payment schedule: a detailed representation of payments
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">Include payments starting from from date</param>
    /// <returns>payments associated to the swap</returns>
    public PaymentSchedule GetTvPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(paymentSchedule, from, Dt.Empty, true);
    }

    /// <summary>
    ///   Validate pricer inputs
    /// </summary>
    /// <param name="errors">Error list</param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", string.Format("Invalid discount curve. Must be specified"));
      if (VarianceSwapCurve == null && VolatilitySurface == null)
        InvalidValue.AddError(errors, this, "VarianceSwapCurve", string.Format("Invalid variance swap curve. Must be specified if volatility surface is not specified"));
    }

    /// <summary>
    ///   Reset the pricer
    ///   <preliminary/>
    /// </summary>
    ///
    /// <remarks>
    ///   
    /// </remarks>
    /// 
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <exclude/>
    public override void Reset(ResetAction what)
    {
      if (what == ResetSettle && IncludePaymentOnSettle && Product.Effective == ProductSettle)
        IncludePaymentOnSettle = false;
      base.Reset(what);
    }

    /// <summary>
    /// Calculate ATM forward price for the underlying at the option expiration date
    /// </summary>
    /// <remarks>
    /// <para>The ATM forward price is implied from the spot price of the underlying, any dividends,
    /// and the funding rate.</para>
    /// </remarks>
    /// <returns>ATM forward price</returns>
    public double AtmForwardPrice()
    {
      return StockCurve.Interpolate(Product.Maturity);
    }

    /// <summary>
    /// Present value of a swap leg at pricing as-of date given pricing arguments
    /// </summary>
    /// <returns>Present value of the swap leg</returns>
    public override double ProductPv()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;

      var unitNotional = VarianceSwap.Notional > 0 ? VarianceSwap.Notional : 1.0;
      var ps = GetPaymentSchedule(null, AsOf);
      var variance = ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true) * 10000; // *100^2
      var kSquared = VarianceSwap.StrikePrice * VarianceSwap.StrikePrice; // already in percent
      var discountedPayoff = variance - kSquared * DiscountCurve.DiscountFactor(Product.EffectiveMaturity);
      return discountedPayoff / unitNotional * CurrentNotional;
    }

    /// <summary>
    ///  Variance Swap Payoff (fractional)
    /// </summary>
    /// <returns></returns>
    public double Payoff()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;
      var ps = GetPaymentSchedule(null, AsOf);
      var kSquared = VarianceSwap.StrikePrice * VarianceSwap.StrikePrice / 10000.0; // convert to fraction
      var variance = ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true) / DiscountCurve.DiscountFactor(Product.EffectiveMaturity);
      return variance - kSquared;
    }

    /// <summary>
    ///  Variance (fractional)
    /// </summary>
    /// <returns></returns>
    public double Variance()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;
      var ps = GetPaymentSchedule(null, AsOf);
      var variance = ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true);
      return variance / DiscountCurve.DiscountFactor(Product.EffectiveMaturity);
    }

    /// <summary>
    /// Theoretical value of a swap leg at pricing as-of date given pricing arguments
    /// </summary>
    /// <returns>Present value of the swap leg</returns>
    public double Tv()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;

      var unitNotional = VarianceSwap.Notional > 0 ? VarianceSwap.Notional : 1.0;
      var ps = GetTvPaymentSchedule(null, AsOf);
      var variance = ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true) * 10000.0;
      var kSquared = VarianceSwap.StrikePrice * VarianceSwap.StrikePrice;
      var discountedPayoff = variance - kSquared * DiscountCurve.DiscountFactor(Product.EffectiveMaturity);
      return discountedPayoff / unitNotional * CurrentNotional;
    }

    /// <summary>
    ///  Variance Swap Payoff
    /// </summary>
    /// <returns></returns>
    public double TvPayoff()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;
      var ps = GetTvPaymentSchedule(null, AsOf);
      var kSquared = VarianceSwap.StrikePrice * VarianceSwap.StrikePrice / 10000.0;
      var variance = ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true) / DiscountCurve.DiscountFactor(Product.EffectiveMaturity);
      return variance - kSquared;
    }

    /// <summary>
    ///  Variance
    /// </summary>
    /// <returns></returns>
    public double TvVariance()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;
      var ps = GetTvPaymentSchedule(null, AsOf);
      var variance = ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true);
      return variance / DiscountCurve.DiscountFactor(Product.EffectiveMaturity);
    }

    #endregion

    #region Properties

    /// <summary>
    ///  Variance Swap Product
    /// </summary>
    public VarianceSwap VarianceSwap => Product as VarianceSwap;

    /// <summary>
    /// Historical Rate Resets
    /// </summary>
    public RateResets RateResets { get; internal set; }

    /// <summary>
    ///  Variance strike
    /// </summary>
    public double VarianceStrike { get; private set; }
    
    /// <summary>
    ///  Variance strike
    /// </summary>
    public double VarianceNotional => Notional;

    /// <summary>
    ///  Variance strike
    /// </summary>
    public double VegaNotional => 2 * VarianceStrike * VarianceNotional;

    /// <summary>
    ///  Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Current quoted price of underlying stock from <see cref="StockCurve"/>
    /// </summary>
    public double UnderlyingPrice => StockCurve.SpotPrice;
    
    /// <summary>
    /// Flat dividend yield to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded dividend yield to option expiration date.</para>
    /// </remarks>
    public double Dividend => StockCurve.ImpliedDividendYield(Settle, Product.Maturity);

    /// <summary>
    /// Discrete dividend schedule
    /// </summary>
    public DividendSchedule DividendSchedule => StockCurve.Dividends;
    
    /// <summary>
    /// Forward curve of underlying asset
    /// </summary>
    public StockCurve StockCurve { get; private set; }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr => RateCalc.Rate(DiscountCurve, AsOf, Product.Maturity);
    
    /// <summary>
    /// ATM volatility to option expiration
    /// </summary>
    /// <remarks>
    /// <para>ATM flat volatility to option expiration date.</para>
    /// </remarks>
    public double Volatility => VolatilitySurface.Interpolate(Product.Maturity, AtmForwardPrice(), AtmForwardPrice());
    
    /// <summary>
    /// Volatility Surface
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }
    
    /// <summary>
    /// Variance swap curve
    /// </summary>
    public VolatilityCurve VarianceSwapCurve { get; set; }

    /// <summary>
    /// Natural number of settle days for the product
    /// </summary>
    public int SpotDays { get; set; }

    ///<summary>
    /// The natural "Settle" date for the pricer
    ///</summary>
    /// <remarks>
    /// Will be the same as Settle except for forward pricing cases
    /// </remarks>
    public Dt ProductSettle => Dt.AddDays(AsOf, SpotDays, VarianceSwap.Calendar);

    /// <summary>
    /// Include payment on trade settlement date
    /// </summary>
    public bool IncludePaymentOnSettle { get; set; }

    /// <summary>
    ///  Price Observations
    /// </summary>
    public RateResets PriceObservations { get; set; }

    /// <summary>
    ///  Use As-Of Resets
    /// </summary>
    public bool UseAsOfResets { get; set; }

    /// <summary>
    /// Build payment pricer
    /// </summary>
    public override IPricer BuildPaymentPricer(Payment payment, DiscountCurve discountCurve)
    {
      if (payment == null || payment.PayDt <= ProductSettle) return null;
      var oneTimeFee = new OneTimeFee(payment.Ccy, payment.Amount, payment.PayDt, "");
      var pricer = new SimpleCashflowPricer(oneTimeFee, AsOf, ProductSettle, discountCurve, null);
      pricer.Add(payment.PayDt, payment.Amount, 0.0, 0.0, 0.0, 0.0, 0.0);
      return pricer;
    }

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment == null) return paymentPricer_;
        return paymentPricer_ ?? (paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve));
      }
    }

    ///<summary>
    /// Present value of any additional payment associated with the pricer.
    ///</summary>
    ///<returns></returns>
    public override double PaymentPv()
    {
      return PaymentPricer != null && Payment.PayDt > ProductSettle ? PaymentPricer.Pv() : 0.0;
    }

    /// <summary>
    /// Valuation currency
    /// </summary>
    public override Currency ValuationCurrency => DiscountCurve.Ccy;

    #endregion

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
      var ps = GetPaymentSchedule(null, AsOf);

      // For non-compounding coupon and custom schedule,
      // we just need to lock the coupons.
      var modified = GetModifiedRateResets(RateResets,
        ps.EnumerateProjectedRates(), anchorDate);
      if (modified == null) return this;

      // We need return a modified pricer.
      var pricer = (VarianceSwapPricer)ShallowCopy();
      pricer.RateResets = modified;
      return pricer;
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      var ps = GetPaymentSchedule(null, asOf);
      var pmt = ps.GetPaymentsByType<CommodityFloatingPricePayment>().FirstOrDefault(fip => fip.ResetDate == asOf);
      if (pmt == null) //there is no rate reset on the pricing date
        return this;

      // For non-compounding coupon and custom schedule,
      // we just need to lock the coupons.
      var modified = GetModifiedRateResets(RateResets,
        ProjectAllFixings(asOf, ps, true), asOf);
      if (modified == null) return this;

      // We need return a modfied pricer.
      var pricer = (VarianceSwapPricer)ShallowCopy();
      pricer.RateResets = modified;
      return pricer;
    }

    private static RateResets GetModifiedRateResets(RateResets oldRateResets,
      IEnumerable<RateReset> projectedRates, Dt anchorDate)
    {
      var resets = new SortedDictionary<Dt, double>();

      if (oldRateResets != null && oldRateResets.HasAllResets)
      {
        foreach (var rr in oldRateResets.AllResets)
        {
          if (rr.Key >= anchorDate || resets.ContainsKey(rr.Key))
            continue;

          resets.Add(rr.Key, rr.Value);
        }
      }

      var origCount = resets.Count;
      foreach (var rr in projectedRates)
      {
        if (rr.Date > anchorDate)
          continue;

        if (resets.ContainsKey(rr.Date))
        {
          if (rr.Date == anchorDate)
          {
            resets[rr.Date] = rr.Rate;
          }

          continue;
        }

        resets.Add(rr.Date, rr.Rate);
      }
      var retVal = resets.Count == origCount ? null : new RateResets(resets);
      if (oldRateResets != null && oldRateResets.HasCurrentReset && retVal != null)
      {
        retVal.CurrentReset = oldRateResets.CurrentReset;
      }

      return retVal;
    }

    // Enumerate the projected fixings instead of the coupon rates.
    // With compounding, a single coupon may consists of several fixings.
    private static IEnumerable<RateReset> ProjectAllFixings(Dt asOf,
      PaymentSchedule ps, bool withSpread)
    {
      if (ps == null) yield break;
      foreach (var d in ps.GetPaymentDates())
      {
        foreach (var p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as VariancePayment;
          if (fip == null || (!fip.IsProjected && !RateResetUtil.ProjectMissingRateReset(fip.ResetDate, asOf, fip.PeriodStartDate))) continue;
          foreach (var rr in EnumerateProjectedFixings(fip, withSpread))
            yield return rr;
        }
      }
    }

    // Find all the rate fixings used in the FloatingInterestPayment.
    private static IEnumerable<RateReset> EnumerateProjectedFixings(
      VariancePayment fip, bool withSpread)
    {
      RateReset resets;
      var projector = fip.RateProjector;
      var schedule = fip.FixingSchedule;
      if (schedule.ObservationDates.Count > 1)
      {
        resets = Project(projector, schedule, withSpread ? fip.Spread : 0.0);
        if (resets != null) yield return resets;
      }
      resets = Project(projector, schedule, fip.Spread);
      if (resets != null) yield return resets;
    }

    // Find the projected rate for a single fixing.
    private static RateReset Project(IRateProjector projector, FixingSchedule fs, double spread)
    {
      var fixing = projector.Fixing(fs);
      return fixing == null || fixing.RateResetState != RateResetState.IsProjected
        ? null
        : new RateReset(fs.ResetDate, fixing.Forward + spread);
    }

    #endregion

  }
}
