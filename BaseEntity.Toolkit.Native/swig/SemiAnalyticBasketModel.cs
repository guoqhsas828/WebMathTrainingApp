/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 1.3.26
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */


using System;

namespace BaseEntity.Toolkit.Models {


/// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="T:SemiAnalyticBasketModel"]/*' />
public static partial class SemiAnalyticBasketModel {
      /// <exclude>For public use only.</exclude>
      [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
      [Serializable]
      public struct RecoveryCorrelationModel
      {
        public int ModelChoice;
        public double MaxRecovery, MinRecovery;
        public RecoveryCorrelationModel(int model,
          double maxRecovery = 1, double minRecovery = 0)
        {
          ModelChoice = model;
          MaxRecovery = maxRecovery;
          MinRecovery = minRecovery;
        }
      }
    
  /// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="M:SemiAnalyticBasketModel_ComputeDistributions__SWIG_0"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, Toolkit.Native.INativeCurve[] survivalCurves, double[] principals, double[] recoveryRates, double[] recoveryDispersions, Toolkit.Native.INativeCurve[] refinanceCurves, SemiAnalyticBasketModel.RecoveryCorrelationModel qcrModel, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_0(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, survivalCurves, principals, recoveryRates, recoveryDispersions, refinanceCurves, qcrModel, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="M:SemiAnalyticBasketModel_ComputeDistributions__SWIG_1"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, double[] maturities, Toolkit.Native.INativeCurve[] survivalCurves, double[] principals, double[] recoveryRates, double[] recoveryDispersions, Toolkit.Native.INativeCurve[] refinanceCurves, SemiAnalyticBasketModel.RecoveryCorrelationModel qcrModel, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_1(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, maturities, survivalCurves, principals, recoveryRates, recoveryDispersions, refinanceCurves, qcrModel, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="M:SemiAnalyticBasketModel_ComputeDistributions__SWIG_2"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, Toolkit.Native.INativeCurve[] survivalCurves, Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals, double[] recoveryRates, double[] recoveryDispersions, SemiAnalyticBasketModel.RecoveryCorrelationModel qcrModel, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_2(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, survivalCurves, survivalCurvesAlt, principals, recoveryRates, recoveryDispersions, qcrModel, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="M:SemiAnalyticBasketModel_ComputeDistributions__SWIG_3"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, double[] maturities, Toolkit.Native.INativeCurve[] survivalCurves, Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals, double[] recoveryRates, double[] recoveryDispersions, SemiAnalyticBasketModel.RecoveryCorrelationModel qcrModel, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_3(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, maturities, survivalCurves, survivalCurvesAlt, principals, recoveryRates, recoveryDispersions, qcrModel, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="M:SemiAnalyticBasketModel_ComputeDistributions__SWIG_4"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, Toolkit.Native.INativeCurve[] survivalCurves, double[] principals, double[] recoveryRates, double[] recoveryDispersions, Curves.Native.Curve counterpartyCurve, double counterpartyFactor, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_4(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, survivalCurves, principals, recoveryRates, recoveryDispersions, Curves.Native.Curve.getCPtr(counterpartyCurve), counterpartyFactor, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

  /// <include file='swig/SemiAnalyticBasketModel.xml' path='doc/members/member[@name="M:SemiAnalyticBasketModel_ComputeDistributions__SWIG_5"]/*' />
  public static void ComputeDistributions(bool wantProbability, int startDateIndex, int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon, int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates, int integrationPointsFirst, int integrationPointsSecond, double[] maturities, Toolkit.Native.INativeCurve[] survivalCurves, Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals, double[] recoveryRates, double[] recoveryRatesAlt, double[] recoveryDispersions, SemiAnalyticBasketModel.RecoveryCorrelationModel qcrModel, double gridSize, Curves.Curve2D lossDistributions, Curves.Curve2D amorDistributions) {
    BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_5(wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon, dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst, integrationPointsSecond, maturities, survivalCurves, survivalCurvesAlt, principals, recoveryRates, recoveryRatesAlt, recoveryDispersions, qcrModel, gridSize, Curves.Curve2D.getCPtr(lossDistributions), Curves.Curve2D.getCPtr(amorDistributions));
    if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
  }

}
}
