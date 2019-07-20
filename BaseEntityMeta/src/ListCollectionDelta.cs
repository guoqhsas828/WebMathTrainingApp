// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class ListCollectionDelta<TValue> : CollectionDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ListCollectionDelta{TValue}"/> class.
    /// </summary>
    /// <param name="itemDeltas">The item deltas.</param>
    /// <remarks></remarks>
    public ListCollectionDelta(IList<ListCollectionItemDelta<TValue>> itemDeltas)
    {
      ItemDeltas = itemDeltas;
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<ListCollectionItemDelta<TValue>> ItemDeltas { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public override void Serialize(IEntityDeltaWriter writer)
    {
      foreach (var itemDelta in ItemDeltas)
      {
        writer.WriteDelta(itemDelta);
      }
    }
  }
}