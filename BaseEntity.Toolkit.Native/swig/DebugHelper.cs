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

namespace BaseEntity.Toolkit.Util.Native {


/// <include file='swig/DebugHelper.xml' path='doc/members/member[@name="T:DebugHelper"]/*' />
[TypeConverter(typeof(ExpandableObjectConverter))]
public partial class DebugHelper : IDisposable {
  private HandleRef swigCPtr;

  /// <exclude />
  protected bool swigCMemOwn;

  public DebugHelper(IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new HandleRef(this, cPtr);
  }

  /// <exclude />
  public static HandleRef getCPtr(DebugHelper obj) {
    return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
  }

  /// <exclude />
  ~DebugHelper() {
    Dispose();
  }

  /// <exclude />
  public virtual void Dispose() {
    if(swigCPtr.Handle != IntPtr.Zero && swigCMemOwn) {
      swigCMemOwn = false;
      BaseEntityPINVOKE.delete_DebugHelper(swigCPtr);
    }
    swigCPtr = new HandleRef(null, IntPtr.Zero);
    GC.SuppressFinalize(this);
  }

  /// <include file='swig/DebugHelper.xml' path='doc/members/member[@name="M:DebugHelper_SetDumpOptions"]/*' />
  public static void SetDumpOptions(string pDumpDir, int dumpType, bool onlyDumpFirstException) {
    BaseEntityPINVOKE.DebugHelper_SetDumpOptions(pDumpDir, dumpType, onlyDumpFirstException);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/DebugHelper.xml' path='doc/members/member[@name="M:new_DebugHelper"]/*' />
  public DebugHelper() : this(BaseEntityPINVOKE.new_DebugHelper(), true) {
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
