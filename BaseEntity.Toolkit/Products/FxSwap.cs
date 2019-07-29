/*
 *  -2012. All rights reserved.
 */
using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using System.Collections;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  /// Setup options for notional amounts on each leg of the swap.  
  /// </summary>
  public enum FxSwapNotionalType
  {
    /// <summary>
    /// Near Receive notional equals Far Pay notional
    /// </summary>
    NearReceiveEqualsFarPay,
    /// <summary>
    /// Near Pay notional equals Far Receive notional
    /// </summary>
    NearPayEqualsFarReceive,
    /// <summary>
    /// Near and far notional amounts are set independently
    /// </summary>
    UnevenNotionals
  }

  /// <summary>
  /// Fx Swap product
  /// </summary>
  /// <remarks>
  /// <para>FX Swaps are OTC contracts commiting a buyer and a seller to two simultaneous exchanges of one currency
  /// for another on two dates (normally the spot date and the forward date).</para>
  /// <para>The FX Swap consists of two legs. A 'near' exchange and a 'far' reverse exchange. The 'near' exchange is
  /// usually the spot date.</para>
  /// <para>On the near exchange date (nearValueDate), the buyer receives 1 unit of the receive currency
  /// (nearReceiveCcy) and pays NearFxRate units of the pay currency (NearPayCcy).</para>
  /// <para>On the far exchange date (farValueDate) this is reversed with the buyer paying 1 unit of
  /// NearReceiveCcy and recieving FarFxRate units of NearPayCcy.</para>
  /// <para>The fx rates (NearFxRate and FarFxRate) are quoted as 1 ReceiveCcy buys n PayCcy or ReceiveCcy/PayCcy
  /// in fx quoting terms. This many not be the standard quoting convention for that currency pair.</para>
  /// <para>FX swaps have been employed to raise foreign currencies, both for financial institutions and their
  /// customers, including exporters and importers, as well as institutional investors who wish to hedge their
  /// positions. They are also frequently used for speculative trading, typically by combining two offsetting
  /// positions with different original maturities. FX swaps are most liquid at terms shorter than one year, but
  /// transactions with longer maturities have been increasing in recent years. For comprehensive data on recent
  /// developments in turnover and outstanding in FX swaps and crosscurrency swaps.</para>
  /// <h1 align="center"><img src="FxSwap.gif" width="80%"/></h1>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.SwapPricer"/>
  /// <seealso href="http://www.bis.org/publ/qtrpdf/r_qt0803z.htm">The basic mechanics of FX swaps and cross-currency basis swaps, BIS</seealso>
  /// <seealso href="http://en.wikipedia.org/wiki/Fx_swap">Wikipedia</seealso>
  [Serializable]
  [ReadOnly(true)]
  public class FxSwap : Product
  {
    private double? _farPayAmountOverride;
    private double? _nearReceiveAmountOverride;

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    FxSwap()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="nearValueDate">Near value (settlement) date</param>
    /// <param name="nearReceiveCcy">Near receive (domestic/base/unit/transaction/source/to/receive) currency</param>
    /// <param name="nearPayCcy">near pay (foreign/quote/price/payment/destination/from/pay) currency</param>
    /// <param name="nearPayAmount">near pay amount in near pay ccy</param>
    /// <param name="farValueDate">Far value (settlement) date</param>
    /// <param name="nearReceiveAmount">near receive amount in near receive ccy</param>
    /// <param name="farPayAmount">far rcv amount in near receive ccy</param>
    /// <param name="farReceiveAmount">far rcv amount in near pay ccy</param>
    public
    FxSwap(Dt nearValueDate, Currency nearReceiveCcy, Currency nearPayCcy,
              double nearReceiveAmount, double nearPayAmount, Dt farValueDate, double farPayAmount, double farReceiveAmount)
      : base(nearValueDate, farValueDate, nearReceiveCcy)
    {
      NearPayCcy = nearPayCcy;
      NearFxRate = nearPayAmount/nearReceiveAmount;
      FarFxRate = farReceiveAmount/farPayAmount;
      NearReceiveAmount = nearReceiveAmount;
      FarPayAmount = farPayAmount; 
      if(nearReceiveAmount.AlmostEquals(farPayAmount))
        NotionalType = FxSwapNotionalType.NearReceiveEqualsFarPay;
      else if(nearPayAmount.AlmostEquals(farReceiveAmount))
        NotionalType = FxSwapNotionalType.NearPayEqualsFarReceive;
      else
        NotionalType = FxSwapNotionalType.UnevenNotionals;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="nearValueDate">Near value (settlement) date</param>
    /// <param name="nearReceiveCcy">Near receive (domestic/base/unit/transaction/source/to/receive) currency</param>
    /// <param name="nearPayCcy">near pay (foreign/quote/price/payment/destination/from/pay) currency</param>
    /// <param name="nearFxRate">near Exchange rate (nearReceiveCcy/nearPayCcy - cost of 1 unit of nearReceiveCcy in nearPayCcy</param>
    /// <param name="farValueDate">Far value (settlement) date</param>
    /// <param name="farFxRate">far Exchange rate (nearReceiveCcy/nearPayCcy - cost of 1 unit of nearReceiveCcy in nearPayCcy</param>
    public
    FxSwap(Dt nearValueDate, Currency nearReceiveCcy, Currency nearPayCcy,
              double nearFxRate, Dt farValueDate, double farFxRate)
      : base(nearValueDate, farValueDate, nearReceiveCcy)
    {
      NearPayCcy = nearPayCcy;
      NearFxRate = nearFxRate;
      FarFxRate = farFxRate;
      NotionalType = FxSwapNotionalType.NearPayEqualsFarReceive;
    }

   
    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Invalid NearValueDate date
      if (!NearValueDate.IsValid())
        InvalidValue.AddError(errors, this, "NearValueDate", String.Format("Invalid near value date. Must be valid date, not {0}", NearValueDate));

      // Pay and receive currencies different
      if (NearReceiveCcy == NearPayCcy)
        InvalidValue.AddError(errors, this, "Ccy", String.Format("Pay currency {0} cannot be same as receive currency {1}", Ccy, NearPayCcy));

      // Strike has to be >= 0
      if (NearFxRate < 0.0)
        InvalidValue.AddError(errors, this, "NearFxRate ", String.Format("Invalid Fx Rate. Must be +ve, not {0}", NearFxRate));

      // Invalid FarValueDate date
      if (!FarValueDate.IsValid())
        InvalidValue.AddError(errors, this, "FarValueDate", String.Format("Invalid far value date. Must be valid date, not {0}", FarValueDate));
      if( Dt.Cmp(FarValueDate, NearValueDate) <= 0 )
        InvalidValue.AddError(errors, this, "FarValueDate", String.Format("Far value date {0} must be after near value date {1}", FarValueDate, NearValueDate));

      // Strike has to be >= 0
      if (FarFxRate < 0.0)
        InvalidValue.AddError(errors, this, "FarFxRate ", String.Format("Invalid Fx Rate. Must be +ve, not {0}", FarFxRate));

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Receive currency
    /// </summary>
    [Category("Base")]
    public Currency NearReceiveCcy
    {
      get { return Ccy; }
      set { Ccy = value; }
    }

    /// <summary>
    ///  Pay currency
    /// </summary>
    [Category("Base")]
    public Currency NearPayCcy { get; set; }

    /// <summary>
    ///   Near Fx rate
    /// </summary>
    [Category("Base")]
    public double NearFxRate { get; set; }

    /// <summary>
    ///   Far Fx rate
    /// </summary>
    [Category("Base")]
    public double FarFxRate { get; set; }

    /// <summary>
    ///   NearValueDate date
    /// </summary>
    /// <remarks>An alias for Effective date</remarks>
    [Category("Base")]
    public Dt NearValueDate
    {
      get { return Effective;} 
      set { Effective = value;}
    }

    /// <summary>
    ///   FarValueDate date
    /// </summary>
    [Category("Base")]
    public Dt FarValueDate
    {
      get { return Maturity; }
      set { Maturity = value; }
    }

    ///<summary>
    /// Either Near Receive notional same as Far Pay notional, Near Pay notional same as Far Receive notional, or all notionals uneven.
    ///</summary>
    [Category("Base")]
    public FxSwapNotionalType NotionalType { get; set; }

    /// <summary>
    /// Near amount paid in pay currency
    /// </summary>
    /// <returns>Amount paid in pay currency</returns>
    public double NearPayAmount
    {
      get
      {
        return NearReceiveAmount * NearFxRate; 
      }
    }

    /// <summary>
    /// Near amount received in receive currency
    /// </summary>
    /// <returns>Amount received in receive currency</returns>
    public double NearReceiveAmount
    {
      get
      {
        if (_nearReceiveAmountOverride.HasValue)
          return _nearReceiveAmountOverride.Value;
        return 1.0; 
      }
      private set { _nearReceiveAmountOverride = value; }
    }

    /// <summary>
    /// Far amount paid in near receive currency
    /// </summary>
    /// <returns>Amount paid in pay currency</returns>
    public double FarPayAmount
    {
      get
      {
        if(_farPayAmountOverride.HasValue)
          return _farPayAmountOverride.Value ;
        return NotionalType == FxSwapNotionalType.NearReceiveEqualsFarPay ? NearReceiveAmount : NearPayAmount / FarFxRate;
      }
      private set { _farPayAmountOverride = value; }
    }

    /// <summary>
    /// Far amount received in near pay currency
    /// </summary>
    /// <returns>Amount received in near pay currency</returns>
    public double FarReceiveAmount
    {
      get
      {
        return NotionalType == FxSwapNotionalType.NearPayEqualsFarReceive ? NearPayAmount : FarPayAmount * FarFxRate;
      }
    }

    #endregion Properties

  } // class FxSwap
}
