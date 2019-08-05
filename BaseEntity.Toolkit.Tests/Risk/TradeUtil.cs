using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using BaseEntity.Database;
using BaseEntity.Metadata;
using BaseEntity.Risk.Trading;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Util;

namespace BaseEntity.Risk
{
  /// <summary>
  /// </summary>
  public class TradeUtil : ObjectFactory<Trade>
  {
    #region Query

    /// <summary>
    ///   Get trade by id
    /// </summary>
    ///
    /// <param name="id">Id for trade to retrieve</param>
    ///
    /// <returns>Trade matching specified id</returns>
    ///
    public static Trade FindById(Int64 id)
    {
      return (Trade)Session.Get(typeof(Trade), id);
    }

    /// <summary>
    /// </summary>
    public static Trade FindByTradeId(string tradeId)
    {
      IList list = Session.Find("from Trade t where t.TradeId = ?", tradeId, ScalarType.String);
      if (list.Count == 0)
        return null;
      if (list.Count == 1)
        return (Trade)list[0];
      throw new DatabaseException("More than one trade with id " + tradeId + " found in database");
    }

    /// <summary>
    /// Find all trades traded on the specified date
    /// </summary>
    public static IList FindByTradeDate(DateTime tradeDate)
    {
      var fromDt = new DateTime(tradeDate.Year, tradeDate.Month, tradeDate.Day);
      DateTime toDt = fromDt.AddDays(1);

      return Session.CreateQuery("FROM Trade WHERE Traded >= :fromDt AND Traded < :toDt")
        .SetParameter("fromDt", fromDt)
        .SetParameter("toDt", toDt)
        .List();
    }

    /// <summary>
    ///   Load all trades in the specified strategy
    /// </summary>
    ///
    /// <param name="strategyName">Strategy name</param>
    ///
    /// <returns>List of matching trades</returns>
    ///
    public static IList FindByStrategy(string strategyName)
    {
      return Session.CreateQuery("FROM Trade trade WHERE trade.Strategy.Name = :strategyName")
        .SetParameter("strategyName", strategyName)
        .List();
    }

    /// <summary>
    ///   Load trades matching the specified strategy and product type
    /// </summary>
    ///
    /// <param name="strategyName">Strategy name</param>
    /// <param name="productType">Type of product</param>
    ///
    /// <returns>List of matching trades</returns>
    ///
    public static IList FindByStrategyAndProduct(string strategyName, Type productType)
    {
      string sql = String.Format("FROM Trade t WHERE t.Strategy.Name={0} AND t.Product IN (select p.ObjectId FROM {1} p)",
        StringUtil.GetSqlSafeString(strategyName), productType.Name);
      return Session.Find(sql);
    }

    /// <summary>
    ///   Load trades matching the specified strategy and product type
    /// </summary>
    ///
    /// <param name="productType">Type of product</param>
    /// <returns>List of matching trades</returns>
    ///
    public static IList FindByProduct(Type productType)
    {
      return Session.Find(String.Format("FROM Trade t WHERE t.Product IN (select p.ObjectId FROM {0} p)", productType.Name));
    }

    /// <summary>
    ///  Load all trades
    /// </summary>
    ///
    /// <returns>List of all trades</returns>
    ///
    public static IList FindAll()
    {
      return Session.Find("from Trade t");
    }

    /// <summary>
    ///   Returns a list of all the active trades on the asOf date.
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="tradeIdList"></param>
    /// <param name="strategyNames"></param>
    /// <param name="substrategyNames"></param>
    /// <param name="productNames"></param>
    /// <param name="productTypes"></param>
    /// <param name="includePending"></param>
    /// <returns></returns>
    public static IList FindAllActiveTrades(Dt asOf, List<string> tradeIdList, List<string> strategyNames, List<string> substrategyNames,
      List<string> productNames, List<string> productTypes, bool includePending)
    {
      return FindAllActiveTrades(asOf, asOf, tradeIdList, strategyNames, substrategyNames, productNames, productTypes, includePending);
    }

    /// <summary>
    ///   Returns a list of all the active trades in the given date range
    /// </summary>
    /// <param name="fromDate">Date to filter out all matured/terminated trades</param>
    /// <param name="toDate">Date to filter out trades based on Traded date.</param>
    /// <param name="tradeIdList">List of TradeId's to filter on</param>
    /// <param name="strategyNames">List of Strategy Names to filter on</param>
    /// <param name="substrategyNames">List of Sub-strategy Names to filter on</param>
    /// <param name="productNames">List of Product Names to filter on</param>
    /// <param name="productTypes">List of Product Types to filter on</param>
    /// <param name="includePending">Whether or not to include Trades in Pending state.</param>
    /// <returns></returns>
    public static IList FindAllActiveTrades(Dt fromDate, Dt toDate, List<string> tradeIdList, List<string> strategyNames, List<string> substrategyNames,
      List<string> productNames, List<string> productTypes, bool includePending)
    {
      var excludedTradeStatuses = includePending ? new TradeStatus[0] : new[] {TradeStatus.Pending};

      return FindAllActiveTrades(
        fromDate,
        toDate,
        tradeIdList,
        strategyNames,
        substrategyNames,
        productNames,
        productTypes,
        excludedTradeStatuses);
    }

