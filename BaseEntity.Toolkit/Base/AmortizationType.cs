/*
 * AmortizationType.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System.ComponentModel;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
	///   Type of amortization specifies the interpretation of an amortization
	///   amount in an amortization schedule.
	/// </summary>
  public enum AmortizationType
  {
    /// <summary>Amortization amount is amortization rate times initial notional</summary>
    PercentOfInitialNotional,
    /// <summary>Amortization amount is amortization rate times current notional</summary>
    PercentOfCurrentNotional,
    /// <summary>amount in schedule is simply the new notional level on that date, scaled by the original notional.  It acts like a straightforward "step" schedule</summary>
    RemainingNotionalLevels
  }
}