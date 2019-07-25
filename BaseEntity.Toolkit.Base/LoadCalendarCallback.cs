// 
// Copyright (c)    2002-2016. All rights reserved.
// 
using System;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <exclude />
  [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(IntArrayMarshaler))]
  public delegate int[] LoadCalendarCallback([MarshalAs(UnmanagedType.LPWStr)] string calendarName);


  /// <exclude />
  internal class IntArrayMarshaler : ICustomMarshaler
  {
    [StructLayout(LayoutKind.Sequential)]
    private struct Array
    {
      // NB: Layout must match EXACTLY the layout of qn::Array<T>!
      /// <exclude />
      public int Count;

      /// <exclude />
      public IntPtr Data;

      /// <exclude />
      public int Owner;

      /// <exclude />
      public IntPtr Gch;
    }

    private static readonly int ArraySize
      = Marshal.SizeOf(typeof(Array)) + Marshal.SizeOf(typeof(int));

    /// <exclude />
    public static ICustomMarshaler GetInstance(string cookie)
    {
      return Marshaler;
    }

    /// <exclude />
    public int GetNativeDataSize()
    {
      return -1;
    }

    /// <exclude />
    public void CleanUpNativeData(IntPtr pNativeData)
    {
      var data = Marshal.PtrToStructure<Array>(pNativeData);

      if (data.Owner != 0)
      {
        if (data.Gch == IntPtr.Zero)
          Marshal.FreeCoTaskMem(data.Data);
        else
        {
          // Unpin
          ((GCHandle) data.Gch).Free();
          data.Gch = IntPtr.Zero;
        }

        data.Data = IntPtr.Zero;
      }

      IntPtr pBlock = IntPtr.Subtract(pNativeData, 4);
      Marshal.FreeCoTaskMem(pBlock);
    }

    /// <exclude />
    public void CleanUpManagedData(object managedObj)
    {
      return;
    }

    /// <exclude />
    public IntPtr MarshalManagedToNative(object managedObj)
    {
      switch (managedObj)
      {
      case null:
        throw new ArgumentNullException(nameof(managedObj));
      case int[] _:
      case int[,] _:
        System.Array srcData = (System.Array) managedObj;
        IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
        IntPtr pNativeData = IntPtr.Add(ptrBlock, 4);
        Marshal.WriteInt32(ptrBlock, 1);

        GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
        var array = new Array
        {
          Count = srcData.Length,
          Data = gch.AddrOfPinnedObject(),
          Gch = (IntPtr) gch,
          Owner = 1,
        };
        Marshal.StructureToPtr(array, pNativeData, false);
        return pNativeData;
      }

      throw new ToolkitException($"Invalid type [{managedObj.GetType()}]");
    }

    /// <exclude />
    public object MarshalNativeToManaged(IntPtr pNativeData)
    {
      if (pNativeData == IntPtr.Zero)
        return null;

      var array = Marshal.PtrToStructure<Array>(pNativeData);
      int[] result = new int[array.Count];
      var ptr = array.Data;
      for (int i = 0; i < result.Length; i++)
      {
        result[i] = Marshal.ReadInt32(ptr);
        ptr = IntPtr.Add(ptr, sizeof(int));
      }
      return result;
    }

    /// <exclude />
    private static readonly ICustomMarshaler Marshaler = new IntArrayMarshaler();
  }
}