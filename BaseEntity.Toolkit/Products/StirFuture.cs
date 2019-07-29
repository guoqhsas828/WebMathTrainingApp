//
// StirFuture.cs
//  -2014. All rights reserved.
//

using System;
using System.Collections;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Short Term Interest Rate (STIR) Futures
  /// </summary>
  /// <remarks>
  ///   <para>STIR Futures are futures where the underlying is a short term interest rate security.</para>
  ///   <para><b>Deposit rate futures</b></para>
  ///   <para>An example of a deposit future is the CME 3M Eurodollar future.</para>
  ///   <para>CME Eurodollar futures are futures on the interest paid on a nominal 3 month (90 day) underlying Eurodollar deposit of 1MM USD.</para>
  ///   <para>The futures are quoted on an indexed rate (100 - rate). By construction the value of each point is the 
  ///   the change in interest accrued. ie <m>0.00005 * \frac{90}{360} * 1,000,000 = $12.5</m>.</para>
  /// 
  ///   <para><b>Discount rate futures</b></para>
  ///   <para>Examples of discount rate futures include TBill futures and the ASX bill future.</para>
  ///   <para>The CME Treasury Bill Future contract is based on 13-week cash treasury bill with a face value of $1MM
  ///   The future contract requires delivery of an underlying asset with 90-day to maturity, 
  ///   although delivery of t-bill with 91 or 92 days remaining till delivery is acceptable with price adjustment.</para>
  ///   <para>The ASX bank bill future contract is based on 90 day bank accepted bills or EBA or bank negotiable certificate of deposit.
  ///   The future contract requires delivery of face value of AUD1MM maturing 85-95 days from settlement days.  
  ///   The price of Treasury/Bank bill future contract is derived from the IMM index, which is equal to 100 minus the
  ///   discount yield of bill futures.</para>
  ///
  ///   <para>Common STIR futures contracts include:</para>
  ///   <list type="number">
  ///   <item><description><a href="http://www.cmegroup.com/trading/interest-rates/stir/eurodollar_contract_specifications.html">CME Eurodollar and Fed Funds Futures</a></description></item>
  ///   <item><description><a href="https://globalderivatives.nyx.com/stirs/nyse-liffe">Liffe Euribor, Euroswiss and Short Sterling Futures</a></description></item>
  ///   <item><description><a href="http://www.asx.com.au/products/interest-rate-derivatives/short-term-interest-rate-derivatives.htm">ASX 30 day interbank cash rate, 90 day bill and 3 month OIS futures</a></description></item>
  ///   </list>
  /// 
  ///   <para><b>Futures</b></para>
  ///   <inheritdoc cref="FutureBase" />
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class StirFuture : FutureBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    ///   <para>Compounded STIR futures reference an underling index such as an
    ///   OIS rate.</para>
    /// </remarks>
    /// <param name="rateFutureType">Rate Future type</param>
    /// <param name="lastDelivery">Delivery date or underlying asset settlement date</param>
    /// <param name="depositAccrualStart">Accrual start date for underlying reference rate</param>
    /// <param name="depositAccrualEnd">Accrual end date for underlying reference rate</param>
    /// <param name="referenceIndex">Underlying deposit index</param>
    /// <param name="contractSize">Size or notional of each contract</param>
    /// <param name="tickSize">Futures quoted price tick size, as percentage points</param>
    /// <param name="tickValue">Futures value per tick size</param>
    public StirFuture(RateFutureType rateFutureType, Dt lastDelivery, Dt depositAccrualStart, Dt depositAccrualEnd, ReferenceIndex referenceIndex,
      double contractSize, double tickSize, double tickValue)
      : base(lastDelivery, contractSize, tickSize)
    {
      base.Ccy = referenceIndex.Currency;
      base.TickValue = tickValue;
      ReferenceIndex = referenceIndex;
      RateFutureType = rateFutureType;
      DepositSettlement = depositAccrualStart;
      DepositMaturity = depositAccrualEnd;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if ((RateFutureType == RateFutureType.ArithmeticAverageRate || RateFutureType == RateFutureType.GeometricAverageRate) &&
          !LastTradingDate.IsValid())
        InvalidValue.AddError(errors, this, "LastTradingDate", String.Format("Invalid last trading date {0}", FirstTradingDate));
    }

    /// <summary>
    /// Scaling factor for Point Value.
    /// </summary>
    /// <returns></returns>
    protected override double PointValueScalingFactor()
    {
      return 1e4; // 1e4 for IR products
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Rate Future type
    /// </summary>
    public RateFutureType RateFutureType { get; set; }

    /// <summary>
    /// Underlying rate reference index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Underlying rate reference index name
    /// </summary>
    public string IndexName => ReferenceIndex != null ? ReferenceIndex.IndexName : "";

      /// <summary>
    /// Underlying rate reference index name
    /// </summary>
    public Tenor IndexTenor { get { return ReferenceIndex != null ? ReferenceIndex.IndexTenor : Tenor.Empty; } }

    /// <summary>
    /// Underlying deposit settlement date
    /// </summary>
    /// <remarks>
    ///   <para>This is the accrual start date for the underlying deposit or index.</para>
    /// </remarks>
    public Dt DepositSettlement { get; set; }

    /// <summary>
    /// Underlying deposit maturity date
    /// </summary>
    /// <remarks>
    ///   <para>This is the accrual end date of the underlying deposit or index.</para>
    /// </remarks>
    public Dt DepositMaturity { get; set; }

    #endregion Properties

    #region Utility Methods

    /// <summary>
    /// Guess contract terms from type and currency
    /// </summary>
    /// <remarks>
    ///   <para>Does a best attempt at calculating terms based on the type
    ///   and currency.</para>
    ///   <para>Provided primarily for backward compatability. It is recommended that
    ///   the details are specified to avoid error.</para>
    /// </remarks>
    /// <param name="rateFutureType">Rate Future type</param>
    /// <param name="ccy">Currency</param>
    /// <param name="month">Contract month</param>
    /// <param name="year">Contract year</param>
    /// <param name="tenor">Underlying tenor</param>
    /// <param name="lastTrading">Last trading date</param>
    /// <param name="lastDelivery">Last delivery date (physical) or underlying settlement date (deposit) or last trading date (compounded)</param>
    /// <param name="depositAccrualStart">Underlying deposit accrual start date</param>
    /// <param name="depositAccrualEnd">Underlying deposit accrual end date</param>
    /// <param name="contractSize">Resulting contract size</param>
    /// <param name="tickSize">Resulting tick size</param>
    /// <param name="tickValue">Resulting tick value</param>
    public static void TermsFromType(RateFutureType rateFutureType, Currency ccy,
      int month, int year, Tenor tenor,
      out Dt lastTrading, out Dt lastDelivery, out Dt depositAccrualStart, out Dt depositAccrualEnd,
      out double contractSize, out double tickSize, out double tickValue
      )
    {
      switch (rateFutureType)
      {
        case RateFutureType.TBill:
          // CME 13W TBill Futures
          contractSize = 1000000;
          tickSize = 0.25 / 1e4;
          tickValue = 12.5;
          lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.ThirdWednesday, BDConvention.Following, Calendar.NYB);
          lastTrading = Dt.AddDays(lastDelivery, -2, Calendar.NYB);
          depositAccrualStart = lastDelivery;
          depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.NYB);
          break;
        case RateFutureType.ASXBankBill:
          // ASX 90 day bank bill futures
          contractSize = 1000000;
          tickSize = 0.01 / 1e4;
          tickValue = 0.0; // Tickvalue is variable
          if( ccy == Currency.NZD )
            lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.SecondWednesday, BDConvention.Following, Calendar.SYB);
          else
            lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.SecondFriday, BDConvention.Following, Calendar.SYB);
          lastTrading = Dt.AddDays(lastDelivery, -1, Calendar.SYB);
          depositAccrualStart = lastDelivery;
          depositAccrualEnd = Dt.Add(lastDelivery, 90);
          break;
        case RateFutureType.ArithmeticAverageRate:
          switch (ccy)
          {
            case Currency.AUD:
              // ASX 30 Day interbank cash rate futures
              contractSize = 3000000;
              tickSize = 0.5 / 1e4;
              tickValue = 24.66;
              lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.Last, BDConvention.Preceding, Calendar.SYB);
              lastTrading = Dt.AddDays(lastDelivery, -1, Calendar.SYB);
              depositAccrualStart = Dt.DayOfMonth(month, year, DayOfMonth.First, BDConvention.Following, Calendar.SYB);
              depositAccrualEnd = lastDelivery;
              break;
            default:
              // CME Fed Funds
              contractSize = 5000000;
              tickSize = 0.5 / 1e4;
              tickValue = 41.67;
              lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.Last, BDConvention.Preceding, Calendar.NYB);
              lastTrading = Dt.AddDays(lastDelivery, -2, Calendar.NYB);
              depositAccrualStart = Dt.DayOfMonth(month, year, DayOfMonth.First, BDConvention.Following, Calendar.NYB);
              depositAccrualEnd = lastDelivery;
              break;
          }
          break;
        case RateFutureType.GeometricAverageRate:
          switch (ccy)
          {
            case Currency.AUD:
              // ASX 3M OIS futures
              contractSize = 1000000;
              tickSize = 0.5 / 1e4;
              tickValue = 24.66;
              lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.SecondThursday, BDConvention.Following, Calendar.SYB);
              lastTrading = Dt.AddDays(lastDelivery, -1, Calendar.NYB);
              depositAccrualStart = lastDelivery;
              depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.SYB);
              break;
            default:
              // OIS Futures
              contractSize = 1000000;
              tickSize = tenor.N < 4 && tenor.Units == TimeUnit.Months ? 0.25 / 1e4 : 0.500 / 1e4;
              tickValue = tenor.N < 4 && tenor.Units == TimeUnit.Months ? 12.5 : 50;
              lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.ThirdWednesday, BDConvention.Following, Calendar.NYB);
              lastTrading = Dt.AddDays(lastDelivery, -1, Calendar.NYB);
              depositAccrualStart = lastDelivery;
              depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.NYB);
              break;
          }
          break;
        case RateFutureType.MoneyMarketCashRate:
        default:
          switch (ccy)
          {
            case Currency.EUR:
              // LIFFE or EUREX 3M Euribor Futures
              contractSize = 1000000;
              tickSize = 0.5 / 1e4;
              tickValue = 12.5;
              break;
            case Currency.GBP:
              // LIFFE 3M Short Sterling Futures
              contractSize = 500000;
              tickSize = 1.0 / 1e4;
              tickValue = 12.5;
              break;
            case Currency.JPY:
              // LIFFE 3M Euroyen Futures
              contractSize = 100000000;
              tickSize = 0.5 / 1e4;
              tickValue = 1250.0;
              break;
            case Currency.CHF:
              // LIFFE 3M Euroswiss Futures
              contractSize = 1000000;
              tickSize = 1.0 / 1e4;
              tickValue = 25.0;
              break;
            case Currency.USD:
            default:
              if (tenor.Units == TimeUnit.Months && tenor.N < 3)
              {
                // CME 1M ED Futures
                contractSize = 3000000;
                tickSize = 0.25 / 1e4;
                tickValue = 6.25;
              }
              else
              {
                // CME 3M ED Futures
                contractSize = 1000000;
                tickSize = 0.25 / 1e4;
                tickValue = 6.25;
              }
              break;
          }
          lastDelivery = Dt.DayOfMonth(month, year, DayOfMonth.ThirdWednesday, BDConvention.Following, Calendar.LNB);
          lastTrading = Dt.AddDays(lastDelivery, -2, Calendar.LNB);
          depositAccrualStart = lastDelivery;
          depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.LNB);
          break;
      }
    }

    #endregion
  }
}
