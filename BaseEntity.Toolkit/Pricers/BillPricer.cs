//
// BillPricer.cs
//  -2011. All rights reserved.
//
using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for a <see cref="BaseEntity.Toolkit.Products.Bill">Bill</see> and other discount
  /// security such as TBill, discount bill, commercial paper or bankers acceptance
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Bill" />
  /// <para><h2>Pricing</h2></para>
  /// <para>Standard money market calculations are suported, along with interest rate
  /// sensitivity.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Bill">Bill Product</seealso>
  [Serializable]
  public partial class BillPricer : PricerBase, IPricer, IRepoAssetPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor based on the product and market information
    /// </summary>
    /// <param name="product">The underlying product</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricer settlement date</param>
    /// <param name="quotingConvention">Quoted type</param>
    /// <param name="marketQuote">Quoted value</param>
    /// <param name="dayCount">Daycount convention of quoted value (if required)</param>
    /// <param name="discountCurve">Discount curve (optional, may be null)</param>
    /// <param name="survivalCurve">Survival curve (optional, may be null)</param>
    /// <param name="recoveryCurve">Recovery curve (optional, may be null)</param>
    /// <param name="notional">Notional (Face or Redemption) amount</param>
    public BillPricer(Bill product, Dt asOf, Dt settle, QuotingConvention quotingConvention, double marketQuote, DayCount dayCount,
      DiscountCurve discountCurve, SurvivalCurve survivalCurve, RecoveryCurve recoveryCurve, double notional)
      : base(product, asOf, settle)
    {
      QuotingConvention = quotingConvention;
      MarketQuote = marketQuote;
      DayCount = dayCount;
      DiscountCurve = discountCurve;
      SurvivalCurve = survivalCurve;
      RecoveryCurve = recoveryCurve;
      Notional = notional;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      return new BillPricer(Bill, AsOf, Settle, QuotingConvention, MarketQuote, DayCount,
        (DiscountCurve != null) ? (DiscountCurve)DiscountCurve.Clone() : null,
        (SurvivalCurve != null) ? (SurvivalCurve)SurvivalCurve.Clone() : null,
        (_recoveryCurve != null) ? (RecoveryCurve)_recoveryCurve.Clone() : null,
        Notional);
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (QuotingConvention != QuotingConvention.FullPrice &&
          QuotingConvention != QuotingConvention.FlatPrice &&
          QuotingConvention != QuotingConvention.Yield &&
          QuotingConvention != QuotingConvention.DiscountRate &&
          QuotingConvention != QuotingConvention.UseModelPrice)
        InvalidValue.AddError(errors, this, "QuotingConvention", String.Format("Unsupported quote {0}", QuotingConvention));

      if (QuotingConvention == QuotingConvention.UseModelPrice && DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      return;
    }

    ///<summary>
    /// Model Net present value of the product, excluding the value
    /// of any additional payment.
    ///</summary>
    ///<returns>Pv</returns>
    public override double ProductPv()
    {
      if (Settle >= Product.Maturity)
        return 0.0;
      if( DiscountCurve == null )
        throw new ArgumentException("DiscountCurve must be specified before model pv can be calculated");
      var ps = GetPaymentSchedule(null, AsOf);

      return ps.CalculatePv(AsOf, Settle, DiscountCurve,
               SurvivalCurve, null, 0.0, 0, TimeUnit.None,
               AdapterUtil.CreateFlags(false, false, false)) * Math.Sign(Notional);
    }

    /// <summary>
    ///   Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <param name="ps">Payment schedule</param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (from > Product.Maturity)
        return ps ?? new PaymentSchedule();
      if (ps == null)
        ps = new PaymentSchedule();
      ps.AddPayment(new PrincipalExchange(Product.Maturity, Math.Abs(Notional), Product.Ccy));
      return ps;
    }

    #endregion Methods

    #region Calculation Methods

    /// <summary>
    /// Price of Bill as a percentage of notional
    /// </summary>
    /// <remarks>
    ///   <para>The price is calculated as:</para>
    ///   <formula>
    ///     P = F - F * Y_d * \frac{t}{T_d}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">P</formula> is the price as a percentage of the notional (redemption) amount</description></item>
    ///			<item><description><formula inline="true">F</formula> is the face (redemption) value</description></item>
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Price of Bill</returns>
    public double Price()
    {
      if (_price == null)
      {
        if (QuotingConvention == QuotingConvention.FullPrice)
          // Full price
          _price = MarketQuote;
        else if (QuotingConvention == QuotingConvention.FlatPrice)
          // Flat price
          _price = MarketQuote;
        else if (QuotingConvention == QuotingConvention.Yield)
          // CD yield
          _price = RateCalc.PriceFromRate(MarketQuote, Settle, Bill.Maturity, DayCount, Frequency.None);
        else if (QuotingConvention == QuotingConvention.DiscountRate)
          // Discount rate
          _price = RateCalc.PriceFromDiscount(MarketQuote, Settle, Bill.Maturity, DayCount);
        else if (QuotingConvention == QuotingConvention.UseModelPrice)
          // Model price
          _price = Pv() / Notional;
        else
          throw new ToolkitException("Unsupported quote {0}", QuotingConvention);
      }
      return _price.Value;
    }

    /// <summary>
    /// Bill full price
    /// </summary>
    /// <returns></returns>
    public double FullPrice()
    {
      switch (QuotingConvention)
      {          
        case QuotingConvention.FlatPrice:
          return MarketQuote + Accrued();
        default:
          return Price();
      }
    }

    /// <summary>
    /// Discount rate
    /// </summary>
    /// <summary>
    /// Discount rate of bill
    /// </summary>
    /// <remarks>
    ///   <details>
    ///   <para>The discount rate is calculated as:</para>
    ///   <formula>
    ///     Y_d = (1 - \frac{P}{F}) * \frac{T_d}{t}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">P</formula> is the price</description></item>
    ///			<item><description><formula inline="true">F</formula> is the face (redemption) value</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///   </list>
    ///   </details>
    /// </remarks>
    /// <param name="dayCount">Daycount for discount rate</param>
    /// <returns>Discount rate of Bill</returns>
    public double DiscountRate(DayCount dayCount)
    {
      return RateCalc.DiscountFromPrice(Price(), Settle, Bill.Maturity, dayCount);
    }

    /// <summary>
    /// Discount as a percentage of notional
    /// </summary>
    /// <remarks>
    ///   <para>The Discount is calculated as:</para>
    ///   <formula>
    ///     D = F - P
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">D</formula> is the discount amount</description></item>
    ///			<item><description><formula inline="true">P</formula> is the price</description></item>
    ///			<item><description><formula inline="true">F</formula> is the face (redemption) value</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Price of Bill</returns>
    public double Discount()
    {
      return 1.0 - Price();
    }

    /// <summary>
    /// Money market (CD) equivalent yield 
    /// </summary>
    /// <remarks>
    ///   <para>The money market (CD) equivalent yield is calculated as:</para>
    ///   <formula>
    ///     Y_cd = \frac{T_cd*Y_d}{T_d - t * Y_d}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">Y_cd</formula> is the money market yield</description></item>
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///			<item><description><formula inline="true">T_cd</formula> is the number of days in the money market yield daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for money market yield</param>
    /// <returns>Money market yield</returns>
    public double MoneyMarketYield(DayCount dayCount)
    {
      return RateCalc.RateFromPrice(Price(), Settle, Bill.Maturity, dayCount, Frequency.None);
    }

    /// <summary>
    /// Bond equivalent yield (Treasury convention)
    /// </summary>
    /// <remarks>
    ///   <para>For bills with 182 days or less to maturity:</para>
    ///   <formula>
    ///     Y_be = \frac{T_d*Y_a}{T_a - t * Y_d}
    ///   </formula>
    ///   <para>For bills with more than 182 days to maturity:</para>
    ///   <formula>
    ///     Y_be = \frac{ \frac{-2*t}{T_a} + 2\left[ \left(\frac{t}{T_a}\right)^2 - \left(\frac{2*t}{T_a}-1\right)*\left(1-\frac{100}{P} \right) \right]^{1/2} }{ \frac{2*t}{T_a} - 1 }
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">Y_be</formula> is the bond equivalent yield</description></item>
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">P</formula> is the price</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///     <item><description><formula inline="true">T_a</formula> is the actual number of days in the calendar year</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Bond equivalent yield</returns>
    public double BondEquivalentYield()
    {
      return RateCalc.BondEquivalentYieldFromDiscount(DiscountRate(DayCount), Settle, Bill.Maturity, DayCount);
    }

    /// <summary>
    /// Bond (basis) yield
    /// </summary>
    /// <remarks>
    ///   <para>The bond SABB yield (Act/Act, SemiAnnual) is calculated as:</para>
    ///   <formula>
    ///     Y_bb = \left( \left( \frac{T_d}{T_d - t * Y_d} \right)^{182.5/t} - 1 \right) * 2
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">Y_bb</formula> is the bond basis yield</description></item>
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for bond yield</param>
    /// <param name="freq">Compounding for bond yield</param>
    /// <returns>Semi annual bond (basis) yield</returns>
    public double BondYield(DayCount dayCount, Frequency freq)
    {
      return RateCalc.RateFromPrice(Price(), Settle, Bill.Maturity, dayCount, freq);
    }

    /// <summary>
    /// Number of days from settlement to maturity
    /// </summary>
    /// <returns>Days to maturity</returns>
    public int DaysToMaturity()
    {
      return Dt.Diff(Settle, Bill.Maturity);
    }

    #region Sensitivities

    /// <summary>
    /// Change in value for a 1bp drop in discount rate per $1000 notional
    /// </summary>
    /// <remarks>
    ///   <para>The Pv01 is calculated as:</para>
    ///   <formula>
    ///     Pv01 = \frac{t}{T_d}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Pv01</formula> is the price change for a 1bp drop in the discount rate</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Pv01 per $1000</returns>
    public double Pv01()
    {
      return Dt.Fraction(Settle, Bill.Maturity, DayCount);
    }

    /// <summary>
    /// Change in value for a 1bp drop in SABB yield per $1000 notional
    /// </summary>
    /// <remarks>
    ///   <para>The bond basis Pv01 is calculated as:</para>
    ///   <formula>
    ///     Pv01_bb = (Y_d(Y_sabb+0.0001) - Y_d ) * 1000 * \frac{t}{T_d}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Pv01_bb</formula> is the price change for a 1bp drop in the bond basis yield</description></item>
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for bond yield</param>
    /// <param name="freq">Compounding for bond yield</param>
    /// <returns>Pv01 on a bond basis yield per $1000</returns>
    public double BondPv01(DayCount dayCount, Frequency freq)
    {
      var yield = RateCalc.RateFromPrice(Price(), Settle, Bill.Maturity, dayCount, freq);
      var pup = RateCalc.PriceFromRate(yield - 0.0001, Settle, Bill.Maturity, dayCount, freq);
      return (pup - Price()) * 10000.0;
    }

    /// <summary>
    /// Calculates duration as the weighted average time to cash flows
    /// </summary>
    /// <remarks>
    ///   <para>Duration is calculated as:</para>
    ///   <formula>
    ///     DUR = \frac{t}{T_a}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">DUR</formula> is the duration</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///     <item><description><formula inline="true">T_a</formula> is the actual number of days in the calendar year</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Duration</returns>
    public double Duration()
    {
      return Dt.Fraction(Settle, Bill.Maturity, DayCount.Actual365Fixed);
    }

    /// <summary>
    /// Calculates modified duration as the percentage price change for a 1bp drop in yield
    /// </summary>
    /// <remarks>
    ///   <para>The modified duration is calculated as:</para>
    ///   <formula>
    ///     DUR_mod = \frac{Pv01}{P}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">DUR_mod</formula> is the modified duration</description></item>
    ///			<item><description><formula inline="true">Pv01</formula> is the price change for a 1bp drop in the discount rate</description></item>
    ///     <item><description><formula inline="true">P</formula> is the price</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Modified duration</returns>
    public double ModDuration()
    {
      return Pv01() / Price();
    }

    /// <summary>
    /// Calculates modified duration as the percentage price change for a 1bp drop in yield
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The modified duration is calculated as:</para>
    ///   <formula>
    ///     DUR_mod = \frac{Pv01_bb}{P}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">DUR_mod</formula> is the modified duration</description></item>
    ///			<item><description><formula inline="true">Pv01_bb</formula> is the price change for a 1bp drop in the bond basis yield</description></item>
    ///     <item><description><formula inline="true">P</formula> is the price</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for bond yield</param>
    /// <param name="freq">Compounding for bond yield</param>
    /// <returns>Modified duration on a bond basis yield</returns>
    public double BondModDuration(DayCount dayCount, Frequency freq)
    {
      return BondPv01(dayCount, freq) / Price();
    }

    #endregion Sensitivities

    #region IRepoSecurityPricer

    /// <summary>
    ///  Security value method for bond repos
    /// </summary>
    /// <returns></returns>
    public double SecurityMarketValue()
    {
      return FullPrice();
    }

    #endregion

    #endregion Calculation Methods

    #region Properties

    /// <summary>
    /// Bill product
    /// </summary>
    public Bill Bill
    {
      get { return (Bill)Product; }
    }

    /// <summary>
    ///   Market quote
    /// </summary>
    /// <details>
    ///   <para>A variety of quoting types are supported
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is Yield.</para>
    /// </details>
    public double MarketQuote { get; private set; }

    /// <summary>
    ///   Quoting convention for market quote
    /// </summary>
    public QuotingConvention QuotingConvention { get; private set; }

    /// <summary>
    /// Daycount of market quote (if required)
    /// </summary>
    public DayCount DayCount { get; set; }

    ///<summary>
    /// Discount curve
    ///</summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; private set; }

    /// <summary>
    /// Recovery curve
    /// </summary>
    /// <remarks>
    ///   <para>If a separate recovery curve has not been specified, the recovery from the survival
    ///   curve is used. In this case the survival curve must have a Calibrator which provides a
    ///   recovery curve otherwise an exception will be thrown.</para>
    /// </remarks>
    public RecoveryCurve RecoveryCurve
    {
      get
      {
        if (_recoveryCurve != null)
          return _recoveryCurve;
        else if (SurvivalCurve != null && SurvivalCurve.SurvivalCalibrator != null)
          return SurvivalCurve.SurvivalCalibrator.RecoveryCurve;
        else
          return null;
      }
      private set { _recoveryCurve = value; }
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

    #endregion Properties

    #region Data

    private RecoveryCurve _recoveryCurve;
    private double? _price;

    #endregion Data

  }
}
