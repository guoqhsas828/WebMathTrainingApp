/*
 * CascadeWalker.cs -
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class CascadeWalker
  {
    private readonly bool _includeSelf;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="includeSelf"></param>
    protected CascadeWalker(bool includeSelf)
    {
      _includeSelf = includeSelf;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootObj"></param>
    /// <returns></returns>
    public void Walk(PersistentObject rootObj)
    {
      if (rootObj == null)
      {
        throw new ArgumentNullException("rootObj");
      }
      
      if (_includeSelf)
      {
        Action(rootObj);
      }
      
      Walk(ClassCache.Find(rootObj), rootObj);
    }

    private void Walk(ClassMeta cm, PersistentObject parentObj)
    {
      foreach (var childObj in cm.CascadeList.Where(c => Filter(c, parentObj)).SelectMany(c => c.ReferencedObjects(parentObj)).Where(Action))
      {
        Walk(ClassCache.Find(childObj), childObj);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cascade"></param>
    /// <param name="parentObj"></param>
    /// <returns></returns>
    public abstract bool Filter(ICascade cascade, PersistentObject parentObj);

    /// <summary>
    /// 
    /// </summary>
    public abstract bool Action(PersistentObject po);
  }

  /// <summary>
  /// Returns a list of all "owned" or "related" objects (cascade != "none")
  /// </summary>
  public class OwnedOrRelatedObjectWalker : CascadeWalker
  {
    /// <summary>
    /// 
    /// </summary>
    public OwnedOrRelatedObjectWalker(bool includeSelf = false)
      : base(includeSelf)
    {
      OwnedObjects = new List<PersistentObject>();
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<PersistentObject> OwnedObjects { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cascade"></param>
    /// <param name="parentObj"></param>
    /// <returns></returns>
    public override bool Filter(ICascade cascade, PersistentObject parentObj)
    {
      return cascade.Cascade != "none";
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool Action(PersistentObject po)
    {
      OwnedObjects.Add(po);
      return true;
    }
  }

  /// <summary>
  /// Returns list of all "owned" object instances that are not child objects
  /// </summary>
  public class OwnedOrRelatedRootObjectWalker : OwnedOrRelatedObjectWalker
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="includeSelf"></param>
    public OwnedOrRelatedRootObjectWalker(bool includeSelf = false)
      : base(includeSelf)
    { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cascade"></param>
    /// <param name="parentObj"></param>
    /// <returns></returns>
    public override bool Filter(ICascade cascade, PersistentObject parentObj)
    {
      return base.Filter(cascade, parentObj) && !cascade.ReferencedEntity.IsChildEntity;
    }
  }

  /// <summary>
  /// Returns a list of all "owned" objects (cascade == "all" or cascade == "all-delete-orphan")
  /// </summary>
  public class OwnedObjectWalker : CascadeWalker
  {
    /// <summary>
    /// 
    /// </summary>
    public OwnedObjectWalker(bool includeSelf = false)
      : base(includeSelf)
    {
      OwnedObjects = new List<PersistentObject>();
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<PersistentObject> OwnedObjects { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cascade"></param>
    /// <param name="parentObj"></param>
    /// <returns></returns>
    public override bool Filter(ICascade cascade, PersistentObject parentObj)
    {
      switch (cascade.Cascade)
      {
        case "all":
        case "all-delete-orphan":
          return true;
        default:
          return false;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool Action(PersistentObject po)
    {
      OwnedObjects.Add(po);
      return true;
    }
  }
}