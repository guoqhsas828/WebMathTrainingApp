// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// EntityContextEditorAdaptor
  /// </summary>
  public class EntityContextEditorAdaptor : IEntityContextAdaptor
  {
    #region Data

    private readonly IEditableEntityContext _context;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="context">context</param>
    public EntityContextEditorAdaptor(IEditableEntityContext context)
    {
      _context = context;
    }

    #endregion

    #region IEntityResolver Members

    /// <summary>
    /// Bool type. IsOpen.
    /// </summary>
    public bool IsOpen
    {
      get { return _context.IsOpen; }
    }
  
    #endregion
    /// <summary>
    /// Get function
    /// </summary>
    /// <param name="id">objet id</param>
    /// <returns></returns>
    public PersistentObject Get(long id, ClassMeta entityMeta = null)
    {
      var po = _context.Get(id);

      if (EntityHelper.IsTransient(id))
      {
        if (po == null)
          po = _context.CreateTransient(id);
      }
      else
      {
        if (po == null)
        {
          throw new KeyNotFoundException("Entity with id [" + id + "] not found");
        }
        var cm = entityMeta ?? ClassCache.Find(po);
        if (cm.IsRootEntity)
        {
          _context.RequestUpdate(po);
        }
        else if (!_context.IsLocked(po))
        {
          throw new MetadataException(string.Format("ChildEntity [{0}:{1}] is not locked", po.GetType().Name, po.ObjectId));
        }
      }

      return po;
    }

    /// <summary>
    /// Get Object reference
    /// </summary>
    /// <param name="id">object id</param>
    /// <returns></returns>
    public ObjectRef GetObjectRef(long id)
    {
      var objectRef = _context.GetObjectRef(id);

      if (objectRef == null)
      {
        if (EntityHelper.IsTransient(id))
        {
          var po = _context.CreateTransient(id);
          objectRef = ObjectRef.Create(po);
        }
        else
        {
          throw new KeyNotFoundException("Entity with id [" + id + "] not found");
        }
      }

      return objectRef;
    }
  }
}
