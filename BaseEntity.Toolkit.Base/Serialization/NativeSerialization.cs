/*
 * Copyright (c)    2002-2016. All rights reserved.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Xml;
using BaseEntity.Shared;
using DeserializeFn = System.Func<
  System.Runtime.Serialization.SerializationInfo,
  System.Runtime.Serialization.StreamingContext, object>;

namespace BaseEntity.Toolkit.Base.Serialization
{
  #region Type: FieldMap

  class FieldMap : IEnumerable<FieldMap.Entry>
  {
    public class Entry
    {
      private readonly MemberInfo _field;
      public readonly string OriginalName;
      public readonly string MappedName;

      internal Entry(string originalName,
        string mappedName, Type fieldType)
      {
        OriginalName = originalName;
        MappedName = mappedName;
        _field = fieldType;
      }

      public Type FieldType
      {
        get { return (_field as Type) ?? ((FieldInfo)_field).FieldType; }
      }
    }

    private readonly Dictionary<string, Entry>
      _byOriginalName = new Dictionary<string, Entry>(),
      _byMappedName = new Dictionary<string, Entry>();

    public void Add(string originalName, string mappedName, Type fieldType)
    {
      var entry = new Entry(originalName, mappedName, fieldType);
      _byOriginalName.Add(originalName, entry);
      _byMappedName.Add(mappedName, entry);
    }

    public KeyValuePair<string, Type> FromMapped(string mappedName)
    {
      Entry entry;
      if (_byMappedName.TryGetValue(mappedName, out entry))
        return new KeyValuePair<string, Type>(entry.OriginalName, entry.FieldType);
      return new KeyValuePair<string, Type>(mappedName, typeof(object));
    }

    public KeyValuePair<string, Type> FromOriginal(string originalName)
    {
      Entry entry;
      if (_byOriginalName.TryGetValue(originalName, out entry))
        return new KeyValuePair<string, Type>(entry.MappedName, entry.FieldType);
      return new KeyValuePair<string, Type>(originalName, typeof(object));
    }

    public IEnumerator<FieldMap.Entry> GetEnumerator()
    {
      return _byOriginalName.Values.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return _byOriginalName.Values.GetEnumerator();
    }
  }

  #endregion

  class NativeObjectSerializer : ISimpleXmlSerializer
  {
    internal static NativeObjectSerializer Instance = new NativeObjectSerializer();

    public bool CanHandle(Type type)
    {
      Debug.Assert(type != null);
      // ReSharper disable once AssignNullToNotNullAttribute
      return type.GetInterface(typeof(INativeSerializable).FullName, false)
        != null;
    }

    public object ReadValue(XmlReader reader,
      SimpleXmlSerializer settings, Type type)
    {
      if (reader.IsEmptyElement)
      {
        reader.Skip();
        return null;
      }

      // Get Serialization info.
      var ctx = new StreamingContext();
      var cvt = new FormatterConverter();
      var info = new SerializationInfo(type, cvt);
      var dict = settings.GetFieldMap(type);
      reader.ReadStartElement();
      while (reader.IsStartElement())
      {
        var name = reader.LocalName;
        var pair = dict != null
          ? dict.FromMapped(name)
          : new KeyValuePair<string, Type>(name, typeof(object));

        var value = SimpleXmlSerializationUtility
          .ReadValue(reader, settings, pair.Value);
        info.AddValue(pair.Key, value);
      }
      reader.ReadEndElement();

      var fn = GetNativeDeserializeMethod(type);
      var dst = fn(info, ctx);
      return dst;
    }

    public void WriteValue(XmlWriter writer,
      SimpleXmlSerializer settings, object obj)
    {
      var data = (INativeSerializable)obj;

      // Get Serialization info.
      var ctx = new StreamingContext();
      var cvt = new FormatterConverter();
      var type = data.GetType();
      var info = new SerializationInfo(type, cvt);
      var dict = settings.GetFieldMap(type);
      data.GetObjectData(info, ctx);
      var enumerator = info.GetEnumerator();
      while (enumerator.MoveNext())
      {
        var name = enumerator.Name;
        var pair = dict != null
          ? dict.FromOriginal(name)
          : new KeyValuePair<string, Type>(name, typeof(object));
        SimpleXmlSerializationUtility.WriteItem(writer, settings,
          pair.Key, pair.Value, false, enumerator.Value);
      }
    }

    #region Create native deserialize method

    /// <summary>
    /// Gets the native deserialization method.
    /// </summary>
    /// <param name="type">The managed type of the native object</param>
    /// <returns>DeserializeFn</returns>
    /// <exception cref="BaseEntity.Shared.FastCloneException"></exception>
    /// <exclude>For internal use only</exclude>
    private static DeserializeFn GetNativeDeserializeMethod(Type type)
    {
      // De-serialization to construct a clone.
      DeserializeFn fn;
      if (!deserFns_.TryGetValue(type, out fn))
      {
        var ctor = type.GetConstructor(
          BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.NonPublic | BindingFlags.Public, null,
          SerializationParameters, null);
        if (ctor == null)
        {
          throw new FastCloneException(String.Format(
            "Deserialization constructor not found for type {0}",
            type));
        }
        fn = CreateConstructorCaller(type, ctor);
        deserFns_.AddOrUpdate(type, fn, (t, f) => f);
      }
      return fn;
    }

    private static DeserializeFn CreateConstructorCaller(
      Type type, ConstructorInfo ctor)
    {
      var dm = new DynamicMethod(String.Format("{0}_Construct_dyn",
        type.Name), typeof(object), SerializationParameters,
        type.Module, true);
      var il = dm.GetILGenerator(); //.EmitConstructorCall(ctor);
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Ldarg_1);
      il.Emit(OpCodes.Newobj, ctor);
      il.Emit(OpCodes.Ret);
      var fn = (DeserializeFn)dm.CreateDelegate(typeof(DeserializeFn));
      return fn;
    }

    private static readonly Type[] SerializationParameters =
      new[] { typeof(SerializationInfo), typeof(StreamingContext) };

    private static readonly ConcurrentDictionary<Type, DeserializeFn>
      deserFns_ = new ConcurrentDictionary<Type, DeserializeFn>();

    #endregion
  }

}
