//
// StandardProductTermsUtil.cs
//   2015. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  ///<preliminary/>
  /// <summary>
  ///   Utility methods for <see cref="IStandardProductTerms">Standard Product Terms</see>
  /// </summary>
  /// <seealso cref="IStandardProductTerms"/>
  public static class StandardProductTermsUtil
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(StandardProductTermsUtil));

    #region Terms Find Methods

    /// <summary>
    ///   Find market-standard CDS terms
    /// </summary>
    /// <remarks>
    ///   <para>CDS are identified by the transaction type.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var key = CdsTerms.GetKey(transactionType);
    ///     return StandardProductTermsCache.GetValue&lt;CdsTerms&gt;(key);
    ///   </code>
    /// </remarks>
    /// <param name="transactionType">Credit derivative transaction name or abreviation</param>
    /// <returns>Found <see cref="CdsTerms"/></returns>
    public static CdsTerms GetCdsTerms(CreditDerivativeTransactionType transactionType)
    {
      var key = CdsTerms.GetKey(transactionType);
      return ToolkitCache.StandardProductTermsCache.GetValue<CdsTerms>(key);
    }

    /// <summary>
    ///   Find market-standard CreditIndex terms
    /// </summary>
    /// <remarks>
    ///   <para>Credit Indices are identified by their name.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var key = CdsTerms.GetKey(transactionType);
    ///     return StandardProductTermsCache.GetValue&lt;CdsTerms&gt;(key);
    ///   </code>
    /// </remarks>
    /// <param name="name">Credit index name</param>
    /// <returns>Found <see cref="CreditIndexTerms"/></returns>
    public static CreditIndexTerms GetCreditIndexTerms(string name)
    {
      var key = CreditIndexTerms.GetKey(name);
      return ToolkitCache.StandardProductTermsCache.GetValue<CreditIndexTerms>(key);
    }

    /// <summary>
    ///   Find market-standard Future terms
    /// </summary>
    /// <remarks>
    ///   <para>Futures are uniquely identified by their contact code.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var terms = StandardProductTermsCache.All.OfType&lt;StandardFutureTermsBase&lt;FutureBase&gt;&gt;().SingleOrDefault(t => t.TransactionType == transactionType);
    ///     if( terms == null )
    ///       throw new ArgumentException(String.Format("No standard contract found for type {0}", transactionType));
    ///     return terms;
    ///   </code>
    /// </remarks>
    /// <param name="exchange">Exchange</param>
    /// <param name="contractCode">Exchange contract code (eg ED)</param>
    /// <returns>Found <see cref="FutureBase"/></returns>
    public static IStandardFutureTerms GetFutureTerms<T>(string contractCode, string exchange) where T : FutureBase
    {
      var key = StandardFutureTermsBase<FutureBase>.GetKey(exchange, contractCode);

      if (!string.IsNullOrEmpty(exchange))
      {
        return ToolkitCache.StandardProductTermsCache.GetValue<IStandardFutureTerms>(key);
      }
      else
      {
        var products = GetFutureTerms<T>(contractCode);
        if (products.Count() == 1)
          return (IStandardFutureTerms)products[0];
        if (products.Count() > 1)
          throw new ToolkitException("Ambigious futures contract code {0}. Try including the exchange", key);
      }
      throw new ToolkitException("Cannot find standard product with key {0}", key);
    }

    /// <summary>
    ///  Get all futures with contract code
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="contractCode"></param>
    /// <returns></returns>
    public static IStandardFutureTerms[] GetFutureTerms<T>(string contractCode)
    {
      return ToolkitCache.StandardProductTermsCache.Values
        .OfType<IStandardFutureTerms>()
        .Where(o => o != null && (o.GetContractCode() == contractCode || o.Key == contractCode))
        .ToArray();
    }

    /// <summary>
    ///   Find market-standard swap terms
    /// </summary>
    /// <remarks>
    ///   <para>Swaps are identified by their floating rate index and traded location.</para>
    ///   <para>This is a convenience function equivalent to:</para>
    ///   <code>
    ///     var key = SwapTerms.GetKey(floatingIndex.Name, location);
    ///     return StandardProductTermsCache.GetValue&lt;SwapTerms&gt;(key);
    ///   </code>
    /// </remarks>
    /// <param name="location">Currency (Country) of trading</param>
    /// <param name="ccy">Receive leg currency</param> 
    /// <param name="floatingIndex">Floating rate index</param>
    /// <param name="indexTenor">Floating rate index tenor</param>
    /// <returns>Found <see cref="SwapTerms"/></returns>
    public static SwapTerms GetFixedFloatSwapTerms(
      Currency location,
      IReferenceRate floatingIndex,
      Currency ccy,
      Tenor indexTenor
      )
    {
      return GetSwapTerms(location, floatingIndex, ccy, indexTenor, null, Currency.None, Tenor.Empty);
    }


    /// <summary>
    ///   Find market-standard swap terms
    /// </summary>
    /// <remarks>
    ///   <para>Swaps are identified by their floating rate index and traded location.</para>
    ///   <para>This is a convenience function equivalent to:</para>
    ///   <code>
    ///     var key = SwapTerms.GetKey(floatingIndex.Name, location);
    ///     return StandardProductTermsCache.GetValue&lt;SwapTerms&gt;(key);
    ///   </code>
    /// </remarks>
    /// <param name="location">Currency (Country) of trading</param>
    /// <param name="leg1Ccy">Receive leg currency</param> 
    /// <param name="leg1FloatingIndex">Floating rate index</param>
    /// <param name="leg1IndexTenor">Floating rate index tenor</param>
    /// <param name="leg2Ccy">Receive leg currency</param> 
    /// <param name="leg2FloatingIndex">Floating rate index</param>
    /// <param name="leg2IndexTenor">Floating rate index tenor</param>
    /// <returns>Found <see cref="SwapTerms"/></returns>
    public static SwapTerms GetSwapTerms(
      Currency location, 
      IReferenceRate leg1FloatingIndex,
      Currency leg1Ccy,
      Tenor leg1IndexTenor,
      IReferenceRate leg2FloatingIndex,
      Currency leg2Ccy,
      Tenor leg2IndexTenor
      )
    {
      var key1 = SwapTerms.GetKey(location, 
        leg1FloatingIndex != null ? leg1FloatingIndex.Key : "", leg1Ccy, leg1IndexTenor,
        leg2FloatingIndex != null ? leg2FloatingIndex.Key : "", leg2Ccy, leg2IndexTenor);

      if (leg1FloatingIndex == null || leg2FloatingIndex == null)
      {
        var floatingIndex = leg1FloatingIndex ?? leg2FloatingIndex;
        return FindStandardTerms<SwapTerms>(key1, null,
          t => (t.Location == location || (location == Currency.None && t.Location == floatingIndex.Currency))
          && (
          (t.Leg2InterestReferenceRate == null && leg1FloatingIndex == t.Leg1InterestReferenceRate && (leg1IndexTenor.IsEmpty || t.Leg1FloatIndexTenor == leg1IndexTenor))
          || (t.Leg2InterestReferenceRate == null && leg2FloatingIndex == t.Leg1InterestReferenceRate && (leg2IndexTenor.IsEmpty || t.Leg1FloatIndexTenor == leg2IndexTenor))
          || (t.Leg1InterestReferenceRate == null && leg1FloatingIndex == t.Leg2InterestReferenceRate && (leg1IndexTenor.IsEmpty || t.Leg2FloatIndexTenor == leg1IndexTenor))
          || (t.Leg1InterestReferenceRate == null && leg2FloatingIndex == t.Leg2InterestReferenceRate && (leg2IndexTenor.IsEmpty || t.Leg2FloatIndexTenor == leg2IndexTenor))),
          () => string.Format("Unable to find SwapTerms with key {0}", key1),
          () => string.Format("Two or more SwapTerms match key {0}", key1));
      }

      var key2 = (leg1FloatingIndex != null && leg2FloatingIndex != null)
        ? SwapTerms.GetKey(location,
        leg2FloatingIndex != null ? leg2FloatingIndex.Key : "", leg2Ccy, leg2IndexTenor,
        leg1FloatingIndex != null ? leg1FloatingIndex.Key : "", leg1Ccy, leg1IndexTenor)
        : null;

      return FindStandardTerms<SwapTerms>(key1, key2,
        t => Match(t, location, leg1FloatingIndex, leg1Ccy, leg1IndexTenor,
        leg2FloatingIndex, leg2Ccy, leg2IndexTenor),
        () => string.Format(
          "Unable to find BasisSwapTerms with floating indices {0}_{1} and {2}_{3}",
          leg1FloatingIndex.Key, leg1IndexTenor.ToString("S", null),
          leg2FloatingIndex.Key, leg2IndexTenor.ToString("S", null)),
        () => string.Format(
          "Two or more BasisSwapTerms match floating indices {0}_{1} and {2}_{3}",
          leg1FloatingIndex.Key, leg1IndexTenor.ToString("S", null),
          leg2FloatingIndex.Key, leg2IndexTenor.ToString("S", null)));
    }

    /// <summary>
    ///   Find market-standard basis swap terms
    /// </summary>
    /// <remarks>
    ///   <para>Basis Swaps are identified by their two floating rate indices.</para>
    ///   <para>This is a convenience function equivalent to:</para>
    ///   <code>
    ///     var key = BasisSwapTerms.GetKey(floatingIndex1.Name, floatingIndex2.Name);
    ///     return StandardProductTermsCache.GetValue&lt;BasisSwapTerms&gt;(key);
    ///   </code>
    /// </remarks>
    /// <param name="location">Trading location</param>
    /// <param name="floatingIndex1">Floating leg 1 reference index</param>
    /// <param name="indexTenor1">Floating leg 1 reference index tenor</param>
    /// <param name="floatingIndex2">Floating leg 2 reference index</param>
    /// <param name="indexTenor2">Floating leg 2 reference index tenor</param>
    /// <returns>Found <see cref="SwapTerms"/></returns>
    public static SwapTerms GetBasisSwapTerms(
      Currency location,
      IReferenceRate floatingIndex1, Tenor indexTenor1,
      IReferenceRate floatingIndex2, Tenor indexTenor2)
    {
      if (floatingIndex1 == null)
        throw new NullReferenceException("floating rate 1 cannot be null");
      if (floatingIndex2 == null)
        throw new NullReferenceException("floating rate 2 cannot be null");

      var key1 = SwapTerms.GetKey(location,
        floatingIndex1.Key, floatingIndex1.Currency, indexTenor1,
        floatingIndex2.Key, floatingIndex2.Currency, indexTenor2);
      var key2 = SwapTerms.GetKey(location,
         floatingIndex2.Key, floatingIndex2.Currency, indexTenor2,
         floatingIndex1.Key, floatingIndex1.Currency, indexTenor1);

      return FindStandardTerms<SwapTerms>(key1, key2,
        t => Match(t, location, floatingIndex1, floatingIndex1.Currency, indexTenor1, 
        floatingIndex2, floatingIndex2.Currency, indexTenor2),
        () => string.Format(
          "Unable to find BasisSwapTerms with floating indices {0}_{1} and {2}_{3}",
          floatingIndex1.Key, indexTenor1.ToString("S", null),
          floatingIndex2.Key, indexTenor2.ToString("S", null)),
        () => string.Format(
          "Two or more BasisSwapTerms match floating indices {0}_{1} and {2}_{3}",
          floatingIndex1.Key, indexTenor1.ToString("S", null),
          floatingIndex2.Key, indexTenor2.ToString("S", null)));
    }

    private static bool Match(SwapTerms t,
      Currency location,
      IReferenceRate floatingIndex1, Currency leg1Ccy, Tenor leg1IndexTenor,
      IReferenceRate floatingIndex2, Currency leg2Ccy, Tenor leg2IndexTenor)
    {
      return
        ((t.Location == location || (location == Currency.None && t.Location == leg1Ccy))
        && ((floatingIndex1 == t.Leg1InterestReferenceRate) && (leg1IndexTenor.IsEmpty || t.Leg1FloatIndexTenor == leg1IndexTenor))
        && ((floatingIndex2 == t.Leg2InterestReferenceRate) && (leg2IndexTenor.IsEmpty || t.Leg2FloatIndexTenor == leg2IndexTenor))) 
        || ((t.Location == location || (location == Currency.None && t.Location == leg1Ccy))
        && ((floatingIndex2 == t.Leg1InterestReferenceRate) && (leg2IndexTenor.IsEmpty || t.Leg1FloatIndexTenor == leg1IndexTenor))
        && ((floatingIndex1 == t.Leg2InterestReferenceRate) && (leg1IndexTenor.IsEmpty || t.Leg2FloatIndexTenor == leg2IndexTenor)));
    }

    private static T FindStandardTerms<T>(
      string key1, string key2,
      Func<T, bool> isMatched,
      Func<string> errorNotFound,
      Func<string> errorAmbiguous)
      where T : class, IStandardProductTerms
    {
      // First try direct search by keys, very fast!
      T st;
      if (ToolkitCache.StandardProductTermsCache.TryGetValue(key1, out st) || (
        key2 != null && ToolkitCache.StandardProductTermsCache.TryGetValue(key2, out st)))
      {
        return st;
      }

      // If fail, try refined search, maybe slow.
      var candidates = ToolkitCache.StandardProductTermsCache.Values.OfType<T>()
        .Where(isMatched).ToList();
      if (candidates.Count == 1) return candidates[0];
      if (candidates.Count == 0)
      {
        throw new ArgumentException(errorNotFound());
      }
      throw new ArgumentException(errorAmbiguous());
    }

    /// <summary>
    ///   Find market-standard inflation zero coupon swap terms
    /// </summary>
    /// <remarks>
    ///   <para>Swaps are identified by their floating inflation rate index and traded location.</para>
    ///   <para>This is a convenience function equivalent to:</para>
    ///   <code>
    ///     var key = SwapTerms.GetKey(inflationIndex.Name, location);
    ///     return StandardProductTermsCache.GetValue&lt;SwapTerms&gt;(key);
    ///   </code>
    /// </remarks>
    /// <param name="inflationIndex">Inflation rate index</param>
    /// <param name="location">Currency (Country) of trading</param>
    /// <param name="payFreq">Payment frequency</param>
    /// <returns>Found <see cref="SwapTerms"/></returns>
    public static InflationSwapTerms GetInflationSwapTerms(IReferenceRate inflationIndex,
      Frequency payFreq, Currency location)
    {
      if (inflationIndex == null)
        throw new NullReferenceException("inflation rate cannot be null");

      if (location == Currency.None)
        location = inflationIndex.Currency;

      var key = InflationSwapTerms.GetKey(inflationIndex.Key, location, payFreq);
      return FindStandardTerms<InflationSwapTerms>(key, null,
        t => t.Leg2IndexName == inflationIndex.Key 
        && t.Leg1PaymentFreq == payFreq 
        && t.Leg2PaymentFreq == payFreq,
        () => $"Unable to find SwapTerms with floating index {inflationIndex.Key}",
        () => $"Two or more SwapTerms match floating index {inflationIndex.Key}");
    }
    #endregion

    #region Factory Methods

    /// <summary>
    ///   Create a standard CDS
    /// </summary>
    /// <remarks>
    ///   <para>Create a CDS based on pre-defined market-standard <see cref="CdsTerms"/>.</para>
    ///   <para>CDS are identified by the transaction type.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var terms = StandardProductTermsUtil.GetCdsTerms(transactionType);
    ///     return terms.GetProduct(asOf, tenorName, currency, premium, fee, cleared);
    ///   </code>
    ///   <inheritdoc cref="StandardProductTerms.CdsTerms.GetProduct(Dt, string, BaseEntity.Toolkit.Base.Currency, double, double, bool)"/>
    /// </remarks>
    /// <seealso cref="GetCdsTerms"/>
    /// <seealso cref="StandardProductTerms.CdsTerms.GetProduct(Dt, string, BaseEntity.Toolkit.Base.Currency, double, double, bool)"/>
    /// <param name="transactionType">Credit Derivative Transaction Type</param>
    /// <param name="asOf">Quoted as-of date</param>
    /// <param name="tenorName">Tenor name (eg 5Yr)</param>
    /// <param name="currency">Currency of premium. If None then use first (primary) currency for this transaction type</param>
    /// <param name="premium">Premium</param>
    /// <param name="fee">Upfront fee</param>
    /// <param name="cleared">True if cleared</param>
    /// <returns>Created <see cref="CDS"/></returns>
    public static CDS GetStandardCds(CreditDerivativeTransactionType transactionType, Dt asOf, string tenorName, Currency currency, double premium, double fee, bool cleared)
    {
      var terms = GetCdsTerms(transactionType);
      return terms.GetProduct(asOf, tenorName, currency, premium, fee, cleared);
    }

    /// <summary>
    ///   Create a standard Credit Index
    /// </summary>
    /// <remarks>
    ///   <para>Create a Credit Index based on pre-defined market-standard <see cref="CreditIndexTerms"/>.</para>
    ///   <para>CDS are identified by a name.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var terms = StandardProductTermsUtil.GetCreditIndexTerms(name);
    ///     return terms.GetProduct(asOf, tenorName);
    ///   </code>
    ///   <inheritdoc cref="StandardProductTerms.CreditIndexTerms.GetProduct(string)"/>
    /// </remarks>
    /// <seealso cref="GetCreditIndexTerms"/>
    /// <seealso cref="StandardProductTerms.CreditIndexTerms.GetProduct(string)"/>
    /// <param name="name">Credit Index name</param>
    /// <param name="asOf">Quoted as-of date</param>
    /// <param name="tenorName">Tenor name (eg 5Yr)</param>
    /// <returns>Created <see cref="CDX"/></returns>
    public static CDX GetStandardCreditIndex(string name, Dt asOf, string tenorName)
    {
      var terms = GetCreditIndexTerms(name);
      return terms.GetProduct(tenorName);
    }

    /// <summary>
    ///   Create a standard Credit Index
    /// </summary>
    /// <remarks>
    ///   <para>Create a Credit Index based on pre-defined market-standard <see cref="CreditIndexTerms"/>.</para>
    ///   <para>CDS are identified by a name.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var terms = StandardProductTermsUtil.GetCreditIndexTerms(name);
    ///     return terms.GetProduct(asOf, tenorName);
    ///   </code>
    ///   <inheritdoc cref="StandardProductTerms.CreditIndexTerms.GetProduct(string,int,int)"/>
    /// </remarks>
    /// <seealso cref="GetCreditIndexTerms"/>
    /// <seealso cref="StandardProductTerms.CreditIndexTerms.GetProduct(string,int,int)"/>
    /// <param name="indexName">Credit Index name</param>
    /// <param name="seriesNumber">Credit Index issue number</param>
    /// <param name="years">Tenor in integer years</param>
    /// <returns>Created <see cref="CDX"/></returns>
    public static CDX GetStandardCreditIndex(string indexName, int seriesNumber, int years)
    {
      var terms = GetCreditIndexTerms(indexName);
      return terms.GetProduct(indexName, seriesNumber, years);
    }

    /// <summary>
    ///   Create a standard Future
    /// </summary>
    /// <remarks>
    ///   <para>Create a Future based on pre-defined market-standard <see cref="StandardFutureTermsBase{Futurebase}"/>.</para>
    ///   <para>Futures are uniquely identified by their contact code.</para>
    ///   <para>Convenience function. Equivalent to:</para>
    ///   <code>
    ///     var terms = StandardProductTermsUtil.GetFutureTerms(contractCode);
    ///     return terms.GetProduct(asOf, expirationCode);
    ///   </code>
    ///   <inheritdoc cref="StandardProductTerms.StandardFutureTermsBase{FutureBase}.GetProduct(Dt,string)"/>
    /// </remarks>
    /// <seealso cref="GetFutureTerms{T}(string, string)"/>
    /// <seealso cref="StandardProductTerms.StandardFutureTermsBase{FutureBase}.GetProduct(Dt,string)"/>
    /// <param name="exchange">Exchange name</param>
    /// <param name="contractCode">Exchange contract code (eg ED)</param>
    /// <param name="asOf">Quoted as-of date (optional. Only required if single digit expiration year is used)</param>
    /// <param name="expirationCode">Contract expiration code (eg Z16 or DEC16)</param>
    /// <returns>Created Future</returns>
    public static FutureBase GetStandardFuture<T>(string contractCode, string exchange, Dt asOf, string expirationCode) where T : FutureBase
    {
      var terms = GetFutureTerms<T>(contractCode, exchange);
      return terms.GetProduct(asOf, expirationCode);
    }

    /// <summary>
    ///   Create standard swap given a date, a maturity tenor, floating rate indices and fixed rates or floating margins
    /// </summary>
    /// <remarks>
    ///   <para>Create a swap based on pre-defined market-standard <see cref="SwapTerms"/>.</para>
    ///   <para>A number of tenor formats as supported.</para>
    ///   <list type="bullet">
    ///     <item><description>A specific date</description></item>
    ///     <item><description>O/N or T/N which is the next business day</description></item>
    ///     <item><description>A tenor in days which is the specified number of business days (eg 3 Days)</description></item>
    ///     <item><description>Any other tenor which is the number of calendar days (eg 1 Month)</description></item>
    ///   </list>
    ///   <para>This is a convenience function equivalent to:</para>
    ///   <code>
    ///     var terms = StandardProductTermsUtil.GetSwapTerms(floatingIndex, location);
    ///     return terms.GetProduct(asOf, tenorName, fixedRate);
    ///   </code>
    ///   <inheritdoc cref="StandardProductTerms.SwapTerms.GetProduct(Dt,string,double)"/>
    /// </remarks>
    /// <seealso cref="GetFutureTerms{T}(string, string)"/>
    /// <seealso cref="StandardProductTerms.SwapTerms.GetProduct(Dt,string,double)"/>
    /// <param name="asOf">Pricing as-of date (optional)</param>
    /// <param name="swapTenor">Swap tenor (can be a O/N, T/N, business days, tenor, or specified date)</param>
    /// <param name="recCcy">Receive currency</param>
    /// <param name="recRate">Receive floating rate index</param>
    /// <param name="recIndexTenor">Floating rate index tenor</param>
    /// <param name="payCcy">Pay currency</param>
    /// <param name="payRate">Receive floating rate index</param>
    /// <param name="payIndexTenor">Floating rate index tenor</param>
    /// <param name="location">Currency of traded location (optional)</param>
    /// <param name="recRateOrMargin">Receive rate coupon or margin</param>
    /// <param name="payRateOrMargin">Pay rate coupon or margin</param>
    /// <returns>Standard <see cref="Swap"/></returns>
    public static Swap GetStandardSwap(Dt asOf, string swapTenor, 
      Currency location,
      Currency recCcy, InterestReferenceRate recRate, Tenor recIndexTenor, double recRateOrMargin,
      Currency payCcy, InterestReferenceRate payRate, Tenor payIndexTenor, double payRateOrMargin)
    {
      var terms = GetSwapTerms(location, recRate, recCcy, recIndexTenor, payRate, payCcy, payIndexTenor);
      return terms.GetSwap(asOf, swapTenor, recRate, payRate, recRateOrMargin, payRateOrMargin);
    }

    /// <summary>
    /// Get Standard inflation zero swap
    /// </summary>
    /// <param name="asOf">Pricing as-of date (optional)</param>
    /// <param name="swapTenor">Swap tenor (can be a O/N, T/N, business days, tenor, or specified date)</param>
    /// <param name="location">Currency of traded location (optional)</param>
    /// <param name="freq">Payment frequency</param>
    /// <param name="refRate">Floating leg reference rate</param>
    /// <param name="fixedRate">Fixed swap leg coupon</param>
    /// <returns></returns>
    public static Swap GetStandardInflationSwap(Dt asOf, string swapTenor, Frequency freq, Currency location, 
      InflationReferenceRate refRate, double fixedRate)
    {
      var terms = GetInflationSwapTerms(refRate, freq, location);
      return terms.GetProduct(asOf, swapTenor, fixedRate);
    }

    #endregion

    #region Other Factory Methods // Simple products that don't require any terms

    /// <summary>
    ///   Create a CD based on a rate index
    /// </summary>
    /// <remarks>
    ///   <para>Create a CD based on pre-defined market-standard terms.</para>
    ///   <para>The terms of the CD are calculated as follows:</para>
    ///   <list type="table">
    ///     <listheader><term>CD Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="Product.Effective"/></term>
    ///       <description>Calculated by adding the Reference Index <see cref="InterestReferenceRate.DaysToSpot"/> in business days to
    ///       the quoted <paramref name="asOf"/> date using the Reference Index <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Maturity"/></term>
    ///       <description>Calculated by adding the specified <paramref name="cdTenor"/> to the <see cref="Product.Effective"/> rolled
    ///       using the Reference Index <see cref="InterestReferenceRate.BDConvention"/> and <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="CD.Coupon"/></term>
    ///       <description>Set to the specified <paramref name="coupon"/>.</description>
    ///     </item><item>
    ///       <term><see cref="CD.DayCount"/></term>
    ///       <description>Set to the Reference Index <see cref="InterestReferenceRate.DayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to Reference Index <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <param name="index">Interest rate index</param>
    /// <param name="asOf">Quoted as-of date</param>
    /// <param name="cdTenor">CD maturity tenor</param>
    /// <param name="coupon">Coupon of CD</param>
    /// <returns>Constructed CD</returns>
    public static CD GetStandardCd(InterestReferenceRate index, Dt asOf, string cdTenor, double coupon)
    {
      Tenor tenor;
      if (!Tenor.TryParse(cdTenor, out tenor))
        throw new ArgumentException(String.Format("Invalid CD maturity tenor {0}", cdTenor));
      var daysToSettle = index.DaysToSpot;
      var calendar = index.Calendar;
      var roll = index.BDConvention;
      var dayCount = index.DayCount;
      var currency = index.Currency;
      var effective = Dt.AddDays(asOf, daysToSettle, calendar);
      var maturity = Dt.Roll(Dt.Add(effective, tenor), roll, calendar);
      return new CD(effective, maturity, currency, coupon, dayCount);
    }

    /// <summary>
    ///   Create standard <see cref="FRA"/> given an rate index, date, a tenor, and a strike
    /// </summary>
    /// <remarks>
    ///   <para>The <paramref name="tenorName"/> specifies the effective date and the termination date in standard FRA form
    ///   A [x] B [o|ov|over C] where:</para>
    ///   <list type="table">
    ///     <item><term>A</term><description>Months from the spot date to settlement</description></item>
    ///     <item><term>B</term><description>Months from the spot date to maturity</description></item>
    ///     <item><term>C</term><description>Is the day of the month, IMM indicating IMM dates, or END indicating end of month</description></item>
    ///   </list>
    ///   <para>For example:</para>
    ///   <list type="table">
    ///     <item><term>1 x 4</term><description>Deposit settles one month from spot and maturies 3 (4-1) months after settlement</description></item>
    ///     <item><term>1 x 4 o IMM</term><description>Deposit settles on the next IMM date after one month from spot and maturies on the next IMM
    ///     date 3 (4-1) months after settlement</description></item>
    ///     <item><term>2 x 5</term><description>Deposit settles two months from spot and maturies 3 (5-2) months after settlement</description></item>
    ///     <item><term>1 x 7 ov 10</term><description>Deposit settles on the 10th of next month and maturies on the 10th 6 (7-1) months after settlement</description></item>
    ///   </list>
    ///   <para>The <paramref name="strike"/> specifies the FRA strike rate.</para>
    ///   <para>The terms of the FRA are implied from the <see cref="BaseEntity.Toolkit.Base.ReferenceIndices.InterestRateIndex"/> specified by the index name, currency and the specified
    ///   reference tenor from the <paramref name="tenorName"/> as follows:</para>
    ///   <para>The floating rate tenor is the difference between the months to termination and the months to effective.</para>
    ///   <list type="table">
    ///     <listheader><term>FRA Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="Product.Effective"/></term>
    ///       <description>Or FRA spot settlement date is calculated by adding the Reference Index <see cref="InterestReferenceRate.DaysToSpot"/> in business days to
    ///       the quoted <paramref name="asOf"/> date using the Reference Index <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Maturity"/></term>
    ///       <description>Or FRA effective date is the spot date plus the first tenor rolled using the roll convention and calendar of the index).</description>
    ///     </item><item>
    ///       <term><see cref="FRA.ContractMaturity"/></term>
    ///       <description>Or FRA maturity is the standard BBA date given the effective date plus the reference tenor and the roll convention and calendar of the index.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to Reference Index <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FRA.Strike"/></term>
    ///       <description>Set to the specified strike.</description>
    ///     </item><item>
    ///       <term><see cref="FRA.ContractPeriodDayCount"/></term>
    ///       <description>Set to the Reference Index <see cref="InterestReferenceRate.DayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.BDConvention"/></term>
    ///       <description>Set to the Reference Index <see cref="InterestReferenceRate.BDConvention"/> days.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Calendar"/></term>
    ///       <description>Set to the Reference Index <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the None.</description>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <param name="rateIndex">Reference index</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="tenorName">FRA tenor expiration x termination in months (eg 1*4 ov 10th)</param>
    /// <param name="strike">Strike rate</param>
    /// <returns>Standard <see cref="FRA"/></returns>
    public static FRA GetStandardFra(IReferenceRate rateIndex, Dt asOf, string tenorName, double strike)
    {
      if (rateIndex is InterestReferenceRate)
      {
        var interestRateIndex = rateIndex as InterestReferenceRate;
        if (strike < -1.0 || strike > 2.0)
        {
          throw new ToolkitException(
            "Invalid FRA strike {0}. Must be between -1 and 2", strike);
        }
        // Interpret FRA tenor
        Dt spot, settle, maturity;
        Tenor tenor;
        if (!TryParseFraTenor(tenorName, asOf, interestRateIndex.DaysToSpot, interestRateIndex.Calendar, interestRateIndex.BDConvention, out spot, out settle, out maturity, out tenor))
          throw new ArgumentException("Invalid FRA tenor. Expected A * B [over C]");
        var referenceIndex = new BaseEntity.Toolkit.Base.ReferenceIndices.InterestRateIndex(interestRateIndex, tenor);
        var fra = new FRA(spot, settle, Frequency.None, strike, referenceIndex, maturity, 
          interestRateIndex.Currency, interestRateIndex.DayCount, interestRateIndex.Calendar, interestRateIndex.BDConvention);
        fra.Validate();
        return fra;
      }
      throw new Exception($"Index {rateIndex.Key} is not an interest rate index");
    }

    /// <summary>
    ///   Create standard <see cref="FRA"/> given an rate index, date, a tenor, and a strike
    /// </summary>
    /// <remarks>
    ///   <para>The <paramref name="tenorName"/> specifies the effective date and the termination date in standard FRA form
    ///   A [x] B [o|ov|over C] where:</para>
    ///   <list type="table">
    ///     <item><term>A</term><description>Months from the spot date to settlement</description></item>
    ///     <item><term>B</term><description>Months from the spot date to maturity</description></item>
    ///     <item><term>C</term><description>Is the day of the month, IMM indicating IMM dates, or END indicating end of month</description></item>
    ///   </list>
    ///   <para>For example:</para>
    ///   <list type="table">
    ///     <item><term>1 x 4</term><description>Deposit settles one month from spot and maturies 3 (4-1) months after settlement</description></item>
    ///     <item><term>1 x 4 o IMM</term><description>Deposit settles on the next IMM date after one month from spot and maturies on the next IMM
    ///     date 3 (4-1) months after settlement</description></item>
    ///     <item><term>2 x 5</term><description>Deposit settles two months from spot and maturies 3 (5-2) months after settlement</description></item>
    ///     <item><term>1 x 7 ov 10</term><description>Deposit settles on the 10th of next month and maturies on the 10th 6 (7-1) months after settlement</description></item>
    ///   </list>
    ///   <para>The <paramref name="rate"/> specifies the FRA strike rate.</para>
    ///   <para>The terms of the FRA are implied from the <see cref="BaseEntity.Toolkit.Base.ReferenceIndices.InterestRateIndex"/> specified by the index name, currency and the specified
    ///   reference tenor from the <paramref name="tenorName"/> as follows:</para>
    ///   <para>The floating rate tenor is the difference between the months to termination and the months to effective.</para>
    ///   <list type="table">
    ///     <listheader><term>FRA Property</term><description>Calculation method</description></listheader>
    ///     <item>
    ///       <term><see cref="Product.Effective"/></term>
    ///       <description>Or FRA spot settlement date is calculated by adding the Reference Index <see cref="InterestReferenceRate.DaysToSpot"/> in business days to
    ///       the quoted <paramref name="asOf"/> date using the Reference Index <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Maturity"/></term>
    ///       <description>Or FRA effective date is the spot date plus the first tenor rolled using the roll convention and calendar of the index).</description>
    ///     </item><item>
    ///       <term><see cref="FRA.ContractMaturity"/></term>
    ///       <description>Or FRA maturity is the standard BBA date given the effective date plus the reference tenor and the roll convention and calendar of the index.</description>
    ///     </item><item>
    ///       <term><see cref="Product.Ccy"/></term>
    ///       <description>Set to Reference Index <see cref="ReferenceRate.Currency"/>.</description>
    ///     </item><item>
    ///       <term><see cref="FRA.Strike"/></term>
    ///       <description>Set to the specified strike.</description>
    ///     </item><item>
    ///       <term><see cref="FRA.ContractPeriodDayCount"/></term>
    ///       <description>Set to the Reference Index <see cref="InterestReferenceRate.DayCount"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.BDConvention"/></term>
    ///       <description>Set to the Reference Index <see cref="InterestReferenceRate.BDConvention"/> days.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Calendar"/></term>
    ///       <description>Set to the Reference Index <see cref="ReferenceRate.Calendar"/>.</description>
    ///     </item><item>
    ///       <term><see cref="ProductWithSchedule.Freq"/></term>
    ///       <description>Set to the None.</description>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <param name="rateIndex">Reference index</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="tenorName">FRA tenor expiration x termination in months (eg 1*4 ov 10th)</param>
    /// <param name="rate">Strike rate</param>
    /// <returns>Standard <see cref="SwapLeg"/></returns>
    public static SwapLeg GetStandardFixedSwapLeg(IReferenceRate rateIndex, Dt asOf, string tenorName, double rate)
    {
      if (rateIndex is ISDAInterestReferenceRate)
      {
        var ISDAIndex = rateIndex as ISDAInterestReferenceRate;
        Tenor tenor;
        if (!Tenor.TryParse(tenorName, out tenor))
          throw new ArgumentException(String.Format("Invalid CD maturity tenor {0}", tenorName));
        var calendar = ISDAIndex.Calendar;
        var roll = ISDAIndex.BDConvention;
        var dayCount = ISDAIndex.SwapFixedDayCount;
        var frequency = ISDAIndex.SwapFixedFrequency; 
        var currency = ISDAIndex.Currency;
        var effective = Dt.AddDays(asOf, ISDAIndex.DaysToSpot, calendar);
        // Only use ISDAIndex.BDConvention to calculate spot date and intermediate payments;
        var maturity = Dt.Roll(Dt.Add(effective, tenor), BDConvention.None, calendar); 
        var swapLeg = new SwapLeg(effective, maturity, ISDAIndex.Currency, rate, dayCount, frequency, roll, calendar, false);
        swapLeg.Validate();
        return swapLeg;
      }
      throw new Exception(string.Format("Index {0} is not an interest rate index", rateIndex.Key));
    }

    #endregion Other

    #region Utility Methods

    /// <summary>
    ///   Utility method to decipher the FRA term A * B over C
    /// </summary>
    /// <remarks>
    ///   <para>The standard FRA tenor defines the months to settlement, the months to maturity, and
    ///   the rule for calculating the day of the month for each.</para>
    ///   <para>The valid tenor format is A [x] B [o|ov|over C] where:</para>
    ///   <list type="table">
    ///     <item><term>A</term><description>Months from the spot date to settlement</description></item>
    ///     <item><term>B</term><description>Months from the spot date to maturity</description></item>
    ///     <item><term>C</term><description>Is the day of the month, IMM indicating IMM dates, or END indicating end of month</description></item>
    ///   </list>
    ///   <para>For example:</para>
    ///   <list type="table">
    ///     <item><term>1 x 4</term><description>Deposit settles one month from spot and maturies 3 (4-1) months after settlement</description></item>
    ///     <item><term>1 x 4 o IMM</term><description>Deposit settles on the next IMM date after one month from spot and maturies on the next IMM date 3 (4-1) months after settlement</description></item>
    ///     <item><term>2 x 5</term><description>Deposit settles two months from spot and maturies 3 (5-2) months after settlement</description></item>
    ///     <item><term>1 x 7 ov 10</term><description>Deposit settles on the 10th of next month and maturies on the 10th 6 (7-1) months after settlement</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="tenorName">FRA composite term in the form of A * B over C</param>
    /// <param name="asOf">Quoted as-of date</param>
    /// <param name="daysToSettle">Business days to settle</param>
    /// <param name="calendar">Calendar for business days</param>
    /// <param name="roll">Business day convention</param>
    /// <param name="spot">Returned spot date</param>
    /// <param name="settle">Returned settlement date</param>
    /// <param name="maturity">returned maturity date</param>
    /// <param name="tenor">Returned tenor</param>
    /// <returns>True if the input is in valid composite tenor format, false otherwise</returns>
    public static bool TryParseFraTenor(string tenorName, Dt asOf, int daysToSettle, Calendar calendar, BDConvention roll, out Dt spot, out Dt settle, out Dt maturity, out Tenor tenor)
    {
      // Initialise
      spot = settle = maturity = Dt.Empty;
      tenor = Tenor.Empty;
      // Interpret FRA tenor
      var match = Regex.Match(tenorName, @"^(\d+)\s*[x\*]\s*(\d+)(?:\s*(?:o|ov|over)\s*(\d+|IMM|END)|)$", RegexOptions.IgnoreCase);
      if (!match.Success) return false;
      var settleMonths = Int32.Parse(match.Groups[1].Value);
      var maturityMonths = Int32.Parse(match.Groups[2].Value);
      if (settleMonths < 0 || settleMonths >= maturityMonths) return false;
      var dayOfMonth = CycleRule.None;
      if (match.Groups.Count > 2 && !String.IsNullOrEmpty(match.Groups[3].Value))
      {
        if (String.Compare(match.Groups[3].Value, "IMM", StringComparison.OrdinalIgnoreCase) == 0)
          dayOfMonth = CycleRule.IMM;
        else if (String.Compare(match.Groups[3].Value, "END", StringComparison.OrdinalIgnoreCase) == 0)
          dayOfMonth = CycleRule.EOM;
        else
          dayOfMonth = CycleRule.First + (Int32.Parse(match.Groups[3].Value) - 1);
      }
      // Calculate dates
      tenor = new Tenor(maturityMonths - settleMonths, TimeUnit.Months);
      spot = Dt.AddDays(asOf, daysToSettle, calendar);
      settle = Dt.Roll(Dt.AddMonths(spot, settleMonths, dayOfMonth), roll, calendar);
      maturity = Dt.Roll(Dt.AddMonths(settle, maturityMonths - settleMonths, dayOfMonth), roll, calendar);
      return true;
    }

    internal static Note GetStandardMoneyMarket(IReferenceRate index, Dt asOf, string cdTenor, double coupon)
    {
      Tenor tenor = Tenor.Empty;
      if (index is InterestReferenceRate)
      {
        var interestRateIndex = index as InterestReferenceRate;
        if (!Tenor.TryParse(cdTenor, out tenor))
          throw new ArgumentException(String.Format("Invalid money market maturity tenor {0}", cdTenor));
        var daysToSettle = tenor.CompareTo(Tenor.OneDay) <= 0 ? interestRateIndex.OvernightDaysToSpot : interestRateIndex.DaysToSpot;
        var calendar = interestRateIndex.Calendar;
        var roll = interestRateIndex.BDConvention;
        var dayCount = interestRateIndex.DayCount;
        var currency = interestRateIndex.Currency;
        var effective = Dt.AddDays(asOf, daysToSettle, calendar);
        var maturity = Dt.Roll(Dt.Add(effective, tenor), roll, calendar);
        return new Note(effective, maturity, currency, coupon, dayCount, Frequency.None, roll, calendar);
      }
      else if (index is ISDAInterestReferenceRate)
      {
        var interestRateIndex = index as ISDAInterestReferenceRate;
        if (!Tenor.TryParse(cdTenor, out tenor))
          throw new ArgumentException(String.Format("Invalid money market maturity tenor {0}", cdTenor));
        var daysToSettle = interestRateIndex.DaysToSpot;
        var calendar = interestRateIndex.Calendar;
        var roll = interestRateIndex.BDConvention;
        var dayCount = interestRateIndex.MoneyMarketDayCount;
        var currency = interestRateIndex.Currency;
        var effective = Dt.AddDays(asOf, daysToSettle, calendar);
        var maturity = Dt.Roll(Dt.Add(effective, tenor), roll, calendar);
        return new Note(effective, maturity, currency, coupon, dayCount, Frequency.None, roll, calendar);
      }
      throw new Exception(string.Format("Index {0} is not a valid rate index", index.Key));
    }

    /// <summary>
    /// FX forward helper
    /// </summary>
    public class FxProductHelper
    {
      bool Inverted = false;

      /// <summary>
      /// Standard FX product helper
      /// </summary>
      /// <param name="inverted"></param>
      public FxProductHelper(bool inverted)
      {
        Inverted = inverted;
      }

      /// <summary>
      /// Get standard FX forward
      /// </summary>
      /// <param name="index">FX rate index</param>
      /// <param name="asOf">As of date</param>
      /// <param name="maturityTenor">Maturity date</param>
      /// <param name="fxRate">FX rate</param>
      /// <returns></returns>
      public FxForward GetStandardFxForward(IReferenceRate index, Dt asOf, string maturityTenor, double fxRate)
      {
        return StandardProductTermsUtil.GetStandardFxForward(index, asOf, maturityTenor, fxRate, Inverted);
      }
    }

    /// <summary>
    /// Get standard FX forward from index
    /// </summary>
    /// <param name="index">FX rate index</param>
    /// <param name="asOf">Trade as of date</param>
    /// <param name="maturityTenor">Maturity tenor</param>
    /// <param name="fxRate">Contractual FX forward rate</param>
    /// <param name="inverted">FX currencies inverted with respect to the index</param>
    /// <returns></returns>
    public static FxForward GetStandardFxForward(IReferenceRate index, Dt asOf, string maturityTenor, double fxRate, bool inverted)
    {
      if (index is FxReferenceRate)
      {
        var fxIndex = index as FxReferenceRate;
        Tenor tenor;
        if (!Tenor.TryParse(maturityTenor, out tenor))
          throw new ArgumentException(String.Format("Invalid maturity tenor {0}", maturityTenor));
        var daysToSettle = fxIndex.DaysToSpot;
        var calendar = new Calendar(fxIndex.Calendar.ToString() + "+" + fxIndex.QuoteCalendar.ToString());
        var roll = fxIndex.RollConvention;
        var currency = inverted ? fxIndex.ForeignCurrency : fxIndex.Currency;
        var forCurrency = inverted ? fxIndex.Currency : fxIndex.ForeignCurrency;
        var effective = Dt.AddDays(asOf, daysToSettle, calendar);
        var maturity = Dt.Roll(Dt.Add(effective, tenor), roll, calendar);
        return new FxForward(maturity, forCurrency, currency, inverted ? 1.0 / fxRate : fxRate);
      }
      throw new Exception(string.Format("Index {0} is not an interest rate index", index.Key));
    }

    internal static StringBuilder AppendIndexName(
      this StringBuilder sb,
      string floatingIndexName, Tenor indexTenor)
    {
      sb.Append(floatingIndexName);
      if (indexTenor == Tenor.Empty) return sb;
      sb.Append('_').Append(indexTenor.ToString("S", null));
      return sb;
    }

    #endregion
  }
}
