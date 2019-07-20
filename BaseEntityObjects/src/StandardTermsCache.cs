//
// Copyright (c) WebMathTraining 2017. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BaseEntity.Shared
{
  /// <summary>
  ///  Standard terms cache base class
  /// </summary>
  /// <remarks>
  ///   <para>Implementation of <seealso cref="StandardTermsCache{T}"/></para>
  ///   <para>Wraps <see cref="ConcurrentDictionary{TKey,TValue}"/>.</para>
  /// </remarks>
  public abstract class StandardTermsCache<T> : IStandardTermsCache<T> where T : IStandardTerms
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="initFunct">Default initialisation function</param>
    protected StandardTermsCache(Action<StandardTermsCache<T>> initFunct)
    {
      _initFunc = initFunct;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   List of all Standard Terms
    /// </summary>
    public IEnumerable<T> Values => Terms.Values;

    /// <summary>
    ///   List of all Standard Term keys
    /// </summary>
    public IEnumerable<string> Keys => Terms.Keys;

    /// <summary>
    ///   Dictionary of all Standard Terms indexed by Name
    /// </summary>
    /// <remarks>
    ///   <para>The cache is loaded the first time it is refereced.</para>
    /// </remarks>
    protected ConcurrentDictionary<string, T> Terms
    {
      get
      {
        if (_terms != null) return _terms;
        // Initialise terms
        _terms = new ConcurrentDictionary<string, T>();
        _initFunc?.Invoke(this);
        return _terms;
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Initialise standard terms
    /// </summary>
    /// <remarks>
    ///   <para>Initialises or re-initialises cache. Clears and re-loads cache.</para>
    ///   <note>Loading is deferred until first use.</note>
    /// </remarks>
    public void Initialise()
    {
      _terms = null;
    }

    /// <summary>
    /// Set initialisation method for loading standard terms.
    /// </summary>
    /// <remarks>
    ///   <note>Loading is deferred until first use.</note>
    /// </remarks>
    /// <param name="initFunc">Function to initialise standard terms</param>
    public void Initialise(Action<IStandardTermsCache<T>> initFunc)
    {
      _initFunc = initFunc;
      Initialise();
    }

    /// <summary>
    /// Clear cache
    /// </summary>
    public void Clear()
    {
      if (_terms == null)
        // Create empty cache to avoid deferred re-initialisation of Cache
        _terms = new ConcurrentDictionary<string, T>();
      else
        _terms.Clear();
      return;
    }

    /// <summary>
    ///  Test if cache contains specified key.
    /// </summary>
    /// <param name="key">Key to seach for</param>
    /// <returns>True if cache contains specifeid key, false otherwise</returns>
    public bool ContainsKey(string key)
    {
      return Terms.ContainsKey(key);
    }

    /// <summary>
    ///   Find cache entry matching the specified key and type
    /// </summary>
    /// <remarks>
    ///   <para>Finds the Terms matching the specified key and type.</para>
    /// </remarks>
    /// <typeparam name="T1">Type to search for</typeparam>
    /// <param name="key">key to search for</param>
    /// <param name="terms">Found terms</param>
    /// <returns>True if terms found, false otherwise</returns>
    public bool TryGetValue<T1>(string key, out T1 terms) where T1 : T
    {
      terms = default(T1);
      T t;
      if (!Terms.TryGetValue(key, out t)) return false;
      if (!(t is T1))
        return false;
      terms = (T1)t;
      return true;
    }

    /// <summary>
    ///   Find terms matching the specified key and type
    /// </summary>
    /// <remarks>
    ///   <para>Finds the terms matching the specified key and type.
    ///   Throws exception if not found.</para>
    /// </remarks>
    /// <typeparam name="T1">Type of term</typeparam>
    /// <param name="key">Key to search for</param>
    /// <exception cref="ArgumentException">Specified key not found</exception>
    /// <returns>Terms found or throws exception</returns>
    public T1 GetValue<T1>(string key) where T1 : T
    {
      T terms;
      if (!Terms.TryGetValue(key, out terms))
        throw new ArgumentException($"Can't find {typeof(T1)} terms matching key {key}");
      if (terms is T1)
        return (T1)terms;
      throw new ArgumentException($"Term matching key {key} is not of required type {typeof(T1)}");
    }

    /// <summary>
    /// Add terms to cache
    /// </summary>
    /// <param name="terms">Terms to add</param>
    /// <exception cref="ArgumentException">Duplicate terms key already defined</exception>
    public void Add(T terms)
    {
      if (!Terms.TryAdd(terms.Key, terms))
        throw new ArgumentException($"Could not add terms {terms.Key}");
      return;
    }

    /// <summary>
    /// Add or update terms to cache
    /// </summary>
    /// <remarks>
    ///   <para>Looks for cache entry with key and type matching <paramref name="terms"/>.
    ///   If found replaces, if not found, adds <paramref name="terms"/> to cache.</para>
    /// </remarks>
    /// <typeparam name="T1">Type of object to add/update</typeparam>
    /// <param name="terms">Object to add/update to cache</param>
    /// <exception cref="ArgumentNullException">Unable to add/update entry in cache</exception>
    public T1 AddOrUpdate<T1>(T1 terms) where T1 : T
    {
      return (T1)Terms.AddOrUpdate(terms.Key, terms, (k, v) => terms);
    }

    #endregion Methods

    #region Data

    private Action<StandardTermsCache<T>> _initFunc; // Initialise function
    private ConcurrentDictionary<string, T> _terms; // Cache of standard terms

    #endregion
  }
}
