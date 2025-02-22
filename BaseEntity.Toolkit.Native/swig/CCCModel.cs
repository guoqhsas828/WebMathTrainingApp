/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/CCCModel.xml' path='doc/members/member[@name="T:CCCModel"]/*' />
public static partial class CCCModel {
  /// <include file='swig/CCCModel.xml' path='doc/members/member[@name="M:CCCModel_FixedPv"]/*' />
  public static double FixedPv(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt effective, BaseEntity.Toolkit.Base.Dt firstPayDt, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Frequency freq, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, double coupon, int currency, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.CCCModel_FixedPv(settle, effective, firstPayDt, maturity, (int)freq, cal, (int)roll, (int)dc, coupon, currency, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/CCCModel.xml' path='doc/members/member[@name="M:CCCModel_FloatingPv"]/*' />
  public static double FloatingPv(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt effective, BaseEntity.Toolkit.Base.Dt firstPayDt, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Frequency freq, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, double spread, BaseEntity.Toolkit.Base.DayCount rateDc, double currentRate, int currency, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.CCCModel_FloatingPv(settle, effective, firstPayDt, maturity, (int)freq, cal, (int)roll, (int)dc, spread, (int)rateDc, currentRate, currency, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/CCCModel.xml' path='doc/members/member[@name="M:CCCModel_FxOption"]/*' />
  public static double FxOption(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, BaseEntity.Toolkit.Base.OptionType type, double strike, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.CCCModel_FxOption(settle, maturity, cal, (int)roll, (int)dc, (int)type, strike, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/CCCModel.xml' path='doc/members/member[@name="M:CCCModel_Df"]/*' />
  public static double Df(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt date, int currency, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.CCCModel_Df(settle, date, currency, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/CCCModel.xml' path='doc/members/member[@name="M:CCCModel_Caplet"]/*' />
  public static double Caplet(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt effective, BaseEntity.Toolkit.Base.Dt firstPayDt, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Frequency freq, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, int paymentIndex, double cappedRate, int currency, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.CCCModel_Caplet(settle, effective, firstPayDt, maturity, (int)freq, cal, (int)roll, (int)dc, paymentIndex, cappedRate, currency, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
