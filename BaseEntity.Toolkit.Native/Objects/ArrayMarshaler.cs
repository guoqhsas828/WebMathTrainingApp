using System;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Shared
{
    /// <exclude />
    public static class IntPtrExtension
    {
        /// <exclude />
        public static IntPtr Add(this IntPtr basePtr, int offset)
        {
            return (x64_)
                     ? new IntPtr(basePtr.ToInt64() + offset)
                     : new IntPtr(basePtr.ToInt32() + offset);
        }

        /// <exclude />
        public static IntPtr Subtract(this IntPtr basePtr, int offset)
        {
            return (x64_)
                     ? new IntPtr(basePtr.ToInt64() - offset)
                     : new IntPtr(basePtr.ToInt32() - offset);
        }

        private static readonly bool x64_ = (IntPtr.Size == 8);
    }



    /// <exclude />
    public abstract class ArrayMarshaler
    {
        /// <exclude />
        public struct Array
        {
            // NB: Layout must match EXACTLY the layout of qn::Array<T>!
            /// <exclude />
            public int n_;
            /// <exclude />
            public IntPtr data_;
            /// <exclude />
            public int owner_;
            /// <exclude />
            public IntPtr gch_;

            internal unsafe T[] ToArray<T>()
            {
                T[] result = new T[n_];
                Type type = typeof(T);
                int size = Marshal.SizeOf(type);
                byte* p = (byte*)data_;
                for (int i = 0; i < result.Length; p += size, i++)
                {
                    result[i] = (T)Marshal.PtrToStructure(new IntPtr(p), type);
                }
                return result;
            }

            public unsafe T Get<T>(int i)
            {
                if (i < 0 || i >= n_)
                {
                    throw new ToolkitException("Index out of range");
                }
                Type type = typeof(T);
                int size = Marshal.SizeOf(type);
                byte* p = ((byte*)data_) + size * i;
                return (T)Marshal.PtrToStructure(new IntPtr(p), type);
            }
        }

        /// <exclude />
        protected static int ArraySize = Marshal.SizeOf(typeof(Array)) + Marshal.SizeOf(typeof(int));
    }


    /// <exclude />
    public unsafe class ArrayOfStructMarshaler<T> : ArrayMarshaler, ICustomMarshaler where T : struct
    {
        /// <exclude />
        public static ICustomMarshaler GetInstance(string cookie)
        {
            return marshaler_;
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
                throw new ArgumentNullException("managedObj");

            if (managedObj is T[] || managedObj is T[,])
            {
                System.Array srcData = (System.Array)managedObj;
                IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
                IntPtr pNativeData = ptrBlock.Add(4);
                int* pi = (int*)ptrBlock.ToPointer();
                *pi = 1;
                Array* pDst = (Array*)pNativeData;
                pDst->n_ = srcData.Length;
                GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
                pDst->data_ = gch.AddrOfPinnedObject();
                pDst->owner_ = 1;
                pDst->gch_ = (IntPtr)gch;
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
            Array* pSrc = (Array*)pNativeData;
            T[] result = new T[pSrc->n_];
            Type type = typeof(T);
            int size = Marshal.SizeOf(typeof(T));
            byte* p = (byte*)pSrc->data_;
            for (int i = 0; i < result.Length; p += size, i++)
            {
                result[i] = (T)Marshal.PtrToStructure(new IntPtr(p), type);
            }
            return result;
        }

        /// <exclude />
        private static readonly ICustomMarshaler marshaler_ = new ArrayOfStructMarshaler<T>();
    }

    // ArrayOfDoubleMarshaler

    /// <exclude />
    public unsafe class ArrayOfDoubleMarshaler : ArrayMarshaler, ICustomMarshaler
    {
        /// <exclude />
        public static ICustomMarshaler GetInstance(string cookie)
        {
            return marshaler_;
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
                throw new ArgumentNullException("managedObj");

            if (managedObj is double[] || managedObj is double[,])
            {
                System.Array srcData = (System.Array)managedObj;
                IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
                IntPtr pNativeData = ptrBlock.Add(4);
                int* pi = (int*)ptrBlock.ToPointer();
                *pi = 1;
                Array* pDst = (Array*)pNativeData;
                pDst->n_ = srcData.Length;
                GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
                pDst->data_ = gch.AddrOfPinnedObject();
                pDst->owner_ = 1;
                pDst->gch_ = (IntPtr)gch;
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
            Array* pSrc = (Array*)pNativeData;
            double[] result = new double[pSrc->n_];
            double* pData = (double*)pSrc->data_;
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = pData[i];
            }
            return result;
        }

        /// <exclude />
        private static readonly ICustomMarshaler marshaler_ = new ArrayOfDoubleMarshaler();
    }

  // ArrayOfFloatMarshaler

  /// <exclude />
  public unsafe class ArrayOfFloatMarshaler : ArrayMarshaler, ICustomMarshaler
  {
    /// <exclude />
    public static ICustomMarshaler GetInstance(string cookie)
    {
      return _marshaler;
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
        throw new ArgumentNullException("managedObj");

      if (managedObj is float[] || managedObj is float[,])
      {
        System.Array srcData = (System.Array)managedObj;
        IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
        IntPtr pNativeData = ptrBlock.Add(4);
        int* pi = (int*)ptrBlock.ToPointer();
        *pi = 1;
        Array* pDst = (Array*)pNativeData;
        pDst->n_ = srcData.Length;
        GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
        pDst->data_ = gch.AddrOfPinnedObject();
        pDst->owner_ = 1;
        pDst->gch_ = (IntPtr)gch;
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
      Array* pSrc = (Array*)pNativeData;
      float[] result = new float[pSrc->n_];
      float* pData = (float*)pSrc->data_;
      for (int i = 0; i < result.Length; i++)
      {
        result[i] = pData[i];
      }
      return result;
    }

    /// <exclude />
    private static readonly ICustomMarshaler _marshaler = new ArrayOfFloatMarshaler();
  }

  // ArrayOfIntMarshaler
  /// <exclude />
  public unsafe class ArrayOfIntMarshaler : ArrayMarshaler, ICustomMarshaler
    {
        /// <exclude />
        public static ICustomMarshaler GetInstance(string cookie)
        {
            return marshaler_;
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
                throw new ArgumentNullException("managedObj");

            if (managedObj is int[] || managedObj is int[,])
            {
                System.Array srcData = (System.Array)managedObj;
                IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
                IntPtr pNativeData = ptrBlock.Add(4);
                int* pi = (int*)ptrBlock.ToPointer();
                *pi = 1;
                Array* pDst = (Array*)pNativeData;
                pDst->n_ = srcData.Length;
                GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
                pDst->data_ = gch.AddrOfPinnedObject();
                pDst->owner_ = 1;
                pDst->gch_ = (IntPtr)gch;
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
            Array* pSrc = (Array*)pNativeData;
            int[] result = new int[pSrc->n_];
            int* pData = (int*)pSrc->data_;
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = pData[i];
            }
            return result;
        }

        /// <exclude />
        private static ICustomMarshaler marshaler_ = new ArrayOfIntMarshaler();
    }


  // ArrayOfIntPtrMarshaler 

  /// <exclude />
  public unsafe class ArrayOfIntPtrMarshaler : ArrayMarshaler, ICustomMarshaler
  {
    /// <exclude />
    public static ICustomMarshaler GetInstance(string cookie)
    {
      return marshaler_;
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
        throw new ArgumentNullException("managedObj");

      if (managedObj is IntPtr[])
      {
        System.Array srcData = (System.Array)managedObj;
        IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
        IntPtr pNativeData = ptrBlock.Add(4);
        int* pi = (int*)ptrBlock.ToPointer();
        *pi = 1;
        Array* pDst = (Array*)pNativeData;
        pDst->n_ = srcData.Length;
        GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
        pDst->data_ = gch.AddrOfPinnedObject();
        pDst->owner_ = 1;
        pDst->gch_ = (IntPtr)gch;
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
      Array* pSrc = (Array*)pNativeData;
      IntPtr[] result = new IntPtr[pSrc->n_];
      IntPtr* pData = (IntPtr*)pSrc->data_;
      for (int i = 0; i < result.Length; i++)
      {
        result[i] = pData[i];
      }
      return result;
    }

    /// <exclude />
    private static readonly ICustomMarshaler marshaler_ = new ArrayOfIntPtrMarshaler();
  }

  /// <exclude />
  unsafe public abstract class Array2DMarshaler
    {
        /// <exclude />
        protected struct Array2D
        {
            // NB: Layout must match EXACTLY the layout of qn::Array2D<T>!
            /// <exclude />
            public int nrow_;
            /// <exclude />
            public int ncol_;
            /// <exclude />
            public IntPtr data_;
            /// <exclude />
            public int owner_;
            /// <exclude />
            public IntPtr gch_;
        }

        /// <exclude />
        protected static int Array2DSize = Marshal.SizeOf(typeof(Array2D)) + Marshal.SizeOf(typeof(int));
    }

    // Array2DOfDoubleMarshaler

    /// <exclude />
    public unsafe class Array2DOfDoubleMarshaler : Array2DMarshaler, ICustomMarshaler
    {
        /// <exclude />
        public static ICustomMarshaler GetInstance(string cookie)
        {
            return marshaler_;
        }

        /// <exclude />
        public int GetNativeDataSize()
        {
            return -1;
        }

        /// <exclude />
        public void CleanUpNativeData(IntPtr pNativeData)
        {
            Array2D* pData = (Array2D*)pNativeData;

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
                throw new ArgumentNullException("managedObj");

            int nrow;
            int ncol;
            if (managedObj is double[])
            {
                nrow = ((double[])managedObj).Length;
                ncol = 1;
            }
            else if (managedObj is double[,])
            {
                double[,] a = (double[,])managedObj;
                nrow = a.GetLength(0);
                ncol = a.GetLength(1);
            }
            else
            {
                throw new ToolkitException(String.Format(
                  "Invalid type [{0}]", managedObj.GetType()));
            }

            Array srcData = (Array)managedObj;
            IntPtr ptrBlock = Marshal.AllocCoTaskMem(Array2DSize);
            IntPtr pNativeData = ptrBlock.Add(4);
            int* pi = (int*)ptrBlock.ToPointer();
            *pi = 1;
            Array2D* pDst = (Array2D*)pNativeData;
            pDst->nrow_ = nrow;
            pDst->ncol_ = ncol;
            GCHandle gch = GCHandle.Alloc(srcData, GCHandleType.Pinned);
            pDst->data_ = gch.AddrOfPinnedObject();
            pDst->owner_ = 1;
            pDst->gch_ = (IntPtr)gch;
            return pNativeData;
        }

        /// <exclude />
        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            if (pNativeData.ToPointer() == null)
                return null;
            Array2D* pSrc = (Array2D*)pNativeData;
            int nrow = pSrc->nrow_;
            int ncol = pSrc->ncol_;
            double[,] result = new double[nrow, ncol];
            double* pData = (double*)pSrc->data_;
            for (int i = 0, idx = 0; i < nrow; i++)
                for (int j = 0; j < ncol; j++)
                {
                    result[i, j] = pData[idx++];
                }
            return result;
        }

        /// <exclude />
        private static readonly ICustomMarshaler marshaler_ = new Array2DOfDoubleMarshaler();
    }


}
