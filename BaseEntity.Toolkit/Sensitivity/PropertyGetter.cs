/*
 * PricerDataRetriever.cs
 *
 *  -2008. All rights reserved. 
 *
 * $Id $
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///   A general delegate taking a parameter and return an object
  /// </summary>
  /// <param name="obj">Parameter</param>
  /// <returns>Object</returns>
  public delegate object Object_Object_Fn(object obj);

  /// <summary>
  ///   A generic base class wrapping the get function which returns
  ///   an array of objects (curves or corrections, for example).
  /// </summary>
  /// <typeparam name="T">The element type in the returned array</typeparam>
  public abstract class PropertyGetter<T> where T : class
  {
    /// <summary>
    ///   Get an array of type T objects from a pricer.
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <returns>Array of T</returns>
    public abstract T[] Get(IPricer pricer);
  }

  /// <summary>
  ///   Building property get function for a pricer
  /// </summary>
  public static class PropertyGetBuilder
  {
    /// <summary>
    ///   Create a get function to retrieve survival curves from a pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property getter object</returns>
    public static PropertyGetter<SurvivalCurve>
      CreateSurvivalGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "SurvivalCurves");
      if (fn != null)
        return new GetOne<SurvivalCurve>(fn);
      fn = CreateDelegate(type, "SurvivalCurve");
      if (fn == null)
        return scnull;
      Object_Object_Fn f1 = CreateDelegate(type, "CounterpartyCurve");
      if (f1 == null)
        return new GetOne<SurvivalCurve>(fn);
      return new GetBoth<SurvivalCurve>(fn, f1);
    }

    /// <summary>
    ///   Create a get function to retrieve recovery curves from a pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property getter object</returns>
    public static PropertyGetter<RecoveryCurve>
      CreateRecoveryGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "RecoveryCurves");
      if (fn != null)
        return new GetOne<RecoveryCurve>(fn);
      fn = CreateDelegate(type, "RecoveryCurve");
      if (fn != null)
        return new GetOne<RecoveryCurve>(fn);
      // Just for CDSOptionPricer backward compatability for now. RTD Feb'07
      fn = CreateDelegate(type, "SurvivalCurve");
      if (fn == null)
        return rcnull;
      return new RecoveryFromSurvival(fn);
    }

    /// <summary>
    ///   Create a get function to retrieve discount curves from a pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property getter object</returns>
    public static PropertyGetter<CalibratedCurve>
      CreateDiscountGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "DiscountCurves");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);
      fn = CreateDelegate(type, "DiscountCurve");
      if (fn == null)
        return dcnull;
      Object_Object_Fn f1 = CreateDelegate(type, "ReferenceCurve");
      if (f1 == null)
        return new GetOne<CalibratedCurve>(fn);
      return new GetBoth<CalibratedCurve>(fn, f1);
    }

    /// <summary>
    ///   Create a get function to retrieve discount curves from a pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property getter object</returns>
    public static PropertyGetter<DiscountCurve>
      CreateFundingCurveGetter(Type type)
    {
      var fn = CreateDelegate(type, "DiscountCurve");
      if (fn == null)
        return fnull;
      return new GetOne<DiscountCurve>(fn);
    }

    /// <summary>
    ///   Create a get function to retrieve fxBasis curves from a pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property getter object</returns>
    public static PropertyGetter<CalibratedCurve>
      CreateBasisCurveGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "BasisAdjustments");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);
      fn = CreateDelegate(type, "BasisAdjustment");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);
      var fxGetter = CreateFxCurveGetter(type);
      if (fxGetter == ccnull)
        return ccnull;
      return new BasisFromFxCurve(fxGetter);
    }

    /// <summary>
    /// Create a get function for the FX Curve from the pricer
    /// Thsi FX curve could be in the form of a Basis adjustment or it could be a user supplied Fx curve
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property Getter object</returns>
    public static PropertyGetter<CalibratedCurve>
      CreateFxCurveGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "FxCurves");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);
      fn = CreateDelegate(type, "FxCurve");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);
      return ccnull;
    }

    /// <summary>
    /// Create a get function for the Reference Curves from the pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property Getter object</returns>
    public static PropertyGetter<CalibratedCurve>
      CreateReferenceCurveGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "ReferenceCurves");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);

      fn = CreateDelegate(type, "ReferenceCurve");
      if (fn != null)
        return new GetOne<CalibratedCurve>(fn);
      return ccnull;
    }

    /// <summary>
    /// Create a get function for the StockCurves from the pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property Getter object</returns>
    public static PropertyGetter<StockCurve>
      CreateStockCurveGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "StockCurves");
      if (fn != null)
        return new GetOne<StockCurve>(fn);

      fn = CreateDelegate(type, "StockCurve");
      if (fn != null)
        return new GetOne<StockCurve>(fn);
      return new GetNone<StockCurve>();
    }

    /// <summary>
    /// Create a get function for the CommodityCurves from the pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property Getter object</returns>
    public static PropertyGetter<CommodityCurve>
      CreateCommodityCurveGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "CommodityCurves");
      if (fn != null)
        return new GetOne<CommodityCurve>(fn);

      fn = CreateDelegate(type, "CommodityCurve");
      if (fn != null)
        return new GetOne<CommodityCurve>(fn);
      return new GetNone<CommodityCurve>();
    }

    /// <summary>
    /// Create a get function for the InflationCurves from the pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property Getter object</returns>
    public static PropertyGetter<InflationCurve>
      CreateInflationCurveGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "InflationCurves");
      if (fn != null)
        return new GetOne<InflationCurve>(fn);

      fn = CreateDelegate(type, "InflationCurve");
      if (fn != null)
        return new GetOne<InflationCurve>(fn);
      return new GetNone<InflationCurve>();
    }


    /// <summary>
    /// Create a get function for the Rate Volatility Cube from the pricer  
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property Getter object</returns>
    public static PropertyGetter<RateVolatilityCube>
      CreateRateVolatilityCubeGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "VolatilityCube");
      if (fn != null)
        return new GetOne<RateVolatilityCube>(fn);

      return fwdCubeNull;
    }

    /// <summary>
    ///   Create a get function to retrieve a reference curve from a pricer
    /// </summary>
    /// <returns>Property getter object</returns>
    public static Func<IPricer,T[]>
      CreateGetter<T>(IPricer pricer, string propertyName) where  T : class 
    {
      Object_Object_Fn fn = CreateDelegate(pricer.GetType(), propertyName);
      if (fn == null)
        return (p) => null;
      return (new GetOne<T>(fn)).Get;
    }

    /// <summary>
    ///   Create a get function to retrieve correlation objects from a pricer
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <returns>Property getter object</returns>
    public static PropertyGetter<CorrelationObject>
      CreateCorrelationGetter(Type type)
    {
      Object_Object_Fn fn = CreateDelegate(type, "Correlation");
      if (fn == null)
        return conull;
      return new GetOne<CorrelationObject>(fn);
    }

    /// <summary>
    ///   Wraping a property get method in a delegate
    /// </summary>
    /// <param name="type">Pricer type</param>
    /// <param name="propertyName">Property name</param>
    /// <returns>A Object_Object_Fn representing the get method</returns>
    internal static Object_Object_Fn CreateDelegate(Type type, string propertyName)
    {
      PropertyInfo info = type.GetProperty(propertyName,
        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
      if (info == null)
        return null;
      return CreateDelegate(info.GetGetMethod());
    }

    /// <summary>
    ///   Wraping a method info in a delegate
    /// </summary>
    /// <param name="method">Method info</param>
    /// <returns>A Object_Object_Fn representing the method</returns>
    internal static Object_Object_Fn CreateDelegate(MethodInfo method)
    {
      // Create dynamic method
      var dm = new DynamicMethod(
        method.Name,
        typeof(object),
        new[]{ typeof(object) },
        typeof(PropertyGetBuilder), true);

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
      var fn = (Object_Object_Fn)
        dm.CreateDelegate(typeof(Object_Object_Fn));

      // Return
      return fn;
    }

    private static PropertyGetter<DiscountCurve> fnull = new GetNone<DiscountCurve>(); 
    private static PropertyGetter<SurvivalCurve> scnull = new GetNone<SurvivalCurve>();
    private static PropertyGetter<RecoveryCurve> rcnull = new GetNone<RecoveryCurve>();
    private static PropertyGetter<CalibratedCurve> dcnull = new GetNone<CalibratedCurve>();
    private static PropertyGetter<CalibratedCurve> ccnull = new GetNone<CalibratedCurve>();
    private static PropertyGetter<CorrelationObject> conull = new GetNone<CorrelationObject>();
   private static PropertyGetter<RateVolatilityCube> fwdCubeNull = new GetNone<RateVolatilityCube>();

    internal class GetNone<T> : PropertyGetter<T>
      where T : class
    {
      public override T[] Get(IPricer pricer)
      {
        return null;
      }
    }

    internal class GetOne<T> : PropertyGetter<T>
       where T : class
    {
      public GetOne(Object_Object_Fn getter)
      {
        getter_ = getter;
      }

      public override T[] Get(IPricer pricer)
      {
        if (getter_ == null)
          return null;
        object obj = getter_(pricer);
        if (obj == null)
          return new T[0];
        if (obj is T[])
          return (T[]) obj;
        if (obj is T)
          return new[] {(T) obj};
        if (obj is IEnumerable<T>)
          return ((IEnumerable<T>)obj).ToArray();
        return new T[0];
      }

      private Object_Object_Fn getter_;
    }

    internal class GetBoth<T> : PropertyGetter<T>
      where T : class
    {
      public GetBoth(
        Object_Object_Fn getter1,
        Object_Object_Fn getter2)
      {
        getter1_ = getter1;
        getter2_ = getter2;
      }

      public override T[] Get(IPricer pricer)
      {
        var c1 = (T)getter1_(pricer);
        var c2 = (T)getter2_(pricer);
        if (c1 != null && c2 != null)
          return new[] {c1, c2};
        if (c1 == null)
          return c2 == null ? new T[0] : new[] {c2};
        return new T[] {c1};
      }

      private Object_Object_Fn getter1_;
      private Object_Object_Fn getter2_;
    }

    internal class GetAll<T> : PropertyGetter<T>
      where T : class
    {
      public GetAll(Object_Object_Fn[] getters)
      {
        getters_ = getters;
      }

      public override T[] Get(IPricer pricer)
      {
        List<T> c = new List<T>();
        for (int i = 0; i < getters_.Length; i++)
        {
          if(getters_[i]!= null)
            c.Add((T) getters_[i](pricer));
        }
        return c.ToArray();
      }
      private readonly Object_Object_Fn[] getters_;
    }

    internal class RecoveryFromSurvival : PropertyGetter<RecoveryCurve>
    {
      public RecoveryFromSurvival(
        Object_Object_Fn getter)
      {
        survivalGetter_ = getter;
      }

      public override RecoveryCurve[] Get(IPricer pricer)
      {
        var sc = (SurvivalCurve)survivalGetter_(pricer);
        RecoveryCurve[] rc =
          sc != null && sc.SurvivalCalibrator != null
          ? new [] { sc.SurvivalCalibrator.RecoveryCurve }
          : new RecoveryCurve[0];
        return rc;
      }
      private readonly Object_Object_Fn survivalGetter_;
    }

    internal class BasisFromFxCurve : PropertyGetter<CalibratedCurve>
    {
      public BasisFromFxCurve(
        PropertyGetter<CalibratedCurve> getter)
      {
        fxGetter_ = getter;
      }

      public override CalibratedCurve[] Get(IPricer pricer)
      {
        var list = new List<CalibratedCurve>();
        foreach (var curve in fxGetter_.Get(pricer))
        {
          var fx = curve as FxCurve;
          if (fx == null || fx.BasisCurve == null) continue;
          var bc = fx.BasisCurve;
          if (bc != null && !list.Contains(bc))
            list.Add(bc);
        }
        return list.ToArray();
      }

      private readonly PropertyGetter<CalibratedCurve> fxGetter_;
    }



  } // class PropertyGetBuilder
}
