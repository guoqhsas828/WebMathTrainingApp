/*
 * KeyedSet.cs
 *
 *  -2010. All rights reserved.
 *
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   A collection of items with a key to uniquely identify it.
  /// </summary>
  /// <remarks>By construction, this class avoids creating multiple instances of the same set.</remarks>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  internal class KeyedSet<T>
  {
    #region Data
    private static KeyGenerator KeyGenerator = new KeyGenerator(1000);
    private static readonly string Prefix = typeof(KeyedSet<T>).TypeHandle.Value.ToString();

    private static readonly ConcurrentDictionary<HashSet<T>, KeyedSet<T>> cache_
      = new ConcurrentDictionary<HashSet<T>, KeyedSet<T>>(HashSet<T>.CreateSetComparer());

    private readonly HashSet<T> set_;
    private readonly string key_;
    #endregion

    /// <summary>
    /// Gets the instance.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns></returns>
    /// <remarks>This class avoids creating multiple instances of the same set.</remarks>
    public static KeyedSet<T> GetInstance(IEnumerable<T> items)
    {
      var set = new HashSet<T>(items);
      KeyedSet<T> group = null;
      cache_.AddOrUpdate(set,
        key => group = new KeyedSet<T>(key),
        (key, sel) => group = sel);
      return group;
    }

    /// <summary>
    /// Prevents a default instance of the <see cref="KeyedSet&lt;T&gt;"/> class from being created.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <remarks></remarks>
    private KeyedSet(HashSet<T> set)
    {
      set_ = set;
      key_ = Prefix + '-' + KeyGenerator.Generate();
    }

    /// <summary>
    /// Determines whether this set contains the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c> if it contains the specified item; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public bool Contains(T item)
    {
      return set_.Contains(item);
    }

    /// <summary>
    /// Gets the key uniquely identifying this set.
    /// </summary>
    /// <remarks>The key is unique within the current application domain.</remarks>
    public string Key
    {
      get { return key_; }
    }
  }

  internal struct KeyGenerator
  {
    private long seed_;
    public KeyGenerator(long seed)
    {
      seed_ = seed;
    }
    public long Generate()
    {
      return Interlocked.Increment(ref seed_);
    }
  }
}
