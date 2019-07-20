// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
  public class JsonEntityReader : EntityReaderBase, IEntityReader
  {
    #region Data

    private static readonly ILog Log = QLogManager.GetLogger(typeof(XmlEntityReader));

    private TextReader _reader;
    private char _currentChar;
    private int _line;
    private int _offset;
    private bool _eof;

    #endregion

    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="json"></param>
    /// <param name="context"></param>
    public JsonEntityReader(string json, IEditableEntityContext context = null)
      : this(new StringReader(json), context)
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="context"></param>
    public JsonEntityReader(TextReader reader, IEditableEntityContext context = null)
      : this(reader, new EntityContextEditorAdaptor(context ?? (IEditableEntityContext)EntityContext.Current))
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="json"></param>
    /// <param name="adaptor"></param>
    public JsonEntityReader(string json, IEntityContextAdaptor adaptor)
      : this(new StringReader(json), adaptor)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="adaptor"></param>
    public JsonEntityReader(TextReader reader, IEntityContextAdaptor adaptor)
    {
      _reader = reader;

      ReadChar();

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
      var intValue = ReadNativeInt32();
      switch (intValue)
      {
        case 0:
          return false;
        case 1:
          return true;
        default:
          throw new MetadataException("Invalid value [" + intValue + "]");
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int ReadInt32()
    {
      return ReadNativeInt32();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int? ReadNullableInt32()
    {
      int? value;

      if (_currentChar == 'n')
      {
        ReadNull();
        value = null;
      }
      else
      {
        value = ReadNativeInt32();
      }

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long ReadInt64()
    {
      return ReadNativeInt64();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long? ReadNullableInt64()
    {
      long? value;

      if (_currentChar == 'n')
      {
        ReadNull();
        value = null;
      }
      else
      {
        value = ReadNativeInt64();
      }

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double ReadDouble()
    {
      return ReadNativeDouble();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double? ReadNullableDouble()
    {
      double? value;

      if (_currentChar == 'n')
      {
        ReadNull();
        value = null;
      }
      else
      {
        value = ReadNativeDouble();
      }

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime ReadDateTime()
    {
      return ReadNativeDateTime();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime? ReadNullableDateTime()
    {
      DateTime? value;

      if (_currentChar == 'n')
      {
        ReadNull();
        value = null;
      }
      else
      {
        value = ReadNativeDateTime();
      }

      return value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue ReadEnum<TValue>() where TValue : struct
    {
      return (TValue)Enum.ToObject(typeof(TValue), ReadNativeInt32());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue? ReadNullableEnum<TValue>() where TValue : struct
    {
      if (_currentChar == 'n')
      {
        ReadNull();
        return null;
      }
      
      return ReadEnum<TValue>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string ReadString()
    {
      return ReadNativeString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Guid ReadGuid()
    {
      return ReadNativeGuid();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double[] ReadArrayOfDoubles()
    {
      return ReadListCollection<double>().ToArray();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public double[,] ReadArrayOfDoubles2D()
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public byte[] ReadBinaryBlob()
    {
      return Convert.FromBase64String(ReadNativeString());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public long ReadObjectId()
    {
      bool isTransient = _currentChar == 'T';
      if (isTransient)
      {
        ReadChar();
      }
      long id = ReadNativeInt64();
      return isTransient ? (long)((ulong)id | EntityHelper.TransientBitMask) : id;
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
    /// <returns></returns>
    public DateTime ReadDate()
    {
      return ReadDateTime();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public DateTime? ReadNullableDate()
    {
      return ReadNullableDateTime();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T ReadValue<T>()
    {
      return ValueReader<JsonEntityReader, T>.ReadValue(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override PersistentObject ReadEntity()
    {
      SkipWhiteSpace();
      if (_currentChar != '{')
      {
        throw new JSonException("Invalid start of object", _line, _offset);
      }
      ReadChar();

      var cm = ReadClassMeta();
      if (!cm.IsEntity)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not an entity");
      }

      var propertyMap = cm.PersistentPropertyMap;

      // Defer creation of entity in case already created via OneToMany or ManyToMany
      PersistentObject entity = null;

      var propIndexes = new HashSet<int>();

      // If class element is empty, there are no property nodes, and no end element.
      // This could occur if all the properties have their default values.

      while (true)
      {
        SkipWhiteSpace();
        if (_currentChar == '}')
        {
          ReadChar();
          break;
        }
        if (_currentChar != ',')
        {
          throw new JSonException("Expecting comma", _line, _offset);
        }
        ReadChar();

        string propName = ReadNativeString();

        PropertyMeta pm;
        if (propertyMap.TryGetValue(propName, out pm))
        {
          SkipWhiteSpace();
          if (_currentChar != ':')
          {
            throw new JSonException("Expecting colon", _line, _offset);
          }
          ReadChar();

          SkipWhiteSpace();
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

          propIndexes.Add(pm.Index);
        }
        else
        {
          // Skip past this property
          Log.WarnFormat("ClassMeta [{0}] does not have PropertyMeta [{1}]", cm.Name, propName);
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
    /// Read ExtendedData properties into specified <see cref="PersistentObject">entity</see>
    /// </summary>
    /// <param name="entity"></param>
    public void ReadEntity(PersistentObject entity)
    {
      if (entity == null)
      {
        throw new ArgumentNullException("entity");
      }

      SkipWhiteSpace();
      if (_currentChar != '{')
      {
        throw new JSonException("Invalid start of object", _line, _offset);
      }
      ReadChar();

      var cm = ReadClassMeta();
      if (!cm.IsEntity)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not an entity");
      }

      var propertyMap = cm.PersistentPropertyMap;

      var propIndexes = new HashSet<int>();

      // If class element is empty, there are no property nodes, and no end element.
      // This could occur if all the properties have their default values.

      while (true)
      {
        SkipWhiteSpace();
        if (_currentChar == '}')
        {
          ReadChar();
          break;
        }
        if (_currentChar != ',')
        {
          throw new JSonException("Expecting comma", _line, _offset);
        }
        ReadChar();

        string propName = ReadNativeString();

        PropertyMeta pm;
        if (propertyMap.TryGetValue(propName, out pm))
        {
          SkipWhiteSpace();
          if (_currentChar != ':')
          {
            throw new JSonException("Expecting colon", _line, _offset);
          }
          ReadChar();
          SkipWhiteSpace();
          pm.Read(entity, this);
          propIndexes.Add(pm.Index);
        }
        else
        {
          // Skip past this property
          Log.WarnFormat("ClassMeta [{0}] does not have PropertyMeta [{1}]", cm.Name, propName);
        }
      }

      // Any ExtendedData property that was not found is set to its default value
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
      SkipWhiteSpace();
      if (_currentChar != '{')
      {
        throw new JSonException("Invalid start of object", _line, _offset);
      }
      ReadChar();

      var cm = ReadClassMeta();
      if (!cm.IsComponent)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not a component");
      }

      var propertyMap = cm.PersistentPropertyMap;

      var obj = (BaseEntityObject)cm.CreateInstance();

      var propIndexes = new HashSet<int>();

      // If class element is empty, there are no property nodes, and no end element.
      // This could occur if all the properties have their default values.

      while (true)
      {
        SkipWhiteSpace();
        if (_currentChar == '}')
        {
          ReadChar();
          break;
        }
        if (_currentChar != ',')
        {
          throw new JSonException("Expecting comma", _line, _offset);
        }
        ReadChar();

        string propName = ReadNativeString();

        PropertyMeta pm;
        if (propertyMap.TryGetValue(propName, out pm))
        {
          SkipWhiteSpace();
          if (_currentChar != ':')
          {
            throw new JSonException("Expecting colon", _line, _offset);
          }
          ReadChar();
          SkipWhiteSpace();
          pm.Read(obj, this);
          propIndexes.Add(pm.Index);
        }
        else
        {
          // Skip past this property
          Log.WarnFormat("ClassMeta [{0}] does not have PropertyMeta [{1}]", cm.Name, propName);
        }
      }

      // Any property that was not found is set to its default value
      foreach (var pm in cm.PersistentPropertyMap.Values.Where(pm => !propIndexes.Contains(pm.Index) && !pm.HasDefaultValue(obj)))
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
      SkipWhiteSpace();

      if (_currentChar != '[')
      {
        throw new JSonException("Invalid start of array", _line, _offset);
      }

      ReadChar();

      var valueReader = ValueReader<JsonEntityReader, TValue>.Instance;

      var set = new HashedSet();

      while (true)
      {
        if (_currentChar == ']')
        {
          ReadChar();
          break;
        }

        set.Add(valueReader(this));

        if (_currentChar == ',')
        {
          ReadChar();
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
      SkipWhiteSpace();
      if (_currentChar != '[')
      {
        throw new JSonException("Expecting '[', found '" + _currentChar + "'", _line, _offset);
      }
      ReadChar();

      var keyReader = ValueReader<JsonEntityReader, TKey>.Instance;

      var valueReader = ValueReader<JsonEntityReader, TValue>.Instance;

      var map = new Dictionary<TKey, TValue>();

      while (true)
      {
        SkipWhiteSpace();
        if (_currentChar == ']')
        {
          ReadChar();
          break;
        }
        if (_currentChar != '[')
        {
          throw new JSonException("Expecting '[', found '" + _currentChar + "'", _line, _offset);
        }
        ReadChar();

        var key = keyReader(this);

        SkipWhiteSpace();
        if (_currentChar != ',')
        {
          throw new JSonException("Expecting ',', found '" + _currentChar + "'", _line, _offset);
        }
        ReadChar();

        var value = valueReader(this);

        SkipWhiteSpace();
        if (_currentChar != ']')
        {
          throw new JSonException("Expecting ']', found '" + _currentChar + "'", _line, _offset);
        }
        ReadChar();

        map.Add(key, value);

        SkipWhiteSpace();
        if (_currentChar == ',')
        {
          ReadChar();
        }
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
      SkipWhiteSpace();

      if (_currentChar != '[')
      {
        throw new JSonException("Invalid start of array", _line, _offset);
      }

      ReadChar();

      var valueReader = ValueReader<JsonEntityReader, TValue>.Instance;

      var list = new List<TValue>();

      while (true)
      {
        if (_currentChar == ']')
        {
          ReadChar();
          break;
        }

        list.Add(valueReader(this));

        if (_currentChar == ',')
        {
          ReadChar();
        }
      }

      return list;
    }

    /// <summary>
    /// EOF. Boolean type.
    /// </summary>
    public override bool EOF
    {
      get { return _eof; }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Consume any whitespace
    /// </summary>
    /// <returns></returns>
    public void SkipWhiteSpace()
    {
      while (char.IsWhiteSpace(_currentChar))
      {
        ReadChar();
      }
    }

    /// <summary>
    /// Reads next character from the input source.
    /// Automatically updates current char, EOF and traced position.
    /// </summary>
    public void ReadChar()
    {
      int data = _reader.Read();

      _eof = data == -1;

      if (!_eof)
      {
        _currentChar = (char)data;
        if (_currentChar == '\n')
        {
          _offset = 1;
          _line++;
        }
        else
        {
          _offset++;
        }
      }
      else
      {
        _currentChar = '\0';
      }
    }

    /// <summary>
    /// Reads the native int32.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">Invalid Int32 value</exception>
    public int ReadNativeInt32()
    {
      string strValue = ReadNumber();

      int value;
      if (Int32.TryParse(strValue, out value))
      {
        return value;
      }

      throw new JSonException("Invalid Int32 value", _line, _offset - strValue.Length);
    }

    /// <summary>
    /// Reads the native int64.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">Invalid Int64 value</exception>
    public long ReadNativeInt64()
    {
      string strValue = ReadNumber();

      long value;
      if (Int64.TryParse(strValue, out value))
      {
        return value;
      }

      throw new JSonException("Invalid Int64 value", _line, _offset - strValue.Length);
    }

    /// <summary>
    /// Reads the native double.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">Invalid number value</exception>
    public double ReadNativeDouble()
    {
      string strValue = ReadNumber();

      double value;
      if (Double.TryParse(strValue, out value))
      {
        return value;
      }

      throw new JSonException("Invalid number value", _line, _offset - strValue.Length);
    }

    /// <summary>
    /// Reads the native date time.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">Invalid DateTime value</exception>
    public DateTime ReadNativeDateTime()
    {
      string strValue = ReadNativeString();

      const DateTimeStyles styles = DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.RoundtripKind;

      DateTime value;
      if (DateTime.TryParseExact(strValue, "o", CultureInfo.InvariantCulture, styles, out value))
      {
        return value;
      }

      throw new JSonException("Invalid DateTime value", _line, _offset - strValue.Length);
    }

    /// <summary>
    /// Reads the native unique identifier.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">Invalid Guid value</exception>
    public Guid ReadNativeGuid()
    {
      string strValue = ReadNativeString();

      Guid value;
      if (Guid.TryParse(strValue, out value))
      {
        return value;
      }

      throw new JSonException("Invalid Guid value", _line, _offset - strValue.Length);
    }

    /// <summary>
    /// Reads the number.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">
    /// Invalid number format
    /// or
    /// Invalid number
    /// </exception>
    public string ReadNumber()
    {
      if (!char.IsDigit(_currentChar) && _currentChar != '-' && _currentChar != '.')
      {
        throw new JSonException("Invalid number format", _line, _offset);
      }

      var sb = new StringBuilder();

      while (true)
      {
        if (_currentChar == '\0')
        {
          break;
        }

        if (char.IsDigit(_currentChar) ||
            _currentChar == '-' ||
            _currentChar == '.' ||
            _currentChar == 'e' ||
            _currentChar == 'E')
        {
          sb.Append(_currentChar);
          ReadChar();
        }
        else
        {
          if (_currentChar != ',' && !char.IsWhiteSpace(_currentChar) && _currentChar != ']' && _currentChar != '}')
          {
            throw new JSonException("Invalid number", _line, _offset - sb.Length);
          }

          break;
        }
      }

      return sb.ToString();
    }

    /// <summary>
    /// Reads the null.
    /// </summary>
    /// <exception cref="JSonException">Expecting 'null'</exception>
    public void ReadNull()
    {
      int line = _line;
      int offset = _offset;

      var sb = new StringBuilder();

      sb.Append(_currentChar);
      ReadChar();
      sb.Append(_currentChar);
      ReadChar();
      sb.Append(_currentChar);
      ReadChar();
      sb.Append(_currentChar);
      ReadChar();

      var result = sb.ToString();
      if (result != "null")
      {
        throw new JSonException("Expecting 'null'", line, offset);
      }
    }

    /// <summary>
    /// Reads the native string.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">
    /// Expecting double-quote to start string
    /// or
    /// Unexpected end of text while reading a string
    /// or
    /// Unexpected new line character in the middle of a string
    /// or
    /// Unknown escape combination
    /// </exception>
    public string ReadNativeString()
    {
      var sb = new StringBuilder();

      if (_currentChar != '"')
      {
        throw new JSonException("Expecting double-quote to start string", _line, _offset);
      }

      bool escape = false;

      while (true)
      {
        ReadChar();

        // verify if not an invalid character was found in text:
        if (_eof)
        {
          throw new JSonException("Unexpected end of text while reading a string", _line, _offset);
        }
        if (_currentChar == '\r' || _currentChar == '\n')
        {
          throw new JSonException("Unexpected new line character in the middle of a string", _line, _offset);
        }

        if (_currentChar == '\\' && !escape)
        {
          escape = true;
        }
        else
        {
          if (escape)
          {
            switch (_currentChar)
            {
              case 'n':
                sb.Append('\n');
                break;
              case 'r':
                sb.Append('\r');
                break;
              case 't':
                sb.Append('\t');
                break;
              case '/':
                sb.Append('/');
                break;
              case '\\':
                sb.Append('\\');
                break;
              case 'f':
                sb.Append('\f');
                break;
              case '"':
                sb.Append('"');
                break;
              case '\'':
                sb.Append('\'');
                break;
              default:
                throw new JSonException("Unknown escape combination", _line, _offset - 2);
            }

            escape = false;
          }
          else
          {
            if (_currentChar == '"')
            {
              ReadChar();
              break;
            }

            sb.Append(_currentChar);
          }
        }
      }

      return sb.ToString();
    }

    /// <summary>
    /// Reads the class meta.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="JSonException">
    /// Invalid @class member
    /// or
    /// Invalid @class member
    /// </exception>
    public ClassMeta ReadClassMeta()
    {
      if (ReadNativeString() != "@class")
      {
        throw new JSonException("Invalid @class member", _line, _offset);
      }
      SkipWhiteSpace();
      if (_currentChar != ':')
      {
        throw new JSonException("Invalid @class member", _line, _offset);
      }
      ReadChar();
      SkipWhiteSpace();
      string className = ReadNativeString();
      var cm = ClassCache.Find(className);
      if (cm == null)
      {
        throw new MetadataException("No entity type found with name [" + className + "]");
      }
      return cm;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ComponentKey ReadComponentKey()
    {
      SkipWhiteSpace();
      if (_currentChar != '{')
      {
        throw new JSonException("Invalid start of ChildKey", _line, _offset);
      }
      ReadChar();

      SkipWhiteSpace();
      var cm = ReadClassMeta();
      if (!cm.IsComponent)
      {
        throw new MetadataException("Class [" + cm.Name + "] is not an component");
      }
      var childKeyPropertyList = cm.ChildKeyPropertyList;
      var state = new object[childKeyPropertyList.Count];

      int i = 0;
      while (true)
      {
        SkipWhiteSpace();
        if (_currentChar == '}')
        {
          ReadChar();
          break;
        }
        if (_currentChar != ',')
        {
          throw new JSonException("Expecting comma", _line, _offset);
        }
        ReadChar();

        string propName = ReadNativeString();

        var pm = childKeyPropertyList.First(prop => prop.Name == propName);
        if (pm == null)
        {
          break;
        }

        SkipWhiteSpace();
        if (_currentChar != ':')
        {
          throw new JSonException("Expecting colon", _line, _offset);
        }
        ReadChar();

        SkipWhiteSpace();
        state[pm.Index] = pm.Read(this);
        i += 1;
      }

      if (i != childKeyPropertyList.Count)
      {
        throw new MetadataException(
          String.Format("Expecting [{0}] key properties; found [{1}]", childKeyPropertyList.Count, i));
      }

      return new ComponentKey(cm, state);
    }

    #endregion
  }
}