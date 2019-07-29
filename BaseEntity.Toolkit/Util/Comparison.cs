//
// Comparison.cs
//   2012-2013. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///  Comparers from delegates and related utilities.
  /// </summary>
  /// <remarks></remarks>
  public static class Comparison
  {
    #region Methods
    /// <summary>
    ///  Make a sequence with all the elements distinct by keys.
    /// </summary>
    /// <typeparam name="T">The souce element type</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>A sequence with all the elements distinct by keys.</returns>
    public static IEnumerable<T> DistinctBy<T, TKey>(
      this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
      return source.Distinct(By(keySelector));
    }

    /// <summary>
    ///  Make a sequence with all the elements distinct by key comparison.
    /// </summary>
    /// <typeparam name="T">The souce element type</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="comparer">The comparer.</param>
    /// <returns>A sequence with all the elements distinct by keys.</returns>
    public static IEnumerable<T> DistinctBy<T, TKey>(
      this IEnumerable<T> source, Func<T, TKey> keySelector,
      IEqualityComparer<TKey> comparer)
    {
      return source.Distinct(By(keySelector, comparer));
    }

    /// <summary>
    ///  Get a new converter which preserves the equality.
    /// </summary>
    /// <typeparam name="TInput">The type of the input.</typeparam>
    /// <typeparam name="TOutput">The type of the output.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="converter">The converter.</param>
    /// <param name="getKey">The get key.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static Func<TInput, TOutput> PreserveEqualityBy<TInput, TOutput, TKey>(
      this Func<TInput, TOutput> converter, Func<TInput, TKey> getKey) where TOutput : class
    {
      var dict = new Dictionary<TKey, TOutput>();
      return t =>
        {
          var key = getKey(t);
          TOutput output;
          if (dict.TryGetValue(key, out output)) return output;
          output = converter(t);
          if (output != null) dict.Add(key, output);
          return output;
        };
    }


    /// <summary>
    ///  Create an equality comparer based on keys.
    /// </summary>
    /// <typeparam name="T">The type of the compared.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>An equality comparer.</returns>
    public static IEqualityComparer<T> By<T, TKey>(Func<T, TKey> keySelector)
    {
      return new DelegateEqualityComparer<T, TKey>(keySelector, null);
    }

    /// <summary>
    ///  Create an equality comparer based on keys.
    /// </summary>
    /// <typeparam name="T">The type of the compared.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="comparer">The comparer.</param>
    /// <returns>An equality comparer.</returns>
    public static IEqualityComparer<T> By<T, TKey>(
      Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer)
    {
      return new DelegateEqualityComparer<T, TKey>(keySelector, comparer);
    }
    #endregion

    #region Nested Types
    private class DelegateEqualityComparer<T, TKey> : IEqualityComparer<T>
    {
      private readonly Func<T, TKey> selector_;
      private readonly IEqualityComparer<TKey> comparer_;

      public DelegateEqualityComparer(Func<T, TKey> selector,
        IEqualityComparer<TKey> comparer)
      {
        selector_ = selector;
        comparer_ = comparer;
      }

      #region IEqualityComparer<T> Members

      public bool Equals(T x, T y)
      {
        return comparer_ == null
          ? Equals(selector_(x), selector_(y))
          : comparer_.Equals(selector_(x), selector_(y));
      }

      public int GetHashCode(T obj)
      {
        return comparer_ == null
          ? selector_(obj).GetHashCode()
          : comparer_.GetHashCode(selector_(obj));
      }

      #endregion
    }
    #endregion
  }
}
