/*
 *  -2012. All rights reserved.
 */

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Volatility quote rule.
  /// </summary>
  /// <remarks></remarks>
  public enum VolatilityQuoteType
  {
    /// <summary>The implied volatilities are mapped with respect to strike prices.</summary>
    StickyStrike,

    /// <summary>The implied volatilities are mapped with respect to the deltas of the options.</summary>
    StickyDelta,

    /// <summary>The implied volatilities are quoted in terms of at-the-money, risk reversal, and butterfly volatilities.</summary>
    /// <remarks>
    /// <para>This quoting format is common in the FX market. Valid formats are:</para>
    /// <list type="table">
    /// <item><term>ATM</term><description>At-the-money volatility</description></item>
    /// <item><term>[delta]RR</term><description>Risk reversal. Eg 25RR is the vol of the 25% delta call less the vol of the 25% delta put.</description></item>
    /// <item><term>[delta]BF</term><description>Butterfly. Eg 25BF is the vol of the long 25% delta put less the vol of the ATM call and ATM put.</description></item>
    /// </list>
    /// </remarks>
    RiskReversalButterfly,

    /// <summary>The implied volatilities are mapped with respect to moneyness (the ratios of the strikes to the ATM forwards).</summary>
    Moneyness,

    /// <summary>The volatility quotes are strike/price pairs.</summary>
    StrikePrice,

    /// <summary>The volatilities are quoted directly</summary>
    VolatilityQuote,
  }
}
