/*
 * Copyright (c)    2012-15. All rights reserved.
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base.Serialization
{
  /// <summary>
  ///   A class helps to serialize the delegates, especially the anonymous delegates.
  /// </summary>
  public static class SerializingDelegates
  {
    private static readonly Type[] GenericActions, GenericFuncs;

    static SerializingDelegates()
    {
      const int n = SerializableDelegate.MaximumParametersSupported;
      GenericActions = new Type[n + 1];
      GenericFuncs = new Type[n + 1];
      GenericActions[0] = Type.GetType("System.Action");
      GenericFuncs[0] = Type.GetType("System.Func`1");
      for (int i = 1; i <= n; ++i)
      {
        GenericActions[i] = Type.GetType("System.Action`" + i);
        GenericFuncs[i] = Type.GetType("System.Func`" + (i + 1));
      }
    }

    /// <summary>
    /// Wraps the delegate into serializable.
    /// </summary>
    /// <typeparam name="T">The delegate type.</typeparam>
    /// <param name="function">The delegate.</param>
    /// <returns>A serializable version of the input delegate.</returns>
    /// <remarks>
    ///   If the delegate target is itself serializable, this function returns the
    ///   input delegate unchanged.  Otherwise, the delegate is wrapped in an ISerializable
    ///   wrapper.  The wrapped version is fully functional as the original one, but it may
    ///   be a bit less efficient.  It is recommended to call this method in the handling of
    ///   the OnSerializing events and then restore the unwrapped version in the OnSerialized
    ///   event.
    /// </remarks>
    public static T WrapSerializableDelegate<T>(this T function) where T : class
    {
      return Transformer<T>.Wrap(function);
    }

    /// <summary>
    /// Unwraps the serializable delegate to the "naked" one.
    /// </summary>
    /// <typeparam name="T">The delegate type.</typeparam>
    /// <param name="function">The function.</param>
    /// <returns>The "naked" version of the input delegate.</returns>
    /// <remarks>
    ///   If the input delegate is not a wrapped version created by <c>WrapSerializableDelegate</c>,
    ///   this function returns the input delegate unchanged.  Otherwise, the wrapped version
    ///   is unwrapped and returned.
    /// </remarks>
    public static T UnwrapSerializableDelegate<T>(this T function) where T : class
    {
      return Transformer<T>.Unwrap(function);
    }

    internal static Delegate Unwrap(Delegate fn)
    {
      var wrapped = fn.Target as SerializableDelegate;
      return wrapped == null ? fn : wrapped.Delegate;
    }

    #region Nested type: Transformer<TDelegate>
    /// <summary>
    ///   A wrapper to make most of the delegates, especially the anonymous delegates,
    ///   to be serialzable.
    /// </summary>
    /// <typeparam name="TDelegate">The delegate type.</typeparam>
    private sealed class Transformer<TDelegate> where TDelegate : class
    {
      private static RuntimeMethodHandle _handle;
      private static Type _stdDelegateType;

      // Constraint check to get around the C# limitation
      static Transformer()
      {
        if (!typeof(TDelegate).IsSubclassOf(typeof(Delegate)))
        {
          throw new InvalidOperationException(typeof(TDelegate).Name
            + " is not a delegate type");
        }

        var method = typeof(TDelegate).GetMethod("Invoke");
        var pars = method.GetParameters();
        int n = pars.Length;
        if (n > SerializableDelegate.MaximumParametersSupported)
        {
          throw new ToolkitException(SerializableDelegate.BadPars);
        }
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
          var args = pars.Select(p => p.ParameterType).ToArray();
          _stdDelegateType = GenericActions[n].MakeGenericType(args);
          var gm = typeof(SerializableDelegate).GetMethod("A" + n);
          Debug.Assert(gm.IsGenericMethod && gm.IsGenericMethodDefinition);
          _handle = gm.MakeGenericMethod(args).MethodHandle;
        }
        else
        {
          var args = pars.Select(p => p.ParameterType)
            .Concat(new[] { method.ReturnType }).ToArray();
          _stdDelegateType = GenericFuncs[n].MakeGenericType(args);
          var gm = typeof(SerializableDelegate).GetMethod("F" + n);
          Debug.Assert(gm.IsGenericMethod && gm.IsGenericMethodDefinition);
          _handle = gm.MakeGenericMethod(args).MethodHandle;
        }
      }

      public static TDelegate Wrap(TDelegate function)
      {
        var fn = function as Delegate;
        if (IsSimple(fn)) return function;
        return (TDelegate)(object)Delegate.CreateDelegate(
          typeof(TDelegate),
          new SerializableDelegate(Transform(fn)),
          (MethodInfo)MethodBase.GetMethodFromHandle(_handle));
      }

      public static TDelegate Unwrap(TDelegate function)
      {
        var fn = function as Delegate;
        if(fn==null) return function;
        var sd = fn.Target as SerializableDelegate;
        if (sd == null || sd.Delegate == null) return function;
        fn = sd.Delegate;
        return (TDelegate)(object)Delegate.CreateDelegate(
          typeof(TDelegate), fn.Target, fn.Method);
      }

      private static Delegate Transform(Delegate f)
      {
        return Delegate.CreateDelegate(_stdDelegateType, f.Target, f.Method);
      }

      private static bool IsSimple(Delegate fn)
      {
        return fn == null
          || (fn.Target == null && !fn.Method.IsDeclaredInScript())
          || fn.Method.DeclaringType == null
          || fn.Method.DeclaringType.GetCustomAttributes(
            typeof (SerializableAttribute), false).Length > 0;
      }
    }
    #endregion

    #region Nested type: Serializable Delegate wrapper
    [Serializable]
    private class SerializableDelegate : ISerializable
    {
      internal const string BadCast = "Invalid cast of delegate";
      internal const string BadPars = "More than 4 parameters not supported";
      internal const int MaximumParametersSupported = 4;

      private readonly Delegate _delegate;

      public SerializableDelegate(Delegate fn)
      {
        _delegate = fn;
      }

      /// <summary>
      /// Gets the delegate to invoke.
      /// </summary>
      /// <remarks></remarks>
      public Delegate Delegate
      {
        get { return _delegate; }
      }

      #region Actions
      public void A0()
      {
        var fn = _delegate as Action;
        if (fn == null) throw new ToolkitException(BadCast);
        fn();
      }

      public void A1<T>(T a)
      {
        var fn = _delegate as Action<T>;
        if (fn == null) throw new ToolkitException(BadCast);
        fn(a);
      }

      public void A2<T1, T2>(T1 a1, T2 a2)
      {
        var fn = _delegate as Action<T1, T2>;
        if (fn == null) throw new ToolkitException(BadCast);
        fn(a1, a2);
      }

      public void A3<T1, T2, T3>(T1 a1, T2 a2, T3 a3)
      {
        var fn = _delegate as Action<T1, T2, T3>;
        if (fn == null) throw new ToolkitException(BadCast);
        fn(a1, a2, a3);
      }

      public void A4<T1, T2, T3, T4>(T1 a1, T2 a2, T3 a3, T4 a4)
      {
        var fn = _delegate as Action<T1, T2, T3, T4>;
        if (fn == null) throw new ToolkitException(BadCast);
        fn(a1, a2, a3, a4);
      }
      #endregion

      #region Functions
      public TResult F0<TResult>()
      {
        var fn = _delegate as Func<TResult>;
        if (fn == null) throw new ToolkitException(BadCast);
        return fn();
      }

      public TResult F1<TArg, TResult>(TArg arg)
      {
        var fn = _delegate as Func<TArg, TResult>;
        if (fn == null) throw new ToolkitException(BadCast);
        return fn(arg);
      }

      public TResult F2<T1, T2, TResult>(T1 a1, T2 a2)
      {
        var fn = _delegate as Func<T1, T2, TResult>;
        if (fn == null) throw new ToolkitException(BadCast);
        return fn(a1, a2);
      }

      public TResult F3<T1, T2, T3, TResult>(T1 a1, T2 a2, T3 a3)
      {
        var fn = _delegate as Func<T1, T2, T3, TResult>;
        if (fn == null) throw new ToolkitException(BadCast);
        return fn(a1, a2, a3);
      }

      public TResult F4<T1, T2, T3, T4, TResult>(T1 a1, T2 a2, T3 a3, T4 a4)
      {
        var fn = _delegate as Func<T1, T2, T3, T4, TResult>;
        if (fn == null) throw new ToolkitException(BadCast);
        return fn(a1, a2, a3, a4);
      }
      #endregion

      #region ISerialzable implementation
      internal SerializableDelegate(SerializationInfo info, StreamingContext context)
      {
        Type delType = (Type)info.GetValue("delegateType", typeof(Type));

        //If it's a "simple" delegate we just read it straight off
        if (info.GetBoolean("isSerializable"))
          this._delegate = (Delegate)info.GetValue("delegate", delType);

          //otherwise, we need to read its anonymous class
        else
        {
          var method = MethodWrapper.Unwrap(info.GetValue("method", typeof(object)));

          var w = info.GetValue("class", typeof(object));

          _delegate = System.Delegate.CreateDelegate(delType, w.UnwrapSerializable(context), method);
        }
      }

      void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
      {
        info.AddValue("delegateType", _delegate.GetType());

        //If it's an "simple" delegate we can serialize it directly
        if (_delegate == null || (!_delegate.Method.IsDeclaredInScript() &&
          (_delegate.Target == null || _delegate.Method.DeclaringType
          .GetCustomAttributes(typeof (SerializableAttribute), false).Length > 0)))
        {
          info.AddValue("isSerializable", true);
          info.AddValue("delegate", _delegate);
        }

        //otherwise, serialize anonymous class
        else
        {
          info.AddValue("isSerializable", false);
          info.AddValue("method", MethodWrapper.Wrap(_delegate.Method));
          info.AddValue("class", _delegate.Target.WrapSerializable(context));
        }
      }
      #endregion
    }
    #endregion
  }
}
