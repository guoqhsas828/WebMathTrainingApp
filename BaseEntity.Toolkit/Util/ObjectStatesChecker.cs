/*
 * ObjectStatesChecker.cs
 *
 *   2004-2008. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   Check two object for the equivalence of the internal states.
  /// </summary>
  public class ObjectStatesChecker
  {
    /// <summary>
    ///   Information about mismatched fields or objects.
    /// </summary>
    public class Mismatch
    {
      internal Mismatch(string location,
        object v1, object v2)
      {
        Name = location;
        FirstValue = v1;
        SecondValue = v2;
      }
      /// <summary>
      ///   Name of the mismatched fields or object types.
      /// </summary>
      public readonly string Name;
      /// <summary>
      ///   The value of the first field or object.
      /// </summary>
      public readonly object FirstValue;
      /// <summary>
      ///   The value of the second field or object.
      /// </summary>
      public readonly object SecondValue;

      /// <summary>
    /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.>.
      /// </summary>
      /// <returns>
    /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.>.
      /// </returns>
      public override string ToString()
      {
        return String.IsNullOrEmpty(Name)
          ? String.Format("Mismatch: {0} : {1}", FirstValue, SecondValue)
          : String.Format("Mismatch at {0}: {1} : {2}", Name, FirstValue, SecondValue);
      }
    };

    /// <summary>
    ///  Compares two objects field by field, and returns the
    ///  first mismatch, or null if they matches each other.
    /// </summary>
  /// <typeparam name="T">Object type to compare.</typeparam>m>
    /// <param name="first">The first object.</param>
    /// <param name="second">The second object.</param>
    /// <returns>If the two object matches, return null;
    ///  otherwise, a <see cref="Mismatch"/> object containing
    ///  the information of the first mismatch.</returns>
    ///  <remarks>
    ///   The fields marked <see cref="MutableAttribute"/> are
    ///   excluded from comparison.
    ///  </remarks>
    ///  <seealso cref="MutableAttribute"/>
    public static Mismatch Compare<T>(T first, T second)
    {
      var compared = new Dictionary<object, object>();
      var stack = new Stack<ObjectPair>();
      Mismatch result = null;
      Match("", compared, stack, first, second, 0.0, ref result);
      return result;
    }

    /// <summary>
    /// Compares two objects field by field, and returns the
    /// first mismatch, or null if they matches each other.
    /// </summary>
  /// <typeparam name="T">Object type to compare.</typeparam>m>
    /// <param name="first">The first object.</param>
    /// <param name="second">The second object.</param>
    /// <param name="tolerance">The tolerance used to compare floating numbers.</param>
    /// <returns>If the two object matches, return null;
    /// otherwise, a <see cref="Mismatch"/> object containing
    /// the information of the first mismatch.</returns>
    /// <seealso cref="MutableAttribute"/>
    /// <remarks>The fields marked <see cref="MutableAttribute"/> are
    /// excluded from comparison.</remarks>
    public static Mismatch Compare<T>(T first, T second, double tolerance)
    {
      var compared = new Dictionary<object, object>();
      var stack = new Stack<ObjectPair>();
      Mismatch result = null;
      Match("", compared, stack, first, second, tolerance, ref result);
      return result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStatesChecker"/> class.
    /// </summary>
    /// <param name="obj">The object to check state invariance.</param>
    public ObjectStatesChecker(object obj)
    {
      saved_ = CloneUtil.CloneObjectGraph(obj);
    }

    /// <summary>
    /// Checks the state invariance against a saved copy of the object.
    /// </summary>
    /// <param name="obj">The object.</param>
    public void CheckInvariance(object obj)
    {
      // We put a pricer invariance check here.
      var mismatch = Compare(saved_, obj);
      if (mismatch != null)
        throw new ToolkitException(mismatch.ToString());
    }

    #region Implementation
    /// <summary>
    ///  Compare states of two objects.
    /// </summary>
    /// <param name="loc">Location.</param>
    /// <param name="compared">Dictionary of compared objects.</param>
    /// <param name="stack">Stack of the objects being comparing recursively.</param>
    /// <param name="A">Object A to compare.</param>
    /// <param name="B">Object B to compare.</param>
    /// <param name="tolerance">Tolerance</param>
    /// <param name="result">The result.</param>
    /// <returns>True if match; otherwise, false.</returns>
    private static bool Match(string loc,
      Dictionary<object, object> compared,
      Stack<ObjectPair> stack,
      object A, object B,
      double tolerance,
      ref Mismatch result)
    {
      if (A == null)
      {
        if (B == null)
          return true;
        // A null, B non-null.
        loc += "!{Null}";
        result = new Mismatch(loc,
          "", B.GetType().FullName);
        return false;
      }
      else if (B == null)
      {
        loc += "!{Null}";
        result = new Mismatch(loc,
          A.GetType().FullName, "");
        return false;
      }

      // Tow object must be the same type.
      Type type = A.GetType();
      if (type != B.GetType())
      {
        loc += "!{Type}";
        result = new Mismatch(loc,
          type.FullName, B.GetType().FullName);
        return false;
      }

      // For primitive types, we use object Equals to compare.
      if (type.IsPrimitive || A is string)
      {
        if (A is double && B is double && tolerance > 0
          && Math.Abs((double)A - (double)B) < tolerance
          || A.Equals(B))
        {
          return true;
        }
        result = new Mismatch(loc, A, B);
        return false;
      }

      // For reference type, we first check if they are the
      // same reference.
      if (Object.ReferenceEquals(A, B))
        return true;

      // Now we check if the object is already compared.
      if (compared.ContainsKey(A))
      {
        if (Object.ReferenceEquals(compared[A], B))
          return true;
      }

      // To avoid infinite recursion, we check if there
      // is any circular containing.
      foreach (ObjectPair item in stack)
      {
        if (ReferenceEquals(item.First, A))
        {
          if (ReferenceEquals(item.Second, B))
            return true;
          // We found a case that lastA contains itself
          // but lastB does not.
          // Right now we simply treat this as an error.
          result = new Mismatch(loc, A, B);
          return false;
        }
      }

      // Save the current pair in the stack.
      stack.Push(new ObjectPair(A, B));

      try
      {
        // For IList, we compare element by element.
        if (A is IList)
        {
          IList blist = B as IList;
          IList alist = A as IList;
          int count = alist.Count;
          if (!Match(loc + ".Count", compared, stack,
            count, blist.Count, tolerance, ref result))
          {
            return false;
          }
          // The following ugly codes go through the IEnumerable interface,
          // in order to get around one .Net problem: calling IList indexer
          // on a multidimensional array throws an exception saying that the array
          // is not one dimensional.
          var aEnum = alist.GetEnumerator();
          var bEnum = blist.GetEnumerator();
          aEnum.MoveNext();
          bEnum.MoveNext();
          for (int i = 0; i < count; ++i)
          {
            if (!Match(loc + ".Item[" + i + ']', compared, stack,
              aEnum.Current, bEnum.Current, tolerance, ref result))
            {
              return false;
            }
            aEnum.MoveNext();
            bEnum.MoveNext();
          }
          // Save A to the collection of compared objects.
          if (!compared.ContainsKey(A))
            compared.Add(A, B);
          return true;
        }

        // For IDictionary, we compare key by key,
        if (A is IDictionary)
        {
          IDictionary adic = A as IDictionary;
          IDictionary bdic = B as IDictionary;
          if (!Match(loc + ".Count", compared, stack,
            adic.Count, bdic.Count, tolerance, ref result))
          {
            return false;
          }
          foreach (object key in adic.Keys)
          {
            if (!bdic.Contains(key))
            {
              loc += "!{Key}";
              result = new Mismatch(loc, key, null);
              return false;
            }
            if (!Match(loc + '[' + key + ']', compared, stack,
              adic[key], bdic[key], tolerance, ref result))
            {
              return false;
            }
          }
          if (!compared.ContainsKey(A))
            compared.Add(A, B);
          return true;
        }

        var fa = A as Delegate;
        var fb = B as Delegate;
        if (fa != null && fb != null)
        {
          if (!Match(loc + ".Method", compared, stack,
            fa.Method, fb.Method, tolerance, ref result))
          {
            return false;
          }
          return Match(loc + ".Target", compared, stack,
            fa.Target, fb.Target, tolerance, ref result);
        }

        // For all other object we compare instance fields.
        FieldInfo[] fields = GetFields(type);
        for (int i = 0; i < fields.Length; ++i)
        {
          FieldInfo fi = fields[i];
          if (fi.FieldType == typeof(HandleRef)
            && fi.Name == "swigCPtr")
          {
            IntPtr aptr = ((HandleRef)fi.GetValue(A)).Handle;
            IntPtr bptr = ((HandleRef)fi.GetValue(B)).Handle;

            // Same pointer?
            if (aptr.Equals(bptr))
              continue;

            // Already compared?
            if (compared.ContainsKey(aptr)
              && compared[aptr].Equals(bptr))
            {
              continue;
            }

            if (!typeof(ISerializable)
              .IsAssignableFrom(fi.DeclaringType))
            {
              result = new Mismatch(loc + '.' +
                fi.DeclaringType.Name + "!{ISerializable}",
                aptr, bptr);
              return false;
            }

            // Handle native comparing.
            if (!NativeMatch(loc + ".swigCPtr", compared, stack,
              fi.DeclaringType, A, B, tolerance, ref result))
            {
              return false;
            }

            // Match, add to dictionary
            if (!compared.ContainsKey(aptr))
              compared.Add(aptr, bptr);
          }
          else if (!Match(loc + '.' + fi.Name, compared, stack,
            fi.GetValue(A), fi.GetValue(B), tolerance, ref result))
          {
            return false;
          }
        }

        if (!compared.ContainsKey(A))
          compared.Add(A, B);
        return true;
      }
      finally
      {
        stack.Pop();
      }
      // done!
    }

    /// <summary>
    /// Compare the native objects.
    /// </summary>
    /// <remarks>
    ///   We call the ISerializable interface method GetObjectData
    ///   on the native objects and compare them field by field.
    /// </remarks>
    /// <param name="loc">The loc.</param>
    /// <param name="compared">The compared.</param>
    /// <param name="stack">The stack.</param>
    /// <param name="type">The type.</param>
    /// <param name="A">The A.</param>
    /// <param name="B">The B.</param>
    /// <param name="tolerance">Tolerance</param>
    /// <param name="result">The result.</param>
    /// <returns>True if they match; false otherwise.</returns>
    private static bool NativeMatch(
      string loc,
      Dictionary<object, object> compared,
      Stack<ObjectPair> stack,
      Type type, object A, object B,
      double tolerance,
      ref Mismatch result)
    {
      MethodInfo mi = type.GetMethod("GetObjectData",
        BindingFlags.Instance | BindingFlags.DeclaredOnly
        | BindingFlags.NonPublic | BindingFlags.Public);
      if(mi == null)
      {
        if (A is ISerializable)
        {
          // ISerializable.GetObjectData must be explicitly defined.
          mi = typeof (ISerializable).GetMethod("GetObjectData",
            BindingFlags.Instance | BindingFlags.DeclaredOnly
              | BindingFlags.NonPublic | BindingFlags.Public);
        }
        if(mi==null)
        {
          throw new ToolkitException(String.Format(
            "Cannot check native data in type {0}", type.FullName));
        }
      }

      // Get Serialization info and compare.
      StreamingContext ctx = new StreamingContext();
      FormatterConverter cvt = new FormatterConverter();
      SerializationInfo ainfo = new SerializationInfo(type, cvt);
      mi.Invoke(A, new object[] { ainfo, ctx });

      SerializationInfo binfo = new SerializationInfo(type, cvt);
      mi.Invoke(B, new object[] { binfo, ctx });

      SerializationInfoEnumerator e = ainfo.GetEnumerator();
      while (e.MoveNext())
      {
        object aobj = e.Value;
        object bobj = binfo.GetValue(e.Name, e.ObjectType);
        if (!Match(loc + '.' + e.Name, compared,stack,
          aobj, bobj, tolerance, ref result))
        {
          return false;
        }
      } // end while moveNext

      return true;
    }

    /// <summary>
    ///   Get all fields of a type, including the fields of
    ///   all the super classes, excluding the fields marked
    ///   by <see cref="MutableAttribute"/>.
    /// </summary>
    /// <param name="type">Type</param>
    /// <returns>An array of fields.</returns>
    private static FieldInfo[] GetFields(Type type)
    {
      const BindingFlags flags = 
        BindingFlags.Instance | BindingFlags.DeclaredOnly
        | BindingFlags.NonPublic | BindingFlags.Public;

      List<FieldInfo> list = new List<FieldInfo>();
      do
      {
        FieldInfo[] fields;

        if (type == typeof (MulticastDelegate))
        {
          type = type.BaseType;
          if (type != null)
          {
            fields = type.GetFields(flags);
            list.AddRange(fields
              .Where(f => f.FieldType.IsClass)
              .ToArray());
          }
          return list.ToArray();
        }

        fields = type.GetFields(flags);
        for (int i = 0; i < fields.Length; ++i)
        {
          FieldInfo fi = fields[i];
          if (IsMutable(fi))
            continue; // ignore mutable fields
          list.Add(fi);
        }
        type = type.BaseType;
      } while (type != null);
      return list.ToArray();
    }

    private static bool IsMutable(FieldInfo fi)
    {
      const BindingFlags flags =
        BindingFlags.Instance | BindingFlags.DeclaredOnly
          | BindingFlags.NonPublic | BindingFlags.Public;
      const string bk = ">k__BackingField";
      if (fi.Name.EndsWith(bk))
      {
        var name = fi.Name.Substring(1, fi.Name.Length - bk.Length - 1);
        if (fi.DeclaringType != null)
        {
          var pi = fi.DeclaringType.GetProperty(name, flags);
          if (pi != null)
            return Attribute.IsDefined(pi, typeof (MutableAttribute));
        }
      }
      return Attribute.IsDefined(fi, typeof (MutableAttribute));
    }

    private struct ObjectPair
    {
      public ObjectPair(object A, object B)
      {
        First = A; Second = B;
      }
      public readonly object First;
      public readonly object Second;
    };

    private readonly object saved_;
    #endregion Implementation
  }

  /// <summary>
  ///   Exception representing state mismatch between two objects.
  /// </summary>
  public class ObjectStateMismatchException : Exception
  {
    /// <summary>Gets the first mismatch</summary>
    public ObjectStatesChecker.Mismatch Mismatch { get; private set; }

    /// <summary>Initialize a state mismatch exception</summary>
    /// <param name="mismatch">The first mismatch</param>
    public ObjectStateMismatchException(ObjectStatesChecker.Mismatch mismatch)
      : base(mismatch.ToString())
    {
      Mismatch = mismatch;
    }
  }
}
