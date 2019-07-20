/*
 * CloneUtil.cs
 *
 * Copyright (C) WebMathTraining 2008. All Rights Reserved.
 *
 * $Id: BaseEntityObject.cs,v 1.8 2006/12/06 11:13:37   $
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace BaseEntity.Shared
{

  /// <summary>
  ///   Utility methods for object cloning
  /// </summary>
  public static class CloneUtil
  {
    /// <summary>
    ///   Deep clone a generic IList of BaseEntityObjects to an ArrayList
    /// </summary>
    ///
    /// <remarks>
    ///   <para>If the input object is null, this method returns null.</para>
    ///
    ///   <para>Does a Clone and then clones the underlying elements.</para>
    ///
    ///   <para>The elements of the IList must be a BaseEntityObject (ie will not
    ///   work with an ArrayList of value types.</para>
    /// </remarks>
    ///
    /// <param name="o">ArrayList to clone</param>
    ///
    /// <returns>Clone of object</returns>
    ///
    public static ArrayList CloneToArrayList(IList o)
    {
      if (o == null)
        return null;
      var copy = new ArrayList(o.Count);
      foreach (IBaseEntityObject obj in o)
        copy.Add(obj.Clone());
      return copy;
    }

    /// <summary>
    /// Clones the IList of BaseEntityObjects as a List.
    /// </summary>
    /// 
    /// <typeparam name="T">The type of object in the list, must be a BaseEntityObject</typeparam>
    /// <param name="source">The list to clone</param>
    /// 
    /// <returns>Clone of object list.</returns>
    /// 
    public static List<T> CloneToGenericList<T>(IList<T> source) where T : BaseEntityObject
    {
      if (source == null)
        return null;
      var result = new List<T>(source.Count);
      for (int i = 0; i < source.Count; i++)
        result.Add(CloneUtil.Clone<T>(source[i]));
      return result;
    }

    /// <summary>
    ///   Deep clone a List
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>If the input object is null, this method returns null.</para>
    ///
    ///   <para>Does a Clone and then clones the underlying elements.</para>
    /// </remarks>
    ///
    /// <typeparam name="T">Type of item</typeparam>
    /// <param name="o">Object to clone</param>
    ///
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is an array
    ///   type but the element type is not value type nor BaseEntityObject</exception>
    ///
    /// <returns>Clone of object</returns>
    ///
    public static List<T> Clone<T>(List<T> o) where T : IBaseEntityObject
    {
      if (o == null)
        return null;
      List<T> copy = new List<T>(o.Count);
      for (int i = 0; i < o.Count; i++)
        copy.Add((T)o[i].Clone());
      return copy;
    }


    /// <summary>
    ///   Deep clone a List
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>If the input object is null, this method returns null.</para>
    ///
    ///   <para>Does a Clone and then clones the underlying elements.</para>
    /// </remarks>
    ///
    /// <typeparam name="TKey">Type of key</typeparam>
    /// <typeparam name="TValue">Type of item</typeparam>
    /// <param name="o">Object to clone</param>
    ///
    /// <exception cref="ArgumentException"><typeparamref name="TValue"/> is an array
    ///   type but the element type is not value type nor BaseEntityObject</exception>
    ///
    /// <returns>Clone of object</returns>
    ///
    public static Dictionary<TKey, TValue> Clone<TKey, TValue>(Dictionary<TKey, TValue> o) where TValue : IBaseEntityObject
    {
      if (o == null)
        return null;
      var copy = new Dictionary<TKey, TValue>(o.Count);
      foreach (TKey key in o.Keys)
      {
        copy.Add(key, (TValue)o[key].Clone());
      }
      return copy;
    }


    /// <summary>
    ///  Clones a dictionary of doubles
    /// </summary>
    /// <typeparam name="TKey">Type of Key</typeparam>
    /// <param name="source">Dictionary to clone</param>
    /// <returns></returns>
    public static IDictionary<TKey, double> Clone<TKey>(IDictionary<TKey, double> source)
    {
      return source == null ? null : new Dictionary<TKey, double>(source);
    }

    /// <summary>
    ///   Deep clone an array
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>If the input object is null, this method returns null.</para>
    ///
    ///   <para>Does a Clone and then clones the underlying elements.</para>
    /// </remarks>
    ///
    /// <typeparam name="T">Type of item</typeparam>
    /// <param name="o">Object to clone</param>
    ///
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is an array
    ///   type but the element type is not value type nor BaseEntityObject</exception>
    ///
    /// <returns>Clone of object</returns>
    ///
    public static T[] Clone<T>(T[] o)
    {
      if (o == null)
        return null;
      Type t = typeof(T); // o.GetType().GetElementType();
      if (t.IsValueType || t == typeof(String))
        return (T[])o.Clone();
      else if (t.IsSubclassOf(typeof(BaseEntityObject)))
      {
        T[] copy = new T[o.Length];
        for (int i = 0; i < o.Length; i++)
          copy[i] = (o[i] == null ? default(T) : (T)(o[i] as BaseEntityObject).Clone());
        return copy;
      }
      else if (t.IsArray && (t.GetElementType().IsValueType || t.GetElementType()==typeof(string)))
      {
        T[] copy = new T[o.Length];
        for (int i = 0; i < o.Length; i++)
        {
          if (o[i] != null)
          {
            copy[i] = (T) (o[i] as Array).Clone();
          }
        }
        return copy;
      }
      else
        throw new ArgumentException("Contents of array must be a BaseEntityObject or value types");
    }

    /// <summary>
    ///   Deep clone a two dimensional array
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>If the input object is null, this method returns null.</para>
    ///
    ///   <para>Does a Clone and then clones the underlying elements.</para>
    /// </remarks>
    ///
    /// <typeparam name="T">Type of item</typeparam>
    /// <param name="o">Object to clone</param>
    ///
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is an array
    ///   type but the element type is not value type nor BaseEntityObject</exception>
    ///
    /// <returns>Clone of object</returns>
    ///
    public static T[,] Clone<T>(T[,] o)
    {
      if (o == null)
        return null;
      Type t = typeof (T);
      if (t.IsValueType || t == typeof (String))
        return (T[,]) o.Clone();
      if (t.IsSubclassOf(typeof (BaseEntityObject)))
      {
        T[,] copy = new T[o.GetLength(0),o.GetLength(1)];
        for (int i = 0; i < o.GetLength(0); i++)
          for (int j = 0; j < o.GetLength(1); j++)
            copy[i, j] = (T) (o[i, j] as BaseEntityObject).Clone();
        return copy;
      }
      throw new ArgumentException("Contents of array must be a BaseEntityObject or value types");
    }

    /// <summary>
    ///   Deep clone a BaseEntityObject
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>If the input object is null, this method returns null.</para>
    /// </remarks>
    ///
    /// <typeparam name="T">Type of item</typeparam>
    /// <param name="o">Object to clone</param>
    ///
    /// <returns>Clone of object</returns>
    ///
    public static T Clone<T>(T o) where T : BaseEntityObject
    {
      if (o == null)
        return null;
      return (T) o.Clone();
    }

    /// <summary>
    /// Make a deep clone of an object and preserve the internal
    /// reference equality.
    /// </summary>
    /// <typeparam name="T">Object type</typeparam>
    /// <param name="o">The object to clone.</param>
    /// <returns>Cloned object.</returns>
    /// <remarks>If the default method is Serialization, then this function is 
    ///  implemented using serialization in binary formmat.
    /// Therefore it required the object to be cloned is marked serializable.</remarks>
    public static T CloneObjectGraph<T>(this T o) where T : class
    {
      return CloneObjectGraph(o, DefaultObjectGraphCloneMethod);
    }

    /// <summary>
    /// Make a deep clone of an object and preserve the internal
    /// reference equality.
    /// </summary>
    /// <typeparam name="T">Object type</typeparam>
    /// <param name="o">The object to clone.</param>
    /// <param name="method">The cloning method.</param>
    /// <returns>Cloned object.</returns>
    /// <remarks>If the method is Serialization, then this function is 
    ///  implemented using serialization in binary formmat.
    /// Therefore it required the object to be cloned is marked serializable.</remarks>
    public static T CloneObjectGraph<T>(this T o, CloneMethod method) where T : class
    {
      if (o == null)
        return null;

      if (method == CloneMethod.FastClone)
      {
        return o.FastClone(null);
      }

      IFormatter formatter = new BinaryFormatter(null,
        new StreamingContext(StreamingContextStates.Clone,
          new FastCloningContext()));
      using (var stream = new MemoryStream())
      {
        formatter.Serialize(stream, o);
        stream.Position = 0;
        T cloned = (T)formatter.Deserialize(stream);
        return cloned;
      }
    }

    /// <summary>
    /// Clone two objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2>
      CloneObjectGraph<T1, T2>(T1 obj1, T2 obj2)
      where T1 : class
      where T2 : class
    {
      return CloneObjectGraph(new Tuple<T1, T2>(obj1, obj2));
    }

    /// <summary>
    /// Clone three objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <typeparam name="T3">The type of item 3.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <param name="obj3">The object 3.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2, T3>
      CloneObjectGraph<T1, T2, T3>(
      T1 obj1, T2 obj2, T3 obj3)
      where T1 : class
      where T2 : class
      where T3 : class
    {
      return CloneObjectGraph(new Tuple<T1, T2, T3>(obj1, obj2, obj3));
    }

    /// <summary>
    /// Clone four objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <typeparam name="T3">The type of item 3.</typeparam>
    /// <typeparam name="T4">The type of item 4.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <param name="obj3">The object 3.</param>
    /// <param name="obj4">The object 4.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2, T3, T4>
      CloneObjectGraph<T1, T2, T3, T4>(
      T1 obj1, T2 obj2, T3 obj3, T4 obj4)
      where T1 : class
      where T2 : class
      where T3 : class
      where T4 : class
    {
      return CloneObjectGraph(new Tuple<T1, T2, T3, T4>(obj1, obj2, obj3, obj4));
    }

    /// <summary>
    /// Clone five objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <typeparam name="T3">The type of item 3.</typeparam>
    /// <typeparam name="T4">The type of item 4.</typeparam>
    /// <typeparam name="T5">The type of item 5.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <param name="obj3">The object 3.</param>
    /// <param name="obj4">The object 4.</param>
    /// <param name="obj5">The object 5.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2, T3, T4, T5>
      CloneObjectGraph<T1, T2, T3, T4, T5>(
      T1 obj1, T2 obj2, T3 obj3, T4 obj4, T5 obj5)
      where T1 : class
      where T2 : class
      where T3 : class
      where T4 : class
      where T5 : class
    {
      return CloneObjectGraph(new Tuple<T1, T2, T3, T4, T5>(obj1, obj2, obj3, obj4, obj5));
    }

    /// <summary>
    /// Clone six objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <typeparam name="T3">The type of item 3.</typeparam>
    /// <typeparam name="T4">The type of item 4.</typeparam>
    /// <typeparam name="T5">The type of item 5.</typeparam>
    /// <typeparam name="T6">The type of item 6.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <param name="obj3">The object 3.</param>
    /// <param name="obj4">The object 4.</param>
    /// <param name="obj5">The object 5.</param>
    /// <param name="obj6">The object 6.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2, T3, T4, T5, T6>
      CloneObjectGraph<T1, T2, T3, T4, T5, T6>(
      T1 obj1, T2 obj2, T3 obj3, T4 obj4, T5 obj5, T6 obj6)
      where T1 : class
      where T2 : class
      where T3 : class
      where T4 : class
      where T5 : class
      where T6 : class
    {
      return CloneObjectGraph(new Tuple<T1, T2, T3, T4, T5, T6>(obj1, obj2, obj3, obj4, obj5, obj6));
    }

    /// <summary>
    /// Clone seven objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <typeparam name="T3">The type of item 3.</typeparam>
    /// <typeparam name="T4">The type of item 4.</typeparam>
    /// <typeparam name="T5">The type of item 5.</typeparam>
    /// <typeparam name="T6">The type of item 6.</typeparam>
    /// <typeparam name="T7">The type of item 7.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <param name="obj3">The object 3.</param>
    /// <param name="obj4">The object 4.</param>
    /// <param name="obj5">The object 5.</param>
    /// <param name="obj6">The object 6.</param>
    /// <param name="obj7">The object 7.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2, T3, T4, T5, T6, T7>
      ClonePreserveReferences<T1, T2, T3, T4, T5, T6, T7>(
      T1 obj1, T2 obj2, T3 obj3, T4 obj4, T5 obj5, T6 obj6, T7 obj7)
      where T1 : class
      where T2 : class
      where T3 : class
      where T4 : class
      where T5 : class
      where T6 : class
      where T7 : class
    {
      return CloneObjectGraph(new Tuple<T1, T2, T3, T4, T5, T6, T7>(obj1, obj2, obj3, obj4, obj5, obj6, obj7));
    }

    /// <summary>
    /// Clone eight or more objects and preserves the references both inter and within objects.
    /// </summary>
    /// <typeparam name="T1">The type of item 1.</typeparam>
    /// <typeparam name="T2">The type of item 2.</typeparam>
    /// <typeparam name="T3">The type of item 3.</typeparam>
    /// <typeparam name="T4">The type of item 4.</typeparam>
    /// <typeparam name="T5">The type of item 5.</typeparam>
    /// <typeparam name="T6">The type of item 6.</typeparam>
    /// <typeparam name="T7">The type of item 7.</typeparam>
    /// <typeparam name="TRest">The type of the rest.</typeparam>
    /// <param name="obj1">The object 1.</param>
    /// <param name="obj2">The object 2.</param>
    /// <param name="obj3">The object 3.</param>
    /// <param name="obj4">The object 4.</param>
    /// <param name="obj5">The object 5.</param>
    /// <param name="obj6">The object 6.</param>
    /// <param name="obj7">The object 7.</param>
    /// <param name="objRest">Rest of the objects.</param>
    /// <returns>Cloned items as a tuple.</returns>
    public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>
      ClonePreserveReferences<T1, T2, T3, T4, T5, T6, T7, TRest>(
      T1 obj1, T2 obj2, T3 obj3, T4 obj4, T5 obj5, T6 obj6, T7 obj7, TRest objRest)
      where T1 : class
      where T2 : class
      where T3 : class
      where T4 : class
      where T5 : class
      where T6 : class
      where T7 : class 
      where TRest : class
    {
      return CloneObjectGraph(new Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>(obj1, obj2, obj3, obj4, obj5, obj6, obj7, objRest));
    }

    /// <summary>
    ///  Set to true to enable fast fine cloning.
    /// <preliminary>Experimental feature.  For WebMathTraining internal use only.</preliminary>
    /// </summary>
    public static CloneMethod DefaultObjectGraphCloneMethod = CloneMethod.Serialization;
  } // class CloneUtil

  /// <summary>
  ///  Clone Method
  /// </summary>
  /// <remarks></remarks>
  public enum CloneMethod
  {
    /// <summary>
    ///  In memory serialization based cloning.
    /// </summary>
    Serialization,
    /// <summary>
    ///  Fine cloning.
    /// </summary>
    FastClone
  }
} // WebMathTraining.Shared
