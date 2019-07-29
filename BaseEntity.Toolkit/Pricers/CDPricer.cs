// 
//  -2012. All rights reserved.
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
  /// Pricer for a <see cref="BaseEntity.Toolkit.Products.CD">CD</see> and other short
  /// term securities that pay interest at maturity such as Certificates of Deposit,
  /// Fed Funds, Repos.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CD" />
  /// <para><h2>Pricing</h2></para>
  /// <para>Standard money market calculations are suported, along with interest rate
  /// sensitivity.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CD">CD Product</seealso>
  [Serializable]
  public partial class CDPricer : PricerBase, IPricer
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
    /// <param name="discountCurve">Discount curve (optional, may be null)</param>
    /// <param name="survivalCurve">Survival curve (optional, may be null)</param>
    /// <param name="recoveryCurve">Recovery curve (optional, may be null)</param>
    /// <param name="notional">Notional or Face amount</param>
    public CDPricer(CD product, Dt asOf, Dt settle, QuotingConvention quotingConvention, double marketQuote,
                    DiscountCurve discountCurve, SurvivalCurve survivalCurve, RecoveryCurve recoveryCurve, double notional)
      : base(product, asOf, settle)
    {
      QuotingConvention = quotingConvention;
      MarketQuote = marketQuote;
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
      return new CDPricer(CD, AsOf, Settle, QuotingConvention, MarketQuote,
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
        InvalidValue.AddError(errors, this, "QuotingConvention", String.Format("Unsupported CD quote {0}", QuotingConvention));

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
      if (DiscountCurve == null)
        throw new ArgumentException("DiscountCurve must be specified before model pv can be calculated");
      var ps = GetPaymentSchedule(null, AsOf);
      return ps.CalculatePv(AsOf, Settle, DiscountCurve, SurvivalCurve, null, 0.0,
               0, TimeUnit.None, AdapterUtil.CreateFlags(false, false, false)) * Math.Sign(Notional);
    }

    /// <summary>
    ///   Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <param name="ps">Payment schedule</param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (from > Product.Maturity)
        return ps ?? new PaymentSchedule();
      if (ps == null)
        ps = new PaymentSchedule();
#if ONE_DAY // Not obvious how to use. Rivisit once cashflows settled down
      ps.AddPayment(new FixedInterestPayment(CD.Effective, CD.Maturity, CD.Ccy, CD.Effective, CD.Maturity, CD.Effective, CD.Effective, Notional, CD.Coupon, CD.DayCount, Frequency.None ));
      ps.AddPayment(new PrincipalExchange(Product.Maturity, Notional, Product.Ccy));
#else
      ps.AddPayment(new PrincipalExchange(Product.Maturity, Math.Abs(Notional) * (1.0 + Interest()), Product.Ccy));
#endif
      return ps;
    }

    #endregion Methods

    #region Calculation Methods

    /// <summary>
    /// Flat price of CD as a percentage of notional
    /// </summary>
    /// <remarks>
    ///   <para>The flat price is calculated as:</para>
    ///   <formula>P=\frac{1}{1+ Y_{cd}\frac{t}{T_{cd}}}</formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">P</formula> is the flat price</description></item>
    ///     <item><description><formula inline="true">Y_cd</formula> is the simple CD yield</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_cd</formula> is the number of days in the yield daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Flat price of CD</returns>
    public double FlatPrice()
    {
      if (_flatPrice == null)
      {
        if (QuotingConvention == QuotingConvention.FullPrice)
          // Full price
          _flatPrice = MarketQuote / (1.0 + Interest());
        else if (QuotingConvention == QuotingConvention.FlatPrice)
          // Flat price
          _flatPrice = MarketQuote;
        else if (QuotingConvention == QuotingConvention.Yield)
          // CD yield
          _flatPrice = RateCalc.PriceFromRate(MarketQuote, Settle, CD.Maturity, CD.DayCount, Frequency.None);
        else if (QuotingConvention == QuotingConvention.DiscountRate)
          // Discount rate
          _flatPrice = RateCalc.PriceFromDiscount(MarketQuote, Settle, CD.Maturity, CD.DayCount);
        else if (QuotingConvention == QuotingConvention.UseModelPrice)
          // Model price
          _flatPrice = Pv() / (Notional*(1.0 + Interest()));
        else
          throw new ToolkitException("Unsupported CD quote {0}", QuotingConvention);
      }
      return _flatPrice.Value;
    }

    /// <summary>
    /// CD yield
    /// </summary>
    /// <remarks>
    ///   <para>The CD yield is calculated by inverting:</para>
    ///   <formula>
    ///     P = \frac{1 + C_{cd} \frac{t_i}{T_{cd}}}{1 + Y_{cd} \frac{t}{T_{cd}}}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">P</formula> is the price</description></item>
    ///     <item><description><formula inline="true">C_cd</formula> is the CD coupon</description></item>
    ///			<item><description><formula inline="true">t_i</formula> is the number of days from issue (effective) to maturity</description></item>
    ///			<item><description><formula inline="true">T_cd</formula> is the number of days in the coupon daycount period (eg 360)</description></item>
    ///     <item><description><formula inline="true">Y_cd</formula> is the simple CD yield</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_dc</formula> is the number of days in the daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for discount rate</param>
    /// <returns>CD Yield</returns>
    public double MoneyMarketYield(DayCount dayCount)
    {
      return RateCalc.RateFromPrice(FlatPrice(), Settle, CD.Maturity, dayCount, Frequency.None);
    }

    /// <summary>
    /// Equivalent discount rate
    /// </summary>
    /// <remarks>
    ///   <para>The discount rate is calculated as:</para>
    ///   <formula>
    ///     Y_d = \frac{T_d*Y_cd}{T_cd - t * Y_cd}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///     <item><description><formula inline="true">Y_cd</formula> is the money market yield</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///			<item><description><formula inline="true">T_cd</formula> is the number of days in the money market yield daycount period (eg 360)</description></item>
    ///    </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for discount rate</param>
    /// <returns>Money market discount rate</returns>
    public double DiscountRate(DayCount dayCount)
    {
      return RateCalc.DiscountFromPrice(FlatPrice(), Settle, CD.Maturity, dayCount);
    }

    /// <summary>
    /// Coupon payment at maturity as a percentage of notional
    /// </summary>
    /// <remarks>
    ///   <para>The coupon payment is calculated as:</para>
    ///   <formula>
    ///     Payment = C_cd * \frac{t_i}{T_cd}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">Payment</formula> is the interest payment at maturity</description></item>
    ///     <item><description><formula inline="true">C_cd</formula> is the anualised CD coupon</description></item>
    ///			<item><description><formula inline="true">t_i</formula> is the number of days from issue (effective) to maturity</description></item>
    ///			<item><description><formula inline="true">T_cd</formula> is the number of days in the coupon daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Coupon payment</returns>
    public double Interest()
    {
      return CD.Coupon * Dt.Fraction(CD.Effective, CD.Maturity, CD.DayCount);
    }

    /// <summary>
    /// Price of CD as a percentage of notional
    /// </summary>
    /// <remarks>
    ///   <para>The price is calculated as:</para>
    ///   <formula>
    ///     P = \frac{1 + C_{cd} \frac{t_i}{T_{cd}}}{1 + Y_{cd} \frac{t}{T_{cd}}}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">P</formula> is the price</description></item>
    ///     <item><description><formula inline="true">C_cd</formula> is the CD coupon</description></item>
    ///     <item><description><formula inline="true">Y_cd</formula> is the simple CD yield</description></item>
    ///			<item><description><formula inline="true">t_i</formula> is the number of days from issue (effective) to maturity</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///			<item><description><formula inline="true">T_cd</formula> is the number of days in the yield daycount period (eg 360)</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Price of CD</returns>
    public double Price()
    {
      return (1.0 + Interest()) * FlatPrice();
    }

    /// <summary>
    /// Bond Basis yield
    /// </summary>
    /// <remarks>
    ///   <para>The Bond basis yield is calculated as:</para>
    ///   <formula>
    ///     Y_bb = \left( \left( 1 + Y_cd * \frac{t}{T_cd} \right)^{182.5/t} - 1 \right) * 2
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">Y_bb</formula> is the semi annual bond basis yield</description></item>
    ///     <item><description><formula inline="true">Y_d</formula> is the annualized yield on a bank discount basis</description></item>
    ///			<item><description><formula inline="true">T_d</formula> is the number of days in the discount rate daycount period (eg 360)</description></item>
    ///			<item><description><formula inline="true">t</formula> is the number of days from settlement to maturity</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="dayCount">Daycount for bond yield</param>
    /// <param name="freq">Compounding frequency for bond yield</param>
    /// <returns>Semi annual bond basis yield</returns>
    public double BondYield(DayCount dayCount, Frequency freq)
    {
      return RateCalc.RateFromPrice(FlatPrice(), Settle, CD.Maturity, dayCount, freq);
    }

    /// <summary>
    /// Number of days from settlement to maturity
    /// </summary>
    /// <returns>Days to maturity</returns>
    public int DaysToMaturity()
    {
      return Dt.Diff(Settle, CD.Maturity);
    }

    #region Sensitivities

    /// <summary>
    /// Change in value for a 1bp drop in the CD rate per $1000 notional
    /// </summary>
    /// <returns>Pv01 per $1000</returns>
    public double Pv01()
    {
      double up = (1.0 + Interest()) * RateCalc.PriceFromRate(MoneyMarketYield(CD.DayCount) - 0.0001, Settle, CD.Maturity, CD.DayCount, Frequency.None);
      return (up - Price()) * 10000.0;
    }

    /// <summary>
    /// Change in value for a 1bp drop in bond basis yield per $1000 notional
    /// </summary>
    /// <returns>Pv01 on a bond basis yield per $1000</returns>
    public double BondPv01()
    {
      double sabby = BondYield(DayCount.Actual365Fixed, Frequency.SemiAnnual);
      double up = (1.0 + Interest()) * RateCalc.PriceFromRate(sabby - 0.0001, Settle, CD.Maturity, DayCount.Actual365Fixed, Frequency.SemiAnnual);
      return (up - Price()) * 10000.0;
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
      return Dt.Fraction(Settle, CD.Maturity, DayCount.Actual365Fixed);
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
    /// <returns>Modified duration on a SABB yield</returns>
    public double BondModDuration()
    {
      return BondPv01() / Price();
    }

    #endregion Sensitivities

    #endregion Calculation Methods

    #region Properties

    /// <summary>
    /// CD product
    /// </summary>
    public CD CD
    {
      get { return (CD)Product; }
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
    /// Daycount of quoted discount rate
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
    private double? _flatPrice;

    #endregion Data
  }
}
