namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  /// <summary>
  ///   <para><see cref="IReferenceRate">Reference Rates</see> are used to represent underlying reference indices
  ///   for derivatives. Examples include the reference index for the floating leg of a swap and the underling
  ///   reference rate for STIR futures. For swaps this is the equivalent of the definition for the
  ///   ISDA Floating Rate Option. For commodity swaps this is the equivalent of the ISDA Commodity Reference
  ///   Price.</para>
  ///   <para><see cref="IReferenceRate">Reference Rates</see> are uniquely identified by a name and are
  ///   immutable, pre-defined and cached. As such they use a design pattern which avoids direct construction and
  ///   hides an internal cache of pre-defined terms.</para>
  ///   <para>Cloning a <see cref="IReferenceRate">Reference Rate</see> returns the same object and serialising
  ///   saves just the unique name which is used to look up the pre-defined matching term on de-serialisation.</para>
  ///   <para>Common usage is to look up a <see cref="IReferenceRate">Reference Rate</see> by name. For specific
  ///   uages, access is provided to query the list of pre-defined terms and add new terms.</para>
  ///   <para>Common <see cref="IReferenceRate">Reference Rates</see> include <see cref="InterestReferenceRate"/>,
  ///   <see cref="FxReferenceRate"/> and <see cref="CommodityReferenceRate"/>.</para>
  /// </summary>
  /// <example>
  /// <para>The following example is of looking up an interest rate reference index by name.</para>
  /// <code language="C#">
  ///    // Find USD Libor index
  ///    var index = InterestReferenceRate.Get("USDLIBOR");
  /// </code>
  /// <para>The following are example is of finding all interest rate indices of a specific currency.</para>
  /// <code language="C#">
  ///    // Find all USD interest rate indices
  ///    var currency = Currency.USD;
  ///    var rateIndices = ReferenceRate.CacheValues.OfType&lt;InterestReferenceRate&gt;().Where(i => i.Currency == currency);
  /// </code>
  /// </example>
  /// <seealso cref="IReferenceRate"/>
  /// <seealso cref="InterestReferenceRate"/>
  /// <seealso cref="FxReferenceRate"/>
  /// <seealso cref="CommodityReferenceRate"/>
  [System.Runtime.CompilerServices.CompilerGeneratedAttribute()] // Hand crafted but exclude class from docs
  class NamespaceDoc
  {
  }
}
