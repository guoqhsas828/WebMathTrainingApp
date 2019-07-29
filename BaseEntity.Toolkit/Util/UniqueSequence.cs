/*
 * UniqueSequence.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   Represents a strongly typed sequence of objects that can be accessed by
  ///   index, with all its elements being unique and sorted.
  /// </summary>
  /// 
  /// <remarks>
  /// </remarks>
  /// 
  /// <typeparam name="T">
  ///   Any type.  If the type has not implemented IComparable interface,
  ///   a comparer must be supplied to the constructor of an
  ///   <c>UniqueSequence</c>.
  /// </typeparam>
  [Serializable]
  public class UniqueSequence<T> : BaseEntityObject, IReadOnlyList<T>, IList<T>, IList
  {
    #region Constructors

    /// <summary>
    ///   Create an empty UniqueSequence with the default initial capacity
    ///   and the default IComparer. 
    /// </summary>
    public UniqueSequence()
    {
      comparer_ = Comparer<T>.Default;
      list_ = new List<T>();
    }

    /// <summary>
    ///   Create an empty UniqueSequence with the default initial capacity
    ///   and using the specified IComparer. 
    /// </summary>
    /// <param name="comparer">The IComparer implementation to use when comparing items.</param>
    public UniqueSequence(IComparer<T> comparer)
    {
      comparer_ = comparer;
      list_ = new List<T>();
    }

    /// <summary>
    ///   Create an instance of UniqueSequence using the specified IComparer,
    ///   and add to it the items from the parameter list.
    /// </summary>
    /// 
    /// <param name="items">List of items to add</param>
    public UniqueSequence(params T[] items)
    {
      comparer_ = Comparer<T>.Default;
      if (items == null || items.Length == 0)
        list_ = new List<T>();
      else
      {
        list_ = new List<T>(items.Length);
        Add(items);
      }
      return;
    }

    /// <summary>
    ///   Create an instance of UniqueSequence using the specified IComparer,
    ///   and add to it the items from the parameter list.
    /// </summary>
    /// 
    /// <param name="comparer">The IComparer implementation to use when comparing items.</param>
    /// <param name="items">List of items to add</param>
    public UniqueSequence(IComparer<T> comparer, params T[] items)
    {
      comparer_ = comparer;
      if (items == null || items.Length == 0)
        list_ = new List<T>();
      else
      {
        list_ = new List<T>(items.Length);
        Add(items);
      }
      return;
    }

    /// <summary>
    ///    Create an UniqueSequence from a list of elements
    /// </summary>
    /// <remarks>
    ///   The input <paramref name="list"/> need not be strongly typed
    ///   but its elements must be able to be casted to the type T.
    /// </remarks>
    /// <param name="list">List of elements</param>
    /// <returns>UniqueSequence</returns>
    public static UniqueSequence<T> From(IEnumerable list)
    {
      UniqueSequence<T> seq = new UniqueSequence<T>();
      foreach (object o in list)
        seq.Add((T)o);
      return seq;
    }

    /// <summary>
    ///    Create an UniqueSequence from a strongly typed list of elements
    /// </summary>
    /// <param name="list">List of elements</param>
    /// <returns>UniqueSequence</returns>
    public static UniqueSequence<T> From(IEnumerable<T> list)
    {
      UniqueSequence<T> seq = new UniqueSequence<T>();
      foreach (T o in list)
        seq.Add(o);
      return seq;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned sequence</returns>
    public override object Clone()
    {
      UniqueSequence<T> obj = (UniqueSequence<T>)base.Clone();
      if (list_ != null)
      {
        List<T> list = new List<T>(list_.Count);
        foreach (T i in list_)
          list.Add(i);
        obj.list_ = list;
      }
      return obj;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Add an item to the sequence
    /// </summary>
    /// <param name="item">Item to add</param>
    /// <returns>The index of the item in the UniqueSequence</returns>
    public int Add(T item)
    {
      int pos = list_.BinarySearch(item, comparer_);
      if (pos < 0)
      {
        pos = ~pos;
        list_.Insert(pos, item);
      }
      return pos;
    }

    /// <summary>
    ///   Add a list of items to the sequence
    /// </summary>
    /// <param name="items">Items to add</param>
    /// <returns>
    ///   True if any of the items is added to the sequence;
    ///   False if none is added, perhaps because they are already
    ///   in the sequence.
    /// </returns>
    public bool Add(params T[] items)
    {
      if (items == null || items.Length == 0)
        return false;
      bool added = false;
      int n = items.Length;
      for (int i = 0; i < n; ++i)
      {
        int pos = list_.BinarySearch(items[i], comparer_);
        if (pos < 0)
        {
          list_.Insert(~pos, items[i]);
          added = true;
        }
      }
      return added;
    }

    /// <summary>
    ///   Add a list of items to the sequence
    /// </summary>
    /// <param name="items">Items to add</param>
    /// <returns>
    ///   True if any of the items is added to the sequence;
    ///   False if none is added, perhaps because they are already
    ///   in the sequence.
    /// </returns>
    public bool Add(IEnumerable<T> items)
    {
      if (items == null)
        return false;
      bool added = false;
      foreach (T item in items)
      {
        int pos = list_.BinarySearch(item, comparer_);
        if (pos < 0)
        {
          list_.Insert(~pos, item);
          added = true;
        }
      }
      return added;
    }

    /// <summary>
    ///   Determines whether all the items are in the sequence. 
    /// </summary>
    /// <param name="items">List of items</param>
    /// <returns>
    ///   False if any of the items is not in the sequence or 
    ///   the input parameters is empty; True otherwise.
    /// </returns>
    public bool ContainsAll(params T[] items)
    {
      if (items == null || items.Length == 0)
        return false;
      foreach (T item in items)
      {
        int pos = list_.BinarySearch(item, comparer_);
        if (pos < 0)
          return false;
      }
      return true;
    }

    /// <summary>
    ///   Determines whether any of the items are in the sequence. 
    /// </summary>
    /// <param name="items">List of items</param>
    /// <returns>
    ///   False if none of the items is in the sequence or 
    ///   the input parameters is empty; True otherwise.
    /// </returns>
    public bool ContainsAny(params T[] items)
    {
      if (items == null || items.Length == 0)
        return false;
      foreach (T item in items)
      {
        int pos = list_.BinarySearch(item, comparer_);
        if (pos >= 0)
          return true;
      }
      return false;
    }

    /// <summary>
    ///   Copies the elements of the List to a new array. 
    /// </summary>
    /// <returns>An array containing copies of the elements of the UniqueSequence.</returns>
    public T[] ToArray()
    {
      return list_.ToArray();
    }

    /// <summary>
    ///    Searches the entire sorted List for an element
    ///   using the default comparer and returns the zero-based 
    ///   index of the element. 
    /// </summary>
    /// <param name="item">The item to search for</param>
    /// <returns>
    ///   The zero-based index of item in the sorted List, 
    ///   if item is found; otherwise, a negative number 
    ///   that is the bitwise complement of the index of 
    ///   the next element that is larger than item or, 
    ///   if there is no larger element, the bitwise 
    ///   complement of Count. 
    /// </returns>
    public int BinarySearch(T item)
    {
      return list_.BinarySearch(item, comparer_);
    }
    #endregion // Methods

    #region Properties
    /// <summary>
    ///   Gets the IComparer for the UniqueSequence
    /// </summary>
    public IComparer<T> Comparer
    {
      get { return comparer_; }
    }

    /// <summary>
    ///   Gets or sets the number of elements that the UniqueSequence can contain. 
    /// </summary>
    public int Capacity
    {
      get { return list_.Capacity; }
      set { list_.Capacity = value; }
    }
    #endregion // Properties

    #region Data

    private List<T> list_;
    private IComparer<T> comparer_;

    #endregion // Data

    #region IList<T> Members

    /// <summary>
    ///   Searches for the specified object and returns the index of it
    ///   within the entire <c>UniqueSequence</c>. 
    /// </summary>
    /// <param name="item">
    ///   The object to locate in the <c>UniqueSequence</c>.
    /// </param>
    /// <returns>
    ///   The index of the item, if found; otherwise, –1. 
    /// </returns>
    public int IndexOf(T item)
    {
      return list_.IndexOf(item);
    }

    /// <summary>
    ///   Note supported by <c>UniqueSequence</c>. 
    /// </summary>
    void IList<T>.Insert(int index, T item)
    {
      throw new System.NotSupportedException(
        "Cannot insert item in UniqueSequence.");
    }

    /// <summary>
    ///   Removes the element at the specified index of the
    ///   <c>UniqueSequence</c>.
    /// </summary>
    /// <param name="index">
    ///   The index of the element to remove.
    /// </param>
    public void RemoveAt(int index)
    {
      list_.RemoveAt(index);
    }

    /// <summary>
    ///   Gets or sets the element at the specified index. 
    /// </summary>
    /// <param name="index">
    ///   The index of the element to get or set.
    /// </param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index]
    {
      get { return list_[index]; }
      set
      {
        throw new System.NotSupportedException(
          "Cannot set item at fixed position in UniqueSequence.");
      }
    }

    #endregion

    #region ICollection<T> Members

    /// <summary>
    ///   Adds an item to the ICollection.
    /// </summary>
    /// <param name="item">Item to add</param>
    void ICollection<T>.Add(T item)
    {
      this.Add(item);
    }

    /// <summary>
    ///   Removes all elements from the <c>UniqueSequence</c>.
    /// </summary>
    public void Clear()
    {
      list_.Clear();
    }

    /// <summary>
    ///   Determines whether the <c>UniqueSequence</c>
    ///   contains a specific item.
    /// </summary>
    /// <param name="item">
    ///   Item to locate in the <c>UniqueSequence</c>.
    /// </param>
    /// <returns>
    ///   true if item is found in the <c>UniqueSequence</c>;
    ///   otherwise, false
    /// </returns>
    public bool Contains(T item)
    {
      return list_.BinarySearch(item,comparer_) >= 0;
    }

    /// <summary>
    ///   Copies the elements of the <c>UniqueSequence</c>
    ///   to an Array, starting at a particular Array index.
    /// </summary>
    /// <param name="array">
    ///   The one-dimensional Array that is the destination of the elements
    ///   copied from <c>UniqueSequence</c>.
    /// </param>
    /// <param name="arrayIndex">
    ///   The index in array at which copying begins.
    /// </param>
    public void CopyTo(T[] array, int arrayIndex)
    {
      list_.CopyTo(array, arrayIndex);
    }

    /// <summary>
    ///   Gets the number of elements contained in the
    ///   <c>UniqueSequence</c>.
    /// </summary>
    public int Count
    {
      get { return list_.Count; }
    }

    /// <summary>
    ///   Gets a value indicating whether the
    ///   <c>UniqueSequence</c>
    ///   is read-only. 
    /// </summary>
    public bool IsReadOnly
    {
      get { return false; }
    }

    /// <summary>
    ///   Removes the item from the <c>UniqueSequence</c>.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>
    ///   true if item is successfully removed from the
    ///   <c>UniqueSequence</c>;
    ///   otherwise, false. This method also returns false
    ///   if item is not found in the original sequence.
    /// </returns>
    public bool Remove(T item)
    {
      int pos = list_.BinarySearch(item);
      if (pos < 0)
        return false;
      list_.RemoveAt(pos);
      return true;
    }

    #endregion

    #region IEnumerable<T> Members

    /// <summary>
    ///   Returns an enumerator that iterates through the collection
    /// </summary>
    /// <returns>
    ///   A IEnumerator that can be used to iterate through the collection
    /// </returns>
    public IEnumerator<T> GetEnumerator()
    {
      return ((IEnumerable<T>)list_).GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    /// <summary>
    ///   Returns an enumerator that iterates through the collection
    /// </summary>
    /// <returns>
    ///   A IEnumerator that can be used to iterate through the collection
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return ((IEnumerable)list_).GetEnumerator();
    }

    #endregion

    #region IList Members

    int IList.Add(object value)
    {
      T item = (T)value;
      return this.Add(item);
    }

    bool IList.Contains(object value)
    {
      T item = (T)value;
      return this.Contains(item);
    }

    int IList.IndexOf(object value)
    {
      return ((IList)list_).IndexOf(value);
    }

    void IList.Insert(int index, object value)
    {
      throw new System.NotSupportedException(
        "Cannot insert item in UniqueSequence.");
    }

    bool IList.IsFixedSize
    {
      get { return ((IList)list_).IsFixedSize; }
    }

    void IList.Remove(object value)
    {
      T item = (T)value;
      this.Remove(item);
    }

    object IList.this[int index]
    {
      get { return ((IList)list_)[index]; }
      set
      {
        throw new System.NotSupportedException(
          "Cannot set item at fixed position in UniqueSequence.");
      }
    }

    #endregion

    #region ICollection Members

    void ICollection.CopyTo(Array array, int index)
    {
      ((ICollection)list_).CopyTo(array, index);
    }

    bool ICollection.IsSynchronized
    {
      get { return ((ICollection)list_).IsSynchronized; }
    }

    object ICollection.SyncRoot
    {
      get { return ((ICollection)list_).SyncRoot; }
    }

    #endregion
  }
}
