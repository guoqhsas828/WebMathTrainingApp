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
  public sealed class BagCollectionItemDelta<T> : ICollectionItemDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BagCollectionItemDelta{T}"/> class.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="item">The item.</param>
    /// <remarks></remarks>
    public BagCollectionItemDelta(ItemAction action, T item)
    {
      ItemAction = action;
      switch (action)
      {
        case ItemAction.Added:
          NewState = item;
          break;
        case ItemAction.Removed:
          OldState = item;
          break;
        default:
          throw new ArgumentException("Action [" + action + "] is not valid for ordered collections");
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ItemAction ItemAction { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public T OldState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public T NewState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public ISnapshotDelta ItemDelta
    {
      get { return null; }
    }
  }
}