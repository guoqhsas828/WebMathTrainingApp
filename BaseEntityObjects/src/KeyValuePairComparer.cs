/*
 * KeyValuePairComparer.cs
 *
 * Copyright (c) WebMathTraining 2008. All rights reserved.
 *
 * Created by rsmulktis on 3/3/2008 10:28:00 AM
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using log4net;

using BaseEntity.Shared;

namespace BaseEntity.Shared
{
  /// <summary>
  /// The order to sort the items.
  /// </summary>
  public enum SortOrder
  {
    /// <summary>
    /// 
    /// </summary>
    Ascending,
    /// <summary>
    /// 
    /// </summary>
    Descending
  }

  /// <summary>
  /// The field to sort the items by.
  /// </summary>
  public enum SortField
  {
    /// <summary>
    /// 
    /// </summary>
    Key,
    /// <summary>
    /// 
    /// </summary>
    Value
  }

  /// <summary>
  /// KeyValuePairComparer class.
  /// </summary>
  public class KeyValuePairComparer<TKey,TValue> : IComparer<KeyValuePair<TKey,TValue>> where TKey : IComparable<TKey> where TValue : IComparable<TValue>
  {
    #region Data
    //logger
    private readonly SortOrder sortOrder_ = SortOrder.Ascending;
    private readonly SortField sortField_ = SortField.Value;
    #endregion

    #region Constructors
    /// <summary>
    /// Default Constructor
    /// </summary>
    public KeyValuePairComparer(SortField field, SortOrder order)
    {
      sortField_ = field;
      sortOrder_ = order;
    }
    #endregion

    #region IComparer<KeyValuePair<TKey,TValue>> Members

    /// <summary>
    /// Compare to key/value pairs.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int Compare(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
    {
      if (sortField_ == SortField.Key)
        return x.Key.CompareTo(y.Key) * (sortOrder_ == SortOrder.Ascending ? 1 : -1);
      else
        return x.Value.CompareTo(y.Value) * (sortOrder_ == SortOrder.Ascending ? 1 : -1);
    }
    #endregion
  }
}
