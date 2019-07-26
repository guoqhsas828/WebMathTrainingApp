/*
 * ArrayOfDtMarshaler.cs
 *
 * Copyright (c)   2002-2008. All rights reserved.
 *
 */

using System;
using System.Runtime.InteropServices;

using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{

	/// <exclude />
  public unsafe class ArrayOfDtMarshaler : ArrayMarshaler, ICustomMarshaler
  {
		/// <exclude />
    public static ICustomMarshaler GetInstance( string cookie )
    {
      return marshaler_;
    }

    /// <exclude />
    public int GetNativeDataSize()
    {
      return -1;
    }

    /// <exclude />
    public void CleanUpNativeData( IntPtr pNativeData )
    {
      Array* pData = (Array*)pNativeData;

      if( pData->owner_ != 0 )
      {
        if( pData->gch_ == IntPtr.Zero )
          Marshal.FreeCoTaskMem( pData->data_ );
        else
        {
          // Unpin
          ( (GCHandle)pData->gch_ ).Free();
          pData->gch_ = IntPtr.Zero;
        }

        pData->data_ = IntPtr.Zero;
      }

      IntPtr pBlock = pNativeData.Subtract( 4 );
      Marshal.FreeCoTaskMem( pBlock );
    }

    /// <exclude />
    public void CleanUpManagedData( object managedObj )
    {
      return;
    }

		/// <exclude />
    public IntPtr MarshalManagedToNative( object managedObj )
    {
      if( managedObj == null )
        throw new ArgumentNullException( "managedObj" );

      if( managedObj is Dt[] )
      {
        Dt[] srcData = (Dt[])managedObj;
        IntPtr ptrBlock = Marshal.AllocCoTaskMem( ArraySize );
        IntPtr pNativeData = ptrBlock.Add( 4 );
        int* pi = (int*)ptrBlock.ToPointer();
        *pi = 1;
        Array* pDst = (Array*)pNativeData;
        pDst->n_ = srcData.Length;
        GCHandle gch = GCHandle.Alloc( srcData, GCHandleType.Pinned );
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
    public object MarshalNativeToManaged( IntPtr pNativeData )
    {
      if( pNativeData.ToPointer() == null )
        return null;
      Array* pSrc = (Array*)pNativeData;
      Dt[] result = new Dt[ pSrc->n_ ];
      Dt* pData = (Dt*)pSrc->data_;
      for( int i=0; i<result.Length; i++ )
      {
        result[i] = pData[i];
      }
      return result;
    }

    /// <exclude />
    private static readonly ICustomMarshaler marshaler_ = new ArrayOfDtMarshaler();
  }
}

