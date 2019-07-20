// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class EntityWriterBase
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    public abstract void WriteEntity(PersistentObject entity);

    /// <summary>
    /// Write all "owned" non-child entities, ensuring that each entity written just once to context
    /// </summary>
    /// <param name="aggregateRoot"></param>
    public void WriteEntityGraph(PersistentObject aggregateRoot)
    {
      if (aggregateRoot == null)
      {
        throw new ArgumentNullException("aggregateRoot");
      }

      WriteEntityGraph(new[] { aggregateRoot });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entities"></param>
    public void WriteEntityGraph(IEnumerable<PersistentObject> entities)
    {
      if (entities == null)
      {
        throw new ArgumentNullException("entities");
      }

      var written = new HashSet<long>();

      foreach (var entity in entities)
      {
        var walker = new OwnedOrRelatedObjectWalker(true);

        walker.Walk(entity);

        foreach (var po in walker.OwnedObjects)
        {
          if (po.IsAnonymous)
          {
            throw new MetadataException("Attempt to write anonymous entity of type [" + po.GetType().Name + "]");
          }

          if (written.Contains(po.ObjectId)) continue;

          WriteEntity(po);

          written.Add(po.ObjectId);
        }
      }
    }
  }
}