/*
 * Copyright (c)    2002-2014. All rights reserved.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base.Serialization
{
  internal static class SimpleXmlSerializationUtility
  {
    #region Serialization utilities

    public static void WriteItem(
      XmlWriter writer, SimpleXmlSerializer settings,
      string name, Type declType, bool ignoreDefault,
      object data)
    {
      Debug.Assert(declType != null);

      if (IsDefaultValue(declType, data))
      {
        if (ignoreDefault)
          return;
        writer.WriteStartElement(name);
        if (data == null && !declType.IsValueType)
        {
          writer.WriteAttributeString(AttrNullName, "true");
        }
        writer.WriteEndElement();
        return;
      }

      writer.WriteStartElement(name);
      if (!WriteReference(writer, settings, declType, data))
      {
        WriteItemValue(writer, settings, declType, data);
      }
      writer.WriteEndElement();
    }

    private static void WriteItemValue(XmlWriter writer,
      SimpleXmlSerializer settings, Type declType, object data)
    {
      if (Surrogates.Wrap(data, settings, out var wrapped, out var type))
      {
        data = wrapped;
      }
      else
      {
        type = data == null ? declType : data.GetType();
      }
      if (!type.IsInferableFrom(declType))
      {
        writer.WriteAttributeString(AttrTypeName, GetTypeName(type));
      }

      if (data == null)
      {
        writer.WriteAttributeString(AttrNullName, "true");
      }

      if (type.IsEnum)
      {
        writer.WriteString(data.ToString());
        return;
      }

      if (type.IsPrimitive || Type.GetTypeCode(type) != TypeCode.Object)
      {
        writer.WriteValue(data);
        return;
      }

      if (type.IsArray)
      {
        WriteArray(writer, settings, type.GetArrayRank(),
          type.GetElementType(), data);
        return;
      }

      if (!TryWriteAsCustomSerializable(writer, settings, type, data)
        && !TryWriteContainer(writer, settings, type, data))
      {
        Debug.Assert(!type.IsPrimitive && !type.IsEnum && type != typeof(string));
        var xs = data as IXmlSerializable;
        if (xs == null)
        {
          var info = settings.GetSerializationInfo(type);
          info.WriteValue(writer, settings, data);
          return;
        }
        xs.WriteXml(writer);
      }
    }

    private static bool WriteReference(XmlWriter writer,
      SimpleXmlSerializer settings, Type declType, object data)
    {
      if (data == null || settings.ReferenceTracker == null
        || (declType != null && Type.GetTypeCode(declType) != TypeCode.Object))
      {
        return false;
      }
      var tracker = settings.ReferenceTracker;
      var id = tracker.GetId(data);
      if (id == null) return false;
      var obj = tracker.GetObject(id);
      if (obj == null)
      {
        writer.WriteAttributeString(AttrIdName, id);
        tracker.Add(id, data);
        return false;
      }
      writer.WriteAttributeString(AttrRefName, id);
      return true;
    }

    internal static string GetTypeName(Type type,
      SimpleXmlSerializer settings = null)
    {
      string name;
      if (settings != null &&
        (name = settings.TryGetMappedTypeName(type)) != null)
      {
        return name;
      }

      return SimpleXmlSerializer.NameBuilder.GetName(type);
    }

    private static bool TryWriteAsCustomSerializable(
      XmlWriter writer, SimpleXmlSerializer settings,
      Type type, object data)
    {
      if (data == null) return false;
      var serializer = CustomSerializers.TryGet(type);
      if (serializer != null)
      {
        serializer.WriteValue(writer, settings, data);
        return true;
      }
      return false;
    }

    private static bool TryWriteContainer(XmlWriter writer,
      SimpleXmlSerializer settings, Type type, object data)
    {
      Type ctype;
      var ta = TryGetCollectionType(settings, type, out type, out ctype);
      if (ta == null) return false;

      if (ctype.GetGenericTypeDefinition() == typeof(IList<>))
      {
        WriteArray(writer, settings, 1, ta[0], data);
      }
      else
      {
        var fn = GetWriteDictionaryFn(ta);
        fn(writer, settings, data);
      }
      return true;
    }

    private static bool IsInferableFrom(this Type type, Type declType)
    {
      if (type == declType) return true;
      if (!declType.IsGenericType) return false;
      var dtype = declType.GetGenericTypeDefinition();
      if (dtype == typeof(IList<>))
      {
        return type.IsGenericType &&
          type.GetGenericTypeDefinition() == typeof(List<>);
      }
      if (dtype == typeof(IDictionary<,>))
      {
        return type.IsGenericType &&
          type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
      }
      return false;
    }

    private static void WriteArray(
      XmlWriter writer, SimpleXmlSerializer settings,
      int rank, Type elemType, object data)
    {
      Debug.Assert(settings != null);

      if (data is byte[] bytes)
      {
        writer.WriteAttributeString(DimensionName, bytes.Length.ToString());
        writer.WriteBase64(bytes, 0, bytes.Length);
        return;
      }

      var a = data as Array;
      if (a == null && rank != 1)
      {
        throw new SerializationException("Data must be a multidimensional array");
      }

      if (a != null)
      {
        var sb = new StringBuilder();
        sb.Append(a.GetLength(0));
        for (int i = 1; i < rank; ++i)
          sb.Append(',').Append(a.GetLength(i));
        writer.WriteAttributeString(DimensionName, sb.ToString());

        // Try compress array
        int skipped = 0;
        foreach (var obj in (IEnumerable)data)
        {
          if (IsDefaultValue(elemType, obj))
          {
            ++skipped;
            continue;
          }
          writer.WriteStartElement(ElementName);
          if (skipped > 0)
          {
            writer.WriteAttributeString(AttrSkipped, skipped.ToString());
            skipped = 0;
          }
          if (!WriteReference(writer, settings, elemType, obj))
          {
            WriteItemValue(writer, settings, elemType, obj);
          }
          writer.WriteEndElement();
        }
        return;
      }

      // data must be IEnumerable
      foreach (var obj in (IEnumerable)data)
      {
        WriteItem(writer, settings, ElementName, elemType, false, obj);
      }
      return;
    }

    private delegate void WriteObjectFn(XmlWriter writer,
      SimpleXmlSerializer settings, object data);

    private static bool IsDefaultValue(Type declType, object data)
    {
      if (data == null) return true;
      return (declType.IsValueType)
        && data.Equals(Activator.CreateInstance(declType));
    }

    #region Create instance by the default constructor

    private static object CreateInstance(Type type)
    {
      if (type.IsValueType)
      {
        return Activator.CreateInstance(type);
      }

      var ctor = type
        .GetConstructors(BindingFlags.Instance |
          BindingFlags.NonPublic | BindingFlags.Public)
        .Select(c => new { F = c, P = c.GetParameters() })
        .OrderBy(o => o.P.Length)
        .FirstOrDefault(o => o.P.Length == 0 || o.P.All(p => p.IsOptional));
      return ctor == null
        ? FormatterServices.GetSafeUninitializedObject(type)
        : ctor.F.Invoke(GetOptionalParameters(ctor.P.Length));
    }

    private static object[] GetOptionalParameters(int count)
    {
      if (count < OptionalParameters.Length)
      {
        return OptionalParameters[count];
      }
      var a = new object[count];
      for (int i = 0; i < a.Length; ++i)
        a[i] = Type.Missing;
      return a;
    }

    private static readonly object[][] OptionalParameters =
    {
      EmptyArray<object>.Instance,
      new[] {Type.Missing},
      new[] {Type.Missing, Type.Missing},
      new[] {Type.Missing, Type.Missing, Type.Missing},
      new[] {Type.Missing, Type.Missing, Type.Missing, Type.Missing},
      new[] {Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing},
    };

    #endregion

    #endregion

    #region De-serialization utilities

    public static object ReadValue(XmlReader reader,
      SimpleXmlSerializer settings, Type declType)
    {
      string id;
      var o = ReadReferenceId(reader, settings, out id);
      if (o != null)
      {
        reader.Skip();
        return o;
      }

      var type = declType;
      if (!type.IsValueType)
      {
        // check type attribute.
        var ta = reader.GetAttribute(AttrTypeName);
        var ty = ta != null ? settings.GetKnownType(ta) : null;
        if (ty != null) type = ty;
      }
      else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        type = type.GetGenericArguments()[0];
      }

      if (type.IsArray)
      {
        return ReadArray(reader, settings, type.GetElementType(), id);
      }

      if (reader.IsEmptyElement)
      {
        return GetEmptyValue(type, id, reader, settings);
      }
      if (type.IsEnum)
      {
        return SetId(Enum.Parse(type, reader.ReadElementContentAsString()),
          id, settings);
      }
      if (Type.GetTypeCode(type) != TypeCode.Object)
      {
        return SetId(reader.ReadElementContentAs(type, null), id, settings);
      }
      var custom = CustomSerializers.TryGet(type);
      if (custom != null)
      {
        return SetId(custom.ReadValue(reader, settings, type), id, settings);
      }

      if (TryReadCollection(reader, settings, type, id, out o))
        return o;

      o = SetId(FormatterServices.GetUninitializedObject(type), id, settings);
      var xs = o as IXmlSerializable;
      if (xs != null)
      {
        xs.ReadXml(reader);
        return o;
      }

      var info = settings.GetSerializationInfo(type);
      var result = info.ReadValue(reader, settings, o);
      if (type != declType)
      {
        result = Surrogates.Unwrap(result, settings);
      }
      return result;
    }

    private static object GetEmptyValue(Type type, string id,
      XmlReader reader, SimpleXmlSerializer settings)
    {
      var isNull = reader.GetAttribute(AttrNullName);
      if (isNull != null)
      {
        if (isNull != "true")
          throw new SerializationException("Attribute 'null' must have value 'true'");
        if (id != null)
          throw new SerializationException($"Non-empty id ({id}) associates with null object");
      }
      else // isNull == null
      {
        var custom = CustomSerializers.TryGet(type);
        if (custom != null)
        {
          var v = custom.ReadValue(reader, settings, type)
            ?? CreateInstance(GetInstanceType(type, settings));
          return SetId(v, id, settings);
        }
      }

      if (id == null)
      {
        reader.Skip();
        if (isNull != null)
        {
          return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        // isNull not specified
        if (type.IsArray)
        {
          // Empty array
          var rank = type.GetArrayRank();
          var dim = new int[rank];
          return Array.CreateInstance(type.GetElementType(), dim);
        }
        return CreateInstance(GetInstanceType(type, settings));
      }

      var o = SetId(FormatterServices.GetUninitializedObject(type), id, settings);
      var xs = o as IXmlSerializable;
      if (xs != null)
      {
        xs.ReadXml(reader);
        return o;
      }
      if (IsCollectionType(settings, type))
      {
        reader.Skip();
        return o;
      }
      var info = settings.GetSerializationInfo(type);
      return info.ReadValue(reader, settings, o);
    }

    private static bool TryReadCollection(XmlReader reader,
      SimpleXmlSerializer settings, Type type, string id,
      out object o)
    {
      o = null;
      Type ctype;
      var ta = TryGetCollectionType(settings, type, out type, out ctype);
      if (ta == null) return false;

      if (ctype.GetGenericTypeDefinition() == typeof(IList<>))
        o = ReadList(reader, settings, type, ta, id);
      else
        o = ReadDictionary(reader, settings, type, ta, id);
      return true;
    }

    private static Type GetInstanceType(Type declareType,
      SimpleXmlSerializer settings)
    {
      Type instanceType, interfaceType;
      TryGetCollectionType(settings, declareType,
        out instanceType, out interfaceType);
      if (instanceType.IsInterface)
      {
        throw new SerializationException(string.Format(
          "Cannot handle interface type: {0}", declareType));
      }
      if (instanceType.IsAbstract)
      {
        throw new SerializationException(string.Format(
          "Cannot handle abstract type: {0}", declareType));
      }
      return instanceType;
    }

    internal static Type[] TryGetCollectionType(
      SimpleXmlSerializer settings,
      Type decalreType,
      out Type instanceType,
      out Type interfaceType)
    {
      Type[] ta;
      instanceType = decalreType;
      interfaceType = settings.GetCollectionInterface(decalreType, out ta);
      if (interfaceType != null)
        return ta ?? interfaceType.GetGenericArguments();

      if (decalreType.IsGenericType)
      {
        var dtype = decalreType.GetGenericTypeDefinition();
        if (dtype == typeof(IList<>))
        {
          interfaceType = decalreType;
          ta = decalreType.GetGenericArguments();
          instanceType = typeof(List<>).MakeGenericType(ta);
          return ta;
        }
        if (dtype == typeof(IDictionary<,>))
        {
          interfaceType = decalreType;
          ta = decalreType.GetGenericArguments();
          instanceType = typeof(Dictionary<,>).MakeGenericType(ta);
          return ta;
        }
      }
      else if (decalreType == typeof(IList))
      {
        interfaceType = typeof(IList<object>);
        instanceType = typeof(List<object>);
        return new[] { typeof(object) };
      }
      else if (decalreType == typeof(IDictionary))
      {
        interfaceType = typeof(IDictionary<object, object>);
        instanceType = typeof(Dictionary<object, object>);
        return new[] { typeof(object), typeof(object) };
      }
      return null;
    }

    private static bool IsCollectionType(
      SimpleXmlSerializer settings, Type decalreType)
    {
      Type[] ta;
      var interfaceType = settings.GetCollectionInterface(decalreType, out ta);
      if (interfaceType != null) return true;

      if (decalreType.IsGenericType)
      {
        var dtype = decalreType.GetGenericTypeDefinition();
        return dtype == typeof(IList<>) ||
          dtype == typeof(IDictionary<,>);
      }
      return decalreType == typeof(IList) || decalreType == typeof(IDictionary);
    }

    private static object ReadReferenceId(XmlReader reader,
      SimpleXmlSerializer settings, out string identifier)
    {
      var tracker = settings.ReferenceTracker;
      var id = identifier = reader.GetAttribute(AttrRefName);
      if (id != null)
      {
        if (tracker != null)
        {
          var obj = tracker.GetObject(id);
          if (obj != null) return obj;
        }
        throw new SerializationException(string.Format(
          "Object with id {0} not found", id));
      }

      id = identifier = reader.GetAttribute(AttrIdName);
      if (id != null && tracker == null)
        settings.ReferenceTracker = new ReferenceTracker(settings);
      return null;
    }

    private static object SetId(object o, string id, SimpleXmlSerializer settings)
    {
      if (id != null) settings.ReferenceTracker.Add(id, o);
      return o;
    }

    private static object ReadArray(XmlReader reader,
      SimpleXmlSerializer settings, Type itemType, string id)
    {
      var dim = ReadArrayDimension(reader);
      if (dim != null && dim.Length == 1 && dim[0] > 0 && itemType == typeof(byte))
      {
        // bytes
        var total = dim[0];
        var bytes = new byte[total];
        var tmp = new byte[16];

        reader.ReadStartElement();
        int start = 0, count = total;
        while (true)
        {
          var buffer = bytes;
          int s = start, c= count;
          if (start >= total)
          {
            buffer = tmp;
            s = 0;
            c = tmp.Length;
          }
          var read = reader.ReadContentAsBase64(buffer, s, c);
          if (read <= 0) break;
          start += read;
          count -= read;
        }
        reader.ReadEndElement();
        return bytes;
      }

      var array = dim == null ? null : Array.CreateInstance(itemType, dim);
      if (id != null)
      {
        if (array == null)
          throw new SerializationException("Expect array length");
        SetId(array, id, settings);
      }
      if (reader.IsEmptyElement)
      {
        reader.Skip();
        return array;
      }

      if (dim != null)
      {
        IEnumerator<int[]> idx = new ArrayIndexEnumerator(dim);
        reader.ReadStartElement();
        while (reader.IsStartElement())
        {
          var attr = reader.GetAttribute(AttrSkipped);
          int skipped = string.IsNullOrEmpty(attr) ? 0 : int.Parse(attr);
          for (; skipped > 0; --skipped)
          {
            if (!idx.MoveNext())
              throw new SerializationException("more items than specified");
          }
          if (!idx.MoveNext())
            throw new SerializationException("more items than specified");
          var v = ReadValue(reader, settings, itemType);
          array.SetValue(v, idx.Current);
        }
        reader.ReadEndElement();
        return array;
      }

      var list = new ArrayList();
      reader.ReadStartElement();
      while (reader.IsStartElement())
      {
        var v = ReadValue(reader, settings, itemType);
        list.Add(v);
      }
      reader.ReadEndElement();
      return list.ToArray(itemType);
    }

    private static int[] ReadArrayDimension(XmlReader reader)
    {
      var sdim = reader.GetAttribute(DimensionName);
      if (string.IsNullOrEmpty(sdim)) return null;
      return sdim.Split(',').Select(int.Parse).ToArray();
    }

    private static object ReadList(XmlReader reader,
      SimpleXmlSerializer settings, Type listType, Type[] argTypes, string id)
    {
      var method = GetReadMethod(ReadList<List<int>, object>)
        .MakeGenericMethod(listType, argTypes[0]);
      var fn = (ReadObjectFn)Delegate.CreateDelegate(typeof(ReadObjectFn), method);
      return fn(reader, settings, id);
    }

    private static object ReadDictionary(XmlReader reader,
      SimpleXmlSerializer settings, Type objType, Type[] argTypes,
      string id)
    {
      var fn = GetReadDictionaryFn(objType, argTypes);
      return fn(reader, settings, id);
    }

    private delegate object ReadObjectFn(XmlReader reader,
      SimpleXmlSerializer settings, string id);

    private static TList ReadList<TList, TElem>(XmlReader reader,
      SimpleXmlSerializer settings, string id) where TList : IList
    {
      var list = Activator.CreateInstance<TList>();
      SetId(list, id, settings);
      reader.ReadStartElement();
      while (reader.IsStartElement())
      {
        var v = (TElem)ReadValue(reader, settings, typeof(TElem));
        list.Add(v);
      }
      reader.ReadEndElement();
      return list;
    }

    #endregion

    #region Serialize Dictionary

    private static WriteObjectFn GetWriteDictionaryFn(Type[] types)
    {
      MethodInfo method;
      if (types[0] == typeof(string))
      {
        method = GetWriteMethod(WriteStringDictionary<object>)
          .MakeGenericMethod(new[] { types[1] });
      }
      else
      {
        method = GetWriteMethod(WriteDictionary<object, object>)
          .MakeGenericMethod(types);
      }
      return (WriteObjectFn)Delegate.CreateDelegate(
        typeof(WriteObjectFn), method);
    }

    private static MethodInfo GetWriteMethod(
      Action<XmlWriter, SimpleXmlSerializer, object> action)
    {
      return action.Method.GetGenericMethodDefinition();
    }

    private static void WriteStringDictionary<TValue>(
      XmlWriter writer, SimpleXmlSerializer settings,
      object data)
    {
      var dict = (IDictionary<string, TValue>)data;
      foreach (var pair in dict)
      {
        writer.WriteStartElement(ElementName);
        writer.WriteAttributeString("key", pair.Key);
        WriteItemValue(writer, settings, typeof(TValue), pair.Value);
        writer.WriteEndElement();
      }
    }

    private static void WriteDictionary<TKey, TValue>(
      XmlWriter writer, SimpleXmlSerializer settings,
      object data)
    {
      var dict = (IDictionary<TKey, TValue>)data;
      foreach (var pair in dict)
      {
        writer.WriteStartElement(ElementName);
        WriteItem(writer, settings, KeyName,
          typeof(TKey), false, pair.Key);
        WriteItem(writer, settings, ValueName,
          typeof(TValue), false, pair.Value);
        writer.WriteEndElement();
      }
    }

    private static ReadObjectFn GetReadDictionaryFn(
      Type dicType, Type[] types)
    {
      MethodInfo method;
      if (types[0] == typeof(string))
      {
        method = GetReadMethod(ReadStringDictionary<
          Dictionary<string, object>, object>)
          .MakeGenericMethod(dicType, types[1]);
      }
      else
      {
        method = GetReadMethod(ReadDictionary<
          Dictionary<object, object>, object, object>)
          .MakeGenericMethod(dicType, types[0], types[1]);
      }
      return (ReadObjectFn)Delegate.CreateDelegate(
        typeof(ReadObjectFn), method);
    }

    private static MethodInfo GetReadMethod(
      Func<XmlReader, SimpleXmlSerializer, string, object> fn)
    {
      return fn.Method.GetGenericMethodDefinition();
    }

    private static object ReadStringDictionary<TDict, TValue>(
      XmlReader reader, SimpleXmlSerializer settings, string id)
      where TDict : IDictionary<string, TValue>
    {
      var dict = Activator.CreateInstance<TDict>();
      SetId(dict, id, settings);
      reader.ReadStartElement();
      while (reader.IsStartElement())
      {
        var k = reader.GetAttribute(KeyName);
        var v = (TValue)ReadValue(reader, settings, typeof(TValue));
        dict.Add(k, v);
      }
      reader.ReadEndElement();
      return dict;
    }

    private static object ReadDictionary<TDict, TKey, TValue>(
      XmlReader reader, SimpleXmlSerializer settings, string id)
      where TDict : IDictionary<TKey, TValue>
    {
      var dict = Activator.CreateInstance<TDict>();
      SetId(dict, id, settings);
      reader.ReadStartElement();
      while (reader.IsStartElement())
      {
        reader.ReadStartElement(); // <item>
        var k = default(TKey);
        if (reader.IsStartElement())
        {
          k = (TKey)ReadValue(reader, settings, typeof(TKey));
        }
        var v = default(TValue);
        if (reader.IsStartElement())
        {
          v = (TValue)ReadValue(reader, settings, typeof(TValue));
        }
        dict.Add(k, v);
        reader.ReadEndElement(); // </item>
      }
      reader.ReadEndElement();
      return dict;
    }

    #endregion

    #region constants

    private const string KeyName = "key", ValueName = "value",
      DimensionName = "dim", ElementName = "item",
      AttrIdName = "id", AttrRefName = "ref", AttrTypeName = "type",
      AttrSkipped = "skipped", AttrNullName = "null";

    #endregion
  }

}
