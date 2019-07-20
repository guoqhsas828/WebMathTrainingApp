// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="TKey"></typeparam>
  /// <typeparam name="TValue"></typeparam>
  public static class MapCollectionHelper<TKey, TValue>
  {
    /// <summary>
    /// 
    /// </summary>
    public static object GetDefaultValue()
    {
      return new Dictionary<TKey, TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mapA"></param>
    /// <param name="mapB"></param>
    /// <returns></returns>
    public static bool IsSame(IDictionary<TKey, TValue> mapA, IDictionary<TKey, TValue> mapB)
    {
      if (mapA == null)
      {
        return mapB == null;
      }

      if (mapB == null)
      {
        return false;
      }

      if (mapA.Count != mapB.Count)
      {
        return false;
      }

      foreach (var entry in mapA)
      {
        TValue newValue;
        if (!mapB.TryGetValue(entry.Key, out newValue))
          return false;

        var oldValue = entry.Value;

        bool isSame = ValueComparer<TValue>.IsSame(oldValue, newValue);
        if (!isSame)
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
      return reader.ReadMapCollectionDelta<TKey, TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldMap"></param>
    /// <param name="newMap"></param>
    /// <returns></returns>
    public static ISnapshotDelta CreateDelta(IDictionary<TKey, TValue> oldMap, IDictionary<TKey, TValue> newMap)
    {
      var itemDiffs = new List<MapCollectionItemDelta<TKey, TValue>>();

      if (IsNullOrEmpty(oldMap))
      {
        if (IsNullOrEmpty(newMap))
        {
          return null;
        }
        itemDiffs.AddRange(newMap.Select(iter => new MapCollectionItemDelta<TKey, TValue>(ItemAction.Added, iter.Key, iter.Value)));
        return new MapCollectionDelta<TKey, TValue>(itemDiffs);
      }

      if (IsNullOrEmpty(newMap))
      {
        itemDiffs.AddRange(oldMap.Select(iter => new MapCollectionItemDelta<TKey, TValue>(ItemAction.Removed, iter.Key, iter.Value)));
        return new MapCollectionDelta<TKey, TValue>(itemDiffs);
      }

      // Used to detect unmatched items
      var unmatchedKeys = new HashSet<TKey>();
      foreach (var key in newMap.Keys)
      {
        unmatchedKeys.Add(key);
      }

      foreach (var kvp in oldMap)
      {
        TKey oldKey = kvp.Key;
        TValue oldValue = kvp.Value;

        TValue newValue;
        if (newMap.TryGetValue(oldKey, out newValue))
        {
          ISnapshotDelta valueDelta;
          if (typeof(BaseEntityObject).IsAssignableFrom(typeof(TValue)))
          {
            valueDelta = ClassMeta.CreateDelta(oldValue as BaseEntityObject, newValue as BaseEntityObject);
          }
          else
          {
            if (oldValue.Equals(default(TValue)))
              valueDelta = newValue.Equals(default(TValue)) ? null : new ScalarDelta<TValue>(default(TValue), newValue);
            else if (newValue.Equals(default(TValue)))
              valueDelta = new ScalarDelta<TValue>(oldValue, default(TValue));
            else
              valueDelta = oldValue.Equals(newValue) ? null : new ScalarDelta<TValue>(oldValue, newValue);
          }
          if (valueDelta != null)
          {
            itemDiffs.Add(new MapCollectionItemDelta<TKey, TValue>(oldKey, valueDelta));
          }

          unmatchedKeys.Remove(oldKey);
        }
        else
        {
          itemDiffs.Add(new MapCollectionItemDelta<TKey, TValue>(ItemAction.Removed, oldKey, oldValue));
        }
      }
      itemDiffs.AddRange(unmatchedKeys.Select(key => new MapCollectionItemDelta<TKey, TValue>(ItemAction.Added, key, newMap[key])));

      return (itemDiffs.Count == 0) ? null : new MapCollectionDelta<TKey, TValue>(itemDiffs);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dict">The dict.</param>
    /// <returns><c>true</c> if [is null or empty] [the specified dict]; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    private static bool IsNullOrEmpty(IDictionary<TKey, TValue> dict)
    {
      return (dict == null || dict.Count == 0);
    }
  }
}