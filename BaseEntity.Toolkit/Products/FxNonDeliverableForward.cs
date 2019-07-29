/*
 *  -2012. All rights reserved.
 */

using System;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  /// Non-Deliverable Fx Forward product
  /// </summary>
  /// <remarks>
  ///   <para>Non-Deliverable fx forwards are OTC contracts commiting a buyer and a seller to exchange
  ///   the economic equivalent of an amount of one currency for an amount of another currency at an
  ///   agreed exchange rate and an agreed future date.</para>
  ///   <para>Unlike a deliverable fx forward where amounts in each currency are exchanged, a
  ///   non-deliverable fx forward is cash settled in the agreed currency. This is typically used for
  ///   currencies which do not allow forward fx trading.</para>
  ///   <para>The convention used is the buyer purchases 1 unit of the receive currency (receiveCcy) for
  ///   fxRate units of the pay currency (payCcy).</para>
  ///   <para>The fx rate (FxRate) is quoted as 1 ReceiveCcy buys n PayCcy or ReceiveCcy/PayCcy
  ///   in fx quoting terms. This many not be the standard quoting convention for that currency pair.</para>
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
  ///   <h1 align="center"><img src="Forward_Payoff_Long.png" width="50%"/></h1>
  ///   <para>For a short position this is <m>F_T = K - S_T</m></para>
  ///   <h1 align="center"><img src="Forward_Payoff_Short.png" width="50%"/></h1>
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
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class FxNonDeliverableForward : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    FxNonDeliverableForward() : base()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="valueDate">Date of future exchange (settlement)</param>
    /// <param name="receiveCcy">Receive (domestic/base/unit/transaction/source/to/receive) currency</param>
    /// <param name="payCcy">Pay (foreign/quote/price/payment/destination/from/pay) currency</param>
    /// <param name="fxRate">Exchange rate (receiveCcy/payCcy - cost of 1 unit of receiveCcy in payCcy</param>
    /// <param name="settleOnPayCcy">True if delivery currency is the same as payCcy, otherwise settle in receiveCcy</param>
    /// <param name="fixingDate">The date that fixing rate is determined</param>
    public FxNonDeliverableForward(Dt valueDate, Currency receiveCcy, Currency payCcy,
      double fxRate, bool settleOnPayCcy, Dt fixingDate)
      : base(Dt.Empty, valueDate, receiveCcy)
    {
      PayCcy = payCcy;
      FxRate = fxRate;
      DeliveryCcy = settleOnPayCcy ? payCcy : receiveCcy;
      FixingDate = fixingDate;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="errors">ArrayList of errors found</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      // Invalid NearValueDate date
      if (!ValueDate.IsValid())
        InvalidValue.AddError(errors, this, "ValueDate", String.Format("Invalid value date. Must be empty or valid date, not {0}", ValueDate));
      // Pay and receive currencies different
      if (Ccy == PayCcy)
        InvalidValue.AddError(errors, this, "Ccy", String.Format("Pay currency {0} cannot be same as receive currency {1}", Ccy, PayCcy));
      // Strike has to be >= 0
      if (FxRate < 0.0)
        InvalidValue.AddError(errors, this, "FxRate ", String.Format("Invalid Fx Rate. Must be +ve, not {0}", FxRate));
      if (!FixingDate.IsValid())
        InvalidValue.AddError(errors, this, "FixingDate", "Invalid FixingDate");
      if (FixingDate.IsValid() && ValueDate.IsValid() && FixingDate > ValueDate)
        InvalidValue.AddError(errors, this, "FixingDate", "FixingDate cannot be after ValueDate");
      if (DeliveryCcy != PayCcy && DeliveryCcy != Ccy)
        InvalidValue.AddError(errors, this, string.Format("Invalid DeliveryCcy, must be either [{0}] or [{1}], not [{2}] ", PayCcy, Ccy, DeliveryCcy));
      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Pay currency
    /// </summary>
    [Category("Base")]
    public Currency PayCcy { get; set; }

    /// <summary>
    ///   Receive currency
    /// </summary>
    [Category("Base")]
    public Currency ReceiveCcy
    {
      get { return Ccy; }
      set { Ccy = value; }
    }

    /// <summary>
    ///   Fx rate
    /// </summary>
    [Category("Base")]
    public double FxRate { get; set; }

    /// <summary>
    ///   ValueDate date
    /// </summary>
    [Category("Base")]
    public Dt ValueDate
    {
      get { return Maturity; }
      set { Maturity = value; }
    }

    ///<summary>
    /// The currency of settlement amount
    ///</summary>
    public Currency DeliveryCcy { get; set; }

    ///<summary>
    /// The date settlement Fx rate is fixed
    ///</summary>
    public Dt FixingDate { get; set; }

    #endregion Properties
  }
}
