/*
 * SurvivalBumpedEvalFn.cs
 *
 *  -2008. All rights reserved. 
 *
 * $Id $
 *
 */

using System;
using System.Reflection;
using System.Reflection.Emit;

using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   Function doing parallel evaluations of price measures based on
  ///   bumped survival curves
  /// </summary>
  /// <param name="pricer">The pricer object used to evaluate</param>
  /// <param name="evalName">The price measure to evaluate</param>
  /// <param name="curves">The bumped survival curves</param>
  /// <returns>Array of values</returns>
  public delegate double[] SurvivalBumpedEvalFn(
    IPricer pricer, string evalName, SurvivalCurve[] curves);

  /// <summary>
  ///   Create <c>SurvivalBumpedEvalFn</c>
  /// </summary>
  public abstract class SurvivalBumpedEvalFnBuilder
  {
    /// <summary>
    ///   Create a <c>SurvivalBumpedEvalFn</c> delegate from an
    ///   evaluation specification
    /// </summary>
    /// <param name="type">Pricer type to be used as the first parameter
    ///  of the delegate</param>
    /// <param name="evalName">Name of the price measure to be evaluated
    ///  (Pv, FeePv, BreakEvenPremium, etc.)</param>
    /// <returns>
    ///   It returns a <c>SurvivalBumpedEvalFn</c> object if a corresponding
    ///   method is implemented in the type; otherwise, it returns null.
    /// </returns>
    public static SurvivalBumpedEvalFn CreateDelegate(
            Type type, string evalName)
    {
      MethodInfo method = FindMethod(type, evalName);
      if (method == null)
        return null;
      SurvivalBumpedEvalFn eval = CreateDelegate(method);
      return eval;
    }

    /// <summary>
    ///   Find in a type an implemeneted method of bumped evaluation
    ///   for a given meaure
    /// </summary>
    /// <param name="type">Pricer type to search for the method</param>
    /// <param name="evalName">Name of the price measure to be evaluated
    ///  (Pv, FeePv, BreakEvenPremium, etc.)</param>
    /// <returns>
    ///   If the method is found, it returns a corresponding <c>MethodInfo</c>
    ///   object;  Otherwise, it returns null.
    /// </returns>
    public static MethodInfo FindMethod(Type type, string evalName)
    {
      MethodInfo method = FindMethodSpecific(type, "Bumped" + evalName);
      if (method != null)
        return method;
      return FindMethodSpecific(type, "BumpedEval");
    }

    /// <summary>
    ///   Find a specific bumped evaluation method with a given name
    ///   in a pricer type
    /// </summary>
    /// <param name="type">Pricer type in which to search for the method</param>
    /// <param name="name">The name of the method to search for</param>
    /// <returns>
    ///   It returns the corresponding MethodInfo if the method is found;
    ///   Otherwise, it returns null.
    /// </returns>
    /// <remarks>
    ///   <para>This method first searches in the <paramref name="type"/>
    ///    for a static method with with the signature
    ///    <c>double[] name(type, SurvivalCurve[])</c>,
    ///    and returns the method on success.
    ///   </para>
    ///
    ///   <para>If this fails, it searches in the <paramref name="type"/>
    ///    for an instance method with with the signature
    ///    <c>double[] name(SurvivalCurve[])</c>,
    ///    and returns the method if found.
    ///   </para>
    ///
    ///   <para>If both fail, it returns null.</para>
    /// </remarks>
    public static MethodInfo FindMethodSpecific(Type type, string name)
    {
      // First, search for a static method
      MethodInfo method = type.GetMethod(name,
              new Type[] { type, typeof(SurvivalCurve[]) });
      if (method != null && method.IsStatic
              && method.ReturnType == typeof(double[]))
      {
        return method;
      }

      // Second, search for an instance method.
      method = type.GetMethod(name,
                                      new Type[] { typeof(SurvivalCurve[]) });
      if (method != null && !method.IsStatic
              && method.ReturnType == typeof(double[]))
      {
        return method;
      }

      return null;
    }

    /// <summary>
    ///   Find a general bumped evaluation method with a given name
    ///   in a pricer type
    /// </summary>
    /// <param name="type">Pricer type in which to search for the method</param>
    /// <param name="name">The name of the method to search for</param>
    /// <returns>
    ///   It returns the corresponding MethodInfo if the method is found;
    ///   Otherwise, it returns null.
    /// </returns>
    /// <remarks>
    ///   <para>This method first searches in the <paramref name="type"/>
    ///    for a static method with with the signature
    ///    <c>double[] name(type, string, SurvivalCurve[])</c>,
    ///    and returns the method on success.
    ///   </para>
    ///
    ///   <para>If this fails, it searches in the <paramref name="type"/>
    ///    for an instance method with with the signature
    ///    <c>double[] name(string, SurvivalCurve[])</c>,
    ///    and returns the method if found.
    ///   </para>
    ///
    ///   <para>If both attempts fail, it returns null.</para>
    /// </remarks>
    public static MethodInfo FindMethodGeneral(Type type, string name)
    {
      // First, search for an instance method.
      MethodInfo method = type.GetMethod(name, new Type[] {
                               type, typeof(string), typeof(SurvivalCurve[]) });
      if (method != null && method.IsStatic
              && method.ReturnType == typeof(double[]))
      {
        return method;
      }

      // Second, search for a static method.
      method = type.GetMethod(name,
              new Type[] { typeof(string), typeof(SurvivalCurve[]) });
      if (method != null && !method.IsStatic
              && method.ReturnType == typeof(double[]))
      {
        return method;
      }

      return null;
    }

    /// <summary>
    ///   Create a <c>SurvivalBumpedEval</c> delegate from a method
    /// </summary>
    /// <param name="method">MethodInfo from which to create delegate</param>
    /// <returns>A <c>SurvivalBumpedEval</c> delegate</returns>
    public static SurvivalBumpedEvalFn CreateDelegate(MethodInfo method)
    {
      // Create dynamic method
      DynamicMethod dm = new DynamicMethod(
              method.Name,
              typeof(double[]),
              new Type[] { typeof(IPricer), typeof(string), typeof(SurvivalCurve[]) },
              typeof(SurvivalBumpedEvalFnBuilder));

      // Now we obtain its IL generator to inject code.
      ILGenerator il = dm.GetILGenerator();

      // Load the first argument on to stack
      il.Emit(OpCodes.Ldarg_0);

      // If the method takes a string parameter,
      // load the second argument on to stack
      int narg = method.GetParameters().Length + (method.IsStatic ? 0 : 1);
      if (narg > 2)
      {
        il.Emit(OpCodes.Ldarg_1);
      }

      // Load the third argument on to stack
      il.Emit(OpCodes.Ldarg_2);

      // Perform actual call.
      // If method is not final, a callvirt is required
      // otherwise, a normal call is emitted.
      if (method.IsFinal || method.IsStatic)
        il.Emit(OpCodes.Call, method);
      else
        il.Emit(OpCodes.Callvirt, method);

      // Emit return opcode.
      il.Emit(OpCodes.Ret);

      // Create and return delegate
      return (SurvivalBumpedEvalFn)
              dm.CreateDelegate(typeof(SurvivalBumpedEvalFn));
    }
  }

}
