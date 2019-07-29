/*
 *  -2014. All rights reserved.
 */
using System;
using System.Linq;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Helper to look-up predefined credit default indices.
  /// </summary>
  public static class CreditIndexDefinitions
  {
    /// <summary>
    /// Looks up predefined credit index by name.
    /// </summary>
    /// <param name="indexName">Name of the index.</param>
    /// <returns>CreditIndexDefinition.</returns>
    public static CreditIndexDefinition LookUpCreditIndexDefinition(
      this string indexName)
    {
      const StringComparison nocase = StringComparison.OrdinalIgnoreCase;
      var sts = ToolkitCache.StandardProductTermsCache.Values.OfType<CreditIndexTerms>()
        .Where(t => indexName.StartsWith(t.SubSeriesName, nocase)).ToList();
      if (sts.Count == 0)
      {
        throw new ArgumentException(String.Format(
          "Unable to find credit index {0}", indexName));
      }
      var terms = (sts.Count == 1)
        ? sts[0] : sts.Aggregate((t1, t2) =>
          t1.SubSeriesName.Length > t2.SubSeriesName.Length ? t1 : t2);
      var tenorName = indexName.Substring(terms.SubSeriesName.Length);
      return new CreditIndexDefinition(terms, tenorName, 1.0,
        terms.QuoteInPrice ? QuotingConvention.FlatPrice
          : QuotingConvention.CreditSpread);
    }
  }

  /// <summary>
  ///  The definition of a credit default index.
  /// </summary>
  /// <remarks>>This is a read-only class used for XML serialization.</remarks>
  [Serializable]
  public class CreditIndexDefinition
  {
    /// <summary>
    /// The currency
    /// </summary>
    public Currency Currency { get { return CDX.Ccy; }}

    /// <summary>
    /// The calendar
    /// </summary>
    public Calendar Calendar { get { return CDX.Calendar; }}

    /// <summary>
    /// The recovery rate
    /// </summary>
    public readonly double RecoveryRate;

    /// <summary>
    /// The entity count
    /// </summary>
    public readonly int EntityCount;

    /// <summary>
    /// The factor
    /// </summary>
    public readonly double Factor;

    /// <summary>
    /// The quoting convention
    /// </summary>
    public readonly QuotingConvention QuotingConvention;

    /// <summary>
    /// Gets the CDX.
    /// </summary>
    /// <value>The CDX.</value>
    public readonly CDX CDX;

    internal CreditIndexDefinition(
      CreditIndexTerms terms,
      string tenorName,
      double factor,
      QuotingConvention qc)
    {
      CDX = terms.GetProduct(tenorName);
      RecoveryRate = terms.Recovery;
      EntityCount = terms.Entities;
      Factor = factor;
      QuotingConvention = qc;
    }
  }
}
