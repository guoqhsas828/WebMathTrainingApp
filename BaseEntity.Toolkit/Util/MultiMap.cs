using System;
using System.Collections.Generic;
using System.Collections;


namespace BaseEntity.Toolkit.Util
{

  /// <summary>
  /// Generic multimap class
  /// </summary>
  /// <typeparam name="TKey">Key</typeparam>
  /// <typeparam name="TValue">Value</typeparam>
  public class SortedMultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, List<TValue>>> where TKey : IComparable
  {
    #region Data

    private readonly SortedDictionary<TKey, List<TValue>> dictionary_ = new SortedDictionary<TKey, List<TValue>>();

    #endregion

    #region Methods

    /// <summary>
    /// Adds a list of values corresponding to a given key. If the key is the same the list will contain as many elements as there are items with the given key   
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="value">Value </param>
    public void Add(TKey key, TValue value)
    {
      List<TValue> list;
      if (dictionary_.TryGetValue(key, out list))
        list.Add(value);
      else
      {
        list = new List<TValue> {value};
        dictionary_[key] = list;
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the collection of keys for enumeration
    /// </summary>
    public IEnumerable<TKey> Keys
    {
      get { return dictionary_.Keys; }
    }

    /// <summary>
    /// Indexer. It returns an empty list if nothing exists. 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public List<TValue> this[TKey key]
    {
      get
      {
        List<TValue> list;
        if (dictionary_.TryGetValue(key, out list))
          return list;
        return new List<TValue>();
      }
    }

    /// <summary>
    /// Counts number of elements in the multimap
    /// </summary>
    public int Count
    {
      get { return dictionary_.Count; }
    }

    #endregion

    #region IEnumerable<KeyValuePair<K,V>> Members

    /// <summary>
    /// Get enumerator
    /// </summary>
    public IEnumerator<KeyValuePair<TKey, List<TValue>>> GetEnumerator()
    {
      return ((IEnumerable<KeyValuePair<TKey, List<TValue>>>)dictionary_).GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      return dictionary_.GetEnumerator();
    }

    #endregion
  }
}