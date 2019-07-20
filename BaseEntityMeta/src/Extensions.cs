// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Extension methods for WebMathTraining.Metadata namespace
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool Contains(this IEntityContext context, long id)
    {
      return context.Get(id) != null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="context"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static T Get<T>(this IEntityContext context, long id) where T : PersistentObject
    {
      return (T)context.Get(id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static PersistentObject CreateTransient(this ITransientEntityContext context, long id)
    {
      if (!EntityHelper.IsTransient(id))
      {
        throw new ArgumentException("id [" + id + "] is not transient");
      }
      var cm = ClassCache.Find(EntityHelper.GetClassFromObjectId(id));
      var po = (PersistentObject)cm.CreateInstance();
      context.RegisterTransient(po, id);
      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="context"></param>
    /// <param name="initializer"></param>
    /// <returns></returns>
    public static T CreateTransient<T>(this ITransientEntityContext context, Action<T> initializer = null) where T : PersistentObject
    {
      var po = ClassCache.CreateInstance(initializer);
      context.RegisterTransient(po);
      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="writer"></param>
    public static void Serialize(this IEntityContext context, IEntityWriter writer)
    {
      foreach (var entity in context)
      {
        writer.WriteEntity(entity);
      }
    }
  }
}