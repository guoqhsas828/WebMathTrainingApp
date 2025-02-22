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


/// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="T:LinearAlgebra"]/*' />
[TypeConverter(typeof(ExpandableObjectConverter))]
public partial class LinearAlgebra : IDisposable {
  private HandleRef swigCPtr;

  /// <exclude />
  protected bool swigCMemOwn;

  public LinearAlgebra(IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new HandleRef(this, cPtr);
  }

  /// <exclude />
  public static HandleRef getCPtr(LinearAlgebra obj) {
    return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
  }

  /// <exclude />
  ~LinearAlgebra() {
    Dispose();
  }

  /// <exclude />
  public virtual void Dispose() {
    if(swigCPtr.Handle != IntPtr.Zero && swigCMemOwn) {
      swigCMemOwn = false;
      BaseEntityPINVOKE.delete_LinearAlgebra(swigCPtr);
    }
    swigCPtr = new HandleRef(null, IntPtr.Zero);
    GC.SuppressFinalize(this);
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Add__SWIG_0"]/*' />
  public static double[] Add(double[] A, double[] B) 
  {
    double[] ret = BaseEntityPINVOKE.LinearAlgebra_Add__SWIG_0(A, B);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }


  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Add__SWIG_1"]/*' />
  public static Util.MatrixOfDoubles Add(Util.MatrixOfDoubles A, Util.MatrixOfDoubles B) {
    IntPtr cPtr = BaseEntityPINVOKE.LinearAlgebra_Add__SWIG_1(Util.MatrixOfDoubles.getCPtr(A), Util.MatrixOfDoubles.getCPtr(B));
    Util.MatrixOfDoubles ret = (cPtr == IntPtr.Zero) ? null : new Util.MatrixOfDoubles(cPtr, false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Subtract__SWIG_0"]/*' />
  public static double[] Subtract(double[] A, double[] B) 
  {
    double[] ret = BaseEntityPINVOKE.LinearAlgebra_Subtract__SWIG_0(A, B);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }


  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Subtract__SWIG_1"]/*' />
  public static Util.MatrixOfDoubles Subtract(Util.MatrixOfDoubles A, Util.MatrixOfDoubles B) {
    IntPtr cPtr = BaseEntityPINVOKE.LinearAlgebra_Subtract__SWIG_1(Util.MatrixOfDoubles.getCPtr(A), Util.MatrixOfDoubles.getCPtr(B));
    Util.MatrixOfDoubles ret = (cPtr == IntPtr.Zero) ? null : new Util.MatrixOfDoubles(cPtr, false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Multiply__SWIG_0"]/*' />
  public static double Multiply(double[] A, double[] B) {
    double ret = BaseEntityPINVOKE.LinearAlgebra_Multiply__SWIG_0(A, B);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Multiply__SWIG_1"]/*' />
  public static Util.MatrixOfDoubles Multiply(Util.MatrixOfDoubles A, Util.MatrixOfDoubles B) {
    IntPtr cPtr = BaseEntityPINVOKE.LinearAlgebra_Multiply__SWIG_1(Util.MatrixOfDoubles.getCPtr(A), Util.MatrixOfDoubles.getCPtr(B));
    Util.MatrixOfDoubles ret = (cPtr == IntPtr.Zero) ? null : new Util.MatrixOfDoubles(cPtr, false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_RightMultiply"]/*' />
  public static double[] RightMultiply(Util.MatrixOfDoubles A, double[] x) 
  {
    double[] ret = BaseEntityPINVOKE.LinearAlgebra_RightMultiply(Util.MatrixOfDoubles.getCPtr(A), x);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }


  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_LeftMultiply"]/*' />
  public static double[] LeftMultiply(double[] x, Util.MatrixOfDoubles A) 
  {
    double[] ret = BaseEntityPINVOKE.LinearAlgebra_LeftMultiply(x, Util.MatrixOfDoubles.getCPtr(A));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }


  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_ElementMultiply__SWIG_0"]/*' />
  public static double[] ElementMultiply(double[] x, double[] y) 
  {
    double[] ret = BaseEntityPINVOKE.LinearAlgebra_ElementMultiply__SWIG_0(x, y);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }


  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_ElementMultiply__SWIG_1"]/*' />
  public static Util.MatrixOfDoubles ElementMultiply(Util.MatrixOfDoubles A, Util.MatrixOfDoubles B) {
    IntPtr cPtr = BaseEntityPINVOKE.LinearAlgebra_ElementMultiply__SWIG_1(Util.MatrixOfDoubles.getCPtr(A), Util.MatrixOfDoubles.getCPtr(B));
    Util.MatrixOfDoubles ret = (cPtr == IntPtr.Zero) ? null : new Util.MatrixOfDoubles(cPtr, false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_ElementDivide__SWIG_0"]/*' />
  public static double[] ElementDivide(double[] A, double[] B) 
  {
    double[] ret = BaseEntityPINVOKE.LinearAlgebra_ElementDivide__SWIG_0(A, B);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }


  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_ElementDivide__SWIG_1"]/*' />
  public static Util.MatrixOfDoubles ElementDivide(Util.MatrixOfDoubles A, Util.MatrixOfDoubles B) {
    IntPtr cPtr = BaseEntityPINVOKE.LinearAlgebra_ElementDivide__SWIG_1(Util.MatrixOfDoubles.getCPtr(A), Util.MatrixOfDoubles.getCPtr(B));
    Util.MatrixOfDoubles ret = (cPtr == IntPtr.Zero) ? null : new Util.MatrixOfDoubles(cPtr, false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Transpose"]/*' />
  public static Util.MatrixOfDoubles Transpose(Util.MatrixOfDoubles A) {
    IntPtr cPtr = BaseEntityPINVOKE.LinearAlgebra_Transpose(Util.MatrixOfDoubles.getCPtr(A));
    Util.MatrixOfDoubles ret = (cPtr == IntPtr.Zero) ? null : new Util.MatrixOfDoubles(cPtr, false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_QuadraticForm"]/*' />
  public static double QuadraticForm(double[] y, Util.MatrixOfDoubles A, double[] x, bool treatAasSymmetric) {
    double ret = BaseEntityPINVOKE.LinearAlgebra_QuadraticForm(y, Util.MatrixOfDoubles.getCPtr(A), x, treatAasSymmetric);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Cholesky__SWIG_0"]/*' />
  public static void Cholesky(double[,] A) {
    BaseEntityPINVOKE.LinearAlgebra_Cholesky__SWIG_0(A);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Cholesky__SWIG_1"]/*' />
  public static void Cholesky(int n, double[] A) {
    BaseEntityPINVOKE.LinearAlgebra_Cholesky__SWIG_1(n, A);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_SymmetricEVD__SWIG_0"]/*' />
  public static void SymmetricEVD(double[,] A, double[] e, double epsilon) {
    BaseEntityPINVOKE.LinearAlgebra_SymmetricEVD__SWIG_0(A, e, epsilon);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_SymmetricEVD__SWIG_1"]/*' />
  public static void SymmetricEVD(double[,] A, double[] e, double epsilon, bool descendOrder) {
    BaseEntityPINVOKE.LinearAlgebra_SymmetricEVD__SWIG_1(A, e, epsilon, descendOrder);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_SymmetricEVD__SWIG_2"]/*' />
  public static void SymmetricEVD(int n, double[] A, double[] e, double epsilon) {
    BaseEntityPINVOKE.LinearAlgebra_SymmetricEVD__SWIG_2(n, A, e, epsilon);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_SymmetricEVD__SWIG_3"]/*' />
  public static void SymmetricEVD(int n, double[] A, double[] e, double epsilon, bool descendOrder) {
    BaseEntityPINVOKE.LinearAlgebra_SymmetricEVD__SWIG_3(n, A, e, epsilon, descendOrder);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_Cholesky__SWIG_2"]/*' />
  public static Util.MatrixOfDoubles Cholesky(Util.MatrixOfDoubles A) {
    Util.MatrixOfDoubles ret = new Util.MatrixOfDoubles(BaseEntityPINVOKE.LinearAlgebra_Cholesky__SWIG_2(Util.MatrixOfDoubles.getCPtr(A)), false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_SymmetricEVD__SWIG_4"]/*' />
  public static Util.MatrixOfDoubles SymmetricEVD(Util.MatrixOfDoubles A, double[] e, double epsilon) {
    Util.MatrixOfDoubles ret = new Util.MatrixOfDoubles(BaseEntityPINVOKE.LinearAlgebra_SymmetricEVD__SWIG_4(Util.MatrixOfDoubles.getCPtr(A), e, epsilon), false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:LinearAlgebra_SymmetricEVD__SWIG_5"]/*' />
  public static Util.MatrixOfDoubles SymmetricEVD(Util.MatrixOfDoubles A, double[] e, double epsilon, bool descendOrder) {
    Util.MatrixOfDoubles ret = new Util.MatrixOfDoubles(BaseEntityPINVOKE.LinearAlgebra_SymmetricEVD__SWIG_5(Util.MatrixOfDoubles.getCPtr(A), e, epsilon, descendOrder), false);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/LinearAlgebra.xml' path='doc/members/member[@name="M:new_LinearAlgebra"]/*' />
  public LinearAlgebra() : this(BaseEntityPINVOKE.new_LinearAlgebra(), true) {
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
