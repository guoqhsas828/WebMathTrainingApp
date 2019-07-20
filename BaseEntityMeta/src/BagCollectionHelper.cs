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
  /// <typeparam name="TValue"></typeparam>
  public static class BagCollectionHelper<TValue>
  {
    /// <summary>
    /// 
    /// </summary>
    public static object GetDefaultValue()
    {
      return new List<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static ISnapshotDelta CreateDelta(IEntityDeltaReader reader)
    {
      return reader.ReadBagCollectionDelta<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldBag"></param>
    /// <param name="newBag"></param>
    /// <returns></returns>
    public static ISnapshotDelta CreateDelta(IList<TValue> oldBag, IList<TValue> newBag)
    {
      var itemDiffs = new List<BagCollectionItemDelta<TValue>>();

      int oldListCount = (IsNullOrEmpty(oldBag)) ? 0 : oldBag.Count;
      int newListCount = (IsNullOrEmpty(newBag)) ? 0 : newBag.Count;

      bool[] oldItemsMatched = new bool[oldListCount];
      bool[] newItemsMatched = new bool[newListCount];

      for (int i = 0; i < oldListCount; i++)
      {
        var oldValue = oldBag[i];
        for (int j = 0; j < newListCount; j++)
        {
          if (!newItemsMatched[j])
          {
            var newValue = newBag[j];
            if (ValueComparer<TValue>.IsSame(oldValue, newValue))
            {
              oldItemsMatched[i] = true;
              newItemsMatched[j] = true;
              break;
            }
          }
        }
      }
      for (int i = 0; i < oldListCount; i++)
      {
        if (!oldItemsMatched[i])
        {
          itemDiffs.Add(new BagCollectionItemDelta<TValue>(ItemAction.Removed, oldBag[i]));
        }
      }
      for (int j = 0; j < newListCount; j++)
      {
        if (!newItemsMatched[j])
        {
          itemDiffs.Add(new BagCollectionItemDelta<TValue>(ItemAction.Added, newBag[j]));
        }
      }

      return (itemDiffs.Count == 0) ? null : new BagCollectionDelta<TValue>(itemDiffs);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldBag"></param>
    /// <param name="newBag"></param>
    /// <returns></returns>
    public static bool IsSame(IList<TValue> oldBag, IList<TValue> newBag)
    {
      int oldListCount = IsNullOrEmpty(oldBag) ? 0 : oldBag.Count;
      int newListCount = IsNullOrEmpty(newBag) ? 0 : newBag.Count;

      if (oldListCount != newListCount)
      {
        return false;
      }

      bool[] newItemsMatched = new bool[newListCount];

      for (int i = 0; i < oldListCount; i++)
      {
        int j = 0;
        while (j < newListCount)
        {
          if (!newItemsMatched[j])
          {
            if (ValueComparer<TValue>.IsSame(oldBag[i], newBag[j]))
            {
              newItemsMatched[j] = true;
              break;
            }
          }
          j++;
        }
        if (j == newListCount)
        {
          return false;
        }
      }
      for (int j = 0; j < newListCount; j++)
      {
        if (!newItemsMatched[j])
        {
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="coll">The coll.</param>
    /// <returns><c>true</c> if [is null or empty] [the specified coll]; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    private static bool IsNullOrEmpty(ICollection<TValue> coll)
    {
      return (coll == null || coll.Count == 0);
    }
  }
}