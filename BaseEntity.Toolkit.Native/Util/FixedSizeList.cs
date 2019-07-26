//
// FixedSizeList.cs
// Copyright (c)   2012-2013. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.Serialization;

namespace BaseEntity.Toolkit.Util.Collections
{
  /// <summary>
  ///   Static class containing list utility functions.
  /// </summary>
  public static class ListUtil
  {
    /// <summary>
    ///   Exchange the positions of two elements in the list
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="list">The list</param>
    /// <param name="index1">Position of the first element</param>
    /// <param name="index2">Position of the second element</param>
    public static void Exchange<T>(this IList<T> list, int index1, int index2)
    {
      Debug.Assert(index1 >= 0 && index1 < list.Count);
      Debug.Assert(index2 >= 0 && index2 < list.Count);
      if (index1 == index2) return;

      var tmp = list[index1];
      list[index1] = list[index2];
      list[index2] = tmp;
    }

    /// <summary>
    /// Appends the specified items to the enumerable list.
    /// </summary>
    /// <typeparam name="T">The type of the item</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="items">The items.</param>
    /// <returns>IEnumerable{``0}.</returns>
    public static IEnumerable<T> Append<T>(
      this IEnumerable<T> list, params T[] items)
    {
      return list == null ? items : (items == null ? list : list.Concat(items));
    }

    /// <summary>
    /// Create a read-only list with specified number of repeated elements
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="element">The element value</param>
    /// <param name="count">The number of elements</param>
    /// <returns>The read-only list</returns>
    public static IReadOnlyList<T> Repeat<T>(this T element, int count)
    {
      return new RepeatedElements<T>(element, count);
    }

    /// <summary>
    /// Creates a read-only, fixed size list.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="size">The size of the list.</param>
    /// <param name="getter">The delegate to get element.</param>
    /// <returns>An instance of <c>IList&lt;T&gt;</c>.</returns>
    public static FixedSizeList<T> CreateList<T>(
      int size, Func<int, T> getter)
    {
      return new DelegateFixedSizeList<T>(size, getter, null);
    }

    /// <summary>
    /// Creates a list of fixed size.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="size">The size of the list.</param>
    /// <param name="getter">The delegate to get element.</param>
    /// <param name="setter">The delegate to set element (can be null for read-only list).</param>
    /// <returns>An instance of <c>IList&lt;T&gt;</c>.</returns>
    public static FixedSizeList<T> CreateList<T>(int size,
      Func<int, T> getter, Action<int, T> setter)
    {
      return new DelegateFixedSizeList<T>(size, getter, setter);
    }

    /// <summary>
    /// Converts a list of one type to an array of another type.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the source list.</typeparam>
    /// <typeparam name="U">The type of the elements of the target list.</typeparam>
    /// <param name="list">The list to convert.</param>
    /// <param name="convert">The conversion function.</param>
    /// <returns>A list of the target type.</returns>
    public static FixedSizeList<U> ConvertAll<T,U>(
      this IList<T> list, Func<T,U> convert)
    {
      if (list ==null)
        return null;
      return CreateList(list.Count, (i) => convert(list[i]));
    }

    /// <summary>
    ///  Creates a read-only list with the elements mapped from another list
    /// </summary>
    /// <typeparam name="TSource">The element type of the source list</typeparam>
    /// <typeparam name="TOutput">The element type of the output list</typeparam>
    /// <param name="source">The source list</param>
    /// <param name="map">The map function</param>
    /// <returns>IReadOnlyList&lt;TOutput&gt;.</returns>
    public static FixedSizeList<TOutput> MapList<TSource, TOutput>(
      IReadOnlyList<TSource> source,
      Func<TSource, TOutput> map)
    {
      if (source == null) return null;

      return CreateList(source.Count, i => map(source[i]));
    }

    public static TOutput[] MapAll<TSource, TOutput>(
      IReadOnlyList<TSource> source,
      Func<TSource, TOutput> generate)
    {
      if (source == null) return null;

      var count = source.Count;
      var a = new TOutput[count];
      for (int i = 0; i < count; ++i)
      {
        a[i] = generate(source[i]);
      }
      return a;
    }

    /// <summary>
    /// Determines whether the list is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the list.</typeparam>
    /// <param name="list">The list.</param>
    /// <returns>
    /// 	<c>true</c> if the list is null or empty; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNullOrEmpty<T>(this IList<T> list)
    {
      return list == null || list.Count == 0;
    }

    /// <summary>
    ///   Convert a list to one dimensional array
    /// </summary>
    /// <typeparam name="T">The type of the elements of the list.</typeparam>
    /// <param name="list">The list</param>
    /// <returns>The converted array</returns>
    public static T[] ToArray<T>(this IList<T> list)
    {
      if (list == null)
        return null;
      if (list is T[])
        return (T[])list;
      var a = new T[list.Count];
      list.CopyTo(a, 0);
      return a;
    }

