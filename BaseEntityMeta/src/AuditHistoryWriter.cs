// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
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
  public sealed class AuditHistoryWriter : IEntityDeltaWriter
  {
    #region Data
    
    /// <summary>
    /// The audit namespace
    /// </summary>
    public const string AuditNamespace = "http://WebMathTrainingsolutions.com/Audit";

    private bool _skipAuditInfo = true;
    private string _dtFormat = "%Y-%m-%d";
    private readonly ILoadableEntityContext _prevTidContext;
    private readonly ILoadableEntityContext _nextTidContext;
    private readonly XmlWriter _xmlWriter;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="prevContext"></param>
    /// <param name="nextContext"></param>
    public AuditHistoryWriter(XmlWriter writer, ILoadableEntityContext prevContext, ILoadableEntityContext nextContext)
    {
      _xmlWriter = writer;
      _prevTidContext = prevContext;
      _nextTidContext = nextContext;
    }

    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    public bool SkipAuditInfo
    {
      get { return _skipAuditInfo; }
      set { _skipAuditInfo = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    public string DtFormat
    {
      get { return _dtFormat; }
      set { _dtFormat = value; }
    }

    #endregion

    #region IEntityWriter Members

    /// <summary>
    ///
    /// </summary>
    public void Write(bool value)
    {
      _xmlWriter.WriteValue(value);
    }

    /// <summary>
    /// Visits integer value.
    /// It can be used only as an array element or value for object member.
    /// </summary>
    /// <param name="value"></param>
    public void Write(int value)
    {
      _xmlWriter.WriteValue(value);
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void Write(int? value)
    {
      if (value.HasValue)
        _xmlWriter.WriteValue(value.Value);
    }

    /// <summary>
    /// Visits integer value.
    /// It can be used only as an array element or value for object member.
    /// </summary>
    /// <param name="value"></param>
    public void Write(long value)
    {
      _xmlWriter.WriteValue(value);
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void Write(long? value)
    {
      if (value.HasValue)
        _xmlWriter.WriteValue(value.Value);
    }

    /// <summary>
    /// Visits decimal value.
    /// It can be used only as an array element or value for object member.
    /// </summary>
    /// <param name="value"></param>
    public void Write(double value)
    {
      _xmlWriter.WriteValue(value.ToString("G17", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void Write(double? value)
    {
      if (value.HasValue)
        _xmlWriter.WriteValue(value.Value.ToString("G17", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Visits DateTime value.
    /// It can be used only as an array element or value for object member.
    /// Before writing, the date is converted into universal time representation and written as string.
    /// </summary>
    /// <param name="snapshot"></param>
    public void Write(DateTime snapshot)
    {
      _xmlWriter.WriteValue(snapshot);
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void Write(DateTime? value)
    {
      if (value.HasValue)
        _xmlWriter.WriteValue(value.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteEnum<TValue>(TValue value) where TValue : struct
    {
      if (!typeof(TValue).IsEnum)
      {
        throw new ArgumentException("Type [" + typeof(TValue) + "] is not an enum type");
      }
      _xmlWriter.WriteValue(value.ToString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteNullableEnum<TValue>(TValue? value) where TValue : struct
    {
      if (value.HasValue)
        WriteEnum(value.Value);
    }

    /// <summary>
    /// Visits string value.
    /// It can be used only as an array element or value for object member.
    /// If 'null' value is passed it will emit the JSON 'null' for given member.
    /// </summary>
    /// <param name="snapshot"></param>
    public void Write(string snapshot)
    {
      //if string value is empty, the value is not wroten.
      if (!string.IsNullOrEmpty(snapshot))
        _xmlWriter.WriteString(snapshot);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(Guid value)
    {
      _xmlWriter.WriteValue(value.ToString());
    }

    /// <summary>
    /// </summary>
    /// <param name="values"></param>
    public void Write(double[] values)
    {
      if (values != null && values.Length != 0)
        _xmlWriter.WriteValue(String.Join(",", values.Select(d => d.ToString("G17", CultureInfo.InvariantCulture)).ToArray()));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="values"></param>
    public void Write(double[,] values)
    {
      if (values != null && values.GetLength(0) != 0 && values.GetLength(1) != 0)
      {
        var vector = new double[values.GetLength(0) * values.GetLength(1)];
        for (var i = 0; i < values.GetLength(0); i++)
        {
          Buffer.BlockCopy(values, i * values.GetLength(0) * sizeof(double), vector, i * values.GetLength(1) * sizeof(double),
            values.GetLength(1) * sizeof(double));
        }
        _xmlWriter.WriteValue(string.Join(",", vector.Select(d => d.ToString("G17", CultureInfo.InvariantCulture)).ToArray()));
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void Write(byte[] value)
    {
      if (value != null && value.Length != 0)
        _xmlWriter.WriteValue(Convert.ToBase64String(value));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Write(ObjectRef value)
    {
      if (value != null && value.Id != 0)
        WriteObjectId(value.Id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteObjectId(long value)
    {
      if (value == 0) return;
     
      // Load the entity so we can export its business key
      var entity = _prevTidContext == null ? null : _prevTidContext.Get(value);
      if (entity == null)
      {
        entity = _nextTidContext.Get(value);
        if (entity == null)
        {
          var classMeta = ClassCache.Find(value);
          throw new MetadataException(string.Format(
            "Attempt to reference invalid [{0}] with ObjectId [{1}]",
            classMeta.Name, value));
        }
      }

      WriteKey(entity);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="value"></param>
    public void WriteValue<TValue>(TValue value)
    {
      ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteDate(DateTime value)
    {
      if (!value.IsEmpty())
        Write(value.ToInt());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteDate(DateTime? value)
    {
      if (value != null)
        WriteDate(value.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="po"></param>
    public void WriteEntity(PersistentObject po)
    {
      if (po == null) return;

      var cm = ClassCache.Find(po);
      _xmlWriter.WriteStartElement("Entity", AuditNamespace);
      _xmlWriter.WriteAttributeString("type", cm.Name);
      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent))
      {
        if (_skipAuditInfo)
        {
          if (pm.Name == "ObjectId" || pm.Name == "ObjectVersion" || pm.Name == "LastUpdated" || pm.Name == "UpdatedBy")
            continue;
        }

        WriteMember(pm, pm.GetFieldValue(po));
      }

      _xmlWriter.WriteEndElement();
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
      if (obj == null) return;

      var cm = ClassCache.Find(obj);
      _xmlWriter.WriteStartElement("Entity", AuditNamespace);
      _xmlWriter.WriteAttributeString("type", cm.Name);
      foreach (var pm in cm.PropertyList.Where(pm => pm.Persistent))
      {
        if (_skipAuditInfo)
        {
          if (pm.Name == "ObjectId" || pm.Name == "ObjectVersion" || pm.Name == "LastUpdated" || pm.Name == "UpdatedBy")
            continue;
        }

        WriteMember(pm, pm.GetFieldValue(obj));
      }

      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void WriteSet<TValue>(ISet value)
    {
      if (value == null) return;

      foreach (TValue item in value)
      {
        _xmlWriter.WriteStartElement("Item", AuditNamespace);
        ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, item);
        _xmlWriter.WriteEndElement();
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="value"></param>
    public void WriteMap<TKey, TValue>(IDictionary<TKey,TValue> value)
    {
      if (value == null) return;

      foreach (var kvp in value)
      {
        _xmlWriter.WriteStartElement("Item", AuditNamespace);

        _xmlWriter.WriteStartElement("Key", AuditNamespace);
        ValueWriter<AuditHistoryWriter, TKey>.WriteValue(this, kvp.Key);
        _xmlWriter.WriteEndElement();

        _xmlWriter.WriteStartElement("Value", AuditNamespace);

        if (kvp.Value == null)
          _xmlWriter.WriteAttributeString("xsi", "nil", XmlSchema.InstanceNamespace, "true");
        else
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, kvp.Value);
        _xmlWriter.WriteEndElement();

        _xmlWriter.WriteEndElement();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void WriteList<TValue>(IList<TValue> value)
    {
      if (value == null) return;

      foreach (var item in value)
      {
        _xmlWriter.WriteStartElement("Item", AuditNamespace); //list can have null element

        if (item == null)
        {
          _xmlWriter.WriteAttributeString("xsi", "nil", XmlSchema.InstanceNamespace, "true");
        }
        else
        {
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, item);
        }

        _xmlWriter.WriteEndElement();
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="delta"></param>
    public void WriteDelta<TValue>(ScalarDelta<TValue> delta)
    {
      if (delta == null)
      {
        throw new ArgumentNullException(nameof(delta));
      }
      
      _xmlWriter.WriteStartElement("Old", AuditNamespace);
      if (delta.OldState == null)
        _xmlWriter.WriteAttributeString("xsi", "nil", XmlSchema.InstanceNamespace, "true");
      else
        WriteValue(delta.OldState);
      _xmlWriter.WriteEndElement();

      _xmlWriter.WriteStartElement("New", AuditNamespace);
      if (delta.NewState == null)
        _xmlWriter.WriteAttributeString("xsi", "nil", XmlSchema.InstanceNamespace, "true");
      else
        WriteValue(delta.NewState);
      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    public void WriteDelta(ObjectDelta delta)
    {
      if (delta == null)
      {
        throw new ArgumentNullException(nameof(delta));
      }

      _xmlWriter.WriteStartElement(delta.ItemAction.ToString(), AuditNamespace);

      var cm = delta.ClassMeta;
      
      if (delta.ItemAction == ItemAction.Added)
      {
        if (delta.NewState != null)
        {
          if (cm.IsEntity)
            WriteEntity((PersistentObject)delta.NewState);
          else
            WriteComponent(delta.NewState);
        }
        else
          WriteKey(delta.Key);
      }
      else if (delta.ItemAction == ItemAction.Removed)
      {
        if (cm.IsEntity)
          WriteEntity((PersistentObject)delta.OldState);
        else
          WriteComponent(delta.OldState);
      }
      else
      {
        WriteKey(delta.Key);
        foreach (var entry in delta.PropertyDeltas)
        {
          var pm = entry.Key;
          if (_skipAuditInfo)
          {
            if (pm.Name == "ObjectVersion" || pm.Name == "LastUpdated" || pm.Name == "UpdatedBy")
              continue;
          }
          var propDelta = entry.Value;
          _xmlWriter.WriteStartElement(propDelta.IsScalar ? "SimplePropertyChange" : "ComplexPropertyChange", AuditNamespace);
          _xmlWriter.WriteAttributeString("name", pm.Name);
          propDelta.Serialize(this);
          _xmlWriter.WriteEndElement();
        }
      }

      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// </summary>
    /// <param name="delta"></param>
    public void WriteDelta<TValue>(SetCollectionItemDelta<TValue> delta)
    {
      if (delta == null)
      {
        throw new ArgumentNullException(nameof(delta));
      }

      var action = delta.ItemAction;
      _xmlWriter.WriteStartElement(action.ToString(), AuditNamespace);
      WriteValue(action == ItemAction.Added ? delta.NewState : delta.OldState);
      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// </summary>
    /// <param name="delta"></param>
    public void WriteDelta<TKey, TValue>(MapCollectionItemDelta<TKey, TValue> delta)
    {
      if (delta == null)
      {
        throw new ArgumentNullException(nameof(delta));
      }

      _xmlWriter.WriteStartElement(delta.ItemAction.ToString(), AuditNamespace);

      _xmlWriter.WriteStartElement("Key", AuditNamespace);
      ValueWriter<AuditHistoryWriter, TKey>.WriteValue(this, delta.Key);
      _xmlWriter.WriteEndElement();

      switch (delta.ItemAction)
      {
        case ItemAction.Added:
          _xmlWriter.WriteStartElement("Value", AuditNamespace);
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, delta.NewState);
          _xmlWriter.WriteEndElement();
          break;

        case ItemAction.Removed:
          _xmlWriter.WriteStartElement("Value", AuditNamespace);
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, delta.OldState);
          _xmlWriter.WriteEndElement();
          break;

        case ItemAction.Changed:
          delta.Delta.Serialize(this);
          break;
      }

      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// </summary>
    /// <param name="delta"></param>
    public void WriteDelta<TValue>(ListCollectionItemDelta<TValue> delta)
    {
      if (delta == null)
      {
        throw new ArgumentNullException(nameof(delta));
      }

      _xmlWriter.WriteStartElement(delta.ItemAction.ToString(), AuditNamespace);
      _xmlWriter.WriteElementString("Idx", AuditNamespace, delta.Idx.ToString(CultureInfo.InvariantCulture));
      _xmlWriter.WriteStartElement("Value", AuditNamespace);
      if (delta.ItemAction == ItemAction.Added)
      {
        if (delta.NewState == null)
          WriteNull();
        else
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, delta.NewState);
      }
      else
      {
        if (delta.OldState == null)
          WriteNull();
        else
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, delta.OldState);
      }
      _xmlWriter.WriteEndElement();
      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// </summary>
    /// <param name="delta"></param>
    public void WriteDelta<TValue>(BagCollectionItemDelta<TValue> delta)
    {
      if (delta == null)
      {
        throw new ArgumentNullException(nameof(delta));
      }

      _xmlWriter.WriteStartElement(delta.ItemAction.ToString(), AuditNamespace);
      if (delta.ItemAction == ItemAction.Added)
      {
        if (delta.NewState == null)
          WriteNull();
        else
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, delta.NewState);
      }
      else
      {
        if (delta.OldState == null)
          WriteNull();
        else
          ValueWriter<AuditHistoryWriter, TValue>.WriteValue(this, delta.OldState);
      }
      _xmlWriter.WriteEndElement();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 
    /// </summary>
    private void WriteNull()
    {
      _xmlWriter.WriteAttributeString("xsi", "nil", XmlSchema.InstanceNamespace, "true");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pm"></param>
    /// <param name="value"></param>
    private void WriteMember(PropertyMeta pm, object value)
    {
      var cm = pm.Entity;
      if (cm.IsChildEntity)
      {
        var mopm = pm as ManyToOnePropertyMeta;
        if (mopm != null)
        {
          var inverseCascade = mopm.InverseCascade;
          // Do not write the reference to the owner (implied by the XML hierearchy)
          if (inverseCascade != null && inverseCascade.Cascade.Contains("all"))
            return;
        }
      }

      _xmlWriter.WriteStartElement("Property", AuditNamespace);
      _xmlWriter.WriteAttributeString("name", pm.Name);

      pm.Write(this, value);

      _xmlWriter.WriteEndElement();
    }

    /// <summary>
    /// Export the business key of the referenced object
    /// </summary>
    /// <param name="entity"></param>
    private void WriteKey(BaseEntityObject entity)
    {
      var cm = ClassCache.Find(entity);
      _xmlWriter.WriteStartElement("BusinessKey", AuditNamespace);
      _xmlWriter.WriteAttributeString("type", cm.Name);

      var keyPropList = cm.KeyPropertyList;
      if (keyPropList.Count == 0)
      {
        if (cm.IsChildEntity)
          keyPropList = cm.ChildKeyPropertyList;
      }

      if (keyPropList.Any(k => k is ObjectIdPropertyMeta))
      {
      }
      else
      {
        foreach (var keyProp in keyPropList)
        {
          WriteMember(keyProp, keyProp.GetFieldValue(entity));
        }
      }

      _xmlWriter.WriteEndElement();
    }

    private void WriteKey(ObjectKey key)
    {
      var entityKey = key as EntityKey;
      if (entityKey != null)
      {
        WriteObjectId(entityKey.Id);
      }
      else
      {
        var cm = key.ClassMeta;
        _xmlWriter.WriteStartElement("BusinessKey", AuditNamespace);
        _xmlWriter.WriteAttributeString("type", cm.Name);

        for (int i = 0; i < key.PropertyList.Count; ++i)
        {
          WriteMember(key.PropertyList[i], key.State[i]);
        }

        _xmlWriter.WriteEndElement();
      }
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
    }

    #endregion
  }
}