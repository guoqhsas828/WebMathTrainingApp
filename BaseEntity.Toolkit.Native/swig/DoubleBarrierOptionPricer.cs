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

namespace BaseEntity.Toolkit.Models.BGM.Native {


  /// <include file='swig/DoubleBarrierOptionPricer.xml' path='doc/members/member[@name="T:DoubleBarrierOptionPricer"]/*' />
  public abstract partial class DoubleBarrierOptionPricer {
  /// <include file='swig/DoubleBarrierOptionPricer.xml' path='doc/members/member[@name="M:DoubleBarrierOptionPricer_P"]/*' />
  public static double P(BaseEntity.Toolkit.Base.OptionType optionType, double S, double K, BaseEntity.Toolkit.Base.OptionBarrierType lowerBarrierType, double L, BaseEntity.Toolkit.Base.OptionBarrierType upperBarrierType, double U, BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt maturity, Curves.Native.Curve volCurve, Curves.Native.Curve rdCurve, Curves.Native.Curve rfCurve, Curves.Native.Curve basisCurve, Curves.Native.Curve fxCurve, int flags) {
    double ret = BaseEntityPINVOKE.DoubleBarrierOptionPricer_P((int)optionType, S, K, (int)lowerBarrierType, L, (int)upperBarrierType, U, settle, maturity, Curves.Native.Curve.getCPtr(volCurve), Curves.Native.Curve.getCPtr(rdCurve), Curves.Native.Curve.getCPtr(rfCurve), Curves.Native.Curve.getCPtr(basisCurve), Curves.Native.Curve.getCPtr(fxCurve), flags);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/DoubleBarrierOptionPricer.xml' path='doc/members/member[@name="M:DoubleBarrierOptionPricer_Price"]/*' />
  public static double Price(BaseEntity.Toolkit.Base.OptionType optionType, double S, double K, BaseEntity.Toolkit.Base.OptionBarrierType lowerBarrierType, double L, BaseEntity.Toolkit.Base.OptionBarrierType upperBarrierType, double U, double T, double rd, double rf, double sigma, int flags) {
    double ret = BaseEntityPINVOKE.DoubleBarrierOptionPricer_Price((int)optionType, S, K, (int)lowerBarrierType, L, (int)upperBarrierType, U, T, rd, rf, sigma, flags);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
