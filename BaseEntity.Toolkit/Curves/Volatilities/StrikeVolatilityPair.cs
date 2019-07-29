/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   Represent a pair of strike and volatility in volatility smile.
  /// </summary>
  [Serializable]
  public struct StrikeVolatilityPair
  {
    /// <summary>
    ///  Get the value of strike.
    /// </summary>
    public readonly double Strike;

    /// <summary>
    ///  Get the value of volatility.
    /// </summary>
    public readonly double Volatility;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrikeVolatilityPair"/> struct.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="volatility">The volatility.</param>
    public StrikeVolatilityPair(double strike, double volatility)
    {
      Strike = strike;
      Volatility = volatility;
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks></remarks>
    public override string ToString()
    {
      return String.Format("{0}, {1}", Strike, Volatility);
    }
  }
}
