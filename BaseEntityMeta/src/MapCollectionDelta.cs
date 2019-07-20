// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class MapCollectionDelta<TKey, TValue> : CollectionDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="MapCollectionDelta{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="itemDeltas">The item deltas.</param>
    /// <remarks></remarks>
    public MapCollectionDelta(IList<MapCollectionItemDelta<TKey, TValue>> itemDeltas)
    {
      ItemDeltas = itemDeltas;
    }

    /// <summary>
    /// Gets the item deltas.
    /// </summary>
    /// <remarks></remarks>
    public IList<MapCollectionItemDelta<TKey, TValue>> ItemDeltas { get; set; }

    /// <summary>
    /// serialize
    /// </summary>
    /// <param name="writer">writer</param>
    public override void Serialize(IEntityDeltaWriter writer)
    {
      foreach (var itemDelta in ItemDeltas)
      {
        writer.WriteDelta(itemDelta);
      }
    }
  }
}