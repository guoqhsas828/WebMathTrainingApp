// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;

using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using static BaseEntity.Toolkit.Pricers.AssetReturnLegPricerFactory;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  An abstract based class for asset return leg pricing
  /// </summary>
  [Serializable]
  public abstract class AssetReturnLegPricer : PricerBase, IAssetReturnLegPricer
  {
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetReturnLegPricer"/> class.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="settle">The settle.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurves">The reference curves.</param>
    /// <param name="assetPriceIndex">The asset price index prices.</param>
    protected AssetReturnLegPricer(
      IAssetReturnLeg product, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      IEnumerable<CalibratedCurve> referenceCurves,
      IAssetPriceIndex assetPriceIndex)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurves = referenceCurves?.Where(c => c != null).ToArray();
      AssetPriceIndex = assetPriceIndex;
    }

    #endregion

    #region Abstract methods

    /// <summary>
    /// Gets the payments of the underlying asset.
    /// </summary>
    /// <param name="begin">The begin.</param>
    /// <param name="end">The end.</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    public abstract IEnumerable<Payment> GetUnderlyerPayments(Dt begin, Dt end);

    /// <summary>
    /// Creates the calculator to find/project the prices of the underlying asset.
    /// </summary>
    /// <returns>IPriceCalculator</returns>
    protected abstract IPriceCalculator CreatePriceCalculator();

    #endregion

    #region Methods

    /// <summary>
    /// Calculate the net present value of the all the payments associated with this product
    /// </summary>
    /// <returns>The net present value</returns>
    public override double ProductPv()
    {
      var ps = GetPaymentSchedule(Settle);

      // Check hypothetical default
      var defaultSettle = AssetDefaultSettleDate;
      var includeSettleCf = !defaultSettle.IsEmpty()
        && defaultSettle == Settle
        && SurvivalCurve.Defaulted == Defaulted.WillDefault;

      return ps.GroupByCutoff().CalculatePv(
        AsOf, Settle, DiscountCurve,
        AssetDefaulted ? null : SurvivalCurve,
        includeSettleCf, true)*Notional;
    }

    /// <summary>
    ///  Calculate the unrealized capital gains/losses from the last valuation date
    ///  to the settle date when the later is in the middle of a price return period;
    ///  Otherwise, it returns 0.
    /// </summary>
    /// <returns>The unrealized capital gains or losses</returns>
    /// <remarks>
    ///  This is the capital gains/losses based on the current price on the settle date.
    ///  Since the later is not a payment date, the position is not closed and it has yet
    ///  to be cashed in.  Due to price changes, the gains/losses are not guaranteed to
    ///  get realized.  They may reverse on the next valuation date.
    /// </remarks>
    public double UnrealizedGain()
    {
      var settle = Settle;
      var defaultSettle = AssetDefaultSettleDate;
      if (!defaultSettle.IsEmpty() && defaultSettle <= Settle)
        return 0;

      var payment = (GetPaymentSchedule(null, settle) as IEnumerable<Payment>)
        .FirstOrDefault(p => p.GetUnderlyingPayment() is PriceReturnPayment);
      if (payment == null) return 0;


      double notional = Notional;
      PriceReturnPayment priceReturn;
      payment.GetNotionalAndUnderlyingPayment(ref notional, out priceReturn);
      if (priceReturn.BeginDate > settle || priceReturn.PayDt <= settle)
        return 0;

      var currentPrice = GetPriceCalculator().GetPrice(AsOf).Value;
      return PriceCalculatorUtility.CalculateReturn(
        priceReturn.BeginFixing.Value, currentPrice,
        priceReturn.IsAbsolute)*notional;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    /// <remarks><para>There are some pricers which need to remember some public state
    /// in order to skip redundant calculation steps. This method is provided
    /// to indicate that all public states should be cleared or updated.</para>
    /// <para>Derived Pricers may implement this and should call base.Reset()</para></remarks>
    public override void Reset()
    {
      _priceCalculator = null;
      base.Reset();
    }

    /// <summary>
    /// Gets the calculator to find/project the prices of the underlying asset.
    /// </summary>
    /// <returns>IPriceCalculator</returns>
    public IPriceCalculator GetPriceCalculator()
    {
      return _priceCalculator ?? (_priceCalculator = CreatePriceCalculator());
    }

    /// <summary>
    /// Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <param name="paymentSchedule">The payment schedule.</param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    /// <remarks>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</remarks>
    public override PaymentSchedule GetPaymentSchedule(
      PaymentSchedule paymentSchedule, Dt @from)
    {
      return GetPaymentSchedule(from);
    }

    /// <summary>
    /// Gets the payment schedule.
    /// </summary>
    /// <param name="fromDate">From the date the payments start</param>
    /// <returns>PaymentSchedule.</returns>
    public virtual PaymentSchedule GetPaymentSchedule(Dt fromDate)
    {
      var returnLeg = (IAssetReturnLeg) Product;

      // Get the price calculator and initial price
      var priceCalculator = GetPriceCalculator();

      // Get the underlying payments
      var underlierPayments = GetUnderlyerPayments(
        fromDate, returnLeg.Maturity).ToList();

      // Get the info re notional changes
      var underlierNotionalSchedule = underlierPayments
        .OfType<BalanceChangeAnnotation>().ToList();

      // Get the initial price
      var initialPrice = GetInitialPrice(returnLeg, priceCalculator);

      // Generate the price return payments
      var ps = new PaymentSchedule();
      ps.AddPayments(returnLeg.GetPriceReturnPayments(
        fromDate, priceCalculator, initialPrice,
        underlierNotionalSchedule, returnLeg.ResettingNotional,
        AssetDefaultSettleDate));

      // Add bond coupon payments based on the bond notional
      // corresponding to 1.0 TRS notional.
      ps.AddPayments(underlierPayments
        .Where(p => p is FloatingPrincipalExchange ||
          !(p is BalanceChangeAnnotation || p is PrincipalExchange ||
          p is RecoveryPayment))
        .Select(p => p.UpdateNotional(1.0/initialPrice)));

      // Add the recovery payments
      if (SurvivalCurve != null && SurvivalCurve.DefaultDate.IsEmpty())
      {
        ps.AddPayments(ps.GetRecoveryReturns(
          returnLeg.UnderlyingAsset.Maturity,
          underlierPayments.OfType<CreditContingentPayment>().GetTimeGrids(),
          SurvivalCurve.SurvivalCalibrator?.RecoveryCurve).ToList());
      }

      // This is the full schedule
      return ps;
    }

    /// <summary>
    /// Get the initial price of the asset return leg
    /// </summary>
    /// <param name="leg">The asset return leg</param>
    /// <param name="priceCalculator">The price calculator.</param>
    /// <returns><c>true</c> if the leg has initial price; otherwise, <c>false</c>.</returns>
    private static double GetInitialPrice(IAssetReturnLeg leg,
      IPriceCalculator priceCalculator)
    {
      return double.IsNaN(leg.InitialPrice)
        ? priceCalculator.GetPrice(leg.Effective).Value
        : leg.InitialPrice;
    }

    /// <summary>
    /// Gets the underlying asset payments when it defaults on the specified date.
    /// </summary>
    /// <param name="settle">The pricer settle date</param>
    /// <param name="ps">The full payment schedule without default</param>
    /// <param name="defaultDate">The default date</param>
    /// <param name="defaultNotional">The default notional</param>
    /// <param name="survivalCurve">The survival curve</param>
    /// <param name="ccy">Default settlement currency</param>
    /// <returns>PaymentSchedule</returns>
    public static PaymentSchedule GetPaymentsWithDefault(
      IEnumerable<Payment> ps, Dt settle,
      Dt defaultDate, double defaultNotional,
      SurvivalCurve survivalCurve, Currency ccy)
    {
      Debug.Assert(survivalCurve != null);
      Debug.Assert(!defaultDate.IsEmpty());

      double defaultSettlePrice = 0;
      Dt defaultSettleDate = Dt.Empty;
      var recoveryCurve = survivalCurve.SurvivalCalibrator?.RecoveryCurve;
      if (recoveryCurve != null)
      {
        defaultSettlePrice = recoveryCurve.Interpolate(defaultDate);
        defaultSettleDate = recoveryCurve.JumpDate;
      }
      if (defaultSettleDate.IsEmpty())
      {
        defaultSettleDate = defaultDate;
      }

      var payments = new PaymentSchedule();
      if (ps != null)
      {
        // Include all the scheduled payments before default 
        foreach (var p in ps)
        {
          if (p.PayDt < defaultDate)
            payments.AddPayment(p);
        }
      }

      // The default settlement payment
      payments.AddPayment(new DefaultSettlement(
        defaultDate, defaultSettleDate, ccy,
        defaultNotional, defaultSettlePrice)
      {
        IsFunded = true
      });
      return payments;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <value>The discount curve.</value>
    public DiscountCurve DiscountCurve { get; }

    /// <summary>
    /// Gets the reference curves.
    /// </summary>
    /// <value>The reference curves.</value>
    public CalibratedCurve[] ReferenceCurves { get; }

    /// <summary>
    /// Gets the asset price index.
    /// </summary>
    /// <value>The asset price index</value>
    public IAssetPriceIndex AssetPriceIndex { get; }

    /// <summary>
    /// Gets the historical prices.
    /// </summary>
    /// <value>The historical prices.</value>
    public RateResets HistoricalPrices => AssetPriceIndex?.HistoricalObservations;

    /// <summary>
    /// Gets the survival curve.
    /// </summary>
    /// <value>The survival curve</value>
    public SurvivalCurve SurvivalCurve => Get<SurvivalCurve>(ReferenceCurves);

    /// <summary>
    /// Gets the initial price of the underlying asset at the TRS effective
    /// </summary>
    /// <value>The initial price</value>
    public double InitialPrice
    {
      get
      {
        var price = ((IAssetReturnLeg)Product).InitialPrice;
        return double.IsNaN(price)
          ? GetPriceCalculator().GetPrice(Product.Effective).Value
          : price;
      }
    }

    public Dt AssetDefaultDate => SurvivalCurve?.DefaultDate ?? Dt.Empty;

    private Dt AssetDefaultSettleDate
    {
      get
      {
        var sc = SurvivalCurve;
        if (sc == null || sc.DefaultDate.IsEmpty()) return Dt.Empty;
        var rc = sc.SurvivalCalibrator?.RecoveryCurve;
        return (rc == null || rc.JumpDate.IsEmpty())
          ? sc.DefaultDate : rc.JumpDate;
      }
    }

    private bool AssetDefaulted
    {
      get
      {
        var sc = SurvivalCurve;
        return sc != null && !sc.DefaultDate.IsEmpty();
      }
    }

    /// <summary>
    /// Gets the asset return leg.
    /// </summary>
    /// <value>The asset return leg.</value>
    IAssetReturnLeg IAssetReturnLegPricer.AssetReturnLeg => (IAssetReturnLeg) Product;

    #endregion

    #region Data

    [NonSerialized, NoClone] private IPriceCalculator _priceCalculator;

    #endregion

    #region Nested type: BalanceChangeAnnotation

    /// <summary>
    /// Information about balance change on the cutoff date
    /// </summary>
    public class BalanceChangeAnnotation : PaymentAnnotation, INotionalChangeInfo
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="BalanceChangeAnnotation" /> class.
      /// </summary>
      /// <param name="paymentDate">The payment date.</param>
      /// <param name="beforeChangeBalance">The before change balance.</param>
      /// <param name="afterChangeBalance">The after change balance.</param>
      public BalanceChangeAnnotation(
        Dt paymentDate,
        double beforeChangeBalance,
        double afterChangeBalance)
      {
        PayDt = paymentDate;
        NotionalBeforeChange = beforeChangeBalance;
        NotionalAfterChange = afterChangeBalance;
      }

      /// <summary>
      /// Gets the underlying payment.
      /// </summary>
      /// <value>The underlying payment.</value>
      public double NotionalBeforeChange { get; }

      /// <summary>
      /// Gets the remaining principal balance after this payment.
      /// </summary>
      /// <value>The remaining balance.</value>
      public double NotionalAfterChange { get; }

      public Dt Date => PayDt;
    }

    #endregion
  }

}
