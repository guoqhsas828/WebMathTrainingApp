/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   The specification of the at-the-money strike.
  /// </summary>
  public enum AtmKind
  {
    /// <summary>At-the-money strike refers to the spot price.</summary>
    Spot,
    /// <summary>At-the-money strike refers to the forward price.</summary>
    Forward,
    /// <summary>At-the-money strike refers to the delta neutral strike.</summary>
    DeltaNeutral
  }

  /// <summary>
  ///   How to determine the delta.
  /// </summary>
  [Flags]
  public enum DeltaStyle
  {
    /// <summary>No special style: the delta is calculated with respect to
    ///  the spot price and does not include premium.</summary>
    None = 0,
    /// <summary>The delta is calculated with respect to the forward price.</summary>
    Forward = 1,
    /// <summary>The delta is premium included.</summary>
    PremiumIncluded = 2,
  }
}
