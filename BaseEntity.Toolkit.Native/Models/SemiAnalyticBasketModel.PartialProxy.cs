
namespace BaseEntity.Toolkit.Models
{
  partial class SemiAnalyticBasketModel
  {
    /// <summary>
    /// computer distributions
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon">common market factor</param>
    /// <param name="dfIdiosyncratic">Idiosyncratic factor</param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="integrationPointsFirst"></param>
    /// <param name="integrationPointsSecond"></param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="refinanceCurves">refinance curves</param>
    /// <param name="qcrModel">qcr model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>
    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int integrationPointsFirst, int integrationPointsSecond,
      Toolkit.Native.INativeCurve[] survivalCurves, double[] principals,
      double[] recoveryRates, double[] recoveryDispersions,
      Toolkit.Native.INativeCurve[] refinanceCurves,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_0(
        wantProbability, startDateIndex, stopDateIndex, (int) copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst,
        integrationPointsSecond, survivalCurves, principals, recoveryRates,
        recoveryDispersions, refinanceCurves, 
        new RecoveryCorrelationModel(qcrModel), gridSize,
        Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }

    /// <summary>
    /// computer probability distribution.
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon"></param>
    /// <param name="dfIdiosyncratic"></param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="integrationPointsFirst"></param>
    /// <param name="integrationPointsSecond"></param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="refinanceCurves">refinance curves</param>
    /// <param name="qcrModel">qcr model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>
    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int integrationPointsFirst, int integrationPointsSecond, double[] maturities,
      Toolkit.Native.INativeCurve[] survivalCurves, double[] principals,
      double[] recoveryRates, double[] recoveryDispersions,
      Toolkit.Native.INativeCurve[] refinanceCurves,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_1(
        wantProbability, startDateIndex, stopDateIndex, (int) copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst,
        integrationPointsSecond, maturities, survivalCurves, principals, recoveryRates,
        recoveryDispersions, refinanceCurves,
        new RecoveryCorrelationModel(qcrModel), gridSize,
        Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }

    /// <summary>
    /// Compute distribution
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon">common market factor</param>
    /// <param name="dfIdiosyncratic">Idiosyncratic factor</param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="integrationPointsFirst">First integration point</param>
    /// <param name="integrationPointsSecond">Second integration point</param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="survivalCurvesAlt">survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="qcrModel">qcr model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>
    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int integrationPointsFirst, int integrationPointsSecond,
      Toolkit.Native.INativeCurve[] survivalCurves,
      Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals,
      double[] recoveryRates, double[] recoveryDispersions,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_2(
        wantProbability, startDateIndex, stopDateIndex, (int) copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst,
        integrationPointsSecond, survivalCurves, survivalCurvesAlt, principals,
        recoveryRates, recoveryDispersions,
        new RecoveryCorrelationModel(qcrModel), gridSize,
        Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }

    /// <summary>
    /// Compute distribution
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon">common market factor</param>
    /// <param name="dfIdiosyncratic">Idiosyncratic factor</param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="integrationPointsFirst">First integration point</param>
    /// <param name="integrationPointsSecond">Second integration point</param>
    /// <param name="maturities">maturities</param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="survivalCurvesAlt">survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="qcrModel">qcr model. Recovery correlation model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>
    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int integrationPointsFirst, int integrationPointsSecond, double[] maturities,
      Toolkit.Native.INativeCurve[] survivalCurves,
      Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals,
      double[] recoveryRates, double[] recoveryDispersions,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_3(
        wantProbability, startDateIndex, stopDateIndex, (int) copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst,
        integrationPointsSecond, maturities, survivalCurves, survivalCurvesAlt, principals,
        recoveryRates, recoveryDispersions,
        new RecoveryCorrelationModel(qcrModel), gridSize,
        Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }

