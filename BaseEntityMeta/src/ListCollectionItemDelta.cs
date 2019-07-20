// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <remarks></remarks>
  public sealed class ListCollectionItemDelta<TValue> : ICollectionItemDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ListCollectionItemDelta{TValue}"/> class.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="idx">The idx.</param>
    /// <param name="value">The value.</param>
    /// <remarks></remarks>
    public ListCollectionItemDelta(ItemAction action, int idx, TValue value)
    {
      ItemAction = action;
      Idx = idx;
      if (action == ItemAction.Added)
      {
        NewState = value;
      }
      else if (action == ItemAction.Removed)
      {
        OldState = value;
      }
      else
      {
        throw new ArgumentException(
          String.Format("Action [{0}] is not valid for OrderedCollections", action));
      }
    }

    /// <summary>
    /// Gets the item action.
    /// </summary>
    /// <remarks></remarks>
    public ItemAction ItemAction { get; private set; }

    /// <summary>
    /// Gets the idx.
    /// </summary>
    /// <remarks></remarks>
    public int Idx { get; private set; }

    /// <summary>
    ///
    /// </summary>
    /// <value>The value.</value>
    /// <remarks></remarks>
    public TValue OldState { get; private set; }

    /// <summary>
    ///
    /// </summary>
    /// <value>The value.</value>
    /// <remarks></remarks>
    public TValue NewState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public ISnapshotDelta ItemDelta
    {
      get { return null; }
    }
  }
}