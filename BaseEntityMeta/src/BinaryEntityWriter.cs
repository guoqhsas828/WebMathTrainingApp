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
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class BinaryEntityWriter : EntityWriterBase, IEntityWriter
  {
    #region Data

    private BinaryWriter _writer;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    public BinaryEntityWriter(Stream stream)
    {
      _writer = new BinaryWriter(stream);
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
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
      if (_writer != null)
      {
        _writer.Flush();
        _writer.Close();
        _writer = null;
      }
    }

    #endregion

    #region IEntityWriter Members

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(bool value)
    {
      _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(int value)
    {
      Write7BitEncodedInt(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(int? value)
    {
      if (value.HasValue)
      {
        _writer.Write(true);
        Write(value.Value);
      }
      else
      {
        _writer.Write(false);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(long value)
    {
      _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(long? value)
    {
      if (value.HasValue)
      {
        _writer.Write(true);
        _writer.Write(value.Value);
      }
      else
      {
        _writer.Write(false);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double value)
    {
      _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double? value)
    {
      if (value.HasValue)
      {
        _writer.Write(true);
        _writer.Write(value.Value);
      }
      else
      {
        _writer.Write(false);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(DateTime value)
    {
      _writer.Write(value.ToUniversalTime().TrimMilliSeconds().Ticks);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(DateTime? value)
    {
      if (value.HasValue)
      {
        _writer.Write(true);
        Write(value.Value);
      }
      else
      {
        _writer.Write(false);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteEnum<T>(T value) where T : struct
    {
      if (!typeof(T).IsEnum)
      {
        throw new ArgumentException("Type [" + typeof(T) + "] is not an enum type");
      }
      Write(Convert.ToInt32(value));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteNullableEnum<T>(T? value) where T : struct
    {
      if (!typeof(T).IsEnum)
      {
        throw new ArgumentException("Type [" + typeof(T) + "] is not an enum type");
      }
      if (value.HasValue)
      {
        Write(true);
        Write(Convert.ToInt32(value.Value));
      }
      else
      {
        Write(false);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(string value)
    {
      _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(Guid value)
    {
      var guid = value;
      var bytes = guid.ToByteArray();
      Write(bytes.Length);
      _writer.Write(bytes);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double[] value)
    {
      Write(value.Length);
      foreach (double d in value)
        _writer.Write(d);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double[,] value)
    {
      Write(value.GetLength(0));
      Write(value.GetLength(1));
      for (var i = 0; i < value.GetLength(0); i++)
      {
        for (var j = 0; j < value.GetLength(1); j++)
        {
          Write(value[i,j]);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(byte[] value)
    {
      Write(value.Length);
      if (value.Length > 0)
        _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(ObjectRef value)
    {
      if (value == null || value.IsNull)
      {
        _writer.Write(0L);
      }
      else
      {
        var id = value.Id;
        if (id == 0)
        {
          throw new InvalidOperationException("Cannot create ObjectDelta for transient (unsaved) entity");
        }
        _writer.Write(id);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteObjectId(long value)
    {
      Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public virtual void WriteDate(DateTime value)
    {
      Write7BitEncodedInt(value.Year * 10000 + value.Month * 100 + value.Day);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public virtual void WriteDate(DateTime? value)
    {
      if (value.HasValue)
      {
        _writer.Write(true);
        WriteDate(value.Value);
      }
      else
      {
        _writer.Write(false);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    public override void WriteEntity(PersistentObject entity)
    {
      if (entity == null)
      {
        throw new ArgumentNullException("entity");
      }
      var cm = ClassCache.Find(entity.GetType());
      if (cm.IsComponent)
      {
        throw new ArgumentException("Type [" + cm.Name + "] is not an entity");
      }
      
      Write(cm.EntityId);

      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && (pm.IsPrimaryKey || !pm.HasDefaultValue(entity))))
      {
        Write(pm.Index);
      
        if (pm.IsPrimaryKey)
        {
          if (entity.ObjectId == 0)
          {
            throw new InvalidOperationException("Cannot write transient (unsaved) entity");
          }
          WriteObjectId(entity.ObjectId);
        }
        else
        {
          pm.Write(entity, this);
        }
      }
      
      Write(-1);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propertyMetas"></param>
    public void WriteEntity(PersistentObject entity, IEnumerable<PropertyMeta> propertyMetas)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    public void WriteComponent(BaseEntityObject obj)
    {
      if (obj == null)
      {
        throw new ArgumentNullException("obj");
      }
      var cm = ClassCache.Find(obj.GetType());
      if (cm.IsEntity)
      {
        throw new ArgumentException("Type [" + cm.Name + "] is an entity");
      }

      Write(cm.Name);

      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && (pm.IsPrimaryKey || !pm.HasDefaultValue(obj))))
      {
        Write(pm.Index);
        pm.Write(obj, this);
      }

      Write(-1);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteSet<TValue>(ISet value)
    {
      Write(value.Count);
      foreach (TValue item in value)
      {
        ValueWriter<BinaryEntityWriter, TValue>.WriteValue(this, item);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteMap<TKey, TValue>(IDictionary<TKey, TValue> value)
    {
      Write(value.Count);
      foreach (var entry in value)
      {
        ValueWriter<BinaryEntityWriter, TKey>.WriteValue(this, entry.Key);

        if (entry.Value == null)
        {
          _writer.Write(false);
        }
        else
        {
          _writer.Write(true);
          ValueWriter<BinaryEntityWriter, TValue>.WriteValue(this, entry.Value);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteList<TValue>(IList<TValue> value)
    {
      Write(value.Count);
      foreach (var item in value)
      {
        if (item == null)
        {
          _writer.Write(false);
        }
        else
        {
          _writer.Write(true);
          ValueWriter<BinaryEntityWriter, TValue>.WriteValue(this, item);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    public void WriteValue<TValue>(TValue value)
    {
      ValueWriter<BinaryEntityWriter, TValue>.Instance(this, value);
    }

    #endregion

    #region Helper Methods

    private void Write7BitEncodedInt(int value)
    {
      // Write out an int 7 bits at a time. The high bit of the byte,
      // when on, tells reader to continue reading more bytes.
      var v = (uint)value; // support negative numbers
      while (v >= 0x80)
      {
        _writer.Write((byte)(v | 0x80));
        v >>= 7;
      }
      _writer.Write((byte)v);
    }

    #endregion
  }
}