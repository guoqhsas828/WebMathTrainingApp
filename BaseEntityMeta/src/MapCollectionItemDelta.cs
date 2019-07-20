// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Represents the difference in a single item in a map
  /// </summary>
  public class MapCollectionItemDelta<TKey, TValue> : ICollectionItemDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="MapCollectionItemDelta{TKey, TValue}"/> class 
    /// to represent an item that has been Added or Removed.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <remarks></remarks>
    public MapCollectionItemDelta(ItemAction action, TKey key, TValue value)
    {
      ItemAction = action;
      Key = key;
      if (action == ItemAction.Added)
        NewState = value;
      else if (action == ItemAction.Removed)
        OldState = value;
      else
        throw new ArgumentException("Invalid ItemAction: " + action);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapCollectionItemDelta{TKey, TValue}"/> class to represent and item that has been changed.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="itemDelta">The snapshot delta.</param>
    /// <remarks></remarks>
    public MapCollectionItemDelta(TKey key, ISnapshotDelta itemDelta)
    {
      ItemAction = ItemAction.Changed;

      Key = key;
      
      Delta = itemDelta;
    }

    /// <summary>
    /// Gets the item action.
    /// </summary>
    /// <remarks></remarks>
    public ItemAction ItemAction { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public TKey Key { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public TValue OldState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public TValue NewState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public ISnapshotDelta Delta { get; private set; }
  }
}