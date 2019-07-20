// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Used to generate transient ObjectId values
  /// </summary>
  public class ObjectIdGenerator
  {
    #region Data

    private const ulong NumLo = 256;

    private const ulong TransientBitMask = ((ulong)long.MaxValue) + 1;

    private readonly IDictionary<Type, EntityGenerator> _generators = new Dictionary<Type, EntityGenerator>();

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="setTransientBit"></param>
    public ObjectIdGenerator(bool setTransientBit = true)
    {
      SetTransientBit = setTransientBit;
    }

    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    public bool SetTransientBit { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    public long Generate(Type type)
    {
      return GetEntry(type).Generate();
    }

    private EntityGenerator GetEntry(Type type)
    {
      EntityGenerator generator;
      if (!_generators.TryGetValue(type, out generator))
      {
        generator = new EntityGenerator(type, SetTransientBit);
        _generators[type] = generator;
      }
      return generator;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public void GenerateTransientIds(PersistentObject po)
    {
      if (po == null)
      {
        throw new ArgumentNullException("po");
      }

      GenerateTransientId(po);

      var walker = new OwnedOrRelatedObjectWalker();
      
      walker.Walk(po);
      
      foreach (var ownedObject in walker.OwnedObjects)
      {
        GenerateTransientId(ownedObject);
      }
    }

    private void GenerateTransientId(PersistentObject po)
    {
      if (po.ObjectId == 0)
      {
        po.ObjectId = Generate(po.GetType());
      }
    }

    #endregion

    #region Nested Types

    private class EntityGenerator
    {
      public EntityGenerator(Type type, bool setTransientBit)
      {
        var entity = ClassCache.Find(type);
        if (entity == null)
        {
          throw new ArgumentException("Invalid type [" + type + "]");
        }

        ulong entityId = (ulong)entity.EntityId;
        if (entityId == 0)
        {
          throw new ArgumentException("Invalid entity [" + type + "]");
        }

        _entityPart = entityId << 48;

        _loPart = NumLo;

        _setTransientBit = setTransientBit;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <returns></returns>
      public long Generate()
      {
        if (_loPart == NumLo)
        {
          ulong hiVal = GetHi();
          _hiPart = hiVal;
          _loPart = 0;
        }

        ulong objectId = _entityPart | _hiPart | _loPart;

        if (_setTransientBit)
        {
          objectId |= TransientBitMask;
        }

        _loPart++;

        return (long)objectId;
      }

      /// <summary>
      /// Get next hi value
      /// </summary>
      private ulong GetHi()
      {
        ulong result = _nextHi;
        _nextHi += NumLo;
        return result;
      }

      private readonly bool _setTransientBit;
      private readonly ulong _entityPart;
      private ulong _nextHi;
      private ulong _hiPart;
      private ulong _loPart;
    }

    #endregion
  }
}