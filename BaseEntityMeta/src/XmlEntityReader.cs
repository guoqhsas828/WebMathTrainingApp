// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Iesi.Collections;
using BaseEntity.Core.Logging;
using BaseEntity.Shared;
using log4net;

#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class XmlEntityReader : EntityReaderBase, IEntityReader
  {
    #region Data

    private static readonly ILog Log = QLogManager.GetLogger(typeof(XmlEntityReader));

    /// <summary>
    /// The default settings
    /// </summary>
    public static readonly XmlReaderSettings DefaultSettings =
      new XmlReaderSettings
      {
        CloseInput = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true
      };

    private XmlReader _reader;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    public XmlEntityReader(string input, IEditableEntityContext context = null)
      : this(XmlReader.Create(new StringReader(input), DefaultSettings), context)
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="context"></param>
    public XmlEntityReader(XmlReader reader, IEditableEntityContext context = null)
    {
      _reader = reader;

      if (_reader.ReadState == ReadState.Initial)
      {
        _reader.Read();
      }

      if (context == null) 
        context = (IEditableEntityContext)EntityContext.Current;

      Adaptor = new EntityContextEditorAdaptor(context);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <param name="adaptor"></param>
    public XmlEntityReader(string input, IEntityContextAdaptor adaptor)
      : this(XmlReader.Create(new StringReader(input), DefaultSettings), adaptor)
    { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="reader">xml reader</param>
    /// <param name="adaptor">entity context adapter</param>
    public XmlEntityReader(XmlReader reader, IEntityContextAdaptor adaptor)
    {
      _reader = reader;

      if (_reader.ReadState == ReadState.Initial)
      {
        _reader.Read();
      }

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
        _reader.Dispose();
        _reader = null;
      }
    }

    #endregion

    #region IEntityReader Members

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool ReadBoolean()
    {
      return _reader.ReadContentAsInt() == 1;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int ReadInt32()
    {
      return _reader.ReadContentAsInt();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int? ReadNullableInt32()
    {
      return IsEmptyValue() ? null : (int?)_reader.ReadContentAsInt();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long ReadInt64()
    {
      return _reader.ReadContentAsLong();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long? ReadNullableInt64()
    {
      return IsEmptyValue() ? null : (long?)_reader.ReadContentAsLong();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double ReadDouble()
    {
      return _reader.ReadContentAsDouble();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double? ReadNullableDouble()
    {
      return IsEmptyValue() ? null : (double?)_reader.ReadContentAsDouble();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime ReadDateTime()
    {
      return _reader.ReadContentAsDateTime().TrimMilliSeconds();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime? ReadNullableDateTime()
    {
      return IsEmptyValue() ? null : (DateTime?)_reader.ReadContentAsDateTime().TrimMilliSeconds();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue ReadEnum<TValue>() where TValue : struct
    {
      return (TValue)Enum.ToObject(typeof(TValue), _reader.ReadContentAsInt());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue? ReadNullableEnum<TValue>() where TValue : struct
    {
      return IsEmptyValue() ? null : (TValue?)ReadEnum<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Guid ReadGuid()
    {
      return Guid.Parse(_reader.ReadContentAsString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string ReadString()
    {
      return IsEmptyValue() ? string.Empty : _reader.ReadContentAsString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double[] ReadArrayOfDoubles()
    {
      if (IsEmptyValue())
      {
        return null;
      }
      string strValue = _reader.ReadContentAsString();
      string[] tokens = strValue.Split(',');
      if (tokens.Length == 0)
      {
        return null;
      }
      var values = new double[tokens.Length];
      for (int i = 0; i < values.Length; i++)
      {
        values[i] = Convert.ToDouble(tokens[i], CultureInfo.InvariantCulture);
      }
      return values;
    }


    /// <summary>
    /// read 2D double array
    /// </summary>
    /// <returns></returns>
    public double[,] ReadArrayOfDoubles2D()
    {
      if (IsEmptyValue())
      {
        return null;
      }
      var strValue = _reader.ReadContentAsString();
      var rowArray = strValue.Split(';');
      if (rowArray.Length == 0 || rowArray[0].Split(',').Length == 0)
      {
        return null;
      }
      var values = new double[rowArray.Length, rowArray[0].Split(',').Length];
      for (var i = 0; i < rowArray.Length; i++)
      {
        var tokens = rowArray[i].Split(',');
        for (var j = 0; j < tokens.Length; j++)
        {
          values[i, j] = Convert.ToDouble(tokens[j], CultureInfo.InvariantCulture);
        }
      }
      return values;
    }

    /// <summary>
    /// read binary blob.
    /// </summary>
    /// <returns></returns>
    public byte[] ReadBinaryBlob()
    {
      return IsEmptyValue() ? null : Convert.FromBase64String(_reader.ReadContentAsString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long ReadObjectId()
    {
      var strValue = _reader.ReadContentAsString();

      return strValue.StartsWith("T")
        ? (long)(ulong.Parse(strValue.Substring(1)) | EntityHelper.TransientBitMask)
        : long.Parse(strValue);
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
      return ValueReader<XmlEntityReader, T>.Instance(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime ReadDate()
    {
      var iValue = ReadInt32();
      return new DateTime(iValue / 10000, (iValue % 10000) / 100, iValue % 100);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime? ReadNullableDate()
    {
      var value = ReadNullableInt32();
      if (value == null)
        return null;

      var iValue = value.Value;
      return new DateTime(iValue / 10000, (iValue % 10000) / 100, iValue % 100);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override PersistentObject ReadEntity()
    {
      var cm = ClassCache.Find(_reader.Name);
      if (cm == null)
      {
        throw new MetadataException("No entity type found with name [" + _reader.Name + "]");
      }
      if (!cm.IsEntity)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not an entity");
      }

      var propertyMap = cm.PersistentPropertyMap;

      // Defer creation of entity in case already created via OneToMany or ManyToMany
      PersistentObject entity = null;

      // If class element is empty, there are no property nodes, and no end element.
      // This could occur if all the properties have their default values.

      bool isEmpty = _reader.IsEmptyElement;

      var propIndexes = new HashSet<int>();

      _reader.ReadStartElement();
      if (!isEmpty)
      {
        while (_reader.IsStartElement())
        {
          PropertyMeta pm;
          if (propertyMap.TryGetValue(_reader.Name, out pm))
          {
            var isNilValue = IsNilValue();
            var isEmptyValue = IsEmptyValue();

            if (isNilValue)
            {
              throw new MetadataException(String.Format("Unexpected nil value found for [{0}.{1}]", pm.Entity.Name, pm.Name));
            }
            if (isEmptyValue)
            {
              pm.Read(entity, this);
              _reader.ReadStartElement(pm.Name);
            }
            else
            {
              _reader.ReadStartElement(pm.Name);

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

              _reader.ReadEndElement();
            }

            propIndexes.Add(pm.Index);
          }
          else
          {
            // Skip past this property
            Log.WarnFormat("ClassMeta [{0}] does not have PropertyMeta [{1}]", cm.Name, _reader.Name);
            _reader.Skip();
          }
        }

        _reader.ReadEndElement();
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
    /// <returns></returns>
    public void ReadEntity(PersistentObject entity)
    {
      if (entity == null)
      {
        throw new ArgumentNullException("entity");
      }

      var cm = ClassCache.Find(_reader.Name);
      if (!entity.GetType().IsAssignableFrom(cm.Type))
      {
        throw new InvalidOperationException(string.Format(
          "Type [{0}] is not a [{1}]", cm.Type, entity.GetType()));
      }

      var propertyMap = cm.PersistentPropertyMap;

      // If class element is empty, there are no property nodes, and no end element.
      // This could occur if all the properties have their default values.

      bool isEmpty = _reader.IsEmptyElement;

      var propIndexes = new HashSet<int>();

      _reader.ReadStartElement();
      if (!isEmpty)
      {
        while (_reader.IsStartElement())
        {
          PropertyMeta pm;
          if (propertyMap.TryGetValue(_reader.Name, out pm))
          {
            var isNilValue = IsNilValue();
            var isEmptyValue = IsEmptyValue();

            if (isNilValue)
            {
              throw new MetadataException(String.Format("Unexpected nil value found for [{0}.{1}]", pm.Entity.Name, pm.Name));
            }
            if (isEmptyValue)
            {
              _reader.ReadStartElement(pm.Name);
            }
            else
            {
              _reader.ReadStartElement(pm.Name);
              pm.Read(entity, this);
              _reader.ReadEndElement();
            }

            propIndexes.Add(pm.Index);
          }
          else
          {
            // Skip past this property
            Log.WarnFormat("ClassMeta [{0}] does not have PropertyMeta [{1}]", cm.Name, _reader.Name);
            _reader.Skip();
          }
        }

        _reader.ReadEndElement();
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
      var cm = ClassCache.Find(_reader.Name);
      if (cm == null)
      {
        throw new MetadataException("No component type found with name [" + _reader.Name + "]");
      }
      if (!cm.IsComponent)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not a component");
      }

      var propertyMap = cm.PersistentPropertyMap;

      var obj = (BaseEntityObject)cm.CreateInstance();

      // If class element is empty, there are no property nodes, and no end element.
      // This could occur if all the properties have their default values.

      bool isEmpty = _reader.IsEmptyElement;

      var propIndexes = new HashSet<int>();

      _reader.ReadStartElement();
      if (!isEmpty)
      {
        while (_reader.IsStartElement())
        {
          PropertyMeta pm;
          if (propertyMap.TryGetValue(_reader.Name, out pm))
          {
            var isNilValue = IsNilValue();
            var isEmptyValue = IsEmptyValue();

            if (isNilValue)
            {
              throw new MetadataException(string.Format("Unexpected nil value found for [{0}.{1}]", pm.Entity.Name, pm.Name));
            }
            if (isEmptyValue)
            {
              pm.Read(obj, this);
              _reader.ReadStartElement(pm.Name);
            }
            else
            {
              _reader.ReadStartElement(pm.Name);
              pm.Read(obj, this);
              _reader.ReadEndElement();
            }

            propIndexes.Add(pm.Index);
          }
          else
          {
            // Skip past this property
            Log.WarnFormat("Component [{0}] does not have PropertyMeta [{1}]", cm.Name, _reader.Name);
            _reader.Skip();
          }
        }

        _reader.ReadEndElement();
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
      var set = new HashedSet();

      var isCollectionEmpty = IsEmptyValue();
      if (isCollectionEmpty)
      {
        return set;
      }

      var valueReader = ValueReader<XmlEntityReader, TValue>.Instance;

      while (_reader.IsStartElement())
      {
        var isEmpty = IsEmptyValue();
        var isNil = IsNilValue();
        if (isNil)
        {
          _reader.ReadStartElement("Item");
          set.Add(default(TValue));
        }
        else if (isEmpty)
        {
          set.Add(default(TValue));
          _reader.ReadStartElement("Item");
        }
        else
        {
          _reader.ReadStartElement("Item");
          set.Add(valueReader(this));
          _reader.ReadEndElement();
        }
      }

      return set;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public IDictionary<TKey, TValue> ReadMapCollection<TKey, TValue>()
    {
      var map = new Dictionary<TKey, TValue>();
      var isCollectionEmpty = IsEmptyValue();
      if (isCollectionEmpty)
      {
        return map;
      }
      while (_reader.IsStartElement())
      {
        //neither the key nor value can be null
        _reader.ReadStartElement("Item");
        var key = ReadNamedValue<TKey>("Key");
        var value = ReadNamedValue<TValue>("Value");
        _reader.ReadEndElement();
        map.Add(key, value);
      }
      return map;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public IList<TValue> ReadListCollection<TValue>()
    {
      var list = new List<TValue>();

      var isCollectionEmpty = IsEmptyValue();
      if (isCollectionEmpty)
      {
        return list;
      }

      var valueReader = ValueReader<XmlEntityReader, TValue>.Instance;

      while (_reader.IsStartElement())
      {
        var isEmpty = IsEmptyValue();
        var isNil = IsNilValue();
        if (isNil)
        {
          _reader.ReadStartElement("Item");
          list.Add(default(TValue));
        }
        else if (isEmpty)
        {
          list.Add(valueReader(this));
          _reader.ReadStartElement("Item");
        }
        else
        {
          _reader.ReadStartElement("Item");
          list.Add(valueReader(this));
          _reader.ReadEndElement();
        }
      }

      return list;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public AuditLog ReadAuditLog()
    {
      var action = (ItemAction)Enum.Parse(typeof(ItemAction), _reader.Name);

      _reader.ReadStartElement();

      _reader.ReadStartElement("Tid");
      var tid = ReadInt32();
      _reader.ReadEndElement();

      _reader.ReadStartElement("ObjectId");
      var objectId = ReadObjectId();
      _reader.ReadEndElement();

      _reader.ReadStartElement("RootObjectId");
      var rootObjectId = ReadObjectId();
      _reader.ReadEndElement();

      _reader.ReadStartElement("ParentObjectId");
      var parentObjectId = ReadObjectId();
      _reader.ReadEndElement();

      _reader.ReadStartElement("EntityId");
      var entityId = ReadInt32();
      _reader.ReadEndElement();

      _reader.ReadStartElement("ValidFrom");
      var validFrom = ReadDate();
      _reader.ReadEndElement();

      byte[] objectDelta = null;
      switch (action)
      {
        case ItemAction.Added:
        case ItemAction.Changed:
          _reader.ReadStartElement("ObjectDelta");
          objectDelta = ReadBinaryBlob();
          _reader.ReadEndElement();
          break;
      }

      _reader.ReadEndElement();

      return new AuditLog
      {
        Tid = tid,
        ObjectId = objectId,
        RootObjectId = rootObjectId,
        ParentObjectId = parentObjectId,
        EntityId = entityId,
        ValidFrom = validFrom,
        Action = action,
        ObjectDelta = objectDelta
      };
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool EOF
    {
      get { return _reader.EOF; }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 
    /// </summary>
    public bool IsEmptyValue()
    {
      if (_reader.IsEmptyElement)
      {
        var nilAttribute = _reader.GetAttribute(XmlSnapshotUtil.NilAttributeName);
        var isNil = (nilAttribute == "1");
        return !isNil;
      }
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsNilValue()
    {
      if (_reader.IsEmptyElement)
      {
        var nilAttribute = _reader.GetAttribute(XmlSnapshotUtil.NilAttributeName);
        var isNil = (nilAttribute == "1");
        return isNil;
      }
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ObjectKey ReadObjectKey()
    {
      ObjectKey key;

      var cm = ClassCache.Find(_reader.Name);
      if (cm.IsEntity)
      {
        _reader.ReadStartElement(cm.Name);
        key = new EntityKey(ReadObjectId());
        _reader.ReadEndElement();
      }
      else
      {
        var childKeyPropertyList = cm.ChildKeyPropertyList;
        var state = new object[childKeyPropertyList.Count];

        int i = 0;
        bool isEmpty = _reader.IsEmptyElement;
        _reader.ReadStartElement(cm.Name);
        if (!isEmpty)
        {
          while (_reader.IsStartElement())
          {
            string propName = _reader.Name;
            var pm = childKeyPropertyList.FirstOrDefault(prop => prop.Name == propName);
            if (pm == null)
            {
              throw new MetadataException(string.Format(
                "ClassMeta [{0}] does not have PropertyMeta [{1}]",
                cm.Name, propName));
            }
            var isNilValue = IsNilValue();
            var isEmptyValue = IsEmptyValue();
            if (isNilValue)
            {
              throw new MetadataException(string.Format("Unexpected nil value found for [{0}.{1}]", pm.Entity.Name, pm.Name));
            }
            if (isEmptyValue)
            {
              state[i] = pm.Read(this);
              _reader.ReadStartElement(pm.Name);
            }
            else
            {
              _reader.ReadStartElement(pm.Name);
              state[i] = pm.Read(this);
              _reader.ReadEndElement();
            }
            i++;
          }

          _reader.ReadEndElement();
        }
        if (i != childKeyPropertyList.Count)
        {
          throw new MetadataException(String.Format(
            "Expecting [{0}] ChildKey values for ClassMeta [{1}] found [{2}]",
            childKeyPropertyList.Count, cm.Name, i));
        }

        key = new ComponentKey(cm, state);
      }

      return key;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private TValue ReadNamedValue<TValue>(string name)
    {
      var valueReader = ValueReader<XmlEntityReader, TValue>.Instance;

      var isEmpty = IsEmptyValue();
      var isNil = IsNilValue();
      if (isNil)
      {
        _reader.ReadStartElement(name);
        return valueReader(this);
      }
      if (isEmpty)
      {
        var value = valueReader(this);
        _reader.ReadStartElement(name);
        return value;
      }
      else
      {
        _reader.ReadStartElement(name);
        var value = valueReader(this);
        _reader.ReadEndElement();
        return value;
      }
    }

    #endregion
  }
}