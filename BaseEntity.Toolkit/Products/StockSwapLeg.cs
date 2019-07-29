// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Equity swap leg
  /// </summary>
  /// <remarks>
  ///   <para>An equity swap is an OTC contract between two counterparties to exchange cashflows based on equity
  ///   returns and typically interest rates. The two sets of cashflows are referred to as legs. One leg is typically
  ///   tied to floating interest rates such as LIBOR (see <see cref="SwapLeg"/>). The other
  ///   leg pays based on the performance of a stock or stock index. This second leg is a <see cref="StockSwapLeg"/></para>
  ///   <para>Both the floating rate and equity swap legs pay on a predetermined payment schedule. Parties may agree to make
  ///   periodic payments or a single payment at the maturity of the swap ("bullet" swap).</para>
  ///   <para><b>Example</b></para>
  ///   <para>Take a simple index swap where Party A swaps £5,000,000 at LIBOR + 0.03% (also called LIBOR + 3 basis points)
  ///   against £5,000,000 (FTSE to the £5,000,000 notional).</para>
  ///   <para>In this case Party A will pay (to Party B) a floating interest rate (LIBOR +0.03%) on the £5,000,000 notional
  ///   and would receive from Party B any percentage increase in the FTSE equity index applied to the £5,000,000 notional.</para>
  ///   <para>In this example, assuming a LIBOR rate of 5.97% p.a. and a swap tenor of precisely 180 days, the floating leg
  ///   payer/equity receiver (Party A) would owe (5.97%+0.03%)*£5,000,000*180/360 = £150,000 to the equity payer/floating
  ///   leg receiver (Party B).</para>
  ///   <para>At the same date (after 180 days) if the FTSE had appreciated by 10% from its level at trade commencement,
  ///   Party B would owe 10%*£5,000,000 = £500,000 to Party A. If, on the other hand, the FTSE at the six-month mark had fallen
  ///   by 10% from its level at trade commencement, Party A would owe an additional 10%*£5,000,000 = £500,000 to Party B, since
  ///   the flow is negative.</para>
  ///   <para>For mitigating credit exposure, the trade can be reset, or "marked-to-market" during its life. In that case,
  ///   appreciation or depreciation since the last reset is paid and the notional is increased by any payment to the pricing
  ///   rate payer or decreased by any payment from the floating leg payer.</para>
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing an Equity Swap Leg.</para>
  /// <code language="C#">
  ///   Dt effectiveDate = Dt.Today();                                  // Effective date is today
  ///   Dt maturity = Dt.Add(effectiveDate, 5, TimeUnit.Years);         // Maturity date is 5Yrs after effective
  ///
  ///   StockSwapLeg swapLeg =
  ///     new StockSwapLeg( effectiveDate,                              // Effective date
  ///                       maturityDate,                               // Maturity date
  ///                       Currency.EUR,                               // Currency is Euros
  ///                       0.001,                                      // Spread is 10bp
  ///                       DayCount.Actual360,                         // Acrual Daycount is Actual/360
  ///                       Frequency.SemiAnnual,                       // Semi-annual payment frequency
  ///                       BDConvention.Following,                     // Following roll convention
  ///                       Calendar.TGT                                // Calendar is Target
  ///                     );
  /// </code>
  /// </example>
  /// <note>
  ///   <para>LIBOR (London Interbank Offer Rate) is the interest rate offered by London banks to other
  ///   banks for eurodollar deposits. There are similar rates for other currencies. The swaps market
  ///   often sets floating payments off these indices.</para>
  /// </note>
  [Serializable]
  [ReadOnly(true)]
  public class StockSwapLeg : ProductWithSchedule
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="spread">Spread over return (annualised)</param>
    /// <param name="dayCount">Daycount convention</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="roll">Roll convention</param>
    /// <param name="cal">Calendar</param>
    public StockSwapLeg(Dt effective, Dt maturity, Currency ccy, double spread, DayCount dayCount,
                        Frequency freq, BDConvention roll, Calendar cal
      )
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, roll, cal, CycleRule.None, MakeFlags())
    {
      Spread = spread;
      DayCount = dayCount;
    }

    private static CashflowFlag MakeFlags()
    {
      CashflowFlag flag = CashflowFlag.None;
      flag |= CashflowFlag.RollLastPaymentDate | CashflowFlag.RespectLastCoupon | CashflowFlag.AdjustLast;
      if (ToolkitConfigurator.Settings.SwapLeg.StubAtEnd)
        flag |= CashflowFlag.StubAtEnd;
      return flag;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Spread over equity return paid
    /// </summary>
    [Category("Base")]
    public double Spread { get; set; }

    /// <summary>
    ///   daycount
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (Spread < -2.0 || Spread > 2.0)
        InvalidValue.AddError(errors, this, "Spread", String.Format("Invalid Spread. Must be between -2.0 and 2.0, not ({0})", Spread));
    }

    #endregion
  }
}