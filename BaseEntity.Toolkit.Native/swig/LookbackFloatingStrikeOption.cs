/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/LookbackFloatingStrikeOption.xml' path='doc/members/member[@name="T:LookbackFloatingStrikeOption"]/*' />
public static partial class LookbackFloatingStrikeOption {
  /// <include file='swig/LookbackFloatingStrikeOption.xml' path='doc/members/member[@name="M:LookbackFloatingStrikeOption_P"]/*' />
  public static double P(BaseEntity.Toolkit.Base.OptionType type, double T, double S, double Sminmax, double r, double d, double v) {
    double ret = BaseEntityPINVOKE.LookbackFloatingStrikeOption_P((int)type, T, S, Sminmax, r, d, v);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
