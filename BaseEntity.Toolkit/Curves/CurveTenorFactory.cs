//
//  -2017. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Curves.TenorQuoteHandlers;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  ///   Factory to build curve tenors
  /// </summary>
  public static class CurveTenorFactory
  {
    #region Update products

    /// <summary>
    ///   Update tenor product to reflect any changes in trade date, quotes, etc.
    /// </summary>
    /// <param name="tenor">The tenor to update</param>
    /// <param name="tradeDate">The trade date</param>
    internal static CurveTenor UpdateProduct(
      this CurveTenor tenor, Dt tradeDate)
    {
      // tenor is null, product exists or no product terms exist
      if (tenor == null || tenor.Product != null || tenor.ProductTerms == null)
        return tenor;
      var p = tenor.ProductTerms.GetProductInfo(tradeDate, tenor.QuoteTenor, tenor.CurrentQuote ?? tenor.MarketQuote);
      tenor.MarketPv = p.TargetValue;
      tenor.Product = p.Product;
      if (string.IsNullOrWhiteSpace(p.Product.Description))
        p.Product.Description = tenor.Name;
      return tenor;
    }

    /// <summary>
    ///   Update a set of tenor products to reflect any changes in trade date, quotes, etc.
    /// </summary>
    /// <param name="tenors">The tenors to update</param>
    /// <param name="tradeDate">The trade date</param>
    internal static void UpdateProducts(
      this IEnumerable<CurveTenor> tenors, Dt tradeDate)
    {
      if (tenors == null) return;
      foreach (var tenor in tenors)
        tenor.UpdateProduct(tradeDate);
    }


    /// <summary>
    ///   Update all the tenor products on the specified curve
    ///   base on Calibrator's as-of date.
    /// </summary>
    /// <param name="curve">The curve to update tenor products.</param>
    internal static T UpdateTenorProducts<T>(this T curve) where T : Curve
    {
      var calibratedCurve = curve as CalibratedCurve;
      if (calibratedCurve != null
        && calibratedCurve.Tenors != null
        && calibratedCurve.Calibrator != null)
      {
        calibratedCurve.Tenors.UpdateProducts(calibratedCurve.Calibrator.AsOf);
      }
      return curve;
    }

    private static CurveTenorProductInfo GetProductInfo(
      this IStandardProductTerms spt,
      Dt date, string tenorName, IMarketQuote quote)
    {
      var builder = CurveTenorProductBuilders.Get(spt);
      if (builder != null)
      {
        return new CurveTenorProductInfo(
          builder(spt, date, tenorName, quote),
          (spt is IStandardFutureTerms ? quote.Value : 0));
      }

      throw new ToolkitException($"Unable to build product from {spt.GetType()}");
    }

    #endregion

    #region Tenor Creation

    /// <summary>
    ///  Create a collection of tenor quotes
    /// </summary>
    /// <param name="tenorNames">An array of tenor names</param>
    /// <param name="instruments">Instrument names or keys of standard product terms.
    ///   A single value for all the quotes, or an array matching tenor names</param>
    /// <param name="quoteValues">Quote values, a matrix with rows matching the tenor names.  Empty values are ignored</param>
    /// <param name="quotingConventions">Quoting conventions.
    ///   A single value for all the quotes, or an array matching instrument names</param>
    /// <param name="referenceRates">Primary reference rates.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="indexTenors">Primary index tenors.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="lastShortTenors">Last short tenor (inclusive)</param>
    /// <param name="referenceRates2">Reference rates for the second floating legs.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="indexTenors2">Index tenors for the second floating legs.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="lastShortTenors2">Last short tenor (inclusive)</param>
    /// <param name="futures">Stir future contract codes or tenors.</param>
    /// <param name="futuresExchanges">Stir future exchanges.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="locations">Currencies (Countries) of trading.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <returns>Tenor quote collection</returns>
    public static IEnumerable<CurveTenor> BuildTenors(
      IReadOnlyList<string> tenorNames,
      IReadOnlyList<object> instruments,
      double[,] quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions,
      IReferenceRate[] referenceRates,
      string[] indexTenors,
      string[] lastShortTenors = null,
      IReferenceRate[] referenceRates2 = null,
      string[] indexTenors2 = null,
      string[] lastShortTenors2 = null,
      string[] futures = null,
      string[] futuresExchanges = null,
      Currency[] locations = null)
    {
      if (tenorNames == null || tenorNames.Count == 0)
        throw new ArgumentException("tenorNames cannot be empty");

      var count = tenorNames.Count;
      if (quoteValues == null || quoteValues.GetLength(0) != count)
        throw new ArgumentException($"Dimensions of quoteValues ({quoteValues?.GetLength(0)??0}) and tenorNames ({tenorNames.Count}) do not match");
      var builder = new CurveTenorBuilder();

      var tenors = new List<CurveTenor>();
      for (var i = 0; i < quoteValues.GetLength(1); i++)
      {
        var list = builder.Build(tenorNames,
          quoteValues.Column(i),
          ValidateOptionalList(quotingConventions,
            count, () => "quotingConventions and tenorNames not match"),
          ValidateList(instruments,
            count, () => "instruments and tenorNames not match"),
          referenceRates[i],
          ArrayUtil.IsNullOrEmpty(indexTenors) ? "" : indexTenors[i],
          ArrayUtil.IsNullOrEmpty(lastShortTenors) ? "" : lastShortTenors[i],
          ArrayUtil.IsNullOrEmpty(locations) ? Currency.None : locations[i],
          ArrayUtil.IsNullOrEmpty(referenceRates2) ? null : referenceRates2[i],
          ArrayUtil.IsNullOrEmpty(indexTenors2) ? "" : indexTenors2[i],
          ArrayUtil.IsNullOrEmpty(lastShortTenors2) ? "" : lastShortTenors2[i],
          ArrayUtil.IsNullOrEmpty(futures) ? "" : futures[i],
          ArrayUtil.IsNullOrEmpty(futuresExchanges) ? "" : futuresExchanges[i]
        ).ToArray();

        for (var j = 0; j < list.Count(); j++)
        {
          var matches = tenors.Where(o => o.Name == list[j].Name && o.ReferenceIndex.IsEqual(list[j].ReferenceIndex))?.ToArray();
          if (matches == null || matches.Length == 0)
            tenors.Add(list[j]);
          else if (matches[0].MarketQuote.Value != list[j].MarketQuote.Value)
            throw new ToolkitException($"Duplicate tenor {list[j].Name} with different quote value not added!");
        }
      }

      return tenors;
    }

    /// <summary>
    ///  Convenient curve tenor constructor for futures only curves
    /// </summary>
    /// <param name="tenorNames"></param>
    /// <param name="quoteValues"></param>
    /// <param name="quotingConventions"></param>
    /// <param name="referenceRate"></param>
    /// <param name="futures"></param>
    /// <param name="futuresExchanges"></param>
    /// <returns></returns>
    public static IEnumerable<CurveTenor> BuildFuturesTenors(
      IReadOnlyList<string> tenorNames,
      double[] quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions,
      IReferenceRate referenceRate,
      string futures = null,
      string futuresExchanges = null)
    {
      return CurveTenorFactory.BuildTenors(tenorNames,
        tenorNames.Select(o => "FUT").ToArray(),
        quoteValues.ToColumn(), 
        quotingConventions,
        new [] { referenceRate },
        null, null, null, null, null,
        new[] { futures },
        new[] { futuresExchanges },
        null).ToList();
    }

    /// <summary>
    ///  Convenient curve tenor constructor for futures only curves
    /// </summary>
    /// <param name="tenorNames"></param>
    /// <param name="quoteValues"></param>
    /// <param name="quotingConventions"></param>
    /// <param name="referenceRates"></param>
    /// <param name="futures"></param>
    /// <param name="futuresExchanges"></param>
    /// <returns></returns>
    public static IEnumerable<CurveTenor> BuildFuturesTenors(
      IReadOnlyList<string> tenorNames,
      double[,] quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions,
      IReferenceRate[] referenceRates,
      string[] futures = null,
      string[] futuresExchanges = null)
    {
      return CurveTenorFactory.BuildTenors(tenorNames,
        tenorNames.Select(o => "FUT").ToArray(),
        quoteValues, quotingConventions,
        referenceRates,
        null, null, null, null, null,
        futures,
        futuresExchanges,
        null).ToList();
    }

    /// <summary>
    ///  Create a collection of tenor quotes
    /// </summary>
    /// <param name="tenorNames">An array of tenor names</param>
    /// <param name="transactionType">Transaction types.
    ///   A single value for all the quotes, or an array matching tenor names</param>
    /// <param name="quoteValues">Quote values, a matrix with rows matching the tenor names.  Empty values are ignored</param>
    /// <param name="quotingConventions">Quoting conventions.
    ///   A single value for all the quotes, or an array matching instrument names</param>
    /// <returns>Tenor quote collection</returns>
    public static IEnumerable<CurveTenor> BuildCdsTenors(
      object transactionType,
      IReadOnlyList<string> tenorNames,
      double[,] quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions)
    {
      if (tenorNames == null || tenorNames.Count == 0)
        throw new ArgumentException("tenorNames cannot be empty");

      var count = tenorNames.Count;
      if (quoteValues == null || (quoteValues.GetLength(0) != count && quoteValues.GetLength(1) != count))
        throw new ArgumentException("quoteValues and tenorNames not match");
      var builder = new CurveTenorBuilder();

      var byRow = quoteValues.GetLength(0) == 1 ? true : false;

      var tenors = new List<CurveTenor>();
      for (var i = 0; i < quoteValues.GetLength(1); i++)
      {
        var list = builder.CdsBuild(transactionType,
          tenorNames,
          byRow ? quoteValues.Row(i) : quoteValues.Column(i),
          ValidateOptionalList(quotingConventions,
            count, () => "quotingConventions and tenorNames not match")).ToArray();

        for (var j = 0; j < list.Count(); j++)
        {
          var matches = tenors.Where(o => o.Name == list[j].Name).ToArray();
          if (matches.Length == 0)
            tenors.Add(list[j]);
          else if (matches[0].MarketQuote.Value != list[j].MarketQuote.Value)
            throw new ToolkitException($"Duplicate tenor {list[j].Name} with different quote value not added!");
        }
      }

      return tenors;
    }

    /// <summary>
    ///  Create a collection of tenor quotes
    /// </summary>
    /// <param name="tenorNames">An array of tenor names</param>
    /// <param name="instruments">Instrument names or keys of standard product terms.
    ///   A single value for all the quotes, or an array matching tenor names</param>
    /// <param name="quoteValues">Quote values, a matrix with rows matching the tenor names.  Empty values are ignored</param>
    /// <param name="quotingConventions">Quoting conventions.
    ///   A single value for all the quotes, or an array matching instrument names</param>
    /// <param name="interestReferenceRate1">Primary reference rates.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="indexTenors1">Primary index tenors.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="interestReferenceRate2">Reference rates for the second floating legs.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="indexTenors2">Index tenors for the second floating legs.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="locations">Currencies (Countries) of trading.
    ///   A single value for all the quotes, or a row matching the columns of quote values,
    ///    or a column matching tenor names, or a matrix match both rows and columns of the quotes</param>
    /// <param name="fxRates">FX Rates for FX Curve quotes</param>
    /// <param name="inverted">FX Rates for FX Curve quotes to be inverted with respect to the index</param>
    /// <param name="futures">FX futures</param>
    /// <param name="futuresExchanges">Futures exchanges</param>
    /// <returns>Tenor quote collection</returns>
    public static IEnumerable<CurveTenor> BuildFxTenors(
      IReadOnlyList<string> tenorNames,
      IReadOnlyList<object> instruments,
      double[,] quoteValues,
      IReadOnlyList<QuotingConvention> quotingConventions,
      IReferenceRate[] fxRates,
      bool[] inverted,
      IReferenceRate[] interestReferenceRate1,
      string[] indexTenors1,
      IReferenceRate[] interestReferenceRate2,
      string[] indexTenors2,
      string[] futures,
      string[] futuresExchanges,
      Currency[] locations)
    {
      if (tenorNames == null || tenorNames.Count == 0)
        throw new ArgumentException("tenorNames cannot be empty");

      var count = tenorNames.Count;
      if (quoteValues == null || quoteValues.GetLength(0) != count)
        throw new ArgumentException("quoteValues and tenorNames not match");
      var builder = new CurveTenorBuilder();

      var tenors = new List<CurveTenor>();
      for (var i = 0; i < quoteValues.GetLength(1); i++)
      {
        var list = builder.FxBuild(tenorNames,
          quoteValues.Column(i),
          ValidateOptionalList(quotingConventions,
            count, () => "quotingConventions and tenorNames not match"),
          ValidateList(instruments,
            count, () => "instruments and tenorNames not match"),
          fxRates[i],
          inverted[i],
          interestReferenceRate1[i],
          ArrayUtil.IsNullOrEmpty(indexTenors1) ? "" : indexTenors1[i],
          interestReferenceRate2[i],
          ArrayUtil.IsNullOrEmpty(indexTenors2) ? "" : indexTenors2[i],
          ArrayUtil.IsNullOrEmpty(locations) ? Currency.None : locations[i],
          ArrayUtil.IsNullOrEmpty(futures) ? "" : futures[i],
          ArrayUtil.IsNullOrEmpty(futuresExchanges) ? "" : futuresExchanges[i]
        ).ToArray();

        for (var j = 0; j < list.Count(); j++)
        {
          var matches = tenors.Where(o => o.Name == list[j].Name)?.ToArray();
          if (matches.Length == 0)
            tenors.Add(list[j]);
          else if (matches[0].MarketQuote.Value != list[j].MarketQuote.Value)
            throw new ToolkitException($"Duplicate tenor {list[j].Name} with different quote value not added!");
        }
      }

      return tenors;
    }

    private static IReadOnlyList<T> ValidateOptionalList<T>(
      IReadOnlyList<T> list, int count, Func<string> getErrorMsg = null,
      T defaultValue = default(T))
    {
      return list == null || list.Count == 0
        ? defaultValue.Repeat(count)
        : ValidateList(list, count, getErrorMsg);
    }

    private static IReadOnlyList<T> ValidateList<T>(
      IReadOnlyList<T> list, int count,
      Func<string> getErrorMsg = null)
    {
      if (list != null)
      {
        var length = list.Count;
        if (length == count) return list;
        if (length == 1) return list[0].Repeat(count);
      }
      throw new ArgumentException(
        getErrorMsg == null
          ? "list does not match the length"
          : getErrorMsg());
    }
    
    #endregion
  }
}
