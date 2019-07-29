namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Projection type
  /// </summary>
  public enum ProjectionType
  {
    /// <summary>
    /// None
    /// </summary> 
    None = 0,
    /// <summary>
    ///   Simply compounded Libor rate.
    /// </summary>
    SimpleProjection = 1,
    /// <summary>
    ///   Swap rate.
    /// </summary>
    SwapRate = 2,
    /// <summary>
    ///   Inflation rate. 
    /// </summary>
    InflationRate = 3,
    /// <summary>
    ///  Rate that is a weighted arithmetic average of daily forward rates (i.e. FedFunds like rate).
    /// </summary>
    ArithmeticAverageRate = 4,
    /// <summary>
    ///  Rate that is a weighted geometric average of daily rates (i.e. Ois like rate).
    /// </summary>
    GeometricAverageRate = 5,
    /// <summary>
    /// Inflation forward price.
    /// </summary>
    InflationForward = 6,
    /// <summary>
    /// Averaging for TBills-linked leg
    /// </summary>
    TBillArithmeticAverageRate = 7,
    /// <summary>
    /// Conventional averaging for CP-linked leg
    /// </summary>
    CPArithmeticAverageRate = 8,
    /// <summary>
    /// Bond par-yield
    /// </summary>
    ParYield = 9,
    /// <summary>
    /// Price of a commodity
    /// </summary>
    CommodityPrice = 10,
    /// <summary>
    /// Average Price of a commodity
    /// </summary>
    AverageCommodityPrice = 11,
    /// <summary>
    /// Price of a equity
    /// </summary>
    EquityPrice = 12,
    /// <summary>
    /// FX rate
    /// </summary>
    FxRate = 13
  }
}