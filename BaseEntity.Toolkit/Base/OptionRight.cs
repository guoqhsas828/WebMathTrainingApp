/*
 * OptionRight.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System.ComponentModel;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Represent the right to enter or cancel a trade.
  /// </summary>
	public enum OptionRight
	{
    /// <summary>
    ///  No option right.
    /// </summary>
    None,
    /// <summary>
    ///  The right to enter a trade.
    /// </summary>
    RightToEnter,
    /// <summary>
    ///  The right to cancel a trade.
    /// </summary>
    [Browsable(false)]
    RightToCancel,
	}
}
