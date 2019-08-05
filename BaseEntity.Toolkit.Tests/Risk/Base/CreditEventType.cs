/*
 * CreditEventType.cs
 *
*/

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Represents the type of CreditEvent
  /// </summary>
  public enum CreditEventType
  {
    /// <summary>
    /// 
    /// </summary>
    Unknown,
    /// <summary>
    /// 
    /// </summary>
    Bankruptcy,
    /// <summary>
    /// 
    /// </summary>
    FailureToPay,
    /// <summary>
    /// 
    /// </summary>
    Restructuring,
    /// <summary>
    /// 
    /// </summary>
    ObligationDefault,
    /// <summary>
    /// 
    /// </summary>
    ObligationAcceleration,
    /// <summary>
    /// 
    /// </summary>
    RepudiationOrMoratorium,
    /// <summary>
    /// 
    /// </summary>
    GovernmentalIntervention
  }
}
