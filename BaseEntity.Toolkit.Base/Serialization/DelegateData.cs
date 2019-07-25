using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base.Serialization
{
  using Utility = SimpleXmlSerializationUtility;


  internal static class Surrogates
  {
    public static bool Wrap(
      object obj, SimpleXmlSerializer settings,
      out object result, out Type resultType)
    {
      if (obj == null)
      {
        resultType = null;
        result = null;
        return false;
      }

      if (obj is Delegate fn)
      {
        fn = SerializingDelegates.Unwrap(fn);
        resultType = typeof(DelegateData);
        result = new DelegateData(fn, settings);
        return true;
      }
      if (obj is Type type)
      {
        resultType = typeof(TypeData);
        result = TypeData.ForceWrap(type, settings);
        return true;
      }

      if (ObjectData.Wrap(obj, settings) is ObjectData wrapper)
      {
        resultType = typeof(ObjectData);
        result = wrapper;
        return true;
      }

      resultType = null;
      result = obj;
      return false;
    }

    public static object Unwrap(object data, SimpleXmlSerializer settings)
    {
      if (data is DelegateData dd)
      {
        return dd.GetDelegate(settings);
      }
      if (data is TypeData td)
      {
        return TypeData.Unwrap(td, settings);
      }
      if (data is ObjectData od)
      {
        return ObjectData.Unwrap(od, settings);
      }
      return data;
    }

  }

  // This type should be used by serialization only.
  // All fields start with lower case letters.
  // ReSharper disable InconsistentNaming
  internal sealed class DelegateData
  {
    internal DelegateData(
      Delegate fn,
      SimpleXmlSerializer settings)
    {
      delegateType = Utility.GetTypeName(fn.GetType(), settings);
      method = new MethodData(fn.Method, settings);
      target = fn.Target;
    }

    internal Delegate GetDelegate(SimpleXmlSerializer settings)
    {
      var deleType = settings.GetKnownType(delegateType);
      return Delegate.CreateDelegate(deleType,
        target, method.GetMethod(settings));
    } 

    private readonly string delegateType;
    private readonly object target;
    private readonly MethodData method;
  }

  internal sealed class MethodData
  {
    public MethodData(MethodInfo method,
      SimpleXmlSerializer settings)
    {
      var typeInfo = method.DeclaringType;
      if (typeInfo == null)
      {
        throw new NotSupportedException(
          "Method without declaring type not supported yet");
      }

      if (TypeData.NeedWrap(typeInfo))
        declaringTypeInfo = TypeData.ForceWrap(typeInfo, settings);
      else
        declaringType = Utility.GetTypeName(typeInfo, settings);

      name = method.Name;
      @static = method.IsStatic;

      var pars = method.GetParameters();
      if (pars.Length == 0) return;

      parameterTypes = Array.ConvertAll(pars,
        p => Utility.GetTypeName(p.ParameterType, settings));

      if (pars.Any(p => p.IsOut || p.IsRetval))
      {
        throw new NotSupportedException(
          "out/ref parameters not supported yet");
      }
    }

    public MethodInfo GetMethod(SimpleXmlSerializer settings)
    {
      var declType = declaringTypeInfo != null
        ? TypeData.Unwrap(declaringTypeInfo, settings)
        : settings.GetKnownType(declaringType);
      var parTypes = parameterTypes.IsNullOrEmpty()
        ? EmptyArray<Type>.Instance
        : Array.ConvertAll(parameterTypes, settings.GetKnownType);
      var flag = BindingFlags.DeclaredOnly |
        BindingFlags.Public | BindingFlags.NonPublic |
        (@static ? BindingFlags.Static : BindingFlags.Instance);
      var method = declType.GetMethod(name, flag, null, parTypes, null);
      return method;
    }

    private readonly string declaringType;
    private readonly TypeData declaringTypeInfo;
    private readonly bool @static;
    private readonly string name;
    private readonly string[] parameterTypes;
  }

  internal sealed class ObjectData
  {
    public static object Wrap(object data, SimpleXmlSerializer settings)
    {
      if (data == null) return null;
      var type = data.GetType();
      return TypeData.NeedWrap(type)
        ? new ObjectData(data, type, settings) : data;
    }

    public static object Unwrap(ObjectData data, SimpleXmlSerializer settings)
    {
      var type = TypeData.Unwrap(data.typeInfo, settings);
      var result = FormatterServices.GetUninitializedObject(type);
      var fields = data.fields;
      if (fields == null) return result;
      var count = fields.Count;
      if (count == 0) return result;

      var infos = new List<MemberInfo>();
      var objects = new List<object>();
      foreach (var field in fields)
      {
        var v = field.Value;
        if (v == null) continue;
        objects.Add(v);
        infos.Add(type.GetField(field.Key, Flags));
      }
      return FormatterServices.PopulateObjectMembers(
        result, infos.ToArray(), objects.ToArray());
    }

    private ObjectData(object data, Type type, SimpleXmlSerializer settings)
    {
      Debug.Assert(data != null);
      Debug.Assert(type != null);

      var context = new StreamingContext();
      typeInfo = TypeData.ForceWrap(type, settings);
      var members = type.GetMembers(Flags);
      if (members.Length == 0) return;

      Dictionary<string, object> bag = null;
      foreach (var fi in members.OfType<FieldInfo>())
      {
        var v = fi.GetValue(data);
        if (v == null) continue;
        //var check = type.GetField(fi.Name, Flags)
        (bag ?? (bag = new Dictionary<string, object>()))
          .Add(fi.Name, v.WrapSerializable(context));
      }

      fields = bag;
    }

    private const BindingFlags Flags = BindingFlags.FlattenHierarchy
      | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly TypeData typeInfo;
    private readonly Dictionary<string, object> fields;
  }
}
