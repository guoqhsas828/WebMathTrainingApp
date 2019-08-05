using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Component]
  public class TradeFilter : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    public TradeFilter()
    {
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="tradeIds"></param>
    /// <param name="strategies"></param>
    /// <param name="substrategies"></param>
    /// <param name="products"></param>
    /// <param name="counterparties"></param>
    /// <param name="includePending"></param>
    /// <param name="includeWhatIf"></param>
    public TradeFilter(string tradeIds, string strategies, string substrategies, string products, string counterparties, bool includePending, bool includeWhatIf)
    {
      TradeIds = tradeIds;
      Strategies = strategies;
      Substrategies = substrategies;
      Products = products;
      Counterparties = counterparties;
      IncludePending = includePending;
    	IncludeWhatIf = includeWhatIf;
    }

    /// <summary>
    /// Construct new instance with the same filter criteria as the original
    /// </summary>
    /// <param name="other"></param>
    public TradeFilter(TradeFilter other)
    {
      TradeIds = other.TradeIds;
      Strategies = other.Strategies;
      Substrategies = other.Substrategies;
      Products = other.Products;
      Counterparties = other.Counterparties;
      IncludePending = other.IncludePending;
    	IncludeWhatIf = other.IncludeWhatIf;

      ExcludeCounterparties = other.ExcludeCounterparties;
      ExcludeProducts = other.ExcludeProducts;
      ExcludeStrategies = other.ExcludeStrategies;
      ExcludeSubstrategies = other.ExcludeSubstrategies;
      ExcludeTradeIds = other.ExcludeTradeIds;
    }
    
    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 1073741820)]
    public string TradeIds { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 1073741820)]
    public string Strategies { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 1073741820)]
    public string Substrategies { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 1073741820)]
    public string Products { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 1073741820)]
    public string Counterparties { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool IncludePending { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[BooleanProperty]
		public bool IncludeWhatIf { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool ExcludeTradeIds { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool ExcludeStrategies { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool ExcludeSubstrategies { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool ExcludeProducts { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool ExcludeCounterparties { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 1073741820)]
    public string BookingEntities { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [BooleanProperty]
    public bool ExcludeBookingEntities { get; set; }

    #endregion
  }
}