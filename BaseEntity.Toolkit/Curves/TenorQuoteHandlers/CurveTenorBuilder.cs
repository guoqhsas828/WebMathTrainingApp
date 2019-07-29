//
//  -2017. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.TenorQuoteHandlers
{
  using ReferenceIndexTable = List<KeyValuePair<KeyValuePair<
    IReferenceRate, Tenor>,ReferenceIndex>>;

  internal class CurveTenorBuilder
  {
    // Look-up table to look up index based on (Rate,Tenor) pair.
    // This avoid duplicated creation of the same reference index
    // when the tenors are built with the same builder.
    private readonly ReferenceIndexTable _table = new ReferenceIndexTable();

    /// <summary>
    ///   Main driver to create a collection of curve tenors
    /// </summary>
    /// <param name="tenorNames">Tenor names to be fed to the product terms</param>
    /// <param name="quoteValues">Market quote values</param>
    /// <param name="quotingConventions">Array quoting conventions.
    ///   If the array is null or the element values are <c>QuotingConvention.None</c>,
    ///   then the quoting conventions is inferred from the corresponding instrument types.</param>
    /// <param name="instruments">The instruments of the quotes, which can be either the conventional instrument names, or standard product terms</param>
    /// <param name="referenceRate">The primary reference rates of the market quotes</param>
    /// <param name="indexTenor">The primary index tenors</param>
    /// <param name="lastShortTenor">Last short tenor (inclusive)</param>
    /// <param name="referenceRate2">The secondary reference rates, for example, the reference rate of the second swap leg.</param>
    /// <param name="indexTenor2">The secondary index tenors</param>
    /// <param name="location">Currencies (Countries) of trading</param>
    /// <param name="lastShortTenor2">Last short tenor (inclusive)</param>
    /// <param name="future">Stir future contract codes or tenors.</param>
    /// <param name="futureExchange">Stir future exchanges.</param>
    /// <returns>An enumerable list of curve tenors</returns>
    public IEnumerable<CurveTenor> Build(
      IReadOnlyList<string> tenorNames,
      IReadOnlyList<double> quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions,
      IReadOnlyList<object> instruments,
      IReferenceRate referenceRate,
      string indexTenor,
      string lastShortTenor,
      Currency location,
      IReferenceRate referenceRate2,
      string indexTenor2,
      string lastShortTenor2,
      string future,
      string futureExchange
      )
    {
      // Then handle the general case.
      for (int i = 0, n = tenorNames.Count; i < n; ++i)
      {
        var instrument = instruments[i];
        if (string.IsNullOrEmpty(tenorNames[i]) ||
          instrument == null || instrument.Equals(string.Empty) ||
          double.IsNaN(quoteValues[i]))
        {
          continue;
        }
        var index = GetReferenceIndex(referenceRate,
          indexTenor, _table);
        var terms = (instrument as IStandardProductTerms)
          ?? GetTerms((string)instrument, tenorNames[i], referenceRate, indexTenor, lastShortTenor,
            referenceRate2, indexTenor2, lastShortTenor2, future, futureExchange, location);
        yield return new CurveTenor(new TenorQuote(tenorNames[i],
          GetQuote(terms, instrument, quoteValues[i], quotingConventions?[i] ?? QuotingConvention.None),
          terms, index), 1.0);
      }
    }

    /// <summary>
    ///   Main driver to create a collection of FX curve tenors
    /// </summary>
    /// <param name="tenorNames">Tenor names to be fed to the product terms</param>
    /// <param name="quoteValues">Market quote values</param>
    /// <param name="quotingConventions">Array quoting conventions.
    ///   If the array is null or the element values are <c>QuotingConvention.None</c>,
    ///   then the quoting conventions is inferred from the corresponding instrument types.</param>
    /// <param name="instruments">The instruments of the quotes, which can be either the conventional instrument names, or standard product terms</param>
    /// <param name="fxRate">Fx rate</param>
    /// <param name="inverted">Inverted fx rates</param>
    /// <param name="referenceRate">The primary reference rates of the market quotes</param>
    /// <param name="indexTenor">The primary index tenors</param>
    /// <param name="referenceRate2">The secondary reference rates, for example, the reference rate of the second swap leg.</param>
    /// <param name="indexTenor2">The secondary index tenors</param>
    /// <param name="future">Fx future codes</param>
    /// <param name="futuresExchange">Fx future exchanges</param>
    /// <param name="location">Currencies (Countries) of trading</param>
    /// <returns>An enumerable list of curve tenors</returns>
    public IEnumerable<CurveTenor> FxBuild(
      IReadOnlyList<string> tenorNames,
      IReadOnlyList<double> quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions,
      IReadOnlyList<object> instruments,
      IReferenceRate fxRate,
      bool inverted,
      IReferenceRate referenceRate,
      string indexTenor,
      IReferenceRate referenceRate2,
      string indexTenor2,
      Currency location,
      string future,
      string futuresExchange
      )
    {
      var index = GetReferenceIndex(fxRate, indexTenor, _table);

      // Then handle the general case.
      for (int i = 0, n = tenorNames.Count; i < n; ++i)
      {
        var instrument = instruments[i];
        if (string.IsNullOrEmpty(tenorNames[i]) ||
          instrument == null || instrument.Equals(string.Empty) ||
          double.IsNaN(quoteValues[i]))
        {
          continue;
        }
        var terms = (instrument as IStandardProductTerms)
          ?? GetFxTerms((string)instrument, location,
              fxRate, inverted,
              referenceRate, indexTenor,
              referenceRate2, indexTenor2,
              future, futuresExchange);

        yield return new CurveTenor(new TenorQuote(tenorNames[i],
          GetQuote(terms, instrument, quoteValues[i], quotingConventions[i]),
          terms, index), 1.0);
      }
    }

    /// <summary>
    ///   Main driver to create a collection of survival curve tenors
    /// </summary>
    /// <param name="transactionType">Credit derivative transaction name or abreviation</param>
    /// <param name="tenorNames">Tenor names to be fed to the product terms</param>
    /// <param name="quoteValues">Market quote values</param>
    /// <param name="quotingConventions">Array quoting conventions.
    ///   If the array is null or the element values are <c>QuotingConvention.None</c>,
    ///   then the quoting conventions is inferred from the corresponding instrument types.</param>
    /// <returns>An enumerable list of curve tenors</returns>
    public IEnumerable<CurveTenor> CdsBuild(
      object transactionType,
      IReadOnlyList<string> tenorNames,
      IReadOnlyList<double> quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions)
    {
      // Then handle the general case.
      for (int i = 0, n = tenorNames.Count; i < n; ++i)
      {       
        if (double.IsNaN(quoteValues[i]))
          continue;

        var key = CdsTerms.GetKey(
          transactionType as CreditDerivativeTransactionType? 
          ?? ((string)transactionType).ToCreditDerivativeTransactionType());
        var terms = ToolkitCache.StandardProductTermsCache.GetValue<CdsTerms>(key);

        yield return new CurveTenor(new TenorQuote(tenorNames[i],
          new MarketQuote(quoteValues[i], quotingConventions[i]), terms, null), 1.0);
      }
    }

    /// <summary>Find the standard product terms</summary>
    private static IStandardProductTerms GetTerms(
      string instrumentType,
      string tenor,
      IReferenceRate referenceRate,
      string indexTenor,
      string lastShortTenor,
      IReferenceRate secondReferenceRate,
      string indexTenor2,
      string lastShortTenor2,
      string future,
      string futureExchange,
      Currency location)
    {
      try
      {
        Tenor rateTenor;
        switch (instrumentType)
        {
          case "FUNDMM":
          case "MM":
            return SimpleRateTerms<Note>.Create(referenceRate,
          StandardProductTermsUtil.GetStandardMoneyMarket, instrumentType);

          case "FRA":
            return SimpleRateTerms<FRA>.Create(referenceRate,
              StandardProductTermsUtil.GetStandardFra, instrumentType);

          case "FUT":
          {
            // Default to 3M LIBOR futures if multiple are found with the same LIBOR rate from standard terms
            Func<IStandardFutureTerms, bool> filter = o => (o is StirFutureTerms) && ((StirFutureTerms)o).Tenor == Tenor.ThreeMonths;
            return GetFutureTerms(referenceRate, future, futureExchange, filter);
          }
          case "Swap":
            // The ISDA curve calibration only requires a fixed swap leg
            if (referenceRate is ISDAInterestReferenceRate)
            {
              return SimpleRateTerms<SwapLeg>.Create(referenceRate,
              StandardProductTermsUtil.GetStandardFixedSwapLeg, instrumentType);
            }
            if (Tenor.TryParse(GetTenor(indexTenor, lastShortTenor, tenor), out rateTenor))
            {
              return StandardProductTermsUtil.GetFixedFloatSwapTerms(
                location, referenceRate, referenceRate.Currency, rateTenor);
            }
            return null;
          case "Basis":
          case "BasisSwap":
            Tenor rateTenor2;
            if (Tenor.TryParse(GetTenor(indexTenor, lastShortTenor, tenor), out rateTenor)
              && Tenor.TryParse(GetTenor(indexTenor2, lastShortTenor2, tenor), out rateTenor2))
            {
              return StandardProductTermsUtil.GetBasisSwapTerms(
              location, referenceRate, Tenor.Parse(indexTenor), secondReferenceRate, Tenor.Parse(indexTenor2));
            }
            return null;
          case "ZeroCouponSwap":
            return StandardProductTermsUtil.GetInflationSwapTerms(referenceRate,
              Frequency.None, referenceRate.Currency);
          case "YoYSwap":
          case "YearOnYearSwap":
            return StandardProductTermsUtil.GetInflationSwapTerms(referenceRate,
              Frequency.Annual, referenceRate.Currency);

        }
        return FindTerms(instrumentType);
      }
      catch (Exception e)
      {
        throw new ToolkitException($"Failed looking up terms for {instrumentType}.", e);
      }
    }

    /// <summary>
    /// Split tenors into long and short tenors
    /// </summary>
    /// <param name="swapTenors"></param>
    /// <param name="lastShortTenor"></param>
    /// <param name="maturityTenor"></param>
    /// <returns></returns>
    internal static string GetTenor(string swapTenors, string lastShortTenor, string maturityTenor)
    {
      Tenor productMaturityTenor;
      if (!Tenor.TryParse(maturityTenor, out productMaturityTenor)) return "";
      if (!swapTenors.Contains('/')) return swapTenors;
      Tenor lastTenor;
      if (string.IsNullOrWhiteSpace(lastShortTenor) || !Tenor.TryParse(lastShortTenor, out lastTenor))
        throw new Exception($"Swaps with multiple tenors \"{swapTenors}\" require a lastShortTenor");
      var words = swapTenors.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
      if (words.Count() == 2)
        return lastTenor.CompareTo(productMaturityTenor) >= 0 ? words[0] : words[1];
      throw new Exception(
        $"Tenor not determined for swap with tenors \"{swapTenors}\" and last short tenor \"{lastShortTenor}\"");
    }

    /// <summary>Find the standard FX product terms</summary>
    /// <remarks>Separate method because we need three reference rates (two IR and the FX rate)</remarks>
    private static IStandardProductTerms GetFxTerms(
      string instrumentType,
      Currency location,
      IReferenceRate fxRate,
      bool inverted,
      IReferenceRate interestRefRate1,
      string indexTenor1,
      IReferenceRate interestRefRate2,
      string indexTenor2,
      string futureCode,
      string futureExchange
      )
    {
      try
      {
        switch (instrumentType)
        {
          case "FUT":
            {
              return GetFutureTerms(fxRate, futureCode, futureExchange);
            }
          case "Basis":
          case "BasisSwap":
            return StandardProductTermsUtil.GetBasisSwapTerms(
              location, interestRefRate1, Tenor.Parse(indexTenor1), interestRefRate2, Tenor.Parse(indexTenor2));

          case "FxFwd":
            return SimpleRateTerms<FxForward>.Create(fxRate,
            new StandardProductTermsUtil.FxProductHelper(inverted).GetStandardFxForward, instrumentType);
        }
        return FindTerms(instrumentType);
      }
      catch (Exception e)
      {
        throw new ToolkitException($"Failed looking up terms for {instrumentType}.", e);
      }
    }

    private static ReferenceIndex GetReferenceIndex(
      IReferenceRate referenceRate, string indexTenor,
      ReferenceIndexTable lookupTable)
    {
      var tenor = string.IsNullOrEmpty(indexTenor)
        ? Tenor.Empty : Tenor.Parse(indexTenor);
      if (referenceRate == null)
        return null;
      if (lookupTable == null)
        return referenceRate.GetReferenceIndex(tenor);
      var key = MakePair(referenceRate, tenor);
      var index = LookUp(lookupTable, key);
      if (index != null) return index;
      index = referenceRate.GetReferenceIndex(tenor);
      lookupTable.Add(MakePair(key, index));
      return index;
    }

    private static MarketQuote GetQuote(
      IStandardProductTerms terms, object instrument,
      double quoteValue, QuotingConvention quoteType)
    {
      const StringComparison nocase = StringComparison.OrdinalIgnoreCase;
      var instrumentType = instrument as string;
      if (quoteType != QuotingConvention.None)
        return new MarketQuote(quoteValue, quoteType);
      if (instrumentType != null)
      { 
        if (instrumentType.Length >= 3 && String.Compare(instrumentType, "FRA", nocase) == 0)
          return new MarketQuote(quoteValue, QuotingConvention.FlatPrice);
        if (instrumentType.Length >= 3 && String.Compare(instrumentType.Substring(0,3), "FUT", nocase) == 0)
          return new MarketQuote(quoteValue, QuotingConvention.FlatPrice);
      }
      var swapTerms = terms as SwapTerms;
      if ((swapTerms == null)) return new MarketQuote(quoteValue, QuotingConvention.Yield);
      return swapTerms.IsBasisSwap() 
        ? new MarketQuote(quoteValue * 1e-4, QuotingConvention.YieldSpread) 
        : new MarketQuote(quoteValue, QuotingConvention.Yield);
    }

    private static IStandardFutureTerms GetFutureTerms(
      IReferenceRate referenceRate, string futuresCode, string exchange, Func<IStandardFutureTerms, bool> func1 = null)
    {
      // Check by futures contract code and exchange first
      if (!string.IsNullOrEmpty(exchange) && !string.IsNullOrEmpty(futuresCode))
        return StandardProductTermsUtil.GetFutureTerms<FutureBase>(futuresCode, exchange);

      // Next by futures contract code
      IStandardFutureTerms[] futures = null;
      if (!string.IsNullOrEmpty(futuresCode))
      {
        var products = StandardProductTermsUtil.GetFutureTerms<FutureBase>(futuresCode);
        if (products.Count() == 1)
          return (IStandardFutureTerms)products[0];
        futures = products;
      }

      // Finally by reference rate
      if (referenceRate == null)
        throw new ToolkitException("Unable to find futures without reference rate");

      // Special handling for FX reference rates
      Func<IStandardFutureTerms, bool> f1 = null;
      var rate = referenceRate as FxReferenceRate;
      if (rate != null)
      {
        var fxRate = rate;
        var inverseFx = fxRate.Currency.ToString() + fxRate.ForeignCurrency.ToString();
        if (string.IsNullOrEmpty(exchange))
          f1 = t => t.GetIndexName() == referenceRate.Key || t.GetIndexName() == inverseFx;
        else
          f1 = t => (t.GetIndexName() == referenceRate.Key || t.GetIndexName() == inverseFx) && t.GetExchange() == exchange;
      }
      else
      {
        if (string.IsNullOrEmpty(exchange))
          f1 = t => t.GetIndexName() == referenceRate.Key;
        else
          f1 = t => t.GetIndexName() == referenceRate.Key && t.GetExchange() == exchange;
      }

      // Select from futures (if determined) otherwise all futures within standard products cache
      var matches = (futures ?? ToolkitCache.StandardProductTermsCache.Values).OfType<IStandardFutureTerms>().Where(f1).ToArray();
      var n = matches.Count();
      switch (n)
      {
        case 0:
          throw new ToolkitException($"Unable to find futures with index {referenceRate.Key}");
        case 1:
          return matches[0];
        default:
        {
          if (func1 != null)
          {
            var matches1 = matches.Where(func1).ToArray();
            if (matches1.Length == 1)
              return matches1[0];
          }
          break;
        }
      }

      throw new ToolkitException(
        $"Ambiguous futures ({matches.Aggregate("", (s, t) => string.IsNullOrEmpty(s) ? t.GetContractCode() : s + "," + t.GetContractCode())})");
    }

    private static IStandardProductTerms FindTerms(string instrument)
    {
      IStandardProductTerms terms;
      if (ToolkitCache.StandardProductTermsCache.TryGetValue(instrument, out terms)
        || ToolkitCache.StandardProductTermsCache.TryGetValue(instrument + "Terms", out terms))
      {
        return terms;
      }
      throw new ArgumentException($"Unknown instrument type: {instrument}");
    }

    private static KeyValuePair<TKey, TValue> MakePair<TKey, TValue>(
      TKey key, TValue value)
    {
      return new KeyValuePair<TKey, TValue>(key, value);
    }

    private static TValue LookUp<TKey, TValue>(
      IEnumerable<KeyValuePair<TKey, TValue>> list,
      TKey key)
    {
      return list.FirstOrDefault(p => p.Key.Equals(key)).Value;
    }

  }
}
