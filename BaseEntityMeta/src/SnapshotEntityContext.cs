// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// An <see cref="IEntityContext"/> that can be used only to capture the state of one or more entities.
  /// Does not support lazy initialization.
  /// </summary>
  public sealed class SnapshotEntityContext : ILoadableEntityContext, ITransientEntityContext
  {
    #region Data

    private bool _isOpen = true;
    private readonly IDictionary<long, PersistentObject> _entityMap = new Dictionary<long, PersistentObject>();
    private readonly ObjectIdGenerator _idGenerator = new ObjectIdGenerator();

    #endregion

    #region IDisposable Members

    private bool _isDisposed;

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
      _isDisposed = true;
      _isOpen = false;
    }

    #endregion

    #region IEntityContext Members

    /// <summary>
    /// Boolean type. Is open or not
    /// </summary>
    public bool IsOpen
    {
      get { return _isOpen; }
    }

    /// <summary>
    /// Boolean type. is disposed or not.
    /// </summary>
    /// <returns></returns>
    public bool IsDisposed()
    {
      return _isDisposed;
    }

    /// <summary>
    /// Get function
    /// </summary>
    /// <param name="id">id</param>
    /// <returns>Persistent object</returns>
    public PersistentObject Get(long id)
    {
      PersistentObject po;
      return _entityMap.TryGetValue(id, out po) ? po : null;
    }

    /// <summary>
    /// Get object reference
    /// </summary>
    /// <param name="id">id</param>
    /// <returns>Object reference</returns>
    public ObjectRef GetObjectRef(long id)
    {
      if (id == 0) return null;

      if (EntityHelper.IsTransient(id))
      {
        PersistentObject po;
        return _entityMap.TryGetValue(id, out po) ? ObjectRef.Create(po) : null;
      }

      return new ObjectRef(id, this);
    }

    /// <summary>
    /// Remove this instance from the context.
    /// </summary>
    /// <remarks>
    /// This operation cascades to associated instances if the association is mapped with <c>cascade="all"</c> or <c>cascade="all-delete-orphan"</c>.
    /// </remarks>
    public void Evict(PersistentObject po)
    {
      var walker = new OwnedObjectWalker(true);

      walker.Walk(po);

      foreach (var obj in walker.OwnedObjects)
      {
        _entityMap.Remove(obj.ObjectId);
      }
    }

    #endregion

    #region ILoadableEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public void Load(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException("po");
      }

      RegisterEntity(po);
    }

    #endregion

    #region ITransientEntityContext Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public long GenerateTransientId(Type type)
    {
      return _idGenerator.Generate(type);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    /// <returns></returns>
    public long RegisterTransient(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException("po");
      }

      if (!po.IsAnonymous)
      {
        throw new ArgumentException("ObjectId [" + po.ObjectId + "] not anonymous");
      }

      var cm = ClassCache.Find(po);
      var id = _idGenerator.Generate(cm.Type);

      RegisterTransient(po, id);

      var walker = new OwnedOrRelatedObjectWalker();

      walker.Walk(po);

      foreach (var oo in walker.OwnedObjects.Where(oo => oo.IsAnonymous))
      {
        RegisterTransient(oo);
      }

      return po.ObjectId;
    }

    /// <summary>
    /// Register transient
    /// </summary>
    /// <param name="po">persistent object</param>
    /// <param name="id">object id</param>
    public void RegisterTransient(PersistentObject po, long id)
    {
      if (po == null)
      {
        throw new ArgumentNullException("po");
      }

      if (!po.IsAnonymous)
      {
        throw new ArgumentException(string.Format("Invalid entity [{0}:{1}] : not Anonymous", po.GetType().Name, po.ObjectId));
      }

      if (!EntityHelper.IsTransient(id))
      {
        throw new ArgumentException("Invalid id [" + id + "] : not Transient");
      }

      if (_entityMap.ContainsKey(id))
      {
        throw new ArgumentException("An entity with id [" + id + "] is already associated with this context");
      }

      po.ObjectId = id;

      _entityMap.Add(po.ObjectId, po);
    }

    /// <summary>
    /// Register transients
    /// </summary>
    public void RegisterTransients()
    {
      foreach (var po in _entityMap.Values.ToList())
      {
        var walker = new OwnedOrRelatedObjectWalker();

        walker.Walk(po);

        foreach (var oo in walker.OwnedObjects.Where(oo => oo.IsAnonymous))
        {
          RegisterTransient(oo);
        }
      }
    }

    #endregion

    #region IEmumerable<PersistentObject> Members

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<PersistentObject> GetEnumerator()
    {
      return _entityMap.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    #endregion

    #region Other Methods

    /// <summary>
    /// 
    /// </summary>
    public void Clear()
    {
      _entityMap.Clear();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    private void RegisterEntity(PersistentObject po)
    {
      _entityMap.Add(po.ObjectId, po);
    }

    #endregion
  }
}