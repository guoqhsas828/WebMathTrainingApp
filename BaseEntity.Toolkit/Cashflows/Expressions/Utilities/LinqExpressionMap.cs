using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Utilities
{
  /// <summary>
  ///  A dictionary with LINQ Expressions as the keys.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class LinqExpressionMap<T> : IEnumerable<KeyValuePair<Expression, T>>
  {
    #region Instance members

    // Both _cache and _map represent the same collection, but with different comparer.
    //  _cache is based on the fast reference equality comparer;
    //  _map is based on the content comparer which is slow but is able to check
    //    if two distinct instances has the same content.
    // This is useful when we build the collection from the subexpressions which are
    //   most likely reused in later queries.
    private readonly Dictionary<Expression, T> _cache
      = new Dictionary<Expression, T>();

    private readonly Dictionary<Expression, T> _map
      = new Dictionary<Expression, T>(LinqExpressionUtility.Comparer);

    /// <summary>
    ///   Get the expression comparer.
    /// </summary>
    public IEqualityComparer<Expression> Comparer => _map.Comparer;

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>value associated with key</returns>
    /// <exception cref="System.InvalidOperationException"></exception>
    public T this[Expression key]
    {
      get
      {
        T value;
        if (TryGetValue(key, out value))
          return value;
        throw new InvalidOperationException(String.Format(
          "Key not found: {0}", key));
      }
    }

    /// <summary>
    /// Try to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c> if the key exists in the dictionary, <c>false</c> otherwise</returns>
    public bool TryGetValue(Expression key, out T value) =>
      _cache.TryGetValue(key, out value) || _map.TryGetValue(key, out value);

    /// <summary>
    /// Adds the specified key-value pair to the dictionary.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Add(Expression key, T value)
    {
      _cache.Add(key, value);
      _map.Add(key, value);
    }

    /// <summary>
    /// Clears this dictionary by removing all the key-value pairs.
    /// </summary>
    public void Clear()
    {
      _cache.Clear();
      _map.Clear();
    }

    /// <summary>
    /// Determines whether the specified key exists.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><c>true</c> if the specified key exists; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(Expression key) =>
      _cache.ContainsKey(key) || _map.ContainsKey(key);

    /// <summary>
    /// Gets the count of items contained in the dictionary.
    /// </summary>
    /// <value>The count.</value>
    public int Count => _map.Count;

    /// <summary>
    ///   Gets all the keys
    /// </summary>
    public IEnumerable<Expression> Keys => _map.Keys;

    #endregion

    #region IEnumerable<KeyValuePair<Expression,T>> Members

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
    public IEnumerator<KeyValuePair<Expression, T>> GetEnumerator()
    {
      return _map.GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return _map.GetEnumerator();
    }

    #endregion
  }
}
