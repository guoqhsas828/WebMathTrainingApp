/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/FDModel.xml' path='doc/members/member[@name="T:FDModel"]/*' />
public static partial class FDModel {
  /// <include file='swig/FDModel.xml' path='doc/members/member[@name="M:FDModel_P"]/*' />
  public static double P(BaseEntity.Toolkit.Base.OptionStyle style, BaseEntity.Toolkit.Base.OptionType type, double T, int notice, double S, double K, double r, double d, double v, int nodes, BaseEntity.Toolkit.Base.DividendSchedule divs, ref double delta, ref double gamma, ref double theta) {
    double ret = BaseEntityPINVOKE.FDModel_P((int)style, (int)type, T, notice, S, K, r, d, v, nodes, BaseEntity.Toolkit.Base.DividendSchedule.getCPtr(divs), ref delta, ref gamma, ref theta);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
