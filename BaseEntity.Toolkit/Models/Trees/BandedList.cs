// 
// 
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace BaseEntity.Toolkit.Models.Trees
{
  /// <summary>
  ///  A read-only for the data with all zeros at the begin and end portions.
  /// </summary>
  /// <typeparam name="T">The element type</typeparam>
  [DebuggerDisplay("Count = {Count}")]
  [Serializable]
  public class BandedList<T> : IReadOnlyList<T>
  {
    /// <summary>
    /// Gets the underlying list
    /// </summary>
    /// <value>The data.</value>
    public IReadOnlyList<T> Data { get { return _data; } }

    /// <summary>
    /// Gets the index of the first significant value
    /// </summary>
    /// <value>The index of the first significant value</value>
    public int BeginIndex { get { return _begin; } }

    /// <summary>
    /// Gets the first index after the last significant value.
    /// </summary>
    /// <value>The index after the last significant value</value>
    public int EndIndex { get { return _begin + _data.Count; } }

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index</param>
    /// <returns>The value at the specified index</returns>
    public T this[int index]
    {
      get
      {
        if (index < _begin) return default(T);
        index -= _begin;
        if (index >= _data.Count) return default(T);
        return _data[index];
      }
    }

    /// <summary>
    /// Gets the number of elements in the collection.
    /// </summary>
    /// <value>The count.</value>
    public int Count
    {
      get { return _count; }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" />
    /// that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
      for (int i = 0; i < _begin; ++i)
        yield return default(T);
      for (int i = 0, n = _data.Count; i < n; ++i)
        yield return _data[i];
      for (int i = _begin + _data.Count, n = Count; i < n; ++i)
        yield return default(T);
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" />
    /// object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BandedList{T}"/> class.
    /// </summary>
    /// <param name="count">The total number of values in the list</param>
    /// <param name="begin">The index of the first significant value</param>
    /// <param name="data">The index of the last significant value plus 1</param>
    public BandedList(int count, int begin, IReadOnlyList<T> data)
    {
      Debug.Assert(begin >= 0);
      Debug.Assert(data != null);
      Debug.Assert(count >= begin + data.Count);
      _count = count;
      _begin = begin;
      _data = data;
    }

    private readonly int _begin, _count;
    private readonly IReadOnlyList<T> _data;
  }
}
