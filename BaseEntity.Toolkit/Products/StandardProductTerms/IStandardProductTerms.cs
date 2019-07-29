//
// IStandardProductTerms.cs
//   2015. All rights reserved.
//

using System;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Interface to create market-standard product terms
  /// </summary>
  /// <remarks>
  ///   <para><see cref="IStandardProductTerms"/> provides an interface for pre-defining common market-standard
  ///   products and allowing their creation using a simple name, date, and tenor.</para>
  ///   <para>A good conceptual framework for understanding this is to mirror market quotes. The
  ///   financial product matching a particular market quote is well defined by market convention and the
  ///   information identifying that market quote is enough information to define the related product.</para>
  ///   <para>Some simple examples include defining all exchange traded products by their exchange product
  ///   code which includes an exchange product (eg ED) and a maturity code (eg M7).</para>
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates creating a STIR Future based on standard terms.</para>
  /// <code language="C#">
  ///   var asOf = Dt.Today();
  ///    var terms = StandardProductTermsCache.All.OfType&lt;StirFutureTerms&gt;().FirstOrDefault(t => t.ContractCode == contractCode);
  ///    if( terms == null )
  ///      throw new ArgumentException(String.Format("No standard contract found for contract code {0}", contractCode));
  ///    return terms.GetProduct(asOf, expiration, null);
  /// </code>
  /// </example>
  public interface IStandardProductTerms : IStandardTerms
  {
    /// <summary>
    /// Provides an interface to return the name of a quote
    /// </summary>
    /// <returns>string</returns>
    string GetQuoteName(string tenor);
  }

  /// <summary>
  ///   Indicate that a method can be used to build by supplying
  ///   an as-of date, a tenor name and a market quote, all optional.
  /// </summary>
  /// <remarks>
  ///  <para>The method must return an instance of <c>IProduct</c>, while
  ///  the parameters consist of some or all of the following set:
  ///  an date of the type <c>Dt</c> for the as-of date,
  ///  a string indicating the tenor specification,
  ///  and a market quote of either the type <c>double</c> or <c>IMariketQuote</c>.</para>
  /// </remarks>
  [AttributeUsage(AttributeTargets.Method)]
  public class ProductBuilderAttribute : Attribute
  {
  }
}
