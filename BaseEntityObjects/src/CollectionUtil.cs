// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Utility class for Collection operations.
  /// </summary>
  public static class CollectionUtil
  {
    /// <summary>
    /// Performs a bubble sort of the specified IList. This is slow and should not be used for large lists.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>See http://en.wikipedia.org/wiki/Bubblesort.</para>
    /// </remarks>
    ///
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    ///
    public static void BubbleSort<T>(IList<T> array) where T : IComparable<T>
    {
      int i, j;
      T temp;

      for (i = array.Count - 1; i > 0; i--)
      {
        for (j = 0; j < i; j++)
        {
          if (array[j].CompareTo(array[j + 1]) > 0)
          {
            temp = array[j];
            array[j] = array[j + 1];
            array[j + 1] = temp;
          }
        }
      }
    }

    /// <summary>
    /// Performs a bubble sort of array and re-orders array2 according to array
    /// </summary>
    public static void BubbleSortTogether<T, U>(IList<T> array, IList<U> array2) where T : IComparable<T>
    {
      int i, j;
      T temp;
      U temp2;

      if (array == null || array2 == null || array.Count <= 1 || array.Count != array2.Count)
        return;

      for (i = array.Count - 1; i > 0; i--)
      {
        for (j = 0; j < i; j++)
        {
          if (array[j].CompareTo(array[j + 1]) > 0)
          {
            temp = array[j];
            array[j] = array[j + 1];
            array[j + 1] = temp;

            temp2 = array2[j];
            array2[j] = array2[j + 1];
            array2[j + 1] = temp2;
          }
        }
      }
    }

    /// <summary>
    /// Performs a quick sort of the specified generic IList.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>See http://en.wikipedia.org/wiki/Quicksort.</para>
    /// </remarks>
    ///
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    ///
    public static void QuickSort<T>(IList<T> array) where T : IComparable<T>
    {
      if (array != null && array.Count > 1)
        QuickSortHelper<T>(array, 0, array.Count - 1);
    }

    /// <summary>
    /// Helps perform a quick sort of the specified generic IList.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    /// <param name="left"></param>
    /// <param name="right"></param>
    private static void QuickSortHelper<T>(IList<T> array, int left, int right) where T : IComparable<T>
    {
      int i, j;
      T x, y;

      i = left;
      j = right;
      x = array[(left + right)/2];

      do
      {
        while ((array[i].CompareTo(x) < 0) && (i < right)) i++;
        while ((array[j].CompareTo(x) > 0) && (j > left)) j--;

        if (i <= j)
        {
          y = array[i];
          array[i] = array[j];
          array[j] = y;
          i++;
          j--;
        }
      } while (i <= j);

      if (left < j)
        QuickSortHelper<T>(array, left, j);
      if (i < right)
        QuickSortHelper<T>(array, i, right);
    }

    /// <summary>
    /// Performs a quick sort of the specified generic IList.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>See http://en.wikipedia.org/wiki/Quicksort.</para>
    /// </remarks>
    ///
    public static void QuickSort<T>(IList<T> array, IComparer<T> comparer)
    {
      if (array != null && array.Count > 1)
        QuickSortHelper<T>(array, 0, array.Count - 1, comparer.Compare);
    }

    /// <summary>
    /// Performs a quick sort of the specified generic IList.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>See http://en.wikipedia.org/wiki/Quicksort.</para>
    /// </remarks>
    ///
    public static void QuickSort<T>(IList<T> array, Comparison<T> comparer)
    {
      if (array != null && array.Count > 1)
        QuickSortHelper<T>(array, 0, array.Count - 1, comparer);
    }

    /// <summary>
    /// Helps perform a quick sort of the specified generic IList.
    /// </summary>
    private static void QuickSortHelper<T>(IList<T> array, int left, int right, Comparison<T> comparer)
    {
      int i, j;
      T x, y;

      i = left;
      j = right;
      x = array[(left + right)/2];

      do
      {
        while ((comparer(array[i], x) < 0) && (i < right)) i++;
        while ((comparer(array[j], x) > 0) && (j > left)) j--;

        if (i <= j)
        {
          y = array[i];
          array[i] = array[j];
          array[j] = y;
          i++;
          j--;
        }
      } while (i <= j);

      if (left < j)
        QuickSortHelper<T>(array, left, j, comparer);
      if (i < right)
        QuickSortHelper<T>(array, i, right, comparer);
    }

    /// <summary>
    /// Converts an IList[TInput] into a TOuput[].
    /// </summary>
    /// 
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <param name="inputList">The list to convert.</param>
    /// <param name="converter">The conversion function.</param>
    /// 
    /// <returns>An Array</returns>
    /// 
    public static TOutput[] ConvertAll<TInput, TOutput>(IList<TInput> inputList, Converter<TInput, TOutput> converter)
    {
      if (inputList == null)
        return new TOutput[0];

      TOutput[] result = new TOutput[inputList.Count];
      for (int i = 0; i < inputList.Count; i++)
      {
        result[i] = converter(inputList[i]);
      }

      // Done
      return result;
    }

    /// <summary>
    /// Copies all of the items in the itemsToAdd list into the destinationList IList. Note, the items are NOT cloned.
    /// </summary>
    /// 
    /// <typeparam name="T"></typeparam>
    /// <param name="destinationList"></param>
    /// <param name="itemsToAdd"></param>
    /// 
    public static void Add<T>(IList<T> destinationList, IEnumerable<T> itemsToAdd)
    {
      if (itemsToAdd != null && destinationList != null)
        foreach (T obj in itemsToAdd)
          destinationList.Add(obj);
    }

    /// <summary>
    /// Copies all of the items in the itemsToAdd Dictionary into the destination Dictionary. Note that the Values of the dictionary 
    /// ARE cloned.
    /// </summary>
    /// 
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U"></typeparam>
    /// <param name="destination"></param>
    /// <param name="itemsToAdd"></param>
    /// 
    public static void Add<T, U>(IDictionary<T, U> destination, IDictionary<T, U> itemsToAdd)
    {
      foreach (KeyValuePair<T, U> itm in itemsToAdd)
        destination.Add(itm.Key, itm.Value);
    }

    /// <summary>
    /// Copies the collection to an Array.
    /// </summary>
    /// 
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// 
    /// <returns>An array of the elements.</returns>
    /// 
    public static T[] ToArray<T>(ICollection<T> list)
    {
      if (list == null)
        return new T[0];

      T[] result = new T[list.Count];
      int i = 0;
      foreach (T obj in list)
        result[i++] = obj;
      return result;
    }

    /// <summary>
    /// Converts a dictionary into corresponding Key/Value arrays.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="map"></param>
    /// <param name="keys"></param>
    /// <param name="values"></param>
    public static void ToArrays<TKey, TValue>(IDictionary<TKey, TValue> map, out TKey[] keys, out TValue[] values)
    {
      keys = new TKey[map.Count];
      values = new TValue[map.Count];
      int i = 0;
      foreach (var item in map)
      {
        keys[i] = item.Key;
        values[i] = item.Value;
        i++;
      }
    }

    /// <summary>
    /// Finds the first item in a collection that matches the predicate.
    /// </summary>
    /// 
    /// <typeparam name="T">Type of items in the collection, must be a class type</typeparam>
    /// <param name="items">The collection</param>
    /// <param name="predicate">The predicate that tests each item</param>
    /// 
    /// <returns>T</returns>
    /// 
    public static T Find<T>(ICollection<T> items, Predicate<T> predicate) where T : class
    {
      foreach (T itm in items)
      {
        if (predicate(itm))
          return itm;
      }

      // Not Found
      return null;
    }

    /// <summary>
    /// Converts to non generic.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="map">The map.</param>
    /// <returns>Hashtable</returns>
    public static Hashtable ConvertToNonGeneric<TKey, TValue>(IDictionary<TKey, TValue> map)
    {
      var result = new Hashtable(map.Count);
      foreach (var pair in map)
        result.Add(pair.Key, pair.Value);
      return result;
    }

    /// <summary>
    /// Converts to non generic.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list">The list.</param>
    /// <returns>ArrayList</returns>
    public static ArrayList ConvertToNonGeneric<T>(IList<T> list)
    {
      var result = new ArrayList(list.Count);
      foreach (var item in list)
        result.Add(item);
      return result;
    }
  }
}