    /// <summary>
    ///   Returns a list of all the active trades in the given date range
    /// </summary>
    /// <param name="fromDate">Date to filter out all matured/terminated trades</param>
    /// <param name="toDate">Date to filter out trades based on Traded date.</param>
    /// <param name="tradeIdList">List of TradeId's to filter on</param>
    /// <param name="strategyNames">List of Strategy Names to filter on</param>
    /// <param name="substrategyNames">List of Sub-strategy Names to filter on</param>
    /// <param name="productNames">List of Product Names to filter on</param>
    /// <param name="productTypes">List of Product Types to filter on</param>
    /// <param name="excludedTradeStatuses">If a non-empty list is provided, any trade that is Canceled or whose TradeStatus is in the list will be excluded, otherwise only Canceled trades will be excluded</param>
    /// <returns></returns>
    public static IList FindAllActiveTrades(Dt fromDate, Dt toDate, List<string> tradeIdList, List<string> strategyNames, List<string> substrategyNames,
      List<string> productNames, List<string> productTypes, IList<TradeStatus> excludedTradeStatuses)
    {
      ICriteria criteria = Session.CreateCriteria(typeof(Trade));
      criteria.Add(Restrictions.Or(Restrictions.IsNull("Traded"), Restrictions.Le("Traded", toDate)));
      criteria.Add(Restrictions.Or(Restrictions.IsNull("Termination"), Restrictions.Ge("Termination", fromDate)));

      ICriteria prodCriteria = criteria.CreateCriteria("Product");
      prodCriteria.Add(Restrictions.Or(Restrictions.IsNull("Maturity"), Restrictions.Ge("Maturity", fromDate)));

      criteria.Add(
        Restrictions.Not(Restrictions.In("TradeStatus", new[] {TradeStatus.Canceled}.Union(excludedTradeStatuses ?? new TradeStatus[0]).Distinct().ToArray())));

      if (tradeIdList != null && tradeIdList.Count > 0)
        criteria.Add(Restrictions.In("TradeId", tradeIdList));

      if (strategyNames != null && strategyNames.Count > 0)
        criteria.CreateCriteria("Strategy").Add(Restrictions.In("Name", strategyNames));

      if (substrategyNames != null && substrategyNames.Count > 0)
        criteria.CreateCriteria("SubStrategy").Add(Restrictions.In("Name", substrategyNames));

      if (productNames != null && productNames.Count > 0)
        prodCriteria.Add(Restrictions.In("Name", productNames));

      if (productTypes != null && productTypes.Count > 0)
      {
        var dc = Restrictions.Disjunction();
        foreach (string typeName in productTypes)
        {
          dc.Add(Expression.Sql("{alias}.ObjectId in (Select ObjectId from " + typeName + ")"));
        }
        prodCriteria.Add(dc);
      }

      criteria.AddOrder(Order.Asc("TradeId"));

      return criteria.List();
    }

    /// <summary>
    /// Checks if a trade implements the IRiskyCounterparty interface
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public static bool IsIRiskyCounterpartyTrade(Trade trade)
    {
      return IsIRiskyCounterpartyTrade(trade.GetType());
    }

    /// <summary>
    /// Checks if a trade implements the IRiskyCounterparty interface
    /// </summary>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public static bool IsIRiskyCounterpartyTrade(Type tradeType)
    {
      return tradeType.GetInterfaces().Contains(typeof(IRiskyCounterparty));
    }

    #endregion Query

    #region Methods

    /// <summary>
    ///   Net trades
    /// </summary>
    ///
    /// <param name="trades">List of trades</param>
    ///
    /// <returns>Netted list of trades</returns>
    ///
    public static IList NetTrades(IList trades)
    {
      IList nettedTrades = new ArrayList();
      for (int i = 0; i < trades.Count; i++)
      {
        Trade t1 = (Trade)trades[i];
        if (t1.Amount != 0.0)
        {
          // Net this trade as not already done
          for (int j = i + 1; j < trades.Count; j++)
          {
            Trade t2 = (Trade)trades[j];
            if (t1.Product == t2.Product &&
                t1.Strategy == t2.Strategy &&
                t1.Counterparty == t2.Counterparty &&
                t1.Trader == t2.Trader)
            {
              // Calculate weighted average price
              t1.Payment = (t1.Payment * t1.Amount + t2.Payment * t2.Amount) / t1.Amount;
              // Net position
              t1.Amount += t2.Amount;
              // Mark this as netted
              t2.Amount = 0.0;
            }
          }
          nettedTrades.Add(t1);
        }
      }

      return nettedTrades;
    }

    /// <summary>
    /// Get a list of all the ClassMeta's for trades 
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<ClassMeta> GetAllTradeMetas()
    {
      var validProductTypes = new HashSet<Type>(); //PricerUtil.GetAllPricerProductTypes()
      return ClassCache.FindAll().Where(m => m.IsA(typeof(Trade)) &&
                                             !m.Type.IsAbstract &&
                                             validProductTypes.Contains(GetProductType(m.Type)));
    }

    /// <summary>
    /// Given a trade data type return its product data type
    /// </summary>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public static Type GetProductType(Type tradeType)
    {
      return ((ProductAttribute)tradeType.GetCustomAttributes(typeof(ProductAttribute), false)[0]).ProductType;
    }

    /// <summary>
    ///   Returns the last updated DateTime for a given trade
    /// </summary>
    /// <param name="t"></param>
    /// <param name="includeDocumentation"></param>
    /// <returns></returns>
    public static DateTime GetLastUpdated(Trade t, bool includeDocumentation)
    {
      DateTime lastUpdated;
      User lastUpdatedBy;

      GetLastUpdatedInfo(t, includeDocumentation, out lastUpdated, out lastUpdatedBy);

      return lastUpdated;
    }

    /// <summary>
    ///   Returns the User who last updated a given trade
    /// </summary>
    /// <param name="t"></param>
    /// <param name="includeDocumentation"></param>
    /// <returns></returns>
    public static User GetLastUpdatedBy(Trade t, bool includeDocumentation)
    {
      DateTime lastUpdated;
      User lastUpdatedBy;

      GetLastUpdatedInfo(t, includeDocumentation, out lastUpdated, out lastUpdatedBy);

      return lastUpdatedBy;
    }

