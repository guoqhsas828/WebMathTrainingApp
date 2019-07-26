using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Native;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Util.Native;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// Native util class
  /// </summary>
  public static class NativeUtil
  {
    public static FixedSizeList<int> Int32Array(
      IntPtr data, int size, object holder)
    {
      if (data == IntPtr.Zero)
        return EmptyArrayOfInt32s;
      return new NativeArrayOfInt32s(
        size, data, holder);
    }

    public static unsafe NativeArray<int> Int32Array(
      int* data, int size, object holder)
    {
      if (data == null)
        return EmptyArrayOfInt32s;
      return new NativeArrayOfInt32s(
        size, data, holder);
    }

    /// <summary>
    /// Double Array
    /// </summary>
    /// <param name="data">data pointer</param>
    /// <param name="size">size</param>
    /// <param name="holder">holder</param>
    /// <returns></returns>
    public static FixedSizeList<double> DoubleArray(
      IntPtr data, int size, object holder)
    {
      if (data == IntPtr.Zero)
        return EmptyArrayOfDoubles;
      return new NativeArrayOfDoubles(
        size, data, holder);
    }

    public static unsafe NativeArray<double> DoubleArray(
      double* data, int size, object holder)
    {
      if (data == null)
        return EmptyArrayOfDoubles;
      return new NativeArrayOfDoubles(
        size, data, holder);
    }

    public static void CopyFrom<T>(this IList<T> list, T[] array)
    {
      int size = list.Count;
      for (int i = 0; i < size; ++i)
        list[i] = array[i];
      return;
    }
    private static NativeArray<double> EmptyArrayOfDoubles
      = new NativeArrayOfDoubles(0, new IntPtr(0), null);
    private static NativeArray<int> EmptyArrayOfInt32s
      = new NativeArrayOfInt32s(0, new IntPtr(0), null);
  } // class NativeUtil
}

namespace BaseEntity.Toolkit.Util.Native
{
  public class NativeArrayOfDoubles : NativeArray<double>
  {
    public NativeArrayOfDoubles(int size, IntPtr data, object holder)
      : base(size, data, holder)
    {
    }

    public unsafe NativeArrayOfDoubles(int size, double* data, object holder)
      : base(size, data, holder)
    {
    }

    public override double this[int index]
    {
      get
      {
        Debug.Assert(index >= 0 && index < Count);
        unsafe
        {
          return ((double*)Pointer)[index];
        }
      }
      set
      {
        Debug.Assert(index >= 0 && index < Count);
        unsafe
        {
          ((double*)Pointer)[index] = value;
        }
      }
    }

    public override int IndexOf(double item)
    {
      unsafe
      {
        var data = (double*) Pointer;
        int size = Count;
        for (int i = 0; i < size; ++i)
          if (Equals(data[i], item)) return i;
        return -1;
      }
    }

    public override void CopyTo(double[] array, int arrayIndex)
    {
      unsafe
      {
        var data = (double*) Pointer;
        int size = Count;
        for (int i = 0; i < size; ++i)
          array[i + arrayIndex] = data[i];
      }
    }
  }

  public class NativeArrayOfInt32s : NativeArray<int>
  {
    public NativeArrayOfInt32s(int size, IntPtr data, object holder)
      : base(size, data, holder)
    {
    }

    public unsafe NativeArrayOfInt32s(int size, int* data, object holder)
      : base(size, data, holder)
    {
    }

    public override int this[int index]
    {
      get
      {
        Debug.Assert(index >= 0 && index < Count);
        unsafe
        {
          return ((int*)Pointer)[index];
        }
      }
      set
      {
        Debug.Assert(index >= 0 && index < Count);
        unsafe
        {
          ((int*)Pointer)[index] = value;
        }
      }
    }

    public override int IndexOf(int item)
    {
      unsafe
      {
        var data = (int*) Pointer;
        int size = Count;
        for (int i = 0; i < size; ++i)
          if (Equals(data[i], item)) return i;
        return -1;
      }
    }

    public override void CopyTo(int[] array, int arrayIndex)
    {
      unsafe
      {
        var data = (int*) Pointer;
        int size = Count;
        for (int i = 0; i < size; ++i)
          array[i + arrayIndex] = data[i];
      }
    }
  } // class NativeArrayOfInt32s

  public class NativeArrayOfStructs<T> : NativeArray<T> where T : struct
  {
    private static readonly int Size = Marshal.SizeOf(typeof(T));

    public NativeArrayOfStructs(int size, IntPtr data, object holder)
      : base(size, data, holder)
    {
    }

    public override T this[int index]
    {
      get
      {
        Debug.Assert(index >= 0 && index < Count);
        unsafe
        {
          return (T) Marshal.PtrToStructure(
            new IntPtr(((byte*) Pointer) + Size),
            typeof (T));
        }
      }
      set
      {
        Debug.Assert(index >= 0 && index < Count);
        unsafe
        {
          Marshal.StructureToPtr(value,
            new IntPtr(((byte*) Pointer) + Size), true);
        }
      }
    }
  } // NativeArrayOfStructs

  /// <summary>
  /// 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public abstract unsafe class NativeArray<T> : FixedSizeList<T>, INativeObject
  {
    public override bool IsReadOnly
    {
      get { return false; }
    }

    public override int IndexOf(T item)
    {
      int size = size_;
      for (int i = 0; i < size; ++i)
        if (Equals(this[i], item)) return i;
      return -1;
    }

    public override void CopyTo(T[] array, int arrayIndex)
    {
      int size = size_;
      for (int i = 0; i < size; ++i)
        array[i + arrayIndex] = this[i];
    }

    public override int Count
    {
      get { return size_; }
    }

    public HandleRef HandleRef
    {
      get { return new HandleRef(holder_, new IntPtr(data_)); }
    }

    public void* Pointer
    {
      get{ return data_;}
    }

    protected NativeArray(int size, IntPtr data, object holder)
    {
      size_ = size;
      data_ = data.ToPointer();
      holder_ = holder;
    }

    protected NativeArray(int size, void* data, object holder)
    {
      size_ = size;
      data_ = data;
      holder_ = holder;
    }

    private readonly int size_;
    private readonly void* data_;
    private readonly object holder_;
  } // class FixedSizeList<T>

}
