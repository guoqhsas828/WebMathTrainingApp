/*
 * Copyright (c)    2002-2016. All rights reserved.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base.Serialization
{
  internal class ReferenceTracker
  {
    #region Data

    private readonly Dictionary<object, string> _objectToIds;
    private Dictionary<string, object> _objectByIds;

    #endregion

    #region Constructors

    internal ReferenceTracker(SimpleXmlSerializer settings,
      object rootObject = null)
    {
      if (rootObject == null) return;
        var list = RecordReferences(rootObject, settings)
          .Where(e =>e.Value > 1).Select(e=>e.Key).ToList();
      var count = list.Count;
      if (count == 0) return;
      var map = _objectToIds = new Dictionary<object, string>(
        TheDefaultComparer);
      for (int i = 0; i < count; ++i)
        map.Add(list[i], (i+1).ToString());
    }

    #endregion

    #region Instance methods

    public string GetId(object data)
    {
      var map = _objectToIds;
      if (map == null) return null;
      string id;
      return map.TryGetValue(data, out id) ? id : null;
    }

    public object GetObject(string id)
    {
      var map = _objectByIds;
      if (map == null) return null;
      object data;
      return map.TryGetValue(id, out data) ? data : null;
    }

    public void Add(string id, object obj)
    {
      var map = _objectByIds;
      if (map == null)
        map = _objectByIds = new Dictionary<string, object>();
      map.Add(id, obj);
    }

    #endregion

    #region Record references

    private static IDictionary<object, int> RecordReferences(
      object data,
      SimpleXmlSerializer settings)
    {
      var dict = new Dictionary<object, int>(TheDefaultComparer);
      RecordReferences(data, null, settings, dict);
      return dict;
    }

    private static void RecordReferences(
      object data, Type declaredType,
      SimpleXmlSerializer settings,
      IDictionary<object, int> dict)
    {
      Debug.Assert(dict != null);

      if (data == null || (declaredType != null && (declaredType.IsEnum ||
        Type.GetTypeCode(declaredType) != TypeCode.Object)))
      {
        return;
      }

      if (declaredType == null || !declaredType.IsValueType)
      {
        int count;
        if (dict.TryGetValue(data, out count))
        {
          dict[data] = count + 1;
          return;
        }
        dict.Add(data, 1);
      }

      var type = data.GetType();
      if (type.IsPrimitive || type.IsEnum ||
        Type.GetTypeCode(type) != TypeCode.Object)
      {
        return;
      }

      // Record members
      if (data is INativeSerializable || data is IXmlSerializable
        || data is System.Reflection.MemberInfo)
      {
        return;
      }

      {
        var fn = data as Delegate;
        if (fn != null)
        {
          RecordReferences(fn.Target, typeof(object), settings, dict);
          RecordReferences(fn.Method,
            typeof(System.Reflection.MethodInfo), settings, dict);
          return;
        }
      }

      if (type.IsArray)
      {
        // Array is IEnumerable
        RecordList(settings, (IEnumerable)data, type.GetElementType(), dict);
        return;
      }

      Type ctype, otype;
      var ta = SimpleXmlSerializationUtility.TryGetCollectionType(
        settings, type, out otype, out ctype);
      if (ta != null)
      {
        if (ctype.GetGenericTypeDefinition() == typeof (IList<>))
        {
          RecordList(settings, (IEnumerable) data, ta[0], dict);
        }
        else
        {
          RecordDictionary(settings, (IDictionary)data, ta[0], ta[1], dict);
        }
        return;
      }

      Debug.Assert(!type.IsPrimitive && !type.IsEnum && type != typeof(string));
      var info = settings.GetSerializationInfo(type);
      foreach (var fi in info.Fields.Select(e => e.Value))
      {
        RecordReferences(fi.GetValue(data), fi.FieldType, settings, dict);
      }
    }

    private static void RecordList(
      SimpleXmlSerializer settings,
      IEnumerable data, Type declaredElemType,
      IDictionary<object, int> dict)
    {
      Debug.Assert(settings != null);

      // Array is IEnumerable
      foreach (var obj in data)
      {
        RecordReferences(obj, declaredElemType, settings, dict);
      }
      return;
    }

    private static void RecordDictionary(
      SimpleXmlSerializer settings,
      IDictionary data, Type keyType, Type valueType,
      IDictionary<object, int> dict)
    {
      foreach (DictionaryEntry entry in data)
      {
        RecordReferences(entry.Key, keyType, settings, dict);
        RecordReferences(entry.Value, valueType, settings, dict);
      }
    }

    #endregion

    #region Comparer
    /// <summary>
    /// The default comparer
    /// </summary>
    private static readonly IEqualityComparer<object> TheDefaultComparer
      = new ReferenceComparer();

    class ReferenceComparer : IEqualityComparer<object>
    {
      #region IEqualityComparer Members

      bool IEqualityComparer<object>.Equals(object x, object y)
      {
        return ReferenceEquals(x, y);
      }

      int IEqualityComparer<object>.GetHashCode(object obj)
      {
        return obj.GetHashCode();
      }

      #endregion
    }
    #endregion

    #region Assembly tracker

    private Dictionary<string, Assembly> _assemblies;

    internal void Add(Assembly assembly)
      => (_assemblies ?? (_assemblies = new Dictionary<string, Assembly>()))
        .Add(assembly.FullName, assembly);

    internal bool ContainsAssembly(string assemblyName)
      => (_assemblies?.ContainsKey(assemblyName)) ?? false;

    internal bool TryGetAssembly(string assemblyName, out Assembly assembly)
    {
      if (_assemblies == null)
      {
        assembly = null;
        return false;
      }
      return _assemblies.TryGetValue(assemblyName, out assembly);
    }
    #endregion
  }
}
