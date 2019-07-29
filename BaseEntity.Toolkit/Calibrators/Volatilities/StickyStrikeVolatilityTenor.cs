/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Represents a tenor with pairs of strikes and volatilities as sticky strike quotes.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class StickyStrikeVolatilityTenor : PlainVolatilityTenor, ISmileDataProvider
  {
    private readonly StrikeVolatilityPair[] _pairs;

    /// <summary>
    /// Initializes a new instance of the <see cref="StickyStrikeVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">Name</param>
    /// <param name="expiry">The expiry</param>
    /// <param name="strikes">The strikes</param>
    /// <param name="volatilities">The volatilities</param>
    /// <remarks></remarks>
    public StickyStrikeVolatilityTenor(
      string name, Dt expiry,
      double[] strikes, double[] volatilities)
      : base(name, expiry)
    {
      if (volatilities == null || volatilities.Length == 0)
      {
        throw new ToolkitException("volatilities cannot be empty.");
      }
      if (strikes == null || strikes.Length == 0)
      {
        throw new ToolkitException("deltas cannot be empty.");
      }
      if (volatilities.Length != strikes.Length)
      {
        throw new ToolkitException("Volatilities and strikes not match.");
      }
      Volatilities = volatilities;
      _pairs = new StrikeVolatilityPair[volatilities.Length];
      Strikes = strikes;
    }

    /// <summary>
    /// Gets the FX volatility quote convention.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityQuoteType QuoteType
    {
      get { return VolatilityQuoteType.StickyStrike; }
    }

    /// <summary>
    /// Gets the quotes.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityQuote[] Quotes
    {
      get
      {
        return _pairs.Select(p => new VolatilityQuote(
          p.Strike.ToString(CultureInfo.InvariantCulture),
          p.Volatility)).ToArray();
      }
    }

    /// <summary>
    /// Gets the strike volatility pairs.
    /// </summary>
    /// <remarks></remarks>
    public StrikeVolatilityPair[] StrikeVolatilityPairs
    {
      get { return _pairs; }
    }

    /// <summary>
    /// Gets the strike/volatility pairs associated with the specified Black-Scholes parameter data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>A sequence of strike/volatility pairs.</returns>
    /// <remarks></remarks>
    public IEnumerable<StrikeVolatilityPair> GetStrikeVolatilityPairs(
      IBlackScholesParameterData data)
    {
      int count = QuoteValues.Count;
      for (int i = 0; i < count; ++i)
        yield return _pairs[i] = new StrikeVolatilityPair(
          Strikes[i], QuoteValues[i]);
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks>This is overriden to provide better infomation in object browser.</remarks>
    public override string ToString()
    {
      return String.Format("StickyStrike {0}", Name);
    }
  }
}