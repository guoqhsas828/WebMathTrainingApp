/*
 * TradeType.cs
 *
 */

using System;

namespace BaseEntity.Risk
{

	/// <summary>
  ///   Trade Type
  /// </summary>
  public enum TradeType
  {
    /// <summary>New Trade</summary>
    New,
    /// <summary>Unwind trade</summary>
    Unwind,
    /// <summary>Assignment trade</summary>
    Assign
  }
}
