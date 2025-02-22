/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models.BGM.Native {


public static partial class BgmBinomialTree {
      public const int LogNormal = 0, Normal = 1, NormalToLogNormal = 2,
        AmericanOption = 16;
    
  /// <include file='swig/BgmBinomialTree.xml' path='doc/members/member[@name="M:BgmBinomialTree_calculateRateSystem"]/*' />
  public static void calculateRateSystem(double stepSize, double tolerance, double[] rates, double[] fractions, double[] resetTimes, Toolkit.Native.INativeCurve[] volatilities, double[] dates, RateSystem distributions) {
    BaseEntityPINVOKE.BgmBinomialTree_calculateRateSystem(stepSize, tolerance, rates, fractions, resetTimes, volatilities, dates, RateSystem.getCPtr(distributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/BgmBinomialTree.xml' path='doc/members/member[@name="M:BgmBinomialTree_calibrateCoTerminalSwaptions"]/*' />
  public static void calibrateCoTerminalSwaptions(BaseEntity.Toolkit.Base.Dt asOf, BaseEntity.Toolkit.Base.Dt maturity, SwaptionInfo[] rates, double tolerance, int flags, RateSystem distributions) {
    BaseEntityPINVOKE.BgmBinomialTree_calibrateCoTerminalSwaptions(asOf, maturity, rates, tolerance, flags, RateSystem.getCPtr(distributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/BgmBinomialTree.xml' path='doc/members/member[@name="M:BgmBinomialTree_evaluateTwoFactorCallable"]/*' />
  public static void evaluateTwoFactorCallable(BaseEntity.Toolkit.Base.Dt asOf, BaseEntity.Toolkit.Base.Dt maturity, SwaptionInfo[] swapRates, double tolerance, Curves.Native.Curve survivalCurve, SwaptionInfo[] cdsSwapRates, double correlation, int distribution, RateSystem distributions) {
    BaseEntityPINVOKE.BgmBinomialTree_evaluateTwoFactorCallable(asOf, maturity, swapRates, tolerance, Curves.Native.Curve.getCPtr(survivalCurve), cdsSwapRates, correlation, distribution, RateSystem.getCPtr(distributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
