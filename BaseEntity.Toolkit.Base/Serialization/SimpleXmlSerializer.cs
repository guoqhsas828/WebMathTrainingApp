/*
 * Copyright (c)    2002-2014. All rights reserved.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using Utility = BaseEntity.Toolkit.Base.Serialization.SimpleXmlSerializationUtility;

namespace BaseEntity.Toolkit.Base.Serialization
{
  /// <summary>
  ///   A simple XML serializer able to serialize more types than <see cref="System.Xml.Serialization.XmlSerializer"/>
  /// </summary>
  public class SimpleXmlSerializer
  {
    #region Instance Data

    private readonly Dictionary<Type, Type> _collectionTypeMap
      = new Dictionary<Type, Type>
      {
        {typeof (List<>), typeof (IList<>)},
        {typeof(Dictionary<,>), typeof(IDictionary<,>)},
        {typeof(SortedList<,>), typeof(IDictionary<,>)},
        {typeof(SortedDictionary<,>), typeof(IDictionary<,>)},
      };

    private Dictionary<Type, FieldMap> _streamFieldMap;

    private readonly Dictionary<string, Type> _nameToTypes
      = new Dictionary<string, Type>();

    private readonly Dictionary<Type, string> _typeToNames
      = new Dictionary<Type, string>();

    private readonly Dictionary<Type, SimpleXmlSerializationInfo> _infos
      = new Dictionary<Type, SimpleXmlSerializationInfo>();

    private Dictionary<MemberInfo, string> _nameMap;

    internal ReferenceTracker ReferenceTracker;

    #endregion

    #region Properties

    /// <summary>Gets or sets the type of the root object.</summary>
    public Type RootType { get; private set; }

    /// <summary>Gets or sets the name of the root element.</summary>
    public string RootElementName { get; set; }

    /// <summary>Gets or sets the indicator whether to track object references</summary>
    public bool TrackObjectReferences { get; set; }

    #endregion

    #region Constructor

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="rootType">The root type</param>
    /// <param name="knownTypes">Known types</param>
    public SimpleXmlSerializer(Type rootType,
      IEnumerable<KeyValuePair<string, Type>> knownTypes)
    {
      if (rootType == null)
        throw new SerializationException("root type cannot be null");
      RootType = rootType;
      RootElementName = GetXmlName(rootType);
      AddKnownType("delegate", typeof(DelegateData));
      AddKnownType(RootType);
      AddKnownTypes(knownTypes);
    }

    private static string GetXmlName(Type type)
    {
      if (type.IsArray)
      {
        return "Array";
      }
      if (!type.IsGenericType)
        return type.Name;
      var dtype = type.GetGenericTypeDefinition();
      if (dtype == typeof(IEnumerable<>) || dtype == typeof(IList<>)
        || dtype == typeof(IReadOnlyList<>) || dtype == typeof(List<>))
      {
        return "List";
      }
      if (dtype == typeof(IDictionary<,>) || dtype == typeof(Dictionary<,>))
      {
        return "Dictionary";
      }
      throw new SerializationException(String.Format(
        "Unsupported generic type {0}", type));
    }

    #endregion

    #region Known types

    /// <summary>
    ///   Find the type corresponding a type name
    /// </summary>
    /// <param name="typeName">Type name</param>
    /// <returns>Type</returns>
    internal Type GetKnownType(string typeName)
    {
      var name = MapTypeName(typeName);
      if (_nameToTypes != null && _nameToTypes.TryGetValue(name, out var type))
      {
        return type;
      }
      return Type.GetType(name);
    }

    private static string MapTypeName(string typeName)
    {
      const string pattern = @",\s*BaseEntity.Net(?=[\],]|$)";
      return Regex.Replace(typeName, pattern, ", BaseEntity.Toolkit");
    }

    /// <summary>
    ///   Get the mapped XML name of the type
    /// </summary>
    /// <param name="type">Type</param>
    /// <returns>string</returns>
    internal string TryGetMappedTypeName(Type type)
    {
      if (_typeToNames.Count > 0)
      {
        string name;
        if (_typeToNames.TryGetValue(type, out name))
          return name;
      }
      var map = _nameToTypes;
      if (map != null)
      {
        Type ty;
        if (map.TryGetValue(type.Name, out ty) && ty == type)
          return type.Name;
        if (map.TryGetValue(type.FullName, out ty) && ty == type)
          return type.FullName;
      }

      return NameBuilder.GetName(type);
    }

    public static readonly TypeNameBuilder NameBuilder= new TypeNameBuilder();

    private void AddKnownTypes(
      IEnumerable<KeyValuePair<string, Type>> knownTypes)
    {
      if (knownTypes == null) return;
      foreach (var pair in knownTypes)
      {
        AddKnownType(pair.Key, pair.Value);
      }
    }

    private void AddKnownType(string name, Type type)
    {
      if (String.IsNullOrEmpty(name))
      {
        AddKnownType(type);
        return;
      }
      var map = _nameToTypes;
      Type existed;
      if (!map.TryGetValue(name, out existed))
      {
        map.Add(name, type);
        var rmap = _typeToNames;
        if (!rmap.ContainsKey(type))
          _typeToNames.Add(type, name);
        return;
      }
      if (existed == type)
        return;
      throw new ArgumentException(String.Format(
        "Name {0} already mapped to a different type {1}",
        name, existed));
    }

    private void AddKnownType(Type type)
    {
      if (type == null) return;
      var map = _nameToTypes;

      Type existed;
      if (!map.TryGetValue(type.Name, out existed))
      {
        map.Add(type.Name, type);
        return;
      }
      if (existed == type) return;

      if (!map.TryGetValue(type.FullName, out existed))
      {
        map.Add(type.FullName, type);
        return;
      }
      if (existed == type) return;

      var aname = type.AssemblyQualifiedName;
      if (aname == null)
        throw new SerializationException(String.Format(
          "Null Assembly Qualified Name for type {0}", type));
      if (!map.TryGetValue(aname, out existed))
      {
        map.Add(aname, type);
        return;
      }
      if (existed == type) return;

      // Seems not possible.  But throw exception anyway.
      throw new SerializationException(String.Format(
        "two type share the same assembly qualitied name {0}", type));
    }

    #endregion

    #region Field map for regular types

    /// <summary>
    /// Creates a map of field name to XML name
    /// </summary>
    /// <param name="declType">Declaring type</param>
    /// <param name="fieldName">Declared field name</param>
    /// <param name="xmlName">XML name for the field</param>
    /// <exception cref="System.Runtime.Serialization.SerializationException">
    /// </exception>
    public void AddFieldNameMap(Type declType, string fieldName, string xmlName)
    {
      var mi = GetFieldInfo(declType, fieldName);
      if (mi == null)
        throw new SerializationException(String.Format(
          "Could not find the field {0} in type {1}", fieldName, declType));
      if (mi.IsStatic)
        throw new SerializationException(String.Format(
          "The field {0} is static in type {1}", fieldName, declType));
      AddFieldNameMap(mi, xmlName);
    }

    /// <summary>
    ///   Creates a map of member name to XML name
    /// </summary>
    /// <param name="mi">Member info</param>
    /// <param name="xmlName">XML name for the field</param>
    private void AddFieldNameMap(MemberInfo mi, string xmlName)
    {
      if (mi == null)
        throw new ArgumentNullException("mi");
      var map = _nameMap;
      if (map == null)
        map = _nameMap = new Dictionary<MemberInfo, string>();
      map.Add(mi, xmlName);
    }

    /// <summary>
    ///  Get the mapped name of a field
    /// </summary>
    /// <param name="fi">The field info</param>
    /// <returns>The name</returns>
    internal string GetMappedName(FieldInfo fi)
    {
      string name;
      var nameMap = _nameMap;
      if (nameMap != null && nameMap.TryGetValue(fi, out name))
        return name;
      var m = Regex.Match(fi.Name, @"<([^>]+)>k__BackingField");
      return m.Success ? m.Groups[1].Value : fi.Name;
    }

    private static FieldInfo GetFieldInfo(Type declaringType, string fieldName)
    {
      if (declaringType == null)
        throw new ArgumentNullException(nameof(declaringType));
      if (string.IsNullOrWhiteSpace(fieldName))
        throw new ArgumentException($"{nameof(fieldName)} cannot be empty");
      const BindingFlags flags = BindingFlags.DeclaredOnly |
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic;
      var mi = declaringType.GetField(fieldName, flags);
      if (mi != null) return mi;
      return declaringType.GetField("<" + fieldName + ">k__BackingField", flags);
    }

    #endregion

    #region Field map for ISerializables

    /// <summary>
    /// Creates a map of field name to XML name
    /// </summary>
    /// <param name="declType">Declaring type</param>
    /// <param name="fieldName">Declared field name</param>
    /// <param name="fieldType">Type of the field.</param>
    /// <param name="mappedName">Name of the mapped.</param>
    /// <exception cref="System.Runtime.Serialization.SerializationException">
    /// </exception>
    public void AddFieldNameMap(Type declType, string fieldName,
      Type fieldType, string mappedName)
    {
      FieldMap dict;
      var map = _streamFieldMap ??
        (_streamFieldMap = new Dictionary<Type, FieldMap>());
      if (!map.TryGetValue(declType, out dict))
      {
        dict = new FieldMap();
        map.Add(declType, dict);
      }
      dict.Add(fieldName, mappedName, fieldType);
    }

    internal FieldMap GetFieldMap(Type declType)
    {
      var map = _streamFieldMap;
      if (map == null) return null;
      FieldMap dict;
      return map.TryGetValue(declType, out dict) ? dict : null;
    }

    #endregion

    #region Collection type interface

    /// <summary>
    /// Gets the collection interface.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="ta">The ta.</param>
    /// <returns>Type.</returns>
    internal Type GetCollectionInterface(Type type, out Type[] ta)
    {
      Debug.Assert(type != null);

      ta = null;
      Type container;
      var map = _collectionTypeMap;
      if (map == null) return null;
      if (map.TryGetValue(type, out container))
        return container;
      if (type.IsGenericType && map.TryGetValue(
        type.GetGenericTypeDefinition(), out container))
      {
        ta = type.GetGenericArguments();
        return container.MakeGenericType(ta);
      }
      return null;
    }

    /// <summary>
    /// Maps the type to the specified collection interface type.
    /// </summary>
    /// <param name="type">The type to map from</param>
    /// <param name="collectionType">The collection interface type,
    ///  either <see cref="IList{T}"/> or <see cref="IDictionary{TKey,TValue}"/></param>
    /// <exception cref="System.ArgumentException">container type must be either IList&lt;&gt; or IDictionary&lt;,&gt;</exception>
    public void MapCollectionType(Type type, Type collectionType)
    {
      Type dtype;
      if (!collectionType.IsGenericType || (typeof (IList<>) !=
        (dtype = collectionType.GetGenericTypeDefinition()) &&
        typeof (IDictionary<,>) != dtype))
      {
        throw new ArgumentException("container type must be either IList<> or IDictionary<,>");
      }
      var map = _collectionTypeMap;
      map.Add(type, collectionType);
    }

    #endregion

    #region SimpleXmlSerializationInfo

    public SimpleXmlSerializationInfo GetSerializationInfo(Type type)
    {
      Debug.Assert(type != null);
      SimpleXmlSerializationInfo info;
      if (!_infos.TryGetValue(type, out info))
      {
        info = SimpleXmlSerializationInfo.Create(type, this);
        _infos.Add(type, info);
      }
      return info;
    }

    #endregion

    #region Write and read object

    /// <summary>
    ///  Writes an object to an XML stream
    /// </summary>
    /// <param name="writer">XML writer</param>
    /// <param name="data">Object to serialize</param>
    public void WriteObject(XmlWriter writer, object data)
    {
      BeginSerialization(data);
      try
      {
        SimpleXmlSerializationUtility.WriteItem(writer, this, RootElementName,
          RootType, false, data);
      }
      finally { EndSerialization(); }
    }

    /// <summary>
    ///  Writes an object as an XML string
    /// </summary>
    /// <param name="data">Object to serialize</param>
    /// <returns>XML string</returns>
    public string WriteObject(object data)
    {
      BeginSerialization(data);
      try
      {
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb,
          new XmlWriterSettings
          {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Indent = true
          }))
        {
          WriteObject(writer, data);
        }
        return sb.ToString();
      }
      finally { EndSerialization(); }
    }

    /// <summary>
    ///  Reads the object from an XML reader
    /// </summary>
    /// <param name="reader">XML reader</param>
    /// <returns>Object</returns>
    public object ReadObject(XmlReader reader)
    {
      BeginSerialization(null);
      try
      {
        //reader.ReadStartElement();
        if (!reader.IsStartElement(RootElementName))
        {
          throw new SerializationException(String.Format(
            "Root element name '{0}' not match required name '{1}'",
            reader.Name, RootElementName));
        }
        return SimpleXmlSerializationUtility.ReadValue(reader, this, RootType);
        //reader.ReadEndElement();
      }
      finally { EndSerialization(); }
    }

    /// <summary>
    ///  Read the object from an XML string
    /// </summary>
    /// <typeparam name="T">The type of the object</typeparam>
    /// <param name="xml">XML string</param>
    /// <returns>Object</returns>
    public T ReadObject<T>(string xml)
    {
      BeginSerialization(null);
      try
      {
        using (var reader = XmlReader.Create(new StringReader(xml)))
        {
          return (T)ReadObject(reader);
        }
      }
      finally { EndSerialization(); }
    }

    private void BeginSerialization(object data)
    {
      if (RootType == null)
        throw new NullReferenceException("root type cannot be null");
      if (data != null && !RootType.IsInstanceOfType(data))
        throw new SerializationException(string.Format(
          "root object is not of root type: {0}", RootType));
      if (TrackObjectReferences)
        ReferenceTracker = new ReferenceTracker(this, data);
    }

    private void EndSerialization()
    {
      ReferenceTracker = null;
    }

    #endregion
  }

}
