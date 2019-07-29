/*
 *   2012. All rights reserved.
 */

using System.Collections.Generic;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   The interface providing volatility smile data points.
  /// </summary>
  /// <remarks></remarks>
  public interface ISmileDataProvider
  {
    /// <summary>
    /// Gets the strike/volatility pairs associated with the specified Black-Scholes parameter data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>A sequence of strike/volatility pairs.</returns>
    /// <remarks></remarks>
    IEnumerable<StrikeVolatilityPair> GetStrikeVolatilityPairs(IBlackScholesParameterData data);
  }
}