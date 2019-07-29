/*
 *   2012-2013. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BaseEntity.Toolkit.Util
{
  [Serializable]
  internal class IntervalIndexedValues<TKey, TValue>
    where TKey : IComparable<TKey>
  {
    private readonly TKey[] _keys;
    private readonly TValue[] _values;

    public IntervalIndexedValues(TKey[] keys, TValue[] values)
    {
      Debug.Assert(keys.Length + 1 == values.Length);
      _keys = keys;
      _values = values;
    }

    /// <summary>
    /// Gets the number of values.
    /// </summary>
    public int Count => _values.Length;

    /// <summary>
    /// Gets the index of the value corresponding the key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The index in the array of values.</returns>
    public int GetIndex(TKey key)
    {
      var n = _keys.Length;
      if (n == 0) return 0;
      var index = Array.BinarySearch(_keys, key);
      if (index < 0) index = ~index;
      if (index >= n) index = n;
      return index;
    }

    /// <summary>
    /// Gets the value corresponding the key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value corresponding the key.</returns>
    public TValue GetValue(TKey key)
    {
      return _values[GetIndex(key)];
    }

    /// <summary>
    /// Gets the list of keys.
    /// </summary>
    /// <value>The keys</value>
    public IReadOnlyList<TKey> Keys => _keys;

    /// <summary>
    /// Gets the list of values.
    /// </summary>
    /// <value>The values</value>
    public IReadOnlyList<TValue> Values => _values;
  }
}
