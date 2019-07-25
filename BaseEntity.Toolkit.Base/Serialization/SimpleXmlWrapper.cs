/*
 * Copyright (c)    2002-2014. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

using Utility = BaseEntity.Toolkit.Base.Serialization.SimpleXmlSerializationUtility;

namespace BaseEntity.Toolkit.Base.Serialization
{
  internal abstract class SimpleXmlSerializationInfo
  {
    #region Instance method

    internal static readonly StreamingContext StreamingContext
      = new StreamingContext();

    internal readonly Dictionary<string, FieldInfo> Fields;

    protected SimpleXmlSerializationInfo(Type type, SimpleXmlSerializer settings)
    {
      Debug.Assert(settings != null);
      Fields = GetFields(type, settings);
    }

    public abstract void OnDeserialized(object data);
    public abstract void OnDeserializing(object data);
    public abstract void OnSerialized(object data);
    public abstract void OnSerializing(object data);

    public abstract object ReadValue(XmlReader reader,
      SimpleXmlSerializer settings, object uninitializedObject);

    public abstract void WriteValue(XmlWriter writer,
      SimpleXmlSerializer settings, object data);

    #endregion

    #region Static constructors

    internal static SimpleXmlSerializationInfo Create(
      Type type, SimpleXmlSerializer settings)
    {
      Func<SimpleXmlSerializer, SimpleXmlSerializationInfo<object>>
        lambda = CreateInstance<object>;
      var method = lambda.Method.GetGenericMethodDefinition()
        .MakeGenericMethod(type);
      var obj = method.Invoke(null, new object[] { settings });
      return (SimpleXmlSerializationInfo) obj;
    }

    private static SimpleXmlSerializationInfo<T> CreateInstance<T>(
      SimpleXmlSerializer settings)
    {
      return new SimpleXmlSerializationInfo<T>(settings);
    }

    #endregion

    #region Collect fields

    public static Dictionary<string, FieldInfo> GetFields(
      Type type, SimpleXmlSerializer settings)
    {
      var fields = new Dictionary<string, FieldInfo>();
      GetFields(type, 0, settings, fields);
      return fields;
    }

    private static void GetFields(Type type, int count,
      SimpleXmlSerializer settings,
      Dictionary<string, FieldInfo> fields)
    {
      const BindingFlags flags = BindingFlags.DeclaredOnly
        | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
      var infos = type.GetFields(flags);
      for (int i = 0; i < infos.Length; ++i)
      {
        var fi = infos[i];
        if (fi.GetCustomAttribute<NonSerializedAttribute>() != null)
        {
          continue;
        }
        var name = settings.GetMappedName(fi);
        if (string.IsNullOrEmpty(name)) continue; // ignored

        if (fields.ContainsKey(name))
        {
          for (int j = 0; j < count; ++j)
          {
            name = "Base." + name;
            if (!fields.ContainsKey(name))
              break;
          }
        }
        fields.Add(name, fi);
      }
      type = type.BaseType;
      if (type != null && type != typeof(object))
        GetFields(type, ++count, settings, fields);
    }

    #endregion
  }

  internal sealed class SimpleXmlSerializationInfo<T> : SimpleXmlSerializationInfo
  {
    #region data and constructor

    private static readonly ParameterExpression[] Parameters =
    {
      Expression.Parameter(typeof(T), "target"), 
      Expression.Parameter(typeof(StreamingContext), "streamingContext"), 
    };

    private readonly Action<T, StreamingContext>
      _onSerializing, _onSerialized, _onDeserializing, _onDeserialized;

    public SimpleXmlSerializationInfo(SimpleXmlSerializer settings)
      : base(typeof(T), settings)
    {
      _onSerializing = GetAction(typeof(T), typeof(OnSerializingAttribute));
      _onSerialized = GetAction(typeof(T), typeof(OnSerializedAttribute));
      _onDeserializing = GetAction(typeof(T), typeof(OnDeserializingAttribute));
      _onDeserialized = GetAction(typeof(T), typeof(OnDeserializedAttribute));
    }

    private static Action<T, StreamingContext> GetAction(Type type, Type attr)
    {
      List<MethodCallExpression> list = null;
      do
      {
        var methods = type.GetMethods(
          BindingFlags.Instance | BindingFlags.DeclaredOnly
          | BindingFlags.NonPublic | BindingFlags.Public);
        for (int i = 0; i < methods.Length; ++i)
        {
          var mi = methods[i];
          if (Attribute.IsDefined(mi, attr))
          {
            var pars = mi.GetParameters();
            if (pars.Length != 1 || (pars[0].ParameterType
              != typeof(StreamingContext)))
            {
              throw new SerializationException(String.Format(
                "Invalid signature of method marked [{0}]",
                attr.Name));
            }
            if (list == null)
            {
              list = new List<MethodCallExpression>();
            }
            list.Add(Expression.Call(Parameters[0], mi, Parameters[1]));
            break;
          }
        }
        type = type.BaseType;
      } while (type != null);

      if (list == null) return null;
      var body = list.Count == 1
        ? (Expression) list[0]
        : Expression.Block(list);
      return Expression.Lambda<Action<T, StreamingContext>>(body, Parameters).Compile();
    }

    #endregion

    #region Override methods

    public override void OnDeserialized(object data)
    {
      if (_onDeserialized != null)
        _onDeserialized((T)data, StreamingContext);
    }

    public override void OnDeserializing(object data)
    {
      if (_onDeserializing != null)
        _onDeserializing((T)data, StreamingContext);
    }

    public override void OnSerialized(object data)
    {
      if (_onSerialized != null)
        _onSerialized((T)data, StreamingContext);
    }

    public override void OnSerializing(object data)
    {
      if (_onSerializing != null)
        _onSerializing((T)data, StreamingContext);
    }

    public override object ReadValue(XmlReader reader,
      SimpleXmlSerializer settings, object uninitializedObject)
    {
      if (uninitializedObject == null) return null;

      var data = uninitializedObject;
      if (_onDeserializing != null)
        _onDeserializing((T)data, StreamingContext);

      var fields = Fields;
      if (fields.Count == 0)
      {
        reader.Skip();
      }
      else
      {
        var infos = new List<MemberInfo>();
        var values = new List<object>();
        reader.ReadStartElement();
        while (reader.IsStartElement())
        {
          var name = reader.LocalName;
          FieldInfo fi;
          if (!fields.TryGetValue(name, out fi))
          {
            throw new SerializationException(
              $"{name}: unknown field in type {typeof(T)}");
          }
          var v = SimpleXmlSerializationUtility.ReadValue(reader, settings, fi.FieldType);
          values.Add(v);
          infos.Add(fi);
        }
        reader.ReadEndElement();

        data = FormatterServices.PopulateObjectMembers(
          data, infos.ToArray(), values.ToArray());
      }

      _onDeserialized?.Invoke((T)data, StreamingContext);

      return data;
    }

    public override void WriteValue(XmlWriter writer,
      SimpleXmlSerializer settings, object data)
    {
      if (data == null) return;

      if (_onSerializing != null)
        _onSerializing((T)data, StreamingContext);

      var fields = Fields;
      foreach (var pair in fields)
      {
        SimpleXmlSerializationUtility.WriteItem(writer, settings, pair.Key,
          pair.Value.FieldType, true, pair.Value.GetValue(data));
      }

      if (_onSerialized != null)
        _onSerialized((T)data, StreamingContext);
    }

    #endregion
  }

}
