// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System.Collections.Generic;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class KeyedCollectionDelta<TValue> : CollectionDelta where TValue : BaseEntityObject
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyedCollectionDelta{TValue}"/> class.
    /// </summary>
    /// <param name="itemDeltas">The item deltas.</param>
    /// <remarks></remarks>
    public KeyedCollectionDelta(IList<ObjectDelta> itemDeltas)
    {
      ItemDeltas = itemDeltas;
    }

    /// <summary>
    /// 
    /// </summary>
    private IList<ObjectDelta> ItemDeltas { get; set; }
 
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