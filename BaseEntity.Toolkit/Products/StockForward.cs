// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Equity Forward product
  /// </summary>
  /// <remarks>
  ///   <para>Stock forwards are forwards where the underlying asset is a common stock or stock index.</para>
  /// 
  ///   <para><h2>Forwards</h2></para>
  ///   <para>A forward is an OTC contract between two counterparties to buy or sell an asset at a specified future time and at
  ///   a specified agreed price. The party agreeing to buy the underlying asset is said to be long the contract and the party
  ///   agreeing to sell the underlying asset is said to be short the contract. The price agreed to buy and sell the underlying
  ///   asset is termed the delivery price and the date agreed is the value or maturity date. The delivery price is set at a fair
  ///   forward price of the asset.</para>
  ///   <para>A closely related product is a futures contract. Futures differ from forwards in that forwards are exchange traded
  ///   with margin posted daily.</para>
  ///   <para>At the trade inception no money is exchanged. The value of the contract at maturity <m>T</m> is a function of difference
  ///   between the value of the underlying asset <m>S_T</m> and the delivery price <m>K</m> on the maturity date.</para>
  ///   <para>For a long position this is <m>F_T = S_T - K</m></para>
  ///   <h1 align="center"><img src="Forward_Payoff_Long.png"/></h1>
  ///   <para>For a short position this is <m>F_T = K - S_T</m></para>
  ///   <h1 align="center"><img src="Forward_Payoff_Short.png"/></h1>
  /// 
  ///   <para><h2>Determining the Fair Forward Price</h2></para>
  ///   <para>The fair forward price of an asset relates to the cost of buying the asset today and the cost of holding or carrying
  ///   that asset to the maturity date of forward contract. The forward price <m>F_T</m> must satisfy:</para>
  ///   <math>F_T = S_t e^{cT} - \sum_{i=0}^{n}\left ( {cf}_i e^{r t_i}  \right )</math>
  ///   <para>where:</para>
  ///   <list>
  ///     <item><m>S</m> is the spot price of the asset</item>
  ///     <item><m>c</m> is the cost of holding the asset</item>
  ///     <item><m>T</m> is the future maturity</item>
  ///     <item><m>{cf}_i</m> is the ith cashflow received from holding the underlying asset</item>
  ///     <item><m>t_i</m> is the time of the ith cashflow</item>
  ///     <item><m>r</m> is the risk free rate</item>
  ///   </list>
  ///   <para>The carry <m>c = r + u - y</m> where:</para>
  ///   <list>
  ///     <item><m>r</m> is the risk free rate</item>
  ///     <item><m>u</m> is the storage cost (for example the cost of storing commodities)</item>
  ///     <item><m>y</m> is the convenience yield. The convenience yield is the benefit to the holder of owning the asset rather than the forward. The convenience
  ///     yield is most evident in commodities and includes benefits such as protecting from short term shortages for a required
  ///     commodity.</item>
  ///   </list>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.StockForwardPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class StockForward : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public StockForward()
      : base(Dt.Empty, Dt.MaxValue, Currency.None)
    {
      Ticker = String.Empty;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="deliveryDate">Delivery date</param>
    /// <param name="deliveryPrice">Delivery price</param>
    /// <param name="ccy">Currency</param>
    public StockForward(Dt deliveryDate, double deliveryPrice, Currency ccy)
      : base(Dt.Empty, deliveryDate, ccy)
    {
      DeliveryPrice = deliveryPrice;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    /// This tests only relationships between fields of the product that
    /// cannot be validated in the property methods.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (!FixingDate.IsEmpty() && !FixingDate.IsValid())
        InvalidValue.AddError(errors, this, "FixingDate", String.Format("Invalid fixing date {0}", FixingDate));
      if (FixingDate.IsValid() && Dt.Cmp(FixingDate, DeliveryDate) > 0)
        InvalidValue.AddError(errors, this, "FixingDate",
                              String.Format("Fixing date {0} must be before delivery date {1}", FixingDate, DeliveryDate));
      if (DeliveryPrice <= 0)
        InvalidValue.AddError(errors, this, "DeliveryPrice", String.Format("Stock price {0} must be positive", DeliveryPrice));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Fixing date
    /// </summary>
    /// <remarks>Dt.Empty if not specified.</remarks>
    public Dt FixingDate { get; set; }

    /// <summary>
    /// Delivery date (Maturity)
    /// </summary>
    public Dt DeliveryDate { get { return Maturity; } set { Maturity = value; } }

    /// <summary>
    /// Stock price
    /// </summary>
    public double DeliveryPrice { get; set; }

    /// <summary>
    /// Roll convention
    /// </summary>
    public BDConvention Roll { get; set; }

    /// <summary>
    /// Calendar
    /// </summary>
    public Calendar Calendar { get; set; }

    /// <summary>
    /// Ticker
    /// </summary>
    public string Ticker { get; set; }

    #endregion Properties
  }
}
