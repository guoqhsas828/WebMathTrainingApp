/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/QuantoContingentCreditMC.xml' path='doc/members/member[@name="T:QuantoContingentCreditMC"]/*' />
public static partial class QuantoContingentCreditMC {
  /// <include file='swig/QuantoContingentCreditMC.xml' path='doc/members/member[@name="M:QuantoContingentCreditMC_FxOption"]/*' />
  public static double FxOption(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, BaseEntity.Toolkit.Base.OptionType type, double strike, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.QuantoContingentCreditMC_FxOption(settle, maturity, cal, (int)roll, (int)dc, stepSize, (int)stepUnit, (int)type, strike, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  /// <include file='swig/QuantoContingentCreditMC.xml' path='doc/members/member[@name="M:QuantoContingentCreditMC_Caplet"]/*' />
  public static double Caplet(BaseEntity.Toolkit.Base.Dt settle, BaseEntity.Toolkit.Base.Dt effective, BaseEntity.Toolkit.Base.Dt firstPayDt, BaseEntity.Toolkit.Base.Dt maturity, BaseEntity.Toolkit.Base.Frequency freq, BaseEntity.Toolkit.Base.Calendar cal, BaseEntity.Toolkit.Base.BDConvention roll, BaseEntity.Toolkit.Base.DayCount dc, int paymentIndex, int currency, double cappedRate, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, double r10, double r20, double fx0, double lambda0, double alpha, double beta, double rho12, double rho13, double rho14, double rho23, double rho24, double rho34, double kappaR1, Curves.Native.Curve thetaR1, Curves.Native.Curve sigmaR1, double kappaR2, Curves.Native.Curve thetaR2, Curves.Native.Curve sigmaR2, Curves.Native.Curve sigmaFx, double kappaL, double thetaL, double sigmaL) {
    double ret = BaseEntityPINVOKE.QuantoContingentCreditMC_Caplet(settle, effective, firstPayDt, maturity, (int)freq, cal, (int)roll, (int)dc, paymentIndex, currency, cappedRate, stepSize, (int)stepUnit, r10, r20, fx0, lambda0, alpha, beta, rho12, rho13, rho14, rho23, rho24, rho34, kappaR1, Curves.Native.Curve.getCPtr(thetaR1), Curves.Native.Curve.getCPtr(sigmaR1), kappaR2, Curves.Native.Curve.getCPtr(thetaR2), Curves.Native.Curve.getCPtr(sigmaR2), Curves.Native.Curve.getCPtr(sigmaFx), kappaL, thetaL, sigmaL);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
}