    /// <summary>
    /// Compute distribution
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon">common market factor</param>
    /// <param name="dfIdiosyncratic">Idiosyncratic factor</param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="integrationPointsFirst">First integration point</param>
    /// <param name="integrationPointsSecond">Second integration point</param>
    /// <param name="maturities">maturities</param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="survivalCurvesAlt">alternative survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryRatesAlt">alternative recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="qcrModel">qcr model. Recovery correlation model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>
    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int integrationPointsFirst, int integrationPointsSecond, double[] maturities,
      Toolkit.Native.INativeCurve[] survivalCurves,
      Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals,
      double[] recoveryRates, double[] recoveryRatesAlt, double[] recoveryDispersions,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel_ComputeDistributions__SWIG_5(
        wantProbability, startDateIndex, stopDateIndex, (int) copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, integrationPointsFirst,
        integrationPointsSecond, maturities, survivalCurves, survivalCurvesAlt, principals,
        recoveryRates, recoveryRatesAlt, recoveryDispersions,
        new RecoveryCorrelationModel(qcrModel), gridSize,
        Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }
  }

  partial class SemiAnalyticBasketModel2
  {
    /// <summary>
    /// Compute distribution
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon">common market factor</param>
    /// <param name="dfIdiosyncratic">Idiosyncratic factor</param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="quadraturePoints">quadrature points</param>
    /// <param name="maturities">maturities</param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="refinanceCurves">refinance curve</param>
    /// <param name="qcrModel">qcr model. Recovery correlation model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>

    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int quadraturePoints, double[] maturities,
      Toolkit.Native.INativeCurve[] survivalCurves, double[] principals,
      double[] recoveryRates, double[] recoveryDispersions,
      Toolkit.Native.INativeCurve[] refinanceCurves,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel2_ComputeDistributions__SWIG_0(
        wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, quadraturePoints, maturities,
        survivalCurves, principals, recoveryRates, recoveryDispersions, refinanceCurves,
        new SemiAnalyticBasketModel.RecoveryCorrelationModel(qcrModel), gridSize,
        Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }

    /// <summary>
    /// Compute distribution
    /// </summary>
    /// <param name="wantProbability">bool type</param>
    /// <param name="startDateIndex">start date index</param>
    /// <param name="stopDateIndex">stop date index</param>
    /// <param name="copulaType">copula type</param>
    /// <param name="dfCommon">common market factor</param>
    /// <param name="dfIdiosyncratic">Idiosyncratic factor</param>
    /// <param name="copulaParams">copula paramters</param>
    /// <param name="corrData">correlation data</param>
    /// <param name="corrDates">correlation dates</param>
    /// <param name="quadraturePoints">quadrature points</param>
    /// <param name="maturities">maturities</param>
    /// <param name="survivalCurves">survival curves</param>
    /// <param name="survivalCurvesAlt">alternative survival curves</param>
    /// <param name="principals">principals</param>
    /// <param name="recoveryRates">recovery rates</param>
    /// <param name="recoveryRatesAlt">alternative recovery rates</param>
    /// <param name="recoveryDispersions">recovery dispersions</param>
    /// <param name="qcrModel">qcr model. Recovery correlation model</param>
    /// <param name="gridSize">grid size</param>
    /// <param name="lossDistributions">loss distribution</param>
    /// <param name="amorDistributions">amortization distribution</param>
    public static void ComputeDistributions(bool wantProbability, int startDateIndex,
      int stopDateIndex, BaseEntity.Toolkit.Base.CopulaType copulaType, int dfCommon,
      int dfIdiosyncratic, double[] copulaParams, double[] corrData, int[] corrDates,
      int quadraturePoints, double[] maturities,
      Toolkit.Native.INativeCurve[] survivalCurves,
      Toolkit.Native.INativeCurve[] survivalCurvesAlt, double[] principals,
      double[] recoveryRates, double[] recoveryRatesAlt, double[] recoveryDispersions,
      int qcrModel,
      double gridSize, Curves.Curve2D lossDistributions,
      Curves.Curve2D amorDistributions)
    {
      BaseEntityPINVOKE.SemiAnalyticBasketModel2_ComputeDistributions__SWIG_1(
        wantProbability, startDateIndex, stopDateIndex, (int)copulaType, dfCommon,
        dfIdiosyncratic, copulaParams, corrData, corrDates, quadraturePoints, maturities,
        survivalCurves, survivalCurvesAlt, principals, recoveryRates, recoveryRatesAlt,
        recoveryDispersions, new SemiAnalyticBasketModel.RecoveryCorrelationModel(qcrModel),
        gridSize, Curves.Curve2D.getCPtr(lossDistributions),
        Curves.Curve2D.getCPtr(amorDistributions));
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
    }
  }
}
