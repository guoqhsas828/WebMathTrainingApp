// 
//  -2017. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Commodity fixed payment
  /// </summary>
  [Serializable]
  public class CommodityFixedPricePayment : CommodityPricePayment
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CommodityFixedPricePayment" /> class.
    /// </summary>
    /// <param name="payDate">The pay date.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="periodStart">The period start.</param>
    /// <param name="periodEnd">The period end.</param>
    /// <param name="notional">The notional quantity.</param>
    /// <param name="price">The price.</param>
    public CommodityFixedPricePayment(Dt payDate,
                                      Currency ccy,
                                      Dt periodStart,
                                      Dt periodEnd,
                                      double notional,
                                      double price
      )
      : base(payDate, ccy, periodStart, periodEnd, notional)
    {
      FixedPrice = price;
    }

    /// <summary>
    /// Gets or sets the fixed price.
    /// </summary>
    /// <value>
    /// The fixed price.
    /// </value>
    public double FixedPrice { get; set; }

    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    /// <value>
    /// The price.
    /// </value>
    public override double Price
    {
      get { return FixedPrice; }
      set { FixedPrice = value; }
    }

    #region Overrides of Payment

    
    #endregion
  }
}