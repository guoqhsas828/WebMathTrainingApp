/*
 * IVolatilityTenor
 *
 *  -2011. All rights reserved.
 *
 */

using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Interface of volatility tenors.
  /// </summary>
  public interface IVolatilityTenor : IBaseEntityObject
  {
    /// <summary>
    /// Gets the maturity (expiry) date associated with the tenor.
    /// </summary>
    /// <remarks></remarks>
    Dt Maturity { get; }

    /// <summary>
    /// Gets the name associated with the tenor.
    /// </summary>
    /// <remarks></remarks>
    string Name { get; }

    /// <summary>
    /// Gets the volatility quote values.
    /// </summary>
    /// <value>The volatility quote values.</value>
    /// <remarks>The quote values may not be the same as the implied volatilities.
    /// For example, when the underlying options are quoted with prices,
    /// the quote values are prices instead of volatilities. 
    /// When the underlying options are quoted as ATM call, Risk Reversals and Butterflies,
    /// the quote values are ATM volatilities and RR/BF deviations.</remarks>
    IList<double> QuoteValues { get; }
  }
}
