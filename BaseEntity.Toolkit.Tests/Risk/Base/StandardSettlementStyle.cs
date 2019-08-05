/*
 * StandardSettlementStyle.cs
 *
 */

namespace BaseEntity.Risk
{

  /// <summary>
  ///   Trade settlement style
  /// </summary>
  ///
  /// <remarks>
  ///   <para>The specification of whether a trade is settling using standard settlement
  ///   instructions as well as whether it is a candidate for settlement netting.</para>
  /// </remarks>
  ///
  public enum StandardSettlementStyle
  {
    /// <summary>Settle using standard pre-determined funds settlement instructions.</summary>
    Standard,

    /// <summary>This is a candidate for settlement netting</summary>
    Net,

    /// <summary>
    ///   Settle using standard pre-determined funds settlement instructions and
    ///   is a candidate for settlement netting.
    /// </summary>
    StandardAndNet
  } // enum StandardSettlementStyle

}  
