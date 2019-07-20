// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class EntityContextLoaderAdaptor : IEntityContextAdaptor
  {
    #region Data

    private readonly ILoadableEntityContext _context;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    public EntityContextLoaderAdaptor(ILoadableEntityContext context)
    {
      _context = context;
    }

    #endregion

    #region IEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    public bool IsOpen => _context.IsOpen;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public PersistentObject Get(long id, ClassMeta entityMeta = null)
    {
      var po = _context.Get(id);
      if (po != null)
      {
        return po;
      }

      var cm = entityMeta ?? ClassCache.Find(id);
      po = (PersistentObject)cm.CreateInstance();
      po.ObjectId = id;
      _context.Load(po);

      return _context.Get(id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public ObjectRef GetObjectRef(long id)
    {
      var objectRef = _context.GetObjectRef(id);
      if (objectRef != null)
      {
        return objectRef;
      }
      
      var cm = ClassCache.Find(id);
      var po = (PersistentObject)cm.CreateInstance();
      po.ObjectId = id;
      _context.Load(po);

      return _context.GetObjectRef(id);
    }

    #endregion
  }
}