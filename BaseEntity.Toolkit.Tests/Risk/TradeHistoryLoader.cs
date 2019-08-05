using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseEntity.Database;
using BaseEntity.Metadata;
using log4net;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class TradeHistoryLoader : IDisposable
  {
    #region Constants and Fields

    private static readonly ILog Logger = LogManager.GetLogger(typeof(TradeHistoryLoader));

    private static readonly IDictionary<string, MethodInfo> QueryMap = GetQueryMap();

    private static readonly IDictionary<string, ClassMeta> ProductTradeMap = GetProductTradeMap();

    private EntityHistoryValidFromContext _historyContext;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public TradeHistoryLoader(DateTime asOf)
    {
      _historyContext = new EntityHistoryValidFromContext(asOf, true);
    }

    #endregion

    #region Implementation of IDisposable

    /// <summary>
    /// Disposes the underlying EntityHistoryValidFromContext
    /// </summary>
    public void Dispose()
    {
      if (_historyContext != null)
      {
        _historyContext.Dispose();
        _historyContext = null;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// 
    /// </summary>
    public DateTime AsOf
    {
      get { return _historyContext.ValidFrom; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Load trades based on filter criteria
    /// </summary>
    public IList<Trade> GetTrades(TradeFilter filter)
    {
      var parsedFilter = new ParsedTradeFilter(filter);

      if (!parsedFilter.IsValid)
      {
        throw new ArgumentException("Invalid TradeFilter");
      }

      return LoadTrades(AsOf, parsedFilter);
    }

    ///// <summary>
    ///// Gets the trades (from risk run criteria).
    ///// </summary>
    ///// <param name="run">The run.</param>
    ///// <returns></returns>
    //public IList<Trade> GetTrades(RiskRunDto run)
    //{
    //  if (run == null)
    //  {
    //    throw new ArgumentNullException("run");
    //  }

    //  if (run.RunId == null)
    //  {
    //    throw new ArgumentException("RunId");
    //  }

    //  if (run.RunId.Date != AsOf)
    //  {
    //    throw new ArgumentException(string.Format(
    //      "RiskRun date [{0}] does not match TradeLoaderEx date [{1}]", run.RunId.Date, AsOf));
    //  }

    //  var filter = new TradeFilter
    //  {
    //    TradeIds = run.TradeIds,
    //    Strategies = run.Strategies,
    //    Substrategies = run.Substrategies,
    //    Products = run.Products,
    //    Counterparties = run.Counterparties,
    //    IncludePending = run.IncludePending,
    //    IncludeWhatIf = run.IncludeWhatIf,
    //    ExcludeCounterparties = run.ExcludeCounterparties,
    //    ExcludeProducts = run.ExcludeProducts,
    //    ExcludeStrategies = run.ExcludeStrategies,
    //    ExcludeSubstrategies = run.ExcludeSubstrategies,
    //    ExcludeTradeIds = run.ExcludeTradeIds,
    //    BookingEntities = run.BookingEntities,
    //    ExcludeBookingEntities = run.ExcludeBookingEntities
    //  };

    //  return GetTrades(filter);
    //}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public IList<Trade> GetTrades(IEnumerable<long> ids)
    {
      if (ids == null)
      {
        throw new ArgumentNullException("ids");
      }

      var trades = new List<Trade>();
      foreach (var g in ids.GroupBy(EntityHelper.GetClassFromObjectId))
      {
        var type = g.Key;
        if (!typeof(Trade).IsAssignableFrom(type))
        {
          throw new ArgumentException(string.Format("{0} is not a valid Trade type", type));
        }

        trades.AddRange(g.Select(id => _historyContext.Get<Trade>(id)));
      }
      return trades;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Trade GetTrade(long id)
    {
      var type = EntityHelper.GetClassFromObjectId(id);

      if (!typeof(Trade).IsAssignableFrom(type))
      {
        throw new ArgumentException(string.Format("{0} is not a valid Trade type", type));
      }

      return _historyContext.Get<Trade>(id);
    }

    #endregion

    #region Helper Methods

    private static Type GetProductType(Type tradeType)
    {
      return TradeProductOwnershipResolver.GetProductAttribute(tradeType).ProductType;
    }

    // Get map of trade type by product type
    private static IDictionary<string, ClassMeta> GetProductTradeMap()
    {
      var results = new Dictionary<string, ClassMeta>();
      var tradeTypes = ClassCache.FindAll().Where(cm => cm.IsDerivedEntity && cm.BaseEntity.Name == "Trade").Select(cm => cm.Type);
      foreach (var tradeType in tradeTypes)
      {
        Type productType = GetProductType(tradeType);
        var productEntity = ClassCache.Find(productType);
        var tradeEntity = ClassCache.Find(tradeType);
        results[productEntity.Name] = tradeEntity;
      }
      return results;
    }

    private static IDictionary<string, MethodInfo> GetQueryMap()
    {
      return ClassCache.FindAll().Where(cm => cm.IsDerivedEntity && cm.BaseEntity.Name == "Trade")
        .ToDictionary(cm => cm.Name, cm => typeof(EntityHistoryValidFromContext).GetMethod("Query").MakeGenericMethod(cm.Type));
    }

    private static bool PostProcess(Trade trade, Dt asOf, ParsedTradeFilter filter)
    {
      if (!filter.IncludeWhatIf && trade.TradeStatus == TradeStatus.Whatif)
      {
        return false;
      }

      if (!filter.IncludePending && trade.TradeStatus == TradeStatus.Pending)
      {
        return false;
      }

      // We filter based on ValidFrom here because it is possible for the initial Trade version to
      // have ValidFrom in the future and the rollback mechanism does not currently support evicting 
      // these Trades from the Session.
      if (trade.Traded > asOf || trade.ValidFrom > asOf)
      {
        return false;
      }

      if (filter.TradeIds.Count > 0)
      {
        if (filter.ExcludeTradeIds)
        {
          if (trade.TradeId != null && filter.TradeIds.Contains(trade.TradeId))
            return false;
        }
        else if (trade.TradeId == null || !filter.TradeIds.Contains(trade.TradeId))
          return false;
      }

      if (filter.Products.Count > 0)
      {
        var productMeta = trade.Product == null ? null : ClassCache.Find(trade.Product);

        if (filter.ExcludeProducts)
        {
          if (productMeta != null && filter.Products.Contains(productMeta.Name))
            return false;
        }
        else if (productMeta == null || !filter.Products.Contains(productMeta.Name))
          return false;
      }

      if (filter.Strategies.Count > 0)
      {
        if (filter.ExcludeStrategies)
        {
          if (trade.Strategy != null && filter.Strategies.Contains(trade.Strategy.Name))
          return false;
        }
        else if (trade.Strategy == null || !filter.Strategies.Contains(trade.Strategy.Name))
          return false;
      }

      if (filter.Substrategies.Count > 0)
      {
        if (filter.ExcludeSubstrategies)
        {
          if (trade.SubStrategy != null && filter.Substrategies.Contains(trade.SubStrategy.Name))
            return false;
        }
        else if (trade.SubStrategy == null || !filter.Substrategies.Contains(trade.SubStrategy.Name))
          return false;
      }

      if (filter.Counterparties.Count > 0)
      {
        if (filter.ExcludeCounterparties)
        {
          if (trade.Counterparty != null && filter.Counterparties.Contains(trade.Counterparty.Name))
            return false;
        }
        else if (trade.Counterparty == null || !filter.Counterparties.Contains(trade.Counterparty.Name))
          return false;
      }

      if (filter.BookingEntities.Count > 0)
      {
        if (filter.ExcludeBookingEntities)
        {
          if (trade.BookingEntity != null && filter.BookingEntities.Contains(trade.BookingEntity.Name))
            return false;
        }
        else if (trade.BookingEntity == null || !filter.BookingEntities.Contains(trade.BookingEntity.Name))
          return false;
      }

      return true;
    }

    private IList<Trade> LoadTrades(Dt asOf, ParsedTradeFilter filter)
    {
      var trades = new List<Trade>();

      if (filter.ExcludeProducts || filter.Products.Count == 0 || filter.Products.Count > 3)
      {
        trades.AddRange(_historyContext.Query<Trade>().Where(trade => PostProcess(trade, asOf, filter)).ToList());
      }
      else
      {
        foreach (var p in filter.Products)
        {
          ClassMeta cm;
          if (!ProductTradeMap.TryGetValue(p, out cm))
          {
            throw new ArgumentException("Invalid ProductType [" + p + "]");
          }

          var queryMethod = QueryMap[cm.Name];
          var productTrades = ((IEnumerable<Trade>)queryMethod.Invoke(_historyContext, null)).Where(trade => PostProcess(trade, asOf, filter));
          trades.AddRange(productTrades);
        }
      }

      return trades;
    }

    #endregion
  }
}