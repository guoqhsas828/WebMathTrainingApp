// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class EntityReaderBase
  {
    /// <summary>
    /// 
    /// </summary>
    public IEntityContextAdaptor Adaptor { get; protected set; }

    /// <summary>
    /// 
    /// </summary>
    public abstract bool EOF { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract PersistentObject ReadEntity();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    protected PersistentObject CreateInstance(long id)
    {
      var cm = ClassCache.Find(id);
      var po = (PersistentObject)cm.CreateInstance();
      po.ObjectId = id;
      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IList<PersistentObject> ReadEntityList()
    {
      var list = new List<PersistentObject>();

      while (!EOF)
      {
        var po = ReadEntity();
        if (po == null)
        {
          break;
        }
        list.Add(po);
      }

      return list;
    }
  }
}