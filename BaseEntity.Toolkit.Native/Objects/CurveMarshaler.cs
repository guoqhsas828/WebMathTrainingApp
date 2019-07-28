using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using System.Runtime.InteropServices;
using BaseEntity.Shared;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;
using Curve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace MagnoliaIG.ToolKits.RateCurves
{
    public unsafe class CurveMarshaler : ArrayMarshaler, ICustomMarshaler
    {
        public static ICustomMarshaler GetInstance(string cookie)
        {
            return marshaler_;
        }

        public int GetNativeDataSize()
        {
            return -1;
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            Array* pData = (Array*)pNativeData;
            if (pData->owner_ != 0)
            {
                if (pData->gch_ == IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pData->data_);
                else 
                {
                    //Unpin
                    ((GCHandle)pData->gch_).Free();
                    pData->gch_ = IntPtr.Zero;
                }

                pData->data_ = IntPtr.Zero;
            }

            IntPtr pBlock = pNativeData.Subtract(4);
            Marshal.FreeCoTaskMem(pBlock);
        }

        public void CleanUpManagedData(object managedObj)
        {
            return;
        }

        public IntPtr MarshalManagedToNative(object managedObj)
        {
            if (managedObj == null)
                throw new ArgumentNullException("managedObj");

            if (managedObj is System.Array)
            {
                object[] srcData = (object[])managedObj;
                IntPtr ptrBlock = Marshal.AllocCoTaskMem(ArraySize);
                IntPtr pNativeData = ptrBlock.Add(4);
                int* pi = (int*)ptrBlock.ToPointer();
                *pi = 1;
                Array* pDst = (Array*)pNativeData;
                pDst->n_ = srcData.Length;
                pDst->data_ = Marshal.AllocCoTaskMem(srcData.Length + Marshal.SizeOf(typeof(IntPtr)));
                IntPtr* pData = (IntPtr*)pDst->data_;
                for (int i = 0; i < srcData.Length; i++)
                {
                    pData[i] = Curve.getCPtr((Curve)srcData[i]).Handle;
                }
                pDst->owner_ = 1;
                pDst->gch_ = (IntPtr)0;
                return pNativeData;
            }
            else
                throw new ToolkitException(string.Format("Invalid type [{0}]", managedObj.GetType()));
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            if (pNativeData.ToPointer() == null)
                return null;

            Array* pSrc = (Array*)pNativeData;
            IntPtr* pData = (IntPtr*)pSrc->data_;

            Curve[] curves = new Curve[pSrc->n_];
            for (int i = 0; i < curves.Length; i++)
                curves[i] = new Curve(new NativeCurve(pData[i], true));

            return curves;
        }

        private static readonly ICustomMarshaler marshaler_ = new CurveMarshaler();
    }
}
