// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public static class ListCollectionHelper<T>
  {
    /// <summary>
    /// 
    /// </summary>
    public static object GetDefaultValue()
    {
      return new List<T>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="listA"></param>
    /// <param name="listB"></param>
    /// <returns></returns>
    public static bool IsSame(IList<T> listA, IList<T> listB)
    {
      if (listA == null)
      {
        return listB == null;
      }
      if (listB == null)
      {
        return false;
      }

      if (listA.Count != listB.Count)
      {
        return false;
      }

      for (int i = 0; i < listA.Count; ++i)
      {
        if (!ValueComparer<T>.IsSame(listA[i], listB[i]))
          return false;
      }

      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadOrderedCollectionDelta<T>();
    }

    /// <summary>
    /// Diffs the ordered collection.
    /// </summary>
    /// <param name="oldList">The old list.</param>
    /// <param name="newList">The new list.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static ISnapshotDelta CreateDelta(IList<T> oldList, IList<T> newList)
    {
      var itemDiffs = new List<ListCollectionItemDelta<T>>();

      if (oldList == null) oldList = new List<T>();
      if (newList == null) newList = new List<T>();

      List<int> addedItems;
      List<int> removedItems;
      CalculateLcsDiff(
        newList,
        oldList,
        out addedItems,
        out removedItems);

      foreach (var idx in removedItems)
      {
        var snapshot = oldList[idx];
        itemDiffs.Add(new ListCollectionItemDelta<T>(ItemAction.Removed, idx, snapshot));
      }

      foreach (var idx in addedItems)
      {
        var snapshot = newList[idx];
        itemDiffs.Add(new ListCollectionItemDelta<T>(ItemAction.Added, idx, snapshot));
      }

      return (itemDiffs.Count == 0) ? null : new ListCollectionDelta<T>(itemDiffs);
    }

    /// <summary>
    /// Calculates the LCS lengths.
    /// </summary>
    /// <param name="oldList">The old list.</param>
    /// <param name="newList">The new list.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    private static int[,] CalculateLcsLengths(IList<T> oldList, IList<T> newList)
    {
      var subLcsLengths = new int[newList.Count + 1, oldList.Count + 1];
      for (int i = newList.Count - 1; i >= 0; i--)
      {
        for (int j = oldList.Count - 1; j >= 0; j--)
        {
          var newItem = newList[i];
          var oldItem = oldList[j];
          if (newItem.Equals(oldItem))
            subLcsLengths[i, j] = 1 + subLcsLengths[i + 1, j + 1];
          else
            subLcsLengths[i, j] = Math.Max(subLcsLengths[i + 1, j], subLcsLengths[i, j + 1]);
        }
      }
      return subLcsLengths;
    }

    /// <summary>
    /// Calculates the LCS diff.
    /// </summary>
    /// <param name="newList">The new list.</param>
    /// <param name="oldList">The old list.</param>
    /// <param name="addedItems">The added items.</param>
    /// <param name="removedItems">The removed items.</param>
    /// <remarks></remarks>
    private static void CalculateLcsDiff(IList<T> newList, IList<T> oldList, out List<int> addedItems, out List<int> removedItems)
    {
      addedItems = new List<int>();
      removedItems = new List<int>();

      var subLcsLengths = CalculateLcsLengths(oldList, newList);

      var i = 0;
      var j = 0;
      while (i < newList.Count && j < oldList.Count)
      {
        var o1 = newList[i];
        var o2 = oldList[j];
        if (ValueComparer<T>.IsSame(o1, o2))
        {
          i++;
          j++;
        }
        else if (subLcsLengths[i + 1, j] >= subLcsLengths[i, j + 1])
        {
          addedItems.Add(i);
          i++;
        }
        else
        {
          removedItems.Add(j);
          j++;
        }
      }

      // All elements after i are new elements
      for (int x = i; x < newList.Count; x++)
      {
        addedItems.Add(x);
      }

      // All elements after j are deleted elements
      for (int x = j; x < oldList.Count; x++)
      {
        removedItems.Add(x);
      }
    }
  }
}