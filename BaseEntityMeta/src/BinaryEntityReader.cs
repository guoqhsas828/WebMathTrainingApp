// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iesi.Collections;
using BaseEntity.Shared;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  /// <remarks></remarks>
  public class BinaryEntityReader : EntityReaderBase, IEntityReader
  {
    #region Data

    private BinaryReader _reader;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="adaptor"></param>
    public BinaryEntityReader(Stream stream, IEntityContextAdaptor adaptor)
    {
      _reader = new BinaryReader(stream);

      Adaptor = adaptor;
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
      if (_reader != null)
      {
        _reader.Close();
        _reader = null;
      }
    }

    #endregion

    #region IEntityReader Members

    /// <summary>
    /// Reads the boolean.
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
    public bool ReadBoolean()
    {
      return _reader.ReadBoolean();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int ReadInt32()
    {
      return Read7BitEncodedInt();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int? ReadNullableInt32()
    {
      bool hasValue = _reader.ReadBoolean();

      int? value;
      if (hasValue)
        value = ReadInt32();
      else
        value = null;

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long ReadInt64()
    {
      return _reader.ReadInt64();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long? ReadNullableInt64()
    {
      bool hasValue = _reader.ReadBoolean();

      long? value;
      if (hasValue)
        value = _reader.ReadInt64();
      else
        value = null;

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double ReadDouble()
    {
      return _reader.ReadDouble();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double? ReadNullableDouble()
    {
      bool hasValue = _reader.ReadBoolean();

      double? value;
      if (hasValue)
        value = _reader.ReadDouble();
      else
        value = null;

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime ReadDateTime()
    {
      return new DateTime(_reader.ReadInt64(), DateTimeKind.Utc).TrimMilliSeconds();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime? ReadNullableDateTime()
    {
      var hasValue = _reader.ReadBoolean();
      if (hasValue)
      {
        return ReadDateTime();
      }
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue ReadEnum<TValue>() where TValue : struct
    {
      return (TValue)Enum.ToObject(typeof(TValue), ReadInt32());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue? ReadNullableEnum<TValue>() where TValue : struct
    {
      var hasValue = _reader.ReadBoolean();
      if (hasValue)
      {
        return ReadEnum<TValue>();
      }
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string ReadString()
    {
      return _reader.ReadString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Guid ReadGuid()
    {
      var size = ReadInt32();
      var bytes = _reader.ReadBytes(size);
      return new Guid(bytes);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double[] ReadArrayOfDoubles()
    {
      int size = ReadInt32();
      var values = new double[size];
      if (size != 0)
      {
        for (int i = 0; i < size; ++i)
          values[i] = _reader.ReadDouble();
      }
      return values;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double[,] ReadArrayOfDoubles2D()
    {
      var values = new double[ReadInt32(), ReadInt32()];
      for (var i = 0; i < values.GetLength(0); i++)
      {
        for (var j = 0; j < values.GetLength(1); j++)
        {
          values[i, j] = _reader.ReadDouble();
        }
      }
      return values;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public byte[] ReadBinaryBlob()
    {
      var size = ReadInt32();
      return size == 0 ? new byte[0] : _reader.ReadBytes(size);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long ReadObjectId()
    {
      return _reader.ReadInt64();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ObjectRef ReadObjectRef()
    {
      return Adaptor.GetObjectRef(ReadObjectId());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T ReadValue<T>()
    {
      return ValueReader<BinaryEntityReader, T>.Instance(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime ReadDate()
    {
      var value = Read7BitEncodedInt();
      return new DateTime(value / 10000, (value % 10000) / 100, value % 100);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime? ReadNullableDate()
    {
      var hasValue = _reader.ReadBoolean();
      if (hasValue)
      {
        return ReadDate();
      }
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override PersistentObject ReadEntity()
    {
      var entityId = ReadInt32();
      var cm = ClassCache.Find(entityId);
      if (cm == null)
      {
        throw new MetadataException("No entity type found with entity id [" + entityId + "]");
      }
      if (!cm.IsEntity)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not an entity");
      }

      PersistentObject entity = null;

      int maxPropIndex = cm.PropertyList.Count - 1;

      var propIndexes = new HashSet<int>();

      while (true)
      {
        int propIndex = ReadInt32();
        if (propIndex == -1 || propIndex > maxPropIndex)
        {
          break;
        }
        propIndexes.Add(propIndex);
        var pm = cm.PropertyList[propIndex];
        if (entity == null)
        {
          var id = ReadObjectId();
          entity = Adaptor.Get(id, cm);
          pm.SetFieldValue(entity, id);
        }
        else
        {
          pm.Read(entity, this);
        }
      }

      // Any property that was not found is set to its default value
      foreach (var pm in cm.PersistentPropertyMap.Values.Where(pm => !propIndexes.Contains(pm.Index) && !pm.HasDefaultValue(entity)))
      {
        pm.SetDefaultValue(entity);
      }

      return entity;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    public void ReadEntity(PersistentObject entity)
    {
      if (entity == null)
      {
        throw new ArgumentNullException("entity");
      }

      var entityId = ReadInt32();
      var cm = ClassCache.Find(entityId);
      if (cm == null)
      {
        throw new MetadataException("No entity type found with EntityId [" + entityId + "]");
      }
      if (!cm.IsEntity)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not an entity");
      }

      var propIndexes = new HashSet<int>();

      while (true)
      {
        int propIndex = ReadInt32();
        if (propIndex == -1)
        {
          break;
        }
        propIndexes.Add(propIndex);
        var pm = cm.PropertyList[propIndex];
        pm.Read(entity, this);
      }

      // Any property that was not found is set to its default value
      foreach (var pm in cm.ExtendedPropertyMap.Values.Where(pm => !propIndexes.Contains(pm.Index) && !pm.HasDefaultValue(entity)))
      {
        pm.SetDefaultValue(entity);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public BaseEntityObject ReadComponent()
    {
      var className = _reader.ReadString();
      var cm = ClassCache.Find(className);
      if (cm == null)
      {
        throw new MetadataException("No component type found with name [" + className + "]");
      }
      if (!cm.IsComponent)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not a component");
      }

      var obj = (BaseEntityObject)cm.CreateInstance();

      var propIndexes = new HashSet<int>();

      while (true)
      {
        int propIndex = ReadInt32();
        if (propIndex == -1)
        {
          break;
        }
        propIndexes.Add(propIndex);
        var pm = cm.PropertyList[propIndex];
        pm.Read(obj, this);
      }

      // Any property that was not found is set to its default value
      foreach (var pm in cm.PersistentPropertyMap.Values.Where(pm => !propIndexes.Contains(pm.Index)))
      {
        pm.SetDefaultValue(obj);
      }

      return obj;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public ISet ReadSetCollection<TValue>()
    {
      var contents = new HashedSet();
      int numItems = ReadInt32();
      for (int i = 0; i < numItems; i++)
      {
        contents.Add(ValueReader<BinaryEntityReader, TValue>.ReadValue(this));
      }
      return contents;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public IDictionary<TKey, TValue> ReadMapCollection<TKey, TValue>()
    {
      var keyReader = ValueReader<BinaryEntityReader, TKey>.Instance;
      var valueReader = ValueReader<BinaryEntityReader, TValue>.Instance;
      int numItems = ReadInt32();
      var contents = new Dictionary<TKey, TValue>();
      for (int i = 0; i < numItems; i++)
      {
        var key = keyReader(this);
        var value = _reader.ReadBoolean() ? valueReader(this) : default(TValue);
        contents.Add(key, value);
      }
      return contents;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public IList<TValue> ReadListCollection<TValue>()
    {
      int numItems = ReadInt32();
      var contents = new List<TValue>(numItems);
      for (int i = 0; i < numItems; i++)
      {
        contents.Add(_reader.ReadBoolean() ? ValueReader<BinaryEntityReader, TValue>.ReadValue(this) : default(TValue));
      }
      return contents;
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool EOF
    {
      get
      {
        var stream = _reader.BaseStream;
        return stream.Position == stream.Length;
      }
    }

    #endregion

    #region Helper Methods

    private int Read7BitEncodedInt()
    {
      // Read out an int 7 bits at a time. The high bit
      // of the byte when on means to continue reading more bytes.
      int count = 0;
      int shift = 0;
      byte b;
      do
      {
        b = _reader.ReadByte();
        count |= (b & 0x7F) << shift;
        shift += 7;
      } while ((b & 0x80) != 0);
      return count;
    }

    #endregion
  }
}