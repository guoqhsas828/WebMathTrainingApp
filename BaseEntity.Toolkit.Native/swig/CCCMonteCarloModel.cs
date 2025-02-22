/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/CCCMonteCarloModel.xml' path='doc/members/member[@name="T:CCCMonteCarloModel"]/*' />
public static partial class CCCMonteCarloModel {
  /// <include file='swig/CCCMonteCarloModel.xml' path='doc/members/member[@name="M:CCCMonteCarloModel_ComputePvs"]/*' />
  public static void ComputePvs(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt effective, BaseEntity.Toolkit.Base.Dt firstPayDt, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Frequency freq, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, double spread, BaseEntity.Toolkit.Base.DayCount rateDc, double currentRate, int floatCurrency, double coupon, int fixedCurrency, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL, int nRuns, double[] result) {
    BaseEntityPINVOKE.CCCMonteCarloModel_ComputePvs(settle, effective, firstPayDt, maturity, (int)freq, cal, (int)roll, (int)dc, spread, (int)rateDc, currentRate, floatCurrency, coupon, fixedCurrency, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL, nRuns, result);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
