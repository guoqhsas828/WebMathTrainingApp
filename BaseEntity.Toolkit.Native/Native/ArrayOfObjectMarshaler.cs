using System;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Native
{
  /// <summary>
  ///  Allocate an array of handles and pass it to native codes.
  ///  Memory allocation involved.
  /// </summary>
  public unsafe class ArrayOfObjectMarshaler : ArrayMarshaler, ICustomMarshaler
  {
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
      Array* pData = (Array*)pNativeData;

      if (pData->owner_ != 0)
      {
        if (pData->gch_ == IntPtr.Zero)
          Marshal.FreeCoTaskMem(pData->data_);
        else
        {
          // Unpin
          ((GCHandle)pData->gch_).Free();
          pData->gch_ = IntPtr.Zero;
        }

        pData->data_ = IntPtr.Zero;
      }

      IntPtr pBlock = pNativeData.Subtract(4);
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
      if (managedObj == null)
        throw new ArgumentNullException(nameof(managedObj));

      if (managedObj is System.Array)
      {
        object[] srcData = (object[])managedObj;
        IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
        IntPtr pNativeData = ptrBlock.Add(4);
        int* pi = (int*)ptrBlock.ToPointer();
        *pi = 1;
        Array* pDst = (Array*)pNativeData;
        pDst->n_ = srcData.Length;
        pDst->data_ = Marshal.AllocCoTaskMem(srcData.Length * Marshal.SizeOf(typeof(IntPtr)));
        IntPtr* pData = (IntPtr*)pDst->data_;
        for (int i = 0; i < srcData.Length; i++)
        {
          var curve = (INativeCurve)srcData[i];
          pData[i] = curve?.HandleRef.Handle ?? IntPtr.Zero;
        }
        pDst->owner_ = 1;
        pDst->gch_ = (IntPtr)0;
        return pNativeData;
      }
      else
      {
        throw new ToolkitException(String.Format(
          "Invalid type [{0}]", managedObj.GetType()));
      }
    }

    /// <exclude />
    public object MarshalNativeToManaged(IntPtr pNativeData)
    {
      if (pNativeData.ToPointer() == null)
        return null;
      throw new NotSupportedException(
        "Convert native to managed array of curves not supported");
    }

    /// <exclude />
    private static readonly ICustomMarshaler Marshaler = new ArrayOfObjectMarshaler();
  }

}
