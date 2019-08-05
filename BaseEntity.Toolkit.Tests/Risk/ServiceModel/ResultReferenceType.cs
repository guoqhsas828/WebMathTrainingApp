namespace BaseEntity.Risk
{
  /// <summary>
  /// Format of riskresult reference string
  /// </summary>
  public enum ResultReferenceType
  {
    /// <summary>
    /// 
    /// </summary>
    None,

    /// <summary>
    /// No reference string
    /// </summary>
    Product,

    /// <summary>
    /// Reference is the name of either a CDSCurve or an LCDSCurve
    /// </summary>
    CreditCurve,

    /// <summary>
    /// Reference is the name of either a CDSCurve or LCDSCurve + "." + Tenor name
    /// </summary>
    CreditCurveByTenor,

    /// <summary>
    /// Reference is the name of an RateCurveConfig 
    /// </summary>
    RateCurve,

    /// <summary>
    /// Reference is the name of an RateCurveConfig + "." + Tenor name
    /// </summary>
    RateCurveByTenor,

    /// <summary>
    /// Reference is the name of a surface + "|" + strike + "|all"
    /// </summary>
    SurfaceByDetachment,

    /// <summary>
    /// Reference is the name of a surface + "|" + strike + "|" + Tenor Name
    /// </summary>
    SurfaceByPoint,

    /// <summary>
    /// Reference is the name of a surface + "|all|" + Tenor Name
    /// </summary>
    SurfaceByTenor,

    /// <summary>
    /// Reference is the name of an index
    /// </summary>
    HedgeIndex,

    /// <summary>
    /// Reference is the name of a surface
    /// </summary>
    Surface,

    /// <summary>
    /// Counterparty result whose dimensions are counterparty/bookingentity/nettingset/confidenceinterval
    /// </summary>
    Counterparty,

    /// <summary>
    /// Deprecated. Use Counterparty instead
    /// </summary>
    NettingSet,

    /// <summary>
    /// Reference is Ticker of counterparty + "." + Name of time bucket on simulation grid
    /// + "." + confidence interval if applicable.
    /// </summary>
    CounterpartyByTimeBucket,

    /// <summary>
    /// Reference is Ticker of counterparty + "|" + CounterpartyRiskSensitivity + "|" + Curve Name
    /// </summary>
    CounterpartySensitivity,

    /// <summary>
    /// Reference is expiry/strike pair
    /// </summary>
    RateVolatilityCube,

    /// <summary>
    /// Reference is unique identifier of instrument at curve tenor
    /// </summary>
    RateCurveByTenorInstrument,

    /// <summary>
    /// Reference is the name of an Fx Curve
    /// </summary>
    FxCurve,

    /// <summary>
    /// Reference is the name of an Fx Curve + "." + Tenor name
    /// </summary>
    FxCurveByTenor,

    ///<summary>
    /// Reference is expiry/tenor pair, mapping the swaption market ATM surface
    ///</summary>
    SwaptionVolatilityCube,

    /// <summary>
    /// Reference is Ticker of counterparty + "|" + CounterpartyRiskSensitivity + "|" + Curve Name + "|" + time bucket on simulation grid
    /// </summary>
    CounterpartySensitivityByTimeBucket,

    /// <summary>
    /// Reference is Ticker of counterparty + "|" + CounterpartyRiskSensitivity + "|" + Curve Name + "|" + curve tenor
    /// </summary>
    CounterpartySensitivityByTenor,

    ///<summary>
    /// Reference is Ticker of counterparty + "|" + CounterpartyRiskSensitivity + "|" + Curve Name + "|" + curve tenor + "|" + time bucket on simulation grid
    ///</summary>
    CounterpartySensitivityByTenorByTimeBucket,

    ///<summary>
    /// Reference is Ticker of counterparty + "|"  + CounterpartyRiskSensitivity + "|" + Name of market factor
    ///</summary>
    CounterpartySensitivityByMarketFactor,

    ///<summary>
    /// Reference is Ticker of counterparty + "|"  + CounterpartyRiskSensitivity + "|" + Name of market factor + "|" + time bucket on simulation grid
    ///</summary>
    CounterpartySensitivityByMarketFactorByTimeBucket,

    /// <summary>
    /// Reference is ConfidenceInterval
    /// </summary>
    TradeByConfidenceInterval,

    /// <summary>
    /// Reference is Ticker.ShiftScenario.ShiftDate
    /// </summary>
    CounterpartyByShift,

    /// <summary>
    /// 
    /// </summary>
    Portfolio,

    /// <summary>
    /// Reference is ShiftScenario.ShiftDate
    /// </summary>
    PortfolioByShift,

    /// <summary>
    /// Reference is ShiftScenario.ShiftDate
    /// </summary>
    TradeByShift,

    /// <summary>
    /// Reference is unique identifier of instrument at FX curve tenor
    /// </summary>
    FxCurveByInstrument,

    /// <summary>
    /// 
    /// </summary>
    Trade,

    /// <summary>
    /// 
    /// </summary>
    TradeCreditCurve,

    /// <summary>
    /// 
    /// </summary>
    TradeCreditCurveByTenor,

    /// <summary>
    /// 
    /// </summary>
    TradeRateCurveByTenor,

    /// <summary>
    /// 
    /// </summary>
    TradeRateCurveByTenorInstrument,

    /// <summary>
    /// 
    /// </summary>
    TradeFxCurve,

    /// <summary>
    /// 
    /// </summary>
    TradeFxCurveByInstrument,

    /// <summary>
    /// trade results with time dimension
    /// </summary>
    TradeByTimeBucket,
    
    /// <summary>
    /// trade results with time dimension and quantile
    /// </summary>
    TradeByTimeBucketByConfidenceInterval, 

    /// <summary>
    /// Reference is inflation curve name
    /// </summary>
    InflationCurve,

    /// <summary>
    /// Reference is inflation curve index, tenor, instrument type
    /// </summary>
    InflationCurveByInstrument,

    /// <summary>
    /// Trade results with inflation curve dimension
    /// </summary>
    TradeInflationCurve,

    /// <summary>
    /// Trade results with inflation curve, tenor, instrument type dimensions
    /// </summary>
    TradeInflationCurveByInstrument,

    /// <summary>
    /// Represents a result determined for a particular named leg of a product in that leg's currency     
    /// </summary>
    TradeByLegCurrency,

    /// <summary>
    /// Represents a single trade-specific payment on a specified date and in a specified currency
    /// </summary>
    TradePayment,

    /// <summary>
    /// by tenor trade rate sensitivity results with time dimension 
    /// </summary>
    TradeRateCurveByTenorInstrumentByTimeBucket,


    /// <summary>
    /// by tenor trade rate sensitivity results with time dimension and quantile
    /// </summary>
    TradeRateCurveByTenorInstrumentByTimeBucketByConfidenceInterval,

    /// <summary>
    /// parallel trade rate sensitivity results with time dimension 
    /// </summary>
    TradeRateCurveByTimeBucket,


    /// <summary>
    /// parallel trade rate sensitivity results with time dimension and quantile
    /// </summary>
    TradeRateCurveByTimeBucketByConfidenceInterval,


    /// <summary>
    /// trade FX sensitivity results with time dimension 
    /// </summary>
    TradeFxCurveByTimeBucket,


    /// <summary>
    /// trade FX sensitivity results with time dimension and quantile
    /// </summary>
    TradeFxCurveByTimeBucketByConfidenceInterval,

    /// <summary>
    /// 
    /// </summary>
    TradeStockCurve,

    /// <summary>
    /// 
    /// </summary>
    TradeCommodityCurve, 
    /// <summary>
    /// trade cash flow results in tenor buckets with category and type information
    /// </summary>
    TradeCashflowByBucket,

    /// <summary>
    /// The Hierarchy for which the Result is referenced
    /// </summary>
    Hierarchy,

    /// <summary>
    /// Trade By Tenor
    /// </summary>
    TradeByTenor,

    /// <summary>
    /// Counterparty By Tenor
    /// </summary>
    CounterpartyByTenor,

    /// <summary>
    /// Hierarchy By Tenor
    /// </summary>
    HierarchyByTenor,

    /// <summary>
    /// Extension Entity By Tenor
    /// </summary>
    ExtensionEntityByTenor,
    /// <summary>
    /// Trade by RiskyParty
    /// </summary>
    TradeByRiskyParty,
    
    /// <summary>
    /// TODO
    /// </summary>
    HierarchyByScenario,

    /// <summary>
    /// TODO
    /// </summary>
    TradeByTenorByScenario,

    /// <summary>
    /// TODO
    /// </summary>
    CounterpartyByTenorByScenario,

    /// <summary>
    /// TODO
    /// </summary>
    HierarchyByTenorByScenario,
    
    /// <summary>
    /// TODO
    /// </summary>
    ExtensionEntityByTenorByScenario,

    /// <summary>
    /// TODO
    /// </summary>
    TradeByScenario,

    /// <summary>
    /// TODO
    /// </summary>
    TradeByLegCurrencyByScenario,

    /// <summary>
    /// TODO:
    /// </summary>
    TradebyRiskyPartyByScenario
  }
}