// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class CloningEntityWriter : XmlEntityWriter
  {
    // Map of source ObjectId to clone ObjectId
    private readonly IDictionary<long, long> _idMap = new Dictionary<long, long>();

    private static readonly Type[] SpecialTypes = {typeof(VersionedObject), typeof(AuditedObject)};

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sb"></param>
    /// <param name="ids"></param>
    public CloningEntityWriter(IEditableEntityContext context, StringBuilder sb, IEnumerable<long> ids)
      : base(sb)
    {
      foreach (var id in ids)
      {
        var type = EntityHelper.GetClassFromObjectId(id);
        long dstObjectId = context.GenerateTransientId(type);
        _idMap[id] = dstObjectId;
      }
    }

    /// <summary>
    /// Write
    /// </summary>
    /// <param name="value">object reference</param>
    public override void Write(ObjectRef value)
    {
      var id = value.Id;
      if (id == 0)
      {
        throw new InvalidOperationException("Cannot create ObjectDelta for transient (unsaved) entity");
      }
      long mappedObjectId;
      base.WriteObjectId(_idMap.TryGetValue(value.Id, out mappedObjectId) ? mappedObjectId : value.Id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public override void WriteObjectId(long value)
    {
      long mappedObjectId;
      if (!_idMap.TryGetValue(value, out mappedObjectId))
      {
        throw new ArgumentException("ObjectId [" + value + "] not found");
      }

      base.WriteObjectId(mappedObjectId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    public override void WriteEntity(PersistentObject entity)
    {
      var cm = ClassCache.Find(entity);
      WriteEntity(entity, cm.PropertyList.Where(pm => pm.Persistent && !SpecialTypes.Contains(pm.PropertyInfo.DeclaringType)));
    }
  }
}