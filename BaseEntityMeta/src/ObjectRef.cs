// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Runtime.Serialization;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [DataContract]
  public class ObjectRef
  {
    /// <summary>
    /// Construct default instance
    /// </summary>
    public ObjectRef()
    {}

    /// <summary>
    /// Create an ObjectRef instance for the specified object
    /// </summary>
    /// <param name="obj"></param>
    internal ObjectRef(object obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException("obj");
      }
      if (obj is ObjectRef)
      {
        throw new ArgumentException("Not an entity [" + obj + "]");
      }
      _obj = (PersistentObject)obj;
    }

    /// <summary>
    /// Create an ObjectRef instance for the specified object id
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="context">Resolver</param>
    public ObjectRef(long id, IEntityContext context)
    {
      _id = id;
      _classFullName = ClassCache.Find(_id).FullName;
      _context = context;
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsNull => (_id == 0 && _obj == null);

    /// <summary>
    /// 
    /// </summary>
    public bool IsUninitialized => (_obj == null);

    /// <summary>
    /// 
    /// </summary>
    public IEntityContext Context
    {
      get { return _context; }

      set
      {
        if (value != _context)
        {
          if (_context != null && _context.IsOpen)
          {
            throw new MetadataException(
              "Cannot associate an ObjectRef with two open Resolvers");
          }
          _context = value;
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public long Id
    {
      get { return _obj?.ObjectId ?? _id; }
      set { _id = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    public string ClassFullName => _classFullName;

    /// <summary>
    /// </summary>
    public object Obj => _obj;

    /// <summary>
    /// Convenience method for optionally creating an ObjectRef
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static ObjectRef Create(PersistentObject value)
    {
      return value == null ? null : new ObjectRef(value);
    }

    /// <summary>
    /// Convenience method for resolving an ObjectRef
    /// </summary>
    /// <param name="objectRef"></param>
    /// <returns></returns>
    public static PersistentObject Resolve(ObjectRef objectRef)
    {
      return objectRef == null ? null : objectRef.Resolve();
    }

    /// <summary>
    ///
    /// </summary>
    internal PersistentObject Resolve()
    {
      if (_obj == null && _id != 0)
      {
        if (!_context.IsOpen)
        {
          throw new MetadataException("Cannot resolve id [" + _id + "] : context not open");
        }
        _obj = _context.Get(_id);
        if (_obj == null)
        {
          throw new MetadataException("Cannot resolve id [" + _id + "] : entity not found");
        }
        _classFullName = null;
        _id = 0;
      }

      return _obj;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return Id.GetHashCode();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      return Equals(obj as ObjectRef);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(ObjectRef other)
    {
      if (other == null) return false;
      return Id == other.Id;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectRefA"></param>
    /// <param name="objectRefB"></param>
    /// <returns></returns>
    public static bool IsSame(ObjectRef objectRefA, ObjectRef objectRefB)
    {
      if (objectRefA == null)
      {
        return objectRefB == null;
      }

      if (objectRefB == null)
      {
        return false;
      }

      return objectRefA.Id == objectRefB.Id;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return Id == 0 ? string.Empty : (EntityHelper.IsTransient(Id) ? "T" + Id : Id.ToString());
    }

    [DataMember] private long _id;
    [DataMember] private string _classFullName;
    [NonSerialized] private IEntityContext _context;
    private PersistentObject _obj;
  }
}