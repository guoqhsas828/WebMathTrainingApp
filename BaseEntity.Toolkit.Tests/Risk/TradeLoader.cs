using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Core.Logging;
using BaseEntity.Database;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;
using log4net;
using NHibernate;
using NHibernate.Criterion;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class TradeLoader
  {
    #region Constants and Fields

    private const int BatchSize = 2000;

    private static readonly ILog Logger = QLogManager.GetLogger(typeof(TradeLoader));

    private ProductFetchStrategy _productFetchStrategy = ProductFetchStrategy.Shallow;

    #endregion

    #region Public Properties

    /// <summary>
    /// Indicates whether and how to eager fetch products for the trades that are loaded
    /// </summary>
    public ProductFetchStrategy ProductFetchStrategy
    {
      get { return _productFetchStrategy; }
      set { _productFetchStrategy = value; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public IList<Trade> GetTrades(IEnumerable ids)
    {
      return GetTrades(ids.Cast<long>());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public IList<Trade> GetTrades(IEnumerable<long> ids)
    {
      return LoadTrades(new List<long>(ids));
    }

    /// <summary>
    /// Load trades based on filter criteria
    /// </summary>
    public IList<Trade> GetTrades(Dt asOf, TradeFilter tradeFilter)
    {
      var filter = new ParsedTradeFilter(tradeFilter);

      if (!filter.IsValid)
      {
        throw new ArgumentException("Invalid TradeFilter");
      }

      return LoadNormal(asOf, filter);
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
    //  return GetTrades(new Dt(run.RunId.Date), filter);
    //}

    #endregion

    #region Methods

    private static DetachedCriteria CreateFilterCriteria(Dt asOf, ParsedTradeFilter filter)
    {
      var criteria = DetachedCriteria.For<Trade>();

      if (asOf == Dt.Today())
      {
        if (!filter.IncludePending)
        {
          criteria.Add(Restrictions.Not(Restrictions.Eq("TradeStatus", TradeStatus.Pending)));
        }

        if (!filter.IncludeWhatIf)
        {
          criteria.Add(Restrictions.Not(Restrictions.Eq("TradeStatus", TradeStatus.Whatif)));
        }

        if (!asOf.IsEmpty())
        {
          criteria.Add(Restrictions.Le("Traded", asOf));
        }

        if (filter.Strategies.Count > 0)
        {
          criteria.CreateAlias("Strategy", "s");
          if (filter.ExcludeStrategies)
            criteria.Add(Restrictions.Or(Restrictions.IsNull("s.Name"), Restrictions.Not(Restrictions.In("s.Name", filter.Strategies))));
          else
            criteria.Add(Restrictions.In("s.Name", filter.Strategies));
        }

        if (filter.Substrategies.Count > 0)
        {
          criteria.CreateAlias("SubStrategy", "ss");
          if (filter.ExcludeSubstrategies)
            criteria.Add(Restrictions.Or(Restrictions.IsNull("ss.Name"), Restrictions.Not(Restrictions.In("ss.Name", filter.Substrategies))));
          else
            criteria.Add(Restrictions.In("ss.Name", filter.Substrategies));
        }

        if (filter.Counterparties.Count > 0)
        {
          criteria.CreateAlias("Counterparty", "c");
          if (filter.ExcludeCounterparties)
            criteria.Add(Restrictions.Or(Restrictions.IsNull("c.Name"), Restrictions.Not(Restrictions.In("c.Name", filter.Counterparties))));
          else
            criteria.Add(Restrictions.In("c.Name", filter.Counterparties));
        }
      }

      if (filter.TradeIds.HasValues)
      {
        if (filter.ExcludeTradeIds)
          criteria.Add(Restrictions.Not(Restrictions.In("TradeId", filter.TradeIds)));
        else
          criteria.Add(Restrictions.In("TradeId", filter.TradeIds));
      }

      if (filter.Products.Count > 0)
      {
        var tradeEntityMap = GetTradeEntityMap();

        if (filter.ExcludeProducts)
        {
          var conjunction = Restrictions.Conjunction();
          foreach (string productTypeName in filter.Products)
          {
            var tradeEntity = tradeEntityMap[productTypeName];
            conjunction.Add(Restrictions.Not(Restrictions.Between("ObjectId", tradeEntity.MinObjectId, tradeEntity.MaxObjectId)));
          }
          criteria.Add(conjunction); 
        }
        else
        {
          var disjunction = Restrictions.Disjunction();
          foreach (string productTypeName in filter.Products)
          {
            ClassMeta tradeEntity;
            if (!tradeEntityMap.TryGetValue(productTypeName, out tradeEntity))
              throw new RiskException($"Invalid product type {productTypeName}");
            disjunction.Add(Restrictions.Between("ObjectId", tradeEntity.MinObjectId, tradeEntity.MaxObjectId));
          }
          criteria.Add(disjunction);
        }
      }

      return criteria;
    }

    private static IEnumerable<Trade> PostProcess(IEnumerable<Trade> trades, Dt asOf, ParsedTradeFilter filter)
    {
      foreach (var trade in trades)
      {
        if (!filter.IncludePending && trade.TradeStatus == TradeStatus.Pending)
        {
          continue;
        }

        if (!filter.IncludeWhatIf && trade.TradeStatus == TradeStatus.Whatif)
        {
          continue;
        }

        // We filter based on ValidFrom here because it is possible for the initial Trade version to
        // have ValidFrom in the future and the rollback mechanism does not currently support evicting 
        // these Trades from the Session.
        if (trade.Traded > asOf || trade.ValidFrom > asOf)
        {
          continue;
        }

        if (filter.Strategies.Count > 0)
        {
          if (filter.ExcludeStrategies)
          {
            if (trade.Strategy != null && filter.Strategies.Contains(trade.Strategy.Name))
            continue;
          }
          else if (trade.Strategy == null || !filter.Strategies.Contains(trade.Strategy.Name))
            continue;
        }

        if (filter.Substrategies.Count > 0)
        {
          if (filter.ExcludeSubstrategies)
          {
            if (trade.SubStrategy != null && filter.Substrategies.Contains(trade.SubStrategy.Name))
              continue;
          }
          else if (trade.SubStrategy == null || !filter.Substrategies.Contains(trade.SubStrategy.Name))
            continue;
        }

        if (filter.Counterparties.Count > 0)
        {
          if (filter.ExcludeCounterparties)
          {
            if (trade.Counterparty != null && filter.Counterparties.Contains(trade.Counterparty.Name))
              continue;
          }
          else if (trade.Counterparty == null || !filter.Counterparties.Contains(trade.Counterparty.Name))
            continue;
        }

        if (filter.BookingEntities.Count > 0)
        {
          if (filter.ExcludeBookingEntities)
          {
            if (trade.BookingEntity != null && filter.BookingEntities.Contains(trade.BookingEntity.Name))
              continue;
          }
          else if (trade.BookingEntity == null || !filter.BookingEntities.Contains(trade.BookingEntity.Name))
            continue;
        }

        yield return trade;
      }
    }

    private static IEnumerable<KeyValuePair<Type, HashSet<long>>> GetProductIdsByType(IEnumerable trades)
    {
      var productIdsByType = new Dictionary<Type, HashSet<long>>();

      foreach (Trade trade in trades)
      {
        long productId = trade.ProductId;
        Type productType = EntityHelper.GetClassFromObjectId(productId);

        HashSet<long> productIds;
        if (!productIdsByType.TryGetValue(productType, out productIds))
        {
          productIds = new HashSet<long>(new[] {productId});
          productIdsByType[productType] = productIds;
        }

        productIds.Add(productId);
      }

      return productIdsByType;
    }

    private static Type GetProductType(Type tradeType)
    {
      return TradeProductOwnershipResolver.GetProductAttribute(tradeType).ProductType;
    }

    // Get map of trade type by product type
    private static IDictionary<string, ClassMeta> GetTradeEntityMap()
    {
      var results = new Dictionary<string, ClassMeta>();
      var tradeTypes = ClassCache.FindAll().Where(cm => cm.IsEntity && cm.IsDerivedEntity && cm.BaseEntity.Name == "Trade").Select(cm => cm.Type);
      foreach (var tradeType in tradeTypes)
      {
        Type productType = GetProductType(tradeType);
        var productEntity = ClassCache.Find(productType);
        var tradeEntity = ClassCache.Find(tradeType);
        results[productEntity.Name] = tradeEntity;
      }
      return results;
    }

    // Return unique set of Product types for the referenced trades
    private static IEnumerable<Type> GetTradeTypes(IEnumerable trades)
    {
      var results = new HashSet<Type>();
      foreach (var trade in trades)
      {
        results.Add(trade.GetType());
      }
      return results;
    }

    private IList<Trade> LoadNormal(Dt asOf, ParsedTradeFilter tradeFilter)
    {
      // Load trades
      DetachedCriteria filterCriteria = CreateFilterCriteria(asOf, tradeFilter);
      var tradeCriteria = new CriteriaProxy(filterCriteria.GetExecutableCriteria(SessionBinder.GetCurrentSession()), Session.EntityContext.Interceptor);
      IEnumerable<Trade> trades = tradeCriteria.List<Trade>();

      // Perform additional filtering in-memory
      trades = PostProcess(trades, asOf, tradeFilter);

      if (_productFetchStrategy != ProductFetchStrategy.None)
      {
        // Load products
        filterCriteria.SetProjection(Projections.Property("Product"));
        foreach (var tradeType in GetTradeTypes(trades))
        {
          var productType = TradeProductOwnershipResolver.GetProductAttribute(tradeType).ProductType;
          var productCriteria = Session.CreateCriteria(productType).Add(Subqueries.PropertyIn("ObjectId", filterCriteria));
          if (_productFetchStrategy == ProductFetchStrategy.Deep)
          {
            var cm = ClassCache.Find(productType);
            foreach (var cascade in cm.CascadeList.Where(c => c.Fetch == "join"))
            {
              productCriteria.SetFetchMode(cascade.Name, FetchMode.Join);
            }
          }
          productCriteria.List();
        }
      }

      return trades.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="productType"></param>
    /// <param name="ids"></param>
    private void LoadProductsByType(Type productType, HashSet<long> ids)
    {
      Logger.DebugFormat("ENTER LoadProductsByType|{0}|{1}", productType.Name, ids.Count);

      var idList = new List<long>(ids);

      var cm = ClassCache.Find(productType);

      int startIdx = 0;
      while (startIdx < idList.Count)
      {
        int span = Math.Min(BatchSize, idList.Count - startIdx);

        ICriteria crit = Session.CreateCriteria(productType);
        crit.Add(Restrictions.In("ObjectId", idList.GetRange(startIdx, span)));
        if (_productFetchStrategy == ProductFetchStrategy.Deep)
        {
          foreach (var cascade in cm.CascadeList.Where(c => c.Fetch == "join"))
          {
            crit.SetFetchMode(cascade.Name, FetchMode.Join);
          }
        }
        crit.List();

        startIdx += span;
      }

      Logger.DebugFormat("EXIT LoadProductsByType|{0}|{1}", productType.Name, ids.Count);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    private IList<Trade> LoadTrades(List<long> ids)
    {
      Logger.DebugFormat("ENTER LoadTrades|{0}", ids.Count);

      var trades = new List<Trade>();

      int startIdx = 0;
      while (startIdx < ids.Count)
      {
        int span = Math.Min(BatchSize, ids.Count - startIdx);

        ICriteria crit = Session.CreateCriteria(typeof(Trade));
        crit.Add(Restrictions.In("ObjectId", ids.GetRange(startIdx, span)));
        trades.AddRange(crit.List().Cast<Trade>());
        startIdx += span;
      }

      if (_productFetchStrategy != ProductFetchStrategy.None)
      {
        foreach (var kvp in GetProductIdsByType(trades))
        {
          LoadProductsByType(kvp.Key, kvp.Value);
        }
      }

      Logger.DebugFormat("EXIT LoadTrades|{0}", ids.Count);

      return trades;
    }

    #endregion

    // Return the Product type for this Trade type
  }
}