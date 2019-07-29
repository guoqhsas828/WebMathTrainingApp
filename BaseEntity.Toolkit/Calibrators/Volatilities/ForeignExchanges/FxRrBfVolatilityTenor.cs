//
// FxRbBfVolatilityTenor.cs
//   2012. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///  A tenor with Risk Reversal and Butterfly volatilities.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class FxRrBfVolatilityTenor : FxVolatilityTenor, ISmileDataProvider, IVolatilityLevelHolder
  {
    private readonly double[] _deltas;
    private readonly StrikeVolatilityPair[] _pairs;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRrBfVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The tenor name.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="deltas">The deltas.</param>
    /// <param name="volatilities">The volatilities.</param>
    /// <param name="flags">The flags.</param>
    /// <remarks></remarks>
    public FxRrBfVolatilityTenor(
      string name, Dt expiry,
      double[] deltas,
      double[] volatilities,
      FxVolatilityQuoteFlags flags)
      : base(name, expiry, flags)
    {
      if (volatilities == null || volatilities.Length == 0)
      {
        throw new ToolkitException("volatilities cannot be empty.");
      }
      if (deltas == null || deltas.Length == 0)
      {
        throw new ToolkitException("deltas cannot be empty.");
      }
      if (volatilities.Length != 1 + 2 * deltas.Length)
      {
        throw new ToolkitException("Volatilities and deltas not match.");
      }
      Volatilities = volatilities;
      _deltas = deltas;
      _pairs = new StrikeVolatilityPair[volatilities.Length];
    }

    /// <summary>
    /// Gets the FX volatility quote convention.
    /// </summary>
    /// <remarks></remarks>
    public override VolatilityQuoteType QuoteType
    {
      get { return VolatilityQuoteType.RiskReversalButterfly; }
    }

    /// <summary>
    /// Gets the strike volatility pairs.
    /// </summary>
    /// <remarks></remarks>
    public override StrikeVolatilityPair[] StrikeVolatilityPairs
    {
      get { return _pairs; }
    }

    /// <summary>
    /// Gets the risk reversal quotes.
    /// </summary>
    /// <remarks></remarks>
    public double[] RiskReversalQuotes
    {
      get { return QuoteValues.Take(_deltas.Length).ToArray(); }
    }

    /// <summary>
    /// Gets the butterfly quotes.
    /// </summary>
    /// <remarks></remarks>
    public double[] ButterflyQuotes
    {
      get { return QuoteValues.Skip(_deltas.Length + 1).ToArray(); }
    }

    public double[] Deltas
    {
      get { return _deltas; }
    }

    /// <summary>
    /// Gets the ATM volatility quote.
    /// </summary>
    /// <remarks></remarks>
    public double AtmQuote
    {
      get { return QuoteValues[_deltas.Length]; }
    }

    /// <summary>
    /// Gets or sets the volatility level (which is the ATM level for ATM-RR-BF quotes).
    /// </summary>
    /// <value>The level.</value>
    double IVolatilityLevelHolder.Level
    {
      get { return QuoteValues[_deltas.Length]; }
      set { QuoteValues[_deltas.Length] = value; }
    }

    /// <summary>
    /// Gets the quotes.
    /// </summary>
    /// <remarks></remarks>
    public override VolatilityQuote[] Quotes
    {
      get { return GetQuotes().ToArray(); }
    }

    private IEnumerable<VolatilityQuote> GetQuotes()
    {
      var n = _deltas.Length;
      yield return new VolatilityQuote("ATM", QuoteValues[n]);
      for (int i = 0; i < n; ++i)
      {
        var delta = (int)(_deltas[i] * 100);
        yield return new VolatilityQuote(String.Format("{0}RR", delta), QuoteValues[i]);
        yield return new VolatilityQuote(String.Format("{0}BF", delta), QuoteValues[n+1+i]);
      }
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
      var pairs = _pairs;
      double time = data.Time;
      double rd = data.Rate2;
      double rf = data.Rate1;
      double spotFx = data.Spot;
      double fwdFxRate = spotFx * Math.Exp((rd - rf) * time);
      int n = _deltas.Length, nn = n + n;
      double sigatm = QuoteValues[n];
      double katm = data.GetAtmStrike(AtmSetting,
        PremiumIncludedDelta, QuoteValues[n]);
      yield return pairs[n] = new StrikeVolatilityPair(katm, sigatm);

      double sqrtTime = Math.Sqrt(time);
      for (int i = 0; i < n; ++i)
      {
        double delta = _deltas[i];
        if (!ForwardDelta)
          delta = data.ConvertSpotToForwardDelta(delta);
        double rr = QuoteValues[i];
        if (Ccy2RiskReversal) rr = -rr;
        double vwb = QuoteValues[n + 1 + i] + sigatm;
        if (OneVolalityBufferfly)
        {
          vwb = BlackScholes.ImplyAverageVolatility(delta, vwb * sqrtTime,
            rr * sqrtTime, PremiumIncludedDelta, Ccy2Strangle) / sqrtTime;
        }
        // put strike
        var sigp = vwb - 0.5 * rr;
        var mp = BlackScholes.GetLogMoneynessFromDelta(
          delta, sigp * sqrtTime, -1, PremiumIncludedDelta);
        yield return pairs[i] = new StrikeVolatilityPair(
          Math.Exp(mp) * fwdFxRate, sigp);

        //call strike
        var sigc = vwb + 0.5 * rr;
        var mc = BlackScholes.GetLogMoneynessFromDelta(
          delta, sigc * sqrtTime, 1, PremiumIncludedDelta);
        yield return pairs[nn - i] = new StrikeVolatilityPair(
          Math.Exp(mc) * fwdFxRate, sigc);
      }
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks>This is overrided to provide better information in object browser.</remarks>
    public override string ToString()
    {
      return String.Format("ATM-RR-BF {0}", Name);
    }

    #region Nested type: Builder

    /// <summary>
    ///   Build FxRrBfVolatilityTenor from a sequence of volatility data.
    /// </summary>
    /// <remarks></remarks>
    public class Builder
    {
      private const int digits = 1000000;

      private readonly double[] _deltas;
      private readonly double[] _volatilities;

      /// <summary>
      /// Initializes a new instance of the <see cref="Builder"/> class.
      /// </summary>
      /// <param name="data">The data.</param>
      /// <remarks></remarks>
      public Builder(IEnumerable<KeyValuePair<FxRrBfSpec, double>> data)
      {
        double atmVol = 0;
        var rr = new Dictionary<int, double>();
        var bf = new Dictionary<int, double>();
        foreach (var pair in data)
        {
          var spec = pair.Key;
          if (spec.IsEmpty) continue;
          if (spec.IsAtm)
          {
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (atmVol != 0.0)
            {
              throw new ToolkitException("Duplicated ATM volatilities not allowed");
            }
            // ReSharper restore CompareOfFloatsByEqualityOperator
            atmVol = pair.Value;
            continue;
          }
          if (spec.IsRiskReversal)
          {
            rr.Add(ToInteger(spec), pair.Value);
            continue;
          }
          bf.Add(ToInteger(spec), pair.Value);
        }

        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (atmVol == 0.0)
        {
          throw new ToolkitException("Must specify ATM volatility");
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        // Find the intersect of RR abd BF deltas.
        int[] keys = rr.Keys.Where(bf.ContainsKey).OrderBy(k => k).ToArray();
        int n = keys.Length;
        double[] deltas = _deltas = new double[n];
        double[] volatilties = _volatilities = new double[2 * n + 1];
        for (int i = 0; i < n; ++i)
        {
          int key = keys[i];
          deltas[i] = ((double)key) / digits;
          volatilties[i] = rr[key];
          volatilties[n + i + 1] = bf[key];
        }
        volatilties[n] = atmVol;
      }

      private static int ToInteger(FxRrBfSpec spec)
      {
        return (int)(spec.Delta * digits);
      }

      /// <summary>
      /// Build the FxRrBfVolatilityTenor.
      /// </summary>
      /// <param name="name">The name.</param>
      /// <param name="expiry">The expiry.</param>
      /// <param name="flags">The flags.</param>
      /// <returns></returns>
      /// <remarks></remarks>
      public FxRrBfVolatilityTenor ToFxRrBfVolatilityTenor(
        string name, Dt expiry, FxVolatilityQuoteFlags flags)
      {
        return new FxRrBfVolatilityTenor(name,
          expiry, _deltas, _volatilities, flags);
      }
    }

    #endregion
  }
}