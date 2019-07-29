//
//   2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  /// <summary>
  /// Base class of <see cref="IReferenceRate">Reference Rates</see>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="IReferenceRate" />
  ///   <note>This class is immutable</note>
  /// </remarks>
  /// <seealso cref="IReferenceRate"/>
  [DebuggerDisplay("Reference Rate {Key}")]
  [Serializable]
  [ImmutableObject(true)]
  public abstract class ReferenceRate : BaseEntityObject, IReferenceRate
  {
    #region Constructors

    /// <summary>
    /// Constructor (full)
    /// </summary>
    /// <param name="name">Reference Rate name</param>
    /// <param name="description">Description</param>
    /// <param name="currency">Currency of denomination of the index</param>
    /// <param name="publicationFreq">Frequency of publication of the fixing</param>
    /// <param name="publicationLag">Business days between as-of date and publication date</param>
    /// <param name="calendar">Calendar for index publication</param>
    protected ReferenceRate(string name, string description, Currency currency,
      Frequency publicationFreq, Tenor publicationLag, Calendar calendar)
    {
      Key = name;
      Description = description;
      Currency = currency;
      PublicationFrequency = publicationFreq;
      PublicationLag = publicationLag;
      Calendar = calendar;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Unique index name
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Currency of denomination of the index
    /// </summary>
    public Currency Currency { get; }

    /// <summary>
    /// Frequency of historical observations (publications)
    /// </summary>
    public Frequency PublicationFrequency { get; }

    /// <summary>
    ///   Lag between as-of date and publication date for index. Business days, or term rolled using business day rules
    /// </summary>
    public Tenor PublicationLag { get; }

    /// <summary>
    /// Calendar for index publication
    /// </summary>
    public Calendar Calendar { get; }

    /// <summary>
    /// Default tenor. For some types of reference rates this is empty.
    /// </summary>
    public virtual Tenor DefaultTenor => Tenor.Empty;

    /// <summary>
    /// List of valid tenors. For some types of reference rates this is empty
    /// </summary>
    public virtual Tenor[] ValidTenors => new Tenor[] {};

    #endregion

    #region Methods

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return string.IsNullOrEmpty(Key) ? base.ToString() : $"{this.GetType().Name}:{Key}";
    }

    /// <summary>
    /// Deep copy
    /// </summary>
    /// <returns>Deep copy of the index</returns>
    public override object Clone()
    {
      // IReferenceRate is Immutable and cached so clone just returns same object
      return this;
    }

    /// <summary>
    /// Is this Reference Rate same as another Reference Rate, or does it have the same key?
    /// </summary>
    /// <param name="other">Other Reference Rate for comparison</param>
    /// <returns>True if <paramref name="other"/> Reference Rate is same as this Reference Rate or has the same key</returns>
    public bool IsEqual(IReferenceRate other)
    {
      if (other == null)
        return false;
      return (this == other) || (this.Key == other.Key);
    }

    /// <summary>
    /// Is this Reference Rate equal (in AreEqual sense) to any of the provided Reference Rates?
    /// </summary>
    /// <param name="referenceRates"></param>
    /// <returns>True if this Reference Rates is same as any in <paramref name="referenceRates"/></returns>
    public bool IsEqualToAnyOf(IEnumerable<IReferenceRate> referenceRates)
    {
      if (referenceRates == null) return false;
      return referenceRates.Any(IsEqual);
    }

    /// <summary>
    /// Create reference index
    /// </summary>
    /// <remarks>
    ///   <para>If <paramref name="tenor"/> is empty, the Default Tenor is used.</para>
    /// </remarks>
    /// <param name="tenor">Tenor. For some types of reference rates, this is ignored.</param>
    public abstract ReferenceIndices.ReferenceIndex GetReferenceIndex(Tenor tenor);

    #endregion

    #region Cache

    /// <summary>
    /// Find pre-defined <see cref="IReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <returns>Found <see cref="IReferenceRate"/></returns>
    /// <exception cref="ArgumentException"><see cref="IReferenceRate"/> matching <paramref name="name"/> not found</exception>
    public static T GetValue<T>(string name) where T : IReferenceRate
    {
      return ReferenceRateCache.GetValue<T>(name);
    }

    /// <summary>
    /// Find pre-defined <see cref="IReferenceRate"/> matching specified name
    /// </summary>
    /// <param name="name">Name of reference rate</param>
    /// <param name="referenceRate">Reference rate found</param>
    /// <returns>True if reference rate found</returns>
    public static bool TryGetValue<T>(string name, out T referenceRate) where T : IReferenceRate
    {
      return ReferenceRateCache.TryGetValue<T>(name, out referenceRate);
    }

    /// <summary>
    /// Find pre-defined <see cref="IReferenceRate"/> matching specified criteria
    /// </summary>
    /// <param name="predicate">Name of reference rate</param>
    /// <typeparam name="T">Type of <see cref="IReferenceRate"/></typeparam>
    /// <returns>True if reference rate found</returns>
    public static IEnumerable<T> GetValueWhere<T>(Func<T, bool> predicate) where T : IReferenceRate
    {
      return ReferenceRateCache.Values.OfType<T>().Where(predicate);
    }

    /// <summary>
    /// Add new <see cref="IReferenceRate"/> to defined terms
    /// </summary>
    /// <param name="terms">Terms to add</param>
    /// <exception cref="ArgumentException">Duplicate terms key already defined</exception>
    /// <exception cref="ArgumentNullException">Key is null</exception>
    /// <exception cref="OverflowException">Max number of elements has been reached</exception>
    public static void Add<T>(T terms) where T : IReferenceRate
    {
      ReferenceRateCache.Add(terms);
      return;
    }

    /// <summary>
    /// True if <see cref="IReferenceRate"/> is in defined terms
    /// </summary>
    /// <param name="terms">Reference rate terms to search for</param>
    /// <returns>True if <paramref name="terms"/> are part of defined terms</returns>
    public static bool CacheContains(IReferenceRate terms)
    {
      return ReferenceRateCache.Contains(terms);
    }

    /// <summary>
    /// All standard (pre-defined) <see cref="IReferenceRate">Reference Rates</see>
    /// </summary>
    public static IEnumerable<IReferenceRate> CacheValues => ReferenceRateCache.Values;

    /// <summary>
    /// Set initialisation method for cache of defined reference rates.
    /// </summary>
    /// <remarks>
    ///   <para>Initialises or re-initialises cache of defined reference rates.
    ///   Clears and re-loads cache.</para>
    ///   <note>Loading is deferred until first use.</note>
    /// </remarks>
    /// <param name="initFunc">Function to initialise defined reference rates</param>
    public static void CacheInitialise(Action<IStandardTermsCache<IReferenceRate>> initFunc = null)
    {
      ReferenceRateCache.Initialise(initFunc);
    }

    /// <summary>
    /// Load defined reference rate cache from an XML file
    /// </summary>
    /// <remarks>
    ///   <para>Replace all currently defined reference rates from the specified xml file.</para>
    /// </remarks>
    /// <param name="filename">Name of xml file to load from</param>
    public static void CacheLoadFromPath(string filename)
    {
      ReferenceRateCache.LoadFromPath(filename);
    }

    /// <summary>
    /// Save defined reference rates in XML to a file
    /// </summary>
    /// <param name="filename">Name of xml file to save to</param>
    public static void CacheSaveXmlTerms(string filename)
    {
      ReferenceRateCache.SaveXmlTerms(filename);
    }

    /// <summary>
    /// Cache of standard <see cref="IReferenceRate">Reference Rates</see>
    /// </summary>
    private static ReferenceRateCache ReferenceRateCache
    {
      get
      {
        if (_referenceRateCache != null)
          return _referenceRateCache;
        _referenceRateCache = Configurator.DefaultContainer.Resolve(typeof(IStandardTermsCache<IStandardTerms>), "ReferenceRateCache") as ReferenceRateCache;
        if (_referenceRateCache == null)
          throw new Exception("Internal error - no registered Reference Rate Cache");
        return _referenceRateCache;
      }
    }

    #endregion Cache

    #region Data

    private static ReferenceRateCache _referenceRateCache;

    #endregion Data

  }
}
