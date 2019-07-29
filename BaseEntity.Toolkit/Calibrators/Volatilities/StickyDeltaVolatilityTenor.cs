/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Represents a tenor with sticky delta quotes.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class StickyDeltaVolatilityTenor : PlainVolatilityTenor, ISmileDataProvider
  {
    private readonly AtmKind _atmKind;
    private readonly DeltaStyle _deltaStyle;
    private readonly StrikeVolatilityPair[] _pairs;
    private readonly DeltaSpec[] _specs;

    /// <summary>
    /// Initializes a new instance of the <see cref="StickyDeltaVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The tenor name.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="deltas">The deltas.</param>
    /// <param name="volatilities">The volatilities.</param>
    /// <param name="atmKind">Kind of the atm.</param>
    /// <param name="deltaStyle">The delta style.</param>
    /// <remarks></remarks>
    public StickyDeltaVolatilityTenor(
      string name, Dt expiry,
      DeltaSpec[] deltas, double[] volatilities,
      AtmKind atmKind, DeltaStyle deltaStyle)
      : base(name, expiry)
    {
      if (volatilities == null || volatilities.Length == 0)
      {
        throw new ToolkitException("volatilities cannot be empty.");
      }
      if (deltas == null || deltas.Length == 0)
      {
        throw new ToolkitException("deltas cannot be empty.");
      }
      if (volatilities.Length != deltas.Length)
      {
        throw new ToolkitException("Volatilities and deltas not match.");
      }
      Volatilities = volatilities;
      _pairs = new StrikeVolatilityPair[volatilities.Length];
      _specs = deltas;
      _atmKind = atmKind;
      _deltaStyle = deltaStyle;
    }

    /// <summary>
    /// Gets the FX volatility quote convention.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityQuoteType QuoteType
    {
      get { return VolatilityQuoteType.StickyDelta; }
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
    /// Gets the quotes.
    /// </summary>
    /// <remarks></remarks>
    public VolatilityQuote[] Quotes
    {
      get
      {
        return Enumerable.Range(0, _specs.Length)
          .Select(i => new VolatilityQuote(_specs[i].ToString(),
            QuoteValues[i])).ToArray();
      }
    }

    /// <summary>
    /// Gets the array of quoted deltas.
    /// </summary>
    /// <value>The quoted deltas, or null if the volatilities are quoted with sticky strike rule.</value>
    internal DeltaSpec[] Deltas
    {
      get { return _specs; }
    }

    /// <summary>
    /// Gets the ATM setting.
    /// </summary>
    /// <remarks></remarks>
    public AtmKind AtmSetting
    {
      get { return _atmKind; }
    }

    /// <summary>
    /// Gets the ATM volaitility.
    /// </summary>
    /// <remarks></remarks>
    internal double AtmQuote
    {
      get
      {
        return Enumerable.Range(0, _specs.Length)
          .SkipWhile(i => !_specs[i].IsAtm)
          .Select(i => _pairs[i]).FirstOrDefault().Volatility;
      }
    }

    /// <summary>
    /// Gets a value indicating premium included delta.
    /// </summary>
    /// <remarks></remarks>
    public bool PremiumIncludedDelta
    {
      get { return (_deltaStyle & DeltaStyle.PremiumIncluded) != 0; }
    }

    /// <summary>
    /// Gets a value indicating premium included delta.
    /// </summary>
    /// <remarks></remarks>
    public bool ForwardDelta
    {
      get { return (_deltaStyle & DeltaStyle.Forward) != 0; }
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks>This is overriden to provide better infomation in object browser.</remarks>
    public override string ToString()
    {
      return String.Format("StickyDelta {0}", Name);
    }

    #region ISmileDataProvider Members

    /// <summary>
    /// Gets the strike/volatility pairs associated with the specified Black-Scholes parameter data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>A sequence of strike/volatility pairs.</returns>
    /// <remarks></remarks>
    public IEnumerable<StrikeVolatilityPair> GetStrikeVolatilityPairs(
      IBlackScholesParameterData data)
    {
      return Enumerable.Range(0, QuoteValues.Count)
        .Select(i =>
        {
          DeltaSpec spec = _specs[i];
          double sigma = QuoteValues[i];
          double strike = spec.IsAtm
            ? data.GetAtmStrike(_atmKind, PremiumIncludedDelta, sigma)
            : data.GetDeltaStrike(
              spec.IsCall ? OptionType.Call : OptionType.Put,
              _deltaStyle, spec.Delta, sigma);
          return _pairs[i] = new StrikeVolatilityPair(strike, sigma);
        });
    }

    #endregion
  }
}