// 
//  -2012. All rights reserved.
// 
// Note: To be completed. Dividend analysis, IRR, other basic stock analytics. RTD. 9Aug12

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.Stock">Stock</see>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Stock" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Stock"/>
  [Serializable]
  public class StockPricer : PricerBase, IPricer, IRepoAssetPricer
  {
    #region Constructors

    /// <summary>
    /// Construct instance with stock market price
    /// </summary>
    /// <param name="stock">Stock to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrice">Underlying stock price</param>
    protected StockPricer(
      Stock stock, Dt asOf, Dt settle, double stockPrice)
      : base(stock, asOf, settle)
    {
      DiscountCurve = new DiscountCurve(asOf, 0.0);
      StockCurve = new StockCurve(asOf, stockPrice, DiscountCurve, 0.0, null);
    }

    /// <summary>
    /// Construct instance with stock curve elements
    /// </summary>
    /// <param name="stock">Stock to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrice">Underlying stock price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="dividend">%Dividend rate (continuous comp, eg. 0.05)</param>
    /// <param name="divs">Schedule of discrete dividends</param>
    public StockPricer(
      Stock stock, Dt asOf, Dt settle, double stockPrice,
      DiscountCurve discountCurve, double dividend, DividendSchedule divs)
      : base(stock, asOf, settle)
    {
      DiscountCurve = discountCurve;
      StockCurve = new StockCurve(asOf, stockPrice, DiscountCurve, dividend, stock);
    }

    /// <summary>
    /// Construct instance using specified stock curve
    /// </summary>
    /// <param name="stock">Stock to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockCurve">Calibrated stock curve </param>
    public StockPricer(
      Stock stock, Dt asOf, Dt settle, StockCurve stockCurve)
      : base(stock, asOf, settle)
    {
      DiscountCurve = stockCurve == null ? null : stockCurve.DiscountCurve;
      StockCurve = stockCurve;
    }

    #endregion Constructors

    #region Utility Methods

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if (StockCurve == null)
        InvalidValue.AddError(errors, this, "StockCurve", "Missing StockCurve - must be specified");
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing DiscountCurve - must be specified");
    }

    #endregion

    #region Properties

    #region Market Data

    /// <summary>
    /// Current quoted price of underlying stock
    /// </summary>
    public double StockPrice
    {
      get { return StockCurve.SpotPrice; }
    }

    /// <summary>
    /// Forward curve of underlying asset
    /// </summary>
    /// <remarks>
    /// <para>Either the underlying spot price and the dividend or the forward curve can be specified.</para>
    /// </remarks>
    public StockCurve StockCurve { get; set; }

    /// <summary>
    /// Risk free rate (5 percent = 0.05)
    /// </summary>
    public double Rfr
    {
      get { return DiscountCurve.R(Stock.Maturity); }
    }

    /// <summary>
    ///   Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    #endregion Market Data

    /// <summary>
    /// Stock product
    /// </summary>
    public Stock Stock
    {
      get { return (Stock)Product; }
    }

    /// <summary>
    /// Trade date
    /// </summary>
    public Dt Traded { get; set; }

    #region IPricer

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
    /// Generate a payment schedule which includes the current stock price, plus pending 
    /// dividends the trade is entitled that passed the ex-div dates but has not reaching payment date yet
    /// </summary>
    /// <param name="ps">Original payment schedule</param>
    /// <param name="from">From date</param>
    /// <returns>Payment schedule</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt @from)
    {
      if (ps == null)
      {
        ps = new PaymentSchedule();
      }
      else
      {
        ps.Clear();
      }

      foreach (var dividend in Stock.DeclaredDividends)
      {
        if (from < dividend.Item1 && AsOf >= dividend.Item1 && dividend.Item2 > Settle && dividend.Item3 == DividendSchedule.DividendType.Fixed)
        {
          ps.AddPayment(new BasicPayment(dividend.Item2, dividend.Item4, Stock.Ccy));
        }
      }
      return ps;
    }

    #endregion IPricer

    #endregion Properties

    #region Methods

    /// <summary>
    /// Calculates present value of stock holding, which is determined by the price level and the dividends went ex-div but not paid yet
    /// </summary>
    public override double ProductPv()
    {
      var ps = GetPaymentSchedule(null, Traded.IsEmpty() ? AsOf : Traded);
      return (ps.Pv(AsOf, AsOf, DiscountCurve, null, true, false) + StockPrice) * Notional;
    }

    /// <summary>
    /// Market value of stock
    /// </summary>
    public double Value()
    {
      return StockPrice * Notional;
    }

    /// <summary>
    /// Delta
    /// </summary>
    public double Delta()
    {
      return 1.0 * Notional;
    }

    /// <summary>
    /// Theta
    /// </summary>
    public double Theta()
    {
      return 0.0;
    }

    /// <summary>
    /// Rho
    /// </summary>
    public double Rho()
    {
      return 0.0;
    }

    #endregion Methods

    #region IRepoSecurityPricer methods

    /// <summary>
    ///  Security value method for bond repos
    /// </summary>
    /// <returns></returns>
    public double SecurityMarketValue()
    {
      return StockPrice;
    }

    #endregion

  }
}
