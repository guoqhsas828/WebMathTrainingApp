//
//   2015. All rights reserved.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Products.StandardProductTerms;

using StdTerms = BaseEntity.Toolkit.Products.StandardProductTerms.StandardProductTermsCache;

namespace BaseEntity.Toolkit.Curves.TenorQuoteHandlers
{
  /// <summary>
  ///   Represent a tenor quote
  /// </summary>
  [Serializable]
  public struct TenorQuote : IStructuralEquatable
  {
    #region Data

    /// <summary>
    ///   Tenor name
    /// </summary>
    public readonly string Tenor;

    /// <summary>
    ///   Quoted value
    /// </summary>
    public readonly MarketQuote Quote;

    /// <summary>
    ///   Product terms
    /// </summary>
    public readonly IStandardProductTerms Terms;

    /// <summary>
    ///   Instrument type
    /// </summary>
    public readonly ReferenceIndex ReferenceIndex;

    /// <summary>
    ///  constructor
    /// </summary>
    /// <param name="tenor"></param>
    /// <param name="quote"></param>
    /// <param name="terms"></param>
    /// <param name="referenceIndex"></param>
    internal TenorQuote(string tenor, MarketQuote quote,
      IStandardProductTerms terms,
      ReferenceIndex referenceIndex)
    {
      Tenor = tenor;
      Quote = quote;
      Terms = terms;
      ReferenceIndex = referenceIndex;
    }

    internal string GetName()
    {
      return MakeName(Terms, Tenor);
    }

    private static string MakeName(IStandardProductTerms terms, string tenor)
    {
      if (terms == null) return tenor;
      StringBuilder sb = new StringBuilder();
      sb.Append(terms.GetQuoteName(tenor));
      return sb.Append('_').Append(tenor).ToString();
    }

    #endregion

    #region IStructuralEquatable Members

    bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
    {
      if (!(other is TenorQuote)) return false;
      var o = (TenorQuote) other;
      return comparer.Equals(Tenor, o.Tenor) && comparer.Equals(Quote, o.Quote)
        && comparer.Equals(Terms, o.Terms);
    }

    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
    {
      var h1 = comparer.GetHashCode(Tenor);
      var h2 = comparer.GetHashCode(Quote);
      h1 = (h1 << 5) + h1 ^ h2;
      h2 = comparer.GetHashCode(Terms);
      return (h1 << 5) + h1 ^ h2;
    }

    #endregion
  }

}
