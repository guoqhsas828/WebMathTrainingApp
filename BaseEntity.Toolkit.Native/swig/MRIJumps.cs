/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/MRIJumps.xml' path='doc/members/member[@name="T:MRIJumps"]/*' />
public static partial class MRIJumps {
  /// <include file='swig/MRIJumps.xml' path='doc/members/member[@name="M:MRIJumps_P"]/*' />
  public static double P(double T, double lambda0, double reversionRate, double lambdaT, double jumpIntensity, double jumpSize) {
    double ret = BaseEntityPINVOKE.MRIJumps_P(T, lambda0, reversionRate, lambdaT, jumpIntensity, jumpSize);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