    /// <summary>
    ///  Gets the last updated datetime and the user who last updated a given trade.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="includeDocumentation"></param>
    /// <param name="lastUpdated"></param>
    /// <param name="lastUpdatedBy"></param>
    public static void GetLastUpdatedInfo(Trade t, bool includeDocumentation, out DateTime lastUpdated, out User lastUpdatedBy)
    {
      var walker = new OwnedOrRelatedObjectWalker();

      lastUpdated = t.LastUpdated;
      lastUpdatedBy = t.UpdatedBy;

      walker.Walk(t);

      foreach (var po in walker.OwnedObjects)
      {
        if (!includeDocumentation)
        {
          //if (po is TradeDocumentation)
          //  continue;
        }

        var ao = (po as AuditedObject);
        if (ao != null && ao.LastUpdated > lastUpdated)
        {
          lastUpdated = ao.LastUpdated;
          lastUpdatedBy = ao.UpdatedBy;
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="leadTrade"></param>
    /// <returns></returns>
    public static double GetMaxUnwindOrAssignAmount(Trade leadTrade)
    {
      var relatedTradesAmt = Session.CreateCriteria(typeof(Trade))
        .Add(Restrictions.Eq("LeadTrade", leadTrade))
        .Add(Restrictions.Not(Restrictions.Eq("TradeStatus", TradeStatus.Canceled)))
        .SetProjection(Projections.Sum("Amount"))
        .UniqueResult<double>();

      // This cast is required to avoid the rounding errors for doubles.
      // If you subtract a doube 0.1 from a double 0.9 you get back 0.09999999987
      // instead of 0.1. This casting will return 0.1
      return (double)(((decimal)leadTrade.Amount) + ((decimal)relatedTradesAmt)) * -1;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="leadTrade"></param>
    /// <param name="isAssign"></param>
    /// <param name="isTransient"></param>
    public static void MakeTradeAnAssignOrUnwind(Trade trade, Trade leadTrade, bool isAssign, bool isTransient = false)
    {
      if (isAssign && trade.Counterparty.ObjectId == leadTrade.Counterparty.ObjectId)
        throw new RiskException("Cannot assign trade to same counterparty");
      if (!isAssign && trade.Counterparty.ObjectId != leadTrade.Counterparty.ObjectId)
        throw new RiskException("Cannot unwind a trade with a different counterparty");

      if (!isTransient)
      {
        var context = new NHibernateEntityContext(ReadWriteMode.ReadOnly);
        using (new EntityContextBinder(context, true))
        {
          // Technically we do not need to edit
          var copy = (Trade)trade.Copy(new EntityContextEditorAdaptor(context));
          copy.LeadTrade = Session.Get<Trade>(leadTrade.ObjectId);
          copy.Product = copy.LeadTrade.Product;
          copy.TradeType = isAssign ? TradeType.Assign : TradeType.Unwind;

          var errors = new ArrayList();
          if (!copy.ValidateWithRelatedTrades(errors))
            throw new RiskException(errors.Cast<InvalidValue>().Select(_ => _.Message).Aggregate((cur, next) => cur + Environment.NewLine + next));
        }
      }
      trade.LeadTrade = leadTrade;
      trade.Product = leadTrade.Product;
      trade.TradeType = isAssign ? TradeType.Assign : TradeType.Unwind;

      // Copy Tags if specified
      CopyTagsToUnwindAssignTrade(trade);
    }

    /// <summary>
    ///   Copy Trade Tags with copyToUnwindTrade/copyToAssignTrade set to true 
    ///   in the TagDefinitions xml file from lead trade to unwind/assign trade
    /// </summary>
    /// <param name="unwindAssignTrade"></param>
    private static void CopyTagsToUnwindAssignTrade(Trade unwindAssignTrade)
    {
      EntityTagDefinition entityTagDef = TagDefinitionHandler.GetEntityTagDefinition(unwindAssignTrade.GetType());
      if (entityTagDef == null)
        return;

      Trade leadTrade = unwindAssignTrade.LeadTrade;
      if (leadTrade == null)
        return;
      bool isAssign = unwindAssignTrade.TradeType == TradeType.Assign;

      foreach (TagDefinition tagDef in entityTagDef.Tags)
      {
        bool copyTag = ((isAssign && tagDef.CopyToAssignTrade) || (!isAssign && tagDef.CopyToUnwindTrade));
        if (!copyTag)
          continue;

        Tag leadTradeTag = leadTrade.Tags.FirstOrDefault(t => t.Name == tagDef.Name);
        if (leadTradeTag == null)
          continue;

        Tag tradeTag = unwindAssignTrade.Tags.FirstOrDefault(t => t.Name == tagDef.Name);
        if (tradeTag == null)
        {
          tradeTag = (Tag)leadTradeTag.Clone();
          unwindAssignTrade.Tags.Add(tradeTag);
        }
        else
        {
          tradeTag.Value = leadTradeTag.Value;
        }
      }
    }

    /// <summary>
    /// Create an offsetting trade for a given tradeId
    /// Exception will be thrown if tradeid doesnt exist or if tradeId is already fully unwound
    /// The trade amount will default to a full unwind (taking in consideration any existing unwinds or assigns in the database)
    /// The trade counterparty will default to same as original trade so the trade type will default to TradeType.Unwind instead of partial or assign
    /// This will work on the current Session. It does not create a new one.
    /// This is also called internally from PerformUnwindOrAssign
    /// </summary>
    /// <param name="leadTrade">trade being unwound</param>
    /// <param name="isPersistent">Assign a TradeId</param>
    /// <param name="isAssign">Is Assignment</param>
    /// <returns>new trade not saved to any session with offsetting details for the tradeid passed in</returns>
    public static Trade CreateUnwindTrade(Trade leadTrade, bool isPersistent, bool isAssign)
    {
      Trade unwindTrade = CreateInstance(leadTrade.GetType());

      unwindTrade.Product = leadTrade.Product;

      unwindTrade.Traded = Dt.Today();
      unwindTrade.LeadTrade = leadTrade;
      unwindTrade.TradeId = isPersistent ? new TradeIdGenerator().Generate(unwindTrade) : GetNextTransientTradeId();
      unwindTrade.Settle = unwindTrade.Product.CalcSettle(unwindTrade.Traded);
      unwindTrade.Trader = EntityContextFactory.User;
      unwindTrade.TradedLevel = 0;
      unwindTrade.TradedLevelType = leadTrade.TradedLevelType;
      unwindTrade.Payment = 0;
      // following not perfect, but keeps longstanding behavior for cds (and similar), assuming standard product behavior, just for cds-like products.
      unwindTrade.PaymentSettle = CDSLikeProductTypes.Contains(unwindTrade.Product.GetType().Name)
        ? Dt.AddDays(unwindTrade.Traded, 3, unwindTrade.Product.Ccy == Currency.JPY ? Calendar.TKB : Calendar.None)
        : unwindTrade.Settle;
      unwindTrade.Counterparty = leadTrade.Counterparty;
      unwindTrade.TradeType = isAssign ? TradeType.Assign : TradeType.Unwind;
      unwindTrade.Strategy = leadTrade.Strategy;
      unwindTrade.SubStrategy = leadTrade.SubStrategy;
      unwindTrade.Broker = leadTrade.Broker;
      unwindTrade.BookingEntity = leadTrade.BookingEntity;
      unwindTrade.Deal = leadTrade.Deal;

      //Calculating the total amount of all existing unwinds or assigns in the database.
      double maxAllowedAmt = GetMaxUnwindOrAssignAmount(leadTrade);

      // Checking to see if the trade is already unwound.
      if (maxAllowedAmt.ApproximatelyEqualsTo(0.0))
        throw new Exception("Trade [" + leadTrade.TradeId + "] is already fully unwound.");

      unwindTrade.Amount = maxAllowedAmt;
      // Also copy some fields related to clearing:
      unwindTrade.IsCleared = leadTrade.IsCleared;
      unwindTrade.ClearingHouse = leadTrade.ClearingHouse;
      unwindTrade.UniqueProductIdentifier = leadTrade.UniqueProductIdentifier;
      unwindTrade.OriginalCounterparty = leadTrade.OriginalCounterparty;

      return unwindTrade;
    }

    /// <summary>
    /// Create an offsetting trade for a given tradeId
    /// Exception will be thrown if tradeid doesnt exist or if tradeId is already fully unwound
    /// The trade amount will default to a full unwind (taking in consideration any existing unwinds or assigns in the database)
    /// The trade counterparty will default to same as original trade so the trade type will default to TradeType.Unwind instead of partial or assign
    /// This will work on the current Session. It does not create a new one.
    /// This is also called internally from PerformUnwindOrAssign
    /// </summary>
    /// <param name="tradeId">trade being unwound</param>
    /// <param name="isPersistent">Assign a TradeId</param>
    /// <param name="isAssign"></param>
    /// <returns>new trade not saved to any session with offsetting details for the tradeid passed in</returns>
    public static Trade CreateUnwindTrade(string tradeId, bool isPersistent, bool isAssign)
    {
      Trade leadTrade = FindByTradeId(tradeId);
      if (leadTrade == null)
        throw new Exception("Cannot find trade id [" + tradeId + "]");

      return CreateUnwindTrade(leadTrade, isPersistent, isAssign);
    }

    /// <summary>
    /// Creates a new unwind or assign trade in the database.
    /// If counterparty parameter is different than the source trade counterparty then its an assignment
    /// This will use its own database session to save a new offsetting trade into the database
    /// The new trade is always saved as pending
    /// </summary>
    /// <param name="sourceTradeId">tradeid of trade to unwind</param>
    /// <param name="traded">traded date for new unwind trade</param>
    /// <param name="settle">settle date for new unwind trade</param>
    /// <param name="amount">amount for new unwind trade</param>
    /// <param name="tradedLevel">notional of new unwind trade</param>
    /// <param name="tradedLevelType">QuoteType name that describes the tradedLevel</param>
    /// <param name="payment">payment amount of new unwind trade</param>
    /// <param name="paymentSettle">payment settlement date</param>
    /// <param name="counterpartyName">counterparty of new unwind if different than source trade then an assignment is created</param>
    /// <param name="isAssign">Is Assign Trade</param>
    /// <returns>tradeid of newly created unwind</returns>
    public static Trade PerformUnwindOrAssign(string sourceTradeId, Dt traded, Dt settle, double amount,
      double tradedLevel, string tradedLevelType, double payment, Dt paymentSettle, string counterpartyName, bool isAssign)
    {
      // Find the source Trade
      Trade sourceTrade = FindByTradeId(sourceTradeId);

      if (sourceTrade == null)
        throw new Exception("Cannot find trade id [" + sourceTradeId + "]");

      // Create the offsetting trade
      Trade unwindTrade = CreateUnwindTrade(sourceTradeId, true, isAssign);

      // Set the unwind properties based on the parameters sent to this method
      unwindTrade.Traded = traded;
      unwindTrade.Settle = settle;
      unwindTrade.Amount = amount;
      unwindTrade.TradedLevel = tradedLevel;
      unwindTrade.TradedLevelType = QuoteTypeUtil.FindByName(tradedLevelType);
      unwindTrade.Payment = payment;
      unwindTrade.PaymentSettle = paymentSettle;
      unwindTrade.TradeStatus = TradeStatus.Pending;
      unwindTrade.InitialMargin = sourceTrade.InitialMargin;

      {
        unwindTrade.Counterparty = LegalEntityUtil.FindByName(counterpartyName);

        if (unwindTrade.Counterparty == null)
          throw new Exception("Cannot find counterparty [" + counterpartyName + "]");
      }

      // Copy Tags if specified
      CopyTagsToUnwindAssignTrade(unwindTrade);

      return unwindTrade;
    }

    /// <summary>
    /// Cancel a trade.
    /// This method will use its own database session to save the trade with a status of cancelled
    /// It will also check if there are any related trades (unwinds/assigns) and if so it will clear 
    /// their termination date if that date was set.
    /// </summary>
    /// <param name="tradeId"></param>
    public static void PerformCancel(string tradeId)
    {
      using (new SessionBinder(ReadWriteMode.Workflow))
      {
        Trade sourceTrade = FindByTradeId(tradeId);
        if (sourceTrade == null)
          throw new Exception("Cannot find trade id [" + tradeId + "]");

        sourceTrade.RequestUpdate();

        sourceTrade.TradeStatus = TradeStatus.Canceled;

        // If this trade is a lead trade itself
        if (sourceTrade.LeadTrade == null)
        {
          // Find any existing unwinds of this trade and cancel them
          IList tradesList = Session.Find("from Trade t where t.LeadTrade in (Select tr.ObjectId from Trade tr where tr.ObjectId=?) and t.TradeStatus<>?",
            new Object[] {sourceTrade.ObjectId, TradeStatus.Canceled},
            new NHibernate.Type.IType[] {ScalarType.Int64, ScalarType.Int32});

          foreach (Trade t in tradesList)
          {
            t.RequestUpdate();
            t.TradeStatus = TradeStatus.Canceled;
          }
        }
        else
        {
          // This trade is an unwind. 

          // Unset termination date of lead trade
          sourceTrade.LeadTrade.RequestUpdate();
          sourceTrade.LeadTrade.Termination = Dt.Empty;

          // Find sibling unwinds (all with same LeadTrade) and un-terminate them
          IList tradesList = Session.Find("from Trade t where t.LeadTrade in (Select tr.ObjectId from Trade tr where tr.ObjectId=?) and t.TradeStatus<>?",
            new Object[] {sourceTrade.LeadTrade.ObjectId, TradeStatus.Canceled},
            new NHibernate.Type.IType[] {ScalarType.Int64, ScalarType.Int32});

          foreach (Trade t in tradesList)
          {
            t.RequestUpdate();
            t.Termination = Dt.Empty;
          }
        }

        Session.CommitTransaction();
      }
    }

    /// <summary>
    ///   This method validates the changes made to the trade's Amount and Termination date
    ///   with respect to its related (assign/unwind) trades and returns the errors and changes.
    ///   It does not commit any changes to the database.
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="forceTermination"></param>
    /// <param name="errors"></param>
    /// <param name="changes"></param>
    /// <returns></returns>
    public static bool CanSynchronizeRelated(Trade trade, bool forceTermination, out List<string> errors, out List<string> changes)
    {
      //AdjustUnwinds(trade, out errors, out changes, false);
      //if (errors.Count != 0)
      //{
      //  changes.Clear();
      //  return false;
      //}
      //return true;

      using (new SessionBinder())
      {
        return SyncRelated(trade, forceTermination, false, out errors, out changes);
      }
    }

    /// <summary>
    ///   This method tries to synchronize the related (assign/unwind) trades with 
    ///   changes made to the trade's Amount and Termination date. If there are no errors
    ///   it commits those changes to the database.
    /// </summary>
    /// <param name="origTrade"></param>
    /// <param name="forceTermination"></param>
    /// <param name="errors"></param>
    /// <param name="changes"></param>
    /// <returns></returns>
    public static bool SynchronizeRelated(Trade origTrade, bool forceTermination, out List<string> errors, out List<string> changes)
    {
      using (new SessionBinder(ReadWriteMode.Workflow))
      {
        Trade trade = FindByTradeId(origTrade.TradeId);

        trade.RequestUpdate();

        //set the new instances prperties to be the same as the original one
        trade.Amount = origTrade.Amount;
        trade.Settle = origTrade.Settle;
        trade.Termination = origTrade.Termination;

        bool success = SyncRelated(trade, forceTermination, true, out errors, out changes);
        if (success)
        {
          Session.CommitTransaction();
        }

        return success;
      }
    }

    /// <summary>
    ///    Tries to synchronize the termination dates on lead trade and associated assign/unwind trades.
    ///    If force termination is true, then it tries to force all related trades to be terminated on 
    ///    the same date as the trade that is passed in.
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="forceTermination"></param>
    /// <param name="commit"></param>
    /// <param name="errors"></param>
    /// <param name="changes"></param>
    /// <returns></returns>
    private static bool SyncRelated(Trade trade, bool forceTermination, bool commit, out List<string> errors, out List<string> changes)
    {
      errors = new List<string>();
      changes = new List<string>();

      if (trade == null)
      {
        errors.Add("Cannot find trade");
        return false;
      }

      Dt terminationDate = trade.Termination;

      IList relatedTrades;
      // If this is the lead trade
      if (trade.LeadTrade == null)
      {
        relatedTrades = Session.CreateCriteria(typeof(Trade)).Add(Restrictions.Eq("LeadTrade", trade)).List();
      }
      else
      {
        relatedTrades = Session.CreateCriteria(typeof(Trade))
          .Add(Restrictions.Eq("LeadTrade", trade.LeadTrade))
          .Add(Restrictions.Not(Restrictions.In("TradeId", new object[] {trade.TradeId})))
          .Add(Restrictions.Not(Restrictions.Eq("TradeStatus", TradeStatus.Canceled)))
          .List();
        relatedTrades.Add(trade.LeadTrade);
      }

      if (relatedTrades.Count == 0)
        return true;

      if (!forceTermination)
      {
        // Get a valid termination date.

        // Get the total amount for entire set of related trades.
        double totalAmount = GetTotalAmount(trade.Amount, relatedTrades);
        double leadTradeAmount = trade.LeadTrade == null ? trade.Amount : trade.LeadTrade.Amount;

        // If the total amount is not zero and the amount on the lead trade is greater in magnitude
        // when compared to total amount on all related trades and opposite in sign then we need to clear 
        // termination date for all trades.
        if ((totalAmount > 0 && Math.Sign(leadTradeAmount) == 1) ||
            (totalAmount < 0 && Math.Sign(leadTradeAmount) == -1))
        {
          TerminateTrades(trade, relatedTrades, Dt.Empty, forceTermination, commit, changes);
          return true;
        }

        if (totalAmount == 0)
        {
          // If the total amount is zero then it is total assign/unwind
          // in which case we need to terminate all trades. Now if the
          // termination date is already set on the trade, validate this
          // date or terminate all trades as of the setle date of the last
          // assign/unwind.
          if (terminationDate.IsEmpty())
            terminationDate = GetValidTerminationDate(trade, relatedTrades);
        }
        else
        {
          errors.Add(
            String.Format(
              "The total amount on Unwind/Assign trades [{0:C}] is greater than the amount on the lead trade [{1:C}]",
              Math.Abs(totalAmount - leadTradeAmount), Math.Abs(leadTradeAmount)));
          return false;
        }
      }

      if (ValidateRelatedTrades(terminationDate, relatedTrades, errors))
      {
        TerminateTrades(trade, relatedTrades, terminationDate, forceTermination, commit, changes);
        return true;
      }

      return false;
    }

    /// <summary>
    ///   Get a list of all non-cancelled unwind and assign trades associated with the given lead trade.
    /// </summary>
    public static IList<Trade> GetUnwindAssignTrades(Trade leadTrade, string tradeIdToExclude = null)
    {
      if (leadTrade == null)
        return null;

      var tradeIdsToExclude = String.IsNullOrWhiteSpace(tradeIdToExclude)
        ? new object[] {leadTrade.TradeId}
        : new object[] {leadTrade.TradeId, tradeIdToExclude};

      return Session.CreateCriteria(typeof(Trade))
        .Add(Restrictions.Eq("LeadTrade", leadTrade))
        .Add(Restrictions.Not(Restrictions.In("TradeId", tradeIdsToExclude)))
        .Add(Restrictions.Not(Restrictions.Eq("TradeStatus", TradeStatus.Canceled)))
        .List<Trade>();
    }

    /// <summary>
    ///   Terminates the given trade and all related trades as of the given termination date.
    /// 
    ///   If the trades are not terminated due to defaults, this method does the following:
    /// 
    ///   1. Sets the Settle date on the latest assign/unwind trade equal to the given termination date.
    ///   2. If there is only one assign/unwind trade and you are terminating the trade, 
    ///      this method sets the trade type to Assign/Unwind based on whether the couterparty
    ///      is different or same for the lead trade and assign/unwind trade.
    ///   3. If you are un-terminating the trades because of notional updates, then this method
    ///      sets the trade type for the child trades to Assign/Unwind based on 
    ///      whether the counterparty is different or same on the lead trade and child trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="relatedTrades"></param>
    /// <param name="terminationDate"></param>
    /// <param name="terminatedDueToDefaults"></param>
    /// <param name="commit"></param>
    /// <param name="results"></param>
    private static void TerminateTrades(Trade trade, IList relatedTrades, Dt terminationDate, bool terminatedDueToDefaults, bool commit, List<string> results)
    {
      if (results == null)
        results = new List<string>();

      if (!trade.Termination.Equals(terminationDate))
      {
        results.Add(String.Format("[{0}] - Termination Date - [{1}]", trade.TradeId,
          (terminationDate.IsEmpty() ? "Clear" : terminationDate.ToString())));
        if (commit)
        {
          if (!trade.IsNewObject())
            trade.RequestUpdate();

          trade.Termination = terminationDate;
        }
      }

      foreach (Trade t in relatedTrades)
      {
        if (!t.Termination.Equals(terminationDate))
        {
          results.Add(String.Format("[{0}] - Termination Date - [{1}]", t.TradeId,
            (terminationDate.IsEmpty() ? "Clear" : terminationDate.ToString())));
          if (commit)
          {
            t.RequestUpdate(); //the trade object can be a new trade?
            t.Termination = terminationDate;
          }
        }
      }

      // If the termination date is not empty and it is not
      // terminated due to defaults we need to set the trade
      // type (either total or partial unwind/assign) and also
      // set the last assign/unwind trade's Settle date 
      // equal to the termination date.
      if (!terminatedDueToDefaults)
      {
        // Get all assign/unwind trades sorted by TradedDate
        var childTrades = new List<Trade>();
        LegalEntity leadTradeCounterParty = null;

        if (trade.LeadTrade != null)
          childTrades.Add(trade);
        else
          leadTradeCounterParty = trade.Counterparty;

        foreach (Trade t in relatedTrades)
        {
          if (t.LeadTrade != null)
            childTrades.Add(t);
          else
            leadTradeCounterParty = t.Counterparty;
        }

        // Get the latest assign/unwind trade. 
        // We should also consider the case where 
        // there can be 2 unwinds on the same date.
        Trade lastTrade = null;
        foreach (Trade t in childTrades)
        {
          if (lastTrade == null)
          {
            lastTrade = t;
          }
          else
          {
            if (lastTrade.Traded == t.Traded)
              lastTrade = (lastTrade.TradeId.CompareTo(t.TradeId) > 0) ? lastTrade : t;
            else
              lastTrade = (lastTrade.Traded > t.Traded) ? lastTrade : t;
          }
        }

        if (!terminationDate.IsEmpty())
        {
          if (!lastTrade.Settle.Equals(terminationDate))
          {
            results.Add(String.Format("[{0}] - Settle Date - [{1}]", lastTrade.TradeId, terminationDate));
            if (commit)
            {
              lastTrade.RequestUpdate();
              lastTrade.Settle = terminationDate;
            }
          }

          if (childTrades.Count == 1)
          {
            TradeType tt = (LegalEntityUtil.AreSame(lastTrade.Counterparty, leadTradeCounterParty))
              ? TradeType.Unwind
              : TradeType.Assign;

            if (lastTrade.TradeType != tt)
            {
              results.Add(String.Format("[{0}] - Trade Type - [{1}]", lastTrade.TradeId, tt));
              if (commit)
              {
                lastTrade.RequestUpdate();
                lastTrade.TradeType = tt;
              }
            }
          }
        }
        else
        {
          foreach (Trade t in childTrades)
          {
            TradeType tt = (LegalEntityUtil.AreSame(t.Counterparty, leadTradeCounterParty))
              ? TradeType.Unwind
              : TradeType.Assign;

            if (t.TradeType != tt)
            {
              results.Add(String.Format("[{0}] - Trade Type - [{1}]", t.TradeId, tt));
              if (commit)
              {
                t.RequestUpdate();
                t.TradeType = tt;
              }
            }
          }
        }
      }
    }

    /// <summary>
    ///   Adds up the amount on all related trades along with the amount
    ///   passed in and returns this total.
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="relatedTrades"></param>
    /// <returns></returns>
    private static double GetTotalAmount(double amount, IList relatedTrades)
    {
      double amt = amount;

      foreach (Trade t in relatedTrades)
        amt += t.Amount;

      return amt;
    }

    /// <summary>
    ///   Returns the latest Settle date among all the related trades.
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="relatedTrades"></param>
    /// <returns></returns>
    private static Dt GetValidTerminationDate(Trade trade, IList relatedTrades)
    {
      Dt settleDate = trade.Settle;

      foreach (Trade t in relatedTrades)
        if (t.Settle > settleDate)
          settleDate = t.Settle;

      return settleDate;
    }

    /// <summary>
    ///   Validates the given termination date against the Traded and Settle date
    ///   of all related trades.
    /// </summary>
    /// <param name="terminationDate"></param>
    /// <param name="relatedTrades"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    private static bool ValidateRelatedTrades(Dt terminationDate, IList relatedTrades, List<string> errors)
    {
      if (errors == null)
        errors = new List<string>();

      if (terminationDate.IsEmpty())
        return true;

      bool isValiid = true;

      foreach (Trade t in relatedTrades)
      {
        if (terminationDate < t.Traded)
        {
          errors.Add(
            String.Format("Invalid Termination Date [{0}]. It is before the Traded date [{1}] for trade [{2}].",
              terminationDate, t.Traded, t.TradeId));
          isValiid = false;
        }

        if (terminationDate < t.Settle)
        {
          errors.Add(
            String.Format("Invalid Termination Date [{0}]. It is before the Settle date [{1}] for trade [{2}].",
              terminationDate, t.Settle, t.TradeId));

          isValiid = false;
        }
      }

      return isValiid;
    }

    /// <summary>
    ///   Gets a temporary TradeId
    /// </summary>
    /// <returns></returns>
    public static string GetNextTransientTradeId()
    {
      return String.Format("Temp{0}", Interlocked.Increment(ref transientTradeId_).ToString("D8"));
    }

    /// <summary>
    /// Given a list of objectids get the associated tradeids
    /// </summary>
    /// <param name="tradeObjectIds"></param>
    /// <returns></returns>
    public static Dictionary<long, string> GetTradeIdsFromObjectIds(IEnumerable<long> tradeObjectIds)
    {
      var dict = new Dictionary<long, string>();

      using (new SessionBinder())
      {
        try
        {
          // Create a temp table with all the objectids
          Session.BulkUpdate("CREATE TABLE #TradeObjectIds (ObjectId bigint NOT NULL PRIMARY KEY CLUSTERED (ObjectId))");

          var dt = new DataTable("#TradeObjectIds");
          dt.Columns.Add("ObjectId", typeof(long));

          foreach (var id in tradeObjectIds)
          {
            var r = dt.NewRow();
            r[0] = id;
            dt.Rows.Add(r);
          }

          Session.BulkInsert(dt);

          // Now join to the temp table to get the tradeIds
          using (
            var reader =
              Session.ExecuteReader(
                "select t.ObjectId,t.TradeId from Trade t,#TradeObjectIds o where t.ObjectId=o.ObjectId"))
          {
            while (reader.Read())
              dict.Add((long)reader[0], (string)reader[1]);
          }
        }
        finally
        {
          Session.BulkUpdate("DROP TABLE #TradeObjectIds");
        }
      }

      return dict;
    }

    /// <summary>
    ///   Returns whether or not a given trade can be assigned.
    ///   NOTE: this method does not check that the trade amount has been already fully unwound.
    ///   Use this method together with IsFullyUnwound, if necessary.
    /// </summary>
    public static bool CanAssign(Trade trade)
    {
      if (trade.IsCleared) return false;
      return CanUnwind(trade);
    }

    /// <summary>
    ///   Returns whether or not a given trade can be unwound.
    ///   NOTE: this method does not check that the trade amount has been already fully unwound.
    ///   Use this method together with IsFullyUnwound, if necessary.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public static bool CanUnwind(Trade trade)
    {
      if (trade.TradeType != TradeType.New || trade.TradeStatus == TradeStatus.Canceled || trade.TradeStatus == TradeStatus.Whatif)
        return false;
      return AssignUnwindProductTypes.Contains(trade.Product.GetType().Name);
    }

    /// <summary>
    ///   Checks whether the total amount of this trade and all its associated non-cancelled unwind and assign trades equals to 0.
    /// </summary>
    public static bool IsFullyUnwound(Trade trade)
    {
      double maxAllowedAmt = GetMaxUnwindOrAssignAmount(trade);
      if (maxAllowedAmt.ApproximatelyEqualsTo(0.0))
        return true;
      return false;
    }

    /// <summary>
    ///   Returns whether or not a given trade can be made an assigneed of another trade
    /// </summary>
    public static bool CanMakeAssign(Trade trade)
    {
      return !trade.IsSwapLike() && CanAssign(trade);
    }

    /// <summary>
    ///   Returns whether or not a given trade can be made an unwound of another trade
    /// </summary>
    public static bool CanMakeUnwind(Trade trade)
    {
      return !trade.IsSwapLike() && CanUnwind(trade);
    }
   


    /// <summary>
    ///   Returns whether or not Clearing is applicale to the specified type of trade
    /// </summary>
    public static bool IsClearingApplicable(Type tradeType)
    {
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public static List<string> LookupRelatedTradeIds(string tradeId)
    {
      HashSet<string> relatedTradeIds = new HashSet<string>();
      if (string.IsNullOrEmpty(tradeId))
        return relatedTradeIds.ToList();

      var lookupTradeId = tradeId.ToUpper();
      relatedTradeIds.Add(tradeId);

      Session.Linq<Trade>().Where(t => t.LeadTrade.TradeId.Contains(lookupTradeId) || t.TradeId.Contains(lookupTradeId)).ForEach(t =>
      {
        relatedTradeIds.Add(t.TradeId);
        if (t.LeadTrade != null)
          relatedTradeIds.Add(t.LeadTrade.TradeId);
      });

      return relatedTradeIds.ToList();
    }
    
    #region Comparison

    /// <summary>
    /// This sort order is typically used to sort a list of trades
    /// for a risk run.
    /// So sort by strategy and within that product type and within that lastupdated
    /// </summary>
    private class CompareTradeByStrategy : IComparer<Trade>
    {
      public int Compare(Trade x, Trade y)
      {
        if (x.Strategy == null)
          return -1;

        if (y.Strategy == null)
          return 1;

        // First sort by strategy
        int ret = String.Compare(x.Strategy.Name, y.Strategy.Name);

        // Now by product type
        if (ret == 0)
          ret = String.Compare(x.Product.GetType().Name, y.Product.GetType().Name);

        // Now by last updated
        if (ret == 0)
          ret = DateTime.Compare(x.LastUpdated, y.LastUpdated);

        return ret;
      }
    }

    /// <summary>
    /// This sort order is typically used to sort a list of trades
    /// for a risk run.
    /// So sort by counterparty and within that product type and within that lastupdated
    /// </summary>
    private class CompareTradeByCounterparty : IComparer<Trade>
    {
      public int Compare(Trade x, Trade y)
      {
        var xRiskyCpty = x as IRiskyCounterparty;
        var yRiskyCpty = y as IRiskyCounterparty;

        if (xRiskyCpty == null)
          return -1;

        if (yRiskyCpty == null)
          return 1;

        if (xRiskyCpty.Counterparty == null)
          return -1;

        if (yRiskyCpty.Counterparty == null)
          return 1;

        string xName = xRiskyCpty.Counterparty.Name;
        string yName = yRiskyCpty.Counterparty.Name;
        // First sort by root counterparty name
        int ret = String.Compare(xName, yName);

        // Now by product type
        if (ret == 0)
          ret = String.Compare(x.Product.GetType().Name, y.Product.GetType().Name);

        // Now by last updated
        if (ret == 0)
          ret = DateTime.Compare(x.LastUpdated, y.LastUpdated);

        return ret;
      }
    }

    #endregion

    #endregion Methods

    #region Factory

    /// <summary>
    ///   Create a trade of a specified product type
    /// </summary>
    ///
    /// <param name="tradeType">Type of Trade to create</param>
    ///
    /// <returns>Created trade</returns>
    ///
    public static Trade CreateInstance(Type tradeType)
    {
      if (!tradeType.IsSubclassOf(typeof(Trade)))
        throw new Exception("TradeUtil.CreateInstance requires a type of Trade not [" + tradeType.Name + "]");

      return (Trade)ClassCache.Find(tradeType).CreateInstance();
    }

    /// <exclude />
    public static T CreateInstance<T>() where T : Trade, new()
    {
      return new T();
    }

    #endregion Factory

    #region Data

    private static int transientTradeId_;

    ///<summary>
    /// This set contains all products that can be Assigned/Unwound
    ///</summary>
    private static readonly HashSet<string> AssignUnwindProductTypes =
      new HashSet<string>(new[]
      {
        typeof(CDS).Name,
        typeof(CDX).Name,
        typeof(LCDS).Name,
        typeof(LCDX).Name,
        //typeof(NTD).Name,
        //typeof(CDXTranche).Name,
        //typeof(LCDXTranche).Name,
        //typeof(CDO).Name,
        //typeof(LCDO).Name,
        typeof(BasketCDS).Name,
        //typeof(FundingLoan).Name,
        typeof(Swap).Name,
        //typeof(CapFloor).Name,
        typeof(FRA).Name,
        //typeof(StandardCDS).Name,
        //typeof(OTCStockOption).Name,
        //typeof(RecoveryLock).Name,
        typeof(CDXOption).Name,
        typeof(CD).Name,
        //typeof(MMDRateLock).Name,
        //typeof(IBoxxTRS).Name
      });

    private static readonly HashSet<string> CDSLikeProductTypes =
      new HashSet<string>(new[]
      {
        typeof(CDS).Name,
        typeof(CDX).Name,
        typeof(LCDS).Name,
        typeof(LCDX).Name,
        //typeof(CDXTranche).Name,
        //typeof(LCDXTranche).Name,
        //typeof(CDO).Name,
        //typeof(LCDO).Name
      });

    #endregion
  } // class TradeUtil
} 