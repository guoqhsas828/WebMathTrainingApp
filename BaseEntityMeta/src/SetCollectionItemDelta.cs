// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Represents the difference in a single item in a set
  /// </summary>
  public class SetCollectionItemDelta<TValue> : ICollectionItemDelta
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SetCollectionItemDelta{TValue}"/> class.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="value">The value.</param>
    /// <remarks></remarks>
    public SetCollectionItemDelta(ItemAction action, TValue value)
    {
      ItemAction = action;
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
        throw new ArgumentException("Action [" + action + "] is not valid for set collections");
      }
    }

    #endregion

    #region ICollectionItemDelta Members

    /// <summary>
    /// Gets the item action.
    /// </summary>
    /// <remarks></remarks>
    public ItemAction ItemAction { get; private set; }

    #endregion

    #region Properties

    /// <summary>
    ///
    /// </summary>
    /// <remarks></remarks>
    public TValue OldState { get; protected set; }

    /// <summary>
    ///
    /// </summary>
    /// <remarks></remarks>
    public TValue NewState { get; protected set; }

    /// <summary>
    /// 
    /// </summary>
    public ISnapshotDelta ItemDelta
    {
      get { return null; }
    }

    #endregion
  }
}