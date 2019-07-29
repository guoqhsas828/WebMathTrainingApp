// 
//  -2013. All rights reserved.
// 
// Note: To be completed. Basic commodity analytics. RTD. Feb'13

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.Commodity">Commodity</see>.
  /// </summary>
  /// <remarks>
  /// <para><b>Commodities</b></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Commodity" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Commodity">Commodity Swap Leg</seealso>
  [Serializable]
  public class CommodityPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="commodity">Commodity to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="spotPrice">Spot commodity price</param>
    public CommodityPricer(
      Commodity commodity, Dt asOf, Dt settle, double spotPrice)
      : base(commodity, asOf, settle)
    {
      // Set data, using properties to include validation
      SpotPrice = spotPrice;
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
      if (SpotPrice <= 0.0)
        InvalidValue.AddError(errors, this, "SpotPrice", String.Format("Invalid price. Must be non negative, Not {0}", SpotPrice));
      if (LeaseRate < 0.0 || LeaseRate > 2.0)
        InvalidValue.AddError(errors, this, "LeaseRate", String.Format("Invalid lease rate {0}. Must be >= 0 and <= 2", LeaseRate));
    }

    #endregion

    #region Properties

    #region Market Data

    /// <summary>
    /// Current Price price of asset
    /// </summary>
    public double SpotPrice { get; set; }

    /// <summary>
    /// Risk free rate (5 percent = 0.05)
    /// </summary>
    /// <remarks>
    /// <para>Returns specified single risk free rate or if none set, interpolates from DiscountCurve.</para>
    /// </remarks>
    public double Rfr
    {
      get { return _rfr.HasValue ? _rfr.Value : DiscountCurve.R(Commodity.Maturity); }
      set { _rfr = value; }
    }

    /// <summary>
    ///   Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Dividend rate (5 percent = 0.05)
    /// </summary>
    public double LeaseRate { get; set; }

    #endregion Market Data

    /// <summary>
    /// Commodity product
    /// </summary>
    public Commodity Commodity
    {
      get { return (Commodity)Product; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Calculates present value of commodity dividends
    /// </summary>
    public override double ProductPv()
    {
      return SpotPrice * Notional;
    }

    /// <summary>
    /// Market value of commodity
    /// </summary>
    public double Value()
    {
      return SpotPrice * Notional;
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

    #region Data

    private double? _rfr;

    #endregion Data
  }
}
