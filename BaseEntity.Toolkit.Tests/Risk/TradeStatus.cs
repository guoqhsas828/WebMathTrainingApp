
namespace BaseEntity.Risk
{
  /// <summary>
  ///  Trade status
  /// </summary>
  public enum TradeStatus
  {
    /// <summary>
    ///  Pending verification
    /// </summary>
    Pending,

    /// <summary>
    ///  Verified
    /// </summary>
    Done,

    /// <summary>
    ///   Terminated
    /// </summary>
    Canceled,

    /// <summary>
    /// What If Trade
    /// </summary>
    Whatif
  }
}