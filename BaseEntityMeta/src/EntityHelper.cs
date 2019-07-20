// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public static class EntityHelper
  {
    /// <summary>
    /// Serialize 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string Serialize(PersistentObject obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException("obj");
      }

      var cm = ClassCache.Find(obj);
      if (cm == null)
      {
        throw new MetadataException("Invalid entity [" + obj.GetType().Name + "] : no ClassMeta");
      }

      var sb = new StringBuilder();
      using (var writer = new XmlEntityWriter(sb))
      {
        writer.WriteEntityGraph(obj);
      }

      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"/>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static T Deserialize<T>(string xml) where T : PersistentObject
    {
      var entity = Deserialize(xml) as T;
      if (entity == null)
      {
        throw new MetadataException("Invalid xml : entity not of type [" + typeof(T) + "]");
      }
      return entity;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static PersistentObject Deserialize(string xml)
    {
      var list = new List<PersistentObject>();
      using (var reader = new XmlEntityReader(xml))
      {
        while (!reader.EOF)
        {
          var po = reader.ReadEntity();
          if (po != null)
            list.Add(po);
        }
      }
      if (list.Count == 0)
      {
        throw new MetadataException("No entities read!");
      }
      return list[0];
    }

    /// <summary>
    /// Bitmask used to determine if an ObjectId is transient
    /// </summary>
    public const ulong TransientBitMask = ((ulong)Int64.MaxValue) + 1;

    /// <summary>
    /// Extract the EntityId from an ObjectId
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    public static short GetEntityIdFromObjectId(long objectId)
    {
      var cleanId = StripTransientBit(objectId);

      var entityId = (short)(cleanId >> 48);
      if (entityId <= 0)
      {
        throw new MetadataException("Invalid entity id [" + entityId + "]");
      }
      
      return entityId;
    }

    /// <summary>
    /// Get the .NET Type for the specified ObjectId
    /// </summary>
    /// <returns></returns>
    public static Type GetClassFromObjectId(long objectId)
    {
      var entityId = GetEntityIdFromObjectId(objectId);

      var classMeta = ClassCache.Find(entityId);
      if (classMeta == null)
      {
        throw new MetadataException($"Cannot determine Class from ObjectId [{objectId}]. No ClassMeta found for entity id [{entityId}]");
      }

      return classMeta.Type;
    }

    /// <summary>
    /// Returns true if the specified <see cref="PersistentObject"/> is anonymous (has a zero ObjectId).
    /// </summary>
    /// <param name="po"></param>
    public static bool IsAnonymous(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException("po");
      }

      return po.ObjectId == 0;
    }

    /// <summary>
    /// Returns true if the specified objectId represents an anonymous entity (i.e. is zero).
    /// </summary>
    /// <param name="objectId"></param>
    public static bool IsSaved(long objectId)
    {
      return !IsAnonymous(objectId) && !IsTransient(objectId);
    }

    /// <summary>
    /// Returns true if the specified objectId represents an anonymous entity (i.e. is zero).
    /// </summary>
    /// <param name="objectId"></param>
    public static bool IsAnonymous(long objectId)
    {
      return objectId == 0;
    }

    /// <summary>
    /// Returns true if the specified <see cref="PersistentObject"/> is transient (has a transient ObjectId).
    /// </summary>
    /// <param name="po">The <see cref="PersistentObject"/></param>
    public static bool IsTransient(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException("po");
      }

      return IsTransient(po.ObjectId);
    }

    /// <summary>
    /// Returns true if objectId is transient.
    /// </summary>
    /// <param name="objectId"></param>
    public static bool IsTransient(long objectId)
    {
      return ((ulong)objectId & TransientBitMask) != 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    public static long StripTransientBit(long objectId)
    {
      return (long)((ulong)objectId & Int64.MaxValue);
    }
  }
}