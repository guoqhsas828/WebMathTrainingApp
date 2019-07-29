//
// NamespaceDoc.cs
// Namespace documentation for StandardProductTerms namespace
//   2015. All rights reserved.
//
namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  ///<preliminary/>
  /// <summary>
  ///   <para><see cref="IStandardProductTerms">Standard Product Terms</see> provides a framework for pre-defining common market-standard
  ///   products and allowing their creation using a simple name, date, and tenor.</para>
  ///   <para>A good conceptual framework for understanding this is to mirror market quotes. The
  ///   financial product matching a particular market quote is well defined by market convention and the
  ///   information identifying that market quote is enough information to define the related product.</para>
  ///   <para>Some simple examples include defining all exchange traded products by their exchange product
  ///   code which includes an exchange product (eg ED) and a maturity code (eg M7).</para>
  /// </summary>
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
  /// <seealso cref="IStandardProductTerms"/>
  /// <seealso cref="StandardProductTermsCache"/>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}
