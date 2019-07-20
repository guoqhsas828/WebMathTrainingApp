// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class BagCollectionDelta<T> : CollectionDelta<T>
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BagCollectionDelta{T}"/> class.
    /// </summary>
    /// <param name="itemDeltas">The item deltas.</param>
    /// <remarks></remarks>
    public BagCollectionDelta(IList<BagCollectionItemDelta<T>> itemDeltas)
    {
      ItemDeltas = itemDeltas;
    }

    /// <summary>
    /// 
    /// </summary>
    private IList<BagCollectionItemDelta<T>> ItemDeltas { get; set; } 

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