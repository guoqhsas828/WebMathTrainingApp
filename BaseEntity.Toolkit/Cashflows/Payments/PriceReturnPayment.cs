// 
//  -2015. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.RateProjectors;

namespace BaseEntity.Toolkit.Cashflows.Payments
{
  /// <summary>
  ///  Price Return Payment.
  /// </summary>
  [Serializable]
  public class PriceReturnPayment: Payment
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="PriceReturnPayment" /> class.
    /// </summary>
    /// <param name="lastPayDt">The last payment date</param>
    /// <param name="payDate">The payment date</param>
    /// <param name="ccy">The payment currency</param>
    /// <param name="beginDate">The reset date of the begin price level</param>
    /// <param name="endDate">The reset date of the end price level</param>
    /// <param name="priceCalculator">The price calculator.</param>
    /// <param name="beginPrice">The begin price.</param>
    /// <param name="isAbsolute">If set to <c>true</c>, the price return
    /// is calculated as the absolute price difference; otherwise,
    /// relative price change</param>
    public PriceReturnPayment(
      Dt lastPayDt, Dt payDate, Currency ccy,
      Dt beginDate, Dt endDate,
      IPriceCalculator priceCalculator,
      double beginPrice = double.NaN,
      bool isAbsolute = false)
      : base(payDate, ccy)
    {
      LastPayDt = lastPayDt;
      BeginDate = beginDate;
      EndDate = endDate;
      PriceCalculator = priceCalculator;
      BeginPriceOverride = beginPrice;
      IsAbsolute = isAbsolute;
    }

    /// <summary>
    /// Gets the last payment date.
    /// </summary>
    /// <value>The last payment date</value>
    public Dt LastPayDt { get; private set; }

    /// <summary>
    /// Gets the reset date of the begin price level
    /// </summary>
    /// <value>The begin date</value>
    public Dt BeginDate { get; private set; }

    /// <summary>
    /// Gets or sets the begin price override.
    /// </summary>
    /// <value>The begin price override.</value>
    internal double BeginPriceOverride { get; set; }

    /// <summary>
    /// Gets the reset date of the end price level.
    /// </summary>
    /// <value>The end date</value>
    public Dt EndDate { get; private set; }

    /// <summary>
    /// Gets the price calculator
    /// </summary>
    /// <value>The price calculator</value>
    public IPriceCalculator PriceCalculator { get; private set; }

    /// <summary>
    ///  Gets a value indicating whether the return calculated
    ///  as the absolute price difference or relative return.
    /// </summary>
    /// <value><c>true</c> if this instance is relative return; otherwise, <c>false</c>.</value>
    public bool IsAbsolute { get; private set; }

    /// <summary>
    /// Gets the fixing of the begin price.
    /// </summary>
    /// <value>The begin price</value>
    public Fixing BeginFixing
    {
      get
      {
        return double.IsNaN(BeginPriceOverride)
          ? PriceCalculator.GetPrice(BeginDate, LastPayDt)
          : new Fixing
          {
            Forward = BeginPriceOverride,
            RateResetState = RateResetState.ResetFound
          };
      }
    }

    /// <summary>
    /// Gets the fixing of the end price.
    /// </summary>
    /// <value>The end price</value>
    public Fixing EndFixing
    {
      get { return PriceCalculator.GetPrice(EndDate, PayDt); }
    }

    /// <summary>
    /// True if the payment is projected
    /// </summary>
    /// <value><c>true</c> if this instance is projected; otherwise, it's historical value</value>
    public override bool IsProjected
    {
      get { return EndFixing.RateResetState == RateResetState.IsProjected; }
    }

    /// <summary>
    /// Computes the price return.
    /// </summary>
    /// <returns>System.Double.</returns>
    public double ComputeReturn()
    {
      return PriceCalculatorUtility.CalculateReturn(
        BeginFixing.Value, EndFixing.Value, IsAbsolute);
    }

    /// <summary>
    /// Calculate the payment amount from the price changes
    /// </summary>
    /// <returns>System.Double.</returns>
    protected override double ComputeAmount()
    {
      return ComputeReturn();
    }
  }
}