    /// <summary>
    ///  Create a list of the larger of the corresponding 
    ///  elements of two input lists.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the list.</typeparam>
    /// <param name="n">The number of elements in the result list.</param>
    /// <param name="list1">The first input list.</param>
    /// <param name="list2">The second input list.</param>
    /// <returns>The result list.</returns>
    public static FixedSizeList<T> MaxElements<T>(int n,
      IList<T> list1, IList<T> list2) where T : IComparable<T>
    {
      Debug.Assert(n >= 0 && n<=list1.Count && n <= list2.Count);
      return CreateList(n, (i) =>
      {
        T v1 = list1[i], v2 = list2[i];
        return v1.CompareTo(v2) >= 0 ? v1 : v2;
      });
    }

    /// <summary>
    ///  Create a list of the larger of the corresponding 
    ///  elements of two input lists.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the list.</typeparam>
    /// <param name="list1">The first input list.</param>
    /// <param name="list2">The second input list.</param>
    /// <returns>The result list.</returns>
    public static FixedSizeList<T> MaxElements<T>(
      IList<T> list1, IList<T> list2) where T : IComparable<T>
    {
      Debug.Assert(list1.Count == list2.Count);
      return CreateList(list1.Count, (i) =>
      {
        T v1 = list1[i], v2 = list2[i];
        return v1.CompareTo(v2) >= 0 ? v1 : v2;
      });
    }

    /// <summary>
    ///  Calculate the sum of numbers.
    /// </summary>
    /// <param name="list">The list of numbers.</param>
    /// <returns>The sum.</returns>
    public static double Sum(this IList<double> list)
    {
      double sum = 0;
      int n = list.Count;
      for (int i = 0; i < n; ++i)
        sum += list[i];
      return sum;
    }

    /// <summary>
    ///  Calculate the sum of the selected elements in the specified list.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the list.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="start">The start index (inclusive) of the elements to sum.</param>
    /// <param name="stop">The stop index (exclusive) of the elements to sum.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The sum</returns>
    public static double Sum<T>(this IList<T> list,
      int start, int stop, Func<int,T,double> selector)
    {
      Debug.Assert(start >= 0 &&
        start <= stop && stop <= list.Count);
      double sum = 0;
      for (int i = start; i < stop; ++i)
        sum += selector(i, list[i]);
      return sum;
    }

    public static IList<T> CopyFrom<T>(this IList<T> list, T[] array)
    {
      int size = list == null ? 0 : list.Count;
      for (int i = 0; i < size; ++i)
        list[i] = array[i];
      return list;
    }

    public static IList<IList<T>> CopyFrom2<T>(this IList<IList<T>> list, T[][] array)
    {
      int dim1 = list == null ? 0 : list.Count;
      for (int i = 0; i < dim1; ++i)
      {
        var a = array[i];
        var l = list[i];
        int dim2 = l == null ? 0 : l.Count;
        for (int j = 0; j < dim2; ++j)
          l[i] = a[i];
      }
      return list;
    }

    public static T[][] ToArray2<T>(this IList<IList<T>> list)
    {
      if (list == null)
        return new T[0][];
      int dim1 = list.Count;
      T[][] array = new T[dim1][];
      for (int i = 0; i < dim1; ++i)
      {
        var l = list[i];
        if (l == null) continue;
        var a = array[i] = new T[l.Count];
        l.CopyTo(a, 0);
      }
      return array;
    }

    public static T[] NewArray<T>(int count, Func<int, T> generate)
    {
      var a = new T[count];
      for (int i = 0; i < count; ++i)
        a[i] = generate(i);
      return a;
    }

    public static TOutput[] PartialSums<TOutput>(
      int count,
      Func<TOutput, int, TOutput> generate,
      TOutput seed)
    {
      var a = new TOutput[count];
      for (int i = 0; i < count; ++i)
      {
        a[i] = (seed = generate(seed, i));
      }
      return a;
    }

    public static TOutput[] PartialSums<TSource, TOutput>(
      IReadOnlyList<TSource> source,
      Func<TOutput, TSource, TOutput> generate,
      TOutput seed)
    {
      if (source == null) return null;

      var count = source.Count;
      var a = new TOutput[count];
      for (int i = 0; i < count; ++i)
      {
        a[i] = (seed = generate(seed, source[i]));
      }
      return a;
    }

  } // class ListUtil

  #region DelegateFixedSizeList

  /// <summary>
  ///   A class the fixed size list based on two delegates
  ///   for elemnent <c>getter</c> and <c>setter</c>.
  /// </summary>
  /// <typeparam name="T">The type of the elements of the list.</typeparam>
  [Serializable]
  public class DelegateFixedSizeList<T> : FixedSizeList<T>
  {
    public DelegateFixedSizeList(int size,
      Func<int, T> getter, Action<int, T> setter)
    {
      if (getter == null)
        throw new ArgumentNullException("getter");
      _getter = getter;
      _setter = setter;
      _size = size;
    }

    #region Properties

