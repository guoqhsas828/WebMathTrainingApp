/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/CorrelatedRecoveryBasketModel.xml' path='doc/members/member[@name="T:CorrelatedRecoveryBasketModel"]/*' />
public static partial class CorrelatedRecoveryBasketModel {
  /// <include file='swig/CorrelatedRecoveryBasketModel.xml' path='doc/members/member[@name="M:CorrelatedRecoveryBasketModel_ComputeDistributions"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, double[] maturities, Toolkit.Native.INativeCurve[] survivalCurves, double[] principals, double[] recoveryRates, double[] recoveryDispersions, double[] recoveryCorrelations, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.CorrelatedRecoveryBasketModel_ComputeDistributions(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, maturities, survivalCurves, principals, recoveryRates, recoveryDispersions, recoveryCorrelations, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
