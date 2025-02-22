/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models.BGM {


/// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="T:SabrCalibrations"]/*' />
public static partial class SabrCalibrations {
  /// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="M:SabrCalibrations_SabrGuessValues"]/*' />
  public static void SabrGuessValues(double betaS, double betaR, double nu0R, double F, double L, double T, double atmR, double atmDerR, double atmS, double atmDerS, double[] guessVals) {
    BaseEntityPINVOKE.SabrCalibrations_SabrGuessValues(betaS, betaR, nu0R, F, L, T, atmR, atmDerR, atmS, atmDerS, guessVals);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="M:SabrCalibrations_SabrCalibrate3Params"]/*' />
  public static void SabrCalibrate3Params(double[] vols, double[] strikes, double F, double T, double beta, double[] guess, double[] lb, double[] ub, double[] pars) {
    BaseEntityPINVOKE.SabrCalibrations_SabrCalibrate3Params(vols, strikes, F, T, beta, guess, lb, ub, pars);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="M:SabrCalibrations_SabrCalibrate2Params"]/*' />
  public static void SabrCalibrate2Params(double[] vols, double[] strikes, double F, double T, double beta, double nu, double[] guess, double[] lb, double[] ub, double[] pars) {
    BaseEntityPINVOKE.SabrCalibrations_SabrCalibrate2Params(vols, strikes, F, T, beta, nu, guess, lb, ub, pars);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="M:SabrCalibrations_SabrCombinedCalibrate"]/*' />
  public static void SabrCombinedCalibrate(double[] rateVols, double[] rateStrikes, double rateF, double rateBeta, double[] stockVols, double[] stockStrikes, double stockF, double stockBeta, double T, double[] guess, double[] pars) {
    BaseEntityPINVOKE.SabrCalibrations_SabrCombinedCalibrate(rateVols, rateStrikes, rateF, rateBeta, stockVols, stockStrikes, stockF, stockBeta, T, guess, pars);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="M:SabrCalibrations_Calibrate__SWIG_0"]/*' />
  public static void Calibrate(double time, double[] moneyness, double[] volatilities, double[] lowerBounds, double[] upperBounds, double[] pars) {
    BaseEntityPINVOKE.SabrCalibrations_Calibrate__SWIG_0(time, moneyness, volatilities, lowerBounds, upperBounds, pars);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SabrCalibrations.xml' path='doc/members/member[@name="M:SabrCalibrations_Calibrate__SWIG_1"]/*' />
  public static void Calibrate(double time, double[] moneyness, double[] volatilities, double[] pars) {
    BaseEntityPINVOKE.SabrCalibrations_Calibrate__SWIG_1(time, moneyness, volatilities, pars);
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
