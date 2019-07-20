// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
  public class JsonEntityWriter : EntityWriterBase, IEntityWriter
  {
    #region Data

    private TextWriter _writer;
    private readonly Stack<ValueContext> _contextStack = new Stack<ValueContext>();
    private readonly bool _exportMode;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="exportMode"></param>
    public JsonEntityWriter(StringBuilder sb, bool exportMode = false)
      : this(new StringWriter(sb), exportMode)
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="exportMode"></param>
    public JsonEntityWriter(TextWriter writer, bool exportMode = false)
    {
      _writer = writer;
      _contextStack.Push(new ValueContext());
      _exportMode = exportMode;
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
        _writer.Dispose();
        _writer = null;
      }
    }

    #endregion

    #region IEntityWriter Members

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">if set to <c>true</c> [value].</param>
    public void Write(bool value)
    {
      if (_exportMode)
        WriteString(value ? "true" : "false");
      else
        Write(value ? 1 : 0);
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Write(char value)
    {
      _writer.Write(char.ToString(value));
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Write(int value)
    {
      _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(int? value)
    {
      if (value.HasValue)
      {
        Write(value.Value);
      }
      else
      {
        WriteValueNull();
      }
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
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
        Write(value.Value);
      }
      else
      {
        WriteValueNull();
      }
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Write(double value)
    {
      WriteString(value.ToString("G17", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double? value)
    {
      if (value.HasValue)
      {
        Write(value.Value);
      }
      else
      {
        WriteValueNull();
      }
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Write(DateTime value)
    {
      WriteValue(value.ToString("o"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(DateTime? value)
    {
      if (value.HasValue)
      {
        Write(value.Value);
      }
      else
      {
        WriteValueNull();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    public void WriteEnum<T>(T value) where T : struct
    {
      if (!typeof(T).IsEnum)
      {
        throw new ArgumentException("Type [" + typeof(T) + "] is not an enum type");
      }

      if (_exportMode)
        Write(value.ToString());
      else
        Write(Convert.ToInt32(value));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    public void WriteNullableEnum<T>(T? value) where T : struct
    {
      if (value.HasValue)
      {
        WriteEnum(value.Value);
      }
      else
      {
        WriteValueNull();
      }
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Write(string value)
    {
      WriteString("\"" + GetSafeString(value) + "\"");
    }

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="value">The value.</param>
    public void Write(Guid value)
    {
      WriteString("\"" + value + "\"");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double[] value)
    {
      WriteList(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double[,] value)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(byte[] value)
    {
      if (value == null)
      {
        WriteValueNull();
      }
      else
      {
        Write(Convert.ToBase64String(value));
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteObjectId(long value)
    {
      if (EntityHelper.IsTransient(value))
      {
        Write("T" + EntityHelper.StripTransientBit(value));
      }
      else
      {
        Write(value);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(ObjectRef value)
    {
      var id = value.Obj == null ? value.Id : ((PersistentObject)value.Obj).ObjectId;

      if (EntityHelper.IsTransient(id))
      {
        Write("T" + EntityHelper.StripTransientBit(id));
      }
      else
      {
        Write(id);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    public void WriteValue<TValue>(TValue value)
    {
      ValueWriter<JsonEntityWriter, TValue>.WriteValue(this, value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public virtual void WriteDate(DateTime value)
    {
      Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public virtual void WriteDate(DateTime? value)
    {
      Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
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

      WriteObjectBegin(cm);

      if (_exportMode)
      {
        foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && !pm.IsPrimaryKey && (pm.IsKey || !pm.HasDefaultValue(entity))))
        {
          WriteProperty(pm, entity);
        }
      }
      else
      {
        foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && (pm.IsPrimaryKey || !pm.HasDefaultValue(entity))))
        {
          WriteProperty(pm, entity);
        }
      }

      WriteObjectEnd();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propertyMetas"></param>
    public void WriteEntity(PersistentObject entity, IEnumerable<PropertyMeta> propertyMetas)
    {
      if (_exportMode)
      {
        throw new InvalidOperationException("This method is not supported when exportMode == true");
      }

      var cm = ClassCache.Find(entity.GetType());

      var filteredPropertyMetas = propertyMetas.Where(pm => pm.Persistent && (pm.IsPrimaryKey || !pm.HasDefaultValue(entity))).ToList();
      if (filteredPropertyMetas.Any())
      {
        WriteObjectBegin(cm);

        foreach (var pm in filteredPropertyMetas)
        {
          WriteMember(pm.Name);

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

        WriteObjectEnd();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteComponent(BaseEntityObject value)
    {
      var cm = ClassCache.Find(value);

      WriteObjectBegin(cm);

      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && !pm.HasDefaultValue(value)))
      {
        WriteMember(pm.Name);

        pm.Write(value, this);
      }

      WriteObjectEnd();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    public void WriteSet<TValue>(ISet value)
    {
      if (value == null || value.Count == 0)
      {
        WriteValueNull();
      }
      else
      {
        var valueWriter = ValueWriter<JsonEntityWriter, TValue>.Instance;

        WriteArrayBegin();
        foreach (TValue item in value)
        {
          valueWriter(this, item);
        }
        WriteArrayEnd();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="map"></param>
    public void WriteMap<TKey, TValue>(IDictionary<TKey, TValue> map)
    {
      if (map == null)
      {
        WriteValueNull();
      }
      else
      {
        var keyWriter = ValueWriter<JsonEntityWriter, TKey>.Instance;
        var valueWriter = ValueWriter<JsonEntityWriter, TValue>.Instance;

        WriteArrayBegin();
        foreach (var kvp in map)
        {
          WriteArrayBegin();
          keyWriter(this, kvp.Key);
          valueWriter(this, kvp.Value);
          WriteArrayEnd();
        }
        WriteArrayEnd();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="list"></param>
    public void WriteList<TValue>(IList<TValue> list)
    {
      if (list == null || list.Count == 0)
      {
        WriteValueNull();
      }
      else
      {
        var valueWriter = ValueWriter<JsonEntityWriter, TValue>.Instance;

        WriteArrayBegin();
        for (int i = 0; i < list.Count; ++i)
        {
          var item = list[i];
          if (i > 0) _writer.Write(",");
          valueWriter(this, item);
        }
        WriteArrayEnd();
      }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Writes the object begin.
    /// </summary>
    /// <param name="cm">The cm.</param>
    public void WriteObjectBegin(ClassMeta cm)
    {
      _writer.Write("{");
      _contextStack.Push(new ValueContext(ContextType.Object));
      WriteMember("@class");
      WriteValue(cm.Name);
    }

    /// <summary>
    /// Writes the object end.
    /// </summary>
    public void WriteObjectEnd()
    {
      _writer.Write("}");
      _contextStack.Pop();
    }

    /// <summary>
    /// Writes the member.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <exception cref="System.InvalidOperationException">oops</exception>
    public void WriteMember(string name)
    {
      var context = _contextStack.Peek();
      if (context.Type != ContextType.Object)
      {
        throw new InvalidOperationException("oops");
      }
      if (context.Count != 0)
      {
        _writer.Write(",");
      }
      _writer.Write("\"" + name + "\":");
      context.Count = context.Count + 1;
    }

    /// <summary>
    /// Writes the array begin.
    /// </summary>
    public void WriteArrayBegin()
    {
      var context = _contextStack.Peek();
      if (context.Type == ContextType.Array)
      {
        if (context.Count != 0)
        {
          _writer.Write(",");
        }
      }

      _writer.Write("[");

      _contextStack.Push(new ValueContext(ContextType.Array));

      if (context.Type == ContextType.Array)
      {
        context.Count += 1;
      }
    }

    /// <summary>
    /// Writes the array end.
    /// </summary>
    public void WriteArrayEnd()
    {
      _writer.Write("]");
      _contextStack.Pop();
    }

    /// <summary>
    /// Writes the value null.
    /// </summary>
    public void WriteValueNull()
    {
      WriteString("null");
    }

    /// <summary>
    /// Writes the string.
    /// </summary>
    /// <param name="value">The value.</param>
    public void WriteString(string value)
    {
      _writer.Write(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pm"></param>
    /// <param name="obj"></param>
    private void WriteProperty(PropertyMeta pm, BaseEntityObject obj)
    {
      WriteMember(pm.Name);

      if (_exportMode)
      {
        var cascade = pm as ICascade;
        if (cascade == null)
        {
          pm.Write(obj, this);
        }
        else
        {
          switch (cascade.Cardinality)
          {
            case Cardinality.OneToMany:
            case Cardinality.ManyToMany:
              WriteArrayBegin();
              break;
          }
          var list = cascade.ReferencedObjects(obj).ToList();
          for (int i = 0; i < list.Count; ++i)
          {
            if (i > 0)
            {
              _writer.Write(",");
            }
            var ro = list[i];
            if (cascade.Cascade == "none")
              WriteKey(ro);
            else
              WriteEntity(ro);
          }
          switch (cascade.Cardinality)
          {
            case Cardinality.OneToMany:
            case Cardinality.ManyToMany:
              WriteArrayEnd();
              break;
          }
        }

      }
      else
      {
        if (pm.IsPrimaryKey)
        {
          var entity = (PersistentObject)obj;

          if (entity.ObjectId == 0)
          {
            throw new InvalidOperationException("Cannot write anonymous entity");
          }

          WriteObjectId(entity.ObjectId);
        }
        else
        {
          pm.Write(obj, this);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    private void WriteKey(PersistentObject entity)
    {
      var cm = ClassCache.Find(entity);
      if (cm == null)
      {
        throw new MetadataException("No ClassMeta found for entity [" + entity.GetType().Name + "]");
      }
      if (!cm.IsEntity)
      {
        throw new MetadataException("ClassMeta [" + cm.Name + "] is not an entity");
      }
      WriteObjectBegin(cm);
      if (cm.HasKey)
      {
        foreach (var pm in cm.KeyPropertyList)
          WriteProperty(pm, entity);
      }
      else if (cm.HasChildKey)
      {
        foreach (var pm in cm.ChildKeyPropertyList)
          WriteProperty(pm, entity);
      }
      else
      {
        throw new InvalidOperationException("Entity [" + cm.Name + "] cannot be exported");
      }
      WriteObjectEnd();
    }

    /// <summary>
    /// Gets the safe string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public string GetSafeString(string value)
    {
      if (value == null)
      {
        return null;
      }

      var sb = new StringBuilder();

      foreach (char c in value)
      {
        switch (c)
        {
          case '\\':
            sb.Append("\\\\");
            break;
          case '"':
            sb.Append("\\\"");
            break;
          case '\b':
            sb.Append("\\b");
            break;
          case '\t':
            sb.Append("\\t");
            break;
          case '\r':
            sb.Append("\\r");
            break;
          case '\n':
            sb.Append("\\n");
            break;
          case '\f':
            sb.Append("\\f");
            break;
          default:
            sb.Append(c);
            break;
        }
      }

      return sb.ToString();
    }

    #endregion

    #region Nested Types

    private enum ContextType
    {
      Root,
      Object,
      Array
    }

    private class ValueContext
    {
      public ValueContext()
        : this(ContextType.Root)
      {}

      public ValueContext(ContextType type)
      {
        Type = type;
      }

      public ContextType Type { get; private set; }
      public int Count { get; set; }
    }

    #endregion
  }
}