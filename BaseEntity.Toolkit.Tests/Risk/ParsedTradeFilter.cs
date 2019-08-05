namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public class ParsedTradeFilter
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tradeFilter"></param>
    public ParsedTradeFilter(TradeFilter tradeFilter)
    {
      TradeIds = new FilterCriterion(tradeFilter.TradeIds);
      Strategies = new FilterCriterion(tradeFilter.Strategies);
      Substrategies = new FilterCriterion(tradeFilter.Substrategies);
      Counterparties = new FilterCriterion(tradeFilter.Counterparties);
      Products = new FilterCriterion(tradeFilter.Products);
      IncludePending = tradeFilter.IncludePending;
    	IncludeWhatIf = tradeFilter.IncludeWhatIf;

      ExcludeCounterparties = tradeFilter.ExcludeCounterparties;
      ExcludeProducts = tradeFilter.ExcludeProducts;
      ExcludeStrategies = tradeFilter.ExcludeStrategies;
      ExcludeSubstrategies = tradeFilter.ExcludeSubstrategies;
      ExcludeTradeIds = tradeFilter.ExcludeTradeIds;

      BookingEntities = new FilterCriterion(tradeFilter.BookingEntities);
      ExcludeBookingEntities = tradeFilter.ExcludeBookingEntities;
    }

    /// <summary>
    /// Construct new instance with the same filter criteria as the original
    /// </summary>
    /// <param name="other"></param>
    public ParsedTradeFilter(ParsedTradeFilter other)
    {
      TradeIds = other.TradeIds;
      Strategies = other.Strategies;
      Substrategies = other.Substrategies;
      Counterparties = other.Counterparties;
      Products = other.Products;
      IncludePending = other.IncludePending;
    	IncludeWhatIf = other.IncludeWhatIf;

      ExcludeCounterparties = other.ExcludeCounterparties;
      ExcludeProducts = other.ExcludeProducts;
      ExcludeStrategies = other.ExcludeStrategies;
      ExcludeSubstrategies = other.ExcludeSubstrategies;
      ExcludeTradeIds = other.ExcludeTradeIds;

      BookingEntities = other.BookingEntities;
      ExcludeBookingEntities = other.ExcludeBookingEntities;
    }
    
    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    public FilterCriterion TradeIds { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    public FilterCriterion Strategies { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    public FilterCriterion Substrategies { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    public FilterCriterion Products { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    public FilterCriterion Counterparties { get; set; }

    /// <summary>
    /// If true, then Trades with TradeStatus=Pending should be included
    /// </summary>
    public bool IncludePending { get; set; }

		/// <summary>
		/// If true, then Trades with TradeStatus=WhatIf should be included
		/// </summary>
		public bool IncludeWhatIf { get; set; }

    /// <summary>
    ///   Exclude TradeIds
    /// </summary>
    public bool ExcludeTradeIds { get; set; }

    /// <summary>
    ///   Exclude Strategies
    /// </summary>
    public bool ExcludeStrategies { get; set; }

    /// <summary>
    ///   Exclude Substrategies
    /// </summary>
    public bool ExcludeSubstrategies { get; set; }

    /// <summary>
    ///   Exclude Products
    /// </summary>
    public bool ExcludeProducts { get; set; }

    /// <summary>
    ///   Exclude Counterparties
    /// </summary>
    public bool ExcludeCounterparties { get; set; }

    /// <summary>
    /// Returns true if all the filter criteria are well formed
    /// </summary>
    public bool IsValid
    {
      get
      {
        return TradeIds.IsValid &&
               Strategies.IsValid &&
               Substrategies.IsValid &&
               Counterparties.IsValid &&
               Products.IsValid &&
               BookingEntities.IsValid;
      }
    }

    /// <summary>
    ///   BookingEntities
    /// </summary>
    public FilterCriterion BookingEntities { get; set; }

    /// <summary>
    ///   Exclude BookingEntities
    /// </summary>
    public bool ExcludeBookingEntities { get; set; }

    #endregion
  }
}