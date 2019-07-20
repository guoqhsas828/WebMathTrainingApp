//
// Copyright (c) WebMathTraining 2017. All rights reserved.
//

using System;
using System.Collections.Generic;

namespace BaseEntity.Shared
{
  /// <summary>
  ///  Standard Terms cache interface
  /// </summary>
  /// <remarks>
  ///   <note>Is Contravariant to allow easy test of assignability</note>
  /// </remarks>
  public interface IStandardTermsCache<out T> where T : IStandardTerms
  {
    /// <summary>
    /// Cache Keys
    /// </summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Cache values
    /// </summary>
    IEnumerable<T> Values { get; }

    /// <summary>
    /// Set initialisation method for loading standard terms.
    /// </summary>
    /// <remarks>
    ///   <para>Initialises or re-initialises cache. Clears and re-loads cache.</para>
    ///   <note>Loading is deferred until first use.</note>
    /// </remarks>
    /// <param name="initFunc">(Optional) function to initialise standard terms</param>
    void Initialise(Action<IStandardTermsCache<T>> initFunc = null);

    /// <summary>
    /// Clear cache
    /// </summary>
    void Clear();

    /// <summary>
    /// Search cache based on key
    /// </summary>
    /// <param name="key">Key to search for</param>
    /// <returns>True of cache contains specified key</returns>
    bool ContainsKey(string key);
  }
}
