/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.ComponentModel;

namespace BaseEntity.Toolkit.Numerics {


			/// <include file='swig/Distribution.xml' path='doc/members/member[@name="T:Distribution"]/*' />
			[Serializable]
			public abstract class  Distribution : IDisposable {
  private HandleRef swigCPtr;

  /// <exclude />
  protected bool swigCMemOwn;

  public Distribution(IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new HandleRef(this, cPtr);
  }

  /// <exclude />
  public static HandleRef getCPtr(Distribution obj) {
    return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
  }

  /// <exclude />
  ~Distribution() {
    Dispose();
  }

  /// <exclude />
  public virtual void Dispose() {
    if(swigCPtr.Handle != IntPtr.Zero && swigCMemOwn) {
      swigCMemOwn = false;
      BaseEntityPINVOKE.delete_Distribution(swigCPtr);
    }
    swigCPtr = new HandleRef(null, IntPtr.Zero);
    GC.SuppressFinalize(this);
  }

			///<exclude/>
			[SecurityPermission(SecurityAction.Demand, SerializationFormatter=true)]
			public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
			{ 
			}

			public Distribution(IntPtr cPtr, 
														bool cMemoryOwn, 
														SerializationInfo info,
														StreamingContext context)
			{
				swigCMemOwn = cMemoryOwn;
				swigCPtr = new HandleRef(this, cPtr);
			}
			
  /// <include file='swig/Distribution.xml' path='doc/members/member[@name="M:Distribution_pdf"]/*' />
  public virtual double pdf(double x) {
    double ret = BaseEntityPINVOKE.Distribution_pdf(swigCPtr, x);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/Distribution.xml' path='doc/members/member[@name="M:Distribution_cdf"]/*' />
  public virtual double cdf(double x) {
    double ret = BaseEntityPINVOKE.Distribution_cdf(swigCPtr, x);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/Distribution.xml' path='doc/members/member[@name="M:Distribution_inverseCdf"]/*' />
  public virtual double inverseCdf(double y) {
    double ret = BaseEntityPINVOKE.Distribution_inverseCdf(swigCPtr, y);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
