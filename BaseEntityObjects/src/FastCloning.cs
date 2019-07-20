// ***********************************************************************
// Assembly         : WebMathTraining.Shared
// Author           : hehuij
// Created          : 04-26-2012
//
// Last Modified By : hehuij
// Last Modified On : 05-01-2013
// ***********************************************************************
// <copyright file="FastCloning.cs" company="WebMathTraining">
//     WebMathTraining. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

using CloneFieldsFn = System.Func<object,
  BaseEntity.Shared.FastCloningContext, object>;
using DeserializeFn = System.Func<
  System.Runtime.Serialization.SerializationInfo,
  System.Runtime.Serialization.StreamingContext, object>;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Fields marked [NoClone] will be cleared or
  /// copied by reference in the cloned object.
  /// </summary>
  [Serializable, AttributeUsage(AttributeTargets.Field)]
  public sealed class NoCloneAttribute : Attribute
  {
    /// <summary>
    /// Gets or sets a value indicating whether to keep the field value
    /// instead of clear it in the cloned copy.
    /// </summary>
    /// <value><c>true</c> if keep; otherwise, <c>false</c>.</value>
    public bool Keep { get; set; }
  }

  /// <summary>
  /// Methods marked [BeforeFieldsCloning] will be called on
  /// the CLONED object after member-wise cloning but BEFORE
  /// the fields are cloned.  Hence when it is called,
  /// this object is the new copy, but all the fields have their
  /// original references or values.
  /// </summary>
  [Serializable, AttributeUsage(AttributeTargets.Method)]
  public sealed class BeforeFieldsCloningAttribute : Attribute
  {
  }

  /// <summary>
  /// Methods marked [AfterFieldsCloned] will be called on
  /// the CLONED object after all the fields being cloned or
  /// cleared based on custom attributes.  This is the place
  /// to do some initialization on the new object.
  /// </summary>
  [Serializable, AttributeUsage(AttributeTargets.Method)]
  public sealed class AfterFieldsClonedAttribute : Attribute
  {
  }

  /// <summary>
  /// Cloning context.  Currently it has only a dictionary mapping
  /// the original object to its cloned.
  /// </summary>
  public class FastCloningContext : Dictionary<object, object>
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="FastCloningContext" /> class.
    /// </summary>
    public FastCloningContext() : base(TheDefaultComparer)
    {}

    /// <summary>
    /// A dictionary mapping the original object to its cloned counterpart.
    /// </summary>
    /// <value>The references.</value>
    public Dictionary<object, object> References {get { return this; }}

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
  }

  /// <summary>
  /// Fine cloning exception
  /// </summary>
  public class FastCloneException : Exception
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="FastCloneException" /> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public FastCloneException(string message):base(message)
    {}
  }

  /// <summary>
  /// A static class implementing the fine cloning.
  /// </summary>
  public static class FastCloning
  {
    #region Public Interface

    /// <summary>
    /// Performing fine cloning which preserves all the references equality
    /// and also allows user-defined object mapping.
    /// </summary>
    /// <typeparam name="T">The type of the object being cloned.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="ctx">The cloning context containing user defined object mapping (null if no user defined mapping).</param>
    /// <returns>The cloned object.</returns>
    /// <remarks>Before calling this function, the user can set some object mapping
    /// in the cloning context.  For example, if the mapping of object A to
    /// object B is set, then all the references to A in the source object
    /// will be replaced by the references to B in the cloned object.  In
    /// this way, the user can fine tune the cloning at the call site.</remarks>
    public static T FastClone<T>(this T source, FastCloningContext ctx)
    {
      if (NoNeedClone(typeof(T))) { return source; }

      if (ctx == null) ctx = new FastCloningContext();

      if (typeof(T).IsValueType)
      {
        T cloned = source;
        return (T)CloneFields(cloned, ctx);
      }
      return (T)MakeClone(source, ctx);
    }
    #endregion

    #region implementation details

    private static object MakeClone(object src, FastCloningContext ctx)
    {
      if (src == null) return null;

      object cloned;
      if (ctx.References.TryGetValue(src, out cloned)) return cloned;

      var native = src as INativeSerializable;
      if (native != null)
      {
        cloned = CloneNativeSerializable(native);
        ctx.References.Add(src, cloned);
        return cloned;
      }

      var type = src.GetType();
      if (NoNeedClone(type))
      {
        return type.IsValueType ? MemberwiseCloneFn(src) : src;
      }

      cloned = MemberwiseCloneFn(src);
      ctx.References.Add(src, cloned);
      return CloneFields(cloned, type, ctx);
    }

    #region Clone fields

    private static object CloneFields(object obj, FastCloningContext ctx)
    {
      return obj == null ? null : CloneFields(obj, obj.GetType(), ctx);
    }

    private static object CloneFields(object obj, Type type, FastCloningContext ctx)
    {
      if (type.IsArray)
      {
        return CloneArrayElements(type.GetElementType(), (Array)obj, ctx);
      }

      var fn = GetCloneFieldsFn(type);
      return fn == DoNothingFn ? obj : fn(obj, ctx);
    }

    private static object Pass(object o, FastCloningContext refs) { return o; }
    private static readonly CloneFieldsFn DoNothingFn = Pass;

    #region A map from types to clone methods
    // The stored methods is a dictionary mapping types to clone methods,
    // It supports multiple concurrent readers but only one writer.
    private static readonly ConcurrentDictionary<Type, CloneFieldsFn>
      storedMethods_ = new ConcurrentDictionary<Type, CloneFieldsFn>();

    /// <summary>
    /// Add a clone fields method for a type.
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="fn">The clone fields method</param>
    public static void AddCloneFieldsFn(Type type, CloneFieldsFn fn)
    {
      if (fn == null) fn = DoNothingFn;
      storedMethods_.AddOrUpdate(type, fn, (t, f) => f);
    }

    private static CloneFieldsFn GetCloneFieldsFn(Type type)
    {
      CloneFieldsFn fn;
      if (!storedMethods_.TryGetValue(type, out fn))
      {
        fn = CreateCloneFieldsFn(type);
        storedMethods_.AddOrUpdate(type, fn, (t, f) => f);
      }
      return fn;
    }
    #endregion

    #region Emit the assembly codes

    private static CloneFieldsFn CreateCloneFieldsFn(Type type)
    {
      // Look for the fields to clone and the associated custom methods.
      // The return value is null if no fields need to clone (a simple
      // memberwise clone is enough).
      var fci = FineCloneInfo.GetFineCloneInfo(type);
      if (fci == null) return DoNothingFn;

      // Create a delegate based on the infomation we obtained.
      var dm = new DynamicMethod(String.Format("{0}_CloneFields_dyn",
        type.Name), typeof(object), FineCloneParameters, type.Module, true);
      dm.GetILGenerator().EmitCloneFieldsMethodBody(fci);
      var fn = (CloneFieldsFn)dm.CreateDelegate(typeof(CloneFieldsFn));
      return fn;
    }

    // Set up once the things we need.
    private static readonly Type[] FineCloneParameters =
      new[] { typeof(object), typeof(FastCloningContext) };
    private static readonly MethodInfo valueClone = typeof(FastCloning)
      .GetMethod("CloneFields", BindingFlags.Static | BindingFlags.NonPublic,
      null, FineCloneParameters, null);
    private static readonly MethodInfo objectClone = typeof(FastCloning)
      .GetMethod("MakeClone", BindingFlags.Static | BindingFlags.NonPublic,
      null, FineCloneParameters, null);

    // Generate IL codes for efficiency.
    private static void EmitCloneFieldsMethodBody(
      this ILGenerator il, FineCloneInfo fci)
    {
      var type = fci.Type;

      // Create a local variable to hold the object of the specific type.
      LocalBuilder value = null;
      if (type.IsValueType)
      {
        value = il.DeclareLocal(type);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Unbox_Any, type);
        il.Emit(OpCodes.Stloc, value);
      }

      // Call the custom methods marked [BeforeFieldsCloning]
      il.EmitCustomCalls(value, fci.BeforeFieldsCloning);

      // Now for each field to clone or to clear....
      var fields = fci.FieldsToClone;
      for (int i = 0; i < fields.Length; ++i)
      {
        var fi = fields[i].FieldInfo;
        var ft = fi.FieldType;

        if (fields[i].ClearOnly)
        {
          // We clear the fields marked [NoClone] without Keep.
          il.EmitLoadObj(value); // for set field.
          if (ft.IsValueType)
          {
            il.Emit(OpCodes.Ldflda, fi);
            il.Emit(OpCodes.Initobj, ft);
          }
          else
          {
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stfld, fi);
          }
          continue;
        }

        // We need to make clone of this field.
        il.EmitLoadObj(value); // for set field.
        {
          // First load the field onto the stack.
          il.EmitLoadObj(value);
          il.Emit(OpCodes.Ldfld, fi);
          // For value type, convert it to object.
          if (ft.IsValueType)
          {
            il.Emit(OpCodes.Box, ft);
          }
          // Then load the second parameter
          il.Emit(OpCodes.Ldarg_1);
          // For value type, convert from object to value.
          if (ft.IsValueType)
          {
            // Call the appropriate clone method.
            il.Emit(OpCodes.Call, valueClone);
            il.Emit(OpCodes.Unbox_Any, ft);
          }
          else
          {
            // Call the appropriate clone method.
            il.Emit(OpCodes.Call, objectClone);
          }
        }
        // Set the new value to the field.
        // Note that this object is already on stack.
        il.Emit(OpCodes.Stfld, fi);
      }

      // Call the custom methods marked [AfterFieldsCloned]
      il.EmitCustomCalls(value, fci.AfterFieldsCloned);

      // All done!
      il.EmitReturnObj(value);
    }

    private static void EmitLoadObj(this ILGenerator il, LocalBuilder value)
    {
      if (value != null)
        il.Emit(OpCodes.Ldloca_S, value);
      else
        il.Emit(OpCodes.Ldarg_0);
    }
    private static void EmitReturnObj(this ILGenerator il, LocalBuilder value)
    {
      if (value != null)
      {
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Box, value.LocalType);
      }
      else
      {
        il.Emit(OpCodes.Ldarg_0);
      }
      il.Emit(OpCodes.Ret);
    }
    private static void EmitNewStreamingContext(this ILGenerator il)
    {
      il.Emit(OpCodes.Ldc_I4_S, (int)StreamingContextStates.Clone);
      il.Emit(OpCodes.Ldarg_1); // this is the dictionary of references
      il.Emit(OpCodes.Newobj, typeof(StreamingContext).GetConstructor(
        new[] { typeof(StreamingContextStates), typeof(object) }));//known not null.
    }
    private static void EmitCustomCalls(this ILGenerator il,
      LocalBuilder value, MethodInfo[] methods)
    {
      for (int i = methods.Length - 1; i >= 0; --i)
      {
        il.EmitLoadObj(value);
        var mi = methods[i];
        // Method is one of 
        //  (1) parameterless instance method;
        //  (2) an instance method taking StreamingContext as the only parameter;
        //  (3) an static method taking the object as the only parameter.
        var p = mi.GetParameters();
        if (p.Length == 1 && p[0].ParameterType == typeof(StreamingContext))
        {
          il.EmitNewStreamingContext();
        }
        il.Emit(mi.IsStatic ? OpCodes.Call : OpCodes.Callvirt, mi);
        if (mi.ReturnType != typeof(void))
          il.Emit(OpCodes.Pop); // discard the returned result.
      }
    }

    #endregion

    #region FineCloneInfo
    /// <summary>
    /// Determine whether a type apparently has no need to clone.
    /// </summary>
    private static bool NoNeedClone(Type type)
    {
      return type.IsPrimitive || type == typeof(string) || type.IsEnum
        || typeof(MemberInfo).IsAssignableFrom(type); // invariant types 
    }
    /// <summary>
    /// FiledInfo plus an indicator for clearing or cloning the field.
    /// </summary>
    private struct FineCloneField
    {
      internal FieldInfo FieldInfo { get; set; }
      internal bool ClearOnly { get; set; }
      public override string ToString()
      {
        return FieldInfo.ToString();
      }
    }
    /// <summary>
    /// A class representing the infomation needed by fine cloning.
    /// </summary>
    private class FineCloneInfo
    {
      internal Type Type { get; private set; }
      internal FineCloneField[] FieldsToClone { get; private set; }
      internal MethodInfo[] BeforeFieldsCloning { get; private set; }
      internal MethodInfo[] AfterFieldsCloned { get; private set; }
      internal static FineCloneInfo GetFineCloneInfo(Type type)
      {
        var fields = FindFieldsToClone(type);
        var befors = GetCustomMethods(type, typeof(BeforeFieldsCloningAttribute));
        var afters = GetCustomMethods(type, typeof(AfterFieldsClonedAttribute));
        if (fields.Length == 0 && befors.Length == 0 && afters.Length == 0)
          return null;
        if (afters.Length == 0)
        {
          var m = GetDictionaryRebuildMethod(type);
          if (m != null) afters = new[] { m };
        }
        return new FineCloneInfo
        {
          Type = type,
          FieldsToClone = fields,
          BeforeFieldsCloning = befors,
          AfterFieldsCloned = afters,
        };
      }
      private static readonly FieldInfo[] EmptyFields = new FieldInfo[0];

      private static readonly BindingFlags Flags = BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

      private static IEnumerable<FieldInfo> GetAllFields(Type type)
      {
        if(type==null) return EmptyFields;
        return type.GetFields(Flags).Union(GetAllFields(type.BaseType));
      }

      private static FineCloneField[] FindFieldsToClone(Type type)
      {
        var fields = GetAllFields(type).ToArray();
        var list = new List<FineCloneField>();
        for (int i = 0; i < fields.Length; ++i)
        {
          var fi = fields[i];
          var a = (NoCloneAttribute)Attribute.GetCustomAttribute(
            fi, typeof(NoCloneAttribute));
          if (a != null)
          {
            if (!a.Keep)
              list.Add(new FineCloneField { FieldInfo = fi, ClearOnly = true });
            continue;
          }
          if (NoNeedClone(fi.FieldType)) continue;
          list.Add(new FineCloneField { FieldInfo = fi, ClearOnly = false });
        }
        return list.ToArray();
      }

      private static MethodInfo[] GetCustomMethods(Type type, Type attr)
      {
        var list = new List<MethodInfo>();
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
              if ((pars.Length == 1 && pars[0].ParameterType
                != typeof(StreamingContext)) || pars.Length > 1)
              {
                throw new FastCloneException(String.Format(
                  "Invalid signature of method marked [{0}]",
                  attr.Name));
              }
              list.Add(mi);
              break;
            }
          }
          type = type.BaseType;
        } while (type != null);
        return list.ToArray();
      }
    }// class FineCloneInfo
    #endregion

    #endregion

    #region Clone Array Elements
    private static Array CloneArrayElements(Type elemType,
      Array array, FastCloningContext refs)
    {
      if (NoNeedClone(elemType)) return array;
      if (elemType.IsValueType)
      {
        var fn = GetCloneFieldsFn(elemType);
        if (fn == DoNothingFn) return array;
        return CloneElements(array, refs, fn);
      }
      return CloneElements(array, refs, MakeClone);
    }

    private static Array CloneElements(Array array,
      FastCloningContext refs, CloneFieldsFn cloneElement)
    {
      int rank = array.Rank;
      if (rank == 1)
      {
        int end0 = array.GetUpperBound(0);
        for (int i = array.GetLowerBound(0); i <= end0; ++i)
        {
          array.SetValue(cloneElement(array.GetValue(i), refs), i);
        }
        return array;
      }
      else if (rank == 2)
      {
        int end0 = array.GetUpperBound(0);
        int end1 = array.GetUpperBound(1);
        for (int i = array.GetLowerBound(0); i <= end0; ++i)
          for (int j = array.GetLowerBound(1); j <= end1; ++j)
          {
            array.SetValue(cloneElement(array.GetValue(i, j), refs), i, j);
          }
        return array;
      }
      else if (rank == 3)
      {
        int end0 = array.GetUpperBound(0);
        int end1 = array.GetUpperBound(1);
        int end2 = array.GetUpperBound(2);
        for (int i = array.GetLowerBound(0); i <= end0; ++i)
          for (int j = array.GetLowerBound(1); j <= end1; ++j)
            for (int k = array.GetLowerBound(2); k <= end2; ++k)
            {
              array.SetValue(cloneElement(array.GetValue(
                i, j, k), refs), i, j, k);
            }
        return array;
      }
      else // general n-dimensional array
      {
        int[] index = new int[rank], low = new int[rank], high = new int[rank];
        for (int i = 0; i < rank; ++i)
        {
          index[i] = low[i] = array.GetLowerBound(i);
          high[i] = array.GetUpperBound(i);
        }
        int d = rank - 1;
        while (true)
        {
          array.SetValue(cloneElement(array.GetValue(index), refs), index);
          if (++index[d] <= high[d]) continue;
          int i = d;
          do
          {
            index[i] = low[i];
            if (--i < 0) return array;
          } while (++index[i] > high[i]);
        }
      }
    }

    #endregion

    #region Clone dictionary

    private static void RebuildGenericDictionary<T>(object cloned)
    {
      var dict = cloned as ICollection<T>;
      if (dict == null) return;
      int count = dict.Count;
      if (count == 0) return;
      var array = new T[count];
      dict.CopyTo(array, 0);
      dict.Clear();
      for (int i = 0; i < count; ++i)
        dict.Add(array[i]);
      return;
    }

    private static void RebuildLegacyDictionary(object cloned)
    {
      var dict = cloned as System.Collections.IDictionary;
      if (dict == null) return;
      int count = dict.Count;
      if (count == 0) return;
      var array = new System.Collections.DictionaryEntry[count];
      dict.CopyTo(array, 0);
      dict.Clear();
      for (int i = 0; i < count; ++i)
        dict.Add(array[i].Key, array[i].Value);
      return;
    }

    private static MethodInfo GetDictionaryRebuildMethod(Type type)
    {
      for (; type != null && type != typeof(object);  type = type.BaseType)
      {
        if (type == typeof(System.Collections.Hashtable))
        {
          return ((Action<object>)RebuildLegacyDictionary).Method;
        }
        if (!type.IsGenericType) continue;

        var ta = type.GetGenericArguments();
        switch (ta.Length)
        {
        case 1:
          if (IsSet(type.GetGenericTypeDefinition()))
          {
            return IsPureValue(ta[0], 0)
              ? null
              : GetGenericRebuildDictionaryMethod().MakeGenericMethod(ta[0]);
          }
          break;
        case 2:
          if (IsDictionary(type.GetGenericTypeDefinition()))
          {
            return IsPureValue(ta[0], 0)
              ? null
              : GetGenericRebuildDictionaryMethod().MakeGenericMethod(
                typeof(KeyValuePair<,>).MakeGenericType(ta));
          }
          break;
        }
      }
      return null;
    }

    private static bool IsSet(Type type)
    {
      return type == typeof(HashSet<>) ||
        type == typeof(SortedSet<>);
    }

    private static bool IsDictionary(Type type)
    {
      return type == typeof(Dictionary<,>) ||
        type == typeof(SortedList<,>) ||
        type == typeof(SortedDictionary<,>);
    }

    private static bool IsPureValue(Type type, int level)
    {
      const BindingFlags bf = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
      if (level > 4) return false;
      return type.IsPrimitive || type == typeof(string) || (type.IsValueType
        && type.GetFields(bf).All(f => IsPureValue(f.FieldType, level + 1)));
    }

    private static MethodInfo GetGenericRebuildDictionaryMethod()
    {
      if (_genericRebuildDictionaryFn == null)
      {
        var m = ((Action<object>)RebuildGenericDictionary<int>).Method;
        return _genericRebuildDictionaryFn = m.GetGenericMethodDefinition();
      }
      return _genericRebuildDictionaryFn;
    }

    private static MethodInfo _genericRebuildDictionaryFn = null;
    #endregion

    #region Memberwise cloning
    /// <summary>
    /// Creates a delegate to call memberwise clone method.
    /// </summary>
    /// <returns>Func{System.ObjectSystem.Object}.</returns>
    /// <exception cref="FastCloneException">Method Object.MemberwiseClone not found.</exception>
    private static Func<object, object> CreateMemberwiseCloneFn()
    {
      var type = typeof(Object);
      MethodInfo m = type.GetMethod("MemberwiseClone",
        BindingFlags.Instance | BindingFlags.DeclaredOnly
        | BindingFlags.NonPublic | BindingFlags.Public,
        null, new Type[0], null);

      // Have we found it?
      if (m == null)
      {
        throw new FastCloneException(
          "Method Object.MemberwiseClone not found.");
      }

      // Now we create delegate by dynamic method.
      var dm = new DynamicMethod("MemberwiseClone_dyn", type, new[] { type }, type);
      ILGenerator il = dm.GetILGenerator();
      il.Emit(OpCodes.Ldarg_0);
      il.Emit(OpCodes.Callvirt, m);
      il.Emit(OpCodes.Ret);
      return (Func<object, object>)dm.CreateDelegate(
        typeof(Func<object, object>));
    }
    private static readonly Func<object, object>
      MemberwiseCloneFn = CreateMemberwiseCloneFn();
    #endregion

    #region Clone INativeSerializable objects
    /// <summary>
    /// Clones the wrappers of the native objects.
    /// </summary>
    /// <param name="src">The source object.</param>
    /// <returns>System.Object.</returns>
    /// <exception cref="FastCloneException"></exception>
    /// <remarks>Important: This function assumes that all the data obtained by
    /// ISerializable.GetObjectData are already cloned and they can be
    /// passed directly to the deserialization constructor to make a clone.</remarks>
    private static object CloneNativeSerializable(INativeSerializable src)
    {
      if (src == null) return null;

      // We only do this for WebMathTraining objects.
      // The purpose is to clone the native objects correctly.
      var type = src.GetType();

      // Get Serialization info.
      var ctx = new StreamingContext();
      var cvt = new FormatterConverter();
      var info = new SerializationInfo(type, cvt);
      src.GetObjectData(info, ctx);

      // Deserializaion to construct a clone.
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
            "Deserialization constructor not found for type {0}", type));
        }
        fn = CreateConstructorCaller(type, ctor);
        deserFns_.AddOrUpdate(type, fn, (t, f) => f);
      }
      var dst = fn(info, ctx);
      return dst;
    }

    private static DeserializeFn CreateConstructorCaller(
      Type type, ConstructorInfo ctor)
    {
      var dm = new DynamicMethod(String.Format("{0}_Construct_dyn",
        type.Name), typeof(object), SerializationParameters,
        type.Module, true);
      var il = dm.GetILGenerator();//.EmitConstructorCall(ctor);
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

    #endregion
  }
}
