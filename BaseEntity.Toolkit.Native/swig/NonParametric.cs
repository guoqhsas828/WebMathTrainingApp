/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/NonParametric.xml' path='doc/members/member[@name="T:NonParametric"]/*' />
public static partial class NonParametric {
  /// <include file='swig/NonParametric.xml' path='doc/members/member[@name="M:NonParametric_P"]/*' />
  public static double P(double a, double b, double c, double d, double t) {
    double ret = BaseEntityPINVOKE.NonParametric_P(a, b, c, d, t);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
