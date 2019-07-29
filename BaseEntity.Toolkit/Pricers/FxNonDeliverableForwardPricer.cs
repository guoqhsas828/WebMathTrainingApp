/*
 *  -2012. All rights reserved.
 */
using System;
using System.Linq;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for FxNonDeliverableForward product.
  /// <para>Price a <see cref="BaseEntity.Toolkit.Products.FxNonDeliverableForward">Fx ND Forward</see> using a fx
  ///   forward curve and interest rate discount curve.</para>
  /// </summary>
  /// <seealso cref="BaseEntity.Toolkit.Products.FxNonDeliverableForward">Fx ND Forward Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Curves.FxCurve">Fx Curve</seealso>
  [Serializable]
  public class FxNonDeliverableForwardPricer : PricerBase, IPricer, ILockedRatesPricerProvider
  {
    private readonly static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(FxNonDeliverableForwardPricer));
    #region FxNonDeliverableForwardPayment
    
    /// <summary>
    /// Payment
    /// </summary>
    [Serializable]
    private class FxNonDeliverableForwardPayment : OneTimePayment
    {
      #region Data

      private readonly double contractedRate_;
      private readonly Dt fixingDt_;
      private readonly FxCurve ccyFxCurve_;
      private readonly FxCurve payCcyFxCurve_;
      private readonly Currency ccy_;
      private readonly Currency payCcy_;
      private readonly Currency deliveryCcy_;
      private readonly Currency valuationCcy_;

      #endregion

      #region Constructor

      public FxNonDeliverableForwardPayment(Dt payDt, Dt fixingDt, double contractedRate, Currency ccy, Currency payCcy, Currency deliveryCcy, Currency valuationCcy, FxCurve ccyFxCurve,
        FxCurve payCcyFxCurve)
        : base(payDt, deliveryCcy)
      {
        fixingDt_ = fixingDt;
        contractedRate_ = contractedRate;
        ccy_ = ccy;
        payCcy_ = payCcy;
        deliveryCcy_ = deliveryCcy;
        valuationCcy_ = valuationCcy;
        ccyFxCurve_ = ccyFxCurve;
        payCcyFxCurve_ = payCcyFxCurve;
      }

      #endregion

      protected override double ComputeAmount()
      {
        double targetCcyToValuationFxRate = (ccyFxCurve_ == null || ccy_ == valuationCcy_)
                                              ? 1.0
                                              : ccyFxCurve_.FxRate(fixingDt_, ccy_, valuationCcy_);
        double deliveryCcyToValuationFxRate = (payCcyFxCurve_ == null || payCcy_ == valuationCcy_)
                                                ? 1.0
                                                : payCcyFxCurve_.FxRate(fixingDt_, payCcy_, valuationCcy_);
        double fixingRate = targetCcyToValuationFxRate / deliveryCcyToValuationFxRate;
        return deliveryCcy_ == ccy_ ?  1.0 -  contractedRate_ /fixingRate : fixingRate / contractedRate_ - 1.0;
      }

      /// <summary>
      /// This is required to be true for ccr simulation to pick up
      /// </summary>
      public override bool IsProjected
      {
        get { return true; }
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    public FxNonDeliverableForwardPricer()
      : base(null)
    {}

    /// <summary>
    /// Construct a pricer for an fx non-deliverable foward
    /// </summary>
    ///
    /// <param name="fwd">Fx Non-Deliverable Forward</param>
    /// <param name="asOf">Pricing (asOf) date</param>
    /// <param name="settle">Spot settlement date</param>
    /// <param name="notional">Notional (amount received in receive currency)</param>
    /// <param name="valuationCcy">Currency to value trade in (or none for fx pay currency)</param>
    /// <param name="discountCurve">Discount curve for valuation currency (or null to take from fx curve calibration)</param>
    /// <param name="receiveCcyFxCurve">Fx curve translating fwd.Ccy (receive Ccy) to valuationCcy (ignored if valuation ccy fx receive currency)</param>
    /// <param name="payCcyFxCurve">Fx curve translating fwd.PayCcy to valuationCcy (ignored if valuation ccy fx pay currency)</param>
    public FxNonDeliverableForwardPricer(FxNonDeliverableForward fwd, Dt asOf, Dt settle, double notional, Currency valuationCcy, 
                                          DiscountCurve discountCurve, FxCurve receiveCcyFxCurve, FxCurve payCcyFxCurve)
      : base(fwd, asOf, settle)
    {
      Notional = notional;
      valuationCurrency_ = (valuationCcy != Currency.None) ? valuationCcy : receiveCcyFxCurve.Ccy1;
      DiscountCurve = discountCurve ?? FxUtil.DiscountCurve(valuationCcy, receiveCcyFxCurve, payCcyFxCurve);
      ReceiveCcyFxCurve = (valuationCcy != fwd.Ccy) ? receiveCcyFxCurve : null;
      PayCcyFxCurve = (valuationCcy != fwd.PayCcy) ? payCcyFxCurve : null;
    }

    /// <summary>
    /// Shallow copy 
    /// </summary>
    /// <returns>A new NDF pricer object.</returns>
    public override object Clone()
    {
      return new FxNonDeliverableForwardPricer(FxNDF, AsOf, Settle, Notional, ValuationCurrency, DiscountCurve,
                                               ReceiveCcyFxCurve, PayCcyFxCurve) { FixedSelltoBuyExchangeRate = FixedSelltoBuyExchangeRate };
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate pricer inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      else if(DiscountCurve.Ccy != FxNDF.DeliveryCcy)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("DiscountCurve for DeliveryCurrency expected"));

      if (FixedSelltoBuyExchangeRate <= 0)
      {
        if (FxNDF.Ccy != ValuationCurrency && ReceiveCcyFxCurve == null)
          InvalidValue.AddError(errors, this, "ReceiveCcyFxCurve", String.Format("Fx curve between [{0}]/[{1}] is required, cannot be null", FxNDF.Ccy, ValuationCurrency));

        if (FxNDF.PayCcy != ValuationCurrency && PayCcyFxCurve == null)
          InvalidValue.AddError(errors, this, "ReceiveCcyFxCurve", String.Format("Fx curve between [{0}]/[{1}] is required, cannot be null", FxNDF.PayCcy, ValuationCurrency));
      }
    }

    /// <summary>
    /// Get the payment schedule for valuation (PV) calculation: a detailed representation of payments
    /// <param name="ps"></param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>payments generated by the product</returns>
    /// <remarks>Payments generated based on a purchase of 1 unit of delivery notional</remarks>
    /// </summary>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt @from)
    {
      var pmtSchedule = ps ?? new PaymentSchedule();
      double fixingFxRate = FixedSelltoBuyExchangeRate;
      if (FxNDF.ValueDate > from)
      {
        if (!RequiresFxRateFixed)
        {
          var p = new FxNonDeliverableForwardPayment(FxNDF.ValueDate, FxNDF.FixingDate, FxNDF.FxRate,
                                                     FxNDF.Ccy, FxNDF.PayCcy,
                                                     FxNDF.DeliveryCcy, ValuationCurrency,
                                                     ReceiveCcyFxCurve, PayCcyFxCurve);
          pmtSchedule.AddPayment(p);
        }
        else
        {
          // netted (pay -ve, receive +ve)
          pmtSchedule.AddPayment(new BasicPayment(FxNDF.ValueDate,
                                                  FxNDF.DeliveryCcy == FxNDF.Ccy
                                                    ? (1.0 - FxNDF.FxRate/fixingFxRate)
                                                    : (fixingFxRate/FxNDF.FxRate - 1.0), FxNDF.DeliveryCcy));
        }
      }
      return pmtSchedule;
    }

    /// <summary>
    /// Present value in valuation currency
    /// </summary>
    public override double ProductPv()
    {
      double pv = 0.0;
      foreach (Payment p in GetPaymentSchedule(null, Settle))
      {
        pv += p.DomesticAmount * DiscountCurve.DiscountFactor(p.PayDt);
      }
      return pv * Notional;
    }

    /// <summary>
    /// Return spot fx rate
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency (default is receive ccy)</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency (default is pay ccy)</param>
    /// <returns>Spot fx rate</returns>
    public double SpotFxRate(Currency ccy1, Currency ccy2)
    {
      return FxUtil.SpotFxRate(ccy1, ccy2, PayCcyFxCurve, ReceiveCcyFxCurve);
    }

    /// <summary>
    /// Return forward fx rate using standard quoting convention on ValueDate
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency (default is receive ccy)</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency (default is pay ccy)</param>
    /// <returns>Forward fx rate at ValueDate</returns>
    public double ForwardFxRate(Currency ccy1, Currency ccy2)
    {
      return FxUtil.ForwardFxRate(FxNDF.ValueDate, ccy1, ccy2, PayCcyFxCurve, ReceiveCcyFxCurve);
    }

    /// <summary>
    /// Return forward fx points using standard quoting convention on ValueDate
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency (default is receive ccy)</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency (default is pay ccy)</param>
    /// <returns>Forward fx points at ValueDate</returns>
    public double ForwardFxPoints(Currency ccy1, Currency ccy2)
    {
      return (ForwardFxRate(ccy1, ccy2) - SpotFxRate(ccy1, ccy2)) * 10000.0;
    }

    /// <summary>
    /// Amount paid in pay currency
    /// </summary>
    /// <returns>Amount paid in pay currency</returns>
    public double PayAmount()
    {
      return Notional * FxNDF.FxRate;
    }

    /// <summary>
    /// Amount received in receive currency
    /// </summary>
    /// <returns>Amount received in receive currency</returns>
    public double ReceiveAmount()
    {
      return Notional;
    }

    /// <summary>
    /// Valuation currency discount factor from ValueDate to pricing date
    /// </summary>
    /// <returns>Valuation currency discount factor from ValueDate</returns>
    public double DiscountFactor()
    {
      return DiscountCurve.DiscountFactor(FxNDF.ValueDate);
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
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    /// <summary>
    ///   FX NDF Product
    /// </summary>
    public FxNonDeliverableForward FxNDF
    {
      get { return (FxNonDeliverableForward)Product; }
    }

    /// <summary>
    /// Accessor for the discount curve. 
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Currency of the Pv calculation
    /// </summary>
    public override Currency ValuationCurrency
    {
      get { return valuationCurrency_; }
    }

    /// <summary>
    /// FxCurve from pay currency to valuation currency (or null if pay currency is valuation currency)
    /// </summary>
    public FxCurve PayCcyFxCurve { get; set;}

    /// <summary>
    /// FxCurve from receive currency to valuation currency (or null if receive currency is valuation currency)
    /// </summary>
    public FxCurve ReceiveCcyFxCurve { get; set; }

    ///<summary>
    /// The fixing Fx rate
    ///</summary>
    public double FixedSelltoBuyExchangeRate { get; set; }

    ///<summary>
    /// Flag to indicate whether the Fx Rate between buy/sell sides has been fixed
    ///</summary>
    public bool RequiresFxRateFixed
    {
      get { return FxNDF != null && AsOf >= FxNDF.FixingDate; }
    }

    #endregion Properties

    #region PropertyGetter Properties

    /// <summary>
    /// Return array of FxCurves used by this pricer
    /// </summary>
    public FxCurve[] FxCurves
    {
      get
      {
        return new[] {ReceiveCcyFxCurve, PayCcyFxCurve}.Where(f => f != null).ToArray();
      }
    }

    /// <summary>
    /// Accessor for the fx basis curve. This is only used for internal purposes to compute sensitivities
    /// </summary>
    public CalibratedCurve[] BasisAdjustments
    {
      get
      {
        return (from fx in new[] { ReceiveCcyFxCurve, PayCcyFxCurve } where fx != null where !fx.IsSupplied select fx.BasisCurve).Cast<CalibratedCurve>().ToArray();
      }
    }

    #endregion

    #region ILockedRatesPricerProvider

    IPricer ILockedRatesPricerProvider.LockRatesAt(Dt fixingDt)
    {
      if ((FxNDF == null || fixingDt < FxNDF.FixingDate) || FixedSelltoBuyExchangeRate > 0)
        return this;

      var copiedPricer = (FxNonDeliverableForwardPricer)this.ShallowCopy();
      double targetCcyToValuationFxRate = (ReceiveCcyFxCurve == null || FxNDF.Ccy == ValuationCurrency)
                                              ? 1.0
                                              : ReceiveCcyFxCurve.FxRate(fixingDt, FxNDF.Ccy, ValuationCurrency);
      double deliveryCcyToValuationFxRate = (PayCcyFxCurve == null || FxNDF.PayCcy == ValuationCurrency)
                                              ? 1.0
                                              : PayCcyFxCurve.FxRate(fixingDt, FxNDF.PayCcy, ValuationCurrency);
      double fixingRate = targetCcyToValuationFxRate / deliveryCcyToValuationFxRate;
      copiedPricer.FixedSelltoBuyExchangeRate = fixingRate;
      return copiedPricer;
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      if ((FxNDF == null || asOf != FxNDF.FixingDate) || (ReceiveCcyFxCurve != null || PayCcyFxCurve != null))
        return null;
      else
      {
        var otherNdfPricer = (FxNonDeliverableForwardPricer)otherPricer;
        var copiedPricer = (FxNonDeliverableForwardPricer)this.ShallowCopy();
        double targetCcyToValuationFxRate = (otherNdfPricer.ReceiveCcyFxCurve == null || FxNDF.Ccy == ValuationCurrency)
                                                ? 1.0
                                                : otherNdfPricer.ReceiveCcyFxCurve.FxRate(asOf, FxNDF.Ccy, ValuationCurrency);
        double deliveryCcyToValuationFxRate = (otherNdfPricer.PayCcyFxCurve == null || FxNDF.PayCcy == ValuationCurrency)
                                                ? 1.0
                                                : otherNdfPricer.PayCcyFxCurve.FxRate(asOf, FxNDF.PayCcy, ValuationCurrency);
        double fixingRate = targetCcyToValuationFxRate / deliveryCcyToValuationFxRate;
        copiedPricer.FixedSelltoBuyExchangeRate = fixingRate;
        return copiedPricer;
      }
    }

    #endregion

    #region Data

    private readonly Currency valuationCurrency_;

    #endregion
  }
}
