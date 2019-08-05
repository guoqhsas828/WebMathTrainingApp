//
// StockTrade.cs
//
using System;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Risk
{
  /// <summary>
  /// <seealso cref="Stock"/> trade
  /// </summary>
  /// <inheritdoc cref="Stock" select="remarks|seealso" />
  [Serializable]
  [Entity(EntityId = 917, Description = "The purchase or sale of an individual stock product")]
  [Product(typeof(Stock))]
  public class StockTrade : Trade
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public StockTrade()
    {
      Product = new Stock();
    }

    /// <inheritdoc cref="Trade.Clone()"/>
    public override object Clone()
    {
      var trade = (StockTrade) base.Clone();
      trade.Product = Product;
      return trade;
    }

    /// <inheritdoc cref="Trade.InitializeDefaultValues(bool)"/>
    public override void InitializeDefaultValues(bool generateTradeId)
    {
      base.InitializeDefaultValues(generateTradeId);

      var stock = (Stock) Product;

      Settle = Dt.AddDays(Traded,
                          stock.DaysToSettle,
                          Calendar.NYB);
    }

    /// <inheritdoc cref="Trade.ComputeTradePayment(string,out string)"/>
    public override Payment ComputeTradePayment(string calcEnvName, out string eMess)
    {
      eMess = string.Empty;
      string[] tradedLevelTypesSupported = { "Stock Price" };
      if (TradedLevelType == null || Array.IndexOf(tradedLevelTypesSupported, TradedLevelType.Name) < 0)
      {
        eMess = (tradedLevelTypesSupported.Length == 1 ? "Traded Level Type must be " : "Traded Level Type must be one of: ") + ProductUtil.AsCommaSeparatedList(tradedLevelTypesSupported);
        return null; // Right now support computing trade payment only if quoted in clean price.
      }
      if (TradedLevel <= 0.0)
      {
        eMess = "Invalid Traded Level";
        return null; // Can not compute payment if the price has not been set yet.
      }
      double paymentAmt = TradedLevel * Amount;
      var pmt = new BasicPayment(Settle, paymentAmt, Currency);
      return pmt;
    }

    /// <summary> Is trade payment computation supported for this type of trade? </summary>
    public override bool CanComputeTradePayment
    {
      get { return true; }
    }
  }
}