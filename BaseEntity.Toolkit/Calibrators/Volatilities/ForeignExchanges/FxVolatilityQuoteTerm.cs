/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///  FxVolatility quote terms
  /// </summary>
  /// <exclude>For internal use only.</exclude>
  /// <remarks></remarks>
  [Serializable]
  public class FxVolatilityQuoteTerm
  {
    private readonly TenorIndexedValues<AtmKind> _atmKinds;
    private readonly TenorIndexedValues<ButterflyQuoteKind> _bfs;
    private readonly Currency _ccy1, _ccy2;
    private readonly TenorIndexedValues<DeltaPremiumKind> _deltaPrems;
    private readonly TenorIndexedValues<DeltaKind> _deltaStyles;
    private readonly TenorIndexedValues<RiskReversalKind> _rrs;

    /// <summary>
    /// Parses the specified spot.
    /// </summary>
    /// <param name="ccy1">Currency 1 (the foreign/base/from currency).</param>
    /// <param name="ccy2">Currency 2 (the domestic/numeraire/to currency).</param>
    /// <param name="atmSettings">The atm tokens.</param>
    /// <param name="deltaPremiumTerms">The delta premium tokens.</param>
    /// <param name="deltaKinds">The delta kinds.</param>
    /// <param name="riskReversals">The risk reversals.</param>
    /// <param name="butterflyTerms">The butterfly tokens.</param>
    /// <returns></returns>
    /// <remarks>The format of tokens are:
    /// {Specification} Up To {'&lt;'|'&lt;'}{Tenor} Then</remarks>
    public FxVolatilityQuoteTerm(
      Currency ccy1,
      Currency ccy2,
      string atmSettings,
      string deltaPremiumTerms,
      string deltaKinds,
      string riskReversals,
      string butterflyTerms)
    {
      _ccy1 = ccy1;
      _ccy2 = ccy2;
      _atmKinds = atmSettings.Parse(
        s => (AtmKind)Enum.Parse(typeof(AtmKind), s));
      _deltaPrems = deltaPremiumTerms.Parse(
        s => (DeltaPremiumKind)Enum.Parse(typeof(DeltaPremiumKind), s));
      _deltaStyles = deltaKinds.Parse(
        s => (DeltaKind)Enum.Parse(typeof(DeltaKind), s));
      _rrs = riskReversals.Parse(s => ParseRiskReversal(s, ccy1, ccy2));
      _bfs = butterflyTerms.Parse(s => ParseButterfly(s, ccy1, ccy2));
    }

    /// <summary>
    /// Gets the currency 1 (base/from/foreign currency).
    /// </summary>
    /// <remarks></remarks>
    public Currency Ccy1
    {
      get { return _ccy1; }
    }

    /// <summary>
    /// Gets the currency 2 (numeraire/to/domestic currency).
    /// </summary>
    /// <remarks></remarks>
    public Currency Ccy2
    {
      get { return _ccy2; }
    }

    /// <summary>
    /// Gets the atm setting.
    /// </summary>
    /// <remarks></remarks>
    public string AtmSetting
    {
      get { return _atmKinds.ToString(); }
    }

    /// <summary>
    /// Gets the delta premium setting.
    /// </summary>
    /// <remarks></remarks>
    public string DeltaPremiumSetting
    {
      get { return _deltaPrems.ToString(); }
    }

    /// <summary>
    /// Gets the delta style setting.
    /// </summary>
    /// <remarks></remarks>
    public string DeltaStyleSetting
    {
      get { return _deltaStyles.ToString(); }
    }

    /// <summary>
    /// Gets the risk reversal setting.
    /// </summary>
    /// <remarks></remarks>
    public string RiskReversalSetting
    {
      get { return _rrs.ToString(); }
    }

    /// <summary>
    /// Gets the butterfly setting.
    /// </summary>
    /// <remarks></remarks>
    public string ButterflySetting
    {
      get { return _bfs.ToString(); }
    }

    private static RiskReversalKind ParseRiskReversal(
      string text, Currency ccy1, Currency ccy2)
    {
      string input = text;
      if (!text.StartsWith("Ccy", StringComparison.OrdinalIgnoreCase))
      {
        var ccy = (Currency)Enum.Parse(typeof(Currency), text.Substring(0, 3));
        if (ccy == ccy1) input = "Ccy1" + text.Substring(3);
        else if (ccy == ccy2) input = "Ccy2" + text.Substring(3);
      }
      return (RiskReversalKind)Enum.Parse(typeof(RiskReversalKind),
        Regex.Replace(input, @"[-\s]", ""));
    }

    private static ButterflyQuoteKind ParseButterfly(
      string text, Currency ccy1, Currency ccy2)
    {
      string input = Regex.Replace(text, @"^\(Call\s*\+\s*Put\)/2\s*\-.+$",
        "CallPutAverage", RegexOptions.IgnoreCase);
      if (!input.StartsWith("Ccy", StringComparison.OrdinalIgnoreCase)
        && !input.StartsWith("Call", StringComparison.OrdinalIgnoreCase))
      {
        var ccy = (Currency)Enum.Parse(typeof(Currency), text.Substring(0, 3));
        if (ccy == ccy1) input = "Ccy1" + text.Substring(3);
        else if (ccy == ccy2) input = "Ccy2" + text.Substring(3);
        else
        {
          throw new ToolkitException(String.Format(
            "{0} is not in the currency pair", ccy));
        }
      }
      int index = input.IndexOf('-');
      if (index > 0) input = input.Substring(0, index).Trim();
      return (ButterflyQuoteKind)Enum.Parse(typeof(ButterflyQuoteKind),
        Regex.Replace(input, @"[-\s]", ""));
    }

    /// <summary>
    /// Gets the kind of the atm.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public AtmKind GetAtmKind(Tenor tenor)
    {
      return _atmKinds.GetValue(tenor.Days);
    }

    /// <summary>
    /// Gets the delta style for the specified tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns>The delta style.</returns>
    /// <remarks></remarks>
    public DeltaStyle GetDeltaStyle(Tenor tenor)
    {
      int days = tenor.Days;
      DeltaStyle style = _deltaStyles.GetValue(days) == DeltaKind.Forward
        ? DeltaStyle.Forward
        : DeltaStyle.None;
      if (_deltaPrems.GetValue(days) == DeltaPremiumKind.Included)
        style |= DeltaStyle.PremiumIncluded;
      return style;
    }

    /// <summary>
    /// Gets the FX quote flags for the specified tenor.
    /// </summary>
    /// <param name="tenor">The tenor.</param>
    /// <returns>The flags.</returns>
    /// <remarks></remarks>
    public FxVolatilityQuoteFlags GetFlags(Tenor tenor)
    {
      int days = tenor.Days;
      FxVolatilityQuoteFlags flags = _deltaStyles.GetValue(days) == DeltaKind.Forward
        ? FxVolatilityQuoteFlags.ForwardDelta
        : FxVolatilityQuoteFlags.None;
      if (_deltaPrems.GetValue(days) == DeltaPremiumKind.Included)
        flags |= FxVolatilityQuoteFlags.PremiumIncludedDelta;
      AtmKind atm = _atmKinds.GetValue(days);
      if (atm == AtmKind.Forward) flags |= FxVolatilityQuoteFlags.ForwardAtm;
      else if (atm == AtmKind.Spot) flags |= FxVolatilityQuoteFlags.SpotAtm;
      RiskReversalKind rr = _rrs.GetValue(days);
      if (rr == RiskReversalKind.Ccy2CallPut || rr == RiskReversalKind.Ccy2PutCall)
        flags |= FxVolatilityQuoteFlags.Ccy2RiskReversal;
      ButterflyQuoteKind bf = _bfs.GetValue(days);
      if (bf == ButterflyQuoteKind.Ccy1Strangle)
        flags |= FxVolatilityQuoteFlags.OneVolatilityBufferfly;
      else if (bf == ButterflyQuoteKind.Ccy2Strangle)
      {
        flags |= FxVolatilityQuoteFlags.OneVolatilityBufferfly
          | FxVolatilityQuoteFlags.Ccy2Strangle;
      }
      return flags;
    }

    #region Nested type: ButterflyQuoteKind

    private enum ButterflyQuoteKind
    {
      CallPutAverage,
      Ccy1Strangle,
      Ccy2Strangle,
    }

    #endregion

    #region Nested type: DeltaKind

    private enum DeltaKind
    {
      Spot,
      Forward,
    }

    #endregion

    #region Nested type: DeltaPremiumKind

    private enum DeltaPremiumKind
    {
      Excluded,
      Included,
    }

    #endregion

    #region Nested type: RiskReversalKind

    private enum RiskReversalKind
    {
      Ccy1CallPut,
      Ccy1PutCall,
      Ccy2CallPut,
      Ccy2PutCall,
    }

    #endregion
  }

  internal static class TenorIndexedValues
  {
    internal static TenorIndexedValues<T> Parse<T>(
      this string tokens, Func<string, T> convert)
    {
      var dates = new List<int>();
      var values = new List<T>();
      foreach (string sect in Regex.Split(tokens,
        @"\bThen\b", RegexOptions.IgnoreCase))
      {
        string[] parts = Regex.Split(sect.Trim(), @"\bUp\s*To\b",
          RegexOptions.IgnoreCase).Select(s => s.Trim())
          .Where(s => !String.IsNullOrEmpty(s)).ToArray();
        if (parts.Length == 0) continue;
        T value = convert(parts[0]);
        if (parts.Length == 1)
        {
          values.Add(value);
          break;
        }
        string term = parts[1];
        if (term[0] == '<')
        {
          int days;
          if (term.Length > 1 && term[1] == '=')
          {
            days = Tenor.Parse(term.Substring(2)).Days;
          }
          else
          {
            days = Tenor.Parse(term.Substring(1)).Days - 1;
          }
          int count = dates.Count;
          if (count > 0 && dates[count - 1] >= days)
          {
            throw new ToolkitException("Tenors out of order");
          }
          dates.Add(days);
          values.Add(value);
          continue;
        }
        if (Regex.IsMatch(term, @"^N/?A\b|None\b", RegexOptions.IgnoreCase))
        {
          values.Add(value);
          break;
        }
        throw new ToolkitException(String.Format(
          "{0}: invalid term specification", term));
      }
      if (values.Count != dates.Count + 1)
      {
        throw new ToolkitException(String.Format(
          "{0}: invalid term specification", tokens));
      }
      return new TenorIndexedValues<T>(dates.ToArray(), values.ToArray());
    }

    internal static Tenor DaysToTenor(this int days)
    {
      if (days < 7)
      {
        return new Tenor(days, TimeUnit.Days);
      }
      if (days < 28)
      {
        return new Tenor((days + 1) / 7, TimeUnit.Weeks);
      }
      if (days < 30 * 12)
      {
        return new Tenor((days + 3) / 30, TimeUnit.Months);
      }
      return new Tenor((days + 6) / (30 * 12), TimeUnit.Years);
    }
  }

  [Serializable]
  internal class TenorIndexedValues<TValue> : IntervalIndexedValues<int, TValue>
  {
    public TenorIndexedValues(int[] tenors, TValue[] values)
      : base(tenors, values)
    {}

    public override string ToString()
    {
      var values = Values;
      var keys = Keys;
      int n = keys.Count;
      if (n == 0) return values[0].ToString();
      var sb = new StringBuilder();
      for (int i = 0; i < n; ++i)
      {
        if (i > 0) sb.Append(" Then ");
        sb.Append(values[i]).Append(" Up To ");
        int days = keys[i];
        Tenor tenor = days.DaysToTenor();
        sb.Append(days < tenor.Days ? "<" : "<=");
        sb.Append(tenor.ToString("S", null));
      }
      sb.Append(" Then ").Append(Values[n]);
      return sb.ToString();
    }
  }
}