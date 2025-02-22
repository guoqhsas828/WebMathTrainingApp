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
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Numerics {


    /// <include file='swig/Extrap.xml' path='doc/members/member[@name="T:Extrap"]/*' />
  	[Serializable]
    public abstract class  Extrap : INativeSerializable, IDisposable {
  private HandleRef swigCPtr;

  /// <exclude />
  protected bool swigCMemOwn;

  public Extrap(IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new HandleRef(this, cPtr);
  }

  /// <exclude />
  public static HandleRef getCPtr(Extrap obj) {
    return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
  }

  /// <exclude />
  ~Extrap() {
    Dispose();
  }

  /// <exclude />
  public virtual void Dispose() {
    if(swigCPtr.Handle != IntPtr.Zero && swigCMemOwn) {
      swigCMemOwn = false;
      BaseEntityPINVOKE.delete_Extrap(swigCPtr);
    }
    swigCPtr = new HandleRef(null, IntPtr.Zero);
    GC.SuppressFinalize(this);
  }

		///<exclude/>
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter=true)]
		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{ 
		}

  	public Extrap(IntPtr cPtr, 
		  							bool cMemoryOwn, 
			  						SerializationInfo info,
				  					StreamingContext context)
  	{
	  	swigCMemOwn = cMemoryOwn;
		  swigCPtr = new HandleRef(this, cPtr);
  	}
  
  /// <include file='swig/Extrap.xml' path='doc/members/member[@name="M:Extrap_clone"]/*' />
  public virtual Extrap clone() 
    {
      IntPtr cPtr = BaseEntityPINVOKE.Extrap_clone(swigCPtr);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      if( cPtr == IntPtr.Zero ) {
        return null;
      }
      else {
        string typeName = String.Format(Interp.TypeFormatString, BaseEntityPINVOKE.Extrap_typeName( new HandleRef(this, cPtr)));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();

        Type type = Type.GetType( typeName );
        object o = type.Assembly.CreateInstance(type.FullName, false, 
                                                System.Reflection.BindingFlags.CreateInstance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance, 
                                                null, new object[]{cPtr, true}, null, null);

        return (Extrap)o;
      }
    }
  

  /// <include file='swig/Extrap.xml' path='doc/members/member[@name="M:Extrap_typeName"]/*' />
  protected string typeName() {
    string ret = BaseEntityPINVOKE.Extrap_typeName(swigCPtr);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/Extrap.xml' path='doc/members/member[@name="M:Extrap_initialize"]/*' />
  public virtual void initialize(Interp interp, InterpFn data) {
    BaseEntityPINVOKE.Extrap_initialize(swigCPtr, Interp.getCPtr(interp), InterpFn.getCPtr(data));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/Extrap.xml' path='doc/members/member[@name="M:Extrap_extrapLower"]/*' />
  public virtual double extrapLower(InterpFn data, double x) {
    double ret = BaseEntityPINVOKE.Extrap_extrapLower(swigCPtr, InterpFn.getCPtr(data), x);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/Extrap.xml' path='doc/members/member[@name="M:Extrap_extrapUpper"]/*' />
  public virtual double extrapUpper(InterpFn data, double x) {
    double ret = BaseEntityPINVOKE.Extrap_extrapUpper(swigCPtr, InterpFn.getCPtr(data), x);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
