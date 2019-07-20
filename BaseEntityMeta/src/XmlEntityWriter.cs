// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
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
  public class XmlEntityWriter : EntityWriterBase, IEntityWriter
  {
    #region Data

    private XmlWriter _writer;

    /// <summary>
    /// 
    /// </summary>
    public static readonly XmlWriterSettings DefaultSettings =
      new XmlWriterSettings
      {
        CloseOutput = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        NewLineHandling = NewLineHandling.Entitize,
        OmitXmlDeclaration = true
      };

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sb"></param>
    public XmlEntityWriter(StringBuilder sb)
      : this(XmlWriter.Create(sb, DefaultSettings))
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    public XmlEntityWriter(XmlWriter writer)
    {
      if (writer == null)
      {
        throw new ArgumentNullException("writer");
      }

      _writer = writer;
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
      _writer.WriteValue(value ? 1 : 0);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(int value)
    {
      _writer.WriteValue(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(int? value)
    {
      if (value.HasValue)
        _writer.WriteValue(value.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(long value)
    {
      _writer.WriteValue(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(long? value)
    {
      if (value.HasValue)
        _writer.WriteValue(value.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double value)
    {
      _writer.WriteValue(value.ToString("G17", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double? value)
    {
      if (value.HasValue)
        Write(value.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(DateTime value)
    {
      _writer.WriteValue(value.TrimMilliSeconds());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(DateTime? value)
    {
      if (value.HasValue)
        _writer.WriteValue(value?.TrimMilliSeconds());
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
      Write(Convert.ToInt32(value));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    public void WriteNullableEnum<T>(T? value) where T : struct
    {
      if (value == null)
        _writer.WriteAttributeString(XmlSnapshotUtil.NilAttributeName, XmlSnapshotUtil.NilAttributeValue);
      else
        WriteEnum(value.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(string value)
    {
      if (!string.IsNullOrEmpty(value))
        _writer.WriteString(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(Guid value)
    {
      _writer.WriteValue(value.ToString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(double[] value)
    {
      if (value == null || value.Length == 0)
      {
        return;
      }

      var sb = new StringBuilder();
      for (int i = 0; i < value.Length; i++)
      {
        if (i > 0)
          sb.Append(',');
        sb.Append(value[i].ToString("G17", CultureInfo.InvariantCulture));
      }
      _writer.WriteValue(sb.ToString());
    }

    /// <summary>
    /// Write function
    /// </summary>
    /// <param name="value"></param>
    public void Write(double[,] value)
    {
      if (value == null || value.GetLength(0) == 0 || value.GetLength(1) == 0)
      {
        return;
      }
      var sb = new  StringBuilder();
      for (int i = 0; i < value.GetLength(0); i++)
      {
        if (i > 0) sb.Append(';');
        for (int j = 0; j < value.GetLength(1); j++)
        {
          if (j > 0) sb.Append(',');
          sb.Append(value[i, j].ToString("G17", CultureInfo.InvariantCulture));
        }
      }
      _writer.WriteValue(sb.ToString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(byte[] value)
    {
      if (value == null || value.Length == 0)
      {
        return;
      }

      _writer.WriteValue(Convert.ToBase64String(value));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public virtual void Write(ObjectRef value)
    {
      var id = value.Id;
      if (id == 0)
      {
        throw new InvalidOperationException("Cannot create ObjectDelta for transient (unsaved) entity");
      }
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
    /// <param name="value"></param>
    public virtual void WriteObjectId(long value)
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
    public virtual void WriteDate(DateTime value)
    {
      Write(value.ToInt());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public virtual void WriteDate(DateTime? value)
    {
      if (value != null)
        WriteDate(value.Value);
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

      _writer.WriteStartElement(cm.Name);

      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && (pm.IsPrimaryKey || !pm.HasDefaultValue(entity))))
      {
        _writer.WriteStartElement(pm.Name);
        
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

        _writer.WriteEndElement();
      }

      _writer.WriteEndElement();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propertyMetas"></param>
    public void WriteEntity(PersistentObject entity, IEnumerable<PropertyMeta> propertyMetas)
    {
      var cm = ClassCache.Find(entity.GetType());

      var filteredPropertyMetas = propertyMetas.Where(pm => pm.Persistent && (pm.IsPrimaryKey || !pm.HasDefaultValue(entity))).ToList();
      if (filteredPropertyMetas.Any())
      {
        _writer.WriteStartElement(cm.Name);

        foreach (var pm in filteredPropertyMetas)
        {
          _writer.WriteStartElement(pm.Name);

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

          _writer.WriteEndElement();
        }

        _writer.WriteEndElement();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteComponent(BaseEntityObject value)
    {
      if (value == null)
      {
        throw new ArgumentNullException("value");
      }
      var cm = ClassCache.Find(value.GetType());
      if (cm.IsEntity)
      {
        throw new ArgumentException("Type [" + cm.Name + "] is an entity");
      }

      _writer.WriteStartElement(cm.Name);
      
      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent && !pm.HasDefaultValue(value)))
      {
        _writer.WriteStartElement(pm.Name);
        pm.Write(value, this);
        _writer.WriteEndElement();
      }

      _writer.WriteEndElement();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="set"></param>
    public void WriteSet<TValue>(ISet set)
    {
      if (set == null || set.Count == 0) return;

      var valueWriter = ValueWriter<XmlEntityWriter, TValue>.Instance;

      foreach (TValue item in set)
      {
        _writer.WriteStartElement("Item"); //list can have null element

        if (item == null)
        {
          _writer.WriteAttributeString(XmlSnapshotUtil.NilAttributeName, XmlSnapshotUtil.NilAttributeValue);
        }
        else
        {
          valueWriter(this, item);
        }

        _writer.WriteEndElement();
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
      if (map == null || !map.Any()) return;

      var keyWriter = ValueWriter<XmlEntityWriter, TKey>.Instance;
      var valueWriter = ValueWriter<XmlEntityWriter, TValue>.Instance;

      foreach (var kvp in map)
      {
        _writer.WriteStartElement("Item");

        _writer.WriteStartElement("Key"); //map can NOT have null key
        keyWriter(this, kvp.Key);
        _writer.WriteEndElement();

        _writer.WriteStartElement("Value"); //map can have null value
        if (kvp.Value == null)
          _writer.WriteAttributeString(XmlSnapshotUtil.NilAttributeName, XmlSnapshotUtil.NilAttributeValue);
        else
          valueWriter(this, kvp.Value);
        _writer.WriteEndElement();

        _writer.WriteEndElement();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="list"></param>
    public void WriteList<TValue>(IList<TValue> list)
    {
      if (list == null || !list.Any()) return;

      var valueWriter = ValueWriter<XmlEntityWriter, TValue>.Instance;

      foreach (var item in list)
      {
        _writer.WriteStartElement("Item"); //list can have null element

        if (item == null)
        {
          _writer.WriteAttributeString(XmlSnapshotUtil.NilAttributeName, XmlSnapshotUtil.NilAttributeValue);
        }
        else
        {
          valueWriter(this, item);
        }

        _writer.WriteEndElement();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    public void WriteValue<TValue>(TValue value)
    {
      ValueWriter<XmlEntityWriter, TValue>.WriteValue(this, value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    public void WriteAuditLog(AuditLog auditLog)
    {
      var action = auditLog.Action;
      _writer.WriteStartElement(action.ToString());

      _writer.WriteStartElement("Tid");
      Write(auditLog.Tid);
      _writer.WriteEndElement();

      _writer.WriteStartElement("ObjectId");
      Write(auditLog.ObjectId);
      _writer.WriteEndElement();

      _writer.WriteStartElement("RootObjectId");
      Write(auditLog.RootObjectId);
      _writer.WriteEndElement();

      _writer.WriteStartElement("ParentObjectId");
      Write(auditLog.ParentObjectId);
      _writer.WriteEndElement();

      _writer.WriteStartElement("EntityId");
      Write(auditLog.EntityId);
      _writer.WriteEndElement();

      _writer.WriteStartElement("ValidFrom");
      WriteDate(auditLog.ValidFrom);
      _writer.WriteEndElement();

      switch (auditLog.Action)
      {
        case ItemAction.Added:
        case ItemAction.Changed:
          _writer.WriteStartElement("ObjectDelta");
          Write(auditLog.ObjectDelta);
          _writer.WriteEndElement();
          break;
      }

      _writer.WriteEndElement();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="auditLog"></param>
    /// <returns></returns>
    public void PrintAuditLog(AuditLog auditLog)
    {
      WriteElement(auditLog.Action.ToString(), () =>
      {
        WriteElement("Tid", () => Write(auditLog.Tid));
        WriteElement("ObjectId", () => Write(auditLog.ObjectId));
        WriteElement("RootObjectId", () => Write(auditLog.RootObjectId));
        WriteElement("ParentObjectId", () => Write(auditLog.ParentObjectId));
        WriteElement("EntityId", () => Write(auditLog.EntityId));
        WriteElement("ValidFrom", () => WriteDate(auditLog.ValidFrom));
        if (auditLog.ObjectDelta != null)
        {
          WriteElement("ObjectDelta", () =>
          {
            using (var context = new SnapshotEntityContext())
            using (var reader = new BinaryEntityReader(new MemoryStream(auditLog.ObjectDelta), new EntityContextLoaderAdaptor(context)))
              WriteEntity(reader.ReadEntity());
          });
        }
      });
    }

    /// <summary>
    /// Write element with contents provided via the specified action
    /// </summary>
    /// <param name="elementName"></param>
    /// <param name="action"></param>
    public void WriteElement(string elementName, Action action)
    {
      _writer.WriteStartElement(elementName);

      action();
      
      _writer.WriteEndElement();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 
    /// </summary>
    public void WriteNull()
    {
      _writer.WriteAttributeString(XmlSnapshotUtil.NilAttributeName, XmlSnapshotUtil.NilAttributeValue);
    }

    /// <summary>
    /// Serialize the reference to the object that is being added or changed.
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="key"></param>
    /// <remarks>
    /// The outer element must identify the (derived) type of the object that
    /// is being referenced.  ChildKeys will include this when serializing the
    /// key, however the ObjectRef does not, so we need to add it here.  We
    /// also handle the case where there is no key (can occur for component
    /// properties).
    /// </remarks>
    public void WriteKey(ClassMeta cm, ObjectKey key)
    {
      if (cm.IsEntity)
      {
        _writer.WriteStartElement(cm.Name);
        var id = ((EntityKey)key).Id;
        WriteObjectId(id);
        _writer.WriteEndElement();
      }
      else
      {
        var propList = cm.ChildKeyPropertyList;
        _writer.WriteStartElement(cm.Name);
        for (int i = 0; i < propList.Count; i++)
        {
          var prop = propList[i];
          _writer.WriteStartElement(prop.Name);
          prop.Write(this, key.State[i]);
          _writer.WriteEndElement();
        }
        _writer.WriteEndElement();
      }
    }

    /// <summary>
    /// Write start element
    /// </summary>
    /// <param name="s"></param>
    public void WriteStartElement(string s)
    {
      _writer.WriteStartElement(s);
    }

    /// <summary>
    /// Write end element
    /// </summary>
    public void WriteEndElement()
    {
      _writer.WriteEndElement();
    }

    /// <summary>
    /// Write attribute string
    /// </summary>
    /// <param name="localName"></param>
    /// <param name="value"></param>
    public void WriteAttributeString(string localName, string value)
    {
      _writer.WriteAttributeString(localName, value);
    }
    #endregion
  }
}