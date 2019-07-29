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
  ///<summary>
  /// Commodity Forward product
  ///</summary>
  /// <remarks>
  ///   <para>A commodity forward is an OTC contract where the holder has the obligation to purchase or sell a
  ///   commodity on a specified future expiration date at a predetermined price.</para>
  ///   <para><b>Forwards</b></para>
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
  ///   <h1 align="center"><img src="Forward_Payoff_Long.png" /></h1>
  ///   <para>For a short position this is <m>F_T = K - S_T</m></para>
  ///   <h1 align="center"><img src="Forward_Payoff_Short.png" /></h1>
  ///   <para><b>Determining the Fair Forward Price</b></para>
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
  ///   <para>The lease rate is the net difference between the commodity funding rate and the expected
  ///   growth rate of the commodity. This rate may not be observable in the market unless the
  ///   underlying commodity has an active lease market.</para>
  ///   <para>The lease rate is:</para>
  ///   <formula>
  ///     \delta = y - u
  ///   </formula>
  ///   <para>where</para>
  ///   <list type="bullet">
  ///     <item><description><formula inline="true">\delta</formula> is the lease rate</description></item>
  ///     <item><m>u</m> is the storage cost (for example the cost of storing commodities)</item>
  ///     <item><m>y</m> is the convenience yield. The convenience yield is the benefit to the holder of owning the asset rather than the forward. The convenience
  ///     yield is most evident in commodities and includes benefits such as protecting from short term shortages for a required
  ///     commodity.</item>
  ///   </list>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommodityForwardPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class CommodityForward : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="commodity">Underlying commodity</param>
    /// <param name="fixingDate">Fixing date</param>
    /// <param name="deliveryDate">Futures delivery date (Maturity)</param>
    /// <param name="deliveryPrice">Delivery price</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="calendar">Calendar</param>
    /// <returns>Created Commodity Future</returns>
    public CommodityForward(Commodity commodity, Dt fixingDate, Dt deliveryDate, double deliveryPrice, BDConvention roll, Calendar calendar)
      : base(Dt.Empty, deliveryDate, Currency.None)
    {
      Commodity = commodity;
      FirstFixingDate = LastFixingDate = fixingDate;
      FirstDeliveryDate = deliveryDate;
      SettlementType = SettlementType.Cash;
      DeliveryPrice = deliveryPrice;
      Roll = roll;
      Calendar = calendar;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="fixingDate">Fixing date</param>
    /// <param name="deliveryDate">Futures delivery date (Maturity)</param>
    /// <param name="deliveryPrice">Delivery price</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="calendar">Calendar</param>
    /// <returns>Created Commodity Future</returns>
    public CommodityForward(Dt fixingDate, Dt deliveryDate, double deliveryPrice, BDConvention roll, Calendar calendar)
      : base(Dt.Empty, deliveryDate, Currency.None)
    {
      Commodity = new Commodity();
      FirstFixingDate = LastFixingDate = fixingDate;
      FirstDeliveryDate = deliveryDate;
      SettlementType = SettlementType.Cash;
      DeliveryPrice = deliveryPrice;
      Roll = roll;
      Calendar = calendar;
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
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      // Valid dates
      if (!FirstFixingDate.IsEmpty() && !FirstFixingDate.IsValid())
        InvalidValue.AddError(errors, this, "FirstFixingDate", String.Format("Invalid first fixing date {0}", FirstFixingDate));
      if (!LastFixingDate.IsEmpty() && !LastFixingDate.IsValid())
        InvalidValue.AddError(errors, this, "LastFixingDate", String.Format("Invalid last fixing date {0}", LastFixingDate));
      if (FirstFixingDate.IsValid() && FirstFixingDate.IsValid() && Dt.Cmp(FirstFixingDate, LastFixingDate) > 0)
        InvalidValue.AddError(errors, this, "FirstFixingDate", String.Format("First fixing date {0} must be before last fixing date {1}", FirstFixingDate, LastFixingDate));
      if (!FirstDeliveryDate.IsEmpty() && !FirstDeliveryDate.IsValid())
        InvalidValue.AddError(errors, this, "FirstDeliveryDate", String.Format("Invalid first delivery date {0}", FirstDeliveryDate));
      if (!LastDeliveryDate.IsValid())
        InvalidValue.AddError(errors, this, "LastDeliveryDate", String.Format("Invalid last deliver date {0}", LastDeliveryDate));
      if (LastDeliveryDate.IsValid() && FirstDeliveryDate.IsValid() && Dt.Cmp(LastDeliveryDate, FirstDeliveryDate) < 0)
        InvalidValue.AddError(errors, this, "LastDeliveryDate", String.Format("First delivery date {0} must be before last delivery date {1}", FirstDeliveryDate, LastDeliveryDate));
      if (LastDeliveryDate.IsValid() && LastFixingDate.IsValid() && Dt.Cmp(LastDeliveryDate, LastFixingDate) < 0)
        InvalidValue.AddError(errors, this, "LastDeliveryDate", String.Format("Last deliver date {0} must be after last trading date {1}", LastDeliveryDate, LastFixingDate));
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Underlying Commodity
    /// </summary>
    public Commodity Commodity { get; private set; }

    /// <summary>
    /// Earliest fixing date
    /// </summary>
    /// <remarks>Dt.Empty if not specified.</remarks>
    public Dt FirstFixingDate { get; set; }

    /// <summary>
    /// Last fixing date
    /// </summary>
    /// <remarks>Dt.Empty if not specified.</remarks>
    public Dt LastFixingDate { get; set; }

    /// <summary>
    /// Earliest delivery date
    /// </summary>
    /// <remarks>Dt.Empty if not specified.</remarks>
    public Dt FirstDeliveryDate { get; set; }

    /// <summary>
    /// Futures last delivery date (Maturity)
    /// </summary>
    public Dt LastDeliveryDate { get { return Maturity; } set { Maturity = value; } }

    /// <summary>
    /// Cash or physical settlement
    /// </summary>
    public SettlementType SettlementType { get; set; }

    /// <summary>
    /// Forward price
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

    #endregion Properties
  }
}
