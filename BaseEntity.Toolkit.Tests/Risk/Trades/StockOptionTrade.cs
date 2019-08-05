//
// StockOptionTrade.cs
//
using System;
using BaseEntity.Database;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Risk
{
  /// <summary>
  /// <seealso cref="StockOption"/> trade
  /// </summary>
  /// <inheritdoc cref="StockOption" select="remarks|seealso" />
  [Serializable]
  [Entity(EntityId = 918, Description = "The purchase or sale of an individual exchange-traded stock option product")]
  [Product(typeof(StockOption))]
  public class StockOptionTrade : Trade
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public StockOptionTrade()
    {
      Product = new StockOption();
    }

    /// <summary>
    /// Enables computing the trade payment corresponding to the Amount, Traded Level
    /// and Notional for the stock option
    /// </summary>
    public override bool CanComputeTradePayment
    {
      get { return true; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="calcEnvName"></param>
    /// <param name="eMess"></param>
    /// <returns></returns>
    public override Payment ComputeTradePayment(string calcEnvName, out string eMess)
    {
      eMess = string.Empty;

      string[] tradedLevelTypesSupported = { "Stock Option Price" };

      if (Product == null)
      {
        eMess = "Select a Stock Option to calculate payment";
        return null;
      }

      //Product is a default new object before a valid Stock Option is selected for the trade
      if (Product.IsNewObject())
        return new BasicPayment(Settle, 0, Currency);

      if (TradedLevelType == null || Array.IndexOf(tradedLevelTypesSupported, TradedLevelType.Name) < 0)
      {
        eMess = "Payment calculation supports Stock Option Price Traded Level Type Only";
        return null;
      }
      
      var payment = Amount * TradedLevel * Product.Notional;
      return new BasicPayment(Settle, payment, Currency);
    }

    /// <inheritdoc cref="Trade.Clone()"/>
    public override object Clone()
    {
      var trade = (StockOptionTrade)base.Clone();
      trade.Product = this.Product;
      return trade;
    }

    /// <inheritdoc cref="Trade.InitializeDefaultValues(bool)"/>
    public override void InitializeDefaultValues(bool generateTradeId)
    {
      base.InitializeDefaultValues(generateTradeId);
      var stockOption = (StockOption)Product;
      Settle = Dt.AddDays(Traded,
                          stockOption.DaysToSettle,
                          Calendar.NYB);
      stockOption.Effective = Dt.Empty;
      stockOption.Notional = 100.0;
    }

  }
}