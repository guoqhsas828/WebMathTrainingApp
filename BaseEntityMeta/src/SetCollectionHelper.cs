// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using Iesi.Collections;
using System.Collections.Generic;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public static class SetCollectionHelper<T>
  {
    /// <summary>
    /// 
    /// </summary>
    public static object GetDefaultValue()
    {
      return new HashedSet();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="setA"></param>
    /// <param name="setB"></param>
    /// <returns></returns>
    public static bool IsSame(ISet setA, ISet setB)
    {
      if (setA == null)
      {
        return setB == null;
      }
      if (setB == null)
      {
        return false;
      }

      if (setA.Count != setB.Count)
      {
        return false;
      }

      foreach (var item in setA)
      {
        if (!setB.Contains(item))
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
      return reader.ReadSetCollectionDelta<T>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldSet"></param>
    /// <param name="newSet"></param>
    /// <returns></returns>
    public static ISnapshotDelta CreateDelta(ISet oldSet, ISet newSet)
    {
      var itemDeltas = new List<SetCollectionItemDelta<T>>();

      if (IsNullOrEmpty(oldSet))
      {
        if (IsNullOrEmpty(newSet))
          return null;

        foreach (T item in newSet)
        {
          itemDeltas.Add(new SetCollectionItemDelta<T>(ItemAction.Added, item));
        }
      }
      else if (IsNullOrEmpty(newSet))
      {
        foreach (T item in oldSet)
        {
          itemDeltas.Add(new SetCollectionItemDelta<T>(ItemAction.Removed, item));
        }
      }
      else
      {
        // Populate this set with keys from the second map.  As
        // we match keys from the first map, they will be removed
        // from this set, so we can quickly tell which keys have
        // been added.

        var newItems = new HashSet<T>();
        foreach (T item in newSet)
        {
          newItems.Add(item);
        }

        foreach (T item in oldSet)
        {
          if (newSet.Contains(item))
          {
            newItems.Remove(item);
          }
          else
          {
            itemDeltas.Add(new SetCollectionItemDelta<T>(ItemAction.Removed, item));
          }
        }
        foreach (T item in newItems)
        {
          itemDeltas.Add(new SetCollectionItemDelta<T>(ItemAction.Added, item));
        }
      }

      return (itemDeltas.Count == 0) ? null : new SetCollectionDelta<T>(itemDeltas);
    }

    /// <summary>
    /// Determines whether the specified set is null or empty
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns><c>true</c> if [is null or empty] [the specified set]; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    private static bool IsNullOrEmpty(ISet set)
    {
      return (set == null || set.Count == 0);
    }
  }
}