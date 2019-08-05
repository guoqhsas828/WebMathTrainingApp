using System;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 1010)]
  [Product(typeof(GenericProduct))]
  public class GenericTrade : Trade, IRiskyCounterparty
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    protected GenericTrade()
    {
      Product = ClassCache.CreateInstance<GenericProduct>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="generateTradeId"></param>
    public override void InitializeDefaultValues(bool generateTradeId)
    {
      base.InitializeDefaultValues(generateTradeId);
      Amount = 1.0;
      Product.Name = TradeId;
      Settle = Product.CalcSettle(Traded);
      Product.Effective = Settle;
    }

    #endregion
  }
}