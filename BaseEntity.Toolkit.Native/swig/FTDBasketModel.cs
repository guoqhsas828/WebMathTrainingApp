/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/FTDBasketModel.xml' path='doc/members/member[@name="T:FTDBasketModel"]/*' />
public static partial class FTDBasketModel {
  /// <include file='swig/FTDBasketModel.xml' path='doc/members/member[@name="M:FTDBasketModel_ComputeDistributions"]/*' />
  public static void ComputeDistributions(bool wantProbability, BaseEntity.Toolkit.Base.Dt asOf, BaseEntity.Toolkit.Base.Dt maturity, int stepSize, BaseEntity.Toolkit.Base.TimeUnit stepUnit, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] corrData, int integrationPointsFirst, int integrationPointsSecond, Toolkit.Native.INativeCurve[] survCurves, int[] ftdIndices, int[] defaultStarts, int[] numDefaults, double[] principals, double[] means, double[] levels, double gridSize, Curves.Curve2D lossDistributions) {
    BaseEntityPINVOKE.FTDBasketModel_ComputeDistributions(wantProbability, asOf, maturity, stepSize, (int)stepUnit, (int)copulaType, dfCommon, dfIdiosyncratic, corrData, integrationPointsFirst, integrationPointsSecond, survCurves, ftdIndices, defaultStarts, numDefaults, principals, means, levels, gridSize, Curves.Curve2D.getCPtr(lossDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
