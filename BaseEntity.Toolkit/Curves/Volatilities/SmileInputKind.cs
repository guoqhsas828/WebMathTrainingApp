using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   What kind input for smile interpolation.
  /// </summary>
  public enum SmileInputKind
  {
    /// <summary>
    ///   Interpolate on the strike.
    /// </summary>
    Strike,

    /// <summary>
    ///  Interpolate on the moneyness (the ratio of the strike to the ATM forward).
    /// </summary>
    Moneyness,

    /// <summary>
    ///   Interpolate on moneyness (the logorithm of the ratio of the strike to the ATM forward).
    /// </summary>
    LogMoneyness,
  }
}
