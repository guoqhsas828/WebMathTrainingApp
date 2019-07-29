// 
//  -2012. All rights reserved.
// 

using System;
using System.Reflection;
using System.Reflection.Emit;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///    Pricer evaluation function
  /// </summary>
  /// 
  /// <param name="pricer">Pricer instance</param>
  /// 
  /// <returns>Value of the function</returns>
  /// 
  public delegate double Double_Pricer_Fn(IPricer pricer);

  /// <summary>
  /// Pricer function that overwrites array of doubles
  /// </summary>
  /// <param name="pricer">Pricer instance</param>
  /// <param name="retVal">Overwritten by return values</param>
  public delegate void Array_Pricer_Fn(IPricer pricer, double[] retVal);

  /// <summary>
  ///   Class for creating pricer evaluation functions
  /// </summary>
  public static class DoublePricerFnBuilder
  {
    #region Public_Interface

    /// <summary>
    ///   Create an evaluation function for a pricer type
    /// </summary>
    /// <param name="pricerType">Type of the pricer</param>
    /// <param name="methodName">Name of the method to invoke</param>
    /// <exception cref="ArgumentException">The methd name cannot be found in the type.</exception>
    /// <returns>A Double_Pricer_Fn to invoke the given method.</returns>
    public static Double_Pricer_Fn CreateDelegate(
      Type pricerType, string methodName)
    {
      MethodInfo method = FindMethod(pricerType, methodName);
      // If not found, throw an exception
      if (method == null)
        throw new System.ArgumentException(String.Format("Method '{0}' not found in {1}", methodName, pricerType.FullName));
      return CreateDelegate(method, pricerType, false);
    }

    internal static Double_Pricer_Fn ToDoublePricerFn(this Delegate fn)
    {
      var f = (fn as Double_Pricer_Fn) ?? ToDoublePricerFn(fn as Func<IPricer, double>);
      if (f == null && fn != null)
        throw new ToolkitException(String.Format("{0}: unknown delegate", fn));
      return f;
    }

    private static Double_Pricer_Fn ToDoublePricerFn(Func<IPricer, double> fn)
    {
      if (fn == null) return null;
      return (Double_Pricer_Fn)Delegate.CreateDelegate(typeof(Double_Pricer_Fn), fn.Target, fn.Method);
    }
    #endregion Public_Interface

    #region Delegate_Implementation

    /// <summary>
    ///   Create an eveluation function
    /// </summary>
    /// <param name="method">Method to invoke</param>
    /// <param name="pricerType">Required type of the input parameter</param>
    /// <param name="checkSignature">If true, check signature compatibility</param>
    /// <returns>Evaluation function</returns>
    internal static Double_Pricer_Fn CreateDelegate(
      MethodInfo method,
      Type pricerType,
      bool checkSignature
      )
    {
      // Check method signature if requested
      if (checkSignature)
        CheckSignature(method, pricerType);

      // Create dynamic method
      DynamicMethod dm = new DynamicMethod(
        method.Name,
        typeof(double),
        new Type[] {typeof(IPricer)},
        typeof(DoublePricerFnBuilder));

      // Now we obtain its IL generator to inject code.
      ILGenerator il = dm.GetILGenerator();

      // Argument 0 of the dynamic method
      // Either the target instance for instance method
      // or the first parameter for static method
      il.Emit(OpCodes.Ldarg_0);

      // Perform actual call.
      // If method is not final a callvirt is required
      // otherwise a normal call will be emitted.
      if (method.IsFinal || method.IsStatic)
        il.Emit(OpCodes.Call, method);
      else
        il.Emit(OpCodes.Callvirt, method);

      // Emit return opcode.
      il.Emit(OpCodes.Ret);

      // Create the delegate
      Double_Pricer_Fn fn = (Double_Pricer_Fn)dm.CreateDelegate(typeof(Double_Pricer_Fn));

      // Return
      return fn;
    }

    /// <summary>
    ///   Check required signature of a method
    /// </summary>
    /// <param name="method">The method to invoke</param>
    /// <param name="pricerType">The required type of the input parameter</param>
    internal static void CheckSignature(MethodInfo method, Type pricerType)
    {
      // check return type
      if (method.ReturnType != typeof(double))
      {
        throw new ArgumentException(String.Format(
          "The return type [{0}] of {1} is not double",
          method.ReturnType, method.Name));
      }

      // check parameter number
      Type paramType = method.DeclaringType;
      if (method.IsStatic)
      {
        ParameterInfo[] pars = method.GetParameters();
        if (pars.Length != 1)
        {
          throw new ArgumentException(String.Format(
            "The number of arguments [{0}] of method {1} is not one",
            pars.Length, method.Name));
        }
        paramType = pars[0].ParameterType;
      }
      else if (method.GetParameters().Length != 0)
      {
        throw new ArgumentException(String.Format(
          "The number of arguments [{0}] of method {1} is not zero",
          method.GetParameters().Length, method.Name));
      }

      // the pricer type must be derived from parameter type
      if (paramType == null)
      {
        throw new ArgumentException("The parameter type is null");
      }
      else if (pricerType == null)
      {
        if (paramType.GetInterface("IPricer") == null)
          throw new ArgumentException(String.Format(
            "The parameter type [{0}] does not implement IPricer interface", paramType.FullName));
      }
      else if (!pricerType.IsSubclassOf(paramType))
      {
        throw new ArgumentException(String.Format(
          "The pricer type [{0}] is not compatible with the required parameter type {1}",
          pricerType.FullName, paramType.FullName));
      }
    }

    /// <summary>
    /// Find an evaluation method in a pricer
    /// </summary>
    /// <param name="type">Type of the pricer</param>
    /// <param name="methodName">Method name</param>
    /// <remarks>
    ///   <para>This method first searches in the <paramref name="type"/>
    ///    for a static method with with the signature
    ///    <c>double[] methodName(type)</c>,
    ///    and returns the method on success.
    ///   </para>
    ///   <para>If this fails, it searches in the <paramref name="type"/>
    ///    for an instance method with with the signature
    ///    <c>double[] methodName()</c>,
    ///    and returns the method if found.
    ///   </para>
    ///   <para>If both fail, null is returned</para>
    /// </remarks>
    /// <returns>MethodInfo object or null if not found</returns>
    internal static MethodInfo FindMethod(Type type, string methodName)
    {
      const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

      // First we search for a static method with one argument
      var mi = type.GetMethod(methodName, flags, null, new [] {type}, null);
      if (mi != null && mi.ReturnType == typeof(double) && mi.IsStatic)
        return mi;
      // Then we search for an instance method with no argument
      mi = type.GetMethod(methodName, flags, null, new Type[0], null);
      if (mi != null && mi.ReturnType == typeof(double) && !mi.IsStatic)
        return mi;
      return null;
    }

    #endregion Delegate_Implementation
  }
}
