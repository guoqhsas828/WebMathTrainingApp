//
// Copyright (c)   2002-2011. All rights reserved.
//
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Numerics
{
  [Serializable]
  sealed partial class Interpolator : INativeSerializable
  {
    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      if (swigCPtr.Handle != IntPtr.Zero && swigCMemOwn)
      {
        swigCMemOwn = false;
        BaseEntityPINVOKE.delete_Interpolator(swigCPtr);
      }
      swigCPtr = new HandleRef(null, IntPtr.Zero);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets the count of data points.
    /// </summary>
    /// <remarks></remarks>
    public int Count
    {
      get { return swigCPtr.Handle == IntPtr.Zero ? 0 : getSize(); }
    }

    /// <summary>
    /// Gets the Interp object.
    /// </summary>
    /// <remarks></remarks>
    public Interp Interp
    {
      get { return swigCPtr.Handle == IntPtr.Zero ? null : getInterp(); }
    }

    #region ISerializable Members

    Interpolator(SerializationInfo info, StreamingContext context)
    {
      var x = (double[])info.GetValue("x_", typeof(double[]));
      if (x.Length == 0)
      {
        swigCPtr = new HandleRef(null, IntPtr.Zero);
        swigCMemOwn = false;
        return;
      }
      var y = (double[])info.GetValue("y_", typeof(double[]));
      var s = (InterpScheme)info.GetValue("interp_", typeof(InterpScheme));
      var interp = s.ToInterp();
      var obj = new Interpolator(interp, x, y);
      swigCPtr = new HandleRef(this, obj.swigCPtr.Handle);
      swigCMemOwn = true;
      obj.swigCMemOwn = false;
      obj.Dispose();
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
      int n = Count;
      double[] x = new double[n], y = new double[n];
      for (int i = 0; i < n; ++i)
      {
        getPoint(i, ref x[i], ref y[i]);
      }
      info.AddValue("x_", x);
      info.AddValue("y_", y);
      info.AddValue("interp_", InterpScheme.FromInterp(Interp));
    }

    #endregion
  }
}