    /// <summary>
    /// Gets the count of elements.
    /// </summary>
    /// <value>The count.</value>
    public override int Count
    {
      get { return _size; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is read only.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is read only; otherwise, <c>false</c>.
    /// </value>
    public override bool IsReadOnly
    {
      get { return _setter == null; }
    }

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <value>The element.</value>
    public override T this[int index]
    {
      get { return _getter(index); }
      set
      {
        if (_setter == null)
          throw new NotSupportedException("Setter not supported.");
        _setter(index, value);
      }
    }

    #endregion

    #region Serialization events

    [OnSerializing]
    private void WrapDelegates(StreamingContext context)
    {
      _getter = _getter.WrapSerializableDelegate();
      _setter = _setter.WrapSerializableDelegate();
    }

    [OnSerialized, OnDeserialized]
    private void UnwrapDelegates(StreamingContext context)
    {
      _getter = _getter.UnwrapSerializableDelegate();
      _setter = _setter.UnwrapSerializableDelegate();
    }

    #endregion

    #region Data

    private int _size;
    private Func<int, T> _getter;
    private Action<int, T> _setter;

    #endregion
  }

  #endregion

  #region FixedSizeList

  /// <summary>
  ///   An abstract class representing a list of the fixed size.
  /// </summary>
  /// <typeparam name="T">The type of the elements of the list.</typeparam>
  [Serializable]
  public abstract class FixedSizeList<T> : BaseEntityObject, IReadOnlyList<T>, IList<T>, IList
  {
    #region Overridable Members
    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <value>The element</value>
    public abstract T this[int index] { get; set; }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
    /// </summary>
    /// <value></value>
    /// <returns>
    /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
    /// </returns>
    public abstract int Count { get; }

    /// <summary>
    /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
    /// </summary>
    /// <value></value>
    /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
    /// </returns>
    public virtual bool IsReadOnly
    {
      get { return false; }
    }

    /// <summary>
    /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1"/>.
    /// </summary>
    /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1"/>.</param>
    /// <returns>
    /// The index of <paramref name="item"/> if found in the list; otherwise, -1.
    /// </returns>
    public virtual int IndexOf(T item)
    {
      int size = Count;
      for (int i = 0; i < size; ++i)
        if (Equals(this[i], item)) return i;
      return -1;
    }

    /// <summary>
    /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
    /// </summary>
    /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="T:System.ArgumentNullException">
    /// 	<paramref name="array"/> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="arrayIndex"/> is less than 0.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// 	<paramref name="array"/> is multidimensional.
    /// -or-
    /// <paramref name="arrayIndex"/> is equal to or greater than the length of <paramref name="array"/>.
    /// -or-
    /// The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.
    /// -or-
    /// Type <paramref name="array"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.
    /// </exception>
    public virtual void CopyTo(T[] array, int arrayIndex)
    {
      int size = Count;
      for (int i = 0; i < size; ++i)
        array[i + arrayIndex] = this[i];
    }
    #endregion

    #region IList<T> Members

    void IList<T>.Insert(int index, T item)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    void IList<T>.RemoveAt(int index)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    #endregion

    #region ICollection<T> Members

    void ICollection<T>.Add(T item)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    void ICollection<T>.Clear()
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    bool ICollection<T>.Contains(T item)
    {
      return (this as List<T>).IndexOf(item) >= 0;
    }

    bool ICollection<T>.Remove(T item)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    #endregion

    #region IEnumerable<T> Members

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
      for (int i = 0; i < Count; ++i)
        yield return (this as IList<T>)[i];
    }

    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      for (int i = 0; i < Count; ++i)
        yield return (this as IList<T>)[i];
    }

    #endregion

    #region IList Members

    int IList.Add(object value)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    void IList.Clear()
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    bool IList.Contains(object value)
    {
      return (this as IList<T>).IndexOf((T)value) >= 0;
    }

    int IList.IndexOf(object value)
    {
      return (this as IList<T>).IndexOf((T)value);
    }

    void IList.Insert(int index, object value)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    bool IList.IsFixedSize
    {
      get { return true; }
    }

    void IList.Remove(object value)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    void IList.RemoveAt(int index)
    {
      throw new NotSupportedException("Operations modifying array size not supported.");
    }

    object IList.this[int index]
    {
      get
      {
        return (this as IList<T>)[index];
      }
      set
      {
        (this as IList<T>)[index] = (T)value;
      }
    }

    #endregion

    #region ICollection Members

    void ICollection.CopyTo(Array array, int index)
    {
      int size = Count;
      for (int i = 0; i < size; ++i)
        array.SetValue(this[i], i + index);
    }

    bool ICollection.IsSynchronized
    {
      get { return false; }
    }

    object ICollection.SyncRoot
    {
      get { return null; }
    }

    #endregion

    #region Object override
    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return String.Format("IList<{0}>({1})", typeof(T), Count);
    }
    #endregion
  } // class FixedSizeList

  #endregion

  #region Repeated elements

  sealed class RepeatedElements<T> : IReadOnlyList<T>
  {
    private readonly T _value;
    private readonly int _count;

    public RepeatedElements(T value, int count)
    {
      _value = value;
      _count = count;
    }

    public T this[int index]
    {
      get { return _value; }
    }

    public int Count
    {
      get { return _count; }
    }

    public IEnumerator<T> GetEnumerator()
    {
      return Enumerable.Repeat(_value, _count).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  #endregion
}
