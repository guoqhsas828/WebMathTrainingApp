// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Swap contract composed of two swap legs. Could be both floating, fixed or any combination thereof. 
  /// </summary>
  /// <remarks>
  /// <para>A Commodity Swap is an agreement involving the exchange of a series of commodity price payments
  /// (fixed amount) against variable commodity price payments (market price) resulting exclusively in a
  /// cash settlement (settlement amount).</para>
  /// <para>The buyer of a Commodity Swap acquires the right to be paid a settlement amount (compensation) if
  /// the market price rises above the fixed amount. In contract, the buyer of a Commodity Swap is obliged
  /// to pay the settlement amount if the market price falls below the fixed amount.</para>
  /// <para>The buyer of a commodity Swap acquires the right to be paid a settlement amount, if the market price
  /// rises above the fixed amount. In contract, the seller of a commodity Swap is obligated to pay the
  /// settlement amount if the market price falls below the fixed amount.</para>
  /// <para>Both streams of payment (fixed/variable) are in the same currency and based on the same nominal
  /// amount. While the fixed side of the swap is of the benchmark nature ( it is constant), the variable side
  /// is related to the trading price of the relevant commodities quoted on a stock exchange or otherwise
  /// published on the commodities futures market on the relevant fixing date or to a commodity price
  /// index.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.CommoditySwapPricer"/>
  [Serializable]
  public class CommoditySwap : Product
  {
    #region Constructors

    /// <summary>
    /// Swap constructor from two swap legs
    /// </summary>
    /// <param name="receiverLeg">CommoditySwapLeg object</param>
    /// <param name="payerLeg">CommoditySwapLeg object</param>
    public CommoditySwap(CommoditySwapLeg receiverLeg, CommoditySwapLeg payerLeg)
      : base(Dt.Min(receiverLeg.Effective, payerLeg.Effective),
             (Dt.Cmp(receiverLeg.Maturity, payerLeg.Maturity) > 0) ? receiverLeg.Maturity : payerLeg.Maturity,
             (payerLeg.Ccy == receiverLeg.Ccy) ? receiverLeg.Ccy : Currency.None)
    {
      ReceiverLeg = receiverLeg;
      PayerLeg = payerLeg;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (CommoditySwap)base.Clone();
      if (ReceiverLeg != null)
        obj.ReceiverLeg = (CommoditySwapLeg)ReceiverLeg.Clone();
      if (PayerLeg != null)
        obj.PayerLeg = (CommoditySwapLeg)PayerLeg.Clone();
      return obj;
    }

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    /// This tests only relationships between fields of the product that
    /// cannot be validated in the property methods.
    /// </remarks>
    /// <param name="errors"></param>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Swap receiver leg. 
    /// </summary>
    public CommoditySwapLeg ReceiverLeg { get; set; }

    /// <summary>
    /// Swap payer leg. 
    /// </summary>
    public CommoditySwapLeg PayerLeg { get; set; }

    /// <summary>
    /// Payer leg is fixed
    /// </summary>
    public bool IsPayerFixed
    {
      get { return !PayerLeg.Floating; }
    }

    /// <summary>
    /// Receiver leg is fixed
    /// </summary>
    public bool IsReceiverFixed
    {
      get { return !ReceiverLeg.Floating; }
    }

    /// <summary>
    /// True if the swap has one fixed and one floating leg
    /// </summary>
    public bool IsFixedAndFloating
    {
      get
      {
        if (PayerLeg == null || ReceiverLeg == null)
          return false;
        return ((IsPayerFixed && !IsReceiverFixed) || (!IsPayerFixed && IsReceiverFixed));
      }
    }

    /// <summary>
    /// Spread paid on receiver (if floating)
    /// </summary>
    public bool IsSpreadOnReceiver
    {
      get { return ReceiverLeg.Floating && Math.Abs(ReceiverLeg.Price) > double.Epsilon; }
    }

    /// <summary>
    /// Spread paid on payer (if floating)
    /// </summary>
    public bool IsSpreadOnPayer
    {
      get { return PayerLeg.Floating && Math.Abs(PayerLeg.Price) > double.Epsilon; }
    }

    /// <summary>
    /// Swap is a Basis swap
    /// </summary>
    public bool IsBasisSwap
    {
      get { return PayerLeg.Floating && ReceiverLeg.Floating; }
    }
    #endregion Properties
  }
}