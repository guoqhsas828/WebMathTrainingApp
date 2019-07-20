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
  public class SetCollectionDelta<TValue> : CollectionDelta
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="SetCollectionDelta{TValue}"/> class.
    /// </summary>
    /// <param name="itemDeltas">The item deltas.</param>
    /// <remarks></remarks>
    public SetCollectionDelta(IList<SetCollectionItemDelta<TValue>> itemDeltas)
    {
      ItemDeltas = itemDeltas;
    }

    /// <summary>
    /// Gets the item deltas.
    /// </summary>
    /// <remarks></remarks>
    public IList<SetCollectionItemDelta<TValue>> ItemDeltas { get; set; }

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