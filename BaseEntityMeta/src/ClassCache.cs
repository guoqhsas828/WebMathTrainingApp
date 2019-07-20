/*
 * ClassCache.cs -
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///  Repository for all entity metadata
  /// </summary>
  public static class ClassCache
  {
    #region Nested Classes

    #endregion

    /// <summary>
    /// Initialize a new instance of the entity or component corresponding to the specified <see cref="Type"/>
    /// </summary>
    /// <remarks>
    /// If there is no <see cref="ClassMeta"/> in the <see cref="ClassCache"/> for the specified type, throws a <see cref="MetadataException"/>
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <returns>An initialized instance of type {T}</returns>
    public static T CreateInstance<T>() where T : BaseEntityObject
    {
      var cm = Find(typeof(T));
      if (cm == null) throw new MetadataException("Invalid type [" + typeof(T).Name + "]");
      return (T)cm.CreateInstance();
    }

    /// <summary>
    /// Initialize a new instance of the entity or component corresponding to the specified <see cref="Type"/> and initializes the instance using the specified delegate.
    /// </summary>
    /// <remarks>
    /// If there is no <see cref="ClassMeta"/> in the <see cref="ClassCache"/> for the specified type, throws a <see cref="MetadataException"/>
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="initializer"></param>
    /// <returns>An initialized instance of type {T}</returns>
    public static T CreateInstance<T>(Action<T> initializer) where T : BaseEntityObject
    {
      if (initializer == null) throw new ArgumentNullException("initializer");
      var obj = CreateInstance<T>();
      initializer(obj);
      return obj;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<ClassMeta> FindAll()
    {
      return Cache.FindAll();
    }

    /// <summary>
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public static ClassMeta Find(int entityId)
    {
      return Cache.Find(entityId);
    }

    /// <summary>
    /// Find the <see cref="ClassMeta"/> either by its Name or its FullName
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static ClassMeta Find(string name)
    {
      return Cache.Find(name);
    }

    /// <summary>
    ///  Return entity for given name
    /// </summary>
    public static ClassMeta Find(Type type)
    {
      return Cache.Find(type);
    }

    /// <summary>
    /// Finds the ClassMeta for the specified object id.
    /// </summary>
    /// <param name="objectId">The object id.</param>
    /// <returns></returns>
    public static ClassMeta Find(long objectId)
    {
      return Cache.Find(objectId);
    }

    /// <summary>
    /// Return entity for given object
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static ClassMeta Find(object obj)
    {
      return Cache.Find(obj);
    }

    /// <summary>
    /// Generate Python code that can read the history for one or more entities into
    /// without access to the ClassMeta corresponding to that version of the entity.
    /// </summary>
    /// <returns></returns>
    public static string GenerateHistoryReaderCode(IList<string> names = null)
    {
      return Cache.GenerateHistoryReaderCode(names);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static string PrintMetaModel()
    {
      return Cache.PrintMetaModel();
    }

    /// <summary>
    /// For internal use only
    /// </summary>
    public static void Clear()
    {
      _lazyCache = new Lazy<InternalClassCache>(() => new InternalClassCache());
    }

    /// <summary>
    /// Used by test fixtures to restrict which entities are initialized
    /// </summary>
    public static ISet<Type> TypeFilter { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public static IEnumerable<Assembly> Assemblies
    {
      get { return Cache.AssemblyCache; }
    }

    private static InternalClassCache Cache
    {
      get { return _lazyCache.Value; }
    }

    private static Lazy<InternalClassCache> _lazyCache = new Lazy<InternalClassCache>(() => new InternalClassCache());
  }
}


