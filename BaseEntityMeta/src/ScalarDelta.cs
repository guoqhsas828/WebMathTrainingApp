// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Describes difference in a scalar property
  /// </summary>
  /// <remarks></remarks>
  public class ScalarDelta<T> : ISnapshotDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ScalarDelta{T}"/> class.
    /// </summary>
    /// <param name="oldState">The old state.</param>
    /// <param name="newState">The new state.</param>
    /// <remarks></remarks>
    public ScalarDelta(T oldState, T newState)
    {
      OldState = oldState;
      NewState = newState;
    }

    /// <summary>
    /// Gets the old state.
    /// </summary>
    /// <remarks></remarks>
    public T OldState { get; private set; }

    /// <summary>
    /// Gets the new state.
    /// </summary>
    /// <remarks></remarks>
    public T NewState { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool IsSame(ISnapshotDelta other)
    {
      ScalarDelta<T> otherDelta = other as ScalarDelta<T>;
      if (otherDelta == null)
      {
        return false;
      }
      if (OldState == null)
      {
        if (otherDelta.OldState != null)
          return false;
      }
      else if (!ValueComparer<T>.IsSame(OldState, otherDelta.OldState))
      {
        return false;
      }
      if (NewState == null)
      {
        if (otherDelta.NewState != null)
          return false;
      }
      else if (!ValueComparer<T>.IsSame(NewState, otherDelta.NewState))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public object Apply(object obj)
    {
      return (object)NewState ?? default(T);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public void Serialize(IEntityDeltaWriter writer)
    {
      writer.WriteDelta(this);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsScalar
    {
      get { return true; }
    }

    /// <summary>
    /// Retrieves a string that indicates the current object
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks></remarks>
    public override string ToString()
    {
      return String.Format("ScalarDelta [{0}] [{1}]", OldState, NewState);
    }
  }
}