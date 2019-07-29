/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///   FX volatility quote flags.
  /// </summary>
  /// <remarks></remarks>
  [Flags]
  public enum FxVolatilityQuoteFlags
  {
    /// <summary>No special flag.</summary>
    None = 0,

    /// <summary>Use the forward FX rate as the ATM strike. Note that this mutually exclusive with <c>SpotAtm</c>.
    /// If both <c>ForwardAtm</c> and <c>SpotAtm</c> are not set, use delta neutral strike.</summary>
    ForwardAtm = 1,

    /// <summary>Use the spot FX rate as the ATM strike. Note that this mutually exclusive with <c>ForwardAtm</c>.
    /// If both <c>ForwardAtm</c> and <c>SpotAtm</c> are not set, use delta neutral strike.</summary>
    SpotAtm = 2,

    /// <summary>Delta is calculated with respect to the forward FX rate. If this is not set, delta is with respect to spot rate.</summary>
    ForwardDelta = 4,

    /// <summary>Set this to indicate that delta is premium included.</summary>
    PremiumIncludedDelta = 8,

    /// <summary>Indicator of one-volatility butterfly convention.</summary>
    OneVolatilityBufferfly = 16,

    /// <summary>Set this to indicate currency 2 strangle; otherwise, currency 1 strangle.</summary>
    Ccy2Strangle = 32,

    /// <summary>If set, the risk reversal is based on Currency 2 call; otherwise it is based on currency 1 call.</summary>
    Ccy2RiskReversal = 64,
  }

}
